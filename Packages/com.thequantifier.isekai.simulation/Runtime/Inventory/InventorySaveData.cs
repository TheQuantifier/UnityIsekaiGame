using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.Inventory
{
    [Serializable]
    public sealed class InventorySaveData
    {
        public int slotCapacity;
        public List<InventoryEntrySaveData> entries = new List<InventoryEntrySaveData>();
    }
}
