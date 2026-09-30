namespace UnityIsekaiGame.GameData
{
    public static class RarityItemUtility
    {
        public static RarityDefinition GetRarity(IHasRarity definition) => definition?.Rarity;

        public static int CompareRarityRank(RarityDefinition left, RarityDefinition right)
        {
            if (left == null) return right == null ? 0 : -1;
            return right == null ? 1 : left.Rank.CompareTo(right.Rank);
        }

        public static bool CanShareDefinitionOnlyStack(IInventoryItemDefinition existing, IInventoryItemDefinition candidate)
        {
            return existing != null && candidate != null && ReferenceEquals(existing, candidate) && existing.Stackable;
        }
    }
}
