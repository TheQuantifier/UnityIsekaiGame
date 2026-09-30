using System;

namespace UnityIsekaiGame.Beings.Biology.Hazards
{
    [Serializable]
    public sealed class BiologicalHazardSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string actorBodyId;
        public long bodyRevision;
        public long vitalRevision;
        public long hazardRevision;
        public BiologicalHazardInstanceSaveData[] activeHazards;
        public string[] committedTickTransactionIds;
    }

    [Serializable]
    public sealed class BiologicalHazardInstanceSaveData
    {
        public string instanceId;
        public string hazardDefinitionId;
        public BiologicalHazardSeverity severity;
        public float elapsedSeconds;
        public long revision;
        public BiologicalHazardSourceSaveData[] sources;
        public BiologicalHazardSuppressionSaveData[] suppressions;
    }

    [Serializable]
    public sealed class BiologicalHazardSourceSaveData
    {
        public string sourceContributionId;
        public BiologicalHazardSourceCategory sourceCategory;
        public BiologicalHazardSeverity severity;
        public float rateMultiplier;
        public float remainingSeconds;
        public string sourceObjectId;
        public string reason;
    }

    [Serializable]
    public sealed class BiologicalHazardSuppressionSaveData
    {
        public string sourceContributionId;
        public BiologicalHazardSuppressionMode mode;
        public float rateMultiplier;
        public string reason;
    }
}
