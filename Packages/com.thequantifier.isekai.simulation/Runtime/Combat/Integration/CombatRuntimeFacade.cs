using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.Contributions;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.Combat.Reactions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Combat.Integration
{
    public sealed class CombatRuntimeFacade
    {
        private readonly DefinitionRegistry registry;
        private readonly GameObject defaultActorObject;
        private CombatRuntimeReadinessState state = CombatRuntimeReadinessState.Uninitialized;
        private CombatTransactionTraceSnapshot lastTransactionTrace = new CombatTransactionTraceSnapshot(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null);

        public CombatRuntimeFacade(
            DefinitionRegistry registry,
            GameObject defaultActorObject,
            IDamageHealingService damageHealingService,
            DefensiveActionService defensiveActionService,
            CombatStateService combatStateService,
            CombatExecutionService combatExecutionService,
            OngoingEffectService ongoingEffectService,
            CombatReactionService combatReactionService,
            CombatContributionService combatContributionService,
            AttackResolutionService attackResolutionService = null)
        {
            this.registry = registry;
            this.defaultActorObject = defaultActorObject;
            DamageHealing = damageHealingService ?? new DamageHealingService();
            Defense = defensiveActionService ?? new DefensiveActionService();
            CombatState = combatStateService;
            Execution = combatExecutionService ?? new CombatExecutionService();
            OngoingEffects = ongoingEffectService;
            Reactions = combatReactionService;
            Contributions = combatContributionService;
            AttackResolution = attackResolutionService ?? new AttackResolutionService(DamageHealing, Defense, CombatState);
            OngoingEffects?.ConfigureDamageHealing(DamageHealing);
            Reactions?.Configure(OngoingEffects, DamageHealing);
            state = CombatRuntimeReadinessState.ResolvingDependencies;
        }

        public IDamageHealingService DamageHealing { get; }
        public AttackResolutionService AttackResolution { get; }
        public DefensiveActionService Defense { get; }
        public CombatStateService CombatState { get; }
        public CombatExecutionService Execution { get; }
        public OngoingEffectService OngoingEffects { get; }
        public CombatReactionService Reactions { get; }
        public CombatContributionService Contributions { get; }
        public CombatTransactionTraceSnapshot LastTransactionTrace => lastTransactionTrace;

        public CombatReadinessResult EvaluateReadiness(GameObject actorObject = null)
        {
            GameObject actor = actorObject == null ? defaultActorObject : actorObject;
            List<CombatRuntimeDiagnostic> diagnostics = new List<CombatRuntimeDiagnostic>();
            if (state == CombatRuntimeReadinessState.Disposed)
            {
                diagnostics.Add(Error(CombatSubsystem.Facade, "Disposed", "Combat runtime facade is disposed."));
                return new CombatReadinessResult(CombatRuntimeReadinessState.Disposed, diagnostics);
            }

            AddMissing(DamageHealing == null, CombatSubsystem.DamageHealing, "MissingDamageHealingService", "DamageHealingService is required.", diagnostics);
            AddMissing(AttackResolution == null, CombatSubsystem.AttackResolution, "MissingAttackResolutionService", "AttackResolutionService is required.", diagnostics);
            AddMissing(Defense == null, CombatSubsystem.Defense, "MissingDefensiveActionService", "DefensiveActionService is required.", diagnostics);
            AddMissing(Execution == null, CombatSubsystem.Execution, "MissingCombatExecutionService", "CombatExecutionService is required.", diagnostics);
            AddMissing(CombatState == null, CombatSubsystem.CombatState, "MissingCombatStateService", "CombatStateService is required.", diagnostics);
            AddMissing(OngoingEffects == null, CombatSubsystem.OngoingEffects, "MissingOngoingEffectService", "OngoingEffectService is required for Step 6 integration.", diagnostics);
            AddMissing(Reactions == null, CombatSubsystem.Reactions, "MissingCombatReactionService", "CombatReactionService is required for Step 6 integration.", diagnostics);
            AddMissing(Contributions == null, CombatSubsystem.Contributions, "MissingCombatContributionService", "CombatContributionService is required for Step 6 integration.", diagnostics);
            AddMissing(registry == null, CombatSubsystem.Facade, "MissingDefinitionRegistry", "Definition registry is required for canonical combat content validation.", diagnostics);

            if (actor == null)
            {
                diagnostics.Add(Warning(CombatSubsystem.Character, "MissingActorObject", "No actor object was supplied; service-level facade queries remain available."));
            }
            else
            {
                CharacterSystemCoordinator character = actor.GetComponentInParent<CharacterSystemCoordinator>();
                CharacterResourceCollection resources = ResolveResources(actor, character);
                ActorLifecycleController lifecycle = actor.GetComponentInParent<ActorLifecycleController>();
                string actorId = ResolveActorId(actor, character);
                if (string.IsNullOrWhiteSpace(actorId))
                {
                    diagnostics.Add(Error(CombatSubsystem.Character, "MissingActorIdentity", "Actor/body identity is missing."));
                }

                if (character == null)
                {
                    diagnostics.Add(Warning(CombatSubsystem.Character, "MissingCharacterCoordinator", "CharacterSystemCoordinator is missing; combat can still use lower-level actor components."));
                }
                else if (!character.IsReady)
                {
                    diagnostics.Add(Warning(CombatSubsystem.Character, "CharacterNotReady", $"Character runtime is {character.Readiness}: {character.LastFailureReason}", actorId));
                }

                if (resources == null)
                {
                    diagnostics.Add(Error(CombatSubsystem.Resources, "MissingResources", "CharacterResourceCollection is required for combat mutations.", actorId));
                }
                else
                {
                    AddMissing(!resources.IsConfigured, CombatSubsystem.Resources, "ResourcesNotConfigured", "Current Resources are not configured.", diagnostics, actorId);
                    AddMissing(!resources.HasResource(ResourceIds.Health), CombatSubsystem.Resources, "MissingHealth", "Health resource is required.", diagnostics, actorId);
                    AddMissing(!resources.HasResource(ResourceIds.Stamina), CombatSubsystem.Resources, "MissingStamina", "Stamina resource is expected for costs and defense.", diagnostics, actorId);
                    AddMissing(!resources.HasResource(ResourceIds.Mana), CombatSubsystem.Resources, "MissingMana", "Mana resource is expected for spell execution.", diagnostics, actorId);
                }

                if (lifecycle == null)
                {
                    diagnostics.Add(Error(CombatSubsystem.Lifecycle, "MissingLifecycle", "ActorLifecycleController is required for Step 6 lifecycle integration.", actorId));
                }
            }

            CombatRuntimeReadinessState resultState = diagnostics.Any(diagnostic => diagnostic.Severity == CombatIntegritySeverity.Error)
                ? CombatRuntimeReadinessState.Invalid
                : CombatRuntimeReadinessState.Ready;
            state = resultState;
            return new CombatReadinessResult(resultState, diagnostics);
        }

        public CombatRuntimeSnapshot CreateSnapshot(GameObject actorObject = null)
        {
            GameObject actor = actorObject == null ? defaultActorObject : actorObject;
            CombatReadinessResult readiness = EvaluateReadiness(actor);
            CharacterSystemCoordinator character = actor == null ? null : actor.GetComponentInParent<CharacterSystemCoordinator>();
            CharacterResourceCollection resources = ResolveResources(actor, character);
            ActorLifecycleController lifecycle = actor == null ? null : actor.GetComponentInParent<ActorLifecycleController>();
            CalculatedStatCollection stats = character == null ? actor == null ? null : actor.GetComponentInParent<CalculatedStatCollection>() : character.CalculatedStats;
            string actorId = ResolveActorId(actor, character);
            string bodyId = ResolveBodyId(actor);
            string personId = character == null ? string.Empty : character.PersonId;

            Defense.TryGetActiveDefense(actorId, out DefensiveActionStateSnapshot activeDefense);
            CombatExecutionStateSnapshot activeExecution = Execution.GetExecutionState(actorId);
            ActorCombatStateSnapshot combat = CombatState == null ? null : CombatState.GetCombatState(actorId);
            IReadOnlyList<CombatEngagementSnapshot> engagements = CombatState == null ? Array.Empty<CombatEngagementSnapshot>() : CombatState.GetActiveEngagements(actorId);
            IReadOnlyList<string> recentOpponents = CombatState == null ? Array.Empty<string>() : CombatState.GetRecentOpponents(actorId);
            IReadOnlyList<CombatOngoingEffectSnapshot> ongoing = OngoingEffects == null
                ? Array.Empty<CombatOngoingEffectSnapshot>()
                : OngoingEffects.QueryActiveEffects(actor, actorId)
                    .Select(instance => new CombatOngoingEffectSnapshot(instance.InstanceId, instance.Definition == null ? string.Empty : instance.Definition.Id, instance.SourceActorId, instance.TargetActorId, instance.StackCount, instance.RemainingDuration, instance.Revision))
                    .ToList();
            IReadOnlyList<CombatContributionLedgerSnapshot> ledgers = Contributions == null ? Array.Empty<CombatContributionLedgerSnapshot>() : Contributions.GetLedgerSnapshots();
            IReadOnlyList<CombatReactionSourceSnapshot> reactions = Reactions == null
                ? Array.Empty<CombatReactionSourceSnapshot>()
                : Reactions.Registrations
                    .Select(registration => new CombatReactionSourceSnapshot(
                        registration.RegistrationId,
                        registration.OwnerActorId,
                        registration.SourceKind.ToString(),
                        registration.SourceStableId,
                        registration.SourceInstanceId,
                        registration.SourcePriority,
                        registration.Definition == null ? string.Empty : registration.Definition.Id,
                        registration.Active))
                    .ToList();
            IReadOnlyList<ResourceSnapshot> resourceSnapshots = resources == null ? Array.Empty<ResourceSnapshot>() : resources.GetSnapshots();
            IReadOnlyList<CombatStatValueSnapshot> combatStats = BuildCombatStats(stats);
            CombatSubsystemRevisionSnapshot revisions = BuildRevisionSnapshot(character, lifecycle, activeDefense, activeExecution, ongoing.Count, engagements.Count, reactions.Count);
            List<CombatRuntimeDiagnostic> diagnostics = readiness.Diagnostics.ToList();
            diagnostics.AddRange(CombatIntegrityValidator.Validate(this, actor).Diagnostics);
            return new CombatRuntimeSnapshot(actorId, bodyId, personId, readiness, lifecycle == null ? ActorLifecycleState.Active : lifecycle.State, resourceSnapshots, combatStats, activeDefense, activeExecution, ongoing, combat, engagements, recentOpponents, reactions, ledgers, revisions, lastTransactionTrace, diagnostics);
        }

        public AttackResolutionResult PreviewAttack(AttackResolutionRequest request)
        {
            if (AttackResolution == null)
            {
                return AttackResolutionResult.Create(true, false, false, AttackOutcome.Invalid, AttackResolutionResultCode.InvalidRequest, "AttackResolutionService is missing.", request, string.Empty, string.Empty, string.Empty, 0f, 0f, 0f, 0f, request.BaseHitChance, request.BaseHitChance, false, false, 0f, false, "Missing service.", null, null);
            }

            return AttackResolution.PreviewAttack(request);
        }

        public AttackResolutionResult ExecuteAttack(AttackResolutionRequest request)
        {
            if (AttackResolution == null)
            {
                return AttackResolutionResult.Create(false, false, false, AttackOutcome.Invalid, AttackResolutionResultCode.InvalidRequest, "AttackResolutionService is missing.", request, string.Empty, string.Empty, string.Empty, 0f, 0f, 0f, 0f, request.BaseHitChance, request.BaseHitChance, false, false, 0f, false, "Missing service.", null, null);
            }

            AttackResolutionResult result = AttackResolution.ExecuteAttack(request);
            RememberTransaction(result);
            return result;
        }

        public DamageApplicationResult PreviewDamage(DamageApplicationRequest request)
        {
            return DamageHealing == null
                ? DamageApplicationResult.Failure(request, ImmediateCombatResultCode.InvalidRequest, "DamageHealingService is missing.")
                : DamageHealing.PreviewDamage(request);
        }

        public DamageApplicationResult ApplyDamage(DamageApplicationRequest request)
        {
            DamageApplicationResult result = DamageHealing == null
                ? DamageApplicationResult.Failure(request, ImmediateCombatResultCode.InvalidRequest, "DamageHealingService is missing.")
                : DamageHealing.ApplyDamage(request);
            RememberTransaction(result);
            return result;
        }

        public HealingApplicationResult PreviewHealing(HealingApplicationRequest request)
        {
            return DamageHealing == null
                ? HealingApplicationResult.Failure(request, ImmediateCombatResultCode.InvalidRequest, "DamageHealingService is missing.")
                : DamageHealing.PreviewHealing(request);
        }

        public HealingApplicationResult ApplyHealing(HealingApplicationRequest request)
        {
            HealingApplicationResult result = DamageHealing == null
                ? HealingApplicationResult.Failure(request, ImmediateCombatResultCode.InvalidRequest, "DamageHealingService is missing.")
                : DamageHealing.ApplyHealing(request);
            RememberTransaction(result);
            return result;
        }

        public CombatExecutionResult PreviewBeginExecution(CombatExecutionBeginRequest request)
        {
            return Execution == null
                ? CombatExecutionResult.Failure(true, CombatExecutionResultCode.InvalidRequest, "CombatExecutionService is missing.", request.TransactionId, request.Definition)
                : Execution.PreviewBeginExecution(request);
        }

        public CombatExecutionResult BeginExecution(CombatExecutionBeginRequest request)
        {
            CombatExecutionResult result = Execution == null
                ? CombatExecutionResult.Failure(false, CombatExecutionResultCode.InvalidRequest, "CombatExecutionService is missing.", request.TransactionId, request.Definition)
                : Execution.BeginExecution(request);
            RememberTransaction(result);
            return result;
        }

        public CombatExecutionResult PreviewCommitExecution(CombatExecutionCommitRequest request)
        {
            return Execution == null
                ? CombatExecutionResult.Failure(true, CombatExecutionResultCode.InvalidRequest, "CombatExecutionService is missing.", request.TransactionId)
                : Execution.PreviewCommitExecution(request);
        }

        public CombatExecutionResult CommitExecution(CombatExecutionCommitRequest request)
        {
            CombatExecutionResult result = Execution == null
                ? CombatExecutionResult.Failure(false, CombatExecutionResultCode.InvalidRequest, "CombatExecutionService is missing.", request.TransactionId)
                : Execution.CommitExecution(request);
            RememberTransaction(result);
            return result;
        }

        public DefenseActivationResult PreviewActivateDefense(DefenseActivationRequest request)
        {
            return Defense == null
                ? DefenseActivationResult.Failure(request, true, DefensiveActionResultCode.InvalidRequest, "DefensiveActionService is missing.")
                : Defense.PreviewActivate(request);
        }

        public DefenseActivationResult ActivateDefense(DefenseActivationRequest request)
        {
            DefenseActivationResult result = Defense == null
                ? DefenseActivationResult.Failure(request, false, DefensiveActionResultCode.InvalidRequest, "DefensiveActionService is missing.")
                : Defense.Activate(request);
            RememberTransaction(result);
            return result;
        }

        public CombatReactionChainResult PreviewReaction(CombatReactionTriggerContext context)
        {
            return Reactions == null
                ? new CombatReactionChainResult(false, true, CombatReactionResultCode.InvalidRequest, "CombatReactionService is missing.", context, null, 0)
                : Reactions.PreviewTrigger(context);
        }

        public CombatReactionChainResult ExecuteReaction(CombatReactionTriggerContext context)
        {
            CombatReactionChainResult result = Reactions == null
                ? new CombatReactionChainResult(false, false, CombatReactionResultCode.InvalidRequest, "CombatReactionService is missing.", context, null, 0)
                : Reactions.ExecuteTrigger(context);
            RememberTransaction(result);
            return result;
        }

        public CombatIntegrityReport ValidateIntegrity(GameObject actorObject = null)
        {
            return CombatIntegrityValidator.Validate(this, actorObject == null ? defaultActorObject : actorObject);
        }

        public void ClearTransientStateForRestore(string actorId = "")
        {
            Defense?.ClearTransientStateForRestore(actorId);
            Execution?.ClearTransientStateForRestore(actorId);
            CombatState?.ClearTransientStateForRestore();
            Reactions?.ClearTransientStateForRestore();
            Contributions?.ClearTransientStateForRestore();
            state = CombatRuntimeReadinessState.Restoring;
            lastTransactionTrace = new CombatTransactionTraceSnapshot(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null);
        }

        public void MarkReadyAfterRestore()
        {
            state = CombatRuntimeReadinessState.ResolvingDependencies;
            EvaluateReadiness();
        }

        public void Dispose()
        {
            state = CombatRuntimeReadinessState.Disposed;
        }

        private CombatSubsystemRevisionSnapshot BuildRevisionSnapshot(CharacterSystemCoordinator character, ActorLifecycleController lifecycle, DefensiveActionStateSnapshot defense, CombatExecutionStateSnapshot execution, int ongoingCount, int engagementCount, int reactionSourceCount)
        {
            long characterRevision = character == null ? 0L : character.Revision;
            long lifecycleRevision = lifecycle == null ? 0L : lifecycle.Revision;
            long contributionRevision = Contributions == null ? 0L : Contributions.Revision;
            long aggregate = characterRevision + lifecycleRevision + contributionRevision + ongoingCount + engagementCount + reactionSourceCount + (defense == null ? 0 : 1) + (execution == null ? 0 : 1);
            return new CombatSubsystemRevisionSnapshot(characterRevision, lifecycleRevision, contributionRevision, ongoingCount, engagementCount, reactionSourceCount, defense != null, execution != null, aggregate);
        }

        private static IReadOnlyList<CombatStatValueSnapshot> BuildCombatStats(CalculatedStatCollection stats)
        {
            if (stats == null)
            {
                return Array.Empty<CombatStatValueSnapshot>();
            }

            string[] ids =
            {
                CalculatedStatIds.PhysicalPower,
                CalculatedStatIds.MagicalPower,
                CalculatedStatIds.PhysicalDefense,
                CalculatedStatIds.MagicalDefense,
                CalculatedStatIds.Accuracy,
                CalculatedStatIds.Evasion
            };
            return ids.Where(stats.HasStat).Select(id => new CombatStatValueSnapshot(id, stats.GetValue(id))).ToList();
        }

        private void RememberTransaction(AttackResolutionResult result)
        {
            if (result == null)
            {
                return;
            }

            lastTransactionTrace = CombatTransactionValidator.BuildTrace(result);
        }

        private void RememberTransaction(DamageApplicationResult result)
        {
            if (result == null)
            {
                return;
            }

            lastTransactionTrace = CombatTransactionValidator.BuildTrace(result);
        }

        private void RememberTransaction(HealingApplicationResult result)
        {
            if (result == null)
            {
                return;
            }

            lastTransactionTrace = new CombatTransactionTraceSnapshot(result.Request.TransactionId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null);
        }

        private void RememberTransaction(CombatExecutionResult result)
        {
            if (result == null)
            {
                return;
            }

            string attack = result.AttackResult == null ? string.Empty : result.AttackResult.AttackTransactionId;
            string damage = result.AttackResult == null ? string.Empty : result.AttackResult.DamageTransactionId;
            lastTransactionTrace = new CombatTransactionTraceSnapshot(result.TransactionId, result.TransactionId, attack, string.Empty, damage, string.Empty, string.Empty, null);
        }

        private void RememberTransaction(DefenseActivationResult result)
        {
            if (result == null)
            {
                return;
            }

            lastTransactionTrace = new CombatTransactionTraceSnapshot(result.Request.TransactionId, string.Empty, string.Empty, result.Request.TransactionId, string.Empty, string.Empty, string.Empty, null);
        }

        private void RememberTransaction(CombatReactionChainResult result)
        {
            if (result == null)
            {
                return;
            }

            string root = result.RootContext == null ? string.Empty : result.RootContext.RootTransactionId;
            lastTransactionTrace = new CombatTransactionTraceSnapshot(root, string.Empty, string.Empty, string.Empty, string.Empty, root, string.Empty, null);
        }

        internal static string ResolveActorId(GameObject actorObject, CharacterSystemCoordinator character = null)
        {
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId))
            {
                return character.ActorId;
            }

            if (actorObject == null)
            {
                return string.Empty;
            }

            WorldEntityIdentity identity = actorObject.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        internal static string ResolveBodyId(GameObject actorObject)
        {
            if (actorObject == null)
            {
                return string.Empty;
            }

            WorldEntityIdentity identity = actorObject.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        internal static CharacterResourceCollection ResolveResources(GameObject actorObject, CharacterSystemCoordinator character = null)
        {
            return character == null ? actorObject == null ? null : actorObject.GetComponentInParent<CharacterResourceCollection>() : character.Resources;
        }

        private static void AddMissing(bool missing, CombatSubsystem subsystem, string code, string message, List<CombatRuntimeDiagnostic> diagnostics, string actorId = "")
        {
            if (missing)
            {
                diagnostics.Add(Error(subsystem, code, message, actorId));
            }
        }

        private static CombatRuntimeDiagnostic Error(CombatSubsystem subsystem, string code, string message, string actorId = "")
        {
            return new CombatRuntimeDiagnostic(CombatIntegritySeverity.Error, subsystem, code, message, actorId);
        }

        private static CombatRuntimeDiagnostic Warning(CombatSubsystem subsystem, string code, string message, string actorId = "")
        {
            return new CombatRuntimeDiagnostic(CombatIntegritySeverity.Warning, subsystem, code, message, actorId);
        }
    }
}
