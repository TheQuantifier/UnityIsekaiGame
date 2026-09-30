using System;

namespace UnityIsekaiGame.Beings.Biology.VitalProcesses
{
    [Serializable]
    public sealed class VitalProcessSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string actorBodyId;
        public string speciesDefinitionId;
        public string profileDefinitionId;
        public long bodyRevision;
        public long anatomyRevision;
        public long conditionRevision;
        public long vitalRevision;
        public VitalResourceSaveData[] resources;
        public string[] committedTransactionIds;
    }

    [Serializable]
    public sealed class VitalResourceSaveData
    {
        public string resourceDefinitionId;
        public bool active;
        public BiologicalResourceModelType modelType;
        public float currentValue;
        public float minimumValue;
        public float maximumValue;
        public float effectiveMaximumValue;
        public float idealValue;
        public float safeMinimum;
        public float safeMaximum;
        public float strainedLow;
        public float strainedHigh;
        public float criticalLow;
        public float criticalHigh;
        public float absoluteMinimum;
        public float absoluteMaximum;
        public VitalProcessState state;
        public long revision;
    }
}
