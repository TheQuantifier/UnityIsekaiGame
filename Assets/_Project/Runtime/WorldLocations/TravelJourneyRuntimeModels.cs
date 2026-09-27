using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    [Serializable]
    public sealed class TravelJourneyPlanSnapshotData
    {
        public string routePlanId;
        public string originLocationId;
        public string destinationLocationId;
        public EntityLocationReferenceData traveler;
        public string travelModeDefinitionId;
        public RoutePlanningObjective objective = RoutePlanningObjective.ShortestDistance;
        public string[] orderedLocationIds = Array.Empty<string>();
        public double totalDistanceMeters;
        public double totalCostUnits;
        public long routeRevision;
        public long connectionRevision;
        public long conditionRevision;
        public bool knowledgeFiltered;
        public string diagnostics;

        public TravelJourneyPlanSnapshotData Clone()
        {
            return new TravelJourneyPlanSnapshotData
            {
                routePlanId = N(routePlanId),
                originLocationId = N(originLocationId),
                destinationLocationId = N(destinationLocationId),
                traveler = traveler?.Clone(),
                travelModeDefinitionId = N(travelModeDefinitionId),
                objective = objective,
                orderedLocationIds = C(orderedLocationIds),
                totalDistanceMeters = Math.Max(0d, totalDistanceMeters),
                totalCostUnits = Math.Max(0d, totalCostUnits),
                routeRevision = routeRevision,
                connectionRevision = connectionRevision,
                conditionRevision = conditionRevision,
                knowledgeFiltered = knowledgeFiltered,
                diagnostics = diagnostics ?? string.Empty
            };
        }

        public static TravelJourneyPlanSnapshotData FromPlan(LocationRoutePlan plan)
        {
            if (plan == null) return null;
            return new TravelJourneyPlanSnapshotData
            {
                routePlanId = plan.PlanId,
                originLocationId = plan.OriginLocationId,
                destinationLocationId = plan.DestinationLocationId,
                traveler = plan.Traveler?.Clone(),
                travelModeDefinitionId = plan.TravelModeDefinitionId,
                objective = plan.Objective,
                orderedLocationIds = (plan.OrderedLocationIds ?? Array.Empty<string>()).ToArray(),
                totalDistanceMeters = plan.TotalDistance.meters,
                totalCostUnits = plan.TotalCost.units,
                routeRevision = plan.RouteRevision,
                connectionRevision = plan.ConnectionRevision,
                conditionRevision = plan.ConditionRevision,
                knowledgeFiltered = plan.KnowledgeFiltered,
                diagnostics = plan.Diagnostics
            };
        }

        public LocationRoutePlan ToRoutePlan(IEnumerable<TravelJourneyStepRecordData> steps)
        {
            LocationRoutePlanStep[] planSteps = (steps ?? Array.Empty<TravelJourneyStepRecordData>())
                .OrderBy(item => item.sequenceIndex)
                .Select(TravelJourneyStepRecordData.ToRoutePlanStep)
                .ToArray();
            return new LocationRoutePlan(
                routePlanId,
                originLocationId,
                destinationLocationId,
                traveler?.Clone(),
                travelModeDefinitionId,
                objective,
                orderedLocationIds,
                planSteps,
                new TravelDistance(totalDistanceMeters),
                new TravelCost(totalCostUnits),
                new RouteRequirementSummary { requiredActions = planSteps.SelectMany(step => step.RequiredActions ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray() },
                routeRevision,
                connectionRevision,
                knowledgeFiltered,
                diagnostics,
                conditionRevision);
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class TravelJourneyStepRecordData
    {
        public string journeyStepId;
        public string journeyId;
        public int sequenceIndex;
        public string sourceLocationId;
        public string destinationLocationId;
        public string edgeId;
        public RouteEdgeKind edgeKind = RouteEdgeKind.Unknown;
        public RouteSegmentCategory category = RouteSegmentCategory.Unknown;
        public RouteVisibility routeVisibility = RouteVisibility.Public;
        public double distanceMeters;
        public double costUnits;
        public string accessState;
        public string travelModeDefinitionId;
        public long sourceRevision;
        public string[] requiredActions = Array.Empty<string>();
        public TravelJourneyStepLifecycleState lifecycleState = TravelJourneyStepLifecycleState.Pending;
        public double startedWorldTime = -1d;
        public double completedWorldTime = -1d;
        public long completedDistanceMillimeters;
        public string supersededByJourneyStepId;
        public long revision = 1L;

        public double CompletedDistanceMeters => completedDistanceMillimeters / 1000d;
        public double ProgressFraction => distanceMeters <= 0d ? (lifecycleState == TravelJourneyStepLifecycleState.Completed ? 1d : 0d) : Math.Max(0d, Math.Min(1d, CompletedDistanceMeters / distanceMeters));

        public TravelJourneyStepRecordData Clone()
        {
            return new TravelJourneyStepRecordData
            {
                journeyStepId = N(journeyStepId),
                journeyId = N(journeyId),
                sequenceIndex = Math.Max(0, sequenceIndex),
                sourceLocationId = N(sourceLocationId),
                destinationLocationId = N(destinationLocationId),
                edgeId = N(edgeId),
                edgeKind = edgeKind,
                category = category,
                routeVisibility = routeVisibility,
                distanceMeters = Math.Max(0d, distanceMeters),
                costUnits = Math.Max(0d, costUnits),
                accessState = N(accessState),
                travelModeDefinitionId = N(travelModeDefinitionId),
                sourceRevision = sourceRevision,
                requiredActions = C(requiredActions),
                lifecycleState = lifecycleState,
                startedWorldTime = startedWorldTime,
                completedWorldTime = completedWorldTime,
                completedDistanceMillimeters = Math.Max(0L, completedDistanceMillimeters),
                supersededByJourneyStepId = N(supersededByJourneyStepId),
                revision = Math.Max(1L, revision)
            };
        }

        public static TravelJourneyStepRecordData FromPlanStep(string journeyId, int index, LocationRoutePlanStep step)
        {
            string id = $"{N(journeyId)}.step.{index:0000}.{StableSuffix(step?.EdgeId)}";
            return new TravelJourneyStepRecordData
            {
                journeyStepId = id,
                journeyId = N(journeyId),
                sequenceIndex = index,
                sourceLocationId = N(step?.SourceLocationId),
                destinationLocationId = N(step?.DestinationLocationId),
                edgeId = N(step?.EdgeId),
                edgeKind = step?.EdgeKind ?? RouteEdgeKind.Unknown,
                category = step?.Category ?? RouteSegmentCategory.Unknown,
                routeVisibility = step?.Visibility ?? RouteVisibility.Public,
                distanceMeters = Math.Max(0d, step?.Distance.meters ?? 0d),
                costUnits = Math.Max(0d, step?.Cost.units ?? 0d),
                accessState = N(step?.AccessState),
                travelModeDefinitionId = N(step?.TravelModeDefinitionId),
                sourceRevision = step?.SourceRevision ?? 0L,
                requiredActions = C(step?.RequiredActions),
                lifecycleState = index == 0 ? TravelJourneyStepLifecycleState.Ready : TravelJourneyStepLifecycleState.Pending,
                revision = 1L
            };
        }

        public static LocationRoutePlanStep ToRoutePlanStep(TravelJourneyStepRecordData data)
        {
            data ??= new TravelJourneyStepRecordData();
            return new LocationRoutePlanStep
            {
                SourceLocationId = N(data.sourceLocationId),
                DestinationLocationId = N(data.destinationLocationId),
                EdgeId = N(data.edgeId),
                EdgeKind = data.edgeKind,
                Category = data.category,
                Visibility = data.routeVisibility,
                Distance = new TravelDistance(Math.Max(0d, data.distanceMeters)),
                Cost = new TravelCost(Math.Max(0d, data.costUnits)),
                AccessState = N(data.accessState),
                TravelModeDefinitionId = N(data.travelModeDefinitionId),
                SourceRevision = data.sourceRevision,
                RequiredActions = C(data.requiredActions)
            };
        }

        private static string StableSuffix(string value)
        {
            string cleaned = N(value);
            if (string.IsNullOrWhiteSpace(cleaned)) return "edge";
            char[] chars = cleaned.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            return new string(chars).Trim('-').ToLowerInvariant();
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class TravelJourneyRecordData
    {
        public string journeyId;
        public string worldId;
        public EntityLocationReferenceData traveler;
        public EntityLocationReferenceData controller;
        public string originLocationId;
        public string destinationLocationId;
        public TravelJourneyPlanSnapshotData routePlan;
        public string travelModeDefinitionId;
        public double movementRateOverrideMetersPerSecond = -1d;
        public TravelJourneyCategory category = TravelJourneyCategory.OrdinaryTravel;
        public TravelJourneyLifecycleState lifecycleState = TravelJourneyLifecycleState.Ready;
        public TravelJourneyProgressionMode progressionMode = TravelJourneyProgressionMode.AutomaticLogical;
        public int currentStepIndex;
        public long currentStepCompletedMillimeters;
        public long completedDistanceMillimeters;
        public long totalDistanceMillimeters;
        public double createdWorldTime;
        public double startedWorldTime = -1d;
        public double lastProgressWorldTime = -1d;
        public double pausedWorldTime = -1d;
        public double endedWorldTime = -1d;
        public TravelJourneyBlockReason blockReason = TravelJourneyBlockReason.None;
        public string blockMessage;
        public int replanCount;
        public long acceptedRouteRevision;
        public long acceptedConnectionRevision;
        public TravelJourneyVisibility visibility = TravelJourneyVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public TravelJourneyRecordData Clone()
        {
            return new TravelJourneyRecordData
            {
                journeyId = N(journeyId),
                worldId = N(worldId),
                traveler = traveler?.Clone(),
                controller = controller?.Clone(),
                originLocationId = N(originLocationId),
                destinationLocationId = N(destinationLocationId),
                routePlan = routePlan?.Clone(),
                travelModeDefinitionId = N(travelModeDefinitionId),
                movementRateOverrideMetersPerSecond = movementRateOverrideMetersPerSecond,
                category = category,
                lifecycleState = lifecycleState,
                progressionMode = progressionMode,
                currentStepIndex = Math.Max(0, currentStepIndex),
                currentStepCompletedMillimeters = Math.Max(0L, currentStepCompletedMillimeters),
                completedDistanceMillimeters = Math.Max(0L, completedDistanceMillimeters),
                totalDistanceMillimeters = Math.Max(0L, totalDistanceMillimeters),
                createdWorldTime = createdWorldTime,
                startedWorldTime = startedWorldTime,
                lastProgressWorldTime = lastProgressWorldTime,
                pausedWorldTime = pausedWorldTime,
                endedWorldTime = endedWorldTime,
                blockReason = blockReason,
                blockMessage = blockMessage ?? string.Empty,
                replanCount = Math.Max(0, replanCount),
                acceptedRouteRevision = acceptedRouteRevision,
                acceptedConnectionRevision = acceptedConnectionRevision,
                visibility = visibility,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = Math.Max(1L, revision)
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class TravelJourneyHistoryRecordData
    {
        public string historyId;
        public string journeyId;
        public string operation;
        public TravelJourneyLifecycleState lifecycleState;
        public int currentStepIndex;
        public double worldTime;
        public string actorKey;
        public string message;
        public long revision;

        public TravelJourneyHistoryRecordData Clone()
        {
            return new TravelJourneyHistoryRecordData
            {
                historyId = N(historyId),
                journeyId = N(journeyId),
                operation = N(operation),
                lifecycleState = lifecycleState,
                currentStepIndex = Math.Max(0, currentStepIndex),
                worldTime = worldTime,
                actorKey = N(actorKey),
                message = message ?? string.Empty,
                revision = Math.Max(0L, revision)
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class TravelJourneyTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string journeyId;
        public string resultReferenceId;
        public long revision;

        public TravelJourneyTransactionRecordData Clone()
        {
            return new TravelJourneyTransactionRecordData
            {
                transactionId = N(transactionId),
                operation = N(operation),
                journeyId = N(journeyId),
                resultReferenceId = N(resultReferenceId),
                revision = Math.Max(0L, revision)
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class TravelJourneyRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public TravelJourneyRecordData[] journeys = Array.Empty<TravelJourneyRecordData>();
        public TravelJourneyStepRecordData[] steps = Array.Empty<TravelJourneyStepRecordData>();
        public TravelJourneyHistoryRecordData[] history = Array.Empty<TravelJourneyHistoryRecordData>();
        public TravelJourneyTransactionRecordData[] transactions = Array.Empty<TravelJourneyTransactionRecordData>();

        public TravelJourneyRuntimeSaveData Clone()
        {
            return new TravelJourneyRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = N(worldId),
                revision = revision,
                journeys = (journeys ?? Array.Empty<TravelJourneyRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                steps = (steps ?? Array.Empty<TravelJourneyStepRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                history = (history ?? Array.Empty<TravelJourneyHistoryRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                transactions = (transactions ?? Array.Empty<TravelJourneyTransactionRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray()
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class TravelJourneyStepSnapshot
    {
        private readonly TravelJourneyStepRecordData data;
        public TravelJourneyStepSnapshot(TravelJourneyStepRecordData record) { data = record?.Clone() ?? new TravelJourneyStepRecordData(); }
        public string JourneyStepId => data.journeyStepId ?? string.Empty;
        public string JourneyId => data.journeyId ?? string.Empty;
        public int SequenceIndex => data.sequenceIndex;
        public string SourceLocationId => data.sourceLocationId ?? string.Empty;
        public string DestinationLocationId => data.destinationLocationId ?? string.Empty;
        public string EdgeId => data.edgeId ?? string.Empty;
        public RouteEdgeKind EdgeKind => data.edgeKind;
        public RouteSegmentCategory Category => data.category;
        public TravelDistance Distance => new TravelDistance(data.distanceMeters);
        public TravelCost Cost => new TravelCost(data.costUnits);
        public string TravelModeDefinitionId => data.travelModeDefinitionId ?? string.Empty;
        public TravelJourneyStepLifecycleState LifecycleState => data.lifecycleState;
        public double StartedWorldTime => data.startedWorldTime;
        public double CompletedWorldTime => data.completedWorldTime;
        public double CompletedDistanceMeters => data.CompletedDistanceMeters;
        public double ProgressFraction => data.ProgressFraction;
        public IReadOnlyList<string> RequiredActions => (data.requiredActions ?? Array.Empty<string>()).ToArray();
        public TravelJourneyStepRecordData ToSaveData() => data.Clone();
    }

    public sealed class TravelJourneySnapshot
    {
        private readonly TravelJourneyRecordData data;
        private readonly TravelJourneyStepRecordData[] steps;

        public TravelJourneySnapshot(TravelJourneyRecordData record, IEnumerable<TravelJourneyStepRecordData> journeySteps)
        {
            data = record?.Clone() ?? new TravelJourneyRecordData();
            steps = (journeySteps ?? Array.Empty<TravelJourneyStepRecordData>()).Where(value => value != null).OrderBy(value => value.sequenceIndex).Select(value => value.Clone()).ToArray();
        }

        public string JourneyId => data.journeyId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public EntityLocationReferenceData Traveler => data.traveler?.Clone();
        public string TravelerKey => data.traveler?.StableKey ?? string.Empty;
        public EntityLocationReferenceData Controller => data.controller?.Clone();
        public string OriginLocationId => data.originLocationId ?? string.Empty;
        public string DestinationLocationId => data.destinationLocationId ?? string.Empty;
        public string RoutePlanId => data.routePlan?.routePlanId ?? string.Empty;
        public string TravelModeDefinitionId => data.travelModeDefinitionId ?? string.Empty;
        public double MovementRateOverrideMetersPerSecond => data.movementRateOverrideMetersPerSecond;
        public TravelJourneyCategory Category => data.category;
        public TravelJourneyLifecycleState LifecycleState => data.lifecycleState;
        public TravelJourneyProgressionMode ProgressionMode => data.progressionMode;
        public int CurrentStepIndex => data.currentStepIndex;
        public TravelDistance CompletedDistance => new TravelDistance(data.completedDistanceMillimeters / 1000d);
        public TravelDistance TotalDistance => new TravelDistance(data.totalDistanceMillimeters / 1000d);
        public TravelDistance RemainingDistance => new TravelDistance(Math.Max(0d, (data.totalDistanceMillimeters - data.completedDistanceMillimeters) / 1000d));
        public double CreatedWorldTime => data.createdWorldTime;
        public double StartedWorldTime => data.startedWorldTime;
        public double LastProgressWorldTime => data.lastProgressWorldTime;
        public double EndedWorldTime => data.endedWorldTime;
        public TravelJourneyBlockReason BlockReason => data.blockReason;
        public string BlockMessage => data.blockMessage ?? string.Empty;
        public int ReplanCount => data.replanCount;
        public TravelJourneyVisibility Visibility => data.visibility;
        public long Revision => data.revision;
        public IReadOnlyList<TravelJourneyStepSnapshot> Steps => steps.Select(step => new TravelJourneyStepSnapshot(step)).ToArray();
        public TravelJourneyStepSnapshot CurrentStep => steps.FirstOrDefault(step => step.sequenceIndex == data.currentStepIndex) is TravelJourneyStepRecordData current ? new TravelJourneyStepSnapshot(current) : null;
        public bool IsTerminal => data.lifecycleState == TravelJourneyLifecycleState.Completed || data.lifecycleState == TravelJourneyLifecycleState.Cancelled || data.lifecycleState == TravelJourneyLifecycleState.Failed || data.lifecycleState == TravelJourneyLifecycleState.Historical;
        public TravelJourneyRecordData ToSaveData() => data.Clone();
    }

    public sealed class TravelMovementRateResult
    {
        public TravelMovementRateResult(string travelModeDefinitionId, TravelModeCategory category, double baseRateMetersPerSecond, double overrideRateMetersPerSecond, double finalRateMetersPerSecond, string diagnostics)
        {
            TravelModeDefinitionId = travelModeDefinitionId ?? string.Empty;
            Category = category;
            BaseRateMetersPerSecond = baseRateMetersPerSecond;
            OverrideRateMetersPerSecond = overrideRateMetersPerSecond;
            FinalRateMetersPerSecond = finalRateMetersPerSecond;
            Diagnostics = diagnostics ?? string.Empty;
        }

        public string TravelModeDefinitionId { get; }
        public TravelModeCategory Category { get; }
        public double BaseRateMetersPerSecond { get; }
        public double OverrideRateMetersPerSecond { get; }
        public double FinalRateMetersPerSecond { get; }
        public string Diagnostics { get; }
        public bool Succeeded => FinalRateMetersPerSecond > 0d && !double.IsNaN(FinalRateMetersPerSecond) && !double.IsInfinity(FinalRateMetersPerSecond);
    }

    public sealed class TravelJourneyPhysicalContextResult
    {
        public TravelJourneyPhysicalContextResult(EntityLocationReferenceData traveler, TravelJourneySnapshot journey, EntityPlacementSnapshot exactPlacement, bool inTransit, string previousLocationId, string nextLocationId, TravelJourneyStepSnapshot currentStep, double progressFraction, double worldTime)
        {
            Traveler = traveler?.Clone();
            Journey = journey;
            ExactPlacement = exactPlacement;
            InTransit = inTransit;
            PreviousLocationId = previousLocationId ?? string.Empty;
            NextLocationId = nextLocationId ?? string.Empty;
            CurrentStep = currentStep;
            ProgressFraction = Math.Max(0d, Math.Min(1d, progressFraction));
            WorldTime = worldTime;
        }

        public EntityLocationReferenceData Traveler { get; }
        public TravelJourneySnapshot Journey { get; }
        public EntityPlacementSnapshot ExactPlacement { get; }
        public bool InTransit { get; }
        public string PreviousLocationId { get; }
        public string NextLocationId { get; }
        public TravelJourneyStepSnapshot CurrentStep { get; }
        public double ProgressFraction { get; }
        public double WorldTime { get; }
    }

    public sealed class TravelJourneyOperationResult
    {
        private TravelJourneyOperationResult(TravelJourneyMutationStatus status, string message, TravelJourneySnapshot journey, TravelJourneyStepSnapshot step, bool preview, bool duplicate, long before, long after, TravelMovementRateResult movementRate = null)
        {
            Status = status;
            Message = message ?? string.Empty;
            Journey = journey;
            Step = step;
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = before;
            RevisionAfter = after;
            MovementRate = movementRate;
        }

        public TravelJourneyMutationStatus Status { get; }
        public string Message { get; }
        public TravelJourneySnapshot Journey { get; }
        public TravelJourneyStepSnapshot Step { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public TravelMovementRateResult MovementRate { get; }
        public bool Succeeded => Status == TravelJourneyMutationStatus.Succeeded || Status == TravelJourneyMutationStatus.Preview || Status == TravelJourneyMutationStatus.Duplicate;

        public static TravelJourneyOperationResult Success(TravelJourneySnapshot journey, string message, long before, long after, TravelJourneyStepSnapshot step = null, bool preview = false, bool duplicate = false, TravelMovementRateResult movementRate = null)
        {
            return new TravelJourneyOperationResult(preview ? TravelJourneyMutationStatus.Preview : duplicate ? TravelJourneyMutationStatus.Duplicate : TravelJourneyMutationStatus.Succeeded, message, journey, step, preview, duplicate, before, after, movementRate);
        }

        public static TravelJourneyOperationResult Failure(TravelJourneyMutationStatus status, string message, long before)
        {
            return new TravelJourneyOperationResult(status, message, null, null, false, false, before, before);
        }

        public static TravelJourneyOperationResult Failure(TravelJourneyMutationStatus status, string message, long before, TravelJourneySnapshot journey, TravelJourneyStepSnapshot step = null)
        {
            return new TravelJourneyOperationResult(status, message, journey, step, false, false, before, before);
        }
    }

    public sealed class TravelJourneyCreateRequest
    {
        public string transactionId;
        public string journeyId;
        public EntityLocationReferenceData traveler;
        public EntityLocationReferenceData controller;
        public string originLocationId;
        public string destinationLocationId;
        public LocationRoutePlan acceptedRoutePlan;
        public string travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId;
        public RoutePlanningObjective objective = RoutePlanningObjective.ShortestDistance;
        public RouteAccessEvaluationMode accessMode = RouteAccessEvaluationMode.RequireCurrentAccess;
        public RouteKnowledgeMode knowledgeMode = RouteKnowledgeMode.AuthoritativeDevelopment;
        public LocationConnectionAccessContextData accessContext;
        public string[] travelerCapabilityIds = Array.Empty<string>();
        public string[] travelerEquipmentDefinitionIds = Array.Empty<string>();
        public TravelConditionEvaluationMode conditionEvaluationMode = TravelConditionEvaluationMode.IgnoreDynamicConditions;
        public TravelLegalComplianceMode legalComplianceMode = TravelLegalComplianceMode.StructuralOnlyDevelopment;
        public string[] knownConditionIds = Array.Empty<string>();
        public string[] knownEncounterIds = Array.Empty<string>();
        public string[] knownHazardExposureIds = Array.Empty<string>();
        public bool includeHiddenDevelopmentConditions;
        public TravelJourneyCategory category = TravelJourneyCategory.OrdinaryTravel;
        public TravelJourneyProgressionMode progressionMode = TravelJourneyProgressionMode.AutomaticLogical;
        public TravelJourneyVisibility visibility = TravelJourneyVisibility.Public;
        public double movementRateOverrideMetersPerSecond = -1d;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public class TravelJourneyLifecycleRequest
    {
        public string transactionId;
        public string journeyId;
        public EntityLocationReferenceData actor;
        public LocationConnectionAccessContextData accessContext;
        public string[] travelerCapabilityIds = Array.Empty<string>();
        public string[] travelerEquipmentDefinitionIds = Array.Empty<string>();
        public TravelConditionEvaluationMode conditionEvaluationMode = TravelConditionEvaluationMode.IgnoreDynamicConditions;
        public TravelLegalComplianceMode legalComplianceMode = TravelLegalComplianceMode.StructuralOnlyDevelopment;
        public string[] knownConditionIds = Array.Empty<string>();
        public string[] knownEncounterIds = Array.Empty<string>();
        public string[] knownHazardExposureIds = Array.Empty<string>();
        public bool includeHiddenDevelopmentConditions;
        public bool travelerCanMove = true;
        public double movementRateOverrideMetersPerSecond = -1d;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
        public int maximumStepsToProcess = 64;
    }

    public sealed class TravelJourneyReplanRequest : TravelJourneyLifecycleRequest
    {
        public string destinationLocationId;
        public RoutePlanningObjective objective = RoutePlanningObjective.ShortestDistance;
        public RouteAccessEvaluationMode accessMode = RouteAccessEvaluationMode.RequireCurrentAccess;
        public RouteKnowledgeMode knowledgeMode = RouteKnowledgeMode.AuthoritativeDevelopment;
    }

    public sealed class TravelJourneyProjectionRequest
    {
        public string journeyId;
        public EntityLocationReferenceData requester;
        public bool privileged;
        public bool developmentView;
        public bool includeHidden;
    }
}
