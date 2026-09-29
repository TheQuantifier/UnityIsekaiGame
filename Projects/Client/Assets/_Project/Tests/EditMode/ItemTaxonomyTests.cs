using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Tests
{
    public sealed class ItemTaxonomyTests
    {
        [Test]
        public void ItemTaxonomyUtility_DetectsWeaponArmorConsumableAndMaterialCategories()
        {
            CategoryDefinition item = ClassificationTestFactory.CreateCategory("category.item", "Item", CategoryDomain.Item);
            CategoryDefinition equipment = ClassificationTestFactory.CreateCategory("category.item.equipment", "Equipment", CategoryDomain.Item, item);
            CategoryDefinition weapon = ClassificationTestFactory.CreateCategory("category.item.weapon", "Weapon", CategoryDomain.Item, equipment);
            CategoryDefinition armor = ClassificationTestFactory.CreateCategory("category.item.armor", "Armor", CategoryDomain.Item, equipment);
            CategoryDefinition consumable = ClassificationTestFactory.CreateCategory("category.item.consumable", "Consumable", CategoryDomain.Item, item);
            CategoryDefinition material = ClassificationTestFactory.CreateCategory("category.item.material", "Material", CategoryDomain.Item, item);

            Assert.That(ItemTaxonomyUtility.IsWeapon(new TestItemDefinition(weapon)), Is.True);
            Assert.That(ItemTaxonomyUtility.IsEquipment(new TestItemDefinition(weapon)), Is.True);
            Assert.That(ItemTaxonomyUtility.IsArmor(new TestItemDefinition(armor)), Is.True);
            Assert.That(ItemTaxonomyUtility.IsConsumable(new TestItemDefinition(consumable)), Is.True);
            Assert.That(ItemTaxonomyUtility.IsInItemCategory(new TestItemDefinition(material), "category.item"), Is.True);
        }

        [Test]
        public void ItemTaxonomyUtility_ReportsUseAndEquipCapabilitiesWithoutCategoryDependence()
        {
            TestItemDefinition item = new TestItemDefinition(null, stackable: true, maximumStackSize: 10, usable: true, equippable: true);

            Assert.That(ItemTaxonomyUtility.HasUseCapability(item), Is.True);
            Assert.That(ItemTaxonomyUtility.HasEquipCapability(item), Is.True);
        }

        [Test]
        public void Registry_LooksUpInventoryItemDefinitionById()
        {
            CategoryDefinition consumable = ClassificationTestFactory.CreateCategory("category.item.consumable", "Consumable", CategoryDomain.Item);
            TestUnityItemDefinition potion = ScriptableObject.CreateInstance<TestUnityItemDefinition>();
            potion.Initialize("item.health-potion", "Health Potion", consumable, true, 10);
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { potion });

            bool found = registry.TryGet("item.health-potion", out IInventoryItemDefinition resolved);

            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(potion));
        }

        [Test]
        public void Validate_WarnsWhenEquipmentCategoryLacksEquipCapability()
        {
            CategoryDefinition item = ClassificationTestFactory.CreateCategory("category.item", "Item", CategoryDomain.Item);
            CategoryDefinition equipment = ClassificationTestFactory.CreateCategory("category.item.equipment", "Equipment", CategoryDomain.Item, item);
            TestUnityItemDefinition sword = ScriptableObject.CreateInstance<TestUnityItemDefinition>();
            sword.Initialize("item.prototype-sword", "Prototype Sword", equipment, false, 1, usable: false, equippable: false);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(ClassificationTestFactory.CreateCatalog(item, equipment, sword));

            Assert.That(report.WarningCount, Is.GreaterThan(0));
            Assert.That(report.GetSummary(), Does.Contain("categorized as equipment"));
        }

        [Test]
        public void Validate_WarnsWhenEquippableItemIsOutsideEquipmentHierarchy()
        {
            CategoryDefinition item = ClassificationTestFactory.CreateCategory("category.item", "Item", CategoryDomain.Item);
            CategoryDefinition material = ClassificationTestFactory.CreateCategory("category.item.material", "Material", CategoryDomain.Item, item);
            TestUnityItemDefinition sword = ScriptableObject.CreateInstance<TestUnityItemDefinition>();
            sword.Initialize("item.prototype-sword", "Prototype Sword", material, false, 1, usable: false, equippable: true);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(ClassificationTestFactory.CreateCatalog(item, material, sword));

            Assert.That(report.WarningCount, Is.GreaterThan(0));
            Assert.That(report.GetSummary(), Does.Contain("equippable but is not in the category.item.equipment"));
        }

        [Test]
        public void Validate_WarnsWhenUsableItemIsOutsideConsumableCategory()
        {
            CategoryDefinition item = ClassificationTestFactory.CreateCategory("category.item", "Item", CategoryDomain.Item);
            CategoryDefinition material = ClassificationTestFactory.CreateCategory("category.item.material", "Material", CategoryDomain.Item, item);
            TestUnityItemDefinition potion = ScriptableObject.CreateInstance<TestUnityItemDefinition>();
            potion.Initialize("item.health-potion", "Health Potion", material, true, 10, usable: true, equippable: false);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(ClassificationTestFactory.CreateCatalog(item, material, potion));

            Assert.That(report.WarningCount, Is.GreaterThan(0));
            Assert.That(report.GetSummary(), Does.Contain("has use effects but is not categorized under category.item.consumable"));
        }

        [Test]
        public void Validate_ReportsMissingPrimaryItemCategory()
        {
            TestUnityItemDefinition item = ScriptableObject.CreateInstance<TestUnityItemDefinition>();
            item.Initialize("item.uncategorized", "Uncategorized", null, true, 10);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(ClassificationTestFactory.CreateCatalog(item));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("missing a primary item category"));
        }

        private sealed class TestItemDefinition : IInventoryItemDefinition, IUsableItemDefinition, IEquippableItemDefinition
        {
            private readonly TagDefinition[] tags;

            public TestItemDefinition(
                CategoryDefinition primaryCategory,
                bool stackable = true,
                int maximumStackSize = 1,
                bool usable = false,
                bool equippable = false,
                params TagDefinition[] tags)
            {
                PrimaryCategory = primaryCategory;
                Stackable = stackable;
                MaximumStackSize = maximumStackSize;
                IsUsable = usable;
                IsEquippable = equippable;
                this.tags = tags;
            }

            public string Id => "item.test";
            public string DisplayName => "Test Item";
            public string Description => string.Empty;
            public Sprite Icon => null;
            public CategoryDefinition PrimaryCategory { get; }
            public CategoryDomain ClassificationDomain => CategoryDomain.Item;
            public IReadOnlyList<TagDefinition> Tags => tags;
            public bool Stackable { get; }
            public int MaximumStackSize { get; }
            public bool IsUsable { get; }
            public int UseEffectCount => IsUsable ? 1 : 0;
            public bool HasMissingUseEffect => false;
            public bool IsEquippable { get; }
        }

        private sealed class TestUnityItemDefinition : ScriptableObject, IInventoryItemDefinition, IUsableItemDefinition, IEquippableItemDefinition
        {
            private string id;
            private string displayName;
            private CategoryDefinition primaryCategory;
            private bool stackable;
            private int maximumStackSize;
            private bool usable;
            private bool equippable;

            public string Id => id;
            public string DisplayName => displayName;
            public string Description => string.Empty;
            public Sprite Icon => null;
            public CategoryDefinition PrimaryCategory => primaryCategory;
            public CategoryDomain ClassificationDomain => CategoryDomain.Item;
            public IReadOnlyList<TagDefinition> Tags => System.Array.Empty<TagDefinition>();
            public bool Stackable => stackable;
            public int MaximumStackSize => stackable ? maximumStackSize : 1;
            public bool IsUsable => usable;
            public int UseEffectCount => usable ? 1 : 0;
            public bool HasMissingUseEffect => false;
            public bool IsEquippable => equippable;

            public void Initialize(
                string definitionId,
                string definitionDisplayName,
                CategoryDefinition category,
                bool itemStackable,
                int stackSize,
                bool usable = false,
                bool equippable = false)
            {
                id = definitionId;
                displayName = definitionDisplayName;
                primaryCategory = category;
                stackable = itemStackable;
                maximumStackSize = stackSize;
                this.usable = usable;
                this.equippable = equippable;
            }
        }
    }
}
