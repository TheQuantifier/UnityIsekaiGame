using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Inventory.Identity
{
    public sealed class ItemIdentityInventoryBridgeResult
    {
        private ItemIdentityInventoryBridgeResult(bool succeeded, string status, string message, ItemInstanceRuntimeSaveData saveData, IReadOnlyList<string> diagnostics)
        {
            Succeeded = succeeded;
            Status = status ?? string.Empty;
            Message = message ?? string.Empty;
            SaveData = saveData;
            Diagnostics = (diagnostics ?? Array.Empty<string>()).ToArray();
        }

        public bool Succeeded { get; }
        public string Status { get; }
        public string Message { get; }
        public ItemInstanceRuntimeSaveData SaveData { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public static ItemIdentityInventoryBridgeResult Success(ItemInstanceRuntimeSaveData saveData, string message, IReadOnlyList<string> diagnostics = null)
        {
            return new ItemIdentityInventoryBridgeResult(true, "Succeeded", message, saveData, diagnostics);
        }

        public static ItemIdentityInventoryBridgeResult Failure(string status, string message, IReadOnlyList<string> diagnostics = null)
        {
            return new ItemIdentityInventoryBridgeResult(false, status, message, null, diagnostics);
        }
    }

    public static class ItemIdentityInventoryBridge
    {
        public static ItemIdentityInventoryBridgeResult SynchronizeInventoryEquipmentRuntime(
            ItemInstanceIdentityRuntime runtime,
            PlayerInventoryEquipmentSaveData saveData,
            DefinitionRegistry registry,
            string ownerPersonId,
            string synchronizationNamespace)
        {
            if (runtime == null)
            {
                return ItemIdentityInventoryBridgeResult.Failure("MissingRuntime", "Item identity runtime is missing.");
            }

            ItemIdentityInventoryBridgeResult migration = MigrateInventoryEquipmentSave(saveData, registry, ownerPersonId, synchronizationNamespace);
            if (!migration.Succeeded)
            {
                return migration;
            }

            ItemInstanceRuntimeSaveData merged = MergeInventoryEquipmentProjection(runtime.CreateSaveData(), migration.SaveData, ownerPersonId);
            if (!ItemInstanceIdentityRuntime.ValidateSaveData(merged, registry, out string validationFailure))
            {
                return ItemIdentityInventoryBridgeResult.Failure("IdentityValidationFailed", validationFailure);
            }

            ItemInstanceOperationResult restore = runtime.RestoreFromSaveData(merged, registry);
            return restore.Succeeded
                ? ItemIdentityInventoryBridgeResult.Success(merged, $"Synchronized {migration.SaveData.records.Count} inventory/equipment item identity record(s).")
                : ItemIdentityInventoryBridgeResult.Failure(restore.Status.ToString(), restore.Message);
        }

        public static ItemIdentityInventoryBridgeResult MigrateInventoryEquipmentSave(
            PlayerInventoryEquipmentSaveData saveData,
            DefinitionRegistry registry,
            string ownerPersonId,
            string migrationNamespace)
        {
            if (saveData == null)
            {
                return ItemIdentityInventoryBridgeResult.Failure("MissingSave", "Inventory/equipment save data is missing.");
            }

            if (registry == null)
            {
                return ItemIdentityInventoryBridgeResult.Failure("MissingRegistry", "Definition registry is missing.");
            }

            string owner = string.IsNullOrWhiteSpace(ownerPersonId) ? "person.local-player" : ownerPersonId;
            string scope = string.IsNullOrWhiteSpace(migrationNamespace) ? "migration.inventory-equipment" : migrationNamespace;
            List<ItemInstanceRecordData> records = new List<ItemInstanceRecordData>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

            if (!CollectInventory(saveData.inventory, registry, owner, scope, records, ids, out string failure))
            {
                return ItemIdentityInventoryBridgeResult.Failure("InventoryMigrationFailed", failure);
            }

            if (!CollectEquipment(saveData.equipment, registry, owner, scope, records, ids, out failure))
            {
                return ItemIdentityInventoryBridgeResult.Failure("EquipmentMigrationFailed", failure);
            }

            ItemInstanceRuntimeSaveData migrated = new ItemInstanceRuntimeSaveData
            {
                schemaVersion = ItemInstanceRuntimeSaveData.CurrentSchemaVersion,
                revision = Math.Max(1L, records.Count),
                records = records.OrderBy(record => record.itemInstanceId, StringComparer.Ordinal).Select(record => record.Clone()).ToList()
            };

            return ItemInstanceIdentityRuntime.ValidateSaveData(migrated, registry, out failure)
                ? ItemIdentityInventoryBridgeResult.Success(migrated, $"Migrated {records.Count} inventory/equipment item identity record(s).")
                : ItemIdentityInventoryBridgeResult.Failure("IdentityValidationFailed", failure);
        }

        public static ItemIdentityInventoryBridgeResult ValidateInventoryEquipmentProjection(
            PlayerInventoryEquipmentSaveData saveData,
            ItemInstanceRuntimeSaveData identitySaveData,
            string ownerPersonId)
        {
            if (saveData == null)
            {
                return ItemIdentityInventoryBridgeResult.Failure("MissingSave", "Inventory/equipment save data is missing.");
            }

            if (identitySaveData == null)
            {
                return ItemIdentityInventoryBridgeResult.Failure("MissingIdentityGraph", "Item identity save data is missing.");
            }

            Dictionary<string, ItemInstanceRecordData> records = (identitySaveData.records ?? new List<ItemInstanceRecordData>())
                .Where(record => record != null && !string.IsNullOrWhiteSpace(record.itemInstanceId))
                .ToDictionary(record => record.itemInstanceId, StringComparer.Ordinal);
            List<string> diagnostics = new List<string>();
            HashSet<string> seenLocations = new HashSet<string>(StringComparer.Ordinal);

            ValidateInventoryEntries(saveData.inventory, ownerPersonId, records, diagnostics, seenLocations);
            ValidateEquipmentEntries(saveData.equipment, ownerPersonId, records, diagnostics, seenLocations);

            return diagnostics.Count == 0
                ? ItemIdentityInventoryBridgeResult.Success(identitySaveData.Clone(), "Inventory/equipment projection matches item identity graph.")
                : ItemIdentityInventoryBridgeResult.Failure("ProjectionMismatch", string.Join(" | ", diagnostics), diagnostics);
        }

        public static ItemIdentityInventoryBridgeResult ValidateSynchronizedProjection(
            PlayerInventoryEquipmentSaveData saveData,
            ItemInstanceRuntimeSaveData identitySaveData,
            DefinitionRegistry registry,
            string ownerPersonId,
            string synchronizationNamespace)
        {
            ItemIdentityInventoryBridgeResult expected = MigrateInventoryEquipmentSave(saveData, registry, ownerPersonId, synchronizationNamespace);
            if (!expected.Succeeded)
            {
                return expected;
            }

            ItemIdentityInventoryBridgeResult projection = ValidateInventoryEquipmentProjection(saveData, identitySaveData, ownerPersonId);
            if (!projection.Succeeded)
            {
                return projection;
            }

            Dictionary<string, ItemInstanceRecordData> records = (identitySaveData.records ?? new List<ItemInstanceRecordData>())
                .Where(record => record != null && !string.IsNullOrWhiteSpace(record.itemInstanceId))
                .ToDictionary(record => record.itemInstanceId, StringComparer.Ordinal);
            List<string> diagnostics = new List<string>();
            foreach (ItemInstanceRecordData expectedRecord in expected.SaveData.records)
            {
                if (!records.TryGetValue(expectedRecord.itemInstanceId, out ItemInstanceRecordData actual))
                {
                    diagnostics.Add($"Expected identity record '{expectedRecord.itemInstanceId}' for inventory/equipment projection is missing.");
                    continue;
                }

                if (!LocationEquals(expectedRecord.location, actual.location) ||
                    expectedRecord.stackQuantity != actual.stackQuantity ||
                    !string.Equals(expectedRecord.itemDefinitionId, actual.itemDefinitionId, StringComparison.Ordinal))
                {
                    diagnostics.Add($"Identity record '{expectedRecord.itemInstanceId}' does not match the current inventory/equipment projection.");
                }
            }

            return diagnostics.Count == 0
                ? ItemIdentityInventoryBridgeResult.Success(identitySaveData.Clone(), "Inventory/equipment identity projection is synchronized.")
                : ItemIdentityInventoryBridgeResult.Failure("ProjectionMismatch", string.Join(" | ", diagnostics), diagnostics);
        }

        public static bool CanShareStack(ItemInstanceSnapshot left, ItemInstanceSnapshot right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (!string.Equals(left.ItemDefinitionId, right.ItemDefinitionId, StringComparison.Ordinal))
            {
                return false;
            }

            if (left.Classification != ItemInstanceClassification.Fungible || right.Classification != ItemInstanceClassification.Fungible)
            {
                return false;
            }

            ItemInstanceRecordData a = left.Data;
            ItemInstanceRecordData b = right.Data;
            return string.Equals(a.labels?.customName, b.labels?.customName, StringComparison.Ordinal)
                && string.Equals(a.labels?.makerMark, b.labels?.makerMark, StringComparison.Ordinal)
                && string.Equals(a.labels?.serialNumber, b.labels?.serialNumber, StringComparison.Ordinal)
                && a.labels?.authenticity == b.labels?.authenticity
                && string.Equals(a.accessPolicyId, b.accessPolicyId, StringComparison.Ordinal)
                && string.Equals(a.ownership?.ownerPersonId, b.ownership?.ownerPersonId, StringComparison.Ordinal)
                && string.Equals(a.ownership?.custodianPersonId, b.ownership?.custodianPersonId, StringComparison.Ordinal)
                && SequenceEquals(a.provenance?.parentItemInstanceIds, b.provenance?.parentItemInstanceIds)
                && SequenceEquals(a.provenance?.sourceItemInstanceIds, b.provenance?.sourceItemInstanceIds)
                && string.Equals(a.location?.worldPlacementId, b.location?.worldPlacementId, StringComparison.Ordinal);
        }

        private static bool CollectInventory(
            InventorySaveData inventory,
            DefinitionRegistry registry,
            string owner,
            string scope,
            List<ItemInstanceRecordData> records,
            HashSet<string> ids,
            out string failure)
        {
            failure = string.Empty;
            IReadOnlyList<InventoryEntrySaveData> entries = inventory?.entries != null ? inventory.entries : Array.Empty<InventoryEntrySaveData>();
            for (int i = 0; i < entries.Count; i++)
            {
                InventoryEntrySaveData entry = entries[i];
                if (entry == null || entry.mode == InventoryEntrySaveMode.Empty)
                {
                    continue;
                }

                ItemInstanceRecordData record;
                if (entry.mode == InventoryEntrySaveMode.StatefulInstance)
                {
                    if (!TryCreateStatefulRecord(entry.definitionId, entry.itemInstanceId, registry, owner, ItemLocationKind.Inventory, owner, string.Empty, out record, out failure))
                    {
                        return false;
                    }
                }
                else
                {
                    string itemInstanceId = ResolveProjectionItemInstanceId(entry.itemInstanceId, $"{scope}.inventory.{i}.{entry.definitionId}.{entry.quantity}");
                    if (!TryCreateFungibleStackRecord(entry.definitionId, entry.quantity, registry, owner, itemInstanceId, out record, out failure))
                    {
                        return false;
                    }
                }

                if (!ids.Add(record.itemInstanceId))
                {
                    failure = $"Duplicate migrated item instance ID '{record.itemInstanceId}'.";
                    return false;
                }

                records.Add(record);
            }

            return true;
        }

        private static ItemInstanceRuntimeSaveData MergeInventoryEquipmentProjection(
            ItemInstanceRuntimeSaveData current,
            ItemInstanceRuntimeSaveData projected,
            string ownerPersonId)
        {
            Dictionary<string, ItemInstanceRecordData> merged = (current?.records ?? new List<ItemInstanceRecordData>())
                .Where(record => record != null && !string.IsNullOrWhiteSpace(record.itemInstanceId))
                .ToDictionary(record => record.itemInstanceId, record => record.Clone(), StringComparer.Ordinal);
            HashSet<string> projectedIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (ItemInstanceRecordData projectedRecord in projected.records ?? new List<ItemInstanceRecordData>())
            {
                if (projectedRecord == null || string.IsNullOrWhiteSpace(projectedRecord.itemInstanceId))
                {
                    continue;
                }

                projectedIds.Add(projectedRecord.itemInstanceId);
                if (merged.TryGetValue(projectedRecord.itemInstanceId, out ItemInstanceRecordData existing))
                {
                    ApplyCurrentProjection(existing, projectedRecord);
                }
                else
                {
                    merged[projectedRecord.itemInstanceId] = projectedRecord.Clone();
                }
            }

            string owner = ownerPersonId ?? string.Empty;
            foreach (ItemInstanceRecordData record in merged.Values)
            {
                if (record == null || projectedIds.Contains(record.itemInstanceId) || !IsOwnedInventoryOrEquipmentProjection(record, owner))
                {
                    continue;
                }

                record.location = new ItemLocationStateData { kind = ItemLocationKind.Unassigned };
                record.lifecycleState = ItemLifecycleState.Lost;
                record.revision = Math.Max(1L, record.revision + 1L);
            }

            return new ItemInstanceRuntimeSaveData
            {
                schemaVersion = ItemInstanceRuntimeSaveData.CurrentSchemaVersion,
                revision = Math.Max(current?.revision ?? 0L, projected?.revision ?? 0L) + 1L,
                records = merged.Values.OrderBy(record => record.itemInstanceId, StringComparer.Ordinal).Select(record => record.Clone()).ToList()
            };
        }

        private static void ApplyCurrentProjection(ItemInstanceRecordData existing, ItemInstanceRecordData projected)
        {
            existing.itemDefinitionId = projected.itemDefinitionId;
            existing.classification = projected.classification;
            existing.stackQuantity = projected.stackQuantity;
            existing.location = projected.location?.Clone() ?? new ItemLocationStateData();
            existing.lifecycleState = projected.location?.kind == ItemLocationKind.Equipped
                ? ItemLifecycleState.Equipped
                : projected.location?.kind == ItemLocationKind.Inventory ? ItemLifecycleState.InInventory : projected.lifecycleState;
            existing.ownership ??= new ItemOwnershipStateData();
            existing.ownership.kind = projected.ownership?.kind ?? existing.ownership.kind;
            existing.ownership.ownerPersonId = projected.ownership?.ownerPersonId ?? existing.ownership.ownerPersonId;
            existing.ownership.custodianPersonId = projected.ownership?.custodianPersonId ?? existing.ownership.custodianPersonId;
            existing.revision = Math.Max(1L, existing.revision + 1L);
        }

        private static bool IsOwnedInventoryOrEquipmentProjection(ItemInstanceRecordData record, string owner)
        {
            if (record?.location == null)
            {
                return false;
            }

            return (record.location.kind == ItemLocationKind.Inventory && string.Equals(record.location.inventoryOwnerId, owner, StringComparison.Ordinal)) ||
                (record.location.kind == ItemLocationKind.Equipped && string.Equals(record.location.equipmentHolderId, owner, StringComparison.Ordinal));
        }

        private static bool LocationEquals(ItemLocationStateData left, ItemLocationStateData right)
        {
            left ??= new ItemLocationStateData();
            right ??= new ItemLocationStateData();
            return left.kind == right.kind
                && string.Equals(left.containerId, right.containerId, StringComparison.Ordinal)
                && string.Equals(left.inventoryOwnerId, right.inventoryOwnerId, StringComparison.Ordinal)
                && string.Equals(left.equipmentHolderId, right.equipmentHolderId, StringComparison.Ordinal)
                && string.Equals(left.equipmentSlotId, right.equipmentSlotId, StringComparison.Ordinal)
                && string.Equals(left.worldPlacementId, right.worldPlacementId, StringComparison.Ordinal)
                && string.Equals(left.worldEntityId, right.worldEntityId, StringComparison.Ordinal)
                && string.Equals(left.sceneKey, right.sceneKey, StringComparison.Ordinal)
                && string.Equals(left.transitId, right.transitId, StringComparison.Ordinal);
        }

        private static bool CollectEquipment(
            EquipmentSaveData equipment,
            DefinitionRegistry registry,
            string owner,
            string scope,
            List<ItemInstanceRecordData> records,
            HashSet<string> ids,
            out string failure)
        {
            failure = string.Empty;
            IReadOnlyList<EquipmentSlotSaveData> slots = equipment?.slots != null ? equipment.slots : Array.Empty<EquipmentSlotSaveData>();
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotSaveData slot = slots[i];
                if (slot == null || slot.mode == EquipmentEntrySaveMode.Empty)
                {
                    continue;
                }

                ItemInstanceRecordData record;
                if (slot.mode == EquipmentEntrySaveMode.StatefulInstance)
                {
                    if (!TryCreateStatefulRecord(slot.definitionId, slot.itemInstanceId, registry, owner, ItemLocationKind.Equipped, owner, slot.slotType.ToString(), out record, out failure))
                    {
                        return false;
                    }
                }
                else
                {
                    string itemInstanceId = ResolveProjectionItemInstanceId(slot.itemInstanceId, $"{scope}.equipment.{slot.slotType}.{slot.definitionId}");
                    if (!TryCreateFungibleStackRecord(slot.definitionId, 1, registry, owner, itemInstanceId, out record, out failure))
                    {
                        return false;
                    }

                    record.classification = ItemInstanceClassification.IndividuallyTracked;
                    record.location = new ItemLocationStateData { kind = ItemLocationKind.Equipped, equipmentHolderId = owner, equipmentSlotId = slot.slotType.ToString() };
                    record.stackQuantity = 1;
                }

                if (!ids.Add(record.itemInstanceId))
                {
                    failure = $"Duplicate migrated item instance ID '{record.itemInstanceId}'.";
                    return false;
                }

                records.Add(record);
            }

            return true;
        }

        private static bool TryCreateStatefulRecord(
            string definitionId,
            string itemInstanceId,
            DefinitionRegistry registry,
            string owner,
            ItemLocationKind locationKind,
            string locationOwner,
            string equipmentSlot,
            out ItemInstanceRecordData record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            string resolvedInstanceId = itemInstanceId;
            string resolvedDefinitionId = definitionId;
            if (string.IsNullOrWhiteSpace(resolvedInstanceId))
            {
                failure = "Stateful item entry is missing a persistent instance ID.";
                return false;
            }

            if (!ItemInstanceId.IsValid(resolvedInstanceId))
            {
                failure = $"Stateful item entry has invalid item instance ID '{resolvedInstanceId}'.";
                return false;
            }

            if (registry == null || !registry.TryGet(resolvedDefinitionId, out ItemDefinition item))
            {
                failure = $"Stateful item references missing item definition '{resolvedDefinitionId}'.";
                return false;
            }

            record = CreateBaseRecord(item.ItemId, resolvedInstanceId, owner, ItemInstanceClassification.IndividuallyTracked);
            record.location = locationKind == ItemLocationKind.Equipped
                ? new ItemLocationStateData { kind = ItemLocationKind.Equipped, equipmentHolderId = locationOwner, equipmentSlotId = equipmentSlot }
                : new ItemLocationStateData { kind = ItemLocationKind.Inventory, inventoryOwnerId = locationOwner };
            return true;
        }

        private static bool TryCreateFungibleStackRecord(
            string definitionId,
            int quantity,
            DefinitionRegistry registry,
            string owner,
            string instanceId,
            out ItemInstanceRecordData record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            if (quantity <= 0)
            {
                failure = $"Definition stack '{definitionId}' has invalid quantity {quantity}.";
                return false;
            }

            if (registry == null || !registry.TryGet(definitionId, out ItemDefinition item))
            {
                failure = $"Definition stack references missing item definition '{definitionId}'.";
                return false;
            }

            record = CreateBaseRecord(item.ItemId, instanceId, owner, ItemInstanceClassification.Fungible);
            record.stackQuantity = quantity;
            record.location = new ItemLocationStateData { kind = ItemLocationKind.Inventory, inventoryOwnerId = owner };
            return true;
        }

        private static ItemInstanceRecordData CreateBaseRecord(string definitionId, string instanceId, string owner, ItemInstanceClassification classification)
        {
            return new ItemInstanceRecordData
            {
                itemInstanceId = instanceId,
                itemDefinitionId = definitionId,
                classification = classification,
                stackQuantity = 1,
                lifecycleState = ItemLifecycleState.Active,
                location = new ItemLocationStateData { kind = ItemLocationKind.Inventory, inventoryOwnerId = owner },
                ownership = new ItemOwnershipStateData { kind = ItemOwnershipKind.PersonOwned, ownerPersonId = owner, custodianPersonId = owner, originalOwnerId = owner, legalOwnerId = owner },
                provenance = new ItemProvenanceData { provenanceRootId = $"item-provenance.{instanceId}", creationSourceId = "inventory-equipment-projection" },
                tags = new[] { "item.instance", "source.inventory-equipment" },
                revision = 1L
            };
        }

        private static void ValidateInventoryEntries(
            InventorySaveData inventory,
            string owner,
            Dictionary<string, ItemInstanceRecordData> records,
            List<string> diagnostics,
            HashSet<string> seenLocations)
        {
            IReadOnlyList<InventoryEntrySaveData> entries = inventory?.entries != null ? inventory.entries : Array.Empty<InventoryEntrySaveData>();
            for (int i = 0; i < entries.Count; i++)
            {
                InventoryEntrySaveData entry = entries[i];
                string itemInstanceId = entry?.itemInstanceId ?? string.Empty;
                if (entry?.mode == InventoryEntrySaveMode.Empty || string.IsNullOrWhiteSpace(itemInstanceId))
                {
                    continue;
                }

                if (!records.TryGetValue(itemInstanceId, out ItemInstanceRecordData record))
                {
                    diagnostics.Add($"Inventory slot {i} contains item '{itemInstanceId}' but the identity graph has no record.");
                    continue;
                }

                if (record.location?.kind != ItemLocationKind.Inventory || !string.Equals(record.location.inventoryOwnerId, owner, StringComparison.Ordinal))
                {
                    diagnostics.Add($"Inventory slot {i} contains item '{itemInstanceId}' but identity location is {record.location?.kind}.");
                }

                if (!seenLocations.Add(itemInstanceId))
                {
                    diagnostics.Add($"Item '{itemInstanceId}' appears in multiple inventory/equipment locations.");
                }
            }
        }

        private static void ValidateEquipmentEntries(
            EquipmentSaveData equipment,
            string owner,
            Dictionary<string, ItemInstanceRecordData> records,
            List<string> diagnostics,
            HashSet<string> seenLocations)
        {
            IReadOnlyList<EquipmentSlotSaveData> slots = equipment?.slots != null ? equipment.slots : Array.Empty<EquipmentSlotSaveData>();
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotSaveData slot = slots[i];
                string itemInstanceId = slot?.itemInstanceId ?? string.Empty;
                if (slot?.mode == EquipmentEntrySaveMode.Empty || string.IsNullOrWhiteSpace(itemInstanceId))
                {
                    continue;
                }

                if (!records.TryGetValue(itemInstanceId, out ItemInstanceRecordData record))
                {
                    diagnostics.Add($"Equipment slot {slot.slotType} contains item '{itemInstanceId}' but the identity graph has no record.");
                    continue;
                }

                if (record.location?.kind != ItemLocationKind.Equipped
                    || !string.Equals(record.location.equipmentHolderId, owner, StringComparison.Ordinal)
                    || !string.Equals(record.location.equipmentSlotId, slot.slotType.ToString(), StringComparison.Ordinal))
                {
                    diagnostics.Add($"Equipment slot {slot.slotType} contains item '{itemInstanceId}' but identity location is {record.location?.kind}/{record.location?.equipmentSlotId}.");
                }

                if (!seenLocations.Add(itemInstanceId))
                {
                    diagnostics.Add($"Item '{itemInstanceId}' appears in multiple inventory/equipment locations.");
                }
            }
        }

        private static string ResolveProjectionItemInstanceId(string itemInstanceId, string deterministicSeed)
        {
            return !string.IsNullOrWhiteSpace(itemInstanceId) ? itemInstanceId : DeterministicGuid(deterministicSeed);
        }

        private static bool SequenceEquals(string[] left, string[] right)
        {
            return (left ?? Array.Empty<string>()).SequenceEqual(right ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        private static string DeterministicGuid(string seed)
        {
            using MD5 md5 = MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(seed ?? string.Empty));
            return new Guid(bytes).ToString("D");
        }
    }
}
