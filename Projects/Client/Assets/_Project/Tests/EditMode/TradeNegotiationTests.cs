using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.Economy.Trading;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class TradeNegotiationTests
    {
        [Test]
        public void DefinitionsValidateConcreteTradePolicies()
        {
            Fixture fixture = Fixture.Create();
            DefinitionValidationReport report = new DefinitionValidationReport();

            foreach (IDefinitionCatalogValidationParticipant participant in fixture.Registry.DefinitionsById.Values.OfType<IDefinitionCatalogValidationParticipant>())
            {
                participant.ValidateCatalogDefinition(fixture.Registry.DefinitionsById, report);
            }

            Assert.That(report.HasErrors, Is.False, report.ToString());
            Assert.That(fixture.Policy.AllowsAssetKind(TradeAssetKind.Money), Is.True);
            Assert.That(fixture.Policy.AllowsAssetKind(TradeAssetKind.ItemInstance), Is.True);
        }

        [Test]
        public void FixedPricePurchaseTransfersMoneyItemRecordAndReceiptAtomically()
        {
            Fixture fixture = Fixture.Create();
            TradeOperationResult open = fixture.OpenSession("trade.purchase", out string sessionId);
            TradeOperationResult offer = fixture.Trades.SubmitOffer(sessionId, fixture.MoneyForSwordOffer(sessionId, "offer.purchase", 40L, quoteId: fixture.CreateQuote("quote.purchase")), "tx.offer");
            TradeOperationResult reserve = fixture.Trades.ReserveOfferAssets(offer.Offer.offerId, fixture.Economy, fixture.Items, 2d, "tx.reserve");
            TradeOperationResult accept = fixture.Trades.AcceptOffer(sessionId, offer.Offer.offerId, "participant.seller", 3d, "tx.accept");
            TradeOperationResult execute = fixture.Trades.ExecuteAcceptedDeal(sessionId, fixture.Economy, fixture.Items, fixture.Markets, fixture.Registry, 4d, "tx.execute");
            TradeOperationResult duplicate = fixture.Trades.ExecuteAcceptedDeal(sessionId, fixture.Economy, fixture.Items, fixture.Markets, fixture.Registry, 4d, "tx.execute");

            Assert.That(open.Succeeded, Is.True, open.Message);
            Assert.That(offer.Succeeded, Is.True, offer.Message);
            Assert.That(reserve.Succeeded, Is.True, reserve.Message);
            Assert.That(accept.Succeeded, Is.True, accept.Message);
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(fixture.Economy.TryGetAccount(fixture.BuyerAccount, out EconomyAccountSnapshot buyer), Is.True);
            Assert.That(fixture.Economy.TryGetAccount(fixture.SellerAccount, out EconomyAccountSnapshot seller), Is.True);
            Assert.That(buyer.BalanceUnits, Is.EqualTo(60L));
            Assert.That(seller.BalanceUnits, Is.EqualTo(40L));
            Assert.That(fixture.Items.TryGetSnapshot(fixture.SwordInstanceId, out ItemInstanceSnapshot sword), Is.True);
            Assert.That(sword.OwnerPersonId, Is.EqualTo(fixture.BuyerPersonId));
            Assert.That(execute.TradeRecord.tradeRecordId, Is.Not.Empty);
            Assert.That(execute.Receipt.receiptId, Is.Not.Empty);
            Assert.That(fixture.Trades.TradeRecordCount, Is.EqualTo(1));
            Assert.That(fixture.Trades.ReceiptCount, Is.EqualTo(1));
        }

        [Test]
        public void CounteroffersSupersedeParentsAndTerminalStatesAreExplicit()
        {
            Fixture fixture = Fixture.Create();
            fixture.OpenSession("trade.counter", out string sessionId);
            TradeOperationResult initial = fixture.Trades.SubmitOffer(sessionId, fixture.MoneyForSwordOffer(sessionId, "offer.initial", 30L), "tx.initial");
            TradeOperationResult counter = fixture.Trades.SubmitCounteroffer(sessionId, initial.Offer.offerId, fixture.MoneyForSwordOffer(sessionId, "offer.counter", 25L, proposer: "participant.seller", responder: "participant.buyer"), "tx.counter");
            TradeOperationResult reject = fixture.Trades.RejectOffer(sessionId, counter.Offer.offerId, "participant.buyer", 4d, "tx.reject");

            Assert.That(counter.Succeeded, Is.True, counter.Message);
            Assert.That(fixture.Trades.TryGetOffer(initial.Offer.offerId, out TradeOfferData parent), Is.True);
            Assert.That(parent.state, Is.EqualTo(TradeOfferState.Superseded));
            Assert.That(reject.Succeeded, Is.True, reject.Message);
            Assert.That(reject.Session.state, Is.EqualTo(TradeSessionState.Rejected));
        }

        [Test]
        public void PartialStackBarterSplitsQuantityAndRollbackRestoresBackingRuntimes()
        {
            Fixture fixture = Fixture.Create();
            fixture.OpenSession("trade.barter", out string sessionId);
            TradeOperationResult offer = fixture.Trades.SubmitOffer(sessionId, fixture.BarterOffer(sessionId, "offer.barter"), "tx.barter.offer");
            TradeOperationResult reserve = fixture.Trades.ReserveOfferAssets(offer.Offer.offerId, fixture.Economy, fixture.Items, 2d, "tx.barter.reserve");
            TradeOperationResult accept = fixture.Trades.AcceptOffer(sessionId, offer.Offer.offerId, "participant.seller", 3d, "tx.barter.accept");
            string itemBefore = JsonUtility.ToJson(fixture.Items.CreateSaveData());
            string economyBefore = JsonUtility.ToJson(fixture.Economy.CreateSaveData());
            TradeOperationResult failed = fixture.Trades.ExecuteAcceptedDeal(sessionId, fixture.Economy, fixture.Items, fixture.Markets, fixture.Registry, 4d, "tx.barter.failed", injectFailureStage: "after-money");

            Assert.That(offer.Succeeded, Is.True, offer.Message);
            Assert.That(reserve.Succeeded, Is.True, reserve.Message);
            Assert.That(accept.Succeeded, Is.True, accept.Message);
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(fixture.Items.CreateSaveData()), Is.EqualTo(itemBefore));
            Assert.That(JsonUtility.ToJson(fixture.Economy.CreateSaveData()), Is.EqualTo(economyBefore));

            TradeOperationResult execute = fixture.Trades.ExecuteAcceptedDeal(sessionId, fixture.Economy, fixture.Items, fixture.Markets, fixture.Registry, 5d, "tx.barter.execute");

            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(fixture.Items.TryGetSnapshot(fixture.HerbStackId, out ItemInstanceSnapshot sourceHerbs), Is.True);
            Assert.That(sourceHerbs.StackQuantity, Is.EqualTo(3));
            Assert.That(fixture.Items.QueryByDefinition(fixture.Herb.Id).Any(item => item.OwnerPersonId == fixture.SellerPersonId && item.StackQuantity == 2), Is.True);
        }

        [Test]
        public void PersistenceParticipantRejectsBrokenTradeGraphBeforeCommit()
        {
            Fixture fixture = Fixture.Create();
            fixture.OpenSession("trade.persist", out string sessionId);
            TradeOperationResult offer = fixture.Trades.SubmitOffer(sessionId, fixture.MoneyForSwordOffer(sessionId, "offer.persist", 20L), "tx.persist.offer");
            TradePersistenceParticipant participant = new TradePersistenceParticipant(fixture.Trades, () => fixture.Registry);
            TradeRuntimeSaveData corrupt = fixture.Trades.CreateSaveData();
            corrupt.offers[0].tradeSessionId = "trade-session.missing";

            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(JsonUtility.ToJson(corrupt), TradePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(offer.Succeeded, Is.True, offer.Message);
            Assert.That(prepared.Succeeded, Is.False);
            Assert.That(fixture.Trades.TryGetOffer(offer.Offer.offerId, out TradeOfferData live), Is.True);
            Assert.That(live.tradeSessionId, Is.EqualTo(sessionId));
        }

        [Test]
        public void ValuationAndProjectionDoNotMutateTradeState()
        {
            Fixture fixture = Fixture.Create();
            fixture.OpenSession("trade.project", out string sessionId);
            TradeOperationResult offer = fixture.Trades.SubmitOffer(sessionId, fixture.MoneyForSwordOffer(sessionId, "offer.project", 45L), "tx.project.offer");
            TradeRuntimeSaveData before = fixture.Trades.CreateSaveData();
            TradeOperationResult valuation = fixture.Trades.ValueAsset("valuation.project", sessionId, offer.Offer.offerId, "participant.buyer", offer.Offer.AllAssets.First(asset => asset.IsItemAsset), fixture.Economy, fixture.Markets, fixture.Items, privilegedHiddenFactors: false, worldTime: 3d);
            TradeRuntimeSaveData afterValuation = fixture.Trades.CreateSaveData();
            TradeProjection<TradeOfferData> projection = fixture.ProjectOffer(offer.Offer);

            Assert.That(valuation.Succeeded, Is.True, valuation.Message);
            Assert.That(afterValuation.valuations.Count, Is.EqualTo(before.valuations.Count + 1));
            Assert.That(projection.Succeeded, Is.True, projection.Message);
            Assert.That(projection.Redacted, Is.True);
            Assert.That(projection.Record.AllAssets.Count, Is.Zero);
            Assert.That(fixture.Trades.TryGetOffer(offer.Offer.offerId, out TradeOfferData live), Is.True);
            Assert.That(live.AllAssets.Count, Is.EqualTo(2));
        }

        private sealed class Fixture
        {
            private Fixture(DefinitionRegistry registry, TradePolicyDefinition policy, CurrencyDefinition gold, ItemDefinition sword, ItemDefinition herb, MarketDefinition market, MarketSubjectDefinition subject)
            {
                Registry = registry;
                Policy = policy;
                Gold = gold;
                Sword = sword;
                Herb = herb;
                Market = market;
                Subject = subject;
                Economy = new EconomyRuntime();
                Economy.Configure(registry, PersistenceService.LocalWorldId);
                Markets = new MarketRuntime();
                Markets.Configure(registry, PersistenceService.LocalWorldId);
                Trades = new TradeRuntime();
                Trades.Configure(registry, PersistenceService.LocalWorldId);
                Items = new ItemInstanceIdentityRuntime();
                BuyerAccount = "account.trade.buyer";
                SellerAccount = "account.trade.seller";
                SwordInstanceId = GuidFor("sword");
                HerbStackId = GuidFor("herbs");
                Economy.CreateAccount(BuyerAccount, Gold, BuyerPersonId, EconomyAccountKind.PersonWallet, 100L, "tx.buyer");
                Economy.CreateAccount(SellerAccount, Gold, SellerPersonId, EconomyAccountKind.PersonWallet, 0L, "tx.seller");
                Items.CreateItem(Sword, ItemInstanceClassification.IndividuallyTracked, SwordInstanceId, ownerPersonId: SellerPersonId, custodianPersonId: SellerPersonId);
                Items.CreateItem(Herb, ItemInstanceClassification.Fungible, HerbStackId, ownerPersonId: BuyerPersonId, custodianPersonId: BuyerPersonId);
                ItemInstanceRuntimeSaveData save = Items.CreateSaveData();
                save.records.First(record => record.itemInstanceId == HerbStackId).stackQuantity = 5;
                Items.RestoreFromSaveData(save, Registry);
                Markets.CreateMarketInstance(Market, MarketId, "region.prototype");
                Markets.RecordSupply(Supply("stock", 10L, 0L, 10L, 1d));
                Markets.RecordDemand(Demand("buyers", 10L, 1d));
                Markets.UpdateMarketSubject(MarketId, Subject.Id, 2d);
            }

            public DefinitionRegistry Registry { get; }
            public TradePolicyDefinition Policy { get; }
            public CurrencyDefinition Gold { get; }
            public ItemDefinition Sword { get; }
            public ItemDefinition Herb { get; }
            public MarketDefinition Market { get; }
            public MarketSubjectDefinition Subject { get; }
            public EconomyRuntime Economy { get; }
            public MarketRuntime Markets { get; }
            public TradeRuntime Trades { get; }
            public ItemInstanceIdentityRuntime Items { get; }
            public string BuyerAccount { get; }
            public string SellerAccount { get; }
            public string SwordInstanceId { get; }
            public string HerbStackId { get; }
            public string BuyerPersonId => "person.trade.buyer";
            public string SellerPersonId => "person.trade.seller";
            private string MarketId => "market.trade.local";

            public static Fixture Create()
            {
                CurrencyDefinition gold = ScriptableObject.CreateInstance<CurrencyDefinition>();
                gold.Initialize("currency.gold", "Gold", "G");
                ItemDefinition sword = Item("item.prototype-sword", "Prototype Sword", stackable: false);
                ItemDefinition herb = Item("item.prototype-herb", "Prototype Herb", stackable: true);
                MarketDefinition market = ScriptableObject.CreateInstance<MarketDefinition>();
                market.Initialize("market.prototype.local", "Prototype Market", gold, MarketCategory.LocalSettlement, MarketScopeType.Settlement, new[] { MarketSubjectKind.ItemDefinition });
                MarketSubjectDefinition subject = ScriptableObject.CreateInstance<MarketSubjectDefinition>();
                subject.Initialize("market-subject.prototype-sword", "Prototype Sword", MarketSubjectKind.ItemDefinition, sword.Id, gold, 100L);
                TradePolicyDefinition policy = ScriptableObject.CreateInstance<TradePolicyDefinition>();
                policy.Initialize("trade-policy.prototype.direct", "Prototype Direct Trade", TradePolicyCategory.DirectPersonToPerson);
                DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { gold, sword, herb, market, subject, policy });
                return new Fixture(registry, policy, gold, sword, herb, market, subject);
            }

            public TradeOperationResult OpenSession(string sessionId, out string resolvedSessionId)
            {
                resolvedSessionId = sessionId;
                return Trades.OpenSession(Policy, new TradeSessionData
                {
                    tradeSessionId = sessionId,
                    participants = new System.Collections.Generic.List<TradeParticipantData>
                    {
                        Participant("participant.buyer", BuyerPersonId, TradeParticipantRole.Buyer),
                        Participant("participant.seller", SellerPersonId, TradeParticipantRole.Seller)
                    },
                    initiatorParticipantId = "participant.buyer",
                    marketInstanceId = MarketId,
                    createdWorldTime = 1d,
                    lastActivityWorldTime = 1d
                }, $"tx.open.{sessionId}");
            }

            public TradeOfferData MoneyForSwordOffer(string sessionId, string offerId, long units, string proposer = "participant.buyer", string responder = "participant.seller", string quoteId = "")
            {
                return new TradeOfferData
                {
                    offerId = offerId,
                    tradeSessionId = sessionId,
                    proposingParticipantId = proposer,
                    respondingParticipantIds = new[] { responder },
                    createdWorldTime = 2d,
                    expiresWorldTime = 120d,
                    merchantQuoteIds = string.IsNullOrWhiteSpace(quoteId) ? Array.Empty<string>() : new[] { quoteId },
                    bundles = new System.Collections.Generic.List<TradeBundleData>
                    {
                        new TradeBundleData
                        {
                            bundleId = $"{offerId}.money",
                            contributingParticipantId = "participant.buyer",
                            receivingParticipantId = "participant.seller",
                            assets = new System.Collections.Generic.List<TradeAssetEntryData>
                            {
                                Money($"{offerId}.gold", "participant.buyer", "participant.seller", BuyerAccount, SellerAccount, Gold.Id, units, quoteId)
                            }
                        },
                        new TradeBundleData
                        {
                            bundleId = $"{offerId}.item",
                            contributingParticipantId = "participant.seller",
                            receivingParticipantId = "participant.buyer",
                            assets = new System.Collections.Generic.List<TradeAssetEntryData>
                            {
                                ItemAsset($"{offerId}.sword", "participant.seller", "participant.buyer", SwordInstanceId, 1)
                            }
                        }
                    }
                };
            }

            public TradeOfferData BarterOffer(string sessionId, string offerId)
            {
                TradeOfferData offer = MoneyForSwordOffer(sessionId, offerId, 15L);
                offer.bundles[0].assets.Add(ItemAsset($"{offerId}.herbs", "participant.buyer", "participant.seller", HerbStackId, 2, TradeAssetKind.StackQuantity));
                return offer;
            }

            public string CreateQuote(string quoteId)
            {
                Markets.CreateMerchantQuote(quoteId, "merchant.prototype", MarketId, Subject.Id, MerchantQuoteDirection.MerchantSells, 1L, 3d, 120d);
                return quoteId;
            }

            public TradeProjection<TradeOfferData> ProjectOffer(TradeOfferData offer)
            {
                InformationAccessRuntime access = new InformationAccessRuntime();
                access.Configure(Registry, "person.viewer");
                access.RegisterPolicy(new InformationAccessPolicyData
                {
                    policyId = "policy.trade",
                    subject = offer.CreateInformationSubject(),
                    classification = InformationVisibilityClassification.Secret,
                    detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                    defaultVisibleDetails = new[] { "detail.participants" },
                    defaultRedactedDetails = new[] { "detail.assets", "detail.valuations" },
                    redactedAccessAcceptable = true
                }, "tx.policy");
                access.GrantAccess(new InformationAccessGrantData
                {
                    grantId = "grant.trade",
                    policyId = "policy.trade",
                    subject = offer.CreateInformationSubject(),
                    granteeKind = InformationGranteeKind.Person,
                    granteeId = "person.viewer",
                    grantorId = SellerPersonId,
                    accessModes = new[] { InformationAccessMode.Inspect },
                    detailIds = new[] { "detail.participants" }
                }, "tx.grant");
                return Trades.GetOfferProjection(offer.offerId, access, new InformationAccessContext { RequestingPersonId = "person.viewer", HasDiscoveredSubject = true, RevealDenialReasons = true }, "policy.trade");
            }

            private MarketObservationRecordData Supply(string source, long quantity, long reserved, long available, double observed)
            {
                return new MarketObservationRecordData
                {
                    observationId = $"supply.{source}",
                    marketInstanceId = MarketId,
                    marketSubjectId = Subject.Id,
                    unit = MarketQuantityUnit.Each,
                    quantity = quantity,
                    reservedQuantity = reserved,
                    availableNowQuantity = available,
                    supplySourceCategory = MarketSupplySourceCategory.MerchantInventory,
                    sourceReferenceId = source,
                    observedWorldTime = observed,
                    reliability = 9000
                };
            }

            private MarketObservationRecordData Demand(string source, long quantity, double observed)
            {
                return new MarketObservationRecordData
                {
                    observationId = $"demand.{source}",
                    marketInstanceId = MarketId,
                    marketSubjectId = Subject.Id,
                    unit = MarketQuantityUnit.Each,
                    quantity = quantity,
                    demandCategory = MarketDemandCategory.Consumer,
                    sourceReferenceId = source,
                    observedWorldTime = observed,
                    reliability = 9000
                };
            }

            private static TradeParticipantData Participant(string id, string person, TradeParticipantRole role)
            {
                return new TradeParticipantData
                {
                    participantId = id,
                    kind = TradeParticipantKind.Person,
                    role = role,
                    subjectId = person,
                    sourceInventoryId = person,
                    receivingInventoryId = person
                };
            }

            private static TradeAssetEntryData Money(string id, string sourceParticipant, string destinationParticipant, string sourceAccount, string destinationAccount, string currency, long units, string quoteId)
            {
                return new TradeAssetEntryData
                {
                    assetEntryId = id,
                    assetKind = TradeAssetKind.Money,
                    sourceParticipantId = sourceParticipant,
                    destinationParticipantId = destinationParticipant,
                    sourceAccountId = sourceAccount,
                    destinationAccountId = destinationAccount,
                    currencyId = currency,
                    units = units,
                    quantity = 1,
                    quoteId = quoteId
                };
            }

            private static TradeAssetEntryData ItemAsset(string id, string sourceParticipant, string destinationParticipant, string itemInstanceId, int quantity, TradeAssetKind kind = TradeAssetKind.ItemInstance)
            {
                return new TradeAssetEntryData
                {
                    assetEntryId = id,
                    assetKind = kind,
                    sourceParticipantId = sourceParticipant,
                    destinationParticipantId = destinationParticipant,
                    itemInstanceId = itemInstanceId,
                    quantity = quantity
                };
            }

            private static ItemDefinition Item(string id, string display, bool stackable)
            {
                ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
                SetPrivate(item, "itemId", id);
                SetPrivate(item, "displayName", display);
                SetPrivate(item, "stackable", stackable);
                SetPrivate(item, "maximumStackSize", stackable ? 99 : 1);
                SetPrivate(item, "instanceMode", ItemInstanceMode.AlwaysInstanced);
                return item;
            }

            private static string GuidFor(string seed)
            {
                using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
                return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seed))).ToString("D");
            }

            private static void SetPrivate(object target, string fieldName, object value)
            {
                target?.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(target, value);
            }
        }
    }
}
