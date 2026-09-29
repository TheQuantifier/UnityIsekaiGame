using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Production;

namespace UnityIsekaiGame.Persistence
{
    public sealed class ProductionRequirementPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.production-requirements";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly ProductionRequirementRuntime runtime;
        private readonly string worldId;

        public ProductionRequirementPersistenceParticipant(ProductionRequirementRuntime runtime, string worldId = "")
        {
            this.runtime = runtime;
            this.worldId = worldId ?? string.Empty;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Inventory;
        public int LoadPriority => -3;
        public IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public IReadOnlyList<string> OptionalDependencies => new[] { ItemInstanceIdentityPersistenceParticipant.Key, ItemDurabilityPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => false;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Production requirement runtime is missing.");
            }

            ProductionRequirementRuntimeSaveData saveData = runtime.CreateSaveData();
            if (!ProductionRequirementRuntime.ValidateSaveData(saveData, out string failure))
            {
                return PersistenceParticipantSaveResult.Failure(failure);
            }

            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Production requirement snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported production requirement participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Production requirement payload is empty.");
            }

            ProductionRequirementRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ProductionRequirementRuntimeSaveData>(payloadJson);
            }
            catch (Exception)
            {
                return PersistenceParticipantPrepareResult.Failure("Production requirement payload is malformed JSON.");
            }

            if (!ProductionRequirementRuntime.ValidateSaveData(saveData, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Production requirement runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared production requirement payload has the wrong type.");
            }

            ProductionRequirementRuntimeSaveData rollback = runtime.CreateSaveData();
            ProductionRequirementEvaluationResult result = runtime.RestoreFromSaveData(prepared.SaveData);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Production requirements restored.");
            }

            runtime.RestoreFromSaveData(rollback);
            return PersistenceParticipantCommitResult.Failure($"Production requirement restore failed; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ProductionRequirementRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public ProductionRequirementRuntimeSaveData SaveData { get; }
        }
    }
}
