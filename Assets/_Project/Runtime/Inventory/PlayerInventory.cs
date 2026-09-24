using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Inventory
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField, Min(1)] private int slotCapacity = 16;
        [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

        public IReadOnlyList<InventorySlot> Slots => slots;
        public int SlotCapacity => slotCapacity;
        public event Action InventoryChanged;

        private void Awake()
        {
            EnsureSlotCapacity();
        }

        private void OnValidate()
        {
            slotCapacity = Mathf.Max(1, slotCapacity);
            EnsureSlotCapacity();
        }

        public InventoryAddResult AddItem(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0)
            {
                return new InventoryAddResult(InventoryAddStatus.None, Mathf.Max(0, quantity), 0);
            }

            if (item.InstanceMode == ItemInstanceMode.AlwaysInstanced)
            {
                Debug.LogWarning($"Cannot add always-instanced item '{item.ItemId}' through the definition stack API.");
                return new InventoryAddResult(InventoryAddStatus.None, quantity, 0);
            }

            EnsureSlotCapacity();

            int requestedQuantity = quantity;
            int remainingQuantity = quantity;

            if (item.Stackable)
            {
                remainingQuantity = AddToExistingStacks(item, remainingQuantity);
            }

            remainingQuantity = AddToNewStacks(item, remainingQuantity);

            int addedQuantity = requestedQuantity - remainingQuantity;
            InventoryAddStatus status = GetAddStatus(requestedQuantity, addedQuantity);

            LogAddResult(item, new InventoryAddResult(status, requestedQuantity, addedQuantity));

            if (addedQuantity > 0)
            {
                InventoryChanged?.Invoke();
            }

            return new InventoryAddResult(status, requestedQuantity, addedQuantity);
        }

        public InventoryInstanceOperationResult AddExistingItemIdentity(ItemDefinition item, string itemInstanceId, int quantity = 1)
        {
            if (!CanAddItemIdentity(item, itemInstanceId, quantity, out string failureReason))
            {
                return InventoryInstanceOperationResult.Failure(failureReason);
            }

            int emptySlotIndex = FindEmptySlotIndex();
            slots[emptySlotIndex].SetIdentity(item, itemInstanceId, Mathf.Max(1, quantity));
            InventoryChanged?.Invoke();

            return InventoryInstanceOperationResult.Success($"Added {item.DisplayName}.", emptySlotIndex);
        }

        public bool CanAddExistingItemIdentity(ItemDefinition item, string itemInstanceId, int quantity = 1)
        {
            return CanAddItemIdentity(item, itemInstanceId, quantity, out _);
        }

        public bool CanAddExistingItemIdentityAfterRemovingFromSlot(ItemDefinition item, string itemInstanceId, int removeSlotIndex, int quantity = 1)
        {
            return CanAddItemIdentityAfterRemovingFromSlot(item, itemInstanceId, removeSlotIndex, quantity, out _);
        }

        public InventorySlot GetSlot(int slotIndex)
        {
            EnsureSlotCapacity();
            return slotIndex >= 0 && slotIndex < slots.Count ? slots[slotIndex] : null;
        }

        public bool ContainsItemIdentity(string itemInstanceId)
        {
            if (string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return false;
            }

            EnsureSlotCapacity();
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot != null && string.Equals(slot.ItemInstanceId, itemInstanceId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryExtractSlotIdentity(int slotIndex, out ItemDefinition item, out string itemInstanceId, out string failureReason)
        {
            item = null;
            itemInstanceId = string.Empty;
            failureReason = string.Empty;
            EnsureSlotCapacity();

            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                failureReason = "No inventory slot selected.";
                return false;
            }

            InventorySlot slot = slots[slotIndex];
            if (slot == null || slot.IsEmpty)
            {
                failureReason = "Selected inventory slot is empty.";
                return false;
            }

            item = slot.Item;
            itemInstanceId = slot.ItemInstanceId;
            if (item == null)
            {
                failureReason = "Selected item identity is invalid.";
                return false;
            }

            if (slot.IsStateful)
            {
                slot.Clear();
                InventoryChanged?.Invoke();
                return true;
            }

            if (!slot.Remove(1))
            {
                failureReason = item == null ? "Could not remove item from inventory." : $"Could not remove {item.DisplayName} from inventory.";
                return false;
            }

            InventoryChanged?.Invoke();
            return true;
        }

        public InventorySaveData CreateSaveData()
        {
            EnsureSlotCapacity();

            InventorySaveData saveData = new InventorySaveData
            {
                slotCapacity = slotCapacity
            };

            foreach (InventorySlot slot in slots)
            {
                InventoryEntrySaveData entry = new InventoryEntrySaveData();
                if (slot == null || slot.IsEmpty)
                {
                    entry.mode = InventoryEntrySaveMode.Empty;
                }
                else if (slot.IsStateful)
                {
                    entry.mode = InventoryEntrySaveMode.StatefulInstance;
                    entry.definitionId = slot.Item.ItemId;
                    entry.itemInstanceId = slot.ItemInstanceId;
                }
                else
                {
                    entry.mode = InventoryEntrySaveMode.DefinitionStack;
                    entry.definitionId = slot.Item.ItemId;
                    entry.itemInstanceId = slot.ItemInstanceId;
                    entry.quantity = slot.Quantity;
                }

                saveData.entries.Add(entry);
            }

            return saveData;
        }

        public InventoryRestoreResult TryRestoreFromSaveData(InventorySaveData saveData, DefinitionRegistry registry)
        {
            if (saveData == null)
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.MissingSaveData, "Inventory save data is missing.");
            }

            List<InventoryEntrySaveData> entries = saveData.entries ?? new List<InventoryEntrySaveData>();
            int restoredCapacity = Mathf.Max(1, saveData.slotCapacity, entries.Count);
            List<InventorySlot> restoredSlots = new List<InventorySlot>(restoredCapacity);
            HashSet<string> instanceIds = new HashSet<string>();

            for (int i = 0; i < restoredCapacity; i++)
            {
                InventoryEntrySaveData entry = i < entries.Count ? entries[i] : null;
                InventoryRestoreResult entryResult = TryCreateRestoredSlot(entry, registry, instanceIds, out InventorySlot restoredSlot);
                if (!entryResult.Succeeded)
                {
                    return entryResult;
                }

                restoredSlots.Add(restoredSlot);
            }

            slotCapacity = restoredCapacity;
            slots = restoredSlots;
            InventoryChanged?.Invoke();
            return InventoryRestoreResult.Success();
        }

        public bool CanAddItem(ItemDefinition item, int quantity)
        {
            return GetAddableQuantity(item, quantity) >= quantity;
        }

        public bool CanAddItemOrInstances(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0)
            {
                return false;
            }

            if (!ShouldGrantAsInstances(item))
            {
                return CanAddItem(item, quantity);
            }

            EnsureSlotCapacity();
            return CountEmptySlots() >= quantity;
        }

        public InventoryAddResult AddItemOrInstances(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0)
            {
                return new InventoryAddResult(InventoryAddStatus.None, Mathf.Max(0, quantity), 0);
            }

            if (!ShouldGrantAsInstances(item))
            {
                return AddItem(item, quantity);
            }

            EnsureSlotCapacity();
            int requestedQuantity = quantity;
            int addedQuantity = 0;

            for (int i = 0; i < requestedQuantity; i++)
            {
                if (FindEmptySlotIndex() < 0)
                {
                    break;
                }

                if (!AddExistingItemIdentity(item, ItemInstanceId.Generate()).Succeeded)
                {
                    break;
                }

                addedQuantity++;
            }

            return new InventoryAddResult(GetAddStatus(requestedQuantity, addedQuantity), requestedQuantity, addedQuantity);
        }

        public bool CanAddItemAfterRemovingFromSlot(ItemDefinition item, int quantity, int removeSlotIndex, int removeQuantity)
        {
            return GetAddableQuantity(item, quantity, removeSlotIndex, removeQuantity) >= quantity;
        }

        public bool RemoveItemAt(int slotIndex, int quantity)
        {
            EnsureSlotCapacity();

            if (slotIndex < 0 || slotIndex >= slots.Count || quantity <= 0)
            {
                return false;
            }

            InventorySlot slot = slots[slotIndex];
            if (slot == null || slot.IsEmpty || slot.Quantity < quantity)
            {
                return false;
            }

            bool removed = slot.Remove(quantity);
            if (removed)
            {
                InventoryChanged?.Invoke();
            }

            return removed;
        }

        public int CountItem(ItemDefinition item)
        {
            if (item == null)
            {
                return 0;
            }

            EnsureSlotCapacity();
            int count = 0;
            foreach (InventorySlot slot in slots)
            {
                if (slot != null && !slot.IsEmpty && !slot.IsStateful && slot.Item == item)
                {
                    count += slot.Quantity;
                }
            }

            return count;
        }

        public bool RemoveItem(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0 || CountItem(item) < quantity)
            {
                return false;
            }

            int remaining = quantity;
            foreach (InventorySlot slot in slots)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (slot == null || slot.IsEmpty || slot.IsStateful || slot.Item != item)
                {
                    continue;
                }

                int toRemove = Mathf.Min(remaining, slot.Quantity);
                slot.Remove(toRemove);
                remaining -= toRemove;
            }

            InventoryChanged?.Invoke();
            return true;
        }

        public ItemUseResult UseItem(int slotIndex, GameObject user)
        {
            EnsureSlotCapacity();

            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return ItemUseResult.Failure("No inventory slot selected.");
            }

            InventorySlot slot = slots[slotIndex];
            if (slot == null || slot.IsEmpty)
            {
                return ItemUseResult.Failure("Selected slot is empty.");
            }

            ItemDefinition item = slot.Item;
            if (item == null || !item.IsUsable)
            {
                string itemName = item == null ? "Item" : item.DisplayName;
                return ItemUseResult.Failure($"{itemName} cannot be used.");
            }

            ItemUseContext context = new ItemUseContext(user, this, slotIndex, item);
            IReadOnlyList<ItemUseEffect> effects = item.UseEffects;

            for (int i = 0; i < effects.Count; i++)
            {
                ItemUseEffect effect = effects[i];
                if (effect == null)
                {
                    return ItemUseResult.Failure($"{item.DisplayName} has a missing use effect.");
                }

                if (!effect.CanUse(in context, out string failureReason))
                {
                    return ItemUseResult.Failure(string.IsNullOrWhiteSpace(failureReason) ? $"{item.DisplayName} cannot be used right now." : failureReason);
                }
            }

            for (int i = 0; i < effects.Count; i++)
            {
                effects[i].Apply(in context);
            }

            slot.Remove(1);
            InventoryChanged?.Invoke();

            string message = $"Used {item.DisplayName}.";
            Debug.Log(message);
            return ItemUseResult.Success(message);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DevelopmentClearInventory()
        {
            EnsureSlotCapacity();
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i]?.Clear();
            }

            InventoryChanged?.Invoke();
        }

        public int DevelopmentOccupiedSlotCount()
        {
            EnsureSlotCapacity();
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }
#endif

        private int AddToExistingStacks(ItemDefinition item, int remainingQuantity)
        {
            foreach (InventorySlot slot in slots)
            {
                if (remainingQuantity <= 0)
                {
                    break;
                }

                if (!slot.CanStack(item))
                {
                    continue;
                }

                remainingQuantity -= slot.AddToStack(remainingQuantity);
            }

            return remainingQuantity;
        }

        private bool CanAddItemIdentity(ItemDefinition item, string itemInstanceId, int quantity, out string failureReason)
        {
            if (!ValidateItemIdentityForInventory(item, itemInstanceId, quantity, out failureReason))
            {
                return false;
            }

            if (FindEmptySlotIndex() < 0)
            {
                failureReason = "Inventory full.";
                return false;
            }

            return true;
        }

        private bool CanAddItemIdentityAfterRemovingFromSlot(ItemDefinition item, string itemInstanceId, int removeSlotIndex, int quantity, out string failureReason)
        {
            if (!ValidateItemIdentityForInventory(item, itemInstanceId, quantity, out failureReason))
            {
                return false;
            }

            if (FindEmptySlotIndex() >= 0)
            {
                return true;
            }

            if (removeSlotIndex >= 0 && removeSlotIndex < slots.Count && slots[removeSlotIndex] != null && !slots[removeSlotIndex].IsEmpty)
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = "Inventory full.";
            return false;
        }

        private bool ValidateItemIdentityForInventory(ItemDefinition item, string itemInstanceId, int quantity, out string failureReason)
        {
            EnsureSlotCapacity();
            failureReason = string.Empty;

            if (item == null)
            {
                failureReason = "Cannot add an item identity without an item definition.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemInstanceId) || !ItemInstanceId.IsValid(itemInstanceId))
            {
                failureReason = $"Item identity '{itemInstanceId}' is not a canonical item instance ID.";
                return false;
            }

            if (quantity <= 0)
            {
                failureReason = "Item identity quantity must be positive.";
                return false;
            }

            if (quantity > item.MaximumStackSize)
            {
                failureReason = $"Item identity quantity {quantity} exceeds stack size {item.MaximumStackSize}.";
                return false;
            }

            if (ContainsItemIdentity(itemInstanceId))
            {
                failureReason = $"Item identity '{itemInstanceId}' is already present in inventory.";
                return false;
            }

            return true;
        }

        private static InventoryRestoreResult TryCreateRestoredSlot(
            InventoryEntrySaveData entry,
            DefinitionRegistry registry,
            HashSet<string> instanceIds,
            out InventorySlot restoredSlot)
        {
            restoredSlot = new InventorySlot();
            if (entry == null || entry.mode == InventoryEntrySaveMode.Empty)
            {
                return InventoryRestoreResult.Success();
            }

            if (entry.mode == InventoryEntrySaveMode.DefinitionStack)
            {
                return TryCreateRestoredDefinitionStack(entry, registry, instanceIds, restoredSlot);
            }

            string restoredInstanceId = entry.itemInstanceId;
            if (string.IsNullOrWhiteSpace(restoredInstanceId))
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.InvalidItemInstance, "Inventory stateful item entry has no item instance ID.");
            }

            if (string.IsNullOrWhiteSpace(entry.definitionId))
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.MissingDefinitionId, "Inventory stateful entry has no definition ID.");
            }

            if (registry == null || !registry.TryGet(entry.definitionId, out ItemDefinition item))
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.MissingItemDefinition, $"Item definition '{entry.definitionId}' was not found.");
            }

            if (!ItemInstanceId.IsValid(restoredInstanceId))
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.InvalidItemInstance, $"Item instance ID '{restoredInstanceId}' is invalid.");
            }

            if (!instanceIds.Add(restoredInstanceId))
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.DuplicateInstanceId, $"Duplicate item instance ID '{restoredInstanceId}' found in inventory save data.");
            }

            restoredSlot.SetIdentity(item, restoredInstanceId, 1);
            return InventoryRestoreResult.Success();
        }

        private static InventoryRestoreResult TryCreateRestoredDefinitionStack(
            InventoryEntrySaveData entry,
            DefinitionRegistry registry,
            HashSet<string> instanceIds,
            InventorySlot restoredSlot)
        {
            if (string.IsNullOrWhiteSpace(entry.definitionId))
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.MissingDefinitionId, "Inventory stack entry has no definition ID.");
            }

            if (registry == null || !registry.TryGet(entry.definitionId, out ItemDefinition item))
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.MissingItemDefinition, $"Item definition '{entry.definitionId}' was not found.");
            }

            if (item.InstanceMode == ItemInstanceMode.AlwaysInstanced)
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.WrongDefinitionType, $"Item definition '{entry.definitionId}' must be restored as item instances.");
            }

            if (entry.quantity <= 0 || entry.quantity > item.MaximumStackSize)
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.InvalidQuantity, $"Inventory stack '{entry.definitionId}' has invalid quantity {entry.quantity}.");
            }

            string itemInstanceId = string.IsNullOrWhiteSpace(entry.itemInstanceId)
                ? ItemInstanceId.Generate()
                : entry.itemInstanceId;
            if (!ItemInstanceId.IsValid(itemInstanceId))
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.InvalidItemInstance, $"Inventory stack identity '{itemInstanceId}' is invalid.");
            }

            if (!instanceIds.Add(itemInstanceId))
            {
                return InventoryRestoreResult.Failure(InventoryRestoreStatus.DuplicateInstanceId, $"Duplicate item instance ID '{itemInstanceId}' found in inventory save data.");
            }

            restoredSlot.SetIdentity(item, itemInstanceId, entry.quantity);
            return InventoryRestoreResult.Success();
        }

        private int GetAddableQuantity(ItemDefinition item, int quantity, int removeSlotIndex = -1, int removeQuantity = 0)
        {
            if (item == null || quantity <= 0 || item.InstanceMode == ItemInstanceMode.AlwaysInstanced)
            {
                return 0;
            }

            EnsureSlotCapacity();

            int remainingQuantity = quantity;
            bool createsEmptySlot = false;

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot == null || slot.IsEmpty)
                {
                    createsEmptySlot = true;
                    continue;
                }

                int simulatedQuantity = slot.Quantity;
                ItemDefinition simulatedItem = slot.IsStateful ? null : slot.Item;

                if (i == removeSlotIndex)
                {
                    if (slot.IsStateful)
                    {
                        if (removeQuantity >= 1)
                        {
                            createsEmptySlot = true;
                        }

                        continue;
                    }

                    simulatedQuantity = Mathf.Max(0, simulatedQuantity - removeQuantity);
                    if (simulatedQuantity == 0)
                    {
                        simulatedItem = null;
                        createsEmptySlot = true;
                    }
                }

                if (simulatedItem == null)
                {
                    continue;
                }

                if (item.Stackable && simulatedItem == item)
                {
                    remainingQuantity -= Mathf.Min(remainingQuantity, Mathf.Max(0, item.MaximumStackSize - simulatedQuantity));
                }

                if (remainingQuantity <= 0)
                {
                    return quantity;
                }
            }

            if (!createsEmptySlot)
            {
                return quantity - remainingQuantity;
            }

            foreach (InventorySlot slot in slots)
            {
                if (remainingQuantity <= 0)
                {
                    break;
                }

                if (slot != null && !slot.IsEmpty)
                {
                    continue;
                }

                remainingQuantity -= Mathf.Min(remainingQuantity, item.MaximumStackSize);
            }

            if (remainingQuantity > 0 && removeSlotIndex >= 0 && removeSlotIndex < slots.Count)
            {
                InventorySlot removedFromSlot = slots[removeSlotIndex];
                if (removedFromSlot != null && removedFromSlot.Quantity <= removeQuantity)
                {
                    remainingQuantity -= Mathf.Min(remainingQuantity, item.MaximumStackSize);
                }
            }

            return quantity - Mathf.Max(0, remainingQuantity);
        }

        private int AddToNewStacks(ItemDefinition item, int remainingQuantity)
        {
            foreach (InventorySlot slot in slots)
            {
                if (remainingQuantity <= 0)
                {
                    break;
                }

                if (!slot.IsEmpty)
                {
                    continue;
                }

                int quantityForSlot = Mathf.Min(remainingQuantity, item.MaximumStackSize);
                slot.SetIdentity(item, ItemInstanceId.Generate(), quantityForSlot);
                remainingQuantity -= quantityForSlot;
            }

            return remainingQuantity;
        }

        private int FindEmptySlotIndex()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null || slots[i].IsEmpty)
                {
                    if (slots[i] == null)
                    {
                        slots[i] = new InventorySlot();
                    }

                    return i;
                }
            }

            return -1;
        }

        private int CountEmptySlots()
        {
            int count = 0;
            foreach (InventorySlot slot in slots)
            {
                if (slot == null || slot.IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }

        private void EnsureSlotCapacity()
        {
            slots ??= new List<InventorySlot>();

            while (slots.Count < slotCapacity)
            {
                slots.Add(new InventorySlot());
            }

            if (slots.Count > slotCapacity)
            {
                slots.RemoveRange(slotCapacity, slots.Count - slotCapacity);
            }
        }

        private static InventoryAddStatus GetAddStatus(int requestedQuantity, int addedQuantity)
        {
            if (addedQuantity <= 0)
            {
                return InventoryAddStatus.None;
            }

            return addedQuantity >= requestedQuantity ? InventoryAddStatus.All : InventoryAddStatus.Partial;
        }

        private static bool ShouldGrantAsInstances(ItemDefinition item)
        {
            if (item == null)
            {
                return false;
            }

            return item.InstanceMode == ItemInstanceMode.AlwaysInstanced
                || (item.InstanceMode == ItemInstanceMode.OptionalInstance && !item.Stackable);
        }

        private static void LogAddResult(ItemDefinition item, InventoryAddResult result)
        {
            if (result.Status == InventoryAddStatus.None)
            {
                Debug.Log($"Inventory full. Could not add {result.RequestedQuantity} x {item.ItemId}.");
                return;
            }

            Debug.Log($"Item added: {item.ItemId}. Quantity added: {result.AddedQuantity}.");

            if (result.Status == InventoryAddStatus.Partial)
            {
                Debug.Log($"Partial pickup: {result.RemainingQuantity} x {item.ItemId} could not fit.");
            }
        }
    }
}
