using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Networking.Server
{
    [DisallowMultipleComponent]
    public sealed class ServerPlayerInventoryAuthority : MonoBehaviour
    {
        private NetworkPlayerInventory networkInventory;
        private NetworkPlayerVitals networkVitals;
        private PlayerInventory inventory;
        private PlayerEquipment equipment;
        private DefinitionRegistry registry;
        private Action<InventorySaveData, EquipmentSaveData> statePersisted;
        private bool executingCommand;
        private bool configured;

        public PlayerInventory Inventory => inventory;
        public PlayerEquipment Equipment => equipment;
        public InventorySaveData CreateInventorySaveData() => inventory?.CreateSaveData();
        public EquipmentSaveData CreateEquipmentSaveData() => equipment?.CreateSaveData();

        public void Configure(
            NetworkPlayerInventory replicatedInventory,
            NetworkPlayerVitals replicatedVitals,
            DefinitionRegistry definitionRegistry,
            InventorySaveData initialInventory,
            EquipmentSaveData initialEquipment,
            Action<InventorySaveData, EquipmentSaveData> onStatePersisted)
        {
            if (configured) throw new InvalidOperationException("Server inventory authority is already configured.");
            networkInventory = replicatedInventory ?? throw new ArgumentNullException(nameof(replicatedInventory));
            networkVitals = replicatedVitals ?? throw new ArgumentNullException(nameof(replicatedVitals));
            registry = definitionRegistry ?? throw new ArgumentNullException(nameof(definitionRegistry));
            statePersisted = onStatePersisted;

            inventory = GetComponent<PlayerInventory>();
            if (inventory == null) inventory = gameObject.AddComponent<PlayerInventory>();
            equipment = GetComponent<PlayerEquipment>();
            if (equipment == null) equipment = gameObject.AddComponent<PlayerEquipment>();

            InventoryRestoreResult inventoryRestore = inventory.TryRestoreFromSaveData(initialInventory, registry);
            if (!inventoryRestore.Succeeded)
            {
                throw new InvalidOperationException($"Could not initialize authoritative inventory: {inventoryRestore.Message}");
            }

            EquipmentRestoreResult equipmentRestore = equipment.TryRestoreFromSaveData(initialEquipment, registry);
            if (!equipmentRestore.Succeeded)
            {
                throw new InvalidOperationException($"Could not initialize authoritative equipment: {equipmentRestore.Message}");
            }

            networkInventory.ServerCommandHandler = ExecuteCommand;
            inventory.InventoryChanged += OnDomainStateChanged;
            equipment.EquipmentChanged += OnDomainStateChanged;
            configured = true;

            BuildSnapshot(out List<NetworkInventorySlotState> inventoryState, out List<NetworkEquipmentReferenceState> equipmentState);
            networkInventory.ConfigureInitialSnapshotServer(inventoryState, equipmentState, inventory.SlotCapacity);
            PersistState();
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.InventoryChanged -= OnDomainStateChanged;
            if (equipment != null) equipment.EquipmentChanged -= OnDomainStateChanged;
            if (networkInventory != null) networkInventory.ServerCommandHandler = null;
        }

        private NetworkInventoryCommandResult ExecuteCommand(NetworkInventoryCommand command)
        {
            executingCommand = true;
            try
            {
                NetworkInventoryCommandResult result = command.CommandType switch
                {
                    InventoryAuthorityCommandType.UseSlot => Use(command),
                    InventoryAuthorityCommandType.EquipSlot => Equip(command),
                    InventoryAuthorityCommandType.UnequipSlot => Unequip(command),
                    InventoryAuthorityCommandType.DropQuantity => Drop(command),
                    _ => NetworkInventoryCommandResult.Reject(command.Sequence, InventoryAuthorityFailure.InvalidCommand, "Unsupported inventory command.")
                };

                if (result.Succeeded)
                {
                    PublishAndPersist();
                }

                return result;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return NetworkInventoryCommandResult.Reject(command.Sequence, InventoryAuthorityFailure.ServerRejected, "The server could not complete the inventory action.");
            }
            finally
            {
                executingCommand = false;
            }
        }

        private NetworkInventoryCommandResult Use(NetworkInventoryCommand command)
        {
            InventorySlot slot = inventory.GetSlot(command.InventorySlotIndex);
            if (slot == null || slot.IsEmpty || slot.Item == null)
            {
                return RejectUnavailable(command, "Selected inventory slot is empty.");
            }

            if (equipment.IsItemEquipped(slot.ItemInstanceId))
            {
                return RejectNotAllowed(command, "Unequip this item before using it.");
            }

            ItemDefinition item = slot.Item;
            if (!item.IsUsable || item.HasMissingUseEffect)
            {
                return RejectNotAllowed(command, $"{item.DisplayName} cannot be used.");
            }

            float health = 0f;
            float stamina = 0f;
            float mana = 0f;
            IReadOnlyList<ItemUseEffect> effects = item.UseEffects;
            for (int i = 0; i < effects.Count; i++)
            {
                switch (effects[i])
                {
                    case RestoreHealthItemUseEffect restoreHealth:
                        health += restoreHealth.HealingAmount;
                        break;
                    case RestoreVitalItemUseEffect restoreVital when restoreVital.RestoreEffect != null:
                        float amount = restoreVital.RestoreEffect.Amount;
                        switch (restoreVital.RestoreEffect.VitalType)
                        {
                            case VitalType.Health: health += amount; break;
                            case VitalType.Stamina: stamina += amount; break;
                            case VitalType.Mana: mana += amount; break;
                            default: return RejectNotAllowed(command, $"{item.DisplayName} has an unsupported vital effect.");
                        }
                        break;
                    default:
                        return RejectNotAllowed(command, $"{item.DisplayName} has an item effect that is not server-authoritative yet.");
                }
            }

            NetworkVitalsState current = networkVitals.CurrentState;
            bool canChange = health > 0f && current.Health < current.MaximumHealth
                || stamina > 0f && current.Stamina < current.MaximumStamina
                || mana > 0f && current.Mana < current.MaximumMana;
            if (!canChange)
            {
                return RejectNotAllowed(command, $"{item.DisplayName} would not change any resource.");
            }

            bool changed = false;
            if (health > 0f) changed |= networkVitals.TryHealServer(health);
            if (stamina > 0f) changed |= networkVitals.TryRestoreStaminaServer(stamina);
            if (mana > 0f) changed |= networkVitals.TryRestoreManaServer(mana);
            if (!changed || !inventory.RemoveItemAt(command.InventorySlotIndex, 1))
            {
                return NetworkInventoryCommandResult.Reject(command.Sequence, InventoryAuthorityFailure.ServerRejected, $"{item.DisplayName} could not be consumed.");
            }

            return NetworkInventoryCommandResult.Success(command.Sequence, $"Used {item.DisplayName}.");
        }

        private NetworkInventoryCommandResult Equip(NetworkInventoryCommand command)
        {
            InventorySlot slot = inventory.GetSlot(command.InventorySlotIndex);
            if (slot == null || slot.IsEmpty || slot.Item == null)
            {
                return RejectUnavailable(command, "Selected inventory slot is empty.");
            }

            if (!slot.Item.IsEquippable || (int)slot.Item.Equipment.SlotType != command.EquipmentSlot)
            {
                return RejectNotAllowed(command, "The item does not belong in the requested equipment slot.");
            }

            EquipmentOperationResult result = equipment.EquipFromInventorySlot(command.InventorySlotIndex);
            return result.Succeeded
                ? NetworkInventoryCommandResult.Success(command.Sequence, result.Message)
                : RejectNotAllowed(command, result.Message);
        }

        private NetworkInventoryCommandResult Unequip(NetworkInventoryCommand command)
        {
            if (!Enum.IsDefined(typeof(EquipmentSlotType), command.EquipmentSlot))
            {
                return NetworkInventoryCommandResult.Reject(command.Sequence, InventoryAuthorityFailure.InvalidEquipmentSlot, "Equipment slot is invalid.");
            }

            EquipmentOperationResult result = equipment.Unequip((EquipmentSlotType)command.EquipmentSlot);
            return result.Succeeded
                ? NetworkInventoryCommandResult.Success(command.Sequence, result.Message)
                : RejectUnavailable(command, result.Message);
        }

        private NetworkInventoryCommandResult Drop(NetworkInventoryCommand command)
        {
            InventorySlot slot = inventory.GetSlot(command.InventorySlotIndex);
            if (slot == null || slot.IsEmpty || slot.Item == null || slot.Quantity < command.Quantity)
            {
                return RejectUnavailable(command, "The requested item quantity is unavailable.");
            }

            if (equipment.IsItemEquipped(slot.ItemInstanceId))
            {
                return RejectNotAllowed(command, "Unequip this item before dropping it.");
            }

            string itemName = slot.Item.DisplayName;
            if (!inventory.RemoveItemAt(command.InventorySlotIndex, command.Quantity))
            {
                return NetworkInventoryCommandResult.Reject(command.Sequence, InventoryAuthorityFailure.ServerRejected, "The server could not remove the dropped item.");
            }

            // World pickup replication belongs to the world-entity authority slice. Until then,
            // the authoritative drop is intentionally a discard instead of creating a client-owned pickup.
            string message = command.Quantity == 1
                ? $"Dropped 1 {itemName}."
                : $"Dropped {command.Quantity} {itemName}.";
            return NetworkInventoryCommandResult.Success(command.Sequence, message);
        }

        private void OnDomainStateChanged()
        {
            if (configured && !executingCommand && networkInventory != null && networkInventory.IsSpawned)
            {
                PublishAndPersist();
            }
        }

        private void PublishAndPersist()
        {
            BuildSnapshot(out List<NetworkInventorySlotState> inventoryState, out List<NetworkEquipmentReferenceState> equipmentState);
            if (!networkInventory.PublishServerSnapshot(inventoryState, equipmentState, inventory.SlotCapacity))
            {
                throw new InvalidOperationException("The authoritative inventory snapshot failed validation.");
            }

            PersistState();
        }

        private void PersistState()
        {
            statePersisted?.Invoke(inventory.CreateSaveData(), equipment.CreateSaveData());
        }

        private void BuildSnapshot(
            out List<NetworkInventorySlotState> inventoryState,
            out List<NetworkEquipmentReferenceState> equipmentState)
        {
            inventoryState = new List<NetworkInventorySlotState>(inventory.Slots.Count);
            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                InventorySlot slot = inventory.Slots[i];
                if (slot == null || slot.IsEmpty || slot.Item == null)
                {
                    inventoryState.Add(new NetworkInventorySlotState(i, string.Empty, string.Empty, 0, NetworkInventoryItemFlags.None));
                    continue;
                }

                NetworkInventoryItemFlags flags = NetworkInventoryItemFlags.None;
                if (slot.IsStateful) flags |= NetworkInventoryItemFlags.Stateful;
                if (slot.Item.IsUsable) flags |= NetworkInventoryItemFlags.Usable;
                if (slot.Item.IsEquippable) flags |= NetworkInventoryItemFlags.Equippable;
                inventoryState.Add(new NetworkInventorySlotState(i, slot.Item.ItemId, slot.ItemInstanceId, slot.Quantity, flags));
            }

            equipmentState = new List<NetworkEquipmentReferenceState>(equipment.Slots.Count);
            for (int i = 0; i < equipment.Slots.Count; i++)
            {
                EquipmentSlotState slot = equipment.Slots[i];
                equipmentState.Add(slot == null || slot.IsEmpty || slot.Item == null
                    ? new NetworkEquipmentReferenceState((int)(slot?.SlotType ?? (EquipmentSlotType)i), string.Empty, string.Empty)
                    : new NetworkEquipmentReferenceState((int)slot.SlotType, slot.Item.ItemId, slot.ItemInstanceId));
            }
        }

        private static NetworkInventoryCommandResult RejectUnavailable(NetworkInventoryCommand command, string message)
        {
            return NetworkInventoryCommandResult.Reject(command.Sequence, InventoryAuthorityFailure.ItemUnavailable, message);
        }

        private static NetworkInventoryCommandResult RejectNotAllowed(NetworkInventoryCommand command, string message)
        {
            return NetworkInventoryCommandResult.Reject(command.Sequence, InventoryAuthorityFailure.NotAllowed, message);
        }
    }
}
