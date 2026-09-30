using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class LocationConnectionRuntime : IDisposable
    {
        private readonly Dictionary<string, LocationConnectionRecordData> connectionsById = new Dictionary<string, LocationConnectionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationConnectionEndpointData> endpointsById = new Dictionary<string, LocationConnectionEndpointData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationAccessGrantData> grantsById = new Dictionary<string, LocationAccessGrantData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationConnectionStateHistoryData> historyById = new Dictionary<string, LocationConnectionStateHistoryData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationConnectionTransactionRecordData> transactionsById = new Dictionary<string, LocationConnectionTransactionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> outgoingByLocationId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> incomingByLocationId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> endpointsByConnectionId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> grantsByConnectionId = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private LocationRuntime locationRuntime;
        private EntityLocationRuntime entityLocationRuntime;
        private InteractionPointRuntime interactionPointRuntime;
        private string worldId = PersistenceService.LocalWorldId;
        private bool disposed;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public string WorldId => worldId;
        public int ConnectionCount => connectionsById.Count;
        public int EndpointCount => endpointsById.Count;
        public int GrantCount => grantsById.Count;
        public int HistoryCount => historyById.Count;
        public IReadOnlyList<LocationConnectionSnapshot> Connections => connectionsById.Values.OrderBy(item => item.connectionId, StringComparer.Ordinal).Select(BuildSnapshot).ToArray();
        public IReadOnlyList<LocationConnectionEndpointSnapshot> Endpoints => endpointsById.Values.OrderBy(item => item.endpointId, StringComparer.Ordinal).Select(BuildEndpointSnapshot).ToArray();
        public IReadOnlyList<LocationAccessGrantSnapshot> Grants => grantsById.Values.OrderBy(item => item.grantId, StringComparer.Ordinal).Select(BuildGrantSnapshot).ToArray();
        public IReadOnlyList<LocationConnectionStateHistoryData> History => historyById.Values.OrderBy(item => item.worldTime).ThenBy(item => item.historyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, LocationRuntime locations, EntityLocationRuntime entityLocations, InteractionPointRuntime interactionPoints = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            registry = definitionRegistry ?? registry;
            locationRuntime = locations ?? locationRuntime;
            entityLocationRuntime = entityLocations ?? entityLocationRuntime;
            interactionPointRuntime = interactionPoints ?? interactionPointRuntime;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId.Trim();
            disposed = false;
        }

        public LocationConnectionOperationResult CreateConnection(LocationConnectionCreateRequest request)
        {
            request ??= new LocationConnectionCreateRequest();
            long before = Revision;
            if (!Ready(before, out LocationConnectionOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out LocationConnectionOperationResult revisionFailure)) return revisionFailure;

            string connectionId = N(request.connectionId);
            string tx = N(request.transactionId);
            if (TryDuplicate(tx, connectionId, "connection.create", before, out LocationConnectionOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(connectionId)) return Fail(LocationConnectionOperationStatus.InvalidRequest, "Connection ID is required.", before);
            if (connectionsById.ContainsKey(connectionId)) return Fail(LocationConnectionOperationStatus.Duplicate, $"Connection '{connectionId}' already exists.", before);
            if (!TryGetConnectionDefinition(request.connectionDefinitionId, before, out LocationConnectionDefinition definition, out LocationConnectionOperationResult failure)) return failure;
            if (!ValidateEndpointLocations(request.sourceLocationId, request.destinationLocationId, definition, before, out LocationSnapshot source, out LocationSnapshot destination, out failure)) return failure;

            LocationConnectionDirectionality directionality = request.directionality == LocationConnectionDirectionality.Unknown ? definition.DefaultDirectionality : request.directionality;
            if (!Enum.IsDefined(typeof(LocationConnectionDirectionality), directionality) || directionality == LocationConnectionDirectionality.Unknown) return Fail(LocationConnectionOperationStatus.InvalidDirection, $"Directionality '{directionality}' is invalid.", before);
            LocationConnectionOpenState open = NormalizeOpenState(definition, request.openState);
            LocationConnectionLockState locked = NormalizeLockState(definition, request.lockState);
            LocationConnectionBlockageState blockage = request.blockageState == LocationConnectionBlockageState.Unknown ? LocationConnectionBlockageState.Clear : request.blockageState;
            if (!ValidateStates(definition, open, locked, blockage, before, out failure)) return failure;
            if (!ValidatePolicies(request.accessPolicyDefinitionIds, before, out failure)) return failure;
            if (!ValidateInteractionPoints(request.interactionPointIds, before, out failure)) return failure;

            LocationConnectionRecordData record = new LocationConnectionRecordData
            {
                connectionId = connectionId,
                connectionDefinitionId = definition.Id,
                worldId = worldId,
                displayName = string.IsNullOrWhiteSpace(request.displayName) ? definition.DisplayName : request.displayName.Trim(),
                sourceLocationId = source.LocationId,
                destinationLocationId = destination.LocationId,
                directionality = directionality,
                lifecycleState = LocationConnectionLifecycleState.Active,
                openState = open,
                lockState = locked,
                blockageState = blockage,
                visibility = request.visibility,
                accessPolicyDefinitionIds = Clean(request.accessPolicyDefinitionIds),
                sourceEndpointId = $"{connectionId}.endpoint.source",
                destinationEndpointId = $"{connectionId}.endpoint.destination",
                interactionPointIds = Clean(request.interactionPointIds),
                semanticIdentityId = N(request.semanticIdentityId),
                sceneBindingKey = definition.SupportsSceneBinding ? N(request.sceneBindingKey) : string.Empty,
                sceneBindingCategory = definition.SupportsSceneBinding ? request.sceneBindingCategory : LocationConnectionSceneBindingCategory.None,
                createdWorldTime = request.worldTime,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };
            LocationConnectionEndpointData sourceEndpoint = CreateEndpoint(record.sourceEndpointId, connectionId, source.LocationId, LocationConnectionEndpointRole.Source, request.visibility, request.sourceEventId, request.sourceRecordId, request.provenanceId);
            LocationConnectionEndpointData destinationEndpoint = CreateEndpoint(record.destinationEndpointId, connectionId, destination.LocationId, LocationConnectionEndpointRole.Destination, request.visibility, request.sourceEventId, request.sourceRecordId, request.provenanceId);

            if (request.preview)
            {
                return LocationConnectionOperationResult.Success(BuildSnapshot(record), "Connection create preview.", before, before, preview: true);
            }

            connectionsById.Add(connectionId, record);
            endpointsById.Add(sourceEndpoint.endpointId, sourceEndpoint);
            endpointsById.Add(destinationEndpoint.endpointId, destinationEndpoint);
            AddHistory(record, "create", request.worldTime, null, request.sourceEventId, request.sourceRecordId, request.provenanceId);
            RebuildIndexes();
            Complete(tx, "connection.create", connectionId, connectionId);
            Touch();
            return LocationConnectionOperationResult.Success(BuildSnapshot(record), "Connection created.", before, Revision);
        }

        public LocationConnectionOperationResult MutateState(LocationConnectionStateMutationRequest request)
        {
            request ??= new LocationConnectionStateMutationRequest();
            long before = Revision;
            if (!Ready(before, out LocationConnectionOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out LocationConnectionOperationResult revisionFailure)) return revisionFailure;
            string connectionId = N(request.connectionId);
            if (TryDuplicate(N(request.transactionId), connectionId, "connection.state", before, out LocationConnectionOperationResult duplicate)) return duplicate;
            if (!connectionsById.TryGetValue(connectionId, out LocationConnectionRecordData existing)) return Fail(LocationConnectionOperationStatus.MissingConnection, $"Connection '{connectionId}' is missing.", before);
            if (!TryGetConnectionDefinition(existing.connectionDefinitionId, before, out LocationConnectionDefinition definition, out LocationConnectionOperationResult failure)) return failure;

            LocationConnectionRecordData changed = existing.Clone();
            if (request.lifecycleState != LocationConnectionLifecycleState.Unknown)
            {
                if (!ValidateLifecycleTransition(changed.lifecycleState, request.lifecycleState)) return Fail(LocationConnectionOperationStatus.InvalidLifecycleTransition, $"Cannot transition connection '{connectionId}' from {changed.lifecycleState} to {request.lifecycleState}.", before);
                changed.lifecycleState = request.lifecycleState;
                if (request.lifecycleState == LocationConnectionLifecycleState.Destroyed || request.lifecycleState == LocationConnectionLifecycleState.Historical)
                {
                    changed.endedWorldTime = request.worldTime;
                }
            }

            if (request.openState != LocationConnectionOpenState.Unknown) changed.openState = request.openState;
            if (request.lockState != LocationConnectionLockState.Unknown) changed.lockState = request.lockState;
            if (request.blockageState != LocationConnectionBlockageState.Unknown) changed.blockageState = request.blockageState;
            if (!ValidateStates(definition, changed.openState, changed.lockState, changed.blockageState, before, out failure)) return failure;

            if (request.openState == LocationConnectionOpenState.Open)
            {
                EntityLocationReferenceData actor = request.accessContext?.actor;
                if (!ValidActor(actor)) return Fail(LocationConnectionOperationStatus.MissingActor, "Opening a connection requires an authoritative actor.", before);
                if (changed.lockState == LocationConnectionLockState.Locked) return Fail(LocationConnectionOperationStatus.DeniedByLock, "The connection is locked.", before);
                if (changed.blockageState != LocationConnectionBlockageState.Clear) return Fail(LocationConnectionOperationStatus.DeniedByBlockage, $"The connection is blocked: {changed.blockageState}.", before);
                if (entityLocationRuntime == null || !entityLocationRuntime.TryGetActivePlacement(actor, out EntityPlacementSnapshot placement)) return Fail(LocationConnectionOperationStatus.MissingPlacement, "The actor has no authoritative placement.", before);
                ResolveDirection(existing, placement.ExactLocationId, out string from, out string to);
                List<string> accessReasons = new List<string>();
                LocationConnectionAccessResult policyResult = new LocationConnectionAccessResult();
                if (!EvaluatePolicies(existing, from, to, request.accessContext, request.worldTime, policyResult, accessReasons))
                {
                    return Fail(LocationConnectionOperationStatus.DeniedByPolicy, $"Connection access denied: {string.Join(", ", accessReasons.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))}.", before);
                }
            }

            if (request.preview)
            {
                return LocationConnectionOperationResult.Success(BuildSnapshot(changed), "Connection state mutation preview.", before, before, preview: true);
            }

            changed.sourceEventId = First(request.sourceEventId, changed.sourceEventId);
            changed.sourceRecordId = First(request.sourceRecordId, changed.sourceRecordId);
            changed.provenanceId = First(request.provenanceId, changed.provenanceId);
            changed.revision++;
            connectionsById[connectionId] = changed;
            AddHistory(changed, "state", request.worldTime, request.accessContext?.actor, request.sourceEventId, request.sourceRecordId, request.provenanceId);
            RebuildIndexes();
            Complete(N(request.transactionId), "connection.state", connectionId, connectionId);
            Touch();
            return LocationConnectionOperationResult.Success(BuildSnapshot(changed), "Connection state updated.", before, Revision);
        }

        public LocationConnectionOperationResult GrantAccess(LocationAccessGrantRequest request)
        {
            request ??= new LocationAccessGrantRequest();
            long before = Revision;
            if (!Ready(before, out LocationConnectionOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out LocationConnectionOperationResult revisionFailure)) return revisionFailure;
            string connectionId = N(request.connectionId);
            string grantId = N(request.grantId);
            if (TryDuplicate(N(request.transactionId), connectionId, "access.grant", before, out LocationConnectionOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(grantId)) return Fail(LocationConnectionOperationStatus.InvalidRequest, "Access grant ID is required.", before);
            if (grantsById.ContainsKey(grantId)) return Fail(LocationConnectionOperationStatus.Duplicate, $"Access grant '{grantId}' already exists.", before);
            if (!connectionsById.TryGetValue(connectionId, out LocationConnectionRecordData connection)) return Fail(LocationConnectionOperationStatus.MissingConnection, $"Connection '{connectionId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.endpointId) && !endpointsById.ContainsKey(N(request.endpointId))) return Fail(LocationConnectionOperationStatus.MissingEndpoint, $"Endpoint '{request.endpointId}' is missing.", before);
            if (!ValidActor(request.grantee)) return Fail(LocationConnectionOperationStatus.MissingActor, "Access grant grantee is invalid.", before);
            if (request.endWorldTime >= 0d && request.endWorldTime < request.startWorldTime) return Fail(LocationConnectionOperationStatus.InvalidRequest, "Access grant ends before it starts.", before);

            LocationAccessGrantData grant = new LocationAccessGrantData
            {
                grantId = grantId,
                connectionId = connectionId,
                endpointId = N(request.endpointId),
                grantee = request.grantee.Clone(),
                directionality = request.directionality == LocationConnectionDirectionality.Unknown ? LocationConnectionDirectionality.Bidirectional : request.directionality,
                startWorldTime = request.startWorldTime,
                endWorldTime = request.endWorldTime,
                lifecycleState = LocationAccessGrantLifecycleState.Active,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };

            if (request.preview)
            {
                return LocationConnectionOperationResult.Success(BuildSnapshot(connection), "Access grant preview.", before, before, preview: true);
            }

            grantsById.Add(grantId, grant);
            RebuildIndexes();
            Complete(N(request.transactionId), "access.grant", connectionId, grantId);
            Touch();
            return LocationConnectionOperationResult.Success(BuildSnapshot(connection), "Access granted.", before, Revision);
        }

        public LocationConnectionAccessResult EvaluateAccess(LocationConnectionTraversalRequest request)
        {
            request ??= new LocationConnectionTraversalRequest();
            LocationConnectionAccessContextData context = request.accessContext?.Clone() ?? new LocationConnectionAccessContextData();
            if (context.actor == null) context.actor = request.actor?.Clone();
            List<string> reasons = new List<string>();
            LocationConnectionAccessResult result = new LocationConnectionAccessResult
            {
                connectionId = N(request.connectionId),
                fromLocationId = N(request.fromLocationId),
                toLocationId = N(request.toLocationId),
                actor = context.actor?.Clone(),
                connectionRevision = Revision,
                entityLocationRevision = entityLocationRuntime?.Revision ?? 0L
            };

            if (disposed) return Deny(result, LocationConnectionAccessState.Invalid, LocationConnectionOperationStatus.Disposed.ToString());
            if (!connectionsById.TryGetValue(N(request.connectionId), out LocationConnectionRecordData connection)) return Deny(result, LocationConnectionAccessState.Invalid, $"missing.connection:{request.connectionId}");

            result.lifecycleState = connection.lifecycleState;
            result.openState = connection.openState;
            result.lockState = connection.lockState;
            result.blockageState = connection.blockageState;
            string from = N(request.fromLocationId);
            string to = N(request.toLocationId);
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            {
                ResolveDirection(connection, from, out from, out to);
                result.fromLocationId = from;
                result.toLocationId = to;
            }

            result.directionAllowed = DirectionAllowed(connection, from, to);
            if (!result.directionAllowed) reasons.Add("direction.denied");
            if (!IsTraversableLifecycle(connection.lifecycleState)) reasons.Add($"lifecycle.{connection.lifecycleState}");
            if (connection.blockageState != LocationConnectionBlockageState.Clear) reasons.Add($"blockage.{connection.blockageState}");
            if (connection.openState == LocationConnectionOpenState.Closed) reasons.Add("open.closed");
            if (connection.lockState == LocationConnectionLockState.Locked) reasons.Add("lock.locked");
            if (connection.lockState == LocationConnectionLockState.JammedPlaceholder || connection.lockState == LocationConnectionLockState.BrokenLockPlaceholder) reasons.Add($"lock.{connection.lockState}");

            result.explicitGrantSatisfied = ActiveGrantExists(connection, context, from, to, request.worldTime);
            result.policyAllowed = EvaluatePolicies(connection, from, to, context, request.worldTime, result, reasons);
            result.denialReasons = reasons.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            result.accessState = AccessStateFromReasons(result.denialReasons, connection);
            result.diagnostics = string.Join("; ", result.denialReasons);
            return result.Clone();
        }

        public LocationConnectionOperationResult PreviewTraversal(LocationConnectionTraversalRequest request)
        {
            return Traverse(request, previewOnly: true);
        }

        public LocationConnectionOperationResult Traverse(LocationConnectionTraversalRequest request)
        {
            return Traverse(request, previewOnly: false);
        }

        public bool TryGetConnection(string connectionId, out LocationConnectionSnapshot snapshot)
        {
            if (connectionsById.TryGetValue(N(connectionId), out LocationConnectionRecordData connection))
            {
                snapshot = BuildSnapshot(connection);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<LocationConnectionSnapshot> GetOutgoingConnections(string locationId, bool includeHidden = false)
        {
            return GetConnectionIds(outgoingByLocationId, N(locationId))
                .Select(id => connectionsById.TryGetValue(id, out LocationConnectionRecordData item) ? item : null)
                .Where(item => item != null && (includeHidden || VisibleToNormalView(item.visibility)))
                .OrderBy(item => item.connectionId, StringComparer.Ordinal)
                .Select(BuildSnapshot)
                .ToArray();
        }

        public IReadOnlyList<LocationConnectionSnapshot> GetIncomingConnections(string locationId, bool includeHidden = false)
        {
            return GetConnectionIds(incomingByLocationId, N(locationId))
                .Select(id => connectionsById.TryGetValue(id, out LocationConnectionRecordData item) ? item : null)
                .Where(item => item != null && (includeHidden || VisibleToNormalView(item.visibility)))
                .OrderBy(item => item.connectionId, StringComparer.Ordinal)
                .Select(BuildSnapshot)
                .ToArray();
        }

        public IReadOnlyList<LocationConnectionSnapshot> GetAccessibleConnections(string locationId, LocationConnectionAccessContextData context, double worldTime, bool includeHidden = false)
        {
            return GetOutgoingConnections(locationId, includeHidden)
                .Where(item => EvaluateAccess(new LocationConnectionTraversalRequest { connectionId = item.ConnectionId, fromLocationId = locationId, accessContext = context, worldTime = worldTime }).Allowed)
                .OrderBy(item => item.ConnectionId, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<LocationConnectionEndpointSnapshot> GetEndpoints(string connectionId, bool includeHidden = false)
        {
            return GetConnectionIds(endpointsByConnectionId, N(connectionId))
                .Select(id => endpointsById.TryGetValue(id, out LocationConnectionEndpointData item) ? item : null)
                .Where(item => item != null && (includeHidden || VisibleToNormalView(item.visibility)))
                .OrderBy(item => item.endpointId, StringComparer.Ordinal)
                .Select(BuildEndpointSnapshot)
                .ToArray();
        }

        public LocationConnectionRuntimeSaveData CreateSaveData()
        {
            return new LocationConnectionRuntimeSaveData
            {
                schemaVersion = LocationConnectionRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                connections = connectionsById.Values.OrderBy(item => item.connectionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                endpoints = endpointsById.Values.OrderBy(item => item.endpointId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                grants = grantsById.Values.OrderBy(item => item.grantId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                history = historyById.Values.OrderBy(item => item.worldTime).ThenBy(item => item.historyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                transactions = transactionsById.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray()
            };
        }

        public LocationConnectionOperationResult RestoreFromSaveData(LocationConnectionRuntimeSaveData saveData, LocationRuntime locations = null, EntityLocationRuntime entityLocations = null, InteractionPointRuntime interactionPoints = null, string expectedWorldId = PersistenceService.LocalWorldId, bool restoring = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, registry, locations ?? locationRuntime, entityLocations ?? entityLocationRuntime, interactionPoints ?? interactionPointRuntime, expectedWorldId, out string failure))
            {
                return Fail(LocationConnectionOperationStatus.PersistenceInvalid, failure, before);
            }

            LocationConnectionRuntimeSaveData rollback = CreateSaveData();
            try
            {
                RestoreInternal(saveData ?? new LocationConnectionRuntimeSaveData());
                locationRuntime = locations ?? locationRuntime;
                entityLocationRuntime = entityLocations ?? entityLocationRuntime;
                interactionPointRuntime = interactionPoints ?? interactionPointRuntime;
                worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
                IsDirty = !restoring;
                return LocationConnectionOperationResult.Success(null, "Location connections restored.", before, Revision);
            }
            catch (Exception exception)
            {
                RestoreInternal(rollback);
                return Fail(LocationConnectionOperationStatus.RestoreFailed, $"Location connection restore failed: {exception.Message}", before);
            }
        }

        public bool ValidateCurrent(out string failure)
        {
            return ValidateSaveData(CreateSaveData(), registry, locationRuntime, entityLocationRuntime, interactionPointRuntime, worldId, out failure);
        }

        public static bool ValidateSaveData(LocationConnectionRuntimeSaveData saveData, DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, InteractionPointRuntime interactionPoints, string expectedWorldId, out string failure)
        {
            List<string> errors = new List<string>();
            saveData ??= new LocationConnectionRuntimeSaveData();
            string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            if (saveData.schemaVersion < 1 || saveData.schemaVersion > LocationConnectionRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported location connection save schema {saveData.schemaVersion}.");
            if (!string.IsNullOrWhiteSpace(saveData.worldId) && !string.Equals(saveData.worldId.Trim(), world, StringComparison.Ordinal)) errors.Add($"Location connection save world '{saveData.worldId}' does not match '{world}'.");

            HashSet<string> connectionIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> endpointIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LocationConnectionRecordData connection in saveData.connections ?? Array.Empty<LocationConnectionRecordData>())
            {
                if (connection == null) { errors.Add("Location connection save contains a null connection."); continue; }
                string id = N(connection.connectionId);
                if (string.IsNullOrWhiteSpace(id)) errors.Add("Location connection has no ID.");
                else if (!connectionIds.Add(id)) errors.Add($"Duplicate location connection '{id}'.");
                if (registry == null || !registry.TryGet(N(connection.connectionDefinitionId), out LocationConnectionDefinition definition)) errors.Add($"Location connection '{id}' references missing definition '{connection.connectionDefinitionId}'.");
                if (locations == null || !locations.TryGetSnapshot(N(connection.sourceLocationId), out LocationSnapshot source)) errors.Add($"Location connection '{id}' references missing source location '{connection.sourceLocationId}'.");
                if (locations == null || !locations.TryGetSnapshot(N(connection.destinationLocationId), out LocationSnapshot destination)) errors.Add($"Location connection '{id}' references missing destination location '{connection.destinationLocationId}'.");
                if (registry != null && registry.TryGet(N(connection.connectionDefinitionId), out LocationConnectionDefinition def)
                    && locations != null
                    && locations.TryGetSnapshot(N(connection.sourceLocationId), out LocationSnapshot src)
                    && locations.TryGetSnapshot(N(connection.destinationLocationId), out LocationSnapshot dst)
                    && TryGetLocationCategory(registry, src, out LocationCategory sourceCategory)
                    && TryGetLocationCategory(registry, dst, out LocationCategory destinationCategory)
                    && !def.SupportsEndpoint(sourceCategory, destinationCategory)) errors.Add($"Location connection '{id}' endpoint categories are unsupported by '{def.Id}'.");
                if (!Enum.IsDefined(typeof(LocationConnectionDirectionality), connection.directionality) || connection.directionality == LocationConnectionDirectionality.Unknown) errors.Add($"Location connection '{id}' has invalid directionality '{connection.directionality}'.");
                if (!Enum.IsDefined(typeof(LocationConnectionLifecycleState), connection.lifecycleState) || connection.lifecycleState == LocationConnectionLifecycleState.Unknown) errors.Add($"Location connection '{id}' has invalid lifecycle '{connection.lifecycleState}'.");
                if (connection.lifecycleState == LocationConnectionLifecycleState.Active && connection.endedWorldTime >= 0d) errors.Add($"Active location connection '{id}' has an end time.");
                if (connection.endedWorldTime >= 0d && connection.endedWorldTime < connection.createdWorldTime) errors.Add($"Location connection '{id}' ends before it starts.");
                foreach (string policyId in Clean(connection.accessPolicyDefinitionIds))
                {
                    if (registry == null || !registry.TryGet(policyId, out LocationAccessPolicyDefinition _)) errors.Add($"Location connection '{id}' references missing access policy '{policyId}'.");
                }
                foreach (string pointId in Clean(connection.interactionPointIds))
                {
                    if (interactionPoints != null && !interactionPoints.TryGetPoint(pointId, out _)) errors.Add($"Location connection '{id}' references missing interaction point '{pointId}'.");
                }
            }

            foreach (LocationConnectionEndpointData endpoint in saveData.endpoints ?? Array.Empty<LocationConnectionEndpointData>())
            {
                if (endpoint == null) { errors.Add("Location connection save contains a null endpoint."); continue; }
                string id = N(endpoint.endpointId);
                if (string.IsNullOrWhiteSpace(id)) errors.Add("Location connection endpoint has no ID.");
                else if (!endpointIds.Add(id)) errors.Add($"Duplicate location connection endpoint '{id}'.");
                if (!connectionIds.Contains(N(endpoint.connectionId))) errors.Add($"Location connection endpoint '{id}' references missing connection '{endpoint.connectionId}'.");
                if (locations == null || !locations.TryGetSnapshot(N(endpoint.locationId), out _)) errors.Add($"Location connection endpoint '{id}' references missing location '{endpoint.locationId}'.");
                foreach (string policyId in Clean(endpoint.sideAccessPolicyDefinitionIds))
                {
                    if (registry == null || !registry.TryGet(policyId, out LocationAccessPolicyDefinition _)) errors.Add($"Location connection endpoint '{id}' references missing access policy '{policyId}'.");
                }
            }

            HashSet<string> grantIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LocationAccessGrantData grant in saveData.grants ?? Array.Empty<LocationAccessGrantData>())
            {
                if (grant == null) { errors.Add("Location connection save contains a null access grant."); continue; }
                string id = N(grant.grantId);
                if (string.IsNullOrWhiteSpace(id)) errors.Add("Location access grant has no ID.");
                else if (!grantIds.Add(id)) errors.Add($"Duplicate location access grant '{id}'.");
                if (!connectionIds.Contains(N(grant.connectionId))) errors.Add($"Location access grant '{id}' references missing connection '{grant.connectionId}'.");
                if (!string.IsNullOrWhiteSpace(grant.endpointId) && !endpointIds.Contains(N(grant.endpointId))) errors.Add($"Location access grant '{id}' references missing endpoint '{grant.endpointId}'.");
                if (!ValidActor(grant.grantee)) errors.Add($"Location access grant '{id}' has invalid grantee.");
                if (grant.endWorldTime >= 0d && grant.endWorldTime < grant.startWorldTime) errors.Add($"Location access grant '{id}' ends before it starts.");
            }

            foreach (LocationConnectionStateHistoryData item in saveData.history ?? Array.Empty<LocationConnectionStateHistoryData>())
            {
                if (item == null) { errors.Add("Location connection save contains null history."); continue; }
                if (string.IsNullOrWhiteSpace(item.historyId)) errors.Add("Location connection history has no ID.");
                if (!connectionIds.Contains(N(item.connectionId))) errors.Add($"Location connection history '{item.historyId}' references missing connection '{item.connectionId}'.");
            }

            failure = string.Join(" | ", errors.OrderBy(item => item, StringComparer.Ordinal));
            return errors.Count == 0;
        }

        public void Reset()
        {
            connectionsById.Clear();
            endpointsById.Clear();
            grantsById.Clear();
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

        private LocationConnectionOperationResult Traverse(LocationConnectionTraversalRequest request, bool previewOnly)
        {
            request ??= new LocationConnectionTraversalRequest();
            long before = Revision;
            if (!Ready(before, out LocationConnectionOperationResult readiness)) return readiness;
            if (!ValidateRevision(request.expectedRevision, before, out LocationConnectionOperationResult revisionFailure)) return revisionFailure;
            string connectionId = N(request.connectionId);
            if (TryDuplicate(N(request.transactionId), connectionId, "connection.traverse", before, out LocationConnectionOperationResult duplicate)) return duplicate;
            if (!connectionsById.TryGetValue(connectionId, out LocationConnectionRecordData connection)) return Fail(LocationConnectionOperationStatus.MissingConnection, $"Connection '{connectionId}' is missing.", before);
            if (!ValidActor(request.actor)) return Fail(LocationConnectionOperationStatus.MissingActor, "Traversal actor is invalid.", before);
            if (entityLocationRuntime == null) return Fail(LocationConnectionOperationStatus.DestinationUnavailable, "Entity location runtime is unavailable.", before);
            if (!ValidateEntityRevision(request.expectedEntityLocationRevision, entityLocationRuntime.Revision, out EntityLocationOperationResult entityRevisionFailure)) return Fail(LocationConnectionOperationStatus.RevisionConflict, entityRevisionFailure.Message, before);

            if (!entityLocationRuntime.TryGetActivePlacement(request.actor, out EntityPlacementSnapshot placement)) return Fail(LocationConnectionOperationStatus.MissingPlacement, "Traversal actor has no active placement.", before);
            string from = string.IsNullOrWhiteSpace(request.fromLocationId) ? placement.ExactLocationId : N(request.fromLocationId);
            string to = string.IsNullOrWhiteSpace(request.toLocationId) ? OtherEndpoint(connection, from) : N(request.toLocationId);
            if (!string.Equals(placement.ExactLocationId, from, StringComparison.Ordinal)) return Fail(LocationConnectionOperationStatus.WrongOrigin, $"Actor is at '{placement.ExactLocationId}', not '{from}'.", before);

            LocationConnectionAccessResult access = EvaluateAccess(new LocationConnectionTraversalRequest { connectionId = connectionId, actor = request.actor, fromLocationId = from, toLocationId = to, accessContext = request.accessContext, worldTime = request.worldTime });
            if (!access.Allowed) return Fail(StatusFromAccess(access), access.diagnostics, before);

            if (previewOnly || request.preview)
            {
                return LocationConnectionOperationResult.Success(BuildSnapshot(connection), "Traversal preview.", before, before, preview: true);
            }

            EntityLocationRuntimeSaveData entityRollback = entityLocationRuntime.CreateSaveData();
            EntityLocationOperationResult relocation = entityLocationRuntime.Relocate(new EntityRelocationRequest
            {
                transactionId = $"{N(request.transactionId)}.entity-location",
                entity = request.actor.Clone(),
                expectedOriginLocationId = from,
                destinationLocationId = to,
                worldTime = request.worldTime,
                sourceEventId = request.sourceEventId,
                sourceRecordId = request.sourceRecordId,
                provenanceId = request.provenanceId,
                expectedRevision = request.expectedEntityLocationRevision
            });
            if (!relocation.Succeeded)
            {
                entityLocationRuntime.RestoreFromSaveData(entityRollback, locationRuntime, worldId, restoring: true);
                return Fail(LocationConnectionOperationStatus.DestinationUnavailable, relocation.Message, before);
            }

            AddHistory(connection, "traverse", request.worldTime, request.actor, request.sourceEventId, request.sourceRecordId, request.provenanceId);
            Complete(N(request.transactionId), "connection.traverse", connectionId, relocation.Placement?.PlacementId ?? string.Empty);
            Touch();
            return LocationConnectionOperationResult.Success(BuildSnapshot(connection), "Traversal completed.", before, Revision, placementResult: relocation);
        }

        private bool EvaluatePolicies(LocationConnectionRecordData connection, string from, string to, LocationConnectionAccessContextData context, double worldTime, LocationConnectionAccessResult result, List<string> reasons)
        {
            result.membershipSatisfied = true;
            result.rankSatisfied = true;
            result.officeSatisfied = true;
            result.authoritySatisfied = true;
            result.employmentSatisfied = true;
            result.ownershipSatisfied = true;
            result.legalPermissionSatisfied = true;
            result.warrantSatisfied = true;
            result.custodySatisfied = true;
            result.keySatisfied = true;

            if (context?.privileged == true)
            {
                return true;
            }

            List<LocationAccessPolicyDefinition> policies = new List<LocationAccessPolicyDefinition>();
            foreach (string id in Clean(connection.accessPolicyDefinitionIds))
            {
                if (registry != null && registry.TryGet(id, out LocationAccessPolicyDefinition policy)) policies.Add(policy);
            }

            foreach (LocationConnectionEndpointData endpoint in endpointsById.Values.Where(item => item.connectionId == connection.connectionId && item.locationId == from))
            {
                foreach (string id in Clean(endpoint.sideAccessPolicyDefinitionIds))
                {
                    if (registry != null && registry.TryGet(id, out LocationAccessPolicyDefinition policy)) policies.Add(policy);
                }
            }

            if (policies.Count == 0)
            {
                return true;
            }

            bool anyAllow = false;
            List<string> policyFailures = new List<string>();
            foreach (LocationAccessPolicyDefinition policy in policies.OrderByDescending(item => item.Priority).ThenBy(item => item.Id, StringComparer.Ordinal))
            {
                if (Contains(policy.BlacklistedPersonIds, context?.personId)) { reasons.Add("policy.blacklist"); return false; }
                if (Contains(policy.WhitelistedPersonIds, context?.personId)) { anyAllow = true; continue; }
                if (policy.DenyByDefault) { reasons.Add($"policy.denied:{policy.Id}"); return false; }
                if (result.explicitGrantSatisfied) continue;
                if (policy.AllowByDefault || policy.Category == LocationAccessPolicyCategory.Public) { anyAllow = true; continue; }
                if (policy.Category == LocationAccessPolicyCategory.ExplicitWhitelist)
                {
                    policyFailures.Add("missing.whitelist");
                    continue;
                }

                List<string> localReasons = new List<string>();
                LocationConnectionAccessResult local = new LocationConnectionAccessResult();
                bool satisfied = PolicySatisfied(policy, context, connection, from, to, worldTime, local, localReasons);
                if (satisfied)
                {
                    anyAllow = true;
                    MergePolicySignals(result, local);
                }
                else
                {
                    policyFailures.AddRange(localReasons);
                }
            }

            if (!anyAllow && !result.explicitGrantSatisfied)
            {
                reasons.AddRange(policyFailures);
                reasons.Add("policy.no-allow");
            }

            return anyAllow || result.explicitGrantSatisfied;
        }

        private static void MergePolicySignals(LocationConnectionAccessResult target, LocationConnectionAccessResult source)
        {
            if (target == null || source == null) return;
            target.membershipSatisfied |= source.membershipSatisfied;
            target.rankSatisfied |= source.rankSatisfied;
            target.officeSatisfied |= source.officeSatisfied;
            target.authoritySatisfied |= source.authoritySatisfied;
            target.employmentSatisfied |= source.employmentSatisfied;
            target.ownershipSatisfied |= source.ownershipSatisfied;
            target.legalPermissionSatisfied |= source.legalPermissionSatisfied;
            target.warrantSatisfied |= source.warrantSatisfied;
            target.custodySatisfied |= source.custodySatisfied;
            target.keySatisfied |= source.keySatisfied;
        }

        private bool PolicySatisfied(LocationAccessPolicyDefinition policy, LocationConnectionAccessContextData context, LocationConnectionRecordData connection, string from, string to, double worldTime, LocationConnectionAccessResult result, List<string> reasons)
        {
            bool ok = true;
            ok &= Require(policy.RequiredOrganizationIds, context?.organizationIds, "membership", reasons, value => result.membershipSatisfied = value);
            ok &= Require(policy.RequiredRankIds, context?.rankIds, "rank", reasons, value => result.rankSatisfied = value);
            ok &= Require(policy.RequiredOfficeIds, context?.officeIds, "office", reasons, value => result.officeSatisfied = value);
            ok &= Require(policy.RequiredAuthorityIds, context?.authorityIds, "authority", reasons, value => result.authoritySatisfied = value);
            ok &= Require(policy.RequiredEmploymentIds, context?.employmentIds, "employment", reasons, value => result.employmentSatisfied = value);
            ok &= Require(policy.RequiredPropertyIds, context?.propertyIds, "ownership", reasons, value => result.ownershipSatisfied = value);
            ok &= Require(policy.RequiredPermitIds, context?.permitIds, "permit", reasons, value => result.legalPermissionSatisfied = value);
            ok &= Require(policy.RequiredWarrantIds, context?.warrantIds, "warrant", reasons, value => result.warrantSatisfied = value);
            ok &= Require(policy.RequiredCustodyRoleIds, context?.custodyRoleIds, "custody", reasons, value => result.custodySatisfied = value);
            bool keyOk = Require(policy.RequiredKeyInstanceIds, context?.keyInstanceIds, "key", reasons, value => result.keySatisfied = value)
                & Require(policy.RequiredKeyDefinitionIds, context?.keyDefinitionIds, "key", reasons, value => result.keySatisfied = result.keySatisfied && value);
            ok &= keyOk;
            ok &= Require(policy.RequiredCredentialIds, context?.credentialIds, "credential", reasons, value => result.keySatisfied = result.keySatisfied && value);
            return ok;
        }

        private bool ActiveGrantExists(LocationConnectionRecordData connection, LocationConnectionAccessContextData context, string from, string to, double worldTime)
        {
            string actorKey = context?.actor?.StableKey ?? string.Empty;
            return GetConnectionIds(grantsByConnectionId, connection.connectionId)
                .Select(id => grantsById.TryGetValue(id, out LocationAccessGrantData grant) ? grant : null)
                .Any(grant => grant != null
                    && grant.lifecycleState == LocationAccessGrantLifecycleState.Active
                    && (grant.endWorldTime < 0d || grant.endWorldTime > worldTime)
                    && grant.startWorldTime <= worldTime
                    && string.Equals(grant.grantee?.StableKey ?? string.Empty, actorKey, StringComparison.Ordinal)
                    && DirectionAllowed(connection, from, to, grant.directionality));
        }

        private static bool Require(IEnumerable<string> required, IEnumerable<string> actual, string reason, ICollection<string> reasons, Action<bool> setResult)
        {
            string[] req = Clean(required);
            if (req.Length == 0)
            {
                setResult?.Invoke(true);
                return true;
            }

            HashSet<string> have = new HashSet<string>(Clean(actual), StringComparer.Ordinal);
            bool ok = req.Any(have.Contains);
            setResult?.Invoke(ok);
            if (!ok) reasons.Add($"missing.{reason}");
            return ok;
        }

        private static LocationConnectionAccessState AccessStateFromReasons(IEnumerable<string> reasons, LocationConnectionRecordData connection)
        {
            string[] values = (reasons ?? Array.Empty<string>()).ToArray();
            if (values.Length == 0) return LocationConnectionAccessState.Allowed;
            if (values.Any(item => item.StartsWith("direction.", StringComparison.Ordinal))) return LocationConnectionAccessState.DeniedByDirection;
            if (values.Any(item => item.StartsWith("lifecycle.", StringComparison.Ordinal))) return LocationConnectionAccessState.DeniedByLifecycle;
            if (values.Any(item => item.StartsWith("blockage.", StringComparison.Ordinal))) return LocationConnectionAccessState.DeniedByBlockage;
            if (values.Any(item => item.StartsWith("missing.key", StringComparison.Ordinal))) return LocationConnectionAccessState.MissingKey;
            if (values.Any(item => item.StartsWith("missing.permit", StringComparison.Ordinal))) return LocationConnectionAccessState.MissingPermit;
            if (values.Any(item => item.StartsWith("missing.authority", StringComparison.Ordinal) || item.StartsWith("missing.office", StringComparison.Ordinal) || item.StartsWith("missing.whitelist", StringComparison.Ordinal) || item.StartsWith("missing.credential", StringComparison.Ordinal))) return LocationConnectionAccessState.MissingAuthority;
            if (values.Any(item => item.StartsWith("missing.membership", StringComparison.Ordinal) || item.StartsWith("missing.rank", StringComparison.Ordinal))) return LocationConnectionAccessState.MissingMembership;
            if (values.Any(item => item.StartsWith("missing.custody", StringComparison.Ordinal))) return LocationConnectionAccessState.CustodyRestricted;
            if (values.Contains("open.closed") && values.Contains("lock.locked")) return LocationConnectionAccessState.AllowedIfOpenedAndUnlocked;
            if (values.Contains("open.closed")) return LocationConnectionAccessState.AllowedIfOpened;
            if (values.Contains("lock.locked")) return LocationConnectionAccessState.AllowedIfUnlocked;
            return LocationConnectionAccessState.DeniedByPolicy;
        }

        private static LocationConnectionAccessResult Deny(LocationConnectionAccessResult result, LocationConnectionAccessState state, string reason)
        {
            result.accessState = state;
            result.denialReasons = string.IsNullOrWhiteSpace(reason) ? Array.Empty<string>() : new[] { reason };
            result.diagnostics = reason ?? string.Empty;
            return result.Clone();
        }

        private static LocationConnectionOperationStatus StatusFromAccess(LocationConnectionAccessResult access)
        {
            return access.accessState switch
            {
                LocationConnectionAccessState.DeniedByDirection => LocationConnectionOperationStatus.DeniedByDirection,
                LocationConnectionAccessState.DeniedByLifecycle => LocationConnectionOperationStatus.DeniedByLifecycle,
                LocationConnectionAccessState.DeniedByBlockage => LocationConnectionOperationStatus.DeniedByBlockage,
                LocationConnectionAccessState.MissingKey or LocationConnectionAccessState.AllowedIfUnlocked or LocationConnectionAccessState.AllowedIfOpenedAndUnlocked => LocationConnectionOperationStatus.MissingKey,
                LocationConnectionAccessState.MissingPermit => LocationConnectionOperationStatus.MissingPermit,
                LocationConnectionAccessState.MissingAuthority => LocationConnectionOperationStatus.MissingAuthority,
                LocationConnectionAccessState.MissingMembership => LocationConnectionOperationStatus.MissingMembership,
                LocationConnectionAccessState.CustodyRestricted => LocationConnectionOperationStatus.CustodyRestricted,
                LocationConnectionAccessState.AllowedIfOpened => LocationConnectionOperationStatus.DeniedByOpenState,
                _ => LocationConnectionOperationStatus.DeniedByPolicy
            };
        }

        private bool Ready(long before, out LocationConnectionOperationResult failure)
        {
            failure = null;
            if (disposed) return SetFailure(LocationConnectionOperationStatus.Disposed, "Location connection runtime is disposed.", before, out failure);
            if (registry == null) return SetFailure(LocationConnectionOperationStatus.MissingDefinition, "Definition registry is missing.", before, out failure);
            if (locationRuntime == null) return SetFailure(LocationConnectionOperationStatus.MissingEndpoint, "Location runtime is missing.", before, out failure);
            return true;
        }

        private bool TryGetConnectionDefinition(string id, long before, out LocationConnectionDefinition definition, out LocationConnectionOperationResult failure)
        {
            definition = null;
            failure = null;
            if (registry == null || !registry.TryGet(N(id), out definition))
            {
                return SetFailure(LocationConnectionOperationStatus.MissingDefinition, $"Location connection definition '{id}' is not registered.", before, out failure);
            }

            return true;
        }

        private bool ValidateEndpointLocations(string sourceId, string destinationId, LocationConnectionDefinition definition, long before, out LocationSnapshot source, out LocationSnapshot destination, out LocationConnectionOperationResult failure)
        {
            source = null;
            destination = null;
            failure = null;
            if (locationRuntime == null || !locationRuntime.TryGetSnapshot(N(sourceId), out source)) return SetFailure(LocationConnectionOperationStatus.MissingEndpoint, $"Source location '{sourceId}' is missing.", before, out failure);
            if (locationRuntime == null || !locationRuntime.TryGetSnapshot(N(destinationId), out destination)) return SetFailure(LocationConnectionOperationStatus.MissingEndpoint, $"Destination location '{destinationId}' is missing.", before, out failure);
            if (!string.Equals(source.WorldId, worldId, StringComparison.Ordinal) || !string.Equals(destination.WorldId, worldId, StringComparison.Ordinal)) return SetFailure(LocationConnectionOperationStatus.WrongWorld, "Connection endpoints must belong to the runtime world.", before, out failure);
            if (!TryGetLocationCategory(registry, source, out LocationCategory sourceCategory)) return SetFailure(LocationConnectionOperationStatus.MissingEndpoint, $"Source location definition '{source.LocationDefinitionId}' is missing.", before, out failure);
            if (!TryGetLocationCategory(registry, destination, out LocationCategory destinationCategory)) return SetFailure(LocationConnectionOperationStatus.MissingEndpoint, $"Destination location definition '{destination.LocationDefinitionId}' is missing.", before, out failure);
            if (!definition.SupportsEndpoint(sourceCategory, destinationCategory)) return SetFailure(LocationConnectionOperationStatus.InvalidEndpointCategory, $"Connection definition '{definition.Id}' does not support endpoint categories {sourceCategory}->{destinationCategory}.", before, out failure);
            return true;
        }

        private static bool TryGetLocationCategory(DefinitionRegistry definitionRegistry, LocationSnapshot location, out LocationCategory category)
        {
            category = LocationCategory.Unknown;
            if (location == null || definitionRegistry == null || !definitionRegistry.TryGet(location.LocationDefinitionId, out LocationDefinition definition)) return false;
            category = definition.Category;
            return category != LocationCategory.Unknown;
        }

        private bool ValidatePolicies(IEnumerable<string> policyIds, long before, out LocationConnectionOperationResult failure)
        {
            failure = null;
            foreach (string id in Clean(policyIds))
            {
                if (registry == null || !registry.TryGet(id, out LocationAccessPolicyDefinition _)) return SetFailure(LocationConnectionOperationStatus.MissingPolicy, $"Access policy '{id}' is not registered.", before, out failure);
            }

            return true;
        }

        private bool ValidateInteractionPoints(IEnumerable<string> pointIds, long before, out LocationConnectionOperationResult failure)
        {
            failure = null;
            if (interactionPointRuntime == null)
            {
                return true;
            }

            foreach (string id in Clean(pointIds))
            {
                if (!interactionPointRuntime.TryGetPoint(id, out _)) return SetFailure(LocationConnectionOperationStatus.InvalidEndpoint, $"Interaction point '{id}' is not registered.", before, out failure);
            }

            return true;
        }

        private bool ValidateStates(LocationConnectionDefinition definition, LocationConnectionOpenState open, LocationConnectionLockState locked, LocationConnectionBlockageState blockage, long before, out LocationConnectionOperationResult failure)
        {
            failure = null;
            if (!Enum.IsDefined(typeof(LocationConnectionOpenState), open) || open == LocationConnectionOpenState.Unknown) return SetFailure(LocationConnectionOperationStatus.InvalidOpenState, $"Open state '{open}' is invalid.", before, out failure);
            if (!Enum.IsDefined(typeof(LocationConnectionLockState), locked) || locked == LocationConnectionLockState.Unknown) return SetFailure(LocationConnectionOperationStatus.InvalidLockState, $"Lock state '{locked}' is invalid.", before, out failure);
            if (!Enum.IsDefined(typeof(LocationConnectionBlockageState), blockage) || blockage == LocationConnectionBlockageState.Unknown) return SetFailure(LocationConnectionOperationStatus.InvalidBlockageState, $"Blockage state '{blockage}' is invalid.", before, out failure);
            if (!definition.SupportsOpenState && open != LocationConnectionOpenState.NotApplicable) return SetFailure(LocationConnectionOperationStatus.InvalidOpenState, $"Definition '{definition.Id}' does not support open state.", before, out failure);
            if (!definition.SupportsLockState && locked != LocationConnectionLockState.NotLockable) return SetFailure(LocationConnectionOperationStatus.InvalidLockState, $"Definition '{definition.Id}' is not lockable.", before, out failure);
            if (!definition.SupportsBlockageState && blockage != LocationConnectionBlockageState.Clear) return SetFailure(LocationConnectionOperationStatus.InvalidBlockageState, $"Definition '{definition.Id}' does not support blockage.", before, out failure);
            return true;
        }

        private static LocationConnectionOpenState NormalizeOpenState(LocationConnectionDefinition definition, LocationConnectionOpenState requested)
        {
            if (!definition.SupportsOpenState) return LocationConnectionOpenState.NotApplicable;
            return requested == LocationConnectionOpenState.Unknown || requested == LocationConnectionOpenState.NotApplicable ? LocationConnectionOpenState.Open : requested;
        }

        private static LocationConnectionLockState NormalizeLockState(LocationConnectionDefinition definition, LocationConnectionLockState requested)
        {
            if (!definition.SupportsLockState) return LocationConnectionLockState.NotLockable;
            return requested == LocationConnectionLockState.Unknown || requested == LocationConnectionLockState.NotLockable ? LocationConnectionLockState.Unlocked : requested;
        }

        private bool TryDuplicate(string transactionId, string connectionId, string operation, long before, out LocationConnectionOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId) || !transactionsById.TryGetValue(transactionId, out LocationConnectionTransactionRecordData tx)) return false;
            if (!string.Equals(tx.operation, operation, StringComparison.Ordinal) || !string.Equals(tx.connectionId, connectionId, StringComparison.Ordinal))
            {
                result = Fail(LocationConnectionOperationStatus.InvalidRequest, $"Transaction '{transactionId}' already exists for a different connection operation.", before);
                return true;
            }

            connectionsById.TryGetValue(connectionId, out LocationConnectionRecordData connection);
            result = LocationConnectionOperationResult.Success(connection == null ? null : BuildSnapshot(connection), "Duplicate location connection transaction ignored.", before, before, duplicate: true);
            return true;
        }

        private void Complete(string transactionId, string operation, string connectionId, string resultReferenceId)
        {
            if (string.IsNullOrWhiteSpace(transactionId)) return;
            transactionsById[transactionId] = new LocationConnectionTransactionRecordData { transactionId = transactionId, operation = operation, connectionId = connectionId, resultReferenceId = resultReferenceId, revision = Revision + 1L };
        }

        private void Touch()
        {
            Revision++;
            IsDirty = true;
        }

        private void AddHistory(LocationConnectionRecordData connection, string operation, double worldTime, EntityLocationReferenceData actor, string sourceEventId, string sourceRecordId, string provenanceId)
        {
            if (connection == null) return;
            string id = $"location-connection-history.{connection.connectionId}.{Math.Max(1, historyById.Count + 1):0000}";
            historyById[id] = new LocationConnectionStateHistoryData
            {
                historyId = id,
                connectionId = connection.connectionId,
                operation = N(operation),
                lifecycleState = connection.lifecycleState,
                openState = connection.openState,
                lockState = connection.lockState,
                blockageState = connection.blockageState,
                worldTime = worldTime,
                actorKey = actor?.StableKey ?? string.Empty,
                sourceEventId = N(sourceEventId),
                sourceRecordId = N(sourceRecordId),
                provenanceId = N(provenanceId),
                revision = Revision + 1L
            };
        }

        private void RestoreInternal(LocationConnectionRuntimeSaveData saveData)
        {
            connectionsById.Clear();
            endpointsById.Clear();
            grantsById.Clear();
            historyById.Clear();
            transactionsById.Clear();
            worldId = string.IsNullOrWhiteSpace(saveData.worldId) ? worldId : saveData.worldId.Trim();
            foreach (LocationConnectionRecordData connection in saveData.connections ?? Array.Empty<LocationConnectionRecordData>()) connectionsById[N(connection.connectionId)] = connection.Clone();
            foreach (LocationConnectionEndpointData endpoint in saveData.endpoints ?? Array.Empty<LocationConnectionEndpointData>()) endpointsById[N(endpoint.endpointId)] = endpoint.Clone();
            foreach (LocationAccessGrantData grant in saveData.grants ?? Array.Empty<LocationAccessGrantData>()) grantsById[N(grant.grantId)] = grant.Clone();
            foreach (LocationConnectionStateHistoryData item in saveData.history ?? Array.Empty<LocationConnectionStateHistoryData>()) historyById[N(item.historyId)] = item.Clone();
            foreach (LocationConnectionTransactionRecordData tx in saveData.transactions ?? Array.Empty<LocationConnectionTransactionRecordData>()) transactionsById[N(tx.transactionId)] = tx.Clone();
            Revision = Math.Max(0L, saveData.revision);
            RebuildIndexes();
        }

        private void RebuildIndexes()
        {
            outgoingByLocationId.Clear();
            incomingByLocationId.Clear();
            endpointsByConnectionId.Clear();
            grantsByConnectionId.Clear();
            foreach (LocationConnectionRecordData connection in connectionsById.Values)
            {
                AddIndex(outgoingByLocationId, connection.sourceLocationId, connection.connectionId);
                AddIndex(incomingByLocationId, connection.destinationLocationId, connection.connectionId);
                if (connection.directionality == LocationConnectionDirectionality.Bidirectional || connection.directionality == LocationConnectionDirectionality.DestinationToSourceOnly)
                {
                    AddIndex(outgoingByLocationId, connection.destinationLocationId, connection.connectionId);
                    AddIndex(incomingByLocationId, connection.sourceLocationId, connection.connectionId);
                }
            }

            foreach (LocationConnectionEndpointData endpoint in endpointsById.Values) AddIndex(endpointsByConnectionId, endpoint.connectionId, endpoint.endpointId);
            foreach (LocationAccessGrantData grant in grantsById.Values) AddIndex(grantsByConnectionId, grant.connectionId, grant.grantId);
        }

        private static void AddIndex(IDictionary<string, List<string>> index, string key, string value)
        {
            key = N(key);
            value = N(value);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) return;
            if (!index.TryGetValue(key, out List<string> list)) index[key] = list = new List<string>();
            if (!list.Contains(value)) list.Add(value);
        }

        private static IReadOnlyList<string> GetConnectionIds(IReadOnlyDictionary<string, List<string>> index, string key)
        {
            return index.TryGetValue(N(key), out List<string> ids) ? ids.OrderBy(id => id, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        }

        private static bool DirectionAllowed(LocationConnectionRecordData connection, string from, string to)
        {
            return DirectionAllowed(connection, from, to, connection.directionality);
        }

        private static bool DirectionAllowed(LocationConnectionRecordData connection, string from, string to, LocationConnectionDirectionality directionality)
        {
            from = N(from);
            to = N(to);
            if (directionality == LocationConnectionDirectionality.Bidirectional) return (connection.sourceLocationId == from && connection.destinationLocationId == to) || (connection.destinationLocationId == from && connection.sourceLocationId == to);
            if (directionality == LocationConnectionDirectionality.SourceToDestinationOnly) return connection.sourceLocationId == from && connection.destinationLocationId == to;
            if (directionality == LocationConnectionDirectionality.DestinationToSourceOnly) return connection.destinationLocationId == from && connection.sourceLocationId == to;
            return false;
        }

        private static bool ResolveDirection(LocationConnectionRecordData connection, string requestedFrom, out string from, out string to)
        {
            requestedFrom = N(requestedFrom);
            if (requestedFrom == connection.destinationLocationId && (connection.directionality == LocationConnectionDirectionality.Bidirectional || connection.directionality == LocationConnectionDirectionality.DestinationToSourceOnly))
            {
                from = connection.destinationLocationId;
                to = connection.sourceLocationId;
                return true;
            }

            from = connection.sourceLocationId;
            to = connection.destinationLocationId;
            return true;
        }

        private static string OtherEndpoint(LocationConnectionRecordData connection, string from)
        {
            from = N(from);
            if (from == connection.sourceLocationId) return connection.destinationLocationId;
            if (from == connection.destinationLocationId) return connection.sourceLocationId;
            return string.Empty;
        }

        private static bool IsTraversableLifecycle(LocationConnectionLifecycleState state) => state == LocationConnectionLifecycleState.Active;
        private static bool ValidateLifecycleTransition(LocationConnectionLifecycleState current, LocationConnectionLifecycleState next) => next != LocationConnectionLifecycleState.Unknown && next != LocationConnectionLifecycleState.Invalid && !(current == LocationConnectionLifecycleState.Destroyed && next == LocationConnectionLifecycleState.Active);
        private static bool VisibleToNormalView(LocationConnectionVisibility visibility) => visibility == LocationConnectionVisibility.Public || visibility == LocationConnectionVisibility.LocallyKnown || visibility == LocationConnectionVisibility.OrganizationKnown || visibility == LocationConnectionVisibility.MemberKnown || visibility == LocationConnectionVisibility.StaffKnown || visibility == LocationConnectionVisibility.GovernmentKnown || visibility == LocationConnectionVisibility.Restricted;
        private static bool ValidActor(EntityLocationReferenceData actor) => actor != null && actor.entityType != LocationOccupantEntityType.Unknown && !string.IsNullOrWhiteSpace(actor.entityId);
        private static bool Contains(IEnumerable<string> values, string target) => !string.IsNullOrWhiteSpace(target) && Clean(values).Contains(N(target), StringComparer.Ordinal);
        private static LocationConnectionEndpointData CreateEndpoint(string endpointId, string connectionId, string locationId, LocationConnectionEndpointRole role, LocationConnectionVisibility visibility, string sourceEventId, string sourceRecordId, string provenanceId) => new LocationConnectionEndpointData { endpointId = N(endpointId), connectionId = N(connectionId), locationId = N(locationId), role = role, visibility = visibility, sourceEventId = N(sourceEventId), sourceRecordId = N(sourceRecordId), provenanceId = N(provenanceId), revision = 1L };
        private static LocationConnectionSnapshot BuildSnapshot(LocationConnectionRecordData connection) => new LocationConnectionSnapshot(connection);
        private static LocationConnectionEndpointSnapshot BuildEndpointSnapshot(LocationConnectionEndpointData endpoint) => new LocationConnectionEndpointSnapshot(endpoint);
        private static LocationAccessGrantSnapshot BuildGrantSnapshot(LocationAccessGrantData grant) => new LocationAccessGrantSnapshot(grant);
        private static LocationConnectionOperationResult Fail(LocationConnectionOperationStatus status, string message, long before) => LocationConnectionOperationResult.Failure(status, message, before);
        private static bool SetFailure(LocationConnectionOperationStatus status, string message, long before, out LocationConnectionOperationResult failure) { failure = Fail(status, message, before); return false; }
        private static bool ValidateRevision(long expectedRevision, long actualRevision, out LocationConnectionOperationResult failure) { failure = null; if (expectedRevision >= 0L && expectedRevision != actualRevision) { failure = Fail(LocationConnectionOperationStatus.RevisionConflict, $"Expected connection revision {expectedRevision}, actual {actualRevision}.", actualRevision); return false; } return true; }
        private static bool ValidateEntityRevision(long expectedRevision, long actualRevision, out EntityLocationOperationResult failure) { failure = null; if (expectedRevision >= 0L && expectedRevision != actualRevision) { failure = EntityLocationOperationResult.Failure(EntityLocationOperationStatus.RevisionConflict, $"Expected entity location revision {expectedRevision}, actual {actualRevision}.", actualRevision); return false; } return true; }
        private static string First(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? N(fallback) : value.Trim();
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
