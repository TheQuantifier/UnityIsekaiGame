using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Economy.Trading
{
    [CreateAssetMenu(fileName = "NewTradePolicyDefinition", menuName = "Unity Isekai Game/Economy/Trade Policy Definition")]
    public sealed class TradePolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string tradePolicyId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private TradePolicyCategory category = TradePolicyCategory.DirectPersonToPerson;
        [SerializeField] private TradeParticipantKind[] allowedParticipantKinds = { TradeParticipantKind.Person, TradeParticipantKind.Organization, TradeParticipantKind.AuthorizedRepresentative };
        [SerializeField] private TradeAssetKind[] allowedAssetKinds = { TradeAssetKind.ItemInstance, TradeAssetKind.StackQuantity, TradeAssetKind.Money };
        [SerializeField] private bool buyingPermitted = true;
        [SerializeField] private bool sellingPermitted = true;
        [SerializeField] private bool barterPermitted = true;
        [SerializeField] private bool mixedMoneyAndItemTradesPermitted = true;
        [SerializeField] private bool partialStackQuantitiesPermitted = true;
        [SerializeField] private CurrencyDefinition[] allowedCurrencies = Array.Empty<CurrencyDefinition>();
        [SerializeField, Min(2)] private int maximumParticipants = 2;
        [SerializeField, Min(1)] private int maximumOfferRounds = 8;
        [SerializeField, Min(0f)] private double defaultOfferDuration = 60d;
        [SerializeField] private TradeReservationPolicyKind reservationPolicy = TradeReservationPolicyKind.ReserveOnAccept;
        [SerializeField] private TradeValuationPolicyKind valuationPolicy = TradeValuationPolicyKind.ParticipantRelative;
        [SerializeField] private bool quoteRequired;
        [SerializeField] private bool approvalRequired;
        [SerializeField] private string accessPolicyId;
        [SerializeField, Min(1)] private int version = 1;

        public string Id => tradePolicyId;
        public string DisplayName => displayName;
        public string Description => description;
        public TradePolicyCategory Category => category;
        public IReadOnlyList<TradeParticipantKind> AllowedParticipantKinds => allowedParticipantKinds ?? Array.Empty<TradeParticipantKind>();
        public IReadOnlyList<TradeAssetKind> AllowedAssetKinds => allowedAssetKinds ?? Array.Empty<TradeAssetKind>();
        public bool BuyingPermitted => buyingPermitted;
        public bool SellingPermitted => sellingPermitted;
        public bool BarterPermitted => barterPermitted;
        public bool MixedMoneyAndItemTradesPermitted => mixedMoneyAndItemTradesPermitted;
        public bool PartialStackQuantitiesPermitted => partialStackQuantitiesPermitted;
        public IReadOnlyList<CurrencyDefinition> AllowedCurrencies => allowedCurrencies ?? Array.Empty<CurrencyDefinition>();
        public IReadOnlyList<string> AllowedCurrencyIds => AllowedCurrencies.Where(currency => currency != null).Select(currency => currency.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        public int MaximumParticipants => Math.Max(2, maximumParticipants);
        public int MaximumOfferRounds => Math.Max(1, maximumOfferRounds);
        public double DefaultOfferDuration => Math.Max(0d, defaultOfferDuration);
        public TradeReservationPolicyKind ReservationPolicy => reservationPolicy;
        public TradeValuationPolicyKind ValuationPolicy => valuationPolicy;
        public bool QuoteRequired => quoteRequired;
        public bool ApprovalRequired => approvalRequired;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void Initialize(string id, string display, TradePolicyCategory policyCategory, IEnumerable<TradeAssetKind> assetKinds = null)
        {
            tradePolicyId = id ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(display) ? id ?? string.Empty : display;
            category = policyCategory;
            allowedAssetKinds = (assetKinds ?? new[] { TradeAssetKind.ItemInstance, TradeAssetKind.StackQuantity, TradeAssetKind.Money }).Distinct().ToArray();
            allowedParticipantKinds ??= new[] { TradeParticipantKind.Person, TradeParticipantKind.Organization, TradeParticipantKind.AuthorizedRepresentative };
            version = Math.Max(1, version);
        }

        private void OnValidate()
        {
            allowedParticipantKinds ??= Array.Empty<TradeParticipantKind>();
            allowedAssetKinds ??= Array.Empty<TradeAssetKind>();
            allowedCurrencies ??= Array.Empty<CurrencyDefinition>();
            maximumParticipants = Math.Max(2, maximumParticipants);
            maximumOfferRounds = Math.Max(1, maximumOfferRounds);
            defaultOfferDuration = Math.Max(0d, defaultOfferDuration);
            version = Math.Max(1, version);
        }

        public bool AllowsAssetKind(TradeAssetKind kind) => AllowedAssetKinds.Contains(kind);

        public bool AllowsCurrency(string currencyId)
        {
            IReadOnlyList<string> ids = AllowedCurrencyIds;
            return ids.Count == 0 || ids.Contains(currencyId ?? string.Empty, StringComparer.Ordinal);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(tradePolicyId))
            {
                report.AddError($"Trade Policy '{name}' is missing an ID.");
            }

            if (!Enum.IsDefined(typeof(TradePolicyCategory), category) || category == TradePolicyCategory.Unknown)
            {
                report.AddError($"Trade Policy '{DisplayName}' must declare a concrete category.");
            }

            if (AllowedParticipantKinds.Count == 0)
            {
                report.AddError($"Trade Policy '{DisplayName}' must allow at least one participant kind.");
            }

            foreach (TradeParticipantKind kind in AllowedParticipantKinds)
            {
                if (!Enum.IsDefined(typeof(TradeParticipantKind), kind) || kind == TradeParticipantKind.Unknown)
                {
                    report.AddError($"Trade Policy '{DisplayName}' has an invalid participant kind.");
                }
            }

            if (AllowedAssetKinds.Count == 0)
            {
                report.AddError($"Trade Policy '{DisplayName}' must allow at least one asset kind.");
            }

            foreach (TradeAssetKind kind in AllowedAssetKinds)
            {
                if (!Enum.IsDefined(typeof(TradeAssetKind), kind) || kind == TradeAssetKind.Unknown)
                {
                    report.AddError($"Trade Policy '{DisplayName}' has an invalid asset kind.");
                }
            }

            if (!barterPermitted && !buyingPermitted && !sellingPermitted)
            {
                report.AddError($"Trade Policy '{DisplayName}' permits no trade direction.");
            }

            if (!Enum.IsDefined(typeof(TradeReservationPolicyKind), reservationPolicy) || reservationPolicy == TradeReservationPolicyKind.Unknown)
            {
                report.AddError($"Trade Policy '{DisplayName}' must declare a concrete reservation policy.");
            }

            if (!Enum.IsDefined(typeof(TradeValuationPolicyKind), valuationPolicy) || valuationPolicy == TradeValuationPolicyKind.Unknown)
            {
                report.AddError($"Trade Policy '{DisplayName}' must declare a concrete valuation policy.");
            }

            if (maximumParticipants < 2)
            {
                report.AddError($"Trade Policy '{DisplayName}' must allow at least two participants.");
            }

            if (maximumOfferRounds <= 0)
            {
                report.AddError($"Trade Policy '{DisplayName}' maximum offer rounds must be positive.");
            }

            if (double.IsNaN(defaultOfferDuration) || double.IsInfinity(defaultOfferDuration) || defaultOfferDuration < 0d)
            {
                report.AddError($"Trade Policy '{DisplayName}' offer duration is invalid.");
            }

            foreach (CurrencyDefinition currency in AllowedCurrencies)
            {
                if (currency == null || string.IsNullOrWhiteSpace(currency.Id) || definitionsById == null || !definitionsById.TryGetValue(currency.Id, out IGameDefinition found) || found is not CurrencyDefinition)
                {
                    report.AddError($"Trade Policy '{DisplayName}' references missing currency '{currency?.Id ?? string.Empty}'.");
                }
            }
        }
    }
}
