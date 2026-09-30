using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class LocationRouteRuntime : IDisposable
    {
        private const string WalkingModeId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId;
        private readonly Dictionary<string, LocationRouteSegmentRecordData> segmentsById = new Dictionary<string, LocationRouteSegmentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationRouteNetworkRecordData> networksById = new Dictionary<string, LocationRouteNetworkRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationRouteSegmentHistoryData> historyById = new Dictionary<string, LocationRouteSegmentHistoryData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationRouteTransactionRecordData> transactionsById = new Dictionary<string, LocationRouteTransactionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> outgoingSegmentsByLocation = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> incomingSegmentsByLocation = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> segmentsByDefinition = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<RouteSegmentCategory, List<string>> segmentsByCategory = new Dictionary<RouteSegmentCategory, List<string>>();
        private readonly Dictionary<string, List<string>> segmentsByNetwork = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> segmentsByTravelMode = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private LocationRuntime locations;
        private LocationConnectionRuntime connections;
        private TravelConditionRuntime travelConditions;
        private string worldId = PersistenceService.LocalWorldId;
        private bool disposed;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public string WorldId => worldId;
        public int SegmentCount => segmentsById.Count;
        public int NetworkCount => networksById.Count;
        public IReadOnlyList<LocationRouteSegmentSnapshot> Segments => segmentsById.Values.OrderBy(item => item.segmentId, StringComparer.Ordinal).Select(BuildSegmentSnapshot).ToArray();
        public IReadOnlyList<LocationRouteNetworkSnapshot> Networks => networksById.Values.OrderBy(item => item.networkId, StringComparer.Ordinal).Select(BuildNetworkSnapshot).ToArray();
        public IReadOnlyList<LocationRouteSegmentHistoryData> History => historyById.Values.OrderBy(item => item.worldTime).ThenBy(item => item.historyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public long UnifiedGraphRevision => Revision + ((connections?.Revision ?? 0L) * 1000003L);

        public void Configure(DefinitionRegistry definitionRegistry, LocationRuntime locationRuntime, LocationConnectionRuntime connectionRuntime, string runtimeWorldId = PersistenceService.LocalWorldId, TravelConditionRuntime conditionRuntime = null)
        {
            registry = definitionRegistry ?? registry;
            locations = locationRuntime ?? locations;
            connections = connectionRuntime ?? connections;
            travelConditions = conditionRuntime ?? travelConditions;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId.Trim();
            disposed = false;
        }

        public LocationRouteMutationResult CreateSegment(LocationRouteSegmentCreateRequest request)
        {
            request ??= new LocationRouteSegmentCreateRequest();
            long before = Revision;
            if (!Ready(before, out LocationRouteMutationResult ready)) return ready;
            if (!ValidateRevision(request.expectedRevision, before, out LocationRouteMutationResult revisionFailure)) return revisionFailure;

            string id = N(request.segmentId);
            if (TryDuplicate(N(request.transactionId), id, "route.segment.create", before, out LocationRouteMutationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(id)) return Fail(RouteMutationStatus.InvalidRequest, "Route segment ID is required.", before);
            if (segmentsById.ContainsKey(id)) return Fail(RouteMutationStatus.Duplicate, $"Route segment '{id}' already exists.", before);
            if (!TryGetSegmentDefinition(request.segmentDefinitionId, before, out RouteSegmentDefinition definition, out LocationRouteMutationResult failure)) return failure;
            if (!ValidateLocation(request.sourceLocationId, before, out LocationSnapshot source, out failure)) return failure;
            if (!ValidateLocation(request.destinationLocationId, before, out LocationSnapshot destination, out failure)) return failure;

            LocationConnectionDirectionality directionality = request.directionality == LocationConnectionDirectionality.Unknown ? definition.DefaultDirectionality : request.directionality;
            if (!ValidDirectionality(directionality)) return Fail(RouteMutationStatus.InvalidDirection, $"Route segment directionality '{directionality}' is invalid.", before);
            double distance = request.distanceMeters >= 0d ? request.distanceMeters : definition.DefaultDistanceMeters;
            double cost = request.baseCostUnits >= 0d ? request.baseCostUnits : definition.DefaultCostUnits;
            if (!ValidateDistance(distance, definition, before, out failure)) return failure;
            if (!ValidateCost(cost, before, out failure)) return failure;
            string[] modes = Clean(request.supportedTravelModeDefinitionIds).Length == 0 ? Clean(definition.SupportedTravelModeDefinitionIds) : Clean(request.supportedTravelModeDefinitionIds);
            if (!ValidateModes(modes, definition, before, out failure)) return failure;
            if (!ValidateAccessPolicies(request.accessPolicyDefinitionIds, definition, before, out failure)) return failure;
            if (!ValidateNetworks(request.networkIds, before, out failure)) return failure;
            if ((request.visibility == RouteVisibility.Secret || request.visibility == RouteVisibility.Hidden) && !definition.MayBeHidden) return Fail(RouteMutationStatus.InvalidRequest, $"Route segment definition '{definition.Id}' does not allow hidden visibility.", before);

            LocationRouteSegmentRecordData record = new LocationRouteSegmentRecordData
            {
                segmentId = id,
                segmentDefinitionId = definition.Id,
                worldId = worldId,
                displayName = string.IsNullOrWhiteSpace(request.displayName) ? definition.DisplayName : request.displayName.Trim(),
                sourceLocationId = source.LocationId,
                destinationLocationId = destination.LocationId,
                directionality = directionality,
                lifecycleState = RouteSegmentLifecycleState.Active,
                blockageState = RouteSegmentBlockageState.Clear,
                distanceMeters = distance,
                baseCostUnits = cost,
                supportedTravelModeDefinitionIds = modes,
                accessPolicyDefinitionIds = definition.SupportsAccessPolicies ? Clean(request.accessPolicyDefinitionIds) : Array.Empty<string>(),
                networkIds = definition.SupportsNetworkMembership ? Clean(request.networkIds) : Array.Empty<string>(),
                visibility = request.visibility,
                createdWorldTime = request.worldTime,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };

            if (request.preview) return LocationRouteMutationResult.Success(BuildSegmentSnapshot(record), "Route segment create preview.", before, before, preview: true);

            segmentsById.Add(id, record);
            AddHistory(record, "create", request.worldTime, null, request.sourceEventId, request.sourceRecordId, request.provenanceId);
            RebuildIndexes();
            Complete(N(request.transactionId), "route.segment.create", id, id);
            Touch();
            return LocationRouteMutationResult.Success(BuildSegmentSnapshot(record), "Route segment created.", before, Revision);
        }

        public LocationRouteMutationResult MutateSegment(LocationRouteSegmentMutationRequest request)
        {
            request ??= new LocationRouteSegmentMutationRequest();
            long before = Revision;
            if (!Ready(before, out LocationRouteMutationResult ready)) return ready;
            if (!ValidateRevision(request.expectedRevision, before, out LocationRouteMutationResult revisionFailure)) return revisionFailure;
            string id = N(request.segmentId);
            if (TryDuplicate(N(request.transactionId), id, "route.segment.mutate", before, out LocationRouteMutationResult duplicate)) return duplicate;
            if (!segmentsById.TryGetValue(id, out LocationRouteSegmentRecordData existing)) return Fail(RouteMutationStatus.MissingSegment, $"Route segment '{id}' is missing.", before);
            if (!TryGetSegmentDefinition(existing.segmentDefinitionId, before, out RouteSegmentDefinition definition, out LocationRouteMutationResult failure)) return failure;

            LocationRouteSegmentRecordData changed = existing.Clone();
            if (request.lifecycleState != RouteSegmentLifecycleState.Unknown)
            {
                if (!ValidLifecycleTransition(changed.lifecycleState, request.lifecycleState)) return Fail(RouteMutationStatus.InvalidLifecycleTransition, $"Cannot transition route segment '{id}' from {changed.lifecycleState} to {request.lifecycleState}.", before);
                changed.lifecycleState = request.lifecycleState;
                if (request.lifecycleState == RouteSegmentLifecycleState.Destroyed || request.lifecycleState == RouteSegmentLifecycleState.Historical) changed.endedWorldTime = request.worldTime;
                if (request.lifecycleState == RouteSegmentLifecycleState.Active) changed.endedWorldTime = -1d;
            }
            if (request.blockageState != RouteSegmentBlockageState.Unknown) changed.blockageState = request.blockageState;
            if (request.distanceMeters >= 0d)
            {
                if (!ValidateDistance(request.distanceMeters, definition, before, out failure)) return failure;
                changed.distanceMeters = request.distanceMeters;
            }
            if (request.baseCostUnits >= 0d)
            {
                if (!ValidateCost(request.baseCostUnits, before, out failure)) return failure;
                changed.baseCostUnits = request.baseCostUnits;
            }
            if (request.supportedTravelModeDefinitionIds != null)
            {
                string[] modes = Clean(request.supportedTravelModeDefinitionIds);
                if (!ValidateModes(modes, definition, before, out failure)) return failure;
                changed.supportedTravelModeDefinitionIds = modes;
            }
            if (request.networkIds != null)
            {
                if (!ValidateNetworks(request.networkIds, before, out failure)) return failure;
                changed.networkIds = Clean(request.networkIds);
            }

            if (request.preview) return LocationRouteMutationResult.Success(BuildSegmentSnapshot(changed), "Route segment mutation preview.", before, before, preview: true);

            changed.sourceEventId = First(request.sourceEventId, changed.sourceEventId);
            changed.sourceRecordId = First(request.sourceRecordId, changed.sourceRecordId);
            changed.provenanceId = First(request.provenanceId, changed.provenanceId);
            changed.revision++;
            segmentsById[id] = changed;
            AddHistory(changed, "mutate", request.worldTime, request.actor, request.sourceEventId, request.sourceRecordId, request.provenanceId);
            RebuildIndexes();
            Complete(N(request.transactionId), "route.segment.mutate", id, id);
            Touch();
            return LocationRouteMutationResult.Success(BuildSegmentSnapshot(changed), "Route segment updated.", before, Revision);
        }

        public LocationRouteMutationResult CreateNetwork(LocationRouteNetworkCreateRequest request)
        {
            request ??= new LocationRouteNetworkCreateRequest();
            long before = Revision;
            if (!Ready(before, out LocationRouteMutationResult ready)) return ready;
            if (!ValidateRevision(request.expectedRevision, before, out LocationRouteMutationResult revisionFailure)) return revisionFailure;
            string id = N(request.networkId);
            if (TryDuplicate(N(request.transactionId), id, "route.network.create", before, out LocationRouteMutationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(id)) return Fail(RouteMutationStatus.InvalidRequest, "Route network ID is required.", before);
            if (networksById.ContainsKey(id)) return Fail(RouteMutationStatus.Duplicate, $"Route network '{id}' already exists.", before);
            if (!Enum.IsDefined(typeof(RouteNetworkCategory), request.category) || request.category == RouteNetworkCategory.Unknown) return Fail(RouteMutationStatus.InvalidRequest, "Route network category is invalid.", before);
            foreach (string segmentId in Clean(request.segmentIds))
            {
                if (!segmentsById.ContainsKey(segmentId)) return Fail(RouteMutationStatus.MissingSegment, $"Route network '{id}' references missing segment '{segmentId}'.", before);
            }

            LocationRouteNetworkRecordData record = new LocationRouteNetworkRecordData
            {
                networkId = id,
                worldId = worldId,
                displayName = string.IsNullOrWhiteSpace(request.displayName) ? id : request.displayName.Trim(),
                category = request.category,
                segmentIds = Clean(request.segmentIds),
                visibility = request.visibility,
                lifecycleState = RouteSegmentLifecycleState.Active,
                ownerSubjectType = N(request.ownerSubjectType),
                ownerSubjectId = N(request.ownerSubjectId),
                createdWorldTime = request.worldTime,
                revision = 1L
            };

            if (request.preview) return LocationRouteMutationResult.NetworkSuccess(BuildNetworkSnapshot(record), "Route network create preview.", before, before, preview: true);

            networksById.Add(id, record);
            foreach (string segmentId in record.segmentIds)
            {
                LocationRouteSegmentRecordData segment = segmentsById[segmentId].Clone();
                segment.networkIds = Clean(segment.networkIds.Concat(new[] { id }));
                segment.revision++;
                segmentsById[segmentId] = segment;
            }
            RebuildIndexes();
            Complete(N(request.transactionId), "route.network.create", id, id);
            Touch();
            return LocationRouteMutationResult.NetworkSuccess(BuildNetworkSnapshot(record), "Route network created.", before, Revision);
        }

        public bool TryGetSegment(string segmentId, out LocationRouteSegmentSnapshot snapshot)
        {
            if (segmentsById.TryGetValue(N(segmentId), out LocationRouteSegmentRecordData segment))
            {
                snapshot = BuildSegmentSnapshot(segment);
                return true;
            }
            snapshot = null;
            return false;
        }

        public bool TryGetNetwork(string networkId, out LocationRouteNetworkSnapshot snapshot)
        {
            if (networksById.TryGetValue(N(networkId), out LocationRouteNetworkRecordData network))
            {
                snapshot = BuildNetworkSnapshot(network);
                return true;
            }
            snapshot = null;
            return false;
        }

        public IReadOnlyList<LocationRouteSegmentSnapshot> GetOutgoingSegments(string locationId, bool includeHidden = false)
        {
            return GetIds(outgoingSegmentsByLocation, N(locationId))
                .Select(id => segmentsById.TryGetValue(id, out LocationRouteSegmentRecordData segment) ? segment : null)
                .Where(segment => segment != null && (includeHidden || VisibleToPublic(segment.visibility)))
                .OrderBy(segment => segment.segmentId, StringComparer.Ordinal)
                .Select(BuildSegmentSnapshot)
                .ToArray();
        }

        public IReadOnlyList<LocationRouteUnifiedEdgeSnapshot> GetUnifiedOutgoingEdges(string locationId, LocationRouteSearchRequest request = null)
        {
            return BuildOutgoingEdges(N(locationId), NormalizeRequest(request), includeRejected: true).Select(edge => edge.Edge.Clone()).ToArray();
        }

        public LocationRouteSearchResult PlanRoute(LocationRouteSearchRequest request)
        {
            long graphRevision = Revision;
            request = NormalizeRequest(request);
            if (disposed) return LocationRouteSearchResult.Failure(RoutePlanningStatus.Disposed, "Location route runtime is disposed.");
            if (registry == null || locations == null) return LocationRouteSearchResult.Failure(RoutePlanningStatus.MissingRuntime, "Route planning requires definitions and locations.");
            string origin = N(request.originLocationId);
            string destination = N(request.destinationLocationId);
            if (string.IsNullOrWhiteSpace(origin) || !locations.TryGetSnapshot(origin, out _)) return LocationRouteSearchResult.Failure(RoutePlanningStatus.MissingOrigin, $"Origin location '{request.originLocationId}' is missing.");
            if (string.IsNullOrWhiteSpace(destination) || !locations.TryGetSnapshot(destination, out _)) return LocationRouteSearchResult.Failure(RoutePlanningStatus.MissingDestination, $"Destination location '{request.destinationLocationId}' is missing.");
            if (!TryGetTravelMode(request.travelModeDefinitionId, out TravelModeDefinition mode)) return LocationRouteSearchResult.Failure(RoutePlanningStatus.ModeUnsupported, $"Travel mode '{request.travelModeDefinitionId}' is missing.");
            if (!TravelerSupportsMode(request, mode)) return LocationRouteSearchResult.Failure(RoutePlanningStatus.ModeUnsupported, $"Traveler does not satisfy travel mode '{mode.Id}'.");
            if (string.Equals(origin, destination, StringComparison.Ordinal))
            {
                LocationRoutePlan self = new LocationRoutePlan(PlanId(request, Array.Empty<LocationRoutePlanStep>()), origin, destination, request.traveler, mode.Id, request.objective, new[] { origin }, Array.Empty<LocationRoutePlanStep>(), TravelDistance.Zero, TravelCost.Zero, new RouteRequirementSummary(), graphRevision, connections?.Revision ?? 0L, IsKnowledgeFiltered(request), "Self route.", travelConditions?.Revision ?? 0L);
                return LocationRouteSearchResult.Success(self, "Self route.", 1, 0, request.preview);
            }

            SearchState initial = new SearchState(origin, new[] { origin }, Array.Empty<LocationRoutePlanStep>(), 0d, 0d, string.Empty);
            List<SearchState> frontier = new List<SearchState> { initial };
            Dictionary<string, SearchState> bestByNode = new Dictionary<string, SearchState>(StringComparer.Ordinal) { [origin] = initial };
            int visited = 0;
            int expanded = 0;
            HashSet<string> settled = new HashSet<string>(StringComparer.Ordinal);

            while (frontier.Count > 0)
            {
                frontier.Sort((left, right) => CompareSearchStates(left, right, request.objective));
                SearchState current = frontier[0];
                frontier.RemoveAt(0);
                if (!settled.Add(current.Node)) continue;
                visited++;
                if (visited > request.maximumVisitedNodes) return LocationRouteSearchResult.Failure(RoutePlanningStatus.SearchBudgetExceeded, "Route search visited-node budget exceeded.", visited, expanded, budget: true);
                if (string.Equals(current.Node, destination, StringComparison.Ordinal))
                {
                    LocationRoutePlan plan = BuildPlan(request, current, graphRevision);
                    return LocationRouteSearchResult.Success(plan, "Route found.", visited, expanded, request.preview);
                }
                if (current.Steps.Count >= request.maximumDepth) continue;

                foreach (EvaluatedEdge edge in BuildOutgoingEdges(current.Node, request, includeRejected: false))
                {
                    expanded++;
                    if (expanded > request.maximumExpandedEdges) return LocationRouteSearchResult.Failure(RoutePlanningStatus.SearchBudgetExceeded, "Route search expanded-edge budget exceeded.", visited, expanded, budget: true);
                    if (!edge.Usable) continue;
                    string next = edge.Edge.DestinationLocationId;
                    if (settled.Contains(next)) continue;
                    double nextDistance = current.DistanceMeters + edge.DistanceMeters;
                    double nextCost = current.CostUnits + edge.CostUnits;
                    if (request.maximumDistanceMeters >= 0d && nextDistance > request.maximumDistanceMeters) continue;
                    if (request.maximumCostUnits >= 0d && nextCost > request.maximumCostUnits) continue;
                    LocationRoutePlanStep step = new LocationRoutePlanStep
                    {
                        SourceLocationId = edge.Edge.SourceLocationId,
                        DestinationLocationId = edge.Edge.DestinationLocationId,
                        EdgeId = edge.Edge.EdgeId,
                        EdgeKind = edge.Edge.EdgeKind,
                        Category = edge.Edge.Category,
                        Visibility = edge.Edge.Visibility,
                        Distance = new TravelDistance(edge.DistanceMeters),
                        Cost = new TravelCost(edge.CostUnits),
                        AccessState = edge.AccessState,
                        TravelModeDefinitionId = mode.Id,
                        SourceRevision = edge.Edge.SourceRevision,
                        RequiredActions = edge.RequiredActions.ToArray()
                    };
                    SearchState candidate = current.Append(next, step, nextDistance, nextCost);
                    if (!bestByNode.TryGetValue(next, out SearchState best) || CompareSearchStates(candidate, best, request.objective) < 0)
                    {
                        bestByNode[next] = candidate;
                        frontier.Add(candidate);
                    }
                }
            }

            return LocationRouteSearchResult.Failure(IsKnowledgeFiltered(request) ? RoutePlanningStatus.UnknownUnderKnowledgeView : RoutePlanningStatus.NoRoute, IsKnowledgeFiltered(request) ? "No known route." : "No route.", visited, expanded);
        }

        public LocationRouteReachabilityResult GetReachableLocations(LocationRouteSearchRequest request)
        {
            request = NormalizeRequest(request);
            HashSet<string> reachable = new HashSet<string>(StringComparer.Ordinal);
            Queue<SearchState> queue = new Queue<SearchState>();
            string origin = N(request.originLocationId);
            if (string.IsNullOrWhiteSpace(origin)) return new LocationRouteReachabilityResult(Array.Empty<string>(), 0, 0, false);
            queue.Enqueue(new SearchState(origin, new[] { origin }, Array.Empty<LocationRoutePlanStep>(), 0d, 0d, string.Empty));
            int visited = 0;
            int expanded = 0;
            while (queue.Count > 0)
            {
                SearchState current = queue.Dequeue();
                if (!reachable.Add(current.Node)) continue;
                visited++;
                if (visited > request.maximumVisitedNodes) return new LocationRouteReachabilityResult(reachable, visited, expanded, true);
                if (current.Steps.Count >= request.maximumDepth) continue;
                foreach (EvaluatedEdge edge in BuildOutgoingEdges(current.Node, request, includeRejected: false))
                {
                    expanded++;
                    if (expanded > request.maximumExpandedEdges) return new LocationRouteReachabilityResult(reachable, visited, expanded, true);
                    if (edge.Usable && !reachable.Contains(edge.Edge.DestinationLocationId)) queue.Enqueue(current.Append(edge.Edge.DestinationLocationId, null, current.DistanceMeters + edge.DistanceMeters, current.CostUnits + edge.CostUnits));
                }
            }
            reachable.Remove(origin);
            return new LocationRouteReachabilityResult(reachable, visited, expanded, false);
        }

        public LocationRouteSearchResult QueryShortestDistance(string originLocationId, string destinationLocationId, string travelModeDefinitionId = WalkingModeId)
        {
            return PlanRoute(new LocationRouteSearchRequest { originLocationId = originLocationId, destinationLocationId = destinationLocationId, travelModeDefinitionId = travelModeDefinitionId, objective = RoutePlanningObjective.ShortestDistance, accessMode = RouteAccessEvaluationMode.StructuralOnly });
        }

        public LocationRouteSearchResult QueryLowestCost(string originLocationId, string destinationLocationId, string travelModeDefinitionId = WalkingModeId)
        {
            return PlanRoute(new LocationRouteSearchRequest { originLocationId = originLocationId, destinationLocationId = destinationLocationId, travelModeDefinitionId = travelModeDefinitionId, objective = RoutePlanningObjective.LowestCost, accessMode = RouteAccessEvaluationMode.StructuralOnly });
        }

        public LocationRouteRevalidationResult RevalidatePlan(LocationRoutePlan plan, LocationRouteSearchRequest request = null)
        {
            if (plan == null) return new LocationRouteRevalidationResult(RoutePlanRevalidationStatus.InvalidPlan, "Route plan is missing.");
            request = NormalizeRequest(request);
            request.originLocationId = plan.OriginLocationId;
            request.destinationLocationId = plan.DestinationLocationId;
            request.travelModeDefinitionId = string.IsNullOrWhiteSpace(request.travelModeDefinitionId) ? plan.TravelModeDefinitionId : request.travelModeDefinitionId;
            request.objective = plan.Objective;
            bool conditionAware = request.conditionEvaluationMode != TravelConditionEvaluationMode.IgnoreDynamicConditions;
            if (plan.RouteRevision != Revision || plan.ConnectionRevision != (connections?.Revision ?? 0L) || (conditionAware && plan.ConditionRevision != (travelConditions?.Revision ?? 0L)))
            {
                foreach (LocationRoutePlanStep step in plan.Steps)
                {
                    EvaluatedEdge current = BuildOutgoingEdges(step.SourceLocationId, request, includeRejected: true).FirstOrDefault(edge => edge.Edge.EdgeKind == step.EdgeKind && string.Equals(edge.Edge.EdgeId, step.EdgeId, StringComparison.Ordinal) && string.Equals(edge.Edge.DestinationLocationId, step.DestinationLocationId, StringComparison.Ordinal));
                    if (current == null) return new LocationRouteRevalidationResult(RoutePlanRevalidationStatus.InvalidEdge, "Route plan edge no longer exists.", step.EdgeId);
                    if (!current.Usable) return new LocationRouteRevalidationResult(RoutePlanRevalidationStatus.ChangedAccess, "Route plan edge is no longer usable.", step.EdgeId);
                    if (!current.Edge.Distance.Equals(step.Distance) || !new TravelCost(current.CostUnits).Equals(step.Cost)) return new LocationRouteRevalidationResult(RoutePlanRevalidationStatus.ChangedCost, "Route plan edge cost changed.", step.EdgeId);
                    if (current.Edge.SourceRevision != step.SourceRevision) return new LocationRouteRevalidationResult(RoutePlanRevalidationStatus.RequiresReplanning, "Route plan source revision changed.", step.EdgeId);
                }
                return new LocationRouteRevalidationResult(RoutePlanRevalidationStatus.StaleGraphRevision, "Route plan graph revision changed.");
            }
            return new LocationRouteRevalidationResult(RoutePlanRevalidationStatus.Valid, "Route plan is still valid.");
        }

        public LocationRouteRuntimeSaveData CreateSaveData()
        {
            return new LocationRouteRuntimeSaveData
            {
                schemaVersion = LocationRouteRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                segments = segmentsById.Values.OrderBy(item => item.segmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                networks = networksById.Values.OrderBy(item => item.networkId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                history = historyById.Values.OrderBy(item => item.worldTime).ThenBy(item => item.historyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                transactions = transactionsById.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray()
            };
        }

        public LocationRouteMutationResult RestoreFromSaveData(LocationRouteRuntimeSaveData saveData, LocationRuntime locationRuntime = null, LocationConnectionRuntime connectionRuntime = null, string expectedWorldId = PersistenceService.LocalWorldId, bool restoring = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, registry, locationRuntime ?? locations, connectionRuntime ?? connections, expectedWorldId, out string failure)) return Fail(RouteMutationStatus.PersistenceInvalid, failure, before);
            LocationRouteRuntimeSaveData rollback = CreateSaveData();
            try
            {
                RestoreInternal(saveData ?? new LocationRouteRuntimeSaveData());
                locations = locationRuntime ?? locations;
                connections = connectionRuntime ?? connections;
                worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
                IsDirty = !restoring;
                return LocationRouteMutationResult.Success(null, "Location routes restored.", before, Revision);
            }
            catch (Exception exception)
            {
                RestoreInternal(rollback);
                return Fail(RouteMutationStatus.RestoreFailed, $"Location route restore failed: {exception.Message}", before);
            }
        }

        public bool ValidateCurrent(out string failure)
        {
            return ValidateSaveData(CreateSaveData(), registry, locations, connections, worldId, out failure);
        }

        public static bool ValidateSaveData(LocationRouteRuntimeSaveData saveData, DefinitionRegistry registry, LocationRuntime locations, LocationConnectionRuntime connections, string expectedWorldId, out string failure)
        {
            List<string> errors = new List<string>();
            saveData ??= new LocationRouteRuntimeSaveData();
            string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            if (saveData.schemaVersion < 1 || saveData.schemaVersion > LocationRouteRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported location route save schema {saveData.schemaVersion}.");
            if (!string.IsNullOrWhiteSpace(saveData.worldId) && !string.Equals(saveData.worldId.Trim(), world, StringComparison.Ordinal)) errors.Add($"Location route save world '{saveData.worldId}' does not match '{world}'.");

            HashSet<string> segmentIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> networkIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LocationRouteSegmentRecordData segment in saveData.segments ?? Array.Empty<LocationRouteSegmentRecordData>())
            {
                if (segment == null) { errors.Add("Location route save contains a null segment."); continue; }
                string id = N(segment.segmentId);
                if (string.IsNullOrWhiteSpace(id)) errors.Add("Route segment has no ID.");
                else if (!segmentIds.Add(id)) errors.Add($"Duplicate route segment '{id}'.");
                if (registry == null || !registry.TryGet(N(segment.segmentDefinitionId), out RouteSegmentDefinition definition)) errors.Add($"Route segment '{id}' references missing definition '{segment.segmentDefinitionId}'.");
                if (locations == null || !locations.TryGetSnapshot(N(segment.sourceLocationId), out _)) errors.Add($"Route segment '{id}' references missing source location '{segment.sourceLocationId}'.");
                if (locations == null || !locations.TryGetSnapshot(N(segment.destinationLocationId), out _)) errors.Add($"Route segment '{id}' references missing destination location '{segment.destinationLocationId}'.");
                if (!ValidDirectionality(segment.directionality)) errors.Add($"Route segment '{id}' has invalid directionality '{segment.directionality}'.");
                if (!Enum.IsDefined(typeof(RouteSegmentLifecycleState), segment.lifecycleState) || segment.lifecycleState == RouteSegmentLifecycleState.Unknown) errors.Add($"Route segment '{id}' has invalid lifecycle '{segment.lifecycleState}'.");
                if (!Enum.IsDefined(typeof(RouteSegmentBlockageState), segment.blockageState) || segment.blockageState == RouteSegmentBlockageState.Unknown) errors.Add($"Route segment '{id}' has invalid blockage '{segment.blockageState}'.");
                if (!TravelDistance.TryCreate(segment.distanceMeters, out _) || (registry != null && registry.TryGet(N(segment.segmentDefinitionId), out RouteSegmentDefinition def) && !def.AllowZeroDistance && segment.distanceMeters <= 0d)) errors.Add($"Route segment '{id}' has invalid distance '{segment.distanceMeters}'.");
                if (!TravelCost.TryCreate(segment.baseCostUnits, out _)) errors.Add($"Route segment '{id}' has invalid base cost '{segment.baseCostUnits}'.");
                if (segment.endedWorldTime >= 0d && segment.endedWorldTime < segment.createdWorldTime) errors.Add($"Route segment '{id}' ends before it starts.");
                foreach (string modeId in Clean(segment.supportedTravelModeDefinitionIds))
                {
                    if (registry == null || !registry.TryGet(modeId, out TravelModeDefinition _)) errors.Add($"Route segment '{id}' references missing travel mode '{modeId}'.");
                }
                foreach (string policyId in Clean(segment.accessPolicyDefinitionIds))
                {
                    if (registry == null || !registry.TryGet(policyId, out LocationAccessPolicyDefinition _)) errors.Add($"Route segment '{id}' references missing access policy '{policyId}'.");
                }
            }

            foreach (LocationRouteNetworkRecordData network in saveData.networks ?? Array.Empty<LocationRouteNetworkRecordData>())
            {
                if (network == null) { errors.Add("Location route save contains a null network."); continue; }
                string id = N(network.networkId);
                if (string.IsNullOrWhiteSpace(id)) errors.Add("Route network has no ID.");
                else if (!networkIds.Add(id)) errors.Add($"Duplicate route network '{id}'.");
                if (!Enum.IsDefined(typeof(RouteNetworkCategory), network.category) || network.category == RouteNetworkCategory.Unknown) errors.Add($"Route network '{id}' has invalid category '{network.category}'.");
                foreach (string segmentId in Clean(network.segmentIds))
                {
                    if (!segmentIds.Contains(segmentId)) errors.Add($"Route network '{id}' references missing segment '{segmentId}'.");
                }
            }

            foreach (LocationRouteSegmentRecordData segment in saveData.segments ?? Array.Empty<LocationRouteSegmentRecordData>())
            {
                if (segment == null) continue;
                foreach (string networkId in Clean(segment.networkIds))
                {
                    if (!networkIds.Contains(networkId)) errors.Add($"Route segment '{segment.segmentId}' references missing network '{networkId}'.");
                }
            }

            foreach (LocationRouteSegmentHistoryData item in saveData.history ?? Array.Empty<LocationRouteSegmentHistoryData>())
            {
                if (item == null) { errors.Add("Location route save contains null history."); continue; }
                if (string.IsNullOrWhiteSpace(item.historyId)) errors.Add("Route segment history has no ID.");
                if (!segmentIds.Contains(N(item.segmentId))) errors.Add($"Route segment history '{item.historyId}' references missing segment '{item.segmentId}'.");
            }

            failure = string.Join(" | ", errors.OrderBy(item => item, StringComparer.Ordinal));
            return errors.Count == 0;
        }

        public void Reset()
        {
            segmentsById.Clear();
            networksById.Clear();
            historyById.Clear();
            transactionsById.Clear();
            RebuildIndexes();
            Revision = 0L;
            IsDirty = false;
            disposed = false;
        }

        public void Dispose()
        {
            Reset();
            disposed = true;
        }

        private IReadOnlyList<EvaluatedEdge> BuildOutgoingEdges(string locationId, LocationRouteSearchRequest request, bool includeRejected)
        {
            List<EvaluatedEdge> edges = new List<EvaluatedEdge>();
            foreach (LocationRouteSegmentRecordData segment in GetIds(outgoingSegmentsByLocation, locationId).Select(id => segmentsById.TryGetValue(id, out LocationRouteSegmentRecordData value) ? value : null).Where(value => value != null))
            {
                AddSegmentEdge(edges, segment, locationId, segment.destinationLocationId, request, includeRejected);
            }
            foreach (LocationRouteSegmentRecordData segment in GetIds(incomingSegmentsByLocation, locationId).Select(id => segmentsById.TryGetValue(id, out LocationRouteSegmentRecordData value) ? value : null).Where(value => value != null))
            {
                if (DirectionAllowed(segment.directionality, locationId, segment.sourceLocationId, segment.sourceLocationId, segment.destinationLocationId))
                {
                    AddSegmentEdge(edges, segment, locationId, segment.sourceLocationId, request, includeRejected);
                }
            }
            if (connections != null)
            {
                foreach (LocationConnectionSnapshot connection in connections.GetOutgoingConnections(locationId, includeHidden: true)) AddConnectionEdge(edges, connection, locationId, connection.DestinationLocationId, request, includeRejected);
                foreach (LocationConnectionSnapshot connection in connections.GetIncomingConnections(locationId, includeHidden: true)) AddConnectionEdge(edges, connection, locationId, connection.SourceLocationId, request, includeRejected);
            }
            return edges.OrderBy(edge => edge.Edge.EdgeId, StringComparer.Ordinal).ThenBy(edge => edge.Edge.DestinationLocationId, StringComparer.Ordinal).ToArray();
        }

        private void AddSegmentEdge(ICollection<EvaluatedEdge> edges, LocationRouteSegmentRecordData segment, string from, string to, LocationRouteSearchRequest request, bool includeRejected)
        {
            if (!DirectionAllowed(segment.directionality, from, to, segment.sourceLocationId, segment.destinationLocationId)) return;
            if (!registry.TryGet(segment.segmentDefinitionId, out RouteSegmentDefinition definition)) return;
            LocationRouteUnifiedEdgeSnapshot edge = new LocationRouteUnifiedEdgeSnapshot
            {
                EdgeId = segment.segmentId,
                EdgeKind = RouteEdgeKind.RouteSegment,
                DefinitionId = segment.segmentDefinitionId,
                Category = definition.Category,
                SourceLocationId = from,
                DestinationLocationId = to,
                Directionality = segment.directionality,
                Distance = new TravelDistance(segment.distanceMeters),
                BaseCost = new TravelCost(segment.baseCostUnits),
                SupportedTravelModeDefinitionIds = Clean(segment.supportedTravelModeDefinitionIds).Length == 0 ? definition.SupportedTravelModeDefinitionIds.ToArray() : Clean(segment.supportedTravelModeDefinitionIds),
                Visibility = segment.visibility,
                LifecycleState = segment.lifecycleState,
                BlockageState = segment.blockageState,
                AccessPolicyDefinitionIds = segment.accessPolicyDefinitionIds.ToArray(),
                NetworkIds = segment.networkIds.ToArray(),
                SourceRevision = segment.revision
            };
            EvaluatedEdge evaluated = EvaluateEdge(edge, request);
            if (evaluated.Usable || includeRejected) edges.Add(evaluated);
        }

        private void AddConnectionEdge(ICollection<EvaluatedEdge> edges, LocationConnectionSnapshot connection, string from, string to, LocationRouteSearchRequest request, bool includeRejected)
        {
            if (!DirectionAllowed(connection.Directionality, from, to, connection.SourceLocationId, connection.DestinationLocationId)) return;
            LocationRouteUnifiedEdgeSnapshot edge = new LocationRouteUnifiedEdgeSnapshot
            {
                EdgeId = connection.ConnectionId,
                EdgeKind = RouteEdgeKind.LocalConnection,
                DefinitionId = connection.ConnectionDefinitionId,
                Category = CategoryFromConnection(connection),
                SourceLocationId = from,
                DestinationLocationId = to,
                Directionality = connection.Directionality,
                Distance = ConnectionDistance(connection),
                BaseCost = ConnectionCost(connection),
                SupportedTravelModeDefinitionIds = new[] { WalkingModeId },
                Visibility = VisibilityFromConnection(connection.Visibility),
                LifecycleState = LifecycleFromConnection(connection.LifecycleState),
                BlockageState = BlockageFromConnection(connection.BlockageState),
                AccessPolicyDefinitionIds = connection.AccessPolicyDefinitionIds.ToArray(),
                SourceRevision = connection.Revision
            };
            EvaluatedEdge evaluated = EvaluateEdge(edge, request);
            if (evaluated.Usable || includeRejected) edges.Add(evaluated);
        }

        private EvaluatedEdge EvaluateEdge(LocationRouteUnifiedEdgeSnapshot edge, LocationRouteSearchRequest request)
        {
            List<string> requiredActions = new List<string>();
            if (!KnowledgeAllows(edge, request)) return EvaluatedEdge.Rejected(edge, "UnknownUnderKnowledgeView");
            if (Clean(request.forbiddenLocationIds).Contains(edge.DestinationLocationId, StringComparer.Ordinal)) return EvaluatedEdge.Rejected(edge, "ForbiddenLocation");
            if ((request.forbiddenCategories ?? Array.Empty<RouteSegmentCategory>()).Contains(edge.Category)) return EvaluatedEdge.Rejected(edge, "ForbiddenCategory");
            if (!TravelModeAllows(edge, request, out TravelModeDefinition mode)) return EvaluatedEdge.Rejected(edge, "ModeUnsupported");
            if (!TravelerSupportsMode(request, mode)) return EvaluatedEdge.Rejected(edge, "TravelerCannotUseMode");
            if (!StructurallyAvailable(edge)) return EvaluatedEdge.Rejected(edge, "StructurallyUnavailable");

            string accessState = "Structural";
            if (edge.EdgeKind == RouteEdgeKind.LocalConnection && request.accessMode != RouteAccessEvaluationMode.StructuralOnly && request.accessMode != RouteAccessEvaluationMode.IgnoreTravelerAccessDevelopment)
            {
                LocationConnectionAccessResult access = connections?.EvaluateAccess(new LocationConnectionTraversalRequest { connectionId = edge.EdgeId, actor = request.traveler, fromLocationId = edge.SourceLocationId, toLocationId = edge.DestinationLocationId, accessContext = request.accessContext, worldTime = request.worldTime });
                accessState = access?.accessState.ToString() ?? "MissingAccess";
                if (access == null) return EvaluatedEdge.Rejected(edge, "MissingAccess");
                if (!access.Allowed)
                {
                    bool unlockable = request.accessMode == RouteAccessEvaluationMode.PermitUnlockableConnections
                        && (access.accessState == LocationConnectionAccessState.AllowedIfOpened || access.accessState == LocationConnectionAccessState.AllowedIfUnlocked || access.accessState == LocationConnectionAccessState.AllowedIfOpenedAndUnlocked);
                    if (!unlockable) return EvaluatedEdge.Rejected(edge, accessState);
                    if (access.accessState == LocationConnectionAccessState.AllowedIfOpened || access.accessState == LocationConnectionAccessState.AllowedIfOpenedAndUnlocked) requiredActions.Add($"open:{edge.EdgeId}");
                    if (access.accessState == LocationConnectionAccessState.AllowedIfUnlocked || access.accessState == LocationConnectionAccessState.AllowedIfOpenedAndUnlocked) requiredActions.Add($"unlock:{edge.EdgeId}");
                }
            }
            else if (edge.EdgeKind == RouteEdgeKind.RouteSegment && edge.AccessPolicyDefinitionIds.Count > 0 && request.accessMode != RouteAccessEvaluationMode.StructuralOnly && request.accessMode != RouteAccessEvaluationMode.IgnoreTravelerAccessDevelopment)
            {
                if (!RoutePoliciesAllow(edge, request, requiredActions, out accessState)) return EvaluatedEdge.Rejected(edge, accessState);
            }

            double preferredMultiplier = (request.preferredCategories ?? Array.Empty<RouteSegmentCategory>()).Contains(edge.Category) ? 0.85d : 1d;
            double distance = Math.Max(0d, edge.Distance.meters * Math.Max(0.0001d, mode.DistanceMultiplier));
            double definitionMultiplier = edge.EdgeKind == RouteEdgeKind.RouteSegment && registry != null && registry.TryGet(edge.DefinitionId, out RouteSegmentDefinition definition)
                ? Math.Max(0.0001d, definition.CostMultiplier)
                : 1d;
            double cost = Math.Max(0d, edge.BaseCost.units * Math.Max(0.0001d, mode.CostMultiplier) * definitionMultiplier * preferredMultiplier);
            TravelConditionEvaluationResult conditions = EvaluateTravelConditions(edge, request);
            if (conditions != null && !conditions.Succeeded) return EvaluatedEdge.Rejected(edge, "TravelConditionEvaluationFailed");
            if (conditions?.HardBlocked == true)
            {
                Add(requiredActions, conditions.RequiredCapabilityIds.Select(id => $"capability:{id}"));
                Add(requiredActions, conditions.RequiredEquipmentDefinitionIds.Select(id => $"equipment:{id}"));
                return EvaluatedEdge.Rejected(edge, "TravelConditionBlocked");
            }
            if (conditions != null)
            {
                distance = Math.Max(0d, distance * (1d / Math.Max(0.0001d, conditions.MovementRateMultiplier)));
                cost = Math.Max(0d, cost * Math.Max(0.0001d, conditions.RouteCostMultiplier));
                Add(requiredActions, conditions.RequiredCapabilityIds.Select(id => $"capability:{id}"));
                Add(requiredActions, conditions.RequiredEquipmentDefinitionIds.Select(id => $"equipment:{id}"));
            }
            return new EvaluatedEdge(edge, true, distance, cost, accessState, requiredActions);
        }

        private TravelConditionEvaluationResult EvaluateTravelConditions(LocationRouteUnifiedEdgeSnapshot edge, LocationRouteSearchRequest request)
        {
            if (travelConditions == null || request.conditionEvaluationMode == TravelConditionEvaluationMode.IgnoreDynamicConditions) return null;
            return travelConditions.Evaluate(new TravelConditionEvaluationRequest
            {
                evaluationMode = request.conditionEvaluationMode,
                target = new TravelConditionTargetReferenceData
                {
                    scope = edge.EdgeKind == RouteEdgeKind.RouteSegment ? TravelConditionTargetScope.RouteSegment : TravelConditionTargetScope.Connection,
                    targetId = edge.EdgeId,
                    sourceLocationId = edge.SourceLocationId,
                    destinationLocationId = edge.DestinationLocationId,
                    edgeKind = edge.EdgeKind,
                    traveler = request.traveler?.Clone()
                },
                traveler = request.traveler?.Clone(),
                travelModeDefinitionId = request.travelModeDefinitionId,
                travelerCapabilityIds = request.travelerCapabilityIds,
                travelerEquipmentDefinitionIds = request.travelerEquipmentDefinitionIds,
                knownConditionIds = request.knownConditionIds,
                knownEncounterIds = request.knownEncounterIds,
                knownHazardExposureIds = request.knownHazardExposureIds,
                includeHiddenDevelopmentConditions = request.includeHiddenDevelopmentConditions,
                worldTime = request.worldTime
            });
        }

        private bool RoutePoliciesAllow(LocationRouteUnifiedEdgeSnapshot edge, LocationRouteSearchRequest request, ICollection<string> requirements, out string accessState)
        {
            accessState = "Allowed";
            if (request.accessContext?.privileged == true) return true;
            foreach (string policyId in edge.AccessPolicyDefinitionIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                if (!registry.TryGet(policyId, out LocationAccessPolicyDefinition policy)) { accessState = "MissingPolicy"; return false; }
                if (policy.BlacklistedPersonIds.Contains(N(request.accessContext?.personId), StringComparer.Ordinal)) { accessState = "PersonBlacklisted"; return false; }
                if (policy.WhitelistedPersonIds.Count > 0 && policy.WhitelistedPersonIds.Contains(N(request.accessContext?.personId), StringComparer.Ordinal)) continue;
                if (policy.DenyByDefault) { accessState = "DeniedByPolicy"; return false; }
                if (policy.AllowByDefault || policy.Category == LocationAccessPolicyCategory.Public) continue;
                if (policy.RequiredKeyInstanceIds.Count > 0 && !Any(policy.RequiredKeyInstanceIds, request.accessContext?.keyInstanceIds)) { Add(requirements, policy.RequiredKeyInstanceIds.Select(id => $"key-instance:{id}")); accessState = "MissingKey"; return false; }
                if (policy.RequiredKeyDefinitionIds.Count > 0 && !Any(policy.RequiredKeyDefinitionIds, request.accessContext?.keyDefinitionIds)) { Add(requirements, policy.RequiredKeyDefinitionIds.Select(id => $"key:{id}")); accessState = "MissingKey"; return false; }
                if (policy.RequiredPermitIds.Count > 0 && !Any(policy.RequiredPermitIds, request.accessContext?.permitIds)) { Add(requirements, policy.RequiredPermitIds.Select(id => $"permit:{id}")); accessState = "MissingPermit"; return false; }
                if (policy.RequiredOrganizationIds.Count > 0 && !Any(policy.RequiredOrganizationIds, request.accessContext?.organizationIds)) { Add(requirements, policy.RequiredOrganizationIds.Select(id => $"membership:{id}")); accessState = "MissingMembership"; return false; }
                if (policy.RequiredRankIds.Count > 0 && !Any(policy.RequiredRankIds, request.accessContext?.rankIds)) { Add(requirements, policy.RequiredRankIds.Select(id => $"rank:{id}")); accessState = "MissingRank"; return false; }
                if (policy.RequiredOfficeIds.Count > 0 && !Any(policy.RequiredOfficeIds, request.accessContext?.officeIds)) { Add(requirements, policy.RequiredOfficeIds.Select(id => $"office:{id}")); accessState = "MissingOffice"; return false; }
                if (policy.RequiredAuthorityIds.Count > 0 && !Any(policy.RequiredAuthorityIds, request.accessContext?.authorityIds)) { Add(requirements, policy.RequiredAuthorityIds.Select(id => $"authority:{id}")); accessState = "MissingAuthority"; return false; }
                if (policy.RequiredEmploymentIds.Count > 0 && !Any(policy.RequiredEmploymentIds, request.accessContext?.employmentIds)) { Add(requirements, policy.RequiredEmploymentIds.Select(id => $"employment:{id}")); accessState = "MissingEmployment"; return false; }
                if (policy.RequiredPropertyIds.Count > 0 && !Any(policy.RequiredPropertyIds, request.accessContext?.propertyIds)) { Add(requirements, policy.RequiredPropertyIds.Select(id => $"property:{id}")); accessState = "MissingProperty"; return false; }
                if (policy.RequiredWarrantIds.Count > 0 && !Any(policy.RequiredWarrantIds, request.accessContext?.warrantIds)) { Add(requirements, policy.RequiredWarrantIds.Select(id => $"warrant:{id}")); accessState = "MissingWarrant"; return false; }
                if (policy.RequiredCustodyRoleIds.Count > 0 && !Any(policy.RequiredCustodyRoleIds, request.accessContext?.custodyRoleIds)) { Add(requirements, policy.RequiredCustodyRoleIds.Select(id => $"custody:{id}")); accessState = "CustodyRestricted"; return false; }
                if (policy.RequiredCredentialIds.Count > 0 && !Any(policy.RequiredCredentialIds, request.accessContext?.credentialIds)) { Add(requirements, policy.RequiredCredentialIds.Select(id => $"credential:{id}")); accessState = "MissingCredential"; return false; }
            }
            return true;
        }

        private LocationRoutePlan BuildPlan(LocationRouteSearchRequest request, SearchState state, long routeRevision)
        {
            string[] actions = state.Steps.SelectMany(step => step.RequiredActions ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            RouteRequirementSummary requirements = new RouteRequirementSummary
            {
                requiredKeys = ValuesWithPrefix(actions, "key:").Concat(ValuesWithPrefix(actions, "key-instance:")).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                requiredPermits = ValuesWithPrefix(actions, "permit:"),
                requiredMemberships = ValuesWithPrefix(actions, "membership:").Concat(ValuesWithPrefix(actions, "rank:")).Concat(ValuesWithPrefix(actions, "employment:")).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                requiredOffices = ValuesWithPrefix(actions, "office:"),
                requiredAuthorities = ValuesWithPrefix(actions, "authority:"),
                requiredCustodyRoles = ValuesWithPrefix(actions, "custody:"),
                requiredActions = actions,
                hiddenRouteEdges = state.Steps.Where(step => IsHiddenStep(step)).Select(step => step.EdgeId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
            return new LocationRoutePlan(PlanId(request, state.Steps), request.originLocationId, request.destinationLocationId, request.traveler, request.travelModeDefinitionId, request.objective, state.Nodes, state.Steps, new TravelDistance(state.DistanceMeters), new TravelCost(state.CostUnits), requirements, routeRevision, connections?.Revision ?? 0L, IsKnowledgeFiltered(request), $"Visited={state.Nodes.Count} Edges={state.Steps.Count}", travelConditions?.Revision ?? 0L);
        }

        private static int CompareSearchStates(SearchState left, SearchState right, RoutePlanningObjective objective)
        {
            int metric = objective switch
            {
                RoutePlanningObjective.LowestCost => left.CostUnits.CompareTo(right.CostUnits),
                RoutePlanningObjective.FewestEdges or RoutePlanningObjective.AnyValidRoute => left.Steps.Count.CompareTo(right.Steps.Count),
                _ => left.DistanceMeters.CompareTo(right.DistanceMeters)
            };
            if (metric != 0) return metric;
            int edges = left.Steps.Count.CompareTo(right.Steps.Count);
            if (edges != 0) return edges;
            int distance = left.DistanceMeters.CompareTo(right.DistanceMeters);
            if (distance != 0) return distance;
            int cost = left.CostUnits.CompareTo(right.CostUnits);
            if (cost != 0) return cost;
            return string.Compare(left.SortKey, right.SortKey, StringComparison.Ordinal);
        }

        private static bool DirectionAllowed(LocationConnectionDirectionality directionality, string from, string to, string source, string destination)
        {
            return directionality switch
            {
                LocationConnectionDirectionality.Bidirectional => (string.Equals(from, source, StringComparison.Ordinal) && string.Equals(to, destination, StringComparison.Ordinal)) || (string.Equals(from, destination, StringComparison.Ordinal) && string.Equals(to, source, StringComparison.Ordinal)),
                LocationConnectionDirectionality.SourceToDestinationOnly => string.Equals(from, source, StringComparison.Ordinal) && string.Equals(to, destination, StringComparison.Ordinal),
                LocationConnectionDirectionality.DestinationToSourceOnly => string.Equals(from, destination, StringComparison.Ordinal) && string.Equals(to, source, StringComparison.Ordinal),
                _ => false
            };
        }

        private static bool StructurallyAvailable(LocationRouteUnifiedEdgeSnapshot edge)
        {
            return edge.LifecycleState == RouteSegmentLifecycleState.Active && edge.BlockageState == RouteSegmentBlockageState.Clear;
        }

        private bool KnowledgeAllows(LocationRouteUnifiedEdgeSnapshot edge, LocationRouteSearchRequest request)
        {
            if (request.knowledgeMode == RouteKnowledgeMode.AuthoritativeDevelopment || request.includeHiddenDevelopmentRoutes) return true;
            if (edge.Visibility == RouteVisibility.Hidden || edge.Visibility == RouteVisibility.Secret || edge.Visibility == RouteVisibility.Diagnostic)
            {
                HashSet<string> knownEdges = new HashSet<string>(Clean(request.knownEdgeIds), StringComparer.Ordinal);
                return request.knowledgeMode == RouteKnowledgeMode.KnownToTraveler && knownEdges.Contains(edge.EdgeId);
            }
            if (request.knowledgeMode == RouteKnowledgeMode.KnownToTraveler)
            {
                HashSet<string> knownLocations = new HashSet<string>(Clean(request.knownLocationIds), StringComparer.Ordinal);
                return knownLocations.Count == 0 || (knownLocations.Contains(edge.SourceLocationId) && knownLocations.Contains(edge.DestinationLocationId));
            }
            return edge.Visibility == RouteVisibility.Public || edge.Visibility == RouteVisibility.LocallyKnown || edge.Visibility == RouteVisibility.OrganizationKnown || edge.Visibility == RouteVisibility.GovernmentKnown;
        }

        private bool TravelModeAllows(LocationRouteUnifiedEdgeSnapshot edge, LocationRouteSearchRequest request, out TravelModeDefinition mode)
        {
            mode = null;
            if (!TryGetTravelMode(request.travelModeDefinitionId, out mode)) return false;
            if (!mode.SupportsCategory(edge.Category)) return false;
            string[] edgeModes = Clean(edge.SupportedTravelModeDefinitionIds);
            return edgeModes.Length == 0 || edgeModes.Contains(mode.Id, StringComparer.Ordinal);
        }

        private bool TravelerSupportsMode(LocationRouteSearchRequest request, TravelModeDefinition mode)
        {
            if (mode == null) return false;
            if (!AnyAll(mode.RequiredCapabilityIds, request.travelerCapabilityIds)) return false;
            if (!AnyAll(mode.RequiredEquipmentDefinitionIds, request.travelerEquipmentDefinitionIds)) return false;
            return true;
        }

        private bool TryGetTravelMode(string id, out TravelModeDefinition mode)
        {
            mode = null;
            string modeId = string.IsNullOrWhiteSpace(id) ? WalkingModeId : id.Trim();
            return registry != null && registry.TryGet(modeId, out mode);
        }

        private LocationRouteSearchRequest NormalizeRequest(LocationRouteSearchRequest request)
        {
            request ??= new LocationRouteSearchRequest();
            request.travelModeDefinitionId = string.IsNullOrWhiteSpace(request.travelModeDefinitionId) ? WalkingModeId : request.travelModeDefinitionId.Trim();
            if (request.objective == RoutePlanningObjective.Unknown) request.objective = RoutePlanningObjective.ShortestDistance;
            if (request.accessMode == RouteAccessEvaluationMode.Unknown) request.accessMode = RouteAccessEvaluationMode.StructuralOnly;
            if (request.knowledgeMode == RouteKnowledgeMode.Unknown) request.knowledgeMode = request.accessMode == RouteAccessEvaluationMode.KnowledgeSafeCurrentAccess ? RouteKnowledgeMode.KnownToTraveler : RouteKnowledgeMode.AuthoritativeDevelopment;
            request.maximumVisitedNodes = Math.Max(1, request.maximumVisitedNodes);
            request.maximumExpandedEdges = Math.Max(1, request.maximumExpandedEdges);
            request.maximumDepth = Math.Max(0, request.maximumDepth);
            if (request.accessContext == null) request.accessContext = new LocationConnectionAccessContextData { actor = request.traveler?.Clone() };
            if (request.accessContext.actor == null) request.accessContext.actor = request.traveler?.Clone();
            return request;
        }

        private static string PlanId(LocationRouteSearchRequest request, IEnumerable<LocationRoutePlanStep> steps)
        {
            string key = string.Join(".", (steps ?? Array.Empty<LocationRoutePlanStep>()).Select(step => step.EdgeId));
            return $"route-plan.{N(request?.originLocationId)}.{N(request?.destinationLocationId)}.{N(request?.travelModeDefinitionId)}.{N(request?.objective.ToString())}.{StableSuffix(key)}";
        }

        private static string StableSuffix(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in value ?? string.Empty)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                return hash.ToString("x8");
            }
        }

        private static RouteSegmentCategory CategoryFromConnection(LocationConnectionSnapshot connection)
        {
            return connection == null ? RouteSegmentCategory.Custom : connection.ConnectionDefinitionId.Contains("dungeon", StringComparison.Ordinal) ? RouteSegmentCategory.DungeonRoute : connection.ConnectionDefinitionId.Contains("stair", StringComparison.Ordinal) ? RouteSegmentCategory.StairRoute : connection.ConnectionDefinitionId.Contains("door", StringComparison.Ordinal) || connection.ConnectionDefinitionId.Contains("entrance", StringComparison.Ordinal) ? RouteSegmentCategory.Corridor : RouteSegmentCategory.Path;
        }

        private static TravelDistance ConnectionDistance(LocationConnectionSnapshot connection)
        {
            return new TravelDistance(connection?.ConnectionDefinitionId.Contains("entrance", StringComparison.Ordinal) == true ? 8d : 1d);
        }

        private static TravelCost ConnectionCost(LocationConnectionSnapshot connection)
        {
            return new TravelCost(ConnectionDistance(connection).meters);
        }

        private static RouteVisibility VisibilityFromConnection(LocationConnectionVisibility visibility)
        {
            return visibility switch
            {
                LocationConnectionVisibility.Secret => RouteVisibility.Secret,
                LocationConnectionVisibility.Hidden => RouteVisibility.Hidden,
                LocationConnectionVisibility.Restricted => RouteVisibility.Restricted,
                LocationConnectionVisibility.OrganizationKnown or LocationConnectionVisibility.MemberKnown or LocationConnectionVisibility.StaffKnown => RouteVisibility.OrganizationKnown,
                LocationConnectionVisibility.GovernmentKnown => RouteVisibility.GovernmentKnown,
                LocationConnectionVisibility.Diagnostic => RouteVisibility.Diagnostic,
                LocationConnectionVisibility.LocallyKnown => RouteVisibility.LocallyKnown,
                _ => RouteVisibility.Public
            };
        }

        private static RouteSegmentLifecycleState LifecycleFromConnection(LocationConnectionLifecycleState lifecycle)
        {
            return lifecycle switch
            {
                LocationConnectionLifecycleState.Active => RouteSegmentLifecycleState.Active,
                LocationConnectionLifecycleState.Inactive or LocationConnectionLifecycleState.Disabled => RouteSegmentLifecycleState.Inactive,
                LocationConnectionLifecycleState.Blocked => RouteSegmentLifecycleState.Blocked,
                LocationConnectionLifecycleState.Destroyed => RouteSegmentLifecycleState.Destroyed,
                LocationConnectionLifecycleState.Historical => RouteSegmentLifecycleState.Historical,
                _ => RouteSegmentLifecycleState.Invalid
            };
        }

        private static RouteSegmentBlockageState BlockageFromConnection(LocationConnectionBlockageState blockage)
        {
            return blockage switch
            {
                LocationConnectionBlockageState.Clear => RouteSegmentBlockageState.Clear,
                LocationConnectionBlockageState.TemporarilyBlocked => RouteSegmentBlockageState.TemporarilyBlocked,
                LocationConnectionBlockageState.PermanentlyBlocked => RouteSegmentBlockageState.PermanentlyBlocked,
                LocationConnectionBlockageState.Collapsed => RouteSegmentBlockageState.Collapsed,
                _ => RouteSegmentBlockageState.ObstructedPlaceholder
            };
        }

        private bool Ready(long before, out LocationRouteMutationResult failure)
        {
            failure = null;
            if (disposed) return SetFailure(RouteMutationStatus.Disposed, "Location route runtime is disposed.", before, out failure);
            if (registry == null) return SetFailure(RouteMutationStatus.MissingDefinition, "Definition registry is missing.", before, out failure);
            if (locations == null) return SetFailure(RouteMutationStatus.MissingLocation, "Location runtime is missing.", before, out failure);
            return true;
        }

        private bool TryGetSegmentDefinition(string id, long before, out RouteSegmentDefinition definition, out LocationRouteMutationResult failure)
        {
            failure = null;
            if (registry != null && registry.TryGet(N(id), out definition)) return true;
            definition = null;
            return SetFailure(RouteMutationStatus.MissingDefinition, $"Route segment definition '{id}' is missing.", before, out failure);
        }

        private bool ValidateLocation(string id, long before, out LocationSnapshot snapshot, out LocationRouteMutationResult failure)
        {
            failure = null;
            if (locations != null && locations.TryGetSnapshot(N(id), out snapshot)) return true;
            snapshot = null;
            return SetFailure(RouteMutationStatus.MissingLocation, $"Location '{id}' is missing.", before, out failure);
        }

        private bool ValidateDistance(double distance, RouteSegmentDefinition definition, long before, out LocationRouteMutationResult failure)
        {
            failure = null;
            if (TravelDistance.TryCreate(distance, out _) && (definition?.AllowZeroDistance == true || distance > 0d)) return true;
            return SetFailure(RouteMutationStatus.InvalidDistance, $"Route distance '{distance}' is invalid.", before, out failure);
        }

        private bool ValidateCost(double cost, long before, out LocationRouteMutationResult failure)
        {
            failure = null;
            if (TravelCost.TryCreate(cost, out _)) return true;
            return SetFailure(RouteMutationStatus.InvalidCost, $"Route cost '{cost}' is invalid.", before, out failure);
        }

        private bool ValidateModes(IEnumerable<string> modes, RouteSegmentDefinition definition, long before, out LocationRouteMutationResult failure)
        {
            failure = null;
            foreach (string modeId in Clean(modes))
            {
                if (!registry.TryGet(modeId, out TravelModeDefinition mode)) return SetFailure(RouteMutationStatus.InvalidTravelMode, $"Travel mode '{modeId}' is missing.", before, out failure);
                if (definition != null && !definition.SupportsTravelMode(modeId)) return SetFailure(RouteMutationStatus.InvalidTravelMode, $"Route definition '{definition.Id}' does not support mode '{modeId}'.", before, out failure);
                if (definition != null && !mode.SupportsCategory(definition.Category)) return SetFailure(RouteMutationStatus.InvalidTravelMode, $"Travel mode '{modeId}' does not support route category '{definition.Category}'.", before, out failure);
            }
            return true;
        }

        private bool ValidateAccessPolicies(IEnumerable<string> policyIds, RouteSegmentDefinition definition, long before, out LocationRouteMutationResult failure)
        {
            failure = null;
            if (definition?.SupportsAccessPolicies != true && Clean(policyIds).Length > 0) return SetFailure(RouteMutationStatus.InvalidAccessPolicy, $"Route definition '{definition.Id}' does not support access policies.", before, out failure);
            foreach (string policyId in Clean(policyIds))
            {
                if (!registry.TryGet(policyId, out LocationAccessPolicyDefinition _)) return SetFailure(RouteMutationStatus.InvalidAccessPolicy, $"Route access policy '{policyId}' is missing.", before, out failure);
            }
            return true;
        }

        private bool ValidateNetworks(IEnumerable<string> ids, long before, out LocationRouteMutationResult failure)
        {
            failure = null;
            foreach (string id in Clean(ids))
            {
                if (!networksById.ContainsKey(id)) return SetFailure(RouteMutationStatus.MissingNetwork, $"Route network '{id}' is missing.", before, out failure);
            }
            return true;
        }

        private static bool ValidDirectionality(LocationConnectionDirectionality directionality)
        {
            return directionality == LocationConnectionDirectionality.Bidirectional || directionality == LocationConnectionDirectionality.SourceToDestinationOnly || directionality == LocationConnectionDirectionality.DestinationToSourceOnly;
        }

        private static bool ValidLifecycleTransition(RouteSegmentLifecycleState from, RouteSegmentLifecycleState to)
        {
            if (!Enum.IsDefined(typeof(RouteSegmentLifecycleState), to) || to == RouteSegmentLifecycleState.Unknown || to == RouteSegmentLifecycleState.Invalid) return false;
            if (from == RouteSegmentLifecycleState.Destroyed && to == RouteSegmentLifecycleState.Active) return false;
            return true;
        }

        private void RestoreInternal(LocationRouteRuntimeSaveData saveData)
        {
            segmentsById.Clear();
            networksById.Clear();
            historyById.Clear();
            transactionsById.Clear();
            foreach (LocationRouteSegmentRecordData item in saveData?.segments ?? Array.Empty<LocationRouteSegmentRecordData>()) segmentsById[N(item.segmentId)] = item.Clone();
            foreach (LocationRouteNetworkRecordData item in saveData?.networks ?? Array.Empty<LocationRouteNetworkRecordData>()) networksById[N(item.networkId)] = item.Clone();
            foreach (LocationRouteSegmentHistoryData item in saveData?.history ?? Array.Empty<LocationRouteSegmentHistoryData>()) historyById[N(item.historyId)] = item.Clone();
            foreach (LocationRouteTransactionRecordData item in saveData?.transactions ?? Array.Empty<LocationRouteTransactionRecordData>()) transactionsById[N(item.transactionId)] = item.Clone();
            worldId = string.IsNullOrWhiteSpace(saveData?.worldId) ? worldId : saveData.worldId.Trim();
            Revision = Math.Max(0L, saveData?.revision ?? 0L);
            RebuildIndexes();
        }

        private void RebuildIndexes()
        {
            outgoingSegmentsByLocation.Clear();
            incomingSegmentsByLocation.Clear();
            segmentsByDefinition.Clear();
            segmentsByCategory.Clear();
            segmentsByNetwork.Clear();
            segmentsByTravelMode.Clear();
            foreach (LocationRouteSegmentRecordData segment in segmentsById.Values.OrderBy(item => item.segmentId, StringComparer.Ordinal))
            {
                AddIndex(outgoingSegmentsByLocation, segment.sourceLocationId, segment.segmentId);
                AddIndex(incomingSegmentsByLocation, segment.destinationLocationId, segment.segmentId);
                AddIndex(segmentsByDefinition, segment.segmentDefinitionId, segment.segmentId);
                AddIndex(segmentsByCategory, CategoryFor(segment), segment.segmentId);
                foreach (string networkId in Clean(segment.networkIds)) AddIndex(segmentsByNetwork, networkId, segment.segmentId);
                foreach (string modeId in Clean(segment.supportedTravelModeDefinitionIds)) AddIndex(segmentsByTravelMode, modeId, segment.segmentId);
            }
        }

        private RouteSegmentCategory CategoryFor(LocationRouteSegmentRecordData segment)
        {
            return registry != null && registry.TryGet(segment.segmentDefinitionId, out RouteSegmentDefinition definition) ? definition.Category : RouteSegmentCategory.Custom;
        }

        private static void AddIndex<TKey>(IDictionary<TKey, List<string>> index, TKey key, string id)
        {
            if (key == null || string.IsNullOrWhiteSpace(id)) return;
            if (!index.TryGetValue(key, out List<string> list))
            {
                list = new List<string>();
                index[key] = list;
            }
            if (!list.Contains(id, StringComparer.Ordinal)) list.Add(id);
            list.Sort(StringComparer.Ordinal);
        }

        private void AddHistory(LocationRouteSegmentRecordData segment, string operation, double worldTime, EntityLocationReferenceData actor, string eventId, string recordId, string provenanceId)
        {
            string id = $"route-history.{segment.segmentId}.{segment.revision}.{StableSuffix(operation + worldTime.ToString("R"))}";
            historyById[id] = new LocationRouteSegmentHistoryData
            {
                historyId = id,
                segmentId = segment.segmentId,
                operation = N(operation),
                lifecycleState = segment.lifecycleState,
                blockageState = segment.blockageState,
                distanceMeters = segment.distanceMeters,
                baseCostUnits = segment.baseCostUnits,
                worldTime = worldTime,
                actorKey = actor?.StableKey ?? string.Empty,
                sourceEventId = N(eventId),
                sourceRecordId = N(recordId),
                provenanceId = N(provenanceId),
                revision = segment.revision
            };
        }

        private bool TryDuplicate(string tx, string segmentId, string operation, long before, out LocationRouteMutationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(tx)) return false;
            if (transactionsById.TryGetValue(tx, out LocationRouteTransactionRecordData existing))
            {
                result = LocationRouteMutationResult.Success(segmentsById.TryGetValue(existing.segmentId, out LocationRouteSegmentRecordData segment) ? BuildSegmentSnapshot(segment) : null, $"Duplicate transaction '{tx}'.", before, before, duplicate: true);
                return true;
            }
            return false;
        }

        private void Complete(string tx, string operation, string segmentId, string resultReferenceId)
        {
            if (string.IsNullOrWhiteSpace(tx)) return;
            transactionsById[tx] = new LocationRouteTransactionRecordData { transactionId = tx, operation = operation, segmentId = segmentId, resultReferenceId = resultReferenceId, revision = Revision + 1L };
        }

        private void Touch()
        {
            Revision++;
            IsDirty = true;
        }

        private static bool ValidateRevision(long expected, long actual, out LocationRouteMutationResult result)
        {
            result = null;
            if (expected < 0L || expected == actual) return true;
            result = LocationRouteMutationResult.Failure(RouteMutationStatus.RevisionConflict, $"Expected route revision {expected}, actual {actual}.", actual);
            return false;
        }

        private static bool SetFailure(RouteMutationStatus status, string message, long before, out LocationRouteMutationResult failure)
        {
            failure = LocationRouteMutationResult.Failure(status, message, before);
            return false;
        }

        private static LocationRouteMutationResult Fail(RouteMutationStatus status, string message, long before) => LocationRouteMutationResult.Failure(status, message, before);
        private static LocationRouteSegmentSnapshot BuildSegmentSnapshot(LocationRouteSegmentRecordData record) => new LocationRouteSegmentSnapshot(record);
        private static LocationRouteNetworkSnapshot BuildNetworkSnapshot(LocationRouteNetworkRecordData record) => new LocationRouteNetworkSnapshot(record);
        private static bool VisibleToPublic(RouteVisibility visibility) => visibility == RouteVisibility.Public || visibility == RouteVisibility.LocallyKnown || visibility == RouteVisibility.OrganizationKnown || visibility == RouteVisibility.GovernmentKnown;
        private static bool IsKnowledgeFiltered(LocationRouteSearchRequest request) => request?.knowledgeMode == RouteKnowledgeMode.KnownToTraveler || request?.accessMode == RouteAccessEvaluationMode.KnowledgeSafeCurrentAccess;
        private static bool IsHiddenStep(LocationRoutePlanStep step)
        {
            if (step == null) return false;
            return step.Visibility == RouteVisibility.Hidden || step.Visibility == RouteVisibility.Secret || step.Visibility == RouteVisibility.Diagnostic || step.AccessState.Contains("Hidden", StringComparison.Ordinal) || step.AccessState.Contains("Secret", StringComparison.Ordinal);
        }
        private static string First(string first, string fallback) => string.IsNullOrWhiteSpace(first) ? N(fallback) : first.Trim();
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static string[] ValuesWithPrefix(IEnumerable<string> values, string prefix) => Clean(values).Where(value => value.StartsWith(prefix, StringComparison.Ordinal)).Select(value => value.Substring(prefix.Length)).ToArray();
        private static IReadOnlyList<string> GetIds(IReadOnlyDictionary<string, List<string>> index, string key) => index != null && index.TryGetValue(N(key), out List<string> ids) ? ids.OrderBy(id => id, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        private static bool Any(IEnumerable<string> required, IEnumerable<string> actual) => Clean(required).Any(new HashSet<string>(Clean(actual), StringComparer.Ordinal).Contains);
        private static bool AnyAll(IEnumerable<string> required, IEnumerable<string> actual)
        {
            string[] req = Clean(required);
            if (req.Length == 0) return true;
            HashSet<string> have = new HashSet<string>(Clean(actual), StringComparer.Ordinal);
            return req.All(have.Contains);
        }
        private static void Add(ICollection<string> target, IEnumerable<string> values)
        {
            foreach (string value in Clean(values)) target.Add(value);
        }

        private sealed class EvaluatedEdge
        {
            public EvaluatedEdge(LocationRouteUnifiedEdgeSnapshot edge, bool usable, double distanceMeters, double costUnits, string accessState, IEnumerable<string> requiredActions)
            {
                Edge = edge.Clone();
                Usable = usable;
                DistanceMeters = distanceMeters;
                CostUnits = costUnits;
                AccessState = accessState ?? string.Empty;
                RequiredActions = (requiredActions ?? Array.Empty<string>()).ToArray();
            }

            public LocationRouteUnifiedEdgeSnapshot Edge { get; }
            public bool Usable { get; }
            public double DistanceMeters { get; }
            public double CostUnits { get; }
            public string AccessState { get; }
            public IReadOnlyList<string> RequiredActions { get; }
            public static EvaluatedEdge Rejected(LocationRouteUnifiedEdgeSnapshot edge, string reason) => new EvaluatedEdge(edge, false, edge.Distance.meters, edge.BaseCost.units, reason, Array.Empty<string>());
        }

        private sealed class SearchState
        {
            public SearchState(string node, IEnumerable<string> nodes, IEnumerable<LocationRoutePlanStep> steps, double distanceMeters, double costUnits, string sortKey)
            {
                Node = node ?? string.Empty;
                Nodes = (nodes ?? Array.Empty<string>()).ToList();
                Steps = (steps ?? Array.Empty<LocationRoutePlanStep>()).Where(step => step != null).Select(step => step.Clone()).ToList();
                DistanceMeters = distanceMeters;
                CostUnits = costUnits;
                SortKey = sortKey ?? string.Empty;
            }

            public string Node { get; }
            public List<string> Nodes { get; }
            public List<LocationRoutePlanStep> Steps { get; }
            public double DistanceMeters { get; }
            public double CostUnits { get; }
            public string SortKey { get; }

            public SearchState Append(string node, LocationRoutePlanStep step, double distanceMeters, double costUnits)
            {
                List<string> nodes = Nodes.ToList();
                nodes.Add(node);
                List<LocationRoutePlanStep> steps = Steps.ToList();
                if (step != null) steps.Add(step);
                return new SearchState(node, nodes, steps, distanceMeters, costUnits, $"{SortKey}/{step?.EdgeId ?? node}");
            }
        }
    }
}
