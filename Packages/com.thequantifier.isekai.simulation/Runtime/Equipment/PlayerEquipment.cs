using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Equipment
{
    /// <summary>
    /// Slot-based references into the player's canonical inventory. Equipment never owns or
    /// transfers an item instance; it only identifies which inventory instance occupies each slot.
    /// </summary>
    public sealed class PlayerEquipment : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private List<EquipmentSlotState> slots = new List<EquipmentSlotState>();
        private PlayerInventory subscribedInventory;
        private bool externalReplicaAuthorityActive;

        public IReadOnlyList<EquipmentSlotState> Slots => slots;
        public bool ExternalReplicaAuthorityActive => externalReplicaAuthorityActive;
        public event Action EquipmentChanged;

        private void Awake()
        {
            EnsureInventory();
            EnsureSlots();
            EnsureInventorySubscription();
        }

        private void OnEnable()
        {
            EnsureInventory();
            EnsureInventorySubscription();

            RemoveDanglingReferences(false);
        }

        private void OnDisable()
        {
            if (subscribedInventory != null) subscribedInventory.InventoryMutated -= HandleInventoryChanged;
            subscribedInventory = null;
        }

        private void OnValidate()
        {
            EnsureSlots();
        }

        public EquipmentOperationResult EquipFromInventorySlot(int inventorySlotIndex)
        {
            if (externalReplicaAuthorityActive)
            {
                return EquipmentOperationResult.Failure("Equipment is controlled by the server.");
            }

            EnsureInventory();
            EnsureInventorySubscription();
            if (inventory == null)
            {
                return EquipmentOperationResult.Failure("No inventory is assigned.");
            }

            InventorySlot inventorySlot = inventory.GetSlot(inventorySlotIndex);
            if (inventorySlot == null || inventorySlot.IsEmpty)
            {
                return EquipmentOperationResult.Failure("Selected inventory slot is empty.");
            }

            ItemDefinition item = inventorySlot.Item;
            if (item == null || !item.IsEquippable)
            {
                return EquipmentOperationResult.Failure($"{(item == null ? "Item" : item.DisplayName)} cannot be equipped.");
            }

            if (item.Stackable || inventorySlot.Quantity != 1)
            {
                return EquipmentOperationResult.Failure($"{item.DisplayName} cannot be equipped from a stack.");
            }

            string instanceId = inventorySlot.ItemInstanceId;
            if (!ItemInstanceId.IsValid(instanceId))
            {
                return EquipmentOperationResult.Failure($"{item.DisplayName} has no valid item identity.");
            }

            EquipmentSlotState target = GetSlot(item.Equipment.SlotType);
            if (target == null)
            {
                return EquipmentOperationResult.Failure("Equipment slot is not supported.");
            }

            bool repairedDuplicateReference = ClearDuplicateReferences(instanceId, target);
            if (string.Equals(target.ItemInstanceId, instanceId, StringComparison.Ordinal))
            {
                if (repairedDuplicateReference) EquipmentChanged?.Invoke();
                return EquipmentOperationResult.Success($"{item.DisplayName} is already equipped.");
            }

            string replacedName = target.IsEmpty ? string.Empty : target.Item.DisplayName;
            target.SetIdentity(item, instanceId);
            EquipmentChanged?.Invoke();

            string message = string.IsNullOrWhiteSpace(replacedName)
                ? $"Equipped {item.DisplayName}."
                : $"Equipped {item.DisplayName} and unequipped {replacedName}.";
            Debug.Log(message);
            return EquipmentOperationResult.Success(message);
        }

        public EquipmentOperationResult Unequip(EquipmentSlotType slotType)
        {
            if (externalReplicaAuthorityActive)
            {
                return EquipmentOperationResult.Failure("Equipment is controlled by the server.");
            }

            EquipmentSlotState slot = GetSlot(slotType);
            if (slot == null || slot.IsEmpty)
            {
                return EquipmentOperationResult.Failure($"{FormatSlotName(slotType)} is empty.");
            }

            string itemName = slot.Item.DisplayName;
            slot.Clear();
            EquipmentChanged?.Invoke();
            string message = $"Unequipped {itemName}.";
            Debug.Log(message);
            return EquipmentOperationResult.Success(message);
        }

        public bool IsItemEquipped(string itemInstanceId)
        {
            return TryGetSlotForItem(itemInstanceId, out _);
        }

        public bool TryGetSlotForItem(string itemInstanceId, out EquipmentSlotState equipmentSlot)
        {
            equipmentSlot = null;
            if (string.IsNullOrWhiteSpace(itemInstanceId)) return false;
            EnsureSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotState candidate = slots[i];
                if (candidate != null && !candidate.IsEmpty
                    && string.Equals(candidate.ItemInstanceId, itemInstanceId, StringComparison.Ordinal))
                {
                    equipmentSlot = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool RemoveItemForDecomposition(string itemInstanceId, bool notifyChange = true)
        {
            if (externalReplicaAuthorityActive) return false;
            if (!TryGetSlotForItem(itemInstanceId, out EquipmentSlotState slot)) return false;
            slot.Clear();
            if (notifyChange) EquipmentChanged?.Invoke();
            return true;
        }

        public void NotifyEquipmentStateChanged()
        {
            if (externalReplicaAuthorityActive) return;
            EquipmentChanged?.Invoke();
        }

        public EquipmentSlotState GetSlot(EquipmentSlotType slotType)
        {
            EnsureSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].SlotType == slotType) return slots[i];
            }

            return null;
        }

        public EquipmentSaveData CreateSaveData()
        {
            EnsureInventory();
            EnsureSlots();
            RemoveDanglingReferences(false);
            EquipmentSaveData saveData = new EquipmentSaveData();
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotState slot = slots[i];
                saveData.slots.Add(new EquipmentSlotSaveData
                {
                    slotType = slot.SlotType,
                    mode = slot.IsEmpty ? EquipmentEntrySaveMode.Empty : EquipmentEntrySaveMode.InventoryReference,
                    definitionId = slot.IsEmpty ? string.Empty : slot.Item.ItemId,
                    itemInstanceId = slot.IsEmpty ? string.Empty : slot.ItemInstanceId
                });
            }

            return saveData;
        }

        public EquipmentRestoreResult TryRestoreFromSaveData(EquipmentSaveData saveData, DefinitionRegistry registry)
        {
            if (externalReplicaAuthorityActive)
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.ExternalAuthority, "Equipment is controlled by the server.");
            }

            return RestoreFromSaveDataCore(saveData, registry, true);
        }

        public EquipmentRestoreResult ApplyExternalReplicaSnapshot(
            EquipmentSaveData saveData,
            DefinitionRegistry registry,
            bool notifyChange = true)
        {
            if (!externalReplicaAuthorityActive)
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.ExternalAuthority, "External replica authority is not active.");
            }

            return RestoreFromSaveDataCore(saveData, registry, notifyChange);
        }

        public void SetExternalReplicaAuthority(bool active)
        {
            externalReplicaAuthorityActive = active;
        }

        public void NotifyExternalReplicaChanged()
        {
            if (externalReplicaAuthorityActive) EquipmentChanged?.Invoke();
        }

        private EquipmentRestoreResult RestoreFromSaveDataCore(
            EquipmentSaveData saveData,
            DefinitionRegistry registry,
            bool notifyChange)
        {
            EnsureInventory();
            EnsureInventorySubscription();
            if (saveData == null)
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.MissingSaveData, "Equipment save data is missing.");
            }

            Dictionary<EquipmentSlotType, EquipmentSlotState> restoredBySlot = CreateEmptySlotMap();
            HashSet<EquipmentSlotType> restoredSlots = new HashSet<EquipmentSlotType>();
            HashSet<string> equippedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<EquipmentSlotSaveData> savedSlots = saveData.slots ?? (IReadOnlyList<EquipmentSlotSaveData>)Array.Empty<EquipmentSlotSaveData>();

            for (int i = 0; i < savedSlots.Count; i++)
            {
                EquipmentSlotSaveData entry = savedSlots[i];
                if (entry == null) continue;
                if (!restoredSlots.Add(entry.slotType))
                {
                    return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.DuplicateSlot, $"Equipment save data contains duplicate {entry.slotType} slots.");
                }

                if (!restoredBySlot.TryGetValue(entry.slotType, out EquipmentSlotState restoredSlot))
                {
                    return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.WrongSlotType, $"Equipment slot '{entry.slotType}' is not supported.");
                }

                EquipmentRestoreResult result = TryApplyRestoredReference(entry, registry, equippedInstanceIds, restoredSlot);
                if (!result.Succeeded) return result;
            }

            slots = new List<EquipmentSlotState>(restoredBySlot.Count);
            Array values = Enum.GetValues(typeof(EquipmentSlotType));
            for (int i = 0; i < values.Length; i++) slots.Add(restoredBySlot[(EquipmentSlotType)values.GetValue(i)]);
            if (notifyChange) EquipmentChanged?.Invoke();
            return EquipmentRestoreResult.Success();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DevelopmentClearEquipment()
        {
            if (externalReplicaAuthorityActive) return;
            EnsureSlots();
            for (int i = 0; i < slots.Count; i++) slots[i]?.Clear();
            EquipmentChanged?.Invoke();
        }
#endif

        private void HandleInventoryChanged()
        {
            RemoveDanglingReferences(true);
        }

        private void RemoveDanglingReferences(bool notify)
        {
            if (inventory == null) return;
            EnsureSlots();
            bool changed = false;
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotState slot = slots[i];
                if (slot == null || slot.IsEmpty) continue;
                if (inventory.TryGetItemIdentity(slot.ItemInstanceId, out InventorySlot ownedItem, out _)
                    && ownedItem.Item == slot.Item)
                {
                    continue;
                }

                slot.Clear();
                changed = true;
            }

            if (changed && notify) EquipmentChanged?.Invoke();
        }

        private void EnsureInventory()
        {
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
        }

        private void EnsureInventorySubscription()
        {
            if (subscribedInventory == inventory) return;
            if (subscribedInventory != null) subscribedInventory.InventoryMutated -= HandleInventoryChanged;
            subscribedInventory = inventory;
            if (subscribedInventory != null) subscribedInventory.InventoryMutated += HandleInventoryChanged;
        }

        private void EnsureSlots()
        {
            slots ??= new List<EquipmentSlotState>();
            Array values = Enum.GetValues(typeof(EquipmentSlotType));
            while (slots.Count < values.Length) slots.Add(new EquipmentSlotState());
            if (slots.Count > values.Length) slots.RemoveRange(values.Length, slots.Count - values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                slots[i] ??= new EquipmentSlotState();
                slots[i].Initialize((EquipmentSlotType)values.GetValue(i));
            }
        }

        private bool ClearDuplicateReferences(string itemInstanceId, EquipmentSlotState target)
        {
            bool changed = false;
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotState slot = slots[i];
                if (slot == null || ReferenceEquals(slot, target)
                    || !string.Equals(slot.ItemInstanceId, itemInstanceId, StringComparison.Ordinal))
                {
                    continue;
                }

                slot.Clear();
                changed = true;
            }

            return changed;
        }

        private EquipmentRestoreResult TryApplyRestoredReference(
            EquipmentSlotSaveData entry,
            DefinitionRegistry registry,
            HashSet<string> equippedInstanceIds,
            EquipmentSlotState restoredSlot)
        {
            if (entry.mode == EquipmentEntrySaveMode.Empty) return EquipmentRestoreResult.Success();
            if (entry.mode != EquipmentEntrySaveMode.InventoryReference)
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.InvalidItemInstance, "Equipment entries must reference inventory-owned item instances.");
            }

            if (string.IsNullOrWhiteSpace(entry.definitionId))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.MissingDefinitionId, "Equipment reference has no definition ID.");
            }

            if (registry == null || !registry.TryGet(entry.definitionId, out ItemDefinition item))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.MissingItemDefinition, $"Item definition '{entry.definitionId}' was not found.");
            }

            EquipmentRestoreResult compatibility = ValidateSlotCompatibility(item, entry.slotType);
            if (!compatibility.Succeeded) return compatibility;
            if (!ItemInstanceId.IsValid(entry.itemInstanceId))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.InvalidItemInstance, $"Equipment item identity '{entry.itemInstanceId}' is invalid.");
            }

            if (!equippedInstanceIds.Add(entry.itemInstanceId))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.DuplicateInstanceId, $"Item instance '{entry.itemInstanceId}' is referenced by multiple equipment slots.");
            }

            if (inventory == null || !inventory.TryGetItemIdentity(entry.itemInstanceId, out InventorySlot ownedItem, out _)
                || ownedItem.Item != item)
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.InvalidItemInstance, $"Equipment reference '{entry.itemInstanceId}' does not match an inventory-owned {item.DisplayName}.");
            }

            restoredSlot.SetIdentity(item, entry.itemInstanceId);
            return EquipmentRestoreResult.Success();
        }

        private static Dictionary<EquipmentSlotType, EquipmentSlotState> CreateEmptySlotMap()
        {
            Dictionary<EquipmentSlotType, EquipmentSlotState> result = new Dictionary<EquipmentSlotType, EquipmentSlotState>();
            Array values = Enum.GetValues(typeof(EquipmentSlotType));
            for (int i = 0; i < values.Length; i++)
            {
                EquipmentSlotType type = (EquipmentSlotType)values.GetValue(i);
                EquipmentSlotState slot = new EquipmentSlotState();
                slot.Initialize(type);
                result.Add(type, slot);
            }

            return result;
        }

        private static EquipmentRestoreResult ValidateSlotCompatibility(ItemDefinition item, EquipmentSlotType slotType)
        {
            if (item == null || !item.IsEquippable)
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.WrongDefinitionType, $"{(item == null ? "Item" : item.DisplayName)} cannot be equipped.");
            }

            if (item.Equipment.SlotType != slotType)
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.WrongSlotType, $"{item.DisplayName} belongs in {FormatSlotName(item.Equipment.SlotType)}, not {FormatSlotName(slotType)}.");
            }

            return EquipmentRestoreResult.Success();
        }

        private static string FormatSlotName(EquipmentSlotType slotType)
        {
            return slotType switch
            {
                EquipmentSlotType.MainHand => "Main Hand",
                EquipmentSlotType.OffHand => "Off Hand",
                _ => slotType.ToString()
            };
        }
    }
}
