using System;

namespace UnityIsekaiGame.Beings.Biology.Transformation
{
    [Serializable]
    public sealed class BodyTransformationSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string actorBodyId;
        public string personId;
        public long transformationRevision;
        public bool activeTemporaryTransformation;
        public string activeMethodId;
        public string activeTransactionId;
        public string originalSpeciesId;
        public string transformedSpeciesId;
        public string targetBodyId;
        public BodyTransformationReversionSaveData reversionBodyState;
        public string[] processedTransactionIds;
    }

    [Serializable]
    public sealed class BodyTransformationReversionSaveData
    {
        public string actorBodyId;
        public string personId;
        public string speciesDefinitionId;
        public long bodyRevision;
        public Anatomy.AnatomySaveData anatomy;
        public Condition.BodyConditionSaveData condition;
        public VitalProcesses.VitalProcessSaveData vitalProcesses;
        public Hazards.BiologicalHazardSaveData biologicalHazards;
        public Recovery.BiologicalRecoverySaveData biologicalRecovery;

        public static BodyTransformationReversionSaveData FromBodySaveData(BodySaveData saveData)
        {
            if (saveData == null)
            {
                return null;
            }

            return new BodyTransformationReversionSaveData
            {
                actorBodyId = saveData.actorBodyId,
                personId = saveData.personId,
                speciesDefinitionId = saveData.speciesDefinitionId,
                bodyRevision = saveData.bodyRevision,
                anatomy = saveData.anatomy,
                condition = saveData.condition,
                vitalProcesses = saveData.vitalProcesses,
                biologicalHazards = saveData.biologicalHazards,
                biologicalRecovery = saveData.biologicalRecovery
            };
        }

        public BodySaveData ToBodySaveData()
        {
            return new BodySaveData
            {
                schemaVersion = 6,
                actorBodyId = actorBodyId,
                personId = personId,
                speciesDefinitionId = speciesDefinitionId,
                bodyRevision = bodyRevision,
                anatomy = anatomy,
                condition = condition,
                vitalProcesses = vitalProcesses,
                biologicalHazards = biologicalHazards,
                biologicalRecovery = biologicalRecovery
            };
        }
    }
}
