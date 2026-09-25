using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Economy.Markets
{
    [Serializable]
    public sealed class MarketPriceFormationPolicyData
    {
        [SerializeField] private MarketPriceFormationKind policyKind = MarketPriceFormationKind.DefaultSupplyDemand;
        [SerializeField, Min(1)] private int scarcityStepBasisPoints = 1250;
        [SerializeField, Min(1)] private int maxMultiplierBasisPoints = 40000;
        [SerializeField, Min(0)] private int minMultiplierBasisPoints = 2500;
        [SerializeField, Min(0)] private int smoothingBasisPoints;
        [SerializeField] private bool allowFixedFallback = true;

        public MarketPriceFormationKind PolicyKind => policyKind;
        public int ScarcityStepBasisPoints => Math.Max(1, scarcityStepBasisPoints);
        public int MaxMultiplierBasisPoints => Math.Max(1, maxMultiplierBasisPoints);
        public int MinMultiplierBasisPoints => Math.Max(0, minMultiplierBasisPoints);
        public int SmoothingBasisPoints => Math.Clamp(smoothingBasisPoints, 0, 10000);
        public bool AllowFixedFallback => allowFixedFallback;

        public MarketPriceFormationPolicyData Clone()
        {
            return new MarketPriceFormationPolicyData
            {
                policyKind = policyKind,
                scarcityStepBasisPoints = scarcityStepBasisPoints,
                maxMultiplierBasisPoints = maxMultiplierBasisPoints,
                minMultiplierBasisPoints = minMultiplierBasisPoints,
                smoothingBasisPoints = smoothingBasisPoints,
                allowFixedFallback = allowFixedFallback
            };
        }

        public void Validate(string label, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (!Enum.IsDefined(typeof(MarketPriceFormationKind), policyKind) || policyKind == MarketPriceFormationKind.Unknown)
            {
                report.AddError($"{label} must declare a concrete price-formation policy.");
            }

            if (scarcityStepBasisPoints <= 0 || maxMultiplierBasisPoints <= 0 || minMultiplierBasisPoints < 0 || minMultiplierBasisPoints > maxMultiplierBasisPoints)
            {
                report.AddError($"{label} has invalid multiplier bounds.");
            }

            if (smoothingBasisPoints < 0 || smoothingBasisPoints > 10000)
            {
                report.AddError($"{label} smoothing must be within 0..10000 basis points.");
            }
        }
    }

    [Serializable]
    public sealed class MarketUpdatePolicyData
    {
        [SerializeField] private MarketUpdatePolicyKind policyKind = MarketUpdatePolicyKind.ExplicitWorldTimeBoundary;
        [SerializeField, Min(0f)] private double minimumWorldTimeInterval;
        [SerializeField, Min(1)] private int maxSubjectsPerUpdate = 64;

        public MarketUpdatePolicyKind PolicyKind => policyKind;
        public double MinimumWorldTimeInterval => Math.Max(0d, minimumWorldTimeInterval);
        public int MaxSubjectsPerUpdate => Math.Max(1, maxSubjectsPerUpdate);

        public MarketUpdatePolicyData Clone()
        {
            return new MarketUpdatePolicyData
            {
                policyKind = policyKind,
                minimumWorldTimeInterval = minimumWorldTimeInterval,
                maxSubjectsPerUpdate = maxSubjectsPerUpdate
            };
        }

        public void Validate(string label, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (!Enum.IsDefined(typeof(MarketUpdatePolicyKind), policyKind) || policyKind == MarketUpdatePolicyKind.Unknown)
            {
                report.AddError($"{label} must declare a concrete update policy.");
            }

            if (double.IsNaN(minimumWorldTimeInterval) || double.IsInfinity(minimumWorldTimeInterval) || minimumWorldTimeInterval < 0d)
            {
                report.AddError($"{label} minimum update interval is invalid.");
            }

            if (maxSubjectsPerUpdate <= 0)
            {
                report.AddError($"{label} max subjects per update must be positive.");
            }
        }
    }

    [Serializable]
    public sealed class MerchantMarginPolicyData
    {
        [SerializeField, Min(0)] private int buyDiscountBasisPoints = 2500;
        [SerializeField, Min(0)] private int sellMarkupBasisPoints = 2500;
        [SerializeField, Min(0)] private int minimumMarginBasisPoints = 500;
        [SerializeField, Min(0)] private int maxMarginBasisPoints = 10000;
        [SerializeField] private bool allowFixedPriceOverride = true;

        public int BuyDiscountBasisPoints => Math.Clamp(buyDiscountBasisPoints, 0, Math.Max(0, maxMarginBasisPoints));
        public int SellMarkupBasisPoints => Math.Clamp(sellMarkupBasisPoints, 0, Math.Max(0, maxMarginBasisPoints));
        public int MinimumMarginBasisPoints => Math.Max(0, minimumMarginBasisPoints);
        public int MaxMarginBasisPoints => Math.Max(MinimumMarginBasisPoints, maxMarginBasisPoints);
        public bool AllowFixedPriceOverride => allowFixedPriceOverride;

        public MerchantMarginPolicyData Clone()
        {
            return new MerchantMarginPolicyData
            {
                buyDiscountBasisPoints = buyDiscountBasisPoints,
                sellMarkupBasisPoints = sellMarkupBasisPoints,
                minimumMarginBasisPoints = minimumMarginBasisPoints,
                maxMarginBasisPoints = maxMarginBasisPoints,
                allowFixedPriceOverride = allowFixedPriceOverride
            };
        }

        public void Validate(string label, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (buyDiscountBasisPoints < 0 || sellMarkupBasisPoints < 0 || minimumMarginBasisPoints < 0 || maxMarginBasisPoints < minimumMarginBasisPoints)
            {
                report.AddError($"{label} has invalid merchant margin bounds.");
            }
        }
    }

    [CreateAssetMenu(fileName = "NewMarketDefinition", menuName = "Unity Isekai Game/Economy/Market Definition")]
    public class MarketDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string marketDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private MarketCategory category = MarketCategory.LocalSettlement;
        [SerializeField] private CurrencyDefinition currency;
        [SerializeField] private MarketScopeType scopeType = MarketScopeType.Settlement;
        [SerializeField] private MarketSubjectKind[] supportedSubjectKinds = { MarketSubjectKind.ItemDefinition, MarketSubjectKind.MaterialDefinition, MarketSubjectKind.Custom };
        [SerializeField] private MarketPriceFormationPolicyData priceFormationPolicy = new MarketPriceFormationPolicyData();
        [SerializeField] private MarketUpdatePolicyData updatePolicy = new MarketUpdatePolicyData();
        [SerializeField] private MerchantMarginPolicyData defaultMerchantMarginPolicy = new MerchantMarginPolicyData();
        [SerializeField] private string accessPolicyId;
        [SerializeField, Min(1)] private int version = 1;

        public string Id => marketDefinitionId;
        public string DisplayName => displayName;
        public string Description => description;
        public MarketCategory Category => category;
        public CurrencyDefinition Currency => currency;
        public string CurrencyId => currency == null ? string.Empty : currency.Id;
        public MarketScopeType ScopeType => scopeType;
        public IReadOnlyList<MarketSubjectKind> SupportedSubjectKinds => supportedSubjectKinds ?? Array.Empty<MarketSubjectKind>();
        public MarketPriceFormationPolicyData PriceFormationPolicy => priceFormationPolicy ?? new MarketPriceFormationPolicyData();
        public MarketUpdatePolicyData UpdatePolicy => updatePolicy ?? new MarketUpdatePolicyData();
        public MerchantMarginPolicyData DefaultMerchantMarginPolicy => defaultMerchantMarginPolicy ?? new MerchantMarginPolicyData();
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void Initialize(string id, string display, CurrencyDefinition marketCurrency, MarketCategory marketCategory = MarketCategory.LocalSettlement, MarketScopeType marketScope = MarketScopeType.Settlement, IEnumerable<MarketSubjectKind> subjectKinds = null)
        {
            marketDefinitionId = id ?? string.Empty;
            displayName = display ?? id ?? string.Empty;
            currency = marketCurrency;
            category = marketCategory;
            scopeType = marketScope;
            supportedSubjectKinds = (subjectKinds ?? new[] { MarketSubjectKind.ItemDefinition, MarketSubjectKind.MaterialDefinition, MarketSubjectKind.Custom }).Distinct().ToArray();
            priceFormationPolicy ??= new MarketPriceFormationPolicyData();
            updatePolicy ??= new MarketUpdatePolicyData();
            defaultMerchantMarginPolicy ??= new MerchantMarginPolicyData();
            version = Math.Max(1, version);
        }

        private void OnValidate()
        {
            priceFormationPolicy ??= new MarketPriceFormationPolicyData();
            updatePolicy ??= new MarketUpdatePolicyData();
            defaultMerchantMarginPolicy ??= new MerchantMarginPolicyData();
            supportedSubjectKinds ??= Array.Empty<MarketSubjectKind>();
            version = Math.Max(1, version);
        }

        public bool Supports(MarketSubjectKind subjectKind) => SupportedSubjectKinds.Contains(subjectKind);

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(marketDefinitionId))
            {
                report.AddError($"Market definition '{name}' is missing an ID.");
            }

            if (!Enum.IsDefined(typeof(MarketCategory), category) || category == MarketCategory.Unknown)
            {
                report.AddError($"Market definition '{DisplayName}' must declare a concrete category.");
            }

            if (!Enum.IsDefined(typeof(MarketScopeType), scopeType) || scopeType == MarketScopeType.Unknown)
            {
                report.AddError($"Market definition '{DisplayName}' must declare a concrete scope type.");
            }

            if (currency == null || string.IsNullOrWhiteSpace(CurrencyId) || definitionsById == null || !definitionsById.TryGetValue(CurrencyId, out IGameDefinition foundCurrency) || foundCurrency is not CurrencyDefinition)
            {
                report.AddError($"Market definition '{DisplayName}' references missing currency '{CurrencyId}'.");
            }

            if (SupportedSubjectKinds.Count == 0)
            {
                report.AddError($"Market definition '{DisplayName}' must support at least one traded-subject kind.");
            }

            foreach (MarketSubjectKind subjectKind in SupportedSubjectKinds)
            {
                if (!Enum.IsDefined(typeof(MarketSubjectKind), subjectKind) || subjectKind == MarketSubjectKind.Unknown)
                {
                    report.AddError($"Market definition '{DisplayName}' has an invalid supported subject kind.");
                }
            }

            PriceFormationPolicy.Validate($"Market definition '{DisplayName}' price policy", report);
            UpdatePolicy.Validate($"Market definition '{DisplayName}' update policy", report);
            DefaultMerchantMarginPolicy.Validate($"Market definition '{DisplayName}' margin policy", report);
        }
    }

    [CreateAssetMenu(fileName = "NewMarketSubjectDefinition", menuName = "Unity Isekai Game/Economy/Market Subject Definition")]
    public class MarketSubjectDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string marketSubjectId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private MarketSubjectKind subjectKind = MarketSubjectKind.ItemDefinition;
        [SerializeField] private string referencedDefinitionId;
        [SerializeField] private MarketQuantityUnit standardUnit = MarketQuantityUnit.Each;
        [SerializeField, Min(1)] private long standardQuantity = 1L;
        [SerializeField] private CurrencyDefinition currency;
        [SerializeField, Min(0)] private long baselinePriceUnits = 1L;
        [SerializeField, Min(0)] private long minimumPriceUnits = 1L;
        [SerializeField, Min(0)] private long maximumPriceUnits;
        [SerializeField, Min(0)] private int regionalModifierBasisPoints = 10000;
        [SerializeField, Min(0)] private int rarityModifierBasisPoints = 10000;
        [SerializeField] private string accessPolicyId;
        [SerializeField, Min(1)] private int version = 1;

        public string Id => marketSubjectId;
        public string DisplayName => displayName;
        public string Description => description;
        public MarketSubjectKind SubjectKind => subjectKind;
        public string ReferencedDefinitionId => referencedDefinitionId ?? string.Empty;
        public MarketQuantityUnit StandardUnit => standardUnit;
        public long StandardQuantity => Math.Max(1L, standardQuantity);
        public CurrencyDefinition Currency => currency;
        public string CurrencyId => currency == null ? string.Empty : currency.Id;
        public long BaselinePriceUnits => Math.Max(0L, baselinePriceUnits);
        public long MinimumPriceUnits => Math.Max(0L, minimumPriceUnits);
        public long MaximumPriceUnits => Math.Max(0L, maximumPriceUnits);
        public int RegionalModifierBasisPoints => Math.Max(0, regionalModifierBasisPoints);
        public int RarityModifierBasisPoints => Math.Max(0, rarityModifierBasisPoints);
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void Initialize(string id, string display, MarketSubjectKind kind, string referencedId, CurrencyDefinition subjectCurrency, long baselinePrice, MarketQuantityUnit unit = MarketQuantityUnit.Each, long standardQty = 1L)
        {
            marketSubjectId = id ?? string.Empty;
            displayName = display ?? id ?? string.Empty;
            subjectKind = kind;
            referencedDefinitionId = referencedId ?? string.Empty;
            currency = subjectCurrency;
            baselinePriceUnits = Math.Max(0L, baselinePrice);
            minimumPriceUnits = Math.Max(1L, Math.Min(Math.Max(1L, baselinePrice), minimumPriceUnits <= 0L ? Math.Max(1L, baselinePrice) : minimumPriceUnits));
            standardUnit = unit;
            standardQuantity = Math.Max(1L, standardQty);
            version = Math.Max(1, version);
        }

        private void OnValidate()
        {
            standardQuantity = Math.Max(1L, standardQuantity);
            baselinePriceUnits = Math.Max(0L, baselinePriceUnits);
            minimumPriceUnits = Math.Max(0L, minimumPriceUnits);
            maximumPriceUnits = Math.Max(0L, maximumPriceUnits);
            regionalModifierBasisPoints = Math.Max(0, regionalModifierBasisPoints);
            rarityModifierBasisPoints = Math.Max(0, rarityModifierBasisPoints);
            version = Math.Max(1, version);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(marketSubjectId))
            {
                report.AddError($"Market subject definition '{name}' is missing an ID.");
            }

            if (!Enum.IsDefined(typeof(MarketSubjectKind), subjectKind) || subjectKind == MarketSubjectKind.Unknown)
            {
                report.AddError($"Market subject '{DisplayName}' must declare a concrete subject kind.");
            }

            if (!Enum.IsDefined(typeof(MarketQuantityUnit), standardUnit) || standardUnit == MarketQuantityUnit.Unknown)
            {
                report.AddError($"Market subject '{DisplayName}' must declare a concrete quantity unit.");
            }

            if (currency == null || string.IsNullOrWhiteSpace(CurrencyId) || definitionsById == null || !definitionsById.TryGetValue(CurrencyId, out IGameDefinition foundCurrency) || foundCurrency is not CurrencyDefinition)
            {
                report.AddError($"Market subject '{DisplayName}' references missing currency '{CurrencyId}'.");
            }

            if (standardQuantity <= 0L)
            {
                report.AddError($"Market subject '{DisplayName}' standard quantity must be positive.");
            }

            if (baselinePriceUnits < 0L || minimumPriceUnits < 0L || maximumPriceUnits < 0L || maximumPriceUnits > 0L && minimumPriceUnits > maximumPriceUnits)
            {
                report.AddError($"Market subject '{DisplayName}' has invalid price bounds.");
            }

            ValidateReference(definitionsById, report);
        }

        private void ValidateReference(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (subjectKind == MarketSubjectKind.Custom || subjectKind == MarketSubjectKind.ItemCategory || subjectKind == MarketSubjectKind.LaborCategoryFoundation || subjectKind == MarketSubjectKind.PropertyCategoryFoundation)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(referencedDefinitionId) || definitionsById == null || !definitionsById.TryGetValue(referencedDefinitionId, out IGameDefinition found))
            {
                report.AddError($"Market subject '{DisplayName}' references missing definition '{referencedDefinitionId}'.");
                return;
            }

            if (subjectKind == MarketSubjectKind.ItemDefinition && found is not ItemDefinition)
            {
                report.AddError($"Market subject '{DisplayName}' must reference an ItemDefinition.");
            }

            if (subjectKind == MarketSubjectKind.MaterialDefinition && found is not MaterialDefinition)
            {
                report.AddError($"Market subject '{DisplayName}' must reference a MaterialDefinition.");
            }
        }
    }
}
