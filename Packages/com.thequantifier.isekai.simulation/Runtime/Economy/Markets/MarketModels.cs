using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Economy.Markets
{
    [Serializable]
    public sealed class MarketInstanceData
    {
        public string marketInstanceId;
        public string marketDefinitionId;
        public string regionId;
        public string organizationId;
        public string settlementId;
        public string stationId;
        public string customScopeId;
        public bool active = true;
        public string currencyId;
        public int regionalModifierBasisPoints = 10000;
        public double lastUpdateWorldTime = -1d;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public MarketInstanceData Clone()
        {
            return new MarketInstanceData
            {
                marketInstanceId = marketInstanceId ?? string.Empty,
                marketDefinitionId = marketDefinitionId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                settlementId = settlementId ?? string.Empty,
                stationId = stationId ?? string.Empty,
                customScopeId = customScopeId ?? string.Empty,
                active = active,
                currencyId = currencyId ?? string.Empty,
                regionalModifierBasisPoints = Math.Max(0, regionalModifierBasisPoints),
                lastUpdateWorldTime = lastUpdateWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class MarketObservationRecordData
    {
        public string observationId;
        public string marketInstanceId;
        public string marketSubjectId;
        public MarketQuantityUnit unit = MarketQuantityUnit.Each;
        public long quantity;
        public long availableNowQuantity;
        public long reservedQuantity;
        public long expectedFutureQuantity;
        public MarketSupplySourceCategory supplySourceCategory = MarketSupplySourceCategory.Unknown;
        public MarketDemandCategory demandCategory = MarketDemandCategory.Unknown;
        public string sourceReferenceId;
        public string sourceInventoryId;
        public string sourceOrganizationId;
        public string sourceProductionJobId;
        public string sourceLotId;
        public double observedWorldTime;
        public double expiresWorldTime = -1d;
        public int reliability = 10000;
        public MarketObservationPrivacy privacy = MarketObservationPrivacy.Public;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public MarketObservationRecordData Clone()
        {
            return new MarketObservationRecordData
            {
                observationId = observationId ?? string.Empty,
                marketInstanceId = marketInstanceId ?? string.Empty,
                marketSubjectId = marketSubjectId ?? string.Empty,
                unit = unit,
                quantity = quantity,
                availableNowQuantity = availableNowQuantity,
                reservedQuantity = reservedQuantity,
                expectedFutureQuantity = expectedFutureQuantity,
                supplySourceCategory = supplySourceCategory,
                demandCategory = demandCategory,
                sourceReferenceId = sourceReferenceId ?? string.Empty,
                sourceInventoryId = sourceInventoryId ?? string.Empty,
                sourceOrganizationId = sourceOrganizationId ?? string.Empty,
                sourceProductionJobId = sourceProductionJobId ?? string.Empty,
                sourceLotId = sourceLotId ?? string.Empty,
                observedWorldTime = observedWorldTime,
                expiresWorldTime = expiresWorldTime,
                reliability = Math.Clamp(reliability, 0, 10000),
                privacy = privacy,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }

        public string SourceKey => $"{marketInstanceId}:{marketSubjectId}:{supplySourceCategory}:{FirstNonEmpty(sourceReferenceId, sourceInventoryId, sourceOrganizationId, sourceProductionJobId, sourceLotId, observationId)}";

        private static string FirstNonEmpty(params string[] values)
        {
            return values == null ? string.Empty : values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class MarketScarcityData
    {
        public string scarcityId;
        public string marketInstanceId;
        public string marketSubjectId;
        public long totalSupply;
        public long availableSupply;
        public long reservedSupply;
        public long expectedSupply;
        public long currentDemand;
        public long expectedDemand;
        public MarketScarcityClass scarcityClass = MarketScarcityClass.Unknown;
        public int confidence;
        public double evaluatedWorldTime;
        public string diagnostics;
        public long revision = 1L;

        public MarketScarcityData Clone()
        {
            return new MarketScarcityData
            {
                scarcityId = scarcityId ?? string.Empty,
                marketInstanceId = marketInstanceId ?? string.Empty,
                marketSubjectId = marketSubjectId ?? string.Empty,
                totalSupply = totalSupply,
                availableSupply = availableSupply,
                reservedSupply = reservedSupply,
                expectedSupply = expectedSupply,
                currentDemand = currentDemand,
                expectedDemand = expectedDemand,
                scarcityClass = scarcityClass,
                confidence = Math.Clamp(confidence, 0, 10000),
                evaluatedWorldTime = evaluatedWorldTime,
                diagnostics = diagnostics ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class MarketPriceRecordData
    {
        public string marketPriceId;
        public string marketInstanceId;
        public string marketSubjectId;
        public string currencyId;
        public long referenceAmountUnits;
        public long quantityBasis = 1L;
        public MarketQuantityUnit unit = MarketQuantityUnit.Each;
        public string scarcityId;
        public MarketScarcityClass scarcityClass = MarketScarcityClass.Unknown;
        public long supplyAvailable;
        public long demandCurrent;
        public MarketPriceFormationKind priceFormationPolicy = MarketPriceFormationKind.DefaultSupplyDemand;
        public bool fixedPriceFallback;
        public int confidence;
        public double createdWorldTime;
        public double validUntilWorldTime = -1d;
        public string priorPriceId;
        public string accessPolicyId;
        public string provenance;
        public string diagnostics;
        public long revision = 1L;

        public MarketPriceRecordData Clone()
        {
            return new MarketPriceRecordData
            {
                marketPriceId = marketPriceId ?? string.Empty,
                marketInstanceId = marketInstanceId ?? string.Empty,
                marketSubjectId = marketSubjectId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                referenceAmountUnits = referenceAmountUnits,
                quantityBasis = Math.Max(1L, quantityBasis),
                unit = unit,
                scarcityId = scarcityId ?? string.Empty,
                scarcityClass = scarcityClass,
                supplyAvailable = supplyAvailable,
                demandCurrent = demandCurrent,
                priceFormationPolicy = priceFormationPolicy,
                fixedPriceFallback = fixedPriceFallback,
                confidence = Math.Clamp(confidence, 0, 10000),
                createdWorldTime = createdWorldTime,
                validUntilWorldTime = validUntilWorldTime,
                priorPriceId = priorPriceId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                diagnostics = diagnostics ?? string.Empty,
                revision = revision
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return MarketInformationSubject.Create("market.price", marketPriceId, marketInstanceId, new[] { marketSubjectId, currencyId });
        }
    }

    [Serializable]
    public sealed class MerchantQuoteRecordData
    {
        public string quoteId;
        public string merchantId;
        public string buyerOrSellerContextId;
        public string marketPriceId;
        public string marketInstanceId;
        public string marketSubjectId;
        public string itemInstanceId;
        public MerchantQuoteDirection direction = MerchantQuoteDirection.Unknown;
        public string currencyId;
        public long quantity = 1L;
        public MarketQuantityUnit unit = MarketQuantityUnit.Each;
        public long referenceAmountUnits;
        public long finalAmountUnits;
        public int marginBasisPoints;
        public int qualityAdjustmentBasisPoints = 10000;
        public int durabilityAdjustmentBasisPoints = 10000;
        public int rarityAdjustmentBasisPoints = 10000;
        public bool hiddenFactorsApplied;
        public bool fixedPriceOverride;
        public double createdWorldTime;
        public double expiresWorldTime = -1d;
        public long marketRevision;
        public long priceRevision;
        public string accessPolicyId;
        public string provenance;
        public string diagnostics;
        public long revision = 1L;

        public MerchantQuoteRecordData Clone()
        {
            return new MerchantQuoteRecordData
            {
                quoteId = quoteId ?? string.Empty,
                merchantId = merchantId ?? string.Empty,
                buyerOrSellerContextId = buyerOrSellerContextId ?? string.Empty,
                marketPriceId = marketPriceId ?? string.Empty,
                marketInstanceId = marketInstanceId ?? string.Empty,
                marketSubjectId = marketSubjectId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                direction = direction,
                currencyId = currencyId ?? string.Empty,
                quantity = Math.Max(1L, quantity),
                unit = unit,
                referenceAmountUnits = referenceAmountUnits,
                finalAmountUnits = finalAmountUnits,
                marginBasisPoints = Math.Max(0, marginBasisPoints),
                qualityAdjustmentBasisPoints = Math.Max(0, qualityAdjustmentBasisPoints),
                durabilityAdjustmentBasisPoints = Math.Max(0, durabilityAdjustmentBasisPoints),
                rarityAdjustmentBasisPoints = Math.Max(0, rarityAdjustmentBasisPoints),
                hiddenFactorsApplied = hiddenFactorsApplied,
                fixedPriceOverride = fixedPriceOverride,
                createdWorldTime = createdWorldTime,
                expiresWorldTime = expiresWorldTime,
                marketRevision = marketRevision,
                priceRevision = priceRevision,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                diagnostics = diagnostics ?? string.Empty,
                revision = revision
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return MarketInformationSubject.Create("market.quote", quoteId, marketPriceId, new[] { marketSubjectId, merchantId });
        }
    }

    [Serializable]
    public sealed class MarketTransactionObservationData
    {
        public string observationId;
        public string transactionId;
        public string marketInstanceId;
        public string marketSubjectId;
        public string currencyId;
        public long paidUnits;
        public long quantity = 1L;
        public bool publicObservation;
        public bool refundOrReversal;
        public double observedWorldTime;
        public string accessPolicyId;
        public long revision = 1L;

        public MarketTransactionObservationData Clone()
        {
            return new MarketTransactionObservationData
            {
                observationId = observationId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                marketInstanceId = marketInstanceId ?? string.Empty,
                marketSubjectId = marketSubjectId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                paidUnits = paidUnits,
                quantity = Math.Max(1L, quantity),
                publicObservation = publicObservation,
                refundOrReversal = refundOrReversal,
                observedWorldTime = observedWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class MarketRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<MarketInstanceData> markets = new List<MarketInstanceData>();
        public List<MarketObservationRecordData> supplyRecords = new List<MarketObservationRecordData>();
        public List<MarketObservationRecordData> demandRecords = new List<MarketObservationRecordData>();
        public List<MarketScarcityData> scarcityRecords = new List<MarketScarcityData>();
        public List<MarketPriceRecordData> priceRecords = new List<MarketPriceRecordData>();
        public List<MerchantQuoteRecordData> quotes = new List<MerchantQuoteRecordData>();
        public List<MarketTransactionObservationData> transactionObservations = new List<MarketTransactionObservationData>();
        public List<MarketCurrentPriceReferenceData> currentPrices = new List<MarketCurrentPriceReferenceData>();

        public MarketRuntimeSaveData Clone()
        {
            return new MarketRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                markets = markets == null ? new List<MarketInstanceData>() : markets.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                supplyRecords = supplyRecords == null ? new List<MarketObservationRecordData>() : supplyRecords.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                demandRecords = demandRecords == null ? new List<MarketObservationRecordData>() : demandRecords.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                scarcityRecords = scarcityRecords == null ? new List<MarketScarcityData>() : scarcityRecords.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                priceRecords = priceRecords == null ? new List<MarketPriceRecordData>() : priceRecords.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                quotes = quotes == null ? new List<MerchantQuoteRecordData>() : quotes.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                transactionObservations = transactionObservations == null ? new List<MarketTransactionObservationData>() : transactionObservations.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                currentPrices = currentPrices == null ? new List<MarketCurrentPriceReferenceData>() : currentPrices.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    [Serializable]
    public sealed class MarketCurrentPriceReferenceData
    {
        public string marketInstanceId;
        public string marketSubjectId;
        public string marketPriceId;

        public MarketCurrentPriceReferenceData Clone()
        {
            return new MarketCurrentPriceReferenceData
            {
                marketInstanceId = marketInstanceId ?? string.Empty,
                marketSubjectId = marketSubjectId ?? string.Empty,
                marketPriceId = marketPriceId ?? string.Empty
            };
        }
    }

    public sealed class MarketOperationResult
    {
        private MarketOperationResult(bool succeeded, bool preview, bool duplicate, MarketResultCode code, string message, long revisionBefore, long revisionAfter, MarketInstanceData market, MarketScarcityData scarcity, MarketPriceRecordData price, MerchantQuoteRecordData quote)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = code;
            Message = message ?? string.Empty;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
            Market = market?.Clone();
            Scarcity = scarcity?.Clone();
            Price = price?.Clone();
            Quote = quote?.Clone();
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public MarketResultCode Code { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public MarketInstanceData Market { get; }
        public MarketScarcityData Scarcity { get; }
        public MarketPriceRecordData Price { get; }
        public MerchantQuoteRecordData Quote { get; }

        public static MarketOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false, MarketInstanceData market = null, MarketScarcityData scarcity = null, MarketPriceRecordData price = null, MerchantQuoteRecordData quote = null)
        {
            return new MarketOperationResult(true, preview, duplicate, preview ? MarketResultCode.Preview : duplicate ? MarketResultCode.Duplicate : MarketResultCode.Success, message, before, after, market, scarcity, price, quote);
        }

        public static MarketOperationResult Failure(MarketResultCode code, string message, long revision, bool preview = false)
        {
            return new MarketOperationResult(false, preview, false, code, message, revision, revision, null, null, null, null);
        }
    }

    public sealed class MarketProjection<TRecord>
    {
        public MarketProjection(TRecord record, InformationAccessDecision decision, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields, string message)
        {
            Record = record;
            Decision = decision;
            Redacted = redacted;
            Denied = denied;
            VisibleFields = (visibleFields ?? Array.Empty<string>()).ToArray();
            RedactedFields = (redactedFields ?? Array.Empty<string>()).ToArray();
            Message = message ?? string.Empty;
        }

        public TRecord Record { get; }
        public InformationAccessDecision Decision { get; }
        public bool Succeeded => !Denied && Record != null;
        public bool Redacted { get; }
        public bool Denied { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
        public string Message { get; }
    }

    public static class MarketInformationSubject
    {
        public static readonly string[] ProtectedFields =
        {
            "detail.market",
            "detail.subject",
            "detail.supply",
            "detail.demand",
            "detail.scarcity",
            "detail.reference-price",
            "detail.margin",
            "detail.history",
            "detail.source",
            "detail.hidden-factors"
        };

        public static InformationSubjectReferenceData Create(string tag, string subjectId, string parentSubjectId = "", IEnumerable<string> tags = null)
        {
            string[] subjectTags = (tags ?? Array.Empty<string>())
                .Concat(new[] { "domain.economy", "market", tag })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                tags = subjectTags
            };
        }
    }
}
