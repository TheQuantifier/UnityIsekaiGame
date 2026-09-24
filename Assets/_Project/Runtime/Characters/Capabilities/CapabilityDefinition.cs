using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Capabilities
{
    [CreateAssetMenu(fileName = "CapabilityDefinition", menuName = "Unity Isekai Game/Capabilities/Capability Definition")]
    public sealed class CapabilityDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string capabilityId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private CapabilityValueType valueType;
        [SerializeField] private CapabilityAggregationPolicy aggregationPolicy;
        [SerializeField] private bool defaultBooleanValue;
        [SerializeField] private float defaultNumericValue;
        [SerializeField] private float minimumValue;
        [SerializeField] private float maximumValue;
        [SerializeField] private bool alphaEnabled = true;

        public string Id => capabilityId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Capability;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public CapabilityValueType ValueType => valueType;
        public CapabilityAggregationPolicy AggregationPolicy => aggregationPolicy;
        public bool DefaultBooleanValue => defaultBooleanValue;
        public float DefaultNumericValue => defaultNumericValue;
        public float MinimumValue => minimumValue;
        public float MaximumValue => maximumValue;
        public bool AlphaEnabled => alphaEnabled;

        private void OnValidate()
        {
            capabilityId = capabilityId?.Trim();
            if (maximumValue < minimumValue)
            {
                maximumValue = minimumValue;
            }
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Capability '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("capability.", StringComparison.Ordinal))
            {
                report.AddWarning($"Capability '{Id}' should use the 'capability.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(CapabilityValueType), valueType))
            {
                report.AddError($"Capability '{DisplayName}' has an invalid value type.");
            }

            if (!Enum.IsDefined(typeof(CapabilityAggregationPolicy), aggregationPolicy))
            {
                report.AddError($"Capability '{DisplayName}' has an invalid aggregation policy.");
            }

            if (valueType == CapabilityValueType.Boolean && aggregationPolicy != CapabilityAggregationPolicy.BooleanAny && aggregationPolicy != CapabilityAggregationPolicy.Blocker)
            {
                report.AddError($"Boolean Capability '{DisplayName}' cannot use aggregation policy '{aggregationPolicy}'.");
            }

            if (valueType == CapabilityValueType.Numeric && aggregationPolicy == CapabilityAggregationPolicy.BooleanAny)
            {
                report.AddError($"Numeric Capability '{DisplayName}' cannot use BooleanAny aggregation.");
            }

            if (!IsFinite(defaultNumericValue) || !IsFinite(minimumValue) || !IsFinite(maximumValue) || maximumValue < minimumValue)
            {
                report.AddError($"Capability '{DisplayName}' has invalid numeric bounds/defaults.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
