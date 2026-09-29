using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge.Access;
using static UnityIsekaiGame.Economy.Trading.TradeModelHelpers;

namespace UnityIsekaiGame.Economy.Trading
{
    [Serializable]
    public sealed class TradeParticipantData
    {
        public string participantId;
        public TradeParticipantKind kind = TradeParticipantKind.Person;
        public TradeParticipantRole role = TradeParticipantRole.Trader;
        public string subjectId;
        public string representedOwnerId;
        public string authorizedRepresentativeId;
        public string sourceInventoryId;
        public string receivingInventoryId;
        public string accountId;
        public string[] permissions = Array.Empty<string>();
        public string accessPolicyId;
        public long revision = 1L;

        public TradeParticipantData Clone()
        {
            return new TradeParticipantData
            {
                participantId = participantId ?? string.Empty,
                kind = kind,
                role = role,
                subjectId = subjectId ?? string.Empty,
                representedOwnerId = representedOwnerId ?? string.Empty,
                authorizedRepresentativeId = authorizedRepresentativeId ?? string.Empty,
                sourceInventoryId = sourceInventoryId ?? string.Empty,
                receivingInventoryId = receivingInventoryId ?? string.Empty,
                accountId = accountId ?? string.Empty,
                permissions = CloneIds(permissions),
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class TradeAssetEntryData
    {
        public string assetEntryId;
        public TradeAssetKind assetKind = TradeAssetKind.ItemInstance;
        public string sourceParticipantId;
        public string destinationParticipantId;
        public string itemInstanceId;
        public string itemDefinitionId;
        public int quantity = 1;
        public string[] itemInstanceIds = Array.Empty<string>();
        public string sourceContainerId;
        public string destinationContainerId;
        public string expectedOwnerPersonId;
        public string expectedCustodianPersonId;
        public ItemLocationKind expectedLocationKind = ItemLocationKind.Unassigned;
        public long expectedItemRevision;
        public string itemReservationId;
        public string createdSplitItemInstanceId;
        public string currencyId;
        public long units;
        public string sourceAccountId;
        public string destinationAccountId;
        public string monetaryReservationId;
        public long expectedAccountRevision;
        public string quoteId;
        public string marketPriceId;
        public string valuationId;
        public TradeDisclosureState disclosureState = TradeDisclosureState.ParticipantOnly;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public TradeAssetEntryData Clone()
        {
            return new TradeAssetEntryData
            {
                assetEntryId = assetEntryId ?? string.Empty,
                assetKind = assetKind,
                sourceParticipantId = sourceParticipantId ?? string.Empty,
                destinationParticipantId = destinationParticipantId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                quantity = Math.Max(1, quantity),
                itemInstanceIds = CloneIds(itemInstanceIds),
                sourceContainerId = sourceContainerId ?? string.Empty,
                destinationContainerId = destinationContainerId ?? string.Empty,
                expectedOwnerPersonId = expectedOwnerPersonId ?? string.Empty,
                expectedCustodianPersonId = expectedCustodianPersonId ?? string.Empty,
                expectedLocationKind = expectedLocationKind,
                expectedItemRevision = expectedItemRevision,
                itemReservationId = itemReservationId ?? string.Empty,
                createdSplitItemInstanceId = createdSplitItemInstanceId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = units,
                sourceAccountId = sourceAccountId ?? string.Empty,
                destinationAccountId = destinationAccountId ?? string.Empty,
                monetaryReservationId = monetaryReservationId ?? string.Empty,
                expectedAccountRevision = expectedAccountRevision,
                quoteId = quoteId ?? string.Empty,
                marketPriceId = marketPriceId ?? string.Empty,
                valuationId = valuationId ?? string.Empty,
                disclosureState = disclosureState,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }

        public bool IsItemAsset => assetKind == TradeAssetKind.ItemInstance || assetKind == TradeAssetKind.StackQuantity || assetKind == TradeAssetKind.MultipleItemInstances || assetKind == TradeAssetKind.PhysicalCurrency;
        public bool IsMoneyAsset => assetKind == TradeAssetKind.Money;
        public string AssetKey => assetKind == TradeAssetKind.Money
            ? $"money:{sourceAccountId}:{currencyId}:{units}"
            : assetKind == TradeAssetKind.MultipleItemInstances
                ? $"items:{string.Join(",", itemInstanceIds ?? Array.Empty<string>())}"
                : $"item:{itemInstanceId}:{quantity}";
    }

    [Serializable]
    public sealed class TradeBundleData
    {
        public string bundleId;
        public string contributingParticipantId;
        public string receivingParticipantId;
        public List<TradeAssetEntryData> assets = new List<TradeAssetEntryData>();
        public string accessPolicyId;
        public string provenance;

        public TradeBundleData Clone()
        {
            return new TradeBundleData
            {
                bundleId = bundleId ?? string.Empty,
                contributingParticipantId = contributingParticipantId ?? string.Empty,
                receivingParticipantId = receivingParticipantId ?? string.Empty,
                assets = assets == null ? new List<TradeAssetEntryData>() : assets.Select(asset => asset?.Clone()).Where(asset => asset != null).ToList(),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class TradeOfferData
    {
        public string offerId;
        public string tradeSessionId;
        public string parentOfferId;
        public int sequence;
        public string proposingParticipantId;
        public string[] respondingParticipantIds = Array.Empty<string>();
        public List<TradeBundleData> bundles = new List<TradeBundleData>();
        public string[] marketPriceIds = Array.Empty<string>();
        public string[] merchantQuoteIds = Array.Empty<string>();
        public string[] valuationIds = Array.Empty<string>();
        public double createdWorldTime;
        public double expiresWorldTime = -1d;
        public long sourceRuntimeRevision;
        public TradeOfferState state = TradeOfferState.Submitted;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public TradeOfferData Clone()
        {
            return new TradeOfferData
            {
                offerId = offerId ?? string.Empty,
                tradeSessionId = tradeSessionId ?? string.Empty,
                parentOfferId = parentOfferId ?? string.Empty,
                sequence = Math.Max(0, sequence),
                proposingParticipantId = proposingParticipantId ?? string.Empty,
                respondingParticipantIds = CloneIds(respondingParticipantIds),
                bundles = bundles == null ? new List<TradeBundleData>() : bundles.Select(bundle => bundle?.Clone()).Where(bundle => bundle != null).ToList(),
                marketPriceIds = CloneIds(marketPriceIds),
                merchantQuoteIds = CloneIds(merchantQuoteIds),
                valuationIds = CloneIds(valuationIds),
                createdWorldTime = createdWorldTime,
                expiresWorldTime = expiresWorldTime,
                sourceRuntimeRevision = sourceRuntimeRevision,
                state = state,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }

        public IReadOnlyList<TradeAssetEntryData> AllAssets => (bundles ?? new List<TradeBundleData>()).SelectMany(bundle => bundle.assets ?? new List<TradeAssetEntryData>()).Select(asset => asset.Clone()).ToArray();

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return TradeInformationSubject.Create("trade.offer", offerId, tradeSessionId, respondingParticipantIds);
        }
    }

    [Serializable]
    public sealed class TradeSessionData
    {
        public string tradeSessionId;
        public string tradePolicyId;
        public List<TradeParticipantData> participants = new List<TradeParticipantData>();
        public string initiatorParticipantId;
        public string hostMerchantId;
        public string hostOrganizationId;
        public string marketInstanceId;
        public string locationReferenceId;
        public TradeSessionState state = TradeSessionState.Open;
        public double createdWorldTime;
        public double lastActivityWorldTime;
        public double expiresWorldTime = -1d;
        public string activeOfferId;
        public string acceptedOfferId;
        public string[] offerHistoryIds = Array.Empty<string>();
        public int negotiationRound;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public TradeSessionData Clone()
        {
            return new TradeSessionData
            {
                tradeSessionId = tradeSessionId ?? string.Empty,
                tradePolicyId = tradePolicyId ?? string.Empty,
                participants = participants == null ? new List<TradeParticipantData>() : participants.Select(participant => participant?.Clone()).Where(participant => participant != null).ToList(),
                initiatorParticipantId = initiatorParticipantId ?? string.Empty,
                hostMerchantId = hostMerchantId ?? string.Empty,
                hostOrganizationId = hostOrganizationId ?? string.Empty,
                marketInstanceId = marketInstanceId ?? string.Empty,
                locationReferenceId = locationReferenceId ?? string.Empty,
                state = state,
                createdWorldTime = createdWorldTime,
                lastActivityWorldTime = lastActivityWorldTime,
                expiresWorldTime = expiresWorldTime,
                activeOfferId = activeOfferId ?? string.Empty,
                acceptedOfferId = acceptedOfferId ?? string.Empty,
                offerHistoryIds = CloneIds(offerHistoryIds),
                negotiationRound = Math.Max(0, negotiationRound),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return TradeInformationSubject.Create("trade.session", tradeSessionId, tradePolicyId, participants?.Select(participant => participant.subjectId));
        }
    }

    [Serializable]
    public sealed class TradeItemReservationData
    {
        public string reservationId;
        public string tradeSessionId;
        public string offerId;
        public string assetEntryId;
        public string itemInstanceId;
        public int quantity = 1;
        public TradeReservationState state = TradeReservationState.Active;
        public double createdWorldTime;
        public double expiresWorldTime = -1d;
        public long expectedItemRevision;
        public long revision = 1L;

        public TradeItemReservationData Clone()
        {
            return new TradeItemReservationData
            {
                reservationId = reservationId ?? string.Empty,
                tradeSessionId = tradeSessionId ?? string.Empty,
                offerId = offerId ?? string.Empty,
                assetEntryId = assetEntryId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                quantity = Math.Max(1, quantity),
                state = state,
                createdWorldTime = createdWorldTime,
                expiresWorldTime = expiresWorldTime,
                expectedItemRevision = expectedItemRevision,
                revision = revision
            };
        }
    }

    public enum TradeReservationState
    {
        Unknown,
        Active,
        Released,
        Committed,
        Expired
    }

    [Serializable]
    public sealed class TradeValuationRecordData
    {
        public string valuationId;
        public string tradeSessionId;
        public string offerId;
        public string evaluatingParticipantId;
        public string assetEntryId;
        public string currencyId;
        public long estimatedUnits;
        public long minimumEstimatedUnits;
        public long maximumEstimatedUnits;
        public int confidence;
        public string[] knownFactors = Array.Empty<string>();
        public string[] unknownFactors = Array.Empty<string>();
        public string assumptions;
        public string[] sourceReferences = Array.Empty<string>();
        public double worldTime;
        public double expiresWorldTime = -1d;
        public bool privilegedHiddenFactors;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public TradeValuationRecordData Clone()
        {
            return new TradeValuationRecordData
            {
                valuationId = valuationId ?? string.Empty,
                tradeSessionId = tradeSessionId ?? string.Empty,
                offerId = offerId ?? string.Empty,
                evaluatingParticipantId = evaluatingParticipantId ?? string.Empty,
                assetEntryId = assetEntryId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                estimatedUnits = estimatedUnits,
                minimumEstimatedUnits = minimumEstimatedUnits,
                maximumEstimatedUnits = maximumEstimatedUnits,
                confidence = Math.Clamp(confidence, 0, 10000),
                knownFactors = CloneIds(knownFactors),
                unknownFactors = CloneIds(unknownFactors),
                assumptions = assumptions ?? string.Empty,
                sourceReferences = CloneIds(sourceReferences),
                worldTime = worldTime,
                expiresWorldTime = expiresWorldTime,
                privilegedHiddenFactors = privilegedHiddenFactors,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class TradeRecordData
    {
        public string tradeRecordId;
        public string tradeSessionId;
        public string acceptedOfferId;
        public string[] participantIds = Array.Empty<string>();
        public List<TradeBundleData> exchangedBundles = new List<TradeBundleData>();
        public string[] economyTransactionIds = Array.Empty<string>();
        public string[] itemTransferReferences = Array.Empty<string>();
        public string[] marketPriceIds = Array.Empty<string>();
        public string[] quoteIds = Array.Empty<string>();
        public string[] valuationIds = Array.Empty<string>();
        public double executionWorldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public TradeRecordData Clone()
        {
            return new TradeRecordData
            {
                tradeRecordId = tradeRecordId ?? string.Empty,
                tradeSessionId = tradeSessionId ?? string.Empty,
                acceptedOfferId = acceptedOfferId ?? string.Empty,
                participantIds = CloneIds(participantIds),
                exchangedBundles = exchangedBundles == null ? new List<TradeBundleData>() : exchangedBundles.Select(bundle => bundle?.Clone()).Where(bundle => bundle != null).ToList(),
                economyTransactionIds = CloneIds(economyTransactionIds),
                itemTransferReferences = CloneIds(itemTransferReferences),
                marketPriceIds = CloneIds(marketPriceIds),
                quoteIds = CloneIds(quoteIds),
                valuationIds = CloneIds(valuationIds),
                executionWorldTime = executionWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class TradeReceiptData
    {
        public string receiptId;
        public string tradeRecordId;
        public string issuerParticipantId;
        public string recipientParticipantId;
        public string[] receivedAssetEntryIds = Array.Empty<string>();
        public string currencyId;
        public long moneyPaidUnits;
        public string[] quoteIds = Array.Empty<string>();
        public string returnPolicyIdFoundation;
        public double worldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public TradeReceiptData Clone()
        {
            return new TradeReceiptData
            {
                receiptId = receiptId ?? string.Empty,
                tradeRecordId = tradeRecordId ?? string.Empty,
                issuerParticipantId = issuerParticipantId ?? string.Empty,
                recipientParticipantId = recipientParticipantId ?? string.Empty,
                receivedAssetEntryIds = CloneIds(receivedAssetEntryIds),
                currencyId = currencyId ?? string.Empty,
                moneyPaidUnits = moneyPaidUnits,
                quoteIds = CloneIds(quoteIds),
                returnPolicyIdFoundation = returnPolicyIdFoundation ?? string.Empty,
                worldTime = worldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class TradeRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<TradeSessionData> sessions = new List<TradeSessionData>();
        public List<TradeOfferData> offers = new List<TradeOfferData>();
        public List<TradeItemReservationData> itemReservations = new List<TradeItemReservationData>();
        public List<TradeValuationRecordData> valuations = new List<TradeValuationRecordData>();
        public List<TradeRecordData> tradeRecords = new List<TradeRecordData>();
        public List<TradeReceiptData> receipts = new List<TradeReceiptData>();
        public List<TradeProcessedCommandData> processedCommands = new List<TradeProcessedCommandData>();

        public TradeRuntimeSaveData Clone()
        {
            return new TradeRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                sessions = sessions == null ? new List<TradeSessionData>() : sessions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                offers = offers == null ? new List<TradeOfferData>() : offers.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                itemReservations = itemReservations == null ? new List<TradeItemReservationData>() : itemReservations.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                valuations = valuations == null ? new List<TradeValuationRecordData>() : valuations.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                tradeRecords = tradeRecords == null ? new List<TradeRecordData>() : tradeRecords.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                receipts = receipts == null ? new List<TradeReceiptData>() : receipts.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                processedCommands = processedCommands == null ? new List<TradeProcessedCommandData>() : processedCommands.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    [Serializable]
    public sealed class TradeProcessedCommandData
    {
        public string transactionId;
        public string operationKey;
        public TradeOperationCode code;
        public string resultId;
        public long revision;

        public TradeProcessedCommandData Clone()
        {
            return new TradeProcessedCommandData
            {
                transactionId = transactionId ?? string.Empty,
                operationKey = operationKey ?? string.Empty,
                code = code,
                resultId = resultId ?? string.Empty,
                revision = revision
            };
        }
    }

    public sealed class TradeOperationResult
    {
        private TradeOperationResult(bool succeeded, bool preview, bool duplicate, TradeOperationCode code, string message, long revisionBefore, long revisionAfter, TradeSessionData session, TradeOfferData offer, TradeValuationRecordData valuation, TradeRecordData tradeRecord, TradeReceiptData receipt)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = code;
            Message = message ?? string.Empty;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
            Session = session?.Clone();
            Offer = offer?.Clone();
            Valuation = valuation?.Clone();
            TradeRecord = tradeRecord?.Clone();
            Receipt = receipt?.Clone();
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public TradeOperationCode Code { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public TradeSessionData Session { get; }
        public TradeOfferData Offer { get; }
        public TradeValuationRecordData Valuation { get; }
        public TradeRecordData TradeRecord { get; }
        public TradeReceiptData Receipt { get; }

        public static TradeOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false, TradeSessionData session = null, TradeOfferData offer = null, TradeValuationRecordData valuation = null, TradeRecordData tradeRecord = null, TradeReceiptData receipt = null)
        {
            return new TradeOperationResult(true, preview, duplicate, preview ? TradeOperationCode.Preview : duplicate ? TradeOperationCode.Duplicate : TradeOperationCode.Success, message, before, after, session, offer, valuation, tradeRecord, receipt);
        }

        public static TradeOperationResult Failure(TradeOperationCode code, string message, long revision, bool preview = false)
        {
            return new TradeOperationResult(false, preview, false, code, message, revision, revision, null, null, null, null, null);
        }
    }

    public sealed class TradeProjection<TRecord>
    {
        public TradeProjection(TRecord record, InformationAccessDecision decision, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields, string message)
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

    public static class TradeInformationSubject
    {
        public static readonly string[] ProtectedFields =
        {
            "detail.session",
            "detail.participants",
            "detail.assets",
            "detail.offer-history",
            "detail.accounts",
            "detail.reservations",
            "detail.hidden-properties",
            "detail.valuations",
            "detail.margin",
            "detail.receipt"
        };

        public static InformationSubjectReferenceData Create(string tag, string subjectId, string parentSubjectId = "", IEnumerable<string> tags = null)
        {
            string[] subjectTags = (tags ?? Array.Empty<string>())
                .Concat(new[] { "domain.economy", "trade", tag })
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

    internal static class TradeModelHelpers
    {
        public static string[] CloneIds(string[] values)
        {
            return values == null ? Array.Empty<string>() : values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }
}
