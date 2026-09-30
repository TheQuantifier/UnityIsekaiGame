using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Economy.InstitutionalRevenue
{
    [CreateAssetMenu(fileName = "Institutional Revenue Definition", menuName = "Unity Isekai Game/Economy/Institutional Revenue Definition")]
    public sealed class InstitutionalRevenueDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string definitionId;
        [SerializeField] private string displayName;
        [SerializeField] private InstitutionalRevenueCategory category = InstitutionalRevenueCategory.Custom;
        [SerializeField] private InstitutionKind chargingInstitutionKind = InstitutionKind.Organization;
        [SerializeField] private InstitutionalRevenueAuthorityCategory requiredAuthorityCategory = InstitutionalRevenueAuthorityCategory.Assess;
        [SerializeField] private RevenueSubjectKind[] taxableSubjectKinds = Array.Empty<RevenueSubjectKind>();
        [SerializeField] private TaxableEventCategory[] taxableEventCategories = Array.Empty<TaxableEventCategory>();
        [SerializeField] private CurrencyDefinition currency;
        [SerializeField] private TaxBaseKind taxBaseKind = TaxBaseKind.FixedAmount;
        [SerializeField] private RevenueRatePolicyData ratePolicy = new RevenueRatePolicyData();
        [SerializeField] private AssessmentPeriodKind periodKind = AssessmentPeriodKind.PerEvent;
        [SerializeField] private string[] exemptionPolicyIds = Array.Empty<string>();
        [SerializeField] private string[] deductionPolicyIds = Array.Empty<string>();
        [SerializeField] private string[] creditPolicyIds = Array.Empty<string>();
        [SerializeField] private bool supportsWithholding;
        [SerializeField] private RevenueAccountPurpose collectionAccountPurpose = RevenueAccountPurpose.TaxCollection;
        [SerializeField] private string allocationPolicyId;
        [SerializeField] private bool filingRequired;
        [SerializeField] private long dueDelayUnits;
        [SerializeField] private long gracePeriodUnits;
        [SerializeField] private string[] penaltyPolicyIds = Array.Empty<string>();
        [SerializeField] private bool refundsAllowed = true;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private int version = 1;

        public string Id => definitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? definitionId : displayName;
        public InstitutionalRevenueCategory Category => category;
        public InstitutionKind ChargingInstitutionKind => chargingInstitutionKind;
        public InstitutionalRevenueAuthorityCategory RequiredAuthorityCategory => requiredAuthorityCategory;
        public IReadOnlyList<RevenueSubjectKind> TaxableSubjectKinds => taxableSubjectKinds ?? Array.Empty<RevenueSubjectKind>();
        public IReadOnlyList<TaxableEventCategory> TaxableEventCategories => taxableEventCategories ?? Array.Empty<TaxableEventCategory>();
        public string CurrencyId => currency == null ? string.Empty : currency.Id;
        public CurrencyDefinition Currency => currency;
        public TaxBaseKind TaxBaseKind => taxBaseKind;
        public RevenueRatePolicyData RatePolicy => ratePolicy?.Clone() ?? new RevenueRatePolicyData();
        public AssessmentPeriodKind PeriodKind => periodKind;
        public IReadOnlyList<string> ExemptionPolicyIds => exemptionPolicyIds ?? Array.Empty<string>();
        public IReadOnlyList<string> DeductionPolicyIds => deductionPolicyIds ?? Array.Empty<string>();
        public IReadOnlyList<string> CreditPolicyIds => creditPolicyIds ?? Array.Empty<string>();
        public bool SupportsWithholding => supportsWithholding;
        public RevenueAccountPurpose CollectionAccountPurpose => collectionAccountPurpose;
        public string AllocationPolicyId => allocationPolicyId ?? string.Empty;
        public bool FilingRequired => filingRequired;
        public long DueDelayUnits => Math.Max(0L, dueDelayUnits);
        public long GracePeriodUnits => Math.Max(0L, gracePeriodUnits);
        public IReadOnlyList<string> PenaltyPolicyIds => penaltyPolicyIds ?? Array.Empty<string>();
        public bool RefundsAllowed => refundsAllowed;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => version <= 0 ? 1 : version;

        public void Initialize(
            string id,
            string name,
            InstitutionalRevenueCategory revenueCategory,
            InstitutionKind institutionKind,
            InstitutionalRevenueAuthorityCategory authorityCategory,
            CurrencyDefinition currencyDefinition,
            TaxBaseKind baseKind,
            RevenueRatePolicyData rate,
            AssessmentPeriodKind assessmentPeriodKind,
            IEnumerable<RevenueSubjectKind> subjectKinds = null,
            IEnumerable<TaxableEventCategory> eventCategories = null,
            bool withholding = false,
            RevenueAccountPurpose accountPurpose = RevenueAccountPurpose.TaxCollection,
            bool requiresFiling = false,
            bool allowsRefunds = true)
        {
            definitionId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            category = revenueCategory;
            chargingInstitutionKind = institutionKind;
            requiredAuthorityCategory = authorityCategory;
            currency = currencyDefinition;
            taxBaseKind = baseKind;
            ratePolicy = rate?.Clone() ?? new RevenueRatePolicyData();
            periodKind = assessmentPeriodKind;
            taxableSubjectKinds = (subjectKinds ?? Array.Empty<RevenueSubjectKind>()).Distinct().OrderBy(item => item).ToArray();
            taxableEventCategories = (eventCategories ?? Array.Empty<TaxableEventCategory>()).Distinct().OrderBy(item => item).ToArray();
            supportsWithholding = withholding;
            collectionAccountPurpose = accountPurpose;
            filingRequired = requiresFiling;
            refundsAllowed = allowsRefunds;
            version = Math.Max(1, version);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (category == InstitutionalRevenueCategory.Unknown)
            {
                report.AddError($"Institutional Revenue Definition '{DisplayName}' must declare a revenue category.");
            }

            if (requiredAuthorityCategory == InstitutionalRevenueAuthorityCategory.Unknown)
            {
                report.AddError($"Institutional Revenue Definition '{DisplayName}' must declare a required authority category.");
            }

            if (TaxableSubjectKinds.Count == 0)
            {
                report.AddError($"Institutional Revenue Definition '{DisplayName}' must declare at least one taxable subject kind.");
            }

            if (TaxableEventCategories.Count == 0)
            {
                report.AddError($"Institutional Revenue Definition '{DisplayName}' must declare at least one taxable event category.");
            }

            if (currency == null || string.IsNullOrWhiteSpace(currency.Id) || definitionsById == null || !definitionsById.ContainsKey(currency.Id))
            {
                report.AddError($"Institutional Revenue Definition '{DisplayName}' must reference a catalog currency.");
            }

            if (!ValidateRatePolicy(ratePolicy, out string failure))
            {
                report.AddError($"Institutional Revenue Definition '{DisplayName}' rate policy is invalid: {failure}");
            }
        }

        public static bool ValidateRatePolicy(RevenueRatePolicyData policy, out string failure)
        {
            failure = string.Empty;
            if (policy == null)
            {
                failure = "Rate policy is missing.";
                return false;
            }

            if (policy.rateKind == RevenueRateKind.Unknown)
            {
                failure = "Rate policy kind is unknown.";
                return false;
            }

            if (policy.smallestChargeableUnit <= 0L)
            {
                failure = "Smallest chargeable unit must be positive.";
                return false;
            }

            if (policy.rateKind is RevenueRateKind.FlatProportional or RevenueRateKind.CappedProportionalCharge or RevenueRateKind.PercentagePlusFixedAmount)
            {
                if (policy.rate == null || policy.rate.numerator < 0L || policy.rate.denominator <= 0L)
                {
                    failure = "Proportional rate requires a non-negative numerator and positive denominator.";
                    return false;
                }
            }

            if (policy.rateKind == RevenueRateKind.PerUnit && policy.perUnitUnits < 0L)
            {
                failure = "Per-unit rate must not be negative.";
                return false;
            }

            if (policy.fixedUnits < 0L || policy.minimumUnits < 0L || policy.maximumUnits < -1L)
            {
                failure = "Fixed, minimum, and maximum amounts must be non-negative.";
                return false;
            }

            if (policy.maximumUnits >= 0L && policy.minimumUnits > policy.maximumUnits)
            {
                failure = "Minimum amount must not exceed maximum amount.";
                return false;
            }

            if (policy.rateKind is RevenueRateKind.ProgressiveBracket or RevenueRateKind.TieredFixedCharge or RevenueRateKind.ThresholdCharge or RevenueRateKind.ValueBand or RevenueRateKind.QuantityBand)
            {
                RevenueBracketData[] brackets = policy.brackets ?? Array.Empty<RevenueBracketData>();
                if (brackets.Length == 0)
                {
                    failure = "Bracketed policy requires at least one bracket.";
                    return false;
                }

                long previousUpper = 0L;
                bool first = true;
                foreach (RevenueBracketData bracket in brackets.OrderBy(item => item.lowerInclusive).ThenBy(item => item.bracketId, StringComparer.Ordinal))
                {
                    if (bracket == null || string.IsNullOrWhiteSpace(bracket.bracketId))
                    {
                        failure = "Every bracket must have a stable ID.";
                        return false;
                    }

                    if (bracket.lowerInclusive < 0L || bracket.upperExclusive < -1L || (bracket.upperExclusive >= 0L && bracket.upperExclusive <= bracket.lowerInclusive))
                    {
                        failure = $"Bracket '{bracket.bracketId}' has invalid bounds.";
                        return false;
                    }

                    if (!first && bracket.lowerInclusive < previousUpper)
                    {
                        failure = $"Bracket '{bracket.bracketId}' overlaps a previous bracket.";
                        return false;
                    }

                    if (bracket.rate == null || bracket.rate.numerator < 0L || bracket.rate.denominator <= 0L || bracket.fixedUnits < 0L)
                    {
                        failure = $"Bracket '{bracket.bracketId}' has invalid rate or fixed amount.";
                        return false;
                    }

                    if (bracket.upperExclusive >= 0L)
                    {
                        previousUpper = bracket.upperExclusive;
                    }

                    first = false;
                }
            }

            return true;
        }
    }
}
