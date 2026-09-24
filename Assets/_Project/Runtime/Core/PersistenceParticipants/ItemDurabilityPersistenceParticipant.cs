using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;

namespace UnityIsekaiGame.Persistence
{
    public sealed class ItemDurabilityPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.item-durability";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly ItemDurabilityRuntime runtime;
        private readonly ItemInstanceIdentityRuntime itemIdentityRuntime;
        private readonly ItemCompositionRuntime itemCompositionRuntime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string worldId;

        public ItemDurabilityPersistenceParticipant(
            ItemDurabilityRuntime runtime,
            ItemInstanceIdentityRuntime itemIdentityRuntime,
            ItemCompositionRuntime itemCompositionRuntime,
            Func<DefinitionRegistry> registryProvider,
            string worldId = "")
        {
            this.runtime = runtime;
            this.itemIdentityRuntime = itemIdentityRuntime;
            this.itemCompositionRuntime = itemCompositionRuntime;
            this.registryProvider = registryProvider;
            this.worldId = worldId ?? string.Empty;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Inventory;
        public int LoadPriority => -5;
        public IReadOnlyList<string> RequiredDependencies => new[] { ItemInstanceIdentityPersistenceParticipant.Key };
        public IReadOnlyList<string> OptionalDependencies => new[] { ItemCompositionPersistenceParticipant.Key, ItemQualityAffixPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Item durability runtime is missing.");
            }

            ItemDurabilityRuntimeSaveData saveData = runtime.CreateSaveData();
            if (!ItemDurabilityRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), itemIdentityRuntime, itemCompositionRuntime, out string failure))
            {
                return PersistenceParticipantSaveResult.Failure(failure);
            }

            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Item durability snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported item durability participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Item durability payload is empty.");
            }

            ItemDurabilityRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ItemDurabilityRuntimeSaveData>(payloadJson);
            }
            catch (Exception)
            {
                return PersistenceParticipantPrepareResult.Failure("Item durability payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Item durability payload did not parse.");
            }

            if (!ItemDurabilityRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), itemIdentityRuntime, itemCompositionRuntime, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Item durability runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared item durability payload has the wrong type.");
            }

            ItemDurabilityRuntimeSaveData rollback = runtime.CreateSaveData();
            ItemDurabilityOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), itemIdentityRuntime, itemCompositionRuntime);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Item durability restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), itemIdentityRuntime, itemCompositionRuntime);
            return PersistenceParticipantCommitResult.Failure($"Item durability restore failed; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ItemDurabilityRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public ItemDurabilityRuntimeSaveData SaveData { get; }
        }
    }
}
