using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.WorldLocations.Integration
{
    public sealed class Step14WorldLocationTravelIntegration
    {
        public Step14IntegrationValidationReport Evaluate(Step14IntegrationSnapshot snapshot)
        {
            return Step14WorldIntegrationValidator.Validate(snapshot);
        }

        public Step14Step15HandoffContract CreateStep15Contract()
        {
            return Step14WorldIntegrationValidator.CreateStep15Contract();
        }
    }

    public static class Step14WorldIntegrationValidator
    {
        public const string SuiteId = "feature.14.12.world-location-travel-integration-finalization";

        public static IReadOnlyList<Step14IntegrationAuthorityEntry> AuthorityMap { get; } = new[]
        {
            A("world identity", "LocationRuntime", authoritative: true, persisted: true, notes: "Owns logical world/location records, not scene objects."),
            A("location records", "LocationRuntime", authoritative: true, persisted: true, notes: "Owns definition-backed location instances and lifecycle."),
            A("location names and aliases", "LocationRuntime", authoritative: true, persisted: true, notes: "Owns naming history and semantic tags."),
            A("location containment hierarchy", "LocationRuntime", authoritative: true, persisted: true, notes: "Owns active and historical parent/child links."),
            A("spatial relationships", "LocationRuntime", authoritative: true, persisted: true, notes: "Descriptive spatial relation only; never route traversal."),
            A("entity placements", "EntityLocationRuntime", authoritative: true, persisted: true, notes: "Owns exact physical location and relocation history."),
            A("person-body physical resolution", "EntityLocationRuntime", authoritative: true, persisted: true, notes: "Resolves Person location through active body references."),
            A("occupancy indexes", "EntityLocationRuntime", derived: true, notes: "Derived from placements and containment; not independently saved."),
            A("interaction points", "InteractionPointRuntime", authoritative: true, persisted: true, notes: "Owns logical functional use points and sessions."),
            A("connection identity and state", "LocationConnectionRuntime", authoritative: true, persisted: true, notes: "Owns traversable entrances, exits, doors, grants, and gate state."),
            A("route graph", "LocationRouteRuntime", authoritative: true, persisted: true, notes: "Owns route segments, networks, and planning graph."),
            A("route plans", "LocationRouteRuntime", derived: true, notes: "Immutable projections created from graph state and caller context."),
            A("journey records", "TravelJourneyRuntime", authoritative: true, persisted: true, notes: "Owns accepted journeys, progress, lifecycle, and replan history."),
            A("journey scheduler state", "TravelJourneyRuntime", derived: true, notes: "Recomputed from journey records and authoritative world time."),
            A("travel conditions", "TravelConditionRuntime", authoritative: true, persisted: true, notes: "Owns weather, hazards, restrictions, modifiers, and explicit encounters."),
            A("political travel overlays", "PoliticalTravelRuntime", authoritative: true, persisted: true, notes: "Owns checkpoint, authorization, and crossing records while delegating law/government facts."),
            A("movement history projections", "MovementHistoryService", derived: true, notes: "Derived from location, placement, journey, condition, and political travel sources."),
            A("scene bindings", "WorldSceneBindingRuntime", derived: true, notes: "Transient Unity binding layer; never authoritative for logical world state."),
            A("Unity transforms", "Unity Scene", external: true, notes: "Presentation and physics only. Logical placement stays in EntityLocationRuntime."),
            A("law and government authority", "Step13", external: true, notes: "Political travel consumes Step 13 records without owning them."),
            A("visibility and redaction", "Step8 InformationAccessRuntime", external: true, notes: "Step 14 exposes subjects; Step 8 decides requester access.")
        };

        public static IReadOnlyList<Step14IntegrationDependencyEntry> PersistenceDependencies { get; } = new[]
        {
            D("step14.locations"),
            D("step14.entity-locations", "step14.locations"),
            D("step14.interaction-points", "step14.locations", "step14.entity-locations"),
            D("step14.connections", "step14.locations", "step14.interaction-points"),
            D("step14.routes", "step14.locations", "step14.connections"),
            D("step14.journeys", "step14.locations", "step14.entity-locations", "step14.connections", "step14.routes"),
            D("step14.travel-conditions", "step14.routes", "step14.journeys"),
            D("step14.political-travel", "step13.governments", "step13.laws", "step13.crimes", "step14.locations", "step14.routes")
        };

        public static Step14IntegrationValidationReport Validate(Step14IntegrationSnapshot snapshot)
        {
            snapshot ??= new Step14IntegrationSnapshot(new Step14PersistenceSnapshotSource { worldId = PersistenceService.LocalWorldId });
            Step14PersistenceManifest manifest = Step14PersistenceManifestBuilder.Build(snapshot.PersistenceSource);
            List<Step14IntegrationDiagnostic> diagnostics = new List<Step14IntegrationDiagnostic>();

            ValidateAuthorityMap(diagnostics);
            ValidateDependencies(manifest, diagnostics);
            ImportPersistenceDiagnostics(manifest, diagnostics);
            ValidateReadinessInputs(snapshot, diagnostics);
            ValidateWorldScope(snapshot.PersistenceSource, diagnostics);
            ValidateStableIdentity(snapshot.PersistenceSource, diagnostics);
            ValidateHierarchy(snapshot.PersistenceSource.locations, diagnostics);
            ValidateEntityPlacements(snapshot.PersistenceSource, diagnostics);
            ValidateInteractionPoints(snapshot.PersistenceSource, diagnostics);
            ValidateConnectionGraph(snapshot.PersistenceSource, diagnostics);
            ValidateRouteGraph(snapshot.PersistenceSource, diagnostics);
            ValidateJourneys(snapshot.PersistenceSource, diagnostics);
            ValidateTravelConditions(snapshot.PersistenceSource, diagnostics);
            ValidatePoliticalTravel(snapshot.PersistenceSource, diagnostics);
            ValidateSceneBindings(snapshot, diagnostics);

            Step14Step15HandoffContract contract = CreateStep15Contract();
            if (!contract.Succeeded)
            {
                diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Fatal, Step14IntegrationDiagnosticDomain.Step15Contract, "step15", "Step 15 handoff contract is incomplete."));
            }

            string fingerprint = CreateCanonicalFingerprint(snapshot);
            Step14IntegrationReadinessState readiness = DetermineReadiness(diagnostics);
            return new Step14IntegrationValidationReport(readiness, manifest, AuthorityMap, PersistenceDependencies, diagnostics, contract, fingerprint);
        }

        public static string CreateCanonicalFingerprint(Step14IntegrationSnapshot snapshot)
        {
            snapshot ??= new Step14IntegrationSnapshot(new Step14PersistenceSnapshotSource { worldId = PersistenceService.LocalWorldId });
            Step14PersistenceSnapshotSource source = snapshot.PersistenceSource;
            StringBuilder builder = new StringBuilder();
            Append(builder, "world", source.worldId);
            Append(builder, "slot", source.saveSlotId);
            Append(builder, "time", source.authoritativeWorldTime.ToString("R"));
            AppendLocations(builder, source.locations);
            AppendEntityLocations(builder, source.entityLocations);
            AppendInteractionPoints(builder, source.interactionPoints);
            AppendConnections(builder, source.connections);
            AppendRoutes(builder, source.routes);
            AppendJourneys(builder, source.journeys);
            AppendConditions(builder, source.travelConditions);
            AppendPoliticalTravel(builder, source.politicalTravel);
            foreach (WorldSceneBindingSnapshot binding in snapshot.SceneBindingValidation?.Bindings ?? Array.Empty<WorldSceneBindingSnapshot>())
            {
                Append(builder, "scene-binding", $"{binding.StableKey}|{binding.Status}|{binding.Role}|{binding.Required}");
            }

            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        public static Step14Step15HandoffContract CreateStep15Contract()
        {
            return new Step14Step15HandoffContract(
                stableReferenceTypes: new[]
                {
                    "world-id",
                    "location-id",
                    "location-path",
                    "entity-location-reference",
                    "interaction-point-id",
                    "connection-id",
                    "route-segment-id",
                    "route-network-id",
                    "journey-id",
                    "travel-condition-id",
                    "travel-encounter-id",
                    "border-checkpoint-id",
                    "travel-authorization-id",
                    "scene-binding-key"
                },
                queryCapabilities: new[]
                {
                    "get-current-location",
                    "get-location-at-time",
                    "get-containment-path",
                    "get-direct-occupants",
                    "get-recursive-occupants",
                    "get-available-interaction-points",
                    "evaluate-connection-access",
                    "plan-route",
                    "revalidate-route-plan",
                    "get-active-journey",
                    "project-movement-history",
                    "evaluate-travel-conditions",
                    "evaluate-political-travel-requirements",
                    "resolve-scene-binding"
                },
                commandCapabilities: new[]
                {
                    "create-location",
                    "assign-containment",
                    "relocate-entity",
                    "reserve-interaction-point",
                    "traverse-connection",
                    "grant-connection-access",
                    "create-route-segment",
                    "start-journey",
                    "pause-resume-cancel-journey",
                    "apply-travel-condition",
                    "trigger-explicit-travel-encounter",
                    "record-border-crossing"
                },
                deferredBoundaries: new[]
                {
                    "quest-authoring",
                    "dialogue-behavior",
                    "autonomous-npc-decision-making",
                    "streaming-world-partitioning",
                    "multiplayer-authority",
                    "final-ui-visibility",
                    "procedural-settlement-generation",
                    "scene-lighting-and-rendering"
                });
        }

        private static void ValidateAuthorityMap(List<Step14IntegrationDiagnostic> diagnostics)
        {
            foreach (IGrouping<string, Step14IntegrationAuthorityEntry> group in AuthorityMap.Where(item => item.Authoritative).GroupBy(item => item.Domain, StringComparer.Ordinal))
            {
                if (group.Count() > 1)
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Fatal, Step14IntegrationDiagnosticDomain.Authority, group.Key, $"Authority domain '{group.Key}' has more than one authoritative owner."));
                }
            }

            if (AuthorityMap.Any(item => item.Domain.Contains("scene", StringComparison.OrdinalIgnoreCase) && item.Authoritative))
            {
                diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Fatal, Step14IntegrationDiagnosticDomain.Authority, "scene-bindings", "Scene binding domains must stay derived or external, never authoritative."));
            }
        }

        private static void ValidateDependencies(Step14PersistenceManifest manifest, List<Step14IntegrationDiagnostic> diagnostics)
        {
            HashSet<string> declared = new HashSet<string>(PersistenceDependencies.Select(item => item.ParticipantId), StringComparer.Ordinal);
            foreach (Step14PersistenceParticipantManifest participant in manifest.Participants)
            {
                if (!declared.Contains(participant.ParticipantId))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Warning, Step14IntegrationDiagnosticDomain.Dependency, participant.ParticipantId, "Persistence participant is not declared in the Step 14 integration dependency graph."));
                }
            }

            Dictionary<string, string[]> dependencyMap = PersistenceDependencies.ToDictionary(item => item.ParticipantId, item => item.RequiredDependencies.Where(dep => dep.StartsWith("step14.", StringComparison.Ordinal)).ToArray(), StringComparer.Ordinal);
            foreach (string participant in dependencyMap.Keys)
            {
                if (HasDependencyCycle(participant, participant, dependencyMap, new HashSet<string>(StringComparer.Ordinal)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Fatal, Step14IntegrationDiagnosticDomain.Dependency, participant, "Persistence dependency graph contains a cycle."));
                }
            }
        }

        private static void ImportPersistenceDiagnostics(Step14PersistenceManifest manifest, List<Step14IntegrationDiagnostic> diagnostics)
        {
            foreach (Step14PersistenceValidationIssue issue in manifest.ValidationReport.Issues)
            {
                Step14IntegrationDiagnosticSeverity severity = issue.Severity == Step14PersistenceValidationSeverity.Fatal
                    ? Step14IntegrationDiagnosticSeverity.Fatal
                    : issue.Severity == Step14PersistenceValidationSeverity.Error
                        ? Step14IntegrationDiagnosticSeverity.Error
                        : issue.Severity == Step14PersistenceValidationSeverity.Warning
                            ? Step14IntegrationDiagnosticSeverity.Warning
                            : Step14IntegrationDiagnosticSeverity.Info;
                diagnostics.Add(new Step14IntegrationDiagnostic(severity, Step14IntegrationDiagnosticDomain.Persistence, issue.RecordId, issue.Message));
            }
        }

        private static void ValidateReadinessInputs(Step14IntegrationSnapshot snapshot, List<Step14IntegrationDiagnostic> diagnostics)
        {
            if (!snapshot.AuthoritativeTimeAvailable)
            {
                diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Fatal, Step14IntegrationDiagnosticDomain.Scheduler, "world-time", "Authoritative world time is required for journey progress and historical movement."));
            }

            if (!snapshot.SchedulerAvailable)
            {
                diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.Scheduler, "journey-scheduler", "Journey scheduling is not available."));
            }

            if (!snapshot.PrototypeFixtureAvailable)
            {
                diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Warning, Step14IntegrationDiagnosticDomain.PrototypeFixture, "test-lab", "Prototype fixture ownership is unavailable; runtime validation can run but automated scenario reset is degraded."));
            }
        }

        private static void ValidateWorldScope(Step14PersistenceSnapshotSource source, List<Step14IntegrationDiagnostic> diagnostics)
        {
            string expected = N(source.worldId, PersistenceService.LocalWorldId);
            CheckWorld("locations", source.locations?.worldId, expected, diagnostics);
            CheckWorld("entity-locations", source.entityLocations?.worldId, expected, diagnostics);
            CheckWorld("interaction-points", source.interactionPoints?.worldId, expected, diagnostics);
            CheckWorld("connections", source.connections?.worldId, expected, diagnostics);
            CheckWorld("routes", source.routes?.worldId, expected, diagnostics);
            CheckWorld("journeys", source.journeys?.worldId, expected, diagnostics);
            CheckWorld("travel-conditions", source.travelConditions?.worldId, expected, diagnostics);
            CheckWorld("political-travel", source.politicalTravel?.worldId, expected, diagnostics);
        }

        private static void ValidateStableIdentity(Step14PersistenceSnapshotSource source, List<Step14IntegrationDiagnostic> diagnostics)
        {
            CheckUnique(source.locations?.records?.Select(item => item?.locationId), "location", Step14IntegrationDiagnosticDomain.StableIdentity, diagnostics);
            CheckUnique(source.locations?.containmentLinks?.Select(item => item?.linkId), "containment-link", Step14IntegrationDiagnosticDomain.StableIdentity, diagnostics);
            CheckUnique(source.entityLocations?.placements?.Select(item => item?.placementId), "entity-placement", Step14IntegrationDiagnosticDomain.StableIdentity, diagnostics);
            CheckUnique(source.interactionPoints?.points?.Select(item => item?.interactionPointId), "interaction-point", Step14IntegrationDiagnosticDomain.StableIdentity, diagnostics);
            CheckUnique(source.connections?.connections?.Select(item => item?.connectionId), "connection", Step14IntegrationDiagnosticDomain.StableIdentity, diagnostics);
            CheckUnique(source.routes?.segments?.Select(item => item?.segmentId), "route-segment", Step14IntegrationDiagnosticDomain.StableIdentity, diagnostics);
            CheckUnique(source.journeys?.journeys?.Select(item => item?.journeyId), "journey", Step14IntegrationDiagnosticDomain.StableIdentity, diagnostics);
            CheckUnique(source.travelConditions?.conditions?.Select(item => item?.conditionId), "travel-condition", Step14IntegrationDiagnosticDomain.StableIdentity, diagnostics);
            CheckUnique(source.politicalTravel?.checkpoints?.Select(item => item?.checkpointId), "border-checkpoint", Step14IntegrationDiagnosticDomain.StableIdentity, diagnostics);
        }

        private static void ValidateHierarchy(LocationRuntimeSaveData locations, List<Step14IntegrationDiagnostic> diagnostics)
        {
            HashSet<string> locationIds = LocationIds(locations);
            Dictionary<string, string> activeParentByChild = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (LocationContainmentLinkData link in locations?.containmentLinks ?? new List<LocationContainmentLinkData>())
            {
                if (link == null || link.state != LocationLinkState.Active || link.kind != LocationContainmentKind.Primary) continue;
                string child = N(link.childLocationId);
                string parent = N(link.parentLocationId);
                if (!locationIds.Contains(child) || !locationIds.Contains(parent))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.Hierarchy, link.linkId, "Active containment link references a missing location."));
                    continue;
                }

                if (activeParentByChild.TryGetValue(child, out string previousParent) && !string.Equals(previousParent, parent, StringComparison.Ordinal))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.Hierarchy, child, "Location has more than one active primary parent."));
                }
                else activeParentByChild[child] = parent;
            }

            foreach (string child in activeParentByChild.Keys)
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                string current = child;
                while (activeParentByChild.TryGetValue(current, out string parent))
                {
                    if (!seen.Add(current) || string.Equals(parent, child, StringComparison.Ordinal))
                    {
                        diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.Hierarchy, child, "Location containment graph contains a cycle."));
                        break;
                    }
                    current = parent;
                }
            }

            foreach (LocationSpatialRelationshipData relation in locations?.spatialRelationships ?? new List<LocationSpatialRelationshipData>())
            {
                if (relation == null || relation.state != LocationLinkState.Active) continue;
                if (!locationIds.Contains(N(relation.sourceLocationId)) || !locationIds.Contains(N(relation.targetLocationId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.SpatialRelationship, relation.relationshipId, "Spatial relationship references a missing location."));
                }
            }
        }

        private static void ValidateEntityPlacements(Step14PersistenceSnapshotSource source, List<Step14IntegrationDiagnostic> diagnostics)
        {
            HashSet<string> locationIds = LocationIds(source.locations);
            HashSet<string> activeEntities = new HashSet<string>(StringComparer.Ordinal);
            foreach (EntityPlacementRecordData placement in source.entityLocations?.placements ?? new List<EntityPlacementRecordData>())
            {
                if (placement == null) continue;
                string entityKey = placement.entity?.StableKey ?? string.Empty;
                if (placement.lifecycleState == EntityPlacementLifecycleState.Active)
                {
                    if (!activeEntities.Add(entityKey))
                    {
                        diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.EntityPlacement, entityKey, "Entity has more than one active exact placement."));
                    }

                    if (!locationIds.Contains(N(placement.exactLocationId)))
                    {
                        diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.EntityPlacement, placement.placementId, "Active entity placement references a missing exact location."));
                    }
                }
            }

            HashSet<string> knownBodies = new HashSet<string>((source.entityLocations?.knownEntities ?? new List<EntityLocationReferenceData>())
                .Where(item => item != null && item.entityType == LocationOccupantEntityType.Body)
                .Select(item => N(item.entityId)), StringComparer.Ordinal);
            foreach (EntityPersonBodyBindingData binding in source.entityLocations?.personBodyBindings ?? new List<EntityPersonBodyBindingData>())
            {
                if (binding == null || binding.bodyDestroyed || string.IsNullOrWhiteSpace(binding.activeBodyId)) continue;
                if (!knownBodies.Contains(N(binding.activeBodyId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.PersonBody, binding.personId, "Person active body binding references a body not registered with entity location authority."));
                }
            }
        }

        private static void ValidateInteractionPoints(Step14PersistenceSnapshotSource source, List<Step14IntegrationDiagnostic> diagnostics)
        {
            HashSet<string> locationIds = LocationIds(source.locations);
            foreach (InteractionPointRecordData point in source.interactionPoints?.points ?? new List<InteractionPointRecordData>())
            {
                if (point == null) continue;
                if (!string.IsNullOrWhiteSpace(point.activeHostLocationId) && !locationIds.Contains(N(point.activeHostLocationId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.InteractionPoint, point.interactionPointId, "Interaction point active host location is missing."));
                }
            }
        }

        private static void ValidateConnectionGraph(Step14PersistenceSnapshotSource source, List<Step14IntegrationDiagnostic> diagnostics)
        {
            HashSet<string> locationIds = LocationIds(source.locations);
            HashSet<string> connectionIds = new HashSet<string>((source.connections?.connections ?? Array.Empty<LocationConnectionRecordData>()).Where(item => item != null).Select(item => N(item.connectionId)), StringComparer.Ordinal);
            foreach (LocationConnectionRecordData connection in source.connections?.connections ?? Array.Empty<LocationConnectionRecordData>())
            {
                if (connection == null) continue;
                if (!locationIds.Contains(N(connection.sourceLocationId)) || !locationIds.Contains(N(connection.destinationLocationId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.ConnectionAccess, connection.connectionId, "Connection references a missing source or destination location."));
                }
            }

            foreach (LocationConnectionEndpointData endpoint in source.connections?.endpoints ?? Array.Empty<LocationConnectionEndpointData>())
            {
                if (endpoint == null) continue;
                if (!connectionIds.Contains(N(endpoint.connectionId)) || !locationIds.Contains(N(endpoint.locationId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.ConnectionAccess, endpoint.endpointId, "Connection endpoint references a missing connection or location."));
                }
            }
        }

        private static void ValidateRouteGraph(Step14PersistenceSnapshotSource source, List<Step14IntegrationDiagnostic> diagnostics)
        {
            HashSet<string> locationIds = LocationIds(source.locations);
            HashSet<string> segmentIds = new HashSet<string>((source.routes?.segments ?? Array.Empty<LocationRouteSegmentRecordData>()).Where(item => item != null).Select(item => N(item.segmentId)), StringComparer.Ordinal);
            foreach (LocationRouteSegmentRecordData segment in source.routes?.segments ?? Array.Empty<LocationRouteSegmentRecordData>())
            {
                if (segment == null) continue;
                if (!locationIds.Contains(N(segment.sourceLocationId)) || !locationIds.Contains(N(segment.destinationLocationId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.RouteGraph, segment.segmentId, "Route segment references a missing source or destination location."));
                }
                if (!double.IsFinite(segment.distanceMeters) || segment.distanceMeters < 0d || !double.IsFinite(segment.baseCostUnits) || segment.baseCostUnits < 0d)
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.RouteGraph, segment.segmentId, "Route segment has invalid distance or cost."));
                }
            }

            foreach (LocationRouteNetworkRecordData network in source.routes?.networks ?? Array.Empty<LocationRouteNetworkRecordData>())
            {
                if (network == null) continue;
                foreach (string segmentId in network.segmentIds ?? Array.Empty<string>())
                {
                    if (!segmentIds.Contains(N(segmentId)))
                    {
                        diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.RouteGraph, network.networkId, $"Route network references missing segment '{segmentId}'."));
                    }
                }
            }
        }

        private static void ValidateJourneys(Step14PersistenceSnapshotSource source, List<Step14IntegrationDiagnostic> diagnostics)
        {
            HashSet<string> locationIds = LocationIds(source.locations);
            HashSet<string> journeyIds = new HashSet<string>((source.journeys?.journeys ?? Array.Empty<TravelJourneyRecordData>()).Where(item => item != null).Select(item => N(item.journeyId)), StringComparer.Ordinal);
            foreach (TravelJourneyRecordData journey in source.journeys?.journeys ?? Array.Empty<TravelJourneyRecordData>())
            {
                if (journey == null) continue;
                if (!locationIds.Contains(N(journey.originLocationId)) || !locationIds.Contains(N(journey.destinationLocationId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.Journey, journey.journeyId, "Journey references a missing origin or destination location."));
                }
            }

            foreach (TravelJourneyStepRecordData step in source.journeys?.steps ?? Array.Empty<TravelJourneyStepRecordData>())
            {
                if (step == null) continue;
                if (!journeyIds.Contains(N(step.journeyId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.Journey, step.journeyStepId, "Journey step references a missing journey."));
                }
            }
        }

        private static void ValidateTravelConditions(Step14PersistenceSnapshotSource source, List<Step14IntegrationDiagnostic> diagnostics)
        {
            HashSet<string> journeyIds = new HashSet<string>((source.journeys?.journeys ?? Array.Empty<TravelJourneyRecordData>()).Where(item => item != null).Select(item => N(item.journeyId)), StringComparer.Ordinal);
            HashSet<string> conditionIds = new HashSet<string>((source.travelConditions?.conditions ?? Array.Empty<TravelConditionRecordData>()).Where(item => item != null).Select(item => N(item.conditionId)), StringComparer.Ordinal);
            foreach (TravelEncounterRecordData encounter in source.travelConditions?.encounters ?? Array.Empty<TravelEncounterRecordData>())
            {
                if (encounter == null) continue;
                if (!string.IsNullOrWhiteSpace(encounter.sourceConditionId) && !conditionIds.Contains(N(encounter.sourceConditionId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.Encounter, encounter.encounterId, "Travel encounter references a missing source condition."));
                }
                if (!string.IsNullOrWhiteSpace(encounter.journeyId) && !journeyIds.Contains(N(encounter.journeyId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.Encounter, encounter.encounterId, "Travel encounter references a missing journey."));
                }
            }
        }

        private static void ValidatePoliticalTravel(Step14PersistenceSnapshotSource source, List<Step14IntegrationDiagnostic> diagnostics)
        {
            HashSet<string> routeSegmentIds = new HashSet<string>((source.routes?.segments ?? Array.Empty<LocationRouteSegmentRecordData>()).Where(item => item != null).Select(item => N(item.segmentId)), StringComparer.Ordinal);
            foreach (BorderCheckpointRecordData checkpoint in source.politicalTravel?.checkpoints ?? Array.Empty<BorderCheckpointRecordData>())
            {
                if (checkpoint == null) continue;
                if (!string.IsNullOrWhiteSpace(checkpoint.routeSegmentId) && !routeSegmentIds.Contains(N(checkpoint.routeSegmentId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.PoliticalTravel, checkpoint.checkpointId, "Border checkpoint references a missing route segment."));
                }
            }
        }

        private static void ValidateSceneBindings(Step14IntegrationSnapshot snapshot, List<Step14IntegrationDiagnostic> diagnostics)
        {
            WorldSceneBindingValidationReport report = snapshot.SceneBindingValidation;
            if (report == null)
            {
                diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Info, Step14IntegrationDiagnosticDomain.SceneBinding, "scene-binding", "No scene binding report supplied; logical validation remains authoritative."));
                return;
            }

            foreach (WorldSceneBindingIssue issue in report.Issues)
            {
                Step14IntegrationDiagnosticSeverity severity = issue.Severity == WorldSceneBindingIssueSeverity.Error ? Step14IntegrationDiagnosticSeverity.Error : issue.Severity == WorldSceneBindingIssueSeverity.Warning ? Step14IntegrationDiagnosticSeverity.Warning : Step14IntegrationDiagnosticSeverity.Info;
                diagnostics.Add(new Step14IntegrationDiagnostic(severity, Step14IntegrationDiagnosticDomain.SceneBinding, issue.LogicalId, issue.Message));
            }

            HashSet<string> locationIds = LocationIds(snapshot.PersistenceSource.locations);
            foreach (WorldSceneBindingSnapshot binding in report.Bindings.Where(item => item.Category == WorldSceneBindingCategory.Location && item.Required))
            {
                if (!locationIds.Contains(N(binding.LogicalId)))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.SceneBinding, binding.BindingKey, "Required location scene binding references a missing logical location."));
                }
            }
        }

        private static Step14IntegrationReadinessState DetermineReadiness(IReadOnlyList<Step14IntegrationDiagnostic> diagnostics)
        {
            if (diagnostics.Any(item => item.Severity == Step14IntegrationDiagnosticSeverity.Fatal)) return Step14IntegrationReadinessState.Failed;
            if (diagnostics.Any(item => item.Severity == Step14IntegrationDiagnosticSeverity.Error)) return Step14IntegrationReadinessState.Failed;
            if (diagnostics.Any(item => item.Severity == Step14IntegrationDiagnosticSeverity.Warning)) return Step14IntegrationReadinessState.Degraded;
            return Step14IntegrationReadinessState.Ready;
        }

        private static bool HasDependencyCycle(string root, string current, Dictionary<string, string[]> dependencies, HashSet<string> seen)
        {
            if (!seen.Add(current)) return false;
            if (!dependencies.TryGetValue(current, out string[] next)) return false;
            foreach (string dependency in next)
            {
                if (string.Equals(dependency, root, StringComparison.Ordinal)) return true;
                if (HasDependencyCycle(root, dependency, dependencies, seen)) return true;
            }
            return false;
        }

        private static void CheckWorld(string participant, string worldId, string expected, List<Step14IntegrationDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(worldId)) return;
            if (!string.Equals(worldId.Trim(), expected, StringComparison.Ordinal))
            {
                diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, Step14IntegrationDiagnosticDomain.WorldScope, participant, $"Participant world '{worldId}' does not match expected world '{expected}'."));
            }
        }

        private static void CheckUnique(IEnumerable<string> ids, string label, Step14IntegrationDiagnosticDomain domain, List<Step14IntegrationDiagnostic> diagnostics)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string raw in ids ?? Array.Empty<string>())
            {
                string id = N(raw);
                if (string.IsNullOrWhiteSpace(id))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, domain, label, $"{label} has a missing stable ID."));
                }
                else if (!seen.Add(id))
                {
                    diagnostics.Add(new Step14IntegrationDiagnostic(Step14IntegrationDiagnosticSeverity.Error, domain, id, $"{label} stable ID is duplicated."));
                }
            }
        }

        private static HashSet<string> LocationIds(LocationRuntimeSaveData locations)
        {
            return new HashSet<string>((locations?.records ?? new List<LocationRecordData>()).Where(item => item != null).Select(item => N(item.locationId)).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        }

        private static void AppendLocations(StringBuilder builder, LocationRuntimeSaveData data)
        {
            Append(builder, "locations-schema", data?.schemaVersion.ToString() ?? "missing");
            foreach (LocationRecordData record in data?.records?.Where(item => item != null).OrderBy(item => item.locationId, StringComparer.Ordinal) ?? Enumerable.Empty<LocationRecordData>())
                Append(builder, "location", $"{record.locationId}|{record.locationDefinitionId}|{record.worldId}|{record.lifecycleState}|{record.visibility}|{record.revision}");
            foreach (LocationContainmentLinkData link in data?.containmentLinks?.Where(item => item != null).OrderBy(item => item.linkId, StringComparer.Ordinal) ?? Enumerable.Empty<LocationContainmentLinkData>())
                Append(builder, "containment", $"{link.linkId}|{link.parentLocationId}|{link.childLocationId}|{link.kind}|{link.state}|{link.effectiveStartWorldTime:R}|{link.effectiveEndWorldTime:R}");
            foreach (LocationSpatialRelationshipData relation in data?.spatialRelationships?.Where(item => item != null).OrderBy(item => item.relationshipId, StringComparer.Ordinal) ?? Enumerable.Empty<LocationSpatialRelationshipData>())
                Append(builder, "spatial", $"{relation.relationshipId}|{relation.sourceLocationId}|{relation.targetLocationId}|{relation.kind}|{relation.directionality}|{relation.state}");
        }

        private static void AppendEntityLocations(StringBuilder builder, EntityLocationRuntimeSaveData data)
        {
            Append(builder, "entity-schema", data?.schemaVersion.ToString() ?? "missing");
            foreach (EntityPlacementRecordData placement in data?.placements?.Where(item => item != null).OrderBy(item => item.placementId, StringComparer.Ordinal) ?? Enumerable.Empty<EntityPlacementRecordData>())
                Append(builder, "placement", $"{placement.placementId}|{placement.entity?.StableKey}|{placement.exactLocationId}|{placement.lifecycleState}|{placement.startWorldTime:R}|{placement.endWorldTime:R}|{placement.revision}");
            foreach (EntityPersonBodyBindingData binding in data?.personBodyBindings?.Where(item => item != null).OrderBy(item => item.personId, StringComparer.Ordinal) ?? Enumerable.Empty<EntityPersonBodyBindingData>())
                Append(builder, "person-body", $"{binding.personId}|{binding.activeBodyId}|{binding.bodyDestroyed}");
        }

        private static void AppendInteractionPoints(StringBuilder builder, InteractionPointRuntimeSaveData data)
        {
            Append(builder, "interaction-schema", data?.schemaVersion.ToString() ?? "missing");
            foreach (InteractionPointRecordData point in data?.points?.Where(item => item != null).OrderBy(item => item.interactionPointId, StringComparer.Ordinal) ?? Enumerable.Empty<InteractionPointRecordData>())
                Append(builder, "interaction-point", $"{point.interactionPointId}|{point.interactionPointDefinitionId}|{point.activeHostLocationId}|{point.lifecycleState}|{point.visibility}|{point.revision}");
        }

        private static void AppendConnections(StringBuilder builder, LocationConnectionRuntimeSaveData data)
        {
            Append(builder, "connection-schema", data?.schemaVersion.ToString() ?? "missing");
            foreach (LocationConnectionRecordData connection in data?.connections?.Where(item => item != null).OrderBy(item => item.connectionId, StringComparer.Ordinal) ?? Enumerable.Empty<LocationConnectionRecordData>())
                Append(builder, "connection", $"{connection.connectionId}|{connection.connectionDefinitionId}|{connection.sourceLocationId}|{connection.destinationLocationId}|{connection.lifecycleState}|{connection.openState}|{connection.lockState}|{connection.blockageState}|{connection.revision}");
            foreach (LocationConnectionEndpointData endpoint in data?.endpoints?.Where(item => item != null).OrderBy(item => item.endpointId, StringComparer.Ordinal) ?? Enumerable.Empty<LocationConnectionEndpointData>())
                Append(builder, "connection-endpoint", $"{endpoint.endpointId}|{endpoint.connectionId}|{endpoint.locationId}|{endpoint.role}|{endpoint.revision}");
        }

        private static void AppendRoutes(StringBuilder builder, LocationRouteRuntimeSaveData data)
        {
            Append(builder, "route-schema", data?.schemaVersion.ToString() ?? "missing");
            foreach (LocationRouteSegmentRecordData segment in data?.segments?.Where(item => item != null).OrderBy(item => item.segmentId, StringComparer.Ordinal) ?? Enumerable.Empty<LocationRouteSegmentRecordData>())
                Append(builder, "route-segment", $"{segment.segmentId}|{segment.segmentDefinitionId}|{segment.sourceLocationId}|{segment.destinationLocationId}|{segment.distanceMeters:R}|{segment.baseCostUnits:R}|{segment.lifecycleState}|{segment.blockageState}|{segment.revision}");
            foreach (LocationRouteNetworkRecordData network in data?.networks?.Where(item => item != null).OrderBy(item => item.networkId, StringComparer.Ordinal) ?? Enumerable.Empty<LocationRouteNetworkRecordData>())
                Append(builder, "route-network", $"{network.networkId}|{string.Join(",", network.segmentIds ?? Array.Empty<string>())}|{network.lifecycleState}|{network.revision}");
        }

        private static void AppendJourneys(StringBuilder builder, TravelJourneyRuntimeSaveData data)
        {
            Append(builder, "journey-schema", data?.schemaVersion.ToString() ?? "missing");
            foreach (TravelJourneyRecordData journey in data?.journeys?.Where(item => item != null).OrderBy(item => item.journeyId, StringComparer.Ordinal) ?? Enumerable.Empty<TravelJourneyRecordData>())
                Append(builder, "journey", $"{journey.journeyId}|{journey.traveler?.StableKey}|{journey.originLocationId}|{journey.destinationLocationId}|{journey.lifecycleState}|{journey.currentStepIndex}|{journey.completedDistanceMillimeters}|{journey.revision}");
            foreach (TravelJourneyStepRecordData step in data?.steps?.Where(item => item != null).OrderBy(item => item.journeyId, StringComparer.Ordinal).ThenBy(item => item.sequenceIndex).ThenBy(item => item.journeyStepId, StringComparer.Ordinal) ?? Enumerable.Empty<TravelJourneyStepRecordData>())
                Append(builder, "journey-step", $"{step.journeyStepId}|{step.journeyId}|{step.sequenceIndex}|{step.edgeId}|{step.edgeKind}|{step.lifecycleState}|{step.completedDistanceMillimeters}|{step.revision}");
        }

        private static void AppendConditions(StringBuilder builder, TravelConditionRuntimeSaveData data)
        {
            Append(builder, "condition-schema", data?.schemaVersion.ToString() ?? "missing");
            foreach (TravelConditionRecordData condition in data?.conditions?.Where(item => item != null).OrderBy(item => item.conditionId, StringComparer.Ordinal) ?? Enumerable.Empty<TravelConditionRecordData>())
                Append(builder, "condition", $"{condition.conditionId}|{condition.conditionDefinitionId}|{condition.target?.StableKey}|{condition.lifecycleState}|{condition.severity}|{condition.movementRateMultiplier:R}|{condition.routeCostMultiplier:R}|{condition.hardBlocksTravel}|{condition.revision}");
            foreach (TravelEncounterRecordData encounter in data?.encounters?.Where(item => item != null).OrderBy(item => item.encounterId, StringComparer.Ordinal) ?? Enumerable.Empty<TravelEncounterRecordData>())
                Append(builder, "encounter", $"{encounter.encounterId}|{encounter.sourceConditionId}|{encounter.journeyId}|{encounter.lifecycleState}|{encounter.resolution}|{encounter.revision}");
        }

        private static void AppendPoliticalTravel(StringBuilder builder, PoliticalTravelRuntimeSaveData data)
        {
            Append(builder, "political-schema", data?.schemaVersion.ToString() ?? "missing");
            foreach (BorderCheckpointRecordData checkpoint in data?.checkpoints?.Where(item => item != null).OrderBy(item => item.checkpointId, StringComparer.Ordinal) ?? Enumerable.Empty<BorderCheckpointRecordData>())
                Append(builder, "checkpoint", $"{checkpoint.checkpointId}|{checkpoint.routeSegmentId}|{checkpoint.locationId}|{checkpoint.sourceTerritoryId}|{checkpoint.destinationTerritoryId}|{checkpoint.policy}|{checkpoint.lifecycleState}|{checkpoint.revision}");
            foreach (TravelCrossingAuthorizationRecordData authorization in data?.authorizations?.Where(item => item != null).OrderBy(item => item.authorizationId, StringComparer.Ordinal) ?? Enumerable.Empty<TravelCrossingAuthorizationRecordData>())
                Append(builder, "authorization", $"{authorization.authorizationId}|{authorization.travelerPersonId}|{authorization.checkpointId}|{authorization.revoked}|{authorization.revision}");
            foreach (PoliticalTravelCrossingRecordData crossing in data?.crossings?.Where(item => item != null).OrderBy(item => item.crossingId, StringComparer.Ordinal) ?? Enumerable.Empty<PoliticalTravelCrossingRecordData>())
                Append(builder, "crossing", $"{crossing.crossingId}|{crossing.travelerPersonId}|{crossing.routeSegmentId}|{crossing.lifecycleState}|{crossing.combinedState}|{crossing.revision}");
        }

        private static void Append(StringBuilder builder, string label, string value)
        {
            builder.Append(label).Append('=').Append(value ?? string.Empty).Append('\n');
        }

        private static Step14IntegrationAuthorityEntry A(string domain, string owner = "", bool authoritative = false, bool persisted = false, bool derived = false, bool external = false, string notes = "")
        {
            return new Step14IntegrationAuthorityEntry(domain, owner, authoritative, persisted, derived, external, notes);
        }

        private static Step14IntegrationDependencyEntry D(string participantId, params string[] required)
        {
            return new Step14IntegrationDependencyEntry(participantId, required, Array.Empty<string>());
        }

        private static string N(string value, string fallback = "")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
