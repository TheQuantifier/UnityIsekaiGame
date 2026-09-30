using NUnit.Framework;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Networking;

namespace UnityIsekaiGame.Tests
{
    public sealed class AuthoritativeInventoryProtocolTests
    {
        [Test]
        public void Commands_reject_replay_invalid_slots_and_invalid_quantities()
        {
            InventoryCommandValidationResult replay = NetworkInventoryCommandValidator.Validate(
                new NetworkInventoryCommand(4u, InventoryAuthorityCommandType.UseSlot, 0),
                4u,
                16);
            Assert.That(replay.Succeeded, Is.False);
            Assert.That(replay.Failure, Is.EqualTo(InventoryAuthorityFailure.ReplayedCommand));

            InventoryCommandValidationResult slot = NetworkInventoryCommandValidator.Validate(
                new NetworkInventoryCommand(5u, InventoryAuthorityCommandType.EquipSlot, 16, equipmentSlot: 0),
                4u,
                16);
            Assert.That(slot.Succeeded, Is.False);
            Assert.That(slot.Failure, Is.EqualTo(InventoryAuthorityFailure.InvalidSlot));

            InventoryCommandValidationResult quantity = NetworkInventoryCommandValidator.Validate(
                new NetworkInventoryCommand(5u, InventoryAuthorityCommandType.DropQuantity, 0, quantity: 0),
                4u,
                16);
            Assert.That(quantity.Succeeded, Is.False);
            Assert.That(quantity.Failure, Is.EqualTo(InventoryAuthorityFailure.InvalidQuantity));

            InventoryCommandValidationResult move = NetworkInventoryCommandValidator.Validate(
                new NetworkInventoryCommand(
                    6u,
                    InventoryAuthorityCommandType.MoveSlot,
                    1,
                    destinationInventorySlotIndex: 3),
                5u,
                16);
            Assert.That(move.Succeeded, Is.True, move.Message);

            InventoryCommandValidationResult sameSlotMove = NetworkInventoryCommandValidator.Validate(
                new NetworkInventoryCommand(
                    7u,
                    InventoryAuthorityCommandType.MoveSlot,
                    1,
                    destinationInventorySlotIndex: 1),
                6u,
                16);
            Assert.That(sameSlotMove.Succeeded, Is.False);
            Assert.That(sameSlotMove.Failure, Is.EqualTo(InventoryAuthorityFailure.InvalidSlot));
        }

        [Test]
        public void Command_sequence_supports_uint_wraparound()
        {
            Assert.That(NetworkInventoryCommandValidator.IsNewer(uint.MaxValue, uint.MaxValue - 1), Is.True);
            Assert.That(NetworkInventoryCommandValidator.IsNewer(1u, uint.MaxValue), Is.True);
            Assert.That(NetworkInventoryCommandValidator.IsNewer(uint.MaxValue - 1, 1u), Is.False);
        }

        [Test]
        public void Snapshot_revision_queue_keeps_newest_callback_before_application()
        {
            uint pending = 0u;

            Assert.That(NetworkSnapshotRevisionQueue.TryQueue(12u, 10u, ref pending), Is.True);
            Assert.That(NetworkSnapshotRevisionQueue.TryQueue(11u, 10u, ref pending), Is.False);
            Assert.That(NetworkSnapshotRevisionQueue.TryQueue(12u, 10u, ref pending), Is.False);
            Assert.That(pending, Is.EqualTo(12u));
        }

        [Test]
        public void Snapshot_revision_queue_handles_wraparound_and_rejects_applied_revisions()
        {
            uint pending = 0u;

            Assert.That(NetworkSnapshotRevisionQueue.TryQueue(1u, uint.MaxValue, ref pending), Is.True);
            Assert.That(NetworkSnapshotRevisionQueue.TryQueue(uint.MaxValue, uint.MaxValue, ref pending), Is.False);
            Assert.That(NetworkSnapshotRevisionQueue.TryQueue(2u, uint.MaxValue, ref pending), Is.True);
            Assert.That(pending, Is.EqualTo(2u));
        }

        [Test]
        public void Snapshot_requires_equipment_to_reference_inventory_owned_identity()
        {
            NetworkInventorySlotState[] inventory =
            {
                new NetworkInventorySlotState(0, "item.sword", "item-instance.sword", 1, NetworkInventoryItemFlags.Stateful | NetworkInventoryItemFlags.Equippable),
                new NetworkInventorySlotState(1, string.Empty, string.Empty, 0, NetworkInventoryItemFlags.None)
            };
            NetworkEquipmentReferenceState[] equipment =
            {
                new NetworkEquipmentReferenceState(4, "item.sword", "item-instance.sword")
            };

            Assert.That(NetworkInventorySnapshotValidator.TryValidate(inventory, equipment, 2, out string failure), Is.True, failure);

            equipment[0] = new NetworkEquipmentReferenceState(4, "item.other", "item-instance.sword");
            Assert.That(NetworkInventorySnapshotValidator.TryValidate(inventory, equipment, 2, out failure), Is.False);
            Assert.That(failure, Does.Contain("matching inventory-owned"));
        }

        [Test]
        public void Snapshot_rejects_duplicate_item_identity_and_partial_empty_state()
        {
            NetworkInventorySlotState[] duplicate =
            {
                new NetworkInventorySlotState(0, "item.sword", "item-instance.shared", 1, NetworkInventoryItemFlags.Stateful),
                new NetworkInventorySlotState(1, "item.axe", "item-instance.shared", 1, NetworkInventoryItemFlags.Stateful)
            };
            Assert.That(NetworkInventorySnapshotValidator.TryValidate(duplicate, null, 2, out string duplicateFailure), Is.False);
            Assert.That(duplicateFailure, Does.Contain("duplicate item identity"));

            NetworkInventorySlotState[] partialEmpty =
            {
                new NetworkInventorySlotState(0, string.Empty, string.Empty, 1, NetworkInventoryItemFlags.None)
            };
            Assert.That(NetworkInventorySnapshotValidator.TryValidate(partialEmpty, null, 1, out string emptyFailure), Is.False);
            Assert.That(emptyFailure, Does.Contain("Empty inventory slot"));
        }

        [Test]
        public void Connected_inventory_rejects_local_mutation_but_accepts_valid_replica_snapshot()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(
                "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset");
            ItemDefinition item = catalog.DefinitionAssets.OfType<ItemDefinition>()
                .First(candidate => candidate.InstanceMode != ItemInstanceMode.AlwaysInstanced);
            GameObject owner = new GameObject("Authoritative Inventory Replica Test");
            try
            {
                PlayerInventory inventory = owner.AddComponent<PlayerInventory>();
                Assert.That(inventory.AddItem(item, 2).AddedQuantity, Is.EqualTo(2));
                InventorySaveData snapshot = inventory.CreateSaveData();

                inventory.SetExternalReplicaAuthority(true);

                Assert.That(inventory.RemoveItemAt(0, 1), Is.False);
                Assert.That(inventory.AddItem(item, 1).AddedQuantity, Is.Zero);
                Assert.That(inventory.TryRestoreFromSaveData(snapshot, catalog.CreateRegistry()).Status,
                    Is.EqualTo(InventoryRestoreStatus.ExternalAuthority));
                Assert.That(inventory.ApplyExternalReplicaSnapshot(snapshot, catalog.CreateRegistry()).Succeeded, Is.True);
                Assert.That(inventory.CountItem(item), Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Connected_equipment_rejects_local_mutation_but_accepts_valid_replica_snapshot()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(
                "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset");
            ItemDefinition item = catalog.DefinitionAssets.OfType<ItemDefinition>().First(candidate => candidate.IsEquippable);
            GameObject owner = new GameObject("Authoritative Equipment Replica Test");
            try
            {
                PlayerInventory inventory = owner.AddComponent<PlayerInventory>();
                PlayerEquipment equipment = owner.AddComponent<PlayerEquipment>();
                Assert.That(inventory.AddItemOrInstances(item, 1).AddedQuantity, Is.EqualTo(1));
                int slotIndex = Enumerable.Range(0, inventory.Slots.Count)
                    .First(index => inventory.GetSlot(index)?.Item == item);
                Assert.That(equipment.EquipFromInventorySlot(slotIndex).Succeeded, Is.True);
                EquipmentSaveData snapshot = equipment.CreateSaveData();

                inventory.SetExternalReplicaAuthority(true);
                equipment.SetExternalReplicaAuthority(true);

                Assert.That(equipment.Unequip(item.Equipment.SlotType).Succeeded, Is.False);
                Assert.That(equipment.TryRestoreFromSaveData(snapshot, catalog.CreateRegistry()).Status,
                    Is.EqualTo(EquipmentRestoreStatus.ExternalAuthority));
                Assert.That(equipment.ApplyExternalReplicaSnapshot(snapshot, catalog.CreateRegistry()).Succeeded, Is.True);
                Assert.That(equipment.GetSlot(item.Equipment.SlotType).IsEmpty, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Command_result_messages_are_bounded_to_protocol_limit()
        {
            NetworkInventoryCommandResult result = NetworkInventoryCommandResult.Reject(
                1u,
                InventoryAuthorityFailure.ServerRejected,
                new string('界', 200));

            Assert.That(Encoding.UTF8.GetByteCount(result.MessageText),
                Is.LessThanOrEqualTo(InventoryAuthorityLimits.MaximumResultMessageBytes));
        }

        [Test]
        public void World_pickup_state_requires_exact_identity_quantity_and_storage_mode()
        {
            NetworkWorldItemState valid = new NetworkWorldItemState(
                "item.wood-log",
                "c8fd91b1-77b4-4aa7-877a-ec74b2ebda2f",
                "Wood Log",
                2,
                NetworkWorldItemStorageMode.DefinitionStack);
            Assert.That(NetworkWorldItemStateValidator.TryValidate(valid, out string failure), Is.True, failure);

            NetworkWorldItemState missingIdentity = new NetworkWorldItemState(
                "item.wood-log",
                string.Empty,
                "Wood Log",
                2,
                NetworkWorldItemStorageMode.DefinitionStack);
            Assert.That(NetworkWorldItemStateValidator.TryValidate(missingIdentity, out failure), Is.False);
            Assert.That(failure, Does.Contain("instance ID"));

            NetworkWorldItemState invalidQuantity = new NetworkWorldItemState(
                "item.wood-log",
                "c8fd91b1-77b4-4aa7-877a-ec74b2ebda2f",
                "Wood Log",
                0,
                NetworkWorldItemStorageMode.DefinitionStack);
            Assert.That(NetworkWorldItemStateValidator.TryValidate(invalidQuantity, out failure), Is.False);
            Assert.That(failure, Does.Contain("quantity"));
        }

        [Test]
        public void Exact_definition_stack_identity_can_leave_and_reenter_inventory_without_becoming_stateful()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(
                "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset");
            ItemDefinition item = catalog.DefinitionAssets.OfType<ItemDefinition>()
                .First(candidate => candidate.Stackable && candidate.InstanceMode != ItemInstanceMode.AlwaysInstanced);
            GameObject owner = new GameObject("Exact Stack Identity Test");
            try
            {
                PlayerInventory inventory = owner.AddComponent<PlayerInventory>();
                Assert.That(inventory.AddItem(item, 2).AddedQuantity, Is.EqualTo(2));
                InventorySlot original = inventory.Slots.First(slot => slot != null && !slot.IsEmpty && slot.Item == item);
                string identity = original.ItemInstanceId;
                Assert.That(original.IsStateful, Is.False);
                int originalIndex = Enumerable.Range(0, inventory.Slots.Count).First(index => ReferenceEquals(inventory.Slots[index], original));
                Assert.That(inventory.RemoveItemAt(originalIndex, 2), Is.True);

                InventoryInstanceOperationResult restored = inventory.AddExistingDefinitionStackIdentity(item, identity, 2);
                Assert.That(restored.Succeeded, Is.True, restored.Message);
                InventorySlot roundTripped = inventory.GetSlot(restored.SlotIndex);
                Assert.That(roundTripped.ItemInstanceId, Is.EqualTo(identity));
                Assert.That(roundTripped.Quantity, Is.EqualTo(2));
                Assert.That(roundTripped.IsStateful, Is.False);
                Assert.That(inventory.CreateSaveData().entries[restored.SlotIndex].mode, Is.EqualTo(InventoryEntrySaveMode.DefinitionStack));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Individually_dropped_stack_items_merge_by_definition_when_recollected()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(
                "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset");
            ItemDefinition item = catalog.DefinitionAssets.OfType<ItemDefinition>()
                .First(candidate => candidate.Stackable && candidate.MaximumStackSize >= 3
                    && candidate.InstanceMode != ItemInstanceMode.AlwaysInstanced);
            GameObject owner = new GameObject("Recollected Definition Stack Test");
            try
            {
                PlayerInventory inventory = owner.AddComponent<PlayerInventory>();
                Assert.That(inventory.AddItem(item, 3).AddedAll, Is.True);
                InventorySlot original = inventory.Slots.First(slot => slot?.Item == item);
                string originalStackId = original.ItemInstanceId;
                int slotIndex = Enumerable.Range(0, inventory.Slots.Count)
                    .First(index => ReferenceEquals(inventory.Slots[index], original));

                string firstWorldPickupId = ItemInstanceId.Generate();
                Assert.That(inventory.RemoveItemAt(slotIndex, 1), Is.True);
                InventoryInstanceOperationResult firstCollection =
                    inventory.AddExistingDefinitionStackIdentity(item, firstWorldPickupId, 1);
                Assert.That(firstCollection.Succeeded, Is.True, firstCollection.Message);

                string secondWorldPickupId = ItemInstanceId.Generate();
                Assert.That(inventory.RemoveItemAt(slotIndex, 1), Is.True);
                InventoryInstanceOperationResult secondCollection =
                    inventory.AddExistingDefinitionStackIdentity(item, secondWorldPickupId, 1);
                Assert.That(secondCollection.Succeeded, Is.True, secondCollection.Message);

                InventorySlot merged = inventory.GetSlot(slotIndex);
                Assert.That(merged.Quantity, Is.EqualTo(3));
                Assert.That(merged.ItemInstanceId, Is.EqualTo(originalStackId));
                Assert.That(merged.IsStateful, Is.False);
                Assert.That(inventory.ContainsItemIdentity(firstWorldPickupId), Is.False);
                Assert.That(inventory.ContainsItemIdentity(secondWorldPickupId), Is.False);
                Assert.That(inventory.Slots.Count(slot => slot?.Item == item), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Recollected_definition_stack_keeps_world_identity_only_when_creating_a_new_stack()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(
                "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset");
            ItemDefinition item = catalog.DefinitionAssets.OfType<ItemDefinition>()
                .First(candidate => candidate.Stackable && candidate.InstanceMode != ItemInstanceMode.AlwaysInstanced);
            GameObject owner = new GameObject("New Recollected Stack Identity Test");
            try
            {
                PlayerInventory inventory = owner.AddComponent<PlayerInventory>();
                string worldPickupId = ItemInstanceId.Generate();

                InventoryInstanceOperationResult collection =
                    inventory.AddExistingDefinitionStackIdentity(item, worldPickupId, 1);

                Assert.That(collection.Succeeded, Is.True, collection.Message);
                InventorySlot created = inventory.GetSlot(collection.SlotIndex);
                Assert.That(created.ItemInstanceId, Is.EqualTo(worldPickupId));
                Assert.That(created.Quantity, Is.EqualTo(1));
                Assert.That(created.IsStateful, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Separated_definition_stacks_merge_by_drag_destination_and_drop_remains_valid()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(
                "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset");
            ItemDefinition arrows = catalog.DefinitionAssets.OfType<ItemDefinition>()
                .First(candidate => candidate.ItemId == "item.prototype-arrow");
            GameObject owner = new GameObject("Separated Arrow Stack Test");
            try
            {
                PlayerInventory inventory = owner.AddComponent<PlayerInventory>();
                string firstIdentity = ItemInstanceId.Generate();
                string secondIdentity = ItemInstanceId.Generate();
                InventorySaveData separated = new InventorySaveData { slotCapacity = 4 };
                separated.entries.Add(new InventoryEntrySaveData
                {
                    mode = InventoryEntrySaveMode.DefinitionStack,
                    definitionId = arrows.ItemId,
                    itemInstanceId = firstIdentity,
                    quantity = 1
                });
                separated.entries.Add(new InventoryEntrySaveData
                {
                    mode = InventoryEntrySaveMode.DefinitionStack,
                    definitionId = arrows.ItemId,
                    itemInstanceId = secondIdentity,
                    quantity = 1
                });
                separated.entries.Add(new InventoryEntrySaveData());
                separated.entries.Add(new InventoryEntrySaveData());
                Assert.That(inventory.TryRestoreFromSaveData(separated, catalog.CreateRegistry()).Succeeded, Is.True);

                InventoryInstanceOperationResult merged = inventory.MoveOrMergeSlot(0, 1);

                Assert.That(merged.Succeeded, Is.True, merged.Message);
                Assert.That(inventory.GetSlot(0).IsEmpty, Is.True);
                Assert.That(inventory.GetSlot(1).Quantity, Is.EqualTo(2));
                Assert.That(inventory.GetSlot(1).ItemInstanceId, Is.EqualTo(secondIdentity));
                Assert.That(inventory.ContainsItemIdentity(firstIdentity), Is.False);
                Assert.That(inventory.RemoveItemAt(1, 1), Is.True,
                    "A merged definition stack must remain quantity-droppable.");
                Assert.That(inventory.GetSlot(1).Quantity, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Inventory_drag_moves_to_empty_slots_and_swaps_incompatible_contents_without_changing_identity()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(
                "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset");
            ItemDefinition arrows = catalog.DefinitionAssets.OfType<ItemDefinition>()
                .First(candidate => candidate.ItemId == "item.prototype-arrow");
            ItemDefinition wood = catalog.DefinitionAssets.OfType<ItemDefinition>()
                .First(candidate => candidate.ItemId == "item.wood-log");
            GameObject owner = new GameObject("Inventory Move And Swap Test");
            try
            {
                PlayerInventory inventory = owner.AddComponent<PlayerInventory>();
                Assert.That(inventory.AddItem(arrows, 2).AddedAll, Is.True);
                Assert.That(inventory.AddItem(wood, 3).AddedAll, Is.True);
                int arrowSlot = Enumerable.Range(0, inventory.Slots.Count).First(index => inventory.GetSlot(index).Item == arrows);
                int woodSlot = Enumerable.Range(0, inventory.Slots.Count).First(index => inventory.GetSlot(index).Item == wood);
                int emptySlot = Enumerable.Range(0, inventory.Slots.Count).First(index => inventory.GetSlot(index).IsEmpty);
                string arrowIdentity = inventory.GetSlot(arrowSlot).ItemInstanceId;
                string woodIdentity = inventory.GetSlot(woodSlot).ItemInstanceId;

                Assert.That(inventory.MoveOrMergeSlot(arrowSlot, emptySlot).Succeeded, Is.True);
                Assert.That(inventory.GetSlot(arrowSlot).IsEmpty, Is.True);
                Assert.That(inventory.GetSlot(emptySlot).Item, Is.SameAs(arrows));
                Assert.That(inventory.GetSlot(emptySlot).ItemInstanceId, Is.EqualTo(arrowIdentity));

                Assert.That(inventory.MoveOrMergeSlot(emptySlot, woodSlot).Succeeded, Is.True);
                Assert.That(inventory.GetSlot(emptySlot).Item, Is.SameAs(wood));
                Assert.That(inventory.GetSlot(emptySlot).ItemInstanceId, Is.EqualTo(woodIdentity));
                Assert.That(inventory.GetSlot(woodSlot).Item, Is.SameAs(arrows));
                Assert.That(inventory.GetSlot(woodSlot).ItemInstanceId, Is.EqualTo(arrowIdentity));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
