using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Hazards
{
    [CreateAssetMenu(fileName = "EnvironmentalExposure", menuName = "Unity Isekai Game/Beings/Biology/Environmental Exposure")]
    public sealed class EnvironmentalExposureDefinition : ScriptableObject, IGameDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string exposureId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private BiologicalHazardDefinition hazardDefinition;
        [SerializeField] private BiologicalHazardSeverity defaultSeverity = BiologicalHazardSeverity.Minor;
        [SerializeField, Min(0f)] private float defaultRateMultiplier = 1f;
        [SerializeField] private bool alphaEnabled = true;
        [SerializeField] private TagDefinition[] tags;

        public string Id => exposureId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public BiologicalHazardDefinition HazardDefinition => hazardDefinition;
        public string HazardDefinitionId => hazardDefinition == null ? string.Empty : hazardDefinition.Id;
        public BiologicalHazardSeverity DefaultSeverity => defaultSeverity;
        public float DefaultRateMultiplier => Mathf.Max(0f, defaultRateMultiplier);
        public bool AlphaEnabled => alphaEnabled;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();

        private void OnValidate()
        {
            exposureId = exposureId?.Trim();
            displayName = displayName?.Trim();
            defaultRateMultiplier = Mathf.Max(0f, defaultRateMultiplier);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"EnvironmentalExposureDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("exposure.environment.", StringComparison.Ordinal))
            {
                report.AddWarning($"EnvironmentalExposureDefinition '{Id}' should use the 'exposure.environment.' namespace prefix.");
            }

            if (hazardDefinition == null)
            {
                report.AddError($"EnvironmentalExposureDefinition '{DisplayName}' is missing a hazard reference.");
            }
            else if (definitionsById != null && (!definitionsById.TryGetValue(HazardDefinitionId, out IGameDefinition hazard) || !ReferenceEquals(hazard, hazardDefinition)))
            {
                report.AddError($"EnvironmentalExposureDefinition '{DisplayName}' references unknown Biological Hazard '{HazardDefinitionId}'.");
            }

            foreach (TagDefinition tag in Tags)
            {
                if (tag == null)
                {
                    report.AddError($"EnvironmentalExposureDefinition '{DisplayName}' has a missing tag reference.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(tag.Id, out IGameDefinition found) || found is not TagDefinition))
                {
                    report.AddError($"EnvironmentalExposureDefinition '{DisplayName}' references tag '{tag.Id}', which is not in the configured catalog.");
                }
            }

            ValidateCanonicalAlphaSet(definitionsById, report);
        }

        private static void ValidateCanonicalAlphaSet(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || !definitionsById.ContainsKey("species.human"))
            {
                return;
            }

            string[] required =
            {
                BiologicalExposureIds.BreathableAirUnavailable,
                BiologicalExposureIds.Heat,
                BiologicalExposureIds.Cold,
                BiologicalExposureIds.GeneralExposure
            };

            foreach (string id in required)
            {
                if (!definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not EnvironmentalExposureDefinition)
                {
                    report.AddError($"Canonical EnvironmentalExposureDefinition '{id}' must be registered in the alpha definition catalog.");
                }
            }
        }
    }
}
