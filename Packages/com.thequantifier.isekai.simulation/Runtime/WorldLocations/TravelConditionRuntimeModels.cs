using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    [Serializable]
    public sealed class TravelConditionTargetReferenceData
    {
        public TravelConditionTargetScope scope = TravelConditionTargetScope.RouteSegment;
        public string targetId;
        public string sourceLocationId;
        public string destinationLocationId;
        public RouteEdgeKind edgeKind;
        public string routeNetworkId;
        public string journeyId;
        public EntityLocationReferenceData traveler;

        public string StableKey => $"{scope}:{N(targetId)}:{N(sourceLocationId)}:{N(destinationLocationId)}:{edgeKind}:{N(routeNetworkId)}:{N(journeyId)}:{traveler?.StableKey ?? string.Empty}";

        public TravelConditionTargetReferenceData Clone()
        {
            return new TravelConditionTargetReferenceData
            {
                scope = scope,
                targetId = N(targetId),
                sourceLocationId = N(sourceLocationId),
                destinationLocationId = N(destinationLocationId),
                edgeKind = edgeKind,
                routeNetworkId = N(routeNetworkId),
                journeyId = N(journeyId),
                traveler = traveler?.Clone()
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class TravelConditionRecordData
    {
        public string conditionId;
        public string conditionDefinitionId;
        public string worldId;
        public TravelConditionTargetReferenceData target;
        public TravelConditionLifecycleState lifecycleState = TravelConditionLifecycleState.Active;
        public TravelConditionSeverity severity = TravelConditionSeverity.Minor;
        public TravelConditionVisibility visibility = TravelConditionVisibility.Public;
        public double movementRateMultiplier = 1d;
        public double routeCostMultiplier = 1d;
        public bool hardBlocksTravel;
        public string[] additionalRequiredCapabilityIds = Array.Empty<string>();
        public string[] additionalRequiredEquipmentDefinitionIds = Array.Empty<string>();
        public double startsWorldTime;
        public double endsWorldTime = -1d;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public TravelConditionRecordData Clone()
        {
            return new TravelConditionRecordData
            {
                conditionId = N(conditionId),
                conditionDefinitionId = N(conditionDefinitionId),
                worldId = N(worldId),
                target = target?.Clone(),
                lifecycleState = lifecycleState,
                severity = severity,
                visibility = visibility,
                movementRateMultiplier = movementRateMultiplier,
                routeCostMultiplier = routeCostMultiplier,
                hardBlocksTravel = hardBlocksTravel,
                additionalRequiredCapabilityIds = C(additionalRequiredCapabilityIds),
                additionalRequiredEquipmentDefinitionIds = C(additionalRequiredEquipmentDefinitionIds),
                startsWorldTime = startsWorldTime,
                endsWorldTime = endsWorldTime,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class TravelHazardExposureRecordData
    {
        public string hazardExposureId;
        public string hazardDefinitionId;
        public string sourceConditionId;
        public string worldId;
        public TravelConditionTargetReferenceData target;
        public EntityLocationReferenceData traveler;
        public TravelHazardExposureLifecycleState lifecycleState = TravelHazardExposureLifecycleState.Potential;
        public TravelHazardOutcome outcome = TravelHazardOutcome.None;
        public double createdWorldTime;
        public double triggeredWorldTime = -1d;
        public double resolvedWorldTime = -1d;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public TravelHazardExposureRecordData Clone()
        {
            return new TravelHazardExposureRecordData
            {
                hazardExposureId = N(hazardExposureId),
                hazardDefinitionId = N(hazardDefinitionId),
                sourceConditionId = N(sourceConditionId),
                worldId = N(worldId),
                target = target?.Clone(),
                traveler = traveler?.Clone(),
                lifecycleState = lifecycleState,
                outcome = outcome,
                createdWorldTime = createdWorldTime,
                triggeredWorldTime = triggeredWorldTime,
                resolvedWorldTime = resolvedWorldTime,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class TravelEncounterRecordData
    {
        public string encounterId;
        public string encounterDefinitionId;
        public string sourceConditionId;
        public string worldId;
        public TravelConditionTargetReferenceData target;
        public string journeyId;
        public EntityLocationReferenceData traveler;
        public string[] participantReferenceKeys = Array.Empty<string>();
        public TravelEncounterLifecycleState lifecycleState = TravelEncounterLifecycleState.Opportunity;
        public TravelEncounterResolution resolution = TravelEncounterResolution.None;
        public double createdWorldTime;
        public double triggeredWorldTime = -1d;
        public double resolvedWorldTime = -1d;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public TravelEncounterRecordData Clone()
        {
            return new TravelEncounterRecordData
            {
                encounterId = N(encounterId),
                encounterDefinitionId = N(encounterDefinitionId),
                sourceConditionId = N(sourceConditionId),
                worldId = N(worldId),
                target = target?.Clone(),
                journeyId = N(journeyId),
                traveler = traveler?.Clone(),
                participantReferenceKeys = C(participantReferenceKeys),
                lifecycleState = lifecycleState,
                resolution = resolution,
                createdWorldTime = createdWorldTime,
                triggeredWorldTime = triggeredWorldTime,
                resolvedWorldTime = resolvedWorldTime,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class TravelConditionTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string targetId;
        public string resultReferenceId;
        public long revision;

        public TravelConditionTransactionRecordData Clone()
        {
            return new TravelConditionTransactionRecordData { transactionId = N(transactionId), operation = N(operation), targetId = N(targetId), resultReferenceId = N(resultReferenceId), revision = revision };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class TravelConditionRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public TravelConditionRecordData[] conditions = Array.Empty<TravelConditionRecordData>();
        public TravelHazardExposureRecordData[] hazardExposures = Array.Empty<TravelHazardExposureRecordData>();
        public TravelEncounterRecordData[] encounters = Array.Empty<TravelEncounterRecordData>();
        public TravelConditionTransactionRecordData[] transactions = Array.Empty<TravelConditionTransactionRecordData>();

        public TravelConditionRuntimeSaveData Clone()
        {
            return new TravelConditionRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = N(worldId),
                revision = revision,
                conditions = (conditions ?? Array.Empty<TravelConditionRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                hazardExposures = (hazardExposures ?? Array.Empty<TravelHazardExposureRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                encounters = (encounters ?? Array.Empty<TravelEncounterRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                transactions = (transactions ?? Array.Empty<TravelConditionTransactionRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray()
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class TravelConditionSnapshot
    {
        private readonly TravelConditionRecordData data;
        public TravelConditionSnapshot(TravelConditionRecordData record) { data = record?.Clone() ?? new TravelConditionRecordData(); }
        public string ConditionId => data.conditionId ?? string.Empty;
        public string ConditionDefinitionId => data.conditionDefinitionId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public TravelConditionTargetReferenceData Target => data.target?.Clone();
        public TravelConditionLifecycleState LifecycleState => data.lifecycleState;
        public TravelConditionSeverity Severity => data.severity;
        public TravelConditionVisibility Visibility => data.visibility;
        public double MovementRateMultiplier => data.movementRateMultiplier;
        public double RouteCostMultiplier => data.routeCostMultiplier;
        public bool HardBlocksTravel => data.hardBlocksTravel;
        public IReadOnlyList<string> AdditionalRequiredCapabilityIds => (data.additionalRequiredCapabilityIds ?? Array.Empty<string>()).ToArray();
        public IReadOnlyList<string> AdditionalRequiredEquipmentDefinitionIds => (data.additionalRequiredEquipmentDefinitionIds ?? Array.Empty<string>()).ToArray();
        public double StartsWorldTime => data.startsWorldTime;
        public double EndsWorldTime => data.endsWorldTime;
        public string SourceEventId => data.sourceEventId ?? string.Empty;
        public string SourceRecordId => data.sourceRecordId ?? string.Empty;
        public string ProvenanceId => data.provenanceId ?? string.Empty;
        public long Revision => data.revision;
        public TravelConditionRecordData ToSaveData() => data.Clone();
    }

    public sealed class TravelHazardExposureSnapshot
    {
        private readonly TravelHazardExposureRecordData data;
        public TravelHazardExposureSnapshot(TravelHazardExposureRecordData record) { data = record?.Clone() ?? new TravelHazardExposureRecordData(); }
        public string HazardExposureId => data.hazardExposureId ?? string.Empty;
        public string HazardDefinitionId => data.hazardDefinitionId ?? string.Empty;
        public string SourceConditionId => data.sourceConditionId ?? string.Empty;
        public TravelConditionTargetReferenceData Target => data.target?.Clone();
        public EntityLocationReferenceData Traveler => data.traveler?.Clone();
        public TravelHazardExposureLifecycleState LifecycleState => data.lifecycleState;
        public TravelHazardOutcome Outcome => data.outcome;
        public long Revision => data.revision;
        public TravelHazardExposureRecordData ToSaveData() => data.Clone();
    }

    public sealed class TravelEncounterSnapshot
    {
        private readonly TravelEncounterRecordData data;
        public TravelEncounterSnapshot(TravelEncounterRecordData record) { data = record?.Clone() ?? new TravelEncounterRecordData(); }
        public string EncounterId => data.encounterId ?? string.Empty;
        public string EncounterDefinitionId => data.encounterDefinitionId ?? string.Empty;
        public string SourceConditionId => data.sourceConditionId ?? string.Empty;
        public TravelConditionTargetReferenceData Target => data.target?.Clone();
        public string JourneyId => data.journeyId ?? string.Empty;
        public EntityLocationReferenceData Traveler => data.traveler?.Clone();
        public IReadOnlyList<string> ParticipantReferenceKeys => (data.participantReferenceKeys ?? Array.Empty<string>()).ToArray();
        public TravelEncounterLifecycleState LifecycleState => data.lifecycleState;
        public TravelEncounterResolution Resolution => data.resolution;
        public long Revision => data.revision;
        public TravelEncounterRecordData ToSaveData() => data.Clone();
    }

    public sealed class TravelConditionApplicableSnapshot
    {
        public TravelConditionApplicableSnapshot(TravelConditionSnapshot condition, string displayName, TravelConditionCategory category, TravelConditionSeverity severity, bool redacted)
        {
            Condition = condition;
            DisplayName = displayName ?? string.Empty;
            Category = category;
            Severity = severity;
            Redacted = redacted;
        }

        public TravelConditionSnapshot Condition { get; }
        public string DisplayName { get; }
        public TravelConditionCategory Category { get; }
        public TravelConditionSeverity Severity { get; }
        public bool Redacted { get; }
    }

    public sealed class TravelConditionEncounterRiskSummary
    {
        public TravelConditionEncounterRiskSummary(IEnumerable<string> visibleEncounterDefinitionIds, IEnumerable<string> hiddenKnownEncounterDefinitionIds)
        {
            VisibleEncounterDefinitionIds = C(visibleEncounterDefinitionIds);
            HiddenKnownEncounterDefinitionIds = C(hiddenKnownEncounterDefinitionIds);
            VisibleEncounterCount = VisibleEncounterDefinitionIds.Count;
        }

        public IReadOnlyList<string> VisibleEncounterDefinitionIds { get; }
        public IReadOnlyList<string> HiddenKnownEncounterDefinitionIds { get; }
        public int VisibleEncounterCount { get; }
        public bool HasVisibleRisk => VisibleEncounterCount > 0;
        private static IReadOnlyList<string> C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public sealed class TravelConditionEvaluationResult
    {
        public TravelConditionEvaluationResult(bool succeeded, bool hardBlocked, double movementRateMultiplier, double routeCostMultiplier, IEnumerable<TravelConditionApplicableSnapshot> conditions, IEnumerable<string> requiredCapabilities, IEnumerable<string> requiredEquipment, IEnumerable<string> missingCapabilities, IEnumerable<string> missingEquipment, TravelConditionEncounterRiskSummary encounters, long sourceRevision, string diagnostics)
        {
            Succeeded = succeeded;
            HardBlocked = hardBlocked;
            MovementRateMultiplier = ClampMultiplier(movementRateMultiplier);
            RouteCostMultiplier = ClampMultiplier(routeCostMultiplier);
            ApplicableConditions = (conditions ?? Array.Empty<TravelConditionApplicableSnapshot>()).Where(value => value != null).ToArray();
            RequiredCapabilityIds = C(requiredCapabilities);
            RequiredEquipmentDefinitionIds = C(requiredEquipment);
            MissingCapabilityIds = C(missingCapabilities);
            MissingEquipmentDefinitionIds = C(missingEquipment);
            EncounterRisk = encounters ?? new TravelConditionEncounterRiskSummary(Array.Empty<string>(), Array.Empty<string>());
            SourceRevision = sourceRevision;
            Diagnostics = diagnostics ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool HardBlocked { get; }
        public double MovementRateMultiplier { get; }
        public double RouteCostMultiplier { get; }
        public IReadOnlyList<TravelConditionApplicableSnapshot> ApplicableConditions { get; }
        public IReadOnlyList<string> RequiredCapabilityIds { get; }
        public IReadOnlyList<string> RequiredEquipmentDefinitionIds { get; }
        public IReadOnlyList<string> MissingCapabilityIds { get; }
        public IReadOnlyList<string> MissingEquipmentDefinitionIds { get; }
        public TravelConditionEncounterRiskSummary EncounterRisk { get; }
        public long SourceRevision { get; }
        public string Diagnostics { get; }

        public static TravelConditionEvaluationResult Empty(long revision = 0L) => new TravelConditionEvaluationResult(true, false, 1d, 1d, Array.Empty<TravelConditionApplicableSnapshot>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new TravelConditionEncounterRiskSummary(Array.Empty<string>(), Array.Empty<string>()), revision, "No applicable travel conditions.");
        public static TravelConditionEvaluationResult Failure(string message, long revision = 0L) => new TravelConditionEvaluationResult(false, true, 1d, 1d, Array.Empty<TravelConditionApplicableSnapshot>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new TravelConditionEncounterRiskSummary(Array.Empty<string>(), Array.Empty<string>()), revision, message);
        private static double ClampMultiplier(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d ? Math.Max(0.0001d, Math.Min(999999d, value)) : 1d;
        private static IReadOnlyList<string> C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public sealed class TravelConditionOperationResult
    {
        private TravelConditionOperationResult(TravelConditionMutationStatus status, string message, long before, long after, TravelConditionSnapshot condition = null, TravelHazardExposureSnapshot hazard = null, TravelEncounterSnapshot encounter = null, TravelConditionEvaluationResult evaluation = null, bool preview = false, bool duplicate = false)
        {
            Status = status;
            Message = message ?? string.Empty;
            RevisionBefore = before;
            RevisionAfter = after;
            Condition = condition;
            HazardExposure = hazard;
            Encounter = encounter;
            Evaluation = evaluation;
            Preview = preview;
            Duplicate = duplicate;
        }

        public TravelConditionMutationStatus Status { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public TravelConditionSnapshot Condition { get; }
        public TravelHazardExposureSnapshot HazardExposure { get; }
        public TravelEncounterSnapshot Encounter { get; }
        public TravelConditionEvaluationResult Evaluation { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public bool Succeeded => Status == TravelConditionMutationStatus.Succeeded || Status == TravelConditionMutationStatus.Preview || Status == TravelConditionMutationStatus.Duplicate;
        public static TravelConditionOperationResult Success(TravelConditionSnapshot condition, string message, long before, long after, bool preview = false, bool duplicate = false) => new TravelConditionOperationResult(preview ? TravelConditionMutationStatus.Preview : duplicate ? TravelConditionMutationStatus.Duplicate : TravelConditionMutationStatus.Succeeded, message, before, after, condition, preview: preview, duplicate: duplicate);
        public static TravelConditionOperationResult HazardSuccess(TravelHazardExposureSnapshot hazard, string message, long before, long after, bool preview = false, bool duplicate = false) => new TravelConditionOperationResult(preview ? TravelConditionMutationStatus.Preview : duplicate ? TravelConditionMutationStatus.Duplicate : TravelConditionMutationStatus.Succeeded, message, before, after, hazard: hazard, preview: preview, duplicate: duplicate);
        public static TravelConditionOperationResult EncounterSuccess(TravelEncounterSnapshot encounter, string message, long before, long after, bool preview = false, bool duplicate = false) => new TravelConditionOperationResult(preview ? TravelConditionMutationStatus.Preview : duplicate ? TravelConditionMutationStatus.Duplicate : TravelConditionMutationStatus.Succeeded, message, before, after, encounter: encounter, preview: preview, duplicate: duplicate);
        public static TravelConditionOperationResult EvaluationSuccess(TravelConditionEvaluationResult evaluation, string message, long before) => new TravelConditionOperationResult(TravelConditionMutationStatus.Succeeded, message, before, before, evaluation: evaluation);
        public static TravelConditionOperationResult Failure(TravelConditionMutationStatus status, string message, long before) => new TravelConditionOperationResult(status, message, before, before);
    }

    public sealed class TravelConditionCreateRequest
    {
        public string transactionId;
        public string conditionId;
        public string conditionDefinitionId;
        public TravelConditionTargetReferenceData target;
        public TravelConditionLifecycleState lifecycleState = TravelConditionLifecycleState.Active;
        public TravelConditionSeverity severity = TravelConditionSeverity.Unknown;
        public TravelConditionVisibility visibility;
        public double movementRateMultiplier = -1d;
        public double routeCostMultiplier = -1d;
        public bool? hardBlocksTravel;
        public string[] additionalRequiredCapabilityIds = Array.Empty<string>();
        public string[] additionalRequiredEquipmentDefinitionIds = Array.Empty<string>();
        public double startsWorldTime;
        public double endsWorldTime = -1d;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public sealed class TravelConditionMutationRequest
    {
        public string transactionId;
        public string conditionId;
        public TravelConditionLifecycleState lifecycleState = TravelConditionLifecycleState.Unknown;
        public double endsWorldTime = -2d;
        public double worldTime;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public sealed class TravelConditionEvaluationRequest
    {
        public TravelConditionEvaluationMode evaluationMode = TravelConditionEvaluationMode.CurrentConditions;
        public TravelConditionTargetReferenceData target;
        public string travelModeDefinitionId;
        public EntityLocationReferenceData traveler;
        public string[] travelerCapabilityIds = Array.Empty<string>();
        public string[] travelerEquipmentDefinitionIds = Array.Empty<string>();
        public string[] knownConditionIds = Array.Empty<string>();
        public string[] knownHazardExposureIds = Array.Empty<string>();
        public string[] knownEncounterIds = Array.Empty<string>();
        public bool includeHiddenDevelopmentConditions;
        public double worldTime;
    }

    public sealed class TravelHazardTriggerRequest
    {
        public string transactionId;
        public string hazardExposureId;
        public string hazardDefinitionId;
        public string sourceConditionId;
        public TravelConditionTargetReferenceData target;
        public EntityLocationReferenceData traveler;
        public TravelHazardOutcome outcome = TravelHazardOutcome.Exposed;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public sealed class TravelEncounterTriggerRequest
    {
        public string transactionId;
        public string encounterId;
        public string encounterDefinitionId;
        public string sourceConditionId;
        public TravelConditionTargetReferenceData target;
        public string journeyId;
        public EntityLocationReferenceData traveler;
        public string[] participantReferenceKeys = Array.Empty<string>();
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
    }
}
