using UnityEngine;

namespace UnityIsekaiGame.Combat
{
    public readonly struct DamageApplicationRequest
    {
        public DamageApplicationRequest(
            string transactionId,
            string sourceActorId,
            GameObject sourceObject,
            string targetActorId,
            GameObject targetObject,
            DamagePacket damagePacket,
            string reason = "",
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            SourceObject = sourceObject;
            TargetActorId = targetActorId ?? string.Empty;
            TargetObject = targetObject;
            DamagePacket = damagePacket;
            Reason = reason ?? string.Empty;
            AuthorityValidated = authorityValidated;
        }

        public DamageApplicationRequest(
            string transactionId,
            string sourceActorId,
            GameObject sourceObject,
            string targetActorId,
            GameObject targetObject,
            DamageTypeDefinition damageType,
            float requestedAmount,
            string reason = "",
            bool authorityValidated = false)
            : this(
                transactionId,
                sourceActorId,
                sourceObject,
                targetActorId,
                targetObject,
                DamagePacket.Single(sourceObject, new DamageComponent(damageType, requestedAmount)),
                reason,
                authorityValidated)
        {
        }

        public string TransactionId { get; }
        public string SourceActorId { get; }
        public GameObject SourceObject { get; }
        public string TargetActorId { get; }
        public GameObject TargetObject { get; }
        public DamagePacket DamagePacket { get; }
        public DamageTypeDefinition DamageType => DamagePacket.Components.Count == 1 ? DamagePacket.Components[0].DamageType : null;
        public float RequestedAmount
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < DamagePacket.Components.Count; i++)
                {
                    total += Mathf.Max(0f, DamagePacket.Components[i].Amount);
                }

                return total;
            }
        }
        public string Reason { get; }
        public bool AuthorityValidated { get; }

        public bool HasTransactionId => !string.IsNullOrWhiteSpace(TransactionId);
    }
}
