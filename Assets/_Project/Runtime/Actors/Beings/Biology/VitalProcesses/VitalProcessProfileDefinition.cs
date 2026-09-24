using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.VitalProcesses
{
    [Serializable]
    public sealed class VitalResourceProfileEntry
    {
        [SerializeField] private BiologicalResourceDefinition resourceDefinition;
        [SerializeField] private bool active = true;
        [SerializeField] private float minimumValue;
        [SerializeField] private float maximumValue = 100f;
        [SerializeField] private float initialValue = 100f;
        [SerializeField] private float idealValue = 100f;
        [SerializeField] private float safeMinimum = 60f;
        [SerializeField] private float safeMaximum = 100f;
        [SerializeField] private float strainedLow = 35f;
        [SerializeField] private float strainedHigh = 75f;
        [SerializeField] private float criticalLow = 10f;
        [SerializeField] private float criticalHigh = 90f;
        [SerializeField] private float absoluteMinimum;
        [SerializeField] private float absoluteMaximum = 100f;
        [SerializeField] private float consumptionPerHour;
        [SerializeField] private float restorationPerHour;

        public BiologicalResourceDefinition ResourceDefinition => resourceDefinition;
        public string ResourceDefinitionId => resourceDefinition == null ? string.Empty : resourceDefinition.Id;
        public bool Active => active;
        public float MinimumValue => minimumValue;
        public float MaximumValue => maximumValue;
        public float InitialValue => initialValue;
        public float IdealValue => idealValue;
        public float SafeMinimum => safeMinimum;
        public float SafeMaximum => safeMaximum;
        public float StrainedLow => strainedLow;
        public float StrainedHigh => strainedHigh;
        public float CriticalLow => criticalLow;
        public float CriticalHigh => criticalHigh;
        public float AbsoluteMinimum => absoluteMinimum;
        public float AbsoluteMaximum => absoluteMaximum;
        public float ConsumptionPerHour => consumptionPerHour;
        public float RestorationPerHour => restorationPerHour;
    }

    [CreateAssetMenu(fileName = "VitalProcessProfile", menuName = "Unity Isekai Game/Beings/Biology/Vital Process Profile")]
    public sealed class VitalProcessProfileDefinition : ScriptableObject, IGameDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string profileId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private SpeciesDefinition[] compatibleSpecies;
        [SerializeField] private BiologicalClassificationDefinition[] compatibleClassifications;
        [SerializeField] private VitalResourceProfileEntry[] resources;
        [SerializeField] private TagDefinition[] tags;

        public string Id => profileId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public IReadOnlyList<SpeciesDefinition> CompatibleSpecies => compatibleSpecies ?? Array.Empty<SpeciesDefinition>();
        public IReadOnlyList<BiologicalClassificationDefinition> CompatibleClassifications => compatibleClassifications ?? Array.Empty<BiologicalClassificationDefinition>();
        public IReadOnlyList<string> CompatibleSpeciesIds => CompatibleSpecies.Select(definition => definition.Id).ToArray();
        public IReadOnlyList<string> CompatibleClassificationIds => CompatibleClassifications.Select(definition => definition.Id).ToArray();
        public IReadOnlyList<VitalResourceProfileEntry> Resources => resources ?? Array.Empty<VitalResourceProfileEntry>();
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();

        private void OnValidate()
        {
            profileId = profileId?.Trim();
            displayName = displayName?.Trim();
            compatibleSpecies = compatibleSpecies == null ? Array.Empty<SpeciesDefinition>() : compatibleSpecies.Where(definition => definition != null).Distinct().OrderBy(definition => definition.Id, StringComparer.Ordinal).ToArray();
            compatibleClassifications = compatibleClassifications == null ? Array.Empty<BiologicalClassificationDefinition>() : compatibleClassifications.Where(definition => definition != null).Distinct().OrderBy(definition => definition.Id, StringComparer.Ordinal).ToArray();
        }

        public bool IsCompatibleWith(SpeciesDefinition species)
        {
            if (species == null)
            {
                return false;
            }

            bool speciesMatch = CompatibleSpecies.Count == 0 || CompatibleSpecies.Contains(species);
            bool classificationMatch = CompatibleClassifications.Count == 0 || (species.BiologicalClassification != null && CompatibleClassifications.Contains(species.BiologicalClassification));
            return speciesMatch && classificationMatch;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"VitalProcessProfileDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("vital-profile.", StringComparison.Ordinal))
            {
                report.AddWarning($"VitalProcessProfileDefinition '{Id}' should use the 'vital-profile.' namespace prefix.");
            }

            HashSet<string> seenResources = new HashSet<string>(StringComparer.Ordinal);
            foreach (VitalResourceProfileEntry entry in Resources)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceDefinitionId))
                {
                    report.AddError($"VitalProcessProfileDefinition '{DisplayName}' has a missing Resource entry.");
                    continue;
                }

                if (!seenResources.Add(entry.ResourceDefinitionId))
                {
                    report.AddError($"VitalProcessProfileDefinition '{DisplayName}' has duplicate Resource entry '{entry.ResourceDefinitionId}'.");
                }

                if (definitionsById != null && (!definitionsById.TryGetValue(entry.ResourceDefinitionId, out IGameDefinition found) || found is not BiologicalResourceDefinition))
                {
                    report.AddError($"VitalProcessProfileDefinition '{DisplayName}' references unknown Biological Resource '{entry.ResourceDefinitionId}'.");
                }

                ValidateThresholds(entry, report);
            }

            foreach (SpeciesDefinition species in CompatibleSpecies)
            {
                if (definitionsById != null && (!definitionsById.TryGetValue(species.Id, out IGameDefinition found) || !ReferenceEquals(found, species)))
                {
                    report.AddError($"VitalProcessProfileDefinition '{DisplayName}' references unknown Species '{species.Id}'.");
                }
            }

            foreach (BiologicalClassificationDefinition classification in CompatibleClassifications)
            {
                if (definitionsById != null && (!definitionsById.TryGetValue(classification.Id, out IGameDefinition found) || !ReferenceEquals(found, classification)))
                {
                    report.AddError($"VitalProcessProfileDefinition '{DisplayName}' references unknown biological classification '{classification.Id}'.");
                }
            }

            ValidateCanonicalAlphaSet(definitionsById, report);
        }

        private static void ValidateThresholds(VitalResourceProfileEntry entry, DefinitionValidationReport report)
        {
            if (!IsFinite(entry.MinimumValue) || !IsFinite(entry.MaximumValue) || !IsFinite(entry.InitialValue) || !IsFinite(entry.IdealValue))
            {
                report.AddError($"Vital Resource profile entry '{entry.ResourceDefinitionId}' contains a non-finite value.");
            }

            if (entry.MaximumValue < entry.MinimumValue)
            {
                report.AddError($"Vital Resource profile entry '{entry.ResourceDefinitionId}' has maximum below minimum.");
            }

            if (entry.AbsoluteMaximum < entry.AbsoluteMinimum)
            {
                report.AddError($"Vital Resource profile entry '{entry.ResourceDefinitionId}' has absolute maximum below absolute minimum.");
            }

            if (entry.Active && (entry.InitialValue < entry.AbsoluteMinimum || entry.InitialValue > entry.AbsoluteMaximum))
            {
                report.AddError($"Vital Resource profile entry '{entry.ResourceDefinitionId}' has initial value outside absolute bounds.");
            }

            if (entry.Active && entry.ResourceDefinitionId == BiologicalResourceIds.Temperature && (entry.IdealValue < entry.SafeMinimum || entry.IdealValue > entry.SafeMaximum))
            {
                report.AddError($"Temperature profile entry '{entry.ResourceDefinitionId}' has ideal value outside the safe range.");
            }

            if (entry.ConsumptionPerHour < 0f || entry.RestorationPerHour < 0f)
            {
                report.AddError($"Vital Resource profile entry '{entry.ResourceDefinitionId}' has a negative process rate.");
            }
        }

        private void ValidateCanonicalAlphaSet(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || !definitionsById.ContainsKey("species.human"))
            {
                return;
            }

            string[] required =
            {
                "vital-profile.human",
                "vital-profile.undead-human",
                "vital-profile.basic-construct",
                "vital-profile.basic-spirit"
            };

            foreach (string id in required)
            {
                if (!definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not VitalProcessProfileDefinition)
                {
                    report.AddError($"Canonical VitalProcessProfileDefinition '{id}' must be registered in the alpha definition catalog.");
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
