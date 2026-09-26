#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Crimes;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.Integration;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(14, "World Locations", 1400)]
    public static class PrototypeStep14AutomationSuites
    {
        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.14.1.world-location-identity-foundation",
                "World and Location Identity Foundation",
                "14.1",
                "Authoritative runtime location identity, definition separation, lifecycle, references, persistence, validation, and Test Lab coverage.",
                14010,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "LocationRuntime", "LocationDefinition", "LocationPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("60.1-readiness-and-definitions", "Location definitions and prototype records are available", 10, Step("step14-location-readiness", "Resolve location definitions and seeded records", ReadinessAndDefinitions)),
                    Scenario("60.2-definition-vs-instance", "Definitions remain separate from runtime instances", 20, Step("step14-location-definition-separation", "Create multiple instances from one definition", DefinitionVsInstance)),
                    Scenario("60.3-stable-identity", "Location IDs are stable through rename and scene binding changes", 30, Step("step14-location-stable-identity", "Rename without changing identity", StableIdentity)),
                    Scenario("60.4-lifecycle", "Lifecycle transitions are explicit and deterministic", 40, Step("step14-location-lifecycle", "Transition active to closed and historical", Lifecycle)),
                    Scenario("60.5-names-tags", "Names, aliases, and semantic tags are indexed deterministically", 50, Step("step14-location-names-tags", "Query by tag and definition", NamesTagsQueries)),
                    Scenario("60.6-association-references", "Property, organization, government, and territory associations stay as references", 60, Step("step14-location-associations", "Create association references without owning external records", Associations)),
                    Scenario("60.7-scene-independence", "Locations do not depend on GameObject or Transform identity", 70, Step("step14-location-scene-independence", "Save and restore scene binding keys only", SceneIndependence)),
                    Scenario("60.8-visibility-subject", "Location projections expose Step 8 subject references", 80, Step("step14-location-visibility-subject", "Create redacted-capable subject reference", VisibilitySubject)),
                    Scenario("60.9-preview-idempotence", "Preview and duplicate transactions do not mutate state", 90, Step("step14-location-preview-idempotence", "Preview create and duplicate commit", PreviewIdempotence)),
                    Scenario("60.10-revision-safety", "Revision conflicts reject stale writes", 100, Step("step14-location-revision", "Reject stale expected revision", RevisionSafety)),
                    Scenario("60.11-persistence-validation", "Persistence rejects corrupt graphs before commit", 110, Step("step14-location-persistence", "Save, restore, and reject invalid payload", PersistenceValidation)),
                    Scenario("60.12-fixture-snapshot", "Fixture snapshots restore location mutations", 120, Step("step14-location-fixture", "Snapshot and restore location state", FixtureSnapshot))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.14.2.location-hierarchy-containment-spatial-relationships",
                "Location Hierarchy, Containment, and Spatial Relationships",
                "14.2",
                "Runtime-authoritative location containment, spatial relationship records, deterministic traversal, persistence validation, and scene-independent projections.",
                14020,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "LocationRuntime", "LocationContainmentLink", "LocationSpatialRelationship" },
                scenarios: new[]
                {
                    Scenario("72.1-readiness-and-seeded-graph", "Seeded prototype containment graph is authoritative and valid", 10, Step("step14-location-hierarchy-readiness", "Resolve seeded hierarchy and validation", HierarchyReadiness)),
                    Scenario("72.2-parent-child-traversal", "Parent, child, ancestor, and descendant queries are deterministic", 20, Step("step14-location-hierarchy-traversal", "Query containment traversal", ParentChildTraversal)),
                    Scenario("72.3-cycle-prevention", "Containment cycles reject without mutation", 30, Step("step14-location-hierarchy-cycle", "Reject containment cycle", CyclePrevention)),
                    Scenario("72.4-active-parent-constraint", "Ordinary locations have only one active primary parent", 40, Step("step14-location-hierarchy-active-parent", "Reject second active parent", ActiveParentConstraint)),
                    Scenario("72.5-reparent-history", "Reparenting ends the previous link and preserves history", 50, Step("step14-location-hierarchy-reparent", "Atomic reparent and historical link", ReparentHistory)),
                    Scenario("72.6-spatial-directionality", "Spatial relationships resolve directional, inverse, and symmetric semantics", 60, Step("step14-location-spatial-directionality", "Evaluate spatial directionality", SpatialDirectionality)),
                    Scenario("72.7-spatial-no-routing", "Spatial relationships do not imply travel or occupancy state", 70, Step("step14-location-spatial-boundary", "Verify spatial boundary is descriptive only", SpatialNoRouting)),
                    Scenario("72.8-preview-idempotence", "Preview and duplicate hierarchy/spatial transactions do not mutate twice", 80, Step("step14-location-hierarchy-preview", "Preview and duplicate graph operations", HierarchyPreviewIdempotence)),
                    Scenario("72.9-persistence-round-trip", "Containment and spatial relationships persist deterministically", 90, Step("step14-location-hierarchy-persistence", "Save and restore graph records", HierarchyPersistence)),
                    Scenario("72.10-corrupt-restore-rejection", "Corrupt containment payloads reject before commit", 100, Step("step14-location-hierarchy-corrupt", "Reject corrupt graph restore", CorruptHierarchyRejection)),
                    Scenario("72.11-visibility-projections", "Hidden hierarchy links can be omitted from normal projections", 110, Step("step14-location-hierarchy-visibility", "Evaluate visibility-safe graph queries", VisibilityProjections)),
                    Scenario("72.12-fixture-snapshot", "Fixture snapshots restore location graph mutations", 120, Step("step14-location-hierarchy-fixture", "Snapshot and restore containment/spatial state", HierarchyFixtureSnapshot))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.14.3.entity-location-occupancy",
                "Entity Location and Occupancy",
                "14.3",
                "Authoritative exact entity placements, derived occupancy, Person/body physical resolution, relocation history, persistence validation, and inventory/world-placement boundaries.",
                14030,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "EntityLocationRuntime", "LocationRuntime", "LocationContainmentLink", "EntityLocationPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("73.1-readiness-and-seeded-placements", "Entity placement runtime is seeded and valid", 10, Step("step14-entity-location-readiness", "Resolve seeded entities and placements", EntityLocationReadiness)),
                    Scenario("73.2-single-active-exact-placement", "One entity cannot have two active exact placements", 20, Step("step14-entity-location-single-active", "Reject conflicting active placement", SingleActiveExactPlacement)),
                    Scenario("73.3-person-resolves-through-body", "Person physical location resolves through active body", 30, Step("step14-entity-location-person-body", "Resolve Person through body placement", PersonResolvesThroughBody)),
                    Scenario("73.4-direct-and-recursive-occupancy", "Direct occupancy is stored while recursive occupancy is derived", 40, Step("step14-entity-location-occupancy", "Query direct and recursive occupancy", DirectAndRecursiveOccupancy)),
                    Scenario("73.5-relocation-history-and-diff", "Relocation ends prior placement and reports hierarchy diff", 50, Step("step14-entity-location-relocate", "Relocate entity atomically", RelocationHistoryAndDiff)),
                    Scenario("73.6-unplacement-last-known", "Unplacement is explicit and last-known remains queryable", 60, Step("step14-entity-location-unplace", "End active placement", UnplacementLastKnown)),
                    Scenario("73.7-location-lifecycle-rejection", "Unavailable locations reject new ordinary placement", 70, Step("step14-entity-location-lifecycle", "Reject placement into closed location", LocationLifecycleRejection)),
                    Scenario("73.8-capacity-and-type-rules", "Capacity and occupant-type rules reject without mutation", 80, Step("step14-entity-location-capacity", "Evaluate capacity rules", CapacityAndTypeRules)),
                    Scenario("73.9-inventory-world-exclusion", "Inventory-held items cannot also occupy world locations", 90, Step("step14-entity-location-inventory", "Reject inventory/world overlap", InventoryWorldExclusion)),
                    Scenario("73.10-persistence-round-trip", "Entity placements persist and restore deterministically", 100, Step("step14-entity-location-persistence", "Save and restore placements", EntityLocationPersistenceRoundTrip)),
                    Scenario("73.11-corrupt-restore-rejection", "Corrupt placement payloads reject before commit", 110, Step("step14-entity-location-corrupt", "Reject corrupt placement restore", EntityLocationCorruptRestore)),
                    Scenario("73.12-fixture-snapshot", "Fixture snapshots restore entity placement mutations", 120, Step("step14-entity-location-fixture", "Snapshot and restore placement state", EntityLocationFixtureSnapshot))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.14.4.interaction-points-functional-locations",
                "Interaction Points and Functional Locations",
                "14.4",
                "Authoritative logical interaction points, service bindings, provider/presence eligibility, sessions, reservations, persistence, and scene-independent routing.",
                14040,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "InteractionPointRuntime", "LocationRuntime", "EntityLocationRuntime", "InteractionPointPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("74.1-readiness-and-definitions", "Interaction point definitions and seeded points are available", 10, Step("step14-interaction-readiness", "Resolve seeded interaction points", InteractionReadiness)),
                    Scenario("74.2-definition-vs-instance", "Point definitions remain separate from runtime records", 20, Step("step14-interaction-definition-separation", "Create multiple points from one definition", InteractionDefinitionVsInstance)),
                    Scenario("74.3-host-validation-and-reassignment", "Hosts validate against location categories and history is preserved", 30, Step("step14-interaction-hosts", "Reject invalid host and reassign valid host", InteractionHostValidation)),
                    Scenario("74.4-subject-provider-boundaries", "Subject links and providers remain references to owning systems", 40, Step("step14-interaction-subject-provider", "Add links and provider assignments", InteractionSubjectProviderBoundaries)),
                    Scenario("74.5-presence-eligibility", "Consumer and provider presence uses entity location authority", 50, Step("step14-interaction-presence", "Evaluate presence-based eligibility", InteractionPresenceEligibility)),
                    Scenario("74.6-capacity-reservation-session", "Capacity, reservations, and sessions are deterministic", 60, Step("step14-interaction-capacity", "Start exclusive use and reject overflow", InteractionCapacityReservationSession)),
                    Scenario("74.7-visibility-and-scene-independence", "Hidden points and scene bindings do not leak or require GameObjects", 70, Step("step14-interaction-scene-independence", "Query visibility and binding keys", InteractionVisibilitySceneIndependence)),
                    Scenario("74.8-destination-routing", "Invocation validates context without owning destination mutation", 80, Step("step14-interaction-routing", "Invoke validated service route", InteractionDestinationRouting)),
                    Scenario("74.9-persistence-validation", "Interaction point persistence round-trips and rejects corrupt graphs", 90, Step("step14-interaction-persistence", "Save, restore, and reject corrupt payload", InteractionPersistenceValidation)),
                    Scenario("74.10-fixture-snapshot", "Fixture snapshots restore interaction point mutations", 100, Step("step14-interaction-fixture", "Snapshot and restore interaction state", InteractionFixtureSnapshot))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.14.5.entrances-exits-connections-access",
                "Entrances, Exits, Connections, and Access",
                "14.5",
                "Scene-independent traversable location connections, endpoints, state gates, access policies, grants, traversal, persistence validation, and visibility-safe projections.",
                14050,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "LocationConnectionRuntime", "LocationRuntime", "EntityLocationRuntime", "LocationConnectionPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("75.1-readiness-and-seeded-connections", "Connection definitions and seeded prototype graph are available", 10, Step("step14-connection-readiness", "Resolve seeded connections and access policies", ConnectionReadiness)),
                    Scenario("75.2-definition-and-adjacency-boundaries", "Connection records stay separate from definitions and spatial adjacency", 20, Step("step14-connection-boundaries", "Verify definition/runtime and adjacency boundaries", ConnectionDefinitionAdjacencyBoundaries)),
                    Scenario("75.3-state-gated-traversal", "Open, lock, blockage, and lifecycle states gate traversal atomically", 30, Step("step14-connection-state-gates", "Evaluate state gates before traversal", ConnectionStateGates)),
                    Scenario("75.4-access-policy-matrix", "Access policies consume external authority references without owning them", 40, Step("step14-connection-access-matrix", "Evaluate representative access inputs", ConnectionAccessPolicyMatrix)),
                    Scenario("75.5-hidden-one-way-and-grants", "Hidden, one-way, and explicit grants are authoritative but projection-safe", 50, Step("step14-connection-visibility-grants", "Evaluate visibility, directionality, and grants", ConnectionVisibilityOneWayGrants)),
                    Scenario("75.6-persistence-validation", "Connection persistence round-trips and rejects corrupt graphs", 60, Step("step14-connection-persistence", "Save, restore, and reject corrupt connection payload", ConnectionPersistenceValidation)),
                    Scenario("75.7-fixture-snapshot", "Fixture snapshots restore connection mutations", 70, Step("step14-connection-fixture", "Snapshot and restore connection state", ConnectionFixtureSnapshot))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.14.6.routes-distance-travel-networks",
                "Routes, Distance, and Travel Networks",
                "14.6",
                "Authoritative route segments and networks layered over location connections, deterministic route planning, access-aware traversal, knowledge-safe projections, persistence validation, and fixture ownership.",
                14060,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "LocationRouteRuntime", "LocationConnectionRuntime", "LocationRuntime", "LocationRoutePersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("76.1-readiness-and-seeded-routes", "Route definitions and seeded graph are available", 10, Step("step14-route-readiness", "Resolve seeded route graph", RouteReadiness)),
                    Scenario("76.2-multi-edge-planning", "Route plans can compose route segments and connection edges", 20, Step("step14-route-multi-edge", "Plan across routes and local connections", RouteMultiEdgePlanning)),
                    Scenario("76.3-objectives-and-tie-breaks", "Planning objectives are deterministic across parallel edges", 30, Step("step14-route-objectives", "Compare shortest and lowest-cost planning", RouteObjectivesTieBreaks)),
                    Scenario("76.4-access-and-unlockable-edges", "Connection access gates participate without mutating connection state", 40, Step("step14-route-access", "Evaluate current and unlockable access", RouteAccessAndUnlockableEdges)),
                    Scenario("76.5-knowledge-safe-hidden-routes", "Knowledge-safe planning filters hidden routes without leaking counts", 50, Step("step14-route-knowledge", "Filter hidden route edges", RouteKnowledgeSafeHiddenRoutes)),
                    Scenario("76.6-stale-plan-revalidation", "Route plans are immutable and revalidate against graph revisions", 60, Step("step14-route-revalidation", "Detect changed route graph", RouteStalePlanRevalidation)),
                    Scenario("76.7-persistence-validation", "Route persistence round-trips and rejects corrupt route graphs", 70, Step("step14-route-persistence", "Save, restore, and reject corrupt route payload", RoutePersistenceValidation)),
                    Scenario("76.8-fixture-snapshot", "Fixture snapshots restore route mutations", 80, Step("step14-route-fixture", "Snapshot and restore route state", RouteFixtureSnapshot))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.14.7.travel-planning-journey-state",
                "Travel Planning and Journey State",
                "14.7",
                "Authoritative journey records created from accepted route plans, deterministic world-time progress, local connection traversal, lifecycle controls, replanning, persistence validation, and fixture ownership.",
                14070,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "TravelJourneyRuntime", "LocationRouteRuntime", "EntityLocationRuntime", "TravelJourneyPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("77.1-readiness-and-create", "Journey runtime creates ready journeys from accepted route plans", 10, Step("step14-journey-create", "Create journey from accepted route plan", JourneyReadinessAndCreate)),
                    Scenario("77.2-start-no-teleport", "Starting a journey does not mutate exact placement", 20, Step("step14-journey-start", "Start journey while remaining at origin", JourneyStartDoesNotTeleport)),
                    Scenario("77.3-deterministic-progress", "Route-segment progress is deterministic by world time", 30, Step("step14-journey-progress", "Advance route segment and arrive deterministically", JourneyDeterministicProgress)),
                    Scenario("77.4-local-connection-step", "Local connection steps use connection traversal authority", 40, Step("step14-journey-connection", "Advance through route and local connection", JourneyLocalConnectionStep)),
                    Scenario("77.5-pause-resume-cancel", "Pause, resume, and cancel preserve placement and lifecycle history", 50, Step("step14-journey-lifecycle", "Exercise pause, resume, and cancel", JourneyPauseResumeCancel)),
                    Scenario("77.6-block-and-replan", "Stale blocked routes block travel and can replan from current placement", 60, Step("step14-journey-replan", "Block stale route and replan", JourneyBlockAndReplan)),
                    Scenario("77.7-projection-boundaries", "Journey projections expose in-transit context without leaking hidden details", 70, Step("step14-journey-projection", "Evaluate physical and redacted projections", JourneyProjectionBoundaries)),
                    Scenario("77.8-persistence-validation", "Journey persistence round-trips and rejects corrupt graphs before commit", 80, Step("step14-journey-persistence", "Save, restore, and reject corrupt journey payload", JourneyPersistenceValidation)),
                    Scenario("77.9-fixture-snapshot", "Fixture snapshots restore journey mutations", 90, Step("step14-journey-fixture", "Snapshot and restore journey state", JourneyFixtureSnapshot))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.14.8.travel-conditions-restrictions-encounters",
                "Travel Conditions, Restrictions, and Encounters",
                "14.8",
                "Definition-backed travel conditions, hard blockers, requirements, hidden risks, explicit hazards, encounter interruption, persistence, and route/journey integration.",
                14080,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "TravelConditionRuntime", "LocationRouteRuntime", "TravelJourneyRuntime", "TravelConditionPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("78.1-readiness-and-definitions", "Condition, hazard, and encounter definitions are available", 10, Step("step14-travel-condition-readiness", "Resolve prototype travel condition definitions", TravelConditionReadiness)),
                    Scenario("78.2-route-condition-modifiers", "Route planning applies movement and cost modifiers", 20, Step("step14-travel-condition-route-modifier", "Plan route with muddy-road modifier", TravelConditionRouteModifier)),
                    Scenario("78.3-hard-block-and-revalidation", "Hard travel blockers invalidate accepted route plans", 30, Step("step14-travel-condition-hard-block", "Block route and revalidate plan", TravelConditionHardBlockRevalidation)),
                    Scenario("78.4-requirements-without-mutation", "Missing capability and equipment requirements block without mutation", 40, Step("step14-travel-condition-requirements", "Evaluate requirement condition", TravelConditionRequirements)),
                    Scenario("78.5-hidden-risk-knowledge-safety", "Hidden condition and encounter risk do not leak under knowledge-safe queries", 50, Step("step14-travel-condition-hidden", "Evaluate hidden ambush safely", TravelConditionHiddenKnowledgeSafety)),
                    Scenario("78.6-journey-slowdown", "Journey progress uses condition-adjusted movement rate", 60, Step("step14-travel-condition-journey-slowdown", "Advance journey through slowdown", TravelConditionJourneySlowdown)),
                    Scenario("78.7-encounter-interruption", "Checkpoint encounters interrupt journeys without creating combat state", 70, Step("step14-travel-condition-encounter", "Trigger encounter at checkpoint", TravelConditionEncounterInterruption)),
                    Scenario("78.8-hazard-and-persistence", "Explicit hazards and persistence preserve state without retriggering", 80, Step("step14-travel-condition-persistence", "Trigger hazard, save, restore, reject corrupt payload", TravelConditionHazardPersistence))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.14.9.territory-jurisdiction-borders-world-state-travel",
                "Territory, Jurisdiction, Borders, and World-State Travel Integration",
                "14.9",
                "Political travel evaluation integrates Step 14 routes with Step 13 territory, jurisdiction, law, crime, warrants, checkpoint authorization, persistence, and fixture ownership.",
                14090,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "PoliticalTravelRuntime", "GovernmentRuntime", "LegalRuntime", "CrimeRuntime", "JusticeRuntime", "PoliticalTravelPersistenceParticipant" },
                scenarios: new[]
                {
                    Scenario("79.1-readiness-and-ownership", "Political travel runtime is fixture-owned and delegates to Step 13 authorities", 10, Step("step14-political-travel-readiness", "Resolve political travel runtime and owner revisions", PoliticalTravelReadinessOwnership)),
                    Scenario("79.2-territory-jurisdiction-evaluation", "Territory and jurisdiction resolve from authoritative government records", 20, Step("step14-political-travel-territory", "Evaluate a cross-territory route", PoliticalTravelTerritoryJurisdictionEvaluation)),
                    Scenario("79.3-legal-compliance-modes", "Legal compliance modes do not replace physical traversability", 30, Step("step14-political-travel-compliance", "Compare legal block, illegal crossing, and physical block", PoliticalTravelLegalComplianceModes)),
                    Scenario("79.4-checkpoint-authorization", "Checkpoint authorization gates border crossing without mutating routes or laws", 40, Step("step14-political-travel-checkpoint", "Require and grant checkpoint authorization", PoliticalTravelCheckpointAuthorization)),
                    Scenario("79.5-wanted-warrant-visibility", "Wanted and warrant summaries respect political visibility", 50, Step("step14-political-travel-wanted", "Evaluate restricted enforcement visibility", PoliticalTravelWantedVisibility)),
                    Scenario("79.6-route-requirements", "Route planning exposes political requirements without owning government state", 60, Step("step14-political-travel-route-requirements", "Build route requirement summary", PoliticalTravelRouteRequirements)),
                    Scenario("79.7-persistence-and-fixture", "Political travel persistence validates graph references and fixture snapshots restore state", 70, Step("step14-political-travel-persistence", "Save, restore, reject corrupt payload, and restore fixture", PoliticalTravelPersistenceFixture))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.14.10.world-location-persistence-historical-movement",
                "World/Location Persistence and Historical Movement",
                "14.10",
                "Step 14 persistence ownership, historical movement projections, temporal queries, visibility boundaries, and reconstruction validation are consolidated without introducing a second movement owner.",
                14100,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "Step14PersistenceManifest", "MovementHistoryService", "LocationRuntime", "EntityLocationRuntime", "LocationRouteRuntime", "TravelJourneyRuntime", "TravelConditionRuntime", "PoliticalTravelRuntime" },
                scenarios: new[]
                {
                    Scenario("80.1-ownership-manifest", "Persistence manifest declares one authoritative owner per state category", 10, Step("step14-persistence-manifest", "Build and validate Step 14 persistence manifest", PersistenceManifestOwnership)),
                    Scenario("80.2-exact-location-at-time", "Historical exact-location queries separate placement from in-transit state", 20, Step("step14-history-exact-location", "Resolve exact location and active journey context", HistoricalExactLocation)),
                    Scenario("80.3-path-occupancy-visits", "Historical containment, occupancy, visits, and distance are derived deterministically", 30, Step("step14-history-path-occupancy", "Query containment path, occupancy, visits, and distance", HistoricalPathOccupancyVisits)),
                    Scenario("80.4-timeline-visibility", "Movement timelines preserve source references without leaking hidden details", 40, Step("step14-history-timeline-visibility", "Evaluate visibility-safe timeline ranges", HistoricalTimelineVisibility)),
                    Scenario("80.5-validation-and-snapshot", "Movement history validates source graphs and remains immutable after runtime mutation", 50, Step("step14-history-validation-snapshot", "Validate and snapshot movement history projections", HistoricalValidationSnapshot))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                "feature.14.11.scene-binding-prototype-world-integration",
                "Scene Binding and Prototype World Integration",
                "14.11",
                "Transient Unity scene bindings map prototype GameObjects to authoritative Step 14 locations, interaction points, connections, routes, checkpoints, and entity placements without making scene objects authoritative.",
                14110,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "WorldSceneBindingRuntime", "LocationRuntime", "EntityLocationRuntime", "InteractionPointRuntime", "LocationConnectionRuntime", "LocationRouteRuntime", "PoliticalTravelRuntime" },
                scenarios: new[]
                {
                    Scenario("81.1-binding-readiness", "Scene bindings resolve authoritative records and report duplicates deterministically", 10, Step("step14-scene-binding-readiness", "Register representative scene bindings", SceneBindingReadiness)),
                    Scenario("81.2-interaction-and-connections", "Scene interaction and door bindings route through owning runtimes", 20, Step("step14-scene-binding-interaction-connection", "Use interaction and connection bindings", SceneBindingInteractionAndConnection)),
                    Scenario("81.3-entity-materialization", "Scene entities materialize from authoritative placements without writing Transform drift", 30, Step("step14-scene-binding-entity-materialize", "Materialize entity from runtime placement", SceneBindingEntityMaterialization)),
                    Scenario("81.4-route-checkpoint-transient", "Route and checkpoint scene markers stay transient presentation mappings", 40, Step("step14-scene-binding-route-checkpoint", "Bind route and checkpoint markers", SceneBindingRouteCheckpointTransient))
                }), out _);

            registry?.TryRegister(new TestLabAutomationSuite(
                Step14WorldIntegrationValidator.SuiteId,
                "Step 14 World, Location, and Travel Integration Finalization",
                "14.12",
                "Aggregate Step 14 readiness, ownership, persistence, scene binding boundaries, deterministic fingerprints, and Step 15 handoff contracts without introducing a second world-state owner.",
                14120,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "Step14WorldIntegrationValidator", "Step14PersistenceManifest", "MovementHistoryService", "WorldSceneBindingRuntime" },
                scenarios: new[]
                {
                    Scenario("82.1-readiness-and-ownership", "Integrated Step 14 readiness and authority ownership are clean", 10, Step("step14-integration-readiness", "Validate aggregate readiness and authority map", IntegrationReadinessAndOwnership)),
                    Scenario("82.2-concept-separation", "Logical world state remains separate from scene, visibility, and external legal authority", 20, Step("step14-integration-concept-separation", "Validate ownership boundaries", IntegrationConceptSeparation)),
                    Scenario("82.3-deterministic-fingerprint", "Integrated save graph fingerprint is deterministic and non-mutating", 30, Step("step14-integration-fingerprint", "Compare deterministic integration fingerprints", IntegrationDeterministicFingerprint)),
                    Scenario("82.4-step15-contract", "Step 15 receives explicit read and command contracts", 40, Step("step14-integration-step15", "Validate Step 15 handoff contract", IntegrationStep15Contract)),
                    Scenario("82.5-corrupt-graph-rejection", "Aggregate validation rejects corrupt cross-runtime state before consumers use it", 50, Step("step14-integration-corrupt", "Reject corrupted world-scope and placement graph", IntegrationCorruptGraphRejection))
                }), out _);
        }

        private static ITestLabAutomationScenario Scenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.WorldLocations,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[]
                {
                    PrototypeLocationDefinitionFactory.SettlementDefinitionId,
                    PrototypeLocationDefinitionFactory.GuildHallDefinitionId,
                    PrototypeLocationDefinitionFactory.OfficeDefinitionId,
                    PrototypeLocationDefinitionFactory.DungeonDefinitionId
                });
        }

        private static ITestLabScenarioStep Step(string id, string displayName, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            return new TestLabScenarioStep(id, displayName, action);
        }

        private static TestLabAutomationStepResult ReadinessAndDefinitions(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool hasSettlement = registry.TryGet(PrototypeLocationDefinitionFactory.SettlementDefinitionId, out LocationDefinition settlement);
            bool hasGuildHall = registry.TryGet(PrototypeLocationDefinitionFactory.GuildHallDefinitionId, out LocationDefinition guildHall);
            bool hasDetention = registry.TryGet(PrototypeLocationDefinitionFactory.DetentionAreaDefinitionId, out LocationDefinition detention);
            bool definitions = hasSettlement && hasGuildHall && hasDetention;
            bool hasVillage = runtime.TryGetSnapshot("location.prototype.village", out LocationSnapshot village);
            bool hasGuild = runtime.TryGetSnapshot("location.prototype.adventurers-guild", out LocationSnapshot guild);
            bool seeded = hasVillage && hasGuild;
            LocationValidationReport report = runtime.ValidateRuntime();
            bool valid = definitions && seeded && report.Succeeded && settlement.Category == LocationCategory.Settlement && guildHall.SupportsOrganizationAssociation && detention.SupportsVisibility(LocationVisibility.Restricted) && village.WorldId == context.ScenarioContext.Runtimes.WorldId && guild.AssociatedOrganizationId == "organization.prototype.adventurers-guild";
            return TestLabAssertions.True("step14-location-readiness", "Location definitions and seeded records resolve", valid, $"Definitions={definitions} Seeded={seeded} Validation={report.Summary}");
        }

        private static TestLabAutomationStepResult DefinitionVsInstance(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult first = Create(context, "market-a", PrototypeLocationDefinitionFactory.MarketStallDefinitionId, "Market Stall A");
            LocationOperationResult second = Create(context, "market-b", PrototypeLocationDefinitionFactory.MarketStallDefinitionId, "Market Stall B");
            bool valid = first.Succeeded && second.Succeeded && first.Snapshot.LocationId != second.Snapshot.LocationId && first.Snapshot.LocationDefinitionId == second.Snapshot.LocationDefinitionId && runtime.QueryByDefinition(PrototypeLocationDefinitionFactory.MarketStallDefinitionId).Count >= 2;
            return TestLabAssertions.True("step14-location-definition-separation", "One definition can back distinct runtime locations", valid, $"First={first.Status} Second={second.Status} Count={runtime.QueryByDefinition(PrototypeLocationDefinitionFactory.MarketStallDefinitionId).Count}");
        }

        private static TestLabAutomationStepResult StableIdentity(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult create = Create(context, "stable-create", PrototypeLocationDefinitionFactory.GuildHallDefinitionId, "Old Guild Hall", binding: "scene.old");
            LocationOperationResult rename = runtime.RenameLocation(new LocationRenameRequest { transactionId = Tx(context, "stable-rename"), locationId = create.Snapshot?.LocationId, newName = "Renamed Guild Hall", category = LocationNameCategory.Official, effectiveWorldTime = 10d });
            bool valid = create.Succeeded && rename.Succeeded && rename.Snapshot.LocationId == create.Snapshot.LocationId && rename.Snapshot.LocationDefinitionId == create.Snapshot.LocationDefinitionId && rename.Snapshot.PrototypeSceneBindingKey == "scene.old";
            return TestLabAssertions.True("step14-location-stable-identity", "Rename preserves runtime location identity", valid, $"Create={create.Status} Rename={rename.Status} Id={rename.Snapshot?.LocationId}");
        }

        private static TestLabAutomationStepResult Lifecycle(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult create = Create(context, "lifecycle", PrototypeLocationDefinitionFactory.OfficeDefinitionId, "Lifecycle Office");
            LocationOperationResult close = runtime.TransitionLifecycle(new LocationLifecycleTransitionRequest { transactionId = Tx(context, "close"), locationId = create.Snapshot?.LocationId, targetState = LocationLifecycleState.Closed, worldTime = 12d });
            LocationOperationResult historical = runtime.TransitionLifecycle(new LocationLifecycleTransitionRequest { transactionId = Tx(context, "historical"), locationId = create.Snapshot?.LocationId, targetState = LocationLifecycleState.Historical, worldTime = 20d });
            LocationReferenceResolutionResult resolution = runtime.ResolveReference(create.Snapshot?.ToReference());
            bool valid = create.Succeeded && close.Succeeded && historical.Succeeded && historical.Snapshot.LifecycleState == LocationLifecycleState.Historical && resolution.Succeeded;
            return TestLabAssertions.True("step14-location-lifecycle", "Lifecycle transitions keep stable references", valid, $"Create={create.Status} Close={close.Status} Historical={historical.Status} Resolve={resolution.Status}");
        }

        private static TestLabAutomationStepResult NamesTagsQueries(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult create = Create(context, "tags", PrototypeLocationDefinitionFactory.WildernessDefinitionId, "Northern Ring", tags: new[] { "wilderness", "outdoor" });
            LocationOperationResult alias = runtime.RenameLocation(new LocationRenameRequest { transactionId = Tx(context, "alias"), locationId = create.Snapshot?.LocationId, newName = "Old North Ring", category = LocationNameCategory.Alias, effectiveWorldTime = 15d });
            bool valid = create.Succeeded && alias.Succeeded && alias.Snapshot.Aliases.Contains("Old North Ring") && runtime.QueryByTag("wilderness").Any(item => item.LocationId == create.Snapshot.LocationId) && runtime.QueryByCategory(LocationCategory.Wilderness).Any(item => item.LocationId == create.Snapshot.LocationId);
            return TestLabAssertions.True("step14-location-names-tags", "Names, aliases, tags, and indexes are deterministic", valid, $"Create={create.Status} Alias={alias.Status} Wilderness={runtime.QueryByTag("wilderness").Count}");
        }

        private static TestLabAutomationStepResult Associations(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult create = runtime.CreateLocation(new LocationCreateRequest
            {
                transactionId = Tx(context, "assoc"),
                locationId = Id(context, "assoc"),
                locationDefinitionId = PrototypeLocationDefinitionFactory.GovernmentBuildingDefinitionId,
                officialName = "Associated Civic Building",
                associatedPropertyId = "property.prototype.guild-building",
                associatedOrganizationId = "organization.prototype.government",
                associatedGovernmentId = "government.prototype.civic",
                associatedTerritoryIds = new[] { "territory.prototype.village" },
                associations = new[] { new LocationAssociationReferenceData { kind = LocationAssociationKind.Provenance, referenceId = $"location-record.charter.{context.RunId}", worldId = context.ScenarioContext.Runtimes.WorldId } },
                semanticTagIds = new[] { "government", "building", "civic" }
            });
            bool valid = create.Succeeded && create.Snapshot.AssociatedPropertyId == "property.prototype.guild-building" && create.Snapshot.AssociatedOrganizationId == "organization.prototype.government" && create.Snapshot.AssociatedGovernmentId == "government.prototype.civic" && create.Snapshot.AssociatedTerritoryIds.Contains("territory.prototype.village") && create.Snapshot.Associations.Count == 1;
            return TestLabAssertions.True("step14-location-associations", "External systems remain references, not owned records", valid, $"Create={create.Status} Property={create.Snapshot?.AssociatedPropertyId} Org={create.Snapshot?.AssociatedOrganizationId}");
        }

        private static TestLabAutomationStepResult SceneIndependence(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult create = Create(context, "scene-free", PrototypeLocationDefinitionFactory.DungeonDefinitionId, "Scene Free Dungeon", visibility: LocationVisibility.Secret, binding: "prototype.marker.dungeon");
            LocationRuntimeSaveData save = runtime.CreateSaveData();
            LocationRuntime restored = new LocationRuntime();
            restored.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.WorldId);
            LocationOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.WorldId);
            bool found = restored.TryGetSnapshot(create.Snapshot?.LocationId, out LocationSnapshot snapshot);
            bool valid = create.Succeeded && restore.Succeeded && found && snapshot.PrototypeSceneBindingKey == "prototype.marker.dungeon" && snapshot.LocationId == create.Snapshot.LocationId;
            return TestLabAssertions.True("step14-location-scene-independence", "Save data carries binding keys without depending on scene objects", valid, $"Create={create.Status} Restore={restore.Status} Found={found}");
        }

        private static TestLabAutomationStepResult VisibilitySubject(TestLabAutomationContext context)
        {
            LocationOperationResult create = Create(context, "hidden-subject", PrototypeLocationDefinitionFactory.DungeonDefinitionId, "Hidden Dungeon", visibility: LocationVisibility.Hidden);
            bool valid = create.Succeeded && create.Snapshot.Visibility == LocationVisibility.Hidden && create.Snapshot.ToInformationSubject().subjectType == UnityIsekaiGame.Knowledge.Access.InformationSubjectType.Location && create.Snapshot.ToInformationSubject().subjectId == create.Snapshot.LocationId;
            return TestLabAssertions.True("step14-location-visibility-subject", "Location snapshots expose stable Step 8 subject references", valid, $"Create={create.Status} Subject={create.Snapshot?.ToInformationSubject().subjectId}");
        }

        private static TestLabAutomationStepResult PreviewIdempotence(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            long before = runtime.Revision;
            LocationOperationResult preview = Create(context, "preview", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Preview Room", preview: true);
            bool absentAfterPreview = !runtime.TryGetSnapshot(Id(context, "preview"), out _);
            LocationOperationResult execute = Create(context, "preview", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Preview Room");
            LocationOperationResult duplicate = Create(context, "preview", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Preview Room");
            bool valid = preview.Preview && absentAfterPreview && execute.Succeeded && duplicate.Duplicate && runtime.Revision == before + 1L;
            return TestLabAssertions.True("step14-location-preview-idempotence", "Preview and duplicate create do not mutate more than once", valid, $"Preview={preview.Status} Execute={execute.Status} Duplicate={duplicate.Status} Revision={before}->{runtime.Revision}");
        }

        private static TestLabAutomationStepResult RevisionSafety(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult create = Create(context, "revision", PrototypeLocationDefinitionFactory.OfficeDefinitionId, "Revision Office");
            LocationOperationResult stale = runtime.RenameLocation(new LocationRenameRequest { transactionId = Tx(context, "stale"), locationId = create.Snapshot?.LocationId, newName = "Stale Office", expectedRevision = 0L });
            bool valid = create.Succeeded && !stale.Succeeded && stale.Status == LocationOperationStatus.RevisionConflict && runtime.TryGetSnapshot(create.Snapshot.LocationId, out LocationSnapshot snapshot) && snapshot.OfficialName == "Revision Office";
            return TestLabAssertions.True("step14-location-revision", "Stale expected revisions reject without mutation", valid, $"Create={create.Status} Stale={stale.Status}");
        }

        private static TestLabAutomationStepResult PersistenceValidation(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            Create(context, "persist", PrototypeLocationDefinitionFactory.GuildHallDefinitionId, "Persistent Guild Hall", organizationId: "organization.prototype.adventurers-guild");
            LocationPersistenceParticipant participant = new LocationPersistenceParticipant(runtime, () => context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.WorldId);
            var save = participant.CapturePayload();
            var prepared = participant.PreparePayload(save.PayloadJson, LocationPersistenceParticipant.CurrentParticipantSchemaVersion);
            LocationRuntimeSaveData corrupt = runtime.CreateSaveData();
            corrupt.records[0].locationDefinitionId = "location-definition.missing";
            var rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), LocationPersistenceParticipant.CurrentParticipantSchemaVersion);
            bool unchanged = runtime.Count >= PrototypeLocationDefinitionFactory.PrototypeLocationIds.Length + 1;
            bool valid = save.Succeeded && prepared.Succeeded && rejected != null && !rejected.Succeeded && unchanged;
            return TestLabAssertions.True("step14-location-persistence", "Persistence validates before commit and rejects corrupt graphs", valid, $"Save={save.Succeeded} Prepare={prepared.Succeeded} Rejected={rejected?.Succeeded == false} Count={runtime.Count}");
        }

        private static TestLabAutomationStepResult FixtureSnapshot(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            TestLabRuntimeBundleSnapshot snapshot = context.ScenarioContext.Runtimes.CreateSnapshot();
            int before = runtime.Count;
            Create(context, "fixture", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Fixture Room");
            bool restored = context.ScenarioContext.Runtimes.RestoreSnapshot(snapshot, out string failure);
            bool missing = !runtime.TryGetSnapshot(Id(context, "fixture"), out _);
            bool valid = restored && missing && runtime.Count == before;
            return TestLabAssertions.True("step14-location-fixture", "Fixture snapshot restore removes undeclared location mutations", valid, $"Restored={restored} Missing={missing} Count={runtime.Count}/{before} Failure={failure}");
        }

        private static TestLabAutomationStepResult HierarchyReadiness(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationValidationReport report = runtime.ValidateRuntime();
            LocationContainmentSnapshot villageParent = runtime.GetActiveParentLink("location.prototype.village");
            bool spatial = runtime.AreSpatiallyRelated("location.prototype.market-district", "location.prototype.adventurers-guild", LocationSpatialRelationshipKind.Near);
            bool valid = report.Succeeded
                && villageParent != null
                && villageParent.ParentLocationId == "location.prototype.region"
                && runtime.GetRoots().Any(root => root.LocationId == "location.prototype.world")
                && runtime.ContainmentLinkCount >= 10
                && runtime.SpatialRelationshipCount >= 4
                && spatial;
            return TestLabAssertions.True("step14-location-hierarchy-readiness", "Seeded location graph is valid and queryable", valid, $"Validation={report.Summary} Links={runtime.ContainmentLinkCount} Spatial={runtime.SpatialRelationshipCount} VillageParent={villageParent?.ParentLocationId}");
        }

        private static TestLabAutomationStepResult ParentChildTraversal(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            IReadOnlyList<LocationSnapshot> path = runtime.GetHierarchyPath("location.prototype.guildmaster-office").Path;
            IReadOnlyList<LocationSnapshot> children = runtime.GetChildren("location.prototype.village");
            IReadOnlyList<LocationSnapshot> descendants = runtime.GetDescendants("location.prototype.village");
            bool deterministic = descendants.Select(item => item.LocationId).SequenceEqual(descendants.Select(item => item.LocationId).OrderBy(id => id, StringComparer.Ordinal));
            bool valid = path.Select(item => item.LocationId).SequenceEqual(new[] { "location.prototype.world", "location.prototype.region", "location.prototype.village", "location.prototype.adventurers-guild", "location.prototype.guildmaster-office" })
                && children.Any(item => item.LocationId == "location.prototype.adventurers-guild")
                && descendants.Any(item => item.LocationId == "location.prototype.basement-prison")
                && deterministic;
            return TestLabAssertions.True("step14-location-hierarchy-traversal", "Hierarchy traversal returns deterministic paths and descendants", valid, $"Path={string.Join("/", path.Select(item => item.LocationId))} Children={children.Count} Descendants={descendants.Count}");
        }

        private static TestLabAutomationStepResult CyclePrevention(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult parent = Create(context, "cycle-parent", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Cycle Parent Room", tags: new[] { "room", "interior" });
            LocationOperationResult child = Create(context, "cycle-child", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Cycle Child Room", tags: new[] { "room", "interior" });
            LocationOperationResult initial = runtime.AssignContainment(new LocationContainmentRequest
            {
                transactionId = Tx(context, "cycle-initial"),
                linkId = context.ScenarioContext.ScopedId("location-containment.test", "cycle-initial"),
                parentLocationId = parent.Snapshot?.LocationId,
                childLocationId = child.Snapshot?.LocationId,
                kind = LocationContainmentKind.Interior,
                effectiveWorldTime = 70d
            });
            long before = runtime.Revision;
            LocationOperationResult cycle = runtime.AssignContainment(new LocationContainmentRequest
            {
                transactionId = Tx(context, "cycle"),
                linkId = context.ScenarioContext.ScopedId("location-containment.test", "cycle"),
                parentLocationId = child.Snapshot?.LocationId,
                childLocationId = parent.Snapshot?.LocationId,
                kind = LocationContainmentKind.Interior,
                effectiveWorldTime = 71d
            });
            bool valid = parent.Succeeded && child.Succeeded && initial.Succeeded && cycle.Status == LocationOperationStatus.CycleDetected && runtime.Revision == before && runtime.GetActiveParentLink(parent.Snapshot.LocationId) == null;
            return TestLabAssertions.True("step14-location-hierarchy-cycle", "Cycle creation rejects without mutation", valid, $"Parent={parent.Status} Child={child.Status} Initial={initial.Status} Cycle={cycle.Status} Revision={before}->{runtime.Revision} Message={cycle.Message}");
        }

        private static TestLabAutomationStepResult ActiveParentConstraint(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult second = runtime.AssignContainment(new LocationContainmentRequest
            {
                transactionId = Tx(context, "active-parent"),
                linkId = context.ScenarioContext.ScopedId("location-containment.test", "second-parent"),
                parentLocationId = "location.prototype.market-district",
                childLocationId = "location.prototype.adventurers-guild",
                effectiveWorldTime = 75d
            });
            bool valid = second.Status == LocationOperationStatus.ActiveParentConflict && runtime.GetActiveParentLink("location.prototype.adventurers-guild").ParentLocationId == "location.prototype.village";
            return TestLabAssertions.True("step14-location-hierarchy-active-parent", "Second active primary parent rejects", valid, $"Status={second.Status} Parent={runtime.GetActiveParentLink("location.prototype.adventurers-guild")?.ParentLocationId}");
        }

        private static TestLabAutomationStepResult ReparentHistory(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult room = Create(context, "reparent-room", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Reparent Room", tags: new[] { "room", "interior" });
            LocationOperationResult initial = runtime.AssignContainment(new LocationContainmentRequest
            {
                transactionId = Tx(context, "reparent-initial"),
                linkId = context.ScenarioContext.ScopedId("location-containment.test", "reparent-initial"),
                parentLocationId = "location.prototype.adventurers-guild",
                childLocationId = room.Snapshot?.LocationId,
                kind = LocationContainmentKind.Interior,
                effectiveWorldTime = 80d
            });
            LocationOperationResult reparent = runtime.ReparentLocation(new LocationReparentRequest
            {
                transactionId = Tx(context, "reparent-new"),
                oldParentLocationId = "location.prototype.adventurers-guild",
                newParentLocationId = "location.prototype.civic-office",
                childLocationId = room.Snapshot?.LocationId,
                newLinkId = context.ScenarioContext.ScopedId("location-containment.test", "reparent-new"),
                kind = LocationContainmentKind.Interior,
                effectiveWorldTime = 90d
            });
            bool oldEnded = runtime.ContainmentLinks.Any(link => link.ChildLocationId == room.Snapshot.LocationId && link.ParentLocationId == "location.prototype.adventurers-guild" && link.State == LocationLinkState.Ended);
            bool valid = room.Succeeded && initial.Succeeded && reparent.Succeeded && oldEnded && runtime.GetActiveParentLink(room.Snapshot.LocationId).ParentLocationId == "location.prototype.civic-office";
            return TestLabAssertions.True("step14-location-hierarchy-reparent", "Reparenting preserves ended link history and active parent", valid, $"Room={room.Status} Initial={initial.Status} Reparent={reparent.Status} OldEnded={oldEnded}");
        }

        private static TestLabAutomationStepResult SpatialDirectionality(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult directional = runtime.CreateSpatialRelationship(new LocationSpatialRelationshipRequest
            {
                transactionId = Tx(context, "spatial-directional"),
                relationshipId = context.ScenarioContext.ScopedId("location-spatial.test", "above"),
                sourceLocationId = "location.prototype.guildmaster-office",
                targetLocationId = "location.prototype.basement-prison",
                kind = LocationSpatialRelationshipKind.Above,
                directionality = LocationSpatialDirectionality.Directional
            });
            bool above = runtime.AreSpatiallyRelated("location.prototype.guildmaster-office", "location.prototype.basement-prison", LocationSpatialRelationshipKind.Above);
            bool inverse = runtime.AreSpatiallyRelated("location.prototype.basement-prison", "location.prototype.guildmaster-office", LocationSpatialRelationshipKind.Below);
            bool symmetric = runtime.AreSpatiallyRelated("location.prototype.adventurers-guild", "location.prototype.market-district", LocationSpatialRelationshipKind.Near);
            bool valid = directional.Succeeded && above && inverse && symmetric;
            return TestLabAssertions.True("step14-location-spatial-directionality", "Spatial relationships resolve directional inverse and symmetric semantics", valid, $"Create={directional.Status} Above={above} Inverse={inverse} Symmetric={symmetric}");
        }

        private static TestLabAutomationStepResult SpatialNoRouting(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationSpatialRelationshipSnapshot spatial = runtime.GetSpatialRelationships("location.prototype.dungeon-entry", includeIncoming: true, includeHidden: true).FirstOrDefault(item => item.Kind == LocationSpatialRelationshipKind.PartOfComplex);
            bool noRouteFields = spatial != null && spatial.ToSaveData().GetType().GetField("routeCost") == null && spatial.ToSaveData().GetType().GetField("travelMode") == null;
            bool valid = spatial != null && noRouteFields && runtime.TryGetSnapshot(spatial.SourceLocationId, out _) && runtime.TryGetSnapshot(spatial.TargetLocationId, out _);
            return TestLabAssertions.True("step14-location-spatial-boundary", "Spatial relationships remain descriptive and do not carry travel semantics", valid, $"Spatial={spatial?.RelationshipId} NoRouteFields={noRouteFields}");
        }

        private static TestLabAutomationStepResult HierarchyPreviewIdempotence(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationOperationResult room = Create(context, "preview-room", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Preview Link Room", tags: new[] { "room", "interior" });
            long before = runtime.Revision;
            LocationContainmentRequest request = new LocationContainmentRequest
            {
                transactionId = Tx(context, "preview-link"),
                linkId = context.ScenarioContext.ScopedId("location-containment.test", "preview-link"),
                parentLocationId = "location.prototype.adventurers-guild",
                childLocationId = room.Snapshot?.LocationId,
                kind = LocationContainmentKind.Interior,
                effectiveWorldTime = 100d,
                preview = true
            };
            LocationOperationResult preview = runtime.AssignContainment(request);
            request.preview = false;
            LocationOperationResult execute = runtime.AssignContainment(request);
            LocationOperationResult duplicate = runtime.AssignContainment(request);
            bool valid = room.Succeeded && preview.Preview && execute.Succeeded && duplicate.Duplicate && runtime.Revision == before + 1L;
            return TestLabAssertions.True("step14-location-hierarchy-preview", "Preview and duplicate graph operations mutate exactly once", valid, $"Preview={preview.Status} Execute={execute.Status} Duplicate={duplicate.Status} Revision={before}->{runtime.Revision}");
        }

        private static TestLabAutomationStepResult HierarchyPersistence(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationRuntimeSaveData save = runtime.CreateSaveData();
            LocationRuntime restored = new LocationRuntime();
            restored.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.WorldId);
            LocationOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.WorldId);
            bool valid = restore.Succeeded
                && restored.GetActiveParentLink("location.prototype.guildmaster-office")?.ParentLocationId == "location.prototype.adventurers-guild"
                && restored.AreSpatiallyRelated("location.prototype.market-district", "location.prototype.adventurers-guild", LocationSpatialRelationshipKind.Near)
                && restored.CreateSaveData().containmentLinks.Select(link => link.linkId).SequenceEqual(save.containmentLinks.Select(link => link.linkId));
            return TestLabAssertions.True("step14-location-hierarchy-persistence", "Save and restore preserve graph records deterministically", valid, $"Restore={restore.Status} Links={restored.ContainmentLinkCount}/{runtime.ContainmentLinkCount} Spatial={restored.SpatialRelationshipCount}/{runtime.SpatialRelationshipCount}");
        }

        private static TestLabAutomationStepResult CorruptHierarchyRejection(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            LocationRuntimeSaveData corrupt = runtime.CreateSaveData();
            corrupt.containmentLinks[0].parentLocationId = "location.prototype.missing";
            LocationRuntimeSaveData before = runtime.CreateSaveData();
            LocationOperationResult rejected = runtime.RestoreFromSaveData(corrupt, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.WorldId);
            bool unchanged = runtime.CreateSaveData().containmentLinks.Select(link => link.parentLocationId).SequenceEqual(before.containmentLinks.Select(link => link.parentLocationId));
            bool valid = rejected.Status == LocationOperationStatus.PersistenceInvalid && unchanged;
            return TestLabAssertions.True("step14-location-hierarchy-corrupt", "Corrupt hierarchy restore rejects without mutation", valid, $"Rejected={rejected.Status} Unchanged={unchanged} Message={rejected.Message}");
        }

        private static TestLabAutomationStepResult VisibilityProjections(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            IReadOnlyList<LocationSnapshot> normal = runtime.GetChildren("location.prototype.wilderness-ring", includeHidden: false);
            IReadOnlyList<LocationSnapshot> privileged = runtime.GetChildren("location.prototype.wilderness-ring", includeHidden: true);
            bool hiddenOmitted = normal.All(item => item.LocationId != "location.prototype.dungeon-entry");
            bool hiddenVisible = privileged.Any(item => item.LocationId == "location.prototype.dungeon-entry");
            bool valid = hiddenOmitted && hiddenVisible;
            return TestLabAssertions.True("step14-location-hierarchy-visibility", "Hidden containment links are omitted unless privileged", valid, $"Normal={normal.Count} Privileged={privileged.Count} HiddenOmitted={hiddenOmitted}");
        }

        private static TestLabAutomationStepResult HierarchyFixtureSnapshot(TestLabAutomationContext context)
        {
            LocationRuntime runtime = Runtime(context);
            TestLabRuntimeBundleSnapshot snapshot = context.ScenarioContext.Runtimes.CreateSnapshot();
            int beforeLinks = runtime.ContainmentLinkCount;
            LocationOperationResult room = Create(context, "fixture-room", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Fixture Link Room", tags: new[] { "room", "interior" });
            runtime.AssignContainment(new LocationContainmentRequest
            {
                transactionId = Tx(context, "fixture-link"),
                linkId = context.ScenarioContext.ScopedId("location-containment.test", "fixture-link"),
                parentLocationId = "location.prototype.adventurers-guild",
                childLocationId = room.Snapshot?.LocationId,
                kind = LocationContainmentKind.Interior
            });
            bool restored = context.ScenarioContext.Runtimes.RestoreSnapshot(snapshot, out string failure);
            bool valid = restored && runtime.ContainmentLinkCount == beforeLinks && !runtime.TryGetSnapshot(room.Snapshot?.LocationId, out _);
            return TestLabAssertions.True("step14-location-hierarchy-fixture", "Fixture snapshot restores graph mutations", valid, $"Restored={restored} Links={runtime.ContainmentLinkCount}/{beforeLinks} Failure={failure}");
        }

        private static TestLabAutomationStepResult EntityLocationReadiness(TestLabAutomationContext context)
        {
            EntityLocationRuntime runtime = EntityRuntime(context);
            bool validRuntime = runtime.ValidateRuntime(out string failure);
            bool hasBody = runtime.TryGetActivePlacement(Body(PrototypeEntityLocationFactory.PlayerBodyId, context), out EntityPlacementSnapshot playerBody);
            EntityLocationResolutionResult player = runtime.ResolvePhysicalLocation(Person(PrototypeEntityLocationFactory.PlayerPersonId, context));
            bool valid = runtime != null && validRuntime && hasBody && player.Succeeded && player.LocationId == "location.prototype.village" && runtime.ActivePlacementCount >= 8 && runtime.KnownEntityCount >= 12;
            return TestLabAssertions.True("step14-entity-location-readiness", "Seeded entity placements validate", valid, $"Valid={validRuntime} Failure={failure} Active={runtime?.ActivePlacementCount} Known={runtime?.KnownEntityCount} Player={player.Status}:{player.LocationId} Body={playerBody?.ExactLocationId}");
        }

        private static TestLabAutomationStepResult SingleActiveExactPlacement(TestLabAutomationContext context)
        {
            EntityLocationRuntime runtime = EntityRuntime(context);
            EntityLocationReferenceData entity = Body(PrototypeEntityLocationFactory.GuildMasterBodyId, context);
            long before = runtime.Revision;
            EntityLocationOperationResult rejected = runtime.Place(new EntityPlacementRequest
            {
                transactionId = Tx(context, "entity-single-active"),
                placementId = context.ScenarioContext.ScopedId("placement.test", "single-active"),
                entity = entity,
                exactLocationId = "location.prototype.market-district",
                worldTime = 50d
            });
            bool valid = rejected.Status == EntityLocationOperationStatus.ConflictingActivePlacement && runtime.Revision == before && runtime.ResolvePhysicalLocation(entity).LocationId == "location.prototype.guildmaster-office";
            return TestLabAssertions.True("step14-entity-location-single-active", "Conflicting active placement rejects without mutation", valid, $"Status={rejected.Status} Revision={before}->{runtime.Revision} Location={runtime.ResolvePhysicalLocation(entity).LocationId}");
        }

        private static TestLabAutomationStepResult PersonResolvesThroughBody(TestLabAutomationContext context)
        {
            EntityLocationRuntime runtime = EntityRuntime(context);
            EntityLocationResolutionResult person = runtime.ResolvePhysicalLocation(Person(PrototypeEntityLocationFactory.PrisonerPersonId, context));
            bool directPerson = runtime.TryGetActivePlacement(Person(PrototypeEntityLocationFactory.PrisonerPersonId, context), out _);
            bool valid = person.Status == EntityPhysicalLocationResolutionStatus.ResolvedThroughBody && person.LocationId == "location.prototype.basement-prison" && !directPerson;
            return TestLabAssertions.True("step14-entity-location-person-body", "Person resolves through active body without duplicate Person placement", valid, $"Status={person.Status} Location={person.LocationId} DirectPerson={directPerson}");
        }

        private static TestLabAutomationStepResult DirectAndRecursiveOccupancy(TestLabAutomationContext context)
        {
            EntityLocationRuntime runtime = EntityRuntime(context);
            LocationOccupancySnapshot direct = runtime.GetDirectOccupancy("location.prototype.adventurers-guild");
            LocationOccupancySnapshot recursive = runtime.GetRecursiveOccupancy("location.prototype.adventurers-guild");
            bool directHasChest = direct.Placements.Any(item => item.EntityId == PrototypeEntityLocationFactory.GuildChestEntityId);
            bool recursiveHasGuildmaster = recursive.Placements.Any(item => item.EntityId == PrototypeEntityLocationFactory.GuildMasterBodyId);
            bool deterministic = recursive.Placements.Select(item => item.EntityKey).SequenceEqual(recursive.Placements.Select(item => item.EntityKey).OrderBy(id => id, StringComparer.Ordinal));
            bool valid = directHasChest && recursiveHasGuildmaster && recursive.Count > direct.Count && deterministic;
            return TestLabAssertions.True("step14-entity-location-occupancy", "Recursive occupancy is derived through location descendants", valid, $"Direct={direct.Count} Recursive={recursive.Count} Chest={directHasChest} Guildmaster={recursiveHasGuildmaster} Deterministic={deterministic}");
        }

        private static TestLabAutomationStepResult RelocationHistoryAndDiff(TestLabAutomationContext context)
        {
            EntityLocationRuntime runtime = EntityRuntime(context);
            EntityLocationReferenceData entity = Body(PrototypeEntityLocationFactory.MerchantBodyId, context);
            EntityLocationOperationResult move = runtime.Relocate(new EntityRelocationRequest
            {
                transactionId = Tx(context, "entity-relocate"),
                newPlacementId = context.ScenarioContext.ScopedId("placement.test", "merchant-civic"),
                entity = entity,
                expectedOriginLocationId = "location.prototype.merchant-counter",
                destinationLocationId = "location.prototype.civic-office",
                category = EntityPlacementCategory.Visiting,
                worldTime = 100d
            });
            bool oldAtTime = runtime.GetPlacementAtTime(entity, 50d)?.ExactLocationId == "location.prototype.merchant-counter";
            bool now = runtime.ResolvePhysicalLocation(entity).LocationId == "location.prototype.civic-office";
            bool diff = move.TransitionDiff.EnteredLocationIds.Contains("location.prototype.civic-office") && move.TransitionDiff.ExitedLocationIds.Contains("location.prototype.merchant-counter");
            bool valid = move.Succeeded && oldAtTime && now && diff;
            return TestLabAssertions.True("step14-entity-location-relocate", "Relocation preserves history and hierarchy transition diff", valid, $"Move={move.Status} OldAt50={oldAtTime} Now={now} Entered={string.Join(",", move.TransitionDiff.EnteredLocationIds)} Exited={string.Join(",", move.TransitionDiff.ExitedLocationIds)}");
        }

        private static TestLabAutomationStepResult UnplacementLastKnown(TestLabAutomationContext context)
        {
            EntityLocationRuntime runtime = EntityRuntime(context);
            EntityLocationReferenceData entity = Item(PrototypeEntityLocationFactory.ArrowItemInstanceId, context);
            EntityLocationOperationResult unplace = runtime.Unplace(new EntityUnplacementRequest
            {
                transactionId = Tx(context, "entity-unplace"),
                entity = entity,
                worldTime = 120d,
                sourceEventId = "testlab.entity-location.unplace"
            });
            EntityLocationResolutionResult active = runtime.ResolvePhysicalLocation(entity);
            EntityPlacementSnapshot lastKnown = runtime.GetLastKnownPlacement(entity);
            bool valid = unplace.Succeeded && active.Status == EntityPhysicalLocationResolutionStatus.Unplaced && lastKnown != null && lastKnown.ExactLocationId == "location.prototype.market-district" && lastKnown.LifecycleState == EntityPlacementLifecycleState.Ended;
            return TestLabAssertions.True("step14-entity-location-unplace", "Unplacement ends active occupancy but preserves last-known location", valid, $"Unplace={unplace.Status} Active={active.Status} Last={lastKnown?.ExactLocationId}:{lastKnown?.LifecycleState}");
        }

        private static TestLabAutomationStepResult LocationLifecycleRejection(TestLabAutomationContext context)
        {
            LocationRuntime locations = Runtime(context);
            EntityLocationRuntime runtime = EntityRuntime(context);
            LocationOperationResult room = Create(context, "closed-occupancy", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Closed Occupancy Room", tags: new[] { "room", "interior" });
            LocationOperationResult close = locations.TransitionLifecycle(new LocationLifecycleTransitionRequest { transactionId = Tx(context, "closed-occupancy-location"), locationId = room.Snapshot?.LocationId, targetState = LocationLifecycleState.Closed, worldTime = 130d });
            long before = runtime.Revision;
            EntityLocationOperationResult place = runtime.Place(new EntityPlacementRequest
            {
                transactionId = Tx(context, "entity-closed-place"),
                entity = Item(PrototypeEntityLocationFactory.ArrowItemInstanceId, context),
                exactLocationId = room.Snapshot?.LocationId,
                category = EntityPlacementCategory.Dropped,
                worldTime = 131d
            });
            bool valid = room.Succeeded && close.Succeeded && place.Status == EntityLocationOperationStatus.InactiveLocation && runtime.Revision == before;
            return TestLabAssertions.True("step14-entity-location-lifecycle", "Closed locations reject new ordinary placement without evicting elsewhere", valid, $"Room={room.Status} Close={close.Status} Place={place.Status} Revision={before}->{runtime.Revision}");
        }

        private static TestLabAutomationStepResult CapacityAndTypeRules(TestLabAutomationContext context)
        {
            EntityLocationRuntime runtime = EntityRuntime(context);
            LocationOperationResult room = Create(context, "capacity", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Capacity Room", tags: new[] { "room", "interior" });
            runtime.ConfigureCapacity(new EntityLocationCapacityRuleData
            {
                locationId = room.Snapshot?.LocationId,
                maxDirectOccupants = 1,
                allowedEntityTypes = new[] { LocationOccupantEntityType.Body }
            });
            EntityLocationReferenceData body = new EntityLocationReferenceData { entityType = LocationOccupantEntityType.Body, entityId = context.ScenarioContext.ScopedId("body.prototype.test", "capacity"), worldId = context.ScenarioContext.Runtimes.WorldId };
            EntityLocationReferenceData itemEntity = new EntityLocationReferenceData { entityType = LocationOccupantEntityType.ItemInstance, entityId = context.ScenarioContext.ScopedId("item-instance.test", "capacity"), worldId = context.ScenarioContext.Runtimes.WorldId };
            runtime.RegisterKnownEntity(body);
            runtime.RegisterKnownEntity(itemEntity);
            EntityLocationOperationResult first = runtime.Place(new EntityPlacementRequest { transactionId = Tx(context, "capacity-first"), entity = body, exactLocationId = room.Snapshot?.LocationId, category = EntityPlacementCategory.Present, worldTime = 140d });
            EntityLocationOperationResult item = runtime.Place(new EntityPlacementRequest { transactionId = Tx(context, "capacity-item"), entity = itemEntity, exactLocationId = room.Snapshot?.LocationId, category = EntityPlacementCategory.Dropped, worldTime = 141d });
            bool valid = room.Succeeded && first.Succeeded && item.Status == EntityLocationOperationStatus.OccupantTypeNotAllowed && runtime.GetDirectOccupancy(room.Snapshot.LocationId).Count == 1;
            return TestLabAssertions.True("step14-entity-location-capacity", "Capacity/type rules reject disallowed occupants without mutation", valid, $"Room={room.Status} First={first.Status} Item={item.Status} Count={runtime.GetDirectOccupancy(room.Snapshot?.LocationId).Count}");
        }

        private static TestLabAutomationStepResult InventoryWorldExclusion(TestLabAutomationContext context)
        {
            EntityLocationRuntime runtime = EntityRuntime(context);
            EntityLocationReferenceData held = new EntityLocationReferenceData { entityType = LocationOccupantEntityType.ItemInstance, entityId = context.ScenarioContext.ScopedId("item-instance.test", "held"), worldId = context.ScenarioContext.Runtimes.WorldId };
            runtime.RegisterKnownEntity(held);
            runtime.MarkInventoryHeld(held, true);
            long before = runtime.Revision;
            EntityLocationOperationResult place = runtime.Place(new EntityPlacementRequest { transactionId = Tx(context, "inventory-held"), entity = held, exactLocationId = "location.prototype.market-district", category = EntityPlacementCategory.Dropped, worldTime = 150d });
            bool valid = place.Status == EntityLocationOperationStatus.InventoryConflict && runtime.Revision == before && !runtime.TryGetActivePlacement(held, out _);
            return TestLabAssertions.True("step14-entity-location-inventory", "Inventory-held item rejects world placement", valid, $"Status={place.Status} Revision={before}->{runtime.Revision}");
        }

        private static TestLabAutomationStepResult EntityLocationPersistenceRoundTrip(TestLabAutomationContext context)
        {
            EntityLocationRuntime runtime = EntityRuntime(context);
            EntityLocationRuntimeSaveData save = runtime.CreateSaveData();
            EntityLocationRuntime restored = new EntityLocationRuntime();
            EntityLocationOperationResult restore = restored.RestoreFromSaveData(save, Runtime(context), context.ScenarioContext.Runtimes.WorldId, restoring: true);
            EntityLocationResolutionResult player = restored.ResolvePhysicalLocation(Person(PrototypeEntityLocationFactory.PlayerPersonId, context));
            bool valid = restore.Succeeded
                && player.LocationId == "location.prototype.village"
                && restored.CreateSaveData().placements.Select(item => item.placementId).SequenceEqual(save.placements.Select(item => item.placementId));
            return TestLabAssertions.True("step14-entity-location-persistence", "Entity locations save and restore deterministically", valid, $"Restore={restore.Status} Player={player.Status}:{player.LocationId} Count={restored.PlacementCount}/{runtime.PlacementCount}");
        }

        private static TestLabAutomationStepResult EntityLocationCorruptRestore(TestLabAutomationContext context)
        {
            EntityLocationRuntime runtime = EntityRuntime(context);
            EntityLocationRuntimeSaveData before = runtime.CreateSaveData();
            EntityLocationRuntimeSaveData corrupt = before.Clone();
            corrupt.placements[0].exactLocationId = "location.prototype.missing";
            EntityLocationOperationResult rejected = runtime.RestoreFromSaveData(corrupt, Runtime(context), context.ScenarioContext.Runtimes.WorldId, restoring: true);
            bool unchanged = runtime.CreateSaveData().placements.Select(item => item.exactLocationId).SequenceEqual(before.placements.Select(item => item.exactLocationId));
            bool valid = rejected.Status == EntityLocationOperationStatus.PersistenceInvalid && unchanged;
            return TestLabAssertions.True("step14-entity-location-corrupt", "Corrupt entity placement restore rejects before commit", valid, $"Rejected={rejected.Status} Unchanged={unchanged} Message={rejected.Message}");
        }

        private static TestLabAutomationStepResult EntityLocationFixtureSnapshot(TestLabAutomationContext context)
        {
            EntityLocationRuntime runtime = EntityRuntime(context);
            TestLabRuntimeBundleSnapshot snapshot = context.ScenarioContext.Runtimes.CreateSnapshot();
            int before = runtime.ActivePlacementCount;
            EntityLocationReferenceData entity = new EntityLocationReferenceData { entityType = LocationOccupantEntityType.WorldEntity, entityId = context.ScenarioContext.ScopedId("world-entity.test", "fixture"), worldId = context.ScenarioContext.Runtimes.WorldId };
            runtime.RegisterKnownEntity(entity);
            runtime.Place(new EntityPlacementRequest { transactionId = Tx(context, "entity-fixture"), entity = entity, exactLocationId = "location.prototype.village", category = EntityPlacementCategory.Present, worldTime = 160d });
            bool restored = context.ScenarioContext.Runtimes.RestoreSnapshot(snapshot, out string failure);
            bool missing = !runtime.TryGetActivePlacement(entity, out _);
            bool valid = restored && missing && runtime.ActivePlacementCount == before;
            return TestLabAssertions.True("step14-entity-location-fixture", "Fixture snapshot restores entity placement mutations", valid, $"Restored={restored} Missing={missing} Active={runtime.ActivePlacementCount}/{before} Failure={failure}");
        }

        private static TestLabAutomationStepResult InteractionReadiness(TestLabAutomationContext context)
        {
            InteractionPointRuntime runtime = InteractionRuntime(context);
            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool hasPointDefinition = registry.TryGet(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterDefinitionId, out InteractionPointDefinition pointDefinition);
            bool hasServiceDefinition = registry.TryGet(PrototypeInteractionPointDefinitionFactory.QuestBoardBrowseServiceId, out InteractionServiceDefinition serviceDefinition);
            bool hasSeededCounter = runtime.TryGetPoint(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, out InteractionPointSnapshot counter);
            bool hasSeededBoard = runtime.TryGetPoint(PrototypeInteractionPointDefinitionFactory.QuestBoardPointId, out InteractionPointSnapshot board);
            bool validRuntime = runtime.ValidateCurrent(out string failure);
            bool valid = runtime != null
                && hasPointDefinition
                && hasServiceDefinition
                && hasSeededCounter
                && hasSeededBoard
                && validRuntime
                && pointDefinition.Category == InteractionPointCategory.GuildCounter
                && serviceDefinition.DestinationRuntime == InteractionDestinationRuntime.QuestPlaceholder
                && counter.ActiveHostLocationId == "location.prototype.adventurers-guild"
                && board.ServiceDefinitionIds.Contains(PrototypeInteractionPointDefinitionFactory.QuestBoardBrowseServiceId);
            return TestLabAssertions.True("step14-interaction-readiness", "Seeded interaction points and definitions resolve", valid, $"Definitions={hasPointDefinition}/{hasServiceDefinition} Seeded={hasSeededCounter}/{hasSeededBoard} Points={runtime?.PointCount} Validation={validRuntime}:{failure}");
        }

        private static TestLabAutomationStepResult InteractionDefinitionVsInstance(TestLabAutomationContext context)
        {
            InteractionPointRuntime runtime = InteractionRuntime(context);
            InteractionPointOperationResult first = CreateInteractionPoint(context, "shop-a", PrototypeInteractionPointDefinitionFactory.MerchantStallCounterDefinitionId, "Shop Counter A", "location.prototype.merchant-counter", new[] { PrototypeInteractionPointDefinitionFactory.ShopSaleServiceId });
            InteractionPointOperationResult second = CreateInteractionPoint(context, "shop-b", PrototypeInteractionPointDefinitionFactory.MerchantStallCounterDefinitionId, "Shop Counter B", "location.prototype.merchant-counter", new[] { PrototypeInteractionPointDefinitionFactory.ShopSaleServiceId });
            bool valid = first.Succeeded
                && second.Succeeded
                && first.Point.InteractionPointId != second.Point.InteractionPointId
                && first.Point.InteractionPointDefinitionId == second.Point.InteractionPointDefinitionId
                && runtime.GetPointsByDefinition(PrototypeInteractionPointDefinitionFactory.MerchantStallCounterDefinitionId).Count >= 3;
            return TestLabAssertions.True("step14-interaction-definition-separation", "One point definition can back distinct runtime points", valid, $"First={first.Status} Second={second.Status} Count={runtime.GetPointsByDefinition(PrototypeInteractionPointDefinitionFactory.MerchantStallCounterDefinitionId).Count}");
        }

        private static TestLabAutomationStepResult InteractionHostValidation(TestLabAutomationContext context)
        {
            InteractionPointRuntime runtime = InteractionRuntime(context);
            InteractionPointOperationResult invalid = CreateInteractionPoint(context, "invalid-host", PrototypeInteractionPointDefinitionFactory.WorkstationDefinitionId, "Invalid Wilderness Workstation", "location.prototype.wilderness-ring", new[] { PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId });
            InteractionPointOperationResult point = CreateInteractionPoint(context, "valid-host", PrototypeInteractionPointDefinitionFactory.WorkstationDefinitionId, "Valid Workstation", "location.prototype.merchant-counter", new[] { PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId });
            InteractionPointOperationResult reassign = runtime.ReassignHost(new InteractionPointHostReassignmentRequest
            {
                transactionId = Tx(context, "interaction-host-reassign"),
                interactionPointId = point.Point?.InteractionPointId,
                newHostAssignmentId = context.ScenarioContext.ScopedId("interaction-host.test", "reassign"),
                newHostLocationId = "location.prototype.adventurers-guild",
                worldTime = 80d
            });
            bool history = runtime.CreateSaveData().hostAssignments.Count(assignment => assignment.interactionPointId == point.Point?.InteractionPointId) == 2;
            bool valid = invalid.Status == InteractionPointOperationStatus.InvalidHostLocation && point.Succeeded && reassign.Succeeded && reassign.Point.ActiveHostLocationId == "location.prototype.adventurers-guild" && history;
            return TestLabAssertions.True("step14-interaction-hosts", "Invalid hosts reject and valid host reassignments preserve history", valid, $"Invalid={invalid.Status} Point={point.Status} Reassign={reassign.Status} History={history}");
        }

        private static TestLabAutomationStepResult InteractionSubjectProviderBoundaries(TestLabAutomationContext context)
        {
            InteractionPointRuntime runtime = InteractionRuntime(context);
            InteractionPointOperationResult link = runtime.AddSubjectLink(new InteractionSubjectLinkRequest
            {
                transactionId = Tx(context, "interaction-link"),
                linkId = context.ScenarioContext.ScopedId("interaction-subject.test", "guild-counter"),
                interactionPointId = PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId,
                role = InteractionSubjectLinkRole.AssociatedRecordsCollection,
                subject = Subject("KnowledgeRecordCollection", "records.prototype.guild-public", context),
                worldTime = 90d
            });
            InteractionPointOperationResult provider = runtime.AssignProvider(new InteractionProviderAssignmentRequest
            {
                transactionId = Tx(context, "interaction-provider"),
                assignmentId = context.ScenarioContext.ScopedId("interaction-provider.test", "guild-info"),
                interactionPointId = PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.AdventurerInformationServiceId,
                requirementKind = InteractionProviderRequirementKind.AssignedPerson,
                providerEntity = Person(PrototypeEntityLocationFactory.GuildMasterPersonId, context),
                providerOrganizationId = "organization.prototype.adventurers-guild",
                presencePolicy = InteractionPhysicalPresencePolicy.WithinHostLocation,
                worldTime = 90d
            });
            bool linkFound = runtime.GetSubjectLinks(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId).Any(item => item.Subject.subjectId == "records.prototype.guild-public");
            bool providerFound = runtime.GetProviderAssignments(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId).Any(item => item.ServiceDefinitionId == PrototypeInteractionPointDefinitionFactory.AdventurerInformationServiceId);
            bool valid = link.Succeeded && provider.Succeeded && linkFound && providerFound && context.ScenarioContext.Runtimes.OrganizationMemberships != null && context.ScenarioContext.Runtimes.Records != null;
            return TestLabAssertions.True("step14-interaction-subject-provider", "Subject links and provider assignments reference owning runtimes without owning them", valid, $"Link={link.Status} Provider={provider.Status} Links={runtime.GetSubjectLinks(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId).Count} Providers={runtime.GetProviderAssignments(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId).Count}");
        }

        private static TestLabAutomationStepResult InteractionPresenceEligibility(TestLabAutomationContext context)
        {
            InteractionPointRuntime runtime = InteractionRuntime(context);
            EntityLocationRuntime entityRuntime = EntityRuntime(context);
            EntityLocationOperationResult movePlayer = entityRuntime.Relocate(new EntityRelocationRequest
            {
                transactionId = Tx(context, "interaction-player-guild"),
                newPlacementId = context.ScenarioContext.ScopedId("placement.test", "interaction-player-guild"),
                entity = Body(PrototypeEntityLocationFactory.PlayerBodyId, context),
                expectedOriginLocationId = "location.prototype.village",
                destinationLocationId = "location.prototype.adventurers-guild",
                category = EntityPlacementCategory.Visiting,
                worldTime = 100d
            });
            InteractionPointOperationResult provider = runtime.AssignProvider(new InteractionProviderAssignmentRequest
            {
                transactionId = Tx(context, "interaction-presence-provider"),
                assignmentId = context.ScenarioContext.ScopedId("interaction-provider.test", "guild-info-presence"),
                interactionPointId = PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.AdventurerInformationServiceId,
                providerEntity = Person(PrototypeEntityLocationFactory.GuildMasterPersonId, context),
                presencePolicy = InteractionPhysicalPresencePolicy.WithinHostLocation,
                worldTime = 100d
            });
            InteractionEligibilityResult eligible = runtime.EvaluateEligibility(new InteractionEligibilityRequest
            {
                interactionPointId = PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.AdventurerInformationServiceId,
                consumerEntity = Person(PrototypeEntityLocationFactory.PlayerPersonId, context)
            });
            InteractionEligibilityResult absent = runtime.EvaluateEligibility(new InteractionEligibilityRequest
            {
                interactionPointId = PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.AdventurerInformationServiceId,
                consumerEntity = Person(PrototypeEntityLocationFactory.MerchantPersonId, context)
            });
            bool valid = movePlayer.Succeeded && provider.Succeeded && eligible.Eligible && absent.Status == InteractionPointOperationStatus.ConsumerAbsent;
            return TestLabAssertions.True("step14-interaction-presence", "Interaction eligibility consults entity location presence", valid, $"Move={movePlayer.Status} Provider={provider.Status} Eligible={eligible.Status}:{eligible.Fingerprint} Absent={absent.Status}:{string.Join(",", absent.FailureReasons)}");
        }

        private static TestLabAutomationStepResult InteractionCapacityReservationSession(TestLabAutomationContext context)
        {
            InteractionPointRuntime runtime = InteractionRuntime(context);
            EntityLocationRuntime entityRuntime = EntityRuntime(context);
            entityRuntime.Relocate(new EntityRelocationRequest
            {
                transactionId = Tx(context, "interaction-player-board"),
                newPlacementId = context.ScenarioContext.ScopedId("placement.test", "interaction-player-board"),
                entity = Body(PrototypeEntityLocationFactory.PlayerBodyId, context),
                expectedOriginLocationId = "location.prototype.village",
                destinationLocationId = "location.prototype.merchant-counter",
                category = EntityPlacementCategory.Visiting,
                worldTime = 110d
            });
            InteractionPointOperationResult point = CreateInteractionPoint(context, "capacity-workstation", PrototypeInteractionPointDefinitionFactory.WorkstationDefinitionId, "Capacity Workstation", "location.prototype.merchant-counter", new[] { PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId });
            InteractionPointOperationResult reservation = runtime.Reserve(new InteractionReservationRequest
            {
                transactionId = Tx(context, "interaction-reserve"),
                reservationId = context.ScenarioContext.ScopedId("interaction-reservation.test", "quest-board"),
                interactionPointId = point.Point?.InteractionPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId,
                reservingSubject = Subject("Person", PrototypeEntityLocationFactory.PlayerPersonId, context),
                startWorldTime = 111d,
                endWorldTime = 120d
            });
            InteractionPointOperationResult session = runtime.StartSession(new InteractionSessionStartRequest
            {
                transactionId = Tx(context, "interaction-session"),
                sessionId = context.ScenarioContext.ScopedId("interaction-session.test", "quest-board"),
                interactionPointId = point.Point?.InteractionPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId,
                consumerEntity = Person(PrototypeEntityLocationFactory.PlayerPersonId, context),
                reservationId = reservation.Reservation?.ReservationId,
                startWorldTime = 112d
            });
            InteractionPointOperationResult duplicate = runtime.StartSession(new InteractionSessionStartRequest
            {
                transactionId = Tx(context, "interaction-session-second"),
                sessionId = context.ScenarioContext.ScopedId("interaction-session.test", "quest-board-second"),
                interactionPointId = point.Point?.InteractionPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId,
                consumerEntity = Person(PrototypeEntityLocationFactory.MerchantPersonId, context),
                startWorldTime = 113d
            });
            bool valid = point.Succeeded && reservation.Succeeded && session.Succeeded && duplicate.Status == InteractionPointOperationStatus.CapacityFull && runtime.GetUseSessions(point.Point.InteractionPointId).Count == 1;
            return TestLabAssertions.True("step14-interaction-capacity", "Capacity and reservation/session records are enforced deterministically", valid, $"Point={point.Status} Reservation={reservation.Status} Session={session.Status} Second={duplicate.Status} Sessions={runtime.GetUseSessions(point.Point?.InteractionPointId).Count}");
        }

        private static TestLabAutomationStepResult InteractionVisibilitySceneIndependence(TestLabAutomationContext context)
        {
            InteractionPointRuntime runtime = InteractionRuntime(context);
            InteractionPointOperationResult hidden = CreateInteractionPoint(context, "hidden", PrototypeInteractionPointDefinitionFactory.PrisonCellDefinitionId, "Hidden Cell Point", "location.prototype.basement-prison", new[] { PrototypeInteractionPointDefinitionFactory.PrisonCellInspectServiceId }, InteractionPointVisibility.Hidden, "prototype.scene.binding.hidden-cell");
            IReadOnlyList<InteractionPointSnapshot> normal = runtime.GetPointsByHost("location.prototype.basement-prison");
            IReadOnlyList<InteractionPointSnapshot> privileged = runtime.GetPointsByHost("location.prototype.basement-prison", includeHidden: true);
            bool normalOmitted = normal.All(item => item.InteractionPointId != hidden.Point?.InteractionPointId);
            bool privilegedFound = privileged.Any(item => item.InteractionPointId == hidden.Point?.InteractionPointId && item.SceneBindingKey == "prototype.scene.binding.hidden-cell");
            bool valid = hidden.Succeeded && normalOmitted && privilegedFound;
            return TestLabAssertions.True("step14-interaction-scene-independence", "Scene binding keys are persisted without requiring scene objects and hidden points can be omitted", valid, $"Hidden={hidden.Status} Normal={normal.Count} Privileged={privileged.Count} Binding={hidden.Point?.SceneBindingKey}");
        }

        private static TestLabAutomationStepResult InteractionDestinationRouting(TestLabAutomationContext context)
        {
            InteractionPointRuntime runtime = InteractionRuntime(context);
            EntityRuntime(context).Relocate(new EntityRelocationRequest
            {
                transactionId = Tx(context, "interaction-route-player"),
                newPlacementId = context.ScenarioContext.ScopedId("placement.test", "interaction-route-player"),
                entity = Body(PrototypeEntityLocationFactory.PlayerBodyId, context),
                expectedOriginLocationId = "location.prototype.village",
                destinationLocationId = "location.prototype.adventurers-guild",
                category = EntityPlacementCategory.Visiting,
                worldTime = 125d
            });
            long before = runtime.Revision;
            InteractionInvocationResult result = runtime.Invoke(new InteractionRequest
            {
                transactionId = Tx(context, "interaction-route"),
                interactionPointId = PrototypeInteractionPointDefinitionFactory.QuestBoardPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.QuestBoardBrowseServiceId,
                consumerEntity = Person(PrototypeEntityLocationFactory.PlayerPersonId, context),
                preview = false
            });
            bool valid = result.Success && result.DestinationRuntime == InteractionDestinationRuntime.QuestPlaceholder && runtime.Revision == before && result.RevisionBefore == result.RevisionAfter;
            return TestLabAssertions.True("step14-interaction-routing", "Interaction invocation validates route and leaves destination mutation to owning runtime", valid, $"Success={result.Success} Destination={result.DestinationRuntime} Revision={before}->{runtime.Revision} Message={result.Message}");
        }

        private static TestLabAutomationStepResult InteractionPersistenceValidation(TestLabAutomationContext context)
        {
            InteractionPointRuntime runtime = InteractionRuntime(context);
            InteractionPointPersistenceParticipant participant = new InteractionPointPersistenceParticipant(runtime, () => context.ScenarioContext.Runtimes.DefinitionRegistry, () => Runtime(context), () => EntityRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            var save = participant.CapturePayload();
            var prepared = participant.PreparePayload(save.PayloadJson, InteractionPointPersistenceParticipant.CurrentParticipantSchemaVersion);
            InteractionPointRuntime restored = new InteractionPointRuntime();
            restored.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, Runtime(context), EntityRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            InteractionPointOperationResult restore = restored.RestoreFromSaveData(runtime.CreateSaveData(), Runtime(context), EntityRuntime(context), context.ScenarioContext.Runtimes.WorldId, restoring: true);
            InteractionPointRuntimeSaveData corrupt = runtime.CreateSaveData();
            corrupt.points[0].activeHostLocationId = "location.prototype.missing";
            var rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), InteractionPointPersistenceParticipant.CurrentParticipantSchemaVersion);
            bool valid = save.Succeeded && prepared.Succeeded && restore.Succeeded && rejected != null && !rejected.Succeeded && restored.PointCount == runtime.PointCount;
            return TestLabAssertions.True("step14-interaction-persistence", "Interaction point save data round-trips and corrupt graphs reject before commit", valid, $"Save={save.Succeeded} Prepare={prepared.Succeeded} Restore={restore.Status} Rejected={rejected?.Succeeded == false} Count={restored.PointCount}/{runtime.PointCount}");
        }

        private static TestLabAutomationStepResult InteractionFixtureSnapshot(TestLabAutomationContext context)
        {
            InteractionPointRuntime runtime = InteractionRuntime(context);
            TestLabRuntimeBundleSnapshot snapshot = context.ScenarioContext.Runtimes.CreateSnapshot();
            int before = runtime.PointCount;
            InteractionPointOperationResult created = CreateInteractionPoint(context, "fixture", PrototypeInteractionPointDefinitionFactory.WorkstationDefinitionId, "Fixture Workstation", "location.prototype.merchant-counter", new[] { PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId });
            bool restored = context.ScenarioContext.Runtimes.RestoreSnapshot(snapshot, out string failure);
            bool missing = !runtime.TryGetPoint(created.Point?.InteractionPointId, out _);
            bool valid = created.Succeeded && restored && missing && runtime.PointCount == before;
            return TestLabAssertions.True("step14-interaction-fixture", "Fixture snapshot restores interaction point mutations", valid, $"Created={created.Status} Restored={restored} Missing={missing} Count={runtime.PointCount}/{before} Failure={failure}");
        }

        private static TestLabAutomationStepResult ConnectionReadiness(TestLabAutomationContext context)
        {
            LocationConnectionRuntime runtime = ConnectionRuntime(context);
            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool hasDoor = registry.TryGet(PrototypeLocationConnectionDefinitionFactory.LockableDoorDefinitionId, out LocationConnectionDefinition door);
            bool hasPolicy = registry.TryGet(PrototypeLocationConnectionDefinitionFactory.GuildMemberAccessPolicyId, out LocationAccessPolicyDefinition policy);
            bool hasVillage = runtime.TryGetConnection(PrototypeLocationConnectionDefinitionFactory.VillageGuildEntranceConnectionId, out LocationConnectionSnapshot village);
            bool hasHidden = runtime.GetOutgoingConnections("location.prototype.guildmaster-office", includeHidden: true).Any(item => item.ConnectionId == PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId);
            bool validation = runtime.ValidateCurrent(out string failure);
            bool valid = hasDoor && hasPolicy && hasVillage && hasHidden && validation && door.SupportsLockState && policy.Category == LocationAccessPolicyCategory.OrganizationMembers && village.SceneBindingCategory == LocationConnectionSceneBindingCategory.PrototypeMarker;
            return TestLabAssertions.True("step14-connection-readiness", "Connection definitions and seeded records resolve", valid, $"Door={hasDoor} Policy={hasPolicy} Village={hasVillage} Hidden={hasHidden} Count={runtime.ConnectionCount} Validation={validation} Failure={failure}");
        }

        private static TestLabAutomationStepResult ConnectionDefinitionAdjacencyBoundaries(TestLabAutomationContext context)
        {
            LocationConnectionRuntime runtime = ConnectionRuntime(context);
            LocationConnectionOperationResult preview = CreateConnection(context, "preview", "location.prototype.adventurers-guild", "location.prototype.merchant-counter", preview: true);
            LocationConnectionOperationResult first = CreateConnection(context, "runtime-a", "location.prototype.adventurers-guild", "location.prototype.merchant-counter");
            LocationConnectionOperationResult second = CreateConnection(context, "runtime-b", "location.prototype.adventurers-guild", "location.prototype.merchant-counter");
            LocationConnectionOperationResult duplicate = CreateConnection(context, "runtime-a", "location.prototype.adventurers-guild", "location.prototype.merchant-counter");
            bool adjacencyDoesNotConnectGuild = Runtime(context).GetSpatialRelationships("location.prototype.market-district").Any()
                && !runtime.GetOutgoingConnections("location.prototype.market-district").Any(item => item.DestinationLocationId == "location.prototype.adventurers-guild");
            bool valid = preview.Status == LocationConnectionOperationStatus.Preview
                && !runtime.TryGetConnection(preview.Connection?.ConnectionId, out _)
                && first.Succeeded
                && second.Succeeded
                && first.Connection.ConnectionDefinitionId == second.Connection.ConnectionDefinitionId
                && first.Connection.ConnectionId != second.Connection.ConnectionId
                && duplicate.Status == LocationConnectionOperationStatus.Duplicate
                && adjacencyDoesNotConnectGuild;
            return TestLabAssertions.True("step14-connection-boundaries", "Connections are runtime records and do not derive from spatial adjacency", valid, $"Preview={preview.Status} First={first.Status} Second={second.Status} Duplicate={duplicate.Status} AdjacencySeparated={adjacencyDoesNotConnectGuild}");
        }

        private static TestLabAutomationStepResult ConnectionStateGates(TestLabAutomationContext context)
        {
            LocationConnectionRuntime runtime = ConnectionRuntime(context);
            EntityLocationRuntime entities = EntityRuntime(context);
            EntityLocationReferenceData actor = Body(PrototypeEntityLocationFactory.PlayerBodyId, context);
            EntityLocationOperationResult moveToGuild = entities.Relocate(new EntityRelocationRequest
            {
                transactionId = Tx(context, "connection-move-guild"),
                newPlacementId = context.ScenarioContext.ScopedId("placement.test", "connection-move-guild"),
                entity = actor,
                destinationLocationId = "location.prototype.adventurers-guild",
                worldTime = 19d
            });
            long entityBefore = entities.Revision;
            LocationConnectionOperationResult locked = runtime.Traverse(Traversal(context, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", AccessContext(context, actor, organizations: new[] { "organization.prototype.adventurers-guild" })));
            long entityAfterLocked = entities.Revision;
            LocationConnectionOperationResult unlock = UnlockConnection(context, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId);
            LocationConnectionOperationResult block = runtime.MutateState(new LocationConnectionStateMutationRequest
            {
                transactionId = Tx(context, "connection-block"),
                connectionId = PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId,
                blockageState = LocationConnectionBlockageState.TemporarilyBlocked,
                worldTime = 20d
            });
            LocationConnectionOperationResult blocked = runtime.Traverse(Traversal(context, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", AccessContext(context, actor, offices: new[] { "office.prototype.guild-head" }, authorities: new[] { "permission.prototype.guild.rank-admin" })));
            LocationConnectionOperationResult clear = runtime.MutateState(new LocationConnectionStateMutationRequest
            {
                transactionId = Tx(context, "connection-clear"),
                connectionId = PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId,
                blockageState = LocationConnectionBlockageState.Clear,
                worldTime = 21d
            });
            LocationConnectionOperationResult traversed = runtime.Traverse(Traversal(context, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", AccessContext(context, actor, offices: new[] { "office.prototype.guild-head" }, authorities: new[] { "permission.prototype.guild.rank-admin" })));
            bool placed = entities.TryGetActivePlacement(actor, out EntityPlacementSnapshot placement);
            bool valid = moveToGuild.Succeeded
                && locked.Status == LocationConnectionOperationStatus.MissingKey
                && entityBefore == entityAfterLocked
                && unlock.Succeeded
                && block.Succeeded
                && blocked.Status == LocationConnectionOperationStatus.DeniedByBlockage
                && clear.Succeeded
                && traversed.Succeeded
                && traversed.PlacementResult?.Succeeded == true
                && placed
                && placement.ExactLocationId == "location.prototype.guildmaster-office";
            return TestLabAssertions.True("step14-connection-state-gates", "Connection states gate traversal without partial movement", valid, $"Move={moveToGuild.Status} Locked={locked.Status} Unlock={unlock.Status} Block={block.Status} Blocked={blocked.Status} Clear={clear.Status} Traversed={traversed.Status} Placement={placement?.ExactLocationId}");
        }

        private static TestLabAutomationStepResult ConnectionAccessPolicyMatrix(TestLabAutomationContext context)
        {
            LocationConnectionRuntime runtime = ConnectionRuntime(context);
            EntityLocationReferenceData actor = Body(PrototypeEntityLocationFactory.PlayerBodyId, context);
            UnlockConnection(context, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId);
            UnlockConnection(context, PrototypeLocationConnectionDefinitionFactory.MayorOfficeConnectionId);
            UnlockConnection(context, PrototypeLocationConnectionDefinitionFactory.RecordsOfficeConnectionId);
            UnlockConnection(context, PrototypeLocationConnectionDefinitionFactory.GuildStorageConnectionId);
            UnlockConnection(context, PrototypeLocationConnectionDefinitionFactory.PrisonCellConnectionId);

            LocationConnectionAccessResult guildDenied = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", AccessContext(context, actor)));
            LocationConnectionAccessResult guildOffice = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", AccessContext(context, actor, offices: new[] { "office.prototype.guild-head" }, authorities: new[] { "permission.prototype.guild.rank-admin" })));
            LocationConnectionAccessResult mayor = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.MayorOfficeConnectionId, actor, "location.prototype.civic-office", "location.prototype.mayor-office", AccessContext(context, actor, offices: new[] { "office.prototype.mayor" }, authorities: new[] { "authority.government.prototype" })));
            LocationConnectionAccessResult recordsEmployment = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.RecordsOfficeConnectionId, actor, "location.prototype.civic-office", "location.prototype.mayor-office", AccessContext(context, actor, employments: new[] { "employment.prototype.records-clerk" })));
            LocationConnectionAccessResult recordsPermit = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.RecordsOfficeConnectionId, actor, "location.prototype.civic-office", "location.prototype.mayor-office", AccessContext(context, actor, permits: new[] { "legal-right.prototype.records.restricted-read" })));
            LocationConnectionAccessResult recordsWarrant = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.RecordsOfficeConnectionId, actor, "location.prototype.civic-office", "location.prototype.mayor-office", AccessContext(context, actor, warrants: new[] { "warrant.prototype.search" })));
            LocationConnectionAccessResult storageOwner = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.GuildStorageConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.merchant-counter", AccessContext(context, actor, properties: new[] { "property.prototype.guild-storage" })));
            LocationConnectionAccessResult storageKey = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.GuildStorageConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.merchant-counter", AccessContext(context, actor, keyDefinitions: new[] { "item.prototype-storage-key" })));
            LocationConnectionAccessResult custody = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.PrisonCellConnectionId, actor, "location.prototype.civic-office", "location.prototype.basement-prison", AccessContext(context, actor, custodyRoles: new[] { "custody-role.prototype.guard" })));

            bool valid = guildDenied.accessState == LocationConnectionAccessState.MissingAuthority
                && guildOffice.Allowed
                && mayor.Allowed
                && recordsEmployment.Allowed
                && recordsPermit.Allowed
                && recordsWarrant.Allowed
                && storageOwner.Allowed
                && storageKey.Allowed
                && custody.Allowed;
            return TestLabAssertions.True("step14-connection-access-matrix", "Access policies consume external references without owning external records", valid, $"GuildDenied={guildDenied.accessState} GuildOffice={guildOffice.accessState} Mayor={mayor.accessState} Employment={recordsEmployment.accessState} Permit={recordsPermit.accessState} Warrant={recordsWarrant.accessState} Owner={storageOwner.accessState} Key={storageKey.accessState} Custody={custody.accessState}");
        }

        private static TestLabAutomationStepResult ConnectionVisibilityOneWayGrants(TestLabAutomationContext context)
        {
            LocationConnectionRuntime runtime = ConnectionRuntime(context);
            EntityLocationRuntime entities = EntityRuntime(context);
            EntityLocationReferenceData actor = Body(PrototypeEntityLocationFactory.PlayerBodyId, context);
            bool hiddenOmitted = !runtime.GetOutgoingConnections("location.prototype.guildmaster-office").Any(item => item.ConnectionId == PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId);
            bool hiddenIncluded = runtime.GetOutgoingConnections("location.prototype.guildmaster-office", includeHidden: true).Any(item => item.ConnectionId == PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId);
            LocationConnectionAccessResult forward = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.DungeonOneWayDropConnectionId, actor, "location.prototype.wilderness-ring", "location.prototype.dungeon-entry", AccessContext(context, actor, privileged: true)));
            LocationConnectionAccessResult reverse = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.DungeonOneWayDropConnectionId, actor, "location.prototype.dungeon-entry", "location.prototype.wilderness-ring", AccessContext(context, actor, privileged: true)));
            EntityLocationOperationResult move = entities.Relocate(new EntityRelocationRequest
            {
                transactionId = Tx(context, "connection-hidden-move"),
                entity = actor,
                destinationLocationId = "location.prototype.guildmaster-office",
                worldTime = 30d
            });
            LocationConnectionAccessResult denied = runtime.EvaluateAccess(Traversal(context, PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId, actor, "location.prototype.guildmaster-office", "location.prototype.basement-prison", AccessContext(context, actor)));
            LocationConnectionOperationResult grant = runtime.GrantAccess(new LocationAccessGrantRequest
            {
                transactionId = Tx(context, "connection-hidden-grant"),
                grantId = context.ScenarioContext.ScopedId("location-access-grant.test", "hidden"),
                connectionId = PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId,
                grantee = actor,
                startWorldTime = 30d,
                endWorldTime = 40d
            });
            LocationConnectionOperationResult traverse = runtime.Traverse(Traversal(context, PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId, actor, "location.prototype.guildmaster-office", "location.prototype.basement-prison", AccessContext(context, actor), worldTime: 35d));
            bool valid = hiddenOmitted
                && hiddenIncluded
                && forward.Allowed
                && reverse.accessState == LocationConnectionAccessState.DeniedByDirection
                && move.Succeeded
                && !denied.Allowed
                && grant.Succeeded
                && traverse.Succeeded;
            return TestLabAssertions.True("step14-connection-visibility-grants", "Hidden, one-way, and explicit grant rules are authoritative", valid, $"Hidden={hiddenOmitted}/{hiddenIncluded} OneWay={forward.accessState}/{reverse.accessState} Move={move.Status} Denied={denied.accessState} Grant={grant.Status} Traverse={traverse.Status}");
        }

        private static TestLabAutomationStepResult ConnectionPersistenceValidation(TestLabAutomationContext context)
        {
            LocationConnectionRuntime runtime = ConnectionRuntime(context);
            LocationConnectionPersistenceParticipant participant = new LocationConnectionPersistenceParticipant(runtime, () => context.ScenarioContext.Runtimes.DefinitionRegistry, () => Runtime(context), () => EntityRuntime(context), () => InteractionRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            var prepared = participant.PreparePayload(save.PayloadJson, LocationConnectionPersistenceParticipant.CurrentParticipantSchemaVersion);
            LocationConnectionRuntime restored = new LocationConnectionRuntime();
            restored.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, Runtime(context), EntityRuntime(context), InteractionRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            LocationConnectionOperationResult restore = restored.RestoreFromSaveData(JsonUtility.FromJson<LocationConnectionRuntimeSaveData>(save.PayloadJson), Runtime(context), EntityRuntime(context), InteractionRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            LocationConnectionRuntimeSaveData before = runtime.CreateSaveData();
            LocationConnectionRuntimeSaveData corrupt = before.Clone();
            corrupt.connections[0].destinationLocationId = "location.prototype.missing";
            var rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), LocationConnectionPersistenceParticipant.CurrentParticipantSchemaVersion);
            bool unchanged = runtime.CreateSaveData().connections.Select(item => item.destinationLocationId).SequenceEqual(before.connections.Select(item => item.destinationLocationId));
            bool valid = save.Succeeded && prepared.Succeeded && restore.Succeeded && restored.ConnectionCount == runtime.ConnectionCount && !rejected.Succeeded && unchanged;
            return TestLabAssertions.True("step14-connection-persistence", "Persistence round-trips and rejects corrupt graphs before commit", valid, $"Save={save.Succeeded}:{save.Message} Prepare={prepared.Succeeded}:{prepared.Message} Restore={restore.Status} Rejected={rejected.Succeeded}:{rejected.Message} Unchanged={unchanged} Count={restored.ConnectionCount}/{runtime.ConnectionCount}");
        }

        private static TestLabAutomationStepResult ConnectionFixtureSnapshot(TestLabAutomationContext context)
        {
            LocationConnectionRuntime runtime = ConnectionRuntime(context);
            TestLabRuntimeBundleSnapshot snapshot = context.ScenarioContext.Runtimes.CreateSnapshot();
            int before = runtime.ConnectionCount;
            LocationConnectionOperationResult created = CreateConnection(context, "fixture", "location.prototype.adventurers-guild", "location.prototype.merchant-counter");
            bool restored = context.ScenarioContext.Runtimes.RestoreSnapshot(snapshot, out string failure);
            bool missing = !runtime.TryGetConnection(created.Connection?.ConnectionId, out _);
            bool valid = created.Succeeded && restored && missing && runtime.ConnectionCount == before;
            return TestLabAssertions.True("step14-connection-fixture", "Fixture snapshot restores connection mutations", valid, $"Created={created.Status} Restored={restored} Missing={missing} Count={runtime.ConnectionCount}/{before} Failure={failure}");
        }

        private static TestLabAutomationStepResult RouteReadiness(TestLabAutomationContext context)
        {
            LocationRouteRuntime runtime = RouteRuntime(context);
            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool hasWalking = registry.TryGet(PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId, out TravelModeDefinition walking);
            bool hasStreet = registry.TryGet(PrototypeLocationRouteDefinitionFactory.StreetSegmentDefinitionId, out RouteSegmentDefinition street);
            bool hasSeed = runtime.TryGetSegment(PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId, out LocationRouteSegmentSnapshot seed);
            bool hasNetwork = runtime.TryGetNetwork(PrototypeLocationRouteDefinitionFactory.VillageStreetNetworkId, out LocationRouteNetworkSnapshot network);
            LocationRouteSearchResult plan = runtime.PlanRoute(RouteRequest(context, "location.prototype.village", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess));
            bool usesSeed = plan.Plan?.Steps.Any(step => step.EdgeKind == RouteEdgeKind.RouteSegment && step.EdgeId == PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId) == true;
            bool valid = runtime != null && hasWalking && hasStreet && hasSeed && hasNetwork && plan.Succeeded && usesSeed;
            return TestLabAssertions.True("step14-route-readiness", "Route definitions and seeded graph are available", valid, $"Walking={hasWalking}:{walking?.Id} Street={hasStreet}:{street?.Id} Seed={hasSeed}:{seed?.SegmentId} Network={hasNetwork}:{network?.NetworkId} Plan={plan.Status} Edges={plan.Plan?.EdgeCount ?? 0}");
        }

        private static TestLabAutomationStepResult RouteMultiEdgePlanning(TestLabAutomationContext context)
        {
            LocationRouteRuntime runtime = RouteRuntime(context);
            LocationRouteSearchResult plan = runtime.PlanRoute(RouteRequest(context, "location.prototype.village", "location.prototype.merchant-counter", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess));
            bool includesRoute = plan.Plan?.Steps.Any(step => step.EdgeKind == RouteEdgeKind.RouteSegment) == true;
            bool includesConnection = plan.Plan?.Steps.Any(step => step.EdgeKind == RouteEdgeKind.LocalConnection) == true;
            bool valid = plan.Succeeded && includesRoute && includesConnection && plan.Plan.EdgeCount >= 2 && plan.Plan.OrderedLocationIds.SequenceEqual(plan.Plan.OrderedLocationIds.Distinct(StringComparer.Ordinal));
            return TestLabAssertions.True("step14-route-multi-edge", "Route plans compose route segments and existing connection edges", valid, $"Status={plan.Status} Edges={plan.Plan?.EdgeCount ?? 0} Route={includesRoute} Connection={includesConnection} Distance={plan.Plan?.TotalDistance.meters ?? 0}");
        }

        private static TestLabAutomationStepResult RouteObjectivesTieBreaks(TestLabAutomationContext context)
        {
            LocationRouteRuntime runtime = RouteRuntime(context);
            LocationRouteMutationResult created = CreateRouteSegment(context, "long-cheap", "location.prototype.village", "location.prototype.market-district", 180d, 5d);
            LocationRouteSearchResult shortest = runtime.PlanRoute(RouteRequest(context, "location.prototype.village", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess, objective: RoutePlanningObjective.ShortestDistance));
            LocationRouteSearchResult cheapest = runtime.PlanRoute(RouteRequest(context, "location.prototype.village", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess, objective: RoutePlanningObjective.LowestCost));
            LocationRouteSearchResult cheapestAgain = runtime.PlanRoute(RouteRequest(context, "location.prototype.village", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess, objective: RoutePlanningObjective.LowestCost));
            bool deterministic = cheapest.Plan?.PlanId == cheapestAgain.Plan?.PlanId;
            bool valid = created.Succeeded && shortest.Succeeded && cheapest.Succeeded && deterministic
                && shortest.Plan.Steps[0].EdgeId == PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId
                && cheapest.Plan.Steps[0].EdgeId == created.Segment.SegmentId;
            return TestLabAssertions.True("step14-route-objectives", "Planning objectives are deterministic across parallel edges", valid, $"Created={created.Status} Shortest={shortest.Plan?.Steps.FirstOrDefault()?.EdgeId} Cheapest={cheapest.Plan?.Steps.FirstOrDefault()?.EdgeId} Deterministic={deterministic}");
        }

        private static TestLabAutomationStepResult RouteAccessAndUnlockableEdges(TestLabAutomationContext context)
        {
            LocationRouteRuntime runtime = RouteRuntime(context);
            EntityLocationReferenceData actor = Person(PrototypeEntityLocationFactory.PlayerPersonId, context);
            LocationConnectionAccessContextData authorized = AccessContext(context, actor, offices: new[] { "office.prototype.guild-head" }, authorities: new[] { "permission.prototype.guild.rank-admin" });
            LocationRouteSearchResult denied = runtime.PlanRoute(RouteRequest(context, "location.prototype.village", "location.prototype.guildmaster-office", RouteAccessEvaluationMode.RequireCurrentAccess, authorized));
            LocationRouteSearchResult unlockable = runtime.PlanRoute(RouteRequest(context, "location.prototype.village", "location.prototype.guildmaster-office", RouteAccessEvaluationMode.PermitUnlockableConnections, authorized));
            bool connectionUnchanged = ConnectionRuntime(context).TryGetConnection(PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, out LocationConnectionSnapshot connection) && connection.OpenState == LocationConnectionOpenState.Closed && connection.LockState == LocationConnectionLockState.Locked;
            bool hasRequiredActions = unlockable.Plan?.Requirements.requiredActions.Any(action => action.StartsWith("open:", StringComparison.Ordinal) || action.StartsWith("unlock:", StringComparison.Ordinal)) == true;
            bool valid = !denied.Succeeded && unlockable.Succeeded && hasRequiredActions && connectionUnchanged;
            return TestLabAssertions.True("step14-route-access", "Connection access gates participate without mutating connection state", valid, $"Denied={denied.Status} Unlockable={unlockable.Status} Actions={string.Join(",", unlockable.Plan?.Requirements.requiredActions ?? Array.Empty<string>())} Unchanged={connectionUnchanged}");
        }

        private static TestLabAutomationStepResult RouteKnowledgeSafeHiddenRoutes(TestLabAutomationContext context)
        {
            LocationRouteRuntime runtime = RouteRuntime(context);
            LocationRouteSearchResult hidden = runtime.PlanRoute(RouteRequest(context, "location.prototype.guildmaster-office", "location.prototype.basement-prison", accessMode: RouteAccessEvaluationMode.IgnoreTravelerAccessDevelopment, includeHidden: true));
            LocationRouteSearchRequest safeRequest = RouteRequest(context, "location.prototype.guildmaster-office", "location.prototype.basement-prison", accessMode: RouteAccessEvaluationMode.KnowledgeSafeCurrentAccess);
            safeRequest.knowledgeMode = RouteKnowledgeMode.PublicKnownOnly;
            LocationRouteSearchResult filtered = runtime.PlanRoute(safeRequest);
            safeRequest.knowledgeMode = RouteKnowledgeMode.KnownToTraveler;
            safeRequest.knownEdgeIds = new[] { PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId };
            safeRequest.includeHiddenDevelopmentRoutes = false;
            safeRequest.accessContext = AccessContext(context, Person(PrototypeEntityLocationFactory.PlayerPersonId, context), privileged: true);
            LocationRouteSearchResult known = runtime.PlanRoute(safeRequest);
            bool valid = hidden.Succeeded && !filtered.Succeeded && filtered.Status == RoutePlanningStatus.UnknownUnderKnowledgeView && known.Succeeded;
            return TestLabAssertions.True("step14-route-knowledge", "Knowledge-safe planning filters hidden routes without leaking counts", valid, $"Hidden={hidden.Status} Filtered={filtered.Status} Known={known.Status} FilterExpanded={filtered.ExpandedEdgeCount}");
        }

        private static TestLabAutomationStepResult RouteStalePlanRevalidation(TestLabAutomationContext context)
        {
            LocationRouteRuntime runtime = RouteRuntime(context);
            LocationRouteSearchResult plan = runtime.PlanRoute(RouteRequest(context, "location.prototype.village", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess));
            LocationRouteMutationResult mutate = runtime.MutateSegment(new LocationRouteSegmentMutationRequest
            {
                transactionId = Tx(context, "route-block-market"),
                segmentId = PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId,
                blockageState = RouteSegmentBlockageState.TemporarilyBlocked,
                worldTime = 25d
            });
            LocationRouteRevalidationResult revalidate = runtime.RevalidatePlan(plan.Plan, RouteRequest(context, "location.prototype.village", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess));
            bool immutable = plan.Plan?.Steps.FirstOrDefault()?.EdgeId == PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId;
            bool valid = plan.Succeeded && mutate.Succeeded && immutable && revalidate.Status == RoutePlanRevalidationStatus.ChangedAccess;
            return TestLabAssertions.True("step14-route-revalidation", "Route plans are immutable and revalidate against graph revisions", valid, $"Plan={plan.Status} Mutate={mutate.Status} Revalidate={revalidate.Status} Immutable={immutable}");
        }

        private static TestLabAutomationStepResult RoutePersistenceValidation(TestLabAutomationContext context)
        {
            LocationRouteRuntime runtime = RouteRuntime(context);
            LocationRoutePersistenceParticipant participant = new LocationRoutePersistenceParticipant(runtime, () => context.ScenarioContext.Runtimes.DefinitionRegistry, () => Runtime(context), () => ConnectionRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(save.PayloadJson, LocationRoutePersistenceParticipant.CurrentParticipantSchemaVersion);
            LocationRouteRuntime restored = new LocationRouteRuntime();
            restored.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, Runtime(context), ConnectionRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            LocationRouteMutationResult restore = restored.RestoreFromSaveData(JsonUtility.FromJson<LocationRouteRuntimeSaveData>(save.PayloadJson), Runtime(context), ConnectionRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            LocationRouteRuntimeSaveData before = runtime.CreateSaveData();
            LocationRouteRuntimeSaveData corrupt = before.Clone();
            corrupt.segments[0].destinationLocationId = "location.prototype.missing";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), LocationRoutePersistenceParticipant.CurrentParticipantSchemaVersion);
            bool unchanged = runtime.CreateSaveData().segments.Select(item => item.destinationLocationId).SequenceEqual(before.segments.Select(item => item.destinationLocationId));
            bool valid = save.Succeeded && prepared.Succeeded && restore.Succeeded && restored.SegmentCount == runtime.SegmentCount && !rejected.Succeeded && unchanged;
            return TestLabAssertions.True("step14-route-persistence", "Route persistence round-trips and rejects corrupt graphs before commit", valid, $"Save={save.Succeeded}:{save.Message} Prepare={prepared.Succeeded}:{prepared.Message} Restore={restore.Status} Rejected={rejected.Succeeded}:{rejected.Message} Unchanged={unchanged} Count={restored.SegmentCount}/{runtime.SegmentCount}");
        }

        private static TestLabAutomationStepResult RouteFixtureSnapshot(TestLabAutomationContext context)
        {
            LocationRouteRuntime runtime = RouteRuntime(context);
            TestLabRuntimeBundleSnapshot snapshot = context.ScenarioContext.Runtimes.CreateSnapshot();
            int before = runtime.SegmentCount;
            LocationRouteMutationResult created = CreateRouteSegment(context, "fixture", "location.prototype.market-district", "location.prototype.civic-office", 45d, 45d);
            bool restored = context.ScenarioContext.Runtimes.RestoreSnapshot(snapshot, out string failure);
            bool missing = !runtime.TryGetSegment(created.Segment?.SegmentId, out _);
            bool valid = created.Succeeded && restored && missing && runtime.SegmentCount == before;
            return TestLabAssertions.True("step14-route-fixture", "Fixture snapshot restores route mutations", valid, $"Created={created.Status} Restored={restored} Missing={missing} Count={runtime.SegmentCount}/{before} Failure={failure}");
        }

        private static TestLabAutomationStepResult JourneyReadinessAndCreate(TestLabAutomationContext context)
        {
            TravelJourneyRuntime runtime = JourneyRuntime(context);
            EntityLocationReferenceData traveler = Body(PrototypeEntityLocationFactory.PlayerBodyId, context);
            LocationRouteSearchResult plan = RouteRuntime(context).PlanRoute(RouteRequest(context, "location.prototype.village", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess, traveler: traveler));
            TravelJourneyOperationResult created = CreateJourney(context, "create", "location.prototype.market-district", acceptedRoutePlan: plan.Plan);
            bool exactStillOrigin = EntityRuntime(context).TryGetActivePlacement(Body(PrototypeEntityLocationFactory.PlayerBodyId, context), out EntityPlacementSnapshot placement)
                && placement.ExactLocationId == "location.prototype.village";
            string validationFailure = string.Empty;
            bool valid = runtime != null
                && plan.Succeeded
                && created.Succeeded
                && created.Journey?.LifecycleState == TravelJourneyLifecycleState.Ready
                && created.Journey.Steps.Count == plan.Plan.EdgeCount
                && exactStillOrigin
                && runtime.ValidateCurrent(out validationFailure);
            return TestLabAssertions.True("step14-journey-create", "Journey runtime creates ready journeys from accepted route plans", valid, $"Plan={plan.Status} Create={created.Status} Steps={created.Journey?.Steps.Count ?? 0}/{plan.Plan?.EdgeCount ?? 0} ExactOrigin={exactStillOrigin} Validation={validationFailure}");
        }

        private static TestLabAutomationStepResult JourneyStartDoesNotTeleport(TestLabAutomationContext context)
        {
            TravelJourneyOperationResult created = CreateJourney(context, "start", "location.prototype.market-district");
            TravelJourneyOperationResult started = JourneyRuntime(context).StartJourney(Lifecycle(context, created.Journey?.JourneyId, "start", worldTime: 22d));
            bool exactOrigin = EntityRuntime(context).TryGetActivePlacement(Body(PrototypeEntityLocationFactory.PlayerBodyId, context), out EntityPlacementSnapshot placement)
                && placement.ExactLocationId == "location.prototype.village";
            bool valid = created.Succeeded && started.Succeeded && started.Journey?.LifecycleState == TravelJourneyLifecycleState.Active && exactOrigin;
            return TestLabAssertions.True("step14-journey-start", "Starting a journey does not mutate exact placement", valid, $"Create={created.Status} Start={started.Status} State={started.Journey?.LifecycleState} Exact={placement?.ExactLocationId}");
        }

        private static TestLabAutomationStepResult JourneyDeterministicProgress(TestLabAutomationContext context)
        {
            TravelJourneyRuntime runtime = JourneyRuntime(context);
            TravelJourneyOperationResult created = CreateJourney(context, "progress", "location.prototype.market-district", rate: 500d);
            TravelJourneyOperationResult started = runtime.StartJourney(Lifecycle(context, created.Journey?.JourneyId, "progress-start", worldTime: 10d, rate: 500d));
            TravelJourneyOperationResult preview = runtime.AdvanceJourney(Lifecycle(context, created.Journey?.JourneyId, "progress-preview", worldTime: 10.1d, rate: 500d, preview: true));
            TravelJourneyOperationResult arrived = runtime.AdvanceJourney(Lifecycle(context, created.Journey?.JourneyId, "progress-arrive", worldTime: 11d, rate: 500d));
            TravelJourneyOperationResult duplicateBoundary = runtime.AdvanceJourney(Lifecycle(context, created.Journey?.JourneyId, "progress-duplicate", worldTime: 11d, rate: 500d));
            bool exactDestination = EntityRuntime(context).TryGetActivePlacement(Body(PrototypeEntityLocationFactory.PlayerBodyId, context), out EntityPlacementSnapshot placement)
                && placement.ExactLocationId == "location.prototype.market-district";
            bool valid = created.Succeeded
                && started.Succeeded
                && preview.Preview
                && arrived.Succeeded
                && arrived.Journey?.LifecycleState == TravelJourneyLifecycleState.Completed
                && !duplicateBoundary.Succeeded
                && duplicateBoundary.Status == TravelJourneyMutationStatus.InvalidLifecycle
                && exactDestination;
            return TestLabAssertions.True("step14-journey-progress", "Route-segment progress is deterministic by world time", valid, $"Create={created.Status} Start={started.Status} Preview={preview.Status} Arrive={arrived.Status}:{arrived.Journey?.LifecycleState} Repeat={duplicateBoundary.Status} Exact={placement?.ExactLocationId}");
        }

        private static TestLabAutomationStepResult JourneyLocalConnectionStep(TestLabAutomationContext context)
        {
            TravelJourneyRuntime runtime = JourneyRuntime(context);
            TravelJourneyOperationResult created = CreateJourney(context, "connection", "location.prototype.merchant-counter", rate: 500d);
            TravelJourneyOperationResult started = runtime.StartJourney(Lifecycle(context, created.Journey?.JourneyId, "connection-start", worldTime: 10d, rate: 500d));
            long connectionRevisionBefore = ConnectionRuntime(context).Revision;
            TravelJourneyOperationResult advanced = runtime.AdvanceJourney(Lifecycle(context, created.Journey?.JourneyId, "connection-arrive", worldTime: 11d, rate: 500d));
            bool exactDestination = EntityRuntime(context).TryGetActivePlacement(Body(PrototypeEntityLocationFactory.PlayerBodyId, context), out EntityPlacementSnapshot placement)
                && placement.ExactLocationId == "location.prototype.merchant-counter";
            bool hasConnection = advanced.Journey?.Steps.Any(step => step.EdgeKind == RouteEdgeKind.LocalConnection && step.LifecycleState == TravelJourneyStepLifecycleState.Completed) == true;
            bool connectionUsed = ConnectionRuntime(context).Revision > connectionRevisionBefore;
            bool valid = created.Succeeded && started.Succeeded && advanced.Succeeded && advanced.Journey?.LifecycleState == TravelJourneyLifecycleState.Completed && exactDestination && hasConnection && connectionUsed;
            return TestLabAssertions.True("step14-journey-connection", "Local connection steps use connection traversal authority", valid, $"Create={created.Status} Start={started.Status} Advance={advanced.Status}:{advanced.Journey?.LifecycleState} Exact={placement?.ExactLocationId} Local={hasConnection} ConnectionRevision={connectionRevisionBefore}->{ConnectionRuntime(context).Revision}");
        }

        private static TestLabAutomationStepResult JourneyPauseResumeCancel(TestLabAutomationContext context)
        {
            TravelJourneyRuntime runtime = JourneyRuntime(context);
            TravelJourneyOperationResult created = CreateJourney(context, "lifecycle", "location.prototype.market-district", rate: 5d);
            TravelJourneyOperationResult started = runtime.StartJourney(Lifecycle(context, created.Journey?.JourneyId, "lifecycle-start", worldTime: 10d, rate: 5d));
            TravelJourneyOperationResult partial = runtime.AdvanceJourney(Lifecycle(context, created.Journey?.JourneyId, "lifecycle-partial", worldTime: 11d, rate: 5d));
            TravelJourneyOperationResult paused = runtime.PauseJourney(Lifecycle(context, created.Journey?.JourneyId, "lifecycle-pause", worldTime: 12d));
            TravelJourneyOperationResult blockedAdvance = runtime.AdvanceJourney(Lifecycle(context, created.Journey?.JourneyId, "lifecycle-paused-advance", worldTime: 13d, rate: 5d));
            TravelJourneyOperationResult resumed = runtime.ResumeJourney(Lifecycle(context, created.Journey?.JourneyId, "lifecycle-resume", worldTime: 14d, rate: 5d));
            TravelJourneyOperationResult cancelled = runtime.CancelJourney(Lifecycle(context, created.Journey?.JourneyId, "lifecycle-cancel", worldTime: 15d));
            bool exactOrigin = EntityRuntime(context).TryGetActivePlacement(Body(PrototypeEntityLocationFactory.PlayerBodyId, context), out EntityPlacementSnapshot placement)
                && placement.ExactLocationId == "location.prototype.village";
            bool valid = created.Succeeded
                && started.Succeeded
                && partial.Succeeded
                && partial.Journey?.LifecycleState == TravelJourneyLifecycleState.Active
                && paused.Succeeded
                && paused.Journey?.LifecycleState == TravelJourneyLifecycleState.Paused
                && !blockedAdvance.Succeeded
                && resumed.Succeeded
                && cancelled.Succeeded
                && cancelled.Journey?.LifecycleState == TravelJourneyLifecycleState.Cancelled
                && exactOrigin;
            return TestLabAssertions.True("step14-journey-lifecycle", "Pause, resume, and cancel preserve placement and lifecycle history", valid, $"Create={created.Status} Start={started.Status} Partial={partial.Status} Pause={paused.Status} AdvancePaused={blockedAdvance.Status} Resume={resumed.Status} Cancel={cancelled.Status}:{cancelled.Journey?.LifecycleState} Exact={placement?.ExactLocationId}");
        }

        private static TestLabAutomationStepResult JourneyBlockAndReplan(TestLabAutomationContext context)
        {
            TravelJourneyRuntime runtime = JourneyRuntime(context);
            TravelJourneyOperationResult created = CreateJourney(context, "replan", "location.prototype.market-district", rate: 5d);
            TravelJourneyOperationResult started = runtime.StartJourney(Lifecycle(context, created.Journey?.JourneyId, "replan-start", worldTime: 10d, rate: 5d));
            LocationRouteMutationResult blockedSegment = RouteRuntime(context).MutateSegment(new LocationRouteSegmentMutationRequest
            {
                transactionId = Tx(context, "journey-block-market-segment"),
                segmentId = PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId,
                blockageState = RouteSegmentBlockageState.TemporarilyBlocked,
                worldTime = 11d
            });
            TravelJourneyOperationResult blocked = runtime.AdvanceJourney(Lifecycle(context, created.Journey?.JourneyId, "replan-blocked-advance", worldTime: 12d, rate: 5d));
            LocationRouteMutationResult replacement = CreateRouteSegment(context, "journey-alt-market", "location.prototype.village", "location.prototype.market-district", 95d, 30d);
            TravelJourneyOperationResult replanned = runtime.ReplanJourney(new TravelJourneyReplanRequest
            {
                transactionId = Tx(context, "journey-replan"),
                journeyId = created.Journey?.JourneyId,
                destinationLocationId = "location.prototype.market-district",
                accessContext = AccessContext(context, Body(PrototypeEntityLocationFactory.PlayerBodyId, context)),
                worldTime = 13d,
                movementRateOverrideMetersPerSecond = 5d
            });
            bool exactOrigin = EntityRuntime(context).TryGetActivePlacement(Body(PrototypeEntityLocationFactory.PlayerBodyId, context), out EntityPlacementSnapshot placement)
                && placement.ExactLocationId == "location.prototype.village";
            bool valid = created.Succeeded
                && started.Succeeded
                && blockedSegment.Succeeded
                && !blocked.Succeeded
                && blocked.Status == TravelJourneyMutationStatus.Blocked
                && replacement.Succeeded
                && replanned.Succeeded
                && replanned.Journey?.LifecycleState == TravelJourneyLifecycleState.Active
                && replanned.Journey.ReplanCount == 1
                && exactOrigin;
            return TestLabAssertions.True("step14-journey-replan", "Stale blocked routes block travel and can replan from current placement", valid, $"Create={created.Status} Start={started.Status} BlockSegment={blockedSegment.Status} Block={blocked.Status} Replacement={replacement.Status} Replan={replanned.Status}:{replanned.Journey?.LifecycleState} Count={replanned.Journey?.ReplanCount ?? 0} Exact={placement?.ExactLocationId}");
        }

        private static TestLabAutomationStepResult JourneyProjectionBoundaries(TestLabAutomationContext context)
        {
            TravelJourneyRuntime runtime = JourneyRuntime(context);
            TravelJourneyOperationResult created = CreateJourney(context, "projection", "location.prototype.market-district", rate: 5d, visibility: TravelJourneyVisibility.Hidden);
            TravelJourneyOperationResult started = runtime.StartJourney(Lifecycle(context, created.Journey?.JourneyId, "projection-start", worldTime: 10d, rate: 5d));
            TravelJourneyOperationResult partial = runtime.AdvanceJourney(Lifecycle(context, created.Journey?.JourneyId, "projection-partial", worldTime: 11d, rate: 5d));
            TravelJourneyPhysicalContextResult physical = runtime.GetPhysicalContext(created.Journey?.JourneyId, 11d);
            TravelJourneySnapshot denied = runtime.GetProjection(new TravelJourneyProjectionRequest { journeyId = created.Journey?.JourneyId, requester = Person(PrototypeEntityLocationFactory.PlayerPersonId, context) });
            TravelJourneySnapshot redacted = runtime.GetProjection(new TravelJourneyProjectionRequest { journeyId = created.Journey?.JourneyId, requester = Person(PrototypeEntityLocationFactory.PlayerPersonId, context), includeHidden = true });
            TravelJourneySnapshot privileged = runtime.GetProjection(new TravelJourneyProjectionRequest { journeyId = created.Journey?.JourneyId, requester = Person(PrototypeEntityLocationFactory.PlayerPersonId, context), privileged = true });
            bool exactOrigin = physical?.ExactPlacement?.ExactLocationId == "location.prototype.village";
            bool valid = created.Succeeded
                && started.Succeeded
                && partial.Succeeded
                && physical != null
                && physical.InTransit
                && physical.NextLocationId == "location.prototype.market-district"
                && exactOrigin
                && denied == null
                && redacted != null
                && string.IsNullOrWhiteSpace(redacted.DestinationLocationId)
                && redacted.Steps.Count == 0
                && privileged?.DestinationLocationId == "location.prototype.market-district";
            return TestLabAssertions.True("step14-journey-projection", "Journey projections expose in-transit context without leaking hidden details", valid, $"Create={created.Status} Start={started.Status} Partial={partial.Status} InTransit={physical?.InTransit} Exact={physical?.ExactPlacement?.ExactLocationId} Denied={denied == null} RedactedSteps={redacted?.Steps.Count ?? -1} Privileged={privileged?.DestinationLocationId}");
        }

        private static TestLabAutomationStepResult JourneyPersistenceValidation(TestLabAutomationContext context)
        {
            TravelJourneyRuntime runtime = JourneyRuntime(context);
            TravelJourneyOperationResult created = CreateJourney(context, "persistence", "location.prototype.market-district", rate: 5d);
            runtime.StartJourney(Lifecycle(context, created.Journey?.JourneyId, "persistence-start", worldTime: 10d, rate: 5d));
            runtime.AdvanceJourney(Lifecycle(context, created.Journey?.JourneyId, "persistence-partial", worldTime: 11d, rate: 5d));
            TravelJourneyPersistenceParticipant participant = new TravelJourneyPersistenceParticipant(runtime, () => context.ScenarioContext.Runtimes.DefinitionRegistry, () => Runtime(context), () => EntityRuntime(context), () => ConnectionRuntime(context), () => RouteRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(save.PayloadJson, TravelJourneyPersistenceParticipant.CurrentParticipantSchemaVersion);
            TravelJourneyRuntime restored = new TravelJourneyRuntime();
            restored.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, Runtime(context), EntityRuntime(context), ConnectionRuntime(context), RouteRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            TravelJourneyOperationResult restore = restored.RestoreFromSaveData(JsonUtility.FromJson<TravelJourneyRuntimeSaveData>(save.PayloadJson), context.ScenarioContext.Runtimes.DefinitionRegistry, Runtime(context), EntityRuntime(context), ConnectionRuntime(context), RouteRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            TravelJourneyRuntimeSaveData before = runtime.CreateSaveData();
            TravelJourneyRuntimeSaveData corrupt = before.Clone();
            corrupt.journeys[0].destinationLocationId = "location.prototype.missing";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), TravelJourneyPersistenceParticipant.CurrentParticipantSchemaVersion);
            bool unchanged = runtime.CreateSaveData().journeys.Select(item => item.destinationLocationId).SequenceEqual(before.journeys.Select(item => item.destinationLocationId));
            bool valid = created.Succeeded && save.Succeeded && prepared.Succeeded && restore.Succeeded && restored.JourneyCount == runtime.JourneyCount && !rejected.Succeeded && unchanged;
            return TestLabAssertions.True("step14-journey-persistence", "Journey persistence round-trips and rejects corrupt graphs before commit", valid, $"Create={created.Status} Save={save.Succeeded}:{save.Message} Prepare={prepared.Succeeded}:{prepared.Message} Restore={restore.Status} Rejected={rejected.Succeeded}:{rejected.Message} Unchanged={unchanged} Count={restored.JourneyCount}/{runtime.JourneyCount}");
        }

        private static TestLabAutomationStepResult JourneyFixtureSnapshot(TestLabAutomationContext context)
        {
            TravelJourneyRuntime runtime = JourneyRuntime(context);
            TestLabRuntimeBundleSnapshot snapshot = context.ScenarioContext.Runtimes.CreateSnapshot();
            int before = runtime.JourneyCount;
            TravelJourneyOperationResult created = CreateJourney(context, "fixture", "location.prototype.market-district");
            bool restored = context.ScenarioContext.Runtimes.RestoreSnapshot(snapshot, out string failure);
            bool missing = !runtime.TryGetJourney(created.Journey?.JourneyId, out _);
            bool valid = created.Succeeded && restored && missing && runtime.JourneyCount == before;
            return TestLabAssertions.True("step14-journey-fixture", "Fixture snapshots restore journey mutations", valid, $"Created={created.Status} Restored={restored} Missing={missing} Count={runtime.JourneyCount}/{before} Failure={failure}");
        }

        private static TestLabAutomationStepResult TravelConditionReadiness(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool condition = registry.TryGet(PrototypeTravelConditionDefinitionFactory.MuddyRoadConditionId, out TravelConditionDefinition muddy);
            bool hazard = registry.TryGet(PrototypeTravelConditionDefinitionFactory.HeatExposureHazardId, out TravelHazardDefinition heat);
            bool encounter = registry.TryGet(PrototypeTravelConditionDefinitionFactory.HiddenAmbushEncounterId, out TravelEncounterDefinition ambush);
            TravelConditionRuntime runtime = ConditionRuntime(context);
            string failure = runtime == null ? "Missing TravelConditionRuntime." : string.Empty;
            bool validRuntime = runtime != null && runtime.ValidateCurrent(out failure);
            bool valid = condition && hazard && encounter && muddy.MovementRateMultiplier < 1d && heat.TriggerPolicy == TravelHazardTriggerPolicy.ExplicitOnly && ambush.InterruptionPolicy == TravelEncounterInterruptionPolicy.BlockJourney && validRuntime;
            return TestLabAssertions.True("step14-travel-condition-readiness", "Travel condition definitions and runtime resolve", valid, $"Condition={condition} Hazard={hazard} Encounter={encounter} Runtime={validRuntime}:{failure}");
        }

        private static TestLabAutomationStepResult TravelConditionRouteModifier(TestLabAutomationContext context)
        {
            TravelConditionRuntime conditions = ConditionRuntime(context);
            LocationRouteSearchResult baseline = RouteRuntime(context).PlanRoute(RouteRequest(context, "location.prototype.village", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess));
            TravelConditionOperationResult created = CreateRouteCondition(context, conditions, "muddy-route", PrototypeTravelConditionDefinitionFactory.MuddyRoadConditionId, PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId);
            LocationRouteSearchRequest request = RouteRequest(context, "location.prototype.village", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess);
            request.conditionEvaluationMode = TravelConditionEvaluationMode.CurrentConditions;
            LocationRouteSearchResult modified = RouteRuntime(context).PlanRoute(request);
            bool valid = baseline.Succeeded && created.Succeeded && modified.Succeeded && modified.Plan.TotalCost.units > baseline.Plan.TotalCost.units && modified.Plan.TotalDistance.meters > baseline.Plan.TotalDistance.meters;
            return TestLabAssertions.True("step14-travel-condition-route-modifier", "Route planning applies condition movement and cost modifiers", valid, $"Baseline={baseline.Plan?.TotalCost.units:0.###}/{baseline.Plan?.TotalDistance.meters:0.###} Modified={modified.Plan?.TotalCost.units:0.###}/{modified.Plan?.TotalDistance.meters:0.###} Create={created.Status}");
        }

        private static TestLabAutomationStepResult TravelConditionHardBlockRevalidation(TestLabAutomationContext context)
        {
            TravelConditionRuntime conditions = ConditionRuntime(context);
            LocationRouteSearchRequest request = RouteRequest(context, "location.prototype.village", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess);
            request.conditionEvaluationMode = TravelConditionEvaluationMode.CurrentConditions;
            LocationRouteSearchResult before = RouteRuntime(context).PlanRoute(request);
            TravelConditionOperationResult block = CreateRouteCondition(context, conditions, "collapsed", PrototypeTravelConditionDefinitionFactory.CollapsedPassConditionId, PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId);
            LocationRouteSearchResult after = RouteRuntime(context).PlanRoute(request);
            LocationRouteRevalidationResult revalidate = RouteRuntime(context).RevalidatePlan(before.Plan, request);
            bool valid = before.Succeeded && block.Succeeded && !after.Succeeded && after.Status == RoutePlanningStatus.NoRoute && revalidate.Status == RoutePlanRevalidationStatus.ChangedAccess;
            return TestLabAssertions.True("step14-travel-condition-hard-block", "Hard blockers invalidate condition-aware routes", valid, $"Before={before.Status} Block={block.Status} After={after.Status} Revalidate={revalidate.Status}:{revalidate.Message}");
        }

        private static TestLabAutomationStepResult TravelConditionRequirements(TestLabAutomationContext context)
        {
            TravelConditionRuntime conditions = ConditionRuntime(context);
            TravelConditionOperationResult created = CreateRouteCondition(context, conditions, "climb-required", PrototypeTravelConditionDefinitionFactory.ClimbingRequiredConditionId, PrototypeLocationRouteDefinitionFactory.VillageWildernessTrailSegmentId);
            TravelConditionEvaluationResult missing = conditions.Evaluate(new TravelConditionEvaluationRequest { evaluationMode = TravelConditionEvaluationMode.CurrentConditions, target = RouteTarget(PrototypeLocationRouteDefinitionFactory.VillageWildernessTrailSegmentId, context), traveler = Body(PrototypeEntityLocationFactory.PlayerBodyId, context), travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId, worldTime = 12d });
            TravelConditionEvaluationResult allowed = conditions.Evaluate(new TravelConditionEvaluationRequest { evaluationMode = TravelConditionEvaluationMode.CurrentConditions, target = RouteTarget(PrototypeLocationRouteDefinitionFactory.VillageWildernessTrailSegmentId, context), traveler = Body(PrototypeEntityLocationFactory.PlayerBodyId, context), travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId, travelerCapabilityIds = new[] { PrototypeTravelConditionDefinitionFactory.ClimbCapabilityId }, worldTime = 12d });
            bool noMutation = conditions.Revision == created.RevisionAfter;
            bool valid = created.Succeeded && missing.HardBlocked && missing.MissingCapabilityIds.Contains(PrototypeTravelConditionDefinitionFactory.ClimbCapabilityId) && !allowed.HardBlocked && noMutation;
            return TestLabAssertions.True("step14-travel-condition-requirements", "Requirements block without mutating condition state", valid, $"Create={created.Status} Missing={missing.HardBlocked}:{string.Join(",", missing.MissingCapabilityIds)} Allowed={allowed.HardBlocked} Revision={conditions.Revision}/{created.RevisionAfter}");
        }

        private static TestLabAutomationStepResult TravelConditionHiddenKnowledgeSafety(TestLabAutomationContext context)
        {
            TravelConditionRuntime conditions = ConditionRuntime(context);
            TravelConditionOperationResult created = CreateRouteCondition(context, conditions, "hidden-ambush", PrototypeTravelConditionDefinitionFactory.HiddenAmbushRiskConditionId, PrototypeLocationRouteDefinitionFactory.VillageWildernessTrailSegmentId);
            TravelConditionEvaluationResult safe = conditions.Evaluate(new TravelConditionEvaluationRequest { evaluationMode = TravelConditionEvaluationMode.KnowledgeSafeCurrentConditions, target = RouteTarget(PrototypeLocationRouteDefinitionFactory.VillageWildernessTrailSegmentId, context), travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId, worldTime = 10d });
            TravelConditionEvaluationResult known = conditions.Evaluate(new TravelConditionEvaluationRequest { evaluationMode = TravelConditionEvaluationMode.KnowledgeSafeCurrentConditions, target = RouteTarget(PrototypeLocationRouteDefinitionFactory.VillageWildernessTrailSegmentId, context), travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId, knownConditionIds = new[] { created.Condition.ConditionId }, knownEncounterIds = new[] { PrototypeTravelConditionDefinitionFactory.HiddenAmbushEncounterId }, worldTime = 10d });
            bool valid = created.Succeeded && safe.ApplicableConditions.Count == 0 && !safe.EncounterRisk.HasVisibleRisk && known.ApplicableConditions.Count == 1 && known.EncounterRisk.HiddenKnownEncounterDefinitionIds.Contains(PrototypeTravelConditionDefinitionFactory.HiddenAmbushEncounterId);
            return TestLabAssertions.True("step14-travel-condition-hidden", "Hidden travel risk does not leak under knowledge-safe evaluation", valid, $"Create={created.Status} Safe={safe.ApplicableConditions.Count}/{safe.EncounterRisk.VisibleEncounterCount} Known={known.ApplicableConditions.Count}/{string.Join(",", known.EncounterRisk.HiddenKnownEncounterDefinitionIds)}");
        }

        private static TestLabAutomationStepResult TravelConditionJourneySlowdown(TestLabAutomationContext context)
        {
            TravelConditionRuntime conditions = ConditionRuntime(context);
            TravelConditionOperationResult condition = CreateRouteCondition(context, conditions, "journey-muddy", PrototypeTravelConditionDefinitionFactory.MuddyRoadConditionId, PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId);
            TravelJourneyOperationResult created = CreateJourney(context, "condition-slowdown", "location.prototype.market-district", rate: 100d, conditionMode: TravelConditionEvaluationMode.CurrentConditions);
            TravelJourneyOperationResult started = JourneyRuntime(context).StartJourney(Lifecycle(context, created.Journey?.JourneyId, "condition-slowdown-start", worldTime: 10d, rate: 100d, conditionMode: TravelConditionEvaluationMode.CurrentConditions));
            TravelJourneyOperationResult advanced = JourneyRuntime(context).AdvanceJourney(Lifecycle(context, created.Journey?.JourneyId, "condition-slowdown-advance", worldTime: 10.5d, rate: 100d, conditionMode: TravelConditionEvaluationMode.CurrentConditions));
            bool slower = advanced.MovementRate != null && advanced.MovementRate.FinalRateMetersPerSecond < 100d;
            bool notArrived = advanced.Journey?.LifecycleState == TravelJourneyLifecycleState.Active;
            bool valid = condition.Succeeded && created.Succeeded && started.Succeeded && advanced.Succeeded && slower && notArrived;
            return TestLabAssertions.True("step14-travel-condition-journey-slowdown", "Journey progress uses condition-adjusted movement", valid, $"Condition={condition.Status} Create={created.Status} Start={started.Status} Advance={advanced.Status}:{advanced.Journey?.LifecycleState} Rate={advanced.MovementRate?.FinalRateMetersPerSecond:0.###}");
        }

        private static TestLabAutomationStepResult TravelConditionEncounterInterruption(TestLabAutomationContext context)
        {
            TravelConditionRuntime conditions = ConditionRuntime(context);
            TravelConditionOperationResult condition = CreateRouteCondition(context, conditions, "checkpoint-ambush", PrototypeTravelConditionDefinitionFactory.HiddenAmbushRiskConditionId, PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId);
            TravelJourneyOperationResult created = CreateJourney(context, "condition-encounter", "location.prototype.market-district", rate: 100d, conditionMode: TravelConditionEvaluationMode.CurrentConditions);
            TravelJourneyOperationResult started = JourneyRuntime(context).StartJourney(Lifecycle(context, created.Journey?.JourneyId, "condition-encounter-start", worldTime: 10d, rate: 100d, conditionMode: TravelConditionEvaluationMode.CurrentConditions));
            bool interrupted = !started.Succeeded && started.Status == TravelJourneyMutationStatus.Blocked && started.Journey?.BlockReason == TravelJourneyBlockReason.EncounterInterrupted;
            bool encounterCreated = conditions.EncounterCount == 1 && conditions.Encounters.First().EncounterDefinitionId == PrototypeTravelConditionDefinitionFactory.HiddenAmbushEncounterId;
            bool valid = condition.Succeeded && created.Succeeded && interrupted && encounterCreated;
            return TestLabAssertions.True("step14-travel-condition-encounter", "Checkpoint encounter interrupts journey without owning combat state", valid, $"Condition={condition.Status} Create={created.Status} Start={started.Status}:{started.Journey?.BlockReason} Encounters={conditions.EncounterCount}");
        }

        private static TestLabAutomationStepResult TravelConditionHazardPersistence(TestLabAutomationContext context)
        {
            TravelConditionRuntime conditions = ConditionRuntime(context);
            TravelConditionOperationResult condition = conditions.CreateCondition(new TravelConditionCreateRequest
            {
                transactionId = Tx(context, "travel-condition-heat"),
                conditionId = context.ScenarioContext.ScopedId("travel-condition", "heat"),
                conditionDefinitionId = PrototypeTravelConditionDefinitionFactory.HeatConditionId,
                target = new TravelConditionTargetReferenceData
                {
                    scope = TravelConditionTargetScope.RouteNetwork,
                    targetId = PrototypeLocationRouteDefinitionFactory.RegionalTrailNetworkId,
                    sourceLocationId = "location.prototype.village",
                    destinationLocationId = "location.prototype.wilderness-ring",
                    traveler = Body(PrototypeEntityLocationFactory.PlayerBodyId, context)
                },
                lifecycleState = TravelConditionLifecycleState.Active,
                startsWorldTime = 0d,
                sourceEventId = "testlab.feature.14.8",
                provenanceId = "testlab.feature.14.8"
            });
            TravelConditionOperationResult hazard = conditions.TriggerHazard(new TravelHazardTriggerRequest
            {
                transactionId = Tx(context, "travel-condition-heat-hazard"),
                hazardDefinitionId = PrototypeTravelConditionDefinitionFactory.HeatExposureHazardId,
                sourceConditionId = condition.Condition?.ConditionId,
                target = RouteTarget(PrototypeLocationRouteDefinitionFactory.VillageWildernessTrailSegmentId, context),
                traveler = Body(PrototypeEntityLocationFactory.PlayerBodyId, context),
                worldTime = 20d,
                provenanceId = "automation.14.8"
            });
            TravelConditionPersistenceParticipant participant = new TravelConditionPersistenceParticipant(conditions, () => context.ScenarioContext.Runtimes.DefinitionRegistry, () => RouteRuntime(context), () => JourneyRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(save.PayloadJson, TravelConditionPersistenceParticipant.CurrentParticipantSchemaVersion);
            TravelConditionRuntime restored = new TravelConditionRuntime();
            restored.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, RouteRuntime(context), JourneyRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            TravelConditionOperationResult restore = restored.RestoreFromSaveData(JsonUtility.FromJson<TravelConditionRuntimeSaveData>(save.PayloadJson), context.ScenarioContext.Runtimes.DefinitionRegistry, RouteRuntime(context), JourneyRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            TravelConditionRuntimeSaveData corrupt = conditions.CreateSaveData();
            if (corrupt.conditions.Length > 0)
            {
                corrupt.conditions[0].conditionDefinitionId = "travel-condition-definition.missing";
            }

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), TravelConditionPersistenceParticipant.CurrentParticipantSchemaVersion);
            bool valid = condition.Succeeded && hazard.Succeeded && save.Succeeded && prepared.Succeeded && restore.Succeeded && restored.ConditionCount == conditions.ConditionCount && restored.HazardExposureCount == conditions.HazardExposureCount && !rejected.Succeeded;
            return TestLabAssertions.True("step14-travel-condition-persistence", "Hazards and travel conditions persist without retriggering", valid, $"Condition={condition.Status} Hazard={hazard.Status} Save={save.Succeeded} Prepare={prepared.Succeeded} Restore={restore.Status} Count={restored.ConditionCount}/{conditions.ConditionCount} Hazards={restored.HazardExposureCount}/{conditions.HazardExposureCount} Rejected={rejected.Succeeded}:{rejected.Message}");
        }

        private static TestLabAutomationStepResult PoliticalTravelReadinessOwnership(TestLabAutomationContext context)
        {
            if (!TryPreparePoliticalTravelFixture(context, "readiness", out PoliticalTravelAutomationFixture fixture, out string failure)) return PoliticalTravelFail("step14-political-travel-readiness", failure);
            long governmentRevision = fixture.Runtimes.Governments.Revision;
            long legalRevision = fixture.Runtimes.Laws.Revision;
            long crimeRevision = fixture.Runtimes.Crimes.Revision;
            PoliticalTravelEvaluationResult evaluation = fixture.PoliticalTravel.EvaluateCrossing(fixture.Evaluation(TravelLegalComplianceMode.RequireLegalTravel));

            bool valid = fixture.PoliticalTravel != null
                && evaluation.Succeeded
                && evaluation.Classification == PoliticalTravelCrossingClassification.BorderCrossing
                && fixture.Runtimes.Governments.Revision == governmentRevision
                && fixture.Runtimes.Laws.Revision == legalRevision
                && fixture.Runtimes.Crimes.Revision == crimeRevision;
            return TestLabAssertions.True("step14-political-travel-readiness", "Political travel runtime delegates ownership to Step 13 systems", valid, $"Runtime={fixture.PoliticalTravel != null} Evaluation={evaluation.Code} Class={evaluation.Classification} Gov={governmentRevision}->{fixture.Runtimes.Governments.Revision} Law={legalRevision}->{fixture.Runtimes.Laws.Revision} Crime={crimeRevision}->{fixture.Runtimes.Crimes.Revision}");
        }

        private static TestLabAutomationStepResult PoliticalTravelTerritoryJurisdictionEvaluation(TestLabAutomationContext context)
        {
            if (!TryPreparePoliticalTravelFixture(context, "jurisdiction", out PoliticalTravelAutomationFixture fixture, out string failure)) return PoliticalTravelFail("step14-political-travel-territory", failure);
            PoliticalTravelEvaluationResult evaluation = fixture.PoliticalTravel.EvaluateCrossing(fixture.Evaluation(TravelLegalComplianceMode.RequireLegalTravel));
            bool valid = evaluation.Succeeded
                && evaluation.OriginTerritory?.TerritoryId == fixture.OriginTerritoryId
                && evaluation.DestinationTerritory?.TerritoryId == fixture.DestinationTerritoryId
                && evaluation.DestinationJurisdiction?.SelectedJurisdiction?.jurisdictionId == fixture.DestinationJurisdictionId
                && evaluation.CombinedState == PhysicalLegalTravelState.TravelableAndLegal;
            return TestLabAssertions.True("step14-political-travel-territory", "Territory and jurisdiction resolve from government authority", valid, $"Status={evaluation.Code} Origin={evaluation.OriginTerritory?.TerritoryId} Destination={evaluation.DestinationTerritory?.TerritoryId} Jurisdiction={evaluation.DestinationJurisdiction?.SelectedJurisdiction?.jurisdictionId} Combined={evaluation.CombinedState}");
        }

        private static TestLabAutomationStepResult PoliticalTravelLegalComplianceModes(TestLabAutomationContext context)
        {
            if (!TryPreparePoliticalTravelFixture(context, "compliance", out PoliticalTravelAutomationFixture fixture, out string failure)) return PoliticalTravelFail("step14-political-travel-compliance", failure);
            LegalOperationResult law = fixture.EnactTravelLaw("ban", PoliticalTravelRuntime.CrossBorderActionId, LegalEffectCategory.Prohibition);
            PoliticalTravelOperationResult blocked = fixture.PoliticalTravel.RecordCrossing(fixture.Crossing("blocked", TravelLegalComplianceMode.RequireLegalTravel));
            int afterBlocked = fixture.PoliticalTravel.CrossingCount;
            PoliticalTravelOperationResult illegal = fixture.PoliticalTravel.RecordCrossing(fixture.Crossing("illegal", TravelLegalComplianceMode.AllowIllegalTravel));
            PoliticalTravelEvaluationResult physical = fixture.PoliticalTravel.EvaluateCrossing(fixture.Evaluation(TravelLegalComplianceMode.AllowIllegalTravel, physicalTravelPossible: false));

            bool valid = law.Succeeded
                && blocked.Code == PoliticalTravelOperationCode.LegalBlocked
                && afterBlocked == 0
                && illegal.Succeeded
                && illegal.Crossing?.illegalCrossing == true
                && illegal.Crossing.combinedState == PhysicalLegalTravelState.IllegalButPhysicallyPossible
                && physical.Code == PoliticalTravelOperationCode.PhysicalBlocked
                && physical.CombinedState == PhysicalLegalTravelState.PhysicallyBlocked;
            return TestLabAssertions.True("step14-political-travel-compliance", "Legal modes keep physical and political travel distinct", valid, $"Law={law.Code} Blocked={blocked.Code} AfterBlocked={afterBlocked} Illegal={illegal.Code}:{illegal.Crossing?.combinedState} Physical={physical.Code}:{physical.CombinedState}");
        }

        private static TestLabAutomationStepResult PoliticalTravelCheckpointAuthorization(TestLabAutomationContext context)
        {
            if (!TryPreparePoliticalTravelFixture(context, "checkpoint", out PoliticalTravelAutomationFixture fixture, out string failure)) return PoliticalTravelFail("step14-political-travel-checkpoint", failure);
            long routeRevision = fixture.Runtimes.LocationRoutes.Revision;
            long legalRevision = fixture.Runtimes.Laws.Revision;
            PoliticalTravelOperationResult checkpoint = fixture.PoliticalTravel.CreateCheckpoint(fixture.Checkpoint(BorderCheckpointPolicy.RequireAuthorization));
            PoliticalTravelOperationResult denied = fixture.PoliticalTravel.RecordCrossing(fixture.Crossing("no-permit", TravelLegalComplianceMode.RequireLegalTravel));
            PoliticalTravelOperationResult grant = fixture.PoliticalTravel.GrantAuthorization(fixture.Authorization(checkpoint.Checkpoint?.CheckpointId));
            PoliticalTravelOperationResult allowed = fixture.PoliticalTravel.RecordCrossing(fixture.Crossing("permit", TravelLegalComplianceMode.RequireLegalTravel));

            bool valid = checkpoint.Succeeded
                && denied.Code == PoliticalTravelOperationCode.LegalBlocked
                && grant.Succeeded
                && allowed.Succeeded
                && allowed.Crossing?.authorizationId == grant.Authorization?.authorizationId
                && fixture.Runtimes.LocationRoutes.Revision == routeRevision
                && fixture.Runtimes.Laws.Revision == legalRevision;
            return TestLabAssertions.True("step14-political-travel-checkpoint", "Checkpoint authorization gates border crossing without route or law mutation", valid, $"Checkpoint={checkpoint.Code} Denied={denied.Code} Grant={grant.Code} Allowed={allowed.Code} Auth={allowed.Crossing?.authorizationId} Route={routeRevision}->{fixture.Runtimes.LocationRoutes.Revision} Law={legalRevision}->{fixture.Runtimes.Laws.Revision}");
        }

        private static TestLabAutomationStepResult PoliticalTravelWantedVisibility(TestLabAutomationContext context)
        {
            if (!TryPreparePoliticalTravelFixture(context, "wanted", out PoliticalTravelAutomationFixture fixture, out string failure)) return PoliticalTravelFail("step14-political-travel-wanted", failure);
            CrimeOperationResult wanted = fixture.Runtimes.Crimes.CreateWantedStatus(new WantedStatusRequest
            {
                transactionId = Tx(context, "political-travel-wanted"),
                wantedStatusId = context.ScenarioContext.ScopedId("wanted-status.political-travel", "hidden"),
                wantedDefinitionId = PrototypeCrimeDefinitionFactory.WantedForArrestDefinitionId,
                subjectId = fixture.TravelerPersonId,
                territoryId = fixture.DestinationTerritoryId,
                jurisdictionId = fixture.DestinationJurisdictionId,
                activeWorldTime = 0d,
                visibility = PoliticalVisibility.Hidden
            });
            PoliticalTravelEvaluationResult safe = fixture.PoliticalTravel.EvaluateCrossing(fixture.Evaluation(TravelLegalComplianceMode.RequireLegalTravel, PoliticalTravelVisibilityMode.TravelerSafe));
            PoliticalTravelEvaluationResult privileged = fixture.PoliticalTravel.EvaluateCrossing(fixture.Evaluation(TravelLegalComplianceMode.RequireLegalTravel, PoliticalTravelVisibilityMode.Privileged));

            bool valid = wanted.Succeeded
                && safe.Wanted.VisibleWantedStatusIds.Count == 0
                && safe.Wanted.HiddenRestrictedInformation
                && privileged.Wanted.VisibleWantedStatusIds.Contains(wanted.SubjectId)
                && privileged.EnforcementOpportunity;
            return TestLabAssertions.True("step14-political-travel-wanted", "Wanted and warrant summaries respect political visibility", valid, $"Wanted={wanted.Code}:{wanted.SubjectId} Safe={safe.Wanted.VisibleWantedStatusIds.Count}/{safe.Wanted.HiddenRestrictedInformation} Privileged={string.Join(",", privileged.Wanted.VisibleWantedStatusIds)} Enforcement={privileged.EnforcementOpportunity}");
        }

        private static TestLabAutomationStepResult PoliticalTravelRouteRequirements(TestLabAutomationContext context)
        {
            if (!TryPreparePoliticalTravelFixture(context, "requirements", out PoliticalTravelAutomationFixture fixture, out string failure)) return PoliticalTravelFail("step14-political-travel-route-requirements", failure);
            PoliticalTravelOperationResult checkpoint = fixture.PoliticalTravel.CreateCheckpoint(fixture.Checkpoint(BorderCheckpointPolicy.RequireInspection));
            LocationRouteSearchResult plan = fixture.Runtimes.LocationRoutes.PlanRoute(RouteRequest(context, PoliticalTravelAutomationFixture.OriginLocationId, PoliticalTravelAutomationFixture.DestinationLocationId, traveler: Body(PrototypeEntityLocationFactory.PlayerBodyId, context)));
            RouteRequirementSummary requirements = fixture.PoliticalTravel.BuildPoliticalRouteRequirements(plan.Plan, fixture.Evaluation(TravelLegalComplianceMode.RequireLegalTravel));

            bool valid = checkpoint.Succeeded
                && plan.Succeeded
                && requirements.requiredLegalTravelActions.Contains(PoliticalTravelRuntime.CrossBorderActionId)
                && requirements.requiredCheckpointIds.Contains(checkpoint.Checkpoint.CheckpointId)
                && requirements.requiredPoliticalTerritoryIds.Contains(fixture.OriginTerritoryId)
                && requirements.requiredPoliticalTerritoryIds.Contains(fixture.DestinationTerritoryId);
            return TestLabAssertions.True("step14-political-travel-route-requirements", "Route requirements include political actions, checkpoints, and territories", valid, $"Checkpoint={checkpoint.Code} Plan={plan.Status} Actions={string.Join(",", requirements.requiredLegalTravelActions)} Checkpoints={string.Join(",", requirements.requiredCheckpointIds)} Territories={string.Join(",", requirements.requiredPoliticalTerritoryIds)}");
        }

        private static TestLabAutomationStepResult PoliticalTravelPersistenceFixture(TestLabAutomationContext context)
        {
            if (!TryPreparePoliticalTravelFixture(context, "persistence", out PoliticalTravelAutomationFixture fixture, out string failure)) return PoliticalTravelFail("step14-political-travel-persistence", failure);
            PoliticalTravelOperationResult checkpoint = fixture.PoliticalTravel.CreateCheckpoint(fixture.Checkpoint(BorderCheckpointPolicy.ObserveOnly));
            PoliticalTravelPersistenceParticipant participant = new PoliticalTravelPersistenceParticipant(fixture.PoliticalTravel, () => fixture.Runtimes.Governments, () => fixture.Runtimes.Laws, () => fixture.Runtimes.Crimes, () => fixture.Runtimes.Locations, () => fixture.Runtimes.LocationRoutes, fixture.Runtimes.WorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(save.PayloadJson, PoliticalTravelPersistenceParticipant.CurrentParticipantSchemaVersion);
            PoliticalTravelRuntime restored = new PoliticalTravelRuntime();
            restored.Configure(fixture.Runtimes.DefinitionRegistry, fixture.Runtimes.Governments, fixture.Runtimes.Laws, fixture.Runtimes.Crimes, fixture.Runtimes.Justice, fixture.Runtimes.Locations, fixture.Runtimes.LocationRoutes, fixture.Runtimes.WorldId);
            PoliticalTravelOperationResult restore = restored.RestoreFromSaveData(JsonUtility.FromJson<PoliticalTravelRuntimeSaveData>(save.PayloadJson), fixture.Runtimes.Governments, fixture.Runtimes.Laws, fixture.Runtimes.Crimes, fixture.Runtimes.Locations, fixture.Runtimes.LocationRoutes, fixture.Runtimes.WorldId);
            PoliticalTravelRuntimeSaveData corrupt = fixture.PoliticalTravel.CreateSaveData();
            if (corrupt.checkpoints.Length > 0)
            {
                corrupt.checkpoints[0].destinationTerritoryId = "political-territory.missing";
            }

            long beforeRejected = fixture.PoliticalTravel.Revision;
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), PoliticalTravelPersistenceParticipant.CurrentParticipantSchemaVersion);
            TestLabRuntimeBundleSnapshot snapshot = fixture.Runtimes.CreateSnapshot();
            int beforeExtraCount = fixture.PoliticalTravel.CheckpointCount;
            PoliticalTravelOperationResult extra = fixture.PoliticalTravel.CreateCheckpoint(fixture.Checkpoint(BorderCheckpointPolicy.RequireInspection, "extra"));
            bool mutated = extra.Succeeded && fixture.PoliticalTravel.CheckpointCount == beforeExtraCount + 1;
            bool snapshotRestored = fixture.Runtimes.RestoreSnapshot(snapshot, out string restoreFailure);

            bool valid = checkpoint.Succeeded
                && save.Succeeded
                && prepared.Succeeded
                && restore.Succeeded
                && restored.CheckpointCount == fixture.PoliticalTravel.CheckpointCount
                && !rejected.Succeeded
                && fixture.PoliticalTravel.Revision == beforeRejected
                && mutated
                && snapshotRestored
                && fixture.PoliticalTravel.CheckpointCount == 1;
            return TestLabAssertions.True("step14-political-travel-persistence", "Political travel persistence and fixture restore are graph-safe", valid, $"Checkpoint={checkpoint.Code} Save={save.Succeeded} Prepare={prepared.Succeeded} Restore={restore.Code} Count={restored.CheckpointCount}/{fixture.PoliticalTravel.CheckpointCount} Rejected={rejected.Succeeded}:{rejected.Message} Extra={extra.Code} Snapshot={snapshotRestored}:{restoreFailure}");
        }

        private static TestLabAutomationStepResult PersistenceManifestOwnership(TestLabAutomationContext context)
        {
            Step14PersistenceSnapshotSource source = Source(context);
            Step14PersistenceManifest manifest = Step14PersistenceManifestBuilder.Build(source);
            Step14PersistenceOwnerRecord currentPlacement = manifest.Ownership.FirstOrDefault(item => item.Category == "entity placement");
            Step14PersistenceOwnerRecord movementProjection = manifest.Ownership.FirstOrDefault(item => item.Category == "movement historical projections");
            bool currentPlacementOwner = currentPlacement != null
                && currentPlacement.OwnerKind == Step14PersistenceOwnerKind.Authoritative
                && currentPlacement.OwnerParticipantId == Step14PersistenceManifestBuilder.EntityLocationParticipantId;
            bool movementProjectionOwner = movementProjection != null
                && movementProjection.OwnerKind == Step14PersistenceOwnerKind.Derived;
            bool travelDependencies = manifest.Participants.Any(participant => participant.ParticipantId == Step14PersistenceManifestBuilder.JourneyParticipantId
                && participant.RequiredDependencies.Contains(Step14PersistenceManifestBuilder.RouteParticipantId)
                && participant.RequiredDependencies.Contains(Step14PersistenceManifestBuilder.EntityLocationParticipantId));
            bool valid = manifest.Succeeded && currentPlacementOwner && movementProjectionOwner && travelDependencies;
            return TestLabAssertions.True("step14-persistence-manifest", "Step 14 persistence ownership is explicit and non-duplicated", valid, $"Succeeded={manifest.Succeeded} Participants={manifest.Participants.Count} Owners={manifest.Ownership.Count} Errors={manifest.ValidationReport.Errors.Count} PlacementOwner={currentPlacement?.OwnerParticipantId} Movement={movementProjection?.OwnerKind}");
        }

        private static TestLabAutomationStepResult HistoricalExactLocation(TestLabAutomationContext context)
        {
            TravelJourneyOperationResult created = CreateJourney(context, "history-exact", "location.prototype.market-district", rate: 5d);
            TravelJourneyOperationResult started = JourneyRuntime(context).StartJourney(Lifecycle(context, created.Journey?.JourneyId, "history-exact-start", worldTime: 20d, rate: 5d));
            MovementHistoryService history = History(context);
            EntityLocationReferenceData traveler = Body(PrototypeEntityLocationFactory.PlayerBodyId, context);
            HistoricalExactLocationResult before = history.ResolveExactLocationAt(traveler, 5d, MovementHistoryVisibilityMode.DevelopmentAuthoritative);
            HistoricalExactLocationResult during = history.ResolveExactLocationAt(traveler, 21d, MovementHistoryVisibilityMode.DevelopmentAuthoritative);
            bool livePlacementUnchanged = EntityRuntime(context).TryGetActivePlacement(traveler, out EntityPlacementSnapshot placement) && placement.ExactLocationId == "location.prototype.village";
            bool valid = created.Succeeded
                && started.Succeeded
                && before.Status == HistoricalExactLocationStatus.ExactLocationFound
                && before.ExactLocationId == "location.prototype.village"
                && during.Status == HistoricalExactLocationStatus.InTransit
                && during.InTransit != null
                && during.InTransit.PreviousLocationId == "location.prototype.village"
                && during.InTransit.NextLocationId == "location.prototype.market-district"
                && livePlacementUnchanged;
            return TestLabAssertions.True("step14-history-exact-location", "Historical exact-location projection separates placement from travel state", valid, $"Create={created.Status} Start={started.Status} Before={before.Status}:{before.ExactLocationId} During={during.Status}:{during.InTransit?.JourneyId} Live={placement?.ExactLocationId}");
        }

        private static TestLabAutomationStepResult HistoricalPathOccupancyVisits(TestLabAutomationContext context)
        {
            EntityLocationRuntime entities = EntityRuntime(context);
            EntityLocationReferenceData merchant = Body(PrototypeEntityLocationFactory.MerchantBodyId, context);
            EntityLocationOperationResult moved = entities.Relocate(new EntityRelocationRequest
            {
                transactionId = Tx(context, "history-path-relocate"),
                newPlacementId = context.ScenarioContext.ScopedId("placement.test", "history-path-merchant"),
                entity = merchant,
                expectedOriginLocationId = "location.prototype.merchant-counter",
                destinationLocationId = "location.prototype.civic-office",
                category = EntityPlacementCategory.Visiting,
                worldTime = 110d,
                sourceEventId = "testlab.feature.14.10.history-path",
                provenanceId = "testlab.feature.14.10"
            });
            MovementHistoryService history = History(context);
            HistoricalLocationPathResult path = history.ResolveHistoricalLocationPath("location.prototype.civic-office", 111d);
            HistoricalOccupancyResult occupancy = history.GetHistoricalOccupancy("location.prototype.village", 111d, recursive: true, visibilityMode: MovementHistoryVisibilityMode.DevelopmentAuthoritative);
            VisitedLocationSummary visits = history.GetVisitSummary(merchant, "location.prototype.village", 0d, 200d, exactOnly: false, MovementHistoryVisibilityMode.DevelopmentAuthoritative);
            MovementDistanceSummary distance = history.GetMovementDistance(merchant, 0d, 200d, MovementHistoryVisibilityMode.DevelopmentAuthoritative);
            bool deterministicOccupancy = occupancy.Placements.Select(item => item.entity?.StableKey ?? string.Empty).SequenceEqual(occupancy.Placements.Select(item => item.entity?.StableKey ?? string.Empty).OrderBy(id => id, StringComparer.Ordinal));
            bool valid = moved.Succeeded
                && path.Succeeded
                && path.LocationPathIds.Contains("location.prototype.village")
                && occupancy.Placements.Any(item => item.entity?.entityId == PrototypeEntityLocationFactory.MerchantBodyId)
                && deterministicOccupancy
                && visits.VisitCount >= 1
                && distance.TotalCompletedDistanceMeters >= 0d;
            return TestLabAssertions.True("step14-history-path-occupancy", "Historical path, occupancy, visits, and distance remain derived and deterministic", valid, $"Move={moved.Status} Path={string.Join("/", path.LocationPathIds)} Occupancy={occupancy.Placements.Count} Visits={visits.VisitCount} Distance={distance.TotalCompletedDistanceMeters} Deterministic={deterministicOccupancy}");
        }

        private static TestLabAutomationStepResult HistoricalTimelineVisibility(TestLabAutomationContext context)
        {
            TravelConditionRuntime conditions = ConditionRuntime(context);
            LocationRouteMutationResult segment = CreateRouteSegment(context, "history-hidden", "location.prototype.village", "location.prototype.wilderness-ring", 35d, 35d);
            TravelConditionOperationResult hidden = CreateRouteCondition(context, conditions, "history-hidden", PrototypeTravelConditionDefinitionFactory.HiddenAmbushRiskConditionId, segment.Segment?.SegmentId);
            MovementHistoryService history = History(context);
            EntityLocationReferenceData traveler = Body(PrototypeEntityLocationFactory.PlayerBodyId, context);
            MovementTimelineResult development = history.BuildTimeline(new MovementHistoryQuery
            {
                routeSegmentId = segment.Segment?.SegmentId,
                startWorldTime = 0d,
                endWorldTime = 1000d,
                visibilityMode = MovementHistoryVisibilityMode.DevelopmentAuthoritative
            });
            MovementTimelineResult publicView = history.BuildTimeline(new MovementHistoryQuery
            {
                routeSegmentId = segment.Segment?.SegmentId,
                startWorldTime = 0d,
                endWorldTime = 1000d,
                visibilityMode = MovementHistoryVisibilityMode.Public
            });
            bool deterministic = development.Entries.Select(TimelineKey).SequenceEqual(development.Entries.Select(TimelineKey).OrderBy(id => id, StringComparer.Ordinal));
            bool hiddenVisibleToDevelopment = development.Entries.Any(item => item.SourceRecordId == hidden.Condition?.ConditionId);
            bool hiddenOmittedPublic = !publicView.Entries.Any(item => item.SourceRecordId == hidden.Condition?.ConditionId);
            bool sourceRefs = development.Entries.All(item => !string.IsNullOrWhiteSpace(item.SourceParticipantId) && !string.IsNullOrWhiteSpace(item.SourceRecordId));
            bool valid = segment.Succeeded && hidden.Succeeded && deterministic && sourceRefs && hiddenVisibleToDevelopment && hiddenOmittedPublic;
            return TestLabAssertions.True("step14-history-timeline-visibility", "Timeline projections are deterministic and visibility-safe", valid, $"Segment={segment.Status} Hidden={hidden.Status} Dev={development.Entries.Count} Public={publicView.Entries.Count} Deterministic={deterministic} SourceRefs={sourceRefs} HiddenPublic={hiddenOmittedPublic}");
        }

        private static TestLabAutomationStepResult HistoricalValidationSnapshot(TestLabAutomationContext context)
        {
            Step14PersistenceSnapshotSource source = Source(context);
            MovementHistoryService history = new MovementHistoryService(source);
            MovementHistoryValidationReport validReport = history.ValidateHistory();
            EntityLocationReferenceData traveler = Body(PrototypeEntityLocationFactory.PlayerBodyId, context);
            MovementTimelineResult before = history.BuildTimeline(new MovementHistoryQuery
            {
                entity = traveler,
                startWorldTime = 0d,
                endWorldTime = 500d
            });
            Create(context, "history-snapshot-extra", PrototypeLocationDefinitionFactory.RoomDefinitionId, "Historical Snapshot Extra");
            MovementTimelineResult after = history.BuildTimeline(new MovementHistoryQuery
            {
                entity = traveler,
                startWorldTime = 0d,
                endWorldTime = 500d
            });
            Step14PersistenceSnapshotSource corrupt = Source(context);
            corrupt.entityLocations.placements.Add(new EntityPlacementRecordData
            {
                placementId = context.ScenarioContext.ScopedId("placement.corrupt", "missing-location"),
                entity = Body(context.ScenarioContext.ScopedId("body.corrupt", "missing-location"), context),
                exactLocationId = "location.prototype.missing",
                startWorldTime = 1000d,
                lifecycleState = EntityPlacementLifecycleState.Active
            });
            MovementHistoryValidationReport corruptReport = new MovementHistoryService(corrupt).ValidateHistory();
            bool immutable = before.Entries.Select(TimelineKey).SequenceEqual(after.Entries.Select(TimelineKey));
            bool corruptCaught = !corruptReport.Succeeded && corruptReport.Issues.Any(issue => issue.Severity == MovementHistoryIssueSeverity.Error
                && issue.Message.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0
                && issue.Message.IndexOf("location", StringComparison.OrdinalIgnoreCase) >= 0);
            bool valid = validReport.Succeeded && immutable && corruptCaught;
            int validErrors = validReport.Issues.Count(issue => issue.Severity == MovementHistoryIssueSeverity.Error);
            int corruptErrors = corruptReport.Issues.Count(issue => issue.Severity == MovementHistoryIssueSeverity.Error);
            return TestLabAssertions.True("step14-history-validation-snapshot", "Movement projections validate source graphs and remain immutable after runtime mutation", valid, $"Valid={validReport.Succeeded}:{validErrors} Immutable={immutable} Corrupt={corruptReport.Succeeded}:{corruptErrors}");
        }

        private static TestLabAutomationStepResult SceneBindingReadiness(TestLabAutomationContext context)
        {
            WorldSceneBindingRuntime.Default.ClearTransientBindings();
            WorldSceneBindingRuntime runtime = SceneBindingRuntime(context);
            LocationSceneBinding village = null;
            LocationSceneBinding guild = null;
            LocationSceneBinding duplicateGuild = null;
            LocationSceneBinding missingOptional = null;
            try
            {
                village = NewBinding<LocationSceneBinding>("scene-binding-village");
                village.ConfigureLocation("location.prototype.village", "prototype.scene.location.village", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, PrototypeLocationDefinitionFactory.SettlementDefinitionId, requiredBinding: true);
                guild = NewBinding<LocationSceneBinding>("scene-binding-guild");
                guild.ConfigureLocation("location.prototype.adventurers-guild", "prototype.scene.location.guild", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, PrototypeLocationDefinitionFactory.GuildHallDefinitionId, requiredBinding: true);
                duplicateGuild = NewBinding<LocationSceneBinding>("scene-binding-guild-duplicate");
                duplicateGuild.ConfigureLocation("location.prototype.adventurers-guild", "prototype.scene.location.guild.duplicate", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, PrototypeLocationDefinitionFactory.GuildHallDefinitionId, requiredBinding: true);
                missingOptional = NewBinding<LocationSceneBinding>("scene-binding-missing-optional");
                string missingLocationId = context.ScenarioContext.ScopedId("location.prototype.missing", "scene-binding");
                missingOptional.ConfigureLocation(missingLocationId, "prototype.scene.location.optional-missing", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, requiredBinding: false);

                runtime.Register(village);
                runtime.Register(guild);
                runtime.Register(duplicateGuild);
                runtime.Register(missingOptional);
                WorldSceneBindingValidationReport report = runtime.Validate();
                bool boundVillage = runtime.TryResolve(WorldSceneBindingCategory.Location, "location.prototype.village", out WorldSceneBindingComponent villageBinding) && villageBinding.Status == WorldSceneBindingStatus.Bound;
                bool deterministicDuplicate = guild.Status == WorldSceneBindingStatus.Bound && duplicateGuild.Status == WorldSceneBindingStatus.Duplicate;
                bool optionalWarning = missingOptional.Status == WorldSceneBindingStatus.WaitingForLogicalRecord && report.WarningCount == 1;
                bool valid = boundVillage && deterministicDuplicate && optionalWarning && report.ErrorCount == 1 && report.DuplicateCount == 1 && !runtime.TryGetLocation(missingLocationId, out _);
                return TestLabAssertions.True("step14-scene-binding-readiness", "Scene bindings resolve authoritative records and report duplicate scene owners", valid, $"Report={report.Summary} Village={village.Status} Guild={guild.Status} Duplicate={duplicateGuild.Status} Optional={missingOptional.Status}");
            }
            finally
            {
                DestroyBindings(village, guild, duplicateGuild, missingOptional);
                runtime.ClearTransientBindings();
                WorldSceneBindingRuntime.Default.ClearTransientBindings();
            }
        }

        private static TestLabAutomationStepResult SceneBindingInteractionAndConnection(TestLabAutomationContext context)
        {
            WorldSceneBindingRuntime.Default.ClearTransientBindings();
            WorldSceneBindingRuntime runtime = SceneBindingRuntime(context);
            InteractionPointSceneBinding counter = null;
            ConnectionSceneBinding entrance = null;
            ConnectionSceneBinding officeDoor = null;
            GameObject door = null;
            GameObject officeDoorObject = null;
            try
            {
                counter = NewBinding<InteractionPointSceneBinding>("scene-binding-counter");
                counter.ConfigureBinding(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, "prototype.scene.interaction.adventurer-guild-counter", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, WorldSceneBindingRole.Primary, true);
                door = new GameObject("scene-binding-door-collider");
                BoxCollider collider = door.AddComponent<BoxCollider>();
                entrance = door.AddComponent<ConnectionSceneBinding>();
                entrance.ConfigureConnection(PrototypeLocationConnectionDefinitionFactory.VillageGuildEntranceConnectionId, "prototype.scene.connection.guild-entrance", "location.prototype.village", "location.prototype.adventurers-guild", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, collider, true);
                officeDoorObject = new GameObject("scene-binding-office-door-collider");
                BoxCollider officeCollider = officeDoorObject.AddComponent<BoxCollider>();
                officeDoor = officeDoorObject.AddComponent<ConnectionSceneBinding>();
                officeDoor.ConfigureConnection(PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, "prototype.scene.connection.guild-head-office", "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, officeCollider, true);

                runtime.Register(counter);
                runtime.Register(entrance);
                runtime.Register(officeDoor);
                runtime.SyncAllFromAuthoritative(true);

                InteractionContext interactionContext = default;
                bool canInteract = counter.CanInteract(in interactionContext);
                counter.Interact(in interactionContext);
                bool routedInteraction = counter.LastPoint != null && counter.LastPoint.InteractionPointId == PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId;

                LocationConnectionOperationResult close = runtime.RequestConnectionOpenState(Tx(context, "scene-binding-close-door"), PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, LocationConnectionOpenState.Closed, null, null, 22d, false);
                runtime.SyncAllFromAuthoritative(false);
                bool closedBlocks = close.Succeeded && officeCollider.enabled;
                LocationConnectionOperationResult open = runtime.RequestConnectionOpenState(Tx(context, "scene-binding-open-door"), PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, LocationConnectionOpenState.Open, null, null, 23d, false);
                runtime.SyncAllFromAuthoritative(false);
                bool openClears = open.Succeeded && !officeCollider.enabled;

                EntityLocationReferenceData actor = Body(PrototypeEntityLocationFactory.PlayerBodyId, context);
                SceneBindingTransitionResult traversal = entrance.RequestTraversal(actor, AccessContext(context, actor), 24d);
                bool placed = EntityRuntime(context).TryGetActivePlacement(actor, out EntityPlacementSnapshot placement);
                SceneBindingTransitionResult denied = runtime.RequestTransition(new SceneBindingTransitionRequest
                {
                    transactionId = Tx(context, "scene-binding-denied-office"),
                    actor = actor,
                    connectionId = PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId,
                    fromLocationId = "location.prototype.adventurers-guild",
                    toLocationId = "location.prototype.guildmaster-office",
                    accessContext = AccessContext(context, actor),
                    worldTime = 25d
                });
                bool valid = canInteract && routedInteraction && closedBlocks && openClears && traversal.Succeeded && placed && placement.ExactLocationId == "location.prototype.adventurers-guild" && denied.Status == SceneBindingTransitionStatus.AccessDenied;
                return TestLabAssertions.True("step14-scene-binding-interaction-connection", "Scene interaction and connection bindings delegate to authoritative runtimes", valid, $"CanInteract={canInteract} Routed={routedInteraction} Close={close.Status}:{closedBlocks} Open={open.Status}:{openClears} Traverse={traversal.Status} Placement={placement?.ExactLocationId} Denied={denied.Status}");
            }
            finally
            {
                DestroyBindings(counter, entrance, officeDoor);
                if (door != null)
                {
                    UnityEngine.Object.DestroyImmediate(door);
                }
                if (officeDoorObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(officeDoorObject);
                }
                runtime.ClearTransientBindings();
                WorldSceneBindingRuntime.Default.ClearTransientBindings();
            }
        }

        private static TestLabAutomationStepResult SceneBindingEntityMaterialization(TestLabAutomationContext context)
        {
            WorldSceneBindingRuntime.Default.ClearTransientBindings();
            WorldSceneBindingRuntime runtime = SceneBindingRuntime(context);
            LocationSceneBinding village = null;
            LocationSceneBinding guild = null;
            WorldEntitySceneBinding player = null;
            try
            {
                village = NewBinding<LocationSceneBinding>("scene-binding-village-anchor");
                village.transform.SetPositionAndRotation(new Vector3(-2f, 0f, -3f), Quaternion.Euler(0f, 20f, 0f));
                village.ConfigureLocation("location.prototype.village", "prototype.scene.location.village.anchor", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, requiredBinding: true);
                guild = NewBinding<LocationSceneBinding>("scene-binding-guild-anchor");
                guild.transform.SetPositionAndRotation(new Vector3(7f, 0f, 4f), Quaternion.Euler(0f, 90f, 0f));
                guild.ConfigureLocation("location.prototype.adventurers-guild", "prototype.scene.location.guild.anchor", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, requiredBinding: true);
                player = NewBinding<WorldEntitySceneBinding>("scene-binding-player-body");
                player.transform.position = new Vector3(100f, 10f, 100f);
                player.ConfigureEntity(LocationOccupantEntityType.Body, PrototypeEntityLocationFactory.PlayerBodyId, "prototype.scene.entity.player-body", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, snapToGround: false);

                runtime.Register(village);
                runtime.Register(guild);
                runtime.Register(player);
                runtime.SyncAllFromAuthoritative(true);
                Vector3 firstPosition = player.transform.position;
                bool initialMaterialized = Vector3.Distance(firstPosition, village.transform.position) < 0.001f;

                EntityLocationReferenceData actor = Body(PrototypeEntityLocationFactory.PlayerBodyId, context);
                EntityLocationOperationResult relocate = EntityRuntime(context).Relocate(new EntityRelocationRequest
                {
                    transactionId = Tx(context, "scene-binding-relocate-player"),
                    newPlacementId = context.ScenarioContext.ScopedId("placement.scene-binding", "player-guild"),
                    entity = actor,
                    expectedOriginLocationId = "location.prototype.village",
                    destinationLocationId = "location.prototype.adventurers-guild",
                    worldTime = 30d,
                    sourceEventId = "testlab.feature.14.11",
                    provenanceId = "testlab.feature.14.11"
                });
                runtime.SyncAllFromAuthoritative(false);
                bool relocatedMaterialized = relocate.Succeeded && Vector3.Distance(player.transform.position, guild.transform.position) < 0.001f;

                player.transform.position = new Vector3(-50f, 3f, 12f);
                bool authoritativeUnchanged = EntityRuntime(context).TryGetActivePlacement(actor, out EntityPlacementSnapshot placement) && placement.ExactLocationId == "location.prototype.adventurers-guild";
                bool valid = initialMaterialized && relocatedMaterialized && authoritativeUnchanged;
                return TestLabAssertions.True("step14-scene-binding-entity-materialize", "Entity scene binding follows authoritative placement and ignores Transform drift", valid, $"Initial={initialMaterialized}:{firstPosition} Relocate={relocate.Status}:{relocatedMaterialized} Authoritative={placement?.ExactLocationId}");
            }
            finally
            {
                DestroyBindings(village, guild, player);
                runtime.ClearTransientBindings();
                WorldSceneBindingRuntime.Default.ClearTransientBindings();
            }
        }

        private static TestLabAutomationStepResult SceneBindingRouteCheckpointTransient(TestLabAutomationContext context)
        {
            WorldSceneBindingRuntime.Default.ClearTransientBindings();
            WorldSceneBindingRuntime runtime = SceneBindingRuntime(context);
            RouteSegmentSceneBinding route = null;
            CheckpointSceneBinding checkpoint = null;
            try
            {
                PoliticalTravelRuntime political = PoliticalRuntime(context);
                PoliticalTravelOperationResult createCheckpoint = political.CreateCheckpoint(new BorderCheckpointCreateRequest
                {
                    transactionId = Tx(context, "scene-binding-checkpoint-create"),
                    checkpointId = context.ScenarioContext.ScopedId("checkpoint.prototype.scene-binding", "village-gate"),
                    displayName = "Scene Binding Village Gate",
                    locationId = "location.prototype.village",
                    routeSegmentId = PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId,
                    policy = BorderCheckpointPolicy.RequireInspection,
                    lifecycleState = BorderCheckpointLifecycleState.Active,
                    visibility = PoliticalVisibility.Public,
                    worldTime = 35d,
                    sourceEventId = "testlab.feature.14.11",
                    provenanceId = "testlab.feature.14.11"
                });

                route = NewBinding<RouteSegmentSceneBinding>("scene-binding-route-segment");
                route.ConfigureBinding(PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId, "prototype.scene.route.village-market-street", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, WorldSceneBindingRole.Primary, true);
                checkpoint = NewBinding<CheckpointSceneBinding>("scene-binding-checkpoint");
                checkpoint.ConfigureBinding(createCheckpoint.Checkpoint?.CheckpointId, "prototype.scene.checkpoint.village-gate", "scene.prototype", context.ScenarioContext.Runtimes.WorldId, WorldSceneBindingRole.Primary, true);

                long routeRevisionBefore = RouteRuntime(context).Revision;
                long politicalRevisionBefore = political.Revision;
                runtime.Register(route);
                runtime.Register(checkpoint);
                WorldSceneBindingValidationReport report = runtime.SyncAllFromAuthoritative(true);
                long routeRevisionAfter = RouteRuntime(context).Revision;
                long politicalRevisionAfter = political.Revision;
                bool resolvedRoute = runtime.TryResolve(WorldSceneBindingCategory.RouteSegment, PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId, out WorldSceneBindingComponent routeBinding) && routeBinding.Status == WorldSceneBindingStatus.Bound;
                WorldSceneBindingComponent checkpointBinding = null;
                bool resolvedCheckpoint = createCheckpoint.Succeeded && runtime.TryResolve(WorldSceneBindingCategory.Checkpoint, createCheckpoint.Checkpoint.CheckpointId, out checkpointBinding) && checkpointBinding.Status == WorldSceneBindingStatus.Bound;
                bool noMutation = routeRevisionBefore == routeRevisionAfter && politicalRevisionBefore == politicalRevisionAfter;
                bool valid = report.Succeeded && resolvedRoute && resolvedCheckpoint && noMutation;
                return TestLabAssertions.True("step14-scene-binding-route-checkpoint", "Route and checkpoint bindings are transient presentation mappings", valid, $"Checkpoint={createCheckpoint.Code} Report={report.Summary} Route={routeBinding?.Status} CheckpointBinding={checkpointBinding?.Status} Revisions={routeRevisionBefore}->{routeRevisionAfter}/{politicalRevisionBefore}->{politicalRevisionAfter}");
            }
            finally
            {
                DestroyBindings(route, checkpoint);
                runtime.ClearTransientBindings();
                WorldSceneBindingRuntime.Default.ClearTransientBindings();
            }
        }

        private static TestLabAutomationStepResult IntegrationReadinessAndOwnership(TestLabAutomationContext context)
        {
            Step14IntegrationValidationReport report = Step14WorldIntegrationValidator.Validate(new Step14IntegrationSnapshot(Source(context)));
            bool hasLocationOwner = report.AuthorityMap.Any(item => item.Domain == "location records" && item.AuthoritativeRuntime == "LocationRuntime" && item.Authoritative);
            bool hasPlacementOwner = report.AuthorityMap.Any(item => item.Domain == "entity placements" && item.AuthoritativeRuntime == "EntityLocationRuntime" && item.Authoritative);
            bool sceneIsDerived = report.AuthorityMap.Any(item => item.Domain == "scene bindings" && item.Derived && !item.Authoritative);
            bool valid = report.Succeeded && report.Readiness == Step14IntegrationReadinessState.Ready && hasLocationOwner && hasPlacementOwner && sceneIsDerived;
            return TestLabAssertions.True("step14-integration-readiness", "Integrated Step 14 readiness and authority ownership are clean", valid, $"Readiness={report.Readiness} Success={report.Succeeded} Failures={report.Failures.Count} Fingerprint={report.Fingerprint}");
        }

        private static TestLabAutomationStepResult IntegrationConceptSeparation(TestLabAutomationContext context)
        {
            Step14IntegrationValidationReport report = Step14WorldIntegrationValidator.Validate(new Step14IntegrationSnapshot(Source(context)));
            bool physical = report.AuthorityMap.Any(item => item.Domain == "Unity transforms" && item.External)
                && report.AuthorityMap.Any(item => item.Domain == "entity placements" && item.Authoritative);
            bool legal = report.AuthorityMap.Any(item => item.Domain == "political travel overlays" && item.Authoritative)
                && report.AuthorityMap.Any(item => item.Domain == "law and government authority" && item.External);
            bool routeJourney = report.AuthorityMap.Any(item => item.Domain == "route graph" && item.Authoritative)
                && report.AuthorityMap.Any(item => item.Domain == "journey records" && item.Authoritative);
            bool visibility = report.AuthorityMap.Any(item => item.Domain == "visibility and redaction" && item.External);
            bool valid = report.Succeeded && physical && legal && routeJourney && visibility;
            return TestLabAssertions.True("step14-integration-concept-separation", "Logical, scene, legal, route, journey, and visibility responsibilities stay separated", valid, $"Success={report.Succeeded} Physical={physical} Legal={legal} RouteJourney={routeJourney} Visibility={visibility}");
        }

        private static TestLabAutomationStepResult IntegrationDeterministicFingerprint(TestLabAutomationContext context)
        {
            Step14PersistenceSnapshotSource source = Source(context);
            long locationRevision = Runtime(context)?.Revision ?? -1L;
            long entityRevision = EntityRuntime(context)?.Revision ?? -1L;
            long routeRevision = RouteRuntime(context)?.Revision ?? -1L;
            string first = Step14WorldIntegrationValidator.CreateCanonicalFingerprint(new Step14IntegrationSnapshot(source));
            string second = Step14WorldIntegrationValidator.CreateCanonicalFingerprint(new Step14IntegrationSnapshot(source.Clone()));
            bool noMutation = locationRevision == (Runtime(context)?.Revision ?? -2L)
                && entityRevision == (EntityRuntime(context)?.Revision ?? -2L)
                && routeRevision == (RouteRuntime(context)?.Revision ?? -2L);
            bool valid = !string.IsNullOrWhiteSpace(first) && first == second && noMutation;
            return TestLabAssertions.True("step14-integration-fingerprint", "Integrated save graph fingerprint is deterministic and non-mutating", valid, $"First={first} Second={second} NoMutation={noMutation}");
        }

        private static TestLabAutomationStepResult IntegrationStep15Contract(TestLabAutomationContext context)
        {
            Step14Step15HandoffContract contract = Step14WorldIntegrationValidator.CreateStep15Contract();
            bool queries = contract.QueryCapabilities.Contains("get-current-location") && contract.QueryCapabilities.Contains("plan-route") && contract.QueryCapabilities.Contains("evaluate-political-travel-requirements");
            bool commands = contract.CommandCapabilities.Contains("relocate-entity") && contract.CommandCapabilities.Contains("start-journey") && contract.CommandCapabilities.Contains("record-border-crossing");
            bool deferred = contract.DeferredBoundaries.Contains("autonomous-npc-decision-making") && contract.DeferredBoundaries.Contains("multiplayer-authority");
            bool valid = contract.Succeeded && queries && commands && deferred;
            return TestLabAssertions.True("step14-integration-step15", "Step 15 receives explicit read and command contracts", valid, $"References={contract.StableReferenceTypes.Count} Queries={contract.QueryCapabilities.Count} Commands={contract.CommandCapabilities.Count} Deferred={contract.DeferredBoundaries.Count}");
        }

        private static TestLabAutomationStepResult IntegrationCorruptGraphRejection(TestLabAutomationContext context)
        {
            Step14PersistenceSnapshotSource corrupt = Source(context).Clone();
            corrupt.entityLocations.worldId = "world.corrupt";
            corrupt.entityLocations.placements.Add(new EntityPlacementRecordData
            {
                placementId = context.ScenarioContext.ScopedId("placement.corrupt", "missing-location"),
                worldId = corrupt.worldId,
                exactLocationId = "location.prototype.missing",
                entity = Body(context.ScenarioContext.ScopedId("body.prototype.corrupt", "traveler"), context),
                lifecycleState = EntityPlacementLifecycleState.Active
            });
            Step14IntegrationValidationReport report = Step14WorldIntegrationValidator.Validate(new Step14IntegrationSnapshot(corrupt));
            bool rejectedWorld = report.Failures.Any(item => item.Domain == Step14IntegrationDiagnosticDomain.WorldScope);
            bool rejectedPlacement = report.Failures.Any(item => item.Domain == Step14IntegrationDiagnosticDomain.EntityPlacement || item.Domain == Step14IntegrationDiagnosticDomain.Persistence);
            bool valid = !report.Succeeded && rejectedWorld && rejectedPlacement;
            return TestLabAssertions.True("step14-integration-corrupt", "Aggregate validation rejects corrupt cross-runtime state before consumers use it", valid, $"Success={report.Succeeded} Readiness={report.Readiness} World={rejectedWorld} Placement={rejectedPlacement} Failures={report.Failures.Count}");
        }

        private static WorldSceneBindingRuntime SceneBindingRuntime(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            WorldSceneBindingRuntime runtime = new WorldSceneBindingRuntime();
            runtime.Configure(
                Runtime(context),
                EntityRuntime(context),
                InteractionRuntime(context),
                ConnectionRuntime(context),
                RouteRuntime(context),
                JourneyRuntime(context),
                PoliticalRuntime(context),
                runtimes?.WorldId ?? PersistenceService.LocalWorldId);
            return runtime;
        }

        private static T NewBinding<T>(string name) where T : Component
        {
            GameObject obj = new GameObject(name);
            return obj.AddComponent<T>();
        }

        private static void DestroyBindings(params Component[] bindings)
        {
            foreach (Component binding in bindings)
            {
                if (binding == null)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(binding.gameObject);
            }
        }

        private static LocationRuntime Runtime(TestLabAutomationContext context)
        {
            return context?.ScenarioContext?.Runtimes?.Locations;
        }

        private static PoliticalTravelRuntime PoliticalRuntime(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            PoliticalTravelRuntime runtime = runtimes?.PoliticalTravel;
            runtime?.Configure(runtimes.DefinitionRegistry, runtimes.Governments, runtimes.Laws, runtimes.Crimes, runtimes.Justice, runtimes.Locations, runtimes.LocationRoutes, runtimes.WorldId);
            return runtime;
        }

        private static EntityLocationRuntime EntityRuntime(TestLabAutomationContext context)
        {
            return context?.ScenarioContext?.Runtimes?.EntityLocations;
        }

        private static InteractionPointRuntime InteractionRuntime(TestLabAutomationContext context)
        {
            return context?.ScenarioContext?.Runtimes?.InteractionPoints;
        }

        private static LocationConnectionRuntime ConnectionRuntime(TestLabAutomationContext context)
        {
            return context?.ScenarioContext?.Runtimes?.LocationConnections;
        }

        private static LocationRouteRuntime RouteRuntime(TestLabAutomationContext context)
        {
            return context?.ScenarioContext?.Runtimes?.LocationRoutes;
        }

        private static TravelJourneyRuntime JourneyRuntime(TestLabAutomationContext context)
        {
            return context?.ScenarioContext?.Runtimes?.TravelJourneys;
        }

        private static TravelConditionRuntime ConditionRuntime(TestLabAutomationContext context)
        {
            TravelConditionRuntime runtime = context?.ScenarioContext?.Runtimes?.TravelConditions;
            if (runtime == null)
            {
                return null;
            }

            runtime.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, RouteRuntime(context), JourneyRuntime(context), context.ScenarioContext.Runtimes.WorldId);
            RouteRuntime(context)?.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, Runtime(context), ConnectionRuntime(context), context.ScenarioContext.Runtimes.WorldId, runtime);
            JourneyRuntime(context)?.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, Runtime(context), EntityRuntime(context), ConnectionRuntime(context), RouteRuntime(context), context.ScenarioContext.Runtimes.WorldId, runtime);
            return runtime;
        }

        private static Step14PersistenceSnapshotSource Source(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            return Step14PersistenceSnapshotSource.FromRuntimes(
                Runtime(context),
                EntityRuntime(context),
                InteractionRuntime(context),
                ConnectionRuntime(context),
                RouteRuntime(context),
                JourneyRuntime(context),
                ConditionRuntime(context),
                PoliticalRuntime(context),
                runtimes?.WorldId ?? PersistenceService.LocalWorldId,
                context?.RunId ?? string.Empty,
                0d);
        }

        private static MovementHistoryService History(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            return MovementHistoryService.FromRuntimes(
                Runtime(context),
                EntityRuntime(context),
                InteractionRuntime(context),
                ConnectionRuntime(context),
                RouteRuntime(context),
                JourneyRuntime(context),
                ConditionRuntime(context),
                PoliticalRuntime(context),
                runtimes?.WorldId ?? PersistenceService.LocalWorldId,
                context?.RunId ?? string.Empty,
                0d);
        }

        private static string TimelineKey(MovementTimelineEntry entry)
        {
            if (entry == null) return string.Empty;
            return $"{entry.WorldTime:000000000000.000000}:{(int)entry.Kind:000}:{entry.Priority:000}:{entry.SourceParticipantId}:{entry.SourceRecordId}:{entry.JourneyStepId}";
        }

        private static bool TryPreparePoliticalTravelFixture(TestLabAutomationContext context, string suffix, out PoliticalTravelAutomationFixture fixture, out string failure)
        {
            fixture = null;
            failure = string.Empty;
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            if (runtimes == null) { failure = "Test Lab runtime bundle is missing."; return false; }
            PoliticalTravelRuntime runtime = PoliticalRuntime(context);
            if (runtime == null) { failure = "PoliticalTravelRuntime is missing from the Test Lab runtime bundle."; return false; }
            if (runtimes.Governments == null || runtimes.Laws == null || runtimes.Crimes == null || runtimes.Locations == null || runtimes.LocationRoutes == null)
            {
                failure = "Required Step 13 or Step 14 owner runtime is missing.";
                return false;
            }

            string scope = string.IsNullOrWhiteSpace(suffix) ? "default" : suffix.Trim();
            fixture = new PoliticalTravelAutomationFixture(context, runtimes, runtime, scope);
            if (!fixture.SeedGovernmentGraph(out failure))
            {
                return false;
            }

            runtime.Configure(runtimes.DefinitionRegistry, runtimes.Governments, runtimes.Laws, runtimes.Crimes, runtimes.Justice, runtimes.Locations, runtimes.LocationRoutes, runtimes.WorldId);
            return true;
        }

        private static TestLabAutomationStepResult PoliticalTravelFail(string stepId, string failure)
        {
            return TestLabAssertions.Fail(stepId, "Prepare political travel fixture", "PoliticalTravelFixture", "Present", "Missing", failure);
        }

        private sealed class PoliticalTravelAutomationFixture
        {
            public const string OriginLocationId = "location.prototype.village";
            public const string DestinationLocationId = "location.prototype.market-district";

            private readonly TestLabAutomationContext context;
            private readonly string scope;

            public PoliticalTravelAutomationFixture(TestLabAutomationContext context, TestLabRuntimeBundle runtimes, PoliticalTravelRuntime politicalTravel, string scope)
            {
                this.context = context;
                Runtimes = runtimes;
                PoliticalTravel = politicalTravel;
                this.scope = scope ?? "default";
                TravelerPersonId = PrototypeEntityLocationFactory.PlayerPersonId;
                OriginPolityId = Id("polity", "origin");
                DestinationPolityId = Id("polity", "destination");
                OriginGovernmentId = Id("government", "origin");
                DestinationGovernmentId = Id("government", "destination");
                OriginTerritoryId = Id("political-territory", "origin");
                DestinationTerritoryId = Id("political-territory", "destination");
                OriginJurisdictionId = Id("jurisdiction", "origin-border");
                DestinationJurisdictionId = Id("jurisdiction", "destination-border");
            }

            public TestLabRuntimeBundle Runtimes { get; }
            public PoliticalTravelRuntime PoliticalTravel { get; }
            public string TravelerPersonId { get; }
            public string OriginPolityId { get; }
            public string DestinationPolityId { get; }
            public string OriginGovernmentId { get; }
            public string DestinationGovernmentId { get; }
            public string OriginTerritoryId { get; }
            public string DestinationTerritoryId { get; }
            public string OriginJurisdictionId { get; }
            public string DestinationJurisdictionId { get; }

            public bool SeedGovernmentGraph(out string failure)
            {
                failure = string.Empty;
                PoliticalOperationResult originPolity = Runtimes.Governments.CreatePolity(new PolityCreateRequest { transactionId = Tx("polity-origin"), polityId = OriginPolityId, polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Origin Realm", worldTime = 0d });
                PoliticalOperationResult destinationPolity = Runtimes.Governments.CreatePolity(new PolityCreateRequest { transactionId = Tx("polity-destination"), polityId = DestinationPolityId, polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Destination Realm", worldTime = 0d });
                PoliticalOperationResult originGovernment = Runtimes.Governments.RegisterGovernment(new GovernmentRegisterRequest { transactionId = Tx("government-origin"), governmentId = OriginGovernmentId, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, polityId = OriginPolityId, officialName = "Origin Government", primaryGoverningOrganizationId = "organization.prototype.adventurers-guild", governingOrganizationIds = new[] { "organization.prototype.adventurers-guild" }, level = GovernmentLevel.Central, worldTime = 0d });
                PoliticalOperationResult destinationGovernment = Runtimes.Governments.RegisterGovernment(new GovernmentRegisterRequest { transactionId = Tx("government-destination"), governmentId = DestinationGovernmentId, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, polityId = DestinationPolityId, officialName = "Destination Government", primaryGoverningOrganizationId = "organization.prototype.adventurers-guild", governingOrganizationIds = new[] { "organization.prototype.adventurers-guild" }, level = GovernmentLevel.Central, worldTime = 0d });
                PoliticalOperationResult originTerritory = Runtimes.Governments.CreateTerritory(new TerritoryCreateRequest { transactionId = Tx("territory-origin"), territoryId = OriginTerritoryId, territoryDefinitionId = PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId, displayName = "Origin Territory", polityId = OriginPolityId, primaryGovernmentId = OriginGovernmentId, placeIds = new[] { OriginLocationId }, worldTime = 0d });
                PoliticalOperationResult destinationTerritory = Runtimes.Governments.CreateTerritory(new TerritoryCreateRequest { transactionId = Tx("territory-destination"), territoryId = DestinationTerritoryId, territoryDefinitionId = PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId, displayName = "Destination Territory", polityId = DestinationPolityId, primaryGovernmentId = DestinationGovernmentId, placeIds = new[] { DestinationLocationId }, worldTime = 0d });
                PoliticalOperationResult originJurisdiction = Runtimes.Governments.CreateJurisdiction(new JurisdictionCreateRequest { transactionId = Tx("jurisdiction-origin"), jurisdictionId = OriginJurisdictionId, jurisdictionDefinitionId = PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId, governmentId = OriginGovernmentId, category = JurisdictionCategory.GeneralGovernment, scopeDimensions = JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.SubjectMatter, subjectMatters = new[] { JurisdictionSubjectMatter.BorderAdministration }, territoryIds = new[] { OriginTerritoryId }, priority = 100, worldTime = 0d });
                PoliticalOperationResult destinationJurisdiction = Runtimes.Governments.CreateJurisdiction(new JurisdictionCreateRequest { transactionId = Tx("jurisdiction-destination"), jurisdictionId = DestinationJurisdictionId, jurisdictionDefinitionId = PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId, governmentId = DestinationGovernmentId, category = JurisdictionCategory.GeneralGovernment, scopeDimensions = JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.SubjectMatter, subjectMatters = new[] { JurisdictionSubjectMatter.BorderAdministration }, territoryIds = new[] { DestinationTerritoryId }, priority = 100, worldTime = 0d });

                PoliticalOperationResult[] results = { originPolity, destinationPolity, originGovernment, destinationGovernment, originTerritory, destinationTerritory, originJurisdiction, destinationJurisdiction };
                if (results.All(result => result.Succeeded)) return true;
                failure = string.Join(" | ", results.Where(result => !result.Succeeded).Select(result => $"{result.Code}: {result.Message}"));
                return false;
            }

            public PoliticalTravelEvaluationRequest Evaluation(TravelLegalComplianceMode mode, PoliticalTravelVisibilityMode visibility = PoliticalTravelVisibilityMode.Privileged, bool physicalTravelPossible = true)
            {
                return new PoliticalTravelEvaluationRequest
                {
                    travelerPersonId = TravelerPersonId,
                    originLocationId = OriginLocationId,
                    destinationLocationId = DestinationLocationId,
                    routeSegmentId = PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId,
                    physicalTravelPossible = physicalTravelPossible,
                    legalComplianceMode = mode,
                    visibilityMode = visibility,
                    worldTime = 20d
                };
            }

            public PoliticalTravelCrossingRequest Crossing(string localSuffix, TravelLegalComplianceMode mode)
            {
                return new PoliticalTravelCrossingRequest
                {
                    transactionId = Tx($"crossing-{localSuffix}"),
                    crossingId = Id("political-travel-crossing", localSuffix),
                    travelerPersonId = TravelerPersonId,
                    originLocationId = OriginLocationId,
                    destinationLocationId = DestinationLocationId,
                    routeSegmentId = PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId,
                    physicalTravelPossible = true,
                    legalComplianceMode = mode,
                    visibilityMode = PoliticalTravelVisibilityMode.Privileged,
                    worldTime = 20d
                };
            }

            public BorderCheckpointCreateRequest Checkpoint(BorderCheckpointPolicy policy, string localSuffix = "market-gate")
            {
                return new BorderCheckpointCreateRequest
                {
                    transactionId = Tx($"checkpoint-{localSuffix}"),
                    checkpointId = Id("border-checkpoint", localSuffix),
                    displayName = "Market Gate",
                    routeSegmentId = PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId,
                    sourceTerritoryId = OriginTerritoryId,
                    destinationTerritoryId = DestinationTerritoryId,
                    governingGovernmentId = DestinationGovernmentId,
                    jurisdictionId = DestinationJurisdictionId,
                    policy = policy,
                    lifecycleState = BorderCheckpointLifecycleState.Active,
                    worldTime = 0d,
                    sourceEventId = "testlab.feature.14.9",
                    provenanceId = "testlab.feature.14.9"
                };
            }

            public TravelCrossingAuthorizationRequest Authorization(string checkpointId)
            {
                return new TravelCrossingAuthorizationRequest
                {
                    transactionId = Tx("authorization"),
                    authorizationId = Id("travel-authorization", "destination"),
                    travelerPersonId = TravelerPersonId,
                    checkpointId = checkpointId,
                    territoryId = DestinationTerritoryId,
                    jurisdictionId = DestinationJurisdictionId,
                    issuingGovernmentId = DestinationGovernmentId,
                    authorizedActionIds = new[] { PoliticalTravelRuntime.PassCheckpointActionId },
                    effectiveWorldTime = 0d
                };
            }

            public LegalOperationResult EnactTravelLaw(string localSuffix, string actionId, LegalEffectCategory effect)
            {
                string provisionDefinitionId = effect == LegalEffectCategory.Prohibition ? PrototypeLegalDefinitionFactory.ProhibitionProvisionId : PrototypeLegalDefinitionFactory.PermissionProvisionId;
                return Runtimes.Laws.Enact(new EnactLegalInstrumentRequest
                {
                    transactionId = Tx($"law-{localSuffix}"),
                    instrumentId = Id("legal-instrument", localSuffix),
                    instrumentDefinitionId = PrototypeLegalDefinitionFactory.CentralStatuteId,
                    authorityDefinitionId = PrototypeLegalDefinitionFactory.SovereignAuthorityId,
                    title = "Political Travel Law",
                    governmentId = DestinationGovernmentId,
                    organizationId = "organization.prototype.adventurers-guild",
                    jurisdictionIds = new[] { DestinationJurisdictionId },
                    enactmentWorldTime = 1d,
                    publicationWorldTime = 1d,
                    effectiveWorldTime = 1d,
                    published = true,
                    trustedSystemOperation = true,
                    provisions = new[]
                    {
                        new LegalProvisionCreateRequest
                        {
                            provisionId = Id("legal-provision", localSuffix),
                            provisionDefinitionId = provisionDefinitionId,
                            version = new LegalProvisionVersionData
                            {
                                effect = effect,
                                actionId = actionId,
                                territoryIds = new[] { DestinationTerritoryId },
                                effectiveWorldTime = 1d
                            }
                        }
                    }
                });
            }

            private string Id(string prefix, string localSuffix)
            {
                return context.ScenarioContext.ScopedId($"{prefix}.testlab.political-travel", $"{scope}.{localSuffix}");
            }

            private string Tx(string localSuffix)
            {
                return context.ScenarioContext.ScopedId("political-travel.tx", $"{scope}.{localSuffix}");
            }
        }

        private static TravelConditionTargetReferenceData RouteTarget(string routeSegmentId, TestLabAutomationContext context)
        {
            return new TravelConditionTargetReferenceData
            {
                scope = TravelConditionTargetScope.RouteSegment,
                targetId = routeSegmentId,
                sourceLocationId = "location.prototype.village",
                destinationLocationId = routeSegmentId == PrototypeLocationRouteDefinitionFactory.VillageWildernessTrailSegmentId ? "location.prototype.wilderness-ring" : "location.prototype.market-district",
                edgeKind = RouteEdgeKind.RouteSegment,
                traveler = Body(PrototypeEntityLocationFactory.PlayerBodyId, context)
            };
        }

        private static TravelConditionOperationResult CreateRouteCondition(TestLabAutomationContext context, TravelConditionRuntime runtime, string suffix, string definitionId, string routeSegmentId)
        {
            return runtime.CreateCondition(new TravelConditionCreateRequest
            {
                transactionId = Tx(context, $"travel-condition-{suffix}"),
                conditionId = context.ScenarioContext.ScopedId("travel-condition.prototype.test", suffix),
                conditionDefinitionId = definitionId,
                target = RouteTarget(routeSegmentId, context),
                lifecycleState = TravelConditionLifecycleState.Active,
                startsWorldTime = 0d,
                sourceEventId = "testlab.feature.14.8",
                provenanceId = "testlab.feature.14.8"
            });
        }

        private static EntityLocationReferenceData Person(string id, TestLabAutomationContext context)
        {
            return PrototypeEntityLocationFactory.Person(id, context.ScenarioContext.Runtimes.WorldId);
        }

        private static EntityLocationReferenceData Body(string id, TestLabAutomationContext context)
        {
            return PrototypeEntityLocationFactory.Body(id, context.ScenarioContext.Runtimes.WorldId);
        }

        private static EntityLocationReferenceData Item(string id, TestLabAutomationContext context)
        {
            return PrototypeEntityLocationFactory.Item(id, context.ScenarioContext.Runtimes.WorldId);
        }

        private static LocationOperationResult Create(TestLabAutomationContext context, string suffix, string definitionId, string officialName, IEnumerable<string> tags = null, string organizationId = null, LocationVisibility visibility = LocationVisibility.Public, string binding = null, bool preview = false)
        {
            LocationRuntime runtime = Runtime(context);
            return runtime.CreateLocation(new LocationCreateRequest
            {
                transactionId = Tx(context, suffix),
                locationId = Id(context, suffix),
                locationDefinitionId = definitionId,
                officialName = officialName,
                commonName = officialName,
                semanticTagIds = tags ?? Array.Empty<string>(),
                associatedOrganizationId = organizationId,
                visibility = visibility,
                prototypeSceneBindingKey = binding,
                createdWorldTime = 10d,
                sourceEventId = $"location-source.{context.RunId}",
                provenanceId = "testlab.feature.14.1",
                preview = preview
            });
        }

        private static string Id(TestLabAutomationContext context, string suffix)
        {
            return context.ScenarioContext.ScopedId("location.prototype.test", suffix);
        }

        private static string Tx(TestLabAutomationContext context, string suffix)
        {
            return context.ScenarioContext.ScopedId("location.tx", suffix);
        }

        private static TravelJourneyOperationResult CreateJourney(
            TestLabAutomationContext context,
            string suffix,
            string destination,
            LocationRoutePlan acceptedRoutePlan = null,
            double rate = -1d,
            TravelJourneyVisibility visibility = TravelJourneyVisibility.Public,
            TravelConditionEvaluationMode conditionMode = TravelConditionEvaluationMode.IgnoreDynamicConditions)
        {
            EntityLocationReferenceData traveler = Body(PrototypeEntityLocationFactory.PlayerBodyId, context);
            return JourneyRuntime(context).CreateJourney(new TravelJourneyCreateRequest
            {
                transactionId = Tx(context, $"journey-create-{suffix}"),
                journeyId = context.ScenarioContext.ScopedId("travel-journey.prototype.test", suffix),
                traveler = traveler,
                controller = Person(PrototypeEntityLocationFactory.PlayerPersonId, context),
                originLocationId = "location.prototype.village",
                destinationLocationId = destination,
                acceptedRoutePlan = acceptedRoutePlan,
                travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId,
                objective = RoutePlanningObjective.ShortestDistance,
                accessMode = RouteAccessEvaluationMode.RequireCurrentAccess,
                accessContext = AccessContext(context, traveler),
                movementRateOverrideMetersPerSecond = rate,
                visibility = visibility,
                worldTime = 10d,
                sourceEventId = "testlab.feature.14.7",
                provenanceId = "testlab.feature.14.7"
            });
        }

        private static TravelJourneyLifecycleRequest Lifecycle(TestLabAutomationContext context, string journeyId, string suffix, double worldTime, double rate = -1d, bool preview = false, TravelConditionEvaluationMode conditionMode = TravelConditionEvaluationMode.IgnoreDynamicConditions)
        {
            EntityLocationReferenceData traveler = Body(PrototypeEntityLocationFactory.PlayerBodyId, context);
            return new TravelJourneyLifecycleRequest
            {
                transactionId = Tx(context, $"journey-{suffix}"),
                journeyId = journeyId,
                actor = Person(PrototypeEntityLocationFactory.PlayerPersonId, context),
                accessContext = AccessContext(context, traveler),
                conditionEvaluationMode = conditionMode,
                movementRateOverrideMetersPerSecond = rate,
                worldTime = worldTime,
                sourceEventId = "testlab.feature.14.7",
                provenanceId = "testlab.feature.14.7",
                preview = preview
            };
        }

        private static LocationRouteSearchRequest RouteRequest(
            TestLabAutomationContext context,
            string origin,
            string destination,
            RouteAccessEvaluationMode accessMode = RouteAccessEvaluationMode.StructuralOnly,
            LocationConnectionAccessContextData accessContext = null,
            RoutePlanningObjective objective = RoutePlanningObjective.ShortestDistance,
            bool includeHidden = false,
            EntityLocationReferenceData traveler = null)
        {
            EntityLocationReferenceData actor = traveler ?? Person(PrototypeEntityLocationFactory.PlayerPersonId, context);
            return new LocationRouteSearchRequest
            {
                requestId = Tx(context, $"route-request-{origin}-{destination}-{objective}-{accessMode}"),
                traveler = actor,
                originLocationId = origin,
                destinationLocationId = destination,
                travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId,
                objective = objective,
                accessMode = accessMode,
                accessContext = accessContext ?? AccessContext(context, actor),
                includeHiddenDevelopmentRoutes = includeHidden,
                worldTime = 20d
            };
        }

        private static LocationRouteMutationResult CreateRouteSegment(TestLabAutomationContext context, string suffix, string source, string destination, double distance, double cost)
        {
            string segmentId = context.ScenarioContext.ScopedId("route-segment.test", suffix);
            return RouteRuntime(context).CreateSegment(new LocationRouteSegmentCreateRequest
            {
                transactionId = Tx(context, $"route-create-{suffix}"),
                segmentId = segmentId,
                segmentDefinitionId = PrototypeLocationRouteDefinitionFactory.StreetSegmentDefinitionId,
                displayName = segmentId,
                sourceLocationId = source,
                destinationLocationId = destination,
                directionality = LocationConnectionDirectionality.Bidirectional,
                distanceMeters = distance,
                baseCostUnits = cost,
                supportedTravelModeDefinitionIds = new[] { PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId },
                visibility = RouteVisibility.Public,
                worldTime = 20d,
                sourceEventId = "testlab.feature.14.6",
                provenanceId = "testlab.feature.14.6"
            });
        }

        private static LocationConnectionOperationResult CreateConnection(TestLabAutomationContext context, string suffix, string source, string destination, bool preview = false, string transactionId = null)
        {
            string connectionId = context.ScenarioContext.ScopedId("location-connection.test", suffix);
            return ConnectionRuntime(context).CreateConnection(new LocationConnectionCreateRequest
            {
                transactionId = transactionId ?? Tx(context, $"connection-{suffix}"),
                connectionId = connectionId,
                connectionDefinitionId = PrototypeLocationConnectionDefinitionFactory.PublicDoorwayDefinitionId,
                displayName = connectionId,
                sourceLocationId = source,
                destinationLocationId = destination,
                accessPolicyDefinitionIds = new[] { PrototypeLocationConnectionDefinitionFactory.PublicAccessPolicyId },
                sceneBindingKey = $"prototype.connection.{suffix}",
                sceneBindingCategory = LocationConnectionSceneBindingCategory.PrototypeMarker,
                worldTime = 10d,
                sourceEventId = "testlab.feature.14.5",
                provenanceId = "testlab.feature.14.5",
                preview = preview
            });
        }

        private static LocationConnectionOperationResult UnlockConnection(TestLabAutomationContext context, string connectionId)
        {
            return ConnectionRuntime(context).MutateState(new LocationConnectionStateMutationRequest
            {
                transactionId = Tx(context, $"connection-unlock-{connectionId}"),
                connectionId = connectionId,
                openState = LocationConnectionOpenState.Open,
                lockState = LocationConnectionLockState.Unlocked,
                blockageState = LocationConnectionBlockageState.Clear,
                worldTime = 15d
            });
        }

        private static LocationConnectionTraversalRequest Traversal(TestLabAutomationContext context, string connectionId, EntityLocationReferenceData actor, string from, string to, LocationConnectionAccessContextData accessContext, double worldTime = 20d)
        {
            return new LocationConnectionTraversalRequest
            {
                transactionId = Tx(context, $"connection-traverse-{connectionId}-{worldTime}"),
                connectionId = connectionId,
                actor = actor,
                fromLocationId = from,
                toLocationId = to,
                accessContext = accessContext,
                worldTime = worldTime,
                sourceEventId = "testlab.feature.14.5",
                provenanceId = "testlab.feature.14.5"
            };
        }

        private static LocationConnectionAccessContextData AccessContext(
            TestLabAutomationContext context,
            EntityLocationReferenceData actor,
            bool privileged = false,
            string[] organizations = null,
            string[] ranks = null,
            string[] offices = null,
            string[] authorities = null,
            string[] employments = null,
            string[] properties = null,
            string[] permits = null,
            string[] warrants = null,
            string[] custodyRoles = null,
            string[] keyDefinitions = null)
        {
            return new LocationConnectionAccessContextData
            {
                actor = actor,
                personId = PrototypeEntityLocationFactory.PlayerPersonId,
                organizationIds = organizations ?? Array.Empty<string>(),
                rankIds = ranks ?? Array.Empty<string>(),
                officeIds = offices ?? Array.Empty<string>(),
                authorityIds = authorities ?? Array.Empty<string>(),
                employmentIds = employments ?? Array.Empty<string>(),
                propertyIds = properties ?? Array.Empty<string>(),
                permitIds = permits ?? Array.Empty<string>(),
                warrantIds = warrants ?? Array.Empty<string>(),
                custodyRoleIds = custodyRoles ?? Array.Empty<string>(),
                keyDefinitionIds = keyDefinitions ?? Array.Empty<string>(),
                privileged = privileged
            };
        }

        private static InteractionSubjectReferenceData Subject(string subjectType, string subjectId, TestLabAutomationContext context)
        {
            return new InteractionSubjectReferenceData
            {
                subjectType = subjectType,
                subjectId = subjectId,
                worldId = context.ScenarioContext.Runtimes.WorldId
            };
        }

        private static InteractionPointOperationResult CreateInteractionPoint(
            TestLabAutomationContext context,
            string suffix,
            string definitionId,
            string displayName,
            string hostLocationId,
            IEnumerable<string> serviceIds,
            InteractionPointVisibility visibility = InteractionPointVisibility.Public,
            string sceneBindingKey = null,
            bool preview = false)
        {
            return InteractionRuntime(context).CreatePoint(new InteractionPointCreateRequest
            {
                transactionId = Tx(context, $"interaction-{suffix}"),
                interactionPointId = context.ScenarioContext.ScopedId("interaction-point.test", suffix),
                interactionPointDefinitionId = definitionId,
                displayName = displayName,
                hostLocationId = hostLocationId,
                hostAssignmentId = context.ScenarioContext.ScopedId("interaction-host.test", suffix),
                serviceDefinitionIds = serviceIds ?? Array.Empty<string>(),
                visibility = visibility,
                sceneBindingKey = sceneBindingKey,
                sceneBindingCategory = string.IsNullOrWhiteSpace(sceneBindingKey) ? InteractionSceneBindingCategory.None : InteractionSceneBindingCategory.PrototypeMarker,
                worldTime = 70d,
                sourceEventId = "testlab.feature.14.4",
                provenanceId = "testlab.feature.14.4",
                preview = preview
            });
        }
    }
}
#endif
