using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Production
{
    [Serializable]
    public sealed class ProductionStageDefinitionData
    {
        public string stageId;
        public string displayName;
        public ProductionStageCategory category = ProductionStageCategory.Unknown;
        public string recipeDefinitionId;
        public string recipeVersionId;
        public string recipeVariantId;
        public string[] requirementIds = Array.Empty<string>();
        public string[] dependencyStageIds = Array.Empty<string>();
        public bool optional;
        public bool conditional;
        public int repeatCount = 1;
        public float estimatedDuration = 1f;
        public float requiredWorkUnits = 1f;
        public ProductionProgressModel progressModel = ProductionProgressModel.TimeBased;
        public ProductionInputConsumptionPolicy inputConsumptionPolicy = ProductionInputConsumptionPolicy.ReservedAtStartConsumedAtCompletion;
        public ProductionToolWearTiming toolWearTiming = ProductionToolWearTiming.StageCompletion;
        public ProductionPartialBatchPolicy partialBatchPolicy = ProductionPartialBatchPolicy.AllOrNothing;
        public int priority;
        public string outputIntermediateId;
        public string outputLotId;
        public string accessPolicyId;
        public string provenance;

        public ProductionStageDefinitionData Clone()
        {
            return new ProductionStageDefinitionData
            {
                stageId = stageId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                category = category,
                recipeDefinitionId = recipeDefinitionId ?? string.Empty,
                recipeVersionId = recipeVersionId ?? string.Empty,
                recipeVariantId = recipeVariantId ?? string.Empty,
                requirementIds = NormalizeIds(requirementIds),
                dependencyStageIds = NormalizeIds(dependencyStageIds),
                optional = optional,
                conditional = conditional,
                repeatCount = Math.Max(1, repeatCount),
                estimatedDuration = Math.Max(0f, estimatedDuration),
                requiredWorkUnits = Math.Max(0f, requiredWorkUnits),
                progressModel = progressModel,
                inputConsumptionPolicy = inputConsumptionPolicy,
                toolWearTiming = toolWearTiming,
                partialBatchPolicy = partialBatchPolicy,
                priority = priority,
                outputIntermediateId = outputIntermediateId ?? string.Empty,
                outputLotId = outputLotId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty
            };
        }

        internal static string[] NormalizeIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        internal static string[] NormalizeOrderedIds(IEnumerable<string> values)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Where(seen.Add)
                .ToArray();
        }
    }

    [Serializable]
    public sealed class ProductionChainVersionData
    {
        public string versionId;
        public string chainDefinitionId;
        public string priorVersionId;
        public string supersedingVersionId;
        public string worldTime;
        public string authorOrSourceId;
        public string changeReason;
        public ProductionChainLifecycleState state = ProductionChainLifecycleState.Active;
        public ProductionStageDefinitionData[] stages = Array.Empty<ProductionStageDefinitionData>();

        public ProductionChainVersionData Clone()
        {
            return new ProductionChainVersionData
            {
                versionId = versionId ?? string.Empty,
                chainDefinitionId = chainDefinitionId ?? string.Empty,
                priorVersionId = priorVersionId ?? string.Empty,
                supersedingVersionId = supersedingVersionId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                authorOrSourceId = authorOrSourceId ?? string.Empty,
                changeReason = changeReason ?? string.Empty,
                state = state,
                stages = (stages ?? Array.Empty<ProductionStageDefinitionData>()).Select(stage => stage?.Clone()).Where(stage => stage != null).ToArray()
            };
        }
    }

    [Serializable]
    public sealed class ProductionWorkOrderData
    {
        public string workOrderId;
        public string requesterPersonId;
        public string beneficiaryId;
        public string recipeDefinitionId;
        public string chainDefinitionId;
        public string versionId;
        public string variantId;
        public int requestedQuantity = 1;
        public int priority;
        public string earliestStartWorldTime;
        public string dueWorldTime;
        public string destinationId;
        public string ownerPersonId;
        public string custodianPersonId;
        public string accessPolicyId;
        public string secrecy;
        public string notes;
        public string provenance;
        public ProductionWorkOrderState state = ProductionWorkOrderState.Draft;
        public string[] jobIds = Array.Empty<string>();
        public string[] outputItemIds = Array.Empty<string>();
        public long revision = 1L;

        public ProductionWorkOrderData Clone()
        {
            return new ProductionWorkOrderData
            {
                workOrderId = workOrderId ?? string.Empty,
                requesterPersonId = requesterPersonId ?? string.Empty,
                beneficiaryId = beneficiaryId ?? string.Empty,
                recipeDefinitionId = recipeDefinitionId ?? string.Empty,
                chainDefinitionId = chainDefinitionId ?? string.Empty,
                versionId = versionId ?? string.Empty,
                variantId = variantId ?? string.Empty,
                requestedQuantity = Math.Max(1, requestedQuantity),
                priority = priority,
                earliestStartWorldTime = earliestStartWorldTime ?? string.Empty,
                dueWorldTime = dueWorldTime ?? string.Empty,
                destinationId = destinationId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                custodianPersonId = custodianPersonId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                secrecy = secrecy ?? string.Empty,
                notes = notes ?? string.Empty,
                provenance = provenance ?? string.Empty,
                state = state,
                jobIds = ProductionStageDefinitionData.NormalizeIds(jobIds),
                outputItemIds = ProductionStageDefinitionData.NormalizeIds(outputItemIds),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProductionStageProgressData
    {
        public string stageId;
        public string recipeDefinitionId;
        public string recipeVersionId;
        public string recipeVariantId;
        public string[] dependencyStageIds = Array.Empty<string>();
        public ProductionStageRuntimeState state = ProductionStageRuntimeState.Pending;
        public float requiredWork;
        public float completedWork;
        public string startWorldTime;
        public string lastEvaluatedWorldTime;
        public string completionWorldTime;
        public int attemptCount;
        public string requirementPlanId;
        public string[] reservationIds = Array.Empty<string>();
        public string occupancyId;
        public string[] workerAssignmentIds = Array.Empty<string>();
        public string[] intermediateIds = Array.Empty<string>();
        public string[] outputItemIds = Array.Empty<string>();
        public string craftingOperationId;
        public string deterministicSeed;
        public string blockReason;
        public string pauseReason;
        public string interruptReason;
        public long revision = 1L;

        public ProductionStageProgressData Clone()
        {
            return new ProductionStageProgressData
            {
                stageId = stageId ?? string.Empty,
                recipeDefinitionId = recipeDefinitionId ?? string.Empty,
                recipeVersionId = recipeVersionId ?? string.Empty,
                recipeVariantId = recipeVariantId ?? string.Empty,
                dependencyStageIds = ProductionStageDefinitionData.NormalizeIds(dependencyStageIds),
                state = state,
                requiredWork = Math.Max(0f, requiredWork),
                completedWork = Math.Max(0f, completedWork),
                startWorldTime = startWorldTime ?? string.Empty,
                lastEvaluatedWorldTime = lastEvaluatedWorldTime ?? string.Empty,
                completionWorldTime = completionWorldTime ?? string.Empty,
                attemptCount = Math.Max(0, attemptCount),
                requirementPlanId = requirementPlanId ?? string.Empty,
                reservationIds = ProductionStageDefinitionData.NormalizeIds(reservationIds),
                occupancyId = occupancyId ?? string.Empty,
                workerAssignmentIds = ProductionStageDefinitionData.NormalizeIds(workerAssignmentIds),
                intermediateIds = ProductionStageDefinitionData.NormalizeIds(intermediateIds),
                outputItemIds = ProductionStageDefinitionData.NormalizeIds(outputItemIds),
                craftingOperationId = craftingOperationId ?? string.Empty,
                deterministicSeed = deterministicSeed ?? string.Empty,
                blockReason = blockReason ?? string.Empty,
                pauseReason = pauseReason ?? string.Empty,
                interruptReason = interruptReason ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProductionJobData
    {
        public string jobId;
        public string workOrderId;
        public string recipeDefinitionId;
        public string chainDefinitionId;
        public string versionId;
        public string variantId;
        public int batchQuantity = 1;
        public string batchId;
        public string queueId;
        public string ownerPersonId;
        public string custodianPersonId;
        public string currentStageId;
        public string[] completedStageIds = Array.Empty<string>();
        public string[] readyStageIds = Array.Empty<string>();
        public string[] blockedStageIds = Array.Empty<string>();
        public string[] requirementPlanIds = Array.Empty<string>();
        public string[] reservationIds = Array.Empty<string>();
        public string[] occupancyIds = Array.Empty<string>();
        public string[] assignmentIds = Array.Empty<string>();
        public string[] inputLotIds = Array.Empty<string>();
        public string[] outputLotIds = Array.Empty<string>();
        public string[] intermediateIds = Array.Empty<string>();
        public string[] outputItemIds = Array.Empty<string>();
        public string[] byproductItemIds = Array.Empty<string>();
        public string[] wasteItemIds = Array.Empty<string>();
        public string startWorldTime;
        public string lastEvaluatedWorldTime;
        public string expectedCompletionWorldTime;
        public string completionWorldTime;
        public ProductionJobState state = ProductionJobState.Created;
        public string pauseReason;
        public string blockReason;
        public string failureReason;
        public int retryCount;
        public int priority;
        public string deterministicSeed;
        public string provenance;
        public ProductionOutputCollectionState outputCollectionState = ProductionOutputCollectionState.NotReady;
        public List<ProductionStageProgressData> stages = new List<ProductionStageProgressData>();
        public long revision = 1L;

        public ProductionJobData Clone()
        {
            return new ProductionJobData
            {
                jobId = jobId ?? string.Empty,
                workOrderId = workOrderId ?? string.Empty,
                recipeDefinitionId = recipeDefinitionId ?? string.Empty,
                chainDefinitionId = chainDefinitionId ?? string.Empty,
                versionId = versionId ?? string.Empty,
                variantId = variantId ?? string.Empty,
                batchQuantity = Math.Max(1, batchQuantity),
                batchId = batchId ?? string.Empty,
                queueId = queueId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                custodianPersonId = custodianPersonId ?? string.Empty,
                currentStageId = currentStageId ?? string.Empty,
                completedStageIds = ProductionStageDefinitionData.NormalizeIds(completedStageIds),
                readyStageIds = ProductionStageDefinitionData.NormalizeIds(readyStageIds),
                blockedStageIds = ProductionStageDefinitionData.NormalizeIds(blockedStageIds),
                requirementPlanIds = ProductionStageDefinitionData.NormalizeIds(requirementPlanIds),
                reservationIds = ProductionStageDefinitionData.NormalizeIds(reservationIds),
                occupancyIds = ProductionStageDefinitionData.NormalizeIds(occupancyIds),
                assignmentIds = ProductionStageDefinitionData.NormalizeIds(assignmentIds),
                inputLotIds = ProductionStageDefinitionData.NormalizeIds(inputLotIds),
                outputLotIds = ProductionStageDefinitionData.NormalizeIds(outputLotIds),
                intermediateIds = ProductionStageDefinitionData.NormalizeIds(intermediateIds),
                outputItemIds = ProductionStageDefinitionData.NormalizeIds(outputItemIds),
                byproductItemIds = ProductionStageDefinitionData.NormalizeIds(byproductItemIds),
                wasteItemIds = ProductionStageDefinitionData.NormalizeIds(wasteItemIds),
                startWorldTime = startWorldTime ?? string.Empty,
                lastEvaluatedWorldTime = lastEvaluatedWorldTime ?? string.Empty,
                expectedCompletionWorldTime = expectedCompletionWorldTime ?? string.Empty,
                completionWorldTime = completionWorldTime ?? string.Empty,
                state = state,
                pauseReason = pauseReason ?? string.Empty,
                blockReason = blockReason ?? string.Empty,
                failureReason = failureReason ?? string.Empty,
                retryCount = Math.Max(0, retryCount),
                priority = priority,
                deterministicSeed = deterministicSeed ?? string.Empty,
                provenance = provenance ?? string.Empty,
                outputCollectionState = outputCollectionState,
                stages = (stages ?? new List<ProductionStageProgressData>()).Select(stage => stage?.Clone()).Where(stage => stage != null).OrderBy(stage => stage.stageId, StringComparer.Ordinal).ToList(),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProductionQueueData
    {
        public string queueId;
        public string ownerId;
        public string stationInstanceId;
        public ProductionQueuePolicy policy = ProductionQueuePolicy.PriorityThenFifo;
        public ProductionQueueState state = ProductionQueueState.Active;
        public int maximumLength = 100;
        public List<string> jobIds = new List<string>();
        public long revision = 1L;

        public ProductionQueueData Clone()
        {
            return new ProductionQueueData
            {
                queueId = queueId ?? string.Empty,
                ownerId = ownerId ?? string.Empty,
                stationInstanceId = stationInstanceId ?? string.Empty,
                policy = policy,
                state = state,
                maximumLength = Math.Max(1, maximumLength),
                jobIds = ProductionStageDefinitionData.NormalizeOrderedIds(jobIds).ToList(),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProductionBatchData
    {
        public string batchId;
        public string jobId;
        public string recipeOrChainId;
        public string versionId;
        public string variantId;
        public int batchQuantity = 1;
        public ProductionBatchConsistencyPolicy consistencyPolicy = ProductionBatchConsistencyPolicy.IdenticalAuthoritativeState;
        public ProductionPartialBatchPolicy partialBatchPolicy = ProductionPartialBatchPolicy.AllOrNothing;
        public string[] inputLotIds = Array.Empty<string>();
        public string[] outputLotIds = Array.Empty<string>();
        public string[] outputItemIds = Array.Empty<string>();
        public string[] byproductIds = Array.Empty<string>();
        public string[] wasteIds = Array.Empty<string>();
        public string[] workerIds = Array.Empty<string>();
        public string[] stationIds = Array.Empty<string>();
        public string[] toolIds = Array.Empty<string>();
        public string startWorldTime;
        public string completionWorldTime;
        public string deterministicSeed;
        public string qualitySummary;
        public string affixSummary;
        public string durabilitySummary;
        public string provenance;
        public long revision = 1L;

        public ProductionBatchData Clone()
        {
            return new ProductionBatchData
            {
                batchId = batchId ?? string.Empty,
                jobId = jobId ?? string.Empty,
                recipeOrChainId = recipeOrChainId ?? string.Empty,
                versionId = versionId ?? string.Empty,
                variantId = variantId ?? string.Empty,
                batchQuantity = Math.Max(1, batchQuantity),
                consistencyPolicy = consistencyPolicy,
                partialBatchPolicy = partialBatchPolicy,
                inputLotIds = ProductionStageDefinitionData.NormalizeIds(inputLotIds),
                outputLotIds = ProductionStageDefinitionData.NormalizeIds(outputLotIds),
                outputItemIds = ProductionStageDefinitionData.NormalizeIds(outputItemIds),
                byproductIds = ProductionStageDefinitionData.NormalizeIds(byproductIds),
                wasteIds = ProductionStageDefinitionData.NormalizeIds(wasteIds),
                workerIds = ProductionStageDefinitionData.NormalizeIds(workerIds),
                stationIds = ProductionStageDefinitionData.NormalizeIds(stationIds),
                toolIds = ProductionStageDefinitionData.NormalizeIds(toolIds),
                startWorldTime = startWorldTime ?? string.Empty,
                completionWorldTime = completionWorldTime ?? string.Empty,
                deterministicSeed = deterministicSeed ?? string.Empty,
                qualitySummary = qualitySummary ?? string.Empty,
                affixSummary = affixSummary ?? string.Empty,
                durabilitySummary = durabilitySummary ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProductionLotMaterialAssignmentData
    {
        public string recipeInputId;
        public string componentRoleId;
        public string itemDefinitionId;
        public string materialDefinitionId;
        public float quantity = 1f;

        public ProductionLotMaterialAssignmentData Clone()
        {
            return new ProductionLotMaterialAssignmentData
            {
                recipeInputId = recipeInputId ?? string.Empty,
                componentRoleId = componentRoleId ?? string.Empty,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                materialDefinitionId = materialDefinitionId ?? string.Empty,
                quantity = Math.Max(0f, quantity)
            };
        }
    }

    [Serializable]
    public sealed class ProductionLotData
    {
        public string lotId;
        public string definitionOrMaterialId;
        public string ownerId;
        public string custodianId;
        public string[] sourceItemIds = Array.Empty<string>();
        public string[] containedItemIds = Array.Empty<string>();
        public float quantity;
        public long discreteQuantity = -1L;
        public ProductionQuantityUnit unit = ProductionQuantityUnit.Count;
        public string batchSourceId;
        public string[] parentLotIds = Array.Empty<string>();
        public string[] childLotIds = Array.Empty<string>();
        public string locationId;
        public string accessPolicyId;
        public string compositionSummary;
        public string qualitySummary;
        public string puritySummary;
        public string recipeDefinitionId;
        public string recipeVersionId;
        public ProductionLotMaterialAssignmentData[] materialAssignments = Array.Empty<ProductionLotMaterialAssignmentData>();
        public string deterministicSeed;
        public double createdWorldTime;
        public long nextMaterializationIndex;
        public float baseQuality = 0.3f;
        public float qualityVariation = 0.08f;
        public string provenance;
        public ProductionLotState state = ProductionLotState.Active;
        public long revision = 1L;

        public long WholeQuantity => unit == ProductionQuantityUnit.Count
            ? Math.Max(0L, discreteQuantity >= 0L ? discreteQuantity : (long)MathF.Round(quantity))
            : Math.Max(0L, (long)Math.Floor(quantity));

        public ProductionLotData Clone()
        {
            return new ProductionLotData
            {
                lotId = lotId ?? string.Empty,
                definitionOrMaterialId = definitionOrMaterialId ?? string.Empty,
                ownerId = ownerId ?? string.Empty,
                custodianId = custodianId ?? string.Empty,
                sourceItemIds = ProductionStageDefinitionData.NormalizeIds(sourceItemIds),
                containedItemIds = ProductionStageDefinitionData.NormalizeIds(containedItemIds),
                quantity = unit == ProductionQuantityUnit.Count ? WholeQuantity : Math.Max(0f, quantity),
                discreteQuantity = unit == ProductionQuantityUnit.Count ? WholeQuantity : -1L,
                unit = unit,
                batchSourceId = batchSourceId ?? string.Empty,
                parentLotIds = ProductionStageDefinitionData.NormalizeIds(parentLotIds),
                childLotIds = ProductionStageDefinitionData.NormalizeIds(childLotIds),
                locationId = locationId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                compositionSummary = compositionSummary ?? string.Empty,
                qualitySummary = qualitySummary ?? string.Empty,
                puritySummary = puritySummary ?? string.Empty,
                recipeDefinitionId = recipeDefinitionId ?? string.Empty,
                recipeVersionId = recipeVersionId ?? string.Empty,
                materialAssignments = (materialAssignments ?? Array.Empty<ProductionLotMaterialAssignmentData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToArray(),
                deterministicSeed = deterministicSeed ?? string.Empty,
                createdWorldTime = Math.Max(0d, createdWorldTime),
                nextMaterializationIndex = Math.Max(0L, nextMaterializationIndex),
                baseQuality = Math.Clamp(baseQuality, 0f, 1f),
                qualityVariation = Math.Clamp(qualityVariation, 0f, 1f),
                provenance = provenance ?? string.Empty,
                state = state,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProductionIntermediateData
    {
        public string intermediateId;
        public string jobId;
        public string producingStageId;
        public string[] consumingStageIds = Array.Empty<string>();
        public string itemInstanceId;
        public string lotId;
        public float quantity;
        public ProductionQuantityUnit unit = ProductionQuantityUnit.Count;
        public ProductionIntermediateState state = ProductionIntermediateState.Available;
        public string locationId;
        public string containerId;
        public string ownerId;
        public string custodianId;
        public string reservationId;
        public string expirationWorldTime;
        public string qualitySummary;
        public string compositionSummary;
        public string durabilitySummary;
        public string provenance;
        public long revision = 1L;

        public ProductionIntermediateData Clone()
        {
            return new ProductionIntermediateData
            {
                intermediateId = intermediateId ?? string.Empty,
                jobId = jobId ?? string.Empty,
                producingStageId = producingStageId ?? string.Empty,
                consumingStageIds = ProductionStageDefinitionData.NormalizeIds(consumingStageIds),
                itemInstanceId = itemInstanceId ?? string.Empty,
                lotId = lotId ?? string.Empty,
                quantity = Math.Max(0f, quantity),
                unit = unit,
                state = state,
                locationId = locationId ?? string.Empty,
                containerId = containerId ?? string.Empty,
                ownerId = ownerId ?? string.Empty,
                custodianId = custodianId ?? string.Empty,
                reservationId = reservationId ?? string.Empty,
                expirationWorldTime = expirationWorldTime ?? string.Empty,
                qualitySummary = qualitySummary ?? string.Empty,
                compositionSummary = compositionSummary ?? string.Empty,
                durabilitySummary = durabilitySummary ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProductionStationOccupancyData
    {
        public string occupancyId;
        public string jobId;
        public string stageId;
        public string stationInstanceId;
        public int capacityUnits = 1;
        public string startWorldTime;
        public string expectedReleaseWorldTime;
        public string actualReleaseWorldTime;
        public ProductionOccupancyState state = ProductionOccupancyState.Active;
        public string[] reservationIds = Array.Empty<string>();
        public string[] workerAssignmentIds = Array.Empty<string>();
        public string[] toolItemIds = Array.Empty<string>();
        public long revision = 1L;

        public ProductionStationOccupancyData Clone()
        {
            return new ProductionStationOccupancyData
            {
                occupancyId = occupancyId ?? string.Empty,
                jobId = jobId ?? string.Empty,
                stageId = stageId ?? string.Empty,
                stationInstanceId = stationInstanceId ?? string.Empty,
                capacityUnits = Math.Max(1, capacityUnits),
                startWorldTime = startWorldTime ?? string.Empty,
                expectedReleaseWorldTime = expectedReleaseWorldTime ?? string.Empty,
                actualReleaseWorldTime = actualReleaseWorldTime ?? string.Empty,
                state = state,
                reservationIds = ProductionStageDefinitionData.NormalizeIds(reservationIds),
                workerAssignmentIds = ProductionStageDefinitionData.NormalizeIds(workerAssignmentIds),
                toolItemIds = ProductionStageDefinitionData.NormalizeIds(toolItemIds),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProductionWorkerAssignmentData
    {
        public string assignmentId;
        public string jobId;
        public string stageId;
        public string personId;
        public string actorId;
        public ProductionWorkerRole role = ProductionWorkerRole.PrimaryCrafter;
        public bool primary;
        public string startWorldTime;
        public string endWorldTime;
        public string skillSnapshot;
        public string capabilitySnapshot;
        public string knowledgeSourceId;
        public string accessDecisionId;
        public ProductionWorkerAssignmentState state = ProductionWorkerAssignmentState.Assigned;
        public float contributionWeight = 1f;
        public long revision = 1L;

        public ProductionWorkerAssignmentData Clone()
        {
            return new ProductionWorkerAssignmentData
            {
                assignmentId = assignmentId ?? string.Empty,
                jobId = jobId ?? string.Empty,
                stageId = stageId ?? string.Empty,
                personId = personId ?? string.Empty,
                actorId = actorId ?? string.Empty,
                role = role,
                primary = primary,
                startWorldTime = startWorldTime ?? string.Empty,
                endWorldTime = endWorldTime ?? string.Empty,
                skillSnapshot = skillSnapshot ?? string.Empty,
                capabilitySnapshot = capabilitySnapshot ?? string.Empty,
                knowledgeSourceId = knowledgeSourceId ?? string.Empty,
                accessDecisionId = accessDecisionId ?? string.Empty,
                state = state,
                contributionWeight = Math.Max(0f, contributionWeight),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProductionWorkflowEventData
    {
        public string eventId;
        public string eventKind;
        public string jobId;
        public string stageId;
        public string workOrderId;
        public string batchId;
        public string lotId;
        public string worldTime;
        public string message;
        public long sequence;

        public ProductionWorkflowEventData Clone()
        {
            return new ProductionWorkflowEventData
            {
                eventId = eventId ?? string.Empty,
                eventKind = eventKind ?? string.Empty,
                jobId = jobId ?? string.Empty,
                stageId = stageId ?? string.Empty,
                workOrderId = workOrderId ?? string.Empty,
                batchId = batchId ?? string.Empty,
                lotId = lotId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                message = message ?? string.Empty,
                sequence = sequence
            };
        }
    }

    [Serializable]
    public sealed class ProductionWorkflowRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 3;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public long nextEventSequence;
        public List<ProductionWorkOrderData> workOrders = new List<ProductionWorkOrderData>();
        public List<ProductionJobData> jobs = new List<ProductionJobData>();
        public List<ProductionQueueData> queues = new List<ProductionQueueData>();
        public List<ProductionBatchData> batches = new List<ProductionBatchData>();
        public List<ProductionLotData> lots = new List<ProductionLotData>();
        public List<ProductionIntermediateData> intermediates = new List<ProductionIntermediateData>();
        public List<ProductionStationOccupancyData> occupancies = new List<ProductionStationOccupancyData>();
        public List<ProductionWorkerAssignmentData> assignments = new List<ProductionWorkerAssignmentData>();
        public List<ProductionWorkflowEventData> events = new List<ProductionWorkflowEventData>();

        public ProductionWorkflowRuntimeSaveData Clone()
        {
            return new ProductionWorkflowRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                nextEventSequence = nextEventSequence,
                workOrders = (workOrders ?? new List<ProductionWorkOrderData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                jobs = (jobs ?? new List<ProductionJobData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                queues = (queues ?? new List<ProductionQueueData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                batches = (batches ?? new List<ProductionBatchData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                lots = (lots ?? new List<ProductionLotData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                intermediates = (intermediates ?? new List<ProductionIntermediateData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                occupancies = (occupancies ?? new List<ProductionStationOccupancyData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                assignments = (assignments ?? new List<ProductionWorkerAssignmentData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                events = (events ?? new List<ProductionWorkflowEventData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToList()
            };
        }
    }

    public sealed class ProductionWorkflowResult
    {
        private ProductionWorkflowResult(ProductionWorkflowStatus status, string message, bool preview, bool duplicate, ProductionWorkOrderData workOrder, ProductionJobData job, ProductionQueueData queue, ProductionBatchData batch, ProductionLotData lot, ProductionIntermediateData intermediate, IReadOnlyList<string> diagnostics)
        {
            Status = status;
            Message = message ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            WorkOrder = workOrder?.Clone();
            Job = job?.Clone();
            Queue = queue?.Clone();
            Batch = batch?.Clone();
            Lot = lot?.Clone();
            Intermediate = intermediate?.Clone();
            Diagnostics = (diagnostics ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public ProductionWorkflowStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == ProductionWorkflowStatus.Succeeded || Status == ProductionWorkflowStatus.Preview || Status == ProductionWorkflowStatus.Duplicate;
        public bool Preview { get; }
        public bool Duplicate { get; }
        public ProductionWorkOrderData WorkOrder { get; }
        public ProductionJobData Job { get; }
        public ProductionQueueData Queue { get; }
        public ProductionBatchData Batch { get; }
        public ProductionLotData Lot { get; }
        public ProductionIntermediateData Intermediate { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public static ProductionWorkflowResult Success(string message, ProductionWorkOrderData workOrder = null, ProductionJobData job = null, ProductionQueueData queue = null, ProductionBatchData batch = null, ProductionLotData lot = null, ProductionIntermediateData intermediate = null, IReadOnlyList<string> diagnostics = null, bool duplicate = false)
        {
            return new ProductionWorkflowResult(duplicate ? ProductionWorkflowStatus.Duplicate : ProductionWorkflowStatus.Succeeded, message, false, duplicate, workOrder, job, queue, batch, lot, intermediate, diagnostics);
        }

        public static ProductionWorkflowResult PreviewResult(string message, ProductionWorkOrderData workOrder = null, ProductionJobData job = null, IReadOnlyList<string> diagnostics = null)
        {
            return new ProductionWorkflowResult(ProductionWorkflowStatus.Preview, message, true, false, workOrder, job, null, null, null, null, diagnostics);
        }

        public static ProductionWorkflowResult Failure(ProductionWorkflowStatus status, string message, ProductionWorkOrderData workOrder = null, ProductionJobData job = null, IReadOnlyList<string> diagnostics = null)
        {
            return new ProductionWorkflowResult(status, message, false, false, workOrder, job, null, null, null, null, diagnostics);
        }
    }

    public sealed class ProductionProjectionData
    {
        public ProductionProjectionData(string subjectId, ProductionProjectionDecision decision, string displayName, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields)
        {
            SubjectId = subjectId ?? string.Empty;
            Decision = decision;
            DisplayName = displayName ?? string.Empty;
            VisibleFields = (visibleFields ?? Array.Empty<string>()).ToArray();
            RedactedFields = (redactedFields ?? Array.Empty<string>()).ToArray();
        }

        public string SubjectId { get; }
        public ProductionProjectionDecision Decision { get; }
        public string DisplayName { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    public static class ProductionInformationSubject
    {
        public static InformationSubjectReferenceData Create(string kind, string subjectId, string parentSubjectId = "", string ownerPersonId = "")
        {
            string normalizedKind = string.IsNullOrWhiteSpace(kind) ? "production" : kind.Trim();
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                tags = new[] { "domain.production", $"production.{normalizedKind}" }
            };
        }
    }
}
