using System;

namespace UnityIsekaiGame.Beings.Biology.BiologicalConditions
{
    [Serializable]
    public sealed class BiologicalConditionSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string actorBodyId;
        public long biologicalConditionRevision;
        public BiologicalConditionInstanceSaveData[] instances = Array.Empty<BiologicalConditionInstanceSaveData>();
        public BiologicalConditionImmunityMemorySaveData[] immunityMemory = Array.Empty<BiologicalConditionImmunityMemorySaveData>();
        public string[] processedTransactionIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class BiologicalConditionInstanceSaveData
    {
        public string instanceId;
        public string actorBodyId;
        public string conditionDefinitionId;
        public string strainId;
        public string sourceId;
        public string sourceBodyId;
        public string sourceEventId;
        public int sourceCategory;
        public int exposureRoute;
        public string targetAnatomyNodeId;
        public int stage;
        public int severity;
        public float load;
        public float accumulatedDose;
        public float incubationProgress;
        public float progressionProgress;
        public float recoveryProgress;
        public bool dormant;
        public bool chronic;
        public bool carrier;
        public bool suppressed;
        public float createdGameTime;
        public string lastTickTransactionId;
        public long revision;
    }

    [Serializable]
    public sealed class BiologicalConditionImmunityMemorySaveData
    {
        public string memoryId;
        public string actorBodyId;
        public string conditionDefinitionId;
        public string strainId;
        public float strength;
        public string sourceInstanceId;
        public long revision;
    }
}
