using System;
using UnityEngine;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Loot
{
    [Serializable]
    public sealed class LootEntry
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(1)] private int minimumQuantity = 1;
        [SerializeField, Min(1)] private int maximumQuantity = 1;
        [SerializeField, Range(0f, 1f)] private float dropChance = 1f;

        public ItemDefinition Item => item;
        public int MinimumQuantity => minimumQuantity;
        public int MaximumQuantity => maximumQuantity;
        public float DropChance => dropChance;

        public void Validate()
        {
            minimumQuantity = Mathf.Max(1, minimumQuantity);
            maximumQuantity = Mathf.Max(minimumQuantity, maximumQuantity);
            dropChance = Mathf.Clamp01(dropChance);
        }

        public bool TryRoll(ILootRandom random, out ItemDefinition rolledItem, out int quantity)
        {
            rolledItem = null;
            quantity = 0;

            if (item == null || random == null || random.Next01() > dropChance)
            {
                return false;
            }

            rolledItem = item;
            quantity = random.RangeInclusive(minimumQuantity, maximumQuantity);
            return quantity > 0;
        }
    }
}
