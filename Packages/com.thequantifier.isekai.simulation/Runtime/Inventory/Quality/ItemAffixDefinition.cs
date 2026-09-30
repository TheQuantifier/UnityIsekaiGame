using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Inventory.Quality
{
    [CreateAssetMenu(fileName = "ItemAffixDefinition", menuName = "Unity Isekai Game/Inventory/Item Affix Definition")]
    public sealed class ItemAffixDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string affixId;
        [SerializeField] private string displayName;
        [SerializeField] private ItemAffixClassification classification = ItemAffixClassification.Prefix;
        [SerializeField] private CategoryDefinition[] applicableCategories;
        [SerializeField] private ItemDefinition[] applicableItemDefinitions;
        [SerializeField] private TagDefinition[] requiredItemTags;
        [SerializeField] private TagDefinition[] forbiddenItemTags;
        [SerializeField] private TagDefinition[] requiredMaterialTags;
        [SerializeField] private TagDefinition[] forbiddenMaterialTags;
        [SerializeField] private string[] requiredComponentRoles;
        [SerializeField] private string[] compatibleGroups;
        [SerializeField] private string[] exclusiveGroups;
        [SerializeField, Min(1)] private int maximumOccurrences = 1;
        [SerializeField, Min(0)] private int maximumPrefixCount = 3;
        [SerializeField, Min(0)] private int maximumSuffixCount = 3;
        [SerializeField, Min(0)] private int maximumTotalAffixCount = 6;
        [SerializeField] private ItemAffixTierData[] tiers;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private bool hiddenByDefault;
        [SerializeField] private int identificationDifficulty;
        [SerializeField] private float rarityContribution;
        [SerializeField, Min(0f)] private float generationWeight = 1f;
        [SerializeField] private ItemAffixSource[] allowedSources;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private int version = 1;

        public string Id => affixId;
        public string DisplayName => displayName;
        public ItemAffixClassification Classification => classification;
        public IReadOnlyList<CategoryDefinition> ApplicableCategories => applicableCategories ?? Array.Empty<CategoryDefinition>();
        public IReadOnlyList<ItemDefinition> ApplicableItemDefinitions => applicableItemDefinitions ?? Array.Empty<ItemDefinition>();
        public IReadOnlyList<TagDefinition> RequiredItemTags => requiredItemTags ?? Array.Empty<TagDefinition>();
        public IReadOnlyList<TagDefinition> ForbiddenItemTags => forbiddenItemTags ?? Array.Empty<TagDefinition>();
        public IReadOnlyList<TagDefinition> RequiredMaterialTags => requiredMaterialTags ?? Array.Empty<TagDefinition>();
        public IReadOnlyList<TagDefinition> ForbiddenMaterialTags => forbiddenMaterialTags ?? Array.Empty<TagDefinition>();
        public IReadOnlyList<string> RequiredComponentRoles => requiredComponentRoles ?? Array.Empty<string>();
        public IReadOnlyList<string> CompatibleGroups => compatibleGroups ?? Array.Empty<string>();
        public IReadOnlyList<string> ExclusiveGroups => exclusiveGroups ?? Array.Empty<string>();
        public int MaximumOccurrences => Mathf.Max(1, maximumOccurrences);
        public int MaximumPrefixCount => Mathf.Max(0, maximumPrefixCount);
        public int MaximumSuffixCount => Mathf.Max(0, maximumSuffixCount);
        public int MaximumTotalAffixCount => Mathf.Max(0, maximumTotalAffixCount);
        public IReadOnlyList<ItemAffixTierData> Tiers => tiers ?? Array.Empty<ItemAffixTierData>();
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public bool HiddenByDefault => hiddenByDefault || classification == ItemAffixClassification.Hidden;
        public int IdentificationDifficulty => identificationDifficulty;
        public float RarityContribution => rarityContribution;
        public float GenerationWeight => Mathf.Max(0f, generationWeight);
        public IReadOnlyList<ItemAffixSource> AllowedSources => allowedSources ?? Array.Empty<ItemAffixSource>();
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public int Version => Mathf.Max(1, version);

        private void OnValidate()
        {
            maximumOccurrences = Mathf.Max(1, maximumOccurrences);
            maximumPrefixCount = Mathf.Max(0, maximumPrefixCount);
            maximumSuffixCount = Mathf.Max(0, maximumSuffixCount);
            maximumTotalAffixCount = Mathf.Max(0, maximumTotalAffixCount);
            generationWeight = Mathf.Max(0f, generationWeight);
            version = Mathf.Max(1, version);
        }

        public ItemAffixTierData ResolveBestTier(float quality)
        {
            return Tiers
                .Where(tier => tier != null && quality >= tier.minimumItemQuality && quality <= tier.maximumItemQuality)
                .OrderByDescending(tier => tier.sortOrder)
                .ThenBy(tier => tier.tierId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (!Enum.IsDefined(typeof(ItemAffixClassification), classification) || classification == ItemAffixClassification.Unknown)
            {
                report.AddError($"Affix definition '{DisplayName}' has an invalid classification.");
            }

            ValidateReferences("applicable item", ApplicableItemDefinitions, definitionsById, report);
            ValidateReferences("applicable category", ApplicableCategories, definitionsById, report);
            ValidateReferences("required item tag", RequiredItemTags, definitionsById, report);
            ValidateReferences("forbidden item tag", ForbiddenItemTags, definitionsById, report);
            ValidateReferences("required material tag", RequiredMaterialTags, definitionsById, report);
            ValidateReferences("forbidden material tag", ForbiddenMaterialTags, definitionsById, report);

            HashSet<string> tierIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemAffixTierData tier in Tiers)
            {
                if (tier == null || string.IsNullOrWhiteSpace(tier.tierId) || !tierIds.Add(tier.tierId))
                {
                    report.AddError($"Affix definition '{DisplayName}' has a missing or duplicate tier ID.");
                    continue;
                }

                if (tier.minimumItemQuality < 0f || tier.maximumItemQuality > 1f || tier.maximumItemQuality < tier.minimumItemQuality)
                {
                    report.AddError($"Affix definition '{DisplayName}' tier '{tier.tierId}' has an invalid item-quality range.");
                }

                if (tier.valueMaximum < tier.valueMinimum)
                {
                    report.AddError($"Affix definition '{DisplayName}' tier '{tier.tierId}' has an invalid value range.");
                }

                foreach (CalculatedStatModifierDefinition modifier in tier.modifierTemplates ?? Array.Empty<CalculatedStatModifierDefinition>())
                {
                    if (modifier == null || !modifier.IsValid)
                    {
                        report.AddError($"Affix definition '{DisplayName}' tier '{tier.tierId}' has an invalid stat modifier template.");
                    }
                    else if (definitionsById == null || !definitionsById.TryGetValue(modifier.Stat.Id, out IGameDefinition registeredStat) || !ReferenceEquals(registeredStat, modifier.Stat))
                    {
                        report.AddError($"Affix definition '{DisplayName}' tier '{tier.tierId}' references calculated stat '{modifier.Stat.Id}' outside the configured catalog.");
                    }
                }
            }

            if (Tiers.Count == 0)
            {
                report.AddError($"Affix definition '{DisplayName}' must declare at least one tier.");
            }
        }

        private static void ValidateReferences<TDefinition>(
            string label,
            IEnumerable<TDefinition> definitions,
            IReadOnlyDictionary<string, IGameDefinition> definitionsById,
            DefinitionValidationReport report)
            where TDefinition : class, IGameDefinition
        {
            foreach (TDefinition definition in definitions ?? Array.Empty<TDefinition>())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    report.AddError($"Affix definition has a missing {label} reference.");
                    continue;
                }

                if (definitionsById == null || !definitionsById.TryGetValue(definition.Id, out IGameDefinition found) || found is not TDefinition)
                {
                    report.AddError($"Affix definition {label} reference '{definition.Id}' is not present in the catalog.");
                }
            }
        }
    }
}
