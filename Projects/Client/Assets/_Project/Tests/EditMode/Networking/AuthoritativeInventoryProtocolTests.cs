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
    }
}
