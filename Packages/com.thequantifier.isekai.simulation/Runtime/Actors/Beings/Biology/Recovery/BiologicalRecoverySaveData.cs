using System;

namespace UnityIsekaiGame.Beings.Biology.Recovery
{
    [Serializable]
    public sealed class BiologicalRecoverySaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string actorBodyId;
        public string personId;
        public string speciesDefinitionId;
        public string profileDefinitionId;
        public long bodyRevision;
        public long conditionRevision;
        public long vitalRevision;
        public long hazardRevision;
        public long compatibilityRevision;
        public long recoveryRevision;
        public RecoveryRestContextSaveData restContext;
        public RecoveryProcessSaveData[] processes = Array.Empty<RecoveryProcessSaveData>();
        public RecoveryRateModifierSaveData[] rateModifiers = Array.Empty<RecoveryRateModifierSaveData>();
        public string[] committedTransactionIds = Array.Empty<string>();
        public string[] committedTickIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class RecoveryRateModifierSaveData
    {
        public string sourceId;
        public float rateMultiplier;
        public string reason;
        public long revision;
    }

    [Serializable]
    public sealed class RecoveryRestContextSaveData
    {
        public string actorBodyId;
        public RecoveryRestType restType;
        public string sourceId;
        public string transactionId;
        public float quality;
        public string[] tags = Array.Empty<string>();
    }

    [Serializable]
    public sealed class RecoveryTargetSaveData
    {
        public RecoveryTargetCategory targetCategory;
        public string actorBodyId;
        public string anatomyNodeId;
        public string injuryId;
        public string resourceDefinitionId;
        public string hazardInstanceId;
        public string stableTargetKey;
        public long owningSystemRevision;
    }

    [Serializable]
    public sealed class RecoveryProcessSaveData
    {
        public string processId;
        public string actorBodyId;
        public string recoveryMethodId;
        public string sourceId;
        public string sourceTransactionId;
        public RecoveryTargetSaveData target;
        public float currentProgress;
        public float requiredProgress;
        public float baseRatePerHour;
        public float effectiveRatePerHour;
        public RecoveryProcessState state;
        public RecoveryInterruptionPolicy interruptionPolicy;
        public RecoveryLimit recoveryLimit;
        public RecoveryPermanentOutcome projectedPermanentOutcome;
        public string compatibilitySummary;
        public long createdSequence;
        public string lastCommittedTickId;
        public long revision;
    }
}
