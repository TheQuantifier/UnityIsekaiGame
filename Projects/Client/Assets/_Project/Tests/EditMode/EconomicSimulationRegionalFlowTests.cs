using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.Economy.RegionalFlow;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class EconomicSimulationRegionalFlowTests
    {
        [Test]
        public void RegionCommodityPoolAndQuantityOperationsAreExactAndImmutable()
        {
            Fixture fixture = Fixture.Create();
            RegionalFlowOperationResult region = fixture.CreateRegion(Fixture.RegionA);
            RegionalFlowOperationResult pool = fixture.CreatePool(Fixture.FoodPoolA, Fixture.RegionA, fixture.Food.Id, 10L);
            RegionalFlowOperationResult preview = fixture.Flow.ApplyQuantityOperation(Op("op.preview", AggregateQuantityOperationKind.Add, fixture.Food.Id, string.Empty, Fixture.FoodPoolA, 5L), preview: true);
            RegionalFlowOperationResult add = fixture.Flow.ApplyQuantityOperation(Op("op.add", AggregateQuantityOperationKind.Add, fixture.Food.Id, string.Empty, Fixture.FoodPoolA, 5L), "tx.add");
            RegionalFlowOperationResult duplicate = fixture.Flow.ApplyQuantityOperation(Op("op.add", AggregateQuantityOperationKind.Add, fixture.Food.Id, string.Empty, Fixture.FoodPoolA, 5L), "tx.add");
            RegionalFlowOperationResult reserve = fixture.Flow.ApplyQuantityOperation(Op("op.reserve", AggregateQuantityOperationKind.Reserve, fixture.Food.Id, Fixture.FoodPoolA, string.Empty, 4L), "tx.reserve");
            RegionalFlowOperationResult release = fixture.Flow.ApplyQuantityOperation(Op("op.release", AggregateQuantityOperationKind.ReleaseReservation, fixture.Food.Id, Fixture.FoodPoolA, string.Empty, 2L), "tx.release");

            Assert.That(region.Succeeded, Is.True, region.Message);
            Assert.That(pool.Succeeded, Is.True, pool.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(add.Succeeded, Is.True, add.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(reserve.Succeeded, Is.True, reserve.Message);
            Assert.That(release.Succeeded, Is.True, release.Message);
            Assert.That(fixture.Flow.TryGetPool(Fixture.FoodPoolA, out CommodityPoolData live), Is.True);
            Assert.That(live.totalQuantity, Is.EqualTo(15L));
            Assert.That(live.reservedQuantity, Is.EqualTo(2L));
            live.totalQuantity = 999L;
            Assert.That(fixture.Flow.TryGetPool(Fixture.FoodPoolA, out CommodityPoolData unchanged), Is.True);
            Assert.That(unchanged.totalQuantity, Is.EqualTo(15L));
        }

        [Test]
        public void ExactAggregationAndMaterializationAreExplicitAndRejectDoubleCounting()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateRegion(Fixture.RegionA);
            fixture.CreatePool(Fixture.FoodPoolA, Fixture.RegionA, fixture.Food.Id, 0L);

            RegionalFlowOperationResult aggregate = fixture.Flow.ApplyQuantityOperation(Op("op.aggregate.exact", AggregateQuantityOperationKind.AggregateExactItems, fixture.Food.Id, string.Empty, Fixture.FoodPoolA, 3L, "exact.inventory.bundle.1"), "tx.aggregate");
            RegionalFlowOperationResult duplicateSource = fixture.Flow.ApplyQuantityOperation(Op("op.aggregate.exact.2", AggregateQuantityOperationKind.AggregateExactItems, fixture.Food.Id, string.Empty, Fixture.FoodPoolA, 3L, "exact.inventory.bundle.1"), "tx.aggregate.other");
            RegionalFlowOperationResult materialize = fixture.Flow.ApplyQuantityOperation(Op("op.materialize", AggregateQuantityOperationKind.Materialize, fixture.Food.Id, string.Empty, Fixture.FoodPoolA, 1L, "materialize.request"), "tx.materialize");

            Assert.That(aggregate.Succeeded, Is.True, aggregate.Message);
            Assert.That(duplicateSource.Succeeded, Is.False);
            Assert.That(duplicateSource.Message, Does.Contain("double counted"));
            Assert.That(materialize.Succeeded, Is.True, materialize.Message);
            Assert.That(fixture.Flow.TryGetPool(Fixture.FoodPoolA, out CommodityPoolData pool), Is.True);
            Assert.That(pool.totalQuantity, Is.EqualTo(4L));
        }

        [Test]
        public void AggregateProductionConsumptionAndMarketObservationsRemainDistinct()
        {
            Fixture fixture = Fixture.CreateWithMarket();
            fixture.CreateRegion(Fixture.RegionA);
            fixture.CreatePool(Fixture.OrePoolA, Fixture.RegionA, fixture.Ore.Id, 10L);
            fixture.CreatePool(Fixture.MetalPoolA, Fixture.RegionA, fixture.Metal.Id, 0L);
            fixture.CreatePool(Fixture.FoodPoolA, Fixture.RegionA, fixture.Food.Id, 6L);
            fixture.CreateLaborCohort(2L, 5L);

            ProductionCapacityResultData capacity = fixture.Flow.EvaluateProductionCapacity("capacity.smelting", Fixture.RegionA, Fixture.CohortA, fixture.Smelting, 1d, new[] { Fixture.OrePoolA });
            RegionalFlowOperationResult production = fixture.Flow.ExecuteAggregateProduction("production.smelting.1", Fixture.RegionA, Fixture.CohortA, fixture.Smelting, new[] { Fixture.OrePoolA }, new[] { Fixture.MetalPoolA }, 1L, 1d, fixture.Market, Fixture.MarketId, "tx.production");
            RegionalFlowOperationResult consumption = fixture.Flow.ExecuteAggregateConsumption("consumption.food.1", Fixture.RegionA, Fixture.CohortA, fixture.Consumption, new[] { Fixture.FoodPoolA }, 1L, 1d, fixture.Market, Fixture.MarketId, "tx.consumption");

            Assert.That(capacity.effectiveOutputCapacity, Is.EqualTo(2L));
            Assert.That(production.Succeeded, Is.True, production.Message);
            Assert.That(consumption.Succeeded, Is.True, consumption.Message);
            Assert.That(fixture.Flow.TryGetPool(Fixture.OrePoolA, out CommodityPoolData ore), Is.True);
            Assert.That(fixture.Flow.TryGetPool(Fixture.MetalPoolA, out CommodityPoolData metal), Is.True);
            Assert.That(fixture.Flow.TryGetPool(Fixture.FoodPoolA, out CommodityPoolData food), Is.True);
            Assert.That(ore.totalQuantity, Is.EqualTo(8L));
            Assert.That(metal.totalQuantity, Is.EqualTo(2L));
            Assert.That(food.totalQuantity, Is.EqualTo(4L));
            Assert.That(fixture.Market.SupplyCount, Is.EqualTo(1));
            Assert.That(fixture.Market.DemandCount, Is.EqualTo(1));
        }

        [Test]
        public void LaborWealthShortageAndWagePressureAreDerivedWithoutExternalMutation()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateRegion(Fixture.RegionA);
            fixture.CreatePool(Fixture.FoodPoolA, Fixture.RegionA, fixture.Food.Id, 5L);
            RegionalFlowOperationResult cohort = fixture.Flow.RegisterCohort(new EconomicCohortData
            {
                cohortId = Fixture.CohortA,
                regionId = Fixture.RegionA,
                category = EconomicCohortCategory.Laborers,
                populationQuantity = 10L,
                laborDistribution = new[] { new RegionalLaborQuantityData { laborCategory = LaborCategory.GeneralLabor, units = 8L } },
                accountId = "account.cohort.liquidity",
                commodityPoolIds = new[] { Fixture.FoodPoolA }
            }, "tx.cohort");

            LaborMarketSnapshotData labor = fixture.Flow.EvaluateLaborMarket("labor.general", Fixture.RegionA, LaborCategory.GeneralLabor, 12L, 1d);
            WealthSummaryData wealth = fixture.Flow.EvaluateWealth("wealth.cohort", Fixture.RegionA, new RegionalSubjectReferenceData { subjectKind = "cohort", subjectId = Fixture.CohortA }, fixture.Gold.Id, 50L, 100L, 30L, 1d);
            ShortageSurplusData shortage = fixture.Flow.EvaluateCommodityShortage("shortage.food", Fixture.RegionA, fixture.Food.Id, 8L, 2L, 1d);

            Assert.That(cohort.Succeeded, Is.True, cohort.Message);
            Assert.That(labor.supplyUnits, Is.EqualTo(8L));
            Assert.That(labor.demandUnits, Is.EqualTo(12L));
            Assert.That(labor.wagePressure, Is.EqualTo(WagePressureState.Upward));
            Assert.That(wealth.netEstimatedWealthUnits, Is.EqualTo(120L));
            Assert.That(shortage.state, Is.EqualTo(ShortageState.Shortage));
            Assert.That(shortage.shortageKind, Is.EqualTo(ShortageKind.Commodity));
        }

        [Test]
        public void TradeFlowPlanningExecutionAndArrivalConserveQuantities()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateRegion(Fixture.RegionA);
            fixture.CreateRegion(Fixture.RegionB);
            fixture.CreatePool(Fixture.FoodPoolA, Fixture.RegionA, fixture.Food.Id, 20L);
            fixture.CreatePool(Fixture.FoodPoolB, Fixture.RegionB, fixture.Food.Id, 0L);
            RegionalFlowOperationResult connection = fixture.Flow.RegisterTradeConnection(new TradeConnectionData
            {
                connectionId = Fixture.ConnectionAB,
                sourceRegionId = Fixture.RegionA,
                destinationRegionId = Fixture.RegionB,
                permittedCommodityIds = new[] { fixture.Food.Id },
                capacityUnits = 10L,
                leadTimeUnits = 2L,
                state = TradeConnectionState.Active
            }, "tx.connection");
            RegionalFlowOperationResult plan = fixture.Flow.PlanFlow("flow.food.ab", Fixture.ConnectionAB, Fixture.FoodPoolA, Fixture.FoodPoolB, fixture.Food.Id, 6L, 1L);
            RegionalFlowOperationResult reserve = fixture.Flow.ReserveFlow(plan.FlowOrder, "tx.flow.reserve");
            RegionalFlowOperationResult early = fixture.Flow.ArriveFlow("flow.food.ab", 1L, 1d, transactionId: "tx.flow.early");
            RegionalFlowOperationResult depart = fixture.Flow.DepartFlow("flow.food.ab", 1L, 1d, "tx.flow.depart");
            RegionalFlowOperationResult arrive = fixture.Flow.ArriveFlow("flow.food.ab", 3L, 3d, lossUnits: 1L, transactionId: "tx.flow.arrive");

            Assert.That(connection.Succeeded, Is.True, connection.Message);
            Assert.That(plan.Preview, Is.True);
            Assert.That(reserve.Succeeded, Is.True, reserve.Message);
            Assert.That(early.Succeeded, Is.False);
            Assert.That(depart.Succeeded, Is.True, depart.Message);
            Assert.That(arrive.Succeeded, Is.True, arrive.Message);
            Assert.That(fixture.Flow.TryGetPool(Fixture.FoodPoolA, out CommodityPoolData source), Is.True);
            Assert.That(fixture.Flow.TryGetPool(Fixture.FoodPoolB, out CommodityPoolData destination), Is.True);
            Assert.That(source.totalQuantity, Is.EqualTo(14L));
            Assert.That(destination.totalQuantity, Is.EqualTo(5L));
        }

        [Test]
        public void EconomicCycleIsBoundaryIdempotentAndRollsBackInjectedFailures()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateRegion(Fixture.RegionA);
            fixture.CreatePool(Fixture.OrePoolA, Fixture.RegionA, fixture.Ore.Id, 10L);
            fixture.CreatePool(Fixture.MetalPoolA, Fixture.RegionA, fixture.Metal.Id, 0L);
            fixture.CreateLaborCohort(2L, 5L);
            RegionalFlowRuntimeSaveData before = fixture.Flow.CreateSaveData();

            RegionalFlowOperationResult failed = fixture.Flow.RunEconomicCycle("cycle.fail", Fixture.RegionA, 1L, 1d, new[] { fixture.Smelting }, Array.Empty<AggregateConsumptionProfileDefinition>(), injectFailureStage: "after-production");
            Assert.That(failed.Code, Is.EqualTo(RegionalFlowResultCode.RolledBack));
            Assert.That(fixture.Flow.TryGetPool(Fixture.OrePoolA, out CommodityPoolData afterFailedOre), Is.True);
            Assert.That(afterFailedOre.totalQuantity, Is.EqualTo(before.pools.Single(item => item.poolId == Fixture.OrePoolA).totalQuantity));

            RegionalFlowOperationResult cycle = fixture.Flow.RunEconomicCycle("cycle.ok", Fixture.RegionA, 1L, 1d, new[] { fixture.Smelting }, Array.Empty<AggregateConsumptionProfileDefinition>());
            RegionalFlowOperationResult duplicate = fixture.Flow.RunEconomicCycle("cycle.ok", Fixture.RegionA, 1L, 1d, new[] { fixture.Smelting }, Array.Empty<AggregateConsumptionProfileDefinition>());

            Assert.That(cycle.Succeeded, Is.True, cycle.Message);
            Assert.That(cycle.Cycle.succeeded, Is.True);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(cycle.Audit.balanced, Is.True);
        }

        [Test]
        public void AccessProjectionRedactsPrivateRegionalQuantities()
        {
            Fixture fixture = Fixture.Create();
            InformationAccessOperationResult policy = fixture.Access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = "policy.private.pool",
                subject = RegionalFlowInformationSubject.Create("economy.pool", Fixture.FoodPoolA, Fixture.RegionA, new[] { fixture.Food.Id }),
                classification = InformationVisibilityClassification.Private,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Redacted,
                defaultVisibleDetails = new[] { "detail.region" },
                defaultRedactedDetails = new[] { "detail.quantity", "detail.provenance" },
                redactedAccessAcceptable = true
            }, "tx.policy.pool");
            InformationAccessOperationResult grant = fixture.Access.GrantAccess(new InformationAccessGrantData
            {
                grantId = "grant.private.pool.redacted",
                policyId = "policy.private.pool",
                subject = RegionalFlowInformationSubject.Create("economy.pool", Fixture.FoodPoolA, Fixture.RegionA, new[] { fixture.Food.Id }),
                granteeKind = InformationGranteeKind.Person,
                granteeId = "person.public",
                grantorId = "person.owner",
                accessModes = new[] { InformationAccessMode.Inspect },
                detailIds = new[] { "detail.region" }
            }, "tx.grant.pool");
            Assert.That(policy.Succeeded, Is.True, policy.Message);
            Assert.That(grant.Succeeded, Is.True, grant.Message);
            fixture.CreateRegion(Fixture.RegionA);
            fixture.CreatePool(Fixture.FoodPoolA, Fixture.RegionA, fixture.Food.Id, 12L, "policy.private.pool");

            InformationAccessProjection<CommodityPoolData> publicProjection = fixture.Flow.GetPoolProjection(Fixture.FoodPoolA, fixture.Access, new InformationAccessContext { RequestingPersonId = "person.public", RedactedAccessAcceptable = true });
            InformationAccessProjection<CommodityPoolData> privilegedProjection = fixture.Flow.GetPoolProjection(Fixture.FoodPoolA, fixture.Access, new InformationAccessContext { RequestingPersonId = "person.admin", ContextKind = InformationContextKind.Debug });

            Assert.That(publicProjection.Redacted, Is.True);
            Assert.That(publicProjection.Record, Is.Not.Null);
            Assert.That(publicProjection.Record.totalQuantity, Is.Zero);
            Assert.That(privilegedProjection.FullAccess, Is.True);
            Assert.That(privilegedProjection.Record, Is.Not.Null);
            Assert.That(privilegedProjection.Record.totalQuantity, Is.EqualTo(12L));
        }

        [Test]
        public void PersistenceRejectsBrokenGraphBeforeCommitWithoutReplayingSimulation()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateRegion(Fixture.RegionA);
            fixture.CreatePool(Fixture.FoodPoolA, Fixture.RegionA, fixture.Food.Id, 7L);
            fixture.Flow.RunEconomicCycle("cycle.persist", Fixture.RegionA, 1L, 1d);
            RegionalFlowPersistenceParticipant participant = new RegionalFlowPersistenceParticipant(fixture.Flow, () => fixture.Registry);
            RegionalFlowRuntimeSaveData corrupt = fixture.Flow.CreateSaveData();
            corrupt.pools[0].commodityId = "commodity.missing";

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), RegionalFlowPersistenceParticipant.CurrentParticipantSchemaVersion);
            RegionalFlowRuntimeSaveData live = fixture.Flow.CreateSaveData();

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(live.pools.Single().commodityId, Is.EqualTo(fixture.Food.Id));
            Assert.That(live.cycles.Count, Is.EqualTo(1));
            Assert.That(live.pools.Single().totalQuantity, Is.EqualTo(7L));
        }

        private static AggregateQuantityOperationData Op(string id, AggregateQuantityOperationKind kind, string commodityId, string sourcePoolId, string destinationPoolId, long quantity, string sourceEventId = "")
        {
            return new AggregateQuantityOperationData
            {
                operationId = id,
                operationKind = kind,
                commodityId = commodityId,
                sourcePoolId = sourcePoolId,
                destinationPoolId = destinationPoolId,
                unit = CommodityUnit.Each,
                quantity = quantity,
                sourceEventId = sourceEventId,
                authorityId = "test.authority"
            };
        }

        private sealed class Fixture
        {
            private Fixture(DefinitionRegistry registry, CurrencyDefinition gold, CommodityDefinition food, CommodityDefinition ore, CommodityDefinition metal, EconomicRegionDefinition settlement, AggregateProductionProfileDefinition smelting, AggregateConsumptionProfileDefinition consumption)
            {
                Registry = registry;
                Gold = gold;
                Food = food;
                Ore = ore;
                Metal = metal;
                Settlement = settlement;
                Smelting = smelting;
                Consumption = consumption;
                Flow = new RegionalFlowRuntime();
                Market = new MarketRuntime();
                Access = new InformationAccessRuntime();
                Flow.Configure(registry, PersistenceService.LocalWorldId);
                Market.Configure(registry, PersistenceService.LocalWorldId);
                Access.Configure(registry, "person.owner");
            }

            public const string RegionA = "economy.region.prototype.a";
            public const string RegionB = "economy.region.prototype.b";
            public const string FoodPoolA = "commodity-pool.food.a";
            public const string FoodPoolB = "commodity-pool.food.b";
            public const string OrePoolA = "commodity-pool.ore.a";
            public const string MetalPoolA = "commodity-pool.metal.a";
            public const string CohortA = "cohort.prototype.laborers";
            public const string ConnectionAB = "trade-connection.prototype.a-b";
            public const string MarketId = "market.prototype.region";

            public DefinitionRegistry Registry { get; }
            public CurrencyDefinition Gold { get; }
            public CommodityDefinition Food { get; }
            public CommodityDefinition Ore { get; }
            public CommodityDefinition Metal { get; }
            public EconomicRegionDefinition Settlement { get; }
            public AggregateProductionProfileDefinition Smelting { get; }
            public AggregateConsumptionProfileDefinition Consumption { get; }
            public RegionalFlowRuntime Flow { get; }
            public MarketRuntime Market { get; }
            public InformationAccessRuntime Access { get; }

            public static Fixture Create()
            {
                CurrencyDefinition gold = ScriptableObject.CreateInstance<CurrencyDefinition>();
                gold.Initialize("currency.gold", "Gold", "G");
                CommodityDefinition food = Commodity("commodity.food.prototype", "Prototype Food", CommodityCategory.Food, "market-subject.food.prototype");
                CommodityDefinition ore = Commodity("commodity.ore.prototype", "Prototype Ore", CommodityCategory.Ore, "market-subject.ore.prototype");
                CommodityDefinition metal = Commodity("commodity.metal.prototype", "Prototype Metal", CommodityCategory.Metal, "market-subject.metal.prototype");
                EconomicRegionDefinition settlement = ScriptableObject.CreateInstance<EconomicRegionDefinition>();
                settlement.Initialize("economic-region.prototype.settlement", "Prototype Settlement Economy", EconomicRegionCategory.SettlementEconomy, new[] { LaborCategory.GeneralLabor });
                AggregateProductionProfileDefinition smelting = Production("production-profile.prototype.smelting", ore.Id, metal.Id);
                AggregateConsumptionProfileDefinition consumption = ConsumptionProfile("consumption-profile.prototype.food", food.Id);
                DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { gold, food, ore, metal, settlement, smelting, consumption });
                return new Fixture(registry, gold, food, ore, metal, settlement, smelting, consumption);
            }

            public static Fixture CreateWithMarket()
            {
                Fixture fixture = Create();
                MarketDefinition marketDefinition = ScriptableObject.CreateInstance<MarketDefinition>();
                marketDefinition.Initialize("market.prototype.local", "Prototype Regional Market", fixture.Gold);
                MarketSubjectDefinition foodSubject = ScriptableObject.CreateInstance<MarketSubjectDefinition>();
                foodSubject.Initialize("market-subject.food.prototype", "Food", MarketSubjectKind.Custom, fixture.Food.Id, fixture.Gold, 2L, MarketQuantityUnit.Each, 1L);
                MarketSubjectDefinition metalSubject = ScriptableObject.CreateInstance<MarketSubjectDefinition>();
                metalSubject.Initialize("market-subject.metal.prototype", "Metal", MarketSubjectKind.Custom, fixture.Metal.Id, fixture.Gold, 5L, MarketQuantityUnit.Each, 1L);
                DefinitionRegistry registry = new DefinitionRegistry(fixture.Registry.DefinitionsById.Values.Concat(new IGameDefinition[] { marketDefinition, foodSubject, metalSubject }));
                fixture.Flow.Configure(registry, PersistenceService.LocalWorldId);
                fixture.Market.Configure(registry, PersistenceService.LocalWorldId);
                fixture.Market.CreateMarketInstance(marketDefinition, MarketId, RegionA);
                return fixture;
            }

            public RegionalFlowOperationResult CreateRegion(string regionId)
            {
                return Flow.RegisterRegion(new EconomicRegionData
                {
                    regionId = regionId,
                    regionDefinitionId = Settlement.Id,
                    displayName = regionId,
                    state = EconomicRegionState.Active,
                    simulationFidelity = RegionalSimulationFidelity.AggregatePools
                }, "tx.region." + regionId);
            }

            public RegionalFlowOperationResult CreatePool(string poolId, string regionId, string commodityId, long quantity, string accessPolicyId = "")
            {
                return Flow.RegisterCommodityPool(new CommodityPoolData
                {
                    poolId = poolId,
                    regionId = regionId,
                    commodityId = commodityId,
                    unit = CommodityUnit.Each,
                    totalQuantity = quantity,
                    purpose = CommodityPoolPurpose.GeneralRegionalSupply,
                    owner = new RegionalSubjectReferenceData { subjectKind = "cohort", subjectId = CohortA },
                    accessPolicyId = accessPolicyId
                }, "tx.pool." + poolId);
            }

            public RegionalFlowOperationResult CreateLaborCohort(long laborUnits, long population)
            {
                return Flow.RegisterCohort(new EconomicCohortData
                {
                    cohortId = CohortA,
                    regionId = RegionA,
                    category = EconomicCohortCategory.Laborers,
                    populationQuantity = population,
                    laborDistribution = new[] { new RegionalLaborQuantityData { laborCategory = LaborCategory.GeneralLabor, units = laborUnits } },
                    accountId = "account.cohort.liquidity",
                    commodityPoolIds = new[] { FoodPoolA }
                }, "tx.cohort");
            }

            private static CommodityDefinition Commodity(string id, string name, CommodityCategory category, string marketSubjectId)
            {
                CommodityDefinition definition = ScriptableObject.CreateInstance<CommodityDefinition>();
                definition.Initialize(id, name, category, CommodityUnit.Each, marketSubjectId);
                return definition;
            }

            private static AggregateProductionProfileDefinition Production(string id, string inputCommodity, string outputCommodity)
            {
                RegionalCommodityQuantityDefinitionData input = new RegionalCommodityQuantityDefinitionData();
                input.Initialize(inputCommodity, CommodityUnit.Each, 2L);
                RegionalCommodityQuantityDefinitionData output = new RegionalCommodityQuantityDefinitionData();
                output.Initialize(outputCommodity, CommodityUnit.Each, 2L);
                AggregateProductionProfileDefinition profile = ScriptableObject.CreateInstance<AggregateProductionProfileDefinition>();
                profile.Initialize(id, "Prototype Smelting", ProductionProfileCategory.Smelting, new[] { output }, new[] { input }, new[] { Labor(LaborCategory.GeneralLabor, 1L) });
                return profile;
            }

            private static AggregateConsumptionProfileDefinition ConsumptionProfile(string id, string commodityId)
            {
                RegionalCommodityQuantityDefinitionData item = new RegionalCommodityQuantityDefinitionData();
                item.Initialize(commodityId, CommodityUnit.Each, 2L);
                AggregateConsumptionProfileDefinition profile = ScriptableObject.CreateInstance<AggregateConsumptionProfileDefinition>();
                profile.Initialize(id, "Prototype Consumption", ConsumptionProfileCategory.HouseholdNeed, new[] { item });
                return profile;
            }

            private static RegionalLaborQuantityDefinitionData Labor(LaborCategory category, long units)
            {
                RegionalLaborQuantityDefinitionData labor = new RegionalLaborQuantityDefinitionData();
                labor.Initialize(category, units);
                return labor;
            }
        }
    }
}
