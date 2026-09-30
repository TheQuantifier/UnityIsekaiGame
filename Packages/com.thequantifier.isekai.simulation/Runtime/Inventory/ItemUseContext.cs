using UnityEngine;

namespace UnityIsekaiGame.Inventory
{
    public readonly struct ItemUseContext
    {
        public ItemUseContext(GameObject user, PlayerInventory inventory, int slotIndex, ItemDefinition item)
        {
            User = user;
            Inventory = inventory;
            SlotIndex = slotIndex;
            Item = item;
        }

        public GameObject User { get; }
        public PlayerInventory Inventory { get; }
        public int SlotIndex { get; }
        public ItemDefinition Item { get; }
    }
}
