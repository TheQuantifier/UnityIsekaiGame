using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Production;

namespace UnityIsekaiGame.Persistence
{
    public sealed class ProductionWorkflowPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.production-workflow";
        public const int CurrentParticipantSchemaVersion = 3;

        private readonly ProductionWorkflowRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string worldId;

        public ProductionWorkflowPersistenceParticipant(ProductionWorkflowRuntime runtime, Func<DefinitionRegistry> registryProvider, string worldId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.worldId = worldId ?? string.Empty;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Inventory;
        public int LoadPriority => 46;
        public IReadOnlyList<string> RequiredDependencies => new[]
        {
            ItemInstanceIdentityPersistenceParticipant.Key,
            ProductionRequirementPersistenceParticipant.Key,
            CraftingExecutionPersistenceParticipant.Key
        };
        public IReadOnlyList<string> OptionalDependencies => new[]
        {
            ItemCompositionPersistenceParticipant.Key,
            ItemQualityAffixPersistenceParticipant.Key,
            ItemDurabilityPersistenceParticipant.Key,
            RecipeKnowledgePersistenceParticipant.Key,
            KnowledgeRecordPersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key
        };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Production workflow runtime is missing.");
            }

            ProductionWorkflowRuntimeSaveData saveData = runtime.CreateSaveData();
            if (!ProductionWorkflowRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantSaveResult.Failure(failure);
            }

            string json = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(json, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Production workflow snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(json);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported production workflow participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Production workflow payload is empty.");
            }

            ProductionWorkflowRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ProductionWorkflowRuntimeSaveData>(payloadJson);
            }
            catch (Exception)
            {
                return PersistenceParticipantPrepareResult.Failure("Production workflow payload is malformed JSON.");
            }

            if (!ProductionWorkflowRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Production workflow runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared production workflow payload has the wrong type.");
            }

            ProductionWorkflowRuntimeSaveData rollback = runtime.CreateSaveData();
            ProductionWorkflowResult restore = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke());
            if (restore.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Production workflow restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke());
            return PersistenceParticipantCommitResult.Failure($"Production workflow restore failed; rollback attempted: {restore.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ProductionWorkflowRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public ProductionWorkflowRuntimeSaveData SaveData { get; }
        }
    }
}
