using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Knowledge.Access;
using static UnityIsekaiGame.Economy.Trading.TradeModelHelpers;

namespace UnityIsekaiGame.Economy.Trading
{
    public sealed class TradeRuntime
    {
        private readonly Dictionary<string, TradeSessionData> sessionsById = new Dictionary<string, TradeSessionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TradeOfferData> offersById = new Dictionary<string, TradeOfferData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TradeItemReservationData> itemReservationsById = new Dictionary<string, TradeItemReservationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TradeValuationRecordData> valuationsById = new Dictionary<string, TradeValuationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TradeRecordData> tradeRecordsById = new Dictionary<string, TradeRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TradeReceiptData> receiptsById = new Dictionary<string, TradeReceiptData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TradeProcessedCommandData> processedByTransactionId = new Dictionary<string, TradeProcessedCommandData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string worldId;

        public long Revision { get; private set; }
        public int SessionCount => sessionsById.Count;
        public int OfferCount => offersById.Count;
        public int TradeRecordCount => tradeRecordsById.Count;
        public int ReceiptCount => receiptsById.Count;

        public IReadOnlyList<TradeSessionData> Sessions => sessionsById.Values.OrderBy(item => item.tradeSessionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TradeOfferData> Offers => offersById.Values.OrderBy(item => item.tradeSessionId, StringComparer.Ordinal).ThenBy(item => item.sequence).ThenBy(item => item.offerId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TradeRecordData> TradeRecords => tradeRecordsById.Values.OrderBy(item => item.executionWorldTime).ThenBy(item => item.tradeRecordId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TradeReceiptData> Receipts => receiptsById.Values.OrderBy(item => item.worldTime).ThenBy(item => item.receiptId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, string world)
        {
            registry = definitionRegistry ?? registry;
            worldId = world ?? string.Empty;
        }

        public TradeOperationResult OpenSession(TradePolicyDefinition policy, TradeSessionData session, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (policy == null)
            {
                return Fail(TradeOperationCode.MissingPolicy, "Trade policy is required.", preview);
            }

            if (session == null)
            {
                return Fail(TradeOperationCode.InvalidRequest, "Trade session data is required.", preview);
            }

            if (!string.IsNullOrWhiteSpace(transactionId) && IsDuplicate(transactionId, "open-session", session.tradeSessionId, out TradeOperationResult duplicate))
            {
                return duplicate;
            }

            TradeSessionData working = session.Clone();
            working.tradePolicyId = policy.Id;
            working.state = working.state == TradeSessionState.Unknown ? TradeSessionState.Open : working.state;
            working.createdWorldTime = Math.Max(0d, working.createdWorldTime);
            working.lastActivityWorldTime = Math.Max(working.createdWorldTime, working.lastActivityWorldTime);
            if (working.expiresWorldTime < 0d && policy.DefaultOfferDuration > 0d)
            {
                working.expiresWorldTime = working.createdWorldTime + policy.DefaultOfferDuration;
            }

            if (!ValidateSession(working, policy, out TradeOperationCode code, out string failure))
            {
                return Fail(code, failure, preview);
            }

            if (sessionsById.ContainsKey(working.tradeSessionId))
            {
                return Fail(TradeOperationCode.InvalidRequest, $"Trade session '{working.tradeSessionId}' already exists.", preview);
            }

            if (preview)
            {
                return TradeOperationResult.Success("Trade session preview succeeded.", before, before, preview: true, session: working);
            }

            sessionsById.Add(working.tradeSessionId, working);
            Revision++;
            Remember(transactionId, "open-session", working.tradeSessionId);
            return TradeOperationResult.Success("Trade session opened.", before, Revision, session: working);
        }

        public TradeOperationResult SubmitOffer(string sessionId, TradeOfferData offer, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!TryGetSession(sessionId, out TradeSessionData session))
            {
                return Fail(TradeOperationCode.MissingSession, $"Trade session '{sessionId}' was not found.", preview);
            }

            if (!TryGetPolicy(session.tradePolicyId, out TradePolicyDefinition policy))
            {
                return Fail(TradeOperationCode.MissingPolicy, $"Trade policy '{session.tradePolicyId}' was not found.", preview);
            }

            if (!IsActiveSession(session.state))
            {
                return Fail(TradeOperationCode.InvalidState, $"Trade session '{sessionId}' is {session.state}.", preview);
            }

            if (offer == null)
            {
                return Fail(TradeOperationCode.InvalidRequest, "Trade offer data is required.", preview);
            }

            TradeOfferData working = offer.Clone();
            working.tradeSessionId = session.tradeSessionId;
            working.state = TradeOfferState.Submitted;
            working.sequence = working.sequence <= 0 ? NextOfferSequence(session.tradeSessionId) : working.sequence;
            working.createdWorldTime = Math.Max(session.createdWorldTime, working.createdWorldTime);
            if (working.expiresWorldTime < 0d && policy.DefaultOfferDuration > 0d)
            {
                working.expiresWorldTime = working.createdWorldTime + policy.DefaultOfferDuration;
            }

            if (!string.IsNullOrWhiteSpace(transactionId) && IsDuplicate(transactionId, "submit-offer", working.offerId, out TradeOperationResult duplicate))
            {
                return duplicate;
            }

            if (!ValidateOffer(session, policy, working, out TradeOperationCode code, out string failure))
            {
                return Fail(code, failure, preview);
            }

            if (offersById.ContainsKey(working.offerId))
            {
                return Fail(TradeOperationCode.InvalidRequest, $"Trade offer '{working.offerId}' already exists.", preview);
            }

            if (session.negotiationRound >= policy.MaximumOfferRounds)
            {
                return Fail(TradeOperationCode.PolicyViolation, $"Trade policy '{policy.Id}' allows only {policy.MaximumOfferRounds} offer rounds.", preview);
            }

            if (preview)
            {
                return TradeOperationResult.Success("Trade offer preview succeeded.", before, before, preview: true, session: session, offer: working);
            }

            SupersedeActiveOffer(session);
            offersById.Add(working.offerId, working);
            session.activeOfferId = working.offerId;
            session.offerHistoryIds = AddId(session.offerHistoryIds, working.offerId);
            session.negotiationRound++;
            session.state = TradeSessionState.AwaitingResponse;
            Touch(session, working.createdWorldTime);
            Revision++;
            Remember(transactionId, "submit-offer", working.offerId);
            return TradeOperationResult.Success("Trade offer submitted.", before, Revision, session: session, offer: working);
        }

        public TradeOperationResult SubmitCounteroffer(string sessionId, string parentOfferId, TradeOfferData counteroffer, string transactionId = "", bool preview = false)
        {
            if (counteroffer == null)
            {
                return Fail(TradeOperationCode.InvalidRequest, "Counteroffer data is required.", preview);
            }

            if (!offersById.TryGetValue(parentOfferId ?? string.Empty, out TradeOfferData parent))
            {
                return Fail(TradeOperationCode.MissingOffer, $"Parent offer '{parentOfferId}' was not found.", preview);
            }

            TradeOfferData working = counteroffer.Clone();
            working.parentOfferId = parent.offerId;
            TradeOperationResult result = SubmitOffer(sessionId, working, transactionId, preview);
            if (result.Succeeded && !preview && offersById.TryGetValue(parent.offerId, out TradeOfferData storedParent) && storedParent.state == TradeOfferState.Submitted)
            {
                storedParent.state = TradeOfferState.Superseded;
                storedParent.revision++;
            }

            return result;
        }

        public TradeOperationResult AcceptOffer(string sessionId, string offerId, string respondingParticipantId, double worldTime, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!TryGetSession(sessionId, out TradeSessionData session))
            {
                return Fail(TradeOperationCode.MissingSession, $"Trade session '{sessionId}' was not found.", preview);
            }

            if (!offersById.TryGetValue(offerId ?? string.Empty, out TradeOfferData offer))
            {
                return Fail(TradeOperationCode.MissingOffer, $"Trade offer '{offerId}' was not found.", preview);
            }

            if (!string.IsNullOrWhiteSpace(transactionId) && IsDuplicate(transactionId, "accept-offer", offerId, out TradeOperationResult duplicate))
            {
                return duplicate;
            }

            if (!string.Equals(session.activeOfferId, offer.offerId, StringComparison.Ordinal) || offer.state != TradeOfferState.Submitted)
            {
                return Fail(TradeOperationCode.InvalidState, "Only the current submitted offer may be accepted.", preview);
            }

            if (!ParticipantMayRespond(offer, respondingParticipantId))
            {
                return Fail(TradeOperationCode.Unauthorized, $"Participant '{respondingParticipantId}' is not authorized to accept offer '{offerId}'.", preview);
            }

            if (IsExpired(offer.expiresWorldTime, worldTime))
            {
                return Fail(TradeOperationCode.InvalidState, $"Trade offer '{offerId}' is expired.", preview);
            }

            if (preview)
            {
                TradeOfferData previewOffer = offer.Clone();
                TradeSessionData previewSession = session.Clone();
                previewOffer.state = TradeOfferState.Accepted;
                previewSession.state = TradeSessionState.AcceptedPendingExecution;
                previewSession.acceptedOfferId = previewOffer.offerId;
                return TradeOperationResult.Success("Trade offer acceptance preview succeeded.", before, before, preview: true, session: previewSession, offer: previewOffer);
            }

            offer.state = TradeOfferState.Accepted;
            offer.revision++;
            session.state = TradeSessionState.AcceptedPendingExecution;
            session.acceptedOfferId = offer.offerId;
            Touch(session, worldTime);
            Revision++;
            Remember(transactionId, "accept-offer", offerId);
            return TradeOperationResult.Success("Trade offer accepted.", before, Revision, session: session, offer: offer);
        }

        public TradeOperationResult RejectOffer(string sessionId, string offerId, string respondingParticipantId, double worldTime, string transactionId = "", bool preview = false)
        {
            return TerminalOfferOperation(sessionId, offerId, respondingParticipantId, TradeOfferState.Rejected, TradeSessionState.Rejected, worldTime, transactionId, "reject-offer", preview);
        }

        public TradeOperationResult WithdrawOffer(string sessionId, string offerId, string proposingParticipantId, double worldTime, string transactionId = "", bool preview = false)
        {
            if (offersById.TryGetValue(offerId ?? string.Empty, out TradeOfferData offer) && !string.Equals(offer.proposingParticipantId, proposingParticipantId ?? string.Empty, StringComparison.Ordinal))
            {
                return Fail(TradeOperationCode.Unauthorized, $"Participant '{proposingParticipantId}' did not propose offer '{offerId}'.", preview);
            }

            return TerminalOfferOperation(sessionId, offerId, proposingParticipantId, TradeOfferState.Withdrawn, TradeSessionState.Withdrawn, worldTime, transactionId, "withdraw-offer", preview, requireResponder: false);
        }

        public TradeOperationResult ExpireOffer(string sessionId, string offerId, double worldTime, string transactionId = "", bool preview = false)
        {
            return TerminalOfferOperation(sessionId, offerId, string.Empty, TradeOfferState.Expired, TradeSessionState.Expired, worldTime, transactionId, "expire-offer", preview, requireResponder: false);
        }

        public TradeOperationResult ReserveOfferAssets(string offerId, EconomyRuntime economy, ItemInstanceIdentityRuntime items, double worldTime, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!offersById.TryGetValue(offerId ?? string.Empty, out TradeOfferData offer))
            {
                return Fail(TradeOperationCode.MissingOffer, $"Trade offer '{offerId}' was not found.", preview);
            }

            if (!TryGetSession(offer.tradeSessionId, out TradeSessionData session))
            {
                return Fail(TradeOperationCode.MissingSession, $"Trade session '{offer.tradeSessionId}' was not found.", preview);
            }

            if (!TryGetPolicy(session.tradePolicyId, out TradePolicyDefinition policy))
            {
                return Fail(TradeOperationCode.MissingPolicy, $"Trade policy '{session.tradePolicyId}' was not found.", preview);
            }

            List<EconomyReservationData> createdMoneyReservations = new List<EconomyReservationData>();
            List<TradeItemReservationData> createdItemReservations = new List<TradeItemReservationData>();
            foreach (TradeAssetEntryData asset in EnumerateStoredAssets(offer))
            {
                if (asset.IsMoneyAsset)
                {
                    if (economy == null)
                    {
                        return Fail(TradeOperationCode.MissingRuntime, "Economy runtime is required for monetary reservations.", preview);
                    }

                    string reservationId = string.IsNullOrWhiteSpace(asset.monetaryReservationId) ? StableId("trade-money-reservation", offer.offerId, asset.assetEntryId) : asset.monetaryReservationId;
                    EconomyOperationResult reservation = economy.Reserve(reservationId, asset.sourceAccountId, new MoneyAmount(asset.currencyId, asset.units), offer.offerId, worldTime, offer.expiresWorldTime, preview);
                    if (!reservation.Succeeded)
                    {
                        return Fail(TradeOperationCode.ReservationUnavailable, reservation.Message, preview);
                    }

                    if (!preview)
                    {
                        asset.monetaryReservationId = reservationId;
                    }
                    createdMoneyReservations.Add(reservation.Reservation);
                }

                if (asset.IsItemAsset)
                {
                    if (items == null || !items.TryGetSnapshot(asset.itemInstanceId, out ItemInstanceSnapshot snapshot))
                    {
                        return Fail(TradeOperationCode.MissingItem, $"Item instance '{asset.itemInstanceId}' was not found.", preview);
                    }

                    if (HasActiveItemReservation(asset.itemInstanceId, asset.quantity, offer.offerId))
                    {
                        return Fail(TradeOperationCode.ReservationUnavailable, $"Item instance '{asset.itemInstanceId}' is already reserved by another active trade offer.", preview);
                    }

                    TradeItemReservationData reservation = new TradeItemReservationData
                    {
                        reservationId = StableId("trade-item-reservation", offer.offerId, asset.assetEntryId),
                        tradeSessionId = offer.tradeSessionId,
                        offerId = offer.offerId,
                        assetEntryId = asset.assetEntryId,
                        itemInstanceId = asset.itemInstanceId,
                        quantity = Math.Max(1, asset.quantity),
                        state = TradeReservationState.Active,
                        createdWorldTime = Math.Max(0d, worldTime),
                        expiresWorldTime = offer.expiresWorldTime,
                        expectedItemRevision = snapshot.Revision
                    };

                    if (!preview)
                    {
                        asset.itemReservationId = reservation.reservationId;
                    }
                    createdItemReservations.Add(reservation);
                }
            }

            if (preview)
            {
                return TradeOperationResult.Success("Trade reservation preview succeeded.", before, before, preview: true, session: session, offer: offer);
            }

            foreach (TradeItemReservationData reservation in createdItemReservations)
            {
                itemReservationsById[reservation.reservationId] = reservation;
            }

            if (createdItemReservations.Count > 0 || createdMoneyReservations.Count > 0)
            {
                offer.revision++;
                Revision++;
            }

            Remember(transactionId, "reserve-offer", offerId);
            return TradeOperationResult.Success("Trade assets reserved.", before, Revision, session: session, offer: offer);
        }

        public TradeOperationResult ReleaseOfferReservations(string offerId, EconomyRuntime economy, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!offersById.TryGetValue(offerId ?? string.Empty, out TradeOfferData offer))
            {
                return Fail(TradeOperationCode.MissingOffer, $"Trade offer '{offerId}' was not found.", preview);
            }

            if (preview)
            {
                return TradeOperationResult.Success("Trade reservation release preview succeeded.", before, before, preview: true, offer: offer);
            }

            foreach (TradeItemReservationData reservation in itemReservationsById.Values.Where(item => item.offerId == offerId && item.state == TradeReservationState.Active).ToArray())
            {
                reservation.state = TradeReservationState.Released;
                reservation.revision++;
            }

            foreach (TradeAssetEntryData asset in offer.AllAssets.Where(asset => asset.IsMoneyAsset && !string.IsNullOrWhiteSpace(asset.monetaryReservationId)))
            {
                EconomyOperationResult release = economy?.ReleaseReservation(asset.monetaryReservationId, $"{transactionId}.{asset.assetEntryId}");
                if (release != null && !release.Succeeded && release.Code != EconomyResultCode.ReservationUnavailable)
                {
                    return Fail(TradeOperationCode.ReservationUnavailable, release.Message, preview);
                }
            }

            Revision++;
            Remember(transactionId, "release-offer", offerId);
            return TradeOperationResult.Success("Trade reservations released.", before, Revision, offer: offer);
        }

        public TradeOperationResult ValueAsset(string valuationId, string sessionId, string offerId, string evaluatingParticipantId, TradeAssetEntryData asset, EconomyRuntime economy, MarketRuntime markets, ItemInstanceIdentityRuntime items, bool privilegedHiddenFactors, double worldTime, bool preview = false, ItemQualityAffixRuntime qualityRuntime = null, ItemDurabilityRuntime durabilityRuntime = null)
        {
            long before = Revision;
            if (asset == null)
            {
                return Fail(TradeOperationCode.InvalidAsset, "Trade valuation requires an asset.", preview);
            }

            if (!TryGetSession(sessionId, out TradeSessionData session) || !session.participants.Any(participant => participant.participantId == evaluatingParticipantId))
            {
                return Fail(TradeOperationCode.MissingParticipant, $"Participant '{evaluatingParticipantId}' was not found in session '{sessionId}'.", preview);
            }

            long estimated = 0L;
            string currencyId = asset.currencyId;
            List<string> known = new List<string>();
            List<string> unknown = new List<string>();
            if (asset.IsMoneyAsset)
            {
                estimated = asset.units;
                known.Add("money");
            }
            else
            {
                if (markets != null && !string.IsNullOrWhiteSpace(asset.quoteId) && markets.TryGetQuote(asset.quoteId, out MerchantQuoteRecordData quote))
                {
                    estimated = quote.finalAmountUnits;
                    currencyId = quote.currencyId;
                    known.Add("quote");
                }
                else if (markets != null && !string.IsNullOrWhiteSpace(asset.marketPriceId))
                {
                    known.Add("market-price");
                }
                else
                {
                    estimated = Math.Max(1L, asset.quantity) * 100L;
                    currencyId = string.IsNullOrWhiteSpace(currencyId) ? "currency.gold" : currencyId;
                    unknown.Add("market-reference");
                }

                if (items != null && items.TryGetSnapshot(asset.itemInstanceId, out ItemInstanceSnapshot snapshot))
                {
                    if (qualityRuntime != null && qualityRuntime.TryGetQualityForItem(asset.itemInstanceId, out ItemQualitySnapshot quality) && quality.OverallQuality >= 0.7f)
                    {
                        estimated = checked(estimated * 12500L / 10000L);
                        known.Add("quality");
                    }

                    if (durabilityRuntime != null && durabilityRuntime.TryGetDurabilityForItem(asset.itemInstanceId, out ItemDurabilitySnapshot durability) && durability.NormalizedDurability < 0.75f)
                    {
                        estimated = Math.Max(1L, estimated * 7500L / 10000L);
                        known.Add("condition");
                    }

                    if (!privilegedHiddenFactors && !string.IsNullOrWhiteSpace(snapshot.MakerMark))
                    {
                        unknown.Add("hidden-maker");
                    }
                    else if (privilegedHiddenFactors && !string.IsNullOrWhiteSpace(snapshot.MakerMark))
                    {
                        estimated = checked(estimated * 11000L / 10000L);
                        known.Add("hidden-maker");
                    }
                }
            }

            TradeValuationRecordData valuation = new TradeValuationRecordData
            {
                valuationId = string.IsNullOrWhiteSpace(valuationId) ? StableId("trade-valuation", sessionId, offerId, evaluatingParticipantId, asset.assetEntryId, worldTime.ToString("0.###")) : valuationId,
                tradeSessionId = sessionId,
                offerId = offerId ?? string.Empty,
                evaluatingParticipantId = evaluatingParticipantId,
                assetEntryId = asset.assetEntryId,
                currencyId = currencyId,
                estimatedUnits = Math.Max(0L, estimated),
                minimumEstimatedUnits = Math.Max(0L, estimated * 9L / 10L),
                maximumEstimatedUnits = Math.Max(0L, estimated * 11L / 10L),
                confidence = unknown.Count == 0 ? 9000 : 6500,
                knownFactors = known.ToArray(),
                unknownFactors = unknown.ToArray(),
                worldTime = Math.Max(0d, worldTime),
                expiresWorldTime = worldTime + 30d,
                privilegedHiddenFactors = privilegedHiddenFactors
            };

            if (!ValidateValuation(valuation, out TradeOperationCode code, out string failure))
            {
                return Fail(code, failure, preview);
            }

            if (preview)
            {
                return TradeOperationResult.Success("Trade valuation preview succeeded.", before, before, preview: true, valuation: valuation);
            }

            valuationsById[valuation.valuationId] = valuation;
            Revision++;
            return TradeOperationResult.Success("Trade valuation recorded.", before, Revision, valuation: valuation);
        }

        public TradeOperationResult ExecuteAcceptedDeal(string sessionId, EconomyRuntime economy, ItemInstanceIdentityRuntime items, MarketRuntime markets, DefinitionRegistry definitionRegistry, double worldTime, string transactionId = "", string injectFailureStage = "", bool preview = false)
        {
            long before = Revision;
            if (!TryGetSession(sessionId, out TradeSessionData session))
            {
                return Fail(TradeOperationCode.MissingSession, $"Trade session '{sessionId}' was not found.", preview);
            }

            if (session.state == TradeSessionState.Completed && tradeRecordsById.Values.Any(record => record.tradeSessionId == sessionId))
            {
                TradeRecordData existing = tradeRecordsById.Values.First(record => record.tradeSessionId == sessionId);
                return TradeOperationResult.Success("Trade execution was already completed.", before, before, duplicate: true, session: session, tradeRecord: existing);
            }

            if (!offersById.TryGetValue(session.acceptedOfferId ?? string.Empty, out TradeOfferData offer))
            {
                return Fail(TradeOperationCode.MissingOffer, "Accepted trade offer is missing.", preview);
            }

            if (offer.state != TradeOfferState.Accepted || session.state != TradeSessionState.AcceptedPendingExecution)
            {
                return Fail(TradeOperationCode.InvalidState, "Trade must be accepted before execution.", preview);
            }

            if (!ValidateExecutionPlan(session, offer, economy, items, markets, worldTime, out TradeOperationCode code, out string failure))
            {
                return Fail(code, failure, preview);
            }

            if (preview)
            {
                return TradeOperationResult.Success("Trade execution preview succeeded.", before, before, preview: true, session: session, offer: offer);
            }

            EconomyRuntimeSaveData economyRollback = economy?.CreateSaveData();
            ItemInstanceRuntimeSaveData itemRollback = items?.CreateSaveData();
            TradeRuntimeSaveData tradeRollback = CreateSaveData();
            try
            {
                if (string.Equals(injectFailureStage, "before-item-transfer", StringComparison.Ordinal))
                {
                    return Fail(TradeOperationCode.ExecutionFailed, "Injected item-transfer failure.", preview);
                }

                session.state = TradeSessionState.Executing;
                session.revision++;
                List<string> itemTransferRefs = new List<string>();
                List<string> transactionIds = new List<string>();
                foreach (TradeAssetEntryData asset in offer.AllAssets.OrderBy(asset => asset.assetEntryId, StringComparer.Ordinal))
                {
                    TradeParticipantData destination = session.participants.First(participant => participant.participantId == asset.destinationParticipantId);
                    if (asset.assetKind == TradeAssetKind.ItemInstance || asset.assetKind == TradeAssetKind.PhysicalCurrency)
                    {
                        ItemInstanceOperationResult ownership = items.TransferOwnership(asset.itemInstanceId, ItemOwnershipKind.PersonOwned, destination.subjectId);
                        if (!ownership.Succeeded) throw new InvalidOperationException(ownership.Message);
                        ItemInstanceOperationResult custody = items.TransferCustody(asset.itemInstanceId, destination.subjectId);
                        if (!custody.Succeeded) throw new InvalidOperationException(custody.Message);
                        ItemInstanceOperationResult location = items.SetInventoryLocation(asset.itemInstanceId, string.IsNullOrWhiteSpace(destination.receivingInventoryId) ? destination.subjectId : destination.receivingInventoryId);
                        if (!location.Succeeded) throw new InvalidOperationException(location.Message);
                        itemTransferRefs.Add($"item-transfer.{offer.offerId}.{asset.assetEntryId}");
                    }
                    else if (asset.assetKind == TradeAssetKind.StackQuantity)
                    {
                        ItemInstanceOperationResult stack = items.TransferStackQuantity(asset.itemInstanceId, asset.quantity, destination.subjectId, destination.subjectId, string.IsNullOrWhiteSpace(destination.receivingInventoryId) ? destination.subjectId : destination.receivingInventoryId, asset.createdSplitItemInstanceId);
                        if (!stack.Succeeded) throw new InvalidOperationException(stack.Message);
                        itemTransferRefs.Add($"stack-transfer.{offer.offerId}.{asset.assetEntryId}");
                    }
                    else if (asset.assetKind == TradeAssetKind.Money)
                    {
                        if (string.Equals(injectFailureStage, "before-payment", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Injected payment failure.");
                        }

                        EconomyOperationResult payment = economy.Transfer($"{transactionId}.{asset.assetEntryId}", asset.sourceAccountId, asset.destinationAccountId, new MoneyAmount(asset.currencyId, asset.units), EconomyTransactionKind.Payment, asset.monetaryReservationId, actorId: asset.sourceParticipantId, priceSnapshotId: FirstNonEmpty(asset.quoteId, asset.marketPriceId), worldTime: worldTime);
                        if (!payment.Succeeded) throw new InvalidOperationException(payment.Message);
                        transactionIds.Add(payment.Transaction.TransactionId);
                        if (string.Equals(injectFailureStage, "after-money", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Injected post-payment failure.");
                        }
                    }
                }

                if (string.Equals(injectFailureStage, "receipt", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Injected receipt failure.");
                }

                string recordId = StableId("trade-record", session.tradeSessionId, offer.offerId);
                TradeRecordData record = new TradeRecordData
                {
                    tradeRecordId = recordId,
                    tradeSessionId = session.tradeSessionId,
                    acceptedOfferId = offer.offerId,
                    participantIds = session.participants.Select(participant => participant.participantId).ToArray(),
                    exchangedBundles = offer.bundles.Select(bundle => bundle.Clone()).ToList(),
                    economyTransactionIds = transactionIds.ToArray(),
                    itemTransferReferences = itemTransferRefs.ToArray(),
                    marketPriceIds = CloneIds(offer.marketPriceIds),
                    quoteIds = CloneIds(offer.merchantQuoteIds),
                    valuationIds = CloneIds(offer.valuationIds),
                    executionWorldTime = Math.Max(0d, worldTime),
                    accessPolicyId = offer.accessPolicyId,
                    provenance = $"offer:{offer.offerId}",
                    revision = 1L
                };

                TradeReceiptData receipt = new TradeReceiptData
                {
                    receiptId = StableId("trade-receipt", recordId),
                    tradeRecordId = recordId,
                    issuerParticipantId = offer.proposingParticipantId,
                    recipientParticipantId = offer.respondingParticipantIds.FirstOrDefault() ?? string.Empty,
                    receivedAssetEntryIds = offer.AllAssets.Select(asset => asset.assetEntryId).ToArray(),
                    currencyId = offer.AllAssets.FirstOrDefault(asset => asset.IsMoneyAsset)?.currencyId ?? string.Empty,
                    moneyPaidUnits = offer.AllAssets.Where(asset => asset.IsMoneyAsset).Sum(asset => asset.units),
                    quoteIds = CloneIds(offer.merchantQuoteIds),
                    worldTime = Math.Max(0d, worldTime),
                    accessPolicyId = offer.accessPolicyId,
                    provenance = $"trade:{recordId}",
                    revision = 1L
                };

                tradeRecordsById.Add(record.tradeRecordId, record);
                receiptsById.Add(receipt.receiptId, receipt);
                MarkOfferReservationsCommitted(offer.offerId);
                offer.state = TradeOfferState.Accepted;
                offer.revision++;
                session.state = TradeSessionState.Completed;
                Touch(session, worldTime);
                Revision++;
                Remember(transactionId, "execute-deal", session.tradeSessionId);
                return TradeOperationResult.Success("Trade executed atomically.", before, Revision, session: session, offer: offer, tradeRecord: record, receipt: receipt);
            }
            catch (Exception exception)
            {
                if (economyRollback != null && economy != null)
                {
                    economy.RestoreFromSaveData(economyRollback, definitionRegistry ?? registry);
                }

                if (itemRollback != null && items != null)
                {
                    items.RestoreFromSaveData(itemRollback, definitionRegistry ?? registry);
                }

                RestoreRuntimeState(tradeRollback);
                return Fail(TradeOperationCode.ExecutionFailed, exception.Message, preview);
            }
        }

        /// <summary>
        /// Commits the audit record for a deal whose assets were settled by another authoritative
        /// coordinator. This is used for aggregate commodity pools and scene inventories that are
        /// intentionally outside ItemInstanceIdentityRuntime, while preserving the normal
        /// session/offer/policy lifecycle and replay protection.
        /// </summary>
        public TradeOperationResult RecordExternallySettledDeal(
            string sessionId,
            IReadOnlyList<string> economyTransactionIds,
            IReadOnlyList<string> itemTransferReferences,
            double worldTime,
            string transactionId = "",
            bool preview = false)
        {
            long before = Revision;
            if (!TryGetSession(sessionId, out TradeSessionData session))
            {
                return Fail(TradeOperationCode.MissingSession, $"Trade session '{sessionId}' was not found.", preview);
            }

            if (session.state == TradeSessionState.Completed
                && tradeRecordsById.Values.FirstOrDefault(record => string.Equals(record.tradeSessionId, sessionId, StringComparison.Ordinal)) is TradeRecordData completed)
            {
                return TradeOperationResult.Success("External trade settlement was already recorded.", before, before, duplicate: true,
                    session: session, tradeRecord: completed,
                    receipt: receiptsById.Values.FirstOrDefault(receipt => string.Equals(receipt.tradeRecordId, completed.tradeRecordId, StringComparison.Ordinal)));
            }

            if (!offersById.TryGetValue(session.acceptedOfferId ?? string.Empty, out TradeOfferData offer)
                || offer.state != TradeOfferState.Accepted
                || session.state != TradeSessionState.AcceptedPendingExecution)
            {
                return Fail(TradeOperationCode.InvalidState, "Trade must have an accepted offer before external settlement is recorded.", preview);
            }

            string replayId = string.IsNullOrWhiteSpace(transactionId) ? $"external-settlement.{sessionId}" : transactionId.Trim();
            if (!preview && IsDuplicate(replayId, "external-settlement", sessionId, out TradeOperationResult duplicate))
            {
                return duplicate;
            }

            string[] transactionIds = CloneIds(economyTransactionIds?.ToArray());
            string[] transferReferences = CloneIds(itemTransferReferences?.ToArray());
            if (transactionIds.Length == 0 && transferReferences.Length == 0)
            {
                return Fail(TradeOperationCode.InvalidRequest, "External settlement requires a currency transaction or asset-transfer reference.", preview);
            }

            string recordId = StableId("trade-record", session.tradeSessionId, offer.offerId);
            TradeRecordData record = new TradeRecordData
            {
                tradeRecordId = recordId,
                tradeSessionId = session.tradeSessionId,
                acceptedOfferId = offer.offerId,
                participantIds = session.participants.Select(participant => participant.participantId).ToArray(),
                exchangedBundles = offer.bundles.Select(bundle => bundle.Clone()).ToList(),
                economyTransactionIds = transactionIds,
                itemTransferReferences = transferReferences,
                marketPriceIds = CloneIds(offer.marketPriceIds),
                quoteIds = CloneIds(offer.merchantQuoteIds),
                valuationIds = CloneIds(offer.valuationIds),
                executionWorldTime = Math.Max(0d, worldTime),
                accessPolicyId = offer.accessPolicyId,
                provenance = $"external-settlement:{offer.offerId}",
                revision = 1L
            };
            TradeReceiptData receipt = new TradeReceiptData
            {
                receiptId = StableId("trade-receipt", recordId),
                tradeRecordId = recordId,
                issuerParticipantId = offer.proposingParticipantId,
                recipientParticipantId = offer.respondingParticipantIds.FirstOrDefault() ?? string.Empty,
                receivedAssetEntryIds = offer.AllAssets.Select(asset => asset.assetEntryId).ToArray(),
                currencyId = offer.AllAssets.FirstOrDefault(asset => asset.IsMoneyAsset)?.currencyId ?? string.Empty,
                moneyPaidUnits = offer.AllAssets.Where(asset => asset.IsMoneyAsset).Sum(asset => asset.units),
                quoteIds = CloneIds(offer.merchantQuoteIds),
                worldTime = Math.Max(0d, worldTime),
                accessPolicyId = offer.accessPolicyId,
                provenance = $"trade:{recordId}",
                revision = 1L
            };

            if (preview)
            {
                return TradeOperationResult.Success("External settlement preview succeeded.", before, before, preview: true,
                    session: session, offer: offer, tradeRecord: record, receipt: receipt);
            }

            tradeRecordsById.Add(record.tradeRecordId, record);
            receiptsById.Add(receipt.receiptId, receipt);
            MarkOfferReservationsCommitted(offer.offerId);
            session.state = TradeSessionState.Completed;
            Touch(session, worldTime);
            Revision++;
            Remember(replayId, "external-settlement", sessionId);
            return TradeOperationResult.Success("Externally settled trade recorded.", before, Revision,
                session: session, offer: offer, tradeRecord: record, receipt: receipt);
        }

        public bool TryGetSession(string sessionId, out TradeSessionData session)
        {
            if (!string.IsNullOrWhiteSpace(sessionId) && sessionsById.TryGetValue(sessionId, out TradeSessionData found))
            {
                session = found;
                return true;
            }

            session = null;
            return false;
        }

        public bool TryGetOffer(string offerId, out TradeOfferData offer)
        {
            if (!string.IsNullOrWhiteSpace(offerId) && offersById.TryGetValue(offerId, out TradeOfferData found))
            {
                offer = found.Clone();
                return true;
            }

            offer = null;
            return false;
        }

        public TradeProjection<TradeSessionData> GetSessionProjection(string sessionId, InformationAccessRuntime access, InformationAccessContext context, string policyId = "")
        {
            if (!sessionsById.TryGetValue(sessionId ?? string.Empty, out TradeSessionData session))
            {
                return new TradeProjection<TradeSessionData>(null, null, false, true, Array.Empty<string>(), TradeInformationSubject.ProtectedFields, $"Trade session '{sessionId}' was not found.");
            }

            return Project(session.Clone(), session.CreateInformationSubject(), access, context, policyId, RedactSession);
        }

        public TradeProjection<TradeOfferData> GetOfferProjection(string offerId, InformationAccessRuntime access, InformationAccessContext context, string policyId = "")
        {
            if (!offersById.TryGetValue(offerId ?? string.Empty, out TradeOfferData offer))
            {
                return new TradeProjection<TradeOfferData>(null, null, false, true, Array.Empty<string>(), TradeInformationSubject.ProtectedFields, $"Trade offer '{offerId}' was not found.");
            }

            return Project(offer.Clone(), offer.CreateInformationSubject(), access, context, policyId, RedactOffer);
        }

        public TradeRuntimeSaveData CreateSaveData()
        {
            return new TradeRuntimeSaveData
            {
                schemaVersion = TradeRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId ?? string.Empty,
                revision = Revision,
                sessions = sessionsById.Values.OrderBy(item => item.tradeSessionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                offers = offersById.Values.OrderBy(item => item.tradeSessionId, StringComparer.Ordinal).ThenBy(item => item.sequence).ThenBy(item => item.offerId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                itemReservations = itemReservationsById.Values.OrderBy(item => item.reservationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                valuations = valuationsById.Values.OrderBy(item => item.valuationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                tradeRecords = tradeRecordsById.Values.OrderBy(item => item.tradeRecordId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                receipts = receiptsById.Values.OrderBy(item => item.receiptId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                processedCommands = processedByTransactionId.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public TradeOperationResult RestoreFromSaveData(TradeRuntimeSaveData saveData, DefinitionRegistry definitionRegistry)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, out string failure))
            {
                return Fail(TradeOperationCode.RestoreFailed, failure, preview: false);
            }

            RestoreRuntimeState(saveData);
            return TradeOperationResult.Success("Trade runtime restored.", Revision, Revision);
        }

        private void RestoreRuntimeState(TradeRuntimeSaveData saveData)
        {
            sessionsById.Clear();
            offersById.Clear();
            itemReservationsById.Clear();
            valuationsById.Clear();
            tradeRecordsById.Clear();
            receiptsById.Clear();
            processedByTransactionId.Clear();
            foreach (TradeSessionData item in saveData.sessions.Select(item => item.Clone()).OrderBy(item => item.tradeSessionId, StringComparer.Ordinal)) sessionsById.Add(item.tradeSessionId, item);
            foreach (TradeOfferData item in saveData.offers.Select(item => item.Clone()).OrderBy(item => item.offerId, StringComparer.Ordinal)) offersById.Add(item.offerId, item);
            foreach (TradeItemReservationData item in saveData.itemReservations.Select(item => item.Clone()).OrderBy(item => item.reservationId, StringComparer.Ordinal)) itemReservationsById.Add(item.reservationId, item);
            foreach (TradeValuationRecordData item in saveData.valuations.Select(item => item.Clone()).OrderBy(item => item.valuationId, StringComparer.Ordinal)) valuationsById.Add(item.valuationId, item);
            foreach (TradeRecordData item in saveData.tradeRecords.Select(item => item.Clone()).OrderBy(item => item.tradeRecordId, StringComparer.Ordinal)) tradeRecordsById.Add(item.tradeRecordId, item);
            foreach (TradeReceiptData item in saveData.receipts.Select(item => item.Clone()).OrderBy(item => item.receiptId, StringComparer.Ordinal)) receiptsById.Add(item.receiptId, item);
            foreach (TradeProcessedCommandData item in saveData.processedCommands.Select(item => item.Clone()).OrderBy(item => item.transactionId, StringComparer.Ordinal)) processedByTransactionId.Add(item.transactionId, item);
            Revision = Math.Max(0L, saveData.revision);
            worldId = saveData.worldId ?? worldId;
        }

        public static bool ValidateSaveData(TradeRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Trade save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != TradeRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported trade schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> sessionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TradeSessionData session in saveData.sessions ?? new List<TradeSessionData>())
            {
                if (string.IsNullOrWhiteSpace(session.tradeSessionId) || !sessionIds.Add(session.tradeSessionId))
                {
                    failure = "Trade save contains a missing or duplicate session ID.";
                    return false;
                }

                if (registry != null && !registry.TryGet(session.tradePolicyId, out TradePolicyDefinition _))
                {
                    failure = $"Trade session '{session.tradeSessionId}' references missing policy '{session.tradePolicyId}'.";
                    return false;
                }
            }

            HashSet<string> offerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TradeOfferData offer in saveData.offers ?? new List<TradeOfferData>())
            {
                if (string.IsNullOrWhiteSpace(offer.offerId) || !offerIds.Add(offer.offerId))
                {
                    failure = "Trade save contains a missing or duplicate offer ID.";
                    return false;
                }

                if (!sessionIds.Contains(offer.tradeSessionId))
                {
                    failure = $"Trade offer '{offer.offerId}' references missing session '{offer.tradeSessionId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(offer.parentOfferId) && !offerIds.Contains(offer.parentOfferId) && !(saveData.offers ?? new List<TradeOfferData>()).Any(item => item.offerId == offer.parentOfferId))
                {
                    failure = $"Trade offer '{offer.offerId}' references missing parent offer '{offer.parentOfferId}'.";
                    return false;
                }
            }

            foreach (TradeSessionData session in saveData.sessions ?? new List<TradeSessionData>())
            {
                if (!string.IsNullOrWhiteSpace(session.activeOfferId) && !offerIds.Contains(session.activeOfferId))
                {
                    failure = $"Trade session '{session.tradeSessionId}' references missing active offer '{session.activeOfferId}'.";
                    return false;
                }
            }

            HashSet<string> recordIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TradeRecordData record in saveData.tradeRecords ?? new List<TradeRecordData>())
            {
                if (string.IsNullOrWhiteSpace(record.tradeRecordId) || !recordIds.Add(record.tradeRecordId))
                {
                    failure = "Trade save contains a missing or duplicate trade record ID.";
                    return false;
                }

                if (!sessionIds.Contains(record.tradeSessionId) || !offerIds.Contains(record.acceptedOfferId))
                {
                    failure = $"Trade record '{record.tradeRecordId}' references missing session or offer.";
                    return false;
                }
            }

            foreach (TradeReceiptData receipt in saveData.receipts ?? new List<TradeReceiptData>())
            {
                if (string.IsNullOrWhiteSpace(receipt.receiptId) || !recordIds.Contains(receipt.tradeRecordId))
                {
                    failure = $"Trade receipt '{receipt?.receiptId ?? string.Empty}' references missing trade record '{receipt?.tradeRecordId ?? string.Empty}'.";
                    return false;
                }
            }

            return true;
        }

        private bool ValidateExecutionPlan(TradeSessionData session, TradeOfferData offer, EconomyRuntime economy, ItemInstanceIdentityRuntime items, MarketRuntime markets, double worldTime, out TradeOperationCode code, out string failure)
        {
            code = TradeOperationCode.Success;
            failure = string.Empty;
            HashSet<string> itemKeys = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> debitKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (TradeAssetEntryData asset in offer.AllAssets)
            {
                if (!session.participants.Any(participant => participant.participantId == asset.sourceParticipantId) || !session.participants.Any(participant => participant.participantId == asset.destinationParticipantId))
                {
                    code = TradeOperationCode.MissingParticipant;
                    failure = $"Trade asset '{asset.assetEntryId}' references a missing source or destination participant.";
                    return false;
                }

                string quoteFailure = string.Empty;
                if (!string.IsNullOrWhiteSpace(asset.quoteId) && (markets == null || !markets.ValidateQuoteForExecution(asset.quoteId, worldTime, out quoteFailure)))
                {
                    code = TradeOperationCode.StaleQuote;
                    failure = quoteFailure;
                    return false;
                }

                if (asset.IsMoneyAsset)
                {
                    if (economy == null || !economy.TryGetAccount(asset.sourceAccountId, out EconomyAccountSnapshot account))
                    {
                        code = TradeOperationCode.MissingAccount;
                        failure = $"Source account '{asset.sourceAccountId}' was not found.";
                        return false;
                    }

                    if (!string.Equals(account.CurrencyId, asset.currencyId, StringComparison.Ordinal) || asset.units <= 0L)
                    {
                        code = TradeOperationCode.CurrencyMismatch;
                        failure = $"Money asset '{asset.assetEntryId}' has invalid currency or units.";
                        return false;
                    }

                    if (asset.expectedAccountRevision > 0L && account.Revision != asset.expectedAccountRevision)
                    {
                        code = TradeOperationCode.StaleAccount;
                        failure = $"Account '{asset.sourceAccountId}' revision is stale.";
                        return false;
                    }

                    if (account.AvailableUnits < asset.units && string.IsNullOrWhiteSpace(asset.monetaryReservationId))
                    {
                        code = TradeOperationCode.InsufficientFunds;
                        failure = $"Account '{asset.sourceAccountId}' has insufficient available funds.";
                        return false;
                    }

                    if (!debitKeys.Add($"{asset.sourceAccountId}:{asset.currencyId}:{asset.units}:{asset.assetEntryId}"))
                    {
                        code = TradeOperationCode.ValidationFailed;
                        failure = $"Duplicate account debit detected for asset '{asset.assetEntryId}'.";
                        return false;
                    }
                }
                else if (asset.IsItemAsset)
                {
                    if (items == null || !items.TryGetSnapshot(asset.itemInstanceId, out ItemInstanceSnapshot item))
                    {
                        code = TradeOperationCode.MissingItem;
                        failure = $"Item instance '{asset.itemInstanceId}' was not found.";
                        return false;
                    }

                    if (item.LifecycleState is ItemLifecycleState.Destroyed or ItemLifecycleState.Consumed)
                    {
                        code = TradeOperationCode.InvalidAsset;
                        failure = $"Item instance '{asset.itemInstanceId}' is {item.LifecycleState}.";
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(asset.itemDefinitionId) && !string.Equals(item.ItemDefinitionId, asset.itemDefinitionId, StringComparison.Ordinal))
                    {
                        code = TradeOperationCode.StaleItem;
                        failure = $"Item instance '{asset.itemInstanceId}' definition changed.";
                        return false;
                    }

                    if (asset.expectedItemRevision > 0L && item.Revision != asset.expectedItemRevision)
                    {
                        code = TradeOperationCode.StaleItem;
                        failure = $"Item instance '{asset.itemInstanceId}' revision is stale.";
                        return false;
                    }

                    if (asset.quantity <= 0 || asset.quantity > item.StackQuantity)
                    {
                        code = TradeOperationCode.InvalidQuantity;
                        failure = $"Item asset '{asset.assetEntryId}' quantity is invalid.";
                        return false;
                    }

                    if (!itemKeys.Add($"{asset.itemInstanceId}:{asset.assetEntryId}"))
                    {
                        code = TradeOperationCode.ValidationFailed;
                        failure = $"Item instance '{asset.itemInstanceId}' appears twice in the same trade.";
                        return false;
                    }

                    if (HasActiveItemReservation(asset.itemInstanceId, asset.quantity, offer.offerId) && !itemReservationsById.Values.Any(reservation => reservation.offerId == offer.offerId && reservation.itemInstanceId == asset.itemInstanceId && reservation.state == TradeReservationState.Active))
                    {
                        code = TradeOperationCode.ReservationUnavailable;
                        failure = $"Item instance '{asset.itemInstanceId}' is reserved by another offer.";
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool ValidateSession(TradeSessionData session, TradePolicyDefinition policy, out TradeOperationCode code, out string failure)
        {
            code = TradeOperationCode.Success;
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(session.tradeSessionId))
            {
                code = TradeOperationCode.InvalidRequest;
                failure = "Trade session ID is required.";
                return false;
            }

            if (session.participants == null || session.participants.Count < 2 || session.participants.Count > policy.MaximumParticipants)
            {
                code = TradeOperationCode.PolicyViolation;
                failure = $"Trade session must include 2..{policy.MaximumParticipants} participants.";
                return false;
            }

            HashSet<string> participantIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TradeParticipantData participant in session.participants)
            {
                if (participant == null || string.IsNullOrWhiteSpace(participant.participantId) || string.IsNullOrWhiteSpace(participant.subjectId) || !participantIds.Add(participant.participantId))
                {
                    code = TradeOperationCode.MissingParticipant;
                    failure = "Trade participants require unique participant and subject IDs.";
                    return false;
                }

                if (!policy.AllowedParticipantKinds.Contains(participant.kind))
                {
                    code = TradeOperationCode.PolicyViolation;
                    failure = $"Trade policy '{policy.Id}' does not allow participant kind {participant.kind}.";
                    return false;
                }
            }

            if (session.expiresWorldTime >= 0d && session.expiresWorldTime < session.createdWorldTime)
            {
                code = TradeOperationCode.InvalidRequest;
                failure = "Trade session expiration cannot be before creation.";
                return false;
            }

            return true;
        }

        private static bool ValidateOffer(TradeSessionData session, TradePolicyDefinition policy, TradeOfferData offer, out TradeOperationCode code, out string failure)
        {
            code = TradeOperationCode.Success;
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(offer.offerId))
            {
                code = TradeOperationCode.InvalidRequest;
                failure = "Trade offer ID is required.";
                return false;
            }

            if (!session.participants.Any(participant => participant.participantId == offer.proposingParticipantId))
            {
                code = TradeOperationCode.MissingParticipant;
                failure = $"Offer proposer '{offer.proposingParticipantId}' is not a trade participant.";
                return false;
            }

            if (offer.bundles == null || offer.bundles.Count == 0 || offer.AllAssets.Count == 0)
            {
                code = TradeOperationCode.InvalidAsset;
                failure = "Submitted trade offers must include at least one asset.";
                return false;
            }

            bool hasMoney = false;
            bool hasItem = false;
            foreach (TradeAssetEntryData asset in offer.AllAssets)
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.assetEntryId) || !policy.AllowsAssetKind(asset.assetKind))
                {
                    code = TradeOperationCode.PolicyViolation;
                    failure = "Trade offer contains an unsupported asset kind.";
                    return false;
                }

                if (asset.quantity <= 0)
                {
                    code = TradeOperationCode.InvalidQuantity;
                    failure = $"Trade asset '{asset.assetEntryId}' quantity must be positive.";
                    return false;
                }

                if (asset.IsMoneyAsset)
                {
                    hasMoney = true;
                    if (asset.units <= 0L || string.IsNullOrWhiteSpace(asset.currencyId) || !policy.AllowsCurrency(asset.currencyId))
                    {
                        code = TradeOperationCode.CurrencyMismatch;
                        failure = $"Trade asset '{asset.assetEntryId}' has invalid money details.";
                        return false;
                    }
                }

                if (asset.IsItemAsset)
                {
                    hasItem = true;
                    if (string.IsNullOrWhiteSpace(asset.itemInstanceId))
                    {
                        code = TradeOperationCode.MissingItem;
                        failure = $"Trade asset '{asset.assetEntryId}' must reference an item instance.";
                        return false;
                    }
                }

                if (asset.assetKind == TradeAssetKind.StackQuantity && !policy.PartialStackQuantitiesPermitted)
                {
                    code = TradeOperationCode.PolicyViolation;
                    failure = $"Trade policy '{policy.Id}' does not permit partial stack quantities.";
                    return false;
                }
            }

            if (hasMoney && hasItem && !policy.MixedMoneyAndItemTradesPermitted)
            {
                code = TradeOperationCode.PolicyViolation;
                failure = $"Trade policy '{policy.Id}' does not permit mixed money-and-item trades.";
                return false;
            }

            if (!hasMoney && hasItem && !policy.BarterPermitted)
            {
                code = TradeOperationCode.PolicyViolation;
                failure = $"Trade policy '{policy.Id}' does not permit barter.";
                return false;
            }

            if (offer.expiresWorldTime >= 0d && offer.expiresWorldTime < offer.createdWorldTime)
            {
                code = TradeOperationCode.InvalidRequest;
                failure = "Trade offer expiration cannot be before creation.";
                return false;
            }

            return true;
        }

        private static bool ValidateValuation(TradeValuationRecordData valuation, out TradeOperationCode code, out string failure)
        {
            code = TradeOperationCode.Success;
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(valuation.valuationId) || string.IsNullOrWhiteSpace(valuation.evaluatingParticipantId) || string.IsNullOrWhiteSpace(valuation.assetEntryId))
            {
                code = TradeOperationCode.InvalidRequest;
                failure = "Valuation ID, evaluator, and asset are required.";
                return false;
            }

            if (valuation.estimatedUnits < 0L || valuation.minimumEstimatedUnits < 0L || valuation.maximumEstimatedUnits < valuation.minimumEstimatedUnits || valuation.confidence < 0 || valuation.confidence > 10000)
            {
                code = TradeOperationCode.ValidationFailed;
                failure = "Trade valuation range or confidence is invalid.";
                return false;
            }

            return true;
        }

        private TradeOperationResult TerminalOfferOperation(string sessionId, string offerId, string participantId, TradeOfferState offerState, TradeSessionState sessionState, double worldTime, string transactionId, string operation, bool preview, bool requireResponder = true)
        {
            long before = Revision;
            if (!TryGetSession(sessionId, out TradeSessionData session)) return Fail(TradeOperationCode.MissingSession, $"Trade session '{sessionId}' was not found.", preview);
            if (!offersById.TryGetValue(offerId ?? string.Empty, out TradeOfferData offer)) return Fail(TradeOperationCode.MissingOffer, $"Trade offer '{offerId}' was not found.", preview);
            if (!string.IsNullOrWhiteSpace(transactionId) && IsDuplicate(transactionId, operation, offerId, out TradeOperationResult duplicate)) return duplicate;
            if (requireResponder && !ParticipantMayRespond(offer, participantId)) return Fail(TradeOperationCode.Unauthorized, $"Participant '{participantId}' cannot respond to offer '{offerId}'.", preview);
            if (offer.state != TradeOfferState.Submitted) return Fail(TradeOperationCode.InvalidState, $"Trade offer '{offerId}' is {offer.state}.", preview);
            if (preview) return TradeOperationResult.Success("Terminal offer operation preview succeeded.", before, before, preview: true, session: session, offer: offer);
            offer.state = offerState;
            offer.revision++;
            session.state = sessionState;
            if (string.Equals(session.activeOfferId, offer.offerId, StringComparison.Ordinal)) session.activeOfferId = string.Empty;
            Touch(session, worldTime);
            Revision++;
            Remember(transactionId, operation, offerId);
            return TradeOperationResult.Success("Trade offer moved to terminal state.", before, Revision, session: session, offer: offer);
        }

        private bool TryGetPolicy(string policyId, out TradePolicyDefinition policy)
        {
            if (registry != null && !string.IsNullOrWhiteSpace(policyId) && registry.TryGet(policyId, out TradePolicyDefinition found))
            {
                policy = found;
                return true;
            }

            policy = null;
            return false;
        }

        private bool HasActiveItemReservation(string itemInstanceId, int quantity, string sameOfferId)
        {
            return itemReservationsById.Values.Any(reservation =>
                reservation.state == TradeReservationState.Active
                && string.Equals(reservation.itemInstanceId, itemInstanceId ?? string.Empty, StringComparison.Ordinal)
                && !string.Equals(reservation.offerId, sameOfferId ?? string.Empty, StringComparison.Ordinal)
                && reservation.quantity > 0
                && quantity > 0);
        }

        private void MarkOfferReservationsCommitted(string offerId)
        {
            foreach (TradeItemReservationData reservation in itemReservationsById.Values.Where(item => item.offerId == offerId && item.state == TradeReservationState.Active))
            {
                reservation.state = TradeReservationState.Committed;
                reservation.revision++;
            }
        }

        private void SupersedeActiveOffer(TradeSessionData session)
        {
            if (!string.IsNullOrWhiteSpace(session.activeOfferId) && offersById.TryGetValue(session.activeOfferId, out TradeOfferData active) && active.state == TradeOfferState.Submitted)
            {
                active.state = TradeOfferState.Superseded;
                active.revision++;
            }
        }

        private static bool ParticipantMayRespond(TradeOfferData offer, string participantId)
        {
            return string.IsNullOrWhiteSpace(participantId) || (offer.respondingParticipantIds ?? Array.Empty<string>()).Contains(participantId, StringComparer.Ordinal);
        }

        private static bool IsActiveSession(TradeSessionState state)
        {
            return state == TradeSessionState.Proposed || state == TradeSessionState.Open || state == TradeSessionState.AwaitingResponse || state == TradeSessionState.AcceptedPendingExecution;
        }

        private static bool IsExpired(double expiresWorldTime, double worldTime) => expiresWorldTime >= 0d && worldTime > expiresWorldTime;

        private int NextOfferSequence(string sessionId)
        {
            return offersById.Values.Where(offer => offer.tradeSessionId == sessionId).Select(offer => offer.sequence).DefaultIfEmpty(0).Max() + 1;
        }

        private void Touch(TradeSessionData session, double worldTime)
        {
            session.lastActivityWorldTime = Math.Max(session.lastActivityWorldTime, Math.Max(0d, worldTime));
            session.revision++;
        }

        private static string[] AddId(string[] ids, string id)
        {
            return (ids ?? Array.Empty<string>()).Concat(new[] { id }).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private bool IsDuplicate(string transactionId, string operation, string subject, out TradeOperationResult duplicate)
        {
            duplicate = null;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return false;
            }

            if (processedByTransactionId.TryGetValue(transactionId, out TradeProcessedCommandData processed)
                && string.Equals(processed.operationKey, $"{operation}:{subject}", StringComparison.Ordinal))
            {
                duplicate = TradeOperationResult.Success("Duplicate trade command ignored.", Revision, Revision, duplicate: true);
                return true;
            }

            return false;
        }

        private void Remember(string transactionId, string operation, string subject)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            processedByTransactionId[transactionId] = new TradeProcessedCommandData
            {
                transactionId = transactionId,
                operationKey = $"{operation}:{subject}",
                code = TradeOperationCode.Success,
                resultId = subject ?? string.Empty,
                revision = Revision
            };
        }

        private TradeOperationResult Fail(TradeOperationCode code, string message, bool preview)
        {
            return TradeOperationResult.Failure(code, message, Revision, preview);
        }

        private static IEnumerable<TradeAssetEntryData> EnumerateStoredAssets(TradeOfferData offer)
        {
            if (offer?.bundles == null)
            {
                yield break;
            }

            foreach (TradeBundleData bundle in offer.bundles)
            {
                if (bundle?.assets == null)
                {
                    continue;
                }

                foreach (TradeAssetEntryData asset in bundle.assets)
                {
                    if (asset != null)
                    {
                        yield return asset;
                    }
                }
            }
        }

        private TradeProjection<TRecord> Project<TRecord>(TRecord record, InformationSubjectReferenceData subject, InformationAccessRuntime access, InformationAccessContext context, string policyId, Func<TRecord, RedactedInformationProjection, TRecord> redact)
        {
            if (access == null)
            {
                return new TradeProjection<TRecord>(record, null, false, false, TradeInformationSubject.ProtectedFields, Array.Empty<string>(), "Information access runtime is missing; privileged projection returned.");
            }

            InformationAccessContext request = InformationAccessProjectionUtility.BuildContext(context, subject, InformationAccessMode.Inspect, InformationAccessPurpose.Gameplay, TradeInformationSubject.ProtectedFields, policyId);
            RedactedInformationProjection projection = access.Project(request, TradeInformationSubject.ProtectedFields);
            bool denied = projection.Decision.Decision == InformationAccessDecisionKind.Denied || projection.Decision.Decision == InformationAccessDecisionKind.MissingAuthorization;
            bool redacted = denied || projection.Decision.Decision == InformationAccessDecisionKind.RedactedAccess || projection.Decision.Decision == InformationAccessDecisionKind.PartialAccess;
            TRecord projected = redacted ? redact(record, projection) : record;
            return new TradeProjection<TRecord>(denied ? default : projected, projection.Decision, redacted, denied, projection.Decision.AllowedDetails, projection.Decision.RedactedDetails.Concat(projection.Decision.HiddenDetails).ToArray(), projection.Decision.VisibleReason);
        }

        private static TradeSessionData RedactSession(TradeSessionData session, RedactedInformationProjection projection)
        {
            if (!InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.participants")) session.participants = new List<TradeParticipantData>();
            if (!InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.offer-history")) session.offerHistoryIds = Array.Empty<string>();
            if (!InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.reservations")) session.activeOfferId = string.Empty;
            return session;
        }

        private static TradeOfferData RedactOffer(TradeOfferData offer, RedactedInformationProjection projection)
        {
            if (!InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.assets")) offer.bundles = new List<TradeBundleData>();
            if (!InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.valuations")) offer.valuationIds = Array.Empty<string>();
            if (!InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.offer-history")) offer.parentOfferId = string.Empty;
            return offer;
        }

        private static string StableId(string prefix, params string[] parts)
        {
            string raw = string.Join(".", (parts ?? Array.Empty<string>()).Where(part => !string.IsNullOrWhiteSpace(part)).Select(Sanitize));
            return string.IsNullOrWhiteSpace(raw) ? prefix : $"{prefix}.{raw}";
        }

        private static string Sanitize(string value)
        {
            return new string((value ?? string.Empty).Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray()).Trim('-');
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values == null ? string.Empty : values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }
}
