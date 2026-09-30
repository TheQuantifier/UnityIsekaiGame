using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;

namespace UnityIsekaiGame.Inventory.Production
{
    [CreateAssetMenu(fileName = "ProductionToolDefinition", menuName = "Unity Isekai Game/Inventory/Production Tool Definition")]
    public sealed class ProductionToolDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string toolId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ProductionToolCategory category = ProductionToolCategory.Unknown;
        [SerializeField] private ProductionToolRole[] roles = Array.Empty<ProductionToolRole>();
        [SerializeField] private string[] capabilityIds = Array.Empty<string>();
        [SerializeField] private string[] substitutesForToolIds = Array.Empty<string>();
        [SerializeField, Range(0f, 1f)] private float minimumQuality;
        [SerializeField, Range(0f, 1f)] private float minimumDurability = 0.05f;
        [SerializeField, Min(0f)] private float durabilityWearPerUse;
        [SerializeField] private int priority;

        public string Id => toolId;
        public string DisplayName => displayName;
        public string Description => description ?? string.Empty;
        public ProductionToolCategory Category => category;
        public IReadOnlyList<ProductionToolRole> Roles => roles ?? Array.Empty<ProductionToolRole>();
        public IReadOnlyList<string> CapabilityIds => capabilityIds ?? Array.Empty<string>();
        public IReadOnlyList<string> SubstitutesForToolIds => substitutesForToolIds ?? Array.Empty<string>();
        public float MinimumQuality => minimumQuality;
        public float MinimumDurability => minimumDurability;
        public float DurabilityWearPerUse => durabilityWearPerUse;
        public int Priority => priority;

        private void OnValidate()
        {
            roles = NormalizeRoles(roles);
            capabilityIds = NormalizeIds(capabilityIds);
            substitutesForToolIds = NormalizeIds(substitutesForToolIds);
            minimumQuality = Mathf.Clamp01(minimumQuality);
            minimumDurability = Mathf.Clamp01(minimumDurability);
            durabilityWearPerUse = Mathf.Max(0f, durabilityWearPerUse);
        }

        public bool Supports(ProductionToolRole role, ProductionToolCategory requiredCategory, string capabilityId, string exactToolDefinitionId, bool allowSubstitution)
        {
            if (!string.IsNullOrWhiteSpace(exactToolDefinitionId)
                && !string.Equals(Id, exactToolDefinitionId, StringComparison.Ordinal)
                && !(allowSubstitution && SubstitutesForToolIds.Contains(exactToolDefinitionId, StringComparer.Ordinal)))
            {
                return false;
            }

            if (role != ProductionToolRole.Unknown && !Roles.Contains(role))
            {
                return false;
            }

            if (requiredCategory != ProductionToolCategory.Unknown && Category != requiredCategory)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(capabilityId) || CapabilityIds.Contains(capabilityId, StringComparer.Ordinal);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(toolId))
            {
                report.AddError($"Production Tool definition '{name}' is missing an ID.");
            }

            if (!Enum.IsDefined(typeof(ProductionToolCategory), category) || category == ProductionToolCategory.Unknown)
            {
                report.AddError($"Production Tool '{DisplayName}' must declare a concrete category.");
            }

            if (Roles.Count == 0 || Roles.Contains(ProductionToolRole.Unknown))
            {
                report.AddError($"Production Tool '{DisplayName}' must declare at least one concrete role.");
            }

            if (minimumDurability < 0f || minimumDurability > 1f || minimumQuality < 0f || minimumQuality > 1f)
            {
                report.AddError($"Production Tool '{DisplayName}' has invalid quality or durability thresholds.");
            }
        }

        private static ProductionToolRole[] NormalizeRoles(IEnumerable<ProductionToolRole> values)
        {
            return (values ?? Array.Empty<ProductionToolRole>())
                .Where(value => value != ProductionToolRole.Unknown && Enum.IsDefined(typeof(ProductionToolRole), value))
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
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

    [CreateAssetMenu(fileName = "ProductionStationDefinition", menuName = "Unity Isekai Game/Inventory/Production Station Definition")]
    public sealed partial class ProductionStationDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string stationId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ProductionStationCategory category = ProductionStationCategory.Unknown;
        [SerializeField] private string[] capabilityIds = Array.Empty<string>();
        [SerializeField] private ProductionToolRole[] supportedToolRoles = Array.Empty<ProductionToolRole>();
        [SerializeField, Min(1)] private int concurrentReservationLimit = 1;
        [SerializeField] private bool portable;
        [SerializeField] private int priority;

        public string Id => stationId;
        public string DisplayName => displayName;
        public string Description => description ?? string.Empty;
        public ProductionStationCategory Category => category;
        public IReadOnlyList<string> CapabilityIds => capabilityIds ?? Array.Empty<string>();
        public IReadOnlyList<ProductionToolRole> SupportedToolRoles => supportedToolRoles ?? Array.Empty<ProductionToolRole>();
        public int ConcurrentReservationLimit => Math.Max(1, concurrentReservationLimit);
        public bool Portable => portable;
        public int Priority => priority;

        private void OnValidate()
        {
            capabilityIds = NormalizeIds(capabilityIds);
            supportedToolRoles = (supportedToolRoles ?? Array.Empty<ProductionToolRole>())
                .Where(value => value != ProductionToolRole.Unknown && Enum.IsDefined(typeof(ProductionToolRole), value))
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            concurrentReservationLimit = Math.Max(1, concurrentReservationLimit);
        }

        public bool Supports(ProductionStationCategory requiredCategory, string capabilityId, ProductionToolRole toolRole)
        {
            if (requiredCategory != ProductionStationCategory.Unknown && Category != requiredCategory)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(capabilityId) && !CapabilityIds.Contains(capabilityId, StringComparer.Ordinal))
            {
                return false;
            }

            return toolRole == ProductionToolRole.Unknown || SupportedToolRoles.Count == 0 || SupportedToolRoles.Contains(toolRole);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(stationId))
            {
                report.AddError($"Production Station definition '{name}' is missing an ID.");
            }

            if (!Enum.IsDefined(typeof(ProductionStationCategory), category) || category == ProductionStationCategory.Unknown)
            {
                report.AddError($"Production Station '{DisplayName}' must declare a concrete category.");
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

    [Serializable]
    public sealed class ProductionRequirementAlternativeDefinition
    {
        [SerializeField] private ProductionRequirementType requirementType = ProductionRequirementType.Unknown;
        [SerializeField] private ProductionToolDefinition toolDefinition;
        [SerializeField] private ProductionToolRole toolRole = ProductionToolRole.Unknown;
        [SerializeField] private ProductionToolCategory toolCategory = ProductionToolCategory.Unknown;
        [SerializeField] private string toolCapabilityId;
        [SerializeField] private ProductionStationDefinition stationDefinition;
        [SerializeField] private ProductionStationCategory stationCategory = ProductionStationCategory.Unknown;
        [SerializeField] private string stationCapabilityId;
        [SerializeField] private string capabilityId;
        [SerializeField] private string knowledgeFactDefinitionId;
        [SerializeField] private ItemDefinition itemDefinition;
        [SerializeField] private MaterialDefinition materialDefinition;
        [SerializeField, Min(0f)] private float quantity = 1f;
        [SerializeField] private ProductionQuantityUnit quantityUnit = ProductionQuantityUnit.Count;

        public ProductionRequirementType RequirementType => requirementType;
        public ProductionToolDefinition ToolDefinition => toolDefinition;
        public string ToolDefinitionId => toolDefinition == null ? string.Empty : toolDefinition.Id;
        public ProductionToolRole ToolRole => toolRole;
        public ProductionToolCategory ToolCategory => toolCategory;
        public string ToolCapabilityId => toolCapabilityId ?? string.Empty;
        public ProductionStationDefinition StationDefinition => stationDefinition;
        public string StationDefinitionId => stationDefinition == null ? string.Empty : stationDefinition.Id;
        public ProductionStationCategory StationCategory => stationCategory;
        public string StationCapabilityId => stationCapabilityId ?? string.Empty;
        public string CapabilityId => capabilityId ?? string.Empty;
        public string KnowledgeFactDefinitionId => knowledgeFactDefinitionId ?? string.Empty;
        public ItemDefinition ItemDefinition => itemDefinition;
        public string ItemDefinitionId => itemDefinition == null ? string.Empty : itemDefinition.Id;
        public MaterialDefinition MaterialDefinition => materialDefinition;
        public string MaterialDefinitionId => materialDefinition == null ? string.Empty : materialDefinition.Id;
        public float Quantity => Mathf.Max(0f, quantity);
        public ProductionQuantityUnit QuantityUnit => quantityUnit;
    }

    [CreateAssetMenu(fileName = "ProductionRequirementDefinition", menuName = "Unity Isekai Game/Inventory/Production Requirement Definition")]
    public sealed partial class ProductionRequirementDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string requirementId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string requirementGroupId;
        [SerializeField] private ProductionRequirementType requirementType = ProductionRequirementType.Unknown;
        [SerializeField] private ProductionRequirementStrictness strictness = ProductionRequirementStrictness.Required;
        [SerializeField] private bool allowSubstitution = true;
        [SerializeField] private ProductionToolDefinition toolDefinition;
        [SerializeField] private ProductionToolRole toolRole = ProductionToolRole.Unknown;
        [SerializeField] private ProductionToolCategory toolCategory = ProductionToolCategory.Unknown;
        [SerializeField] private string toolCapabilityId;
        [SerializeField] private ProductionStationDefinition stationDefinition;
        [SerializeField] private ProductionStationCategory stationCategory = ProductionStationCategory.Unknown;
        [SerializeField] private string stationCapabilityId;
        [SerializeField] private string capabilityId;
        [SerializeField] private string knowledgeFactDefinitionId;
        [SerializeField] private string resourceId;
        [SerializeField] private ItemDefinition itemDefinition;
        [SerializeField] private MaterialDefinition materialDefinition;
        [SerializeField, Min(0f)] private float quantity = 1f;
        [SerializeField] private ProductionQuantityUnit quantityUnit = ProductionQuantityUnit.Count;
        [SerializeField] private string environmentKey;
        [SerializeField] private string accessKey;
        [SerializeField] private string bodyCapabilityId;
        [SerializeField] private ProductionRequirementAlternativeDefinition[] alternatives = Array.Empty<ProductionRequirementAlternativeDefinition>();
        [SerializeField] private int priority;

        public string Id => requirementId;
        public string DisplayName => displayName;
        public string Description => description ?? string.Empty;
        public string RequirementGroupId => requirementGroupId ?? string.Empty;
        public ProductionRequirementType RequirementType => requirementType;
        public ProductionRequirementStrictness Strictness => strictness;
        public bool AllowSubstitution => allowSubstitution;
        public ProductionToolDefinition ToolDefinition => toolDefinition;
        public string ToolDefinitionId => toolDefinition == null ? string.Empty : toolDefinition.Id;
        public ProductionToolRole ToolRole => toolRole;
        public ProductionToolCategory ToolCategory => toolCategory;
        public string ToolCapabilityId => toolCapabilityId ?? string.Empty;
        public ProductionStationDefinition StationDefinition => stationDefinition;
        public string StationDefinitionId => stationDefinition == null ? string.Empty : stationDefinition.Id;
        public ProductionStationCategory StationCategory => stationCategory;
        public string StationCapabilityId => stationCapabilityId ?? string.Empty;
        public string CapabilityId => capabilityId ?? string.Empty;
        public string KnowledgeFactDefinitionId => knowledgeFactDefinitionId ?? string.Empty;
        public string ResourceId => resourceId ?? string.Empty;
        public ItemDefinition ItemDefinition => itemDefinition;
        public string ItemDefinitionId => itemDefinition == null ? string.Empty : itemDefinition.Id;
        public MaterialDefinition MaterialDefinition => materialDefinition;
        public string MaterialDefinitionId => materialDefinition == null ? string.Empty : materialDefinition.Id;
        public float Quantity => Mathf.Max(0f, quantity);
        public ProductionQuantityUnit QuantityUnit => quantityUnit;
        public string EnvironmentKey => environmentKey ?? string.Empty;
        public string AccessKey => accessKey ?? string.Empty;
        public string BodyCapabilityId => bodyCapabilityId ?? string.Empty;
        public IReadOnlyList<ProductionRequirementAlternativeDefinition> Alternatives => alternatives ?? Array.Empty<ProductionRequirementAlternativeDefinition>();
        public int Priority => priority;

        private void OnValidate()
        {
            quantity = Mathf.Max(0f, quantity);
            alternatives ??= Array.Empty<ProductionRequirementAlternativeDefinition>();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(requirementId))
            {
                report.AddError($"Production Requirement definition '{name}' is missing an ID.");
            }

            if (!Enum.IsDefined(typeof(ProductionRequirementType), requirementType) || requirementType == ProductionRequirementType.Unknown)
            {
                report.AddError($"Production Requirement '{DisplayName}' must declare a concrete requirement type.");
            }

            if (Quantity <= 0f && (requirementType == ProductionRequirementType.Resource || requirementType == ProductionRequirementType.Item || requirementType == ProductionRequirementType.Material))
            {
                report.AddError($"Production Requirement '{DisplayName}' must declare a positive quantity.");
            }

            ValidateReferences(definitionsById, report, requirementType, ToolDefinitionId, StationDefinitionId, ItemDefinitionId, MaterialDefinitionId, $"Production Requirement '{DisplayName}'");
            foreach (ProductionRequirementAlternativeDefinition alternative in Alternatives)
            {
                if (alternative == null)
                {
                    report.AddError($"Production Requirement '{DisplayName}' has a null alternative.");
                    continue;
                }

                ValidateReferences(definitionsById, report, alternative.RequirementType, alternative.ToolDefinitionId, alternative.StationDefinitionId, alternative.ItemDefinitionId, alternative.MaterialDefinitionId, $"Production Requirement '{DisplayName}' alternative");
            }
        }

        private static void ValidateReferences(
            IReadOnlyDictionary<string, IGameDefinition> definitionsById,
            DefinitionValidationReport report,
            ProductionRequirementType type,
            string toolId,
            string stationId,
            string itemId,
            string materialId,
            string label)
        {
            if (!string.IsNullOrWhiteSpace(toolId) && (definitionsById == null || !definitionsById.TryGetValue(toolId, out IGameDefinition tool) || tool is not ProductionToolDefinition))
            {
                report.AddError($"{label} references missing Production Tool '{toolId}'.");
            }

            if (!string.IsNullOrWhiteSpace(stationId) && (definitionsById == null || !definitionsById.TryGetValue(stationId, out IGameDefinition station) || station is not ProductionStationDefinition))
            {
                report.AddError($"{label} references missing Production Station '{stationId}'.");
            }

            if (!string.IsNullOrWhiteSpace(itemId) && (definitionsById == null || !definitionsById.TryGetValue(itemId, out IGameDefinition item) || item is not UnityIsekaiGame.Inventory.ItemDefinition))
            {
                report.AddError($"{label} references missing Item '{itemId}'.");
            }

            if (!string.IsNullOrWhiteSpace(materialId) && (definitionsById == null || !definitionsById.TryGetValue(materialId, out IGameDefinition material) || material is not UnityIsekaiGame.Inventory.Composition.MaterialDefinition))
            {
                report.AddError($"{label} references missing Material '{materialId}'.");
            }

            if (type == ProductionRequirementType.Tool && string.IsNullOrWhiteSpace(toolId))
            {
                return;
            }
        }
    }
}
