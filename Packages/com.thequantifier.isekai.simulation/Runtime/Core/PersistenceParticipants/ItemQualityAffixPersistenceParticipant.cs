using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Quality;

namespace UnityIsekaiGame.Persistence
{
    public sealed class ItemQualityAffixPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.item-quality-affixes";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly ItemQualityAffixRuntime runtime;
        private readonly ItemInstanceIdentityRuntime itemIdentityRuntime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string worldId;

        public ItemQualityAffixPersistenceParticipant(
            ItemQualityAffixRuntime runtime,
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
        public int LoadPriority => -6;
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
                return PersistenceParticipantSaveResult.Failure("Item quality runtime is missing.");
            }

            ItemQualityAffixRuntimeSaveData saveData = runtime.CreateSaveData();
            if (!ItemQualityAffixRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), itemIdentityRuntime, out string failure))
            {
                return PersistenceParticipantSaveResult.Failure(failure);
            }

            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Item quality snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported item quality participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Item quality payload is empty.");
            }

            ItemQualityAffixRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ItemQualityAffixRuntimeSaveData>(payloadJson);
            }
            catch (Exception)
            {
                return PersistenceParticipantPrepareResult.Failure("Item quality payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Item quality payload did not parse.");
            }

            if (!ItemQualityAffixRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), itemIdentityRuntime, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Item quality runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared item quality payload has the wrong type.");
            }

            ItemQualityAffixRuntimeSaveData rollback = runtime.CreateSaveData();
            ItemQualityAffixOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), itemIdentityRuntime);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Item quality and affixes restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), itemIdentityRuntime);
            return PersistenceParticipantCommitResult.Failure($"Item quality restore failed; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ItemQualityAffixRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public ItemQualityAffixRuntimeSaveData SaveData { get; }
        }
    }
}
