using System;

namespace UnityIsekaiGame.Inventory
{
    [Serializable]
    public sealed class InventoryEntrySaveData
    {
        public InventoryEntrySaveMode mode;
        public string definitionId;
        public string itemInstanceId;
        public int quantity;
    }
}
