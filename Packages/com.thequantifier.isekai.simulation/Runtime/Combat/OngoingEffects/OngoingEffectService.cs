using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Combat.OngoingEffects
{
    [DisallowMultipleComponent]
    public sealed class OngoingEffectService : MonoBehaviour
    {
        [SerializeField] private CharacterSystemCoordinator defaultTarget;
        [SerializeField] private bool autoProcessWithUnityTime;
        [SerializeField, Min(1)] private int maximumTicksPerProcess = 32;

        private readonly List<RuntimeOngoingEffectInstance> activeInstances = new List<RuntimeOngoingEffectInstance>();
        private readonly HashSet<string> processedApplicationTransactions = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> processedCancellationTransactions = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> processedTickTransactions = new HashSet<string>(StringComparer.Ordinal);
        private IDamageHealingService damageHealingService = new DamageHealingService();
        private float currentTimeSeconds;

        public event Action<OngoingEffectApplicationResult> OngoingEffectApplied;
        public event Action<OngoingEffectApplicationResult> OngoingEffectRefreshed;
        public event Action<OngoingEffectApplicationResult> OngoingEffectStackChanged;
        public event Action<OngoingEffectTickResult> OngoingEffectTickProcessed;
        public event Action<OngoingEffectTickResult> OngoingEffectTickSkipped;
        public event Action<RuntimeOngoingEffectInstance> OngoingEffectPaused;
        public event Action<RuntimeOngoingEffectInstance> OngoingEffectResumed;
        public event Action<OngoingEffectCancellationResult> OngoingEffectCancelled;
        public event Action<RuntimeOngoingEffectInstance> OngoingEffectCompleted;

        public IReadOnlyList<RuntimeOngoingEffectInstance> ActiveInstances => activeInstances.Where(instance => instance != null && !instance.IsTerminal).ToList();
        public int MaximumTicksPerProcess => Mathf.Max(1, maximumTicksPerProcess);
        public float CurrentTimeSeconds => currentTimeSeconds;

        private void Awake()
        {
            if (defaultTarget == null)
            {
                defaultTarget = GetComponentInParent<CharacterSystemCoordinator>();
            }
        }

        private void Update()
        {
            if (autoProcessWithUnityTime)
            {
                ProcessDueTicks(Time.deltaTime);
            }
        }

        public void Configure(CharacterSystemCoordinator target)
        {
            defaultTarget = target == null ? defaultTarget : target;
        }

        public void ConfigureDamageHealing(IDamageHealingService service)
        {
            damageHealingService = service ?? damageHealingService;
        }

        public void SetClock(float nowSeconds)
        {
            currentTimeSeconds = Mathf.Max(0f, nowSeconds);
        }

        public void ClearTransientStateForRestore()
        {
            activeInstances.Clear();
            processedApplicationTransactions.Clear();
            processedCancellationTransactions.Clear();
            processedTickTransactions.Clear();
            currentTimeSeconds = 0f;
        }

        public OngoingEffectApplicationResult PreviewApplyOngoingEffect(OngoingEffectApplicationRequest request)
        {
            return ApplyInternal(request, execute: false);
        }

        public OngoingEffectApplicationResult ApplyOngoingEffect(OngoingEffectApplicationRequest request)
        {
            return ApplyInternal(request, execute: true);
        }

        public OngoingEffectCancellationResult PreviewCancelOngoingEffect(OngoingEffectCancellationRequest request)
        {
            return CancelInternal(request, execute: false);
        }

        public OngoingEffectCancellationResult CancelOngoingEffect(OngoingEffectCancellationRequest request)
        {
            return CancelInternal(request, execute: true);
        }

        public IReadOnlyList<RuntimeOngoingEffectInstance> QueryActiveEffects(GameObject targetObject = null, string targetActorId = "")
        {
            string actorId = !string.IsNullOrWhiteSpace(targetActorId) ? targetActorId : ResolveActorId(targetObject);
            return activeInstances
                .Where(instance => instance != null
                    && !instance.IsTerminal
                    && (string.IsNullOrWhiteSpace(actorId) || string.Equals(instance.TargetActorId, actorId, StringComparison.Ordinal)))
                .OrderBy(instance => instance.Definition == null ? string.Empty : instance.Definition.Id, StringComparer.Ordinal)
                .ThenBy(instance => instance.InstanceId, StringComparer.Ordinal)
                .ToList();
        }

        public OngoingEffectProcessResult AdvanceTime(float deltaSeconds)
        {
            return ProcessDueTicks(deltaSeconds);
        }

        public OngoingEffectProcessResult ProcessDueTicks(float deltaSeconds)
        {
            float delta = Mathf.Max(0f, deltaSeconds);
            currentTimeSeconds += delta;
            List<OngoingEffectTickResult> results = new List<OngoingEffectTickResult>();
            int cap = MaximumTicksPerProcess;
            int processed = 0;
            bool capped = false;

            foreach (RuntimeOngoingEffectInstance instance in activeInstances.OrderBy(instance => instance.InstanceId, StringComparer.Ordinal).ToList())
            {
                if (instance == null || instance.IsTerminal)
                {
                    continue;
                }

                if (!ApplyLifecyclePolicy(instance, out OngoingEffectTickResult lifecycleResult))
                {
                    if (lifecycleResult != null)
                    {
                        results.Add(lifecycleResult);
                    }

                    continue;
                }

                instance.AdvanceElapsed(delta);
                while (instance.HasDueTick)
                {
                    if (processed >= cap)
                    {
                        capped = true;
                        results.Add(OngoingEffectTickResult.Create(false, OngoingEffectResultCode.ProcessingCapReached, "Ongoing effect tick processing cap reached.", instance, instance.CompletedTicks, BuildTickTransactionId(instance, instance.CompletedTicks), instance.NextTickElapsedSeconds, instance.ElapsedSeconds, instance.CurrentTickAmount, null, null, null, ResolveLifecycleState(instance.TargetObject), OngoingEffectTickOutcome.Failed, false));
                        break;
                    }

                    OngoingEffectTickResult tick = ExecuteTick(instance);
                    results.Add(tick);
                    processed++;
                    if (tick.Completed)
                    {
                        break;
                    }
                }

                if (capped)
                {
                    break;
                }

                CompleteIfFinished(instance);
            }

            PruneTerminalInstances();
            return new OngoingEffectProcessResult(delta, processed, capped, results);
        }

        public string BuildTickTransactionId(RuntimeOngoingEffectInstance instance, int tickIndex)
        {
            if (instance == null || instance.Definition == null)
            {
                return string.Empty;
            }

            return $"{instance.InstanceId}.tick.{tickIndex}.{instance.Definition.OperationType}";
        }

        public OngoingEffectsSaveData CreateSaveData(string playerId, string targetActorId)
        {
            OngoingEffectsSaveData saveData = new OngoingEffectsSaveData
            {
                playerId = playerId ?? string.Empty,
                targetActorId = targetActorId ?? string.Empty,
                instances = activeInstances
                    .Where(instance => instance != null && !instance.IsTerminal)
                    .Select(CreateInstanceSaveData)
                    .ToList()
            };
            return saveData;
        }

        public bool RestoreFromSaveData(OngoingEffectsSaveData saveData, DefinitionRegistry registry, GameObject targetObject, string expectedPlayerId, string expectedTargetActorId, out string failureReason, bool restoring)
        {
            failureReason = string.Empty;
            if (!ValidateSaveData(saveData, registry, expectedPlayerId, expectedTargetActorId, out failureReason))
            {
                return false;
            }

            if (targetObject == null)
            {
                failureReason = "Ongoing effect restore target is missing.";
                return false;
            }

            string actualTargetActorId = ResolveActorId(targetObject);
            if (!string.IsNullOrWhiteSpace(expectedTargetActorId) && !string.Equals(actualTargetActorId, expectedTargetActorId, StringComparison.Ordinal))
            {
                failureReason = $"Ongoing effect restore target actor '{actualTargetActorId}' does not match expected '{expectedTargetActorId}'.";
                return false;
            }

            List<RuntimeOngoingEffectInstance> restored = new List<RuntimeOngoingEffectInstance>();
            foreach (OngoingEffectInstanceSaveData record in saveData.instances ?? new List<OngoingEffectInstanceSaveData>())
            {
                if (!registry.TryGet(record.definitionId, out OngoingEffectDefinition definition))
                {
                    failureReason = $"Ongoing effect restore references missing definition '{record.definitionId}'.";
                    return false;
                }

                if (!Enum.TryParse(record.state, out OngoingEffectInstanceState state))
                {
                    failureReason = $"Ongoing effect restore record '{record.instanceId}' has invalid state '{record.state}'.";
                    return false;
                }

                RuntimeOngoingEffectInstance instance = new RuntimeOngoingEffectInstance(
                    definition,
                    record.instanceId,
                    record.applicationTransactionId,
                    record.sourceActorId,
                    null,
                    record.targetActorId,
                    targetObject,
                    record.originId,
                    record.amountPerTick,
                    record.tickInterval,
                    record.totalDuration,
                    record.finiteTickCount,
                    record.stackCount);
                instance.RestoreMutableState(record.elapsedSeconds, record.nextTickElapsedSeconds, record.completedTicks, record.stackCount, state, record.revision);
                restored.Add(instance);
            }

            activeInstances.Clear();
            activeInstances.AddRange(restored);
            PruneTerminalInstances();
            return true;
        }

        public static bool ValidateSaveData(OngoingEffectsSaveData saveData, DefinitionRegistry registry, string expectedPlayerId, string expectedTargetActorId, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Ongoing effects save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != OngoingEffectsSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported ongoing effects schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedPlayerId) && !string.Equals(saveData.playerId, expectedPlayerId, StringComparison.Ordinal))
            {
                failureReason = $"Saved ongoing effect owner '{saveData.playerId}' does not match current player '{expectedPlayerId}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedTargetActorId) && !string.Equals(saveData.targetActorId, expectedTargetActorId, StringComparison.Ordinal))
            {
                failureReason = $"Saved ongoing effect target '{saveData.targetActorId}' does not match current actor '{expectedTargetActorId}'.";
                return false;
            }

            if (registry == null)
            {
                failureReason = "Definition registry is not available for ongoing effect restore.";
                return false;
            }

            HashSet<string> instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (OngoingEffectInstanceSaveData record in saveData.instances ?? new List<OngoingEffectInstanceSaveData>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.instanceId))
                {
                    failureReason = "Ongoing effect save record has no instance ID.";
                    return false;
                }

                if (!instanceIds.Add(record.instanceId))
                {
                    failureReason = $"Duplicate ongoing effect instance ID '{record.instanceId}' in save data.";
                    return false;
                }

                if (!registry.TryGet(record.definitionId, out OngoingEffectDefinition definition) || definition == null)
                {
                    failureReason = $"Ongoing effect save record '{record.instanceId}' references unknown definition '{record.definitionId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(record.targetActorId) || (!string.IsNullOrWhiteSpace(expectedTargetActorId) && !string.Equals(record.targetActorId, expectedTargetActorId, StringComparison.Ordinal)))
                {
                    failureReason = $"Ongoing effect save record '{record.instanceId}' targets stale actor '{record.targetActorId}'.";
                    return false;
                }

                if (!IsFinite(record.amountPerTick) || record.amountPerTick <= 0f || !IsFinite(record.tickInterval) || record.tickInterval <= 0f || !IsFinite(record.elapsedSeconds) || !IsFinite(record.nextTickElapsedSeconds))
                {
                    failureReason = $"Ongoing effect save record '{record.instanceId}' has invalid schedule or amount.";
                    return false;
                }
            }

            return true;
        }

        private OngoingEffectApplicationResult ApplyInternal(OngoingEffectApplicationRequest request, bool execute)
        {
            if (!ValidateApplication(request, out TargetRuntime target, out string code, out string message))
            {
                return OngoingEffectApplicationResult.Failure(code, message, request.TransactionId, request.Definition == null ? string.Empty : request.Definition.Id, string.Empty, request.SourceActorId, request.TargetActorId);
            }

            OngoingEffectDefinition definition = request.Definition;
            RuntimeOngoingEffectInstance existing = FindMatchingInstance(request, target.ActorId);
            if (!execute)
            {
                OngoingEffectApplicationOutcome previewOutcome = existing == null || definition.StackingPolicy == OngoingEffectStackingPolicy.IndependentInstances
                    ? OngoingEffectApplicationOutcome.Created
                    : PreviewStackingOutcome(definition);
                return OngoingEffectApplicationResult.Success(true, false, OngoingEffectResultCode.Preview, "Ongoing effect application preview calculated without mutation.", request.TransactionId, definition.Id, existing == null ? string.Empty : existing.InstanceId, request.SourceActorId, target.ActorId, previewOutcome, existing == null ? 0 : existing.StackCount, ResolvePreviewStackCount(definition, existing, request.StackCount), existing == null ? 0f : existing.RemainingDuration, definition.ResolveDuration(request.DurationOverride), null);
            }

            if (!string.IsNullOrWhiteSpace(request.TransactionId) && !processedApplicationTransactions.Add(request.TransactionId))
            {
                return OngoingEffectApplicationResult.Success(false, true, OngoingEffectResultCode.DuplicateApplication, "Duplicate ongoing effect application ignored.", request.TransactionId, definition.Id, existing == null ? string.Empty : existing.InstanceId, request.SourceActorId, target.ActorId, OngoingEffectApplicationOutcome.DuplicateRejected, existing == null ? 0 : existing.StackCount, existing == null ? 0 : existing.StackCount, existing == null ? 0f : existing.RemainingDuration, existing == null ? 0f : existing.RemainingDuration, null);
            }

            if (existing != null && definition.StackingPolicy != OngoingEffectStackingPolicy.IndependentInstances)
            {
                return ApplyStackingPolicy(request, target, existing);
            }

            RuntimeOngoingEffectInstance created = CreateInstance(request, target);
            activeInstances.Add(created);
            OngoingEffectApplicationResult result = OngoingEffectApplicationResult.Success(false, false, OngoingEffectResultCode.Success, $"Applied ongoing effect {definition.DisplayName}.", request.TransactionId, definition.Id, created.InstanceId, request.SourceActorId, target.ActorId, OngoingEffectApplicationOutcome.Created, 0, created.StackCount, 0f, created.RemainingDuration, null);
            OngoingEffectApplied?.Invoke(result);

            if (definition.TickImmediately)
            {
                OngoingEffectProcessResult immediate = ProcessDueTicks(0f);
                return OngoingEffectApplicationResult.Success(false, false, OngoingEffectResultCode.Success, result.Message, request.TransactionId, definition.Id, created.InstanceId, request.SourceActorId, target.ActorId, OngoingEffectApplicationOutcome.Created, 0, created.StackCount, 0f, created.RemainingDuration, immediate.TickResults);
            }

            return result;
        }

        private OngoingEffectApplicationResult ApplyStackingPolicy(OngoingEffectApplicationRequest request, TargetRuntime target, RuntimeOngoingEffectInstance existing)
        {
            OngoingEffectDefinition definition = request.Definition;
            int previousStacks = existing.StackCount;
            float previousDuration = existing.RemainingDuration;
            switch (definition.StackingPolicy)
            {
                case OngoingEffectStackingPolicy.RejectDuplicate:
                    return OngoingEffectApplicationResult.Failure(OngoingEffectResultCode.DuplicateRejected, $"{definition.DisplayName} is already active.", request.TransactionId, definition.Id, existing.InstanceId, request.SourceActorId, target.ActorId);
                case OngoingEffectStackingPolicy.RefreshDuration:
                    if (definition.RefreshDurationOnReapply)
                    {
                        existing.RefreshDuration();
                    }

                    OngoingEffectApplicationResult refresh = OngoingEffectApplicationResult.Success(false, false, OngoingEffectResultCode.Success, $"Refreshed {definition.DisplayName}.", request.TransactionId, definition.Id, existing.InstanceId, request.SourceActorId, target.ActorId, OngoingEffectApplicationOutcome.Refreshed, previousStacks, existing.StackCount, previousDuration, existing.RemainingDuration);
                    OngoingEffectRefreshed?.Invoke(refresh);
                    return refresh;
                case OngoingEffectStackingPolicy.ReplaceExisting:
                    existing.Cancel();
                    RuntimeOngoingEffectInstance replacement = CreateInstance(request, target);
                    activeInstances.Add(replacement);
                    OngoingEffectApplicationResult replaced = OngoingEffectApplicationResult.Success(false, false, OngoingEffectResultCode.Success, $"Replaced {definition.DisplayName}.", request.TransactionId, definition.Id, replacement.InstanceId, request.SourceActorId, target.ActorId, OngoingEffectApplicationOutcome.Replaced, previousStacks, replacement.StackCount, previousDuration, replacement.RemainingDuration);
                    OngoingEffectCancelled?.Invoke(OngoingEffectCancellationResult.Success(false, false, OngoingEffectResultCode.Success, "Existing ongoing effect replaced.", request.TransactionId, existing));
                    OngoingEffectApplied?.Invoke(replaced);
                    return replaced;
                case OngoingEffectStackingPolicy.AddStacks:
                    if (!existing.AddStacks(request.StackCount))
                    {
                        return OngoingEffectApplicationResult.Failure(OngoingEffectResultCode.MaximumStacksReached, $"{definition.DisplayName} is already at maximum stacks.", request.TransactionId, definition.Id, existing.InstanceId, request.SourceActorId, target.ActorId);
                    }

                    if (definition.RefreshDurationOnReapply)
                    {
                        existing.RefreshDuration();
                    }

                    OngoingEffectApplicationResult stacked = OngoingEffectApplicationResult.Success(false, false, OngoingEffectResultCode.Success, $"Added stack to {definition.DisplayName}.", request.TransactionId, definition.Id, existing.InstanceId, request.SourceActorId, target.ActorId, OngoingEffectApplicationOutcome.StackAdded, previousStacks, existing.StackCount, previousDuration, existing.RemainingDuration);
                    OngoingEffectStackChanged?.Invoke(stacked);
                    return stacked;
                default:
                    return OngoingEffectApplicationResult.Failure(OngoingEffectResultCode.DuplicateRejected, $"{definition.DisplayName} is already active.", request.TransactionId, definition.Id, existing.InstanceId, request.SourceActorId, target.ActorId);
            }
        }

        private OngoingEffectCancellationResult CancelInternal(OngoingEffectCancellationRequest request, bool execute)
        {
            RuntimeOngoingEffectInstance instance = activeInstances.FirstOrDefault(candidate => candidate != null && !candidate.IsTerminal && string.Equals(candidate.InstanceId, request.InstanceId, StringComparison.Ordinal));
            if (instance == null)
            {
                return OngoingEffectCancellationResult.Failure(OngoingEffectResultCode.MissingInstance, $"Ongoing effect instance '{request.InstanceId}' was not found.", request.TransactionId, request.InstanceId);
            }

            if (!string.IsNullOrWhiteSpace(request.TargetActorId) && !string.Equals(request.TargetActorId, instance.TargetActorId, StringComparison.Ordinal))
            {
                return OngoingEffectCancellationResult.Failure(OngoingEffectResultCode.StaleTarget, "Cancellation target actor does not match the instance target.", request.TransactionId, request.InstanceId);
            }

            if (request.TargetObject != null && !string.Equals(ResolveActorId(request.TargetObject), instance.TargetActorId, StringComparison.Ordinal))
            {
                return OngoingEffectCancellationResult.Failure(OngoingEffectResultCode.StaleTarget, "Cancellation target object no longer resolves to the instance target.", request.TransactionId, request.InstanceId);
            }

            if (!execute)
            {
                return OngoingEffectCancellationResult.Success(true, false, OngoingEffectResultCode.Preview, "Ongoing effect cancellation preview calculated without mutation.", request.TransactionId, instance);
            }

            if (!string.IsNullOrWhiteSpace(request.TransactionId) && !processedCancellationTransactions.Add(request.TransactionId))
            {
                return OngoingEffectCancellationResult.Success(false, true, OngoingEffectResultCode.DuplicateCancellation, "Duplicate ongoing effect cancellation ignored.", request.TransactionId, instance);
            }

            instance.Cancel();
            OngoingEffectCancellationResult result = OngoingEffectCancellationResult.Success(false, false, OngoingEffectResultCode.Success, string.IsNullOrWhiteSpace(request.Reason) ? "Ongoing effect cancelled." : request.Reason, request.TransactionId, instance);
            OngoingEffectCancelled?.Invoke(result);
            PruneTerminalInstances();
            return result;
        }

        private OngoingEffectTickResult ExecuteTick(RuntimeOngoingEffectInstance instance)
        {
            int tickIndex = instance.CompletedTicks;
            float scheduled = instance.NextTickElapsedSeconds;
            string tickTransactionId = BuildTickTransactionId(instance, tickIndex);
            if (!processedTickTransactions.Add(tickTransactionId))
            {
                instance.ReserveDueTick();
                OngoingEffectTickResult duplicate = OngoingEffectTickResult.Create(true, OngoingEffectResultCode.DuplicateTick, "Duplicate ongoing effect tick ignored.", instance, tickIndex, tickTransactionId, scheduled, instance.ElapsedSeconds, instance.CurrentTickAmount, null, null, null, ResolveLifecycleState(instance.TargetObject), OngoingEffectTickOutcome.Duplicate, false);
                OngoingEffectTickSkipped?.Invoke(duplicate);
                return duplicate;
            }

            instance.ReserveDueTick();
            OngoingEffectTickResult result = ExecuteTickOperation(instance, tickIndex, tickTransactionId, scheduled);
            if (!result.Succeeded && result.Outcome != OngoingEffectTickOutcome.Prevented)
            {
                processedTickTransactions.Remove(tickTransactionId);
                instance.RollbackReservedTick();
            }

            if (result.Succeeded)
            {
                OngoingEffectTickProcessed?.Invoke(result);
            }
            else
            {
                OngoingEffectTickSkipped?.Invoke(result);
            }

            CompleteIfFinished(instance);
            return result;
        }

        private OngoingEffectTickResult ExecuteTickOperation(RuntimeOngoingEffectInstance instance, int tickIndex, string tickTransactionId, float scheduled)
        {
            OngoingEffectDefinition definition = instance.Definition;
            ActorLifecycleState lifecycleState = ResolveLifecycleState(instance.TargetObject);
            float amount = instance.CurrentTickAmount;
            switch (definition.OperationType)
            {
                case OngoingEffectOperationType.Damage:
                    DamageApplicationResult damage = damageHealingService.ApplyDamage(new DamageApplicationRequest(tickTransactionId, instance.SourceActorId, instance.SourceObject, instance.TargetActorId, instance.TargetObject, definition.DamageType, amount, definition.Id, authorityValidated: true));
                    return OngoingEffectTickResult.Create(damage.Succeeded, damage.Succeeded ? damage.Code : OngoingEffectResultCode.DamageHealingRejected, damage.Message, instance, tickIndex, tickTransactionId, scheduled, instance.ElapsedSeconds, amount, damage, null, damage.ResourceResult, lifecycleState, damage.Succeeded && damage.HealthChanged ? OngoingEffectTickOutcome.Applied : OngoingEffectTickOutcome.Prevented, false);
                case OngoingEffectOperationType.Healing:
                    HealingApplicationResult healing = damageHealingService.ApplyHealing(new HealingApplicationRequest(tickTransactionId, instance.SourceActorId, instance.SourceObject, instance.TargetActorId, instance.TargetObject, amount, definition.Id, authorityValidated: true));
                    return OngoingEffectTickResult.Create(healing.Succeeded, healing.Succeeded ? healing.Code : OngoingEffectResultCode.DamageHealingRejected, healing.Message, instance, tickIndex, tickTransactionId, scheduled, instance.ElapsedSeconds, amount, null, healing, healing.ResourceResult, lifecycleState, healing.Succeeded && healing.HealthChanged ? OngoingEffectTickOutcome.Applied : OngoingEffectTickOutcome.Prevented, false);
                case OngoingEffectOperationType.ResourceGain:
                case OngoingEffectOperationType.ResourceSpend:
                    return ExecuteResourceTick(instance, tickIndex, tickTransactionId, scheduled, amount, lifecycleState);
                default:
                    return OngoingEffectTickResult.Create(false, OngoingEffectResultCode.InvalidSchedule, "Unsupported ongoing effect operation.", instance, tickIndex, tickTransactionId, scheduled, instance.ElapsedSeconds, amount, null, null, null, lifecycleState, OngoingEffectTickOutcome.Failed, false);
            }
        }

        private OngoingEffectTickResult ExecuteResourceTick(RuntimeOngoingEffectInstance instance, int tickIndex, string tickTransactionId, float scheduled, float amount, ActorLifecycleState lifecycleState)
        {
            if (instance.TargetObject == null)
            {
                return OngoingEffectTickResult.Create(false, OngoingEffectResultCode.MissingTarget, "Ongoing effect target is missing.", instance, tickIndex, tickTransactionId, scheduled, instance.ElapsedSeconds, amount, null, null, null, lifecycleState, OngoingEffectTickOutcome.Failed, false);
            }

            CharacterResourceCollection resources = ResolveResources(instance.TargetObject);
            if (resources == null || !resources.HasResource(instance.Definition.TargetResourceId))
            {
                return OngoingEffectTickResult.Create(false, OngoingEffectResultCode.MissingResource, $"Target Resource '{instance.Definition.TargetResourceId}' is missing.", instance, tickIndex, tickTransactionId, scheduled, instance.ElapsedSeconds, amount, null, null, null, lifecycleState, OngoingEffectTickOutcome.Failed, false);
            }

            ResourceChangeOperation operation = instance.Definition.OperationType == OngoingEffectOperationType.ResourceSpend
                ? ResourceChangeOperation.Spend
                : ResourceChangeOperation.Gain;
            ResourceChangeResult resource = resources.ApplyChange(new ResourceChangeRequest(instance.Definition.TargetResourceId, operation, amount, ResourceChangeSourceCategory.Regeneration, instance.SourceActorId, instance.Definition.Id, tickTransactionId, allowPartial: true, authorityValidated: true));
            bool applied = resource != null && resource.Succeeded && resource.AppliedAmount > CharacterResourceCollection.Epsilon;
            return OngoingEffectTickResult.Create(resource != null && resource.Succeeded, resource == null ? OngoingEffectResultCode.ResourceRejected : resource.Code, resource == null ? "Resource mutation failed." : resource.Message, instance, tickIndex, tickTransactionId, scheduled, instance.ElapsedSeconds, amount, null, null, resource, lifecycleState, applied ? OngoingEffectTickOutcome.Applied : OngoingEffectTickOutcome.Prevented, false);
        }

        private bool ApplyLifecyclePolicy(RuntimeOngoingEffectInstance instance, out OngoingEffectTickResult result)
        {
            result = null;
            ActorLifecycleState state = ResolveLifecycleState(instance.TargetObject);
            if (state == ActorLifecycleState.Dead)
            {
                if (instance.Definition.DeathPolicy == OngoingEffectDeathPolicy.CancelOnDeath)
                {
                    instance.Cancel();
                    result = OngoingEffectTickResult.Create(true, OngoingEffectResultCode.InvalidLifecycleState, "Ongoing effect cancelled because target is dead.", instance, instance.CompletedTicks, BuildTickTransactionId(instance, instance.CompletedTicks), instance.NextTickElapsedSeconds, instance.ElapsedSeconds, 0f, null, null, null, state, OngoingEffectTickOutcome.Cancelled, true);
                    OngoingEffectTickSkipped?.Invoke(result);
                    OngoingEffectCancelled?.Invoke(OngoingEffectCancellationResult.Success(false, false, OngoingEffectResultCode.Success, "Cancelled on death.", string.Empty, instance));
                    return false;
                }

                if (instance.Definition.DeathPolicy == OngoingEffectDeathPolicy.PauseAfterDeath)
                {
                    instance.Pause();
                    OngoingEffectPaused?.Invoke(instance);
                    result = OngoingEffectTickResult.Create(true, OngoingEffectResultCode.InvalidLifecycleState, "Ongoing effect paused because target is dead.", instance, instance.CompletedTicks, BuildTickTransactionId(instance, instance.CompletedTicks), instance.NextTickElapsedSeconds, instance.ElapsedSeconds, 0f, null, null, null, state, OngoingEffectTickOutcome.Paused, false);
                    OngoingEffectTickSkipped?.Invoke(result);
                    return false;
                }
            }
            else if (state == ActorLifecycleState.Unconscious || state == ActorLifecycleState.Defeated)
            {
                if (instance.Definition.UnconsciousPolicy == OngoingEffectUnconsciousPolicy.CancelWhenUnconscious)
                {
                    instance.Cancel();
                    result = OngoingEffectTickResult.Create(true, OngoingEffectResultCode.InvalidLifecycleState, "Ongoing effect cancelled because target is unconscious or defeated.", instance, instance.CompletedTicks, BuildTickTransactionId(instance, instance.CompletedTicks), instance.NextTickElapsedSeconds, instance.ElapsedSeconds, 0f, null, null, null, state, OngoingEffectTickOutcome.Cancelled, true);
                    OngoingEffectTickSkipped?.Invoke(result);
                    OngoingEffectCancelled?.Invoke(OngoingEffectCancellationResult.Success(false, false, OngoingEffectResultCode.Success, "Cancelled on unconsciousness.", string.Empty, instance));
                    return false;
                }

                if (instance.Definition.UnconsciousPolicy == OngoingEffectUnconsciousPolicy.PauseWhileUnconscious)
                {
                    instance.Pause();
                    OngoingEffectPaused?.Invoke(instance);
                    result = OngoingEffectTickResult.Create(true, OngoingEffectResultCode.InvalidLifecycleState, "Ongoing effect paused because target is unconscious or defeated.", instance, instance.CompletedTicks, BuildTickTransactionId(instance, instance.CompletedTicks), instance.NextTickElapsedSeconds, instance.ElapsedSeconds, 0f, null, null, null, state, OngoingEffectTickOutcome.Paused, false);
                    OngoingEffectTickSkipped?.Invoke(result);
                    return false;
                }
            }

            if (instance.State == OngoingEffectInstanceState.Paused)
            {
                instance.Resume();
                OngoingEffectResumed?.Invoke(instance);
            }

            return true;
        }

        private void CompleteIfFinished(RuntimeOngoingEffectInstance instance)
        {
            if (instance == null || instance.IsTerminal)
            {
                return;
            }

            bool countComplete = instance.HasTickCountLimit && instance.CompletedTicks >= instance.FiniteTickCount;
            bool durationComplete = instance.HasDurationLimit && instance.ElapsedSeconds >= instance.TotalDuration - CharacterResourceCollection.Epsilon && !instance.HasDueTick;
            if (!countComplete && !durationComplete)
            {
                return;
            }

            instance.Complete();
            OngoingEffectCompleted?.Invoke(instance);
        }

        private bool ValidateApplication(OngoingEffectApplicationRequest request, out TargetRuntime target, out string code, out string message)
        {
            target = default;
            code = string.Empty;
            message = string.Empty;
            OngoingEffectDefinition definition = request.Definition;
            if (definition == null)
            {
                code = OngoingEffectResultCode.MissingDefinition;
                message = "Ongoing effect definition is missing.";
                return false;
            }

            if (!IsFinite(definition.AmountPerTick) || definition.ResolveAmount(request.AmountOverride, 1) <= 0f)
            {
                code = OngoingEffectResultCode.InvalidAmount;
                message = $"{definition.DisplayName} has no positive tick amount.";
                return false;
            }

            if (!IsFinite(definition.ResolveInterval(request.IntervalOverride)) || definition.ResolveInterval(request.IntervalOverride) <= 0f)
            {
                code = OngoingEffectResultCode.InvalidSchedule;
                message = $"{definition.DisplayName} has no positive tick interval.";
                return false;
            }

            if (definition.OperationType == OngoingEffectOperationType.Damage && definition.DamageType == null)
            {
                code = OngoingEffectResultCode.MissingDamageType;
                message = $"{definition.DisplayName} has no Damage Type.";
                return false;
            }

            if (!TryResolveTarget(request.TargetObject, request.TargetActorId, out target, out code, out message))
            {
                return false;
            }

            string resolvedSourceActorId = ResolveActorId(request.SourceObject);
            if (!string.IsNullOrWhiteSpace(request.SourceActorId) && !string.IsNullOrWhiteSpace(resolvedSourceActorId) && !string.Equals(request.SourceActorId, resolvedSourceActorId, StringComparison.Ordinal))
            {
                code = OngoingEffectResultCode.InvalidSource;
                message = $"Source actor identity '{request.SourceActorId}' no longer resolves to '{resolvedSourceActorId}'.";
                return false;
            }

            if (definition.OperationType == OngoingEffectOperationType.Damage || definition.OperationType == OngoingEffectOperationType.Healing)
            {
                if (!string.Equals(definition.TargetResourceId, ResourceIds.Health, StringComparison.Ordinal))
                {
                    code = OngoingEffectResultCode.MissingResource;
                    message = "Damage and healing ongoing effects must target Health.";
                    return false;
                }
            }

            if ((definition.OperationType == OngoingEffectOperationType.ResourceGain || definition.OperationType == OngoingEffectOperationType.ResourceSpend)
                && (target.Resources == null || !target.Resources.HasResource(definition.TargetResourceId)))
            {
                code = OngoingEffectResultCode.MissingResource;
                message = $"Target Resource '{definition.TargetResourceId}' is missing.";
                return false;
            }

            if (!RequirementsPass(definition, target, out message))
            {
                code = OngoingEffectResultCode.RequirementRejected;
                return false;
            }

            if (!CapabilitiesPass(definition, target, out message))
            {
                code = OngoingEffectResultCode.CapabilityRejected;
                return false;
            }

            return true;
        }

        private bool TryResolveTarget(GameObject targetObject, string requestedTargetActorId, out TargetRuntime target, out string failureCode, out string failureMessage)
        {
            target = default;
            failureCode = string.Empty;
            failureMessage = string.Empty;
            GameObject resolvedObject = targetObject == null && defaultTarget != null ? defaultTarget.gameObject : targetObject;
            if (resolvedObject == null)
            {
                failureCode = OngoingEffectResultCode.MissingTarget;
                failureMessage = "Ongoing effect target is missing.";
                return false;
            }

            CharacterSystemCoordinator character = resolvedObject.GetComponentInParent<CharacterSystemCoordinator>();
            if (character != null && character.Readiness == CharacterReadinessState.Failed)
            {
                failureCode = OngoingEffectResultCode.TargetNotReady;
                failureMessage = "Target Character System is failed.";
                return false;
            }

            CharacterResourceCollection resources = ResolveResources(resolvedObject);
            string actorId = ResolveActorId(resolvedObject);
            if (string.IsNullOrWhiteSpace(actorId))
            {
                failureCode = OngoingEffectResultCode.MissingTarget;
                failureMessage = "Ongoing effect target actor identity is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(requestedTargetActorId) && !string.Equals(requestedTargetActorId, actorId, StringComparison.Ordinal))
            {
                failureCode = OngoingEffectResultCode.StaleTarget;
                failureMessage = $"Target actor identity '{requestedTargetActorId}' no longer resolves to '{actorId}'.";
                return false;
            }

            target = new TargetRuntime(
                resolvedObject,
                actorId,
                resources,
                character,
                character == null ? resolvedObject.GetComponentInParent<CharacterTraitCollection>() : character.Traits,
                character == null ? resolvedObject.GetComponentInParent<CharacterCapabilityCollection>() : character.Capabilities,
                character == null ? resolvedObject.GetComponentInParent<ActorBodyRuntime>() : character.Body);
            return true;
        }

        private RuntimeOngoingEffectInstance CreateInstance(OngoingEffectApplicationRequest request, TargetRuntime target)
        {
            OngoingEffectDefinition definition = request.Definition;
            return new RuntimeOngoingEffectInstance(
                definition,
                $"ongoing.{definition.Id}.{Guid.NewGuid():N}",
                request.TransactionId,
                string.IsNullOrWhiteSpace(request.SourceActorId) ? ResolveActorId(request.SourceObject) : request.SourceActorId,
                request.SourceObject,
                target.ActorId,
                target.GameObject,
                request.OriginId,
                definition.ResolveAmount(request.AmountOverride, 1),
                definition.ResolveInterval(request.IntervalOverride),
                definition.ResolveDuration(request.DurationOverride),
                definition.ResolveTickCount(request.TickCountOverride),
                Mathf.Max(1, request.StackCount));
        }

        private RuntimeOngoingEffectInstance FindMatchingInstance(OngoingEffectApplicationRequest request, string targetActorId)
        {
            if (request.Definition == null)
            {
                return null;
            }

            string sourceActorId = string.IsNullOrWhiteSpace(request.SourceActorId) ? ResolveActorId(request.SourceObject) : request.SourceActorId;
            foreach (RuntimeOngoingEffectInstance instance in activeInstances)
            {
                if (instance == null || instance.IsTerminal || instance.Definition == null)
                {
                    continue;
                }

                if (!string.Equals(instance.Definition.Id, request.Definition.Id, StringComparison.Ordinal) || !string.Equals(instance.TargetActorId, targetActorId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (request.Definition.SourceOwnership == OngoingEffectSourceOwnership.SourceSpecific && !string.Equals(instance.SourceActorId, sourceActorId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (request.Definition.SourceOwnership == OngoingEffectSourceOwnership.OriginSpecific && !string.Equals(instance.OriginId, request.OriginId, StringComparison.Ordinal))
                {
                    continue;
                }

                return instance;
            }

            return null;
        }

        private static OngoingEffectApplicationOutcome PreviewStackingOutcome(OngoingEffectDefinition definition)
        {
            return definition.StackingPolicy switch
            {
                OngoingEffectStackingPolicy.RefreshDuration => OngoingEffectApplicationOutcome.Refreshed,
                OngoingEffectStackingPolicy.ReplaceExisting => OngoingEffectApplicationOutcome.Replaced,
                OngoingEffectStackingPolicy.AddStacks => OngoingEffectApplicationOutcome.StackAdded,
                OngoingEffectStackingPolicy.RejectDuplicate => OngoingEffectApplicationOutcome.DuplicateRejected,
                _ => OngoingEffectApplicationOutcome.Created
            };
        }

        private static int ResolvePreviewStackCount(OngoingEffectDefinition definition, RuntimeOngoingEffectInstance existing, int addedStacks)
        {
            if (existing == null)
            {
                return Mathf.Clamp(Mathf.Max(1, addedStacks), 1, definition.MaximumStacks);
            }

            return definition.StackingPolicy == OngoingEffectStackingPolicy.AddStacks
                ? Mathf.Clamp(existing.StackCount + Mathf.Max(1, addedStacks), 1, definition.MaximumStacks)
                : existing.StackCount;
        }

        private bool RequirementsPass(OngoingEffectDefinition definition, TargetRuntime target, out string summary)
        {
            summary = string.Empty;
            if (definition == null || definition.Requirements == null)
            {
                return true;
            }

            RequirementEvaluationResult result = target.Character == null
                ? CapabilityRequirementEvaluator.Evaluate(definition.Requirements, new RequirementEvaluationContext { Resources = target.Resources, Traits = target.Traits, Capabilities = target.Capabilities, Body = target.Body })
                : target.Character.Query.EvaluateRequirement(definition.Requirements);
            summary = string.Join("; ", result.TestLabFailureReasons);
            return result.Passed;
        }

        private static bool CapabilitiesPass(OngoingEffectDefinition definition, TargetRuntime target, out string summary)
        {
            summary = string.Empty;
            if (definition == null || definition.RequiredCapabilityIds == null || definition.RequiredCapabilityIds.Count == 0)
            {
                return true;
            }

            if (target.Capabilities == null)
            {
                summary = "Target has no capability runtime.";
                return false;
            }

            foreach (string capabilityId in definition.RequiredCapabilityIds)
            {
                CapabilitySnapshot snapshot = target.Capabilities == null ? null : target.Capabilities.Evaluate(capabilityId);
                if (snapshot == null || snapshot.Blocked || !snapshot.BooleanValue)
                {
                    summary = $"Required Capability '{capabilityId}' is missing, false, or blocked.";
                    return false;
                }
            }

            return true;
        }

        private static CharacterResourceCollection ResolveResources(GameObject gameObject)
        {
            CharacterSystemCoordinator character = gameObject == null ? null : gameObject.GetComponentInParent<CharacterSystemCoordinator>();
            return character == null ? gameObject == null ? null : gameObject.GetComponentInParent<CharacterResourceCollection>() : character.Resources;
        }

        private static ActorLifecycleState ResolveLifecycleState(GameObject gameObject)
        {
            ActorLifecycleController lifecycle = gameObject == null ? null : gameObject.GetComponentInParent<ActorLifecycleController>();
            return lifecycle == null ? ActorLifecycleState.Active : lifecycle.State;
        }

        private static string ResolveActorId(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            CharacterSystemCoordinator character = gameObject.GetComponentInParent<CharacterSystemCoordinator>();
            if (character != null && !string.IsNullOrWhiteSpace(character.ActorId))
            {
                return character.ActorId;
            }

            WorldEntityIdentity identity = gameObject.GetComponentInParent<WorldEntityIdentity>();
            return identity == null ? string.Empty : identity.EntityId;
        }

        private void PruneTerminalInstances()
        {
            for (int i = activeInstances.Count - 1; i >= 0; i--)
            {
                if (activeInstances[i] == null || activeInstances[i].IsTerminal)
                {
                    activeInstances.RemoveAt(i);
                }
            }
        }

        private static OngoingEffectInstanceSaveData CreateInstanceSaveData(RuntimeOngoingEffectInstance instance)
        {
            return new OngoingEffectInstanceSaveData
            {
                instanceId = instance.InstanceId,
                definitionId = instance.Definition == null ? string.Empty : instance.Definition.Id,
                sourceActorId = instance.SourceActorId,
                targetActorId = instance.TargetActorId,
                originId = instance.OriginId,
                applicationTransactionId = instance.ApplicationTransactionId,
                amountPerTick = instance.AmountPerTick,
                tickInterval = instance.TickInterval,
                totalDuration = instance.TotalDuration,
                finiteTickCount = instance.FiniteTickCount,
                elapsedSeconds = instance.ElapsedSeconds,
                nextTickElapsedSeconds = instance.NextTickElapsedSeconds,
                completedTicks = instance.CompletedTicks,
                stackCount = instance.StackCount,
                state = instance.State.ToString(),
                revision = instance.Revision
            };
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct TargetRuntime
        {
            public TargetRuntime(GameObject gameObject, string actorId, CharacterResourceCollection resources, CharacterSystemCoordinator character, CharacterTraitCollection traits, CharacterCapabilityCollection capabilities, ActorBodyRuntime body)
            {
                GameObject = gameObject;
                ActorId = actorId ?? string.Empty;
                Resources = resources;
                Character = character;
                Traits = traits;
                Capabilities = capabilities;
                Body = body;
            }

            public GameObject GameObject { get; }
            public string ActorId { get; }
            public CharacterResourceCollection Resources { get; }
            public CharacterSystemCoordinator Character { get; }
            public CharacterTraitCollection Traits { get; }
            public CharacterCapabilityCollection Capabilities { get; }
            public ActorBodyRuntime Body { get; }
        }
    }
}
