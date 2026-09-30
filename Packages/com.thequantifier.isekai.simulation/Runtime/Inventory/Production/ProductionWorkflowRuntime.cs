using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;

namespace UnityIsekaiGame.Inventory.Production
{
    public sealed class ProductionWorkflowRuntime
    {
        private readonly Dictionary<string, ProductionWorkOrderData> workOrdersById = new Dictionary<string, ProductionWorkOrderData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProductionJobData> jobsById = new Dictionary<string, ProductionJobData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProductionQueueData> queuesById = new Dictionary<string, ProductionQueueData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProductionBatchData> batchesById = new Dictionary<string, ProductionBatchData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProductionLotData> lotsById = new Dictionary<string, ProductionLotData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProductionIntermediateData> intermediatesById = new Dictionary<string, ProductionIntermediateData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProductionStationOccupancyData> occupanciesById = new Dictionary<string, ProductionStationOccupancyData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProductionWorkerAssignmentData> assignmentsById = new Dictionary<string, ProductionWorkerAssignmentData>(StringComparer.Ordinal);
        private readonly List<ProductionWorkflowEventData> events = new List<ProductionWorkflowEventData>();
        private long revision;
        private long nextEventSequence;

        public long Revision => revision;
        public int WorkOrderCount => workOrdersById.Count;
        public int JobCount => jobsById.Count;
        public int QueueCount => queuesById.Count;
        public int BatchCount => batchesById.Count;
        public int LotCount => lotsById.Count;
        public int IntermediateCount => intermediatesById.Count;
        public int OccupancyCount => occupanciesById.Count;
        public int AssignmentCount => assignmentsById.Count;

        public IReadOnlyList<ProductionWorkOrderData> WorkOrders => workOrdersById.Values.OrderBy(entry => entry.workOrderId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ProductionJobData> Jobs => jobsById.Values.OrderBy(entry => entry.jobId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ProductionQueueData> Queues => queuesById.Values.OrderBy(entry => entry.queueId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ProductionBatchData> Batches => batchesById.Values.OrderBy(entry => entry.batchId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ProductionLotData> Lots => lotsById.Values.OrderBy(entry => entry.lotId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ProductionIntermediateData> Intermediates => intermediatesById.Values.OrderBy(entry => entry.intermediateId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ProductionStationOccupancyData> Occupancies => occupanciesById.Values.OrderBy(entry => entry.occupancyId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ProductionWorkerAssignmentData> Assignments => assignmentsById.Values.OrderBy(entry => entry.assignmentId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ProductionWorkflowEventData> Events => events.OrderBy(entry => entry.sequence).ThenBy(entry => entry.eventId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();

        public bool TryGetWorkOrder(string workOrderId, out ProductionWorkOrderData workOrder)
        {
            if (!string.IsNullOrWhiteSpace(workOrderId) && workOrdersById.TryGetValue(workOrderId, out ProductionWorkOrderData found))
            {
                workOrder = found.Clone();
                return true;
            }

            workOrder = null;
            return false;
        }

        public bool TryGetJob(string jobId, out ProductionJobData job)
        {
            if (!string.IsNullOrWhiteSpace(jobId) && jobsById.TryGetValue(jobId, out ProductionJobData found))
            {
                job = found.Clone();
                return true;
            }

            job = null;
            return false;
        }

        public bool TryGetLot(string lotId, out ProductionLotData lot)
        {
            if (!string.IsNullOrWhiteSpace(lotId) && lotsById.TryGetValue(lotId, out ProductionLotData found))
            {
                lot = found.Clone();
                return true;
            }

            lot = null;
            return false;
        }

        public ProductionWorkflowResult CreateWorkOrder(ProductionWorkOrderData request, DefinitionRegistry registry, bool preview = false)
        {
            ProductionWorkOrderData workOrder = (request ?? new ProductionWorkOrderData()).Clone();
            if (string.IsNullOrWhiteSpace(workOrder.workOrderId))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Work order ID is required.");
            }

            if (workOrder.requestedQuantity <= 0)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Work order quantity must be positive.");
            }

            if (string.IsNullOrWhiteSpace(workOrder.recipeDefinitionId) && string.IsNullOrWhiteSpace(workOrder.chainDefinitionId))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Work order must target a recipe or production chain.");
            }

            if (!string.IsNullOrWhiteSpace(workOrder.recipeDefinitionId) && registry != null && !registry.TryGet(workOrder.recipeDefinitionId, out RecipeDefinition _))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingDefinition, $"Recipe definition '{workOrder.recipeDefinitionId}' was not found.");
            }

            if (!string.IsNullOrWhiteSpace(workOrder.chainDefinitionId) && registry != null && !registry.TryGet(workOrder.chainDefinitionId, out ProductionChainDefinition chain))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingDefinition, $"Production chain definition '{workOrder.chainDefinitionId}' was not found.");
            }

            if (!string.IsNullOrWhiteSpace(workOrder.chainDefinitionId) && registry != null && registry.TryGet(workOrder.chainDefinitionId, out ProductionChainDefinition resolvedChain) && !resolvedChain.TryGetVersion(workOrder.versionId, out _))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingDefinition, $"Production chain version '{workOrder.versionId}' was not found.");
            }

            if (workOrdersById.TryGetValue(workOrder.workOrderId, out ProductionWorkOrderData existing))
            {
                return ProductionWorkflowResult.Success("Work order already exists.", workOrder: existing, duplicate: true);
            }

            if (preview)
            {
                return ProductionWorkflowResult.PreviewResult("Work order preview prepared.", workOrder: workOrder);
            }

            workOrder.state = workOrder.state == ProductionWorkOrderState.Draft ? ProductionWorkOrderState.Submitted : workOrder.state;
            workOrder.revision = 1L;
            workOrdersById.Add(workOrder.workOrderId, workOrder.Clone());
            Touch($"work-order-created.{workOrder.workOrderId}", "WorkOrderCreated", workOrder.workOrderId, string.Empty, workOrder.workOrderId, string.Empty, string.Empty, workOrder.earliestStartWorldTime, "Work order created.");
            return ProductionWorkflowResult.Success("Work order created.", workOrder: workOrder);
        }

        public ProductionWorkflowResult TransitionWorkOrder(string workOrderId, ProductionWorkOrderState targetState)
        {
            if (!workOrdersById.TryGetValue(workOrderId ?? string.Empty, out ProductionWorkOrderData workOrder))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingWorkOrder, $"Work order '{workOrderId}' was not found.");
            }

            if (!CanTransition(workOrder.state, targetState))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidState, $"Cannot transition work order '{workOrderId}' from {workOrder.state} to {targetState}.");
            }

            workOrder.state = targetState;
            workOrder.revision++;
            Touch($"work-order-state.{workOrderId}.{targetState}", "WorkOrderStateChanged", workOrderId, string.Empty, workOrderId, string.Empty, string.Empty, string.Empty, $"Work order state changed to {targetState}.");
            return ProductionWorkflowResult.Success("Work order state changed.", workOrder: workOrder);
        }

        public ProductionWorkflowResult CreateJobFromWorkOrder(
            string jobId,
            string workOrderId,
            DefinitionRegistry registry,
            string queueId = "",
            string deterministicSeed = "",
            bool preview = false)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Production job ID is required.");
            }

            if (jobsById.ContainsKey(jobId))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, $"Production job '{jobId}' already exists.");
            }

            if (!workOrdersById.TryGetValue(workOrderId ?? string.Empty, out ProductionWorkOrderData workOrder))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingWorkOrder, $"Work order '{workOrderId}' was not found.");
            }

            if (workOrder.state != ProductionWorkOrderState.Approved && workOrder.state != ProductionWorkOrderState.Planned && workOrder.state != ProductionWorkOrderState.Submitted)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidState, $"Work order '{workOrderId}' is not ready for job creation.");
            }

            ProductionChainDefinition chain = null;
            ProductionChainVersionData chainVersion = null;
            if (!string.IsNullOrWhiteSpace(workOrder.chainDefinitionId))
            {
                if (registry == null || !registry.TryGet(workOrder.chainDefinitionId, out chain))
                {
                    return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingDefinition, $"Production chain definition '{workOrder.chainDefinitionId}' was not found.");
                }

                if (!chain.TryGetVersion(workOrder.versionId, out chainVersion))
                {
                    return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingDefinition, $"Production chain version '{workOrder.versionId}' was not found.");
                }
            }
            else if (registry != null && !registry.TryGet(workOrder.recipeDefinitionId, out RecipeDefinition _))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingDefinition, $"Recipe definition '{workOrder.recipeDefinitionId}' was not found.");
            }

            ProductionWorkflowRuntimeSaveData rollback = CreateSaveData();
            try
            {
                string batchId = StableId("production-batch", jobId, workOrder.workOrderId);
                ProductionJobData job = new ProductionJobData
                {
                    jobId = jobId,
                    workOrderId = workOrder.workOrderId,
                    recipeDefinitionId = workOrder.recipeDefinitionId,
                    chainDefinitionId = workOrder.chainDefinitionId,
                    versionId = string.IsNullOrWhiteSpace(workOrder.versionId) && chain != null ? chain.CurrentVersionId : workOrder.versionId,
                    variantId = workOrder.variantId,
                    batchQuantity = workOrder.requestedQuantity,
                    batchId = batchId,
                    queueId = queueId ?? string.Empty,
                    ownerPersonId = workOrder.ownerPersonId,
                    custodianPersonId = workOrder.custodianPersonId,
                    state = string.IsNullOrWhiteSpace(queueId) ? ProductionJobState.Ready : ProductionJobState.Queued,
                    priority = workOrder.priority,
                    deterministicSeed = string.IsNullOrWhiteSpace(deterministicSeed) ? StableId("production-seed", jobId) : deterministicSeed,
                    provenance = $"work-order={workOrder.workOrderId}",
                    outputCollectionState = ProductionOutputCollectionState.NotReady
                };

                job.stages = BuildStageProgress(job, chainVersion);
                job.readyStageIds = ReadyStageIds(job).ToArray();
                job.currentStageId = job.readyStageIds.FirstOrDefault() ?? string.Empty;

                ProductionBatchData batch = new ProductionBatchData
                {
                    batchId = batchId,
                    jobId = jobId,
                    recipeOrChainId = string.IsNullOrWhiteSpace(job.chainDefinitionId) ? job.recipeDefinitionId : job.chainDefinitionId,
                    versionId = job.versionId,
                    variantId = job.variantId,
                    batchQuantity = job.batchQuantity,
                    consistencyPolicy = chain?.BatchConsistencyPolicy ?? ProductionBatchConsistencyPolicy.IdenticalAuthoritativeState,
                    partialBatchPolicy = chain?.PartialBatchPolicy ?? ProductionPartialBatchPolicy.AllOrNothing,
                    deterministicSeed = StableId("production-batch-seed", jobId, batchId),
                    provenance = job.provenance
                };

                if (preview)
                {
                    return ProductionWorkflowResult.PreviewResult("Production job preview prepared.", workOrder: workOrder, job: job);
                }

                jobsById.Add(job.jobId, job.Clone());
                batchesById.Add(batch.batchId, batch.Clone());
                workOrder.jobIds = Append(workOrder.jobIds, job.jobId);
                workOrder.state = string.IsNullOrWhiteSpace(queueId) ? ProductionWorkOrderState.Planned : ProductionWorkOrderState.Queued;
                workOrder.revision++;

                if (!string.IsNullOrWhiteSpace(queueId))
                {
                    ProductionWorkflowResult enqueue = EnqueueJob(queueId, job.jobId);
                    if (!enqueue.Succeeded)
                    {
                        RestoreFromSaveData(rollback, registry);
                        return enqueue;
                    }
                }
                else
                {
                    Touch($"job-created.{job.jobId}", "JobCreated", job.jobId, string.Empty, workOrder.workOrderId, batch.batchId, string.Empty, string.Empty, "Production job created.");
                }

                return ProductionWorkflowResult.Success("Production job created.", workOrder: workOrder, job: jobsById[job.jobId], batch: batch);
            }
            catch (Exception ex)
            {
                RestoreFromSaveData(rollback, registry);
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.RollbackFailed, ex.Message);
            }
        }

        public ProductionWorkflowResult EnsureQueue(string queueId, ProductionQueuePolicy policy = ProductionQueuePolicy.PriorityThenFifo, string ownerId = "", string stationInstanceId = "", int maximumLength = 100)
        {
            if (string.IsNullOrWhiteSpace(queueId))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Queue ID is required.");
            }

            if (queuesById.TryGetValue(queueId, out ProductionQueueData existing))
            {
                return ProductionWorkflowResult.Success("Queue already exists.", queue: existing, duplicate: true);
            }

            ProductionQueueData queue = new ProductionQueueData
            {
                queueId = queueId,
                policy = policy,
                ownerId = ownerId ?? string.Empty,
                stationInstanceId = stationInstanceId ?? string.Empty,
                maximumLength = Math.Max(1, maximumLength)
            };
            queuesById.Add(queue.queueId, queue);
            Touch($"queue-created.{queue.queueId}", "QueueCreated", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "Production queue created.");
            return ProductionWorkflowResult.Success("Queue created.", queue: queue);
        }

        public ProductionWorkflowResult EnqueueJob(string queueId, string jobId)
        {
            if (!queuesById.TryGetValue(queueId ?? string.Empty, out ProductionQueueData queue))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingQueue, $"Queue '{queueId}' was not found.");
            }

            if (!jobsById.TryGetValue(jobId ?? string.Empty, out ProductionJobData job))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingJob, $"Production job '{jobId}' was not found.");
            }

            if (IsTerminal(job.state))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidState, $"Terminal job '{jobId}' cannot be queued.");
            }

            if (queue.jobIds.Contains(jobId, StringComparer.Ordinal))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, $"Job '{jobId}' is already in queue '{queueId}'.");
            }

            if (queue.jobIds.Count >= queue.maximumLength)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, $"Queue '{queueId}' is full.");
            }

            queue.jobIds.Add(jobId);
            queue.jobIds = OrderQueue(queue).ToList();
            queue.revision++;
            job.queueId = queueId;
            job.state = queue.state == ProductionQueueState.Paused ? ProductionJobState.Blocked : ProductionJobState.Queued;
            job.revision++;
            Touch($"job-enqueued.{queueId}.{jobId}", "JobQueued", jobId, string.Empty, job.workOrderId, job.batchId, string.Empty, string.Empty, "Production job queued.");
            return ProductionWorkflowResult.Success("Production job queued.", job: job, queue: queue);
        }

        public ProductionWorkflowResult ReorderQueue(string queueId, IReadOnlyList<string> orderedJobIds)
        {
            if (!queuesById.TryGetValue(queueId ?? string.Empty, out ProductionQueueData queue))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingQueue, $"Queue '{queueId}' was not found.");
            }

            string[] requested = ProductionStageDefinitionData.NormalizeIds(orderedJobIds);
            if (!requested.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(queue.jobIds.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Queue reorder must include exactly the existing queued job IDs.");
            }

            queue.jobIds = requested.ToList();
            queue.policy = ProductionQueuePolicy.ManualOrder;
            queue.revision++;
            Touch($"queue-reordered.{queueId}.{queue.revision}", "QueueReordered", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "Production queue reordered.");
            return ProductionWorkflowResult.Success("Queue reordered.", queue: queue);
        }

        public ProductionWorkflowResult StartStage(
            string jobId,
            string stageId,
            ProductionRequirementRuntime productionRuntime,
            DefinitionRegistry registry,
            ProductionContextData context,
            string stationInstanceId = "",
            int capacityUnits = 1,
            string worldTime = "")
        {
            if (!jobsById.TryGetValue(jobId ?? string.Empty, out ProductionJobData job))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingJob, $"Production job '{jobId}' was not found.");
            }

            ProductionStageProgressData stage = FindStage(job, stageId);
            if (stage == null)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingStage, $"Production stage '{stageId}' was not found.");
            }

            if (stage.state == ProductionStageRuntimeState.Completed)
            {
                return ProductionWorkflowResult.Success("Production stage was already completed.", job: job, duplicate: true);
            }

            if (stage.state != ProductionStageRuntimeState.Ready && stage.state != ProductionStageRuntimeState.Paused && stage.state != ProductionStageRuntimeState.Interrupted)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidState, $"Stage '{stage.stageId}' is not ready to start.");
            }

            string missingDependency = stage.dependencyStageIds.FirstOrDefault(id => !job.completedStageIds.Contains(id, StringComparer.Ordinal));
            if (!string.IsNullOrWhiteSpace(missingDependency))
            {
                stage.state = ProductionStageRuntimeState.Blocked;
                stage.blockReason = $"Dependency '{missingDependency}' is not complete.";
                job.blockedStageIds = Append(job.blockedStageIds, stage.stageId);
                job.state = ProductionJobState.Blocked;
                job.blockReason = stage.blockReason;
                job.revision++;
                revision++;
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.DependencyBlocked, stage.blockReason, job: job);
            }

            ProductionWorkflowRuntimeSaveData rollback = CreateSaveData();
            try
            {
                if (productionRuntime != null && registry != null && !string.IsNullOrWhiteSpace(stage.requirementPlanId))
                {
                    ProductionRequirementEvaluationResult current = productionRuntime.ValidatePlanCurrent(stage.requirementPlanId);
                    if (current == null || !current.Succeeded)
                    {
                        RestoreFromSaveData(rollback, registry);
                        return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.ReservationFailed, current?.Message ?? "Stage requirement plan is not current.", job: job);
                    }
                }

                if (!string.IsNullOrWhiteSpace(stationInstanceId) && !CanClaimStation(stationInstanceId, capacityUnits, out string occupancyFailure))
                {
                    RestoreFromSaveData(rollback, registry);
                    return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.OccupancyFailed, occupancyFailure, job: job);
                }

                stage.attemptCount++;
                stage.state = ProductionStageRuntimeState.Running;
                stage.startWorldTime = string.IsNullOrWhiteSpace(stage.startWorldTime) ? worldTime : stage.startWorldTime;
                stage.lastEvaluatedWorldTime = string.IsNullOrWhiteSpace(stage.lastEvaluatedWorldTime) ? worldTime : stage.lastEvaluatedWorldTime;
                stage.revision++;
                job.state = ProductionJobState.Running;
                job.currentStageId = stage.stageId;
                job.startWorldTime = string.IsNullOrWhiteSpace(job.startWorldTime) ? worldTime : job.startWorldTime;
                job.lastEvaluatedWorldTime = string.IsNullOrWhiteSpace(job.lastEvaluatedWorldTime) ? worldTime : job.lastEvaluatedWorldTime;
                job.readyStageIds = job.readyStageIds.Where(id => !string.Equals(id, stage.stageId, StringComparison.Ordinal)).ToArray();
                job.revision++;

                if (!string.IsNullOrWhiteSpace(stationInstanceId))
                {
                    string occupancyId = StableId("production-occupancy", job.jobId, stage.stageId, stationInstanceId);
                    ProductionStationOccupancyData occupancy = new ProductionStationOccupancyData
                    {
                        occupancyId = occupancyId,
                        jobId = job.jobId,
                        stageId = stage.stageId,
                        stationInstanceId = stationInstanceId,
                        capacityUnits = Math.Max(1, capacityUnits),
                        startWorldTime = worldTime ?? string.Empty,
                        expectedReleaseWorldTime = string.Empty,
                        state = ProductionOccupancyState.Active,
                        reservationIds = stage.reservationIds
                    };
                    occupanciesById[occupancyId] = occupancy;
                    stage.occupancyId = occupancyId;
                    job.occupancyIds = Append(job.occupancyIds, occupancyId);
                }

                Touch($"stage-started.{job.jobId}.{stage.stageId}.{stage.attemptCount}", "StageStarted", job.jobId, stage.stageId, job.workOrderId, job.batchId, string.Empty, worldTime, "Production stage started.");
                return ProductionWorkflowResult.Success("Production stage started.", job: job);
            }
            catch (Exception ex)
            {
                RestoreFromSaveData(rollback, registry);
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.RollbackFailed, ex.Message, job: job);
            }
        }

        public ProductionWorkflowResult EvaluateJobToWorldTime(string jobId, string worldTime)
        {
            if (!jobsById.TryGetValue(jobId ?? string.Empty, out ProductionJobData job))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingJob, $"Production job '{jobId}' was not found.");
            }

            if (!TryParseWorldTime(worldTime, out double target))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.TimeRejected, $"World time '{worldTime}' is not a deterministic numeric boundary.");
            }

            if (job.state == ProductionJobState.Paused || job.state == ProductionJobState.Blocked || job.state == ProductionJobState.Interrupted || IsTerminal(job.state))
            {
                return ProductionWorkflowResult.Success("Production job is not active at this boundary.", job: job);
            }

            ProductionStageProgressData stage = FindStage(job, job.currentStageId);
            if (stage == null || stage.state != ProductionStageRuntimeState.Running)
            {
                return ProductionWorkflowResult.Success("No running production stage exists.", job: job);
            }

            TryParseWorldTime(stage.lastEvaluatedWorldTime, out double last);
            if (target < last)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.TimeRejected, "Production evaluation cannot move backward in world time.", job: job);
            }

            if (Math.Abs(target - last) < 0.0001d)
            {
                return ProductionWorkflowResult.Success("Production job was already evaluated at this boundary.", job: job, duplicate: true);
            }

            float delta = (float)(target - last);
            stage.completedWork = Math.Min(stage.requiredWork, stage.completedWork + delta);
            stage.lastEvaluatedWorldTime = worldTime ?? string.Empty;
            job.lastEvaluatedWorldTime = worldTime ?? string.Empty;
            if (stage.completedWork >= stage.requiredWork)
            {
                stage.state = ProductionStageRuntimeState.ReadyToComplete;
                job.state = ProductionJobState.Completing;
            }

            stage.revision++;
            job.revision++;
            revision++;
            return ProductionWorkflowResult.Success("Production job progressed.", job: job);
        }

        public ProductionWorkflowResult CompleteStage(
            string jobId,
            string stageId,
            DefinitionRegistry registry,
            RecipeRuntime recipeRuntime,
            ProductionRequirementRuntime productionRuntime,
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            ItemDurabilityRuntime durabilityRuntime,
            CraftingExecutionRuntime craftingRuntime,
            ProductionContextData productionContext,
            string worldTime = "")
        {
            if (!jobsById.TryGetValue(jobId ?? string.Empty, out ProductionJobData job))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingJob, $"Production job '{jobId}' was not found.");
            }

            ProductionStageProgressData stage = FindStage(job, stageId);
            if (stage == null)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingStage, $"Production stage '{stageId}' was not found.");
            }

            if (stage.state == ProductionStageRuntimeState.Completed)
            {
                return ProductionWorkflowResult.Success("Production stage was already completed.", job: job, duplicate: true);
            }

            if (stage.state != ProductionStageRuntimeState.ReadyToComplete && stage.state != ProductionStageRuntimeState.Running && stage.state != ProductionStageRuntimeState.Ready)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidState, $"Production stage '{stage.stageId}' cannot complete from state {stage.state}.", job: job);
            }

            ProductionWorkflowRuntimeSaveData rollback = CreateSaveData();
            try
            {
                if (!string.IsNullOrWhiteSpace(stage.recipeDefinitionId))
                {
                    if (craftingRuntime == null)
                    {
                        RestoreFromSaveData(rollback, registry);
                        return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.CraftingFailed, "Crafting execution runtime is required for recipe stages.", job: job);
                    }

                    string operationId = string.IsNullOrWhiteSpace(stage.craftingOperationId)
                        ? StableId("crafting-operation.production-stage", job.jobId, stage.stageId)
                        : stage.craftingOperationId;
                    CraftingExecutionRequest request = new CraftingExecutionRequest
                    {
                        operationId = operationId,
                        recipeId = stage.recipeDefinitionId,
                        versionId = stage.recipeVersionId,
                        variantId = stage.recipeVariantId,
                        batchSize = Math.Max(1, job.batchQuantity),
                        actorPersonId = job.ownerPersonId,
                        ownerPersonId = job.ownerPersonId,
                        custodianPersonId = job.custodianPersonId,
                        locationId = productionContext?.locationId ?? string.Empty,
                        worldTime = worldTime ?? string.Empty,
                        deterministicSeed = StableId("production-stage-seed", job.jobId, stage.stageId, job.deterministicSeed),
                        productionContext = productionContext?.Clone() ?? new ProductionContextData()
                    };
                    CraftingExecutionResult crafted = craftingRuntime.Execute(request, registry, recipeRuntime, productionRuntime, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime);
                    if (crafted == null || !crafted.Succeeded)
                    {
                        RestoreFromSaveData(rollback, registry);
                        return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.CraftingFailed, crafted?.Message ?? "Production stage crafting failed.", job: job);
                    }

                    stage.craftingOperationId = operationId;
                    stage.outputItemIds = Append(stage.outputItemIds, crafted.Operation?.outputs?.Where(output => output.createdItemInstance).Select(output => output.itemInstanceId).ToArray());
                    job.outputItemIds = Append(job.outputItemIds, stage.outputItemIds);
                    if (batchesById.TryGetValue(job.batchId, out ProductionBatchData batch))
                    {
                        batch.outputItemIds = Append(batch.outputItemIds, stage.outputItemIds);
                        batch.completionWorldTime = worldTime ?? string.Empty;
                        batch.revision++;
                    }
                }

                string lotId = StableId("production-lot", job.jobId, stage.stageId);
                if (!lotsById.ContainsKey(lotId))
                {
                    ProductionLotData lot = new ProductionLotData
                    {
                        lotId = lotId,
                        definitionOrMaterialId = string.IsNullOrWhiteSpace(stage.recipeDefinitionId) ? job.chainDefinitionId : stage.recipeDefinitionId,
                        ownerId = job.ownerPersonId,
                        custodianId = job.custodianPersonId,
                        containedItemIds = stage.outputItemIds,
                        quantity = Math.Max(1, stage.outputItemIds.Length == 0 ? job.batchQuantity : stage.outputItemIds.Length),
                        unit = ProductionQuantityUnit.Count,
                        batchSourceId = job.batchId,
                        provenance = $"job={job.jobId};stage={stage.stageId};batch={job.batchId}"
                    };
                    lotsById.Add(lotId, lot);
                    job.outputLotIds = Append(job.outputLotIds, lotId);
                    if (batchesById.TryGetValue(job.batchId, out ProductionBatchData batch))
                    {
                        batch.outputLotIds = Append(batch.outputLotIds, lotId);
                    }
                }

                if (!IsFinalStage(job, stage.stageId))
                {
                    string intermediateId = StableId("production-intermediate", job.jobId, stage.stageId);
                    if (!intermediatesById.ContainsKey(intermediateId))
                    {
                        ProductionIntermediateData intermediate = new ProductionIntermediateData
                        {
                            intermediateId = intermediateId,
                            jobId = job.jobId,
                            producingStageId = stage.stageId,
                            consumingStageIds = job.stages.Where(candidate => candidate.dependencyStageIds.Contains(stage.stageId, StringComparer.Ordinal)).Select(candidate => candidate.stageId).ToArray(),
                            itemInstanceId = stage.outputItemIds.FirstOrDefault() ?? string.Empty,
                            lotId = lotId,
                            quantity = Math.Max(1, stage.outputItemIds.Length == 0 ? job.batchQuantity : stage.outputItemIds.Length),
                            unit = ProductionQuantityUnit.Count,
                            state = ProductionIntermediateState.Available,
                            ownerId = job.ownerPersonId,
                            custodianId = job.custodianPersonId,
                            provenance = $"job={job.jobId};stage={stage.stageId};lot={lotId}"
                        };
                        intermediatesById.Add(intermediateId, intermediate);
                        stage.intermediateIds = Append(stage.intermediateIds, intermediateId);
                        job.intermediateIds = Append(job.intermediateIds, intermediateId);
                    }
                }

                ReleaseOccupancy(stage, worldTime);
                stage.completedWork = stage.requiredWork;
                stage.state = ProductionStageRuntimeState.Completed;
                stage.completionWorldTime = worldTime ?? string.Empty;
                stage.revision++;
                job.completedStageIds = Append(job.completedStageIds, stage.stageId);
                job.readyStageIds = ReadyStageIds(job).ToArray();
                job.blockedStageIds = Array.Empty<string>();
                job.currentStageId = job.readyStageIds.FirstOrDefault() ?? string.Empty;
                job.state = job.readyStageIds.Length == 0 ? ProductionJobState.AwaitingCollection : ProductionJobState.Ready;
                job.outputCollectionState = job.readyStageIds.Length == 0 ? ProductionOutputCollectionState.Ready : ProductionOutputCollectionState.NotReady;
                job.completionWorldTime = job.readyStageIds.Length == 0 ? worldTime ?? string.Empty : string.Empty;
                job.revision++;

                if (workOrdersById.TryGetValue(job.workOrderId, out ProductionWorkOrderData workOrder))
                {
                    workOrder.outputItemIds = Append(workOrder.outputItemIds, job.outputItemIds);
                    workOrder.state = job.readyStageIds.Length == 0 ? ProductionWorkOrderState.Completed : ProductionWorkOrderState.InProgress;
                    workOrder.revision++;
                }

                Touch($"stage-completed.{job.jobId}.{stage.stageId}", "StageCompleted", job.jobId, stage.stageId, job.workOrderId, job.batchId, lotId, worldTime, "Production stage completed.");
                return ProductionWorkflowResult.Success("Production stage completed.", job: job, batch: batchesById.TryGetValue(job.batchId, out ProductionBatchData completedBatch) ? completedBatch : null, lot: lotsById.TryGetValue(lotId, out ProductionLotData completedLot) ? completedLot : null);
            }
            catch (Exception ex)
            {
                RestoreFromSaveData(rollback, registry);
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.RollbackFailed, ex.Message, job: job);
            }
        }

        public ProductionWorkflowResult PauseJob(string jobId, string reason, string worldTime = "", bool releaseOccupancy = true)
        {
            if (!jobsById.TryGetValue(jobId ?? string.Empty, out ProductionJobData job))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingJob, $"Production job '{jobId}' was not found.");
            }

            if (IsTerminal(job.state))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidState, "Terminal jobs cannot pause.");
            }

            foreach (ProductionStageProgressData stage in job.stages.Where(stage => stage.state == ProductionStageRuntimeState.Running))
            {
                stage.state = ProductionStageRuntimeState.Paused;
                stage.pauseReason = reason ?? string.Empty;
                stage.revision++;
                if (releaseOccupancy)
                {
                    ReleaseOccupancy(stage, worldTime);
                }
            }

            job.state = ProductionJobState.Paused;
            job.pauseReason = reason ?? string.Empty;
            job.revision++;
            Touch($"job-paused.{job.jobId}.{job.revision}", "JobPaused", job.jobId, job.currentStageId, job.workOrderId, job.batchId, string.Empty, worldTime, reason);
            return ProductionWorkflowResult.Success("Production job paused.", job: job);
        }

        public ProductionWorkflowResult ResumeJob(string jobId, string worldTime = "")
        {
            if (!jobsById.TryGetValue(jobId ?? string.Empty, out ProductionJobData job))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingJob, $"Production job '{jobId}' was not found.");
            }

            if (job.state != ProductionJobState.Paused && job.state != ProductionJobState.Interrupted && job.state != ProductionJobState.Blocked)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidState, "Only paused, blocked, or interrupted jobs can resume.");
            }

            foreach (ProductionStageProgressData stage in job.stages.Where(stage => stage.state == ProductionStageRuntimeState.Paused || stage.state == ProductionStageRuntimeState.Interrupted || stage.state == ProductionStageRuntimeState.Blocked))
            {
                stage.state = stage.completedWork >= stage.requiredWork ? ProductionStageRuntimeState.ReadyToComplete : ProductionStageRuntimeState.Ready;
                stage.pauseReason = string.Empty;
                stage.interruptReason = string.Empty;
                stage.blockReason = string.Empty;
                stage.revision++;
            }

            job.readyStageIds = ReadyStageIds(job).ToArray();
            job.currentStageId = job.readyStageIds.FirstOrDefault() ?? job.currentStageId;
            job.state = ProductionJobState.Ready;
            job.pauseReason = string.Empty;
            job.blockReason = string.Empty;
            job.failureReason = string.Empty;
            job.revision++;
            Touch($"job-resumed.{job.jobId}.{job.revision}", "JobResumed", job.jobId, job.currentStageId, job.workOrderId, job.batchId, string.Empty, worldTime, "Production job resumed.");
            return ProductionWorkflowResult.Success("Production job resumed.", job: job);
        }

        public ProductionWorkflowResult InterruptJob(string jobId, string reason, string worldTime = "")
        {
            if (!jobsById.TryGetValue(jobId ?? string.Empty, out ProductionJobData job))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingJob, $"Production job '{jobId}' was not found.");
            }

            foreach (ProductionStageProgressData stage in job.stages.Where(stage => stage.state == ProductionStageRuntimeState.Running || stage.state == ProductionStageRuntimeState.Ready))
            {
                stage.state = ProductionStageRuntimeState.Interrupted;
                stage.interruptReason = reason ?? string.Empty;
                stage.revision++;
                ReleaseOccupancy(stage, worldTime, ProductionOccupancyState.Interrupted);
            }

            job.state = ProductionJobState.Interrupted;
            job.blockReason = reason ?? string.Empty;
            job.revision++;
            Touch($"job-interrupted.{job.jobId}.{job.revision}", "JobInterrupted", job.jobId, job.currentStageId, job.workOrderId, job.batchId, string.Empty, worldTime, reason);
            return ProductionWorkflowResult.Success("Production job interrupted.", job: job);
        }

        public ProductionWorkflowResult AssignWorker(string assignmentId, string jobId, string stageId, string personId, ProductionWorkerRole role, string worldTime = "", string skillSnapshot = "", string capabilitySnapshot = "")
        {
            if (string.IsNullOrWhiteSpace(assignmentId) || string.IsNullOrWhiteSpace(personId))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Assignment ID and Person ID are required.");
            }

            if (!jobsById.TryGetValue(jobId ?? string.Empty, out ProductionJobData job))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingJob, $"Production job '{jobId}' was not found.");
            }

            ProductionStageProgressData stage = FindStage(job, stageId);
            if (stage == null)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingStage, $"Production stage '{stageId}' was not found.");
            }

            if (assignmentsById.ContainsKey(assignmentId))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, $"Worker assignment '{assignmentId}' already exists.");
            }

            ProductionWorkerAssignmentData assignment = new ProductionWorkerAssignmentData
            {
                assignmentId = assignmentId,
                jobId = jobId,
                stageId = stageId,
                personId = personId,
                role = role,
                primary = role == ProductionWorkerRole.PrimaryCrafter,
                startWorldTime = worldTime ?? string.Empty,
                skillSnapshot = skillSnapshot ?? string.Empty,
                capabilitySnapshot = capabilitySnapshot ?? string.Empty,
                state = ProductionWorkerAssignmentState.Assigned
            };
            assignmentsById.Add(assignmentId, assignment);
            stage.workerAssignmentIds = Append(stage.workerAssignmentIds, assignmentId);
            job.assignmentIds = Append(job.assignmentIds, assignmentId);
            job.revision++;
            stage.revision++;
            Touch($"worker-assigned.{assignmentId}", "WorkerAssigned", jobId, stageId, job.workOrderId, job.batchId, string.Empty, worldTime, "Production worker assigned.");
            return ProductionWorkflowResult.Success("Production worker assigned.", job: job);
        }

        public ProductionWorkflowResult CancelJob(string jobId, string reason = "", string worldTime = "")
        {
            if (!jobsById.TryGetValue(jobId ?? string.Empty, out ProductionJobData job))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingJob, $"Production job '{jobId}' was not found.");
            }

            if (job.state == ProductionJobState.Cancelled)
            {
                return ProductionWorkflowResult.Success("Production job was already cancelled.", job: job, duplicate: true);
            }

            if (IsTerminal(job.state) && job.state != ProductionJobState.AwaitingCollection)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidState, "Terminal production job cannot be cancelled.");
            }

            foreach (ProductionStageProgressData stage in job.stages.Where(stage => stage.state != ProductionStageRuntimeState.Completed))
            {
                stage.state = ProductionStageRuntimeState.Cancelled;
                stage.revision++;
                ReleaseOccupancy(stage, worldTime, ProductionOccupancyState.Cancelled);
            }

            if (!string.IsNullOrWhiteSpace(job.queueId) && queuesById.TryGetValue(job.queueId, out ProductionQueueData queue))
            {
                queue.jobIds.RemoveAll(id => string.Equals(id, job.jobId, StringComparison.Ordinal));
                queue.revision++;
            }

            job.state = ProductionJobState.Cancelled;
            job.failureReason = reason ?? string.Empty;
            job.outputCollectionState = ProductionOutputCollectionState.NotReady;
            job.revision++;
            if (workOrdersById.TryGetValue(job.workOrderId, out ProductionWorkOrderData workOrder))
            {
                workOrder.state = ProductionWorkOrderState.Cancelled;
                workOrder.revision++;
            }

            Touch($"job-cancelled.{job.jobId}.{job.revision}", "JobCancelled", job.jobId, job.currentStageId, job.workOrderId, job.batchId, string.Empty, worldTime, reason);
            return ProductionWorkflowResult.Success("Production job cancelled.", job: job);
        }

        public ProductionWorkflowResult CollectOutputs(
            string jobId,
            string destinationInventoryOwnerId,
            ItemInstanceIdentityRuntime itemRuntime,
            DefinitionRegistry registry,
            string worldTime = "")
        {
            if (!jobsById.TryGetValue(jobId ?? string.Empty, out ProductionJobData job))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.MissingJob, $"Production job '{jobId}' was not found.");
            }

            if (job.outputCollectionState == ProductionOutputCollectionState.Collected)
            {
                return ProductionWorkflowResult.Success("Production outputs were already collected.", job: job, duplicate: true);
            }

            if (job.outputCollectionState != ProductionOutputCollectionState.Ready && job.state != ProductionJobState.AwaitingCollection)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.CollectionFailed, "Production outputs are not ready for collection.", job: job);
            }

            if (itemRuntime == null || string.IsNullOrWhiteSpace(destinationInventoryOwnerId))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.CollectionFailed, "Output collection requires an item runtime and destination inventory owner.", job: job);
            }

            ItemInstanceRuntimeSaveData itemRollback = itemRuntime.CreateSaveData();
            foreach (string itemInstanceId in job.outputItemIds ?? Array.Empty<string>())
            {
                if (!itemRuntime.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot output)
                    || output.LifecycleState is ItemLifecycleState.Consumed or ItemLifecycleState.Destroyed or ItemLifecycleState.Disassembled)
                {
                    itemRuntime.RestoreFromSaveData(itemRollback, registry);
                    return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.CollectionFailed, $"Output item '{itemInstanceId}' is missing or no longer collectable.", job: job);
                }

                ItemInstanceOperationResult moved = itemRuntime.SetInventoryLocation(itemInstanceId, destinationInventoryOwnerId);
                if (!moved.Succeeded)
                {
                    itemRuntime.RestoreFromSaveData(itemRollback, registry);
                    return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.CollectionFailed, moved.Message, job: job);
                }
            }

            job.outputCollectionState = ProductionOutputCollectionState.Collected;
            job.state = ProductionJobState.Completed;
            job.completionWorldTime = string.IsNullOrWhiteSpace(job.completionWorldTime) ? worldTime ?? string.Empty : job.completionWorldTime;
            job.revision++;
            if (workOrdersById.TryGetValue(job.workOrderId, out ProductionWorkOrderData workOrder))
            {
                workOrder.outputItemIds = Append(workOrder.outputItemIds, job.outputItemIds);
                workOrder.state = ProductionWorkOrderState.Completed;
                workOrder.revision++;
            }

            Touch($"outputs-collected.{job.jobId}.{job.revision}", "OutputsCollected", job.jobId, string.Empty, job.workOrderId, job.batchId, string.Empty, worldTime, $"Outputs collected to inventory '{destinationInventoryOwnerId}'.");
            return ProductionWorkflowResult.Success("Production outputs collected.", job: job);
        }

        public ProductionWorkflowResult CreateLot(ProductionLotData request)
        {
            ProductionLotData lot = (request ?? new ProductionLotData()).Clone();
            if (string.IsNullOrWhiteSpace(lot.lotId))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Lot ID is required.");
            }

            if (lot.quantity <= 0f)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Lot quantity must be positive.");
            }

            if (lot.unit == ProductionQuantityUnit.Count)
            {
                lot.discreteQuantity = lot.WholeQuantity;
                lot.quantity = lot.discreteQuantity;
            }

            if (lotsById.ContainsKey(lot.lotId))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, $"Lot '{lot.lotId}' already exists.");
            }

            lotsById.Add(lot.lotId, lot);
            Touch($"lot-created.{lot.lotId}", "LotCreated", string.Empty, string.Empty, string.Empty, lot.batchSourceId, lot.lotId, string.Empty, "Production lot created.");
            return ProductionWorkflowResult.Success("Production lot created.", lot: lot);
        }

        public ProductionWorkflowResult AddLotQuantity(string lotId, float quantity, string eventId)
        {
            if (!lotsById.TryGetValue(lotId ?? string.Empty, out ProductionLotData lot))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, $"Lot '{lotId}' was not found.");
            }

            if (quantity <= 0f || lot.unit == ProductionQuantityUnit.Count && Math.Abs(quantity - MathF.Round(quantity)) > 0.0001f)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Added lot quantity must be positive and use whole units for count-based lots.");
            }

            string resolvedEventId = string.IsNullOrWhiteSpace(eventId) ? $"lot-quantity-added.{lotId}.{lot.revision + 1}" : eventId.Trim();
            if (events.Any(entry => string.Equals(entry.eventId, resolvedEventId, StringComparison.Ordinal)))
            {
                return ProductionWorkflowResult.Success("Lot quantity addition already applied.", lot: lot, duplicate: true);
            }

            float updatedQuantity = lot.quantity + quantity;
            if (float.IsNaN(updatedQuantity) || float.IsInfinity(updatedQuantity))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Added lot quantity would exceed the supported range.");
            }

            if (lot.unit == ProductionQuantityUnit.Count)
            {
                lot.discreteQuantity = checked(lot.WholeQuantity + (long)MathF.Round(quantity));
                lot.quantity = lot.discreteQuantity;
            }
            else
            {
                lot.quantity = updatedQuantity;
            }
            lot.state = ProductionLotState.Active;
            lot.revision++;
            Touch(resolvedEventId, "LotQuantityAdded", string.Empty, string.Empty, string.Empty, lot.batchSourceId, lot.lotId, string.Empty, $"Added {quantity:0.###} units to production lot.");
            return ProductionWorkflowResult.Success("Production lot quantity increased.", lot: lot);
        }

        public ProductionWorkflowResult ConsumeLotQuantity(string lotId, int quantity, string eventId)
        {
            if (!lotsById.TryGetValue(lotId ?? string.Empty, out ProductionLotData lot))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, $"Lot '{lotId}' was not found.");
            }

            if (quantity <= 0 || lot.unit != ProductionQuantityUnit.Count || lot.state != ProductionLotState.Active || lot.WholeQuantity < quantity)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "The active production lot does not contain the requested whole-unit quantity.");
            }

            string resolvedEventId = string.IsNullOrWhiteSpace(eventId) ? $"lot-quantity-consumed.{lotId}.{lot.nextMaterializationIndex}" : eventId.Trim();
            if (events.Any(entry => string.Equals(entry.eventId, resolvedEventId, StringComparison.Ordinal)))
            {
                return ProductionWorkflowResult.Success("Lot quantity consumption already applied.", lot: lot, duplicate: true);
            }

            lot.discreteQuantity = checked(lot.WholeQuantity - quantity);
            lot.quantity = lot.discreteQuantity;
            lot.nextMaterializationIndex = checked(lot.nextMaterializationIndex + quantity);
            lot.state = lot.quantity <= 0.0001f ? ProductionLotState.Consumed : ProductionLotState.Active;
            lot.revision++;
            Touch(resolvedEventId, "LotQuantityConsumed", string.Empty, string.Empty, string.Empty, lot.batchSourceId, lot.lotId, string.Empty, $"Consumed {quantity} units from production lot.");
            return ProductionWorkflowResult.Success("Production lot quantity consumed.", lot: lot);
        }

        public ProductionWorkflowResult SplitLot(string sourceLotId, string childLotId, float quantity)
        {
            if (!lotsById.TryGetValue(sourceLotId ?? string.Empty, out ProductionLotData source))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, $"Lot '{sourceLotId}' was not found.");
            }

            if (quantity <= 0f || quantity >= source.quantity)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Lot split quantity must be positive and less than the source quantity.");
            }

            if (lotsById.ContainsKey(childLotId ?? string.Empty))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, $"Lot '{childLotId}' already exists.");
            }

            ProductionLotData child = source.Clone();
            child.lotId = childLotId;
            child.quantity = quantity;
            child.discreteQuantity = child.unit == ProductionQuantityUnit.Count ? (long)MathF.Round(quantity) : -1L;
            child.parentLotIds = Append(child.parentLotIds, source.lotId);
            child.childLotIds = Array.Empty<string>();
            child.revision = 1L;
            source.quantity -= quantity;
            if (source.unit == ProductionQuantityUnit.Count)
            {
                source.discreteQuantity = checked(source.WholeQuantity - (long)MathF.Round(quantity));
                source.quantity = source.discreteQuantity;
            }
            source.childLotIds = Append(source.childLotIds, child.lotId);
            source.revision++;
            lotsById.Add(child.lotId, child);
            Touch($"lot-split.{source.lotId}.{child.lotId}", "LotSplit", string.Empty, string.Empty, string.Empty, source.batchSourceId, source.lotId, string.Empty, "Production lot split.");
            return ProductionWorkflowResult.Success("Production lot split.", lot: child);
        }

        public ProductionWorkflowResult MergeLots(string mergedLotId, IReadOnlyList<string> sourceLotIds)
        {
            string[] ids = ProductionStageDefinitionData.NormalizeIds(sourceLotIds);
            if (ids.Length < 2)
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "At least two lots are required for merge.");
            }

            if (lotsById.ContainsKey(mergedLotId ?? string.Empty))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, $"Merged lot '{mergedLotId}' already exists.");
            }

            List<ProductionLotData> sources = new List<ProductionLotData>();
            foreach (string id in ids)
            {
                if (!lotsById.TryGetValue(id, out ProductionLotData source))
                {
                    return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, $"Lot '{id}' was not found.");
                }

                sources.Add(source);
            }

            string definition = sources[0].definitionOrMaterialId;
            ProductionQuantityUnit unit = sources[0].unit;
            if (sources.Any(source => !string.Equals(source.definitionOrMaterialId, definition, StringComparison.Ordinal) || source.unit != unit))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.InvalidRequest, "Lots are incompatible for merge.");
            }

            ProductionLotData merged = new ProductionLotData
            {
                lotId = mergedLotId,
                definitionOrMaterialId = definition,
                ownerId = sources[0].ownerId,
                custodianId = sources[0].custodianId,
                sourceItemIds = ProductionStageDefinitionData.NormalizeIds(sources.SelectMany(source => source.sourceItemIds)),
                containedItemIds = ProductionStageDefinitionData.NormalizeIds(sources.SelectMany(source => source.containedItemIds)),
                quantity = sources[0].unit == ProductionQuantityUnit.Count ? sources.Sum(source => source.WholeQuantity) : sources.Sum(source => source.quantity),
                discreteQuantity = sources[0].unit == ProductionQuantityUnit.Count ? sources.Sum(source => source.WholeQuantity) : -1L,
                unit = unit,
                batchSourceId = sources[0].batchSourceId,
                parentLotIds = ids,
                provenance = string.Join("|", sources.Select(source => source.provenance).Where(value => !string.IsNullOrWhiteSpace(value)).OrderBy(value => value, StringComparer.Ordinal))
            };

            foreach (ProductionLotData source in sources)
            {
                source.state = ProductionLotState.Merged;
                source.childLotIds = Append(source.childLotIds, merged.lotId);
                source.revision++;
            }

            lotsById.Add(merged.lotId, merged);
            Touch($"lot-merged.{merged.lotId}", "LotMerged", string.Empty, string.Empty, string.Empty, merged.batchSourceId, merged.lotId, string.Empty, "Production lots merged.");
            return ProductionWorkflowResult.Success("Production lots merged.", lot: merged);
        }

        public ProductionProjectionData ProjectJob(string jobId, ProductionProjectionAudience audience)
        {
            if (!jobsById.TryGetValue(jobId ?? string.Empty, out ProductionJobData job))
            {
                return new ProductionProjectionData(jobId, ProductionProjectionDecision.Denied, string.Empty, Array.Empty<string>(), new[] { "job" });
            }

            if (audience == ProductionProjectionAudience.PrivilegedDebug || audience == ProductionProjectionAudience.InternalAuthority || audience == ProductionProjectionAudience.JobOwner)
            {
                return new ProductionProjectionData(job.jobId, ProductionProjectionDecision.FullAccess, job.jobId, new[] { "state", "stages", "batches", "lots", "outputs", "provenance" }, Array.Empty<string>());
            }

            return new ProductionProjectionData(job.jobId, ProductionProjectionDecision.RedactedAccess, job.jobId, new[] { "state", "collection" }, new[] { "stages", "reservationIds", "workerAssignmentIds", "hiddenMaterials", "provenance" });
        }

        public ProductionWorkflowRuntimeSaveData CreateSaveData()
        {
            return new ProductionWorkflowRuntimeSaveData
            {
                schemaVersion = ProductionWorkflowRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                nextEventSequence = nextEventSequence,
                workOrders = workOrdersById.Values.OrderBy(entry => entry.workOrderId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                jobs = jobsById.Values.OrderBy(entry => entry.jobId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                queues = queuesById.Values.OrderBy(entry => entry.queueId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                batches = batchesById.Values.OrderBy(entry => entry.batchId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                lots = lotsById.Values.OrderBy(entry => entry.lotId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                intermediates = intermediatesById.Values.OrderBy(entry => entry.intermediateId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                occupancies = occupanciesById.Values.OrderBy(entry => entry.occupancyId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                assignments = assignmentsById.Values.OrderBy(entry => entry.assignmentId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                events = events.OrderBy(entry => entry.sequence).ThenBy(entry => entry.eventId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList()
            };
        }

        public ProductionWorkflowResult RestoreFromSaveData(ProductionWorkflowRuntimeSaveData saveData, DefinitionRegistry registry = null)
        {
            if (!ValidateSaveData(saveData, registry, out string failure))
            {
                return ProductionWorkflowResult.Failure(ProductionWorkflowStatus.RestoreFailed, failure);
            }

            workOrdersById.Clear();
            jobsById.Clear();
            queuesById.Clear();
            batchesById.Clear();
            lotsById.Clear();
            intermediatesById.Clear();
            occupanciesById.Clear();
            assignmentsById.Clear();
            events.Clear();

            foreach (ProductionWorkOrderData workOrder in saveData.workOrders.Select(entry => entry.Clone()).OrderBy(entry => entry.workOrderId, StringComparer.Ordinal))
            {
                workOrdersById[workOrder.workOrderId] = workOrder;
            }

            foreach (ProductionJobData job in saveData.jobs.Select(entry => entry.Clone()).OrderBy(entry => entry.jobId, StringComparer.Ordinal))
            {
                jobsById[job.jobId] = job;
            }

            foreach (ProductionQueueData queue in saveData.queues.Select(entry => entry.Clone()).OrderBy(entry => entry.queueId, StringComparer.Ordinal))
            {
                queuesById[queue.queueId] = queue;
            }

            foreach (ProductionBatchData batch in saveData.batches.Select(entry => entry.Clone()).OrderBy(entry => entry.batchId, StringComparer.Ordinal))
            {
                batchesById[batch.batchId] = batch;
            }

            foreach (ProductionLotData lot in saveData.lots.Select(entry => entry.Clone()).OrderBy(entry => entry.lotId, StringComparer.Ordinal))
            {
                lotsById[lot.lotId] = lot;
            }

            foreach (ProductionIntermediateData intermediate in saveData.intermediates.Select(entry => entry.Clone()).OrderBy(entry => entry.intermediateId, StringComparer.Ordinal))
            {
                intermediatesById[intermediate.intermediateId] = intermediate;
            }

            foreach (ProductionStationOccupancyData occupancy in saveData.occupancies.Select(entry => entry.Clone()).OrderBy(entry => entry.occupancyId, StringComparer.Ordinal))
            {
                occupanciesById[occupancy.occupancyId] = occupancy;
            }

            foreach (ProductionWorkerAssignmentData assignment in saveData.assignments.Select(entry => entry.Clone()).OrderBy(entry => entry.assignmentId, StringComparer.Ordinal))
            {
                assignmentsById[assignment.assignmentId] = assignment;
            }

            events.AddRange(saveData.events.Select(entry => entry.Clone()).OrderBy(entry => entry.sequence).ThenBy(entry => entry.eventId, StringComparer.Ordinal));
            revision = Math.Max(0L, saveData.revision);
            nextEventSequence = Math.Max(0L, saveData.nextEventSequence);
            return ProductionWorkflowResult.Success("Production workflow runtime restored.");
        }

        public static bool ValidateSaveData(ProductionWorkflowRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Production workflow save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != ProductionWorkflowRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported production workflow schema version {saveData.schemaVersion}.";
                return false;
            }

            if (saveData.revision < 0L || saveData.nextEventSequence < 0L)
            {
                failure = "Production workflow revisions cannot be negative.";
                return false;
            }

            if (!Unique(saveData.workOrders, entry => entry?.workOrderId, "work order", out failure)
                || !Unique(saveData.jobs, entry => entry?.jobId, "job", out failure)
                || !Unique(saveData.queues, entry => entry?.queueId, "queue", out failure)
                || !Unique(saveData.batches, entry => entry?.batchId, "batch", out failure)
                || !Unique(saveData.lots, entry => entry?.lotId, "lot", out failure)
                || !Unique(saveData.intermediates, entry => entry?.intermediateId, "intermediate", out failure)
                || !Unique(saveData.occupancies, entry => entry?.occupancyId, "occupancy", out failure)
                || !Unique(saveData.assignments, entry => entry?.assignmentId, "assignment", out failure))
            {
                return false;
            }

            HashSet<string> workOrders = new HashSet<string>((saveData.workOrders ?? new List<ProductionWorkOrderData>()).Select(entry => entry.workOrderId), StringComparer.Ordinal);
            HashSet<string> jobs = new HashSet<string>((saveData.jobs ?? new List<ProductionJobData>()).Select(entry => entry.jobId), StringComparer.Ordinal);
            HashSet<string> batches = new HashSet<string>((saveData.batches ?? new List<ProductionBatchData>()).Select(entry => entry.batchId), StringComparer.Ordinal);
            HashSet<string> lots = new HashSet<string>((saveData.lots ?? new List<ProductionLotData>()).Select(entry => entry.lotId), StringComparer.Ordinal);
            HashSet<string> intermediates = new HashSet<string>((saveData.intermediates ?? new List<ProductionIntermediateData>()).Select(entry => entry.intermediateId), StringComparer.Ordinal);
            HashSet<string> occupancies = new HashSet<string>((saveData.occupancies ?? new List<ProductionStationOccupancyData>()).Select(entry => entry.occupancyId), StringComparer.Ordinal);
            HashSet<string> assignments = new HashSet<string>((saveData.assignments ?? new List<ProductionWorkerAssignmentData>()).Select(entry => entry.assignmentId), StringComparer.Ordinal);

            foreach (ProductionLotData lot in saveData.lots ?? new List<ProductionLotData>())
            {
                if (float.IsNaN(lot.quantity) || float.IsInfinity(lot.quantity)
                    || double.IsNaN(lot.createdWorldTime) || double.IsInfinity(lot.createdWorldTime)
                    || float.IsNaN(lot.baseQuality) || float.IsInfinity(lot.baseQuality)
                    || float.IsNaN(lot.qualityVariation) || float.IsInfinity(lot.qualityVariation)
                    || lot.quantity < 0f || lot.createdWorldTime < 0d || lot.nextMaterializationIndex < 0L
                    || lot.baseQuality is < 0f or > 1f || lot.qualityVariation is < 0f or > 1f)
                {
                    failure = $"Production lot '{lot.lotId}' contains invalid quantity, materialization, or quality values.";
                    return false;
                }

                if (lot.unit == ProductionQuantityUnit.Count && Math.Abs(lot.quantity - MathF.Round(lot.quantity)) > 0.0001f)
                {
                    failure = $"Count-based production lot '{lot.lotId}' must contain a whole-unit quantity.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(lot.recipeDefinitionId) && registry != null)
                {
                    if (!registry.TryGet(lot.recipeDefinitionId, out RecipeDefinition recipe))
                    {
                        failure = $"Production lot '{lot.lotId}' references missing recipe '{lot.recipeDefinitionId}'.";
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(lot.recipeVersionId)
                        && !recipe.Versions.Any(version => string.Equals(version.versionId, lot.recipeVersionId, StringComparison.Ordinal)))
                    {
                        failure = $"Production lot '{lot.lotId}' references missing recipe version '{lot.recipeVersionId}'.";
                        return false;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(lot.recipeVersionId))
                {
                    failure = $"Production lot '{lot.lotId}' references a recipe version without a recipe definition.";
                    return false;
                }

                foreach (ProductionLotMaterialAssignmentData material in lot.materialAssignments ?? Array.Empty<ProductionLotMaterialAssignmentData>())
                {
                    if (material == null || string.IsNullOrWhiteSpace(material.recipeInputId)
                        || string.IsNullOrWhiteSpace(material.materialDefinitionId)
                        || float.IsNaN(material.quantity) || float.IsInfinity(material.quantity) || material.quantity <= 0f)
                    {
                        failure = $"Production lot '{lot.lotId}' contains an invalid material assignment.";
                        return false;
                    }

                    if (registry != null && !registry.TryGet(material.materialDefinitionId, out MaterialDefinition _))
                    {
                        failure = $"Production lot '{lot.lotId}' references missing material '{material.materialDefinitionId}'.";
                        return false;
                    }
                }
            }

            foreach (ProductionJobData job in saveData.jobs ?? new List<ProductionJobData>())
            {
                if (!string.IsNullOrWhiteSpace(job.workOrderId) && !workOrders.Contains(job.workOrderId))
                {
                    failure = $"Production job '{job.jobId}' references missing work order '{job.workOrderId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(job.batchId) && !batches.Contains(job.batchId))
                {
                    failure = $"Production job '{job.jobId}' references missing batch '{job.batchId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(job.chainDefinitionId) && registry != null && !registry.TryGet(job.chainDefinitionId, out ProductionChainDefinition chain))
                {
                    failure = $"Production job '{job.jobId}' references missing production chain '{job.chainDefinitionId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(job.chainDefinitionId) && registry != null && registry.TryGet(job.chainDefinitionId, out ProductionChainDefinition foundChain) && !foundChain.TryGetVersion(job.versionId, out _))
                {
                    failure = $"Production job '{job.jobId}' references missing production chain version '{job.versionId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(job.recipeDefinitionId) && registry != null && !registry.TryGet(job.recipeDefinitionId, out RecipeDefinition _))
                {
                    failure = $"Production job '{job.jobId}' references missing recipe '{job.recipeDefinitionId}'.";
                    return false;
                }

                foreach (string id in job.intermediateIds ?? Array.Empty<string>())
                {
                    if (!intermediates.Contains(id))
                    {
                        failure = $"Production job '{job.jobId}' references missing intermediate '{id}'.";
                        return false;
                    }
                }

                foreach (string id in job.outputLotIds ?? Array.Empty<string>())
                {
                    if (!lots.Contains(id))
                    {
                        failure = $"Production job '{job.jobId}' references missing lot '{id}'.";
                        return false;
                    }
                }

                foreach (string id in job.occupancyIds ?? Array.Empty<string>())
                {
                    if (!occupancies.Contains(id))
                    {
                        failure = $"Production job '{job.jobId}' references missing occupancy '{id}'.";
                        return false;
                    }
                }

                foreach (string id in job.assignmentIds ?? Array.Empty<string>())
                {
                    if (!assignments.Contains(id))
                    {
                        failure = $"Production job '{job.jobId}' references missing assignment '{id}'.";
                        return false;
                    }
                }

                if (job.stages.Any(stage => stage.completedWork < 0f || stage.requiredWork < 0f || stage.completedWork > stage.requiredWork + 0.0001f))
                {
                    failure = $"Production job '{job.jobId}' has invalid stage progress.";
                    return false;
                }
            }

            foreach (ProductionQueueData queue in saveData.queues ?? new List<ProductionQueueData>())
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string jobId in queue.jobIds ?? new List<string>())
                {
                    if (!seen.Add(jobId))
                    {
                        failure = $"Production queue '{queue.queueId}' contains duplicate job '{jobId}'.";
                        return false;
                    }

                    if (!jobs.Contains(jobId))
                    {
                        failure = $"Production queue '{queue.queueId}' references missing job '{jobId}'.";
                        return false;
                    }
                }
            }

            if (HasLotCycle(saveData.lots ?? new List<ProductionLotData>(), out string lotCycle))
            {
                failure = $"Production lot lineage contains a cycle at '{lotCycle}'.";
                return false;
            }

            return true;
        }

        private static List<ProductionStageProgressData> BuildStageProgress(ProductionJobData job, ProductionChainVersionData version)
        {
            if (version == null)
            {
                return new List<ProductionStageProgressData>
                {
                    new ProductionStageProgressData
                    {
                        stageId = "stage.single-recipe",
                        recipeDefinitionId = job.recipeDefinitionId,
                        recipeVersionId = job.versionId,
                        recipeVariantId = job.variantId,
                        state = ProductionStageRuntimeState.Ready,
                        requiredWork = 1f,
                        deterministicSeed = StableId("production-stage-seed", job.jobId, "stage.single-recipe")
                    }
                };
            }

            return version.stages
                .Select(stage => stage.Clone())
                .OrderBy(stage => stage.priority)
                .ThenBy(stage => stage.stageId, StringComparer.Ordinal)
                .Select(stage => new ProductionStageProgressData
                {
                    stageId = stage.stageId,
                    recipeDefinitionId = stage.recipeDefinitionId,
                    recipeVersionId = string.IsNullOrWhiteSpace(stage.recipeVersionId) ? job.versionId : stage.recipeVersionId,
                    recipeVariantId = stage.recipeVariantId,
                    dependencyStageIds = stage.dependencyStageIds,
                    state = (stage.dependencyStageIds == null || stage.dependencyStageIds.Length == 0) ? ProductionStageRuntimeState.Ready : ProductionStageRuntimeState.Pending,
                    requiredWork = stage.progressModel == ProductionProgressModel.InstantWhenReady ? 0f : Math.Max(1f, stage.requiredWorkUnits > 0f ? stage.requiredWorkUnits : stage.estimatedDuration),
                    deterministicSeed = StableId("production-stage-seed", job.jobId, stage.stageId)
                })
                .ToList();
        }

        private IEnumerable<string> OrderQueue(ProductionQueueData queue)
        {
            IEnumerable<string> jobs = queue.jobIds.Where(id => jobsById.ContainsKey(id));
            return queue.policy switch
            {
                ProductionQueuePolicy.StrictPriority => jobs.OrderByDescending(id => WorkOrderPriority(jobsById[id])).ThenBy(id => id, StringComparer.Ordinal),
                ProductionQueuePolicy.FirstInFirstOut => jobs,
                ProductionQueuePolicy.ManualOrder => jobs,
                _ => jobs.OrderByDescending(id => WorkOrderPriority(jobsById[id])).ThenBy(id => id, StringComparer.Ordinal)
            };
        }

        private static bool CanTransition(ProductionWorkOrderState current, ProductionWorkOrderState target)
        {
            if (current == target)
            {
                return true;
            }

            if (current == ProductionWorkOrderState.Cancelled || current == ProductionWorkOrderState.Completed || current == ProductionWorkOrderState.Archived)
            {
                return false;
            }

            return target != ProductionWorkOrderState.Draft;
        }

        private int WorkOrderPriority(ProductionJobData job)
        {
            if (job == null)
            {
                return 0;
            }

            if (job.priority != 0)
            {
                return job.priority;
            }

            return workOrdersById.TryGetValue(job.workOrderId, out ProductionWorkOrderData workOrder) ? workOrder.priority : 0;
        }

        private static ProductionStageProgressData FindStage(ProductionJobData job, string stageId)
        {
            string resolved = string.IsNullOrWhiteSpace(stageId) ? job?.currentStageId : stageId;
            return job?.stages?.FirstOrDefault(stage => string.Equals(stage.stageId, resolved, StringComparison.Ordinal));
        }

        private static IReadOnlyList<string> ReadyStageIds(ProductionJobData job)
        {
            HashSet<string> completed = new HashSet<string>(job.completedStageIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            return (job.stages ?? new List<ProductionStageProgressData>())
                .Where(stage => stage.state == ProductionStageRuntimeState.Ready || stage.state == ProductionStageRuntimeState.Pending)
                .Where(stage => (stage.dependencyStageIds ?? Array.Empty<string>()).All(completed.Contains))
                .OrderBy(stage => stage.stageId, StringComparer.Ordinal)
                .Select(stage =>
                {
                    stage.state = ProductionStageRuntimeState.Ready;
                    return stage.stageId;
                })
                .ToArray();
        }

        private bool IsFinalStage(ProductionJobData job, string stageId)
        {
            return !job.stages.Any(stage => (stage.dependencyStageIds ?? Array.Empty<string>()).Contains(stageId, StringComparer.Ordinal) && stage.state != ProductionStageRuntimeState.Cancelled);
        }

        private bool CanClaimStation(string stationInstanceId, int capacityUnits, out string failure)
        {
            failure = string.Empty;
            int active = occupanciesById.Values
                .Where(occupancy => occupancy.state == ProductionOccupancyState.Active && string.Equals(occupancy.stationInstanceId, stationInstanceId, StringComparison.Ordinal))
                .Sum(occupancy => Math.Max(1, occupancy.capacityUnits));
            if (active + Math.Max(1, capacityUnits) > 1)
            {
                failure = $"Station '{stationInstanceId}' has no available capacity.";
                return false;
            }

            return true;
        }

        private void ReleaseOccupancy(ProductionStageProgressData stage, string worldTime, ProductionOccupancyState state = ProductionOccupancyState.Released)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.occupancyId) || !occupanciesById.TryGetValue(stage.occupancyId, out ProductionStationOccupancyData occupancy))
            {
                return;
            }

            if (occupancy.state != ProductionOccupancyState.Active)
            {
                return;
            }

            occupancy.state = state;
            occupancy.actualReleaseWorldTime = worldTime ?? string.Empty;
            occupancy.revision++;
        }

        private static bool IsTerminal(ProductionJobState state)
        {
            return state == ProductionJobState.Completed
                || state == ProductionJobState.Cancelled
                || state == ProductionJobState.Failed
                || state == ProductionJobState.Invalid
                || state == ProductionJobState.RolledBack;
        }

        private static bool TryParseWorldTime(string worldTime, out double value)
        {
            if (string.IsNullOrWhiteSpace(worldTime))
            {
                value = 0d;
                return true;
            }

            return double.TryParse(worldTime, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private void Touch(string eventId, string kind, string jobId, string stageId, string workOrderId, string batchId, string lotId, string worldTime, string message)
        {
            revision++;
            events.Add(new ProductionWorkflowEventData
            {
                eventId = string.IsNullOrWhiteSpace(eventId) ? StableId("production-event", kind, jobId, stageId, nextEventSequence.ToString(CultureInfo.InvariantCulture)) : eventId,
                eventKind = kind ?? string.Empty,
                jobId = jobId ?? string.Empty,
                stageId = stageId ?? string.Empty,
                workOrderId = workOrderId ?? string.Empty,
                batchId = batchId ?? string.Empty,
                lotId = lotId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                message = message ?? string.Empty,
                sequence = nextEventSequence++
            });
        }

        private static string[] Append(IEnumerable<string> current, params string[] additions)
        {
            return ProductionStageDefinitionData.NormalizeIds((current ?? Array.Empty<string>()).Concat(additions ?? Array.Empty<string>()));
        }

        private static string[] Append(IEnumerable<string> current, IEnumerable<string> additions)
        {
            return ProductionStageDefinitionData.NormalizeIds((current ?? Array.Empty<string>()).Concat(additions ?? Array.Empty<string>()));
        }

        private static bool Unique<T>(IEnumerable<T> values, Func<T, string> id, string kind, out string failure)
        {
            failure = string.Empty;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (T value in values ?? Array.Empty<T>())
            {
                string key = id(value);
                if (string.IsNullOrWhiteSpace(key))
                {
                    failure = $"Production workflow contains a {kind} without an ID.";
                    return false;
                }

                if (!seen.Add(key))
                {
                    failure = $"Production workflow contains duplicate {kind} ID '{key}'.";
                    return false;
                }
            }

            return true;
        }

        private static bool HasLotCycle(IReadOnlyList<ProductionLotData> lots, out string cycle)
        {
            cycle = string.Empty;
            Dictionary<string, ProductionLotData> byId = (lots ?? Array.Empty<ProductionLotData>()).Where(lot => lot != null && !string.IsNullOrWhiteSpace(lot.lotId)).ToDictionary(lot => lot.lotId, StringComparer.Ordinal);
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in byId.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (VisitLot(id, byId, visiting, visited, out cycle))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool VisitLot(string id, IReadOnlyDictionary<string, ProductionLotData> byId, HashSet<string> visiting, HashSet<string> visited, out string cycle)
        {
            cycle = string.Empty;
            if (visited.Contains(id))
            {
                return false;
            }

            if (!visiting.Add(id))
            {
                cycle = id;
                return true;
            }

            if (byId.TryGetValue(id, out ProductionLotData lot))
            {
                foreach (string child in lot.childLotIds ?? Array.Empty<string>())
                {
                    if (byId.ContainsKey(child) && VisitLot(child, byId, visiting, visited, out cycle))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(id);
            visited.Add(id);
            return false;
        }

        internal static string StableId(string prefix, params string[] parts)
        {
            string joined = string.Join("|", parts ?? Array.Empty<string>());
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(joined));
                string suffix = BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
                string normalizedPrefix = string.IsNullOrWhiteSpace(prefix) ? "production" : prefix.Trim();
                return $"{normalizedPrefix}.{suffix}";
            }
        }
    }
}
