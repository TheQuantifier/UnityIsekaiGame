using UnityEngine;

namespace UnityIsekaiGame.Combat
{
    public readonly struct HealingApplicationRequest
    {
        public HealingApplicationRequest(
            string transactionId,
            string sourceActorId,
            GameObject sourceObject,
            string targetActorId,
            GameObject targetObject,
            float requestedAmount,
            string reason = "",
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            SourceObject = sourceObject;
            TargetActorId = targetActorId ?? string.Empty;
            TargetObject = targetObject;
            RequestedAmount = requestedAmount;
            Reason = reason ?? string.Empty;
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public string SourceActorId { get; }
        public GameObject SourceObject { get; }
        public string TargetActorId { get; }
        public GameObject TargetObject { get; }
        public float RequestedAmount { get; }
        public string Reason { get; }
        public bool AuthorityValidated { get; }

        public bool HasTransactionId => !string.IsNullOrWhiteSpace(TransactionId);
    }
}
