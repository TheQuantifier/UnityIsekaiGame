using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Inventory.Durability
{
    [Serializable]
    public sealed class ItemConditionBandData
    {
        public string bandId;
        public string displayName;
        [Range(0f, 1f)] public float minimumNormalized;
        [Range(0f, 1f)] public float maximumNormalized = 1f;
        [Range(0f, 1f)] public float equipmentContribution = 1f;
        public ItemFunctionalState functionalState = ItemFunctionalState.FullyFunctional;
        public ItemBreakageState breakageState = ItemBreakageState.None;

        public bool Contains(float value, bool isLast)
        {
            return value >= minimumNormalized && (value < maximumNormalized || isLast && value <= maximumNormalized);
        }
    }

    [CreateAssetMenu(fileName = "ItemConditionScale", menuName = "Unity Isekai Game/Inventory/Item Condition Scale")]
    public sealed class ItemConditionScaleDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string scaleId = "condition.scale.standard";
        [SerializeField] private string displayName = "Standard Item Condition";
        [SerializeField] private ItemConditionBandData[] bands = Array.Empty<ItemConditionBandData>();

        public string Id => scaleId;
        public string DisplayName => displayName;
        public IReadOnlyList<ItemConditionBandData> Bands => bands ?? Array.Empty<ItemConditionBandData>();

        public bool TryResolve(float normalized, out ItemConditionBandData band)
        {
            float value = Mathf.Clamp01(normalized);
            ItemConditionBandData[] ordered = Bands.Where(entry => entry != null).OrderBy(entry => entry.minimumNormalized).ToArray();
            for (int i = 0; i < ordered.Length; i++)
            {
                if (ordered[i].Contains(value, i == ordered.Length - 1))
                {
                    band = ordered[i];
                    return true;
                }
            }

            band = null;
            return false;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            ItemConditionBandData[] ordered = Bands.Where(entry => entry != null).OrderBy(entry => entry.minimumNormalized).ToArray();
            if (ordered.Length == 0)
            {
                report?.AddError($"Condition scale '{Id}' has no bands.");
                return;
            }

            const float epsilon = 0.0001f;
            float expected = 0f;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemConditionBandData band in ordered)
            {
                if (!DefinitionIdValidator.IsValid(band.bandId) || !band.bandId.StartsWith("condition.", StringComparison.Ordinal))
                {
                    report?.AddError($"Condition scale '{Id}' has invalid band ID '{band.bandId}'. Use condition.<name>.");
                }
                if (!ids.Add(band.bandId ?? string.Empty)) report?.AddError($"Condition scale '{Id}' repeats band '{band.bandId}'.");
                if (band.minimumNormalized < expected - epsilon) report?.AddError($"Condition band '{band.bandId}' overlaps a previous band.");
                if (band.minimumNormalized > expected + epsilon) report?.AddError($"Condition scale '{Id}' has a gap before '{band.bandId}'.");
                if (band.maximumNormalized <= band.minimumNormalized || band.maximumNormalized > 1f) report?.AddError($"Condition band '{band.bandId}' has invalid bounds.");
                expected = band.maximumNormalized;
            }
            if (expected < 1f - epsilon) report?.AddError($"Condition scale '{Id}' does not cover values through 1.");
        }
    }
}
