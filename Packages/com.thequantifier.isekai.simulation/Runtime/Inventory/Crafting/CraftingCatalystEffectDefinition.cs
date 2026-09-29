using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Quality;

namespace UnityIsekaiGame.Inventory.Crafting
{
    [CreateAssetMenu(fileName = "CraftingCatalystEffectDefinition", menuName = "Unity Isekai Game/Inventory/Crafting Catalyst Effect Definition")]
    public sealed class CraftingCatalystEffectDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string effectId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ItemDefinition[] catalystItems = Array.Empty<ItemDefinition>();
        [SerializeField] private CategoryDefinition[] catalystCategories = Array.Empty<CategoryDefinition>();
        [SerializeField] private TagDefinition[] catalystTags = Array.Empty<TagDefinition>();
        [SerializeField] private ItemAffixDefinition resultingAffix;
        [SerializeField, Min(0f)] private float generationWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float baseChance = 0.08f;
        [SerializeField, Range(0f, 1f)] private float chancePerRarityRank = 0.1f;
        [SerializeField, Range(0f, 1f)] private float chancePerAdditionalItem = 0.07f;
        [SerializeField, Range(0f, 1f)] private float chancePerSkillGrade = 0.02f;
        [SerializeField, Range(0f, 1f)] private float maximumChance = 0.9f;
        [SerializeField, Min(1)] private int additionalItemsPerAffixTier = 2;

        public string Id => effectId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public ItemAffixDefinition ResultingAffix => resultingAffix;
        public float GenerationWeight => Mathf.Max(0f, generationWeight);

        public bool Matches(ItemDefinition catalyst)
        {
            if (catalyst == null)
            {
                return false;
            }

            if ((catalystItems ?? Array.Empty<ItemDefinition>()).Any(item => item != null && string.Equals(item.Id, catalyst.Id, StringComparison.Ordinal)))
            {
                return true;
            }

            if (catalyst.PrimaryCategory != null && (catalystCategories ?? Array.Empty<CategoryDefinition>()).Any(category => category != null && string.Equals(category.Id, catalyst.PrimaryCategory.Id, StringComparison.Ordinal)))
            {
                return true;
            }

            return (catalystTags ?? Array.Empty<TagDefinition>()).Any(required => required != null && catalyst.Tags.Any(actual => actual != null && string.Equals(actual.Id, required.Id, StringComparison.Ordinal)));
        }

        public float CalculateChance(ItemDefinition catalyst, int quantity, int skillGradeTier)
        {
            int rarityRank = catalyst?.Rarity?.Rank ?? 0;
            return Mathf.Clamp(
                baseChance
                + rarityRank * chancePerRarityRank
                + Mathf.Max(0, quantity - 1) * chancePerAdditionalItem
                + Mathf.Max(0, skillGradeTier) * chancePerSkillGrade,
                0f,
                maximumChance);
        }

        public string SelectAffixTierId(ItemDefinition catalyst, int quantity)
        {
            if (resultingAffix == null || resultingAffix.Tiers.Count == 0)
            {
                return string.Empty;
            }

            int rarityRank = catalyst?.Rarity?.Rank ?? 0;
            int tierIndex = rarityRank + Mathf.Max(0, quantity - 1) / Mathf.Max(1, additionalItemsPerAffixTier);
            return resultingAffix.Tiers
                .OrderBy(tier => tier.sortOrder)
                .ThenBy(tier => tier.tierId, StringComparer.Ordinal)
                .ElementAt(Mathf.Clamp(tierIndex, 0, resultingAffix.Tiers.Count - 1))
                .tierId;
        }

        private void OnValidate()
        {
            effectId = effectId?.Trim();
            generationWeight = Mathf.Max(0f, generationWeight);
            baseChance = Mathf.Clamp01(baseChance);
            chancePerRarityRank = Mathf.Clamp01(chancePerRarityRank);
            chancePerAdditionalItem = Mathf.Clamp01(chancePerAdditionalItem);
            chancePerSkillGrade = Mathf.Clamp01(chancePerSkillGrade);
            maximumChance = Mathf.Clamp01(maximumChance);
            additionalItemsPerAffixTier = Mathf.Max(1, additionalItemsPerAffixTier);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Crafting catalyst effect '{name}' is missing an ID.");
            if ((catalystItems?.Length ?? 0) == 0 && (catalystCategories?.Length ?? 0) == 0 && (catalystTags?.Length ?? 0) == 0) report.AddError($"Crafting catalyst effect '{DisplayName}' has no catalyst matcher.");
            if (resultingAffix == null || definitionsById == null || !definitionsById.TryGetValue(resultingAffix.Id, out IGameDefinition affix) || affix is not ItemAffixDefinition) report.AddError($"Crafting catalyst effect '{DisplayName}' references a missing result affix.");
            if (generationWeight <= 0f) report.AddError($"Crafting catalyst effect '{DisplayName}' must have a positive generation weight.");
            if (maximumChance < baseChance) report.AddError($"Crafting catalyst effect '{DisplayName}' has a maximum chance below its base chance.");
        }
    }
}
