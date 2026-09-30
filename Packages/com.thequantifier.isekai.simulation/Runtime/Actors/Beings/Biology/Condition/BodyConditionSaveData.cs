using System;

namespace UnityIsekaiGame.Beings.Biology.Condition
{
    [Serializable]
    public sealed class BodyConditionSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string actorBodyId;
        public string anatomyDefinitionId;
        public long bodyRevision;
        public long anatomyRevision;
        public long conditionRevision;
        public StructureConditionSaveData[] structures = Array.Empty<StructureConditionSaveData>();
        public InjuryRecordSaveData[] injuries = Array.Empty<InjuryRecordSaveData>();
        public string[] committedTransactionIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class StructureConditionSaveData
    {
        public string nodeId;
        public int maximumIntegrity;
        public int currentIntegrity;
        public StructureFunctionalState functionalState;
        public StructureDamageState structuralState;
        public RuntimeStructurePresenceState runtimePresence;
        public string[] activeInjuryIds = Array.Empty<string>();
        public long revision;
    }

    [Serializable]
    public sealed class InjuryRecordSaveData
    {
        public string injuryId;
        public string actorBodyId;
        public string targetNodeId;
        public string injuryDefinitionId;
        public string sourceActorBodyId;
        public string sourceTransactionId;
        public string damageTypeId;
        public InjurySeverity severity;
        public int appliedStructuralDamage;
        public StructureFunctionalState functionalImpact;
        public StructureDamageState structuralImpact;
        public InjuryRecordState state;
        public long sequence;
        public long revision;
    }
}
