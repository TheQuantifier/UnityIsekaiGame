using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.Equipment
{
    [Serializable]
    public sealed class EquipmentSaveData
    {
        public List<EquipmentSlotSaveData> slots = new List<EquipmentSlotSaveData>();
    }
}
