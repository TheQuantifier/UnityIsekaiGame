using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Inventory.Composition
{
    [Serializable]
    public sealed class MaterialPhysicalPropertySet
    {
        [Min(0f)] public float densityKgPerLiter;
        [Range(0f, 1f)] public float hardness;
        [Range(0f, 1f)] public float durability;
        [Range(0f, 1f)] public float flexibility;
        [Range(0f, 1f)] public float conductivity;
        [Range(0f, 1f)] public float flammability;
        [Range(0f, 1f)] public float biologicalCompatibility;
        public string propertyProfileId;

        public float DurabilityPotential => durability;
        public float WearResistance => durability;

        public MaterialPhysicalPropertySet Clone()
        {
            return new MaterialPhysicalPropertySet
            {
                densityKgPerLiter = densityKgPerLiter,
                hardness = hardness,
                durability = durability,
                flexibility = flexibility,
                conductivity = conductivity,
                flammability = flammability,
                biologicalCompatibility = biologicalCompatibility,
                propertyProfileId = propertyProfileId ?? string.Empty
            };
        }

        public void Validate(string label, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            ValidateNonNegativeFinite(densityKgPerLiter, $"{label} density", report);
            ValidateNormalized(hardness, $"{label} hardness", report);
            ValidateNormalized(durability, $"{label} durability", report);
            ValidateNormalized(flexibility, $"{label} flexibility", report);
            ValidateNormalized(conductivity, $"{label} conductivity", report);
            ValidateNormalized(flammability, $"{label} flammability", report);
            ValidateNormalized(biologicalCompatibility, $"{label} biological compatibility", report);
        }

        private static void ValidateNormalized(float value, string label, DefinitionValidationReport report)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
            {
                report.AddError($"{label} must be within 0..1.");
            }
        }

        private static void ValidateNonNegativeFinite(float value, string label, DefinitionValidationReport report)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                report.AddError($"{label} must be non-negative.");
            }
        }
    }

    [Serializable]
    public sealed class CompositeMaterialConstituentDefinition
    {
        [SerializeField] private MaterialDefinition material;
        [SerializeField, Range(0f, 1f)] private float ratio = 1f;

        public MaterialDefinition Material => material;
        public string MaterialId => material == null ? string.Empty : material.Id;
        public float Ratio => Mathf.Clamp01(ratio);
    }

    [CreateAssetMenu(fileName = "NewMaterialDefinition", menuName = "Unity Isekai Game/Inventory/Material Definition")]
    public sealed class MaterialDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string materialId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private MaterialCategory category = MaterialCategory.Unknown;
        [SerializeField] private string[] materialTags = Array.Empty<string>();
        [SerializeField] private bool canBeStructural = true;
        [SerializeField] private bool canBeCoating = true;
        [SerializeField] private bool canBeBinding;
        [SerializeField] private MaterialPhysicalPropertySet physicalProperties = new MaterialPhysicalPropertySet();
        [SerializeField] private CompositeMaterialConstituentDefinition[] constituents = Array.Empty<CompositeMaterialConstituentDefinition>();

        public string Id => materialId;
        public string DisplayName => displayName;
        public string Description => description;
        public MaterialCategory Category => category;
        public IReadOnlyList<string> MaterialTags => materialTags ?? Array.Empty<string>();
        public bool CanBeStructural => canBeStructural;
        public bool CanBeCoating => canBeCoating;
        public bool CanBeBinding => canBeBinding;
        public MaterialPhysicalPropertySet PhysicalProperties => physicalProperties ?? new MaterialPhysicalPropertySet();
        public IReadOnlyList<CompositeMaterialConstituentDefinition> Constituents => constituents ?? Array.Empty<CompositeMaterialConstituentDefinition>();
        public bool IsComposite => Constituents.Count > 0 || category == MaterialCategory.Composite;

        private void OnValidate()
        {
            materialTags = NormalizeIds(materialTags);
            physicalProperties ??= new MaterialPhysicalPropertySet();
            constituents ??= Array.Empty<CompositeMaterialConstituentDefinition>();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(materialId))
            {
                report.AddError($"Material definition '{name}' is missing an ID.");
            }

            if (!Enum.IsDefined(typeof(MaterialCategory), category) || category == MaterialCategory.Unknown)
            {
                report.AddError($"Material definition '{DisplayName}' must declare a concrete material category.");
            }

            physicalProperties?.Validate($"Material definition '{DisplayName}'", report);
            HashSet<string> seenConstituents = new HashSet<string>(StringComparer.Ordinal);
            foreach (CompositeMaterialConstituentDefinition constituent in Constituents)
            {
                if (constituent == null || constituent.Material == null || string.IsNullOrWhiteSpace(constituent.MaterialId))
                {
                    report.AddError($"Material definition '{DisplayName}' has a composite constituent with no material reference.");
                    continue;
                }

                if (constituent.Ratio <= 0f)
                {
                    report.AddError($"Material definition '{DisplayName}' constituent '{constituent.MaterialId}' has no positive ratio.");
                }

                if (!seenConstituents.Add(constituent.MaterialId))
                {
                    report.AddWarning($"Material definition '{DisplayName}' lists constituent '{constituent.MaterialId}' more than once.");
                }

                if (definitionsById == null || !definitionsById.TryGetValue(constituent.MaterialId, out IGameDefinition found) || found is not MaterialDefinition)
                {
                    report.AddError($"Material definition '{DisplayName}' references missing constituent material '{constituent.MaterialId}'.");
                }
            }
        }

        private static string[] NormalizeIds(IEnumerable<string> values)
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
