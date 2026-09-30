using UnityEngine;

namespace UnityIsekaiGame.Combat.OngoingEffects
{
    public readonly struct OngoingEffectApplicationRequest
    {
        public OngoingEffectApplicationRequest(
            string transactionId,
            OngoingEffectDefinition definition,
            string sourceActorId,
            GameObject sourceObject,
            string targetActorId,
            GameObject targetObject,
            string originId = "",
            float amountOverride = 0f,
            float intervalOverride = 0f,
            float durationOverride = 0f,
            int tickCountOverride = 0,
            int stackCount = 1,
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            Definition = definition;
            SourceActorId = sourceActorId ?? string.Empty;
            SourceObject = sourceObject;
            TargetActorId = targetActorId ?? string.Empty;
            TargetObject = targetObject;
            OriginId = originId ?? string.Empty;
            AmountOverride = amountOverride;
            IntervalOverride = intervalOverride;
            DurationOverride = durationOverride;
            TickCountOverride = tickCountOverride;
            StackCount = stackCount;
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public OngoingEffectDefinition Definition { get; }
        public string SourceActorId { get; }
        public GameObject SourceObject { get; }
        public string TargetActorId { get; }
        public GameObject TargetObject { get; }
        public string OriginId { get; }
        public float AmountOverride { get; }
        public float IntervalOverride { get; }
        public float DurationOverride { get; }
        public int TickCountOverride { get; }
        public int StackCount { get; }
        public bool AuthorityValidated { get; }
    }

    public readonly struct OngoingEffectCancellationRequest
    {
        public OngoingEffectCancellationRequest(string transactionId, string instanceId, string targetActorId = "", GameObject targetObject = null, string reason = "", bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            TargetObject = targetObject;
            Reason = reason ?? string.Empty;
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public string InstanceId { get; }
        public string TargetActorId { get; }
        public GameObject TargetObject { get; }
        public string Reason { get; }
        public bool AuthorityValidated { get; }
    }
}
