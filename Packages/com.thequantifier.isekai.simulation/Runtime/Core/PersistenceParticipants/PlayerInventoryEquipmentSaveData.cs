using System;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Persistence
{
    [Serializable]
    public sealed class PlayerInventoryEquipmentSaveData
    {
        public int schemaVersion = PlayerInventoryEquipmentPersistenceParticipant.CurrentParticipantSchemaVersion;
        public InventorySaveData inventory;
        public EquipmentSaveData equipment;
    }
}
