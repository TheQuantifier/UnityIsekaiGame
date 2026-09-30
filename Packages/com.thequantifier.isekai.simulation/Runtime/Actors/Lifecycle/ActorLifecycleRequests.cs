using UnityEngine;

namespace UnityIsekaiGame.ActorLifecycle
{
    public readonly struct DefeatResolutionRequest
    {
        public DefeatResolutionRequest(
            string transactionId,
            string sourceActorId,
            GameObject sourceObject,
            string targetActorId,
            GameObject targetObject,
            LifecycleTriggerKind trigger,
            string triggeringResourceEventId = "",
            string reason = "",
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            SourceObject = sourceObject;
            TargetActorId = targetActorId ?? string.Empty;
            TargetObject = targetObject;
            Trigger = trigger;
            TriggeringResourceEventId = triggeringResourceEventId ?? string.Empty;
            Reason = reason ?? string.Empty;
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public string SourceActorId { get; }
        public GameObject SourceObject { get; }
        public string TargetActorId { get; }
        public GameObject TargetObject { get; }
        public LifecycleTriggerKind Trigger { get; }
        public string TriggeringResourceEventId { get; }
        public string Reason { get; }
        public bool AuthorityValidated { get; }
    }

    public readonly struct LifecycleRecoveryRequest
    {
        public LifecycleRecoveryRequest(
            string transactionId,
            string sourceActorId,
            GameObject sourceObject,
            string targetActorId,
            GameObject targetObject,
            float requestedHealthRestore,
            string reason = "",
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            SourceObject = sourceObject;
            TargetActorId = targetActorId ?? string.Empty;
            TargetObject = targetObject;
            RequestedHealthRestore = requestedHealthRestore;
            Reason = reason ?? string.Empty;
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public string SourceActorId { get; }
        public GameObject SourceObject { get; }
        public string TargetActorId { get; }
        public GameObject TargetObject { get; }
        public float RequestedHealthRestore { get; }
        public string Reason { get; }
        public bool AuthorityValidated { get; }
    }

    public readonly struct LifecycleDeathRequest
    {
        public LifecycleDeathRequest(
            string transactionId,
            string sourceActorId,
            GameObject sourceObject,
            string targetActorId,
            GameObject targetObject,
            LifecycleTriggerKind trigger,
            string reason = "",
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            SourceObject = sourceObject;
            TargetActorId = targetActorId ?? string.Empty;
            TargetObject = targetObject;
            Trigger = trigger;
            Reason = reason ?? string.Empty;
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public string SourceActorId { get; }
        public GameObject SourceObject { get; }
        public string TargetActorId { get; }
        public GameObject TargetObject { get; }
        public LifecycleTriggerKind Trigger { get; }
        public string Reason { get; }
        public bool AuthorityValidated { get; }
    }

    public readonly struct LifecycleRevivalRequest
    {
        public LifecycleRevivalRequest(
            string transactionId,
            string sourceActorId,
            GameObject sourceObject,
            string targetActorId,
            GameObject targetObject,
            float requestedHealthRestore,
            string reason = "",
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            SourceObject = sourceObject;
            TargetActorId = targetActorId ?? string.Empty;
            TargetObject = targetObject;
            RequestedHealthRestore = requestedHealthRestore;
            Reason = reason ?? string.Empty;
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public string SourceActorId { get; }
        public GameObject SourceObject { get; }
        public string TargetActorId { get; }
        public GameObject TargetObject { get; }
        public float RequestedHealthRestore { get; }
        public string Reason { get; }
        public bool AuthorityValidated { get; }
    }
}
