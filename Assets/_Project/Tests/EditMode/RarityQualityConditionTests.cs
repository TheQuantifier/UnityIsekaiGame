using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Quality;

namespace UnityIsekaiGame.Tests
{
    public sealed class RarityQualityConditionTests
    {
        [Test]
        public void QualityTierRangesResolveBoundariesDeterministically()
        {
            QualityTierDefinition poor = ClassificationTestFactory.CreateQualityTier("quality.poor", "Poor", 0f, 0.35f, 0);
            QualityTierDefinition fine = ClassificationTestFactory.CreateQualityTier("quality.fine", "Fine", 0.35f, 1f, 1);
            Assert.That(poor.Contains(0.349f), Is.True);
            Assert.That(poor.Contains(0.35f), Is.False);
            Assert.That(fine.Contains(0.35f), Is.True);
            Assert.That(fine.Contains(1f), Is.True);
        }

        [Test]
        public void QualityTierValidationRejectsOverlaps()
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            bool valid = QualityTierDefinition.ValidateTierRanges(new[]
            {
                ClassificationTestFactory.CreateQualityTier("quality.poor", "Poor", 0f, 0.6f, 0),
                ClassificationTestFactory.CreateQualityTier("quality.fine", "Fine", 0.5f, 1f, 1)
            }, report, requireGapless: true);
            Assert.That(valid, Is.False);
            Assert.That(report.GetSummary(), Does.Contain("overlaps"));
        }

        [Test]
        public void ConditionScaleResolvesAuthoredBandsAndClampsInput()
        {
            ItemConditionScaleDefinition scale = CreateScale();
            Assert.That(scale.TryResolve(-1f, out ItemConditionBandData broken), Is.True);
            Assert.That(broken.bandId, Is.EqualTo("condition.broken"));
            Assert.That(scale.TryResolve(0.5f, out ItemConditionBandData good), Is.True);
            Assert.That(good.bandId, Is.EqualTo("condition.good"));
            Assert.That(scale.TryResolve(2f, out ItemConditionBandData pristine), Is.True);
            Assert.That(pristine.bandId, Is.EqualTo("condition.pristine"));
        }

        [Test]
        public void ConditionScaleValidationRejectsGaps()
        {
            ItemConditionScaleDefinition scale = ClassificationTestFactory.CreateConditionScale(
                Band("condition.broken", 0f, 0.25f, ItemFunctionalState.Broken, 0f),
                Band("condition.good", 0.5f, 1f, ItemFunctionalState.FullyFunctional, 1f));
            DefinitionValidationReport report = new DefinitionValidationReport();
            scale.ValidateCatalogDefinition(new Dictionary<string, IGameDefinition>(), report);
            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("gap"));
        }

        [Test]
        public void CatalogLooksUpRarityQualityTierAndConditionScaleByType()
        {
            RarityDefinition common = ClassificationTestFactory.CreateRarity("rarity.common", "Common", 0);
            QualityTierDefinition quality = ClassificationTestFactory.CreateQualityTier("quality.common", "Common", 0f, 1f, 0);
            ItemConditionScaleDefinition condition = CreateScale();
            DefinitionRegistry registry = ClassificationTestFactory.CreateCatalog(common, quality, condition).CreateRegistry();
            Assert.That(registry.TryGet(common.Id, out RarityDefinition foundRarity), Is.True);
            Assert.That(registry.TryGet(quality.Id, out QualityTierDefinition foundQuality), Is.True);
            Assert.That(registry.TryGet(condition.Id, out ItemConditionScaleDefinition foundCondition), Is.True);
            Assert.That(foundRarity, Is.SameAs(common));
            Assert.That(foundQuality, Is.SameAs(quality));
            Assert.That(foundCondition, Is.SameAs(condition));
        }

        [Test]
        public void StackCompatibilityRemainsDefinitionOnlyForFungibleInventory()
        {
            TestRarityItem item = ScriptableObject.CreateInstance<TestRarityItem>();
            item.Initialize("item.stackable", "Stackable Item", true);
            TestRarityItem sameIdDifferentDefinition = ScriptableObject.CreateInstance<TestRarityItem>();
            sameIdDifferentDefinition.Initialize("item.stackable", "Stackable Item", true);
            Assert.That(RarityItemUtility.CanShareDefinitionOnlyStack(item, item), Is.True);
            Assert.That(RarityItemUtility.CanShareDefinitionOnlyStack(item, sameIdDifferentDefinition), Is.False);
        }

        private static ItemConditionScaleDefinition CreateScale()
        {
            return ClassificationTestFactory.CreateConditionScale(
                Band("condition.broken", 0f, 0.25f, ItemFunctionalState.Broken, 0f),
                Band("condition.good", 0.25f, 0.95f, ItemFunctionalState.FullyFunctional, 1f),
                Band("condition.pristine", 0.95f, 1f, ItemFunctionalState.FullyFunctional, 1f));
        }

        private static ItemConditionBandData Band(string id, float minimum, float maximum, ItemFunctionalState state, float contribution)
        {
            return new ItemConditionBandData
            {
                bandId = id,
                displayName = id,
                minimumNormalized = minimum,
                maximumNormalized = maximum,
                functionalState = state,
                breakageState = state == ItemFunctionalState.Broken ? ItemBreakageState.Broken : ItemBreakageState.None,
                equipmentContribution = contribution
            };
        }

        private sealed class TestRarityItem : ScriptableObject, IInventoryItemDefinition
        {
            private string id;
            private string displayName;
            private bool stackable;
            public string Id => id;
            public string DisplayName => displayName;
            public CategoryDefinition PrimaryCategory => null;
            public CategoryDomain ClassificationDomain => CategoryDomain.Item;
            public IReadOnlyList<TagDefinition> Tags => System.Array.Empty<TagDefinition>();
            public string Description => string.Empty;
            public Sprite Icon => null;
            public bool Stackable => stackable;
            public int MaximumStackSize => stackable ? 10 : 1;
            public void Initialize(string itemId, string itemDisplayName, bool isStackable) { id = itemId; displayName = itemDisplayName; stackable = isStackable; }
        }
    }
}
