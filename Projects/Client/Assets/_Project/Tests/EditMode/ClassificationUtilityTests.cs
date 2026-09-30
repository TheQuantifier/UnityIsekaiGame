using System.Collections.Generic;
using NUnit.Framework;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Tests
{
    public sealed class ClassificationUtilityTests
    {
        [Test]
        public void IsInCategory_ReturnsTrueForDirectCategory()
        {
            CategoryDefinition item = ClassificationTestFactory.CreateCategory("category.item", "Item", CategoryDomain.Item);
            TestClassifiedDefinition definition = new TestClassifiedDefinition(item);

            Assert.That(ClassificationUtility.IsInCategory(definition, item), Is.True);
            Assert.That(ClassificationUtility.IsInCategory(definition, "category.item"), Is.True);
        }

        [Test]
        public void IsInCategory_ReturnsTrueForInheritedParentCategory()
        {
            CategoryDefinition item = ClassificationTestFactory.CreateCategory("category.item", "Item", CategoryDomain.Item);
            CategoryDefinition weapon = ClassificationTestFactory.CreateCategory("category.item.weapon", "Weapon", CategoryDomain.Item, item);
            TestClassifiedDefinition definition = new TestClassifiedDefinition(weapon);

            Assert.That(ClassificationUtility.IsInCategory(definition, item), Is.True);
            Assert.That(ClassificationUtility.IsInCategory(definition, "category.item"), Is.True);
        }

        [Test]
        public void GetAncestors_ReturnsParentChain()
        {
            CategoryDefinition item = ClassificationTestFactory.CreateCategory("category.item", "Item", CategoryDomain.Item);
            CategoryDefinition weapon = ClassificationTestFactory.CreateCategory("category.item.weapon", "Weapon", CategoryDomain.Item, item);
            CategoryDefinition melee = ClassificationTestFactory.CreateCategory("category.item.weapon.melee", "Melee Weapon", CategoryDomain.Item, weapon);

            IReadOnlyList<CategoryDefinition> ancestors = ClassificationUtility.GetAncestors(melee);

            Assert.That(ancestors, Has.Count.EqualTo(2));
            Assert.That(ancestors[0], Is.SameAs(weapon));
            Assert.That(ancestors[1], Is.SameAs(item));
        }

        [Test]
        public void HasTag_ReturnsTrueForMatchingTagId()
        {
            TagDefinition healing = ClassificationTestFactory.CreateTag("tag.general.healing", "Healing");
            TestTaggedDefinition definition = new TestTaggedDefinition(healing);

            Assert.That(ClassificationUtility.HasTag(definition, healing), Is.True);
            Assert.That(ClassificationUtility.HasTag(definition, "tag.general.healing"), Is.True);
        }

        [Test]
        public void Queries_HandleMissingCategoryAndTagSafely()
        {
            Assert.That(ClassificationUtility.IsInCategory((CategoryDefinition)null, "category.item"), Is.False);
            Assert.That(ClassificationUtility.HasTag(null, "tag.general.healing"), Is.False);
        }

        private sealed class TestClassifiedDefinition : ICategorizableDefinition
        {
            public TestClassifiedDefinition(CategoryDefinition primaryCategory)
            {
                PrimaryCategory = primaryCategory;
            }

            public CategoryDefinition PrimaryCategory { get; }
            public CategoryDomain ClassificationDomain => CategoryDomain.Item;
        }

        private sealed class TestTaggedDefinition : ITaggedDefinition
        {
            private readonly TagDefinition[] tags;

            public TestTaggedDefinition(params TagDefinition[] tags)
            {
                this.tags = tags;
            }

            public IReadOnlyList<TagDefinition> Tags => tags;
        }
    }
}
