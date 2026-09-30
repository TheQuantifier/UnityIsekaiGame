using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.Stats
{
    [Serializable]
    public sealed class RuntimeAttributeValueRecord
    {
        public string attributeId;
        public float foundationValue = 1f;
        public float permanentSourceTotal;
        public float growthTotal;
        public float currentValue = 1f;
    }

    [Serializable]
    public sealed class RuntimeAttributeSourceContribution
    {
        public string contributionId;
        public string attributeId;
        public string sourceId;
        public int sourceCategory;
        public float amount;
        public bool removable = true;
        public string appliedAtUtc;
    }

    [Serializable]
    public sealed class AttributeTrainingEventRecord
    {
        public string eventId;
        public int category;
        public string sourceSystem;
        public string recordedAtUtc;
        public List<RuntimeAttributeSourceContribution> contributions = new List<RuntimeAttributeSourceContribution>();
    }

    [Serializable]
    public sealed class PlayerAttributesSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string playerId;
        public string personId;
        public List<RuntimeAttributeSourceContribution> permanentSourceContributions = new List<RuntimeAttributeSourceContribution>();
        public List<AttributeTrainingEventRecord> trainingEvents = new List<AttributeTrainingEventRecord>();
    }
}
