using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Combat.Reactions
{
    [DisallowMultipleComponent]
    public sealed class CombatReactionService : MonoBehaviour
    {
        public const int DefaultMaximumChainDepth = 8;
        public const int DefaultMaximumReactionCount = 64;

        [SerializeField, Min(1)] private int maximumChainDepth = DefaultMaximumChainDepth;
        [SerializeField, Min(1)] private int maximumReactionCount = DefaultMaximumReactionCount;
        [SerializeField] private OngoingEffectService ongoingEffectService;

        private readonly List<CombatReactionSourceRegistration> registrations = new List<CombatReactionSourceRegistration>();
        private readonly HashSet<string> processedRootTransactions = new HashSet<string>(StringComparer.Ordinal);
        private IDamageHealingService damageHealingService = new DamageHealingService();

        public event Action<CombatReactionTriggerContext> TriggerAccepted;
        public event Action<CombatReactionExecutionResult> ReactionProcessed;
        public event Action<CombatReactionChainResult> ChainProcessed;

        public IReadOnlyList<CombatReactionSourceRegistration> Registrations => registrations.ToList();

        private void Awake()
        {
            if (ongoingEffectService == null)
            {
                ongoingEffectService = GetComponent<OngoingEffectService>();
            }
        }

        public CombatReactionSourceRegistration RegisterSource(CombatReactionSourceRegistration registration)
        {
            if (registration == null || registration.Definition == null)
            {
                return null;
            }

            string key = BuildRegistrationKey(registration);
            CombatReactionSourceRegistration existing = registrations.FirstOrDefault(candidate => string.Equals(BuildRegistrationKey(candidate), key, StringComparison.Ordinal));
            if (existing != null)
            {
                return existing;
            }

            registrations.Add(registration);
            return registration;
        }

        public void Configure(OngoingEffectService configuredOngoingEffectService)
        {
            ongoingEffectService = configuredOngoingEffectService == null ? ongoingEffectService : configuredOngoingEffectService;
        }

        public void ConfigureDamageHealing(IDamageHealingService service)
        {
            damageHealingService = service ?? damageHealingService;
        }

        public void Configure(OngoingEffectService configuredOngoingEffectService, IDamageHealingService service)
        {
            Configure(configuredOngoingEffectService);
            ConfigureDamageHealing(service);
        }

        public bool UnregisterSource(string registrationId)
        {
            if (string.IsNullOrWhiteSpace(registrationId))
            {
                return false;
            }

            int removed = registrations.RemoveAll(registration => string.Equals(registration.RegistrationId, registrationId, StringComparison.Ordinal));
            return removed > 0;
        }

        public void ClearSourcesForOwner(string ownerActorId)
        {
            if (string.IsNullOrWhiteSpace(ownerActorId))
            {
                return;
            }

            registrations.RemoveAll(registration => string.Equals(registration.OwnerActorId, ownerActorId, StringComparison.Ordinal));
        }

        public void ClearAllSources()
        {
            registrations.Clear();
        }

        public void ClearTransientStateForRestore()
        {
            processedRootTransactions.Clear();
        }

        public CombatReactionChainResult PreviewTrigger(CombatReactionTriggerContext context)
        {
            return ProcessTrigger(context, execute: false);
        }

        public CombatReactionChainResult ExecuteTrigger(CombatReactionTriggerContext context)
        {
            return ProcessTrigger(context, execute: true);
        }

        public CombatReactionChainResult ExecuteTriggerFromDamage(DamageApplicationResult result, CombatReactionTriggerType triggerType)
        {
            return CombatReactionTriggerContext.TryFromDamageResult(result, triggerType, out CombatReactionTriggerContext context)
                ? ExecuteTrigger(context)
                : new CombatReactionChainResult(false, false, CombatReactionResultCode.InvalidRequest, "Damage result did not qualify for the requested reaction trigger.", null, null, 0);
        }

        public CombatReactionChainResult ExecuteTriggerFromHealing(HealingApplicationResult result, CombatReactionTriggerType triggerType)
        {
            return CombatReactionTriggerContext.TryFromHealingResult(result, triggerType, out CombatReactionTriggerContext context)
                ? ExecuteTrigger(context)
                : new CombatReactionChainResult(false, false, CombatReactionResultCode.InvalidRequest, "Healing result did not qualify for the requested reaction trigger.", null, null, 0);
        }

        private CombatReactionChainResult ProcessTrigger(CombatReactionTriggerContext context, bool execute)
        {
            if (context == null || context.TriggerType == CombatReactionTriggerType.None)
            {
                return new CombatReactionChainResult(false, !execute, CombatReactionResultCode.InvalidRequest, "Combat reaction trigger context is invalid.", context, null, 0);
            }

            if (execute && !string.IsNullOrWhiteSpace(context.RootTransactionId) && !processedRootTransactions.Add(context.RootTransactionId))
            {
                CombatReactionChainResult duplicate = new CombatReactionChainResult(true, false, CombatReactionResultCode.Duplicate, "Duplicate root reaction trigger ignored.", context, null, 0);
                ChainProcessed?.Invoke(duplicate);
                return duplicate;
            }

            if (execute)
            {
                TriggerAccepted?.Invoke(context);
            }
            CombatReactionChainState state = new CombatReactionChainState(context.RootTransactionId, Mathf.Max(1, maximumChainDepth), Mathf.Max(1, maximumReactionCount));
            ProcessContext(context, execute, state, 0);
            CombatReactionChainResult result = new CombatReactionChainResult(true, !execute, execute ? CombatReactionResultCode.Success : CombatReactionResultCode.Preview, "Combat reaction trigger processed.", context, state.Results, state.DeepestDepth);
            if (execute)
            {
                ChainProcessed?.Invoke(result);
            }
            return result;
        }

        private void ProcessContext(CombatReactionTriggerContext context, bool execute, CombatReactionChainState state, int depth)
        {
            if (context == null || state.Results.Count >= state.MaximumReactionCount || depth >= state.MaximumDepth)
            {
                return;
            }

            state.DeepestDepth = Mathf.Max(state.DeepestDepth, depth);
            List<CombatReactionSourceRegistration> candidates = registrations
                .Where(registration => IsCandidate(registration, context))
                .OrderByDescending(registration => registration.Definition.Priority)
                .ThenByDescending(registration => registration.SourcePriority)
                .ThenBy(registration => registration.Definition.Id, StringComparer.Ordinal)
                .ThenBy(registration => registration.SourceStableId, StringComparer.Ordinal)
                .ThenBy(registration => registration.SourceInstanceId, StringComparer.Ordinal)
                .ToList();

            for (int i = 0; i < candidates.Count; i++)
            {
                if (state.Results.Count >= state.MaximumReactionCount)
                {
                    state.Results.Add(CombatReactionExecutionResult.Failure(!execute, CombatReactionResultCode.ChainLimitReached, "Combat reaction count limit reached.", null, null, context));
                    return;
                }

                CombatReactionExecutionResult result = ExecuteReaction(candidates[i], context, execute, state, depth, i);
                state.Results.Add(result);
                if (execute)
                {
                    ReactionProcessed?.Invoke(result);
                }

                if (!execute || !result.Succeeded || result.Duplicate)
                {
                    continue;
                }

                ProcessChildContext(result, execute, state, depth + 1);
            }
        }

        private CombatReactionExecutionResult ExecuteReaction(CombatReactionSourceRegistration source, CombatReactionTriggerContext context, bool execute, CombatReactionChainState state, int depth, int index)
        {
            CombatReactionDefinition definition = source == null ? null : source.Definition;
            if (definition == null)
            {
                return CombatReactionExecutionResult.Failure(!execute, CombatReactionResultCode.MissingDefinition, "Combat reaction definition is missing.", definition, source, context);
            }

            string executionKey = BuildExecutionKey(source, definition);
            if (definition.RecursionPolicy == CombatReactionRecursionPolicy.OncePerSourcePerChain && !state.RememberExecution(executionKey, definition.MaximumExecutionsPerChain))
            {
                return CombatReactionExecutionResult.Duplicated(definition, source, context, BuildChildTransactionId(state.RootTransactionId, context, source, depth, index));
            }

            float roll = CalculateRoll(state.RootTransactionId, context.TriggerType, definition.Id, source.SourceStableId, source.SourceInstanceId, index);
            if (roll < 0f || roll >= 1f)
            {
                return CombatReactionExecutionResult.Failure(!execute, CombatReactionResultCode.InvalidRequest, "Combat reaction deterministic roll was outside [0,1).", definition, source, context, roll: roll);
            }

            if (roll >= definition.ProcChance)
            {
                return CombatReactionExecutionResult.Failure(!execute, CombatReactionResultCode.ProcFailed, $"Combat reaction proc failed. Roll {roll:0.###} >= chance {definition.ProcChance:0.###}.", definition, source, context, roll: roll);
            }

            GameObject target = ResolveTargetObject(definition.TargetPolicy, source, context);
            string targetActorId = ResolveTargetActorId(definition.TargetPolicy, source, context, target);
            if (definition.TargetPolicy != CombatReactionTargetPolicy.None && target == null && string.IsNullOrWhiteSpace(targetActorId))
            {
                return CombatReactionExecutionResult.Failure(!execute, CombatReactionResultCode.MissingTarget, "Combat reaction target could not be resolved.", definition, source, context, roll: roll);
            }

            string transactionId = BuildChildTransactionId(state.RootTransactionId, context, source, depth, index);
            float amount = CalculateAmount(definition, context);
            if (!execute)
            {
                return CombatReactionExecutionResult.Success(true, "Combat reaction preview calculated without mutation.", definition, source, context, transactionId, roll, amount, null);
            }

            return ExecuteOperation(definition, source, context, target, targetActorId, transactionId, amount, roll);
        }

        private CombatReactionExecutionResult ExecuteOperation(CombatReactionDefinition definition, CombatReactionSourceRegistration source, CombatReactionTriggerContext context, GameObject target, string targetActorId, string transactionId, float amount, float roll)
        {
            switch (definition.OperationType)
            {
                case CombatReactionOperationType.NoOpDiagnostic:
                    return CombatReactionExecutionResult.Success(false, "Diagnostic combat reaction executed.", definition, source, context, transactionId, roll, amount, null);
                case CombatReactionOperationType.ApplyDamage:
                    return ApplyDamage(definition, source, context, target, targetActorId, transactionId, amount, roll);
                case CombatReactionOperationType.ApplyHealing:
                    return ApplyHealing(definition, source, context, target, targetActorId, transactionId, amount, roll);
                case CombatReactionOperationType.ApplyOngoingEffect:
                    return ApplyOngoingEffect(definition, source, context, target, targetActorId, transactionId, amount, roll);
                case CombatReactionOperationType.ModifyResource:
                    return ModifyResource(definition, source, context, target, transactionId, amount, roll);
                case CombatReactionOperationType.ApplyStatusEffect:
                case CombatReactionOperationType.RemoveStatusEffect:
                case CombatReactionOperationType.ApplyCondition:
                case CombatReactionOperationType.RemoveCondition:
                case CombatReactionOperationType.TriggerImmediateAbility:
                    return CombatReactionExecutionResult.Failure(false, CombatReactionResultCode.UnsupportedOperation, $"Combat reaction operation '{definition.OperationType}' is deferred until a safe production API exists.", definition, source, context, transactionId, roll);
                default:
                    return CombatReactionExecutionResult.Failure(false, CombatReactionResultCode.UnsupportedOperation, $"Combat reaction operation '{definition.OperationType}' is not supported.", definition, source, context, transactionId, roll);
            }
        }

        private CombatReactionExecutionResult ApplyDamage(CombatReactionDefinition definition, CombatReactionSourceRegistration source, CombatReactionTriggerContext context, GameObject target, string targetActorId, string transactionId, float amount, float roll)
        {
            DamageApplicationRequest request = new DamageApplicationRequest(
                transactionId,
                source.OwnerActorId,
                source.OwnerObject,
                targetActorId,
                target,
                definition.DamageType,
                amount,
                $"Combat reaction {definition.Id}",
                authorityValidated: true);
            DamageApplicationResult result = damageHealingService.ApplyDamage(request);
            return result != null && result.Succeeded
                ? CombatReactionExecutionResult.Success(false, "Combat reaction damage applied.", definition, source, context, transactionId, roll, amount, result)
                : CombatReactionExecutionResult.Failure(false, CombatReactionResultCode.OperationRejected, result == null ? "Damage operation failed." : result.Message, definition, source, context, transactionId, roll);
        }

        private CombatReactionExecutionResult ApplyHealing(CombatReactionDefinition definition, CombatReactionSourceRegistration source, CombatReactionTriggerContext context, GameObject target, string targetActorId, string transactionId, float amount, float roll)
        {
            HealingApplicationRequest request = new HealingApplicationRequest(
                transactionId,
                source.OwnerActorId,
                source.OwnerObject,
                targetActorId,
                target,
                amount,
                $"Combat reaction {definition.Id}",
                authorityValidated: true);
            HealingApplicationResult result = damageHealingService.ApplyHealing(request);
            return result != null && result.Succeeded
                ? CombatReactionExecutionResult.Success(false, "Combat reaction healing applied.", definition, source, context, transactionId, roll, amount, result)
                : CombatReactionExecutionResult.Failure(false, CombatReactionResultCode.OperationRejected, result == null ? "Healing operation failed." : result.Message, definition, source, context, transactionId, roll);
        }

        private CombatReactionExecutionResult ApplyOngoingEffect(CombatReactionDefinition definition, CombatReactionSourceRegistration source, CombatReactionTriggerContext context, GameObject target, string targetActorId, string transactionId, float amount, float roll)
        {
            if (ongoingEffectService == null)
            {
                return CombatReactionExecutionResult.Failure(false, CombatReactionResultCode.UnsupportedOperation, "No OngoingEffectService is configured for combat reactions.", definition, source, context, transactionId, roll);
            }

            OngoingEffectApplicationRequest request = new OngoingEffectApplicationRequest(
                transactionId,
                definition.OngoingEffect,
                source.OwnerActorId,
                source.OwnerObject,
                targetActorId,
                target,
                definition.Id,
                amountOverride: amount,
                authorityValidated: true);
            OngoingEffectApplicationResult result = ongoingEffectService.ApplyOngoingEffect(request);
            return result != null && result.Succeeded
                ? CombatReactionExecutionResult.Success(false, "Combat reaction ongoing effect applied.", definition, source, context, transactionId, roll, amount, result)
                : CombatReactionExecutionResult.Failure(false, CombatReactionResultCode.OperationRejected, result == null ? "Ongoing-effect operation failed." : result.Message, definition, source, context, transactionId, roll);
        }

        private CombatReactionExecutionResult ModifyResource(CombatReactionDefinition definition, CombatReactionSourceRegistration source, CombatReactionTriggerContext context, GameObject target, string transactionId, float amount, float roll)
        {
            CharacterResourceCollection resources = target == null ? null : target.GetComponentInParent<CharacterResourceCollection>();
            if (resources == null)
            {
                return CombatReactionExecutionResult.Failure(false, CombatReactionResultCode.MissingTarget, "Combat reaction resource target has no CharacterResourceCollection.", definition, source, context, transactionId, roll);
            }

            ResourceChangeRequest request = new ResourceChangeRequest(
                definition.ResourceId,
                amount >= 0f ? ResourceChangeOperation.Gain : ResourceChangeOperation.Spend,
                Mathf.Abs(amount),
                ResourceChangeSourceCategory.Combat,
                definition.Id,
                $"Combat reaction {definition.Id}",
                transactionId,
                allowPartial: true,
                authorityValidated: true);
            ResourceChangeResult result = resources.ApplyChange(request);
            return result != null && result.Succeeded
                ? CombatReactionExecutionResult.Success(false, "Combat reaction resource modification applied.", definition, source, context, transactionId, roll, amount, result)
                : CombatReactionExecutionResult.Failure(false, CombatReactionResultCode.OperationRejected, result == null ? "Resource operation failed." : result.Message, definition, source, context, transactionId, roll);
        }

        private void ProcessChildContext(CombatReactionExecutionResult result, bool execute, CombatReactionChainState state, int depth)
        {
            if (result == null || depth >= state.MaximumDepth)
            {
                return;
            }

            if (result.DamageResult != null)
            {
                if (CombatReactionTriggerContext.TryFromDamageResult(result.DamageResult, CombatReactionTriggerType.DamageApplied, out CombatReactionTriggerContext damageContext))
                {
                    ProcessContext(damageContext, execute, state, depth);
                }

                if (CombatReactionTriggerContext.TryFromDamageResult(result.DamageResult, CombatReactionTriggerType.HealthReachedZero, out CombatReactionTriggerContext zeroContext))
                {
                    ProcessContext(zeroContext, execute, state, depth);
                }
            }

            if (result.HealingResult != null && CombatReactionTriggerContext.TryFromHealingResult(result.HealingResult, CombatReactionTriggerType.HealingApplied, out CombatReactionTriggerContext healingContext))
            {
                ProcessContext(healingContext, execute, state, depth);
            }
        }

        private bool IsCandidate(CombatReactionSourceRegistration registration, CombatReactionTriggerContext context)
        {
            if (registration == null || !registration.Active || registration.Definition == null || context == null)
            {
                return false;
            }

            if (!registration.Definition.SupportsTrigger(context.TriggerType))
            {
                return false;
            }

            if (!MatchesOwnership(registration, context))
            {
                return false;
            }

            if (!MatchesTags(registration.Definition, context))
            {
                return false;
            }

            return true;
        }

        private bool MatchesOwnership(CombatReactionSourceRegistration registration, CombatReactionTriggerContext context)
        {
            switch (registration.Definition.OwnershipSide)
            {
                case CombatReactionOwnershipSide.Any:
                    return true;
                case CombatReactionOwnershipSide.Source:
                    return string.Equals(registration.OwnerActorId, context.SourceActorId, StringComparison.Ordinal);
                case CombatReactionOwnershipSide.Target:
                    return string.Equals(registration.OwnerActorId, context.TargetActorId, StringComparison.Ordinal);
                case CombatReactionOwnershipSide.ReactionOwner:
                    return !string.IsNullOrWhiteSpace(registration.OwnerActorId);
                default:
                    return false;
            }
        }

        private bool MatchesTags(CombatReactionDefinition definition, CombatReactionTriggerContext context)
        {
            if (definition.RequiredContextTags.Count > 0)
            {
                for (int i = 0; i < definition.RequiredContextTags.Count; i++)
                {
                    if (!context.Tags.Contains(definition.RequiredContextTags[i]))
                    {
                        return false;
                    }
                }
            }

            for (int i = 0; i < definition.ExcludedContextTags.Count; i++)
            {
                if (context.Tags.Contains(definition.ExcludedContextTags[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static GameObject ResolveTargetObject(CombatReactionTargetPolicy policy, CombatReactionSourceRegistration source, CombatReactionTriggerContext context)
        {
            switch (policy)
            {
                case CombatReactionTargetPolicy.OriginalSource:
                    return context.SourceObject;
                case CombatReactionTargetPolicy.OriginalTarget:
                    return context.TargetObject;
                case CombatReactionTargetPolicy.ReactionOwner:
                case CombatReactionTargetPolicy.Self:
                    return source.OwnerObject;
                case CombatReactionTargetPolicy.OtherCombatant:
                    return string.Equals(source.OwnerActorId, context.SourceActorId, StringComparison.Ordinal) ? context.TargetObject : context.SourceObject;
                default:
                    return null;
            }
        }

        private static string ResolveTargetActorId(CombatReactionTargetPolicy policy, CombatReactionSourceRegistration source, CombatReactionTriggerContext context, GameObject target)
        {
            switch (policy)
            {
                case CombatReactionTargetPolicy.OriginalSource:
                    return context.SourceActorId;
                case CombatReactionTargetPolicy.OriginalTarget:
                    return context.TargetActorId;
                case CombatReactionTargetPolicy.ReactionOwner:
                case CombatReactionTargetPolicy.Self:
                    return source.OwnerActorId;
                case CombatReactionTargetPolicy.OtherCombatant:
                    return string.Equals(source.OwnerActorId, context.SourceActorId, StringComparison.Ordinal) ? context.TargetActorId : context.SourceActorId;
                case CombatReactionTargetPolicy.ExplicitActor:
                    return ResolveActorId(target);
                default:
                    return string.Empty;
            }
        }

        private static string ResolveActorId(GameObject actorObject)
        {
            if (actorObject == null)
            {
                return string.Empty;
            }

            CharacterSystemCoordinator coordinator = actorObject.GetComponentInParent<CharacterSystemCoordinator>();
            if (coordinator != null && !string.IsNullOrWhiteSpace(coordinator.ActorId))
            {
                return coordinator.ActorId;
            }

            WorldEntityIdentity identity = actorObject.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        private static float CalculateAmount(CombatReactionDefinition definition, CombatReactionTriggerContext context)
        {
            if (definition == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, definition.Amount + definition.Multiplier * (context == null ? 0f : context.Magnitude));
        }

        private static string BuildRegistrationKey(CombatReactionSourceRegistration registration)
        {
            return $"{registration.OwnerActorId}|{registration.SourceKind}|{registration.SourceStableId}|{registration.SourceInstanceId}|{registration.Definition.Id}";
        }

        private static string BuildExecutionKey(CombatReactionSourceRegistration source, CombatReactionDefinition definition)
        {
            return $"{definition.Id}|{source.SourceStableId}|{source.SourceInstanceId}";
        }

        private static string BuildChildTransactionId(string rootTransactionId, CombatReactionTriggerContext context, CombatReactionSourceRegistration source, int depth, int index)
        {
            string root = string.IsNullOrWhiteSpace(rootTransactionId) ? context.TriggerType.ToString() : rootTransactionId;
            return $"{root}.reaction.{depth}.{index}.{source.Definition.Id}.{source.SourceStableId}.{source.SourceInstanceId}";
        }

        private static float CalculateRoll(string rootTransactionId, CombatReactionTriggerType triggerType, string reactionId, string sourceStableId, string sourceInstanceId, int index)
        {
            string key = $"{rootTransactionId}|{triggerType}|{reactionId}|{sourceStableId}|{sourceInstanceId}|{index}";
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < key.Length; i++)
                {
                    hash ^= key[i];
                    hash *= 16777619;
                }

                return (hash & 0x00FFFFFF) / 16777216f;
            }
        }

        private sealed class CombatReactionChainState
        {
            private readonly Dictionary<string, int> executionCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            public CombatReactionChainState(string rootTransactionId, int maximumDepth, int maximumReactionCount)
            {
                RootTransactionId = rootTransactionId ?? string.Empty;
                MaximumDepth = maximumDepth;
                MaximumReactionCount = maximumReactionCount;
            }

            public string RootTransactionId { get; }
            public int MaximumDepth { get; }
            public int MaximumReactionCount { get; }
            public int DeepestDepth { get; set; }
            public List<CombatReactionExecutionResult> Results { get; } = new List<CombatReactionExecutionResult>();

            public bool RememberExecution(string key, int maximumExecutions)
            {
                if (!executionCounts.TryGetValue(key, out int count))
                {
                    executionCounts[key] = 1;
                    return true;
                }

                if (count >= maximumExecutions)
                {
                    return false;
                }

                executionCounts[key] = count + 1;
                return true;
            }
        }
    }
}
