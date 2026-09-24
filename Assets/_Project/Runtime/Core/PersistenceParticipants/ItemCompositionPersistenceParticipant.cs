using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Identity;

namespace UnityIsekaiGame.Persistence
{
    public sealed class ItemCompositionPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.item-composition";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly ItemCompositionRuntime runtime;
        private readonly ItemInstanceIdentityRuntime itemIdentityRuntime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string worldId;

        public ItemCompositionPersistenceParticipant(
            ItemCompositionRuntime runtime,
            ItemInstanceIdentityRuntime itemIdentityRuntime,
            Func<DefinitionRegistry> registryProvider,
            string worldId = "")
        {
            this.runtime = runtime;
            this.itemIdentityRuntime = itemIdentityRuntime;
            this.registryProvider = registryProvider;
            this.worldId = worldId ?? string.Empty;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Inventory;
        public int LoadPriority => -7;
        public IReadOnlyList<string> RequiredDependencies => new[] { ItemInstanceIdentityPersistenceParticipant.Key };
        public IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Item composition runtime is missing.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            ItemCompositionRuntimeSaveData saveData = runtime.CreateSaveData();
            if (!ItemCompositionRuntime.ValidateSaveData(saveData, registry, itemIdentityRuntime, out string failure))
            {
                return PersistenceParticipantSaveResult.Failure(failure);
            }

            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Item composition snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported item composition participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Item composition payload is empty.");
            }

            ItemCompositionRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ItemCompositionRuntimeSaveData>(payloadJson);
            }
            catch (Exception)
            {
                return PersistenceParticipantPrepareResult.Failure("Item composition payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Item composition payload did not parse.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!ItemCompositionRuntime.ValidateSaveData(saveData, registry, itemIdentityRuntime, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Item composition runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared item composition payload has the wrong type.");
            }

            ItemCompositionRuntimeSaveData rollback = runtime.CreateSaveData();
            ItemCompositionOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), itemIdentityRuntime);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Item compositions restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), itemIdentityRuntime);
            return PersistenceParticipantCommitResult.Failure($"Item composition restore failed; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ItemCompositionRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public ItemCompositionRuntimeSaveData SaveData { get; }
        }
    }
}
