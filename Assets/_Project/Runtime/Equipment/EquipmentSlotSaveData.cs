using System;

namespace UnityIsekaiGame.Equipment
{
    [Serializable]
    public sealed class EquipmentSlotSaveData
    {
        public EquipmentSlotType slotType;
        public EquipmentEntrySaveMode mode;
        public string definitionId;
        public string itemInstanceId;
    }
}
