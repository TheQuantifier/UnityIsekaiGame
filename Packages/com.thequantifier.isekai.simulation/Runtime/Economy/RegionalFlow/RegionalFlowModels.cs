using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Economy.RegionalFlow
{
    [Serializable]
    public sealed class RegionalSubjectReferenceData
    {
        public string subjectKind;
        public string subjectId;

        public RegionalSubjectReferenceData Clone()
        {
            return new RegionalSubjectReferenceData
            {
                subjectKind = subjectKind ?? string.Empty,
                subjectId = subjectId ?? string.Empty
            };
        }

        public string StableKey => $"{subjectKind ?? string.Empty}:{subjectId ?? string.Empty}";
    }

    [Serializable]
    public sealed class RegionalCommodityQuantityData
    {
        public string commodityId;
        public CommodityUnit unit = CommodityUnit.Each;
        public long quantity;

        public RegionalCommodityQuantityData Clone()
        {
            return new RegionalCommodityQuantityData
            {
                commodityId = commodityId ?? string.Empty,
                unit = unit,
                quantity = Math.Max(0L, quantity)
            };
        }
    }

    [Serializable]
    public sealed class RegionalLaborQuantityData
    {
        public LaborCategory laborCategory = LaborCategory.GeneralLabor;
        public long units;

        public RegionalLaborQuantityData Clone()
        {
            return new RegionalLaborQuantityData
            {
                laborCategory = laborCategory,
                units = Math.Max(0L, units)
            };
        }
    }

    [Serializable]
    public sealed class EconomicRegionData
    {
        public string regionId;
        public string regionDefinitionId;
        public string displayName;
        public string futureWorldLocationReference;
        public string[] marketInstanceIds = Array.Empty<string>();
        public string[] organizationOrInstitutionIds = Array.Empty<string>();
        public bool active = true;
        public string[] commodityPoolIds = Array.Empty<string>();
        public string[] cohortIds = Array.Empty<string>();
        public LaborCategory[] laborMarketCategories = Array.Empty<LaborCategory>();
        public string[] businessIds = Array.Empty<string>();
        public string[] establishmentIds = Array.Empty<string>();
        public string[] propertyReferenceIds = Array.Empty<string>();
        public string[] tradeConnectionIds = Array.Empty<string>();
        public long currentUpdateBoundary;
        public double lastSuccessfulUpdateWorldTime = -1d;
        public RegionalSimulationFidelity simulationFidelity = RegionalSimulationFidelity.AggregatePools;
        public EconomicRegionState state = EconomicRegionState.Active;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public EconomicRegionData Clone()
        {
            return new EconomicRegionData
            {
                regionId = regionId ?? string.Empty,
                regionDefinitionId = regionDefinitionId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                futureWorldLocationReference = futureWorldLocationReference ?? string.Empty,
                marketInstanceIds = Clean(marketInstanceIds),
                organizationOrInstitutionIds = Clean(organizationOrInstitutionIds),
                active = active,
                commodityPoolIds = Clean(commodityPoolIds),
                cohortIds = Clean(cohortIds),
                laborMarketCategories = (laborMarketCategories ?? Array.Empty<LaborCategory>()).Distinct().OrderBy(item => item).ToArray(),
                businessIds = Clean(businessIds),
                establishmentIds = Clean(establishmentIds),
                propertyReferenceIds = Clean(propertyReferenceIds),
                tradeConnectionIds = Clean(tradeConnectionIds),
                currentUpdateBoundary = Math.Max(0L, currentUpdateBoundary),
                lastSuccessfulUpdateWorldTime = lastSuccessfulUpdateWorldTime,
                simulationFidelity = simulationFidelity,
                state = state,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return RegionalFlowInformationSubject.Create("economy.region", regionId, string.Empty, new[] { regionDefinitionId });
        }

        private static string[] Clean(IEnumerable<string> values) => RegionalFlowModelHelpers.Clean(values);
    }

    [Serializable]
    public sealed class CommodityPoolData
    {
        public string poolId;
        public string regionId;
        public string commodityId;
        public RegionalSubjectReferenceData owner = new RegionalSubjectReferenceData();
        public string custodianReferenceId;
        public CommodityPoolPurpose purpose = CommodityPoolPurpose.GeneralRegionalSupply;
        public CommodityUnit unit = CommodityUnit.Each;
        public long totalQuantity;
        public long reservedQuantity;
        public long inboundQuantity;
        public long outboundQuantity;
        public long inaccessibleQuantity;
        public long exactInventoryObservedQuantity;
        public long expectedFutureQuantity;
        public long consumedQuantity;
        public long lostQuantity;
        public long minimumReserve;
        public long targetReserve;
        public string qualityBandSummary;
        public string conditionBandSummary;
        public string[] sourceSummaries = Array.Empty<string>();
        public double lastReconciliationWorldTime = -1d;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public long AvailableQuantity => Math.Max(0L, totalQuantity - reservedQuantity - outboundQuantity - inaccessibleQuantity);

        public CommodityPoolData Clone()
        {
            return new CommodityPoolData
            {
                poolId = poolId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                commodityId = commodityId ?? string.Empty,
                owner = owner?.Clone() ?? new RegionalSubjectReferenceData(),
                custodianReferenceId = custodianReferenceId ?? string.Empty,
                purpose = purpose,
                unit = unit,
                totalQuantity = Math.Max(0L, totalQuantity),
                reservedQuantity = Math.Max(0L, reservedQuantity),
                inboundQuantity = Math.Max(0L, inboundQuantity),
                outboundQuantity = Math.Max(0L, outboundQuantity),
                inaccessibleQuantity = Math.Max(0L, inaccessibleQuantity),
                exactInventoryObservedQuantity = Math.Max(0L, exactInventoryObservedQuantity),
                expectedFutureQuantity = Math.Max(0L, expectedFutureQuantity),
                consumedQuantity = Math.Max(0L, consumedQuantity),
                lostQuantity = Math.Max(0L, lostQuantity),
                minimumReserve = Math.Max(0L, minimumReserve),
                targetReserve = Math.Max(0L, targetReserve),
                qualityBandSummary = qualityBandSummary ?? string.Empty,
                conditionBandSummary = conditionBandSummary ?? string.Empty,
                sourceSummaries = RegionalFlowModelHelpers.Clean(sourceSummaries),
                lastReconciliationWorldTime = lastReconciliationWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return RegionalFlowInformationSubject.Create("economy.pool", poolId, regionId, new[] { commodityId, owner?.StableKey ?? string.Empty });
        }
    }

    [Serializable]
    public sealed class AggregateQuantityOperationData
    {
        public string operationId;
        public AggregateQuantityOperationKind operationKind = AggregateQuantityOperationKind.Add;
        public string commodityId;
        public string sourcePoolId;
        public string destinationPoolId;
        public CommodityUnit unit = CommodityUnit.Each;
        public long quantity;
        public string purpose;
        public string sourceEventId;
        public double worldTime;
        public string authorityId;
        public string accessPolicyId;
        public string provenance;
        public long sourceRevisionBefore;
        public long destinationRevisionBefore;
        public long sourceRevisionAfter;
        public long destinationRevisionAfter;
        public long revision = 1L;

        public AggregateQuantityOperationData Clone()
        {
            return new AggregateQuantityOperationData
            {
                operationId = operationId ?? string.Empty,
                operationKind = operationKind,
                commodityId = commodityId ?? string.Empty,
                sourcePoolId = sourcePoolId ?? string.Empty,
                destinationPoolId = destinationPoolId ?? string.Empty,
                unit = unit,
                quantity = Math.Max(0L, quantity),
                purpose = purpose ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                worldTime = Math.Max(0d, worldTime),
                authorityId = authorityId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                sourceRevisionBefore = sourceRevisionBefore,
                destinationRevisionBefore = destinationRevisionBefore,
                sourceRevisionAfter = sourceRevisionAfter,
                destinationRevisionAfter = destinationRevisionAfter,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class EconomicCohortData
    {
        public string cohortId;
        public string regionId;
        public EconomicCohortCategory category = EconomicCohortCategory.GeneralConsumers;
        public long populationQuantity;
        public RegionalLaborQuantityData[] laborDistribution = Array.Empty<RegionalLaborQuantityData>();
        public string[] consumptionProfileIds = Array.Empty<string>();
        public string[] productionProfileIds = Array.Empty<string>();
        public string accountId;
        public string[] commodityPoolIds = Array.Empty<string>();
        public string[] wealthSummaryIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public EconomicCohortData Clone()
        {
            return new EconomicCohortData
            {
                cohortId = cohortId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                category = category,
                populationQuantity = Math.Max(0L, populationQuantity),
                laborDistribution = RegionalFlowModelHelpers.CloneArray(laborDistribution, item => item.Clone()),
                consumptionProfileIds = RegionalFlowModelHelpers.Clean(consumptionProfileIds),
                productionProfileIds = RegionalFlowModelHelpers.Clean(productionProfileIds),
                accountId = accountId ?? string.Empty,
                commodityPoolIds = RegionalFlowModelHelpers.Clean(commodityPoolIds),
                wealthSummaryIds = RegionalFlowModelHelpers.Clean(wealthSummaryIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ProductionCapacityResultData
    {
        public string capacityResultId;
        public string regionId;
        public string producerOrCohortId;
        public string productionProfileId;
        public long maximumPotentialOutput;
        public long inputLimitedOutput;
        public long laborLimitedOutput;
        public long infrastructureLimitedOutput;
        public long effectiveOutputCapacity;
        public string[] bindingConstraints = Array.Empty<string>();
        public string[] sourceReferences = Array.Empty<string>();
        public int confidence;
        public double worldTime;
        public string accessPolicyId;
        public string provenance;

        public ProductionCapacityResultData Clone()
        {
            return new ProductionCapacityResultData
            {
                capacityResultId = capacityResultId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                producerOrCohortId = producerOrCohortId ?? string.Empty,
                productionProfileId = productionProfileId ?? string.Empty,
                maximumPotentialOutput = Math.Max(0L, maximumPotentialOutput),
                inputLimitedOutput = Math.Max(0L, inputLimitedOutput),
                laborLimitedOutput = Math.Max(0L, laborLimitedOutput),
                infrastructureLimitedOutput = Math.Max(0L, infrastructureLimitedOutput),
                effectiveOutputCapacity = Math.Max(0L, effectiveOutputCapacity),
                bindingConstraints = RegionalFlowModelHelpers.Clean(bindingConstraints),
                sourceReferences = RegionalFlowModelHelpers.Clean(sourceReferences),
                confidence = Math.Clamp(confidence, 0, 10000),
                worldTime = Math.Max(0d, worldTime),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class AggregateProductionRecordData
    {
        public string productionRecordId;
        public string regionId;
        public string producerOrCohortId;
        public string productionProfileId;
        public string[] inputOperationIds = Array.Empty<string>();
        public string[] outputOperationIds = Array.Empty<string>();
        public long boundary;
        public double worldTime;
        public string sourceReferenceId;
        public string marketSupplyObservationId;
        public long revision = 1L;

        public AggregateProductionRecordData Clone()
        {
            return new AggregateProductionRecordData
            {
                productionRecordId = productionRecordId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                producerOrCohortId = producerOrCohortId ?? string.Empty,
                productionProfileId = productionProfileId ?? string.Empty,
                inputOperationIds = RegionalFlowModelHelpers.Clean(inputOperationIds),
                outputOperationIds = RegionalFlowModelHelpers.Clean(outputOperationIds),
                boundary = Math.Max(0L, boundary),
                worldTime = Math.Max(0d, worldTime),
                sourceReferenceId = sourceReferenceId ?? string.Empty,
                marketSupplyObservationId = marketSupplyObservationId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class AggregateConsumptionRecordData
    {
        public string consumptionRecordId;
        public string regionId;
        public string consumerOrCohortId;
        public string consumptionProfileId;
        public string[] operationIds = Array.Empty<string>();
        public long boundary;
        public double worldTime;
        public string marketDemandObservationId;
        public long revision = 1L;

        public AggregateConsumptionRecordData Clone()
        {
            return new AggregateConsumptionRecordData
            {
                consumptionRecordId = consumptionRecordId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                consumerOrCohortId = consumerOrCohortId ?? string.Empty,
                consumptionProfileId = consumptionProfileId ?? string.Empty,
                operationIds = RegionalFlowModelHelpers.Clean(operationIds),
                boundary = Math.Max(0L, boundary),
                worldTime = Math.Max(0d, worldTime),
                marketDemandObservationId = marketDemandObservationId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class LaborMarketSnapshotData
    {
        public string snapshotId;
        public string regionId;
        public LaborCategory laborCategory = LaborCategory.GeneralLabor;
        public long supplyUnits;
        public long demandUnits;
        public WagePressureState wagePressure = WagePressureState.Balanced;
        public long boundary;
        public double worldTime;
        public string[] sourceReferences = Array.Empty<string>();
        public long revision = 1L;

        public LaborMarketSnapshotData Clone()
        {
            return new LaborMarketSnapshotData
            {
                snapshotId = snapshotId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                laborCategory = laborCategory,
                supplyUnits = Math.Max(0L, supplyUnits),
                demandUnits = Math.Max(0L, demandUnits),
                wagePressure = wagePressure,
                boundary = Math.Max(0L, boundary),
                worldTime = Math.Max(0d, worldTime),
                sourceReferences = RegionalFlowModelHelpers.Clean(sourceReferences),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class WealthSummaryData
    {
        public string wealthSummaryId;
        public string regionId;
        public RegionalSubjectReferenceData subject = new RegionalSubjectReferenceData();
        public string currencyId;
        public long liquidityUnits;
        public long assetEstimateUnits;
        public long debtEstimateUnits;
        public long netEstimatedWealthUnits;
        public bool unknownValue;
        public double worldTime;
        public string accessPolicyId;
        public long revision = 1L;

        public WealthSummaryData Clone()
        {
            return new WealthSummaryData
            {
                wealthSummaryId = wealthSummaryId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                subject = subject?.Clone() ?? new RegionalSubjectReferenceData(),
                currencyId = currencyId ?? string.Empty,
                liquidityUnits = Math.Max(0L, liquidityUnits),
                assetEstimateUnits = Math.Max(0L, assetEstimateUnits),
                debtEstimateUnits = Math.Max(0L, debtEstimateUnits),
                netEstimatedWealthUnits = Math.Max(0L, netEstimatedWealthUnits),
                unknownValue = unknownValue,
                worldTime = Math.Max(0d, worldTime),
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ShortageSurplusData
    {
        public string shortageId;
        public string regionId;
        public string commodityId;
        public LaborCategory laborCategory = LaborCategory.Unknown;
        public ShortageKind shortageKind = ShortageKind.Commodity;
        public ShortageState state = ShortageState.Balanced;
        public long effectiveSupply;
        public long expectedDemand;
        public long reserveRequirement;
        public long inboundExpected;
        public long surplusQuantity;
        public long shortageQuantity;
        public bool affordabilityDriven;
        public double worldTime;
        public long boundary;
        public string diagnostics;
        public long revision = 1L;

        public ShortageSurplusData Clone()
        {
            return new ShortageSurplusData
            {
                shortageId = shortageId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                commodityId = commodityId ?? string.Empty,
                laborCategory = laborCategory,
                shortageKind = shortageKind,
                state = state,
                effectiveSupply = Math.Max(0L, effectiveSupply),
                expectedDemand = Math.Max(0L, expectedDemand),
                reserveRequirement = Math.Max(0L, reserveRequirement),
                inboundExpected = Math.Max(0L, inboundExpected),
                surplusQuantity = Math.Max(0L, surplusQuantity),
                shortageQuantity = Math.Max(0L, shortageQuantity),
                affordabilityDriven = affordabilityDriven,
                worldTime = Math.Max(0d, worldTime),
                boundary = Math.Max(0L, boundary),
                diagnostics = diagnostics ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class TradeConnectionData
    {
        public string connectionId;
        public string sourceRegionId;
        public string destinationRegionId;
        public string[] permittedCommodityIds = Array.Empty<string>();
        public long capacityUnits;
        public long reservedCapacityUnits;
        public long leadTimeUnits;
        public long transferCostUnits;
        public string currencyId;
        public TradeConnectionState state = TradeConnectionState.Active;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public long AvailableCapacityUnits => Math.Max(0L, capacityUnits - reservedCapacityUnits);

        public TradeConnectionData Clone()
        {
            return new TradeConnectionData
            {
                connectionId = connectionId ?? string.Empty,
                sourceRegionId = sourceRegionId ?? string.Empty,
                destinationRegionId = destinationRegionId ?? string.Empty,
                permittedCommodityIds = RegionalFlowModelHelpers.Clean(permittedCommodityIds),
                capacityUnits = Math.Max(0L, capacityUnits),
                reservedCapacityUnits = Math.Max(0L, reservedCapacityUnits),
                leadTimeUnits = Math.Max(0L, leadTimeUnits),
                transferCostUnits = Math.Max(0L, transferCostUnits),
                currencyId = currencyId ?? string.Empty,
                state = state,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class FlowOrderData
    {
        public string flowOrderId;
        public string connectionId;
        public string sourcePoolId;
        public string destinationPoolId;
        public string commodityId;
        public CommodityUnit unit = CommodityUnit.Each;
        public long quantity;
        public long reservedCapacityUnits;
        public long lossUnits;
        public long plannedDepartureBoundary;
        public long plannedArrivalBoundary;
        public FlowOrderState state = FlowOrderState.Planned;
        public string departureOperationId;
        public string arrivalOperationId;
        public string paymentReferenceId;
        public string deliveryReferenceId;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public FlowOrderData Clone()
        {
            return new FlowOrderData
            {
                flowOrderId = flowOrderId ?? string.Empty,
                connectionId = connectionId ?? string.Empty,
                sourcePoolId = sourcePoolId ?? string.Empty,
                destinationPoolId = destinationPoolId ?? string.Empty,
                commodityId = commodityId ?? string.Empty,
                unit = unit,
                quantity = Math.Max(0L, quantity),
                reservedCapacityUnits = Math.Max(0L, reservedCapacityUnits),
                lossUnits = Math.Max(0L, lossUnits),
                plannedDepartureBoundary = Math.Max(0L, plannedDepartureBoundary),
                plannedArrivalBoundary = Math.Max(0L, plannedArrivalBoundary),
                state = state,
                departureOperationId = departureOperationId ?? string.Empty,
                arrivalOperationId = arrivalOperationId ?? string.Empty,
                paymentReferenceId = paymentReferenceId ?? string.Empty,
                deliveryReferenceId = deliveryReferenceId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class EconomicModifierData
    {
        public string modifierId;
        public string regionId;
        public EconomicModifierKind modifierKind = EconomicModifierKind.Production;
        public string targetId;
        public int multiplierBasisPoints = 10000;
        public double effectiveStartWorldTime;
        public double expirationWorldTime = -1d;
        public string sourceRecordId;
        public string provenance;
        public long revision = 1L;

        public EconomicModifierData Clone()
        {
            return new EconomicModifierData
            {
                modifierId = modifierId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                modifierKind = modifierKind,
                targetId = targetId ?? string.Empty,
                multiplierBasisPoints = Math.Max(0, multiplierBasisPoints),
                effectiveStartWorldTime = Math.Max(0d, effectiveStartWorldTime),
                expirationWorldTime = expirationWorldTime,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class EconomicCycleRecordData
    {
        public string cycleId;
        public string regionId;
        public long boundary;
        public double worldTime;
        public EconomicCycleStage[] stages = Array.Empty<EconomicCycleStage>();
        public string[] productionRecordIds = Array.Empty<string>();
        public string[] consumptionRecordIds = Array.Empty<string>();
        public string[] shortageIds = Array.Empty<string>();
        public string[] flowOrderIds = Array.Empty<string>();
        public string[] marketObservationIds = Array.Empty<string>();
        public bool succeeded;
        public string diagnostics;
        public long revision = 1L;

        public EconomicCycleRecordData Clone()
        {
            return new EconomicCycleRecordData
            {
                cycleId = cycleId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                boundary = Math.Max(0L, boundary),
                worldTime = Math.Max(0d, worldTime),
                stages = (stages ?? Array.Empty<EconomicCycleStage>()).ToArray(),
                productionRecordIds = RegionalFlowModelHelpers.Clean(productionRecordIds),
                consumptionRecordIds = RegionalFlowModelHelpers.Clean(consumptionRecordIds),
                shortageIds = RegionalFlowModelHelpers.Clean(shortageIds),
                flowOrderIds = RegionalFlowModelHelpers.Clean(flowOrderIds),
                marketObservationIds = RegionalFlowModelHelpers.Clean(marketObservationIds),
                succeeded = succeeded,
                diagnostics = diagnostics ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RegionalConservationAuditData
    {
        public string auditId;
        public string regionId;
        public string commodityId;
        public long startingQuantity;
        public long producedQuantity;
        public long consumedQuantity;
        public long lostQuantity;
        public long correctedQuantity;
        public long endingQuantity;
        public bool balanced;
        public string diagnostics;
        public double worldTime;
        public long revision = 1L;

        public RegionalConservationAuditData Clone()
        {
            return new RegionalConservationAuditData
            {
                auditId = auditId ?? string.Empty,
                regionId = regionId ?? string.Empty,
                commodityId = commodityId ?? string.Empty,
                startingQuantity = Math.Max(0L, startingQuantity),
                producedQuantity = Math.Max(0L, producedQuantity),
                consumedQuantity = Math.Max(0L, consumedQuantity),
                lostQuantity = Math.Max(0L, lostQuantity),
                correctedQuantity = correctedQuantity,
                endingQuantity = Math.Max(0L, endingQuantity),
                balanced = balanced,
                diagnostics = diagnostics ?? string.Empty,
                worldTime = Math.Max(0d, worldTime),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RegionalFlowProcessedTransactionData
    {
        public string transactionId;
        public string operationKey;
        public RegionalFlowResultCode code = RegionalFlowResultCode.Succeeded;

        public RegionalFlowProcessedTransactionData Clone()
        {
            return new RegionalFlowProcessedTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                operationKey = operationKey ?? string.Empty,
                code = code
            };
        }
    }

    [Serializable]
    public sealed class RegionalFlowRuntimeSaveData
    {
        public int schemaVersion = 1;
        public long revision;
        public string worldId;
        public List<EconomicRegionData> regions = new List<EconomicRegionData>();
        public List<CommodityPoolData> pools = new List<CommodityPoolData>();
        public List<AggregateQuantityOperationData> operations = new List<AggregateQuantityOperationData>();
        public List<EconomicCohortData> cohorts = new List<EconomicCohortData>();
        public List<AggregateProductionRecordData> productionRecords = new List<AggregateProductionRecordData>();
        public List<AggregateConsumptionRecordData> consumptionRecords = new List<AggregateConsumptionRecordData>();
        public List<LaborMarketSnapshotData> laborSnapshots = new List<LaborMarketSnapshotData>();
        public List<WealthSummaryData> wealthSummaries = new List<WealthSummaryData>();
        public List<ShortageSurplusData> shortages = new List<ShortageSurplusData>();
        public List<TradeConnectionData> connections = new List<TradeConnectionData>();
        public List<FlowOrderData> flowOrders = new List<FlowOrderData>();
        public List<EconomicModifierData> modifiers = new List<EconomicModifierData>();
        public List<EconomicCycleRecordData> cycles = new List<EconomicCycleRecordData>();
        public List<RegionalConservationAuditData> audits = new List<RegionalConservationAuditData>();
        public List<RegionalFlowProcessedTransactionData> processedTransactions = new List<RegionalFlowProcessedTransactionData>();

        public RegionalFlowRuntimeSaveData Clone()
        {
            return new RegionalFlowRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                worldId = worldId ?? string.Empty,
                regions = CloneList(regions, item => item.Clone()),
                pools = CloneList(pools, item => item.Clone()),
                operations = CloneList(operations, item => item.Clone()),
                cohorts = CloneList(cohorts, item => item.Clone()),
                productionRecords = CloneList(productionRecords, item => item.Clone()),
                consumptionRecords = CloneList(consumptionRecords, item => item.Clone()),
                laborSnapshots = CloneList(laborSnapshots, item => item.Clone()),
                wealthSummaries = CloneList(wealthSummaries, item => item.Clone()),
                shortages = CloneList(shortages, item => item.Clone()),
                connections = CloneList(connections, item => item.Clone()),
                flowOrders = CloneList(flowOrders, item => item.Clone()),
                modifiers = CloneList(modifiers, item => item.Clone()),
                cycles = CloneList(cycles, item => item.Clone()),
                audits = CloneList(audits, item => item.Clone()),
                processedTransactions = CloneList(processedTransactions, item => item.Clone())
            };
        }

        private static List<T> CloneList<T>(IEnumerable<T> values, Func<T, T> clone)
        {
            return (values ?? Array.Empty<T>()).Where(item => item != null).Select(clone).ToList();
        }
    }

    public sealed class RegionalFlowOperationResult
    {
        private RegionalFlowOperationResult(RegionalFlowResultCode code, string message, long revisionBefore, long revisionAfter, bool preview, bool duplicate)
        {
            Code = code;
            Message = message ?? string.Empty;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
            Preview = preview;
            Duplicate = duplicate;
        }

        public RegionalFlowResultCode Code { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public bool Succeeded => Code == RegionalFlowResultCode.Succeeded || Code == RegionalFlowResultCode.Preview || Code == RegionalFlowResultCode.Duplicate;
        public EconomicRegionData Region { get; private set; }
        public CommodityPoolData Pool { get; private set; }
        public AggregateQuantityOperationData Operation { get; private set; }
        public EconomicCohortData Cohort { get; private set; }
        public ProductionCapacityResultData Capacity { get; private set; }
        public AggregateProductionRecordData ProductionRecord { get; private set; }
        public AggregateConsumptionRecordData ConsumptionRecord { get; private set; }
        public LaborMarketSnapshotData LaborSnapshot { get; private set; }
        public WealthSummaryData WealthSummary { get; private set; }
        public ShortageSurplusData Shortage { get; private set; }
        public TradeConnectionData Connection { get; private set; }
        public FlowOrderData FlowOrder { get; private set; }
        public EconomicModifierData Modifier { get; private set; }
        public EconomicCycleRecordData Cycle { get; private set; }
        public RegionalConservationAuditData Audit { get; private set; }

        public static RegionalFlowOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false)
        {
            return new RegionalFlowOperationResult(preview ? RegionalFlowResultCode.Preview : duplicate ? RegionalFlowResultCode.Duplicate : RegionalFlowResultCode.Succeeded, message, before, after, preview, duplicate);
        }

        public static RegionalFlowOperationResult Failure(RegionalFlowResultCode code, string message, long revision, bool preview = false)
        {
            return new RegionalFlowOperationResult(code, message, revision, revision, preview, false);
        }

        public RegionalFlowOperationResult With(
            EconomicRegionData region = null,
            CommodityPoolData pool = null,
            AggregateQuantityOperationData operation = null,
            EconomicCohortData cohort = null,
            ProductionCapacityResultData capacity = null,
            AggregateProductionRecordData productionRecord = null,
            AggregateConsumptionRecordData consumptionRecord = null,
            LaborMarketSnapshotData laborSnapshot = null,
            WealthSummaryData wealthSummary = null,
            ShortageSurplusData shortage = null,
            TradeConnectionData connection = null,
            FlowOrderData flowOrder = null,
            EconomicModifierData modifier = null,
            EconomicCycleRecordData cycle = null,
            RegionalConservationAuditData audit = null)
        {
            Region = region?.Clone();
            Pool = pool?.Clone();
            Operation = operation?.Clone();
            Cohort = cohort?.Clone();
            Capacity = capacity?.Clone();
            ProductionRecord = productionRecord?.Clone();
            ConsumptionRecord = consumptionRecord?.Clone();
            LaborSnapshot = laborSnapshot?.Clone();
            WealthSummary = wealthSummary?.Clone();
            Shortage = shortage?.Clone();
            Connection = connection?.Clone();
            FlowOrder = flowOrder?.Clone();
            Modifier = modifier?.Clone();
            Cycle = cycle?.Clone();
            Audit = audit?.Clone();
            return this;
        }
    }

    public sealed class RegionalFlowSnapshot
    {
        public RegionalFlowSnapshot(RegionalFlowRuntimeSaveData data)
        {
            Data = data?.Clone() ?? new RegionalFlowRuntimeSaveData();
            Regions = new ReadOnlyCollection<EconomicRegionData>(Data.regions.Select(item => item.Clone()).ToList());
            Pools = new ReadOnlyCollection<CommodityPoolData>(Data.pools.Select(item => item.Clone()).ToList());
            FlowOrders = new ReadOnlyCollection<FlowOrderData>(Data.flowOrders.Select(item => item.Clone()).ToList());
            Cycles = new ReadOnlyCollection<EconomicCycleRecordData>(Data.cycles.Select(item => item.Clone()).ToList());
        }

        public RegionalFlowRuntimeSaveData Data { get; }
        public IReadOnlyList<EconomicRegionData> Regions { get; }
        public IReadOnlyList<CommodityPoolData> Pools { get; }
        public IReadOnlyList<FlowOrderData> FlowOrders { get; }
        public IReadOnlyList<EconomicCycleRecordData> Cycles { get; }
    }

    public static class RegionalFlowInformationSubject
    {
        public static InformationSubjectReferenceData Create(string typeTag, string subjectId, string parentSubjectId, IEnumerable<string> tags = null)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                tags = RegionalFlowModelHelpers.Clean((tags ?? Array.Empty<string>()).Concat(new[] { typeTag ?? string.Empty, "economy.regional-flow" }))
            };
        }
    }

    internal static class RegionalFlowModelHelpers
    {
        public static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        public static T[] CloneArray<T>(IEnumerable<T> values, Func<T, T> clone)
        {
            return (values ?? Array.Empty<T>()).Where(item => item != null).Select(clone).ToArray();
        }
    }
}
