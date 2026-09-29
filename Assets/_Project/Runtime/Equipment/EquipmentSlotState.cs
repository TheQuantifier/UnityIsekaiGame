using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Equipment
{
    [Serializable]
    public sealed class EquipmentSlotState
    {
        [SerializeField] private EquipmentSlotType slotType;
        [SerializeField] private ItemDefinition item;
        [SerializeField] private string itemInstanceId;

        public EquipmentSlotType SlotType => slotType;
        public ItemDefinition Item => item;
        public string ItemInstanceId => itemInstanceId ?? string.Empty;
        public bool HasItemIdentity => !string.IsNullOrWhiteSpace(ItemInstanceId);
        public bool IsStateful => HasItemIdentity;
        public bool IsEmpty => Item == null;

        internal void Initialize(EquipmentSlotType type)
        {
            slotType = type;
        }

        internal void SetIdentity(ItemDefinition newItem, string newItemInstanceId)
        {
            item = newItem;
            itemInstanceId = newItemInstanceId ?? string.Empty;
        }

        internal void Clear()
        {
            item = null;
            itemInstanceId = string.Empty;
        }
    }
}
