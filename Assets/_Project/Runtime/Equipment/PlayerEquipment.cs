using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Equipment
{
    public sealed class PlayerEquipment : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private List<EquipmentSlotState> slots = new List<EquipmentSlotState>();

        public IReadOnlyList<EquipmentSlotState> Slots => slots;
        public event Action EquipmentChanged;

        private void Awake()
        {
            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
            }

            EnsureSlots();
        }

        private void OnValidate()
        {
            EnsureSlots();
        }

        public EquipmentOperationResult EquipFromInventorySlot(int inventorySlotIndex)
        {
            EnsureInventory();
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
                string itemName = item == null ? "Item" : item.DisplayName;
                return EquipmentOperationResult.Failure($"{itemName} cannot be equipped.");
            }

            EquipmentSlotState equipmentSlot = GetSlot(item.Equipment.SlotType);
            if (equipmentSlot == null)
            {
                return EquipmentOperationResult.Failure("Equipment slot is not supported.");
            }

            ItemDefinition replacedItem = equipmentSlot.Item;
            string replacedItemInstanceId = equipmentSlot.ItemInstanceId;
            if (!CanReturnEquippedItemToInventory(replacedItem, replacedItemInstanceId, inventorySlotIndex))
            {
                return EquipmentOperationResult.Failure($"No inventory room to unequip {replacedItem.DisplayName}.");
            }

            if (!inventory.TryExtractSlotIdentity(inventorySlotIndex, out ItemDefinition extractedItem, out string extractedItemInstanceId, out string extractFailureReason))
            {
                return EquipmentOperationResult.Failure(string.IsNullOrWhiteSpace(extractFailureReason) ? $"Could not remove {item.DisplayName} from inventory." : extractFailureReason);
            }

            if (!TryReturnEquippedItemToInventory(replacedItem, replacedItemInstanceId))
            {
                RestoreExtractedInventoryItem(extractedItem, extractedItemInstanceId);
                return EquipmentOperationResult.Failure($"No inventory room to unequip {replacedItem.DisplayName}.");
            }

            equipmentSlot.SetIdentity(extractedItem, extractedItemInstanceId);

            EquipmentChanged?.Invoke();

            string message = replacedItem == null
                ? $"Equipped {extractedItem.DisplayName}."
                : $"Equipped {item.DisplayName} and unequipped {replacedItem.DisplayName}.";
            Debug.Log(message);
            return EquipmentOperationResult.Success(message);
        }

        public EquipmentOperationResult Unequip(EquipmentSlotType slotType)
        {
            EnsureInventory();
            if (inventory == null)
            {
                return EquipmentOperationResult.Failure("No inventory is assigned.");
            }

            EquipmentSlotState slot = GetSlot(slotType);
            if (slot == null || slot.IsEmpty)
            {
                return EquipmentOperationResult.Failure($"{FormatSlotName(slotType)} is empty.");
            }

            ItemDefinition item = slot.Item;
            string itemInstanceId = slot.ItemInstanceId;
            if (!string.IsNullOrWhiteSpace(itemInstanceId))
            {
                if (!inventory.CanAddExistingItemIdentity(item, itemInstanceId))
                {
                    return EquipmentOperationResult.Failure($"No inventory room to unequip {item.DisplayName}.");
                }

                InventoryInstanceOperationResult instanceResult = inventory.AddExistingItemIdentity(item, itemInstanceId);
                if (!instanceResult.Succeeded)
                {
                    return EquipmentOperationResult.Failure($"No inventory room to unequip {item.DisplayName}.");
                }

                slot.Clear();
                EquipmentChanged?.Invoke();

                string instanceMessage = $"Unequipped {item.DisplayName}.";
                Debug.Log(instanceMessage);
                return EquipmentOperationResult.Success(instanceMessage);
            }

            if (!inventory.CanAddItem(item, 1))
            {
                return EquipmentOperationResult.Failure($"No inventory room to unequip {item.DisplayName}.");
            }

            InventoryAddResult result = inventory.AddItem(item, 1);
            if (!result.AddedAll)
            {
                return EquipmentOperationResult.Failure($"No inventory room to unequip {item.DisplayName}.");
            }

            slot.Clear();
            EquipmentChanged?.Invoke();

            string message = $"Unequipped {item.DisplayName}.";
            Debug.Log(message);
            return EquipmentOperationResult.Success(message);
        }

        public bool RemoveItemForDecomposition(string itemInstanceId, bool notifyChange = true)
        {
            if (string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return false;
            }

            EnsureSlots();
            foreach (EquipmentSlotState slot in slots)
            {
                if (slot != null && string.Equals(slot.ItemInstanceId, itemInstanceId, StringComparison.Ordinal))
                {
                    slot.Clear();
                    if (notifyChange)
                    {
                        EquipmentChanged?.Invoke();
                    }
                    return true;
                }
            }

            return false;
        }

        public void NotifyEquipmentStateChanged()
        {
            EquipmentChanged?.Invoke();
        }

        public EquipmentSlotState GetSlot(EquipmentSlotType slotType)
        {
            EnsureSlots();

            foreach (EquipmentSlotState slot in slots)
            {
                if (slot.SlotType == slotType)
                {
                    return slot;
                }
            }

            return null;
        }

        public EquipmentSaveData CreateSaveData()
        {
            EnsureSlots();

            EquipmentSaveData saveData = new EquipmentSaveData();
            foreach (EquipmentSlotState slot in slots)
            {
                EquipmentSlotSaveData entry = new EquipmentSlotSaveData
                {
                    slotType = slot.SlotType
                };

                if (slot.IsEmpty)
                {
                    entry.mode = EquipmentEntrySaveMode.Empty;
                }
                else if (slot.IsStateful)
                {
                    entry.mode = EquipmentEntrySaveMode.StatefulInstance;
                    entry.definitionId = slot.Item.ItemId;
                    entry.itemInstanceId = slot.ItemInstanceId;
                }
                else
                {
                    entry.mode = EquipmentEntrySaveMode.DefinitionOnly;
                    entry.definitionId = slot.Item.ItemId;
                    entry.itemInstanceId = slot.ItemInstanceId;
                }

                saveData.slots.Add(entry);
            }

            return saveData;
        }

        public EquipmentRestoreResult TryRestoreFromSaveData(EquipmentSaveData saveData, DefinitionRegistry registry)
        {
            if (saveData == null)
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.MissingSaveData, "Equipment save data is missing.");
            }

            Dictionary<EquipmentSlotType, EquipmentSlotState> restoredBySlot = CreateEmptySlotMap();
            HashSet<EquipmentSlotType> restoredSlots = new HashSet<EquipmentSlotType>();
            HashSet<string> instanceIds = new HashSet<string>();
            IReadOnlyList<EquipmentSlotSaveData> savedSlots = saveData.slots;
            if (savedSlots == null)
            {
                savedSlots = Array.Empty<EquipmentSlotSaveData>();
            }

            for (int i = 0; i < savedSlots.Count; i++)
            {
                EquipmentSlotSaveData entry = savedSlots[i];
                if (entry == null)
                {
                    continue;
                }

                if (!restoredSlots.Add(entry.slotType))
                {
                    return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.DuplicateSlot, $"Equipment save data contains duplicate {entry.slotType} slots.");
                }

                if (!restoredBySlot.TryGetValue(entry.slotType, out EquipmentSlotState restoredSlot))
                {
                    return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.WrongSlotType, $"Equipment slot '{entry.slotType}' is not supported.");
                }

                EquipmentRestoreResult entryResult = TryApplyRestoredSlot(entry, registry, instanceIds, restoredSlot);
                if (!entryResult.Succeeded)
                {
                    return entryResult;
                }
            }

            slots = new List<EquipmentSlotState>(restoredBySlot.Count);
            Array values = Enum.GetValues(typeof(EquipmentSlotType));
            for (int i = 0; i < values.Length; i++)
            {
                slots.Add(restoredBySlot[(EquipmentSlotType)values.GetValue(i)]);
            }

            EquipmentChanged?.Invoke();
            return EquipmentRestoreResult.Success();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DevelopmentClearEquipment()
        {
            EnsureSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i]?.Clear();
            }

            EquipmentChanged?.Invoke();
        }
#endif

        private void EnsureInventory()
        {
            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
            }
        }

        private void EnsureSlots()
        {
            slots ??= new List<EquipmentSlotState>();

            Array values = Enum.GetValues(typeof(EquipmentSlotType));
            while (slots.Count < values.Length)
            {
                slots.Add(new EquipmentSlotState());
            }

            if (slots.Count > values.Length)
            {
                slots.RemoveRange(values.Length, slots.Count - values.Length);
            }

            for (int i = 0; i < values.Length; i++)
            {
                slots[i].Initialize((EquipmentSlotType)values.GetValue(i));
            }
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

        private bool CanReturnEquippedItemToInventory(ItemDefinition item, string itemInstanceId, int removingInventorySlotIndex)
        {
            if (item == null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return inventory.CanAddExistingItemIdentityAfterRemovingFromSlot(item, itemInstanceId, removingInventorySlotIndex);
            }

            return inventory.CanAddItemAfterRemovingFromSlot(item, 1, removingInventorySlotIndex, 1);
        }

        private bool TryReturnEquippedItemToInventory(ItemDefinition item, string itemInstanceId)
        {
            if (item == null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return inventory.AddExistingItemIdentity(item, itemInstanceId).Succeeded;
            }

            return inventory.AddItem(item, 1).AddedAll;
        }

        private void RestoreExtractedInventoryItem(ItemDefinition item, string itemInstanceId)
        {
            if (!string.IsNullOrWhiteSpace(itemInstanceId))
            {
                inventory.AddExistingItemIdentity(item, itemInstanceId);
                return;
            }

            inventory.AddItem(item, 1);
        }

        private static Dictionary<EquipmentSlotType, EquipmentSlotState> CreateEmptySlotMap()
        {
            Dictionary<EquipmentSlotType, EquipmentSlotState> slotMap = new Dictionary<EquipmentSlotType, EquipmentSlotState>();
            Array values = Enum.GetValues(typeof(EquipmentSlotType));
            for (int i = 0; i < values.Length; i++)
            {
                EquipmentSlotType slotType = (EquipmentSlotType)values.GetValue(i);
                EquipmentSlotState slot = new EquipmentSlotState();
                slot.Initialize(slotType);
                slotMap.Add(slotType, slot);
            }

            return slotMap;
        }

        private static EquipmentRestoreResult TryApplyRestoredSlot(
            EquipmentSlotSaveData entry,
            DefinitionRegistry registry,
            HashSet<string> instanceIds,
            EquipmentSlotState restoredSlot)
        {
            if (entry.mode == EquipmentEntrySaveMode.Empty)
            {
                return EquipmentRestoreResult.Success();
            }

            if (entry.mode == EquipmentEntrySaveMode.DefinitionOnly)
            {
                return TryApplyRestoredDefinitionItem(entry, registry, restoredSlot);
            }

            string entryItemInstanceId = entry.itemInstanceId;
            if (string.IsNullOrWhiteSpace(entryItemInstanceId))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.InvalidItemInstance, "Equipment stateful item entry has no item instance ID.");
            }

            if (string.IsNullOrWhiteSpace(entry.definitionId))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.MissingDefinitionId, "Equipment stateful entry has no definition ID.");
            }

            if (registry == null || !registry.TryGet(entry.definitionId, out ItemDefinition item))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.MissingItemDefinition, $"Item definition '{entry.definitionId}' was not found.");
            }

            EquipmentRestoreResult compatibilityResult = ValidateSlotCompatibility(item, entry.slotType);
            if (!compatibilityResult.Succeeded)
            {
                return compatibilityResult;
            }

            if (!ItemInstanceId.IsValid(entryItemInstanceId))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.InvalidItemInstance, $"Equipment item identity '{entryItemInstanceId}' is invalid.");
            }

            if (!instanceIds.Add(entryItemInstanceId))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.DuplicateInstanceId, $"Duplicate item instance ID '{entryItemInstanceId}' found in equipment save data.");
            }

            restoredSlot.SetIdentity(item, entryItemInstanceId);
            return EquipmentRestoreResult.Success();
        }

        private static EquipmentRestoreResult TryApplyRestoredDefinitionItem(
            EquipmentSlotSaveData entry,
            DefinitionRegistry registry,
            EquipmentSlotState restoredSlot)
        {
            if (string.IsNullOrWhiteSpace(entry.definitionId))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.MissingDefinitionId, "Equipment entry has no definition ID.");
            }

            if (registry == null || !registry.TryGet(entry.definitionId, out ItemDefinition item))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.MissingItemDefinition, $"Item definition '{entry.definitionId}' was not found.");
            }

            EquipmentRestoreResult compatibilityResult = ValidateSlotCompatibility(item, entry.slotType);
            if (!compatibilityResult.Succeeded)
            {
                return compatibilityResult;
            }

            string itemInstanceId = string.IsNullOrWhiteSpace(entry.itemInstanceId)
                ? ItemInstanceId.Generate()
                : entry.itemInstanceId;
            if (!ItemInstanceId.IsValid(itemInstanceId))
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.InvalidItemInstance, $"Equipment item identity '{itemInstanceId}' is invalid.");
            }

            restoredSlot.SetIdentity(item, itemInstanceId);
            return EquipmentRestoreResult.Success();
        }

        private static EquipmentRestoreResult ValidateSlotCompatibility(ItemDefinition item, EquipmentSlotType slotType)
        {
            if (item == null || !item.IsEquippable)
            {
                string itemName = item == null ? "Item" : item.DisplayName;
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.WrongDefinitionType, $"{itemName} cannot be equipped.");
            }

            if (item.Equipment.SlotType != slotType)
            {
                return EquipmentRestoreResult.Failure(EquipmentRestoreStatus.WrongSlotType, $"{item.DisplayName} belongs in {FormatSlotName(item.Equipment.SlotType)}, not {FormatSlotName(slotType)}.");
            }

            return EquipmentRestoreResult.Success();
        }
    }
}
