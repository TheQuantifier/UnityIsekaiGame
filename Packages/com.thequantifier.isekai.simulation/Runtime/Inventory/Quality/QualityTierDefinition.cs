using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Inventory.Quality
{
    [CreateAssetMenu(fileName = "QualityTierDefinition", menuName = "Unity Isekai Game/Inventory/Quality Tier Definition")]
    public sealed class QualityTierDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string tierId;
        [SerializeField] private string displayName;
        [SerializeField, Range(0f, 1f)] private float minimumQuality;
        [SerializeField, Range(0f, 1f)] private float maximumQuality = 1f;
        [SerializeField] private int sortOrder;
        [SerializeField] private string gameplayClassification;
        [SerializeField] private string defaultModifierPolicyId;
        [SerializeField] private int appraisalDifficulty;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private int version = 1;

        public string Id => tierId;
        public string DisplayName => displayName;
        public float MinimumQuality => minimumQuality;
        public float MaximumQuality => maximumQuality;
        public int SortOrder => sortOrder;
        public string GameplayClassification => gameplayClassification ?? string.Empty;
        public string DefaultModifierPolicyId => defaultModifierPolicyId ?? string.Empty;
        public int AppraisalDifficulty => appraisalDifficulty;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public int Version => Math.Max(1, version);

        public bool Contains(float quality)
        {
            return quality >= minimumQuality && (quality < maximumQuality || Mathf.Approximately(maximumQuality, 1f) && quality <= maximumQuality);
        }

        private void OnValidate()
        {
            minimumQuality = Mathf.Clamp01(minimumQuality);
            maximumQuality = Mathf.Clamp01(maximumQuality);
            if (maximumQuality < minimumQuality)
            {
                maximumQuality = minimumQuality;
            }

            version = Mathf.Max(1, version);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (minimumQuality < 0f || maximumQuality > 1f || maximumQuality < minimumQuality)
            {
                report.AddError($"Quality tier '{DisplayName}' has an invalid quality range {minimumQuality:0.###}..{maximumQuality:0.###}.");
            }

            if (!Id.StartsWith("quality.", StringComparison.Ordinal))
            {
                report.AddError($"Quality tier '{Id}' must use the 'quality.<name>' namespace.");
            }

            foreach (TagDefinition tag in Tags)
            {
                if (tag == null)
                {
                    report.AddError($"Quality tier '{DisplayName}' has a missing tag.");
                }
            }

            QualityTierDefinition[] catalogTiers = definitionsById?.Values
                .OfType<QualityTierDefinition>()
                .OrderBy(tier => tier.Id, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<QualityTierDefinition>();
            if (catalogTiers.Length > 0 && ReferenceEquals(catalogTiers[0], this))
            {
                ValidateTierRanges(catalogTiers, report, requireGapless: true);
                foreach (IGrouping<int, QualityTierDefinition> duplicateOrder in catalogTiers.GroupBy(tier => tier.SortOrder).Where(group => group.Count() > 1))
                {
                    report.AddError($"Quality sort order {duplicateOrder.Key} is shared by {string.Join(", ", duplicateOrder.Select(tier => $"'{tier.Id}'"))}.");
                }
            }
        }

        public static bool ValidateTierRanges(IEnumerable<QualityTierDefinition> tiers, DefinitionValidationReport report, bool requireGapless = false)
        {
            List<QualityTierDefinition> ordered = new List<QualityTierDefinition>();
            if (tiers != null)
            {
                foreach (QualityTierDefinition tier in tiers)
                {
                    if (tier != null)
                    {
                        ordered.Add(tier);
                    }
                }
            }

            ordered.Sort((left, right) =>
            {
                int order = left.MinimumQuality.CompareTo(right.MinimumQuality);
                return order != 0 ? order : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
            });

            bool valid = true;
            if (requireGapless && ordered.Count > 0 && ordered[0].MinimumQuality > 0.0001f)
            {
                valid = false;
                report?.AddError($"Quality tiers have a gap from 0 to '{ordered[0].Id}'.");
            }
            float previousMax = -1f;
            string previousId = string.Empty;
            for (int i = 0; i < ordered.Count; i++)
            {
                QualityTierDefinition tier = ordered[i];
                if (tier.MinimumQuality < previousMax)
                {
                    valid = false;
                    report?.AddError($"Quality tier '{tier.Id}' overlaps previous tier '{previousId}'.");
                }

                if (requireGapless && previousMax >= 0f && tier.MinimumQuality > previousMax + 0.0001f)
                {
                    valid = false;
                    report?.AddError($"Quality tiers have a gap between '{previousId}' and '{tier.Id}'.");
                }

                previousMax = tier.MaximumQuality;
                previousId = tier.Id;
            }

            if (requireGapless && ordered.Count > 0 && previousMax < 0.9999f)
            {
                valid = false;
                report?.AddError($"Quality tiers have a gap after '{previousId}' through 1.");
            }

            return valid;
        }
    }
}
