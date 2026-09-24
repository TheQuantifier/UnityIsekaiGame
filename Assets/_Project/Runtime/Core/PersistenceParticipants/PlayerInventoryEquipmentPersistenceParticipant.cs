using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Quality;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerInventoryEquipmentPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "player.inventory-equipment";
        public const int CurrentParticipantSchemaVersion = 2;

        private readonly PlayerInventory inventory;
        private readonly PlayerEquipment equipment;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;
        private readonly ItemInstanceIdentityRuntime itemIdentityRuntime;
        private readonly string itemIdentitySynchronizationNamespace;

        public PlayerInventoryEquipmentPersistenceParticipant(
            PlayerInventory inventory,
            PlayerEquipment equipment,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalPlayerId,
            ItemInstanceIdentityRuntime itemIdentityRuntime = null,
            string itemIdentitySynchronizationNamespace = "persistence.player.inventory-equipment")
        {
            this.inventory = inventory;
            this.equipment = equipment;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
            this.itemIdentityRuntime = itemIdentityRuntime;
            this.itemIdentitySynchronizationNamespace = string.IsNullOrWhiteSpace(itemIdentitySynchronizationNamespace) ? "persistence.player.inventory-equipment" : itemIdentitySynchronizationNamespace;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Inventory;
        public int LoadPriority => 0;
        public IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public IReadOnlyList<string> OptionalDependencies => new[]
        {
            ItemInstanceIdentityPersistenceParticipant.Key,
            ItemCompositionPersistenceParticipant.Key,
            ItemQualityAffixPersistenceParticipant.Key
        };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (inventory == null)
            {
                return PersistenceParticipantSaveResult.Failure("Player inventory is missing.");
            }

            if (equipment == null)
            {
                return PersistenceParticipantSaveResult.Failure("Player equipment is missing.");
            }

            PlayerInventoryEquipmentSaveData saveData = new PlayerInventoryEquipmentSaveData
            {
                schemaVersion = CurrentParticipantSchemaVersion,
                inventory = inventory.CreateSaveData(),
                equipment = equipment.CreateSaveData()
            };

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (itemIdentityRuntime != null)
            {
                ItemIdentityInventoryBridgeResult synchronization = ItemIdentityInventoryBridge.SynchronizeInventoryEquipmentRuntime(
                    itemIdentityRuntime,
                    saveData,
                    registry,
                    ownerId,
                    itemIdentitySynchronizationNamespace);
                if (!synchronization.Succeeded)
                {
                    return PersistenceParticipantSaveResult.Failure($"Item identity synchronization failed: {synchronization.Message}");
                }
            }

            PersistenceParticipantPrepareResult validation = PreparePayload(JsonUtility.ToJson(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Inventory/equipment snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(JsonUtility.ToJson(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported inventory/equipment participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Inventory/equipment payload is empty.");
            }

            PlayerInventoryEquipmentSaveData saveData;
            try
            {
                saveData = JsonUtility.FromJson<PlayerInventoryEquipmentSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Inventory/equipment payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Inventory/equipment payload did not parse.");
            }

            if (saveData.schemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported inventory/equipment payload schema version {saveData.schemaVersion}.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Definition registry is not available for inventory/equipment restore.");
            }

            if (!ValidateOnTemporaryRuntime(saveData, registry, out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            if (!ValidateCrossSystemInstanceIds(saveData, out failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            if (itemIdentityRuntime != null)
            {
                ItemIdentityInventoryBridgeResult projection = ItemIdentityInventoryBridge.ValidateSynchronizedProjection(
                    saveData,
                    itemIdentityRuntime.CreateSaveData(),
                    registry,
                    ownerId,
                    itemIdentitySynchronizationNamespace);
                if (!projection.Succeeded)
                {
                    return PersistenceParticipantPrepareResult.Failure($"Inventory/equipment item identity projection failed: {projection.Message}");
                }
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (inventory == null)
            {
                return PersistenceParticipantCommitResult.Failure("Player inventory is missing.");
            }

            if (equipment == null)
            {
                return PersistenceParticipantCommitResult.Failure("Player equipment is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared || prepared.SaveData == null)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared inventory/equipment payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantCommitResult.Failure("Definition registry is not available for inventory/equipment commit.");
            }

            InventorySaveData rollbackInventory = inventory.CreateSaveData();
            EquipmentSaveData rollbackEquipment = equipment.CreateSaveData();

            InventoryRestoreResult inventoryResult = inventory.TryRestoreFromSaveData(prepared.SaveData.inventory, registry);
            if (!inventoryResult.Succeeded)
            {
                return PersistenceParticipantCommitResult.Failure($"Inventory commit failed after preparation: {inventoryResult.Message}");
            }

            EquipmentRestoreResult equipmentResult = equipment.TryRestoreFromSaveData(prepared.SaveData.equipment, registry);
            if (equipmentResult.Succeeded)
            {
                if (itemIdentityRuntime != null)
                {
                    ItemIdentityInventoryBridgeResult synchronization = ItemIdentityInventoryBridge.SynchronizeInventoryEquipmentRuntime(
                        itemIdentityRuntime,
                        prepared.SaveData,
                        registry,
                        ownerId,
                        itemIdentitySynchronizationNamespace);
                    if (!synchronization.Succeeded)
                    {
                        inventory.TryRestoreFromSaveData(rollbackInventory, registry);
                        equipment.TryRestoreFromSaveData(rollbackEquipment, registry);
                        return PersistenceParticipantCommitResult.Failure($"Item identity synchronization failed after inventory/equipment commit; rollback attempted: {synchronization.Message}");
                    }
                }

                return PersistenceParticipantCommitResult.Success("Player inventory and equipment restored.");
            }

            inventory.TryRestoreFromSaveData(rollbackInventory, registry);
            equipment.TryRestoreFromSaveData(rollbackEquipment, registry);
            return PersistenceParticipantCommitResult.Failure($"Equipment commit failed after preparation; rollback attempted: {equipmentResult.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private static bool ValidateOnTemporaryRuntime(PlayerInventoryEquipmentSaveData saveData, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            GameObject temporary = new GameObject("Inventory Equipment Persistence Validation");
            temporary.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                PlayerInventory temporaryInventory = temporary.AddComponent<PlayerInventory>();
                PlayerEquipment temporaryEquipment = temporary.AddComponent<PlayerEquipment>();

                InventoryRestoreResult inventoryResult = temporaryInventory.TryRestoreFromSaveData(saveData.inventory, registry);
                if (!inventoryResult.Succeeded)
                {
                    failureReason = $"Inventory restore validation failed: {inventoryResult.Message}";
                    return false;
                }

                EquipmentRestoreResult equipmentResult = temporaryEquipment.TryRestoreFromSaveData(saveData.equipment, registry);
                if (!equipmentResult.Succeeded)
                {
                    failureReason = $"Equipment restore validation failed: {equipmentResult.Message}";
                    return false;
                }

                return true;
            }
            finally
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(temporary);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(temporary);
                }
            }
        }

        private static bool ValidateCrossSystemInstanceIds(PlayerInventoryEquipmentSaveData saveData, out string failureReason)
        {
            failureReason = string.Empty;
            HashSet<string> inventoryIds = new HashSet<string>(StringComparer.Ordinal);
            CollectInventoryInstanceIds(saveData.inventory, inventoryIds);

            HashSet<string> equipmentIds = new HashSet<string>(StringComparer.Ordinal);
            CollectEquipmentInstanceIds(saveData.equipment, equipmentIds);

            foreach (string instanceId in inventoryIds)
            {
                if (equipmentIds.Contains(instanceId))
                {
                    failureReason = $"Item instance ID '{instanceId}' appears in both inventory and equipment save data.";
                    return false;
                }
            }

            return true;
        }

        private static void CollectInventoryInstanceIds(InventorySaveData inventorySaveData, HashSet<string> instanceIds)
        {
            if (inventorySaveData?.entries == null)
            {
                return;
            }

            for (int i = 0; i < inventorySaveData.entries.Count; i++)
            {
                InventoryEntrySaveData entry = inventorySaveData.entries[i];
                string instanceId = entry == null || entry.mode == InventoryEntrySaveMode.Empty
                    ? null
                    : entry.itemInstanceId;
                if (!string.IsNullOrWhiteSpace(instanceId))
                {
                    instanceIds.Add(instanceId);
                }
            }
        }

        private static void CollectEquipmentInstanceIds(EquipmentSaveData equipmentSaveData, HashSet<string> instanceIds)
        {
            if (equipmentSaveData?.slots == null)
            {
                return;
            }

            for (int i = 0; i < equipmentSaveData.slots.Count; i++)
            {
                EquipmentSlotSaveData entry = equipmentSaveData.slots[i];
                string instanceId = entry == null || entry.mode == EquipmentEntrySaveMode.Empty
                    ? null
                    : entry.itemInstanceId;
                if (!string.IsNullOrWhiteSpace(instanceId))
                {
                    instanceIds.Add(instanceId);
                }
            }
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PlayerInventoryEquipmentSaveData saveData)
            {
                SaveData = saveData;
            }

            public PlayerInventoryEquipmentSaveData SaveData { get; }
        }
    }
}
