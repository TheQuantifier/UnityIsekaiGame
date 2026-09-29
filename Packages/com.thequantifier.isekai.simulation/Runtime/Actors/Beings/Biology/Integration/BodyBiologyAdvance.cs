using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Beings.Biology.BiologicalConditions;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.Recovery;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;

namespace UnityIsekaiGame.Beings.Biology.Integration
{
    public enum BodyBiologyAdvanceCode
    {
        Success,
        Preview,
        Duplicate,
        InvalidRequest,
        RuntimeNotReady,
        StaleSnapshot,
        ConditionFailure,
        HazardFailure,
        VitalFailure,
        RecoveryFailure
    }

    public sealed class BodyBiologyAdvanceRequest
    {
        public BodyBiologyAdvanceRequest(
            string actorBodyId,
            float elapsedGameSeconds,
            string transactionId,
            string authorityContext = "",
            IDamageHealingService damageHealing = null,
            GameObject damageTargetObject = null,
            GameObject damageSourceObject = null,
            string damageTargetActorId = "",
            string damageSourceActorId = "")
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            ElapsedGameSeconds = elapsedGameSeconds;
            TransactionId = transactionId ?? string.Empty;
            AuthorityContext = authorityContext ?? string.Empty;
            DamageHealing = damageHealing;
            DamageTargetObject = damageTargetObject;
            DamageSourceObject = damageSourceObject;
            DamageTargetActorId = damageTargetActorId ?? string.Empty;
            DamageSourceActorId = damageSourceActorId ?? string.Empty;
        }

        public string ActorBodyId { get; }
        public float ElapsedGameSeconds { get; }
        public string TransactionId { get; }
        public string AuthorityContext { get; }
        public IDamageHealingService DamageHealing { get; }
        public GameObject DamageTargetObject { get; }
        public GameObject DamageSourceObject { get; }
        public string DamageTargetActorId { get; }
        public string DamageSourceActorId { get; }
    }

    public sealed class BodyBiologyAdvanceStepResult
    {
        public BodyBiologyAdvanceStepResult(string stepId, bool succeeded, bool preview, bool duplicate, string code, string message)
        {
            StepId = stepId ?? string.Empty;
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string StepId { get; }
        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public string Code { get; }
        public string Message { get; }
    }

    public sealed class BodyBiologyAdvanceResult
    {
        private BodyBiologyAdvanceResult(
            bool succeeded,
            BodyBiologyAdvanceCode code,
            string message,
            bool preview,
            bool duplicate,
            BodyBiologySnapshot before,
            BodyBiologySnapshot after,
            IReadOnlyList<BodyBiologyAdvanceStepResult> steps)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            Before = before;
            After = after;
            Steps = steps == null ? Array.Empty<BodyBiologyAdvanceStepResult>() : steps.ToArray();
        }

        public bool Succeeded { get; }
        public BodyBiologyAdvanceCode Code { get; }
        public string Message { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public BodyBiologySnapshot Before { get; }
        public BodyBiologySnapshot After { get; }
        public IReadOnlyList<BodyBiologyAdvanceStepResult> Steps { get; }

        public static BodyBiologyAdvanceResult Success(string message, bool preview, BodyBiologySnapshot before, BodyBiologySnapshot after, IReadOnlyList<BodyBiologyAdvanceStepResult> steps)
        {
            bool duplicate = steps != null && steps.Count > 0 && steps.All(step => step.Duplicate || step.Preview);
            return new BodyBiologyAdvanceResult(true, duplicate && !preview ? BodyBiologyAdvanceCode.Duplicate : preview ? BodyBiologyAdvanceCode.Preview : BodyBiologyAdvanceCode.Success, message, preview, duplicate && !preview, before, after, steps);
        }

        public static BodyBiologyAdvanceResult Failure(BodyBiologyAdvanceCode code, string message, bool preview, BodyBiologySnapshot before, BodyBiologySnapshot after, IReadOnlyList<BodyBiologyAdvanceStepResult> steps)
        {
            return new BodyBiologyAdvanceResult(false, code, message, preview, false, before, after, steps);
        }
    }

    public static class BodyBiologyAdvanceSteps
    {
        public const string Conditions = "biological-conditions";
        public const string Hazards = "hazards";
        public const string Vitals = "vital-processes";
        public const string Recovery = "recovery";

        public static BodyBiologyAdvanceStepResult FromConditions(BiologicalConditionConsequenceExecutionResult result)
        {
            return new BodyBiologyAdvanceStepResult(Conditions, result != null && result.Succeeded, result != null && result.Preview, result != null && result.Duplicate, result == null ? "MissingResult" : result.Code.ToString(), result == null ? "Biological Condition consequence result is missing." : result.Message);
        }

        public static BodyBiologyAdvanceStepResult FromHazards(BiologicalHazardTickResult result)
        {
            return new BodyBiologyAdvanceStepResult(Hazards, result != null && result.Succeeded, result != null && result.Preview, result != null && result.Duplicate, result == null ? "MissingResult" : result.Code.ToString(), result == null ? "Biological Hazard result is missing." : result.Message);
        }

        public static BodyBiologyAdvanceStepResult FromVitals(VitalResourceMutationResult result)
        {
            return new BodyBiologyAdvanceStepResult(Vitals, result != null && result.Succeeded, result != null && result.Preview, result != null && result.Duplicate, result == null ? "MissingResult" : result.Code.ToString(), result == null ? "Vital Process result is missing." : result.Message);
        }

        public static BodyBiologyAdvanceStepResult FromRecovery(BiologicalRecoveryResult result)
        {
            return new BodyBiologyAdvanceStepResult(Recovery, result != null && result.Succeeded, result != null && result.Preview, result != null && result.Duplicate, result == null ? "MissingResult" : result.Code.ToString(), result == null ? "Biological Recovery result is missing." : result.Message);
        }
    }
}
