using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.Combat;

namespace UnityIsekaiGame.Beings.Biology.Hazards
{
    public static class BiologicalHazardIds
    {
        public const string Bleeding = "hazard.biology.bleeding";
        public const string Suffocation = "hazard.biology.suffocation";
        public const string Overheating = "hazard.biology.overheating";
        public const string Hypothermia = "hazard.biology.hypothermia";
        public const string Starvation = "hazard.biology.starvation";
        public const string Dehydration = "hazard.biology.dehydration";
        public const string ExtremeFatigue = "hazard.biology.extreme-fatigue";
        public const string SleepDeprivation = "hazard.biology.sleep-deprivation";
        public const string EnvironmentalExposure = "hazard.environment.exposure";
    }

    public static class BiologicalExposureIds
    {
        public const string BreathableAirUnavailable = "exposure.environment.breathable-air-unavailable";
        public const string Heat = "exposure.environment.heat";
        public const string Cold = "exposure.environment.cold";
        public const string GeneralExposure = "exposure.environment.general";
    }

    public enum BiologicalHazardReadinessState
    {
        Uninitialized,
        WaitingForBody,
        WaitingForVitals,
        Ready,
        Restoring,
        Invalid,
        Disposed
    }

    public enum BiologicalHazardSeverity
    {
        Trace,
        Minor,
        Moderate,
        Serious,
        Severe,
        Critical,
        Catastrophic
    }

    public enum BiologicalHazardSourceCategory
    {
        Injury,
        VitalProcess,
        Environment,
        Condition,
        Ability,
        Equipment,
        System
    }

    public enum BiologicalHazardStackingPolicy
    {
        Independent,
        MergeSources,
        AdditiveRate,
        StrongestSource,
        MaximumSeverity,
        RefreshDuration,
        ReplaceSameSource,
        NonStacking
    }

    public enum BiologicalHazardSuppressionMode
    {
        RateMultiplier,
        Pause,
        Remove
    }

    public enum BiologicalHazardResultCode
    {
        Success,
        Preview,
        Duplicate,
        RuntimeNotReady,
        MissingActorBody,
        MissingVitalProcesses,
        MissingHazardDefinition,
        MissingCompatibility,
        MissingInteraction,
        MissingSource,
        InactiveResource,
        InvalidRequest,
        InvalidAmount,
        StaleBody,
        InvalidRestore,
        NoActiveHazards,
        Step6DamageDeferred,
        LifecycleEvaluationRequested
    }

    public enum BiologicalHazardTickConsequenceKind
    {
        None,
        VitalResourceMutation,
        Step6DamagePlan,
        LifecycleEvaluationRequest
    }

    public enum BiologicalHazardLifecycleRequestKind
    {
        None,
        EvaluatePressure,
        UnconsciousnessPressure,
        DeathPressure
    }

    public readonly struct BiologicalHazardSourceRequest
    {
        public BiologicalHazardSourceRequest(
            string actorBodyId,
            string hazardDefinitionId,
            string sourceContributionId,
            BiologicalHazardSourceCategory sourceCategory,
            BiologicalHazardSeverity severity,
            float rateMultiplier = 1f,
            float durationSeconds = 0f,
            string sourceObjectId = "",
            string reason = "")
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            HazardDefinitionId = hazardDefinitionId ?? string.Empty;
            SourceContributionId = sourceContributionId ?? string.Empty;
            SourceCategory = sourceCategory;
            Severity = severity;
            RateMultiplier = rateMultiplier;
            DurationSeconds = durationSeconds;
            SourceObjectId = sourceObjectId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string ActorBodyId { get; }
        public string HazardDefinitionId { get; }
        public string SourceContributionId { get; }
        public BiologicalHazardSourceCategory SourceCategory { get; }
        public BiologicalHazardSeverity Severity { get; }
        public float RateMultiplier { get; }
        public float DurationSeconds { get; }
        public string SourceObjectId { get; }
        public string Reason { get; }
    }

    public readonly struct BiologicalHazardSuppressionRequest
    {
        public BiologicalHazardSuppressionRequest(string actorBodyId, string hazardDefinitionId, string sourceContributionId, BiologicalHazardSuppressionMode mode, float rateMultiplier, string reason = "")
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            HazardDefinitionId = hazardDefinitionId ?? string.Empty;
            SourceContributionId = sourceContributionId ?? string.Empty;
            Mode = mode;
            RateMultiplier = rateMultiplier;
            Reason = reason ?? string.Empty;
        }

        public string ActorBodyId { get; }
        public string HazardDefinitionId { get; }
        public string SourceContributionId { get; }
        public BiologicalHazardSuppressionMode Mode { get; }
        public float RateMultiplier { get; }
        public string Reason { get; }
    }

    public readonly struct BiologicalHazardTickRequest
    {
        public BiologicalHazardTickRequest(string actorBodyId, float elapsedGameSeconds, string transactionId, bool preview = false, string reason = "")
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            ElapsedGameSeconds = elapsedGameSeconds;
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

    public sealed class BiologicalHazardDamagePlan
    {
        public BiologicalHazardDamagePlan(string hazardDefinitionId, string transactionId, string targetActorBodyId, DamageTypeDefinition damageType, float requestedAmount, string reason)
        {
            HazardDefinitionId = hazardDefinitionId ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            TargetActorBodyId = targetActorBodyId ?? string.Empty;
            DamageType = damageType;
            RequestedAmount = Math.Max(0f, requestedAmount);
            Reason = reason ?? string.Empty;
        }

        public string HazardDefinitionId { get; }
        public string TransactionId { get; }
        public string TargetActorBodyId { get; }
        public DamageTypeDefinition DamageType { get; }
        public float RequestedAmount { get; }
        public string Reason { get; }
    }

    public sealed class BiologicalHazardConsequence
    {
        public BiologicalHazardConsequence(BiologicalHazardTickConsequenceKind kind, string hazardDefinitionId, string resourceId, VitalResourceMutationResult vitalResult, BiologicalHazardDamagePlan damagePlan, BiologicalHazardLifecycleRequestKind lifecycleRequest, string message)
        {
            Kind = kind;
            HazardDefinitionId = hazardDefinitionId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            VitalResult = vitalResult;
            DamagePlan = damagePlan;
            LifecycleRequest = lifecycleRequest;
            Message = message ?? string.Empty;
        }

        public BiologicalHazardTickConsequenceKind Kind { get; }
        public string HazardDefinitionId { get; }
        public string ResourceId { get; }
        public VitalResourceMutationResult VitalResult { get; }
        public BiologicalHazardDamagePlan DamagePlan { get; }
        public BiologicalHazardLifecycleRequestKind LifecycleRequest { get; }
        public string Message { get; }
    }

    public sealed class BiologicalHazardOperationResult
    {
        private BiologicalHazardOperationResult(bool succeeded, BiologicalHazardResultCode code, string message, bool preview, bool duplicate, BiologicalHazardSnapshot snapshot)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public BiologicalHazardResultCode Code { get; }
        public string Message { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public BiologicalHazardSnapshot Snapshot { get; }

        public static BiologicalHazardOperationResult Success(string message, BiologicalHazardSnapshot snapshot, bool preview = false, bool duplicate = false)
        {
            return new BiologicalHazardOperationResult(true, duplicate ? BiologicalHazardResultCode.Duplicate : preview ? BiologicalHazardResultCode.Preview : BiologicalHazardResultCode.Success, message, preview, duplicate, snapshot);
        }

        public static BiologicalHazardOperationResult Failure(BiologicalHazardResultCode code, string message, BiologicalHazardSnapshot snapshot = null)
        {
            return new BiologicalHazardOperationResult(false, code, message, false, false, snapshot);
        }
    }

    public sealed class BiologicalHazardTickResult
    {
        private BiologicalHazardTickResult(bool succeeded, BiologicalHazardResultCode code, string message, BiologicalHazardTickRequest request, IReadOnlyList<BiologicalHazardConsequence> consequences, bool preview, bool duplicate, BiologicalHazardSnapshot snapshot)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Request = request;
            Consequences = consequences == null ? Array.Empty<BiologicalHazardConsequence>() : consequences.ToArray();
            Preview = preview;
            Duplicate = duplicate;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public BiologicalHazardResultCode Code { get; }
        public string Message { get; }
        public BiologicalHazardTickRequest Request { get; }
        public IReadOnlyList<BiologicalHazardConsequence> Consequences { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public BiologicalHazardSnapshot Snapshot { get; }
        public IReadOnlyList<BiologicalHazardDamagePlan> DamagePlans => Consequences.Where(consequence => consequence.DamagePlan != null).Select(consequence => consequence.DamagePlan).ToArray();
        public bool HasLifecyclePressure => Consequences.Any(consequence => consequence.LifecycleRequest != BiologicalHazardLifecycleRequestKind.None);

        public static BiologicalHazardTickResult Success(BiologicalHazardTickRequest request, IReadOnlyList<BiologicalHazardConsequence> consequences, BiologicalHazardSnapshot snapshot, bool duplicate = false)
        {
            string message = duplicate ? "Duplicate biological hazard tick." : request.Preview ? "Biological hazard tick preview resolved." : "Biological hazard tick applied.";
            return new BiologicalHazardTickResult(true, duplicate ? BiologicalHazardResultCode.Duplicate : request.Preview ? BiologicalHazardResultCode.Preview : BiologicalHazardResultCode.Success, message, request, consequences, request.Preview, duplicate, snapshot);
        }

        public static BiologicalHazardTickResult Failure(BiologicalHazardTickRequest request, BiologicalHazardResultCode code, string message, BiologicalHazardSnapshot snapshot = null)
        {
            return new BiologicalHazardTickResult(false, code, message, request, Array.Empty<BiologicalHazardConsequence>(), false, false, snapshot);
        }
    }
}
