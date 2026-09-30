using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Reputation
{
    [CreateAssetMenu(fileName = "ReputationDimensionDefinition", menuName = "Unity Isekai Game/Social/Reputation Dimension Definition")]
    public sealed class ReputationDimensionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string dimensionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ReputationDimensionCategory category = ReputationDimensionCategory.Regard;
        [SerializeField] private int minimumValue = -100;
        [SerializeField] private int maximumValue = 100;
        [SerializeField] private int neutralValue;
        [SerializeField] private bool negativeValuesAllowed = true;
        [SerializeField] private bool higherMeansMoreOfDimension = true;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => dimensionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public ReputationDimensionCategory Category => category;
        public int MinimumValue => minimumValue;
        public int MaximumValue => maximumValue;
        public int NeutralValue => neutralValue;
        public bool NegativeValuesAllowed => negativeValuesAllowed;
        public bool HigherMeansMoreOfDimension => higherMeansMoreOfDimension;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            dimensionId = dimensionId?.Trim();
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            ReputationDimensionCategory dimensionCategory,
            int minimum,
            int maximum,
            int neutral,
            bool allowNegative,
            string text = "",
            IEnumerable<string> tagIds = null)
        {
            dimensionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            description = text ?? string.Empty;
            category = dimensionCategory;
            minimumValue = minimum;
            maximumValue = maximum;
            neutralValue = neutral;
            negativeValuesAllowed = allowNegative;
            higherMeansMoreOfDimension = true;
            tags = Clean(tagIds);
            version = 1;
        }

        public int Clamp(int value, out bool clamped)
        {
            int clampedValue = Math.Max(MinimumValue, Math.Min(MaximumValue, value));
            clamped = clampedValue != value;
            return clampedValue;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Reputation Dimension Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("reputation.", StringComparison.Ordinal))
            {
                report.AddWarning($"Reputation Dimension Definition '{Id}' should use the 'reputation.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(ReputationDimensionCategory), category))
            {
                report.AddError($"Reputation Dimension Definition '{DisplayName}' has invalid category '{category}'.");
            }

            if (minimumValue >= maximumValue)
            {
                report.AddError($"Reputation Dimension Definition '{DisplayName}' must have minimum less than maximum.");
            }

            if (!negativeValuesAllowed && minimumValue < 0)
            {
                report.AddError($"Reputation Dimension Definition '{DisplayName}' disallows negative values but has minimum {minimumValue}.");
            }

            if (neutralValue < minimumValue || neutralValue > maximumValue)
            {
                report.AddError($"Reputation Dimension Definition '{DisplayName}' has neutral value outside the authored range.");
            }

            if (version < 1)
            {
                report.AddError($"Reputation Dimension Definition '{DisplayName}' has invalid version '{version}'.");
            }

            foreach (string tag in tags ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    report.AddError($"Reputation Dimension Definition '{DisplayName}' contains a blank tag.");
                }
            }
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
