using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public sealed class WorldSceneBindingRuntime
    {
        private readonly Dictionary<string, WorldSceneBindingComponent> bindingsByInstanceId = new Dictionary<string, WorldSceneBindingComponent>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> instanceIdsByLogicalKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> instanceIdsByBindingKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private LocationRuntime locations;
        private EntityLocationRuntime entityLocations;
        private InteractionPointRuntime interactionPoints;
        private LocationConnectionRuntime connections;
        private LocationRouteRuntime routes;
        private TravelJourneyRuntime journeys;
        private PoliticalTravelRuntime politicalTravel;
        private string worldId = PersistenceService.LocalWorldId;

        public static WorldSceneBindingRuntime Default { get; } = new WorldSceneBindingRuntime();

        public string WorldId => worldId;
        public int BindingCount => bindingsByInstanceId.Count;
        public bool HasAuthoritativeRuntimes => locations != null || entityLocations != null || interactionPoints != null || connections != null || routes != null || journeys != null || politicalTravel != null;

        public void Configure(
            LocationRuntime locationRuntime,
            EntityLocationRuntime entityLocationRuntime,
            InteractionPointRuntime interactionPointRuntime = null,
            LocationConnectionRuntime connectionRuntime = null,
            LocationRouteRuntime routeRuntime = null,
            TravelJourneyRuntime journeyRuntime = null,
            PoliticalTravelRuntime politicalTravelRuntime = null,
            string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            locations = locationRuntime ?? locations;
            entityLocations = entityLocationRuntime ?? entityLocations;
            interactionPoints = interactionPointRuntime ?? interactionPoints;
            connections = connectionRuntime ?? connections;
            routes = routeRuntime ?? routes;
            journeys = journeyRuntime ?? journeys;
            politicalTravel = politicalTravelRuntime ?? politicalTravel;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? worldId : runtimeWorldId.Trim();
        }

        public void ClearTransientBindings()
        {
            foreach (WorldSceneBindingComponent binding in bindingsByInstanceId.Values.Where(value => value != null).ToArray())
            {
                binding.ApplyBindingResolution(WorldSceneBindingStatus.Unregistered, "Scene binding runtime cleared.");
            }

            bindingsByInstanceId.Clear();
            instanceIdsByLogicalKey.Clear();
            instanceIdsByBindingKey.Clear();
        }

        public WorldSceneBindingSnapshot Register(WorldSceneBindingComponent binding)
        {
            if (binding == null)
            {
                return null;
            }

            string instanceId = binding.InstanceId;
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                binding.RefreshGeneratedInstanceId();
                instanceId = binding.InstanceId;
            }

            binding.AttachRuntime(this);
            bindingsByInstanceId[instanceId] = binding;
            RebuildIndexes();
            ResolveBinding(binding);
            return binding.CreateSnapshot();
        }

        public void Unregister(WorldSceneBindingComponent binding)
        {
            if (binding == null)
            {
                return;
            }

            bindingsByInstanceId.Remove(binding.InstanceId);
            RebuildIndexes();
            binding.ApplyBindingResolution(WorldSceneBindingStatus.Unregistered, "Scene binding unregistered.");
        }

        public bool TryResolve(WorldSceneBindingCategory category, string logicalId, out WorldSceneBindingComponent binding)
        {
            binding = null;
            string key = LogicalKey(category, logicalId);
            if (!instanceIdsByLogicalKey.TryGetValue(key, out List<string> ids))
            {
                return false;
            }

            binding = ids.Select(id => bindingsByInstanceId.TryGetValue(id, out WorldSceneBindingComponent value) ? value : null)
                .Where(value => value != null && value.Status == WorldSceneBindingStatus.Bound)
                .OrderBy(value => value.Role == WorldSceneBindingRole.Primary ? 0 : 1)
                .ThenBy(value => value.BindingKey, StringComparer.Ordinal)
                .FirstOrDefault();
            return binding != null;
        }

        public bool TryResolveByBindingKey(string bindingKey, out WorldSceneBindingComponent binding)
        {
            binding = null;
            string key = N(bindingKey);
            if (string.IsNullOrWhiteSpace(key) || !instanceIdsByBindingKey.TryGetValue(key, out List<string> ids))
            {
                return false;
            }

            binding = ids.Select(id => bindingsByInstanceId.TryGetValue(id, out WorldSceneBindingComponent value) ? value : null)
                .Where(value => value != null && value.Status == WorldSceneBindingStatus.Bound)
                .OrderBy(value => value.Category)
                .ThenBy(value => value.LogicalId, StringComparer.Ordinal)
                .FirstOrDefault();
            return binding != null;
        }

        public IReadOnlyList<WorldSceneBindingSnapshot> GetSnapshots()
        {
            return bindingsByInstanceId.Values
                .Where(value => value != null)
                .Select(value => value.CreateSnapshot())
                .OrderBy(value => value.Category)
                .ThenBy(value => value.LogicalId, StringComparer.Ordinal)
                .ThenBy(value => value.BindingKey, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<EntityLocationReferenceData> GetBoundEntityReferences()
        {
            return bindingsByInstanceId.Values
                .OfType<WorldEntitySceneBinding>()
                .Where(value => value != null && value.isActiveAndEnabled)
                .Select(value => value.EntityReference)
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.entityId))
                .GroupBy(value => $"{value.entityType}:{value.entityId}", StringComparer.Ordinal)
                .Select(group => group.First().Clone())
                .OrderBy(value => value.entityType)
                .ThenBy(value => value.entityId, StringComparer.Ordinal)
                .ToArray();
        }

        public WorldSceneBindingValidationReport Validate()
        {
            foreach (WorldSceneBindingComponent binding in bindingsByInstanceId.Values.Where(value => value != null).ToArray())
            {
                ResolveBinding(binding);
            }

            List<WorldSceneBindingIssue> issues = new List<WorldSceneBindingIssue>();
            foreach (WorldSceneBindingComponent binding in bindingsByInstanceId.Values.Where(value => value != null))
            {
                if (binding.Status == WorldSceneBindingStatus.Invalid || binding.Status == WorldSceneBindingStatus.Duplicate)
                {
                    issues.Add(new WorldSceneBindingIssue(WorldSceneBindingIssueSeverity.Error, binding.Category, binding.LogicalId, binding.BindingKey, binding.Diagnostics));
                }
                else if (binding.Status == WorldSceneBindingStatus.WaitingForLogicalRecord)
                {
                    issues.Add(new WorldSceneBindingIssue(binding.Required ? WorldSceneBindingIssueSeverity.Error : WorldSceneBindingIssueSeverity.Warning, binding.Category, binding.LogicalId, binding.BindingKey, binding.Diagnostics));
                }
                else if (binding.Status == WorldSceneBindingStatus.WaitingForRuntime)
                {
                    issues.Add(new WorldSceneBindingIssue(binding.Required ? WorldSceneBindingIssueSeverity.Error : WorldSceneBindingIssueSeverity.Warning, binding.Category, binding.LogicalId, binding.BindingKey, binding.Diagnostics));
                }
            }

            return new WorldSceneBindingValidationReport(GetSnapshots(), issues);
        }

        public WorldSceneBindingValidationReport SyncAllFromAuthoritative(bool initialSync = false)
        {
            foreach (WorldSceneBindingComponent binding in bindingsByInstanceId.Values.Where(value => value != null).ToArray())
            {
                ResolveBinding(binding);
                if (binding.Status == WorldSceneBindingStatus.Bound)
                {
                    binding.SyncFromAuthoritative(this, initialSync);
                }
            }

            return Validate();
        }

        public bool LogicalRecordExists(WorldSceneBindingCategory category, string logicalId)
        {
            string id = N(logicalId);
            if (string.IsNullOrWhiteSpace(id))
            {
                return category == WorldSceneBindingCategory.PresentationOnly || category == WorldSceneBindingCategory.Custom;
            }

            switch (category)
            {
                case WorldSceneBindingCategory.Location:
                case WorldSceneBindingCategory.SpawnAnchor:
                    return locations != null && locations.TryGetSnapshot(id, out _);
                case WorldSceneBindingCategory.InteractionPoint:
                    return interactionPoints != null && interactionPoints.TryGetPoint(id, out _);
                case WorldSceneBindingCategory.Connection:
                    return connections != null && connections.TryGetConnection(id, out _);
                case WorldSceneBindingCategory.Entity:
                    return entityLocations != null && entityLocations.ResolvePhysicalLocation(ParseEntityReference(id)).Succeeded;
                case WorldSceneBindingCategory.RouteSegment:
                    return routes != null && routes.TryGetSegment(id, out _);
                case WorldSceneBindingCategory.Journey:
                    return journeys != null && journeys.TryGetJourney(id, out _);
                case WorldSceneBindingCategory.Checkpoint:
                    return politicalTravel != null && politicalTravel.TryGetCheckpoint(id, out _);
                case WorldSceneBindingCategory.PresentationOnly:
                case WorldSceneBindingCategory.Custom:
                    return true;
                default:
                    return false;
            }
        }

        public bool TryGetLocation(string locationId, out LocationSnapshot snapshot)
        {
            snapshot = null;
            return locations != null && locations.TryGetSnapshot(locationId, out snapshot);
        }

        public bool TryGetInteractionPoint(string pointId, out InteractionPointSnapshot snapshot)
        {
            snapshot = null;
            return interactionPoints != null && interactionPoints.TryGetPoint(pointId, out snapshot);
        }

        public bool TryGetConnection(string connectionId, out LocationConnectionSnapshot snapshot)
        {
            snapshot = null;
            return connections != null && connections.TryGetConnection(connectionId, out snapshot);
        }

        public bool TryGetActivePlacement(EntityLocationReferenceData entity, out EntityPlacementSnapshot placement)
        {
            placement = null;
            return entityLocations != null && entityLocations.TryGetActivePlacement(entity, out placement);
        }

        public SceneBindingMaterializationResult MaterializeEntity(WorldEntitySceneBinding entityBinding)
        {
            if (entityBinding == null)
            {
                return SceneBindingMaterializationResult.Failure("Entity scene binding is missing.");
            }

            if (entityLocations == null)
            {
                return SceneBindingMaterializationResult.Failure("Entity location runtime is not configured.", entityBinding.LogicalId, entityBinding.BindingKey);
            }

            EntityLocationReferenceData entity = entityBinding.EntityReference;
            if (!entityLocations.TryGetActivePlacement(entity, out EntityPlacementSnapshot placement))
            {
                return SceneBindingMaterializationResult.Failure("Entity has no active authoritative placement.", entityBinding.LogicalId, entityBinding.BindingKey);
            }

            if (!TryResolve(WorldSceneBindingCategory.Location, placement.ExactLocationId, out WorldSceneBindingComponent locationBinding))
            {
                return SceneBindingMaterializationResult.Failure("No loaded scene binding represents the authoritative location.", placement.ExactLocationId);
            }

            Transform anchor = locationBinding.BindingTransform;
            if (anchor == null)
            {
                return SceneBindingMaterializationResult.Failure("Resolved location binding has no Transform anchor.", placement.ExactLocationId, locationBinding.BindingKey);
            }

            entityBinding.transform.position = anchor.position;
            entityBinding.transform.rotation = anchor.rotation;
            if (entityBinding.SnapToGroundAfterMaterialization)
            {
                SpawnGroundingUtility.TrySnapToNearestSolidSurface(entityBinding.transform.position, entityBinding.transform, out Vector3 groundedPosition, out _);
                entityBinding.transform.position = groundedPosition;
            }

            return SceneBindingMaterializationResult.Success("Entity scene representation materialized from authoritative placement.", placement.ExactLocationId, locationBinding.BindingKey);
        }

        public SceneBindingTransitionResult RequestTransition(SceneBindingTransitionRequest request)
        {
            request ??= new SceneBindingTransitionRequest();
            EntityLocationReferenceData actor = request.actor?.Clone();
            if (actor == null || actor.entityType == LocationOccupantEntityType.Unknown || string.IsNullOrWhiteSpace(actor.entityId))
            {
                return SceneBindingTransitionResult.Failure(SceneBindingTransitionStatus.InvalidRequest, "A valid transition actor is required.");
            }

            if (entityLocations == null)
            {
                return SceneBindingTransitionResult.Failure(SceneBindingTransitionStatus.MissingRuntime, "Entity location runtime is not configured.");
            }

            if (!entityLocations.TryGetActivePlacement(actor, out EntityPlacementSnapshot placement))
            {
                return SceneBindingTransitionResult.Failure(SceneBindingTransitionStatus.MissingPlacement, "Transition actor has no active authoritative placement.");
            }

            string source = string.IsNullOrWhiteSpace(request.fromLocationId) ? placement.ExactLocationId : N(request.fromLocationId);
            string destination = N(request.toLocationId);
            if (string.IsNullOrWhiteSpace(destination))
            {
                return SceneBindingTransitionResult.Failure(SceneBindingTransitionStatus.InvalidRequest, "A destination location is required.", source);
            }

            if (!string.IsNullOrWhiteSpace(request.connectionId))
            {
                if (connections == null)
                {
                    return SceneBindingTransitionResult.Failure(SceneBindingTransitionStatus.MissingRuntime, "Location connection runtime is not configured.", source, destination, request.connectionId);
                }

                LocationConnectionOperationResult traversal = connections.Traverse(new LocationConnectionTraversalRequest
                {
                    transactionId = string.IsNullOrWhiteSpace(request.transactionId) ? $"scene-binding.traverse.{Guid.NewGuid():N}" : request.transactionId.Trim(),
                    connectionId = request.connectionId,
                    actor = actor,
                    fromLocationId = source,
                    toLocationId = destination,
                    accessContext = request.accessContext,
                    worldTime = request.worldTime,
                    sourceEventId = "scene-binding.transition",
                    provenanceId = "scene-binding",
                    preview = request.preview
                });

                if (traversal.Succeeded)
                {
                    return SceneBindingTransitionResult.Success(traversal.Message, source, destination, request.connectionId, traversal.Preview, traversal.PlacementResult, traversal);
                }

                SceneBindingTransitionStatus status = traversal.Status == LocationConnectionOperationStatus.MissingPlacement
                    ? SceneBindingTransitionStatus.MissingPlacement
                    : IsAccessFailure(traversal.Status) ? SceneBindingTransitionStatus.AccessDenied : SceneBindingTransitionStatus.RuntimeRejected;
                return SceneBindingTransitionResult.Failure(status, traversal.Message, source, destination, request.connectionId, traversal);
            }

            EntityLocationOperationResult relocation = entityLocations.Relocate(new EntityRelocationRequest
            {
                transactionId = string.IsNullOrWhiteSpace(request.transactionId) ? $"scene-binding.relocate.{Guid.NewGuid():N}" : request.transactionId.Trim(),
                entity = actor,
                expectedOriginLocationId = source,
                destinationLocationId = destination,
                worldTime = request.worldTime,
                sourceEventId = "scene-binding.transition",
                provenanceId = "scene-binding",
                preview = request.preview
            });

            return relocation.Succeeded
                ? SceneBindingTransitionResult.Success(relocation.Message, source, destination, string.Empty, relocation.Preview, relocation, null)
                : SceneBindingTransitionResult.Failure(relocation.Status == EntityLocationOperationStatus.MissingPlacement ? SceneBindingTransitionStatus.MissingPlacement : SceneBindingTransitionStatus.RuntimeRejected, relocation.Message, source, destination);
        }

        public LocationConnectionOperationResult RequestConnectionOpenState(string transactionId, string connectionId, LocationConnectionOpenState openState, EntityLocationReferenceData actor, LocationConnectionAccessContextData accessContext, double worldTime, bool preview)
        {
            if (connections == null)
            {
                return LocationConnectionOperationResult.Failure(LocationConnectionOperationStatus.Disposed, "Location connection runtime is not configured.", 0L);
            }

            if (!connections.TryGetConnection(connectionId, out LocationConnectionSnapshot current))
            {
                return LocationConnectionOperationResult.Failure(LocationConnectionOperationStatus.MissingConnection, $"Connection '{connectionId}' is missing.", connections.Revision);
            }

            return connections.MutateState(new LocationConnectionStateMutationRequest
            {
                transactionId = string.IsNullOrWhiteSpace(transactionId) ? $"scene-binding.connection-state.{Guid.NewGuid():N}" : transactionId.Trim(),
                connectionId = connectionId,
                openState = openState,
                lockState = current.LockState,
                blockageState = current.BlockageState,
                accessContext = accessContext,
                worldTime = worldTime,
                sourceEventId = "scene-binding.connection-state",
                provenanceId = "scene-binding",
                preview = preview
            });
        }

        private void ResolveBinding(WorldSceneBindingComponent binding)
        {
            if (binding == null)
            {
                return;
            }

            if (binding.Role == WorldSceneBindingRole.PresentationOnly || binding.Category == WorldSceneBindingCategory.PresentationOnly)
            {
                binding.ApplyBindingResolution(WorldSceneBindingStatus.Bound, "Presentation-only binding registered.");
                return;
            }

            if (!HasRuntimeFor(binding.Category))
            {
                binding.ApplyBindingResolution(WorldSceneBindingStatus.WaitingForRuntime, $"No authoritative runtime is configured for '{binding.Category}'.");
                return;
            }

            if (string.IsNullOrWhiteSpace(binding.LogicalId))
            {
                binding.ApplyBindingResolution(WorldSceneBindingStatus.Invalid, "Logical ID is required for authoritative scene bindings.");
                return;
            }

            if (IsDuplicatePrimary(binding))
            {
                binding.ApplyBindingResolution(WorldSceneBindingStatus.Duplicate, $"Another primary scene binding already represents '{binding.Category}:{binding.LogicalId}'.");
                return;
            }

            if (!LogicalRecordExists(binding.Category, binding.LogicalId))
            {
                binding.ApplyBindingResolution(WorldSceneBindingStatus.WaitingForLogicalRecord, $"No authoritative logical record exists for '{binding.Category}:{binding.LogicalId}'.");
                return;
            }

            binding.ApplyBindingResolution(WorldSceneBindingStatus.Bound, "Scene binding resolved against authoritative runtime.");
        }

        private bool HasRuntimeFor(WorldSceneBindingCategory category)
        {
            switch (category)
            {
                case WorldSceneBindingCategory.Location:
                case WorldSceneBindingCategory.SpawnAnchor:
                    return locations != null;
                case WorldSceneBindingCategory.InteractionPoint:
                    return interactionPoints != null;
                case WorldSceneBindingCategory.Connection:
                    return connections != null;
                case WorldSceneBindingCategory.Entity:
                    return entityLocations != null;
                case WorldSceneBindingCategory.RouteSegment:
                    return routes != null;
                case WorldSceneBindingCategory.Journey:
                    return journeys != null;
                case WorldSceneBindingCategory.Checkpoint:
                    return politicalTravel != null;
                case WorldSceneBindingCategory.PresentationOnly:
                case WorldSceneBindingCategory.Custom:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsDuplicatePrimary(WorldSceneBindingComponent binding)
        {
            if (binding.Role != WorldSceneBindingRole.Primary)
            {
                return false;
            }

            string logicalKey = LogicalKey(binding.Category, binding.LogicalId);
            if (!instanceIdsByLogicalKey.TryGetValue(logicalKey, out List<string> ids))
            {
                return false;
            }

            WorldSceneBindingComponent[] primaries = ids
                .Select(id => bindingsByInstanceId.TryGetValue(id, out WorldSceneBindingComponent value) ? value : null)
                .Where(value => value != null && value.Role == WorldSceneBindingRole.Primary)
                .OrderBy(value => value.BindingKey, StringComparer.Ordinal)
                .ThenBy(value => value.InstanceId, StringComparer.Ordinal)
                .ToArray();
            return primaries.Length > 1 && !ReferenceEquals(binding, primaries[0]);
        }

        private void RebuildIndexes()
        {
            instanceIdsByLogicalKey.Clear();
            instanceIdsByBindingKey.Clear();
            foreach (WorldSceneBindingComponent binding in bindingsByInstanceId.Values.Where(value => value != null))
            {
                AddIndex(instanceIdsByLogicalKey, LogicalKey(binding.Category, binding.LogicalId), binding.InstanceId);
                if (!string.IsNullOrWhiteSpace(binding.BindingKey))
                {
                    AddIndex(instanceIdsByBindingKey, binding.BindingKey, binding.InstanceId);
                }
            }
        }

        private static void AddIndex(Dictionary<string, List<string>> index, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!index.TryGetValue(key, out List<string> list))
            {
                list = new List<string>();
                index[key] = list;
            }

            if (!list.Contains(value, StringComparer.Ordinal))
            {
                list.Add(value);
                list.Sort(StringComparer.Ordinal);
            }
        }

        private static string LogicalKey(WorldSceneBindingCategory category, string logicalId) => $"{category}:{N(logicalId)}";

        private static bool IsAccessFailure(LocationConnectionOperationStatus status)
        {
            switch (status)
            {
                case LocationConnectionOperationStatus.DeniedByPolicy:
                case LocationConnectionOperationStatus.DeniedByLaw:
                case LocationConnectionOperationStatus.DeniedByDirection:
                case LocationConnectionOperationStatus.DeniedByLifecycle:
                case LocationConnectionOperationStatus.DeniedByOpenState:
                case LocationConnectionOperationStatus.DeniedByLock:
                case LocationConnectionOperationStatus.DeniedByBlockage:
                case LocationConnectionOperationStatus.MissingKey:
                case LocationConnectionOperationStatus.MissingPermit:
                case LocationConnectionOperationStatus.MissingAuthority:
                case LocationConnectionOperationStatus.MissingMembership:
                case LocationConnectionOperationStatus.CustodyRestricted:
                    return true;
                default:
                    return false;
            }
        }

        public static EntityLocationReferenceData ParseEntityReference(string logicalId)
        {
            string id = N(logicalId);
            string[] parts = id.Split(':');
            if (parts.Length == 3 && Enum.TryParse(parts[0], out LocationOccupantEntityType parsed))
            {
                return new EntityLocationReferenceData { entityType = parsed, worldId = parts[1], entityId = parts[2] };
            }

            return new EntityLocationReferenceData { entityType = LocationOccupantEntityType.Person, entityId = id, worldId = PersistenceService.LocalWorldId };
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
