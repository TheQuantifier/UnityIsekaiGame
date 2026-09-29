using System;

namespace UnityIsekaiGame.Beings.Biology.Recovery
{
    public sealed class RecoveryTargetReference
    {
        public RecoveryTargetCategory TargetCategory { get; set; }
        public string ActorBodyId { get; set; }
        public string AnatomyNodeId { get; set; }
        public string InjuryId { get; set; }
        public string ResourceDefinitionId { get; set; }
        public string HazardInstanceId { get; set; }
        public string StableTargetKey { get; set; }
        public long OwningSystemRevision { get; set; }

        public string GetStableKey()
        {
            if (!string.IsNullOrWhiteSpace(StableTargetKey))
            {
                return StableTargetKey.Trim();
            }

            switch (TargetCategory)
            {
                case RecoveryTargetCategory.Injury:
                    return $"injury:{InjuryId ?? string.Empty}";
                case RecoveryTargetCategory.AnatomyNode:
                case RecoveryTargetCategory.StructuralIntegrity:
                    return $"node:{AnatomyNodeId ?? string.Empty}";
                case RecoveryTargetCategory.VitalResource:
                    return $"resource:{ResourceDefinitionId ?? string.Empty}";
                case RecoveryTargetCategory.Hazard:
                    return $"hazard:{HazardInstanceId ?? string.Empty}";
                default:
                    return $"target:{AnatomyNodeId ?? InjuryId ?? ResourceDefinitionId ?? HazardInstanceId ?? string.Empty}";
            }
        }
    }

    public sealed class RecoveryProcessStartRequest
    {
        public string ActorBodyId { get; set; }
        public string RecoveryMethodId { get; set; }
        public string SourceId { get; set; }
        public RecoveryTargetReference Target { get; set; }
        public string TransactionId { get; set; }
        public string AuthorityContext { get; set; }
        public long ExpectedBodyRevision { get; set; }
    }

    public sealed class RecoveryTickRequest
    {
        public string ActorBodyId { get; set; }
        public string TickId { get; set; }
        public float ElapsedGameSeconds { get; set; }
        public string AuthorityContext { get; set; }
        public long ExpectedRecoveryRevision { get; set; }
        public long ExpectedBodyRevision { get; set; }
        public long ExpectedConditionRevision { get; set; }
        public long ExpectedVitalRevision { get; set; }
        public long ExpectedHazardRevision { get; set; }
        public long ExpectedCompatibilityRevision { get; set; }
    }

    public sealed class RecoveryCancellationRequest
    {
        public string ActorBodyId { get; set; }
        public string ProcessId { get; set; }
        public string TransactionId { get; set; }
        public string Reason { get; set; }
    }

    public sealed class RecoveryRestContextRequest
    {
        public string ActorBodyId { get; set; }
        public RecoveryRestType RestType { get; set; }
        public string SourceId { get; set; }
        public string TransactionId { get; set; }
        public float Quality { get; set; } = 1f;
        public string[] Tags { get; set; } = Array.Empty<string>();
    }

    public readonly struct RecoveryRateModifierRequest
    {
        public RecoveryRateModifierRequest(string actorBodyId, string sourceId, float rateMultiplier, string transactionId, string reason = "")
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            RateMultiplier = rateMultiplier;
            TransactionId = transactionId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string ActorBodyId { get; }
        public string SourceId { get; }
        public float RateMultiplier { get; }
        public string TransactionId { get; }
        public string Reason { get; }
    }
}
