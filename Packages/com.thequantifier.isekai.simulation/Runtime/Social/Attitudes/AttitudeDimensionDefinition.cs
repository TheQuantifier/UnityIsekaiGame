using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Attitudes
{
    [CreateAssetMenu(fileName = "AttitudeDimensionDefinition", menuName = "Unity Isekai Game/Social/Attitude Dimension Definition")]
    public sealed class AttitudeDimensionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string dimensionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private AttitudeDimensionCategory category = AttitudeDimensionCategory.Regard;
        [SerializeField] private int minimumValue = -100;
        [SerializeField] private int maximumValue = 100;
        [SerializeField] private int neutralValue;
        [SerializeField] private bool negativeValuesAllowed = true;
        [SerializeField] private AttitudeSemanticDirection semanticDirection = AttitudeSemanticDirection.HigherMeansMoreOfDimension;
        [SerializeField] private AttitudeValuePrecision precision = AttitudeValuePrecision.Integer;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => dimensionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public AttitudeDimensionCategory Category => category;
        public int MinimumValue => minimumValue;
        public int MaximumValue => maximumValue;
        public int NeutralValue => neutralValue;
        public bool NegativeValuesAllowed => negativeValuesAllowed;
        public AttitudeSemanticDirection SemanticDirection => semanticDirection;
        public AttitudeValuePrecision Precision => precision;
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
            AttitudeDimensionCategory dimensionCategory,
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
            semanticDirection = AttitudeSemanticDirection.HigherMeansMoreOfDimension;
            precision = AttitudeValuePrecision.Integer;
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
                report.AddError($"Attitude Dimension Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("attitude.", StringComparison.Ordinal))
            {
                report.AddWarning($"Attitude Dimension Definition '{Id}' should use the 'attitude.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(AttitudeDimensionCategory), category))
            {
                report.AddError($"Attitude Dimension Definition '{DisplayName}' has invalid category '{category}'.");
            }

            if (!Enum.IsDefined(typeof(AttitudeSemanticDirection), semanticDirection))
            {
                report.AddError($"Attitude Dimension Definition '{DisplayName}' has invalid semantic direction '{semanticDirection}'.");
            }

            if (!Enum.IsDefined(typeof(AttitudeValuePrecision), precision))
            {
                report.AddError($"Attitude Dimension Definition '{DisplayName}' has invalid precision '{precision}'.");
            }

            if (minimumValue >= maximumValue)
            {
                report.AddError($"Attitude Dimension Definition '{DisplayName}' must have minimum less than maximum.");
            }

            if (!negativeValuesAllowed && minimumValue < 0)
            {
                report.AddError($"Attitude Dimension Definition '{DisplayName}' disallows negative values but has minimum {minimumValue}.");
            }

            if (neutralValue < minimumValue || neutralValue > maximumValue)
            {
                report.AddError($"Attitude Dimension Definition '{DisplayName}' has neutral value outside the authored range.");
            }

            if (version < 1)
            {
                report.AddError($"Attitude Dimension Definition '{DisplayName}' has invalid version '{version}'.");
            }

            foreach (string tag in tags ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    report.AddError($"Attitude Dimension Definition '{DisplayName}' contains a blank tag.");
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
