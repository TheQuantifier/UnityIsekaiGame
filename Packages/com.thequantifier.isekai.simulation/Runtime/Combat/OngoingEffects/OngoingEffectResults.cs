using System.Collections.Generic;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat.OngoingEffects
{
    public sealed class OngoingEffectApplicationResult
    {
        private OngoingEffectApplicationResult(
            bool succeeded,
            bool preview,
            bool duplicate,
            string code,
            string message,
            string transactionId,
            string definitionId,
            string instanceId,
            string sourceActorId,
            string targetActorId,
            OngoingEffectApplicationOutcome outcome,
            int previousStackCount,
            int resultingStackCount,
            float previousRemainingDuration,
            float resultingRemainingDuration,
            IReadOnlyList<OngoingEffectTickResult> immediateTicks)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? OngoingEffectResultCode.Success : OngoingEffectResultCode.MissingDefinition : code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            Outcome = outcome;
            PreviousStackCount = previousStackCount;
            ResultingStackCount = resultingStackCount;
            PreviousRemainingDuration = previousRemainingDuration;
            ResultingRemainingDuration = resultingRemainingDuration;
            ImmediateTicks = immediateTicks ?? System.Array.Empty<OngoingEffectTickResult>();
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public string DefinitionId { get; }
        public string InstanceId { get; }
        public string SourceActorId { get; }
        public string TargetActorId { get; }
        public OngoingEffectApplicationOutcome Outcome { get; }
        public int PreviousStackCount { get; }
        public int ResultingStackCount { get; }
        public float PreviousRemainingDuration { get; }
        public float ResultingRemainingDuration { get; }
        public IReadOnlyList<OngoingEffectTickResult> ImmediateTicks { get; }

        public static OngoingEffectApplicationResult Success(bool preview, bool duplicate, string code, string message, string transactionId, string definitionId, string instanceId, string sourceActorId, string targetActorId, OngoingEffectApplicationOutcome outcome, int previousStacks, int resultingStacks, float previousDuration, float resultingDuration, IReadOnlyList<OngoingEffectTickResult> immediateTicks = null)
        {
            return new OngoingEffectApplicationResult(true, preview, duplicate, code, message, transactionId, definitionId, instanceId, sourceActorId, targetActorId, outcome, previousStacks, resultingStacks, previousDuration, resultingDuration, immediateTicks);
        }

        public static OngoingEffectApplicationResult Failure(string code, string message, string transactionId = "", string definitionId = "", string instanceId = "", string sourceActorId = "", string targetActorId = "")
        {
            return new OngoingEffectApplicationResult(false, false, false, code, message, transactionId, definitionId, instanceId, sourceActorId, targetActorId, OngoingEffectApplicationOutcome.Preview, 0, 0, 0f, 0f, null);
        }
    }

    public sealed class OngoingEffectTickResult
    {
        private OngoingEffectTickResult(
            bool succeeded,
            string code,
            string message,
            string instanceId,
            string definitionId,
            int tickIndex,
            string tickTransactionId,
            OngoingEffectOperationType operationType,
            float scheduledElapsedTime,
            float processedElapsedTime,
            float requestedAmount,
            DamageApplicationResult damageResult,
            HealingApplicationResult healingResult,
            ResourceChangeResult resourceResult,
            ActorLifecycleState lifecycleState,
            OngoingEffectTickOutcome outcome,
            bool completed)
        {
            Succeeded = succeeded;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? OngoingEffectResultCode.Success : OngoingEffectResultCode.ResourceRejected : code;
            Message = message ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            TickIndex = tickIndex;
            TickTransactionId = tickTransactionId ?? string.Empty;
            OperationType = operationType;
            ScheduledElapsedTime = scheduledElapsedTime;
            ProcessedElapsedTime = processedElapsedTime;
            RequestedAmount = requestedAmount;
            DamageResult = damageResult;
            HealingResult = healingResult;
            ResourceResult = resourceResult;
            LifecycleState = lifecycleState;
            Outcome = outcome;
            Completed = completed;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public int TickIndex { get; }
        public string TickTransactionId { get; }
        public OngoingEffectOperationType OperationType { get; }
        public float ScheduledElapsedTime { get; }
        public float ProcessedElapsedTime { get; }
        public float RequestedAmount { get; }
        public DamageApplicationResult DamageResult { get; }
        public HealingApplicationResult HealingResult { get; }
        public ResourceChangeResult ResourceResult { get; }
        public ActorLifecycleState LifecycleState { get; }
        public OngoingEffectTickOutcome Outcome { get; }
        public bool Completed { get; }

        public static OngoingEffectTickResult Create(bool succeeded, string code, string message, RuntimeOngoingEffectInstance instance, int tickIndex, string tickTransactionId, float scheduledElapsedTime, float processedElapsedTime, float requestedAmount, DamageApplicationResult damageResult, HealingApplicationResult healingResult, ResourceChangeResult resourceResult, ActorLifecycleState lifecycleState, OngoingEffectTickOutcome outcome, bool completed)
        {
            return new OngoingEffectTickResult(succeeded, code, message, instance == null ? string.Empty : instance.InstanceId, instance == null || instance.Definition == null ? string.Empty : instance.Definition.Id, tickIndex, tickTransactionId, instance == null || instance.Definition == null ? default : instance.Definition.OperationType, scheduledElapsedTime, processedElapsedTime, requestedAmount, damageResult, healingResult, resourceResult, lifecycleState, outcome, completed);
        }
    }

    public sealed class OngoingEffectCancellationResult
    {
        private OngoingEffectCancellationResult(bool succeeded, bool preview, bool duplicate, string code, string message, string transactionId, string instanceId, string definitionId, string targetActorId)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? OngoingEffectResultCode.Success : OngoingEffectResultCode.MissingInstance : code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public string TargetActorId { get; }

        public static OngoingEffectCancellationResult Success(bool preview, bool duplicate, string code, string message, string transactionId, RuntimeOngoingEffectInstance instance)
        {
            return new OngoingEffectCancellationResult(true, preview, duplicate, code, message, transactionId, instance == null ? string.Empty : instance.InstanceId, instance == null || instance.Definition == null ? string.Empty : instance.Definition.Id, instance == null ? string.Empty : instance.TargetActorId);
        }

        public static OngoingEffectCancellationResult Failure(string code, string message, string transactionId = "", string instanceId = "")
        {
            return new OngoingEffectCancellationResult(false, false, false, code, message, transactionId, instanceId, string.Empty, string.Empty);
        }
    }

    public sealed class OngoingEffectProcessResult
    {
        public OngoingEffectProcessResult(float deltaSeconds, int processedTicks, bool capped, IReadOnlyList<OngoingEffectTickResult> tickResults)
        {
            DeltaSeconds = deltaSeconds;
            ProcessedTicks = processedTicks;
            Capped = capped;
            TickResults = tickResults ?? System.Array.Empty<OngoingEffectTickResult>();
        }

        public float DeltaSeconds { get; }
        public int ProcessedTicks { get; }
        public bool Capped { get; }
        public IReadOnlyList<OngoingEffectTickResult> TickResults { get; }
    }
}
