using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.ActorLifecycle
{
    [DisallowMultipleComponent]
    public sealed class ActorLifecycleController : MonoBehaviour
    {
        private const double DefaultRevivalWaitRealSeconds = 3600d;

        [SerializeField] private DefeatPolicyDefinition defeatPolicy;
        [SerializeField] private CharacterSystemCoordinator character;
        [SerializeField] private CharacterResourceCollection resources;
        [SerializeField] private CharacterTraitCollection traits;
        [SerializeField] private CharacterCapabilityCollection capabilities;
        [SerializeField] private WorldEntityIdentity worldEntityIdentity;
        [SerializeField] private ActorLifecycleState lifecycleState = ActorLifecycleState.Active;

        private readonly HashSet<string> processedLifecycleTransactionIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> processedLifecycleTransactionOrder = new Queue<string>();
        private bool subscribed;
        private bool suppressResourceDefeatHandling;
        private long revision;
        private IActorLifecycleUtcClock utcClock = SystemActorLifecycleUtcClock.Instance;
        private DateTimeOffset? diedAtUtc;
        private DateTimeOffset? revivalAvailableAtUtc;

        public event Action<ActorLifecycleResult> DefeatProcessed;
        public event Action<ActorLifecycleResult> ActorDefeated;
        public event Action<ActorLifecycleResult> ActorBecameUnconscious;
        public event Action<ActorLifecycleResult> ActorRecovered;
        public event Action<ActorLifecycleResult> ActorDied;
        public event Action<ActorLifecycleResult> ActorRevived;
        public event Action<ActorLifecycleResult> LifecycleTransitionRejected;

        public ActorLifecycleState State => lifecycleState;
        public DefeatPolicyDefinition DefeatPolicy => ActiveDefeatPolicy;
        public bool CanAct => lifecycleState == ActorLifecycleState.Active;
        public bool IsDefeatedOrWorse => lifecycleState == ActorLifecycleState.Defeated || lifecycleState == ActorLifecycleState.Unconscious || lifecycleState == ActorLifecycleState.Dead;
        public long Revision => revision;
        public string ActorId => ResolveActorId();
        public DateTimeOffset? DiedAtUtc => diedAtUtc;
        public DateTimeOffset? RevivalAvailableAtUtc => revivalAvailableAtUtc;
        public double RevivalWaitRealSeconds => ActiveDefeatPolicy == null ? DefaultRevivalWaitRealSeconds : ActiveDefeatPolicy.RevivalWaitRealSeconds;
        public double RevivalWaitRemainingSeconds => CalculateRevivalWaitRemainingSeconds();
        public bool IsRevivalWaitComplete => RevivalWaitRemainingSeconds <= 0d;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            DefeatPolicyDefinition policy,
            CharacterResourceCollection resourceCollection = null,
            CharacterSystemCoordinator characterSystem = null,
            CharacterTraitCollection traitCollection = null,
            CharacterCapabilityCollection capabilityCollection = null)
        {
            Unsubscribe();
            defeatPolicy = policy == null ? defeatPolicy : policy;
            character = characterSystem == null ? character : characterSystem;
            resources = resourceCollection == null ? resources : resourceCollection;
            traits = traitCollection == null ? traits : traitCollection;
            capabilities = capabilityCollection == null ? capabilities : capabilityCollection;
            ResolveReferences();
            Subscribe();
        }

        public void ConfigureUtcClock(IActorLifecycleUtcClock clock)
        {
            utcClock = clock ?? SystemActorLifecycleUtcClock.Instance;
        }

        public ActorLifecycleResult PreviewDefeat(DefeatResolutionRequest request)
        {
            return ResolveDefeat(request, execute: false);
        }

        public ActorLifecycleResult ExecuteDefeat(DefeatResolutionRequest request)
        {
            return ResolveDefeat(request, execute: true);
        }

        public ActorLifecycleResult PreviewRecovery(LifecycleRecoveryRequest request)
        {
            return ResolveRecovery(request, execute: false);
        }

        public ActorLifecycleResult ExecuteRecovery(LifecycleRecoveryRequest request)
        {
            return ResolveRecovery(request, execute: true);
        }

        public ActorLifecycleResult PreviewDeath(LifecycleDeathRequest request)
        {
            return ResolveDeath(request, execute: false);
        }

        public ActorLifecycleResult ExecuteDeath(LifecycleDeathRequest request)
        {
            return ResolveDeath(request, execute: true);
        }

        public ActorLifecycleResult PreviewRevival(LifecycleRevivalRequest request)
        {
            return ResolveRevival(request, execute: false);
        }

        public ActorLifecycleResult ExecuteRevival(LifecycleRevivalRequest request)
        {
            return ResolveRevival(request, execute: true);
        }

        public ActorLifecycleSaveData CreateSaveData(string playerId, string personId)
        {
            return new ActorLifecycleSaveData
            {
                schemaVersion = ActorLifecycleSaveData.CurrentSchemaVersion,
                playerId = playerId ?? string.Empty,
                personId = personId ?? string.Empty,
                actorId = ActorId,
                policyId = PolicyId,
                lifecycleState = lifecycleState.ToString(),
                diedAtUtc = FormatUtc(diedAtUtc),
                revivalAvailableAtUtc = FormatUtc(revivalAvailableAtUtc)
            };
        }

        public bool RestoreFromSaveData(ActorLifecycleSaveData saveData, string expectedPlayerId, string expectedActorId, out string failureReason, bool restoring)
        {
            if (!ValidateSaveData(saveData, expectedPlayerId, expectedActorId, out failureReason))
            {
                return false;
            }

            ActorLifecycleState restored = (ActorLifecycleState)Enum.Parse(typeof(ActorLifecycleState), saveData.lifecycleState);
            ActorLifecycleState previous = lifecycleState;
            lifecycleState = restored;
            diedAtUtc = ParseUtcOrNull(saveData.diedAtUtc);
            revivalAvailableAtUtc = ParseUtcOrNull(saveData.revivalAvailableAtUtc);
            revision++;
            if (!restoring && previous != restored)
            {
                RaiseTransition(ActorLifecycleResult.Create(true, false, false, ActorLifecycleResultCode.Success, $"Lifecycle restored to {restored}.", string.Empty, string.Empty, ActorId, PolicyId, LifecycleTransitionKind.None, LifecycleTriggerKind.Scripted, previous, restored, PolicyOutcome, CurrentHealth, CurrentHealth, HealthMinimum, HealthMaximum, 0f, 0f, 0f, string.Empty, revision));
            }

            return true;
        }

        public static bool ValidateSaveData(ActorLifecycleSaveData saveData, string expectedPlayerId, string expectedActorId, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Actor lifecycle save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != ActorLifecycleSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported actor lifecycle schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedPlayerId) && !string.Equals(saveData.playerId, expectedPlayerId, StringComparison.Ordinal))
            {
                failureReason = $"Saved lifecycle owner '{saveData.playerId}' does not match current player '{expectedPlayerId}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(saveData.actorId))
            {
                failureReason = "Saved lifecycle actor ID is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedActorId) && !string.Equals(saveData.actorId, expectedActorId, StringComparison.Ordinal))
            {
                failureReason = $"Saved lifecycle actor '{saveData.actorId}' does not match current actor '{expectedActorId}'.";
                return false;
            }

            if (!Enum.TryParse(saveData.lifecycleState, out ActorLifecycleState restoredState))
            {
                failureReason = $"Saved lifecycle state '{saveData.lifecycleState}' is invalid.";
                return false;
            }

            bool hasDiedAt = TryParseUtc(saveData.diedAtUtc, out DateTimeOffset savedDiedAt);
            bool hasRevivalDeadline = TryParseUtc(saveData.revivalAvailableAtUtc, out DateTimeOffset savedRevivalDeadline);
            if (restoredState == ActorLifecycleState.Dead)
            {
                if (!hasDiedAt || !hasRevivalDeadline)
                {
                    failureReason = "Dead lifecycle save data requires valid death and revival-availability UTC timestamps.";
                    return false;
                }

                if (savedRevivalDeadline < savedDiedAt)
                {
                    failureReason = "Saved revival-availability time cannot precede the death time.";
                    return false;
                }
            }
            else if (!string.IsNullOrWhiteSpace(saveData.diedAtUtc) || !string.IsNullOrWhiteSpace(saveData.revivalAvailableAtUtc))
            {
                failureReason = $"Lifecycle state '{restoredState}' cannot retain death timing data.";
                return false;
            }

            return true;
        }

        public bool ValidateHealthStateCoherence(out string failureReason)
        {
            failureReason = string.Empty;
            if (!TryGetHealth(out ResourceSnapshot health))
            {
                failureReason = "Lifecycle actor has no Health resource.";
                return false;
            }

            bool empty = health.Current <= health.Minimum + CharacterResourceCollection.Epsilon;
            if (lifecycleState == ActorLifecycleState.Active && empty)
            {
                failureReason = "Active actors may not be restored with zero Health.";
                return false;
            }

            if (lifecycleState == ActorLifecycleState.Dead && health.Current > health.Minimum + CharacterResourceCollection.Epsilon)
            {
                failureReason = "Dead actors may not be restored with positive Health.";
                return false;
            }

            return true;
        }

        public void ResetToActiveForRestore()
        {
            ActorLifecycleState previous = lifecycleState;
            lifecycleState = ActorLifecycleState.Active;
            ClearDeathTiming();
            processedLifecycleTransactionIds.Clear();
            processedLifecycleTransactionOrder.Clear();
            if (previous != ActorLifecycleState.Active)
            {
                revision++;
            }
        }

        private ActorLifecycleResult ResolveDefeat(DefeatResolutionRequest request, bool execute)
        {
            if (!ValidateTarget(request.TargetObject, request.TargetActorId, out string actorId, out ResourceSnapshot health, out ActorLifecycleResult invalid, request.TransactionId, request.SourceActorId, LifecycleTransitionKind.Defeat, request.Trigger))
            {
                return Reject(invalid, execute);
            }

            DefeatPolicyOutcome outcome = PolicyOutcome;
            ActorLifecycleState previous = lifecycleState;
            ActorLifecycleState resultState = CalculateDefeatResult(previous, outcome, out LifecycleTransitionKind transition);
            bool alreadyHandled = previous != ActorLifecycleState.Active;
            if (alreadyHandled)
            {
                return Reject(ActorLifecycleResult.Create(true, !execute, false, ActorLifecycleResultCode.NoChange, $"Lifecycle already handled: {previous}.", request.TransactionId, request.SourceActorId, actorId, PolicyId, LifecycleTransitionKind.None, request.Trigger, previous, previous, outcome, health.Current, health.Current, health.Minimum, health.Maximum, 0f, 0f, 0f, string.Empty, revision), execute, emitRejected: false);
            }

            if (outcome == DefeatPolicyOutcome.BecomeUnconscious && !CanBecomeUnconscious(out string unconsciousReason))
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.CapabilityRejected, unconsciousReason, request.TransactionId, request.SourceActorId, actorId, transition, request.Trigger, health, outcome), execute);
            }

            if (outcome == DefeatPolicyOutcome.DieImmediately && !CanDie(out string deathReason))
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.CapabilityRejected, deathReason, request.TransactionId, request.SourceActorId, actorId, transition, request.Trigger, health, outcome), execute);
            }

            if (!execute)
            {
                return ActorLifecycleResult.Create(true, true, false, ActorLifecycleResultCode.Preview, $"Defeat preview resolves to {resultState}.", request.TransactionId, request.SourceActorId, actorId, PolicyId, transition, request.Trigger, previous, resultState, outcome, health.Current, health.Current, health.Minimum, health.Maximum, 0f, 0f, 0f, string.Empty, revision);
            }

            if (IsDuplicateLifecycleTransaction(request.TransactionId))
            {
                return ActorLifecycleResult.Create(true, false, true, ActorLifecycleResultCode.DuplicateTransaction, "Duplicate lifecycle transition ignored.", request.TransactionId, request.SourceActorId, actorId, PolicyId, transition, request.Trigger, previous, previous, outcome, health.Current, health.Current, health.Minimum, health.Maximum, 0f, 0f, 0f, string.Empty, revision);
            }

            lifecycleState = resultState;
            if (resultState == ActorLifecycleState.Dead)
            {
                RecordDeathTiming();
            }
            RememberLifecycleTransaction(request.TransactionId);
            revision++;
            ActorLifecycleResult result = ActorLifecycleResult.Create(true, false, false, ActorLifecycleResultCode.Success, $"Actor lifecycle changed from {previous} to {resultState}.", request.TransactionId, request.SourceActorId, actorId, PolicyId, transition, request.Trigger, previous, resultState, outcome, health.Current, health.Current, health.Minimum, health.Maximum, 0f, 0f, 0f, string.Empty, revision);
            RaiseTransition(result);
            return result;
        }

        private ActorLifecycleResult ResolveRecovery(LifecycleRecoveryRequest request, bool execute)
        {
            if (!ValidateTarget(request.TargetObject, request.TargetActorId, out string actorId, out ResourceSnapshot health, out ActorLifecycleResult invalid, request.TransactionId, request.SourceActorId, LifecycleTransitionKind.Recovery, LifecycleTriggerKind.Recovery))
            {
                return Reject(invalid, execute);
            }

            if (execute && IsDuplicateLifecycleTransaction(request.TransactionId))
            {
                return ActorLifecycleResult.Create(true, false, true, ActorLifecycleResultCode.DuplicateTransaction, "Duplicate lifecycle transition ignored.", request.TransactionId, request.SourceActorId, actorId, PolicyId, LifecycleTransitionKind.Recovery, LifecycleTriggerKind.Recovery, lifecycleState, lifecycleState, PolicyOutcome, health.Current, health.Current, health.Minimum, health.Maximum, request.RequestedHealthRestore, 0f, RecoveryMinimumHealth, string.Empty, revision);
            }

            if (lifecycleState != ActorLifecycleState.Unconscious && lifecycleState != ActorLifecycleState.Defeated)
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.InvalidState, $"Cannot recover from {lifecycleState}.", request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Recovery, LifecycleTriggerKind.Recovery, health, PolicyOutcome), execute);
            }

            if (ActiveDefeatPolicy != null && !ActiveDefeatPolicy.AllowRecovery)
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.PolicyRejected, "Recovery is disallowed by the defeat policy.", request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Recovery, LifecycleTriggerKind.Recovery, health, PolicyOutcome), execute);
            }

            if (!CapabilityAllows(CanRecoverCapabilityId, out string capabilityReason))
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.CapabilityRejected, capabilityReason, request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Recovery, LifecycleTriggerKind.Recovery, health, PolicyOutcome), execute);
            }

            if (!RequirementsPass(ActiveDefeatPolicy == null ? null : ActiveDefeatPolicy.RecoveryRequirements, out string requirementSummary))
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.RequirementRejected, "Recovery requirements failed.", request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Recovery, LifecycleTriggerKind.Recovery, health, PolicyOutcome, requirementSummary), execute);
            }

            float restore = CalculateRestoreAmount(request.RequestedHealthRestore, RecoveryMinimumHealth, health);
            if (!execute)
            {
                float previewHealth = Mathf.Min(health.Maximum, health.Current + restore);
                return ActorLifecycleResult.Create(true, true, false, ActorLifecycleResultCode.Preview, "Recovery preview calculated without mutation.", request.TransactionId, request.SourceActorId, actorId, PolicyId, LifecycleTransitionKind.Recovery, LifecycleTriggerKind.Recovery, lifecycleState, ActorLifecycleState.Active, PolicyOutcome, health.Current, previewHealth, health.Minimum, health.Maximum, request.RequestedHealthRestore, restore, RecoveryMinimumHealth, requirementSummary, revision);
            }

            return ApplyHealthRestoreAndState(request.TransactionId, request.SourceActorId, actorId, request.Reason, restore, request.RequestedHealthRestore, RecoveryMinimumHealth, LifecycleTransitionKind.Recovery, LifecycleTriggerKind.Recovery, ActorLifecycleState.Active, requirementSummary);
        }

        private ActorLifecycleResult ResolveDeath(LifecycleDeathRequest request, bool execute)
        {
            if (!ValidateTarget(request.TargetObject, request.TargetActorId, out string actorId, out ResourceSnapshot health, out ActorLifecycleResult invalid, request.TransactionId, request.SourceActorId, LifecycleTransitionKind.Death, request.Trigger))
            {
                return Reject(invalid, execute);
            }

            if (execute && IsDuplicateLifecycleTransaction(request.TransactionId))
            {
                return ActorLifecycleResult.Create(true, false, true, ActorLifecycleResultCode.DuplicateTransaction, "Duplicate lifecycle transition ignored.", request.TransactionId, request.SourceActorId, actorId, PolicyId, LifecycleTransitionKind.Death, request.Trigger, lifecycleState, lifecycleState, PolicyOutcome, health.Current, health.Current, health.Minimum, health.Maximum, 0f, 0f, 0f, string.Empty, revision);
            }

            if (lifecycleState == ActorLifecycleState.Dead)
            {
                return ActorLifecycleResult.Create(true, !execute, false, ActorLifecycleResultCode.NoChange, "Actor is already dead.", request.TransactionId, request.SourceActorId, actorId, PolicyId, LifecycleTransitionKind.Death, request.Trigger, lifecycleState, lifecycleState, PolicyOutcome, health.Current, health.Current, health.Minimum, health.Maximum, 0f, 0f, 0f, string.Empty, revision);
            }

            if (ActiveDefeatPolicy != null && !ActiveDefeatPolicy.AllowDeath)
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.PolicyRejected, "Death is disallowed by the defeat policy.", request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Death, request.Trigger, health, PolicyOutcome), execute);
            }

            if (HasDeathImmunity())
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.DeathImmune, "Death immunity prevented death.", request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Death, request.Trigger, health, PolicyOutcome), execute);
            }

            if (!CanDie(out string deathReason))
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.CapabilityRejected, deathReason, request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Death, request.Trigger, health, PolicyOutcome), execute);
            }

            if (!RequirementsPass(ActiveDefeatPolicy == null ? null : ActiveDefeatPolicy.DeathRequirements, out string requirementSummary))
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.RequirementRejected, "Death requirements failed.", request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Death, request.Trigger, health, PolicyOutcome, requirementSummary), execute);
            }

            if (!execute)
            {
                return ActorLifecycleResult.Create(true, true, false, ActorLifecycleResultCode.Preview, "Death preview calculated without mutation.", request.TransactionId, request.SourceActorId, actorId, PolicyId, LifecycleTransitionKind.Death, request.Trigger, lifecycleState, ActorLifecycleState.Dead, PolicyOutcome, health.Current, health.Minimum, health.Minimum, health.Maximum, 0f, 0f, 0f, requirementSummary, revision);
            }

            if (IsDuplicateLifecycleTransaction(request.TransactionId))
            {
                return ActorLifecycleResult.Create(true, false, true, ActorLifecycleResultCode.DuplicateTransaction, "Duplicate lifecycle transition ignored.", request.TransactionId, request.SourceActorId, actorId, PolicyId, LifecycleTransitionKind.Death, request.Trigger, lifecycleState, lifecycleState, PolicyOutcome, health.Current, health.Current, health.Minimum, health.Maximum, 0f, 0f, 0f, requirementSummary, revision);
            }

            ActorLifecycleState previous = lifecycleState;
            ResourceChangeResult resourceResult = null;
            float oldHealth = health.Current;
            float newHealth = health.Current;
            if (health.Current > health.Minimum + CharacterResourceCollection.Epsilon)
            {
                suppressResourceDefeatHandling = true;
                try
                {
                    resourceResult = resources.ApplyChange(new ResourceChangeRequest(ResourceIds.Health, ResourceChangeOperation.Damage, health.Current - health.Minimum, ResourceChangeSourceCategory.Gameplay, request.SourceActorId, request.Reason, request.TransactionId, allowPartial: true));
                }
                finally
                {
                    suppressResourceDefeatHandling = false;
                }

                if (resourceResult == null || !resourceResult.Succeeded)
                {
                    return Reject(BuildFailure(ActorLifecycleResultCode.ResourceRejected, resourceResult == null ? "Health death transaction failed." : resourceResult.Message, request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Death, request.Trigger, health, PolicyOutcome, requirementSummary), execute);
                }

                if (resourceResult.DuplicateEvent)
                {
                    return ActorLifecycleResult.Create(true, false, true, ActorLifecycleResultCode.DuplicateTransaction, "Duplicate Health death transaction ignored.", request.TransactionId, request.SourceActorId, actorId, PolicyId, LifecycleTransitionKind.Death, request.Trigger, previous, previous, PolicyOutcome, oldHealth, oldHealth, health.Minimum, health.Maximum, 0f, 0f, 0f, requirementSummary, revision, resourceResult);
                }

                newHealth = resourceResult.NewCurrent;
            }

            lifecycleState = ActorLifecycleState.Dead;
            RecordDeathTiming();
            RememberLifecycleTransaction(request.TransactionId);
            revision++;
            ActorLifecycleResult result = ActorLifecycleResult.Create(true, false, false, ActorLifecycleResultCode.Success, $"Actor lifecycle changed from {previous} to Dead.", request.TransactionId, request.SourceActorId, actorId, PolicyId, LifecycleTransitionKind.Death, request.Trigger, previous, ActorLifecycleState.Dead, PolicyOutcome, oldHealth, newHealth, health.Minimum, health.Maximum, 0f, 0f, 0f, requirementSummary, revision, resourceResult);
            RaiseTransition(result);
            return result;
        }

        private ActorLifecycleResult ResolveRevival(LifecycleRevivalRequest request, bool execute)
        {
            if (!ValidateTarget(request.TargetObject, request.TargetActorId, out string actorId, out ResourceSnapshot health, out ActorLifecycleResult invalid, request.TransactionId, request.SourceActorId, LifecycleTransitionKind.Revival, LifecycleTriggerKind.Revival))
            {
                return Reject(invalid, execute);
            }

            if (execute && IsDuplicateLifecycleTransaction(request.TransactionId))
            {
                return ActorLifecycleResult.Create(true, false, true, ActorLifecycleResultCode.DuplicateTransaction, "Duplicate lifecycle transition ignored.", request.TransactionId, request.SourceActorId, actorId, PolicyId, LifecycleTransitionKind.Revival, LifecycleTriggerKind.Revival, lifecycleState, lifecycleState, PolicyOutcome, health.Current, health.Current, health.Minimum, health.Maximum, request.RequestedHealthRestore, 0f, RevivalMinimumHealth, string.Empty, revision);
            }

            if (lifecycleState != ActorLifecycleState.Dead)
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.InvalidState, $"Cannot revive from {lifecycleState}.", request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Revival, LifecycleTriggerKind.Revival, health, PolicyOutcome), execute);
            }

            if (ActiveDefeatPolicy != null && !ActiveDefeatPolicy.AllowRevival)
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.PolicyRejected, "Revival is disallowed by the defeat policy.", request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Revival, LifecycleTriggerKind.Revival, health, PolicyOutcome), execute);
            }

            double remainingWait = RevivalWaitRemainingSeconds;
            if (remainingWait > 0d)
            {
                string availableAt = revivalAvailableAtUtc.HasValue ? revivalAvailableAtUtc.Value.ToString("O", CultureInfo.InvariantCulture) : "the configured deadline";
                string message = $"Revival is locked for another {FormatDuration(remainingWait)} (available at {availableAt}).";
                return Reject(BuildFailure(ActorLifecycleResultCode.RevivalWaitActive, message, request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Revival, LifecycleTriggerKind.Revival, health, PolicyOutcome), execute);
            }

            if (!CapabilityAllows(CanBeRevivedCapabilityId, out string capabilityReason))
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.CapabilityRejected, capabilityReason, request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Revival, LifecycleTriggerKind.Revival, health, PolicyOutcome), execute);
            }

            if (!RequirementsPass(ActiveDefeatPolicy == null ? null : ActiveDefeatPolicy.RevivalRequirements, out string requirementSummary))
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.RequirementRejected, "Revival requirements failed.", request.TransactionId, request.SourceActorId, actorId, LifecycleTransitionKind.Revival, LifecycleTriggerKind.Revival, health, PolicyOutcome, requirementSummary), execute);
            }

            float restore = CalculateRestoreAmount(request.RequestedHealthRestore, RevivalMinimumHealth, health);
            if (!execute)
            {
                float previewHealth = Mathf.Min(health.Maximum, health.Current + restore);
                return ActorLifecycleResult.Create(true, true, false, ActorLifecycleResultCode.Preview, "Revival preview calculated without mutation.", request.TransactionId, request.SourceActorId, actorId, PolicyId, LifecycleTransitionKind.Revival, LifecycleTriggerKind.Revival, lifecycleState, ActorLifecycleState.Active, PolicyOutcome, health.Current, previewHealth, health.Minimum, health.Maximum, request.RequestedHealthRestore, restore, RevivalMinimumHealth, requirementSummary, revision);
            }

            return ApplyHealthRestoreAndState(request.TransactionId, request.SourceActorId, actorId, request.Reason, restore, request.RequestedHealthRestore, RevivalMinimumHealth, LifecycleTransitionKind.Revival, LifecycleTriggerKind.Revival, ActorLifecycleState.Active, requirementSummary);
        }

        private ActorLifecycleResult ApplyHealthRestoreAndState(string transactionId, string sourceActorId, string actorId, string reason, float restore, float requestedRestore, float policyMinimum, LifecycleTransitionKind transition, LifecycleTriggerKind trigger, ActorLifecycleState resultState, string requirementSummary)
        {
            if (IsDuplicateLifecycleTransaction(transactionId))
            {
                ResourceSnapshot duplicateHealth = HealthSnapshotOrDefault;
                return ActorLifecycleResult.Create(true, false, true, ActorLifecycleResultCode.DuplicateTransaction, "Duplicate lifecycle transition ignored.", transactionId, sourceActorId, actorId, PolicyId, transition, trigger, lifecycleState, lifecycleState, PolicyOutcome, duplicateHealth.Current, duplicateHealth.Current, duplicateHealth.Minimum, duplicateHealth.Maximum, requestedRestore, 0f, policyMinimum, requirementSummary, revision);
            }

            ActorLifecycleState previous = lifecycleState;
            ResourceSnapshot before = HealthSnapshotOrDefault;
            ResourceChangeResult resourceResult = resources.ApplyHealing(ResourceIds.Health, restore, sourceActorId, reason, transactionId);
            if (resourceResult == null || !resourceResult.Succeeded)
            {
                return Reject(BuildFailure(ActorLifecycleResultCode.ResourceRejected, resourceResult == null ? "Health restore transaction failed." : resourceResult.Message, transactionId, sourceActorId, actorId, transition, trigger, before, PolicyOutcome, requirementSummary), execute: true);
            }

            if (resourceResult.DuplicateEvent)
            {
                return ActorLifecycleResult.Create(true, false, true, ActorLifecycleResultCode.DuplicateTransaction, "Duplicate Health restore transaction ignored.", transactionId, sourceActorId, actorId, PolicyId, transition, trigger, previous, previous, PolicyOutcome, before.Current, before.Current, before.Minimum, before.Maximum, requestedRestore, 0f, policyMinimum, requirementSummary, revision, resourceResult);
            }

            lifecycleState = resultState;
            if (transition == LifecycleTransitionKind.Revival)
            {
                ClearDeathTiming();
            }
            RememberLifecycleTransaction(transactionId);
            revision++;
            ActorLifecycleResult result = ActorLifecycleResult.Create(true, false, false, ActorLifecycleResultCode.Success, $"Actor lifecycle changed from {previous} to {resultState}.", transactionId, sourceActorId, actorId, PolicyId, transition, trigger, previous, resultState, PolicyOutcome, before.Current, resourceResult.NewCurrent, before.Minimum, before.Maximum, requestedRestore, resourceResult.AppliedAmount, policyMinimum, requirementSummary, revision, resourceResult);
            RaiseTransition(result);
            return result;
        }

        private void OnResourceChanged(CharacterResourceCollection resourceCollection, ResourceChangeResult result)
        {
            if (suppressResourceDefeatHandling || result == null || !result.Succeeded || result.DuplicateEvent)
            {
                return;
            }

            if (!string.Equals(result.Request.ResourceId, ResourceIds.Health, StringComparison.Ordinal) || !result.BecameEmpty || result.Request.Restoration || result.Request.Migration)
            {
                return;
            }

            string tx = string.IsNullOrWhiteSpace(result.Request.EventId)
                ? $"lifecycle.defeat.{Guid.NewGuid():N}"
                : $"{result.Request.EventId}.lifecycle";
            ExecuteDefeat(new DefeatResolutionRequest(tx, result.Request.SourceId, null, ActorId, gameObject, LifecycleTriggerKind.HealthDepleted, result.Request.EventId, "Health reached zero.", result.Request.AuthorityValidated));
        }

        private void RaiseTransition(ActorLifecycleResult result)
        {
            if (result == null)
            {
                return;
            }

            if (!result.Succeeded)
            {
                LifecycleTransitionRejected?.Invoke(result);
                return;
            }

            if (result.Transition == LifecycleTransitionKind.Defeat || result.Transition == LifecycleTransitionKind.Unconsciousness)
            {
                DefeatProcessed?.Invoke(result);
            }

            if (result.ResultingState == ActorLifecycleState.Defeated)
            {
                ActorDefeated?.Invoke(result);
            }
            else if (result.ResultingState == ActorLifecycleState.Unconscious)
            {
                ActorBecameUnconscious?.Invoke(result);
            }
            else if (result.Transition == LifecycleTransitionKind.Recovery)
            {
                ActorRecovered?.Invoke(result);
            }
            else if (result.ResultingState == ActorLifecycleState.Dead)
            {
                GetComponentInParent<StatusEffectController>()?.HandleDeath();
                ActorDied?.Invoke(result);
            }
            else if (result.Transition == LifecycleTransitionKind.Revival)
            {
                ActorRevived?.Invoke(result);
            }
        }

        private ActorLifecycleResult Reject(ActorLifecycleResult result, bool execute, bool emitRejected = true)
        {
            if (result == null)
            {
                return null;
            }

            if (!execute && !result.Preview)
            {
                result = ActorLifecycleResult.Create(result.Succeeded, true, result.Duplicate, result.Succeeded ? ActorLifecycleResultCode.Preview : result.Code, result.Message, result.TransactionId, result.SourceActorId, result.TargetActorId, result.PolicyId, result.Transition, result.Trigger, result.PreviousState, result.ResultingState, result.PolicyOutcome, result.OldHealth, result.NewHealth, result.HealthMinimum, result.HealthMaximum, result.RequestedHealthRestore, result.AppliedHealthRestore, result.PolicyMinimumHealth, result.RequirementSummary, revision, result.ResourceResult);
            }

            if (execute && !result.Succeeded && emitRejected)
            {
                RaiseTransition(result);
            }

            return result;
        }

        private ActorLifecycleResult BuildFailure(string code, string message, string transactionId, string sourceActorId, string actorId, LifecycleTransitionKind transition, LifecycleTriggerKind trigger, ResourceSnapshot health, DefeatPolicyOutcome outcome, string requirementSummary = "")
        {
            return ActorLifecycleResult.Failure(code, message, transactionId, sourceActorId, actorId, PolicyId, transition, trigger, lifecycleState, outcome, health.Current, health.Minimum, health.Maximum, requirementSummary);
        }

        private bool ValidateTarget(GameObject targetObject, string requestedActorId, out string actorId, out ResourceSnapshot health, out ActorLifecycleResult invalid, string transactionId, string sourceActorId, LifecycleTransitionKind transition, LifecycleTriggerKind trigger)
        {
            ResolveReferences();
            actorId = ActorId;
            health = HealthSnapshotOrDefault;
            invalid = null;

            if (targetObject != null && targetObject.GetComponentInParent<ActorLifecycleController>() != this)
            {
                invalid = BuildFailure(ActorLifecycleResultCode.MissingTarget, "Lifecycle request target does not resolve to this actor.", transactionId, sourceActorId, actorId, transition, trigger, health, PolicyOutcome);
                return false;
            }

            if (string.IsNullOrWhiteSpace(actorId))
            {
                invalid = BuildFailure(ActorLifecycleResultCode.MissingTarget, "Target actor identity is missing.", transactionId, sourceActorId, actorId, transition, trigger, health, PolicyOutcome);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(requestedActorId) && !string.Equals(requestedActorId, actorId, StringComparison.Ordinal))
            {
                invalid = BuildFailure(ActorLifecycleResultCode.StaleActor, $"Target actor identity '{requestedActorId}' no longer resolves to '{actorId}'.", transactionId, sourceActorId, actorId, transition, trigger, health, PolicyOutcome);
                return false;
            }

            if (!TryGetHealth(out health))
            {
                invalid = BuildFailure(ActorLifecycleResultCode.MissingHealth, "Target actor has no Health resource.", transactionId, sourceActorId, actorId, transition, trigger, health, PolicyOutcome);
                return false;
            }

            return true;
        }

        private ActorLifecycleState CalculateDefeatResult(ActorLifecycleState previous, DefeatPolicyOutcome outcome, out LifecycleTransitionKind transition)
        {
            transition = LifecycleTransitionKind.Defeat;
            if (previous != ActorLifecycleState.Active)
            {
                return previous;
            }

            switch (outcome)
            {
                case DefeatPolicyOutcome.IgnoreDefeat:
                    transition = LifecycleTransitionKind.None;
                    return ActorLifecycleState.Active;
                case DefeatPolicyOutcome.DieImmediately:
                    transition = LifecycleTransitionKind.Death;
                    return ActorLifecycleState.Dead;
                case DefeatPolicyOutcome.RemainDefeated:
                    return ActorLifecycleState.Defeated;
                case DefeatPolicyOutcome.BecomeUnconscious:
                default:
                    transition = LifecycleTransitionKind.Unconsciousness;
                    return ActorLifecycleState.Unconscious;
            }
        }

        private bool CanBecomeUnconscious(out string reason)
        {
            if (ActiveDefeatPolicy != null && !ActiveDefeatPolicy.AllowUnconsciousness)
            {
                reason = "Unconsciousness is disallowed by the defeat policy.";
                return false;
            }

            return CapabilityAllows(CanBecomeUnconsciousCapabilityId, out reason);
        }

        private bool CanDie(out string reason)
        {
            if (ActiveDefeatPolicy != null && !ActiveDefeatPolicy.AllowDeath)
            {
                reason = "Death is disallowed by the defeat policy.";
                return false;
            }

            return CapabilityAllows(CanDieCapabilityId, out reason);
        }

        private bool HasDeathImmunity()
        {
            CapabilitySnapshot snapshot = capabilities == null ? null : capabilities.Evaluate(DeathImmunityCapabilityId);
            return snapshot != null && !snapshot.Blocked && snapshot.BooleanValue;
        }

        private bool CapabilityAllows(string capabilityId, out string reason)
        {
            reason = string.Empty;
            if (capabilities == null || string.IsNullOrWhiteSpace(capabilityId))
            {
                return true;
            }

            CapabilitySnapshot snapshot = capabilities.Evaluate(capabilityId);
            if (snapshot == null || snapshot.Sources == null || snapshot.Sources.Count == 0)
            {
                return true;
            }

            if (snapshot.Blocked || !snapshot.BooleanValue)
            {
                reason = $"Capability '{capabilityId}' is false or blocked.";
                return false;
            }

            return true;
        }

        private bool RequirementsPass(RequirementSetDefinition requirements, out string summary)
        {
            summary = string.Empty;
            if (requirements == null)
            {
                return true;
            }

            RequirementEvaluationResult result = character == null
                ? CapabilityRequirementEvaluator.Evaluate(requirements, new RequirementEvaluationContext { Resources = resources, Traits = traits, Capabilities = capabilities })
                : character.Query.EvaluateRequirement(requirements);
            summary = string.Join("; ", result.TestLabFailureReasons);
            return result.Passed;
        }

        private float CalculateRestoreAmount(float requested, float policyMinimum, ResourceSnapshot health)
        {
            float minimumRestore = Mathf.Max(0f, policyMinimum);
            float requestedRestore = float.IsNaN(requested) || float.IsInfinity(requested) ? 0f : Mathf.Max(0f, requested);
            float targetRestore = Mathf.Max(minimumRestore, requestedRestore);
            float missing = Mathf.Max(0f, health.Maximum - health.Current);
            return Mathf.Min(targetRestore, missing);
        }

        private void RecordDeathTiming()
        {
            DateTimeOffset now = (utcClock ?? SystemActorLifecycleUtcClock.Instance).UtcNow.ToUniversalTime();
            diedAtUtc = now;
            double supportedWait = Math.Min(RevivalWaitRealSeconds, (DateTimeOffset.MaxValue - now).TotalSeconds);
            revivalAvailableAtUtc = now.AddSeconds(supportedWait);
        }

        private void ClearDeathTiming()
        {
            diedAtUtc = null;
            revivalAvailableAtUtc = null;
        }

        private double CalculateRevivalWaitRemainingSeconds()
        {
            if (lifecycleState != ActorLifecycleState.Dead || !revivalAvailableAtUtc.HasValue)
            {
                return 0d;
            }

            DateTimeOffset now = (utcClock ?? SystemActorLifecycleUtcClock.Instance).UtcNow.ToUniversalTime();
            return Math.Max(0d, (revivalAvailableAtUtc.Value - now).TotalSeconds);
        }

        private static string FormatUtc(DateTimeOffset? value)
        {
            return value.HasValue ? value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : string.Empty;
        }

        private static DateTimeOffset? ParseUtcOrNull(string value)
        {
            return TryParseUtc(value, out DateTimeOffset parsed) ? parsed.ToUniversalTime() : (DateTimeOffset?)null;
        }

        private static bool TryParseUtc(string value, out DateTimeOffset parsed)
        {
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed);
        }

        private static string FormatDuration(double seconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0d, seconds)));
            if (duration.TotalHours >= 1d)
            {
                return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
            }

            return duration.TotalMinutes >= 1d
                ? $"{duration.Minutes}m {duration.Seconds}s"
                : $"{duration.Seconds}s";
        }

        private bool TryGetHealth(out ResourceSnapshot health)
        {
            ResolveReferences();
            if (resources != null && resources.TryGetResource(ResourceIds.Health, out health))
            {
                return true;
            }

            health = default;
            return false;
        }

        private ResourceSnapshot HealthSnapshotOrDefault => TryGetHealth(out ResourceSnapshot snapshot) ? snapshot : default;
        private DefeatPolicyDefinition ActiveDefeatPolicy => defeatPolicy != null ? defeatPolicy : character?.Body?.Species?.DefaultDefeatPolicy;
        private string PolicyId => ActiveDefeatPolicy == null ? string.Empty : ActiveDefeatPolicy.Id;
        private DefeatPolicyOutcome PolicyOutcome => ActiveDefeatPolicy == null ? DefeatPolicyOutcome.BecomeUnconscious : ActiveDefeatPolicy.ZeroHealthOutcome;
        private float RecoveryMinimumHealth => ActiveDefeatPolicy == null ? 1f : ActiveDefeatPolicy.RecoveryMinimumHealth;
        private float RevivalMinimumHealth => ActiveDefeatPolicy == null ? 1f : ActiveDefeatPolicy.RevivalMinimumHealth;
        private string CanBecomeUnconsciousCapabilityId => ActiveDefeatPolicy == null ? ActorLifecycleCapabilityIds.CanBecomeUnconscious : ActiveDefeatPolicy.CanBecomeUnconsciousCapabilityId;
        private string CanDieCapabilityId => ActiveDefeatPolicy == null ? ActorLifecycleCapabilityIds.CanDie : ActiveDefeatPolicy.CanDieCapabilityId;
        private string CanRecoverCapabilityId => ActiveDefeatPolicy == null ? ActorLifecycleCapabilityIds.CanRecover : ActiveDefeatPolicy.CanRecoverCapabilityId;
        private string CanBeRevivedCapabilityId => ActiveDefeatPolicy == null ? ActorLifecycleCapabilityIds.CanBeRevived : ActiveDefeatPolicy.CanBeRevivedCapabilityId;
        private string DeathImmunityCapabilityId => ActiveDefeatPolicy == null ? ActorLifecycleCapabilityIds.DeathImmunity : ActiveDefeatPolicy.DeathImmunityCapabilityId;
        private float CurrentHealth => TryGetHealth(out ResourceSnapshot health) ? health.Current : 0f;
        private float HealthMinimum => TryGetHealth(out ResourceSnapshot health) ? health.Minimum : 0f;
        private float HealthMaximum => TryGetHealth(out ResourceSnapshot health) ? health.Maximum : 0f;

        private void ResolveReferences()
        {
            character = character == null ? GetComponentInParent<CharacterSystemCoordinator>() : character;
            resources = resources == null ? character == null ? GetComponentInParent<CharacterResourceCollection>() : character.Resources : resources;
            traits = traits == null ? character == null ? GetComponentInParent<CharacterTraitCollection>() : character.Traits : traits;
            capabilities = capabilities == null ? character == null ? GetComponentInParent<CharacterCapabilityCollection>() : character.Capabilities : capabilities;
            worldEntityIdentity = worldEntityIdentity == null ? GetComponentInParent<WorldEntityIdentity>() : worldEntityIdentity;
        }

        private string ResolveActorId()
        {
            ResolveReferences();
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId))
            {
                return character.ActorId;
            }

            if (worldEntityIdentity != null && !string.IsNullOrWhiteSpace(worldEntityIdentity.EntityId))
            {
                return worldEntityIdentity.EntityId;
            }

            return $"actor.runtime.{RuntimeHelpers.GetHashCode(gameObject):x8}";
        }

        private void Subscribe()
        {
            if (subscribed || resources == null)
            {
                return;
            }

            resources.ResourceChanged += OnResourceChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || resources == null)
            {
                subscribed = false;
                return;
            }

            resources.ResourceChanged -= OnResourceChanged;
            subscribed = false;
        }

        private bool IsDuplicateLifecycleTransaction(string transactionId)
        {
            return !string.IsNullOrWhiteSpace(transactionId) && processedLifecycleTransactionIds.Contains(transactionId);
        }

        private void RememberLifecycleTransaction(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || !processedLifecycleTransactionIds.Add(transactionId))
            {
                return;
            }

            processedLifecycleTransactionOrder.Enqueue(transactionId);
            while (processedLifecycleTransactionOrder.Count > 256)
            {
                processedLifecycleTransactionIds.Remove(processedLifecycleTransactionOrder.Dequeue());
            }
        }
    }
}
