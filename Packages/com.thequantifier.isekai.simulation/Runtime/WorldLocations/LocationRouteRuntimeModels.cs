using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    [Serializable]
    public struct TravelDistance : IComparable<TravelDistance>, IEquatable<TravelDistance>
    {
        public double meters;
        public TravelDistance(double meters) { this.meters = meters; }
        public static TravelDistance Zero => new TravelDistance(0d);
        public bool IsValid => IsFinite(meters) && meters >= 0d;
        public int CompareTo(TravelDistance other) => meters.CompareTo(other.meters);
        public bool Equals(TravelDistance other) => Math.Abs(meters - other.meters) < 0.000001d;
        public override bool Equals(object obj) => obj is TravelDistance other && Equals(other);
        public override int GetHashCode() => meters.GetHashCode();
        public override string ToString() => $"{meters:0.###}m";
        public static TravelDistance Add(TravelDistance left, TravelDistance right) => new TravelDistance(Clamp(left.meters + right.meters));
        public static bool TryCreate(double value, out TravelDistance distance)
        {
            distance = new TravelDistance(value);
            return distance.IsValid;
        }

        private static double Clamp(double value) => IsFinite(value) && value >= 0d ? Math.Min(value, 999999999d) : 0d;
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    [Serializable]
    public struct TravelCost : IComparable<TravelCost>, IEquatable<TravelCost>
    {
        public double units;
        public TravelCost(double units) { this.units = units; }
        public static TravelCost Zero => new TravelCost(0d);
        public bool IsValid => IsFinite(units) && units >= 0d;
        public int CompareTo(TravelCost other) => units.CompareTo(other.units);
        public bool Equals(TravelCost other) => Math.Abs(units - other.units) < 0.000001d;
        public override bool Equals(object obj) => obj is TravelCost other && Equals(other);
        public override int GetHashCode() => units.GetHashCode();
        public override string ToString() => $"{units:0.###}cu";
        public static TravelCost Add(TravelCost left, TravelCost right) => new TravelCost(Clamp(left.units + right.units));
        public static bool TryCreate(double value, out TravelCost cost)
        {
            cost = new TravelCost(value);
            return cost.IsValid;
        }

        private static double Clamp(double value) => IsFinite(value) && value >= 0d ? Math.Min(value, 999999999d) : 0d;
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    [Serializable]
    public sealed class LocationRouteSegmentRecordData
    {
        public string segmentId;
        public string segmentDefinitionId;
        public string worldId;
        public string displayName;
        public string sourceLocationId;
        public string destinationLocationId;
        public LocationConnectionDirectionality directionality = LocationConnectionDirectionality.Bidirectional;
        public RouteSegmentLifecycleState lifecycleState = RouteSegmentLifecycleState.Active;
        public RouteSegmentBlockageState blockageState = RouteSegmentBlockageState.Clear;
        public double distanceMeters;
        public double baseCostUnits;
        public string[] supportedTravelModeDefinitionIds = Array.Empty<string>();
        public string[] accessPolicyDefinitionIds = Array.Empty<string>();
        public string[] networkIds = Array.Empty<string>();
        public RouteVisibility visibility = RouteVisibility.Public;
        public double createdWorldTime;
        public double endedWorldTime = -1d;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public LocationRouteSegmentRecordData Clone()
        {
            return new LocationRouteSegmentRecordData
            {
                segmentId = N(segmentId),
                segmentDefinitionId = N(segmentDefinitionId),
                worldId = N(worldId),
                displayName = N(displayName),
                sourceLocationId = N(sourceLocationId),
                destinationLocationId = N(destinationLocationId),
                directionality = directionality,
                lifecycleState = lifecycleState,
                blockageState = blockageState,
                distanceMeters = distanceMeters,
                baseCostUnits = baseCostUnits,
                supportedTravelModeDefinitionIds = C(supportedTravelModeDefinitionIds),
                accessPolicyDefinitionIds = C(accessPolicyDefinitionIds),
                networkIds = C(networkIds),
                visibility = visibility,
                createdWorldTime = createdWorldTime,
                endedWorldTime = endedWorldTime,
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
    public sealed class LocationRouteNetworkRecordData
    {
        public string networkId;
        public string worldId;
        public string displayName;
        public RouteNetworkCategory category = RouteNetworkCategory.Custom;
        public string[] segmentIds = Array.Empty<string>();
        public RouteVisibility visibility = RouteVisibility.Public;
        public RouteSegmentLifecycleState lifecycleState = RouteSegmentLifecycleState.Active;
        public string ownerSubjectType;
        public string ownerSubjectId;
        public double createdWorldTime;
        public double endedWorldTime = -1d;
        public long revision = 1L;

        public LocationRouteNetworkRecordData Clone()
        {
            return new LocationRouteNetworkRecordData
            {
                networkId = N(networkId),
                worldId = N(worldId),
                displayName = N(displayName),
                category = category,
                segmentIds = C(segmentIds),
                visibility = visibility,
                lifecycleState = lifecycleState,
                ownerSubjectType = N(ownerSubjectType),
                ownerSubjectId = N(ownerSubjectId),
                createdWorldTime = createdWorldTime,
                endedWorldTime = endedWorldTime,
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    public sealed class LocationRouteSegmentHistoryData
    {
        public string historyId;
        public string segmentId;
        public string operation;
        public RouteSegmentLifecycleState lifecycleState;
        public RouteSegmentBlockageState blockageState;
        public double distanceMeters;
        public double baseCostUnits;
        public double worldTime;
        public string actorKey;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision;

        public LocationRouteSegmentHistoryData Clone()
        {
            return new LocationRouteSegmentHistoryData
            {
                historyId = N(historyId),
                segmentId = N(segmentId),
                operation = N(operation),
                lifecycleState = lifecycleState,
                blockageState = blockageState,
                distanceMeters = distanceMeters,
                baseCostUnits = baseCostUnits,
                worldTime = worldTime,
                actorKey = N(actorKey),
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class LocationRouteTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string segmentId;
        public string resultReferenceId;
        public long revision;

        public LocationRouteTransactionRecordData Clone()
        {
            return new LocationRouteTransactionRecordData
            {
                transactionId = N(transactionId),
                operation = N(operation),
                segmentId = N(segmentId),
                resultReferenceId = N(resultReferenceId),
                revision = revision
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class LocationRouteRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public LocationRouteSegmentRecordData[] segments = Array.Empty<LocationRouteSegmentRecordData>();
        public LocationRouteNetworkRecordData[] networks = Array.Empty<LocationRouteNetworkRecordData>();
        public LocationRouteSegmentHistoryData[] history = Array.Empty<LocationRouteSegmentHistoryData>();
        public LocationRouteTransactionRecordData[] transactions = Array.Empty<LocationRouteTransactionRecordData>();

        public LocationRouteRuntimeSaveData Clone()
        {
            return new LocationRouteRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = N(worldId),
                revision = revision,
                segments = (segments ?? Array.Empty<LocationRouteSegmentRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                networks = (networks ?? Array.Empty<LocationRouteNetworkRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                history = (history ?? Array.Empty<LocationRouteSegmentHistoryData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                transactions = (transactions ?? Array.Empty<LocationRouteTransactionRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray()
            };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class LocationRouteSegmentSnapshot
    {
        private readonly LocationRouteSegmentRecordData data;
        public LocationRouteSegmentSnapshot(LocationRouteSegmentRecordData record) { data = record?.Clone() ?? new LocationRouteSegmentRecordData(); }
        public string SegmentId => data.segmentId ?? string.Empty;
        public string SegmentDefinitionId => data.segmentDefinitionId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public string DisplayName => data.displayName ?? string.Empty;
        public string SourceLocationId => data.sourceLocationId ?? string.Empty;
        public string DestinationLocationId => data.destinationLocationId ?? string.Empty;
        public LocationConnectionDirectionality Directionality => data.directionality;
        public RouteSegmentLifecycleState LifecycleState => data.lifecycleState;
        public RouteSegmentBlockageState BlockageState => data.blockageState;
        public TravelDistance Distance => new TravelDistance(data.distanceMeters);
        public TravelCost BaseCost => new TravelCost(data.baseCostUnits);
        public IReadOnlyList<string> SupportedTravelModeDefinitionIds => (data.supportedTravelModeDefinitionIds ?? Array.Empty<string>()).ToArray();
        public IReadOnlyList<string> AccessPolicyDefinitionIds => (data.accessPolicyDefinitionIds ?? Array.Empty<string>()).ToArray();
        public IReadOnlyList<string> NetworkIds => (data.networkIds ?? Array.Empty<string>()).ToArray();
        public RouteVisibility Visibility => data.visibility;
        public double CreatedWorldTime => data.createdWorldTime;
        public double EndedWorldTime => data.endedWorldTime;
        public long Revision => data.revision;
        public LocationRouteSegmentRecordData ToSaveData() => data.Clone();
    }

    public sealed class LocationRouteNetworkSnapshot
    {
        private readonly LocationRouteNetworkRecordData data;
        public LocationRouteNetworkSnapshot(LocationRouteNetworkRecordData record) { data = record?.Clone() ?? new LocationRouteNetworkRecordData(); }
        public string NetworkId => data.networkId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public string DisplayName => data.displayName ?? string.Empty;
        public RouteNetworkCategory Category => data.category;
        public IReadOnlyList<string> SegmentIds => (data.segmentIds ?? Array.Empty<string>()).ToArray();
        public RouteVisibility Visibility => data.visibility;
        public RouteSegmentLifecycleState LifecycleState => data.lifecycleState;
        public string OwnerSubjectType => data.ownerSubjectType ?? string.Empty;
        public string OwnerSubjectId => data.ownerSubjectId ?? string.Empty;
        public long Revision => data.revision;
        public LocationRouteNetworkRecordData ToSaveData() => data.Clone();
    }

    public sealed class LocationRouteUnifiedEdgeSnapshot
    {
        public string EdgeId { get; set; } = string.Empty;
        public RouteEdgeKind EdgeKind { get; set; } = RouteEdgeKind.Unknown;
        public string DefinitionId { get; set; } = string.Empty;
        public RouteSegmentCategory Category { get; set; } = RouteSegmentCategory.Custom;
        public string SourceLocationId { get; set; } = string.Empty;
        public string DestinationLocationId { get; set; } = string.Empty;
        public LocationConnectionDirectionality Directionality { get; set; } = LocationConnectionDirectionality.Bidirectional;
        public TravelDistance Distance { get; set; } = TravelDistance.Zero;
        public TravelCost BaseCost { get; set; } = TravelCost.Zero;
        public IReadOnlyList<string> SupportedTravelModeDefinitionIds { get; set; } = Array.Empty<string>();
        public RouteVisibility Visibility { get; set; } = RouteVisibility.Public;
        public RouteSegmentLifecycleState LifecycleState { get; set; } = RouteSegmentLifecycleState.Active;
        public RouteSegmentBlockageState BlockageState { get; set; } = RouteSegmentBlockageState.Clear;
        public IReadOnlyList<string> AccessPolicyDefinitionIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> NetworkIds { get; set; } = Array.Empty<string>();
        public long SourceRevision { get; set; }
        public string Diagnostics { get; set; } = string.Empty;

        public LocationRouteUnifiedEdgeSnapshot Clone()
        {
            return new LocationRouteUnifiedEdgeSnapshot
            {
                EdgeId = EdgeId ?? string.Empty,
                EdgeKind = EdgeKind,
                DefinitionId = DefinitionId ?? string.Empty,
                Category = Category,
                SourceLocationId = SourceLocationId ?? string.Empty,
                DestinationLocationId = DestinationLocationId ?? string.Empty,
                Directionality = Directionality,
                Distance = Distance,
                BaseCost = BaseCost,
                SupportedTravelModeDefinitionIds = (SupportedTravelModeDefinitionIds ?? Array.Empty<string>()).ToArray(),
                Visibility = Visibility,
                LifecycleState = LifecycleState,
                BlockageState = BlockageState,
                AccessPolicyDefinitionIds = (AccessPolicyDefinitionIds ?? Array.Empty<string>()).ToArray(),
                NetworkIds = (NetworkIds ?? Array.Empty<string>()).ToArray(),
                SourceRevision = SourceRevision,
                Diagnostics = Diagnostics ?? string.Empty
            };
        }
    }

    public sealed class RouteRequirementSummary
    {
        public string[] requiredKeys = Array.Empty<string>();
        public string[] requiredPermits = Array.Empty<string>();
        public string[] requiredMemberships = Array.Empty<string>();
        public string[] requiredOffices = Array.Empty<string>();
        public string[] requiredAuthorities = Array.Empty<string>();
        public string[] requiredCustodyRoles = Array.Empty<string>();
        public string[] requiredActions = Array.Empty<string>();
        public string[] requiredLegalTravelActions = Array.Empty<string>();
        public string[] requiredCheckpointIds = Array.Empty<string>();
        public string[] requiredPoliticalTerritoryIds = Array.Empty<string>();
        public string[] hiddenRouteEdges = Array.Empty<string>();

        public RouteRequirementSummary Clone()
        {
            return new RouteRequirementSummary
            {
                requiredKeys = C(requiredKeys),
                requiredPermits = C(requiredPermits),
                requiredMemberships = C(requiredMemberships),
                requiredOffices = C(requiredOffices),
                requiredAuthorities = C(requiredAuthorities),
                requiredCustodyRoles = C(requiredCustodyRoles),
                requiredActions = C(requiredActions),
                requiredLegalTravelActions = C(requiredLegalTravelActions),
                requiredCheckpointIds = C(requiredCheckpointIds),
                requiredPoliticalTerritoryIds = C(requiredPoliticalTerritoryIds),
                hiddenRouteEdges = C(hiddenRouteEdges)
            };
        }

        private static string[] C(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public sealed class LocationRoutePlanStep
    {
        public string SourceLocationId { get; set; } = string.Empty;
        public string DestinationLocationId { get; set; } = string.Empty;
        public string EdgeId { get; set; } = string.Empty;
        public RouteEdgeKind EdgeKind { get; set; }
        public RouteSegmentCategory Category { get; set; }
        public RouteVisibility Visibility { get; set; } = RouteVisibility.Public;
        public TravelDistance Distance { get; set; }
        public TravelCost Cost { get; set; }
        public string AccessState { get; set; } = string.Empty;
        public string TravelModeDefinitionId { get; set; } = string.Empty;
        public long SourceRevision { get; set; }
        public string[] RequiredActions { get; set; } = Array.Empty<string>();

        public LocationRoutePlanStep Clone()
        {
            return new LocationRoutePlanStep
            {
                SourceLocationId = SourceLocationId ?? string.Empty,
                DestinationLocationId = DestinationLocationId ?? string.Empty,
                EdgeId = EdgeId ?? string.Empty,
                EdgeKind = EdgeKind,
                Category = Category,
                Visibility = Visibility,
                Distance = Distance,
                Cost = Cost,
                AccessState = AccessState ?? string.Empty,
                TravelModeDefinitionId = TravelModeDefinitionId ?? string.Empty,
                SourceRevision = SourceRevision,
                RequiredActions = (RequiredActions ?? Array.Empty<string>()).ToArray()
            };
        }
    }

    public sealed class LocationRoutePlan
    {
        private readonly LocationRoutePlanStep[] steps;
        private readonly string[] nodes;
        private readonly RouteRequirementSummary requirements;

        public LocationRoutePlan(string planId, string originLocationId, string destinationLocationId, EntityLocationReferenceData traveler, string travelModeDefinitionId, RoutePlanningObjective objective, IEnumerable<string> orderedNodes, IEnumerable<LocationRoutePlanStep> orderedSteps, TravelDistance totalDistance, TravelCost totalCost, RouteRequirementSummary requirementSummary, long routeRevision, long connectionRevision, bool knowledgeFiltered, string diagnostics, long conditionRevision = 0L)
        {
            PlanId = planId ?? string.Empty;
            OriginLocationId = originLocationId ?? string.Empty;
            DestinationLocationId = destinationLocationId ?? string.Empty;
            Traveler = traveler?.Clone();
            TravelModeDefinitionId = travelModeDefinitionId ?? string.Empty;
            Objective = objective;
            nodes = (orderedNodes ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            steps = (orderedSteps ?? Array.Empty<LocationRoutePlanStep>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
            TotalDistance = totalDistance;
            TotalCost = totalCost;
            requirements = requirementSummary?.Clone() ?? new RouteRequirementSummary();
            RouteRevision = routeRevision;
            ConnectionRevision = connectionRevision;
            ConditionRevision = conditionRevision;
            KnowledgeFiltered = knowledgeFiltered;
            Diagnostics = diagnostics ?? string.Empty;
        }

        public string PlanId { get; }
        public string OriginLocationId { get; }
        public string DestinationLocationId { get; }
        public EntityLocationReferenceData Traveler { get; }
        public string TravelModeDefinitionId { get; }
        public RoutePlanningObjective Objective { get; }
        public IReadOnlyList<string> OrderedLocationIds => nodes.ToArray();
        public IReadOnlyList<LocationRoutePlanStep> Steps => steps.Select(step => step.Clone()).ToArray();
        public TravelDistance TotalDistance { get; }
        public TravelCost TotalCost { get; }
        public int EdgeCount => steps.Length;
        public RouteRequirementSummary Requirements => requirements.Clone();
        public long RouteRevision { get; }
        public long ConnectionRevision { get; }
        public long ConditionRevision { get; }
        public bool KnowledgeFiltered { get; }
        public string Diagnostics { get; }
    }

    public sealed class LocationRouteSearchRequest
    {
        public string requestId;
        public EntityLocationReferenceData traveler;
        public string originLocationId;
        public string destinationLocationId;
        public string travelModeDefinitionId;
        public RoutePlanningObjective objective = RoutePlanningObjective.ShortestDistance;
        public RouteAccessEvaluationMode accessMode = RouteAccessEvaluationMode.StructuralOnly;
        public RouteKnowledgeMode knowledgeMode = RouteKnowledgeMode.AuthoritativeDevelopment;
        public LocationConnectionAccessContextData accessContext;
        public double worldTime;
        public int maximumVisitedNodes = 1000;
        public int maximumExpandedEdges = 5000;
        public int maximumDepth = 256;
        public double maximumDistanceMeters = -1d;
        public double maximumCostUnits = -1d;
        public string[] forbiddenLocationIds = Array.Empty<string>();
        public RouteSegmentCategory[] forbiddenCategories = Array.Empty<RouteSegmentCategory>();
        public RouteSegmentCategory[] preferredCategories = Array.Empty<RouteSegmentCategory>();
        public string[] knownEdgeIds = Array.Empty<string>();
        public string[] knownLocationIds = Array.Empty<string>();
        public string[] travelerCapabilityIds = Array.Empty<string>();
        public string[] travelerEquipmentDefinitionIds = Array.Empty<string>();
        public TravelConditionEvaluationMode conditionEvaluationMode = TravelConditionEvaluationMode.IgnoreDynamicConditions;
        public TravelLegalComplianceMode legalComplianceMode = TravelLegalComplianceMode.StructuralOnlyDevelopment;
        public string[] knownConditionIds = Array.Empty<string>();
        public string[] knownEncounterIds = Array.Empty<string>();
        public string[] knownHazardExposureIds = Array.Empty<string>();
        public bool includeHiddenDevelopmentConditions;
        public bool includeHiddenDevelopmentRoutes;
        public bool preview;
    }

    public sealed class LocationRouteSearchResult
    {
        private LocationRouteSearchResult(RoutePlanningStatus status, string message, LocationRoutePlan plan, int visited, int expanded, bool budgetExceeded)
        {
            Status = status;
            Message = message ?? string.Empty;
            Plan = plan;
            VisitedNodeCount = visited;
            ExpandedEdgeCount = expanded;
            BudgetExceeded = budgetExceeded;
        }

        public RoutePlanningStatus Status { get; }
        public string Message { get; }
        public LocationRoutePlan Plan { get; }
        public int VisitedNodeCount { get; }
        public int ExpandedEdgeCount { get; }
        public bool BudgetExceeded { get; }
        public bool Succeeded => Status == RoutePlanningStatus.Succeeded || Status == RoutePlanningStatus.SelfRoute || Status == RoutePlanningStatus.Preview;
        public static LocationRouteSearchResult Success(LocationRoutePlan plan, string message, int visited, int expanded, bool preview = false) => new LocationRouteSearchResult(preview ? RoutePlanningStatus.Preview : plan?.EdgeCount == 0 ? RoutePlanningStatus.SelfRoute : RoutePlanningStatus.Succeeded, message, plan, visited, expanded, false);
        public static LocationRouteSearchResult Failure(RoutePlanningStatus status, string message, int visited = 0, int expanded = 0, bool budget = false) => new LocationRouteSearchResult(status, message, null, visited, expanded, budget);
    }

    public sealed class LocationRouteMutationResult
    {
        private LocationRouteMutationResult(RouteMutationStatus status, string message, long beforeRevision, long afterRevision, LocationRouteSegmentSnapshot segment, LocationRouteNetworkSnapshot network, bool preview = false, bool duplicate = false)
        {
            Status = status;
            Message = message ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Segment = segment;
            Network = network;
            Preview = preview;
            Duplicate = duplicate;
        }

        public RouteMutationStatus Status { get; }
        public string Message { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public LocationRouteSegmentSnapshot Segment { get; }
        public LocationRouteNetworkSnapshot Network { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public bool Succeeded => Status == RouteMutationStatus.Succeeded || Status == RouteMutationStatus.Preview || Duplicate;
        public static LocationRouteMutationResult Success(LocationRouteSegmentSnapshot segment, string message, long before, long after, bool preview = false, bool duplicate = false) => new LocationRouteMutationResult(preview ? RouteMutationStatus.Preview : duplicate ? RouteMutationStatus.Duplicate : RouteMutationStatus.Succeeded, message, before, after, segment, null, preview, duplicate);
        public static LocationRouteMutationResult NetworkSuccess(LocationRouteNetworkSnapshot network, string message, long before, long after, bool preview = false, bool duplicate = false) => new LocationRouteMutationResult(preview ? RouteMutationStatus.Preview : duplicate ? RouteMutationStatus.Duplicate : RouteMutationStatus.Succeeded, message, before, after, null, network, preview, duplicate);
        public static LocationRouteMutationResult Failure(RouteMutationStatus status, string message, long before) => new LocationRouteMutationResult(status, message, before, before, null, null);
    }

    public sealed class LocationRouteSegmentCreateRequest
    {
        public string transactionId;
        public string segmentId;
        public string segmentDefinitionId;
        public string displayName;
        public string sourceLocationId;
        public string destinationLocationId;
        public LocationConnectionDirectionality directionality = LocationConnectionDirectionality.Unknown;
        public double distanceMeters = -1d;
        public double baseCostUnits = -1d;
        public string[] supportedTravelModeDefinitionIds = Array.Empty<string>();
        public string[] accessPolicyDefinitionIds = Array.Empty<string>();
        public string[] networkIds = Array.Empty<string>();
        public RouteVisibility visibility = RouteVisibility.Public;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public sealed class LocationRouteSegmentMutationRequest
    {
        public string transactionId;
        public string segmentId;
        public RouteSegmentLifecycleState lifecycleState = RouteSegmentLifecycleState.Unknown;
        public RouteSegmentBlockageState blockageState = RouteSegmentBlockageState.Unknown;
        public double distanceMeters = -1d;
        public double baseCostUnits = -1d;
        public string[] supportedTravelModeDefinitionIds;
        public string[] networkIds;
        public double worldTime;
        public EntityLocationReferenceData actor;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public sealed class LocationRouteNetworkCreateRequest
    {
        public string transactionId;
        public string networkId;
        public string displayName;
        public RouteNetworkCategory category = RouteNetworkCategory.Custom;
        public string[] segmentIds = Array.Empty<string>();
        public RouteVisibility visibility = RouteVisibility.Public;
        public string ownerSubjectType;
        public string ownerSubjectId;
        public double worldTime;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public sealed class LocationRouteRevalidationResult
    {
        public LocationRouteRevalidationResult(RoutePlanRevalidationStatus status, string message, string invalidEdgeId = "")
        {
            Status = status;
            Message = message ?? string.Empty;
            InvalidEdgeId = invalidEdgeId ?? string.Empty;
        }

        public RoutePlanRevalidationStatus Status { get; }
        public string Message { get; }
        public string InvalidEdgeId { get; }
        public bool Valid => Status == RoutePlanRevalidationStatus.Valid;
    }

    public sealed class LocationRouteReachabilityResult
    {
        public LocationRouteReachabilityResult(IEnumerable<string> locations, int visited, int expanded, bool budgetExceeded)
        {
            ReachableLocationIds = (locations ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            VisitedNodeCount = visited;
            ExpandedEdgeCount = expanded;
            BudgetExceeded = budgetExceeded;
        }

        public IReadOnlyList<string> ReachableLocationIds { get; }
        public int VisitedNodeCount { get; }
        public int ExpandedEdgeCount { get; }
        public bool BudgetExceeded { get; }
    }
}
