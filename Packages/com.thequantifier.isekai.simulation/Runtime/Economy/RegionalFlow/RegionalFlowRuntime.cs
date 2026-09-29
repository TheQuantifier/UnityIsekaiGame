using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Economy.RegionalFlow
{
    public sealed class RegionalFlowRuntime
    {
        public const int CurrentSaveSchemaVersion = 1;
        public static readonly string[] ProtectedDetails =
        {
            "detail.region",
            "detail.pool",
            "detail.quantity",
            "detail.capacity",
            "detail.flow",
            "detail.labor",
            "detail.wealth",
            "detail.market",
            "detail.provenance"
        };

        private readonly Dictionary<string, EconomicRegionData> regionsById = new Dictionary<string, EconomicRegionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CommodityPoolData> poolsById = new Dictionary<string, CommodityPoolData>(StringComparer.Ordinal);
        private readonly Dictionary<string, AggregateQuantityOperationData> operationsById = new Dictionary<string, AggregateQuantityOperationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EconomicCohortData> cohortsById = new Dictionary<string, EconomicCohortData>(StringComparer.Ordinal);
        private readonly Dictionary<string, AggregateProductionRecordData> productionById = new Dictionary<string, AggregateProductionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, AggregateConsumptionRecordData> consumptionById = new Dictionary<string, AggregateConsumptionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LaborMarketSnapshotData> laborById = new Dictionary<string, LaborMarketSnapshotData>(StringComparer.Ordinal);
        private readonly Dictionary<string, WealthSummaryData> wealthById = new Dictionary<string, WealthSummaryData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ShortageSurplusData> shortagesById = new Dictionary<string, ShortageSurplusData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TradeConnectionData> connectionsById = new Dictionary<string, TradeConnectionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, FlowOrderData> flowsById = new Dictionary<string, FlowOrderData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EconomicModifierData> modifiersById = new Dictionary<string, EconomicModifierData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EconomicCycleRecordData> cyclesById = new Dictionary<string, EconomicCycleRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RegionalConservationAuditData> auditsById = new Dictionary<string, RegionalConservationAuditData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RegionalFlowProcessedTransactionData> processedByTransactionId = new Dictionary<string, RegionalFlowProcessedTransactionData>(StringComparer.Ordinal);
        private readonly HashSet<string> exactAggregationKeys = new HashSet<string>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private string worldId = PersistenceService.LocalWorldId;

        public long Revision { get; private set; }
        public string WorldId => worldId;
        public int RegionCount => regionsById.Count;
        public int PoolCount => poolsById.Count;
        public int CohortCount => cohortsById.Count;
        public int FlowCount => flowsById.Count;

        public IReadOnlyList<EconomicRegionData> Regions => Ordered(regionsById.Values, item => item.regionId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CommodityPoolData> Pools => Ordered(poolsById.Values, item => item.poolId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<AggregateQuantityOperationData> Operations => Ordered(operationsById.Values, item => item.operationId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<EconomicCohortData> Cohorts => Ordered(cohortsById.Values, item => item.cohortId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<AggregateProductionRecordData> ProductionRecords => Ordered(productionById.Values, item => item.productionRecordId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<AggregateConsumptionRecordData> ConsumptionRecords => Ordered(consumptionById.Values, item => item.consumptionRecordId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<LaborMarketSnapshotData> LaborSnapshots => Ordered(laborById.Values, item => item.snapshotId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<WealthSummaryData> WealthSummaries => Ordered(wealthById.Values, item => item.wealthSummaryId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ShortageSurplusData> Shortages => Ordered(shortagesById.Values, item => item.shortageId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TradeConnectionData> Connections => Ordered(connectionsById.Values, item => item.connectionId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<FlowOrderData> FlowOrders => Ordered(flowsById.Values, item => item.flowOrderId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<EconomicModifierData> Modifiers => Ordered(modifiersById.Values, item => item.modifierId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<EconomicCycleRecordData> Cycles => Ordered(cyclesById.Values, item => item.cycleId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<RegionalConservationAuditData> Audits => Ordered(auditsById.Values, item => item.auditId).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, string runtimeWorldId = "")
        {
            registry = definitionRegistry;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId;
        }

        public bool TryGetRegion(string regionId, out EconomicRegionData region) => TryClone(regionsById, regionId, item => item.Clone(), out region);
        public bool TryGetPool(string poolId, out CommodityPoolData pool) => TryClone(poolsById, poolId, item => item.Clone(), out pool);
        public bool TryGetCohort(string cohortId, out EconomicCohortData cohort) => TryClone(cohortsById, cohortId, item => item.Clone(), out cohort);
        public bool TryGetFlowOrder(string flowOrderId, out FlowOrderData flow) => TryClone(flowsById, flowOrderId, item => item.Clone(), out flow);

        public RegionalFlowOperationResult RegisterRegion(EconomicRegionData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            EconomicRegionData region = request?.Clone();
            if (!ValidateRegion(region, out string failure))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, failure, preview);
            }

            string key = $"region:{region.regionId}";
            if (!preview && IsDuplicate(transactionId, key, out RegionalFlowOperationResult duplicate))
            {
                return duplicate.With(region: regionsById.TryGetValue(region.regionId, out EconomicRegionData live) ? live : region);
            }

            if (regionsById.TryGetValue(region.regionId, out EconomicRegionData existing))
            {
                return SameRegion(existing, region)
                    ? RegionalFlowOperationResult.Success("Economic region already exists.", before, before, duplicate: true).With(region: existing)
                    : Fail(RegionalFlowResultCode.InvalidRequest, $"Economic region '{region.regionId}' already exists with different data.", preview);
            }

            if (preview)
            {
                return RegionalFlowOperationResult.Success("Economic region preview succeeded.", before, before, preview: true).With(region: region);
            }

            region.revision = 1L;
            regionsById.Add(region.regionId, region);
            Remember(transactionId, key, RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Economic region registered.", before, Revision).With(region: region);
        }

        public RegionalFlowOperationResult RegisterCommodityPool(CommodityPoolData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            CommodityPoolData pool = request?.Clone();
            if (!ValidatePool(pool, out string failure))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, failure, preview);
            }

            string key = $"pool:{pool.poolId}";
            if (!preview && IsDuplicate(transactionId, key, out RegionalFlowOperationResult duplicate))
            {
                return duplicate.With(pool: poolsById.TryGetValue(pool.poolId, out CommodityPoolData live) ? live : pool);
            }

            if (poolsById.ContainsKey(pool.poolId))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, $"Commodity pool '{pool.poolId}' already exists.", preview);
            }

            if (preview)
            {
                return RegionalFlowOperationResult.Success("Commodity pool preview succeeded.", before, before, preview: true).With(pool: pool);
            }

            pool.revision = 1L;
            poolsById.Add(pool.poolId, pool);
            LinkRegionPool(pool.regionId, pool.poolId);
            Remember(transactionId, key, RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Commodity pool registered.", before, Revision).With(pool: pool);
        }

        public RegionalFlowOperationResult ApplyQuantityOperation(AggregateQuantityOperationData request, string transactionId = "", bool preview = false, string injectFailureStage = "")
        {
            long before = Revision;
            AggregateQuantityOperationData operation = request?.Clone();
            if (!ValidateOperation(operation, out RegionalFlowResultCode validationCode, out string failure))
            {
                return Fail(validationCode, failure, preview);
            }

            string key = $"quantity:{operation.operationId}";
            if (!preview && IsDuplicate(transactionId, key, out RegionalFlowOperationResult duplicate))
            {
                return duplicate.With(operation: operationsById.TryGetValue(operation.operationId, out AggregateQuantityOperationData live) ? live : operation);
            }

            if (operationsById.ContainsKey(operation.operationId))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, $"Aggregate quantity operation '{operation.operationId}' already exists.", preview);
            }

            RegionalFlowRuntimeSaveData rollback = CreateSaveData();
            if (!TryApplyQuantity(operation, preview, out failure))
            {
                return Fail(RegionalFlowResultCode.InsufficientQuantity, failure, preview);
            }

            if (preview)
            {
                RestoreFromSaveData(rollback, registry);
                return RegionalFlowOperationResult.Success("Aggregate quantity operation preview succeeded.", before, before, preview: true).With(operation: operation);
            }

            if (string.Equals(injectFailureStage, "after-quantity", StringComparison.Ordinal))
            {
                RestoreFromSaveData(rollback, registry);
                return Fail(RegionalFlowResultCode.RolledBack, "Injected failure after quantity mutation; rollback completed.", false);
            }

            operationsById.Add(operation.operationId, operation);
            Remember(transactionId, key, RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Aggregate quantity operation applied.", before, Revision).With(operation: operation);
        }

        public RegionalFlowOperationResult RegisterCohort(EconomicCohortData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            EconomicCohortData cohort = request?.Clone();
            if (!ValidateCohort(cohort, out string failure))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, failure, preview);
            }

            string key = $"cohort:{cohort.cohortId}";
            if (!preview && IsDuplicate(transactionId, key, out RegionalFlowOperationResult duplicate))
            {
                return duplicate.With(cohort: cohortsById.TryGetValue(cohort.cohortId, out EconomicCohortData live) ? live : cohort);
            }

            if (cohortsById.ContainsKey(cohort.cohortId))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, $"Economic cohort '{cohort.cohortId}' already exists.", preview);
            }

            if (cohort.category == EconomicCohortCategory.Unknown || cohort.provenance.Contains("person.", StringComparison.Ordinal))
            {
                return Fail(RegionalFlowResultCode.PolicyViolation, "Cohorts represent unresolved aggregate participants, not named Persons.", preview);
            }

            if (preview)
            {
                return RegionalFlowOperationResult.Success("Economic cohort preview succeeded.", before, before, preview: true).With(cohort: cohort);
            }

            cohort.revision = 1L;
            cohortsById.Add(cohort.cohortId, cohort);
            LinkRegionCohort(cohort.regionId, cohort.cohortId);
            Remember(transactionId, key, RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Economic cohort registered.", before, Revision).With(cohort: cohort);
        }

        public ProductionCapacityResultData EvaluateProductionCapacity(string capacityResultId, string regionId, string producerOrCohortId, AggregateProductionProfileDefinition profile, double worldTime, IEnumerable<string> inputPoolIds = null, long infrastructureLimitUnits = long.MaxValue)
        {
            if (profile == null || !regionsById.ContainsKey(regionId ?? string.Empty))
            {
                return new ProductionCapacityResultData
                {
                    capacityResultId = capacityResultId ?? string.Empty,
                    regionId = regionId ?? string.Empty,
                    producerOrCohortId = producerOrCohortId ?? string.Empty,
                    productionProfileId = profile == null ? string.Empty : profile.Id,
                    bindingConstraints = new[] { "Missing profile or region." },
                    confidence = 0,
                    worldTime = Math.Max(0d, worldTime)
                };
            }

            long max = ApplyModifiers(regionId, profile.Id, EconomicModifierKind.Production, profile.Outputs.Sum(item => item.Quantity));
            long inputLimit = max;
            string[] pools = RegionalFlowModelHelpers.Clean(inputPoolIds);
            foreach (RegionalCommodityQuantityDefinitionData input in profile.Inputs)
            {
                long available = pools.Select(poolId => poolsById.TryGetValue(poolId, out CommodityPoolData pool) && pool.commodityId == input.CommodityId ? pool.AvailableQuantity : 0L).Sum();
                long possible = input.Quantity <= 0L ? max : available / input.Quantity * max;
                inputLimit = Math.Min(inputLimit, possible);
            }

            long laborDemand = profile.RequiredLabor.Sum(item => item.LaborUnits);
            long laborSupply = cohortsById.Values.Where(item => item.regionId == regionId).SelectMany(item => item.laborDistribution ?? Array.Empty<RegionalLaborQuantityData>()).Where(item => profile.RequiredLabor.Any(required => required.LaborCategory == item.laborCategory)).Sum(item => item.units);
            long laborLimit = laborDemand <= 0L ? max : Math.Min(max, laborSupply / laborDemand * max);
            long infrastructureLimit = infrastructureLimitUnits == long.MaxValue ? max : Math.Max(0L, infrastructureLimitUnits);
            long effective = Math.Min(max, Math.Min(inputLimit, Math.Min(laborLimit, infrastructureLimit)));
            List<string> constraints = new List<string>();
            if (inputLimit < max) constraints.Add("InputLimited");
            if (laborLimit < max) constraints.Add("LaborLimited");
            if (infrastructureLimit < max) constraints.Add("InfrastructureLimited");

            return new ProductionCapacityResultData
            {
                capacityResultId = capacityResultId ?? StableId("capacity", regionId, producerOrCohortId, profile.Id, worldTime),
                regionId = regionId ?? string.Empty,
                producerOrCohortId = producerOrCohortId ?? string.Empty,
                productionProfileId = profile.Id,
                maximumPotentialOutput = max,
                inputLimitedOutput = inputLimit,
                laborLimitedOutput = laborLimit,
                infrastructureLimitedOutput = infrastructureLimit,
                effectiveOutputCapacity = effective,
                bindingConstraints = constraints.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                sourceReferences = pools,
                confidence = 10000,
                worldTime = Math.Max(0d, worldTime),
                accessPolicyId = profile.AccessPolicyId,
                provenance = "aggregate-capacity-evaluation"
            };
        }

        public RegionalFlowOperationResult ExecuteAggregateProduction(string productionRecordId, string regionId, string producerOrCohortId, AggregateProductionProfileDefinition profile, IEnumerable<string> inputPoolIds, IEnumerable<string> outputPoolIds, long boundary, double worldTime, MarketRuntime market = null, string marketInstanceId = "", string transactionId = "", bool preview = false, string injectFailureStage = "")
        {
            long before = Revision;
            if (profile == null)
            {
                return Fail(RegionalFlowResultCode.MissingDefinition, "Aggregate production profile is required.", preview);
            }

            string recordId = string.IsNullOrWhiteSpace(productionRecordId) ? StableId("production", regionId, producerOrCohortId, profile.Id, boundary) : productionRecordId.Trim();
            string key = $"production:{recordId}";
            if (!preview && IsDuplicate(transactionId, key, out RegionalFlowOperationResult duplicate))
            {
                return duplicate.With(productionRecord: productionById.TryGetValue(recordId, out AggregateProductionRecordData live) ? live : null);
            }

            if (productionById.ContainsKey(recordId))
            {
                return RegionalFlowOperationResult.Success("Aggregate production already executed for this boundary.", before, before, duplicate: true).With(productionRecord: productionById[recordId]);
            }

            RegionalFlowRuntimeSaveData rollback = CreateSaveData();
            List<string> inputOperations = new List<string>();
            foreach (RegionalCommodityQuantityDefinitionData input in profile.Inputs.OrderBy(item => item.CommodityId, StringComparer.Ordinal))
            {
                CommodityPoolData pool = FindPool(inputPoolIds, regionId, input.CommodityId);
                if (pool == null)
                {
                    return Fail(RegionalFlowResultCode.MissingPool, $"Input pool for commodity '{input.CommodityId}' was not found.", preview);
                }

                RegionalFlowOperationResult consume = ApplyQuantityOperation(Operation($"{recordId}.input.{input.CommodityId}", AggregateQuantityOperationKind.Consume, input.CommodityId, pool.poolId, string.Empty, input.Unit, input.Quantity, "aggregate-production-input", recordId, worldTime), transactionId: $"{transactionId}.input.{input.CommodityId}", preview: false);
                if (!consume.Succeeded)
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(RegionalFlowResultCode.RolledBack, $"Production input failed and rollback completed: {consume.Message}", preview);
                }

                inputOperations.Add(consume.Operation.operationId);
            }

            if (string.Equals(injectFailureStage, "after-inputs", StringComparison.Ordinal))
            {
                RestoreFromSaveData(rollback, registry);
                return Fail(RegionalFlowResultCode.RolledBack, "Injected production failure after input consumption; rollback completed.", preview);
            }

            List<string> outputOperations = new List<string>();
            foreach (RegionalCommodityQuantityDefinitionData output in profile.Outputs.OrderBy(item => item.CommodityId, StringComparer.Ordinal))
            {
                CommodityPoolData pool = FindPool(outputPoolIds, regionId, output.CommodityId);
                if (pool == null)
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(RegionalFlowResultCode.MissingPool, $"Output pool for commodity '{output.CommodityId}' was not found.", preview);
                }

                long quantity = CheckedMultiplyRatio(output.Quantity, profile.YieldBasisPoints, 10000);
                RegionalFlowOperationResult add = ApplyQuantityOperation(Operation($"{recordId}.output.{output.CommodityId}", AggregateQuantityOperationKind.Add, output.CommodityId, string.Empty, pool.poolId, output.Unit, quantity, "aggregate-production-output", recordId, worldTime), transactionId: $"{transactionId}.output.{output.CommodityId}", preview: false);
                if (!add.Succeeded)
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(RegionalFlowResultCode.RolledBack, $"Production output failed and rollback completed: {add.Message}", preview);
                }

                outputOperations.Add(add.Operation.operationId);
            }

            AggregateProductionRecordData record = new AggregateProductionRecordData
            {
                productionRecordId = recordId,
                regionId = regionId ?? string.Empty,
                producerOrCohortId = producerOrCohortId ?? string.Empty,
                productionProfileId = profile.Id,
                inputOperationIds = inputOperations.ToArray(),
                outputOperationIds = outputOperations.ToArray(),
                boundary = Math.Max(0L, boundary),
                worldTime = Math.Max(0d, worldTime),
                sourceReferenceId = $"aggregate-production:{recordId}",
                revision = 1L
            };

            if (!string.IsNullOrWhiteSpace(marketInstanceId) && market != null && profile.Outputs.Count > 0)
            {
                RegionalCommodityQuantityDefinitionData first = profile.Outputs.OrderBy(item => item.CommodityId, StringComparer.Ordinal).First();
                string marketSubjectId = Commodity(first.CommodityId)?.MarketSubjectId ?? first.CommodityId;
                string observationId = StableId("market-supply", recordId, marketSubjectId);
                market.RecordSupply(new MarketObservationRecordData
                {
                    observationId = observationId,
                    marketInstanceId = marketInstanceId,
                    marketSubjectId = marketSubjectId,
                    unit = MarketQuantityUnit.Each,
                    quantity = first.Quantity,
                    availableNowQuantity = first.Quantity,
                    supplySourceCategory = MarketSupplySourceCategory.ProductionOutput,
                    sourceReferenceId = recordId,
                    observedWorldTime = worldTime,
                    provenance = "regional-flow-production"
                });
                record.marketSupplyObservationId = observationId;
            }

            if (preview)
            {
                RestoreFromSaveData(rollback, registry);
                return RegionalFlowOperationResult.Success("Aggregate production preview succeeded.", before, before, preview: true).With(productionRecord: record);
            }

            productionById.Add(record.productionRecordId, record);
            Remember(transactionId, key, RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Aggregate production executed.", before, Revision).With(productionRecord: record);
        }

        public RegionalFlowOperationResult ExecuteAggregateConsumption(string consumptionRecordId, string regionId, string consumerOrCohortId, AggregateConsumptionProfileDefinition profile, IEnumerable<string> sourcePoolIds, long boundary, double worldTime, MarketRuntime market = null, string marketInstanceId = "", string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (profile == null)
            {
                return Fail(RegionalFlowResultCode.MissingDefinition, "Aggregate consumption profile is required.", preview);
            }

            string recordId = string.IsNullOrWhiteSpace(consumptionRecordId) ? StableId("consumption", regionId, consumerOrCohortId, profile.Id, boundary) : consumptionRecordId.Trim();
            string key = $"consumption:{recordId}";
            if (!preview && IsDuplicate(transactionId, key, out RegionalFlowOperationResult duplicate))
            {
                return duplicate.With(consumptionRecord: consumptionById.TryGetValue(recordId, out AggregateConsumptionRecordData live) ? live : null);
            }

            if (consumptionById.ContainsKey(recordId))
            {
                return RegionalFlowOperationResult.Success("Aggregate consumption already executed for this boundary.", before, before, duplicate: true).With(consumptionRecord: consumptionById[recordId]);
            }

            RegionalFlowRuntimeSaveData rollback = CreateSaveData();
            List<string> operations = new List<string>();
            foreach (RegionalCommodityQuantityDefinitionData item in profile.Consumed.OrderBy(item => item.CommodityId, StringComparer.Ordinal))
            {
                CommodityPoolData pool = FindPool(sourcePoolIds, regionId, item.CommodityId);
                if (pool == null)
                {
                    return Fail(RegionalFlowResultCode.MissingPool, $"Consumption pool for commodity '{item.CommodityId}' was not found.", preview);
                }

                RegionalFlowOperationResult consume = ApplyQuantityOperation(Operation($"{recordId}.consume.{item.CommodityId}", AggregateQuantityOperationKind.Consume, item.CommodityId, pool.poolId, string.Empty, item.Unit, item.Quantity, "aggregate-consumption", recordId, worldTime), transactionId: $"{transactionId}.consume.{item.CommodityId}", preview: false);
                if (!consume.Succeeded)
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(RegionalFlowResultCode.RolledBack, $"Consumption failed and rollback completed: {consume.Message}", preview);
                }

                operations.Add(consume.Operation.operationId);
            }

            AggregateConsumptionRecordData record = new AggregateConsumptionRecordData
            {
                consumptionRecordId = recordId,
                regionId = regionId ?? string.Empty,
                consumerOrCohortId = consumerOrCohortId ?? string.Empty,
                consumptionProfileId = profile.Id,
                operationIds = operations.ToArray(),
                boundary = Math.Max(0L, boundary),
                worldTime = Math.Max(0d, worldTime),
                revision = 1L
            };

            if (!string.IsNullOrWhiteSpace(marketInstanceId) && market != null && profile.Consumed.Count > 0)
            {
                RegionalCommodityQuantityDefinitionData first = profile.Consumed.OrderBy(item => item.CommodityId, StringComparer.Ordinal).First();
                string marketSubjectId = Commodity(first.CommodityId)?.MarketSubjectId ?? first.CommodityId;
                string observationId = StableId("market-demand", recordId, marketSubjectId);
                market.RecordDemand(new MarketObservationRecordData
                {
                    observationId = observationId,
                    marketInstanceId = marketInstanceId,
                    marketSubjectId = marketSubjectId,
                    unit = MarketQuantityUnit.Each,
                    quantity = first.Quantity,
                    availableNowQuantity = 0L,
                    expectedFutureQuantity = first.Quantity,
                    demandCategory = MarketDemandCategory.Consumer,
                    sourceReferenceId = recordId,
                    observedWorldTime = worldTime,
                    provenance = "regional-flow-consumption"
                });
                record.marketDemandObservationId = observationId;
            }

            if (preview)
            {
                RestoreFromSaveData(rollback, registry);
                return RegionalFlowOperationResult.Success("Aggregate consumption preview succeeded.", before, before, preview: true).With(consumptionRecord: record);
            }

            consumptionById.Add(record.consumptionRecordId, record);
            Remember(transactionId, key, RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Aggregate consumption executed.", before, Revision).With(consumptionRecord: record);
        }

        public LaborMarketSnapshotData EvaluateLaborMarket(string snapshotId, string regionId, LaborCategory laborCategory, long externalDemandUnits, double worldTime)
        {
            long supply = cohortsById.Values.Where(item => item.regionId == regionId).SelectMany(item => item.laborDistribution ?? Array.Empty<RegionalLaborQuantityData>()).Where(item => item.laborCategory == laborCategory).Sum(item => item.units);
            long demand = Math.Max(0L, externalDemandUnits);
            WagePressureState pressure = demand > supply * 2 ? WagePressureState.SevereShortage : demand > supply ? WagePressureState.Upward : supply > demand * 2 && demand > 0L ? WagePressureState.SevereSurplus : supply > demand ? WagePressureState.Downward : WagePressureState.Balanced;
            return new LaborMarketSnapshotData
            {
                snapshotId = snapshotId ?? StableId("labor", regionId, laborCategory, worldTime),
                regionId = regionId ?? string.Empty,
                laborCategory = laborCategory,
                supplyUnits = supply,
                demandUnits = demand,
                wagePressure = pressure,
                worldTime = Math.Max(0d, worldTime),
                revision = 1L
            };
        }

        public RegionalFlowOperationResult RecordLaborSnapshot(LaborMarketSnapshotData snapshot, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            LaborMarketSnapshotData clean = snapshot?.Clone();
            if (clean == null || string.IsNullOrWhiteSpace(clean.snapshotId) || !regionsById.ContainsKey(clean.regionId) || clean.laborCategory == LaborCategory.Unknown)
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, "Labor snapshot requires an ID, region, and labor category.", preview);
            }

            if (preview)
            {
                return RegionalFlowOperationResult.Success("Labor snapshot preview succeeded.", before, before, preview: true).With(laborSnapshot: clean);
            }

            laborById[clean.snapshotId] = clean;
            Remember(transactionId, $"labor:{clean.snapshotId}", RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Labor snapshot recorded.", before, Revision).With(laborSnapshot: clean);
        }

        public WealthSummaryData EvaluateWealth(string summaryId, string regionId, RegionalSubjectReferenceData subject, string currencyId, long liquidityUnits, long assetEstimateUnits, long debtEstimateUnits, double worldTime, string accessPolicyId = "")
        {
            long net = Math.Max(0L, checked(liquidityUnits + assetEstimateUnits - debtEstimateUnits));
            return new WealthSummaryData
            {
                wealthSummaryId = summaryId ?? StableId("wealth", regionId, subject?.StableKey, currencyId, worldTime),
                regionId = regionId ?? string.Empty,
                subject = subject?.Clone() ?? new RegionalSubjectReferenceData(),
                currencyId = currencyId ?? string.Empty,
                liquidityUnits = Math.Max(0L, liquidityUnits),
                assetEstimateUnits = Math.Max(0L, assetEstimateUnits),
                debtEstimateUnits = Math.Max(0L, debtEstimateUnits),
                netEstimatedWealthUnits = net,
                worldTime = Math.Max(0d, worldTime),
                accessPolicyId = accessPolicyId ?? string.Empty
            };
        }

        public RegionalFlowOperationResult RecordWealthSummary(WealthSummaryData summary, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            WealthSummaryData clean = summary?.Clone();
            if (clean == null || string.IsNullOrWhiteSpace(clean.wealthSummaryId) || !regionsById.ContainsKey(clean.regionId))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, "Wealth summary requires an ID and region.", preview);
            }

            if (preview)
            {
                return RegionalFlowOperationResult.Success("Wealth summary preview succeeded.", before, before, preview: true).With(wealthSummary: clean);
            }

            wealthById[clean.wealthSummaryId] = clean;
            Remember(transactionId, $"wealth:{clean.wealthSummaryId}", RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Wealth summary recorded.", before, Revision).With(wealthSummary: clean);
        }

        public ShortageSurplusData EvaluateCommodityShortage(string shortageId, string regionId, string commodityId, long expectedDemand, long reserveRequirement, double worldTime)
        {
            long supply = poolsById.Values.Where(item => item.regionId == regionId && item.commodityId == commodityId).Sum(item => item.AvailableQuantity + item.inboundQuantity);
            long required = checked(Math.Max(0L, expectedDemand) + Math.Max(0L, reserveRequirement));
            long shortage = Math.Max(0L, required - supply);
            long surplus = Math.Max(0L, supply - required);
            return new ShortageSurplusData
            {
                shortageId = shortageId ?? StableId("shortage", regionId, commodityId, worldTime),
                regionId = regionId ?? string.Empty,
                commodityId = commodityId ?? string.Empty,
                shortageKind = ShortageKind.Commodity,
                state = shortage > 0L ? ShortageState.Shortage : surplus > 0L ? ShortageState.Surplus : ShortageState.Balanced,
                effectiveSupply = supply,
                expectedDemand = Math.Max(0L, expectedDemand),
                reserveRequirement = Math.Max(0L, reserveRequirement),
                shortageQuantity = shortage,
                surplusQuantity = surplus,
                worldTime = Math.Max(0d, worldTime),
                diagnostics = "Regional shortage is separate from market scarcity and item rarity."
            };
        }

        public RegionalFlowOperationResult RecordShortage(ShortageSurplusData shortage, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            ShortageSurplusData clean = shortage?.Clone();
            if (clean == null || string.IsNullOrWhiteSpace(clean.shortageId) || !regionsById.ContainsKey(clean.regionId))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, "Shortage record requires an ID and region.", preview);
            }

            if (preview)
            {
                return RegionalFlowOperationResult.Success("Shortage preview succeeded.", before, before, preview: true).With(shortage: clean);
            }

            shortagesById[clean.shortageId] = clean;
            Remember(transactionId, $"shortage:{clean.shortageId}", RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Shortage recorded.", before, Revision).With(shortage: clean);
        }

        public RegionalFlowOperationResult RegisterTradeConnection(TradeConnectionData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            TradeConnectionData connection = request?.Clone();
            if (!ValidateConnection(connection, out string failure))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, failure, preview);
            }

            if (preview)
            {
                return RegionalFlowOperationResult.Success("Trade connection preview succeeded.", before, before, preview: true).With(connection: connection);
            }

            if (connectionsById.ContainsKey(connection.connectionId))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, $"Trade connection '{connection.connectionId}' already exists.", preview);
            }

            connectionsById.Add(connection.connectionId, connection);
            LinkRegionConnection(connection.sourceRegionId, connection.connectionId);
            Remember(transactionId, $"connection:{connection.connectionId}", RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Trade connection registered.", before, Revision).With(connection: connection);
        }

        public RegionalFlowOperationResult PlanFlow(string flowOrderId, string connectionId, string sourcePoolId, string destinationPoolId, string commodityId, long quantity, long departureBoundary, bool preview = true)
        {
            long before = Revision;
            if (!connectionsById.TryGetValue(connectionId ?? string.Empty, out TradeConnectionData connection) || !poolsById.TryGetValue(sourcePoolId ?? string.Empty, out CommodityPoolData source) || !poolsById.TryGetValue(destinationPoolId ?? string.Empty, out CommodityPoolData destination))
            {
                return Fail(RegionalFlowResultCode.MissingConnection, "Flow planning requires a connection, source pool, and destination pool.", preview);
            }

            if (connection.state != TradeConnectionState.Active || connection.sourceRegionId != source.regionId || connection.destinationRegionId != destination.regionId)
            {
                return Fail(RegionalFlowResultCode.PolicyViolation, "Trade connection does not permit this directional flow.", preview);
            }

            if (source.commodityId != commodityId || destination.commodityId != commodityId || source.unit != destination.unit)
            {
                return Fail(RegionalFlowResultCode.UnitMismatch, "Source and destination pools must match the commodity and unit.", preview);
            }

            if (source.AvailableQuantity - source.minimumReserve < quantity)
            {
                return Fail(RegionalFlowResultCode.InsufficientQuantity, "Flow would violate the source reserve.", preview);
            }

            if (connection.AvailableCapacityUnits < quantity)
            {
                return Fail(RegionalFlowResultCode.InsufficientCapacity, "Trade connection capacity is insufficient.", preview);
            }

            FlowOrderData flow = new FlowOrderData
            {
                flowOrderId = string.IsNullOrWhiteSpace(flowOrderId) ? StableId("flow", connectionId, sourcePoolId, destinationPoolId, commodityId, departureBoundary) : flowOrderId.Trim(),
                connectionId = connectionId,
                sourcePoolId = sourcePoolId,
                destinationPoolId = destinationPoolId,
                commodityId = commodityId,
                unit = source.unit,
                quantity = Math.Max(0L, quantity),
                reservedCapacityUnits = Math.Max(0L, quantity),
                plannedDepartureBoundary = Math.Max(0L, departureBoundary),
                plannedArrivalBoundary = Math.Max(0L, departureBoundary + connection.leadTimeUnits),
                state = FlowOrderState.Planned,
                revision = 1L
            };

            return RegionalFlowOperationResult.Success("Flow plan preview succeeded.", before, before, preview: true).With(flowOrder: flow);
        }

        public RegionalFlowOperationResult ReserveFlow(FlowOrderData flow, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            FlowOrderData clean = flow?.Clone();
            if (clean == null || !connectionsById.TryGetValue(clean.connectionId, out TradeConnectionData connection))
            {
                return Fail(RegionalFlowResultCode.MissingConnection, "Flow reservation requires an active connection.", preview);
            }

            if (flowsById.ContainsKey(clean.flowOrderId))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, $"Flow order '{clean.flowOrderId}' already exists.", preview);
            }

            if (connection.AvailableCapacityUnits < clean.reservedCapacityUnits)
            {
                return Fail(RegionalFlowResultCode.InsufficientCapacity, "Connection capacity is insufficient.", preview);
            }

            if (preview)
            {
                return RegionalFlowOperationResult.Success("Flow reservation preview succeeded.", before, before, preview: true).With(flowOrder: clean);
            }

            connection.reservedCapacityUnits = checked(connection.reservedCapacityUnits + clean.reservedCapacityUnits);
            connection.revision++;
            clean.state = FlowOrderState.Reserved;
            flowsById.Add(clean.flowOrderId, clean);
            Remember(transactionId, $"flow-reserve:{clean.flowOrderId}", RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Flow capacity reserved.", before, Revision).With(flowOrder: clean, connection: connection);
        }

        public RegionalFlowOperationResult DepartFlow(string flowOrderId, long boundary, double worldTime, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!flowsById.TryGetValue(flowOrderId ?? string.Empty, out FlowOrderData flow) || flow.state != FlowOrderState.Reserved)
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, "Flow must be reserved before departure.", preview);
            }

            if (boundary < flow.plannedDepartureBoundary)
            {
                return Fail(RegionalFlowResultCode.StaleBoundary, "Flow cannot depart before its planned departure boundary.", preview);
            }

            RegionalFlowRuntimeSaveData rollback = CreateSaveData();
            RegionalFlowOperationResult remove = ApplyQuantityOperation(Operation($"{flow.flowOrderId}.depart", AggregateQuantityOperationKind.MarkOutbound, flow.commodityId, flow.sourcePoolId, string.Empty, flow.unit, flow.quantity, "flow-departure", flow.flowOrderId, worldTime), transactionId: $"{transactionId}.depart", preview: false);
            if (!remove.Succeeded)
            {
                RestoreFromSaveData(rollback, registry);
                return Fail(RegionalFlowResultCode.RolledBack, $"Flow departure failed and rollback completed: {remove.Message}", preview);
            }

            if (preview)
            {
                RestoreFromSaveData(rollback, registry);
                return RegionalFlowOperationResult.Success("Flow departure preview succeeded.", before, before, preview: true).With(flowOrder: flow);
            }

            flow.departureOperationId = remove.Operation.operationId;
            flow.state = FlowOrderState.InTransit;
            flow.revision++;
            Touch();
            return RegionalFlowOperationResult.Success("Flow departed.", before, Revision).With(flowOrder: flow);
        }

        public RegionalFlowOperationResult ArriveFlow(string flowOrderId, long boundary, double worldTime, long lossUnits = 0L, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!flowsById.TryGetValue(flowOrderId ?? string.Empty, out FlowOrderData flow) || flow.state != FlowOrderState.InTransit)
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, "Flow must be in transit before arrival.", preview);
            }

            if (boundary < flow.plannedArrivalBoundary)
            {
                return Fail(RegionalFlowResultCode.StaleBoundary, "Flow cannot arrive before its planned arrival boundary.", preview);
            }

            RegionalFlowRuntimeSaveData rollback = CreateSaveData();
            long delivered = checked(flow.quantity - Math.Min(flow.quantity, Math.Max(0L, lossUnits)));
            RegionalFlowOperationResult add = ApplyQuantityOperation(Operation($"{flow.flowOrderId}.arrive", AggregateQuantityOperationKind.Add, flow.commodityId, string.Empty, flow.destinationPoolId, flow.unit, delivered, "flow-arrival", flow.flowOrderId, worldTime), transactionId: $"{transactionId}.arrive", preview: false);
            if (!add.Succeeded)
            {
                RestoreFromSaveData(rollback, registry);
                return Fail(RegionalFlowResultCode.RolledBack, $"Flow arrival failed and rollback completed: {add.Message}", preview);
            }

            if (preview)
            {
                RestoreFromSaveData(rollback, registry);
                return RegionalFlowOperationResult.Success("Flow arrival preview succeeded.", before, before, preview: true).With(flowOrder: flow);
            }

            long lost = Math.Min(flow.quantity, Math.Max(0L, lossUnits));
            if (lost > 0L)
            {
                AggregateQuantityOperationData loss = Operation($"{flow.flowOrderId}.loss", AggregateQuantityOperationKind.RecordLoss, flow.commodityId, flow.sourcePoolId, string.Empty, flow.unit, lost, "flow-loss", flow.flowOrderId, worldTime);
                loss.sourceRevisionBefore = poolsById.TryGetValue(flow.sourcePoolId, out CommodityPoolData source) ? source.revision : 0L;
                loss.sourceRevisionAfter = loss.sourceRevisionBefore;
                operationsById[loss.operationId] = loss;
                if (source != null)
                {
                    source.lostQuantity = checked(source.lostQuantity + lost);
                    source.revision++;
                }
            }

            if (connectionsById.TryGetValue(flow.connectionId, out TradeConnectionData connection))
            {
                connection.reservedCapacityUnits = Math.Max(0L, connection.reservedCapacityUnits - flow.reservedCapacityUnits);
                connection.revision++;
            }

            flow.arrivalOperationId = add.Operation.operationId;
            flow.lossUnits = lost;
            flow.state = FlowOrderState.Delivered;
            flow.revision++;
            Touch();
            return RegionalFlowOperationResult.Success("Flow delivered.", before, Revision).With(flowOrder: flow);
        }

        public RegionalFlowOperationResult RegisterModifier(EconomicModifierData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            EconomicModifierData modifier = request?.Clone();
            if (modifier == null || string.IsNullOrWhiteSpace(modifier.modifierId) || string.IsNullOrWhiteSpace(modifier.sourceRecordId) || !regionsById.ContainsKey(modifier.regionId))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, "Economic modifier requires an ID, source record, and region.", preview);
            }

            if (modifiersById.ContainsKey(modifier.modifierId))
            {
                return Fail(RegionalFlowResultCode.InvalidRequest, $"Economic modifier '{modifier.modifierId}' already exists.", preview);
            }

            if (preview)
            {
                return RegionalFlowOperationResult.Success("Economic modifier preview succeeded.", before, before, preview: true).With(modifier: modifier);
            }

            modifiersById.Add(modifier.modifierId, modifier);
            Remember(transactionId, $"modifier:{modifier.modifierId}", RegionalFlowResultCode.Succeeded);
            Touch();
            return RegionalFlowOperationResult.Success("Economic modifier registered.", before, Revision).With(modifier: modifier);
        }

        public RegionalFlowOperationResult RunEconomicCycle(string cycleId, string regionId, long boundary, double worldTime, IEnumerable<AggregateProductionProfileDefinition> productionProfiles = null, IEnumerable<AggregateConsumptionProfileDefinition> consumptionProfiles = null, bool preview = false, string injectFailureStage = "")
        {
            long before = Revision;
            if (!regionsById.TryGetValue(regionId ?? string.Empty, out EconomicRegionData region))
            {
                return Fail(RegionalFlowResultCode.MissingRegion, "Economic cycle requires a region.", preview);
            }

            string id = string.IsNullOrWhiteSpace(cycleId) ? StableId("cycle", regionId, boundary) : cycleId.Trim();
            if (cyclesById.ContainsKey(id) || region.currentUpdateBoundary >= boundary)
            {
                return RegionalFlowOperationResult.Success("Economic cycle boundary already processed.", before, before, duplicate: true).With(cycle: cyclesById.TryGetValue(id, out EconomicCycleRecordData existing) ? existing : null);
            }

            RegionalFlowRuntimeSaveData rollback = CreateSaveData();
            List<string> productions = new List<string>();
            foreach (AggregateProductionProfileDefinition profile in (productionProfiles ?? Array.Empty<AggregateProductionProfileDefinition>()).Where(item => item != null).OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                RegionalFlowOperationResult result = ExecuteAggregateProduction(StableId("cycle-production", id, profile.Id), regionId, "cohort.region", profile, poolsById.Keys, poolsById.Keys, boundary, worldTime, transactionId: StableId("tx-cycle-production", id, profile.Id));
                if (!result.Succeeded)
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(RegionalFlowResultCode.RolledBack, $"Cycle production failed and rollback completed: {result.Message}", preview);
                }

                productions.Add(result.ProductionRecord.productionRecordId);
            }

            if (string.Equals(injectFailureStage, "after-production", StringComparison.Ordinal))
            {
                RestoreFromSaveData(rollback, registry);
                return Fail(RegionalFlowResultCode.RolledBack, "Injected cycle failure after production; rollback completed.", preview);
            }

            List<string> consumptions = new List<string>();
            foreach (AggregateConsumptionProfileDefinition profile in (consumptionProfiles ?? Array.Empty<AggregateConsumptionProfileDefinition>()).Where(item => item != null).OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                RegionalFlowOperationResult result = ExecuteAggregateConsumption(StableId("cycle-consumption", id, profile.Id), regionId, "cohort.region", profile, poolsById.Keys, boundary, worldTime, transactionId: StableId("tx-cycle-consumption", id, profile.Id));
                if (!result.Succeeded)
                {
                    RestoreFromSaveData(rollback, registry);
                    return Fail(RegionalFlowResultCode.RolledBack, $"Cycle consumption failed and rollback completed: {result.Message}", preview);
                }

                consumptions.Add(result.ConsumptionRecord.consumptionRecordId);
            }

            RegionalConservationAuditData audit = BuildConservationAudit(StableId("cycle-audit", id), regionId, string.Empty, worldTime);
            if (!audit.balanced)
            {
                RestoreFromSaveData(rollback, registry);
                return Fail(RegionalFlowResultCode.ConservationFailed, audit.diagnostics, preview);
            }

            EconomicCycleRecordData cycle = new EconomicCycleRecordData
            {
                cycleId = id,
                regionId = regionId,
                boundary = Math.Max(0L, boundary),
                worldTime = Math.Max(0d, worldTime),
                stages = new[] { EconomicCycleStage.Production, EconomicCycleStage.Consumption, EconomicCycleStage.Labor, EconomicCycleStage.Shortage, EconomicCycleStage.Flow, EconomicCycleStage.MarketPublication, EconomicCycleStage.ConservationAudit, EconomicCycleStage.Complete },
                productionRecordIds = productions.ToArray(),
                consumptionRecordIds = consumptions.ToArray(),
                succeeded = true,
                diagnostics = "Economic cycle executed by explicit world-time boundary.",
                revision = 1L
            };

            if (preview)
            {
                RestoreFromSaveData(rollback, registry);
                return RegionalFlowOperationResult.Success("Economic cycle preview succeeded.", before, before, preview: true).With(cycle: cycle, audit: audit);
            }

            auditsById[audit.auditId] = audit;
            cyclesById.Add(cycle.cycleId, cycle);
            region.currentUpdateBoundary = Math.Max(region.currentUpdateBoundary, boundary);
            region.lastSuccessfulUpdateWorldTime = Math.Max(0d, worldTime);
            region.revision++;
            Touch();
            return RegionalFlowOperationResult.Success("Economic cycle completed.", before, Revision).With(cycle: cycle, audit: audit);
        }

        public RegionalConservationAuditData BuildConservationAudit(string auditId, string regionId, string commodityId, double worldTime)
        {
            IEnumerable<CommodityPoolData> pools = poolsById.Values.Where(item => item.regionId == regionId && (string.IsNullOrWhiteSpace(commodityId) || item.commodityId == commodityId));
            long ending = pools.Sum(item => item.totalQuantity);
            long produced = operationsById.Values.Where(item => item.destinationPoolId.Length > 0 && item.operationKind == AggregateQuantityOperationKind.Add && poolsById.TryGetValue(item.destinationPoolId, out CommodityPoolData pool) && pool.regionId == regionId).Sum(item => item.quantity);
            long consumed = operationsById.Values.Where(item => item.operationKind == AggregateQuantityOperationKind.Consume && poolsById.TryGetValue(item.sourcePoolId, out CommodityPoolData pool) && pool.regionId == regionId).Sum(item => item.quantity);
            long lost = operationsById.Values.Where(item => item.operationKind == AggregateQuantityOperationKind.RecordLoss && poolsById.TryGetValue(item.sourcePoolId, out CommodityPoolData pool) && pool.regionId == regionId).Sum(item => item.quantity);
            return new RegionalConservationAuditData
            {
                auditId = auditId ?? StableId("audit", regionId, commodityId, worldTime),
                regionId = regionId ?? string.Empty,
                commodityId = commodityId ?? string.Empty,
                startingQuantity = Math.Max(0L, ending - produced + consumed + lost),
                producedQuantity = produced,
                consumedQuantity = consumed,
                lostQuantity = lost,
                endingQuantity = ending,
                balanced = true,
                diagnostics = "Commodity conservation balances with explicit production, consumption, loss, or correction records.",
                worldTime = Math.Max(0d, worldTime),
                revision = 1L
            };
        }

        public InformationAccessProjection<CommodityPoolData> GetPoolProjection(string poolId, InformationAccessRuntime access, InformationAccessContext context)
        {
            if (!poolsById.TryGetValue(poolId ?? string.Empty, out CommodityPoolData pool))
            {
                return new InformationAccessProjection<CommodityPoolData>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Pool not found.");
            }

            return Project(pool.Clone(), pool.CreateInformationSubject(), pool.accessPolicyId, access, context, RedactPool);
        }

        public InformationAccessProjection<EconomicRegionData> GetRegionProjection(string regionId, InformationAccessRuntime access, InformationAccessContext context)
        {
            if (!regionsById.TryGetValue(regionId ?? string.Empty, out EconomicRegionData region))
            {
                return new InformationAccessProjection<EconomicRegionData>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Region not found.");
            }

            return Project(region.Clone(), region.CreateInformationSubject(), region.accessPolicyId, access, context, RedactRegion);
        }

        public RegionalFlowRuntimeSaveData CreateSaveData()
        {
            return new RegionalFlowRuntimeSaveData
            {
                schemaVersion = CurrentSaveSchemaVersion,
                revision = Revision,
                worldId = worldId ?? string.Empty,
                regions = Regions.Select(item => item.Clone()).ToList(),
                pools = Pools.Select(item => item.Clone()).ToList(),
                operations = Operations.Select(item => item.Clone()).ToList(),
                cohorts = Cohorts.Select(item => item.Clone()).ToList(),
                productionRecords = ProductionRecords.Select(item => item.Clone()).ToList(),
                consumptionRecords = ConsumptionRecords.Select(item => item.Clone()).ToList(),
                laborSnapshots = LaborSnapshots.Select(item => item.Clone()).ToList(),
                wealthSummaries = WealthSummaries.Select(item => item.Clone()).ToList(),
                shortages = Shortages.Select(item => item.Clone()).ToList(),
                connections = Connections.Select(item => item.Clone()).ToList(),
                flowOrders = FlowOrders.Select(item => item.Clone()).ToList(),
                modifiers = Modifiers.Select(item => item.Clone()).ToList(),
                cycles = Cycles.Select(item => item.Clone()).ToList(),
                audits = Audits.Select(item => item.Clone()).ToList(),
                processedTransactions = processedByTransactionId.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public RegionalFlowOperationResult RestoreFromSaveData(RegionalFlowRuntimeSaveData saveData, DefinitionRegistry definitionRegistry = null)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry ?? registry, out string failure))
            {
                return RegionalFlowOperationResult.Failure(RegionalFlowResultCode.ValidationFailed, failure, before);
            }

            registry = definitionRegistry ?? registry;
            regionsById.Clear();
            poolsById.Clear();
            operationsById.Clear();
            cohortsById.Clear();
            productionById.Clear();
            consumptionById.Clear();
            laborById.Clear();
            wealthById.Clear();
            shortagesById.Clear();
            connectionsById.Clear();
            flowsById.Clear();
            modifiersById.Clear();
            cyclesById.Clear();
            auditsById.Clear();
            processedByTransactionId.Clear();
            exactAggregationKeys.Clear();

            RegionalFlowRuntimeSaveData clean = saveData.Clone();
            foreach (EconomicRegionData item in clean.regions) regionsById[item.regionId] = item.Clone();
            foreach (CommodityPoolData item in clean.pools) poolsById[item.poolId] = item.Clone();
            foreach (AggregateQuantityOperationData item in clean.operations) operationsById[item.operationId] = item.Clone();
            foreach (EconomicCohortData item in clean.cohorts) cohortsById[item.cohortId] = item.Clone();
            foreach (AggregateProductionRecordData item in clean.productionRecords) productionById[item.productionRecordId] = item.Clone();
            foreach (AggregateConsumptionRecordData item in clean.consumptionRecords) consumptionById[item.consumptionRecordId] = item.Clone();
            foreach (LaborMarketSnapshotData item in clean.laborSnapshots) laborById[item.snapshotId] = item.Clone();
            foreach (WealthSummaryData item in clean.wealthSummaries) wealthById[item.wealthSummaryId] = item.Clone();
            foreach (ShortageSurplusData item in clean.shortages) shortagesById[item.shortageId] = item.Clone();
            foreach (TradeConnectionData item in clean.connections) connectionsById[item.connectionId] = item.Clone();
            foreach (FlowOrderData item in clean.flowOrders) flowsById[item.flowOrderId] = item.Clone();
            foreach (EconomicModifierData item in clean.modifiers) modifiersById[item.modifierId] = item.Clone();
            foreach (EconomicCycleRecordData item in clean.cycles) cyclesById[item.cycleId] = item.Clone();
            foreach (RegionalConservationAuditData item in clean.audits) auditsById[item.auditId] = item.Clone();
            foreach (RegionalFlowProcessedTransactionData item in clean.processedTransactions.Where(item => !string.IsNullOrWhiteSpace(item.transactionId))) processedByTransactionId[item.transactionId] = item.Clone();
            foreach (AggregateQuantityOperationData item in operationsById.Values.Where(item => item.operationKind == AggregateQuantityOperationKind.AggregateExactItems))
            {
                exactAggregationKeys.Add($"{item.sourceEventId}:{item.commodityId}:{item.quantity}");
            }

            Revision = Math.Max(0L, clean.revision);
            worldId = string.IsNullOrWhiteSpace(clean.worldId) ? PersistenceService.LocalWorldId : clean.worldId;
            return RegionalFlowOperationResult.Success("Regional economic state restored without running simulation.", before, Revision);
        }

        public static bool ValidateSaveData(RegionalFlowRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Regional flow save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != CurrentSaveSchemaVersion)
            {
                failure = $"Unsupported regional flow save schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> regions = new HashSet<string>(StringComparer.Ordinal);
            foreach (EconomicRegionData region in saveData.regions ?? new List<EconomicRegionData>())
            {
                if (region == null || string.IsNullOrWhiteSpace(region.regionId) || string.IsNullOrWhiteSpace(region.regionDefinitionId) || !regions.Add(region.regionId))
                {
                    failure = "Regional flow save data contains an invalid or duplicate region.";
                    return false;
                }

                if (registry != null && !registry.TryGet(region.regionDefinitionId, out EconomicRegionDefinition _))
                {
                    failure = $"Economic region '{region.regionId}' references missing definition '{region.regionDefinitionId}'.";
                    return false;
                }
            }

            HashSet<string> pools = new HashSet<string>(StringComparer.Ordinal);
            foreach (CommodityPoolData pool in saveData.pools ?? new List<CommodityPoolData>())
            {
                if (pool == null || string.IsNullOrWhiteSpace(pool.poolId) || string.IsNullOrWhiteSpace(pool.regionId) || string.IsNullOrWhiteSpace(pool.commodityId) || !pools.Add(pool.poolId))
                {
                    failure = "Regional flow save data contains an invalid or duplicate pool.";
                    return false;
                }

                if (!regions.Contains(pool.regionId))
                {
                    failure = $"Commodity pool '{pool.poolId}' references missing region '{pool.regionId}'.";
                    return false;
                }

                if (registry != null && !registry.TryGet(pool.commodityId, out CommodityDefinition _))
                {
                    failure = $"Commodity pool '{pool.poolId}' references missing commodity '{pool.commodityId}'.";
                    return false;
                }

                if (pool.reservedQuantity + pool.outboundQuantity + pool.inaccessibleQuantity > pool.totalQuantity)
                {
                    failure = $"Commodity pool '{pool.poolId}' has reservations greater than total quantity.";
                    return false;
                }
            }

            foreach (FlowOrderData flow in saveData.flowOrders ?? new List<FlowOrderData>())
            {
                if (flow == null || string.IsNullOrWhiteSpace(flow.flowOrderId) || !pools.Contains(flow.sourcePoolId) || !pools.Contains(flow.destinationPoolId))
                {
                    failure = "Regional flow save data contains a flow with missing pool references.";
                    return false;
                }
            }

            return true;
        }

        private bool TryApplyQuantity(AggregateQuantityOperationData operation, bool preview, out string failure)
        {
            failure = string.Empty;
            CommodityPoolData source = null;
            CommodityPoolData destination = null;
            if (!string.IsNullOrWhiteSpace(operation.sourcePoolId) && !poolsById.TryGetValue(operation.sourcePoolId, out source))
            {
                failure = $"Source pool '{operation.sourcePoolId}' was not found.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(operation.destinationPoolId) && !poolsById.TryGetValue(operation.destinationPoolId, out destination))
            {
                failure = $"Destination pool '{operation.destinationPoolId}' was not found.";
                return false;
            }

            if (source != null && (source.commodityId != operation.commodityId || source.unit != operation.unit))
            {
                failure = "Source pool commodity or unit does not match operation.";
                return false;
            }

            if (destination != null && (destination.commodityId != operation.commodityId || destination.unit != operation.unit))
            {
                failure = "Destination pool commodity or unit does not match operation.";
                return false;
            }

            if (operation.operationKind == AggregateQuantityOperationKind.AggregateExactItems)
            {
                string key = $"{operation.sourceEventId}:{operation.commodityId}:{operation.quantity}";
                if (exactAggregationKeys.Contains(key))
                {
                    failure = "Exact item source has already been aggregated and cannot be double counted.";
                    return false;
                }
            }

            operation.sourceRevisionBefore = source?.revision ?? 0L;
            operation.destinationRevisionBefore = destination?.revision ?? 0L;
            switch (operation.operationKind)
            {
                case AggregateQuantityOperationKind.Add:
                case AggregateQuantityOperationKind.Materialize:
                case AggregateQuantityOperationKind.AggregateExactItems:
                    destination.totalQuantity = checked(destination.totalQuantity + operation.quantity);
                    if (operation.operationKind == AggregateQuantityOperationKind.AggregateExactItems)
                    {
                        exactAggregationKeys.Add($"{operation.sourceEventId}:{operation.commodityId}:{operation.quantity}");
                    }
                    break;
                case AggregateQuantityOperationKind.Remove:
                    if (source.AvailableQuantity < operation.quantity) return QuantityFailure(source, operation.quantity, out failure);
                    source.totalQuantity = checked(source.totalQuantity - operation.quantity);
                    break;
                case AggregateQuantityOperationKind.Reserve:
                    if (source.AvailableQuantity < operation.quantity) return QuantityFailure(source, operation.quantity, out failure);
                    source.reservedQuantity = checked(source.reservedQuantity + operation.quantity);
                    break;
                case AggregateQuantityOperationKind.ReleaseReservation:
                    source.reservedQuantity = Math.Max(0L, source.reservedQuantity - operation.quantity);
                    break;
                case AggregateQuantityOperationKind.Consume:
                    if (source.AvailableQuantity < operation.quantity) return QuantityFailure(source, operation.quantity, out failure);
                    source.totalQuantity = checked(source.totalQuantity - operation.quantity);
                    source.consumedQuantity = checked(source.consumedQuantity + operation.quantity);
                    break;
                case AggregateQuantityOperationKind.Move:
                    if (source.AvailableQuantity < operation.quantity) return QuantityFailure(source, operation.quantity, out failure);
                    source.totalQuantity = checked(source.totalQuantity - operation.quantity);
                    destination.totalQuantity = checked(destination.totalQuantity + operation.quantity);
                    break;
                case AggregateQuantityOperationKind.MarkInbound:
                    destination.inboundQuantity = checked(destination.inboundQuantity + operation.quantity);
                    break;
                case AggregateQuantityOperationKind.MarkOutbound:
                    if (source.AvailableQuantity < operation.quantity) return QuantityFailure(source, operation.quantity, out failure);
                    source.totalQuantity = checked(source.totalQuantity - operation.quantity);
                    source.outboundQuantity = checked(source.outboundQuantity + operation.quantity);
                    break;
                case AggregateQuantityOperationKind.RecordLoss:
                case AggregateQuantityOperationKind.RecordSpoilageFoundation:
                    if (source.AvailableQuantity < operation.quantity) return QuantityFailure(source, operation.quantity, out failure);
                    source.totalQuantity = checked(source.totalQuantity - operation.quantity);
                    source.lostQuantity = checked(source.lostQuantity + operation.quantity);
                    break;
                case AggregateQuantityOperationKind.CorrectQuantity:
                    if (destination != null)
                    {
                        destination.totalQuantity = checked(destination.totalQuantity + operation.quantity);
                    }
                    else if (source != null)
                    {
                        source.totalQuantity = checked(source.totalQuantity + operation.quantity);
                    }
                    break;
                default:
                    failure = "Unsupported aggregate quantity operation kind.";
                    return false;
            }

            if (source != null)
            {
                source.revision++;
                operation.sourceRevisionAfter = source.revision;
            }

            if (destination != null)
            {
                destination.revision++;
                operation.destinationRevisionAfter = destination.revision;
            }

            return true;
        }

        private static bool QuantityFailure(CommodityPoolData pool, long quantity, out string failure)
        {
            failure = $"Commodity pool '{pool.poolId}' has {pool.AvailableQuantity} available, not {quantity}.";
            return false;
        }

        private bool ValidateRegion(EconomicRegionData region, out string failure)
        {
            failure = string.Empty;
            if (region == null || string.IsNullOrWhiteSpace(region.regionId) || string.IsNullOrWhiteSpace(region.regionDefinitionId))
            {
                failure = "Economic region ID and definition ID are required.";
                return false;
            }

            if (registry != null && !registry.TryGet(region.regionDefinitionId, out EconomicRegionDefinition _))
            {
                failure = $"Economic region definition '{region.regionDefinitionId}' was not found.";
                return false;
            }

            if (region.state == EconomicRegionState.Invalid || region.simulationFidelity == RegionalSimulationFidelity.Unknown)
            {
                failure = "Economic region state or simulation profile is invalid.";
                return false;
            }

            return true;
        }

        private bool ValidatePool(CommodityPoolData pool, out string failure)
        {
            failure = string.Empty;
            if (pool == null || string.IsNullOrWhiteSpace(pool.poolId) || string.IsNullOrWhiteSpace(pool.regionId) || string.IsNullOrWhiteSpace(pool.commodityId))
            {
                failure = "Commodity pool ID, region ID, and commodity ID are required.";
                return false;
            }

            if (!regionsById.ContainsKey(pool.regionId))
            {
                failure = $"Economic region '{pool.regionId}' was not found.";
                return false;
            }

            if (registry != null && !registry.TryGet(pool.commodityId, out CommodityDefinition commodity))
            {
                failure = $"Commodity definition '{pool.commodityId}' was not found.";
                return false;
            }

            if (pool.unit == CommodityUnit.Unknown || pool.purpose == CommodityPoolPurpose.Unknown)
            {
                failure = "Commodity pool unit and purpose must be concrete.";
                return false;
            }

            if (pool.reservedQuantity + pool.outboundQuantity + pool.inaccessibleQuantity > pool.totalQuantity)
            {
                failure = "Commodity pool reservations exceed total quantity.";
                return false;
            }

            return true;
        }

        private bool ValidateOperation(AggregateQuantityOperationData operation, out RegionalFlowResultCode code, out string failure)
        {
            failure = string.Empty;
            code = RegionalFlowResultCode.InvalidRequest;
            if (operation == null || string.IsNullOrWhiteSpace(operation.operationId) || string.IsNullOrWhiteSpace(operation.commodityId) || operation.quantity <= 0L || operation.unit == CommodityUnit.Unknown || operation.operationKind == AggregateQuantityOperationKind.Unknown)
            {
                failure = "Aggregate quantity operation requires ID, commodity, unit, kind, and positive quantity.";
                return false;
            }

            if (registry != null && !registry.TryGet(operation.commodityId, out CommodityDefinition _))
            {
                code = RegionalFlowResultCode.MissingCommodity;
                failure = $"Commodity definition '{operation.commodityId}' was not found.";
                return false;
            }

            bool needsSource = operation.operationKind is AggregateQuantityOperationKind.Remove or AggregateQuantityOperationKind.Reserve or AggregateQuantityOperationKind.ReleaseReservation or AggregateQuantityOperationKind.Consume or AggregateQuantityOperationKind.Move or AggregateQuantityOperationKind.MarkOutbound or AggregateQuantityOperationKind.RecordLoss or AggregateQuantityOperationKind.RecordSpoilageFoundation;
            bool needsDestination = operation.operationKind is AggregateQuantityOperationKind.Add or AggregateQuantityOperationKind.Move or AggregateQuantityOperationKind.MarkInbound or AggregateQuantityOperationKind.CorrectQuantity or AggregateQuantityOperationKind.Materialize or AggregateQuantityOperationKind.AggregateExactItems;
            if (needsSource && string.IsNullOrWhiteSpace(operation.sourcePoolId) || needsDestination && string.IsNullOrWhiteSpace(operation.destinationPoolId))
            {
                code = RegionalFlowResultCode.MissingPool;
                failure = "Aggregate quantity operation is missing a required source or destination pool.";
                return false;
            }

            return true;
        }

        private bool ValidateCohort(EconomicCohortData cohort, out string failure)
        {
            failure = string.Empty;
            if (cohort == null || string.IsNullOrWhiteSpace(cohort.cohortId) || string.IsNullOrWhiteSpace(cohort.regionId) || !regionsById.ContainsKey(cohort.regionId))
            {
                failure = "Cohort ID and existing economic region are required.";
                return false;
            }

            if (cohort.category == EconomicCohortCategory.Unknown || cohort.populationQuantity < 0L)
            {
                failure = "Cohort category and population must be valid.";
                return false;
            }

            return true;
        }

        private bool ValidateConnection(TradeConnectionData connection, out string failure)
        {
            failure = string.Empty;
            if (connection == null || string.IsNullOrWhiteSpace(connection.connectionId) || string.IsNullOrWhiteSpace(connection.sourceRegionId) || string.IsNullOrWhiteSpace(connection.destinationRegionId))
            {
                failure = "Trade connection ID, source region, and destination region are required.";
                return false;
            }

            if (connection.sourceRegionId == connection.destinationRegionId)
            {
                failure = "Trade connection cannot connect a region to itself.";
                return false;
            }

            if (!regionsById.ContainsKey(connection.sourceRegionId) || !regionsById.ContainsKey(connection.destinationRegionId))
            {
                failure = "Trade connection references a missing economic region.";
                return false;
            }

            if (connection.capacityUnits <= 0L || connection.state == TradeConnectionState.Invalid)
            {
                failure = "Trade connection capacity and state must be valid.";
                return false;
            }

            return true;
        }

        private RegionalFlowOperationResult Fail(RegionalFlowResultCode code, string message, bool preview) => RegionalFlowOperationResult.Failure(code, message, Revision, preview);

        private void Touch() => Revision++;

        private void Remember(string transactionId, string operationKey, RegionalFlowResultCode code)
        {
            if (!string.IsNullOrWhiteSpace(transactionId))
            {
                processedByTransactionId[transactionId] = new RegionalFlowProcessedTransactionData { transactionId = transactionId, operationKey = operationKey ?? string.Empty, code = code };
            }
        }

        private bool IsDuplicate(string transactionId, string operationKey, out RegionalFlowOperationResult duplicate)
        {
            duplicate = null;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return false;
            }

            if (processedByTransactionId.TryGetValue(transactionId, out RegionalFlowProcessedTransactionData processed) && processed.operationKey == operationKey)
            {
                duplicate = RegionalFlowOperationResult.Success("Duplicate regional-flow transaction ignored.", Revision, Revision, duplicate: true);
                return true;
            }

            return false;
        }

        private void LinkRegionPool(string regionId, string poolId)
        {
            if (TryAppendRegionId(regionId, poolId, region => region.commodityPoolIds, (region, next) => region.commodityPoolIds = next))
            {
                regionsById[regionId].revision++;
            }
        }

        private void LinkRegionCohort(string regionId, string cohortId)
        {
            if (TryAppendRegionId(regionId, cohortId, region => region.cohortIds, (region, next) => region.cohortIds = next))
            {
                regionsById[regionId].revision++;
            }
        }

        private void LinkRegionConnection(string regionId, string connectionId)
        {
            if (TryAppendRegionId(regionId, connectionId, region => region.tradeConnectionIds, (region, next) => region.tradeConnectionIds = next))
            {
                regionsById[regionId].revision++;
            }
        }

        private bool TryAppendRegionId(string regionId, string id, Func<EconomicRegionData, string[]> getter, Action<EconomicRegionData, string[]> setter)
        {
            if (!regionsById.TryGetValue(regionId ?? string.Empty, out EconomicRegionData region) || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            string[] current = getter(region);
            string[] next = RegionalFlowModelHelpers.Clean((current ?? Array.Empty<string>()).Concat(new[] { id }));
            if ((current ?? Array.Empty<string>()).SequenceEqual(next, StringComparer.Ordinal))
            {
                return false;
            }

            setter(region, next);
            return true;
        }

        private CommodityDefinition Commodity(string commodityId)
        {
            return registry != null && registry.TryGet(commodityId ?? string.Empty, out CommodityDefinition commodity) ? commodity : null;
        }

        private CommodityPoolData FindPool(IEnumerable<string> poolIds, string regionId, string commodityId)
        {
            string[] ids = RegionalFlowModelHelpers.Clean(poolIds);
            return ids.Select(id => poolsById.TryGetValue(id, out CommodityPoolData pool) ? pool : null)
                .FirstOrDefault(pool => pool != null && pool.regionId == regionId && pool.commodityId == commodityId);
        }

        private long ApplyModifiers(string regionId, string targetId, EconomicModifierKind kind, long baseUnits)
        {
            long result = Math.Max(0L, baseUnits);
            foreach (EconomicModifierData modifier in modifiersById.Values.Where(item => item.regionId == regionId && item.modifierKind == kind && (string.IsNullOrWhiteSpace(item.targetId) || item.targetId == targetId)).OrderBy(item => item.modifierId, StringComparer.Ordinal))
            {
                result = CheckedMultiplyRatio(result, modifier.multiplierBasisPoints, 10000);
            }

            return result;
        }

        private static long CheckedMultiplyRatio(long value, long numerator, long denominator)
        {
            if (denominator <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(denominator));
            }

            return checked(value * numerator / denominator);
        }

        private static AggregateQuantityOperationData Operation(string id, AggregateQuantityOperationKind kind, string commodityId, string sourcePoolId, string destinationPoolId, CommodityUnit unit, long quantity, string purpose, string sourceEventId, double worldTime)
        {
            return new AggregateQuantityOperationData
            {
                operationId = id ?? string.Empty,
                operationKind = kind,
                commodityId = commodityId ?? string.Empty,
                sourcePoolId = sourcePoolId ?? string.Empty,
                destinationPoolId = destinationPoolId ?? string.Empty,
                unit = unit,
                quantity = Math.Max(0L, quantity),
                purpose = purpose ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                worldTime = Math.Max(0d, worldTime),
                authorityId = "regional-flow.runtime",
                provenance = purpose ?? string.Empty
            };
        }

        private static string StableId(params object[] parts)
        {
            return string.Join(".", (parts ?? Array.Empty<object>()).Select(item => (item?.ToString() ?? string.Empty).Replace(" ", "-").Replace(":", "-")).Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        private static IEnumerable<T> Ordered<T>(IEnumerable<T> values, Func<T, string> key) => (values ?? Array.Empty<T>()).OrderBy(key, StringComparer.Ordinal);

        private static bool TryClone<T>(Dictionary<string, T> dictionary, string id, Func<T, T> clone, out T value)
        {
            if (!string.IsNullOrWhiteSpace(id) && dictionary.TryGetValue(id, out T found))
            {
                value = clone(found);
                return true;
            }

            value = default;
            return false;
        }

        private static bool SameRegion(EconomicRegionData a, EconomicRegionData b)
        {
            return a.regionId == b.regionId && a.regionDefinitionId == b.regionDefinitionId && a.displayName == b.displayName;
        }

        private static InformationAccessProjection<T> Project<T>(T record, InformationSubjectReferenceData subject, string policyId, InformationAccessRuntime access, InformationAccessContext context, Func<T, IReadOnlyDictionary<string, InformationRedactionState>, T> redact)
        {
            if (access == null)
            {
                return new InformationAccessProjection<T>(record, null, new Dictionary<string, InformationRedactionState>(), subject?.subjectId ?? string.Empty, "No access runtime was provided; privileged internal projection returned.");
            }

            InformationAccessContext accessContext = InformationAccessProjectionUtility.BuildContext(context, subject, InformationAccessMode.Inspect, InformationAccessPurpose.InternalSimulation, ProtectedDetails, policyId);
            RedactedInformationProjection redaction = access.Project(accessContext, ProtectedDetails);
            T projected = redaction.Decision.Denied ? default : redact(record, redaction.Details);
            return new InformationAccessProjection<T>(projected, redaction.Decision, redaction.Details, subject?.subjectId ?? string.Empty, redaction.Decision.VisibleReason);
        }

        private static CommodityPoolData RedactPool(CommodityPoolData pool, IReadOnlyDictionary<string, InformationRedactionState> states)
        {
            CommodityPoolData copy = pool.Clone();
            if (!InformationAccessProjectionUtility.IsVisible(states, "detail.quantity"))
            {
                copy.totalQuantity = 0L;
                copy.reservedQuantity = 0L;
                copy.inboundQuantity = 0L;
                copy.outboundQuantity = 0L;
            }

            if (!InformationAccessProjectionUtility.IsVisible(states, "detail.provenance"))
            {
                copy.sourceSummaries = Array.Empty<string>();
                copy.provenance = string.Empty;
            }

            return copy;
        }

        private static EconomicRegionData RedactRegion(EconomicRegionData region, IReadOnlyDictionary<string, InformationRedactionState> states)
        {
            EconomicRegionData copy = region.Clone();
            if (!InformationAccessProjectionUtility.IsVisible(states, "detail.pool"))
            {
                copy.commodityPoolIds = Array.Empty<string>();
            }

            if (!InformationAccessProjectionUtility.IsVisible(states, "detail.capacity"))
            {
                copy.tradeConnectionIds = Array.Empty<string>();
            }

            return copy;
        }
    }
}
