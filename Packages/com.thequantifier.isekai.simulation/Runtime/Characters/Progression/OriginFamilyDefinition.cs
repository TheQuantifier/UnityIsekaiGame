using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Progression
{
    [CreateAssetMenu(fileName = "OriginFamilyDefinition", menuName = "Unity Isekai Game/Progression/Origin Family Definition")]
    public sealed class OriginFamilyDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string originFamilyId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField, Min(0f)] private float selectionWeight = 1f;
        [SerializeField] private bool enabledForAlpha = true;
        [SerializeField] private OriginDefinition[] allowedOrigins;
        [SerializeField] private RarityWeightModifierDefinition[] giftRarityWeightModifiers;
        [SerializeField] private ProgressionCurrencyGrantDefinition defaultStartingMoney;

        public string OriginFamilyId => originFamilyId;
        public string Id => originFamilyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Origin;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public float SelectionWeight => Mathf.Max(0f, selectionWeight);
        public bool EnabledForAlpha => enabledForAlpha;
        public IReadOnlyList<OriginDefinition> AllowedOrigins => allowedOrigins ?? System.Array.Empty<OriginDefinition>();
        public IReadOnlyList<RarityWeightModifierDefinition> GiftRarityWeightModifiers => giftRarityWeightModifiers ?? System.Array.Empty<RarityWeightModifierDefinition>();
        public ProgressionCurrencyGrantDefinition DefaultStartingMoney => defaultStartingMoney;

        private void OnValidate()
        {
            selectionWeight = Mathf.Max(0f, selectionWeight);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (!Id.StartsWith("origin-family."))
            {
                report.AddWarning($"Origin family '{DisplayName}' should use the 'origin-family.' namespace prefix.");
            }

            if (SelectionWeight <= 0f && enabledForAlpha)
            {
                report.AddError($"Origin family '{DisplayName}' is alpha-enabled but has no selection weight.");
            }

            int enabledOriginCount = 0;
            HashSet<string> seenOriginIds = new HashSet<string>();
            foreach (OriginDefinition origin in AllowedOrigins)
            {
                if (origin == null)
                {
                    report.AddError($"Origin family '{DisplayName}' has a missing allowed origin reference.");
                    continue;
                }

                if (!seenOriginIds.Add(origin.Id))
                {
                    report.AddError($"Origin family '{DisplayName}' has duplicate allowed origin '{origin.Id}'.");
                }

                if (definitionsById == null || !definitionsById.TryGetValue(origin.Id, out IGameDefinition found) || found is not OriginDefinition)
                {
                    report.AddError($"Origin family '{DisplayName}' references origin '{origin.Id}', which is not in the configured catalog.");
                }

                if (origin.Family != this)
                {
                    report.AddError($"Origin '{origin.DisplayName}' is listed by family '{DisplayName}' but points to '{origin.Family?.Id ?? "none"}'.");
                }

                if (origin.EnabledForAlpha)
                {
                    enabledOriginCount++;
                }
            }

            if (enabledForAlpha && enabledOriginCount == 0)
            {
                report.AddError($"Origin family '{DisplayName}' is alpha-enabled but has no enabled origins.");
            }

            HashSet<string> modifiedRarityIds = new HashSet<string>();
            foreach (RarityWeightModifierDefinition modifier in GiftRarityWeightModifiers)
            {
                if (modifier == null || modifier.Rarity == null)
                {
                    report.AddError($"Origin family '{DisplayName}' has a missing gift rarity modifier.");
                    continue;
                }

                if (modifier.RawWeightMultiplier < 0f || float.IsNaN(modifier.RawWeightMultiplier) || float.IsInfinity(modifier.RawWeightMultiplier))
                {
                    report.AddError($"Origin family '{DisplayName}' has an invalid rarity weight modifier.");
                }

                if (!modifiedRarityIds.Add(modifier.Rarity.Id))
                {
                    report.AddError($"Origin family '{DisplayName}' has duplicate weight modifiers for rarity '{modifier.Rarity.Id}'.");
                }

                if (definitionsById == null || !definitionsById.TryGetValue(modifier.Rarity.Id, out IGameDefinition found) || found is not RarityDefinition)
                {
                    report.AddError($"Origin family '{DisplayName}' references rarity '{modifier.Rarity.Id}', which is not in the configured catalog.");
                }
            }
        }
    }
}
