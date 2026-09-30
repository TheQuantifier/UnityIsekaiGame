using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Economy.Markets
{
    public sealed class MarketRuntime
    {
        private readonly Dictionary<string, MarketInstanceData> marketsById = new Dictionary<string, MarketInstanceData>(StringComparer.Ordinal);
        private readonly Dictionary<string, MarketObservationRecordData> supplyById = new Dictionary<string, MarketObservationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, MarketObservationRecordData> demandById = new Dictionary<string, MarketObservationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, MarketScarcityData> scarcityById = new Dictionary<string, MarketScarcityData>(StringComparer.Ordinal);
        private readonly Dictionary<string, MarketPriceRecordData> pricesById = new Dictionary<string, MarketPriceRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, MerchantQuoteRecordData> quotesById = new Dictionary<string, MerchantQuoteRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, MarketTransactionObservationData> transactionObservationsById = new Dictionary<string, MarketTransactionObservationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> currentPriceByMarketSubject = new Dictionary<string, string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string worldId;

        public long Revision { get; private set; }
        public int MarketCount => marketsById.Count;
        public int SupplyCount => supplyById.Count;
        public int DemandCount => demandById.Count;
        public int PriceCount => pricesById.Count;
        public int QuoteCount => quotesById.Count;

        public IReadOnlyList<MarketInstanceData> Markets => marketsById.Values.OrderBy(item => item.marketInstanceId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<MarketObservationRecordData> SupplyRecords => supplyById.Values.OrderBy(item => item.observationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<MarketObservationRecordData> DemandRecords => demandById.Values.OrderBy(item => item.observationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<MarketPriceRecordData> PriceHistory => pricesById.Values.OrderBy(item => item.createdWorldTime).ThenBy(item => item.marketPriceId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<MerchantQuoteRecordData> Quotes => quotesById.Values.OrderBy(item => item.quoteId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public int PruneExpiredSimulationHistory(double cutoffWorldTime)
        {
            cutoffWorldTime = Math.Max(0d, cutoffWorldTime);
            string latestCheckpointId = demandById.Values
                .Where(record => string.Equals(record.provenance, "prototype-market-boundary-complete", StringComparison.Ordinal))
                .OrderByDescending(record => record.observedWorldTime)
                .ThenByDescending(record => record.observationId, StringComparer.Ordinal)
                .Select(record => record.observationId)
                .FirstOrDefault();
            int removed = RemoveWhere(supplyById, record => record.expiresWorldTime >= 0d && record.expiresWorldTime < cutoffWorldTime);
            removed += RemoveWhere(demandById, record => record.expiresWorldTime >= 0d
                && record.expiresWorldTime < cutoffWorldTime
                && !string.Equals(record.observationId, latestCheckpointId, StringComparison.Ordinal));

            HashSet<string> retainedPriceIds = currentPriceByMarketSubject.Values.ToHashSet(StringComparer.Ordinal);
            removed += RemoveWhere(pricesById, record => record.createdWorldTime < cutoffWorldTime && !retainedPriceIds.Contains(record.marketPriceId));
            HashSet<string> retainedScarcityIds = pricesById.Values
                .Where(record => !string.IsNullOrWhiteSpace(record.scarcityId))
                .Select(record => record.scarcityId)
                .ToHashSet(StringComparer.Ordinal);
            removed += RemoveWhere(scarcityById, record => record.evaluatedWorldTime < cutoffWorldTime && !retainedScarcityIds.Contains(record.scarcityId));
            if (removed > 0)
            {
                Revision++;
            }
            return removed;
        }

        private static int RemoveWhere<T>(Dictionary<string, T> values, Func<T, bool> predicate)
        {
            string[] ids = values.Where(pair => predicate(pair.Value)).Select(pair => pair.Key).ToArray();
            foreach (string id in ids) values.Remove(id);
            return ids.Length;
        }

        public void Configure(DefinitionRegistry definitionRegistry, string world)
        {
            registry = definitionRegistry ?? registry;
            worldId = world ?? string.Empty;
        }

        public MarketOperationResult CreateMarketInstance(MarketDefinition definition, string marketInstanceId, string regionId, string organizationId = "", string settlementId = "", string stationId = "", string customScopeId = "", bool preview = false)
        {
            long before = Revision;
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return Fail(MarketResultCode.MissingDefinition, "Market definition is required.", preview);
            }

            if (!ValidateDefinition(definition.Id, out MarketDefinition resolved, out string failure) || resolved != definition && resolved == null)
            {
                return Fail(MarketResultCode.MissingDefinition, failure, preview);
            }

            if (string.IsNullOrWhiteSpace(marketInstanceId))
            {
                return Fail(MarketResultCode.InvalidRequest, "Market instance ID is required.", preview);
            }

            if (marketsById.ContainsKey(marketInstanceId))
            {
                return Fail(MarketResultCode.InvalidRequest, $"Market instance '{marketInstanceId}' already exists.", preview);
            }

            MarketInstanceData market = new MarketInstanceData
            {
                marketInstanceId = marketInstanceId.Trim(),
                marketDefinitionId = definition.Id,
                regionId = regionId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                settlementId = settlementId ?? string.Empty,
                stationId = stationId ?? string.Empty,
                customScopeId = customScopeId ?? string.Empty,
                currencyId = definition.CurrencyId,
                regionalModifierBasisPoints = 10000,
                accessPolicyId = definition.AccessPolicyId,
                provenance = $"definition:{definition.Id}",
                revision = 1L
            };

            if (!ValidateMarket(market, registry, out failure))
            {
                return Fail(MarketResultCode.ValidationFailed, failure, preview);
            }

            if (preview)
            {
                return MarketOperationResult.Success("Market instance preview succeeded.", before, before, preview: true, market: market);
            }

            marketsById.Add(market.marketInstanceId, market);
            Revision++;
            return MarketOperationResult.Success("Market instance created.", before, Revision, market: market);
        }

        public MarketOperationResult RecordSupply(MarketObservationRecordData supply, bool preview = false)
        {
            return RecordObservation(supply, supplyById, isSupply: true, preview);
        }

        public MarketOperationResult RecordDemand(MarketObservationRecordData demand, bool preview = false)
        {
            return RecordObservation(demand, demandById, isSupply: false, preview);
        }

        public MarketOperationResult EvaluateScarcity(string scarcityId, string marketInstanceId, string marketSubjectId, double worldTime, bool preview = false)
        {
            long before = Revision;
            if (!TryGetMarketAndSubject(marketInstanceId, marketSubjectId, out MarketInstanceData market, out MarketSubjectDefinition subject, out MarketOperationResult failure, preview))
            {
                return failure;
            }

            IReadOnlyList<MarketObservationRecordData> supply = ActiveSupply(marketInstanceId, marketSubjectId, worldTime).ToArray();
            IReadOnlyList<MarketObservationRecordData> demand = ActiveDemand(marketInstanceId, marketSubjectId, worldTime).ToArray();
            MarketScarcityData scarcity = BuildScarcity(string.IsNullOrWhiteSpace(scarcityId) ? StableId("scarcity", marketInstanceId, marketSubjectId, worldTime) : scarcityId.Trim(), market, subject, supply, demand, worldTime);

            if (preview)
            {
                return MarketOperationResult.Success("Scarcity preview succeeded.", before, before, preview: true, scarcity: scarcity);
            }

            if (scarcityById.ContainsKey(scarcity.scarcityId))
            {
                return MarketOperationResult.Success("Duplicate scarcity evaluation ignored.", before, before, duplicate: true, scarcity: scarcityById[scarcity.scarcityId]);
            }

            scarcityById.Add(scarcity.scarcityId, scarcity);
            Revision++;
            return MarketOperationResult.Success("Scarcity evaluated.", before, Revision, scarcity: scarcity);
        }

        public MarketOperationResult FormReferencePrice(string marketPriceId, string marketInstanceId, string marketSubjectId, double worldTime, bool preview = false, bool requireMarketData = false)
        {
            long before = Revision;
            if (!TryGetMarketAndSubject(marketInstanceId, marketSubjectId, out MarketInstanceData market, out MarketSubjectDefinition subject, out MarketOperationResult failure, preview))
            {
                return failure;
            }

            string priceId = string.IsNullOrWhiteSpace(marketPriceId) ? StableId("market-price", marketInstanceId, marketSubjectId, worldTime) : marketPriceId.Trim();
            if (!preview && pricesById.TryGetValue(priceId, out MarketPriceRecordData existing))
            {
                return MarketOperationResult.Success("Duplicate price formation ignored.", before, before, duplicate: true, price: existing);
            }

            MarketDefinition definition = registry.TryGet(market.marketDefinitionId, out MarketDefinition foundDefinition) ? foundDefinition : null;
            MarketOperationResult scarcityResult = EvaluateScarcity(StableId("scarcity", marketInstanceId, marketSubjectId, worldTime), marketInstanceId, marketSubjectId, worldTime, preview: true);
            if (!scarcityResult.Succeeded)
            {
                return scarcityResult;
            }

            MarketScarcityData scarcity = scarcityResult.Scarcity;
            bool insufficientData = scarcity.scarcityClass == MarketScarcityClass.Unknown;
            if (insufficientData && requireMarketData)
            {
                return Fail(MarketResultCode.InsufficientData, "Insufficient supply or demand data for market-derived price.", preview);
            }

            MarketPriceRecordData prior = TryGetCurrentPrice(marketInstanceId, marketSubjectId, out MarketPriceRecordData current) ? current : null;
            bool fixedFallback = insufficientData || definition == null || definition.PriceFormationPolicy.PolicyKind == MarketPriceFormationKind.FixedFallbackOnly;
            long amount = fixedFallback
                ? Math.Max(subject.MinimumPriceUnits, subject.BaselinePriceUnits)
                : CalculateReferencePrice(subject, definition.PriceFormationPolicy, market.regionalModifierBasisPoints, scarcity, prior);

            amount = ClampPrice(amount, subject);
            MarketPriceRecordData price = new MarketPriceRecordData
            {
                marketPriceId = priceId,
                marketInstanceId = marketInstanceId,
                marketSubjectId = marketSubjectId,
                currencyId = subject.CurrencyId,
                referenceAmountUnits = amount,
                quantityBasis = subject.StandardQuantity,
                unit = subject.StandardUnit,
                scarcityId = scarcity.scarcityId,
                scarcityClass = scarcity.scarcityClass,
                supplyAvailable = scarcity.availableSupply,
                demandCurrent = scarcity.currentDemand,
                priceFormationPolicy = fixedFallback ? MarketPriceFormationKind.FixedFallbackOnly : definition.PriceFormationPolicy.PolicyKind,
                fixedPriceFallback = fixedFallback,
                confidence = fixedFallback ? Math.Min(7000, scarcity.confidence) : scarcity.confidence,
                createdWorldTime = Math.Max(0d, worldTime),
                priorPriceId = prior?.marketPriceId ?? string.Empty,
                accessPolicyId = subject.AccessPolicyId,
                provenance = fixedFallback ? "fixed-price-fallback" : $"market-policy:{definition?.Id}",
                diagnostics = fixedFallback ? "Fixed-price fallback used." : scarcity.diagnostics,
                revision = 1L
            };

            if (!ValidatePrice(price, marketsById, SubjectIds(), out string validationFailure))
            {
                return Fail(MarketResultCode.ValidationFailed, validationFailure, preview);
            }

            if (preview)
            {
                return MarketOperationResult.Success("Reference price preview succeeded.", before, before, preview: true, scarcity: scarcity, price: price);
            }

            if (!scarcityById.ContainsKey(scarcity.scarcityId))
            {
                scarcityById.Add(scarcity.scarcityId, scarcity);
            }

            pricesById.Add(price.marketPriceId, price);
            currentPriceByMarketSubject[PriceKey(marketInstanceId, marketSubjectId)] = price.marketPriceId;
            market.lastUpdateWorldTime = Math.Max(market.lastUpdateWorldTime, worldTime);
            market.revision++;
            Revision++;
            return MarketOperationResult.Success("Reference price formed.", before, Revision, market: market, scarcity: scarcity, price: price);
        }

        public MarketOperationResult UpdateMarketSubject(string marketInstanceId, string marketSubjectId, double worldTime, bool preview = false)
        {
            if (TryGetCurrentPrice(marketInstanceId, marketSubjectId, out MarketPriceRecordData current)
                && Math.Abs(current.createdWorldTime - worldTime) < 0.0001d)
            {
                return MarketOperationResult.Success("Market update already evaluated for this world-time boundary.", Revision, Revision, duplicate: true, price: current);
            }

            return FormReferencePrice(StableId("market-price", marketInstanceId, marketSubjectId, worldTime), marketInstanceId, marketSubjectId, worldTime, preview);
        }

        public MarketOperationResult CreateMerchantQuote(
            string quoteId,
            string merchantId,
            string marketInstanceId,
            string marketSubjectId,
            MerchantQuoteDirection direction,
            long quantity,
            double worldTime,
            double expiresWorldTime,
            string itemInstanceId = "",
            ItemInstanceSnapshot item = null,
            bool privilegedHiddenFactors = false,
            long expectedMarketRevision = -1L,
            long expectedPriceRevision = -1L,
            bool fixedPriceOverride = false,
            bool preview = false,
            ItemQualitySnapshot quality = null,
            ItemDurabilitySnapshot durability = null)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(quoteId))
            {
                return Fail(MarketResultCode.InvalidRequest, "Quote ID is required.", preview);
            }

            if (!Enum.IsDefined(typeof(MerchantQuoteDirection), direction) || direction == MerchantQuoteDirection.Unknown)
            {
                return Fail(MarketResultCode.InvalidRequest, "Quote direction is required.", preview);
            }

            if (quantity <= 0L)
            {
                return Fail(MarketResultCode.InvalidQuantity, "Quote quantity must be positive.", preview);
            }

            if (expiresWorldTime >= 0d && expiresWorldTime < worldTime)
            {
                return Fail(MarketResultCode.Expired, "Quote expiration cannot be before creation.", preview);
            }

            if (!TryGetMarketAndSubject(marketInstanceId, marketSubjectId, out MarketInstanceData market, out MarketSubjectDefinition subject, out MarketOperationResult failure, preview))
            {
                return failure;
            }

            if (expectedMarketRevision >= 0L && market.revision != expectedMarketRevision)
            {
                return Fail(MarketResultCode.StaleRevision, $"Market revision {market.revision} does not match expected {expectedMarketRevision}.", preview);
            }

            MarketPriceRecordData price = null;
            if (!fixedPriceOverride && !TryGetCurrentPrice(marketInstanceId, marketSubjectId, out price))
            {
                return Fail(MarketResultCode.MissingPrice, "No current market price exists for quote creation.", preview);
            }

            price ??= new MarketPriceRecordData
            {
                marketPriceId = StableId("fixed-quote-price", marketInstanceId, marketSubjectId, worldTime),
                marketInstanceId = marketInstanceId,
                marketSubjectId = marketSubjectId,
                currencyId = subject.CurrencyId,
                referenceAmountUnits = Math.Max(subject.MinimumPriceUnits, subject.BaselinePriceUnits),
                quantityBasis = subject.StandardQuantity,
                unit = subject.StandardUnit,
                fixedPriceFallback = true,
                confidence = 6500,
                revision = 1L
            };

            if (expectedPriceRevision >= 0L && price.revision != expectedPriceRevision)
            {
                return Fail(MarketResultCode.StaleRevision, $"Price revision {price.revision} does not match expected {expectedPriceRevision}.", preview);
            }

            MarketDefinition definition = registry.TryGet(market.marketDefinitionId, out MarketDefinition foundDefinition) ? foundDefinition : null;
            MerchantMarginPolicyData margin = definition?.DefaultMerchantMarginPolicy ?? new MerchantMarginPolicyData();
            int marginBasisPoints = direction == MerchantQuoteDirection.MerchantBuys ? margin.BuyDiscountBasisPoints : margin.SellMarkupBasisPoints;
            long amount = ScaleForQuantity(price.referenceAmountUnits, price.quantityBasis, quantity);
            amount = ApplyItemAdjustments(amount, item, quality, durability, subject, privilegedHiddenFactors, out int qualityBps, out int durabilityBps, out int rarityBps, out bool hiddenApplied);
            amount = direction == MerchantQuoteDirection.MerchantBuys
                ? MultiplyBasisPoints(amount, Math.Max(0, 10000 - Math.Max(margin.MinimumMarginBasisPoints, marginBasisPoints)))
                : MultiplyBasisPoints(amount, 10000 + Math.Max(margin.MinimumMarginBasisPoints, marginBasisPoints));
            amount = Math.Max(1L, ClampPrice(amount, subject));

            MerchantQuoteRecordData quote = new MerchantQuoteRecordData
            {
                quoteId = quoteId.Trim(),
                merchantId = merchantId ?? string.Empty,
                marketPriceId = price.marketPriceId,
                marketInstanceId = marketInstanceId,
                marketSubjectId = marketSubjectId,
                itemInstanceId = string.IsNullOrWhiteSpace(itemInstanceId) ? item?.ItemInstanceId ?? string.Empty : itemInstanceId,
                direction = direction,
                currencyId = subject.CurrencyId,
                quantity = quantity,
                unit = subject.StandardUnit,
                referenceAmountUnits = price.referenceAmountUnits,
                finalAmountUnits = amount,
                marginBasisPoints = marginBasisPoints,
                qualityAdjustmentBasisPoints = qualityBps,
                durabilityAdjustmentBasisPoints = durabilityBps,
                rarityAdjustmentBasisPoints = rarityBps,
                hiddenFactorsApplied = hiddenApplied,
                fixedPriceOverride = fixedPriceOverride,
                createdWorldTime = Math.Max(0d, worldTime),
                expiresWorldTime = expiresWorldTime,
                marketRevision = market.revision,
                priceRevision = price.revision,
                accessPolicyId = subject.AccessPolicyId,
                provenance = "merchant-quote",
                diagnostics = hiddenApplied ? "Privileged hidden factors included." : "Ordinary known factors included.",
                revision = 1L
            };

            if (!ValidateQuote(quote, marketsById, SubjectIds(), PriceIds().Concat(new[] { price.marketPriceId }).ToHashSet(StringComparer.Ordinal), out string validationFailure))
            {
                return Fail(MarketResultCode.ValidationFailed, validationFailure, preview);
            }

            if (preview)
            {
                return MarketOperationResult.Success("Merchant quote preview succeeded.", before, before, preview: true, quote: quote);
            }

            if (quotesById.ContainsKey(quote.quoteId))
            {
                return MarketOperationResult.Success("Duplicate quote ignored.", before, before, duplicate: true, quote: quotesById[quote.quoteId]);
            }

            quotesById.Add(quote.quoteId, quote);
            Revision++;
            return MarketOperationResult.Success("Merchant quote created.", before, Revision, quote: quote);
        }

        public MarketOperationResult AddTransactionObservation(string observationId, EconomyTransactionSnapshot transaction, string marketInstanceId, string marketSubjectId, MarketTransactionObservationPolicy policy, bool publicObservation, double worldTime, bool preview = false)
        {
            long before = Revision;
            if (transaction == null)
            {
                return Fail(MarketResultCode.InvalidRequest, "Committed transaction snapshot is required.", preview);
            }

            bool isRefundOrReversal = transaction.Kind == EconomyTransactionKind.Refund || transaction.Kind == EconomyTransactionKind.Reversal || transaction.State == EconomyTransactionState.Refunded || transaction.State == EconomyTransactionState.Reversed;
            if (transaction.State != EconomyTransactionState.Committed && !isRefundOrReversal || transaction.State == EconomyTransactionState.Committed && !policy.HasFlag(MarketTransactionObservationPolicy.IncludeCommitted))
            {
                return Fail(MarketResultCode.InvalidRequest, "Transaction observation policy rejects this transaction.", preview);
            }

            if (isRefundOrReversal && !(policy.HasFlag(MarketTransactionObservationPolicy.IncludeRefunded) || policy.HasFlag(MarketTransactionObservationPolicy.IncludeReversed)))
            {
                return Fail(MarketResultCode.InvalidRequest, "Refunded or reversed transaction is excluded by policy.", preview);
            }

            if (!publicObservation && !policy.HasFlag(MarketTransactionObservationPolicy.IncludePrivate))
            {
                return Fail(MarketResultCode.AccessDenied, "Private transaction observation is excluded by policy.", preview);
            }

            if (!TryGetMarketAndSubject(marketInstanceId, marketSubjectId, out _, out MarketSubjectDefinition subject, out MarketOperationResult failure, preview))
            {
                return failure;
            }

            if (!string.Equals(transaction.CurrencyId, subject.CurrencyId, StringComparison.Ordinal))
            {
                return Fail(MarketResultCode.CurrencyMismatch, "Transaction currency does not match market subject currency.", preview);
            }

            MarketTransactionObservationData observation = new MarketTransactionObservationData
            {
                observationId = string.IsNullOrWhiteSpace(observationId) ? StableId("transaction-observation", transaction.TransactionId, marketInstanceId, worldTime) : observationId.Trim(),
                transactionId = transaction.TransactionId,
                marketInstanceId = marketInstanceId,
                marketSubjectId = marketSubjectId,
                currencyId = transaction.CurrencyId,
                paidUnits = transaction.Units,
                quantity = 1L,
                publicObservation = publicObservation,
                refundOrReversal = isRefundOrReversal,
                observedWorldTime = Math.Max(0d, worldTime),
                revision = 1L
            };

            if (preview)
            {
                return MarketOperationResult.Success("Transaction observation preview succeeded.", before, before, preview: true);
            }

            if (transactionObservationsById.ContainsKey(observation.observationId))
            {
                return MarketOperationResult.Success("Duplicate transaction observation ignored.", before, before, duplicate: true);
            }

            transactionObservationsById.Add(observation.observationId, observation);
            Revision++;
            return MarketOperationResult.Success("Transaction observation recorded.", before, Revision);
        }

        public bool TryGetMarket(string marketInstanceId, out MarketInstanceData market)
        {
            if (!string.IsNullOrWhiteSpace(marketInstanceId) && marketsById.TryGetValue(marketInstanceId, out MarketInstanceData found))
            {
                market = found.Clone();
                return true;
            }

            market = null;
            return false;
        }

        public bool TryGetCurrentPrice(string marketInstanceId, string marketSubjectId, out MarketPriceRecordData price)
        {
            if (currentPriceByMarketSubject.TryGetValue(PriceKey(marketInstanceId, marketSubjectId), out string priceId)
                && pricesById.TryGetValue(priceId, out MarketPriceRecordData found))
            {
                price = found.Clone();
                return true;
            }

            price = null;
            return false;
        }

        public bool TryGetQuote(string quoteId, out MerchantQuoteRecordData quote)
        {
            if (!string.IsNullOrWhiteSpace(quoteId) && quotesById.TryGetValue(quoteId, out MerchantQuoteRecordData found))
            {
                quote = found.Clone();
                return true;
            }

            quote = null;
            return false;
        }

        public bool ValidateQuoteForExecution(string quoteId, double worldTime, out string failure)
        {
            failure = string.Empty;
            if (!quotesById.TryGetValue(quoteId ?? string.Empty, out MerchantQuoteRecordData quote))
            {
                failure = $"Quote '{quoteId}' was not found.";
                return false;
            }

            if (quote.expiresWorldTime >= 0d && worldTime > quote.expiresWorldTime)
            {
                failure = $"Quote '{quoteId}' expired.";
                return false;
            }

            if (!marketsById.TryGetValue(quote.marketInstanceId, out MarketInstanceData market) || market.revision != quote.marketRevision)
            {
                failure = $"Quote '{quoteId}' has a stale market revision.";
                return false;
            }

            if (!pricesById.TryGetValue(quote.marketPriceId, out MarketPriceRecordData price) || price.revision != quote.priceRevision)
            {
                failure = $"Quote '{quoteId}' has a stale price revision.";
                return false;
            }

            return true;
        }

        public IReadOnlyList<MarketPriceRecordData> QueryPriceHistory(string marketInstanceId, string marketSubjectId, double fromWorldTime = 0d, double toWorldTime = double.MaxValue)
        {
            return pricesById.Values
                .Where(price => string.Equals(price.marketInstanceId, marketInstanceId, StringComparison.Ordinal)
                    && string.Equals(price.marketSubjectId, marketSubjectId, StringComparison.Ordinal)
                    && price.createdWorldTime >= fromWorldTime
                    && price.createdWorldTime <= toWorldTime)
                .OrderBy(price => price.createdWorldTime)
                .ThenBy(price => price.marketPriceId, StringComparer.Ordinal)
                .Select(price => price.Clone())
                .ToArray();
        }

        public MarketProjection<MarketPriceRecordData> GetPriceProjection(string marketPriceId, InformationAccessRuntime access, InformationAccessContext context, string policyId = "")
        {
            if (!pricesById.TryGetValue(marketPriceId ?? string.Empty, out MarketPriceRecordData price))
            {
                return new MarketProjection<MarketPriceRecordData>(null, null, false, true, Array.Empty<string>(), MarketInformationSubject.ProtectedFields, $"Market price '{marketPriceId}' was not found.");
            }

            return Project(price.Clone(), price.CreateInformationSubject(), access, context, policyId, data =>
            {
                data.supplyAvailable = 0L;
                data.demandCurrent = 0L;
                data.diagnostics = string.Empty;
                data.provenance = string.Empty;
            });
        }

        public MarketProjection<MerchantQuoteRecordData> GetQuoteProjection(string quoteId, InformationAccessRuntime access, InformationAccessContext context, string policyId = "")
        {
            if (!quotesById.TryGetValue(quoteId ?? string.Empty, out MerchantQuoteRecordData quote))
            {
                return new MarketProjection<MerchantQuoteRecordData>(null, null, false, true, Array.Empty<string>(), MarketInformationSubject.ProtectedFields, $"Quote '{quoteId}' was not found.");
            }

            return Project(quote.Clone(), quote.CreateInformationSubject(), access, context, policyId, data =>
            {
                data.merchantId = string.Empty;
                data.marginBasisPoints = 0;
                data.hiddenFactorsApplied = false;
                data.diagnostics = string.Empty;
            });
        }

        public MarketRuntimeSaveData CreateSaveData()
        {
            return new MarketRuntimeSaveData
            {
                worldId = worldId ?? string.Empty,
                revision = Revision,
                markets = marketsById.Values.OrderBy(item => item.marketInstanceId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                supplyRecords = supplyById.Values.OrderBy(item => item.observationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                demandRecords = demandById.Values.OrderBy(item => item.observationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                scarcityRecords = scarcityById.Values.OrderBy(item => item.scarcityId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                priceRecords = pricesById.Values.OrderBy(item => item.marketPriceId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                quotes = quotesById.Values.OrderBy(item => item.quoteId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                transactionObservations = transactionObservationsById.Values.OrderBy(item => item.observationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                currentPrices = currentPriceByMarketSubject
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair =>
                    {
                        string[] parts = pair.Key.Split('|');
                        return new MarketCurrentPriceReferenceData
                        {
                            marketInstanceId = parts.Length > 0 ? parts[0] : string.Empty,
                            marketSubjectId = parts.Length > 1 ? parts[1] : string.Empty,
                            marketPriceId = pair.Value
                        };
                    })
                    .ToList()
            };
        }

        public MarketOperationResult RestoreFromSaveData(MarketRuntimeSaveData saveData, DefinitionRegistry definitionRegistry)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, out string failure))
            {
                return Fail(MarketResultCode.ValidationFailed, failure, preview: false);
            }

            marketsById.Clear();
            supplyById.Clear();
            demandById.Clear();
            scarcityById.Clear();
            pricesById.Clear();
            quotesById.Clear();
            transactionObservationsById.Clear();
            currentPriceByMarketSubject.Clear();

            registry = definitionRegistry ?? registry;
            worldId = saveData.worldId ?? worldId ?? string.Empty;
            foreach (MarketInstanceData market in saveData.markets ?? new List<MarketInstanceData>())
            {
                marketsById.Add(market.marketInstanceId, market.Clone());
            }

            foreach (MarketObservationRecordData supply in saveData.supplyRecords ?? new List<MarketObservationRecordData>())
            {
                supplyById.Add(supply.observationId, supply.Clone());
            }

            foreach (MarketObservationRecordData demand in saveData.demandRecords ?? new List<MarketObservationRecordData>())
            {
                demandById.Add(demand.observationId, demand.Clone());
            }

            foreach (MarketScarcityData scarcity in saveData.scarcityRecords ?? new List<MarketScarcityData>())
            {
                scarcityById.Add(scarcity.scarcityId, scarcity.Clone());
            }

            foreach (MarketPriceRecordData price in saveData.priceRecords ?? new List<MarketPriceRecordData>())
            {
                pricesById.Add(price.marketPriceId, price.Clone());
            }

            foreach (MerchantQuoteRecordData quote in saveData.quotes ?? new List<MerchantQuoteRecordData>())
            {
                quotesById.Add(quote.quoteId, quote.Clone());
            }

            foreach (MarketTransactionObservationData observation in saveData.transactionObservations ?? new List<MarketTransactionObservationData>())
            {
                transactionObservationsById.Add(observation.observationId, observation.Clone());
            }

            foreach (MarketCurrentPriceReferenceData current in saveData.currentPrices ?? new List<MarketCurrentPriceReferenceData>())
            {
                currentPriceByMarketSubject[PriceKey(current.marketInstanceId, current.marketSubjectId)] = current.marketPriceId;
            }

            Revision = Math.Max(0L, saveData.revision);
            return MarketOperationResult.Success("Market runtime restored.", before, Revision);
        }

        public static bool ValidateSaveData(MarketRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Market save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != MarketRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported market schema version {saveData.schemaVersion}.";
                return false;
            }

            if (saveData.revision < 0L)
            {
                failure = "Market runtime revision cannot be negative.";
                return false;
            }

            Dictionary<string, MarketInstanceData> markets = new Dictionary<string, MarketInstanceData>(StringComparer.Ordinal);
            foreach (MarketInstanceData market in saveData.markets ?? new List<MarketInstanceData>())
            {
                if (!ValidateMarket(market, registry, out failure) || !markets.TryAdd(market.marketInstanceId, market))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate market instance '{market?.marketInstanceId}'." : failure;
                    return false;
                }
            }

            HashSet<string> subjects = registry?.DefinitionsById.Values.OfType<MarketSubjectDefinition>().Select(item => item.Id).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> supplyIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> supplySources = new HashSet<string>(StringComparer.Ordinal);
            foreach (MarketObservationRecordData supply in saveData.supplyRecords ?? new List<MarketObservationRecordData>())
            {
                if (!ValidateObservation(supply, markets, subjects, isSupply: true, out failure) || !supplyIds.Add(supply.observationId))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate supply observation '{supply?.observationId}'." : failure;
                    return false;
                }

                if (!supplySources.Add(supply.SourceKey))
                {
                    failure = $"Supply source '{supply.SourceKey}' is counted more than once.";
                    return false;
                }
            }

            HashSet<string> demandIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MarketObservationRecordData demand in saveData.demandRecords ?? new List<MarketObservationRecordData>())
            {
                if (!ValidateObservation(demand, markets, subjects, isSupply: false, out failure) || !demandIds.Add(demand.observationId))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate demand observation '{demand?.observationId}'." : failure;
                    return false;
                }
            }

            HashSet<string> scarcityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MarketScarcityData scarcity in saveData.scarcityRecords ?? new List<MarketScarcityData>())
            {
                if (!ValidateScarcity(scarcity, markets, subjects, out failure) || !scarcityIds.Add(scarcity.scarcityId))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate scarcity record '{scarcity?.scarcityId}'." : failure;
                    return false;
                }
            }

            HashSet<string> priceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MarketPriceRecordData price in saveData.priceRecords ?? new List<MarketPriceRecordData>())
            {
                if (!ValidatePrice(price, markets, subjects, out failure) || !priceIds.Add(price.marketPriceId))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate market price '{price?.marketPriceId}'." : failure;
                    return false;
                }
            }

            HashSet<string> quoteIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MerchantQuoteRecordData quote in saveData.quotes ?? new List<MerchantQuoteRecordData>())
            {
                if (!ValidateQuote(quote, markets, subjects, priceIds, out failure) || !quoteIds.Add(quote.quoteId))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate merchant quote '{quote?.quoteId}'." : failure;
                    return false;
                }
            }

            HashSet<string> observationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MarketTransactionObservationData observation in saveData.transactionObservations ?? new List<MarketTransactionObservationData>())
            {
                if (!ValidateTransactionObservation(observation, markets, subjects, out failure) || !observationIds.Add(observation.observationId))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate transaction observation '{observation?.observationId}'." : failure;
                    return false;
                }
            }

            foreach (MarketCurrentPriceReferenceData current in saveData.currentPrices ?? new List<MarketCurrentPriceReferenceData>())
            {
                if (!markets.ContainsKey(current.marketInstanceId ?? string.Empty) || !subjects.Contains(current.marketSubjectId ?? string.Empty) || !priceIds.Contains(current.marketPriceId ?? string.Empty))
                {
                    failure = $"Current market price reference '{current?.marketInstanceId}/{current?.marketSubjectId}' is invalid.";
                    return false;
                }
            }

            return true;
        }

        private MarketOperationResult RecordObservation(MarketObservationRecordData observation, Dictionary<string, MarketObservationRecordData> target, bool isSupply, bool preview)
        {
            long before = Revision;
            MarketObservationRecordData record = observation?.Clone();
            if (!ValidateObservation(record, marketsById, SubjectIds(), isSupply, out string failure))
            {
                return Fail(MarketResultCode.ValidationFailed, failure, preview);
            }

            if (target.TryGetValue(record.observationId, out MarketObservationRecordData existingObservation))
            {
                return SameObservation(existingObservation, record)
                    ? MarketOperationResult.Success("Market observation already recorded.", before, before, duplicate: true)
                    : Fail(MarketResultCode.InvalidRequest, $"Observation '{record.observationId}' already exists with different data.", preview);
            }

            if (isSupply && supplyById.Values.Any(existing => string.Equals(existing.SourceKey, record.SourceKey, StringComparison.Ordinal)))
            {
                return Fail(MarketResultCode.InvalidRequest, $"Supply source '{record.SourceKey}' is already counted.", preview);
            }

            if (preview)
            {
                return MarketOperationResult.Success("Market observation preview succeeded.", before, before, preview: true);
            }

            target.Add(record.observationId, record);
            Revision++;
            return MarketOperationResult.Success(isSupply ? "Supply recorded." : "Demand recorded.", before, Revision);
        }

        private static bool SameObservation(MarketObservationRecordData first, MarketObservationRecordData second)
        {
            return first != null && second != null
                && string.Equals(first.marketInstanceId, second.marketInstanceId, StringComparison.Ordinal)
                && string.Equals(first.marketSubjectId, second.marketSubjectId, StringComparison.Ordinal)
                && first.unit == second.unit
                && first.quantity == second.quantity
                && first.availableNowQuantity == second.availableNowQuantity
                && first.reservedQuantity == second.reservedQuantity
                && first.expectedFutureQuantity == second.expectedFutureQuantity
                && first.supplySourceCategory == second.supplySourceCategory
                && first.demandCategory == second.demandCategory
                && string.Equals(first.sourceReferenceId, second.sourceReferenceId, StringComparison.Ordinal)
                && first.observedWorldTime.Equals(second.observedWorldTime)
                && first.expiresWorldTime.Equals(second.expiresWorldTime)
                && string.Equals(first.provenance, second.provenance, StringComparison.Ordinal);
        }

        private IEnumerable<MarketObservationRecordData> ActiveSupply(string marketInstanceId, string marketSubjectId, double worldTime)
        {
            return supplyById.Values
                .Where(record => IsActiveObservation(record, marketInstanceId, marketSubjectId, worldTime))
                .GroupBy(record => record.SourceKey, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(record => record.observedWorldTime).ThenBy(record => record.observationId, StringComparer.Ordinal).First());
        }

        private IEnumerable<MarketObservationRecordData> ActiveDemand(string marketInstanceId, string marketSubjectId, double worldTime)
        {
            return demandById.Values.Where(record => IsActiveObservation(record, marketInstanceId, marketSubjectId, worldTime));
        }

        private static bool IsActiveObservation(MarketObservationRecordData record, string marketInstanceId, string marketSubjectId, double worldTime)
        {
            return record != null
                && string.Equals(record.marketInstanceId, marketInstanceId, StringComparison.Ordinal)
                && string.Equals(record.marketSubjectId, marketSubjectId, StringComparison.Ordinal)
                && record.observedWorldTime <= worldTime
                && (record.expiresWorldTime < 0d || record.expiresWorldTime > worldTime);
        }

        private static MarketScarcityData BuildScarcity(string scarcityId, MarketInstanceData market, MarketSubjectDefinition subject, IReadOnlyList<MarketObservationRecordData> supply, IReadOnlyList<MarketObservationRecordData> demand, double worldTime)
        {
            long total = supply.Sum(record => Math.Max(0L, record.quantity));
            long reserved = supply.Sum(record => Math.Max(0L, record.reservedQuantity));
            long available = supply.Sum(record => Math.Max(0L, record.availableNowQuantity > 0L ? record.availableNowQuantity : record.quantity - record.reservedQuantity));
            long expectedSupply = supply.Sum(record => Math.Max(0L, record.expectedFutureQuantity));
            long currentDemand = demand.Sum(record => Math.Max(0L, record.quantity > 0L ? record.quantity : record.availableNowQuantity));
            long expectedDemand = demand.Sum(record => Math.Max(0L, record.expectedFutureQuantity));
            MarketScarcityClass scarcityClass = ClassifyScarcity(available + expectedSupply, currentDemand + expectedDemand);
            int confidence = supply.Count == 0 && demand.Count == 0 ? 3000 : Math.Clamp((supply.Concat(demand).Sum(record => record.reliability) / Math.Max(1, supply.Count + demand.Count)), 0, 10000);

            return new MarketScarcityData
            {
                scarcityId = scarcityId,
                marketInstanceId = market.marketInstanceId,
                marketSubjectId = subject.Id,
                totalSupply = total,
                availableSupply = available,
                reservedSupply = reserved,
                expectedSupply = expectedSupply,
                currentDemand = currentDemand,
                expectedDemand = expectedDemand,
                scarcityClass = scarcityClass,
                confidence = confidence,
                evaluatedWorldTime = Math.Max(0d, worldTime),
                diagnostics = $"Supply={available}+{expectedSupply}; Demand={currentDemand}+{expectedDemand}; Class={scarcityClass}.",
                revision = 1L
            };
        }

        private static MarketScarcityClass ClassifyScarcity(long effectiveSupply, long effectiveDemand)
        {
            if (effectiveSupply <= 0L && effectiveDemand <= 0L)
            {
                return MarketScarcityClass.Unknown;
            }

            if (effectiveSupply <= 0L)
            {
                return MarketScarcityClass.Critical;
            }

            long ratioBasisPoints = DivideBasisPoints(effectiveDemand, effectiveSupply);
            if (ratioBasisPoints <= 2500) return MarketScarcityClass.Oversupplied;
            if (ratioBasisPoints <= 5000) return MarketScarcityClass.Abundant;
            if (ratioBasisPoints <= 8000) return MarketScarcityClass.Available;
            if (ratioBasisPoints <= 12000) return MarketScarcityClass.Balanced;
            if (ratioBasisPoints <= 17000) return MarketScarcityClass.Limited;
            if (ratioBasisPoints <= 25000) return MarketScarcityClass.Scarce;
            if (ratioBasisPoints <= 40000) return MarketScarcityClass.VeryScarce;
            return MarketScarcityClass.Critical;
        }

        private static long CalculateReferencePrice(MarketSubjectDefinition subject, MarketPriceFormationPolicyData policy, int marketRegionalModifierBasisPoints, MarketScarcityData scarcity, MarketPriceRecordData prior)
        {
            int scarcityIndex = scarcity.scarcityClass switch
            {
                MarketScarcityClass.Oversupplied => -3,
                MarketScarcityClass.Abundant => -2,
                MarketScarcityClass.Available => -1,
                MarketScarcityClass.Balanced => 0,
                MarketScarcityClass.Limited => 1,
                MarketScarcityClass.Scarce => 2,
                MarketScarcityClass.VeryScarce => 3,
                MarketScarcityClass.Critical => 4,
                _ => 0
            };
            int multiplier = 10000 + scarcityIndex * policy.ScarcityStepBasisPoints;
            multiplier = Math.Clamp(multiplier, policy.MinMultiplierBasisPoints, policy.MaxMultiplierBasisPoints);
            multiplier = (int)Math.Max(0L, (long)multiplier * Math.Max(0, marketRegionalModifierBasisPoints) / 10000L);
            multiplier = (int)Math.Max(0L, (long)multiplier * subject.RegionalModifierBasisPoints / 10000L);
            multiplier = (int)Math.Max(0L, (long)multiplier * subject.RarityModifierBasisPoints / 10000L);
            long calculated = MultiplyBasisPoints(subject.BaselinePriceUnits, multiplier);
            if (prior != null && policy.SmoothingBasisPoints > 0)
            {
                calculated = (MultiplyBasisPoints(calculated, 10000 - policy.SmoothingBasisPoints) + MultiplyBasisPoints(prior.referenceAmountUnits, policy.SmoothingBasisPoints));
            }

            return calculated;
        }

        private static long ApplyItemAdjustments(long amount, ItemInstanceSnapshot item, ItemQualitySnapshot quality, ItemDurabilitySnapshot durability, MarketSubjectDefinition subject, bool privilegedHiddenFactors, out int qualityBps, out int durabilityBps, out int rarityBps, out bool hiddenApplied)
        {
            qualityBps = 10000;
            durabilityBps = 10000;
            rarityBps = subject.RarityModifierBasisPoints;
            hiddenApplied = false;
            if (item != null)
            {
                if (quality != null) qualityBps = (int)Math.Round(7000d + Math.Clamp(quality.OverallQuality, 0f, 1f) * 15000d);
                if (durability != null) durabilityBps = Math.Max(1000, (int)Math.Round(Math.Clamp(durability.NormalizedDurability, 0f, 1f) * 10000f));
                if (!privilegedHiddenFactors && !string.IsNullOrWhiteSpace(item.MakerMark))
                {
                    hiddenApplied = false;
                }
                else if (privilegedHiddenFactors && !string.IsNullOrWhiteSpace(item.MakerMark))
                {
                    hiddenApplied = true;
                    qualityBps += 500;
                }
            }

            return MultiplyBasisPoints(MultiplyBasisPoints(MultiplyBasisPoints(amount, qualityBps), durabilityBps), rarityBps);
        }

        private static long ScaleForQuantity(long unitAmount, long quantityBasis, long quantity)
        {
            return DivideRoundHalfUp(checked(unitAmount * Math.Max(1L, quantity)), Math.Max(1L, quantityBasis));
        }

        private static long ClampPrice(long value, MarketSubjectDefinition subject)
        {
            long minimum = Math.Max(1L, subject.MinimumPriceUnits);
            long maximum = subject.MaximumPriceUnits;
            long clamped = Math.Max(minimum, value);
            return maximum > 0L ? Math.Min(maximum, clamped) : clamped;
        }

        private MarketProjection<TRecord> Project<TRecord>(TRecord record, InformationSubjectReferenceData subject, InformationAccessRuntime access, InformationAccessContext context, string policyId, Action<TRecord> redact)
        {
            if (access == null)
            {
                return new MarketProjection<TRecord>(default, null, false, true, Array.Empty<string>(), MarketInformationSubject.ProtectedFields, "Information access runtime is missing.");
            }

            InformationAccessContext request = InformationAccessProjectionUtility.BuildContext(context, subject, InformationAccessMode.Inspect, InformationAccessPurpose.Gameplay, MarketInformationSubject.ProtectedFields, policyId);
            RedactedInformationProjection projection = access.Project(request, MarketInformationSubject.ProtectedFields);
            bool denied = projection.Decision.Denied;
            bool redacted = !denied && (projection.Decision.RedactedAccess || projection.Decision.PartialAccess || projection.Decision.ConditionalAccess);
            if (denied)
            {
                return new MarketProjection<TRecord>(default, projection.Decision, false, true, Array.Empty<string>(), MarketInformationSubject.ProtectedFields, projection.Decision.VisibleReason);
            }

            TRecord projected = record;
            if (redacted)
            {
                redact?.Invoke(projected);
            }

            return new MarketProjection<TRecord>(projected, projection.Decision, redacted, false, projection.Decision.AllowedDetails, projection.Decision.RedactedDetails.Concat(projection.Decision.HiddenDetails).ToArray(), projection.Decision.VisibleReason);
        }

        private bool TryGetMarketAndSubject(string marketInstanceId, string marketSubjectId, out MarketInstanceData market, out MarketSubjectDefinition subject, out MarketOperationResult failure, bool preview)
        {
            market = null;
            subject = null;
            failure = null;
            if (string.IsNullOrWhiteSpace(marketInstanceId) || !marketsById.TryGetValue(marketInstanceId, out market))
            {
                failure = Fail(MarketResultCode.MissingMarket, $"Market instance '{marketInstanceId}' was not found.", preview);
                return false;
            }

            if (!market.active)
            {
                failure = Fail(MarketResultCode.ClosedMarket, $"Market instance '{marketInstanceId}' is closed.", preview);
                return false;
            }

            if (registry == null || !registry.TryGet(marketSubjectId, out subject))
            {
                failure = Fail(MarketResultCode.MissingSubject, $"Market subject '{marketSubjectId}' was not found.", preview);
                return false;
            }

            if (!string.Equals(market.currencyId, subject.CurrencyId, StringComparison.Ordinal))
            {
                failure = Fail(MarketResultCode.CurrencyMismatch, $"Market '{marketInstanceId}' currency '{market.currencyId}' does not match subject '{subject.CurrencyId}'.", preview);
                return false;
            }

            return true;
        }

        private bool ValidateDefinition(string definitionId, out MarketDefinition definition, out string failure)
        {
            definition = null;
            failure = string.Empty;
            if (registry != null && !registry.TryGet(definitionId, out definition))
            {
                failure = $"Market definition '{definitionId}' was not found.";
                return false;
            }

            return true;
        }

        private HashSet<string> SubjectIds()
        {
            return registry?.DefinitionsById.Values.OfType<MarketSubjectDefinition>().Select(item => item.Id).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        }

        private HashSet<string> PriceIds()
        {
            return pricesById.Keys.ToHashSet(StringComparer.Ordinal);
        }

        private MarketOperationResult Fail(MarketResultCode code, string message, bool preview)
        {
            return MarketOperationResult.Failure(code, message, Revision, preview);
        }

        private static bool ValidateMarket(MarketInstanceData market, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (market == null || string.IsNullOrWhiteSpace(market.marketInstanceId))
            {
                failure = "Market instance ID is required.";
                return false;
            }

            if (registry != null && !registry.TryGet(market.marketDefinitionId, out MarketDefinition definition))
            {
                failure = $"Market instance '{market.marketInstanceId}' references missing market definition '{market.marketDefinitionId}'.";
                return false;
            }

            if (registry != null && !registry.TryGet(market.currencyId, out CurrencyDefinition _))
            {
                failure = $"Market instance '{market.marketInstanceId}' references missing currency '{market.currencyId}'.";
                return false;
            }

            if (market.regionalModifierBasisPoints < 0 || market.revision <= 0L)
            {
                failure = $"Market instance '{market.marketInstanceId}' has invalid modifier or revision.";
                return false;
            }

            return true;
        }

        private static bool ValidateObservation(MarketObservationRecordData record, IReadOnlyDictionary<string, MarketInstanceData> markets, ISet<string> subjectIds, bool isSupply, out string failure)
        {
            failure = string.Empty;
            if (record == null || string.IsNullOrWhiteSpace(record.observationId))
            {
                failure = "Market observation ID is required.";
                return false;
            }

            if (!markets.ContainsKey(record.marketInstanceId ?? string.Empty))
            {
                failure = $"Market observation '{record.observationId}' references missing market '{record.marketInstanceId}'.";
                return false;
            }

            if (!subjectIds.Contains(record.marketSubjectId ?? string.Empty))
            {
                failure = $"Market observation '{record.observationId}' references missing subject '{record.marketSubjectId}'.";
                return false;
            }

            if (!Enum.IsDefined(typeof(MarketQuantityUnit), record.unit) || record.unit == MarketQuantityUnit.Unknown)
            {
                failure = $"Market observation '{record.observationId}' has invalid unit.";
                return false;
            }

            if (record.quantity < 0L || record.availableNowQuantity < 0L || record.reservedQuantity < 0L || record.expectedFutureQuantity < 0L)
            {
                failure = $"Market observation '{record.observationId}' has negative quantity.";
                return false;
            }

            if (isSupply && record.reservedQuantity > record.quantity)
            {
                failure = $"Supply observation '{record.observationId}' reserves more than its total quantity.";
                return false;
            }

            if (record.expiresWorldTime >= 0d && record.expiresWorldTime < record.observedWorldTime)
            {
                failure = $"Market observation '{record.observationId}' expires before it was observed.";
                return false;
            }

            if (record.reliability < 0 || record.reliability > 10000)
            {
                failure = $"Market observation '{record.observationId}' has invalid reliability.";
                return false;
            }

            if (isSupply && (!Enum.IsDefined(typeof(MarketSupplySourceCategory), record.supplySourceCategory) || record.supplySourceCategory == MarketSupplySourceCategory.Unknown))
            {
                failure = $"Supply observation '{record.observationId}' must declare a source category.";
                return false;
            }

            if (!isSupply && (!Enum.IsDefined(typeof(MarketDemandCategory), record.demandCategory) || record.demandCategory == MarketDemandCategory.Unknown))
            {
                failure = $"Demand observation '{record.observationId}' must declare a demand category.";
                return false;
            }

            return true;
        }

        private static bool ValidateScarcity(MarketScarcityData scarcity, IReadOnlyDictionary<string, MarketInstanceData> markets, ISet<string> subjectIds, out string failure)
        {
            failure = string.Empty;
            if (scarcity == null || string.IsNullOrWhiteSpace(scarcity.scarcityId))
            {
                failure = "Scarcity record ID is required.";
                return false;
            }

            if (!markets.ContainsKey(scarcity.marketInstanceId ?? string.Empty) || !subjectIds.Contains(scarcity.marketSubjectId ?? string.Empty))
            {
                failure = $"Scarcity record '{scarcity.scarcityId}' references missing market or subject.";
                return false;
            }

            if (!Enum.IsDefined(typeof(MarketScarcityClass), scarcity.scarcityClass) || scarcity.totalSupply < 0L || scarcity.availableSupply < 0L || scarcity.reservedSupply < 0L || scarcity.currentDemand < 0L)
            {
                failure = $"Scarcity record '{scarcity.scarcityId}' has invalid values.";
                return false;
            }

            return true;
        }

        private static bool ValidatePrice(MarketPriceRecordData price, IReadOnlyDictionary<string, MarketInstanceData> markets, ISet<string> subjectIds, out string failure)
        {
            failure = string.Empty;
            if (price == null || string.IsNullOrWhiteSpace(price.marketPriceId))
            {
                failure = "Market price ID is required.";
                return false;
            }

            if (!markets.TryGetValue(price.marketInstanceId ?? string.Empty, out MarketInstanceData market) || !subjectIds.Contains(price.marketSubjectId ?? string.Empty))
            {
                failure = $"Market price '{price.marketPriceId}' references missing market or subject.";
                return false;
            }

            if (price.referenceAmountUnits <= 0L || price.quantityBasis <= 0L)
            {
                failure = $"Market price '{price.marketPriceId}' must have a positive amount and quantity basis.";
                return false;
            }

            if (!string.Equals(market.currencyId, price.currencyId, StringComparison.Ordinal))
            {
                failure = $"Market price '{price.marketPriceId}' currency does not match market currency.";
                return false;
            }

            if (price.validUntilWorldTime >= 0d && price.validUntilWorldTime < price.createdWorldTime)
            {
                failure = $"Market price '{price.marketPriceId}' expires before creation.";
                return false;
            }

            return true;
        }

        private static bool ValidateQuote(MerchantQuoteRecordData quote, IReadOnlyDictionary<string, MarketInstanceData> markets, ISet<string> subjectIds, ISet<string> priceIds, out string failure)
        {
            failure = string.Empty;
            if (quote == null || string.IsNullOrWhiteSpace(quote.quoteId))
            {
                failure = "Merchant quote ID is required.";
                return false;
            }

            if (!markets.TryGetValue(quote.marketInstanceId ?? string.Empty, out MarketInstanceData market) || !subjectIds.Contains(quote.marketSubjectId ?? string.Empty))
            {
                failure = $"Merchant quote '{quote.quoteId}' references missing market or subject.";
                return false;
            }

            if (!priceIds.Contains(quote.marketPriceId ?? string.Empty))
            {
                failure = $"Merchant quote '{quote.quoteId}' references missing market price '{quote.marketPriceId}'.";
                return false;
            }

            if (!Enum.IsDefined(typeof(MerchantQuoteDirection), quote.direction) || quote.direction == MerchantQuoteDirection.Unknown || quote.quantity <= 0L || quote.finalAmountUnits <= 0L || quote.referenceAmountUnits <= 0L)
            {
                failure = $"Merchant quote '{quote.quoteId}' has invalid direction, quantity, or amount.";
                return false;
            }

            if (!string.Equals(market.currencyId, quote.currencyId, StringComparison.Ordinal))
            {
                failure = $"Merchant quote '{quote.quoteId}' currency does not match market currency.";
                return false;
            }

            if (quote.expiresWorldTime >= 0d && quote.expiresWorldTime < quote.createdWorldTime)
            {
                failure = $"Merchant quote '{quote.quoteId}' expires before creation.";
                return false;
            }

            return true;
        }

        private static bool ValidateTransactionObservation(MarketTransactionObservationData observation, IReadOnlyDictionary<string, MarketInstanceData> markets, ISet<string> subjectIds, out string failure)
        {
            failure = string.Empty;
            if (observation == null || string.IsNullOrWhiteSpace(observation.observationId) || string.IsNullOrWhiteSpace(observation.transactionId))
            {
                failure = "Transaction observation requires observation and transaction IDs.";
                return false;
            }

            if (!markets.TryGetValue(observation.marketInstanceId ?? string.Empty, out MarketInstanceData market) || !subjectIds.Contains(observation.marketSubjectId ?? string.Empty))
            {
                failure = $"Transaction observation '{observation.observationId}' references missing market or subject.";
                return false;
            }

            if (observation.paidUnits <= 0L || observation.quantity <= 0L || !string.Equals(market.currencyId, observation.currencyId, StringComparison.Ordinal))
            {
                failure = $"Transaction observation '{observation.observationId}' has invalid amount, quantity, or currency.";
                return false;
            }

            return true;
        }

        private static long MultiplyBasisPoints(long value, int basisPoints)
        {
            return DivideRoundHalfUp(checked(value * (long)Math.Max(0, basisPoints)), 10000L);
        }

        private static long DivideBasisPoints(long numerator, long denominator)
        {
            return denominator <= 0L ? 0L : DivideRoundHalfUp(checked(numerator * 10000L), denominator);
        }

        private static long DivideRoundHalfUp(long numerator, long denominator)
        {
            if (denominator <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(denominator));
            }

            return (numerator + denominator / 2L) / denominator;
        }

        private static string PriceKey(string marketInstanceId, string marketSubjectId)
        {
            return $"{marketInstanceId ?? string.Empty}|{marketSubjectId ?? string.Empty}";
        }

        private static string StableId(string prefix, string first, string second, double worldTime)
        {
            return $"{prefix}.{Sanitize(first)}.{Sanitize(second)}.{Math.Max(0d, worldTime):0.###}";
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "none"
                : new string(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-').ToLowerInvariant();
        }
    }
}
