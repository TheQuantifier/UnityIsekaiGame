using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Economy.RegionalFlow
{
    [Serializable]
    public sealed class RegionalCommodityQuantityDefinitionData
    {
        [SerializeField] private string commodityId;
        [SerializeField] private CommodityUnit unit = CommodityUnit.Each;
        [SerializeField, Min(0)] private long quantity;

        public string CommodityId => commodityId ?? string.Empty;
        public CommodityUnit Unit => unit;
        public long Quantity => Math.Max(0L, quantity);

        public RegionalCommodityQuantityDefinitionData Clone()
        {
            return new RegionalCommodityQuantityDefinitionData
            {
                commodityId = commodityId ?? string.Empty,
                unit = unit,
                quantity = Math.Max(0L, quantity)
            };
        }

        public void Initialize(string id, CommodityUnit quantityUnit, long amount)
        {
            commodityId = id ?? string.Empty;
            unit = quantityUnit;
            quantity = Math.Max(0L, amount);
        }
    }

    [Serializable]
    public sealed class RegionalLaborQuantityDefinitionData
    {
        [SerializeField] private LaborCategory laborCategory = LaborCategory.GeneralLabor;
        [SerializeField, Min(0)] private long laborUnits;

        public LaborCategory LaborCategory => laborCategory;
        public long LaborUnits => Math.Max(0L, laborUnits);

        public RegionalLaborQuantityDefinitionData Clone()
        {
            return new RegionalLaborQuantityDefinitionData
            {
                laborCategory = laborCategory,
                laborUnits = Math.Max(0L, laborUnits)
            };
        }

        public void Initialize(LaborCategory category, long units)
        {
            laborCategory = category;
            laborUnits = Math.Max(0L, units);
        }
    }

    [CreateAssetMenu(fileName = "Economic Region Definition", menuName = "Unity Isekai Game/Economy/Economic Region Definition")]
    public class EconomicRegionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string definitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private EconomicRegionCategory category = EconomicRegionCategory.AbstractTestRegion;
        [SerializeField] private string futureLocationPolicyId;
        [SerializeField] private string[] permittedMarketCategories = Array.Empty<string>();
        [SerializeField] private string[] supportedCommodityCategories = Array.Empty<string>();
        [SerializeField] private LaborCategory[] supportedLaborCategories = { LaborCategory.GeneralLabor };
        [SerializeField] private string[] defaultProductionProfileIds = Array.Empty<string>();
        [SerializeField] private string[] defaultConsumptionProfileIds = Array.Empty<string>();
        [SerializeField] private string defaultReservePolicyId;
        [SerializeField] private string defaultFlowPolicyId;
        [SerializeField, Min(1)] private long defaultUpdateCadenceUnits = 1L;
        [SerializeField] private RegionalSimulationFidelity defaultSimulationFidelity = RegionalSimulationFidelity.AggregatePools;
        [SerializeField] private string accessPolicyId;
        [SerializeField, Min(1)] private int version = 1;

        public string Id => definitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public EconomicRegionCategory Category => category;
        public string FutureLocationPolicyId => futureLocationPolicyId ?? string.Empty;
        public IReadOnlyList<string> PermittedMarketCategories => Clean(permittedMarketCategories);
        public IReadOnlyList<string> SupportedCommodityCategories => Clean(supportedCommodityCategories);
        public IReadOnlyList<LaborCategory> SupportedLaborCategories => (supportedLaborCategories ?? Array.Empty<LaborCategory>()).Distinct().OrderBy(item => item).ToArray();
        public IReadOnlyList<string> DefaultProductionProfileIds => Clean(defaultProductionProfileIds);
        public IReadOnlyList<string> DefaultConsumptionProfileIds => Clean(defaultConsumptionProfileIds);
        public string DefaultReservePolicyId => defaultReservePolicyId ?? string.Empty;
        public string DefaultFlowPolicyId => defaultFlowPolicyId ?? string.Empty;
        public long DefaultUpdateCadenceUnits => Math.Max(1L, defaultUpdateCadenceUnits);
        public RegionalSimulationFidelity DefaultSimulationFidelity => defaultSimulationFidelity;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void Initialize(string id, string name, EconomicRegionCategory regionCategory, IEnumerable<LaborCategory> laborCategories = null)
        {
            definitionId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            category = regionCategory;
            supportedLaborCategories = (laborCategories ?? new[] { LaborCategory.GeneralLabor }).Distinct().OrderBy(item => item).ToArray();
            defaultSimulationFidelity = RegionalSimulationFidelity.AggregatePools;
            defaultUpdateCadenceUnits = Math.Max(1L, defaultUpdateCadenceUnits);
            version = Math.Max(1, version);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Economic Region Definition '{name}' is missing an ID.");
            }

            if (category == EconomicRegionCategory.Unknown)
            {
                report.AddError($"Economic Region Definition '{DisplayName}' must declare a concrete category.");
            }

            if (defaultSimulationFidelity == RegionalSimulationFidelity.Unknown)
            {
                report.AddError($"Economic Region Definition '{DisplayName}' must declare a simulation fidelity.");
            }

            if (DefaultUpdateCadenceUnits <= 0L)
            {
                report.AddError($"Economic Region Definition '{DisplayName}' must declare a positive update cadence.");
            }

            if (SupportedLaborCategories.Count == 0 || SupportedLaborCategories.Contains(LaborCategory.Unknown))
            {
                report.AddError($"Economic Region Definition '{DisplayName}' must declare supported labor categories.");
            }
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [CreateAssetMenu(fileName = "Commodity Definition", menuName = "Unity Isekai Game/Economy/Commodity Definition")]
    public class CommodityDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string commodityId;
        [SerializeField] private string displayName;
        [SerializeField] private ItemDefinition itemDefinition;
        [SerializeField] private MaterialDefinition materialDefinition;
        [SerializeField] private string itemCategoryId;
        [SerializeField] private string serviceCategoryId;
        [SerializeField] private LaborCategory laborCategory = LaborCategory.Unknown;
        [SerializeField] private CommodityCategory category = CommodityCategory.Custom;
        [SerializeField] private CommodityUnit unit = CommodityUnit.Each;
        [SerializeField] private CommodityFungibilityPolicy fungibilityPolicy = CommodityFungibilityPolicy.FullyFungible;
        [SerializeField] private CommodityMaterializationPolicy materializationPolicy = CommodityMaterializationPolicy.ExplicitOnly;
        [SerializeField] private CommodityAggregationPolicy aggregationPolicy = CommodityAggregationPolicy.ExplicitEligibleExactItemsOnly;
        [SerializeField] private string qualityBandPolicyId;
        [SerializeField] private string durabilityBandPolicyId;
        [SerializeField] private string compositionPolicyId;
        [SerializeField] private string spoilagePolicyId;
        [SerializeField, Min(1)] private long minimumAggregateUnit = 1L;
        [SerializeField] private string[] permittedProducerCategories = Array.Empty<string>();
        [SerializeField] private string[] permittedConsumerCategories = Array.Empty<string>();
        [SerializeField] private string marketSubjectId;
        [SerializeField] private string accessPolicyId;
        [SerializeField, Min(1)] private int version = 1;

        public string Id => commodityId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string ItemDefinitionId => itemDefinition == null ? string.Empty : itemDefinition.Id;
        public string MaterialDefinitionId => materialDefinition == null ? string.Empty : materialDefinition.Id;
        public string ItemCategoryId => itemCategoryId ?? string.Empty;
        public string ServiceCategoryId => serviceCategoryId ?? string.Empty;
        public LaborCategory LaborCategory => laborCategory;
        public CommodityCategory Category => category;
        public CommodityUnit Unit => unit;
        public CommodityFungibilityPolicy FungibilityPolicy => fungibilityPolicy;
        public CommodityMaterializationPolicy MaterializationPolicy => materializationPolicy;
        public CommodityAggregationPolicy AggregationPolicy => aggregationPolicy;
        public long MinimumAggregateUnit => Math.Max(1L, minimumAggregateUnit);
        public string MarketSubjectId => marketSubjectId ?? string.Empty;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void Initialize(string id, string name, CommodityCategory commodityCategory, CommodityUnit commodityUnit, string subjectId = "")
        {
            commodityId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            category = commodityCategory;
            unit = commodityUnit;
            marketSubjectId = subjectId ?? string.Empty;
            fungibilityPolicy = CommodityFungibilityPolicy.FullyFungible;
            materializationPolicy = CommodityMaterializationPolicy.ExplicitOnly;
            aggregationPolicy = CommodityAggregationPolicy.ExplicitEligibleExactItemsOnly;
            minimumAggregateUnit = Math.Max(1L, minimumAggregateUnit);
            version = Math.Max(1, version);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Commodity Definition '{name}' is missing an ID.");
            }

            if (category == CommodityCategory.Unknown || unit == CommodityUnit.Unknown || fungibilityPolicy == CommodityFungibilityPolicy.Unknown || materializationPolicy == CommodityMaterializationPolicy.Unknown || aggregationPolicy == CommodityAggregationPolicy.Unknown)
            {
                report.AddError($"Commodity Definition '{DisplayName}' must declare category, unit, fungibility, aggregation, and materialization policies.");
            }

            if (fungibilityPolicy == CommodityFungibilityPolicy.ExactOnly && aggregationPolicy != CommodityAggregationPolicy.ExactInventoryObservationOnly)
            {
                report.AddError($"Commodity Definition '{DisplayName}' cannot aggregate exact-only goods into pooled stock.");
            }

            if (MinimumAggregateUnit <= 0L)
            {
                report.AddError($"Commodity Definition '{DisplayName}' minimum aggregate unit must be positive.");
            }

            if (itemDefinition != null && definitionsById != null && !definitionsById.ContainsKey(itemDefinition.Id))
            {
                report.AddError($"Commodity Definition '{DisplayName}' references an item definition that is not in the catalog.");
            }

            if (materialDefinition != null && definitionsById != null && !definitionsById.ContainsKey(materialDefinition.Id))
            {
                report.AddError($"Commodity Definition '{DisplayName}' references a material definition that is not in the catalog.");
            }
        }
    }

    [CreateAssetMenu(fileName = "Aggregate Production Profile", menuName = "Unity Isekai Game/Economy/Aggregate Production Profile")]
    public class AggregateProductionProfileDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string profileId;
        [SerializeField] private string displayName;
        [SerializeField] private ProductionProfileCategory category = ProductionProfileCategory.Custom;
        [SerializeField] private RegionalCommodityQuantityDefinitionData[] outputs = Array.Empty<RegionalCommodityQuantityDefinitionData>();
        [SerializeField] private RegionalCommodityQuantityDefinitionData[] inputs = Array.Empty<RegionalCommodityQuantityDefinitionData>();
        [SerializeField] private RegionalLaborQuantityDefinitionData[] requiredLabor = Array.Empty<RegionalLaborQuantityDefinitionData>();
        [SerializeField, Min(1)] private long productionIntervalUnits = 1L;
        [SerializeField, Min(0)] private int yieldBasisPoints = 10000;
        [SerializeField, Min(1)] private long capacityLimitUnits = 1L;
        [SerializeField, Min(0)] private long minimumOperatingThresholdUnits;
        [SerializeField] private string accessPolicyId;
        [SerializeField, Min(1)] private int version = 1;

        public string Id => profileId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public ProductionProfileCategory Category => category;
        public IReadOnlyList<RegionalCommodityQuantityDefinitionData> Outputs => Clone(outputs);
        public IReadOnlyList<RegionalCommodityQuantityDefinitionData> Inputs => Clone(inputs);
        public IReadOnlyList<RegionalLaborQuantityDefinitionData> RequiredLabor => Clone(requiredLabor);
        public long ProductionIntervalUnits => Math.Max(1L, productionIntervalUnits);
        public int YieldBasisPoints => Math.Max(0, yieldBasisPoints);
        public long CapacityLimitUnits => Math.Max(1L, capacityLimitUnits);
        public long MinimumOperatingThresholdUnits => Math.Max(0L, minimumOperatingThresholdUnits);
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void Initialize(string id, string name, ProductionProfileCategory profileCategory, IEnumerable<RegionalCommodityQuantityDefinitionData> profileOutputs, IEnumerable<RegionalCommodityQuantityDefinitionData> profileInputs = null, IEnumerable<RegionalLaborQuantityDefinitionData> labor = null)
        {
            profileId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            category = profileCategory;
            outputs = Clone(profileOutputs).ToArray();
            inputs = Clone(profileInputs).ToArray();
            requiredLabor = Clone(labor).ToArray();
            productionIntervalUnits = Math.Max(1L, productionIntervalUnits);
            yieldBasisPoints = Math.Max(0, yieldBasisPoints);
            capacityLimitUnits = Math.Max(1L, capacityLimitUnits);
            version = Math.Max(1, version);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            ValidateProfile("Aggregate Production Profile", DisplayName, Id, category != ProductionProfileCategory.Unknown, Outputs, Inputs, RequiredLabor, report);
        }

        private static void ValidateProfile(string label, string name, string id, bool categoryValid, IReadOnlyList<RegionalCommodityQuantityDefinitionData> outputs, IReadOnlyList<RegionalCommodityQuantityDefinitionData> inputs, IReadOnlyList<RegionalLaborQuantityDefinitionData> labor, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                report.AddError($"{label} '{name}' is missing an ID.");
            }

            if (!categoryValid)
            {
                report.AddError($"{label} '{name}' must declare a concrete category.");
            }

            if (outputs == null || outputs.Count == 0 || outputs.Any(item => item == null || string.IsNullOrWhiteSpace(item.CommodityId) || item.Quantity <= 0L || item.Unit == CommodityUnit.Unknown))
            {
                report.AddError($"{label} '{name}' must declare positive commodity outputs.");
            }

            if ((inputs ?? Array.Empty<RegionalCommodityQuantityDefinitionData>()).Any(item => item == null || string.IsNullOrWhiteSpace(item.CommodityId) || item.Unit == CommodityUnit.Unknown))
            {
                report.AddError($"{label} '{name}' has invalid input requirements.");
            }

            if ((labor ?? Array.Empty<RegionalLaborQuantityDefinitionData>()).Any(item => item == null || item.LaborCategory == LaborCategory.Unknown))
            {
                report.AddError($"{label} '{name}' has invalid labor requirements.");
            }
        }

        private static RegionalCommodityQuantityDefinitionData[] Clone(IEnumerable<RegionalCommodityQuantityDefinitionData> values) => (values ?? Array.Empty<RegionalCommodityQuantityDefinitionData>()).Where(item => item != null).Select(item => item.Clone()).ToArray();
        private static RegionalLaborQuantityDefinitionData[] Clone(IEnumerable<RegionalLaborQuantityDefinitionData> values) => (values ?? Array.Empty<RegionalLaborQuantityDefinitionData>()).Where(item => item != null).Select(item => item.Clone()).ToArray();
    }

    [CreateAssetMenu(fileName = "Aggregate Consumption Profile", menuName = "Unity Isekai Game/Economy/Aggregate Consumption Profile")]
    public class AggregateConsumptionProfileDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string profileId;
        [SerializeField] private string displayName;
        [SerializeField] private ConsumptionProfileCategory category = ConsumptionProfileCategory.HouseholdNeed;
        [SerializeField] private RegionalCommodityQuantityDefinitionData[] consumed = Array.Empty<RegionalCommodityQuantityDefinitionData>();
        [SerializeField, Min(1)] private long consumptionIntervalUnits = 1L;
        [SerializeField] private string accessPolicyId;
        [SerializeField, Min(1)] private int version = 1;

        public string Id => profileId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public ConsumptionProfileCategory Category => category;
        public IReadOnlyList<RegionalCommodityQuantityDefinitionData> Consumed => (consumed ?? Array.Empty<RegionalCommodityQuantityDefinitionData>()).Where(item => item != null).Select(item => item.Clone()).ToArray();
        public long ConsumptionIntervalUnits => Math.Max(1L, consumptionIntervalUnits);
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void Initialize(string id, string name, ConsumptionProfileCategory profileCategory, IEnumerable<RegionalCommodityQuantityDefinitionData> consumedQuantities)
        {
            profileId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            category = profileCategory;
            consumed = (consumedQuantities ?? Array.Empty<RegionalCommodityQuantityDefinitionData>()).Where(item => item != null).Select(item => item.Clone()).ToArray();
            consumptionIntervalUnits = Math.Max(1L, consumptionIntervalUnits);
            version = Math.Max(1, version);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Aggregate Consumption Profile '{name}' is missing an ID.");
            }

            if (category == ConsumptionProfileCategory.Unknown)
            {
                report.AddError($"Aggregate Consumption Profile '{DisplayName}' must declare a concrete category.");
            }

            if (Consumed.Count == 0 || Consumed.Any(item => string.IsNullOrWhiteSpace(item.CommodityId) || item.Quantity <= 0L || item.Unit == CommodityUnit.Unknown))
            {
                report.AddError($"Aggregate Consumption Profile '{DisplayName}' must declare positive consumed commodities.");
            }
        }
    }
}
