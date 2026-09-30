using System;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Inventory
{
    [Serializable]
    public sealed class InventorySlot
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(0)] private int quantity;
        [SerializeField] private string itemInstanceId;
        [SerializeField] private InventorySlotMode storageMode;

        public ItemDefinition Item => item;
        public int Quantity => Mathf.Max(0, quantity);
        public string ItemInstanceId => itemInstanceId ?? string.Empty;
        public bool HasItemIdentity => !string.IsNullOrWhiteSpace(ItemInstanceId);
        public InventorySlotMode Mode
        {
            get
            {
                if (item == null || quantity <= 0)
                {
                    return InventorySlotMode.Empty;
                }

                if (storageMode == InventorySlotMode.StatefulInstance
                    || (storageMode == InventorySlotMode.Empty
                        && (item.InstanceMode == ItemInstanceMode.AlwaysInstanced || !item.Stackable)
                        && HasItemIdentity))
                {
                    return InventorySlotMode.StatefulInstance;
                }

                return InventorySlotMode.DefinitionStack;
            }
        }
        public bool IsStateful => Mode == InventorySlotMode.StatefulInstance;
        public bool IsEmpty => Mode == InventorySlotMode.Empty;

        public int AvailableStackSpace
        {
            get
            {
                if (Mode != InventorySlotMode.DefinitionStack || item == null)
                {
                    return 0;
                }

                return Mathf.Max(0, item.MaximumStackSize - quantity);
            }
        }

        public bool CanStack(ItemDefinition candidate)
        {
            return Mode == InventorySlotMode.DefinitionStack && item == candidate && item.Stackable && AvailableStackSpace > 0;
        }

        internal int AddToStack(int amount)
        {
            if (item == null || amount <= 0)
            {
                return 0;
            }

            int added = Mathf.Min(amount, AvailableStackSpace);
            quantity += added;
            return added;
        }

        internal void Set(ItemDefinition newItem, int newQuantity)
        {
            SetDefinitionStack(newItem, string.Empty, newQuantity);
        }

        internal void SetDefinitionStack(ItemDefinition newItem, string newItemInstanceId, int newQuantity)
        {
            SetCore(newItem, newItemInstanceId, newQuantity, InventorySlotMode.DefinitionStack);
        }

        internal void SetIdentity(ItemDefinition newItem, string newItemInstanceId, int newQuantity)
        {
            SetCore(newItem, newItemInstanceId, newQuantity, InventorySlotMode.StatefulInstance);
        }

        private void SetCore(ItemDefinition newItem, string newItemInstanceId, int newQuantity, InventorySlotMode newMode)
        {
            item = newItem;
            quantity = Mathf.Max(0, newQuantity);
            itemInstanceId = newItemInstanceId ?? string.Empty;
            storageMode = newMode;

            if (item == null || quantity == 0)
            {
                Clear();
            }
        }

        internal void Clear()
        {
            item = null;
            quantity = 0;
            itemInstanceId = string.Empty;
            storageMode = InventorySlotMode.Empty;
        }

        internal bool Remove(int amount)
        {
            if (Mode == InventorySlotMode.StatefulInstance)
            {
                if (amount <= 0 || amount > quantity)
                {
                    return false;
                }

                quantity -= amount;
                if (quantity == 0) Clear();
                return true;
            }

            if (amount <= 0 || IsEmpty || amount > quantity)
            {
                return false;
            }

            quantity = Mathf.Max(0, quantity - amount);
            if (quantity == 0)
            {
                Clear();
            }

            return true;
        }
    }
}
