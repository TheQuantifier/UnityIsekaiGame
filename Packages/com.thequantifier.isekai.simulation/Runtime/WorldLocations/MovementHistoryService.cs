using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public enum MovementHistoryVisibilityMode
    {
        DevelopmentAuthoritative,
        OwnerOrTraveler,
        AuthorizedInstitutional,
        Public
    }

    public enum MovementTimelineEntryKind
    {
        PlacementStarted,
        PlacementEnded,
        LocationEntered,
        LocationExited,
        ConnectionTraversed,
        JourneyCreated,
        JourneyStarted,
        JourneyStepStarted,
        JourneyStepCompleted,
        JourneyPaused,
        JourneyBlocked,
        JourneyReplanned,
        JourneyEncounter,
        TravelConditionStarted,
        TravelConditionEnded,
        JourneyResumed,
        JourneyCompleted,
        JourneyCancelled,
        TerritoryCrossed,
        JurisdictionChanged,
        CheckpointCrossed,
        IllegalCrossing,
        Custom
    }

    public enum HistoricalExactLocationStatus
    {
        ExactLocationFound,
        InTransit,
        Unplaced,
        NoHistoricalRecord,
        EntityNotYetCreated,
        EntityAlreadyEnded,
        InvalidHistory,
        Hidden
    }

    public enum MovementHistoryIssueSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class MovementHistoryQuery
    {
        public EntityLocationReferenceData entity;
        public string journeyId;
        public string locationId;
        public string territoryId;
        public string routeSegmentId;
        public string checkpointId;
        public double startWorldTime = double.NegativeInfinity;
        public double endWorldTime = double.PositiveInfinity;
        public int offset;
        public int limit = 256;
        public MovementHistoryVisibilityMode visibilityMode = MovementHistoryVisibilityMode.DevelopmentAuthoritative;
        public bool recursiveLocationMatch;
    }

    public sealed class MovementTimelineEntry
    {
        public MovementTimelineEntry(MovementTimelineEntryKind kind, double worldTime, int priority, string sourceParticipantId, string sourceRecordId, string entityKey, string locationId, string secondaryLocationId, string journeyId, string journeyStepId, string routeSegmentId, string checkpointId, string territoryId, string jurisdictionId, bool redacted, string diagnostics)
        {
            Kind = kind;
            WorldTime = worldTime;
            Priority = priority;
            SourceParticipantId = sourceParticipantId ?? string.Empty;
            SourceRecordId = sourceRecordId ?? string.Empty;
            EntityKey = entityKey ?? string.Empty;
            LocationId = locationId ?? string.Empty;
            SecondaryLocationId = secondaryLocationId ?? string.Empty;
            JourneyId = journeyId ?? string.Empty;
            JourneyStepId = journeyStepId ?? string.Empty;
            RouteSegmentId = routeSegmentId ?? string.Empty;
            CheckpointId = checkpointId ?? string.Empty;
            TerritoryId = territoryId ?? string.Empty;
            JurisdictionId = jurisdictionId ?? string.Empty;
            Redacted = redacted;
            Diagnostics = diagnostics ?? string.Empty;
        }

        public MovementTimelineEntryKind Kind { get; }
        public double WorldTime { get; }
        public int Priority { get; }
        public string SourceParticipantId { get; }
        public string SourceRecordId { get; }
        public string EntityKey { get; }
        public string LocationId { get; }
        public string SecondaryLocationId { get; }
        public string JourneyId { get; }
        public string JourneyStepId { get; }
        public string RouteSegmentId { get; }
        public string CheckpointId { get; }
        public string TerritoryId { get; }
        public string JurisdictionId { get; }
        public bool Redacted { get; }
        public string Diagnostics { get; }
    }

    public sealed class MovementTimelineResult
    {
        public MovementTimelineResult(IEnumerable<MovementTimelineEntry> entries, int totalVisibleCount, bool budgetExceeded, bool redacted, string diagnostics)
        {
            Entries = (entries ?? Array.Empty<MovementTimelineEntry>()).Where(item => item != null).ToArray();
            TotalVisibleCount = Math.Max(0, totalVisibleCount);
            BudgetExceeded = budgetExceeded;
            Redacted = redacted;
            Diagnostics = diagnostics ?? string.Empty;
        }

        public IReadOnlyList<MovementTimelineEntry> Entries { get; }
        public int TotalVisibleCount { get; }
        public bool BudgetExceeded { get; }
        public bool Redacted { get; }
        public string Diagnostics { get; }
    }

    public sealed class HistoricalInTransitContext
    {
        public HistoricalInTransitContext(string journeyId, string journeyStepId, string routeEdgeId, RouteEdgeKind edgeKind, string previousLocationId, string nextLocationId, double completedDistanceMeters, double stepDistanceMeters, double progressFraction, TravelJourneyLifecycleState lifecycleState, double worldTime)
        {
            JourneyId = journeyId ?? string.Empty;
            JourneyStepId = journeyStepId ?? string.Empty;
            RouteEdgeId = routeEdgeId ?? string.Empty;
            EdgeKind = edgeKind;
            PreviousLocationId = previousLocationId ?? string.Empty;
            NextLocationId = nextLocationId ?? string.Empty;
            CompletedDistanceMeters = Math.Max(0d, completedDistanceMeters);
            StepDistanceMeters = Math.Max(0d, stepDistanceMeters);
            ProgressFraction = Clamp01(progressFraction);
            LifecycleState = lifecycleState;
            WorldTime = worldTime;
        }

        public string JourneyId { get; }
        public string JourneyStepId { get; }
        public string RouteEdgeId { get; }
        public RouteEdgeKind EdgeKind { get; }
        public string PreviousLocationId { get; }
        public string NextLocationId { get; }
        public double CompletedDistanceMeters { get; }
        public double StepDistanceMeters { get; }
        public double ProgressFraction { get; }
        public TravelJourneyLifecycleState LifecycleState { get; }
        public double WorldTime { get; }

        private static double Clamp01(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 0d : Math.Max(0d, Math.Min(1d, value));
    }

    public sealed class HistoricalExactLocationResult
    {
        public HistoricalExactLocationResult(HistoricalExactLocationStatus status, EntityPlacementRecordData placement, HistoricalInTransitContext inTransit, double worldTime, string message)
        {
            Status = status;
            Placement = placement?.Clone();
            InTransit = inTransit;
            WorldTime = worldTime;
            Message = message ?? string.Empty;
        }

        public HistoricalExactLocationStatus Status { get; }
        public EntityPlacementRecordData Placement { get; }
        public HistoricalInTransitContext InTransit { get; }
        public double WorldTime { get; }
        public string ExactLocationId => Placement?.exactLocationId ?? string.Empty;
        public bool Succeeded => Status == HistoricalExactLocationStatus.ExactLocationFound || Status == HistoricalExactLocationStatus.InTransit;
        public string Message { get; }
    }

    public sealed class HistoricalLocationPathResult
    {
        public HistoricalLocationPathResult(string locationId, double worldTime, IEnumerable<string> locationPathIds, bool conflict, bool truncated, string message)
        {
            LocationId = locationId ?? string.Empty;
            WorldTime = worldTime;
            LocationPathIds = (locationPathIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Conflict = conflict;
            Truncated = truncated;
            Message = message ?? string.Empty;
        }

        public string LocationId { get; }
        public double WorldTime { get; }
        public IReadOnlyList<string> LocationPathIds { get; }
        public bool Conflict { get; }
        public bool Truncated { get; }
        public bool Succeeded => !Conflict && !Truncated && LocationPathIds.Count > 0;
        public string Message { get; }
    }

    public sealed class HistoricalOccupancyResult
    {
        public HistoricalOccupancyResult(string locationId, double worldTime, bool recursive, IEnumerable<EntityPlacementRecordData> placements, bool redacted)
        {
            LocationId = locationId ?? string.Empty;
            WorldTime = worldTime;
            Recursive = recursive;
            Placements = (placements ?? Array.Empty<EntityPlacementRecordData>())
                .Where(item => item != null)
                .OrderBy(item => item.entity?.StableKey ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.placementId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
            Redacted = redacted;
        }

        public string LocationId { get; }
        public double WorldTime { get; }
        public bool Recursive { get; }
        public IReadOnlyList<EntityPlacementRecordData> Placements { get; }
        public int Count => Placements.Count;
        public bool Redacted { get; }
    }

    public sealed class MovementDistanceSummary
    {
        public MovementDistanceSummary(string entityKey, double startWorldTime, double endWorldTime, double totalCompletedDistanceMeters, IEnumerable<string> journeyIds)
        {
            EntityKey = entityKey ?? string.Empty;
            StartWorldTime = startWorldTime;
            EndWorldTime = endWorldTime;
            TotalCompletedDistanceMeters = Math.Max(0d, totalCompletedDistanceMeters);
            JourneyIds = C(journeyIds);
        }

        public string EntityKey { get; }
        public double StartWorldTime { get; }
        public double EndWorldTime { get; }
        public double TotalCompletedDistanceMeters { get; }
        public IReadOnlyList<string> JourneyIds { get; }

        private static IReadOnlyList<string> C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public sealed class VisitedLocationSummary
    {
        public VisitedLocationSummary(string locationId, bool exactOnly, double firstVisitWorldTime, double mostRecentVisitWorldTime, double mostRecentExitWorldTime, int visitCount, double totalDuration)
        {
            LocationId = locationId ?? string.Empty;
            ExactOnly = exactOnly;
            FirstVisitWorldTime = firstVisitWorldTime;
            MostRecentVisitWorldTime = mostRecentVisitWorldTime;
            MostRecentExitWorldTime = mostRecentExitWorldTime;
            VisitCount = Math.Max(0, visitCount);
            TotalDuration = Math.Max(0d, totalDuration);
        }

        public string LocationId { get; }
        public bool ExactOnly { get; }
        public double FirstVisitWorldTime { get; }
        public double MostRecentVisitWorldTime { get; }
        public double MostRecentExitWorldTime { get; }
        public int VisitCount { get; }
        public double TotalDuration { get; }
        public bool HasVisited => VisitCount > 0;
    }

    public sealed class HistoricalWorldContext
    {
        public HistoricalWorldContext(EntityLocationReferenceData entity, double worldTime, HistoricalExactLocationResult exactLocation, HistoricalLocationPathResult path, PoliticalTravelCrossingRecordData crossing, TravelEncounterRecordData encounter, IEnumerable<TravelConditionRecordData> conditions)
        {
            Entity = entity?.Clone();
            WorldTime = worldTime;
            ExactLocation = exactLocation;
            Path = path;
            Crossing = crossing?.Clone();
            Encounter = encounter?.Clone();
            Conditions = (conditions ?? Array.Empty<TravelConditionRecordData>()).Where(item => item != null).Select(item => item.Clone()).ToArray();
        }

        public EntityLocationReferenceData Entity { get; }
        public double WorldTime { get; }
        public HistoricalExactLocationResult ExactLocation { get; }
        public HistoricalLocationPathResult Path { get; }
        public PoliticalTravelCrossingRecordData Crossing { get; }
        public TravelEncounterRecordData Encounter { get; }
        public IReadOnlyList<TravelConditionRecordData> Conditions { get; }
    }

    public sealed class MovementHistoryValidationIssue
    {
        public MovementHistoryValidationIssue(MovementHistoryIssueSeverity severity, string sourceParticipantId, string sourceRecordId, string message)
        {
            Severity = severity;
            SourceParticipantId = sourceParticipantId ?? string.Empty;
            SourceRecordId = sourceRecordId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public MovementHistoryIssueSeverity Severity { get; }
        public string SourceParticipantId { get; }
        public string SourceRecordId { get; }
        public string Message { get; }
    }

    public sealed class MovementHistoryValidationReport
    {
        public MovementHistoryValidationReport(IEnumerable<MovementHistoryValidationIssue> issues)
        {
            Issues = (issues ?? Array.Empty<MovementHistoryValidationIssue>())
                .Where(item => item != null)
                .OrderByDescending(item => item.Severity)
                .ThenBy(item => item.SourceParticipantId, StringComparer.Ordinal)
                .ThenBy(item => item.SourceRecordId, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<MovementHistoryValidationIssue> Issues { get; }
        public bool Succeeded => !Issues.Any(item => item.Severity == MovementHistoryIssueSeverity.Error);
        public string Summary => Succeeded ? "Movement history validation passed." : string.Join(" | ", Issues.Where(item => item.Severity == MovementHistoryIssueSeverity.Error).Select(item => item.Message));
    }

    public sealed class MovementHistoryService
    {
        private const int DefaultTraversalLimit = 128;
        private readonly Step14PersistenceSnapshotSource source;
        private readonly Dictionary<string, LocationRecordData> locationsById;
        private readonly Dictionary<string, List<LocationContainmentLinkData>> containmentByChild;
        private readonly Dictionary<string, List<LocationContainmentLinkData>> containmentByParent;
        private readonly Dictionary<string, List<EntityPlacementRecordData>> placementsByEntity;
        private readonly Dictionary<string, List<EntityPlacementRecordData>> placementsByLocation;
        private readonly Dictionary<string, TravelJourneyRecordData> journeysById;
        private readonly Dictionary<string, List<TravelJourneyStepRecordData>> stepsByJourney;
        private readonly Dictionary<string, List<TravelEncounterRecordData>> encountersByJourney;
        private readonly Dictionary<string, List<TravelConditionRecordData>> conditionsByTarget;
        private readonly Dictionary<string, List<PoliticalTravelCrossingRecordData>> crossingsByTraveler;

        public MovementHistoryService(Step14PersistenceSnapshotSource source)
        {
            this.source = source?.Clone() ?? new Step14PersistenceSnapshotSource();
            locationsById = (this.source.locations?.records ?? new List<LocationRecordData>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.locationId))
                .GroupBy(item => item.locationId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.revision).First().Clone(), StringComparer.Ordinal);
            containmentByChild = Group((this.source.locations?.containmentLinks ?? new List<LocationContainmentLinkData>()).Where(item => item != null), item => item.childLocationId);
            containmentByParent = Group((this.source.locations?.containmentLinks ?? new List<LocationContainmentLinkData>()).Where(item => item != null), item => item.parentLocationId);
            placementsByEntity = Group((this.source.entityLocations?.placements ?? new List<EntityPlacementRecordData>()).Where(item => item?.entity != null), item => item.entity.StableKey);
            placementsByLocation = Group((this.source.entityLocations?.placements ?? new List<EntityPlacementRecordData>()).Where(item => item != null), item => item.exactLocationId);
            journeysById = (this.source.journeys?.journeys ?? Array.Empty<TravelJourneyRecordData>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.journeyId))
                .GroupBy(item => item.journeyId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.revision).First().Clone(), StringComparer.Ordinal);
            stepsByJourney = Group((this.source.journeys?.steps ?? Array.Empty<TravelJourneyStepRecordData>()).Where(item => item != null), item => item.journeyId);
            encountersByJourney = Group((this.source.travelConditions?.encounters ?? Array.Empty<TravelEncounterRecordData>()).Where(item => item != null), item => item.journeyId);
            conditionsByTarget = Group((this.source.travelConditions?.conditions ?? Array.Empty<TravelConditionRecordData>()).Where(item => item?.target != null), item => item.target.StableKey);
            crossingsByTraveler = Group((this.source.politicalTravel?.crossings ?? Array.Empty<PoliticalTravelCrossingRecordData>()).Where(item => item != null), item => item.travelerPersonId);
        }

        public static MovementHistoryService FromRuntimes(LocationRuntime locations, EntityLocationRuntime entityLocations, InteractionPointRuntime interactionPoints, LocationConnectionRuntime connections, LocationRouteRuntime routes, TravelJourneyRuntime journeys, TravelConditionRuntime travelConditions, PoliticalTravelRuntime politicalTravel, string worldId, string saveSlotId = "", double authoritativeWorldTime = 0d)
        {
            return new MovementHistoryService(Step14PersistenceSnapshotSource.FromRuntimes(locations, entityLocations, interactionPoints, connections, routes, journeys, travelConditions, politicalTravel, worldId, saveSlotId, authoritativeWorldTime));
        }

        public MovementTimelineResult BuildTimeline(MovementHistoryQuery query)
        {
            MovementHistoryQuery q = query ?? new MovementHistoryQuery();
            int limit = q.limit <= 0 ? 256 : Math.Min(q.limit, 2048);
            int offset = Math.Max(0, q.offset);
            List<MovementTimelineEntry> entries = new List<MovementTimelineEntry>();
            string entityKey = q.entity?.StableKey ?? string.Empty;

            foreach (EntityPlacementRecordData placement in source.entityLocations?.placements ?? new List<EntityPlacementRecordData>())
            {
                if (placement == null || !MatchesEntity(entityKey, placement.entity) || !Visible(placement.visibility, q.visibilityMode)) continue;
                if (!InRange(placement.startWorldTime, q.startWorldTime, q.endWorldTime)) continue;
                if (!MatchesLocation(q.locationId, placement.exactLocationId, q.recursiveLocationMatch, placement.startWorldTime)) continue;
                entries.Add(E(MovementTimelineEntryKind.PlacementStarted, placement.startWorldTime, 10, Step14PersistenceManifestBuilder.EntityLocationParticipantId, placement.placementId, placement.entity?.StableKey, placement.exactLocationId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Placement interval started."));
                entries.Add(E(MovementTimelineEntryKind.LocationEntered, placement.startWorldTime, 11, Step14PersistenceManifestBuilder.EntityLocationParticipantId, placement.placementId, placement.entity?.StableKey, placement.exactLocationId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Exact location entered."));
                if (placement.endWorldTime >= 0d && InRange(placement.endWorldTime, q.startWorldTime, q.endWorldTime))
                {
                    entries.Add(E(MovementTimelineEntryKind.LocationExited, placement.endWorldTime, 20, Step14PersistenceManifestBuilder.EntityLocationParticipantId, placement.placementId, placement.entity?.StableKey, placement.exactLocationId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Exact location exited."));
                    entries.Add(E(MovementTimelineEntryKind.PlacementEnded, placement.endWorldTime, 21, Step14PersistenceManifestBuilder.EntityLocationParticipantId, placement.placementId, placement.entity?.StableKey, placement.exactLocationId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Placement interval ended."));
                }
            }

            foreach (TravelJourneyRecordData journey in source.journeys?.journeys ?? Array.Empty<TravelJourneyRecordData>())
            {
                if (journey == null || !MatchesEntity(entityKey, journey.traveler) || !MatchesJourney(q.journeyId, journey.journeyId) || !Visible(journey.visibility, q.visibilityMode)) continue;
                if (InRange(journey.createdWorldTime, q.startWorldTime, q.endWorldTime)) entries.Add(E(MovementTimelineEntryKind.JourneyCreated, journey.createdWorldTime, 30, Step14PersistenceManifestBuilder.JourneyParticipantId, journey.journeyId, journey.traveler?.StableKey, journey.originLocationId, journey.destinationLocationId, journey.journeyId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Journey created."));
                if (journey.startedWorldTime >= 0d && InRange(journey.startedWorldTime, q.startWorldTime, q.endWorldTime)) entries.Add(E(MovementTimelineEntryKind.JourneyStarted, journey.startedWorldTime, 31, Step14PersistenceManifestBuilder.JourneyParticipantId, journey.journeyId, journey.traveler?.StableKey, journey.originLocationId, journey.destinationLocationId, journey.journeyId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Journey started."));
                if (journey.pausedWorldTime >= 0d && InRange(journey.pausedWorldTime, q.startWorldTime, q.endWorldTime)) entries.Add(E(MovementTimelineEntryKind.JourneyPaused, journey.pausedWorldTime, 41, Step14PersistenceManifestBuilder.JourneyParticipantId, journey.journeyId, journey.traveler?.StableKey, string.Empty, string.Empty, journey.journeyId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Journey paused."));
                if (journey.lifecycleState == TravelJourneyLifecycleState.Blocked && InRange(EventEndTime(journey), q.startWorldTime, q.endWorldTime)) entries.Add(E(MovementTimelineEntryKind.JourneyBlocked, EventEndTime(journey), 42, Step14PersistenceManifestBuilder.JourneyParticipantId, journey.journeyId, journey.traveler?.StableKey, string.Empty, string.Empty, journey.journeyId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, journey.blockMessage));
                if (journey.replanCount > 0 && InRange(journey.lastProgressWorldTime, q.startWorldTime, q.endWorldTime)) entries.Add(E(MovementTimelineEntryKind.JourneyReplanned, journey.lastProgressWorldTime, 43, Step14PersistenceManifestBuilder.JourneyParticipantId, journey.journeyId, journey.traveler?.StableKey, string.Empty, string.Empty, journey.journeyId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Journey route plan changed."));
                if ((journey.lifecycleState == TravelJourneyLifecycleState.Completed || journey.lifecycleState == TravelJourneyLifecycleState.Cancelled || journey.lifecycleState == TravelJourneyLifecycleState.Failed) && InRange(EventEndTime(journey), q.startWorldTime, q.endWorldTime))
                {
                    MovementTimelineEntryKind kind = journey.lifecycleState == TravelJourneyLifecycleState.Completed ? MovementTimelineEntryKind.JourneyCompleted : MovementTimelineEntryKind.JourneyCancelled;
                    entries.Add(E(kind, EventEndTime(journey), 80, Step14PersistenceManifestBuilder.JourneyParticipantId, journey.journeyId, journey.traveler?.StableKey, journey.destinationLocationId, string.Empty, journey.journeyId, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, $"Journey {journey.lifecycleState}."));
                }
            }

            foreach (TravelJourneyStepRecordData step in source.journeys?.steps ?? Array.Empty<TravelJourneyStepRecordData>())
            {
                if (step == null || !journeysById.TryGetValue(step.journeyId ?? string.Empty, out TravelJourneyRecordData journey)) continue;
                if (!MatchesEntity(entityKey, journey.traveler) || !MatchesJourney(q.journeyId, journey.journeyId) || !MatchesRoute(q.routeSegmentId, step.edgeId) || !Visible(journey.visibility, q.visibilityMode)) continue;
                if (step.startedWorldTime >= 0d && InRange(step.startedWorldTime, q.startWorldTime, q.endWorldTime)) entries.Add(E(MovementTimelineEntryKind.JourneyStepStarted, step.startedWorldTime, 35, Step14PersistenceManifestBuilder.JourneyParticipantId, step.journeyStepId, journey.traveler?.StableKey, step.sourceLocationId, step.destinationLocationId, step.journeyId, step.journeyStepId, step.edgeId, string.Empty, string.Empty, string.Empty, false, "Journey step started."));
                if (step.completedWorldTime >= 0d && InRange(step.completedWorldTime, q.startWorldTime, q.endWorldTime)) entries.Add(E(MovementTimelineEntryKind.JourneyStepCompleted, step.completedWorldTime, 55, Step14PersistenceManifestBuilder.JourneyParticipantId, step.journeyStepId, journey.traveler?.StableKey, step.destinationLocationId, step.sourceLocationId, step.journeyId, step.journeyStepId, step.edgeId, string.Empty, string.Empty, string.Empty, false, "Journey step completed."));
            }

            foreach (TravelEncounterRecordData encounter in source.travelConditions?.encounters ?? Array.Empty<TravelEncounterRecordData>())
            {
                if (encounter == null || !MatchesEntity(entityKey, encounter.traveler) || !MatchesJourney(q.journeyId, encounter.journeyId) || !Visible(TravelConditionVisibility.Public, q.visibilityMode)) continue;
                double time = encounter.triggeredWorldTime >= 0d ? encounter.triggeredWorldTime : encounter.createdWorldTime;
                if (InRange(time, q.startWorldTime, q.endWorldTime)) entries.Add(E(MovementTimelineEntryKind.JourneyEncounter, time, 45, Step14PersistenceManifestBuilder.TravelConditionParticipantId, encounter.encounterId, encounter.traveler?.StableKey, encounter.target?.sourceLocationId, encounter.target?.destinationLocationId, encounter.journeyId, string.Empty, encounter.target?.targetId, string.Empty, string.Empty, string.Empty, false, "Travel encounter recorded."));
            }

            foreach (TravelConditionRecordData condition in source.travelConditions?.conditions ?? Array.Empty<TravelConditionRecordData>())
            {
                if (condition?.target == null || !Visible(condition.visibility, q.visibilityMode)) continue;
                if (!MatchesEntity(entityKey, condition.target.traveler)) continue;
                if (!MatchesJourney(q.journeyId, condition.target.journeyId)) continue;
                if (!MatchesRoute(q.routeSegmentId, condition.target.targetId)) continue;
                if (!MatchesConditionLocation(q.locationId, condition.target, q.recursiveLocationMatch, condition.startsWorldTime)) continue;

                bool conditionRedacted = condition.visibility != TravelConditionVisibility.Public && q.visibilityMode != MovementHistoryVisibilityMode.DevelopmentAuthoritative;
                if (InRange(condition.startsWorldTime, q.startWorldTime, q.endWorldTime))
                {
                    entries.Add(E(MovementTimelineEntryKind.TravelConditionStarted, condition.startsWorldTime, 44, Step14PersistenceManifestBuilder.TravelConditionParticipantId, condition.conditionId, condition.target.traveler?.StableKey, condition.target.sourceLocationId, condition.target.destinationLocationId, condition.target.journeyId, string.Empty, condition.target.targetId, string.Empty, string.Empty, string.Empty, conditionRedacted, conditionRedacted ? "Travel condition redacted." : "Travel condition started."));
                }

                if (condition.endsWorldTime >= 0d && InRange(condition.endsWorldTime, q.startWorldTime, q.endWorldTime))
                {
                    entries.Add(E(MovementTimelineEntryKind.TravelConditionEnded, condition.endsWorldTime, 54, Step14PersistenceManifestBuilder.TravelConditionParticipantId, condition.conditionId, condition.target.traveler?.StableKey, condition.target.sourceLocationId, condition.target.destinationLocationId, condition.target.journeyId, string.Empty, condition.target.targetId, string.Empty, string.Empty, string.Empty, conditionRedacted, conditionRedacted ? "Travel condition redacted." : "Travel condition ended."));
                }
            }

            foreach (PoliticalTravelCrossingRecordData crossing in source.politicalTravel?.crossings ?? Array.Empty<PoliticalTravelCrossingRecordData>())
            {
                if (crossing == null || !MatchesTravelerPerson(q.entity, crossing.travelerPersonId) || !MatchesRoute(q.routeSegmentId, crossing.routeSegmentId) || !MatchesCheckpoint(q.checkpointId, crossing.checkpointId) || !MatchesTerritory(q.territoryId, crossing.sourceTerritoryId, crossing.destinationTerritoryId)) continue;
                if (!InRange(crossing.worldTime, q.startWorldTime, q.endWorldTime)) continue;
                entries.Add(E(MovementTimelineEntryKind.TerritoryCrossed, crossing.worldTime, 60, Step14PersistenceManifestBuilder.PoliticalTravelParticipantId, crossing.crossingId, EntityKey(q.entity), crossing.originLocationId, crossing.destinationLocationId, string.Empty, string.Empty, crossing.routeSegmentId, crossing.checkpointId, crossing.destinationTerritoryId, crossing.destinationJurisdictionId, false, $"Political crossing {crossing.classification}."));
                if (!string.Equals(crossing.sourceJurisdictionId, crossing.destinationJurisdictionId, StringComparison.Ordinal)) entries.Add(E(MovementTimelineEntryKind.JurisdictionChanged, crossing.worldTime, 61, Step14PersistenceManifestBuilder.PoliticalTravelParticipantId, crossing.crossingId, EntityKey(q.entity), crossing.originLocationId, crossing.destinationLocationId, string.Empty, string.Empty, crossing.routeSegmentId, crossing.checkpointId, crossing.destinationTerritoryId, crossing.destinationJurisdictionId, false, "Jurisdiction changed."));
                if (!string.IsNullOrWhiteSpace(crossing.checkpointId)) entries.Add(E(MovementTimelineEntryKind.CheckpointCrossed, crossing.worldTime, 62, Step14PersistenceManifestBuilder.PoliticalTravelParticipantId, crossing.crossingId, EntityKey(q.entity), crossing.originLocationId, crossing.destinationLocationId, string.Empty, string.Empty, crossing.routeSegmentId, crossing.checkpointId, crossing.destinationTerritoryId, crossing.destinationJurisdictionId, false, "Checkpoint crossed."));
                if (crossing.illegalCrossing) entries.Add(E(MovementTimelineEntryKind.IllegalCrossing, crossing.worldTime, 63, Step14PersistenceManifestBuilder.PoliticalTravelParticipantId, crossing.crossingId, EntityKey(q.entity), crossing.originLocationId, crossing.destinationLocationId, string.Empty, string.Empty, crossing.routeSegmentId, crossing.checkpointId, crossing.destinationTerritoryId, crossing.destinationJurisdictionId, false, "Illegal crossing recorded."));
            }

            MovementTimelineEntry[] ordered = Order(entries).ToArray();
            bool redacted = q.visibilityMode == MovementHistoryVisibilityMode.Public || q.visibilityMode == MovementHistoryVisibilityMode.OwnerOrTraveler || q.visibilityMode == MovementHistoryVisibilityMode.AuthorizedInstitutional;
            MovementTimelineEntry[] paged = ordered.Skip(offset).Take(limit).ToArray();
            return new MovementTimelineResult(paged, ordered.Length, ordered.Length > offset + limit, redacted, "Movement timeline projected from authoritative Step 14 records.");
        }

        public HistoricalExactLocationResult ResolveExactLocationAt(EntityLocationReferenceData entity, double worldTime, MovementHistoryVisibilityMode visibilityMode = MovementHistoryVisibilityMode.DevelopmentAuthoritative)
        {
            string entityKey = entity?.StableKey ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entityKey)) return new HistoricalExactLocationResult(HistoricalExactLocationStatus.InvalidHistory, null, null, worldTime, "Entity reference is missing.");

            HistoricalInTransitContext transit = ResolveInTransitContext(entity, worldTime, visibilityMode);
            if (transit != null) return new HistoricalExactLocationResult(HistoricalExactLocationStatus.InTransit, null, transit, worldTime, "Entity was in transit; no exact room/location is inferred.");

            EntityPlacementRecordData[] placements = placementsByEntity.TryGetValue(entityKey, out List<EntityPlacementRecordData> list) ? list.OrderBy(item => item.startWorldTime).ThenBy(item => item.placementId, StringComparer.Ordinal).ToArray() : Array.Empty<EntityPlacementRecordData>();
            EntityPlacementRecordData active = placements.FirstOrDefault(item => IntervalActive(item.startWorldTime, item.endWorldTime, worldTime) && Visible(item.visibility, visibilityMode));
            if (active != null) return new HistoricalExactLocationResult(HistoricalExactLocationStatus.ExactLocationFound, active, null, worldTime, "Exact historical placement found.");
            if (placements.Length == 0) return new HistoricalExactLocationResult(HistoricalExactLocationStatus.NoHistoricalRecord, null, null, worldTime, "No placement history exists for the entity.");
            if (placements.All(item => !Visible(item.visibility, visibilityMode))) return new HistoricalExactLocationResult(HistoricalExactLocationStatus.Hidden, null, null, worldTime, "Movement history is not visible to this requester.");
            if (worldTime < placements.Min(item => item.startWorldTime)) return new HistoricalExactLocationResult(HistoricalExactLocationStatus.EntityNotYetCreated, null, null, worldTime, "Entity had not entered tracked placement history yet.");

            EntityPlacementRecordData last = placements.Where(item => item.startWorldTime <= worldTime && Visible(item.visibility, visibilityMode)).OrderByDescending(item => item.startWorldTime).ThenByDescending(item => item.revision).FirstOrDefault();
            if (last != null && last.endWorldTime >= 0d && worldTime > last.endWorldTime)
            {
                return new HistoricalExactLocationResult(HistoricalExactLocationStatus.Unplaced, last, null, worldTime, "Entity was unplaced at the requested time; last known exact placement is returned.");
            }

            return new HistoricalExactLocationResult(HistoricalExactLocationStatus.NoHistoricalRecord, null, null, worldTime, "No valid historical placement was active at the requested time.");
        }

        public HistoricalLocationPathResult ResolveHistoricalLocationPath(string locationId, double worldTime)
        {
            string current = N(locationId);
            if (string.IsNullOrWhiteSpace(current)) return new HistoricalLocationPathResult(locationId, worldTime, Array.Empty<string>(), false, false, "Location ID is missing.");
            List<string> path = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            bool conflict = false;
            bool truncated = false;
            for (int depth = 0; depth < DefaultTraversalLimit && !string.IsNullOrWhiteSpace(current); depth++)
            {
                if (!seen.Add(current))
                {
                    conflict = true;
                    break;
                }

                path.Add(current);
                LocationContainmentLinkData[] parents = containmentByChild.TryGetValue(current, out List<LocationContainmentLinkData> links)
                    ? links.Where(item => item.kind == LocationContainmentKind.Primary && item.state == LocationLinkState.Active && IntervalActive(item.effectiveStartWorldTime, item.effectiveEndWorldTime, worldTime)).OrderByDescending(item => item.effectiveStartWorldTime).ThenBy(item => item.linkId, StringComparer.Ordinal).ToArray()
                    : Array.Empty<LocationContainmentLinkData>();
                if (parents.Length > 1) conflict = true;
                current = parents.FirstOrDefault()?.parentLocationId ?? string.Empty;
            }

            if (path.Count >= DefaultTraversalLimit) truncated = true;
            return new HistoricalLocationPathResult(locationId, worldTime, path, conflict, truncated, conflict ? "Historical containment conflict detected." : truncated ? "Historical containment path exceeded traversal limit." : "Historical location path resolved.");
        }

        public HistoricalOccupancyResult GetHistoricalOccupancy(string locationId, double worldTime, bool recursive, LocationOccupantEntityType entityType = LocationOccupantEntityType.Unknown, MovementHistoryVisibilityMode visibilityMode = MovementHistoryVisibilityMode.DevelopmentAuthoritative)
        {
            HashSet<string> locationIds = recursive ? GetDescendantsAt(locationId, worldTime) : new HashSet<string>(new[] { N(locationId) }, StringComparer.Ordinal);
            List<EntityPlacementRecordData> placements = new List<EntityPlacementRecordData>();
            foreach (string id in locationIds)
            {
                if (!placementsByLocation.TryGetValue(id, out List<EntityPlacementRecordData> byLocation)) continue;
                placements.AddRange(byLocation.Where(item => IntervalActive(item.startWorldTime, item.endWorldTime, worldTime) && Visible(item.visibility, visibilityMode) && (entityType == LocationOccupantEntityType.Unknown || item.entity?.entityType == entityType)).Select(item => item.Clone()));
            }

            return new HistoricalOccupancyResult(locationId, worldTime, recursive, placements, visibilityMode != MovementHistoryVisibilityMode.DevelopmentAuthoritative);
        }

        public MovementDistanceSummary GetMovementDistance(EntityLocationReferenceData entity, double startWorldTime, double endWorldTime, MovementHistoryVisibilityMode visibilityMode = MovementHistoryVisibilityMode.DevelopmentAuthoritative)
        {
            string entityKey = entity?.StableKey ?? string.Empty;
            List<string> journeyIds = new List<string>();
            double total = 0d;
            foreach (TravelJourneyStepRecordData step in source.journeys?.steps ?? Array.Empty<TravelJourneyStepRecordData>())
            {
                if (step == null || !journeysById.TryGetValue(step.journeyId ?? string.Empty, out TravelJourneyRecordData journey)) continue;
                if (!MatchesEntity(entityKey, journey.traveler) || !Visible(journey.visibility, visibilityMode)) continue;
                double time = step.completedWorldTime >= 0d ? step.completedWorldTime : Math.Max(step.startedWorldTime, journey.lastProgressWorldTime);
                if (!InRange(time, startWorldTime, endWorldTime)) continue;
                total += Math.Max(0d, step.CompletedDistanceMeters);
                journeyIds.Add(step.journeyId);
            }

            return new MovementDistanceSummary(entityKey, startWorldTime, endWorldTime, total, journeyIds);
        }

        public VisitedLocationSummary GetVisitSummary(EntityLocationReferenceData entity, string locationId, double startWorldTime, double endWorldTime, bool exactOnly, MovementHistoryVisibilityMode visibilityMode = MovementHistoryVisibilityMode.DevelopmentAuthoritative)
        {
            string target = N(locationId);
            string entityKey = entity?.StableKey ?? string.Empty;
            EntityPlacementRecordData[] placements = placementsByEntity.TryGetValue(entityKey, out List<EntityPlacementRecordData> list) ? list.ToArray() : Array.Empty<EntityPlacementRecordData>();
            List<EntityPlacementRecordData> visits = new List<EntityPlacementRecordData>();
            foreach (EntityPlacementRecordData placement in placements)
            {
                if (!Visible(placement.visibility, visibilityMode) || !IntervalsIntersect(placement.startWorldTime, placement.endWorldTime, startWorldTime, endWorldTime)) continue;
                bool matches = exactOnly
                    ? string.Equals(placement.exactLocationId, target, StringComparison.Ordinal)
                    : ResolveHistoricalLocationPath(placement.exactLocationId, placement.startWorldTime).LocationPathIds.Contains(target, StringComparer.Ordinal);
                if (matches) visits.Add(placement);
            }

            if (visits.Count == 0) return new VisitedLocationSummary(locationId, exactOnly, -1d, -1d, -1d, 0, 0d);
            double totalDuration = visits.Sum(item =>
            {
                double end = item.endWorldTime < 0d ? endWorldTime : Math.Min(endWorldTime, item.endWorldTime);
                double start = Math.Max(startWorldTime, item.startWorldTime);
                return Math.Max(0d, end - start);
            });
            return new VisitedLocationSummary(locationId, exactOnly, visits.Min(item => item.startWorldTime), visits.Max(item => item.startWorldTime), visits.Where(item => item.endWorldTime >= 0d).Select(item => item.endWorldTime).DefaultIfEmpty(-1d).Max(), visits.Count, totalDuration);
        }

        public HistoricalWorldContext ResolveHistoricalWorldContext(EntityLocationReferenceData entity, double worldTime, MovementHistoryVisibilityMode visibilityMode = MovementHistoryVisibilityMode.DevelopmentAuthoritative)
        {
            HistoricalExactLocationResult exact = ResolveExactLocationAt(entity, worldTime, visibilityMode);
            string pathLocation = exact.Status == HistoricalExactLocationStatus.InTransit ? exact.InTransit?.PreviousLocationId : exact.ExactLocationId;
            HistoricalLocationPathResult path = ResolveHistoricalLocationPath(pathLocation, worldTime);
            PoliticalTravelCrossingRecordData crossing = FindNearestCrossing(entity, worldTime, visibilityMode);
            TravelEncounterRecordData encounter = FindActiveEncounter(entity, worldTime);
            IEnumerable<TravelConditionRecordData> conditions = ActiveConditionsForContext(exact, worldTime, visibilityMode);
            return new HistoricalWorldContext(entity, worldTime, exact, path, crossing, encounter, conditions);
        }

        public MovementHistoryValidationReport ValidateHistory()
        {
            List<MovementHistoryValidationIssue> issues = new List<MovementHistoryValidationIssue>();
            foreach (EntityPlacementRecordData placement in source.entityLocations?.placements ?? new List<EntityPlacementRecordData>())
            {
                if (placement == null) continue;
                if (placement.endWorldTime >= 0d && placement.endWorldTime < placement.startWorldTime) issues.Add(new MovementHistoryValidationIssue(MovementHistoryIssueSeverity.Error, Step14PersistenceManifestBuilder.EntityLocationParticipantId, placement.placementId, "Placement interval ends before it starts."));
                if (!string.IsNullOrWhiteSpace(placement.exactLocationId) && !locationsById.ContainsKey(placement.exactLocationId)) issues.Add(new MovementHistoryValidationIssue(MovementHistoryIssueSeverity.Error, Step14PersistenceManifestBuilder.EntityLocationParticipantId, placement.placementId, $"Placement references missing historical location '{placement.exactLocationId}'."));
            }

            foreach (TravelJourneyRecordData journey in source.journeys?.journeys ?? Array.Empty<TravelJourneyRecordData>())
            {
                if (journey == null) continue;
                TravelJourneyStepRecordData[] steps = stepsByJourney.TryGetValue(journey.journeyId ?? string.Empty, out List<TravelJourneyStepRecordData> list) ? list.OrderBy(item => item.sequenceIndex).ToArray() : Array.Empty<TravelJourneyStepRecordData>();
                for (int i = 0; i < steps.Length; i++)
                {
                    if (steps[i].sequenceIndex != i) issues.Add(new MovementHistoryValidationIssue(MovementHistoryIssueSeverity.Warning, Step14PersistenceManifestBuilder.JourneyParticipantId, steps[i].journeyStepId, "Journey step sequence has a gap; deterministic ordering uses sequence index then stable ID."));
                }
            }

            foreach (PoliticalTravelCrossingRecordData crossing in source.politicalTravel?.crossings ?? Array.Empty<PoliticalTravelCrossingRecordData>())
            {
                if (crossing == null) continue;
                if (string.IsNullOrWhiteSpace(crossing.originLocationId) || string.IsNullOrWhiteSpace(crossing.destinationLocationId)) issues.Add(new MovementHistoryValidationIssue(MovementHistoryIssueSeverity.Error, Step14PersistenceManifestBuilder.PoliticalTravelParticipantId, crossing.crossingId, "Political crossing is missing origin or destination location."));
            }

            return new MovementHistoryValidationReport(issues);
        }

        private HistoricalInTransitContext ResolveInTransitContext(EntityLocationReferenceData entity, double worldTime, MovementHistoryVisibilityMode visibilityMode)
        {
            string entityKey = entity?.StableKey ?? string.Empty;
            TravelJourneyRecordData journey = (source.journeys?.journeys ?? Array.Empty<TravelJourneyRecordData>())
                .Where(item => item != null && MatchesEntity(entityKey, item.traveler) && Visible(item.visibility, visibilityMode) && JourneyActiveAt(item, worldTime))
                .OrderByDescending(item => item.startedWorldTime)
                .ThenBy(item => item.journeyId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (journey == null) return null;
            TravelJourneyStepRecordData step = FindStepAt(journey, worldTime);
            if (step == null)
            {
                return new HistoricalInTransitContext(journey.journeyId, string.Empty, string.Empty, RouteEdgeKind.Unknown, journey.originLocationId, journey.destinationLocationId, journey.completedDistanceMillimeters / 1000d, journey.totalDistanceMillimeters / 1000d, journey.totalDistanceMillimeters <= 0L ? 0d : (journey.completedDistanceMillimeters / (double)journey.totalDistanceMillimeters), journey.lifecycleState, worldTime);
            }

            return new HistoricalInTransitContext(journey.journeyId, step.journeyStepId, step.edgeId, step.edgeKind, step.sourceLocationId, step.destinationLocationId, StepCompletedAt(step, journey, worldTime), step.distanceMeters, step.distanceMeters <= 0d ? step.ProgressFraction : StepCompletedAt(step, journey, worldTime) / step.distanceMeters, journey.lifecycleState, worldTime);
        }

        private TravelJourneyStepRecordData FindStepAt(TravelJourneyRecordData journey, double worldTime)
        {
            TravelJourneyStepRecordData[] steps = stepsByJourney.TryGetValue(journey.journeyId ?? string.Empty, out List<TravelJourneyStepRecordData> list)
                ? list.OrderBy(item => item.sequenceIndex).ThenBy(item => item.journeyStepId, StringComparer.Ordinal).ToArray()
                : Array.Empty<TravelJourneyStepRecordData>();
            TravelJourneyStepRecordData active = steps.FirstOrDefault(item => item.startedWorldTime >= 0d && item.startedWorldTime <= worldTime && (item.completedWorldTime < 0d || worldTime <= item.completedWorldTime));
            return active ?? steps.FirstOrDefault(item => item.sequenceIndex == journey.currentStepIndex) ?? steps.LastOrDefault(item => item.startedWorldTime >= 0d && item.startedWorldTime <= worldTime);
        }

        private double StepCompletedAt(TravelJourneyStepRecordData step, TravelJourneyRecordData journey, double worldTime)
        {
            if (step == null) return 0d;
            if (step.completedWorldTime >= 0d && step.completedWorldTime <= worldTime) return step.CompletedDistanceMeters;
            if (step.sequenceIndex == journey.currentStepIndex) return journey.currentStepCompletedMillimeters / 1000d;
            return step.CompletedDistanceMeters;
        }

        private HashSet<string> GetDescendantsAt(string locationId, double worldTime)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
            Queue<string> queue = new Queue<string>();
            string root = N(locationId);
            if (string.IsNullOrWhiteSpace(root)) return result;
            result.Add(root);
            queue.Enqueue(root);
            int visited = 0;
            while (queue.Count > 0 && visited++ < 4096)
            {
                string current = queue.Dequeue();
                if (!containmentByParent.TryGetValue(current, out List<LocationContainmentLinkData> links)) continue;
                foreach (LocationContainmentLinkData child in links.Where(item => item.state == LocationLinkState.Active && IntervalActive(item.effectiveStartWorldTime, item.effectiveEndWorldTime, worldTime)).OrderBy(item => item.childLocationId, StringComparer.Ordinal))
                {
                    if (result.Add(child.childLocationId)) queue.Enqueue(child.childLocationId);
                }
            }

            return result;
        }

        private PoliticalTravelCrossingRecordData FindNearestCrossing(EntityLocationReferenceData entity, double worldTime, MovementHistoryVisibilityMode visibilityMode)
        {
            if (entity == null || !crossingsByTraveler.TryGetValue(entity.entityId ?? string.Empty, out List<PoliticalTravelCrossingRecordData> crossings)) return null;
            return crossings.Where(item => item.worldTime <= worldTime).OrderByDescending(item => item.worldTime).ThenBy(item => item.crossingId, StringComparer.Ordinal).FirstOrDefault()?.Clone();
        }

        private TravelEncounterRecordData FindActiveEncounter(EntityLocationReferenceData entity, double worldTime)
        {
            string entityKey = entity?.StableKey ?? string.Empty;
            return (source.travelConditions?.encounters ?? Array.Empty<TravelEncounterRecordData>())
                .Where(item => item != null && MatchesEntity(entityKey, item.traveler) && IntervalActive(item.createdWorldTime, item.resolvedWorldTime, worldTime))
                .OrderByDescending(item => item.triggeredWorldTime >= 0d ? item.triggeredWorldTime : item.createdWorldTime)
                .ThenBy(item => item.encounterId, StringComparer.Ordinal)
                .FirstOrDefault()?.Clone();
        }

        private IEnumerable<TravelConditionRecordData> ActiveConditionsForContext(HistoricalExactLocationResult exact, double worldTime, MovementHistoryVisibilityMode visibilityMode)
        {
            return (source.travelConditions?.conditions ?? Array.Empty<TravelConditionRecordData>())
                .Where(item => item != null && Visible(item.visibility, visibilityMode) && IntervalActive(item.startsWorldTime, item.endsWorldTime, worldTime))
                .OrderBy(item => item.conditionId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
        }

        private bool MatchesLocation(string queryLocationId, string exactLocationId, bool recursive, double worldTime)
        {
            string query = N(queryLocationId);
            if (string.IsNullOrWhiteSpace(query)) return true;
            if (string.Equals(query, exactLocationId, StringComparison.Ordinal)) return true;
            return recursive && ResolveHistoricalLocationPath(exactLocationId, worldTime).LocationPathIds.Contains(query, StringComparer.Ordinal);
        }

        private bool MatchesConditionLocation(string queryLocationId, TravelConditionTargetReferenceData target, bool recursive, double worldTime)
        {
            string query = N(queryLocationId);
            if (string.IsNullOrWhiteSpace(query)) return true;
            if (target == null) return false;
            return MatchesLocation(query, target.sourceLocationId, recursive, worldTime)
                || MatchesLocation(query, target.destinationLocationId, recursive, worldTime)
                || MatchesLocation(query, target.targetId, recursive, worldTime);
        }

        private static IEnumerable<MovementTimelineEntry> Order(IEnumerable<MovementTimelineEntry> entries)
        {
            return (entries ?? Array.Empty<MovementTimelineEntry>())
                .OrderBy(item => item.WorldTime)
                .ThenBy(item => item.Priority)
                .ThenBy(item => item.Kind.ToString(), StringComparer.Ordinal)
                .ThenBy(item => item.SourceParticipantId, StringComparer.Ordinal)
                .ThenBy(item => item.SourceRecordId, StringComparer.Ordinal);
        }

        private static MovementTimelineEntry E(MovementTimelineEntryKind kind, double time, int priority, string participant, string recordId, string entityKey, string locationId, string secondaryLocationId, string journeyId, string journeyStepId, string routeSegmentId, string checkpointId, string territoryId, string jurisdictionId, bool redacted, string diagnostics)
        {
            return new MovementTimelineEntry(kind, time, priority, participant, recordId, entityKey, locationId, secondaryLocationId, journeyId, journeyStepId, routeSegmentId, checkpointId, territoryId, jurisdictionId, redacted, diagnostics);
        }

        private static Dictionary<string, List<T>> Group<T>(IEnumerable<T> items, Func<T, string> key)
        {
            Dictionary<string, List<T>> result = new Dictionary<string, List<T>>(StringComparer.Ordinal);
            foreach (T item in items ?? Array.Empty<T>())
            {
                string id = N(key(item));
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!result.TryGetValue(id, out List<T> list))
                {
                    list = new List<T>();
                    result[id] = list;
                }

                list.Add(item);
            }

            return result;
        }

        private static bool JourneyActiveAt(TravelJourneyRecordData journey, double worldTime)
        {
            if (journey == null || journey.startedWorldTime < 0d || worldTime < journey.startedWorldTime) return false;
            if (journey.endedWorldTime >= 0d && worldTime > journey.endedWorldTime) return false;
            if ((journey.lifecycleState == TravelJourneyLifecycleState.Completed || journey.lifecycleState == TravelJourneyLifecycleState.Cancelled || journey.lifecycleState == TravelJourneyLifecycleState.Failed) && journey.endedWorldTime >= journey.startedWorldTime)
            {
                return worldTime <= journey.endedWorldTime;
            }

            return journey.lifecycleState == TravelJourneyLifecycleState.Active
                || journey.lifecycleState == TravelJourneyLifecycleState.Paused
                || journey.lifecycleState == TravelJourneyLifecycleState.Blocked
                || journey.lifecycleState == TravelJourneyLifecycleState.Replanning
                || journey.lifecycleState == TravelJourneyLifecycleState.Suspended;
        }

        private static bool IntervalActive(double start, double end, double time) => time >= start && (end < 0d || time <= end);
        private static bool IntervalsIntersect(double start, double end, double rangeStart, double rangeEnd)
        {
            double stop = end < 0d ? double.PositiveInfinity : end;
            return start <= rangeEnd && rangeStart <= stop;
        }

        private static bool InRange(double value, double start, double end) => value >= start && value <= end;
        private static bool MatchesEntity(string queryEntityKey, EntityLocationReferenceData entity) => string.IsNullOrWhiteSpace(queryEntityKey) || string.Equals(queryEntityKey, entity?.StableKey ?? string.Empty, StringComparison.Ordinal);
        private static bool MatchesTravelerPerson(EntityLocationReferenceData entity, string personId) => entity == null || string.IsNullOrWhiteSpace(entity.entityId) || string.Equals(entity.entityId, personId ?? string.Empty, StringComparison.Ordinal);
        private static bool MatchesJourney(string queryJourneyId, string journeyId) => string.IsNullOrWhiteSpace(queryJourneyId) || string.Equals(queryJourneyId.Trim(), journeyId ?? string.Empty, StringComparison.Ordinal);
        private static bool MatchesRoute(string queryRouteSegmentId, string routeSegmentId) => string.IsNullOrWhiteSpace(queryRouteSegmentId) || string.Equals(queryRouteSegmentId.Trim(), routeSegmentId ?? string.Empty, StringComparison.Ordinal);
        private static bool MatchesCheckpoint(string queryCheckpointId, string checkpointId) => string.IsNullOrWhiteSpace(queryCheckpointId) || string.Equals(queryCheckpointId.Trim(), checkpointId ?? string.Empty, StringComparison.Ordinal);
        private static bool MatchesTerritory(string queryTerritoryId, string sourceTerritoryId, string destinationTerritoryId) => string.IsNullOrWhiteSpace(queryTerritoryId) || string.Equals(queryTerritoryId.Trim(), sourceTerritoryId ?? string.Empty, StringComparison.Ordinal) || string.Equals(queryTerritoryId.Trim(), destinationTerritoryId ?? string.Empty, StringComparison.Ordinal);
        private static string EntityKey(EntityLocationReferenceData entity) => entity?.StableKey ?? string.Empty;
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static double EventEndTime(TravelJourneyRecordData journey) => journey.endedWorldTime >= 0d ? journey.endedWorldTime : Math.Max(journey.lastProgressWorldTime, journey.startedWorldTime);

        private static bool Visible(Enum visibility, MovementHistoryVisibilityMode mode)
        {
            if (mode == MovementHistoryVisibilityMode.DevelopmentAuthoritative || mode == MovementHistoryVisibilityMode.AuthorizedInstitutional) return true;
            string value = visibility?.ToString() ?? string.Empty;
            if (mode == MovementHistoryVisibilityMode.OwnerOrTraveler) return !Contains(value, "Hidden");
            return !Contains(value, "Hidden")
                && !Contains(value, "Secret")
                && !Contains(value, "Restricted")
                && !Contains(value, "Diagnostic");
        }

        private static bool Contains(string value, string fragment)
        {
            return (value ?? string.Empty).IndexOf(fragment ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
