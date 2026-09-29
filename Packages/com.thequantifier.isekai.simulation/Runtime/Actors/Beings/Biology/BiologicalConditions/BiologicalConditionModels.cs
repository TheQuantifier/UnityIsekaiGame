using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.Recovery;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.Combat;

namespace UnityIsekaiGame.Beings.Biology.BiologicalConditions
{
    public sealed class BiologicalConditionExposureRequest
    {
        public BiologicalConditionExposureRequest(
            string actorBodyId,
            string conditionDefinitionId,
            string transactionId,
            BiologicalExposureRoute route,
            float dose,
            string strainId = "",
            string sourceId = "",
            string sourceBodyId = "",
            string sourceEventId = "",
            BiologicalConditionSourceCategory sourceCategory = BiologicalConditionSourceCategory.Unknown,
            string targetAnatomyNodeId = "",
            float intensity = 1f,
            float durationSeconds = 0f,
            bool preview = false,
            string authority = "",
            string environmentId = "",
            IReadOnlyList<string> tags = null,
            long expectedBodyRevision = 0L,
            long expectedAnatomyRevision = 0L,
            long expectedConditionRevision = 0L,
            long expectedVitalRevision = 0L,
            long expectedHazardRevision = 0L,
            long expectedCompatibilityRevision = 0L)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            ConditionDefinitionId = conditionDefinitionId ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Route = route;
            Dose = Math.Max(0f, dose);
            StrainId = strainId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            SourceBodyId = sourceBodyId ?? string.Empty;
            SourceEventId = sourceEventId ?? string.Empty;
            SourceCategory = sourceCategory;
            TargetAnatomyNodeId = targetAnatomyNodeId ?? string.Empty;
            Intensity = Math.Max(0f, intensity);
            DurationSeconds = Math.Max(0f, durationSeconds);
            Preview = preview;
            Authority = authority ?? string.Empty;
            EnvironmentId = environmentId ?? string.Empty;
            Tags = tags == null ? Array.Empty<string>() : tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).ToArray();
            ExpectedBodyRevision = expectedBodyRevision;
            ExpectedAnatomyRevision = expectedAnatomyRevision;
            ExpectedConditionRevision = expectedConditionRevision;
            ExpectedVitalRevision = expectedVitalRevision;
            ExpectedHazardRevision = expectedHazardRevision;
            ExpectedCompatibilityRevision = expectedCompatibilityRevision;
        }

        public string ActorBodyId { get; }
        public string ConditionDefinitionId { get; }
        public string TransactionId { get; }
        public BiologicalExposureRoute Route { get; }
        public float Dose { get; }
        public string StrainId { get; }
        public string SourceId { get; }
        public string SourceBodyId { get; }
        public string SourceEventId { get; }
        public BiologicalConditionSourceCategory SourceCategory { get; }
        public string TargetAnatomyNodeId { get; }
        public float Intensity { get; }
        public float DurationSeconds { get; }
        public bool Preview { get; }
        public string Authority { get; }
        public string EnvironmentId { get; }
        public IReadOnlyList<string> Tags { get; }
        public long ExpectedBodyRevision { get; }
        public long ExpectedAnatomyRevision { get; }
        public long ExpectedConditionRevision { get; }
        public long ExpectedVitalRevision { get; }
        public long ExpectedHazardRevision { get; }
        public long ExpectedCompatibilityRevision { get; }
        public BiologicalConditionExposureRequest AsPreview()
        {
            return new BiologicalConditionExposureRequest(ActorBodyId, ConditionDefinitionId, TransactionId, Route, Dose, StrainId, SourceId, SourceBodyId, SourceEventId, SourceCategory, TargetAnatomyNodeId, Intensity, DurationSeconds, true, Authority, EnvironmentId, Tags, ExpectedBodyRevision, ExpectedAnatomyRevision, ExpectedConditionRevision, ExpectedVitalRevision, ExpectedHazardRevision, ExpectedCompatibilityRevision);
        }
    }

    public readonly struct BiologicalConditionTickRequest
    {
        public BiologicalConditionTickRequest(string actorBodyId, float elapsedGameSeconds, string transactionId, bool preview = false, string reason = "")
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            ElapsedGameSeconds = Math.Max(0f, elapsedGameSeconds);
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            Reason = reason ?? string.Empty;
        }

        public string ActorBodyId { get; }
        public float ElapsedGameSeconds { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public string Reason { get; }
    }

    public readonly struct BiologicalConditionTreatmentRequest
    {
        public BiologicalConditionTreatmentRequest(string actorBodyId, string instanceId, string treatmentDefinitionId, string transactionId, float dose = 1f, bool preview = false, string sourceId = "")
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            TreatmentDefinitionId = treatmentDefinitionId ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Dose = Math.Max(0f, dose);
            Preview = preview;
            SourceId = sourceId ?? string.Empty;
        }

        public string ActorBodyId { get; }
        public string InstanceId { get; }
        public string TreatmentDefinitionId { get; }
        public string TransactionId { get; }
        public float Dose { get; }
        public bool Preview { get; }
        public string SourceId { get; }
    }

    public sealed class BiologicalConditionTransmissionRequest
    {
        public BiologicalConditionTransmissionRequest(string sourceActorBodyId, string targetActorBodyId, string instanceId, string transmissionProfileId, string transactionId, bool preview = true)
        {
            SourceActorBodyId = sourceActorBodyId ?? string.Empty;
            TargetActorBodyId = targetActorBodyId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            TransmissionProfileId = transmissionProfileId ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
        }

        public string SourceActorBodyId { get; }
        public string TargetActorBodyId { get; }
        public string InstanceId { get; }
        public string TransmissionProfileId { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
    }

    public sealed class BiologicalConditionResult
    {
        private BiologicalConditionResult(bool succeeded, BiologicalConditionResultCode code, string message, bool preview, bool duplicate, string instanceId, float effectiveDose, BiologicalInteractionEvaluationResult compatibility, BiologicalConditionRuntimeSnapshot snapshot)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            InstanceId = instanceId ?? string.Empty;
            EffectiveDose = Math.Max(0f, effectiveDose);
            Compatibility = compatibility;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public BiologicalConditionResultCode Code { get; }
        public string Message { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string InstanceId { get; }
        public float EffectiveDose { get; }
        public BiologicalInteractionEvaluationResult Compatibility { get; }
        public BiologicalConditionRuntimeSnapshot Snapshot { get; }

        public static BiologicalConditionResult Success(string message, string instanceId, float effectiveDose, BiologicalInteractionEvaluationResult compatibility, BiologicalConditionRuntimeSnapshot snapshot, bool preview = false, bool duplicate = false)
        {
            return new BiologicalConditionResult(true, duplicate ? BiologicalConditionResultCode.Duplicate : preview ? BiologicalConditionResultCode.Preview : BiologicalConditionResultCode.Success, message, preview, duplicate, instanceId, effectiveDose, compatibility, snapshot);
        }

        public static BiologicalConditionResult Failure(BiologicalConditionResultCode code, string message, BiologicalConditionRuntimeSnapshot snapshot = null, BiologicalInteractionEvaluationResult compatibility = null)
        {
            return new BiologicalConditionResult(false, code, message, false, false, string.Empty, 0f, compatibility, snapshot);
        }
    }

    public sealed class BiologicalConditionTickResult
    {
        private BiologicalConditionTickResult(bool succeeded, BiologicalConditionResultCode code, string message, BiologicalConditionTickRequest request, IReadOnlyList<BiologicalConditionConsequencePlanSnapshot> consequences, bool preview, bool duplicate, BiologicalConditionRuntimeSnapshot snapshot)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Request = request;
            Consequences = consequences == null ? Array.Empty<BiologicalConditionConsequencePlanSnapshot>() : consequences.ToArray();
            Preview = preview;
            Duplicate = duplicate;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public BiologicalConditionResultCode Code { get; }
        public string Message { get; }
        public BiologicalConditionTickRequest Request { get; }
        public IReadOnlyList<BiologicalConditionConsequencePlanSnapshot> Consequences { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public BiologicalConditionRuntimeSnapshot Snapshot { get; }

        public static BiologicalConditionTickResult Success(BiologicalConditionTickRequest request, IReadOnlyList<BiologicalConditionConsequencePlanSnapshot> consequences, BiologicalConditionRuntimeSnapshot snapshot, bool duplicate = false)
        {
            return new BiologicalConditionTickResult(true, duplicate ? BiologicalConditionResultCode.Duplicate : request.Preview ? BiologicalConditionResultCode.Preview : BiologicalConditionResultCode.Success, duplicate ? "Duplicate biological condition tick ignored." : request.Preview ? "Biological condition tick preview resolved." : "Biological condition tick applied.", request, consequences, request.Preview, duplicate, snapshot);
        }

        public static BiologicalConditionTickResult Failure(BiologicalConditionTickRequest request, BiologicalConditionResultCode code, string message, BiologicalConditionRuntimeSnapshot snapshot = null)
        {
            return new BiologicalConditionTickResult(false, code, message, request, Array.Empty<BiologicalConditionConsequencePlanSnapshot>(), false, false, snapshot);
        }
    }

    public sealed class BiologicalConditionTransmissionPlan
    {
        public BiologicalConditionTransmissionPlan(string transmissionProfileId, string sourceActorBodyId, string targetActorBodyId, BiologicalConditionExposureRequest exposureRequest, string message)
        {
            TransmissionProfileId = transmissionProfileId ?? string.Empty;
            SourceActorBodyId = sourceActorBodyId ?? string.Empty;
            TargetActorBodyId = targetActorBodyId ?? string.Empty;
            ExposureRequest = exposureRequest;
            Message = message ?? string.Empty;
        }

        public string TransmissionProfileId { get; }
        public string SourceActorBodyId { get; }
        public string TargetActorBodyId { get; }
        public BiologicalConditionExposureRequest ExposureRequest { get; }
        public string Message { get; }
    }

    public sealed class BiologicalConditionConsequenceExecutionRequest
    {
        public BiologicalConditionConsequenceExecutionRequest(
            BiologicalConditionTickRequest tick,
            BodySnapshot body,
            BiologicalCompatibilityRuntime compatibility,
            VitalProcessRuntime vitalProcesses,
            BiologicalHazardRuntime hazards,
            BiologicalRecoveryRuntime recovery,
            IDamageHealingService damageHealing,
            GameObject damageTargetObject,
            GameObject damageSourceObject = null,
            string damageTargetActorId = "",
            string damageSourceActorId = "",
            bool restoring = false)
        {
            Tick = tick;
            Body = body;
            Compatibility = compatibility;
            VitalProcesses = vitalProcesses;
            Hazards = hazards;
            Recovery = recovery;
            DamageHealing = damageHealing;
            DamageTargetObject = damageTargetObject;
            DamageSourceObject = damageSourceObject;
            DamageTargetActorId = damageTargetActorId ?? string.Empty;
            DamageSourceActorId = damageSourceActorId ?? string.Empty;
            Restoring = restoring;
        }

        public BiologicalConditionTickRequest Tick { get; }
        public BodySnapshot Body { get; }
        public BiologicalCompatibilityRuntime Compatibility { get; }
        public VitalProcessRuntime VitalProcesses { get; }
        public BiologicalHazardRuntime Hazards { get; }
        public BiologicalRecoveryRuntime Recovery { get; }
        public IDamageHealingService DamageHealing { get; }
        public GameObject DamageTargetObject { get; }
        public GameObject DamageSourceObject { get; }
        public string DamageTargetActorId { get; }
        public string DamageSourceActorId { get; }
        public bool Restoring { get; }

        public BiologicalConditionConsequenceExecutionRequest AsPreview()
        {
            return new BiologicalConditionConsequenceExecutionRequest(
                new BiologicalConditionTickRequest(Tick.ActorBodyId, Tick.ElapsedGameSeconds, Tick.TransactionId, preview: true, Tick.Reason),
                Body,
                Compatibility,
                VitalProcesses,
                Hazards,
                Recovery,
                DamageHealing,
                DamageTargetObject,
                DamageSourceObject,
                DamageTargetActorId,
                DamageSourceActorId,
                Restoring);
        }
    }

    public sealed class BiologicalConditionConsequenceExecutionResult
    {
        private BiologicalConditionConsequenceExecutionResult(
            bool succeeded,
            BiologicalConditionResultCode code,
            string message,
            bool preview,
            bool duplicate,
            BiologicalConditionTickResult conditionTick,
            IReadOnlyList<VitalResourceMutationResult> vitalResults,
            IReadOnlyList<BiologicalHazardOperationResult> hazardResults,
            IReadOnlyList<BiologicalRecoveryResult> recoveryResults,
            IReadOnlyList<DamageApplicationResult> damageResults)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            ConditionTick = conditionTick;
            VitalResults = vitalResults == null ? Array.Empty<VitalResourceMutationResult>() : vitalResults.ToArray();
            HazardResults = hazardResults == null ? Array.Empty<BiologicalHazardOperationResult>() : hazardResults.ToArray();
            RecoveryResults = recoveryResults == null ? Array.Empty<BiologicalRecoveryResult>() : recoveryResults.ToArray();
            DamageResults = damageResults == null ? Array.Empty<DamageApplicationResult>() : damageResults.ToArray();
        }

        public bool Succeeded { get; }
        public BiologicalConditionResultCode Code { get; }
        public string Message { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public BiologicalConditionTickResult ConditionTick { get; }
        public IReadOnlyList<VitalResourceMutationResult> VitalResults { get; }
        public IReadOnlyList<BiologicalHazardOperationResult> HazardResults { get; }
        public IReadOnlyList<BiologicalRecoveryResult> RecoveryResults { get; }
        public IReadOnlyList<DamageApplicationResult> DamageResults { get; }

        public static BiologicalConditionConsequenceExecutionResult Success(
            string message,
            bool preview,
            bool duplicate,
            BiologicalConditionTickResult conditionTick,
            IReadOnlyList<VitalResourceMutationResult> vitalResults,
            IReadOnlyList<BiologicalHazardOperationResult> hazardResults,
            IReadOnlyList<BiologicalRecoveryResult> recoveryResults,
            IReadOnlyList<DamageApplicationResult> damageResults)
        {
            return new BiologicalConditionConsequenceExecutionResult(true, duplicate ? BiologicalConditionResultCode.Duplicate : preview ? BiologicalConditionResultCode.Preview : BiologicalConditionResultCode.Success, message, preview, duplicate, conditionTick, vitalResults, hazardResults, recoveryResults, damageResults);
        }

        public static BiologicalConditionConsequenceExecutionResult Failure(
            BiologicalConditionResultCode code,
            string message,
            bool preview,
            BiologicalConditionTickResult conditionTick = null,
            IReadOnlyList<VitalResourceMutationResult> vitalResults = null,
            IReadOnlyList<BiologicalHazardOperationResult> hazardResults = null,
            IReadOnlyList<BiologicalRecoveryResult> recoveryResults = null,
            IReadOnlyList<DamageApplicationResult> damageResults = null)
        {
            return new BiologicalConditionConsequenceExecutionResult(false, code, message, preview, duplicate: false, conditionTick, vitalResults, hazardResults, recoveryResults, damageResults);
        }
    }
}
