using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Inventory.Composition
{
    [CreateAssetMenu(fileName = "NewMaterialCompatibilityRule", menuName = "Unity Isekai Game/Inventory/Material Compatibility Rule")]
    public sealed class MaterialCompatibilityRuleDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string ruleId;
        [SerializeField] private string displayName;
        [SerializeField] private MaterialDefinition sourceMaterial;
        [SerializeField] private MaterialDefinition targetMaterial;
        [SerializeField] private MaterialEntryRole sourceRole = MaterialEntryRole.Unknown;
        [SerializeField] private MaterialEntryRole targetRole = MaterialEntryRole.Unknown;
        [SerializeField] private MaterialCompatibilityOutcome outcome = MaterialCompatibilityOutcome.Neutral;
        [SerializeField] private int priority;
        [SerializeField, Range(0f, 2f)] private float durabilityMultiplier = 1f;
        [SerializeField, TextArea] private string message;

        public string Id => ruleId;
        public string DisplayName => displayName;
        public string SourceMaterialId => sourceMaterial == null ? string.Empty : sourceMaterial.Id;
        public string TargetMaterialId => targetMaterial == null ? string.Empty : targetMaterial.Id;
        public MaterialEntryRole SourceRole => sourceRole;
        public MaterialEntryRole TargetRole => targetRole;
        public MaterialCompatibilityOutcome Outcome => outcome;
        public int Priority => priority;
        public float DurabilityMultiplier => Mathf.Clamp(durabilityMultiplier, 0f, 2f);
        public string Message => message;

        private void OnValidate()
        {
            durabilityMultiplier = Mathf.Clamp(durabilityMultiplier, 0f, 2f);
        }

        public bool Matches(ItemMaterialEntryData source, ItemMaterialEntryData target)
        {
            if (source == null || target == null)
            {
                return false;
            }

            return (string.IsNullOrWhiteSpace(SourceMaterialId) || string.Equals(SourceMaterialId, source.materialDefinitionId, StringComparison.Ordinal))
                && (string.IsNullOrWhiteSpace(TargetMaterialId) || string.Equals(TargetMaterialId, target.materialDefinitionId, StringComparison.Ordinal))
                && (SourceRole == MaterialEntryRole.Unknown || SourceRole == source.role)
                && (TargetRole == MaterialEntryRole.Unknown || TargetRole == target.role);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ruleId))
            {
                report.AddError($"Material compatibility rule '{name}' is missing an ID.");
            }

            if (!Enum.IsDefined(typeof(MaterialCompatibilityOutcome), outcome))
            {
                report.AddError($"Material compatibility rule '{DisplayName}' has an invalid outcome.");
            }

            ValidateMaterial(SourceMaterialId, "source", definitionsById, report);
            ValidateMaterial(TargetMaterialId, "target", definitionsById, report);
        }

        private void ValidateMaterial(string materialId, string label, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(materialId))
            {
                return;
            }

            if (definitionsById == null || !definitionsById.TryGetValue(materialId, out IGameDefinition found) || found is not MaterialDefinition)
            {
                report.AddError($"Material compatibility rule '{DisplayName}' references missing {label} material '{materialId}'.");
            }
        }
    }
}
