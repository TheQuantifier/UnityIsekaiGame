using UnityEngine;

namespace UnityIsekaiGame.Combat.CombatState
{
    public readonly struct CombatEngagementRequest
    {
        public CombatEngagementRequest(
            string transactionId,
            string sourceActorId,
            GameObject sourceObject,
            string targetActorId,
            GameObject targetObject,
            CombatActivityClassification classification,
            string originatingId = "",
            string encounterId = "",
            bool hostile = true,
            bool authorityValidated = false)
        {
            TransactionId = transactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            SourceObject = sourceObject;
            TargetActorId = targetActorId ?? string.Empty;
            TargetObject = targetObject;
            Classification = classification;
            OriginatingId = originatingId ?? string.Empty;
            EncounterId = encounterId ?? string.Empty;
            Hostile = hostile;
            AuthorityValidated = authorityValidated;
        }

        public string TransactionId { get; }
        public string SourceActorId { get; }
        public GameObject SourceObject { get; }
        public string TargetActorId { get; }
        public GameObject TargetObject { get; }
        public CombatActivityClassification Classification { get; }
        public string OriginatingId { get; }
        public string EncounterId { get; }
        public bool Hostile { get; }
        public bool AuthorityValidated { get; }
    }

    public readonly struct CombatExitRequest
    {
        public CombatExitRequest(string transactionId, string actorId, GameObject actorObject, CombatExitReason reason, bool authoritative = false, string encounterId = "")
        {
            TransactionId = transactionId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            ActorObject = actorObject;
            Reason = reason;
            Authoritative = authoritative;
            EncounterId = encounterId ?? string.Empty;
        }

        public string TransactionId { get; }
        public string ActorId { get; }
        public GameObject ActorObject { get; }
        public CombatExitReason Reason { get; }
        public bool Authoritative { get; }
        public string EncounterId { get; }
    }

    public readonly struct CombatEncounterEndRequest
    {
        public CombatEncounterEndRequest(string transactionId, string encounterId, CombatEncounterCompletionReason reason, bool authoritative = false)
        {
            TransactionId = transactionId ?? string.Empty;
            EncounterId = encounterId ?? string.Empty;
            Reason = reason;
            Authoritative = authoritative;
        }

        public string TransactionId { get; }
        public string EncounterId { get; }
        public CombatEncounterCompletionReason Reason { get; }
        public bool Authoritative { get; }
    }

    public readonly struct CombatEngagementEndRequest
    {
        public CombatEngagementEndRequest(
            string transactionId,
            string engagementId,
            string sourceActorId,
            string targetActorId,
            CombatExitReason reason,
            bool authoritative = false)
        {
            TransactionId = transactionId ?? string.Empty;
            EngagementId = engagementId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            Reason = reason;
            Authoritative = authoritative;
        }

        public string TransactionId { get; }
        public string EngagementId { get; }
        public string SourceActorId { get; }
        public string TargetActorId { get; }
        public CombatExitReason Reason { get; }
        public bool Authoritative { get; }
    }
}
