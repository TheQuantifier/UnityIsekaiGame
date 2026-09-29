using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology.BiologicalConditions;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.Recovery;

namespace UnityIsekaiGame.Beings.Biology.Integration
{
    public sealed class BodyBiologyFacade
    {
        private readonly ActorBodyRuntime body;

        public BodyBiologyFacade(ActorBodyRuntime body)
        {
            this.body = body;
        }

        public ActorBodyRuntime Runtime => body;
        public string ActorBodyId => body == null ? string.Empty : body.ActorBodyId;
        public string PersonId => body == null ? string.Empty : body.PersonId;
        public string SpeciesId => body == null ? string.Empty : body.SpeciesDefinitionId;
        public bool IsReady => body != null && body.IsReady && Validate().Succeeded;

        public BodyBiologySnapshot CaptureSnapshot(BodyBiologySnapshotDetail detail = BodyBiologySnapshotDetail.Full, int maxAttempts = 3)
        {
            if (body == null)
            {
                return new BodyBiologySnapshot(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, BodyReadinessState.Uninitialized, null, null, null, null, coherent: false, new[] { "Body runtime is missing." });
            }

            BodyBiologySnapshot snapshot = CaptureOnce();
            BodyBiologyRevisionSet first = snapshot.Revisions;
            for (int attempt = 1; attempt < Math.Max(1, maxAttempts); attempt++)
            {
                BodyBiologySnapshot next = CaptureOnce();
                if (first.SameAs(next.Revisions))
                {
                    return next;
                }

                snapshot = next;
                first = next.Revisions;
            }

            List<string> diagnostics = new List<string>(snapshot.Diagnostics)
            {
                "Body biology snapshot revisions changed while capturing; retry after current update completes."
            };

            return new BodyBiologySnapshot(snapshot.ActorBodyId, snapshot.PersonId, snapshot.SpeciesId, snapshot.BiologicalClassificationId, snapshot.BodyFormId, snapshot.Readiness, snapshot.Body, snapshot.BiologicalConditions, snapshot.Transformation, snapshot.Revisions, coherent: false, diagnostics);
        }

        public BodyBiologyValidationResult Validate()
        {
            return BodyBiologyValidator.Validate(CaptureSnapshot());
        }

        public BodyBiologyAdvanceResult PreviewAdvance(BodyBiologyAdvanceRequest request)
        {
            return AdvanceInternal(request, preview: true);
        }

        public BodyBiologyAdvanceResult Advance(BodyBiologyAdvanceRequest request)
        {
            return AdvanceInternal(request, preview: false);
        }

        private BodyBiologyAdvanceResult AdvanceInternal(BodyBiologyAdvanceRequest request, bool preview)
        {
            BodyBiologySnapshot before = CaptureSnapshot();
            List<BodyBiologyAdvanceStepResult> steps = new List<BodyBiologyAdvanceStepResult>();
            BodyBiologyValidationResult validation = BodyBiologyValidator.Validate(before);
            if (!validation.Succeeded)
            {
                return BodyBiologyAdvanceResult.Failure(BodyBiologyAdvanceCode.RuntimeNotReady, validation.Message, preview, before, before, steps);
            }

            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return BodyBiologyAdvanceResult.Failure(BodyBiologyAdvanceCode.InvalidRequest, "Body biology advance requires a transaction ID.", preview, before, before, steps);
            }

            if (!string.Equals(request.ActorBodyId, body.ActorBodyId, StringComparison.Ordinal))
            {
                return BodyBiologyAdvanceResult.Failure(BodyBiologyAdvanceCode.InvalidRequest, $"Request actor/body '{request.ActorBodyId}' does not match runtime body '{body.ActorBodyId}'.", preview, before, before, steps);
            }

            if (float.IsNaN(request.ElapsedGameSeconds) || float.IsInfinity(request.ElapsedGameSeconds) || request.ElapsedGameSeconds < 0f)
            {
                return BodyBiologyAdvanceResult.Failure(BodyBiologyAdvanceCode.InvalidRequest, "Elapsed game seconds must be finite and non-negative.", preview, before, before, steps);
            }

            BiologicalConditionConsequenceExecutionResult conditionResult = ExecuteConditionConsequences(request, preview, before.Body);
            steps.Add(BodyBiologyAdvanceSteps.FromConditions(conditionResult));
            if (conditionResult == null || !conditionResult.Succeeded)
            {
                return BodyBiologyAdvanceResult.Failure(BodyBiologyAdvanceCode.ConditionFailure, conditionResult == null ? "Biological Condition consequence result is missing." : conditionResult.Message, preview, before, CaptureSnapshot(), steps);
            }

            BodySnapshot conditionSnapshot = preview ? before.Body : body.CreateSnapshot();
            BiologicalHazardTickResult hazardResult = ExecuteHazardTick(request, preview, conditionSnapshot);
            steps.Add(BodyBiologyAdvanceSteps.FromHazards(hazardResult));
            if (hazardResult == null || !hazardResult.Succeeded)
            {
                return BodyBiologyAdvanceResult.Failure(BodyBiologyAdvanceCode.HazardFailure, hazardResult == null ? "Biological Hazard result is missing." : hazardResult.Message, preview, before, CaptureSnapshot(), steps);
            }

            BodySnapshot hazardSnapshot = preview ? before.Body : body.CreateSnapshot();
            var vitalResult = preview
                ? body.VitalProcesses.PreviewProcessUpdate(request.ElapsedGameSeconds, StepTransaction(request, "vitals"), hazardSnapshot.Anatomy, hazardSnapshot.Condition)
                : body.VitalProcesses.ApplyProcessUpdate(request.ElapsedGameSeconds, StepTransaction(request, "vitals"), hazardSnapshot.Anatomy, hazardSnapshot.Condition);
            steps.Add(BodyBiologyAdvanceSteps.FromVitals(vitalResult));
            if (vitalResult == null || !vitalResult.Succeeded)
            {
                return BodyBiologyAdvanceResult.Failure(BodyBiologyAdvanceCode.VitalFailure, vitalResult == null ? "Vital Process result is missing." : vitalResult.Message, preview, before, CaptureSnapshot(), steps);
            }

            BodySnapshot vitalSnapshot = preview ? before.Body : body.CreateSnapshot();
            BiologicalRecoveryResult recoveryResult = ExecuteRecoveryTick(request, preview, vitalSnapshot);
            steps.Add(BodyBiologyAdvanceSteps.FromRecovery(recoveryResult));
            if (recoveryResult == null || !recoveryResult.Succeeded)
            {
                return BodyBiologyAdvanceResult.Failure(BodyBiologyAdvanceCode.RecoveryFailure, recoveryResult == null ? "Biological Recovery result is missing." : recoveryResult.Message, preview, before, CaptureSnapshot(), steps);
            }

            BodyBiologySnapshot after = CaptureSnapshot();
            return BodyBiologyAdvanceResult.Success(preview ? "Body biology advance preview resolved without mutation." : "Body biology advance committed in deterministic order.", preview, before, after, steps);
        }

        private BiologicalConditionConsequenceExecutionResult ExecuteConditionConsequences(BodyBiologyAdvanceRequest request, bool preview, BodySnapshot snapshot)
        {
            BiologicalConditionTickRequest tick = new BiologicalConditionTickRequest(body.ActorBodyId, request.ElapsedGameSeconds, StepTransaction(request, "conditions"), preview, "Body biology integration advance");
            BiologicalConditionConsequenceExecutionRequest consequences = new BiologicalConditionConsequenceExecutionRequest(
                tick,
                snapshot,
                body.BiologicalCompatibility,
                body.VitalProcesses,
                body.BiologicalHazards,
                body.BiologicalRecovery,
                request.DamageHealing,
                request.DamageTargetObject,
                request.DamageSourceObject,
                request.DamageTargetActorId,
                request.DamageSourceActorId);

            return preview
                ? body.BiologicalConditions.PreviewTickConsequences(consequences)
                : body.BiologicalConditions.ApplyTickConsequences(consequences);
        }

        private BiologicalHazardTickResult ExecuteHazardTick(BodyBiologyAdvanceRequest request, bool preview, BodySnapshot snapshot)
        {
            BiologicalHazardTickRequest tick = new BiologicalHazardTickRequest(body.ActorBodyId, request.ElapsedGameSeconds, StepTransaction(request, "hazards"), preview, "Body biology integration advance");
            return preview
                ? body.BiologicalHazards.PreviewTick(tick, body.VitalProcesses, snapshot.Anatomy, snapshot.Condition, body.BiologicalCompatibility, snapshot)
                : body.BiologicalHazards.ApplyTick(tick, body.VitalProcesses, snapshot.Anatomy, snapshot.Condition, body.BiologicalCompatibility, snapshot);
        }

        private BiologicalRecoveryResult ExecuteRecoveryTick(BodyBiologyAdvanceRequest request, bool preview, BodySnapshot snapshot)
        {
            RecoveryTickRequest tick = new RecoveryTickRequest
            {
                ActorBodyId = body.ActorBodyId,
                TickId = StepTransaction(request, "recovery"),
                ElapsedGameSeconds = request.ElapsedGameSeconds,
                AuthorityContext = string.IsNullOrWhiteSpace(request.AuthorityContext) ? "body-biology-facade" : request.AuthorityContext,
                ExpectedBodyRevision = snapshot.BodyRevision,
                ExpectedConditionRevision = snapshot.Condition == null ? 0L : snapshot.Condition.ConditionRevision,
                ExpectedVitalRevision = snapshot.VitalProcesses == null ? 0L : snapshot.VitalProcesses.VitalRevision,
                ExpectedHazardRevision = snapshot.BiologicalHazards == null ? 0L : snapshot.BiologicalHazards.HazardRevision,
                ExpectedCompatibilityRevision = snapshot.BiologicalCompatibility == null ? 0L : snapshot.BiologicalCompatibility.CompatibilityRevision,
                ExpectedRecoveryRevision = snapshot.BiologicalRecovery == null ? 0L : snapshot.BiologicalRecovery.RecoveryRevision
            };

            return preview
                ? body.BiologicalRecovery.PreviewTick(tick, snapshot, body.BiologicalCompatibility, body.Condition, body.VitalProcesses)
                : body.BiologicalRecovery.ApplyTick(tick, snapshot, body.BiologicalCompatibility, body.Condition, body.VitalProcesses);
        }

        private BodyBiologySnapshot CaptureOnce()
        {
            BodySnapshot bodySnapshot = body.CreateSnapshot();
            var conditionSnapshot = body.BiologicalConditions.CreateSnapshot();
            var transformationSnapshot = body.Transformation.CreateSnapshot();
            BodyBiologyRevisionSet revisions = new BodyBiologyRevisionSet(
                bodySnapshot == null ? 0L : bodySnapshot.BodyRevision,
                bodySnapshot?.Anatomy == null ? 0L : bodySnapshot.Anatomy.AnatomyRevision,
                bodySnapshot?.Condition == null ? 0L : bodySnapshot.Condition.ConditionRevision,
                bodySnapshot?.VitalProcesses == null ? 0L : bodySnapshot.VitalProcesses.VitalRevision,
                bodySnapshot?.BiologicalHazards == null ? 0L : bodySnapshot.BiologicalHazards.HazardRevision,
                bodySnapshot?.BiologicalCompatibility == null ? 0L : bodySnapshot.BiologicalCompatibility.CompatibilityRevision,
                bodySnapshot?.BiologicalRecovery == null ? 0L : bodySnapshot.BiologicalRecovery.RecoveryRevision,
                conditionSnapshot == null ? 0L : conditionSnapshot.BiologicalConditionRevision,
                transformationSnapshot == null ? 0L : transformationSnapshot.TransformationRevision);

            List<string> diagnostics = new List<string>();
            if (bodySnapshot != null && bodySnapshot.Diagnostics != null)
            {
                diagnostics.AddRange(bodySnapshot.Diagnostics);
            }

            if (conditionSnapshot != null && conditionSnapshot.Diagnostics != null)
            {
                diagnostics.AddRange(conditionSnapshot.Diagnostics);
            }

            if (transformationSnapshot != null && transformationSnapshot.Diagnostics != null)
            {
                diagnostics.AddRange(transformationSnapshot.Diagnostics);
            }

            bool coherent = bodySnapshot != null
                && bodySnapshot.Coherent
                && conditionSnapshot != null
                && conditionSnapshot.Coherent
                && transformationSnapshot != null
                && transformationSnapshot.Coherent;

            return new BodyBiologySnapshot(
                bodySnapshot == null ? body.ActorBodyId : bodySnapshot.ActorBodyId,
                bodySnapshot == null ? body.PersonId : bodySnapshot.PersonId,
                bodySnapshot == null ? body.SpeciesDefinitionId : bodySnapshot.SpeciesId,
                bodySnapshot == null ? string.Empty : bodySnapshot.BiologicalClassificationId,
                bodySnapshot == null ? string.Empty : bodySnapshot.BodyFormId,
                body.Readiness,
                bodySnapshot,
                conditionSnapshot,
                transformationSnapshot,
                revisions,
                coherent,
                diagnostics);
        }

        private static string StepTransaction(BodyBiologyAdvanceRequest request, string step)
        {
            return $"{request.TransactionId}.{step}";
        }
    }
}
