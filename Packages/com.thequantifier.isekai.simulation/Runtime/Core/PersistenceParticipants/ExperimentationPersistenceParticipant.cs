using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Experimentation;

namespace UnityIsekaiGame.Persistence
{
    public sealed class ExperimentationPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.experimentation-discovery";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly ExperimentationRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string worldId;

        public ExperimentationPersistenceParticipant(ExperimentationRuntime runtime, Func<DefinitionRegistry> registryProvider, string worldId)
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
        public int LoadPriority => 47;
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
            ProductionWorkflowPersistenceParticipant.Key,
            PersonKnowledgePersistenceParticipant.Key,
            InformationSourcePersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key,
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
                return PersistenceParticipantSaveResult.Failure("Experimentation runtime is missing.");
            }

            ExperimentationRuntimeSaveData saveData = runtime.CreateSaveData();
            if (!ExperimentationRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantSaveResult.Failure(failure);
            }

            string json = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(json, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Experimentation snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(json);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported experimentation participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Experimentation payload is empty.");
            }

            ExperimentationRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ExperimentationRuntimeSaveData>(payloadJson);
            }
            catch (Exception)
            {
                return PersistenceParticipantPrepareResult.Failure("Experimentation payload is malformed JSON.");
            }

            if (!ExperimentationRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Experimentation runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared experimentation payload has the wrong type.");
            }

            ExperimentationRuntimeSaveData rollback = runtime.CreateSaveData();
            ExperimentationResult restore = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke());
            if (restore.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Experimentation runtime restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke());
            return PersistenceParticipantCommitResult.Failure($"Experimentation restore failed; rollback attempted: {restore.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ExperimentationRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public ExperimentationRuntimeSaveData SaveData { get; }
        }
    }
}
