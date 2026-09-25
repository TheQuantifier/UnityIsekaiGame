using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Economy.Businesses
{
    [Serializable]
    public sealed class BusinessSharePolicyData
    {
        public bool requireTotalActiveOwnership = true;
        public long requiredTotalNumerator = 10000L;
        public long requiredTotalDenominator = 10000L;
        public bool allowControlDifferentFromOwnership = true;

        public BusinessSharePolicyData Clone()
        {
            return new BusinessSharePolicyData
            {
                requireTotalActiveOwnership = requireTotalActiveOwnership,
                requiredTotalNumerator = Math.Max(0L, requiredTotalNumerator),
                requiredTotalDenominator = Math.Max(1L, requiredTotalDenominator),
                allowControlDifferentFromOwnership = allowControlDifferentFromOwnership
            };
        }
    }

    [Serializable]
    public sealed class MerchantSecondhandStockPolicyData
    {
        public bool enabled = true;
        [Range(0, 10000)] public int minimumStockBasisPoints = 1500;
        [Range(0, 10000)] public int maximumStockBasisPoints = 5000;
        [Min(1)] public int minimumEligibleStockQuantity = 2;
        [Range(0, 10000)] public int minimumConditionBasisPoints = 4500;
        [Range(0, 10000)] public int maximumConditionBasisPoints = 9000;

        public MerchantSecondhandStockPolicyData Clone()
        {
            int minimumStock = Math.Clamp(minimumStockBasisPoints, 0, 10000);
            int maximumStock = Math.Clamp(maximumStockBasisPoints, minimumStock, 10000);
            int minimumCondition = Math.Clamp(minimumConditionBasisPoints, 0, 10000);
            return new MerchantSecondhandStockPolicyData
            {
                enabled = enabled,
                minimumStockBasisPoints = minimumStock,
                maximumStockBasisPoints = maximumStock,
                minimumEligibleStockQuantity = Math.Max(1, minimumEligibleStockQuantity),
                minimumConditionBasisPoints = minimumCondition,
                maximumConditionBasisPoints = Math.Clamp(maximumConditionBasisPoints, minimumCondition, 10000)
            };
        }
    }

    [Serializable]
    public sealed class MerchantSimulationPolicyData
    {
        [Min(1)] public int marketIntervalSeconds = 60;
        [Min(1)] public int maximumCatchUpIntervalsPerFrame = 8;
        [Min(0)] public long openingOperatingFunds = 1000L;
        [Min(0)] public long externalMarketLiquidity = 100000L;
        [Min(0)] public long rawResourceOpeningStock = 120L;
        [Min(0)] public long leatherOpeningStock = 40L;
        [Min(0)] public long exportOpeningStock = 12L;
        [Min(0)] public long rawResourceTarget = 120L;
        [Min(0)] public long leatherTarget = 40L;
        [Min(0)] public long exportStockTarget = 12L;
        [Min(0)] public long exportReserve = 4L;
        [Range(0, 10000)] public int productionBaseQualityBasisPoints = 2800;
        [Range(0, 10000)] public int productionQualityVariationBasisPoints = 800;
        [Range(0, 10000)] public int minimumProductionMarginBasisPoints = 1000;
        [Min(60)] public int retainedMarketIntervals = 10080;

        public MerchantSimulationPolicyData Clone()
        {
            long target = Math.Max(0L, exportStockTarget);
            return new MerchantSimulationPolicyData
            {
                marketIntervalSeconds = Math.Max(1, marketIntervalSeconds),
                maximumCatchUpIntervalsPerFrame = Math.Max(1, maximumCatchUpIntervalsPerFrame),
                openingOperatingFunds = Math.Max(0L, openingOperatingFunds),
                externalMarketLiquidity = Math.Max(0L, externalMarketLiquidity),
                rawResourceOpeningStock = Math.Max(0L, rawResourceOpeningStock),
                leatherOpeningStock = Math.Max(0L, leatherOpeningStock),
                exportOpeningStock = Math.Max(0L, exportOpeningStock),
                rawResourceTarget = Math.Max(0L, rawResourceTarget),
                leatherTarget = Math.Max(0L, leatherTarget),
                exportStockTarget = target,
                exportReserve = Math.Clamp(exportReserve, 0L, target),
                productionBaseQualityBasisPoints = Math.Clamp(productionBaseQualityBasisPoints, 0, 10000),
                productionQualityVariationBasisPoints = Math.Clamp(productionQualityVariationBasisPoints, 0, 10000),
                minimumProductionMarginBasisPoints = Math.Clamp(minimumProductionMarginBasisPoints, 0, 10000),
                retainedMarketIntervals = Math.Max(60, retainedMarketIntervals)
            };
        }
    }

    public static class MerchantSecondhandStockCalculator
    {
        public static int CalculateTargetCount(long totalStock, MerchantSecondhandStockPolicyData policy, uint deterministicRoll)
        {
            MerchantSecondhandStockPolicyData resolved = (policy ?? new MerchantSecondhandStockPolicyData()).Clone();
            if (!resolved.enabled || totalStock < resolved.minimumEligibleStockQuantity || totalStock > int.MaxValue)
            {
                return 0;
            }

            int total = (int)totalStock;
            int minimum = (int)Math.Ceiling(total * resolved.minimumStockBasisPoints / 10000d);
            int maximum = (int)Math.Floor(total * resolved.maximumStockBasisPoints / 10000d);
            if (minimum > maximum)
            {
                return 0;
            }

            uint range = (uint)(resolved.maximumStockBasisPoints - resolved.minimumStockBasisPoints + 1);
            int targetBasisPoints = resolved.minimumStockBasisPoints + (int)(deterministicRoll % range);
            int target = (int)Math.Round(total * targetBasisPoints / 10000d, MidpointRounding.AwayFromZero);
            return Math.Clamp(target, minimum, maximum);
        }

        public static int CalculateRetailTargetCount(long aggregateStock, int exactStockCapacity, MerchantSecondhandStockPolicyData policy, uint deterministicRoll)
        {
            MerchantSecondhandStockPolicyData resolved = (policy ?? new MerchantSecondhandStockPolicyData()).Clone();
            aggregateStock = Math.Max(0L, aggregateStock);
            exactStockCapacity = Math.Max(0, exactStockCapacity);
            long maximumCandidateLong = aggregateStock + exactStockCapacity;
            if (!resolved.enabled || maximumCandidateLong < resolved.minimumEligibleStockQuantity || maximumCandidateLong > int.MaxValue)
            {
                return 0;
            }

            int maximumCandidate = (int)maximumCandidateLong;
            int minimum = FirstCountAtOrAboveShare(aggregateStock, exactStockCapacity, maximumCandidate, resolved.minimumStockBasisPoints);
            int maximum = LastCountAtOrBelowShare(aggregateStock, exactStockCapacity, maximumCandidate, resolved.maximumStockBasisPoints);
            if (minimum > maximum)
            {
                return 0;
            }

            uint range = (uint)(resolved.maximumStockBasisPoints - resolved.minimumStockBasisPoints + 1);
            int targetBasisPoints = resolved.minimumStockBasisPoints + (int)(deterministicRoll % range);
            int low = minimum;
            int high = maximum;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (RetailShareBasisPoints(aggregateStock, exactStockCapacity, mid) < targetBasisPoints) low = mid + 1;
                else high = mid;
            }

            int upper = low;
            int lower = Math.Max(minimum, upper - 1);
            int lowerDistance = Math.Abs(RetailShareBasisPoints(aggregateStock, exactStockCapacity, lower) - targetBasisPoints);
            int upperDistance = Math.Abs(RetailShareBasisPoints(aggregateStock, exactStockCapacity, upper) - targetBasisPoints);
            return upperDistance < lowerDistance ? upper : lower;
        }

        public static int RetailShareBasisPoints(long aggregateStock, int exactStockCapacity, int saleEligibleExactCount)
        {
            aggregateStock = Math.Max(0L, aggregateStock);
            exactStockCapacity = Math.Max(0, exactStockCapacity);
            int exact = Math.Clamp(saleEligibleExactCount, 0, checked((int)Math.Min(int.MaxValue, aggregateStock + exactStockCapacity)));
            long aggregateAfterMaterialization = exact <= exactStockCapacity
                ? aggregateStock
                : Math.Max(0L, aggregateStock - (exact - exactStockCapacity));
            long retailTotal = aggregateAfterMaterialization + exact;
            return retailTotal <= 0L ? 0 : (int)Math.Round(exact * 10000d / retailTotal, MidpointRounding.AwayFromZero);
        }

        private static int FirstCountAtOrAboveShare(long aggregateStock, int exactStockCapacity, int maximumCandidate, int basisPoints)
        {
            int low = 0;
            int high = maximumCandidate;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (RetailShareBasisPoints(aggregateStock, exactStockCapacity, mid) < basisPoints) low = mid + 1;
                else high = mid;
            }
            return low;
        }

        private static int LastCountAtOrBelowShare(long aggregateStock, int exactStockCapacity, int maximumCandidate, int basisPoints)
        {
            int low = 0;
            int high = maximumCandidate;
            while (low < high)
            {
                int mid = low + (high - low + 1) / 2;
                if (RetailShareBasisPoints(aggregateStock, exactStockCapacity, mid) <= basisPoints) low = mid;
                else high = mid - 1;
            }
            return low;
        }
    }

    [CreateAssetMenu(fileName = "BusinessDefinition", menuName = "Unity Isekai Game/Economy/Business Definition")]
    public class BusinessDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string businessDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private BusinessCategory category = BusinessCategory.MerchantShop;
        [SerializeField] private BusinessOwnerSubjectKind[] permittedOwnerTypes = { BusinessOwnerSubjectKind.Person, BusinessOwnerSubjectKind.Organization };
        [SerializeField] private BusinessEstablishmentType[] permittedEstablishmentTypes = { BusinessEstablishmentType.Shop, BusinessEstablishmentType.Stall, BusinessEstablishmentType.Workshop };
        [SerializeField] private string[] permittedGoodsAndServiceCategories = Array.Empty<string>();
        [SerializeField] private string[] requiredProfessionOrCredentialIds = Array.Empty<string>();
        [SerializeField] private string[] requiredRoleOrPositionIds = Array.Empty<string>();
        [SerializeField] private BusinessAccountPurpose[] defaultAccountPurposes = { BusinessAccountPurpose.OperatingFunds };
        [SerializeField] private BusinessInventoryPurpose[] defaultInventoryPurposes = { BusinessInventoryPurpose.RetailStock };
        [SerializeField] private ProductionOutputOwnerPolicy defaultOutputOwnerPolicy = ProductionOutputOwnerPolicy.BusinessOwnsOutputs;
        [SerializeField] private BusinessRevenueCategory[] defaultRevenueCategories = { BusinessRevenueCategory.RetailSale, BusinessRevenueCategory.ServiceIncome };
        [SerializeField] private BusinessExpenseCategory[] defaultExpenseCategories = { BusinessExpenseCategory.InventoryPurchase, BusinessExpenseCategory.PayrollExpense };
        [SerializeField] private BusinessSharePolicyData ownershipPolicy = new BusinessSharePolicyData();
        [SerializeField] private MerchantSecondhandStockPolicyData secondhandStockPolicy = new MerchantSecondhandStockPolicyData();
        [SerializeField] private MerchantSimulationPolicyData simulationPolicy = new MerchantSimulationPolicyData();
        [SerializeField] private string defaultControlPolicyId;
        [SerializeField] private string accessPolicyId;
        [SerializeField, Min(1)] private int definitionVersion = 1;

        public string Id => businessDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public BusinessCategory Category => category;
        public IReadOnlyList<BusinessOwnerSubjectKind> PermittedOwnerTypes => BusinessModelHelpers.NormalizeEnums(permittedOwnerTypes);
        public IReadOnlyList<BusinessEstablishmentType> PermittedEstablishmentTypes => BusinessModelHelpers.NormalizeEnums(permittedEstablishmentTypes);
        public IReadOnlyList<string> PermittedGoodsAndServiceCategories => BusinessModelHelpers.CleanIds(permittedGoodsAndServiceCategories);
        public IReadOnlyList<string> RequiredProfessionOrCredentialIds => BusinessModelHelpers.CleanIds(requiredProfessionOrCredentialIds);
        public IReadOnlyList<string> RequiredRoleOrPositionIds => BusinessModelHelpers.CleanIds(requiredRoleOrPositionIds);
        public IReadOnlyList<BusinessAccountPurpose> DefaultAccountPurposes => BusinessModelHelpers.NormalizeEnums(defaultAccountPurposes);
        public IReadOnlyList<BusinessInventoryPurpose> DefaultInventoryPurposes => BusinessModelHelpers.NormalizeEnums(defaultInventoryPurposes);
        public ProductionOutputOwnerPolicy DefaultOutputOwnerPolicy => defaultOutputOwnerPolicy;
        public IReadOnlyList<BusinessRevenueCategory> DefaultRevenueCategories => BusinessModelHelpers.NormalizeEnums(defaultRevenueCategories);
        public IReadOnlyList<BusinessExpenseCategory> DefaultExpenseCategories => BusinessModelHelpers.NormalizeEnums(defaultExpenseCategories);
        public BusinessSharePolicyData OwnershipPolicy => ownershipPolicy?.Clone() ?? new BusinessSharePolicyData();
        public MerchantSecondhandStockPolicyData SecondhandStockPolicy => secondhandStockPolicy?.Clone() ?? new MerchantSecondhandStockPolicyData();
        public MerchantSimulationPolicyData SimulationPolicy => simulationPolicy?.Clone() ?? new MerchantSimulationPolicyData();
        public string DefaultControlPolicyId => defaultControlPolicyId ?? string.Empty;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int DefinitionVersion => Math.Max(1, definitionVersion);

        public void Initialize(string id, string display, BusinessCategory businessCategory)
        {
            businessDefinitionId = id ?? string.Empty;
            displayName = display ?? string.Empty;
            category = businessCategory;
            definitionVersion = Math.Max(1, definitionVersion);
        }

        public void ConfigureSecondhandStock(MerchantSecondhandStockPolicyData policy)
        {
            secondhandStockPolicy = (policy ?? new MerchantSecondhandStockPolicyData()).Clone();
        }

        public void ConfigureSimulation(MerchantSimulationPolicyData policy)
        {
            simulationPolicy = (policy ?? new MerchantSimulationPolicyData()).Clone();
        }

        private void OnValidate()
        {
            definitionVersion = Math.Max(1, definitionVersion);
            ownershipPolicy ??= new BusinessSharePolicyData();
            secondhandStockPolicy = (secondhandStockPolicy ?? new MerchantSecondhandStockPolicyData()).Clone();
            simulationPolicy = (simulationPolicy ?? new MerchantSimulationPolicyData()).Clone();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("business.", StringComparison.Ordinal))
            {
                report.AddError($"Business definition '{DisplayName}' must use the 'business.' namespace.");
            }

            if (!Enum.IsDefined(typeof(BusinessCategory), category) || category == BusinessCategory.Unknown)
            {
                report.AddError($"Business definition '{DisplayName}' has an invalid business category.");
            }

            if (PermittedOwnerTypes.Count == 0 || PermittedOwnerTypes.Contains(BusinessOwnerSubjectKind.Unknown))
            {
                report.AddError($"Business definition '{DisplayName}' must declare permitted owner types.");
            }

            if (PermittedEstablishmentTypes.Count == 0 || PermittedEstablishmentTypes.Contains(BusinessEstablishmentType.Unknown))
            {
                report.AddError($"Business definition '{DisplayName}' must declare permitted establishment types.");
            }

            BusinessSharePolicyData policy = OwnershipPolicy;
            if (policy.requiredTotalDenominator <= 0L || policy.requiredTotalNumerator < 0L || DefinitionVersion <= 0)
            {
                report.AddError($"Business definition '{DisplayName}' has an invalid ownership policy or version.");
            }

            MerchantSecondhandStockPolicyData secondhand = secondhandStockPolicy;
            if (secondhand == null
                || secondhand.minimumStockBasisPoints < 0
                || secondhand.maximumStockBasisPoints > 10000
                || secondhand.minimumStockBasisPoints > secondhand.maximumStockBasisPoints
                || secondhand.minimumEligibleStockQuantity < 1
                || secondhand.minimumConditionBasisPoints < 0
                || secondhand.maximumConditionBasisPoints > 10000
                || secondhand.minimumConditionBasisPoints > secondhand.maximumConditionBasisPoints)
            {
                report.AddError($"Business definition '{DisplayName}' has an invalid secondhand stock policy range.");
            }

            MerchantSimulationPolicyData simulation = simulationPolicy;
            if (simulation == null
                || simulation.marketIntervalSeconds < 1
                || simulation.maximumCatchUpIntervalsPerFrame < 1
                || simulation.openingOperatingFunds < 0L
                || simulation.externalMarketLiquidity < 0L
                || simulation.rawResourceOpeningStock < 0L
                || simulation.leatherOpeningStock < 0L
                || simulation.exportOpeningStock < 0L
                || simulation.rawResourceTarget < 0L
                || simulation.leatherTarget < 0L
                || simulation.exportStockTarget < 0L
                || simulation.exportReserve < 0L
                || simulation.exportReserve > simulation.exportStockTarget
                || simulation.productionBaseQualityBasisPoints < 0
                || simulation.productionBaseQualityBasisPoints > 10000
                || simulation.productionQualityVariationBasisPoints < 0
                || simulation.productionQualityVariationBasisPoints > 10000
                || simulation.minimumProductionMarginBasisPoints < 0
                || simulation.minimumProductionMarginBasisPoints > 10000
                || simulation.retainedMarketIntervals < 60)
            {
                report.AddError($"Business definition '{DisplayName}' has an invalid market simulation policy.");
            }

            foreach (string id in RequiredProfessionOrCredentialIds)
            {
                if (definitionsById != null && !definitionsById.ContainsKey(id))
                {
                    report.AddWarning($"Business definition '{DisplayName}' references optional profession or credential '{id}' that is not in the current catalog.");
                }
            }
        }
    }
}
