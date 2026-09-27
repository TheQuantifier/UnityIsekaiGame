using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class TravelMovementProfile
    {
        public double AttributeMultiplier { get; set; } = 1d;
        public double SkillMultiplier { get; set; } = 1d;
        public double EncumbranceMultiplier { get; set; } = 1d;
        public double ConditionMultiplier { get; set; } = 1d;
        public double TerrainMultiplier { get; set; } = 1d;

        public double Resolve(double baseSpeedMetersPerSecond)
        {
            double multiplier = Valid(AttributeMultiplier) * Valid(SkillMultiplier) * Valid(EncumbranceMultiplier) * Valid(ConditionMultiplier) * Valid(TerrainMultiplier);
            return Math.Max(0.1d, Math.Min(baseSpeedMetersPerSecond * multiplier, baseSpeedMetersPerSecond * 3d));
        }

        private static double Valid(double value) => value > 0d && !double.IsNaN(value) && !double.IsInfinity(value) ? value : 1d;
    }

    public sealed class WorldTravelRequest
    {
        public string RequestId { get; set; }
        public EntityLocationReferenceData Traveler { get; set; }
        public string TravelerPersonId { get; set; }
        public string DestinationLocationId { get; set; }
        public string TravelModeDefinitionId { get; set; } = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId;
        public RoutePlanningObjective Objective { get; set; } = RoutePlanningObjective.ShortestDistance;
        public LocationConnectionAccessContextData AccessContext { get; set; }
        public IEnumerable<string> CapabilityIds { get; set; } = Array.Empty<string>();
        public IEnumerable<string> EquipmentDefinitionIds { get; set; } = Array.Empty<string>();
        public TravelMovementProfile MovementProfile { get; set; } = new TravelMovementProfile();
        public TravelLegalComplianceMode LegalComplianceMode { get; set; } = TravelLegalComplianceMode.RequireLegalTravel;
        public double WorldTime { get; set; }
        public bool Preview { get; set; }
    }

    public sealed class WorldTravelResult
    {
        private WorldTravelResult(bool succeeded, string message, LocationRoutePlan routePlan, TravelJourneySnapshot journey, PoliticalTravelEvaluationResult politicalEvaluation)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            RoutePlan = routePlan;
            Journey = journey;
            PoliticalEvaluation = politicalEvaluation;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public LocationRoutePlan RoutePlan { get; }
        public TravelJourneySnapshot Journey { get; }
        public PoliticalTravelEvaluationResult PoliticalEvaluation { get; }

        public static WorldTravelResult Success(string message, LocationRoutePlan plan, TravelJourneySnapshot journey, PoliticalTravelEvaluationResult political = null) => new WorldTravelResult(true, message, plan, journey, political);
        public static WorldTravelResult Failure(string message, LocationRoutePlan plan = null, PoliticalTravelEvaluationResult political = null) => new WorldTravelResult(false, message, plan, null, political);
    }

    public sealed class WorldTravelCoordinator
    {
        private readonly DefinitionRegistry registry;
        private readonly EntityLocationRuntime entityLocations;
        private readonly LocationRouteRuntime routes;
        private readonly TravelJourneyRuntime journeys;
        private readonly PoliticalTravelRuntime politicalTravel;
        private readonly Func<EntityLocationReferenceData, double, LocationConnectionAccessContextData> accessContextProvider;

        public WorldTravelCoordinator(DefinitionRegistry definitionRegistry, EntityLocationRuntime entityLocationRuntime, LocationRouteRuntime routeRuntime, TravelJourneyRuntime journeyRuntime, PoliticalTravelRuntime politicalTravelRuntime, Func<EntityLocationReferenceData, double, LocationConnectionAccessContextData> liveAccessContextProvider = null)
        {
            registry = definitionRegistry;
            entityLocations = entityLocationRuntime;
            routes = routeRuntime;
            journeys = journeyRuntime;
            politicalTravel = politicalTravelRuntime;
            accessContextProvider = liveAccessContextProvider;
        }

        public WorldTravelResult StartTravel(WorldTravelRequest request)
        {
            request ??= new WorldTravelRequest();
            EntityLocationReferenceData traveler = request.Traveler?.Clone();
            if (traveler == null || string.IsNullOrWhiteSpace(traveler.entityId)) return WorldTravelResult.Failure("A traveler is required.");
            if (!entityLocations.TryGetActivePlacement(traveler, out EntityPlacementSnapshot placement)) return WorldTravelResult.Failure("The traveler has no authoritative location.");

            string requestId = string.IsNullOrWhiteSpace(request.RequestId) ? NextId("travel") : request.RequestId.Trim();
            LocationConnectionAccessContextData accessContext = request.AccessContext ?? accessContextProvider?.Invoke(traveler, request.WorldTime);
            string[] capabilities = Clean(request.CapabilityIds);
            string[] equipment = Clean(request.EquipmentDefinitionIds);
            LocationRouteSearchRequest search = new LocationRouteSearchRequest
            {
                requestId = $"{requestId}.route",
                traveler = traveler,
                originLocationId = placement.ExactLocationId,
                destinationLocationId = request.DestinationLocationId,
                travelModeDefinitionId = request.TravelModeDefinitionId,
                objective = request.Objective,
                accessMode = RouteAccessEvaluationMode.RequireCurrentAccess,
                knowledgeMode = RouteKnowledgeMode.PublicKnownOnly,
                accessContext = accessContext,
                worldTime = request.WorldTime,
                maximumVisitedNodes = 512,
                maximumExpandedEdges = 2048,
                maximumDepth = 64,
                travelerCapabilityIds = capabilities,
                travelerEquipmentDefinitionIds = equipment,
                conditionEvaluationMode = TravelConditionEvaluationMode.CurrentConditions,
                legalComplianceMode = request.LegalComplianceMode,
                preview = request.Preview
            };
            LocationRouteSearchResult route = routes.PlanRoute(search);
            if (!route.Succeeded || route.Plan == null) return WorldTravelResult.Failure(route.Message);

            PoliticalTravelEvaluationResult political = EvaluatePoliticalPlan(route.Plan, request.TravelerPersonId, request.LegalComplianceMode, request.WorldTime);
            if (PoliticalBlocks(political, request.LegalComplianceMode)) return WorldTravelResult.Failure(political.Message, route.Plan, political);

            double baseSpeed = ResolveBaseSpeed(request.TravelModeDefinitionId);
            double movementRate = (request.MovementProfile ?? new TravelMovementProfile()).Resolve(baseSpeed);
            string journeyId = $"journey.{requestId}";
            TravelJourneyOperationResult created = journeys.CreateJourney(new TravelJourneyCreateRequest
            {
                transactionId = $"{requestId}.create",
                journeyId = journeyId,
                traveler = traveler,
                controller = traveler,
                originLocationId = placement.ExactLocationId,
                destinationLocationId = request.DestinationLocationId,
                acceptedRoutePlan = route.Plan,
                travelModeDefinitionId = request.TravelModeDefinitionId,
                objective = request.Objective,
                accessMode = RouteAccessEvaluationMode.RequireCurrentAccess,
                knowledgeMode = RouteKnowledgeMode.PublicKnownOnly,
                accessContext = accessContext,
                travelerCapabilityIds = capabilities,
                travelerEquipmentDefinitionIds = equipment,
                conditionEvaluationMode = TravelConditionEvaluationMode.CurrentConditions,
                legalComplianceMode = request.LegalComplianceMode,
                movementRateOverrideMetersPerSecond = movementRate,
                worldTime = request.WorldTime,
                sourceEventId = "world-travel.start",
                provenanceId = "world-travel.coordinator",
                preview = request.Preview
            });
            if (!created.Succeeded) return WorldTravelResult.Failure(created.Message, route.Plan, political);
            if (request.Preview) return WorldTravelResult.Success(created.Message, route.Plan, created.Journey, political);

            TravelJourneyOperationResult started = journeys.StartJourney(Lifecycle(request, journeyId, movementRate, $"{requestId}.start", accessContext));
            return started.Succeeded
                ? WorldTravelResult.Success(started.Message, route.Plan, started.Journey, political)
                : WorldTravelResult.Failure(started.Message, route.Plan, political);
        }

        public IReadOnlyList<WorldTravelResult> AdvanceActiveJourneys(double worldTime)
        {
            List<WorldTravelResult> results = new List<WorldTravelResult>();
            foreach (TravelJourneySnapshot journey in journeys.Journeys.Where(value => value.LifecycleState == TravelJourneyLifecycleState.Active).OrderBy(value => value.JourneyId, StringComparer.Ordinal).ToArray())
            {
                TravelJourneyStepSnapshot step = journey.CurrentStep;
                PoliticalTravelEvaluationResult political = step == null ? null : EvaluatePoliticalStep(journey, step, TravelLegalComplianceMode.RequireLegalTravel, worldTime);
                if (PoliticalBlocks(political, TravelLegalComplianceMode.RequireLegalTravel))
                {
                    results.Add(WorldTravelResult.Failure(political.Message, political: political));
                    continue;
                }

                TravelJourneyOperationResult advanced = journeys.AdvanceJourney(new TravelJourneyLifecycleRequest
                {
                    transactionId = NextId($"advance.{journey.JourneyId}"),
                    journeyId = journey.JourneyId,
                    actor = journey.Controller ?? journey.Traveler,
                    accessContext = accessContextProvider?.Invoke(journey.Traveler, worldTime) ?? new LocationConnectionAccessContextData { actor = journey.Traveler, personId = PersonId(journey.Traveler) },
                    conditionEvaluationMode = TravelConditionEvaluationMode.CurrentConditions,
                    legalComplianceMode = TravelLegalComplianceMode.RequireLegalTravel,
                    movementRateOverrideMetersPerSecond = journey.MovementRateOverrideMetersPerSecond,
                    worldTime = worldTime,
                    sourceEventId = "world-travel.advance",
                    provenanceId = "world-travel.coordinator",
                    maximumStepsToProcess = 1
                });
                if (advanced.Succeeded && step != null && advanced.Journey != null && advanced.Journey.CurrentStepIndex > journey.CurrentStepIndex)
                {
                    RecordCrossing(journey, step, worldTime);
                }

                results.Add(advanced.Succeeded
                    ? WorldTravelResult.Success(advanced.Message, null, advanced.Journey, political)
                    : WorldTravelResult.Failure(advanced.Message, political: political));
            }
            return results;
        }

        private TravelJourneyLifecycleRequest Lifecycle(WorldTravelRequest request, string journeyId, double movementRate, string transactionId, LocationConnectionAccessContextData accessContext)
        {
            return new TravelJourneyLifecycleRequest
            {
                transactionId = transactionId,
                journeyId = journeyId,
                actor = request.Traveler,
                accessContext = accessContext,
                travelerCapabilityIds = Clean(request.CapabilityIds),
                travelerEquipmentDefinitionIds = Clean(request.EquipmentDefinitionIds),
                conditionEvaluationMode = TravelConditionEvaluationMode.CurrentConditions,
                legalComplianceMode = request.LegalComplianceMode,
                movementRateOverrideMetersPerSecond = movementRate,
                worldTime = request.WorldTime,
                sourceEventId = "world-travel.start",
                provenanceId = "world-travel.coordinator",
                preview = request.Preview
            };
        }

        private PoliticalTravelEvaluationResult EvaluatePoliticalPlan(LocationRoutePlan plan, string personId, TravelLegalComplianceMode mode, double worldTime)
        {
            PoliticalTravelEvaluationResult last = null;
            foreach (LocationRoutePlanStep step in plan.Steps)
            {
                last = politicalTravel?.EvaluateCrossing(new PoliticalTravelEvaluationRequest
                {
                    traveler = plan.Traveler,
                    travelerPersonId = string.IsNullOrWhiteSpace(personId) ? PersonId(plan.Traveler) : personId,
                    originLocationId = step.SourceLocationId,
                    destinationLocationId = step.DestinationLocationId,
                    routeSegmentId = step.EdgeKind == RouteEdgeKind.RouteSegment ? step.EdgeId : string.Empty,
                    physicalTravelPossible = true,
                    legalComplianceMode = mode,
                    visibilityMode = PoliticalTravelVisibilityMode.TravelerSafe,
                    worldTime = worldTime
                });
                if (PoliticalBlocks(last, mode)) return last;
            }
            return last;
        }

        private PoliticalTravelEvaluationResult EvaluatePoliticalStep(TravelJourneySnapshot journey, TravelJourneyStepSnapshot step, TravelLegalComplianceMode mode, double worldTime)
        {
            return politicalTravel?.EvaluateCrossing(new PoliticalTravelEvaluationRequest
            {
                traveler = journey.Traveler,
                travelerPersonId = PersonId(journey.Traveler),
                originLocationId = step.SourceLocationId,
                destinationLocationId = step.DestinationLocationId,
                routeSegmentId = step.EdgeKind == RouteEdgeKind.RouteSegment ? step.EdgeId : string.Empty,
                physicalTravelPossible = true,
                legalComplianceMode = mode,
                visibilityMode = PoliticalTravelVisibilityMode.TravelerSafe,
                worldTime = worldTime
            });
        }

        private void RecordCrossing(TravelJourneySnapshot journey, TravelJourneyStepSnapshot step, double worldTime)
        {
            politicalTravel?.RecordCrossing(new PoliticalTravelCrossingRequest
            {
                transactionId = NextId($"crossing.{journey.JourneyId}.{step.SequenceIndex}"),
                crossingId = NextId("political-crossing"),
                traveler = journey.Traveler,
                travelerPersonId = PersonId(journey.Traveler),
                originLocationId = step.SourceLocationId,
                destinationLocationId = step.DestinationLocationId,
                routeSegmentId = step.EdgeKind == RouteEdgeKind.RouteSegment ? step.EdgeId : string.Empty,
                physicalTravelPossible = true,
                legalComplianceMode = TravelLegalComplianceMode.RequireLegalTravel,
                visibilityMode = PoliticalTravelVisibilityMode.TravelerSafe,
                worldTime = worldTime,
                sourceEventId = "world-travel.crossing",
                provenanceId = "world-travel.coordinator"
            });
        }

        private double ResolveBaseSpeed(string modeId)
        {
            return registry != null && registry.TryGet(modeId, out TravelModeDefinition mode) ? mode.BaseSpeedMetersPerSecond : 1.4d;
        }

        // Coordinator instances are transient and are recreated around save/load. IDs must not
        // restart from a process-local sequence or they can collide with persisted journeys and
        // transaction records created by an earlier coordinator instance.
        private static string NextId(string prefix) => $"{prefix}.{Guid.NewGuid():N}";
        private static bool PoliticalBlocks(PoliticalTravelEvaluationResult result, TravelLegalComplianceMode mode) => result != null && mode == TravelLegalComplianceMode.RequireLegalTravel && (!result.Succeeded || result.CombinedState == PhysicalLegalTravelState.LegallyBlocked);
        private string PersonId(EntityLocationReferenceData traveler)
        {
            return entityLocations != null && entityLocations.TryResolvePersonId(traveler, out string personId) ? personId : string.Empty;
        }
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
