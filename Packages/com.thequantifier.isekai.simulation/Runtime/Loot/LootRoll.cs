using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Loot
{
    public readonly struct LootRoll
    {
        public LootRoll(ItemDefinition item, int quantity)
        {
            Item = item;
            Quantity = quantity;
        }

        public ItemDefinition Item { get; }
        public int Quantity { get; }
    }
}
