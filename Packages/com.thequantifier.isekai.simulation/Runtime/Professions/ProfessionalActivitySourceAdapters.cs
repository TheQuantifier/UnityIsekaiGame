using System;
using System.Linq;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Disassembly;
using UnityIsekaiGame.Inventory.Experimentation;
using UnityIsekaiGame.Inventory.Production;

namespace UnityIsekaiGame.Professions
{
    public static class ProfessionalActivitySourceAdapters
    {
        public static ProfessionalActivitySourceSnapshot FromCraftingOperation(CraftingOperationRecordData record)
        {
            int measuredQuality = record == null
                ? 0
                : Math.Max(0, Math.Min(1000, (int)Math.Round((0.5f + record.craftingQualityAdjustment) * 1000f)));
            ProfessionalActivityDifficulty measuredDifficulty = record == null
                ? ProfessionalActivityDifficulty.Unknown
                : DifficultyFromWork(record.craftDurationSeconds, record.consumedInputs?.Count ?? 0, record.catalysts?.Count ?? 0);
            return FromCraftingOperation(record, measuredQuality, measuredDifficulty);
        }

        public static ProfessionalActivitySourceSnapshot FromCraftingOperation(
            CraftingOperationRecordData record,
            int actualQuality,
            ProfessionalActivityDifficulty difficulty)
        {
            if (record == null)
            {
                return Missing(ProfessionalActivitySourceType.CraftingOperation, "missing");
            }

            return new ProfessionalActivitySourceSnapshot(
                new ProfessionalActivitySourceReferenceData { sourceType = ProfessionalActivitySourceType.CraftingOperation, sourceId = record.operationId, parentSourceId = record.recipeId, sourceRevision = record.revision },
                record.actorPersonId,
                record.worldTime,
                record.status == CraftingExecutionStatus.Succeeded || record.state == CraftingOperationState.Completed ? ProfessionalActivityOutcomeState.Successful : ProfessionalActivityOutcomeState.Failed,
                Math.Max(1f, record.outputs?.Sum(output => output?.quantity ?? 0f) ?? 1f),
                difficulty,
                record.status == CraftingExecutionStatus.Succeeded ? actualQuality : Math.Min(actualQuality, 250),
                new[] { "source.crafting", record.recipeId },
                (record.outputs ?? new System.Collections.Generic.List<CraftingOutputItemData>()).Select(output => output?.itemInstanceId),
                record.state == CraftingOperationState.Completed || record.status == CraftingExecutionStatus.Succeeded,
                false,
                true,
                record.status.ToString());
        }

        public static ProfessionalActivitySourceSnapshot FromProductionJob(ProductionJobData job, ProductionWorkerAssignmentData assignment = null)
        {
            if (job == null)
            {
                return Missing(ProfessionalActivitySourceType.ProductionJob, "missing");
            }

            string personId = assignment?.personId ?? string.Empty;
            float duration = Math.Max(1f, job.stages?.Sum(stage => stage?.completedWork ?? 0f) ?? job.batchQuantity);
            bool completed = job.state == ProductionJobState.Completed;
            return new ProfessionalActivitySourceSnapshot(
                new ProfessionalActivitySourceReferenceData { sourceType = ProfessionalActivitySourceType.ProductionJob, sourceId = job.jobId, parentSourceId = job.workOrderId, sourceRevision = job.revision },
                personId,
                string.IsNullOrWhiteSpace(job.completionWorldTime) ? job.lastEvaluatedWorldTime : job.completionWorldTime,
                completed ? ProfessionalActivityOutcomeState.Successful : ProfessionalActivityOutcomeState.PartialSuccess,
                duration,
                ProfessionalActivityDifficulty.Skilled,
                completed ? 720 : 500,
                new[] { "source.production", job.recipeDefinitionId, assignment?.role.ToString() ?? string.Empty },
                (job.outputItemIds ?? Array.Empty<string>()).Concat(job.byproductItemIds ?? Array.Empty<string>()),
                completed || (job.completedStageIds?.Length ?? 0) > 0,
                false,
                true,
                job.state.ToString());
        }

        public static ProfessionalActivitySourceSnapshot FromRepairRecord(ItemRepairRecordData repair)
        {
            if (repair == null)
            {
                return Missing(ProfessionalActivitySourceType.RepairOperation, "missing");
            }

            return new ProfessionalActivitySourceSnapshot(
                new ProfessionalActivitySourceReferenceData { sourceType = ProfessionalActivitySourceType.RepairOperation, sourceId = repair.repairId, parentSourceId = repair.itemInstanceId, sourceRevision = 1L },
                repair.actorPersonId,
                repair.worldTime,
                repair.recoveredDurability > 0f ? ProfessionalActivityOutcomeState.Successful : ProfessionalActivityOutcomeState.Failed,
                repair.recoveredDurability,
                ProfessionalActivityDifficulty.Routine,
                RepairQualityScore(repair.repairQuality),
                new[] { "source.repair", repair.repairedComponentEntryId, repair.repairQuality.ToString() },
                new[] { repair.itemInstanceId },
                true,
                false,
                true,
                repair.repairQuality.ToString());
        }

        public static ProfessionalActivitySourceSnapshot FromItemRecoveryOperation(DisassemblyOperationRecordData record)
        {
            if (record == null)
            {
                return Missing(ProfessionalActivitySourceType.ItemRecoveryOperation, "missing");
            }

            return new ProfessionalActivitySourceSnapshot(
                new ProfessionalActivitySourceReferenceData { sourceType = ProfessionalActivitySourceType.ItemRecoveryOperation, sourceId = record.operationId, parentSourceId = record.itemInstanceId, sourceRevision = record.revision },
                record.actorPersonId,
                record.worldTime,
                record.state == DisassemblyOperationState.Completed ? ProfessionalActivityOutcomeState.Successful : ProfessionalActivityOutcomeState.PartialSuccess,
                Math.Max(1f, record.outcomes?.Sum(output => output?.returnedQuantity ?? 0) ?? 1f),
                ProfessionalActivityDifficulty.Routine,
                Math.Max(100, Math.Min(1000, (int)Math.Round(record.actualEfficiency * 1000f))),
                new[] { "source.item-recovery", $"recovery.{record.operationKind.ToString().ToLowerInvariant()}", record.itemDefinitionId },
                new[] { record.itemInstanceId }.Concat(record.outcomes?.SelectMany(output => output?.outputItemInstanceIds ?? Array.Empty<string>()) ?? Array.Empty<string>()),
                record.state == DisassemblyOperationState.Completed,
                false,
                true,
                record.efficiencyTier.ToString());
        }

        public static ProfessionalActivitySourceSnapshot FromExperimentTrial(ExperimentTrialData trial, string actingPersonId = "")
        {
            if (trial == null)
            {
                return Missing(ProfessionalActivitySourceType.ExperimentTrial, "missing");
            }

            string personId = string.IsNullOrWhiteSpace(actingPersonId) ? (trial.workerIds ?? Array.Empty<string>()).FirstOrDefault() ?? string.Empty : actingPersonId;
            return new ProfessionalActivitySourceSnapshot(
                new ProfessionalActivitySourceReferenceData { sourceType = ProfessionalActivitySourceType.ExperimentTrial, sourceId = trial.trialId, parentSourceId = trial.experimentRunId, sourceRevision = trial.revision },
                personId,
                trial.completionWorldTime,
                trial.outcome == ExperimentTrialOutcome.ExpectedSuccess || trial.outcome == ExperimentTrialOutcome.UnexpectedSuccess || trial.outcome == ExperimentTrialOutcome.PartialSuccess || trial.outcome == ExperimentTrialOutcome.DifferentValidOutput || trial.outcome == ExperimentTrialOutcome.Inconclusive ? ProfessionalActivityOutcomeState.Successful : ProfessionalActivityOutcomeState.Failed,
                1f,
                ProfessionalActivityDifficulty.Advanced,
                trial.outcome == ExperimentTrialOutcome.ExpectedSuccess || trial.outcome == ExperimentTrialOutcome.UnexpectedSuccess ? 780 : 500,
                new[] { "source.experiment", trial.recipeDefinitionId, trial.trialKind.ToString() },
                (trial.outputItemIds ?? Array.Empty<string>()).Concat(trial.evidenceIds ?? Array.Empty<string>()),
                !string.IsNullOrWhiteSpace(trial.completionWorldTime),
                false,
                true,
                trial.outcome.ToString());
        }

        public static ProfessionalActivitySourceSnapshot FromDiscoveryClaim(DiscoveryClaimData claim)
        {
            if (claim == null)
            {
                return Missing(ProfessionalActivitySourceType.DiscoveryClaim, "missing");
            }

            return new ProfessionalActivitySourceSnapshot(
                new ProfessionalActivitySourceReferenceData { sourceType = ProfessionalActivitySourceType.DiscoveryClaim, sourceId = claim.claimId, parentSourceId = claim.experimentRunId, sourceRevision = claim.revision },
                claim.claimantPersonId,
                claim.worldTime,
                claim.status == DiscoveryClaimStatus.Confirmed || claim.status == DiscoveryClaimStatus.ProvisionallyAccepted || claim.registrationProposed ? ProfessionalActivityOutcomeState.Innovative : ProfessionalActivityOutcomeState.PartialSuccess,
                1f,
                ProfessionalActivityDifficulty.Innovative,
                Math.Max(0, Math.Min(1000, claim.confidence)),
                new[] { "source.discovery", claim.status.ToString() },
                claim.evidenceIds,
                claim.status != DiscoveryClaimStatus.Draft,
                false,
                true,
                claim.status.ToString());
        }

        public static ProfessionalActivitySourceSnapshot FromTrainingPractical(TrainingPracticalWorkRecordData record, string learnerPersonId = "")
        {
            if (record == null)
            {
                return Missing(ProfessionalActivitySourceType.TrainingPracticalAssignment, "missing");
            }

            return new ProfessionalActivitySourceSnapshot(
                new ProfessionalActivitySourceReferenceData { sourceType = ProfessionalActivitySourceType.TrainingPracticalAssignment, sourceId = record.recordId, parentSourceId = record.enrollmentId, sourceRevision = record.revision },
                learnerPersonId ?? string.Empty,
                record.worldTime,
                record.successful ? ProfessionalActivityOutcomeState.Successful : ProfessionalActivityOutcomeState.Failed,
                Math.Max(1, record.quantity),
                ProfessionalActivityDifficulty.Routine,
                record.quality,
                new[] { "source.training-practical", record.assignmentId, record.activityCategory.ToString() },
                new[] { record.activityReferenceId },
                record.accepted,
                false,
                true,
                record.successful ? "Successful" : "Failed");
        }

        public static ProfessionalActivitySourceSnapshot FromTrainingSupervisedWork(TrainingSupervisedWorkRecordData record)
        {
            if (record == null)
            {
                return Missing(ProfessionalActivitySourceType.TrainingSupervisedWork, "missing");
            }

            return new ProfessionalActivitySourceSnapshot(
                new ProfessionalActivitySourceReferenceData { sourceType = ProfessionalActivitySourceType.TrainingSupervisedWork, sourceId = record.recordId, parentSourceId = record.enrollmentId, sourceRevision = record.revision },
                record.learnerPersonId,
                record.completionWorldTime,
                record.outcome == TrainingWorkOutcome.Succeeded ? ProfessionalActivityOutcomeState.Successful : ProfessionalActivityOutcomeState.Failed,
                1f,
                ProfessionalActivityDifficulty.Routine,
                record.quality,
                new[] { "source.training-supervised", record.professionId, record.supervisionLevel.ToString() },
                new[] { record.activityReferenceId },
                record.outcome != TrainingWorkOutcome.Unknown,
                false,
                true,
                record.outcome.ToString());
        }

        public static ProfessionalActivitySourceSnapshot FromTeachingSession(TrainingLearningSessionData record)
        {
            if (record == null)
            {
                return Missing(ProfessionalActivitySourceType.TeachingSession, "missing");
            }

            return new ProfessionalActivitySourceSnapshot(
                new ProfessionalActivitySourceReferenceData { sourceType = ProfessionalActivitySourceType.TeachingSession, sourceId = record.sessionId, parentSourceId = record.enrollmentId, sourceRevision = record.revision },
                (record.instructorIds ?? Array.Empty<string>()).FirstOrDefault() ?? string.Empty,
                record.completionWorldTime,
                record.state == TrainingSessionCompletionState.Completed || record.attended ? ProfessionalActivityOutcomeState.Successful : ProfessionalActivityOutcomeState.PartialSuccess,
                Math.Max(1, record.learnerIds?.Length ?? 1),
                ProfessionalActivityDifficulty.Skilled,
                record.state == TrainingSessionCompletionState.Completed ? 700 : 500,
                new[] { "source.teaching", record.moduleId, record.lessonId, record.teachingMethod.ToString() },
                (record.learnerIds ?? Array.Empty<string>()).Concat(record.evidenceIds ?? Array.Empty<string>()),
                record.attended || record.state == TrainingSessionCompletionState.Completed,
                false,
                true,
                record.state.ToString());
        }

        public static ProfessionalActivitySourceSnapshot FromCustom(ProfessionalActivitySourceType sourceType, string sourceId, string personId, ProfessionalActivityOutcomeState outcome, bool completed = true, int quality = 500, ProfessionalActivityDifficulty difficulty = ProfessionalActivityDifficulty.Routine, string worldTime = "", params string[] tags)
        {
            return new ProfessionalActivitySourceSnapshot(
                new ProfessionalActivitySourceReferenceData { sourceType = sourceType, sourceId = sourceId ?? string.Empty, sourceRevision = 1L },
                personId,
                worldTime,
                outcome,
                1f,
                difficulty,
                quality,
                tags,
                Array.Empty<string>(),
                completed,
                false,
                true,
                "Custom authoritative activity source.");
        }

        private static ProfessionalActivitySourceSnapshot Missing(ProfessionalActivitySourceType sourceType, string sourceId)
        {
            return new ProfessionalActivitySourceSnapshot(
                new ProfessionalActivitySourceReferenceData { sourceType = sourceType, sourceId = sourceId ?? string.Empty },
                string.Empty,
                string.Empty,
                ProfessionalActivityOutcomeState.Unknown,
                0f,
                ProfessionalActivityDifficulty.Unknown,
                0,
                Array.Empty<string>(),
                Array.Empty<string>(),
                false,
                false,
                false,
                "Source record is missing.");
        }

        private static int RepairQualityScore(ItemRepairQuality quality)
        {
            return quality switch
            {
                ItemRepairQuality.Poor => 250,
                ItemRepairQuality.Adequate => 500,
                ItemRepairQuality.Good => 700,
                ItemRepairQuality.Excellent => 850,
                ItemRepairQuality.Masterwork => 1000,
                _ => 500
            };
        }

        private static ProfessionalActivityDifficulty DifficultyFromWork(float durationSeconds, int inputKinds, int catalystKinds)
        {
            float complexity = Math.Max(0f, durationSeconds) / 30f + Math.Max(0, inputKinds - 1) + Math.Max(0, catalystKinds);
            if (complexity >= 8f) return ProfessionalActivityDifficulty.Advanced;
            if (complexity >= 4f) return ProfessionalActivityDifficulty.Skilled;
            if (complexity >= 1f) return ProfessionalActivityDifficulty.Routine;
            return ProfessionalActivityDifficulty.Trivial;
        }
    }
}
