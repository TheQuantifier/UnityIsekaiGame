namespace UnityIsekaiGame.GameData
{
    public static class ItemTaxonomyUtility
    {
        public const string ItemCategoryId = "category.item";
        public const string EquipmentCategoryId = "category.item.equipment";
        public const string WeaponCategoryId = "category.item.weapon";
        public const string ArmorCategoryId = "category.item.armor";
        public const string ConsumableCategoryId = "category.item.consumable";
        public const string MaterialCategoryId = "category.item.material";
        public const string IngredientCategoryId = "category.item.ingredient";
        public const string ToolCategoryId = "category.item.tool";
        public const string TradeGoodCategoryId = "category.item.trade-good";
        public const string KeyCategoryId = "category.item.key";
        public const string QuestItemCategoryId = "category.item.quest-item";
        public const string BookCategoryId = "category.item.book";
        public const string MiscellaneousCategoryId = "category.item.miscellaneous";

        public static bool IsItemDefinition(IGameDefinition definition)
        {
            return definition is IInventoryItemDefinition;
        }

        public static bool IsWeapon(IInventoryItemDefinition item)
        {
            return IsInItemCategory(item, WeaponCategoryId);
        }

        public static bool IsArmor(IInventoryItemDefinition item)
        {
            return IsInItemCategory(item, ArmorCategoryId);
        }

        public static bool IsEquipment(IInventoryItemDefinition item)
        {
            return IsInItemCategory(item, EquipmentCategoryId);
        }

        public static bool IsConsumable(IInventoryItemDefinition item)
        {
            return IsInItemCategory(item, ConsumableCategoryId);
        }

        public static bool IsInItemCategory(IInventoryItemDefinition item, string categoryId)
        {
            return item != null && ClassificationUtility.IsInCategory(item, categoryId);
        }

        public static bool HasTag(IInventoryItemDefinition item, string tagId)
        {
            return item != null && ClassificationUtility.HasTag(item, tagId);
        }

        public static bool HasUseCapability(IGameDefinition definition)
        {
            return definition is IUsableItemDefinition usable && usable.IsUsable;
        }

        public static bool HasEquipCapability(IGameDefinition definition)
        {
            return definition is IEquippableItemDefinition equippable && equippable.IsEquippable;
        }
    }
}
