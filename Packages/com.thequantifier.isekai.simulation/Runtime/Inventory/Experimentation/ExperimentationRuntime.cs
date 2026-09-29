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
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Experimentation
{
    public sealed class ExperimentationRuntime
    {
        private readonly Dictionary<string, ExperimentHypothesisData> hypothesesById = new Dictionary<string, ExperimentHypothesisData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ExperimentPlanData> plansById = new Dictionary<string, ExperimentPlanData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ExperimentRunData> runsById = new Dictionary<string, ExperimentRunData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ExperimentTrialData> trialsById = new Dictionary<string, ExperimentTrialData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ExperimentMeasurementData> measurementsById = new Dictionary<string, ExperimentMeasurementData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ExperimentInferenceData> inferencesById = new Dictionary<string, ExperimentInferenceData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiscoveryClaimData> claimsById = new Dictionary<string, DiscoveryClaimData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiscoveryReviewData> reviewsById = new Dictionary<string, DiscoveryReviewData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RecipeRegistrationProposalData> proposalsById = new Dictionary<string, RecipeRegistrationProposalData>(StringComparer.Ordinal);
        private readonly List<ExperimentLogRecordData> logs = new List<ExperimentLogRecordData>();
        private long revision;
        private long nextLogSequence;

        public long Revision => revision;
        public int HypothesisCount => hypothesesById.Count;
        public int PlanCount => plansById.Count;
        public int RunCount => runsById.Count;
        public int TrialCount => trialsById.Count;
        public int MeasurementCount => measurementsById.Count;
        public int InferenceCount => inferencesById.Count;
        public int ClaimCount => claimsById.Count;
        public int ReviewCount => reviewsById.Count;
        public int RegistrationProposalCount => proposalsById.Count;
        public IReadOnlyList<ExperimentHypothesisData> Hypotheses => hypothesesById.Values.OrderBy(entry => entry.hypothesisId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ExperimentPlanData> Plans => plansById.Values.OrderBy(entry => entry.planId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ExperimentRunData> Runs => runsById.Values.OrderBy(entry => entry.experimentRunId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ExperimentTrialData> Trials => trialsById.Values.OrderBy(entry => entry.trialId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ExperimentMeasurementData> Measurements => measurementsById.Values.OrderBy(entry => entry.measurementId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ExperimentInferenceData> Inferences => inferencesById.Values.OrderBy(entry => entry.inferenceId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<DiscoveryClaimData> Claims => claimsById.Values.OrderBy(entry => entry.claimId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<DiscoveryReviewData> Reviews => reviewsById.Values.OrderBy(entry => entry.reviewId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<RecipeRegistrationProposalData> RegistrationProposals => proposalsById.Values.OrderBy(entry => entry.proposalId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ExperimentLogRecordData> Logs => logs.OrderBy(entry => entry.sequence).ThenBy(entry => entry.logId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();

        public bool TryGetHypothesis(string hypothesisId, out ExperimentHypothesisData hypothesis) => TryGet(hypothesesById, hypothesisId, out hypothesis);
        public bool TryGetPlan(string planId, out ExperimentPlanData plan) => TryGet(plansById, planId, out plan);
        public bool TryGetRun(string runId, out ExperimentRunData run) => TryGet(runsById, runId, out run);
        public bool TryGetTrial(string trialId, out ExperimentTrialData trial) => TryGet(trialsById, trialId, out trial);
        public bool TryGetMeasurement(string measurementId, out ExperimentMeasurementData measurement) => TryGet(measurementsById, measurementId, out measurement);
        public bool TryGetInference(string inferenceId, out ExperimentInferenceData inference) => TryGet(inferencesById, inferenceId, out inference);
        public bool TryGetClaim(string claimId, out DiscoveryClaimData claim) => TryGet(claimsById, claimId, out claim);

        public ExperimentationResult CreateHypothesis(ExperimentHypothesisData request, bool preview = false)
        {
            ExperimentHypothesisData hypothesis = (request ?? new ExperimentHypothesisData()).Clone();
            if (string.IsNullOrWhiteSpace(hypothesis.hypothesisId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, "Hypothesis ID is required.");
            }

            if (hypothesis.claim == null || hypothesis.claim.claimType == HypothesisClaimType.Unknown || string.IsNullOrWhiteSpace(hypothesis.claim.Signature))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, "Hypothesis must declare a concrete claim.");
            }

            if (hypothesesById.TryGetValue(hypothesis.hypothesisId, out ExperimentHypothesisData existing))
            {
                return ExperimentationResult.Success("Hypothesis already exists.", duplicate: true).WithHypothesis(existing);
            }

            if (preview)
            {
                return ExperimentationResult.Success("Hypothesis preview prepared.", preview: true).WithHypothesis(hypothesis);
            }

            hypothesis.revision = 1L;
            hypothesesById.Add(hypothesis.hypothesisId, hypothesis.Clone());
            Touch("hypothesis-created", hypothesis.hypothesisId, string.Empty, "Hypothesis created.");
            return ExperimentationResult.Success("Hypothesis created.").WithHypothesis(hypothesis);
        }

        public ExperimentationResult CreatePlan(ExperimentPlanData request, DefinitionRegistry registry, bool preview = false)
        {
            ExperimentPlanData plan = (request ?? new ExperimentPlanData()).Clone();
            if (string.IsNullOrWhiteSpace(plan.planId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, "Experiment plan ID is required.");
            }

            if (string.IsNullOrWhiteSpace(plan.experimentDefinitionId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, "Experiment plan must reference an Experiment definition.");
            }

            if (registry != null && !registry.TryGet(plan.experimentDefinitionId, out ExperimentDefinition definition))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingDefinition, $"Experiment definition '{plan.experimentDefinitionId}' was not found.");
            }

            if (registry != null && registry.TryGet(plan.experimentDefinitionId, out ExperimentDefinition found))
            {
                string[] missingControls = found.RequiredControls.Select(control => control.controlId).Where(controlId => !plan.controls.Any(control => string.Equals(control.controlId, controlId, StringComparison.Ordinal))).ToArray();
                if (missingControls.Length > 0 && plan.mode == ExperimentPlanMode.Controlled)
                {
                    return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, $"Experiment plan '{plan.planId}' is missing required controls: {string.Join(", ", missingControls)}.");
                }

                foreach (string requirementId in found.ProductionRequirementIds)
                {
                    if (!plan.requirementIds.Contains(requirementId, StringComparer.Ordinal))
                    {
                        plan.requirementIds = Append(plan.requirementIds, requirementId);
                    }
                }
            }

            foreach (string hypothesisId in plan.hypothesisIds ?? Array.Empty<string>())
            {
                if (!hypothesesById.ContainsKey(hypothesisId))
                {
                    return ExperimentationResult.Failure(ExperimentOperationStatus.MissingHypothesis, $"Hypothesis '{hypothesisId}' was not found.");
                }
            }

            if (!string.IsNullOrWhiteSpace(plan.recipeDefinitionId) && registry != null && !registry.TryGet(plan.recipeDefinitionId, out RecipeDefinition _))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingDefinition, $"Recipe definition '{plan.recipeDefinitionId}' was not found.");
            }

            if (plansById.TryGetValue(plan.planId, out ExperimentPlanData existing))
            {
                return ExperimentationResult.Success("Experiment plan already exists.", duplicate: true).WithPlan(existing);
            }

            if (preview || plan.previewOnly)
            {
                return ExperimentationResult.Success("Experiment plan preview prepared.", preview: true).WithPlan(plan);
            }

            plan.revision = 1L;
            plansById.Add(plan.planId, plan.Clone());
            Touch("plan-created", plan.planId, string.Empty, "Experiment plan created.");
            return ExperimentationResult.Success("Experiment plan created.").WithPlan(plan);
        }

        public ExperimentationResult StartRun(string runId, string planId, string actingPersonId, string worldTime, DefinitionRegistry registry, bool preview = false)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, "Experiment run ID is required.");
            }

            if (!plansById.TryGetValue(planId ?? string.Empty, out ExperimentPlanData plan))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingPlan, $"Experiment plan '{planId}' was not found.");
            }

            if (registry != null && !registry.TryGet(plan.experimentDefinitionId, out ExperimentDefinition _))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingDefinition, $"Experiment definition '{plan.experimentDefinitionId}' was not found.");
            }

            if (runsById.TryGetValue(runId, out ExperimentRunData existing))
            {
                return ExperimentationResult.Success("Experiment run already exists.", duplicate: true).WithRun(existing);
            }

            ExperimentRunData run = new ExperimentRunData
            {
                experimentRunId = runId,
                experimentDefinitionId = plan.experimentDefinitionId,
                planId = plan.planId,
                actingPersonId = actingPersonId ?? string.Empty,
                targetRecipeId = plan.recipeDefinitionId,
                hypothesisIds = plan.hypothesisIds,
                inputItemIds = plan.inputItemInstanceIds,
                toolIds = plan.toolItemInstanceIds,
                stationIds = plan.stationInstanceIds,
                startWorldTime = worldTime ?? string.Empty,
                state = ExperimentRunState.Running,
                safetyState = plan.mode == ExperimentPlanMode.DestructiveTest ? ExperimentSafetyState.Caution : ExperimentSafetyState.Safe,
                provenance = $"plan={plan.planId}"
            };

            if (preview)
            {
                return ExperimentationResult.Success("Experiment run preview prepared.", preview: true).WithRun(run);
            }

            runsById.Add(run.experimentRunId, run.Clone());
            foreach (string hypothesisId in run.hypothesisIds)
            {
                if (hypothesesById.TryGetValue(hypothesisId, out ExperimentHypothesisData hypothesis) && hypothesis.status == HypothesisStatus.Proposed)
                {
                    hypothesis.status = HypothesisStatus.Testing;
                    hypothesis.revision++;
                }
            }

            Touch("run-started", run.experimentRunId, string.Empty, "Experiment run started.");
            return ExperimentationResult.Success("Experiment run started.").WithRun(run);
        }

        public ExperimentationResult TransitionRun(string runId, ExperimentRunState targetState, string worldTime = "")
        {
            if (!runsById.TryGetValue(runId ?? string.Empty, out ExperimentRunData run))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingRun, $"Experiment run '{runId}' was not found.");
            }

            if (run.state == targetState)
            {
                return ExperimentationResult.Success("Experiment run is already in the requested state.", duplicate: true).WithRun(run);
            }

            if (!CanTransition(run.state, targetState))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidState, $"Cannot transition experiment run '{runId}' from {run.state} to {targetState}.");
            }

            run.state = targetState;
            if (targetState == ExperimentRunState.Completed || targetState == ExperimentRunState.Failed || targetState == ExperimentRunState.Inconclusive || targetState == ExperimentRunState.Cancelled)
            {
                run.completionWorldTime = worldTime ?? string.Empty;
            }

            run.revision++;
            Touch("run-state", run.experimentRunId, string.Empty, $"Experiment run state changed to {targetState}.");
            return ExperimentationResult.Success("Experiment run state changed.").WithRun(run);
        }

        public ExperimentationResult RecordTrial(ExperimentTrialData request, bool preview = false)
        {
            ExperimentTrialData trial = (request ?? new ExperimentTrialData()).Clone();
            if (string.IsNullOrWhiteSpace(trial.trialId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, "Experiment trial ID is required.");
            }

            if (!runsById.TryGetValue(trial.experimentRunId ?? string.Empty, out ExperimentRunData run))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingRun, $"Experiment run '{trial.experimentRunId}' was not found.");
            }

            if (trialsById.TryGetValue(trial.trialId, out ExperimentTrialData existing))
            {
                return ExperimentationResult.Success("Experiment trial already exists.", duplicate: true).WithTrial(existing);
            }

            if (preview)
            {
                return ExperimentationResult.Success("Experiment trial preview prepared.", preview: true).WithTrial(trial);
            }

            trial.revision = 1L;
            trialsById.Add(trial.trialId, trial.Clone());
            run.trialIds = Append(run.trialIds, trial.trialId);
            run.outcomeIds = Append(run.outcomeIds, trial.trialId);
            run.revision++;
            Touch("trial-recorded", run.experimentRunId, trial.trialId, "Experiment trial recorded.");
            return ExperimentationResult.Success("Experiment trial recorded.").WithTrial(trial);
        }

        public ExperimentationResult ExecuteCraftingTrial(
            string runId,
            string trialId,
            CraftingExecutionRequest craftingRequest,
            DefinitionRegistry registry,
            RecipeRuntime recipeRuntime,
            ProductionRequirementRuntime productionRuntime,
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            ItemDurabilityRuntime durabilityRuntime,
            CraftingExecutionRuntime craftingRuntime,
            ExperimentTrialOutcome expectedOutcome = ExperimentTrialOutcome.ExpectedSuccess,
            bool preview = false)
        {
            if (craftingRuntime == null)
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingRuntime, "Crafting execution runtime is missing.");
            }

            if (!runsById.TryGetValue(runId ?? string.Empty, out ExperimentRunData run))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingRun, $"Experiment run '{runId}' was not found.");
            }

            if (trialsById.TryGetValue(trialId ?? string.Empty, out ExperimentTrialData existing))
            {
                return ExperimentationResult.Success("Experiment trial already exists.", duplicate: true).WithTrial(existing);
            }

            CraftingExecutionRequest request = (craftingRequest ?? new CraftingExecutionRequest()).Clone();
            request.operationId = string.IsNullOrWhiteSpace(request.operationId) ? StableId("crafting-operation.experiment", runId, trialId) : request.operationId;
            request.deterministicSeed = string.IsNullOrWhiteSpace(request.deterministicSeed) ? StableId("experiment-trial-seed", runId, trialId) : request.deterministicSeed;
            request.preview = preview;

            CraftingExecutionResult crafting = preview
                ? craftingRuntime.Preview(request, registry, recipeRuntime, productionRuntime, itemRuntime, durabilityRuntime)
                : craftingRuntime.Execute(request, registry, recipeRuntime, productionRuntime, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime);

            ExperimentTrialData trial = new ExperimentTrialData
            {
                trialId = trialId,
                experimentRunId = runId,
                trialIndex = run.trialIds.Length,
                trialKind = ExperimentTrialKind.Experimental,
                recipeDefinitionId = request.recipeId,
                recipeVersionId = request.versionId,
                recipeVariantId = request.variantId,
                craftingOperationId = request.operationId,
                deterministicSeed = request.deterministicSeed,
                startWorldTime = request.worldTime,
                completionWorldTime = request.worldTime,
                inputItemIds = request.productionContext?.itemQuantities?.Select(item => item.itemInstanceId).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray() ?? Array.Empty<string>(),
                toolIds = request.productionContext?.toolCandidates?.Select(item => item.itemInstanceId).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray() ?? Array.Empty<string>(),
                stationIds = request.productionContext?.environmentKeys ?? Array.Empty<string>(),
                outputItemIds = crafting?.Operation?.outputs?.Select(output => output.itemInstanceId).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray() ?? Array.Empty<string>(),
                failureCode = crafting != null && crafting.Succeeded ? string.Empty : crafting?.Status.ToString() ?? "MissingCraftingResult",
                outcome = ClassifyCraftingOutcome(crafting, expectedOutcome),
                provenance = $"crafting-operation={request.operationId}"
            };

            if (preview)
            {
                return ExperimentationResult.Success("Experiment crafting trial preview prepared.", preview: true).WithTrial(trial).WithCrafting(crafting);
            }

            ExperimentationResult record = RecordTrial(trial);
            return record.Succeeded ? record.WithCrafting(crafting) : record;
        }

        public ExperimentationResult AttachProductionJob(string runId, string jobId, ProductionWorkflowRuntime workflowRuntime)
        {
            if (!runsById.TryGetValue(runId ?? string.Empty, out ExperimentRunData run))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingRun, $"Experiment run '{runId}' was not found.");
            }

            if (workflowRuntime == null || !workflowRuntime.TryGetJob(jobId, out ProductionJobData job))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingRuntime, $"Production job '{jobId}' was not found.");
            }

            if (run.productionJobIds.Contains(job.jobId, StringComparer.Ordinal))
            {
                return ExperimentationResult.Success("Production job is already attached.", duplicate: true).WithRun(run);
            }

            run.productionJobIds = Append(run.productionJobIds, job.jobId);
            run.targetProductionJobId = string.IsNullOrWhiteSpace(run.targetProductionJobId) ? job.jobId : run.targetProductionJobId;
            run.targetBatchId = string.IsNullOrWhiteSpace(run.targetBatchId) ? job.batchId : run.targetBatchId;
            run.revision++;
            Touch("production-job-attached", run.experimentRunId, job.jobId, "Production job attached to experiment.");
            return ExperimentationResult.Success("Production job attached to experiment.").WithRun(run);
        }

        public ExperimentationResult RecordMeasurement(ExperimentMeasurementData request, bool preview = false)
        {
            ExperimentMeasurementData measurement = (request ?? new ExperimentMeasurementData()).Clone();
            if (string.IsNullOrWhiteSpace(measurement.measurementId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, "Experiment measurement ID is required.");
            }

            if (!runsById.TryGetValue(measurement.experimentRunId ?? string.Empty, out ExperimentRunData run))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingRun, $"Experiment run '{measurement.experimentRunId}' was not found.");
            }

            if (!string.IsNullOrWhiteSpace(measurement.trialId) && !trialsById.ContainsKey(measurement.trialId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingTrial, $"Experiment trial '{measurement.trialId}' was not found.");
            }

            if (measurementsById.TryGetValue(measurement.measurementId, out ExperimentMeasurementData existing))
            {
                return ExperimentationResult.Success("Experiment measurement already exists.", duplicate: true).WithMeasurement(existing);
            }

            if (preview)
            {
                return ExperimentationResult.Success("Experiment measurement preview prepared.", preview: true).WithMeasurement(measurement);
            }

            measurementsById.Add(measurement.measurementId, measurement.Clone());
            if (trialsById.TryGetValue(measurement.trialId ?? string.Empty, out ExperimentTrialData trial))
            {
                trial.measurementIds = Append(trial.measurementIds, measurement.measurementId);
                trial.revision++;
            }

            run.observationIds = Append(run.observationIds, measurement.measurementId);
            run.revision++;
            Touch("measurement-recorded", run.experimentRunId, measurement.trialId, "Experiment measurement recorded.");
            return ExperimentationResult.Success("Experiment measurement recorded.").WithMeasurement(measurement);
        }

        public ExperimentationResult GenerateEvidence(
            string runId,
            string trialId,
            string hypothesisId,
            KnowledgeObservationRequest request,
            PersonKnowledgeRuntime knowledgeRuntime,
            ExperimentEvidenceRole role,
            bool preview = false)
        {
            if (knowledgeRuntime == null)
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingRuntime, "Knowledge runtime is missing.");
            }

            if (!runsById.TryGetValue(runId ?? string.Empty, out ExperimentRunData run))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingRun, $"Experiment run '{runId}' was not found.");
            }

            ExperimentTrialData trial = null;
            if (!string.IsNullOrWhiteSpace(trialId) && !trialsById.TryGetValue(trialId, out trial))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingTrial, $"Experiment trial '{trialId}' was not found.");
            }

            ExperimentHypothesisData hypothesis = null;
            if (!string.IsNullOrWhiteSpace(hypothesisId) && !hypothesesById.TryGetValue(hypothesisId, out hypothesis))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingHypothesis, $"Hypothesis '{hypothesisId}' was not found.");
            }

            KnowledgeObservationRequest observation = request ?? new KnowledgeObservationRequest();
            observation.TransactionId = string.IsNullOrWhiteSpace(observation.TransactionId) ? StableId("knowledge.experiment", runId, trialId, hypothesisId, role.ToString()) : observation.TransactionId;
            observation.EvidenceId = string.IsNullOrWhiteSpace(observation.EvidenceId) ? StableId("evidence.experiment", runId, trialId, hypothesisId, role.ToString()) : observation.EvidenceId;
            observation.Tags = Append(observation.Tags, "experiment.evidence");
            observation.Tags = Append(observation.Tags, role == ExperimentEvidenceRole.Contradicting ? "experiment.evidence.contradicting" : "experiment.evidence.supporting");
            observation.Direction = role == ExperimentEvidenceRole.Contradicting ? KnowledgeEvidenceDirection.Opposes : KnowledgeEvidenceDirection.Supports;

            KnowledgeOperationResult knowledge = preview ? knowledgeRuntime.PreviewObservation(observation) : knowledgeRuntime.RecordObservation(observation);
            if (knowledge == null || !knowledge.Succeeded)
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.KnowledgeRejected, knowledge?.Message ?? "Knowledge observation was rejected.");
            }

            string evidenceId = knowledge.Evidence?.EvidenceId ?? observation.EvidenceId;
            if (preview)
            {
                return ExperimentationResult.Success("Experiment evidence preview prepared.", preview: true).WithRun(run);
            }

            run.evidenceIds = Append(run.evidenceIds, evidenceId);
            run.observationIds = Append(run.observationIds, observation.TransactionId);
            run.revision++;
            if (trial != null)
            {
                trial.evidenceIds = Append(trial.evidenceIds, evidenceId);
                trial.observationIds = Append(trial.observationIds, observation.TransactionId);
                trial.revision++;
            }

            if (hypothesis != null)
            {
                if (role == ExperimentEvidenceRole.Contradicting)
                {
                    hypothesis.contradictingEvidenceIds = Append(hypothesis.contradictingEvidenceIds, evidenceId);
                }
                else if (role != ExperimentEvidenceRole.Neutral)
                {
                    hypothesis.supportingEvidenceIds = Append(hypothesis.supportingEvidenceIds, evidenceId);
                }

                UpdateHypothesisStatus(hypothesis);
            }

            Touch("evidence-generated", run.experimentRunId, trialId, "Experiment evidence generated.");
            return ExperimentationResult.Success("Experiment evidence generated.").WithRun(run).WithTrial(trial).WithHypothesis(hypothesis);
        }

        public ExperimentationResult RecordInference(ExperimentInferenceData request, bool preview = false)
        {
            ExperimentInferenceData inference = (request ?? new ExperimentInferenceData()).Clone();
            if (string.IsNullOrWhiteSpace(inference.inferenceId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, "Experiment inference ID is required.");
            }

            if (!runsById.TryGetValue(inference.experimentRunId ?? string.Empty, out ExperimentRunData run))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingRun, $"Experiment run '{inference.experimentRunId}' was not found.");
            }

            foreach (string evidenceId in inference.evidenceIds ?? Array.Empty<string>())
            {
                if (!run.evidenceIds.Contains(evidenceId, StringComparer.Ordinal))
                {
                    return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, $"Inference evidence '{evidenceId}' is not attached to experiment run '{run.experimentRunId}'.");
                }
            }

            if (inferencesById.TryGetValue(inference.inferenceId, out ExperimentInferenceData existing))
            {
                return ExperimentationResult.Success("Experiment inference already exists.", duplicate: true).WithInference(existing);
            }

            if (preview)
            {
                return ExperimentationResult.Success("Experiment inference preview prepared.", preview: true).WithInference(inference);
            }

            inferencesById.Add(inference.inferenceId, inference.Clone());
            run.recordIds = Append(run.recordIds, inference.inferenceId);
            run.revision++;
            Touch("inference-recorded", run.experimentRunId, inference.inferenceId, "Experiment inference recorded.");
            return ExperimentationResult.Success("Experiment inference recorded.").WithInference(inference);
        }

        public ExperimentationResult CreateDiscoveryClaim(DiscoveryClaimData request, ExperimentPolicyData policy = null, bool preview = false)
        {
            DiscoveryClaimData claim = (request ?? new DiscoveryClaimData()).Clone();
            if (string.IsNullOrWhiteSpace(claim.claimId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, "Discovery claim ID is required.");
            }

            if (!runsById.TryGetValue(claim.experimentRunId ?? string.Empty, out ExperimentRunData run))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.MissingRun, $"Experiment run '{claim.experimentRunId}' was not found.");
            }

            if (!string.IsNullOrWhiteSpace(claim.inferenceId) && !inferencesById.ContainsKey(claim.inferenceId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, $"Discovery claim inference '{claim.inferenceId}' was not found.");
            }

            ExperimentPolicyData confirmation = policy?.Clone() ?? new ExperimentPolicyData();
            claim.supportCount = Math.Max(claim.supportCount, claim.evidenceIds.Count(id => run.evidenceIds.Contains(id, StringComparer.Ordinal)));
            claim.independentReproductionCount = Math.Max(claim.independentReproductionCount, CountIndependentReproductions(run));
            if (claim.status == DiscoveryClaimStatus.Draft)
            {
                claim.status = claim.supportCount >= confirmation.confirmationEvidenceThreshold && claim.independentReproductionCount >= confirmation.independentReproductionThreshold
                    ? DiscoveryClaimStatus.Confirmed
                    : DiscoveryClaimStatus.Proposed;
            }

            if (claimsById.TryGetValue(claim.claimId, out DiscoveryClaimData existing))
            {
                return ExperimentationResult.Success("Discovery claim already exists.", duplicate: true).WithClaim(existing);
            }

            if (preview)
            {
                return ExperimentationResult.Success("Discovery claim preview prepared.", preview: true).WithClaim(claim);
            }

            claimsById.Add(claim.claimId, claim.Clone());
            run.discoveryClaimIds = Append(run.discoveryClaimIds, claim.claimId);
            run.revision++;
            Touch("claim-created", run.experimentRunId, claim.claimId, "Discovery claim created.");
            return ExperimentationResult.Success("Discovery claim created.").WithClaim(claim);
        }

        public ExperimentationResult ReviewDiscoveryClaim(DiscoveryReviewData request, bool preview = false)
        {
            DiscoveryReviewData review = (request ?? new DiscoveryReviewData()).Clone();
            if (string.IsNullOrWhiteSpace(review.reviewId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, "Discovery review ID is required.");
            }

            if (!claimsById.TryGetValue(review.claimId ?? string.Empty, out DiscoveryClaimData claim))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, $"Discovery claim '{review.claimId}' was not found.");
            }

            if (reviewsById.TryGetValue(review.reviewId, out DiscoveryReviewData existing))
            {
                return ExperimentationResult.Success("Discovery review already exists.", duplicate: true).WithReview(existing);
            }

            if (preview)
            {
                return ExperimentationResult.Success("Discovery review preview prepared.", preview: true).WithReview(review);
            }

            ApplyReview(claim, review);
            reviewsById.Add(review.reviewId, review.Clone());
            Touch("claim-reviewed", claim.experimentRunId, review.reviewId, "Discovery claim reviewed.");
            return ExperimentationResult.Success("Discovery claim reviewed.").WithReview(review).WithClaim(claim);
        }

        public ExperimentationResult ProposeRecipeRegistration(RecipeRegistrationProposalData request, bool preview = false)
        {
            RecipeRegistrationProposalData proposal = (request ?? new RecipeRegistrationProposalData()).Clone();
            if (string.IsNullOrWhiteSpace(proposal.proposalId))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, "Recipe registration proposal ID is required.");
            }

            if (!claimsById.TryGetValue(proposal.claimId ?? string.Empty, out DiscoveryClaimData claim))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidRequest, $"Discovery claim '{proposal.claimId}' was not found.");
            }

            if (claim.status != DiscoveryClaimStatus.Confirmed)
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.InvalidState, "Only confirmed discovery claims can propose authoritative recipe registration.");
            }

            if (!proposal.authorized)
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.AccessDenied, "Authoritative recipe registration requires explicit authorization.");
            }

            if (proposalsById.TryGetValue(proposal.proposalId, out RecipeRegistrationProposalData existing))
            {
                return ExperimentationResult.Success("Recipe registration proposal already exists.", duplicate: true).WithRegistrationProposal(existing);
            }

            if (preview)
            {
                return ExperimentationResult.Success("Recipe registration proposal preview prepared.", preview: true).WithRegistrationProposal(proposal);
            }

            proposal.submitted = true;
            proposalsById.Add(proposal.proposalId, proposal.Clone());
            claim.registrationProposed = true;
            claim.revision++;
            Touch("registration-proposed", claim.experimentRunId, proposal.proposalId, "Recipe registration proposal created.");
            return ExperimentationResult.Success("Recipe registration proposal created without mutating authoritative recipes.").WithRegistrationProposal(proposal);
        }

        public ExperimentProjectionData ProjectRun(string runId, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, string policyId = "", bool recordAudit = false)
        {
            if (!runsById.TryGetValue(runId ?? string.Empty, out ExperimentRunData run))
            {
                return new ExperimentProjectionData { SubjectId = string.Empty, Decision = ExperimentProjectionDecision.Denied, Redacted = true, HiddenDetails = new[] { "missing-run" } };
            }

            string[] details = new[] { "detail.run", "detail.trials", "detail.hypotheses", "detail.evidence", "detail.inputs", "detail.outputs", "detail.provenance" };
            InformationAccessContext context = InformationAccessProjectionUtility.BuildContext(
                accessContext,
                ExperimentInformationSubject.Experiment(run.experimentRunId, run.experimentDefinitionId),
                InformationAccessMode.Inspect,
                InformationAccessPurpose.InternalSimulation,
                details,
                policyId);
            RedactedInformationProjection projection = accessRuntime?.Project(context, details);
            if (projection == null)
            {
                return new ExperimentProjectionData { SubjectId = string.Empty, Decision = ExperimentProjectionDecision.Denied, Redacted = true, HiddenDetails = new[] { "missing-access-runtime" } };
            }

            if (recordAudit)
            {
                accessRuntime.RecordAudit(projection.Decision, context, gameplayAudit: false);
            }

            bool denied = projection.Decision.Denied;
            bool redacted = !denied && !projection.Decision.FullAccess;
            return new ExperimentProjectionData
            {
                SubjectId = denied ? string.Empty : run.experimentRunId,
                Decision = denied ? ExperimentProjectionDecision.Denied : redacted ? ExperimentProjectionDecision.RedactedAccess : ExperimentProjectionDecision.FullAccess,
                Redacted = redacted,
                Run = denied ? null : RedactRun(run, redacted),
                VisibleTrialIds = denied || redacted ? Array.Empty<string>() : run.trialIds.ToArray(),
                VisibleHypothesisIds = denied ? Array.Empty<string>() : run.hypothesisIds.ToArray(),
                VisibleEvidenceIds = denied || redacted ? Array.Empty<string>() : run.evidenceIds.ToArray(),
                HiddenDetails = denied ? details : projection.Decision.HiddenDetails.Concat(projection.Decision.RedactedDetails).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray()
            };
        }

        public ExperimentationRuntimeSaveData CreateSaveData()
        {
            return new ExperimentationRuntimeSaveData
            {
                schemaVersion = ExperimentationRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                nextLogSequence = nextLogSequence,
                hypotheses = hypothesesById.Values.OrderBy(entry => entry.hypothesisId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                plans = plansById.Values.OrderBy(entry => entry.planId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                runs = runsById.Values.OrderBy(entry => entry.experimentRunId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                trials = trialsById.Values.OrderBy(entry => entry.trialId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                measurements = measurementsById.Values.OrderBy(entry => entry.measurementId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                inferences = inferencesById.Values.OrderBy(entry => entry.inferenceId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                claims = claimsById.Values.OrderBy(entry => entry.claimId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                reviews = reviewsById.Values.OrderBy(entry => entry.reviewId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                registrationProposals = proposalsById.Values.OrderBy(entry => entry.proposalId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                logs = logs.OrderBy(entry => entry.sequence).ThenBy(entry => entry.logId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList()
            };
        }

        public ExperimentationResult RestoreFromSaveData(ExperimentationRuntimeSaveData saveData, DefinitionRegistry registry)
        {
            if (!ValidateSaveData(saveData, registry, out string failure))
            {
                return ExperimentationResult.Failure(ExperimentOperationStatus.RestoreFailed, failure);
            }

            hypothesesById.Clear();
            plansById.Clear();
            runsById.Clear();
            trialsById.Clear();
            measurementsById.Clear();
            inferencesById.Clear();
            claimsById.Clear();
            reviewsById.Clear();
            proposalsById.Clear();
            logs.Clear();

            foreach (ExperimentHypothesisData hypothesis in saveData.hypotheses.Select(entry => entry.Clone()).OrderBy(entry => entry.hypothesisId, StringComparer.Ordinal))
            {
                hypothesesById[hypothesis.hypothesisId] = hypothesis;
            }

            foreach (ExperimentPlanData plan in saveData.plans.Select(entry => entry.Clone()).OrderBy(entry => entry.planId, StringComparer.Ordinal))
            {
                plansById[plan.planId] = plan;
            }

            foreach (ExperimentRunData run in saveData.runs.Select(entry => entry.Clone()).OrderBy(entry => entry.experimentRunId, StringComparer.Ordinal))
            {
                runsById[run.experimentRunId] = run;
            }

            foreach (ExperimentTrialData trial in saveData.trials.Select(entry => entry.Clone()).OrderBy(entry => entry.trialId, StringComparer.Ordinal))
            {
                trialsById[trial.trialId] = trial;
            }

            foreach (ExperimentMeasurementData measurement in saveData.measurements.Select(entry => entry.Clone()).OrderBy(entry => entry.measurementId, StringComparer.Ordinal))
            {
                measurementsById[measurement.measurementId] = measurement;
            }

            foreach (ExperimentInferenceData inference in saveData.inferences.Select(entry => entry.Clone()).OrderBy(entry => entry.inferenceId, StringComparer.Ordinal))
            {
                inferencesById[inference.inferenceId] = inference;
            }

            foreach (DiscoveryClaimData claim in saveData.claims.Select(entry => entry.Clone()).OrderBy(entry => entry.claimId, StringComparer.Ordinal))
            {
                claimsById[claim.claimId] = claim;
            }

            foreach (DiscoveryReviewData review in saveData.reviews.Select(entry => entry.Clone()).OrderBy(entry => entry.reviewId, StringComparer.Ordinal))
            {
                reviewsById[review.reviewId] = review;
            }

            foreach (RecipeRegistrationProposalData proposal in saveData.registrationProposals.Select(entry => entry.Clone()).OrderBy(entry => entry.proposalId, StringComparer.Ordinal))
            {
                proposalsById[proposal.proposalId] = proposal;
            }

            logs.AddRange(saveData.logs.Select(entry => entry.Clone()).OrderBy(entry => entry.sequence).ThenBy(entry => entry.logId, StringComparer.Ordinal));
            revision = Math.Max(0L, saveData.revision);
            nextLogSequence = Math.Max(0L, saveData.nextLogSequence);
            return ExperimentationResult.Success("Experimentation runtime restored.");
        }

        public static bool ValidateSaveData(ExperimentationRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Experimentation save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != ExperimentationRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported experimentation schema version {saveData.schemaVersion}.";
                return false;
            }

            if (saveData.revision < 0L || saveData.nextLogSequence < 0L)
            {
                failure = "Experimentation revisions cannot be negative.";
                return false;
            }

            if (!Unique(saveData.hypotheses, item => item?.hypothesisId, "hypothesis", out failure)
                || !Unique(saveData.plans, item => item?.planId, "plan", out failure)
                || !Unique(saveData.runs, item => item?.experimentRunId, "run", out failure)
                || !Unique(saveData.trials, item => item?.trialId, "trial", out failure)
                || !Unique(saveData.measurements, item => item?.measurementId, "measurement", out failure)
                || !Unique(saveData.inferences, item => item?.inferenceId, "inference", out failure)
                || !Unique(saveData.claims, item => item?.claimId, "claim", out failure)
                || !Unique(saveData.reviews, item => item?.reviewId, "review", out failure)
                || !Unique(saveData.registrationProposals, item => item?.proposalId, "registration proposal", out failure))
            {
                return false;
            }

            HashSet<string> hypotheses = IdSet(saveData.hypotheses, item => item.hypothesisId);
            HashSet<string> plans = IdSet(saveData.plans, item => item.planId);
            HashSet<string> runs = IdSet(saveData.runs, item => item.experimentRunId);
            HashSet<string> trials = IdSet(saveData.trials, item => item.trialId);
            HashSet<string> measurements = IdSet(saveData.measurements, item => item.measurementId);
            HashSet<string> inferences = IdSet(saveData.inferences, item => item.inferenceId);
            HashSet<string> claims = IdSet(saveData.claims, item => item.claimId);

            foreach (ExperimentPlanData plan in saveData.plans ?? new List<ExperimentPlanData>())
            {
                if (registry != null && !registry.TryGet(plan.experimentDefinitionId, out ExperimentDefinition _))
                {
                    failure = $"Experiment plan '{plan.planId}' references missing Experiment definition '{plan.experimentDefinitionId}'.";
                    return false;
                }

                foreach (string hypothesisId in plan.hypothesisIds ?? Array.Empty<string>())
                {
                    if (!hypotheses.Contains(hypothesisId))
                    {
                        failure = $"Experiment plan '{plan.planId}' references missing hypothesis '{hypothesisId}'.";
                        return false;
                    }
                }
            }

            foreach (ExperimentRunData run in saveData.runs ?? new List<ExperimentRunData>())
            {
                if (!plans.Contains(run.planId))
                {
                    failure = $"Experiment run '{run.experimentRunId}' references missing plan '{run.planId}'.";
                    return false;
                }

                foreach (string trialId in run.trialIds ?? Array.Empty<string>())
                {
                    if (!trials.Contains(trialId))
                    {
                        failure = $"Experiment run '{run.experimentRunId}' references missing trial '{trialId}'.";
                        return false;
                    }
                }

                foreach (string claimId in run.discoveryClaimIds ?? Array.Empty<string>())
                {
                    if (!claims.Contains(claimId))
                    {
                        failure = $"Experiment run '{run.experimentRunId}' references missing discovery claim '{claimId}'.";
                        return false;
                    }
                }
            }

            foreach (ExperimentTrialData trial in saveData.trials ?? new List<ExperimentTrialData>())
            {
                if (!runs.Contains(trial.experimentRunId))
                {
                    failure = $"Experiment trial '{trial.trialId}' references missing run '{trial.experimentRunId}'.";
                    return false;
                }

                foreach (string measurementId in trial.measurementIds ?? Array.Empty<string>())
                {
                    if (!measurements.Contains(measurementId))
                    {
                        failure = $"Experiment trial '{trial.trialId}' references missing measurement '{measurementId}'.";
                        return false;
                    }
                }
            }

            foreach (ExperimentMeasurementData measurement in saveData.measurements ?? new List<ExperimentMeasurementData>())
            {
                if (!runs.Contains(measurement.experimentRunId))
                {
                    failure = $"Experiment measurement '{measurement.measurementId}' references missing run '{measurement.experimentRunId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(measurement.trialId) && !trials.Contains(measurement.trialId))
                {
                    failure = $"Experiment measurement '{measurement.measurementId}' references missing trial '{measurement.trialId}'.";
                    return false;
                }
            }

            foreach (ExperimentInferenceData inference in saveData.inferences ?? new List<ExperimentInferenceData>())
            {
                if (!runs.Contains(inference.experimentRunId))
                {
                    failure = $"Experiment inference '{inference.inferenceId}' references missing run '{inference.experimentRunId}'.";
                    return false;
                }
            }

            foreach (DiscoveryClaimData claim in saveData.claims ?? new List<DiscoveryClaimData>())
            {
                if (!runs.Contains(claim.experimentRunId))
                {
                    failure = $"Discovery claim '{claim.claimId}' references missing run '{claim.experimentRunId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(claim.inferenceId) && !inferences.Contains(claim.inferenceId))
                {
                    failure = $"Discovery claim '{claim.claimId}' references missing inference '{claim.inferenceId}'.";
                    return false;
                }
            }

            foreach (DiscoveryReviewData review in saveData.reviews ?? new List<DiscoveryReviewData>())
            {
                if (!claims.Contains(review.claimId))
                {
                    failure = $"Discovery review '{review.reviewId}' references missing claim '{review.claimId}'.";
                    return false;
                }
            }

            foreach (RecipeRegistrationProposalData proposal in saveData.registrationProposals ?? new List<RecipeRegistrationProposalData>())
            {
                if (!claims.Contains(proposal.claimId))
                {
                    failure = $"Recipe registration proposal '{proposal.proposalId}' references missing claim '{proposal.claimId}'.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryGet<T>(Dictionary<string, T> values, string id, out T clone) where T : class
        {
            clone = null;
            if (string.IsNullOrWhiteSpace(id) || !values.TryGetValue(id, out T value))
            {
                return false;
            }

            clone = value switch
            {
                ExperimentHypothesisData hypothesis => hypothesis.Clone() as T,
                ExperimentPlanData plan => plan.Clone() as T,
                ExperimentRunData run => run.Clone() as T,
                ExperimentTrialData trial => trial.Clone() as T,
                ExperimentMeasurementData measurement => measurement.Clone() as T,
                ExperimentInferenceData inference => inference.Clone() as T,
                DiscoveryClaimData claim => claim.Clone() as T,
                _ => value
            };
            return true;
        }

        private void UpdateHypothesisStatus(ExperimentHypothesisData hypothesis)
        {
            int support = hypothesis.supportingEvidenceIds?.Length ?? 0;
            int contradiction = hypothesis.contradictingEvidenceIds?.Length ?? 0;
            hypothesis.confidence = Math.Max(0, Math.Min(1000, 250 + support * 180 - contradiction * 220));
            hypothesis.status = contradiction > support
                ? HypothesisStatus.Contradicted
                : support >= 2
                    ? HypothesisStatus.Supported
                    : support == 1
                        ? HypothesisStatus.WeaklySupported
                        : hypothesis.status;
            hypothesis.revision++;
        }

        private static ExperimentTrialOutcome ClassifyCraftingOutcome(CraftingExecutionResult result, ExperimentTrialOutcome expected)
        {
            if (result == null)
            {
                return ExperimentTrialOutcome.Invalid;
            }

            if (result.Preview)
            {
                return ExperimentTrialOutcome.Inconclusive;
            }

            if (result.Succeeded)
            {
                return expected == ExperimentTrialOutcome.ExpectedFailure ? ExperimentTrialOutcome.UnexpectedSuccess : ExperimentTrialOutcome.ExpectedSuccess;
            }

            return expected == ExperimentTrialOutcome.ExpectedFailure ? ExperimentTrialOutcome.ExpectedFailure : ExperimentTrialOutcome.UnexpectedFailure;
        }

        private int CountIndependentReproductions(ExperimentRunData run)
        {
            return (run?.trialIds ?? Array.Empty<string>())
                .Select(id => trialsById.TryGetValue(id, out ExperimentTrialData trial) ? trial : null)
                .Where(trial => trial != null && trial.outcome == ExperimentTrialOutcome.ExpectedSuccess)
                .GroupBy(trial => trial.ReproducibilitySignature, StringComparer.Ordinal)
                .Sum(group => group.Select(trial => trial.deterministicSeed).Distinct(StringComparer.Ordinal).Count());
        }

        private static void ApplyReview(DiscoveryClaimData claim, DiscoveryReviewData review)
        {
            claim.reviewerPersonId = review.reviewerPersonId ?? string.Empty;
            claim.status = review.decision switch
            {
                DiscoveryReviewDecision.AcceptProvisionally => DiscoveryClaimStatus.ProvisionallyAccepted,
                DiscoveryReviewDecision.RequestReproduction => DiscoveryClaimStatus.ReproductionRequested,
                DiscoveryReviewDecision.Confirm => DiscoveryClaimStatus.Confirmed,
                DiscoveryReviewDecision.Reject => DiscoveryClaimStatus.Rejected,
                DiscoveryReviewDecision.Withdraw => DiscoveryClaimStatus.Withdrawn,
                _ => claim.status
            };
            claim.revision++;
        }

        private static bool CanTransition(ExperimentRunState current, ExperimentRunState target)
        {
            if (current == target)
            {
                return true;
            }

            if (current == ExperimentRunState.Archived || current == ExperimentRunState.Invalid)
            {
                return false;
            }

            return target != ExperimentRunState.Draft;
        }

        private static ExperimentRunData RedactRun(ExperimentRunData run, bool redacted)
        {
            ExperimentRunData clone = run.Clone();
            if (!redacted)
            {
                return clone;
            }

            clone.inputItemIds = Array.Empty<string>();
            clone.toolIds = Array.Empty<string>();
            clone.stationIds = Array.Empty<string>();
            clone.executionOperationIds = Array.Empty<string>();
            clone.evidenceIds = Array.Empty<string>();
            clone.provenance = string.Empty;
            return clone;
        }

        private void Touch(string eventType, string runId, string trialId, string message)
        {
            revision++;
            nextLogSequence++;
            logs.Add(new ExperimentLogRecordData
            {
                logId = StableId("experiment-log", eventType, runId, trialId, nextLogSequence.ToString(CultureInfo.InvariantCulture)),
                experimentRunId = runId ?? string.Empty,
                trialId = trialId ?? string.Empty,
                eventType = eventType ?? string.Empty,
                message = message ?? string.Empty,
                sequence = nextLogSequence
            });
        }

        private static string[] Append(IEnumerable<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ExperimentVariableDefinitionData.NormalizeIds(values);
            }

            return ExperimentVariableDefinitionData.NormalizeIds((values ?? Array.Empty<string>()).Concat(new[] { value }));
        }

        private static bool Unique<T>(IEnumerable<T> values, Func<T, string> idSelector, string label, out string failure)
        {
            failure = string.Empty;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (T value in values ?? Array.Empty<T>())
            {
                string id = idSelector(value);
                if (string.IsNullOrWhiteSpace(id))
                {
                    failure = $"Experimentation {label} is missing an ID.";
                    return false;
                }

                if (!seen.Add(id))
                {
                    failure = $"Duplicate experimentation {label} ID '{id}'.";
                    return false;
                }
            }

            return true;
        }

        private static HashSet<string> IdSet<T>(IEnumerable<T> values, Func<T, string> selector)
        {
            return new HashSet<string>((values ?? Array.Empty<T>()).Select(selector).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        }

        internal static string StableId(string prefix, params string[] parts)
        {
            string input = string.Join("|", parts ?? Array.Empty<string>());
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            string hex = BitConverter.ToString(bytes, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
            return $"{prefix}.{hex}";
        }
    }
}
