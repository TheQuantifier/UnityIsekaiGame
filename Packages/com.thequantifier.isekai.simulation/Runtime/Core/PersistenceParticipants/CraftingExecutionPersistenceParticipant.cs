using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Crafting;

namespace UnityIsekaiGame.Persistence
{
    public sealed class CraftingExecutionPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.crafting-execution";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly CraftingExecutionRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string worldId;

        public CraftingExecutionPersistenceParticipant(CraftingExecutionRuntime runtime, Func<DefinitionRegistry> registryProvider, string worldId)
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
        public int LoadPriority => 45;
        public IReadOnlyList<string> RequiredDependencies => new[]
        {
            ItemInstanceIdentityPersistenceParticipant.Key,
            ProductionRequirementPersistenceParticipant.Key
        };
        public IReadOnlyList<string> OptionalDependencies => new[]
        {
            ItemCompositionPersistenceParticipant.Key,
            ItemQualityAffixPersistenceParticipant.Key,
            ItemDurabilityPersistenceParticipant.Key,
            RecipeKnowledgePersistenceParticipant.Key
        };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Crafting execution runtime is missing.");
            }

            CraftingExecutionRuntimeSaveData saveData = runtime.CreateSaveData();
            if (!CraftingExecutionRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantSaveResult.Failure(failure);
            }

            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Crafting execution snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported crafting execution participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Crafting execution payload is empty.");
            }

            CraftingExecutionRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<CraftingExecutionRuntimeSaveData>(payloadJson);
            }
            catch (Exception)
            {
                return PersistenceParticipantPrepareResult.Failure("Crafting execution payload is malformed JSON.");
            }

            if (!CraftingExecutionRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Crafting execution runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared crafting execution payload has the wrong type.");
            }

            CraftingExecutionRuntimeSaveData rollback = runtime.CreateSaveData();
            CraftingExecutionResult restore = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke());
            if (restore.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Crafting execution restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke());
            return PersistenceParticipantCommitResult.Failure($"Crafting execution restore failed; rollback attempted: {restore.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(CraftingExecutionRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public CraftingExecutionRuntimeSaveData SaveData { get; }
        }
    }
}
