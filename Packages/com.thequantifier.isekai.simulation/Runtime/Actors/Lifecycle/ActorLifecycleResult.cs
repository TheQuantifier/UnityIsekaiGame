using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.ActorLifecycle
{
    public sealed class ActorLifecycleResult
    {
        private ActorLifecycleResult(
            bool succeeded,
            bool preview,
            bool duplicate,
            string code,
            string message,
            string transactionId,
            string sourceActorId,
            string targetActorId,
            string policyId,
            LifecycleTransitionKind transition,
            LifecycleTriggerKind trigger,
            ActorLifecycleState previousState,
            ActorLifecycleState resultingState,
            DefeatPolicyOutcome policyOutcome,
            float oldHealth,
            float newHealth,
            float healthMinimum,
            float healthMaximum,
            float requestedHealthRestore,
            float appliedHealthRestore,
            float policyMinimumHealth,
            string requirementSummary,
            long revision,
            ResourceChangeResult resourceResult)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? ActorLifecycleResultCode.Success : ActorLifecycleResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            PolicyId = policyId ?? string.Empty;
            Transition = transition;
            Trigger = trigger;
            PreviousState = previousState;
            ResultingState = resultingState;
            PolicyOutcome = policyOutcome;
            OldHealth = oldHealth;
            NewHealth = newHealth;
            HealthMinimum = healthMinimum;
            HealthMaximum = healthMaximum;
            RequestedHealthRestore = requestedHealthRestore;
            AppliedHealthRestore = appliedHealthRestore;
            PolicyMinimumHealth = policyMinimumHealth;
            RequirementSummary = requirementSummary ?? string.Empty;
            Revision = revision;
            ResourceResult = resourceResult;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public string SourceActorId { get; }
        public string TargetActorId { get; }
        public string PolicyId { get; }
        public LifecycleTransitionKind Transition { get; }
        public LifecycleTriggerKind Trigger { get; }
        public ActorLifecycleState PreviousState { get; }
        public ActorLifecycleState ResultingState { get; }
        public DefeatPolicyOutcome PolicyOutcome { get; }
        public float OldHealth { get; }
        public float NewHealth { get; }
        public float HealthMinimum { get; }
        public float HealthMaximum { get; }
        public float RequestedHealthRestore { get; }
        public float AppliedHealthRestore { get; }
        public float PolicyMinimumHealth { get; }
        public string RequirementSummary { get; }
        public long Revision { get; }
        public ResourceChangeResult ResourceResult { get; }

        public static ActorLifecycleResult Create(
            bool succeeded,
            bool preview,
            bool duplicate,
            string code,
            string message,
            string transactionId,
            string sourceActorId,
            string targetActorId,
            string policyId,
            LifecycleTransitionKind transition,
            LifecycleTriggerKind trigger,
            ActorLifecycleState previousState,
            ActorLifecycleState resultingState,
            DefeatPolicyOutcome policyOutcome,
            float oldHealth,
            float newHealth,
            float healthMinimum,
            float healthMaximum,
            float requestedHealthRestore,
            float appliedHealthRestore,
            float policyMinimumHealth,
            string requirementSummary,
            long revision,
            ResourceChangeResult resourceResult = null)
        {
            return new ActorLifecycleResult(succeeded, preview, duplicate, code, message, transactionId, sourceActorId, targetActorId, policyId, transition, trigger, previousState, resultingState, policyOutcome, oldHealth, newHealth, healthMinimum, healthMaximum, requestedHealthRestore, appliedHealthRestore, policyMinimumHealth, requirementSummary, revision, resourceResult);
        }

        public static ActorLifecycleResult Failure(
            string code,
            string message,
            string transactionId,
            string sourceActorId,
            string targetActorId,
            string policyId,
            LifecycleTransitionKind transition,
            LifecycleTriggerKind trigger,
            ActorLifecycleState currentState,
            DefeatPolicyOutcome policyOutcome,
            float currentHealth,
            float healthMinimum,
            float healthMaximum,
            string requirementSummary = "")
        {
            return Create(false, false, false, code, message, transactionId, sourceActorId, targetActorId, policyId, transition, trigger, currentState, currentState, policyOutcome, currentHealth, currentHealth, healthMinimum, healthMaximum, 0f, 0f, 0f, requirementSummary, 0L);
        }
    }
}
