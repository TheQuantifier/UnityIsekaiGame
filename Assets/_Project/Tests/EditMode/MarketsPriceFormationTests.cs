using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class MarketsPriceFormationTests
    {
        [Test]
        public void DefinitionsValidateConcreteMarketsSubjectsAndCurrencyReferences()
        {
            Fixture fixture = Fixture.Create();
            DefinitionValidationReport report = new DefinitionValidationReport();

            foreach (IDefinitionCatalogValidationParticipant participant in fixture.Registry.DefinitionsById.Values.OfType<IDefinitionCatalogValidationParticipant>())
            {
                participant.ValidateCatalogDefinition(fixture.Registry.DefinitionsById, report);
            }

            Assert.That(report.HasErrors, Is.False, report.ToString());
            Assert.That(fixture.MarketDefinition.Supports(MarketSubjectKind.ItemDefinition), Is.True);
        }

        [Test]
        public void SupplyDemandObservationsProduceScarcityWithoutDoubleCountingSources()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateMarket("market.local");
            MarketOperationResult supply = fixture.Runtime.RecordSupply(fixture.Supply("market.local", "cart", 100L, 25L, 75L, 1d));
            MarketOperationResult duplicate = fixture.Runtime.RecordSupply(fixture.Supply("market.local", "cart", 50L, 0L, 50L, 2d));
            MarketOperationResult expired = fixture.Runtime.RecordSupply(fixture.Supply("market.local", "expired", 30L, 0L, 30L, 0d, expires: 1d));
            MarketOperationResult demand = fixture.Runtime.RecordDemand(fixture.Demand("market.local", "villagers", 150L, 3d));
            MarketOperationResult scarcity = fixture.Runtime.EvaluateScarcity("scarcity.local", "market.local", fixture.Subject.Id, 3d);

            Assert.That(supply.Succeeded, Is.True, supply.Message);
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(expired.Succeeded, Is.True, expired.Message);
            Assert.That(demand.Succeeded, Is.True, demand.Message);
            Assert.That(scarcity.Succeeded, Is.True, scarcity.Message);
            Assert.That(scarcity.Scarcity.availableSupply, Is.EqualTo(75L));
            Assert.That(scarcity.Scarcity.currentDemand, Is.EqualTo(150L));
            Assert.That(scarcity.Scarcity.scarcityClass, Is.EqualTo(MarketScarcityClass.Scarce));
        }

        [Test]
        public void PriceFormationIsDeterministicRegionalAndIdempotentAtSameBoundary()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateMarket("market.scarce");
            fixture.CreateMarket("market.abundant");
            fixture.Runtime.RecordSupply(fixture.Supply("market.scarce", "mine", 30L, 0L, 30L, 1d));
            fixture.Runtime.RecordDemand(fixture.Demand("market.scarce", "buyers", 120L, 1d));
            fixture.Runtime.RecordSupply(fixture.Supply("market.abundant", "farm", 200L, 0L, 200L, 1d));
            fixture.Runtime.RecordDemand(fixture.Demand("market.abundant", "buyers", 20L, 1d));

            MarketOperationResult scarce = fixture.Runtime.UpdateMarketSubject("market.scarce", fixture.Subject.Id, 10d);
            MarketOperationResult duplicate = fixture.Runtime.UpdateMarketSubject("market.scarce", fixture.Subject.Id, 10d);
            MarketOperationResult abundant = fixture.Runtime.UpdateMarketSubject("market.abundant", fixture.Subject.Id, 10d);

            Assert.That(scarce.Succeeded, Is.True, scarce.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(abundant.Succeeded, Is.True, abundant.Message);
            Assert.That(scarce.Price.referenceAmountUnits, Is.GreaterThan(fixture.Subject.BaselinePriceUnits));
            Assert.That(abundant.Price.referenceAmountUnits, Is.LessThan(fixture.Subject.BaselinePriceUnits));
            Assert.That(fixture.Runtime.QueryPriceHistory("market.scarce", fixture.Subject.Id).Count, Is.EqualTo(1));
        }

        [Test]
        public void MerchantQuotesApplyMarginsAndItemFactorsWithoutExecutingTrade()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateBalancedMarket("market.shop");
            MarketOperationResult price = fixture.Runtime.UpdateMarketSubject("market.shop", fixture.Subject.Id, 2d);
            ItemInstanceSnapshot item = fixture.ItemSnapshot(ItemQualityTier.Fine, 0.5f, makerMark: "hidden.master");
            ItemQualitySnapshot quality = new ItemQualitySnapshot(new ItemQualityRecordData { itemInstanceId = item.ItemInstanceId, overallQuality = 0.8f, qualityTierId = "quality.fine" });
            ItemDurabilitySnapshot durability = new ItemDurabilitySnapshot(new ItemDurabilityRecordData { itemInstanceId = item.ItemInstanceId, currentDurability = 50f, maximumDurability = 100f });

            MarketOperationResult preview = fixture.Runtime.CreateMerchantQuote("quote.preview", "merchant.local", "market.shop", fixture.Subject.Id, MerchantQuoteDirection.MerchantSells, 1L, 3d, 8d, item: item, preview: true, quality: quality, durability: durability);
            MarketOperationResult sell = fixture.Runtime.CreateMerchantQuote("quote.sell", "merchant.local", "market.shop", fixture.Subject.Id, MerchantQuoteDirection.MerchantSells, 1L, 3d, 8d, item: item, quality: quality, durability: durability);
            MarketOperationResult buy = fixture.Runtime.CreateMerchantQuote("quote.buy", "merchant.local", "market.shop", fixture.Subject.Id, MerchantQuoteDirection.MerchantBuys, 1L, 3d, 8d, item: item, quality: quality, durability: durability);
            MarketOperationResult hidden = fixture.Runtime.CreateMerchantQuote("quote.hidden", "merchant.local", "market.shop", fixture.Subject.Id, MerchantQuoteDirection.MerchantSells, 1L, 3d, 8d, item: item, privilegedHiddenFactors: true, quality: quality, durability: durability);

            Assert.That(price.Succeeded, Is.True, price.Message);
            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(fixture.Runtime.QuoteCount, Is.EqualTo(3));
            Assert.That(buy.Quote.finalAmountUnits, Is.LessThan(sell.Quote.finalAmountUnits));
            Assert.That(hidden.Quote.finalAmountUnits, Is.GreaterThan(sell.Quote.finalAmountUnits));
            Assert.That(sell.Quote.hiddenFactorsApplied, Is.False);
            Assert.That(hidden.Quote.hiddenFactorsApplied, Is.True);
            Assert.That(fixture.Runtime.ValidateQuoteForExecution("quote.sell", 4d, out _), Is.True);
            Assert.That(fixture.Runtime.ValidateQuoteForExecution("quote.sell", 9d, out string expired), Is.False);
            Assert.That(expired, Does.Contain("expired"));
        }

        [Test]
        public void PersistenceParticipantRejectsBrokenMarketGraphBeforeCommit()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateBalancedMarket("market.persist");
            fixture.Runtime.UpdateMarketSubject("market.persist", fixture.Subject.Id, 2d);
            MarketPersistenceParticipant participant = new MarketPersistenceParticipant(fixture.Runtime, () => fixture.Registry);
            MarketRuntimeSaveData corrupt = fixture.Runtime.CreateSaveData();
            corrupt.currentPrices[0].marketPriceId = "market-price.missing";

            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(JsonUtility.ToJson(corrupt), MarketPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(prepared.Succeeded, Is.False);
            Assert.That(fixture.Runtime.TryGetCurrentPrice("market.persist", fixture.Subject.Id, out MarketPriceRecordData live), Is.True);
            Assert.That(live.marketPriceId, Is.Not.EqualTo("market-price.missing"));
        }

        [Test]
        public void TransactionObservationsAndAccessProjectionsDoNotMutateSourceRuntimes()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateBalancedMarket("market.private");
            MarketOperationResult price = fixture.Runtime.UpdateMarketSubject("market.private", fixture.Subject.Id, 2d);
            fixture.Economy.CreateAccount("account.buyer", fixture.Gold, "person.buyer", EconomyAccountKind.PersonWallet, 100L, "tx.buyer");
            fixture.Economy.CreateAccount("account.seller", fixture.Gold, "person.seller", EconomyAccountKind.PersonWallet, 0L, "tx.seller");
            EconomyOperationResult transfer = fixture.Economy.Transfer("tx.observed", "account.buyer", "account.seller", new MoneyAmount(fixture.Gold.Id, price.Price.referenceAmountUnits), EconomyTransactionKind.Payment);
            MarketOperationResult observation = fixture.Runtime.AddTransactionObservation("observation.sale", transfer.Transaction, "market.private", fixture.Subject.Id, MarketTransactionObservationPolicy.IncludeCommitted, publicObservation: true, worldTime: 3d);

            InformationAccessRuntime access = new InformationAccessRuntime();
            access.Configure(fixture.Registry, "person.viewer");
            access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = "policy.price",
                subject = price.Price.CreateInformationSubject(),
                classification = InformationVisibilityClassification.Secret,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                defaultVisibleDetails = new[] { "detail.market", "detail.subject", "detail.reference-price" },
                defaultRedactedDetails = new[] { "detail.supply", "detail.demand", "detail.scarcity" },
                redactedAccessAcceptable = true
            }, "tx.policy");
            access.GrantAccess(new InformationAccessGrantData
            {
                grantId = "grant.price",
                policyId = "policy.price",
                subject = price.Price.CreateInformationSubject(),
                granteeKind = InformationGranteeKind.Person,
                granteeId = "person.viewer",
                grantorId = "merchant.local",
                accessModes = new[] { InformationAccessMode.Inspect },
                detailIds = new[] { "detail.market", "detail.subject", "detail.reference-price" }
            }, "tx.grant");

            MarketProjection<MarketPriceRecordData> projection = fixture.Runtime.GetPriceProjection(price.Price.marketPriceId, access, new InformationAccessContext
            {
                RequestingPersonId = "person.viewer",
                HasDiscoveredSubject = true,
                RevealDenialReasons = true
            }, "policy.price");

            Assert.That(transfer.Succeeded, Is.True, transfer.Message);
            Assert.That(observation.Succeeded, Is.True, observation.Message);
            Assert.That(projection.Succeeded, Is.True, projection.Message);
            Assert.That(projection.Redacted, Is.True);
            Assert.That(projection.Record.supplyAvailable, Is.Zero);
            Assert.That(fixture.Economy.TryGetAccount("account.buyer", out EconomyAccountSnapshot buyer), Is.True);
            Assert.That(buyer.BalanceUnits, Is.EqualTo(100L - price.Price.referenceAmountUnits));
        }

        private sealed class Fixture
        {
            private Fixture(DefinitionRegistry registry, CurrencyDefinition gold, ItemDefinition sword, MarketDefinition marketDefinition, MarketSubjectDefinition subject)
            {
                Registry = registry;
                Gold = gold;
                Sword = sword;
                MarketDefinition = marketDefinition;
                Subject = subject;
                Runtime = new MarketRuntime();
                Runtime.Configure(registry, PersistenceService.LocalWorldId);
                Economy = new EconomyRuntime();
                Economy.Configure(registry, PersistenceService.LocalWorldId);
            }

            public DefinitionRegistry Registry { get; }
            public CurrencyDefinition Gold { get; }
            public ItemDefinition Sword { get; }
            public MarketDefinition MarketDefinition { get; }
            public MarketSubjectDefinition Subject { get; }
            public MarketRuntime Runtime { get; }
            public EconomyRuntime Economy { get; }

            public static Fixture Create()
            {
                ItemDefinition sword = ScriptableObject.CreateInstance<ItemDefinition>();
                SetPrivate(sword, "itemId", "item.prototype-sword");
                SetPrivate(sword, "displayName", "Prototype Sword");
                SetPrivate(sword, "stackable", false);
                SetPrivate(sword, "maximumStackSize", 1);
                SetPrivate(sword, "instanceMode", ItemInstanceMode.AlwaysInstanced);

                CurrencyDefinition gold = ScriptableObject.CreateInstance<CurrencyDefinition>();
                gold.Initialize("currency.gold", "Gold", "G");

                MarketDefinition market = ScriptableObject.CreateInstance<MarketDefinition>();
                market.Initialize("market.prototype.local", "Prototype Local Market", gold, MarketCategory.LocalSettlement, MarketScopeType.Settlement, new[] { MarketSubjectKind.ItemDefinition });

                MarketSubjectDefinition subject = ScriptableObject.CreateInstance<MarketSubjectDefinition>();
                subject.Initialize("market-subject.prototype-sword", "Prototype Sword", MarketSubjectKind.ItemDefinition, sword.Id, gold, 100L);
                SetPrivate(subject, "minimumPriceUnits", 1L);
                SetPrivate(subject, "maximumPriceUnits", 1000L);

                DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { gold, sword, market, subject });
                return new Fixture(registry, gold, sword, market, subject);
            }

            public void CreateMarket(string marketId)
            {
                MarketOperationResult result = Runtime.CreateMarketInstance(MarketDefinition, marketId, "region.prototype");
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public void CreateBalancedMarket(string marketId)
            {
                CreateMarket(marketId);
                Runtime.RecordSupply(Supply(marketId, "stock", 20L, 0L, 20L, 1d));
                Runtime.RecordDemand(Demand(marketId, "buyers", 20L, 1d));
            }

            public MarketObservationRecordData Supply(string marketId, string source, long quantity, long reserved, long available, double observed, double expires = -1d)
            {
                return new MarketObservationRecordData
                {
                    observationId = $"supply.{source}",
                    marketInstanceId = marketId,
                    marketSubjectId = Subject.Id,
                    unit = MarketQuantityUnit.Each,
                    quantity = quantity,
                    reservedQuantity = reserved,
                    availableNowQuantity = available,
                    supplySourceCategory = MarketSupplySourceCategory.MerchantInventory,
                    sourceReferenceId = source,
                    observedWorldTime = observed,
                    expiresWorldTime = expires,
                    reliability = 9000
                };
            }

            public MarketObservationRecordData Demand(string marketId, string source, long quantity, double observed)
            {
                return new MarketObservationRecordData
                {
                    observationId = $"demand.{source}",
                    marketInstanceId = marketId,
                    marketSubjectId = Subject.Id,
                    unit = MarketQuantityUnit.Each,
                    quantity = quantity,
                    demandCategory = MarketDemandCategory.Consumer,
                    sourceReferenceId = source,
                    observedWorldTime = observed,
                    reliability = 9000
                };
            }

            public ItemInstanceSnapshot ItemSnapshot(ItemQualityTier quality, float condition, string makerMark = "")
            {
                return new ItemInstanceSnapshot(new ItemInstanceRecordData
                {
                    itemInstanceId = "item-instance.market-test",
                    itemDefinitionId = Sword.Id,
                    labels = new ItemIdentityLabelData { makerMark = makerMark },
                    revision = 1L
                });
            }

            private static void SetPrivate(object target, string fieldName, object value)
            {
                target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(target, value);
            }
        }
    }
}
