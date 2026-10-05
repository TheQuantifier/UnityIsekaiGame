using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class TravelJourneyRuntime : IDisposable
    {
        private const long MillimetersPerMeter = 1000L;
        private readonly Dictionary<string, TravelJourneyRecordData> journeysById = new Dictionary<string, TravelJourneyRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TravelJourneyStepRecordData> stepsById = new Dictionary<string, TravelJourneyStepRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> stepIdsByJourneyId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, TravelJourneyHistoryRecordData> historyById = new Dictionary<string, TravelJourneyHistoryRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TravelJourneyTransactionRecordData> transactionsById = new Dictionary<string, TravelJourneyTransactionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> activeJourneyIdByTravelerKey = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> journeyIdsByDestination = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> journeyIdsByCurrentEdge = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private LocationRuntime locations;
        private EntityLocationRuntime entityLocations;
        private LocationConnectionRuntime connections;
        private LocationRouteRuntime routes;
        private TravelConditionRuntime travelConditions;
        private string worldId = PersistenceService.LocalWorldId;
        private bool disposed;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public string WorldId => worldId;
        public int JourneyCount => journeysById.Count;
        public int StepCount => stepsById.Count;
        public IReadOnlyList<TravelJourneySnapshot> Journeys => journeysById.Values.OrderBy(item => item.journeyId, StringComparer.Ordinal).Select(BuildSnapshot).ToArray();
        public IReadOnlyList<TravelJourneyHistoryRecordData> History => historyById.Values.OrderBy(item => item.worldTime).ThenBy(item => item.historyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public bool HasActiveJourneys
        {
            get
            {
                foreach (TravelJourneyRecordData journey in journeysById.Values)
                {
                    if (journey.lifecycleState == TravelJourneyLifecycleState.Active) return true;
                }

                return false;
            }
        }

        public void Configure(DefinitionRegistry definitionRegistry, LocationRuntime locationRuntime, EntityLocationRuntime entityLocationRuntime, LocationConnectionRuntime connectionRuntime, LocationRouteRuntime routeRuntime, string runtimeWorldId = PersistenceService.LocalWorldId, TravelConditionRuntime conditionRuntime = null)
        {
            registry = definitionRegistry ?? registry;
            locations = locationRuntime ?? locations;
            entityLocations = entityLocationRuntime ?? entityLocations;
            connections = connectionRuntime ?? connections;
            routes = routeRuntime ?? routes;
            travelConditions = conditionRuntime ?? travelConditions;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId.Trim();
            disposed = false;
            RebuildIndexes();
        }

        public TravelJourneyOperationResult CreateJourney(TravelJourneyCreateRequest request)
        {
            request ??= new TravelJourneyCreateRequest();
            long before = Revision;
            if (!Ready(before, out TravelJourneyOperationResult ready)) return ready;
            if (!ValidateRevision(request.expectedRevision, before, out TravelJourneyOperationResult revisionFailure)) return revisionFailure;
            string id = N(request.journeyId);
            if (TryDuplicate(N(request.transactionId), id, "journey.create", before, out TravelJourneyOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(id)) return Fail(TravelJourneyMutationStatus.InvalidRequest, "Journey ID is required.", before);
            if (journeysById.ContainsKey(id)) return Fail(TravelJourneyMutationStatus.Duplicate, $"Journey '{id}' already exists.", before);
            EntityLocationReferenceData traveler = NormalizeTraveler(request.traveler);
            if (!ValidTraveler(traveler)) return Fail(TravelJourneyMutationStatus.MissingTraveler, "Journey traveler is required.", before);
            if (!ValidateNoConflictingJourney(traveler, request.category, before, out TravelJourneyOperationResult conflict)) return conflict;
            if (!entityLocations.TryGetActivePlacement(traveler, out EntityPlacementSnapshot placement)) return Fail(TravelJourneyMutationStatus.MissingPlacement, "Traveler has no active exact placement.", before);

            string origin = First(request.originLocationId, placement.ExactLocationId);
            string destination = N(request.destinationLocationId);
            if (!ValidateLocation(origin, before, out _)) return Fail(TravelJourneyMutationStatus.MissingLocation, $"Origin location '{origin}' is missing.", before);
            if (!ValidateLocation(destination, before, out _)) return Fail(TravelJourneyMutationStatus.MissingLocation, $"Destination location '{destination}' is missing.", before);
            if (!string.Equals(placement.ExactLocationId, origin, StringComparison.Ordinal)) return Fail(TravelJourneyMutationStatus.InvalidRequest, $"Traveler is at '{placement.ExactLocationId}', not journey origin '{origin}'.", before);

            LocationRoutePlan plan = request.acceptedRoutePlan ?? PlanRouteForCreate(request, traveler, origin, destination);
            if (plan == null) return Fail(TravelJourneyMutationStatus.MissingRoute, "A valid route plan could not be resolved.", before);
            if (!PlanMatchesRequest(plan, traveler, origin, destination)) return Fail(TravelJourneyMutationStatus.RouteInvalid, "Accepted route plan does not match traveler, origin, and destination.", before);
            LocationRouteRevalidationResult revalidation = routes.RevalidatePlan(plan, BuildSearchRequest(request, traveler, origin, destination));
            if (!revalidation.Valid) return Fail(TravelJourneyMutationStatus.RouteStale, revalidation.Message, before);
            TravelMovementRateResult movementRate = EvaluateMovementRate(plan.TravelModeDefinitionId, request.movementRateOverrideMetersPerSecond);
            if (!movementRate.Succeeded) return Fail(TravelJourneyMutationStatus.InvalidRequest, movementRate.Diagnostics, before);

            TravelJourneyStepRecordData[] steps = plan.Steps.Select((step, index) => TravelJourneyStepRecordData.FromPlanStep(id, index, step)).ToArray();
            TravelJourneyRecordData record = new TravelJourneyRecordData
            {
                journeyId = id,
                worldId = worldId,
                traveler = traveler.Clone(),
                controller = request.controller?.Clone(),
                originLocationId = origin,
                destinationLocationId = destination,
                routePlan = TravelJourneyPlanSnapshotData.FromPlan(plan),
                travelModeDefinitionId = plan.TravelModeDefinitionId,
                movementRateOverrideMetersPerSecond = request.movementRateOverrideMetersPerSecond,
                category = request.category == TravelJourneyCategory.Unknown ? TravelJourneyCategory.OrdinaryTravel : request.category,
                lifecycleState = TravelJourneyLifecycleState.Ready,
                progressionMode = request.progressionMode == TravelJourneyProgressionMode.Unknown ? TravelJourneyProgressionMode.AutomaticLogical : request.progressionMode,
                currentStepIndex = 0,
                totalDistanceMillimeters = ToMillimeters(plan.TotalDistance.meters),
                createdWorldTime = request.worldTime,
                lastProgressWorldTime = request.worldTime,
                acceptedRouteRevision = plan.RouteRevision,
                acceptedConnectionRevision = plan.ConnectionRevision,
                visibility = request.visibility,
                sourceEventId = request.sourceEventId,
                sourceRecordId = request.sourceRecordId,
                provenanceId = request.provenanceId,
                revision = 1L
            };

            if (request.preview)
            {
                return TravelJourneyOperationResult.Success(new TravelJourneySnapshot(record, steps), "Journey creation preview.", before, before, preview: true, movementRate: movementRate);
            }

            journeysById[id] = record;
            stepIdsByJourneyId[id] = steps.Select(step => step.journeyStepId).ToList();
            foreach (TravelJourneyStepRecordData step in steps)
            {
                stepsById[step.journeyStepId] = step;
            }

            Complete(N(request.transactionId), "journey.create", id, id);
            AddHistory(record, "create", request.worldTime, request.controller ?? traveler, "Journey created.");
            Touch();
            RebuildIndexes();
            return TravelJourneyOperationResult.Success(BuildSnapshot(record), "Journey created.", before, Revision, movementRate: movementRate);
        }

        public TravelJourneyOperationResult StartJourney(TravelJourneyLifecycleRequest request)
        {
            request ??= new TravelJourneyLifecycleRequest();
            long before = Revision;
            if (!Ready(before, out TravelJourneyOperationResult ready)) return ready;
            if (!TryGetJourneyRecord(request.journeyId, before, out TravelJourneyRecordData journey, out TravelJourneyOperationResult failure)) return failure;
            if (!ValidateJourneyRevision(journey, request.expectedRevision, before, out failure)) return failure;
            if (TryDuplicate(N(request.transactionId), journey.journeyId, "journey.start", before, out TravelJourneyOperationResult duplicate)) return duplicate;
            if (journey.lifecycleState != TravelJourneyLifecycleState.Ready && journey.lifecycleState != TravelJourneyLifecycleState.Planned) return Fail(TravelJourneyMutationStatus.InvalidLifecycle, $"Journey '{journey.journeyId}' cannot start from {journey.lifecycleState}.", before);
            if (!ValidateNoConflictingJourney(journey.traveler, journey.category, before, out failure, journey.journeyId)) return failure;
            if (!ValidateTravelerAtCurrentStepSource(journey, before, out failure)) return failure;
            LocationRouteRevalidationResult revalidation = RevalidateJourney(journey, request);
            if (!revalidation.Valid) return BlockJourney(journey, request, TravelJourneyBlockReason.RouteStale, revalidation.Message, before);
            TravelMovementRateResult movementRate = EvaluateMovementRate(journey.travelModeDefinitionId, EffectiveMovementRateOverride(journey, request.movementRateOverrideMetersPerSecond));
            if (!movementRate.Succeeded) return Fail(TravelJourneyMutationStatus.InvalidRequest, movementRate.Diagnostics, before);
            TravelConditionOperationResult conditionCheck = EvaluateCurrentStepConditions(journey, request, movementRate, out movementRate);
            if (conditionCheck?.Evaluation?.HardBlocked == true) return BlockJourney(journey, request, TravelJourneyBlockReason.TravelConditionBlocked, conditionCheck.Evaluation.Diagnostics, before);
            if (conditionCheck?.Encounter != null) return BlockJourney(journey, request, TravelJourneyBlockReason.EncounterInterrupted, $"Travel encounter '{conditionCheck.Encounter.EncounterDefinitionId}' interrupted the journey.", before);
            if (!request.travelerCanMove) return BlockJourney(journey, request, TravelJourneyBlockReason.CapabilityUnavailable, "Traveler cannot move.", before);
            if (request.preview) return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "Journey start preview.", before, before, preview: true, movementRate: movementRate);

            journey.lifecycleState = TravelJourneyLifecycleState.Active;
            journey.startedWorldTime = journey.startedWorldTime < 0d ? request.worldTime : journey.startedWorldTime;
            journey.lastProgressWorldTime = request.worldTime;
            journey.blockReason = TravelJourneyBlockReason.None;
            journey.blockMessage = string.Empty;
            MarkCurrentStepActive(journey, request.worldTime);
            journey.revision++;
            Complete(N(request.transactionId), "journey.start", journey.journeyId, journey.journeyId);
            AddHistory(journey, "start", request.worldTime, request.actor ?? journey.controller ?? journey.traveler, "Journey started.");
            Touch();
            RebuildIndexes();
            return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "Journey started.", before, Revision, BuildStepSnapshot(journey.currentStepIndex, journey.journeyId), movementRate: movementRate);
        }

        public TravelJourneyOperationResult AdvanceJourney(TravelJourneyLifecycleRequest request)
        {
            request ??= new TravelJourneyLifecycleRequest();
            long before = Revision;
            if (!Ready(before, out TravelJourneyOperationResult ready)) return ready;
            if (!TryGetJourneyRecord(request.journeyId, before, out TravelJourneyRecordData journey, out TravelJourneyOperationResult failure)) return failure;
            if (!ValidateJourneyRevision(journey, request.expectedRevision, before, out failure)) return failure;
            if (TryDuplicate(N(request.transactionId), journey.journeyId, "journey.advance", before, out TravelJourneyOperationResult duplicate)) return duplicate;
            if (journey.lifecycleState != TravelJourneyLifecycleState.Active) return Fail(TravelJourneyMutationStatus.InvalidLifecycle, $"Journey '{journey.journeyId}' is {journey.lifecycleState}, not Active.", before);
            if (!request.travelerCanMove) return BlockJourney(journey, request, TravelJourneyBlockReason.CapabilityUnavailable, "Traveler cannot move.", before);
            if (journey.progressionMode == TravelJourneyProgressionMode.ExternalStepControl) return BlockJourney(journey, request, TravelJourneyBlockReason.ExternalControlRequired, "Journey requires external step completion.", before);

            LocationRouteRevalidationResult revalidation = RevalidateJourney(journey, request);
            if (!revalidation.Valid) return BlockJourney(journey, request, RevalidationBlockReason(revalidation), revalidation.Message, before);
            TravelMovementRateResult movementRate = EvaluateMovementRate(journey.travelModeDefinitionId, EffectiveMovementRateOverride(journey, request.movementRateOverrideMetersPerSecond));
            if (!movementRate.Succeeded) return BlockJourney(journey, request, TravelJourneyBlockReason.MovementRateInvalid, movementRate.Diagnostics, before);
            TravelConditionOperationResult conditionCheck = EvaluateCurrentStepConditions(journey, request, movementRate, out movementRate);
            if (conditionCheck?.Evaluation?.HardBlocked == true) return BlockJourney(journey, request, TravelJourneyBlockReason.TravelConditionBlocked, conditionCheck.Evaluation.Diagnostics, before);
            if (conditionCheck?.Encounter != null) return BlockJourney(journey, request, TravelJourneyBlockReason.EncounterInterrupted, $"Travel encounter '{conditionCheck.Encounter.EncounterDefinitionId}' interrupted the journey.", before);
            if (request.worldTime <= journey.lastProgressWorldTime)
            {
                return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "No authoritative time elapsed.", before, before, duplicate: true, movementRate: movementRate);
            }
            if (request.preview) return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "Journey advance preview.", before, before, preview: true, movementRate: movementRate);

            double remainingSeconds = request.worldTime - Math.Max(0d, journey.lastProgressWorldTime);
            int budget = Math.Max(1, request.maximumStepsToProcess);
            int processed = 0;

            while (journey.lifecycleState == TravelJourneyLifecycleState.Active && remainingSeconds >= 0d && processed < budget)
            {
                TravelJourneyStepRecordData step = CurrentStep(journey);
                if (step == null)
                {
                    CompleteJourney(journey, request.worldTime, request.actor, "Journey arrived.");
                    break;
                }

                if (step.lifecycleState == TravelJourneyStepLifecycleState.Pending || step.lifecycleState == TravelJourneyStepLifecycleState.Ready)
                {
                    step.lifecycleState = TravelJourneyStepLifecycleState.Active;
                    step.startedWorldTime = step.startedWorldTime < 0d ? Math.Max(journey.lastProgressWorldTime, request.worldTime - remainingSeconds) : step.startedWorldTime;
                    step.revision++;
                }

                if (step.edgeKind == RouteEdgeKind.LocalConnection)
                {
                    LocationConnectionOperationResult traversal = connections.Traverse(new LocationConnectionTraversalRequest
                    {
                        transactionId = $"{N(request.transactionId)}.connection.{step.sequenceIndex}",
                        connectionId = step.edgeId,
                        actor = journey.traveler.Clone(),
                        fromLocationId = step.sourceLocationId,
                        toLocationId = step.destinationLocationId,
                        accessContext = request.accessContext,
                        worldTime = request.worldTime,
                        sourceEventId = request.sourceEventId,
                        sourceRecordId = request.sourceRecordId,
                        provenanceId = request.provenanceId
                    });
                    if (!traversal.Succeeded)
                    {
                        SetBlocked(journey, step, TravelJourneyBlockReason.RouteAccessDenied, traversal.Message, request.worldTime);
                        break;
                    }

                    CompleteStep(journey, step, request.worldTime);
                    processed++;
                    continue;
                }

                double distanceLeft = Math.Max(0d, step.distanceMeters - step.CompletedDistanceMeters);
                if (distanceLeft <= 0.000001d)
                {
                    if (!RelocateForRouteStep(journey, step, request, out string relocateFailure))
                    {
                        SetBlocked(journey, step, TravelJourneyBlockReason.EdgeUnavailable, relocateFailure, request.worldTime);
                        break;
                    }

                    CompleteStep(journey, step, request.worldTime);
                    processed++;
                    continue;
                }

                double metersThisSlice = Math.Max(0d, remainingSeconds * movementRate.FinalRateMetersPerSecond);
                if (metersThisSlice + 0.000001d < distanceLeft)
                {
                    step.completedDistanceMillimeters += ToMillimeters(metersThisSlice);
                    journey.currentStepCompletedMillimeters = step.completedDistanceMillimeters;
                    journey.completedDistanceMillimeters = CompletedBeforeStep(journey) + step.completedDistanceMillimeters;
                    step.revision++;
                    journey.lastProgressWorldTime = request.worldTime;
                    journey.revision++;
                    remainingSeconds = 0d;
                    break;
                }

                double secondsUsed = movementRate.FinalRateMetersPerSecond <= 0d ? remainingSeconds : distanceLeft / movementRate.FinalRateMetersPerSecond;
                step.completedDistanceMillimeters = ToMillimeters(step.distanceMeters);
                journey.currentStepCompletedMillimeters = step.completedDistanceMillimeters;
                if (!RelocateForRouteStep(journey, step, request, out string failureMessage))
                {
                    SetBlocked(journey, step, TravelJourneyBlockReason.EdgeUnavailable, failureMessage, request.worldTime);
                    break;
                }

                CompleteStep(journey, step, request.worldTime);
                remainingSeconds = Math.Max(0d, remainingSeconds - secondsUsed);
                processed++;
            }

            journey.lastProgressWorldTime = request.worldTime;
            journey.revision++;
            Complete(N(request.transactionId), "journey.advance", journey.journeyId, journey.journeyId);
            AddHistory(journey, "advance", request.worldTime, request.actor ?? journey.controller ?? journey.traveler, $"Journey advanced {processed} step(s).");
            Touch();
            RebuildIndexes();
            return TravelJourneyOperationResult.Success(BuildSnapshot(journey), journey.lifecycleState == TravelJourneyLifecycleState.Completed ? "Journey arrived." : "Journey advanced.", before, Revision, BuildStepSnapshot(journey.currentStepIndex, journey.journeyId), movementRate: movementRate);
        }

        public TravelJourneyOperationResult CompleteExternalStep(TravelJourneyLifecycleRequest request)
        {
            request ??= new TravelJourneyLifecycleRequest();
            long before = Revision;
            if (!Ready(before, out TravelJourneyOperationResult ready)) return ready;
            if (!TryGetJourneyRecord(request.journeyId, before, out TravelJourneyRecordData journey, out TravelJourneyOperationResult failure)) return failure;
            if (journey.lifecycleState != TravelJourneyLifecycleState.Active) return Fail(TravelJourneyMutationStatus.InvalidLifecycle, "Only active journeys can complete external steps.", before);
            TravelJourneyStepRecordData step = CurrentStep(journey);
            if (step == null) return Fail(TravelJourneyMutationStatus.InvalidStep, "Journey has no current step.", before);
            if (request.preview) return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "External step completion preview.", before, before, preview: true);
            if (step.edgeKind == RouteEdgeKind.LocalConnection)
            {
                LocationConnectionOperationResult traversal = connections.Traverse(new LocationConnectionTraversalRequest { transactionId = $"{N(request.transactionId)}.connection", connectionId = step.edgeId, actor = journey.traveler.Clone(), fromLocationId = step.sourceLocationId, toLocationId = step.destinationLocationId, accessContext = request.accessContext, worldTime = request.worldTime });
                if (!traversal.Succeeded) return BlockJourney(journey, request, TravelJourneyBlockReason.RouteAccessDenied, traversal.Message, before);
            }
            else if (!RelocateForRouteStep(journey, step, request, out string relocateFailure))
            {
                return BlockJourney(journey, request, TravelJourneyBlockReason.EdgeUnavailable, relocateFailure, before);
            }

            step.completedDistanceMillimeters = ToMillimeters(step.distanceMeters);
            CompleteStep(journey, step, request.worldTime);
            journey.lastProgressWorldTime = request.worldTime;
            journey.revision++;
            Complete(N(request.transactionId), "journey.external-step", journey.journeyId, step.journeyStepId);
            AddHistory(journey, "external-step", request.worldTime, request.actor ?? journey.controller ?? journey.traveler, "External step completed.");
            Touch();
            RebuildIndexes();
            return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "External step completed.", before, Revision, BuildStepSnapshot(Math.Max(0, journey.currentStepIndex - 1), journey.journeyId));
        }

        public TravelJourneyOperationResult PauseJourney(TravelJourneyLifecycleRequest request)
        {
            return Transition(request, TravelJourneyLifecycleState.Active, TravelJourneyLifecycleState.Paused, "pause", "Journey paused.");
        }

        public TravelJourneyOperationResult ResumeJourney(TravelJourneyLifecycleRequest request)
        {
            request ??= new TravelJourneyLifecycleRequest();
            long before = Revision;
            if (!Ready(before, out TravelJourneyOperationResult ready)) return ready;
            if (!TryGetJourneyRecord(request.journeyId, before, out TravelJourneyRecordData journey, out TravelJourneyOperationResult failure)) return failure;
            if (journey.lifecycleState != TravelJourneyLifecycleState.Paused && journey.lifecycleState != TravelJourneyLifecycleState.Blocked && journey.lifecycleState != TravelJourneyLifecycleState.Suspended) return Fail(TravelJourneyMutationStatus.InvalidLifecycle, $"Journey '{journey.journeyId}' cannot resume from {journey.lifecycleState}.", before);
            if (!request.travelerCanMove) return BlockJourney(journey, request, TravelJourneyBlockReason.CapabilityUnavailable, "Traveler cannot move.", before);
            LocationRouteRevalidationResult revalidation = RevalidateJourney(journey, request);
            if (!revalidation.Valid) return BlockJourney(journey, request, RevalidationBlockReason(revalidation), revalidation.Message, before);
            TravelMovementRateResult movementRate = EvaluateMovementRate(journey.travelModeDefinitionId, EffectiveMovementRateOverride(journey, request.movementRateOverrideMetersPerSecond));
            TravelConditionOperationResult conditionCheck = EvaluateCurrentStepConditions(journey, request, movementRate, out movementRate);
            if (conditionCheck?.Evaluation?.HardBlocked == true) return BlockJourney(journey, request, TravelJourneyBlockReason.TravelConditionBlocked, conditionCheck.Evaluation.Diagnostics, before);
            if (conditionCheck?.Encounter != null) return BlockJourney(journey, request, TravelJourneyBlockReason.EncounterInterrupted, $"Travel encounter '{conditionCheck.Encounter.EncounterDefinitionId}' interrupted the journey.", before);
            if (request.preview) return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "Journey resume preview.", before, before, preview: true);
            journey.lifecycleState = TravelJourneyLifecycleState.Active;
            journey.pausedWorldTime = -1d;
            journey.lastProgressWorldTime = request.worldTime;
            journey.blockReason = TravelJourneyBlockReason.None;
            journey.blockMessage = string.Empty;
            MarkCurrentStepActive(journey, request.worldTime);
            journey.revision++;
            Complete(N(request.transactionId), "journey.resume", journey.journeyId, journey.journeyId);
            AddHistory(journey, "resume", request.worldTime, request.actor ?? journey.controller ?? journey.traveler, "Journey resumed.");
            Touch();
            RebuildIndexes();
            return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "Journey resumed.", before, Revision, BuildStepSnapshot(journey.currentStepIndex, journey.journeyId));
        }

        public TravelJourneyOperationResult CancelJourney(TravelJourneyLifecycleRequest request)
        {
            request ??= new TravelJourneyLifecycleRequest();
            long before = Revision;
            if (!Ready(before, out TravelJourneyOperationResult ready)) return ready;
            if (!TryGetJourneyRecord(request.journeyId, before, out TravelJourneyRecordData journey, out TravelJourneyOperationResult failure)) return failure;
            if (journey.IsTerminalState()) return Fail(TravelJourneyMutationStatus.InvalidLifecycle, "Terminal journeys cannot be cancelled again.", before);
            if (request.preview) return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "Journey cancellation preview.", before, before, preview: true);
            TravelJourneyStepRecordData step = CurrentStep(journey);
            if (step != null && step.lifecycleState != TravelJourneyStepLifecycleState.Completed)
            {
                step.lifecycleState = TravelJourneyStepLifecycleState.Cancelled;
                step.revision++;
            }

            journey.lifecycleState = TravelJourneyLifecycleState.Cancelled;
            journey.endedWorldTime = request.worldTime;
            journey.revision++;
            Complete(N(request.transactionId), "journey.cancel", journey.journeyId, journey.journeyId);
            AddHistory(journey, "cancel", request.worldTime, request.actor ?? journey.controller ?? journey.traveler, "Journey cancelled.");
            Touch();
            RebuildIndexes();
            return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "Journey cancelled without rewinding placement.", before, Revision);
        }

        public TravelJourneyOperationResult ReplanJourney(TravelJourneyReplanRequest request)
        {
            request ??= new TravelJourneyReplanRequest();
            long before = Revision;
            if (!Ready(before, out TravelJourneyOperationResult ready)) return ready;
            if (!TryGetJourneyRecord(request.journeyId, before, out TravelJourneyRecordData journey, out TravelJourneyOperationResult failure)) return failure;
            if (journey.IsTerminalState()) return Fail(TravelJourneyMutationStatus.InvalidLifecycle, "Terminal journeys cannot be replanned.", before);
            if (!entityLocations.TryGetActivePlacement(journey.traveler, out EntityPlacementSnapshot placement)) return Fail(TravelJourneyMutationStatus.MissingPlacement, "Traveler has no active exact placement for replanning.", before);
            string destination = First(request.destinationLocationId, journey.destinationLocationId);
            LocationRouteSearchRequest search = BuildSearchRequest(journey, request, placement.ExactLocationId, destination);
            LocationRouteSearchResult plan = routes.PlanRoute(search);
            if (!plan.Succeeded || plan.Plan == null)
            {
                return BlockJourney(journey, request, TravelJourneyBlockReason.NoReplacementRoute, plan.Message, before);
            }

            if (request.preview) return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "Journey replan preview.", before, before, preview: true);

            foreach (TravelJourneyStepRecordData future in StepsForJourney(journey.journeyId).Where(step => step.sequenceIndex >= journey.currentStepIndex && step.lifecycleState != TravelJourneyStepLifecycleState.Completed))
            {
                future.lifecycleState = TravelJourneyStepLifecycleState.SkippedByReplan;
                future.supersededByJourneyStepId = $"{journey.journeyId}.replan.{journey.replanCount + 1:000}.step.0000";
                future.revision++;
            }

            int startIndex = StepsForJourney(journey.journeyId).Count();
            TravelJourneyStepRecordData[] replacement = plan.Plan.Steps.Select((step, index) => TravelJourneyStepRecordData.FromPlanStep(journey.journeyId, startIndex + index, step)).ToArray();
            foreach (TravelJourneyStepRecordData step in replacement)
            {
                stepsById[step.journeyStepId] = step;
                AddIndex(stepIdsByJourneyId, journey.journeyId, step.journeyStepId);
            }

            journey.destinationLocationId = destination;
            journey.routePlan = TravelJourneyPlanSnapshotData.FromPlan(plan.Plan);
            journey.currentStepIndex = startIndex;
            journey.currentStepCompletedMillimeters = 0L;
            journey.totalDistanceMillimeters = journey.completedDistanceMillimeters + ToMillimeters(plan.Plan.TotalDistance.meters);
            journey.acceptedRouteRevision = plan.Plan.RouteRevision;
            journey.acceptedConnectionRevision = plan.Plan.ConnectionRevision;
            journey.lifecycleState = TravelJourneyLifecycleState.Active;
            journey.blockReason = TravelJourneyBlockReason.None;
            journey.blockMessage = string.Empty;
            journey.replanCount++;
            journey.lastProgressWorldTime = request.worldTime;
            journey.revision++;
            MarkCurrentStepActive(journey, request.worldTime);
            Complete(N(request.transactionId), "journey.replan", journey.journeyId, journey.routePlan?.routePlanId);
            AddHistory(journey, "replan", request.worldTime, request.actor ?? journey.controller ?? journey.traveler, "Journey replanned from current exact placement.");
            Touch();
            RebuildIndexes();
            return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "Journey replanned.", before, Revision, BuildStepSnapshot(journey.currentStepIndex, journey.journeyId));
        }

        public bool TryGetJourney(string journeyId, out TravelJourneySnapshot snapshot)
        {
            if (journeysById.TryGetValue(N(journeyId), out TravelJourneyRecordData record))
            {
                snapshot = BuildSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool TryGetActiveJourney(EntityLocationReferenceData traveler, out TravelJourneySnapshot snapshot)
        {
            string key = traveler?.StableKey ?? string.Empty;
            if (activeJourneyIdByTravelerKey.TryGetValue(key, out string id) && journeysById.TryGetValue(id, out TravelJourneyRecordData record))
            {
                snapshot = BuildSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<TravelJourneySnapshot> GetJourneysByDestination(string destinationLocationId)
        {
            return GetIds(journeyIdsByDestination, N(destinationLocationId)).Select(id => journeysById.TryGetValue(id, out TravelJourneyRecordData journey) ? BuildSnapshot(journey) : null).Where(item => item != null).ToArray();
        }

        public IReadOnlyList<TravelJourneySnapshot> GetJourneysByCurrentEdge(string edgeId)
        {
            return GetIds(journeyIdsByCurrentEdge, N(edgeId)).Select(id => journeysById.TryGetValue(id, out TravelJourneyRecordData journey) ? BuildSnapshot(journey) : null).Where(item => item != null).ToArray();
        }

        public TravelJourneyPhysicalContextResult GetPhysicalContext(string journeyId, double worldTime)
        {
            if (!TryGetJourney(journeyId, out TravelJourneySnapshot journey)) return null;
            EntityPlacementSnapshot exact = null;
            entityLocations?.TryGetActivePlacement(journey.Traveler, out exact);
            TravelJourneyStepSnapshot step = journey.CurrentStep;
            bool inTransit = journey.LifecycleState == TravelJourneyLifecycleState.Active
                && step != null
                && step.EdgeKind == RouteEdgeKind.RouteSegment
                && step.ProgressFraction > 0d
                && step.ProgressFraction < 1d;
            return new TravelJourneyPhysicalContextResult(journey.Traveler, journey, exact, inTransit, step?.SourceLocationId ?? exact?.ExactLocationId ?? string.Empty, step?.DestinationLocationId ?? journey.DestinationLocationId, step, step?.ProgressFraction ?? 0d, worldTime);
        }

        public TravelJourneySnapshot GetProjection(TravelJourneyProjectionRequest request)
        {
            request ??= new TravelJourneyProjectionRequest();
            if (!TryGetJourney(request.journeyId, out TravelJourneySnapshot snapshot)) return null;
            if (request.privileged || request.developmentView || snapshot.Visibility == TravelJourneyVisibility.Public || snapshot.Visibility == TravelJourneyVisibility.Restricted) return snapshot;
            if (snapshot.Visibility == TravelJourneyVisibility.Hidden && !request.includeHidden) return null;
            TravelJourneyRecordData redacted = snapshot.ToSaveData();
            redacted.destinationLocationId = string.Empty;
            redacted.routePlan = null;
            redacted.blockMessage = string.Empty;
            return new TravelJourneySnapshot(redacted, Array.Empty<TravelJourneyStepRecordData>());
        }

        public TravelMovementRateResult EvaluateMovementRate(string travelModeDefinitionId, double overrideRateMetersPerSecond = -1d)
        {
            string modeId = string.IsNullOrWhiteSpace(travelModeDefinitionId) ? PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId : travelModeDefinitionId.Trim();
            TravelModeCategory category = TravelModeCategory.Walking;
            double baseRate = 1.4d;
            if (registry != null && registry.TryGet(modeId, out TravelModeDefinition mode))
            {
                category = mode.Category;
                baseRate = mode.BaseSpeedMetersPerSecond;
            }
            double final = overrideRateMetersPerSecond > 0d ? overrideRateMetersPerSecond : baseRate;
            bool valid = final > 0d && !double.IsNaN(final) && !double.IsInfinity(final);
            return new TravelMovementRateResult(modeId, category, baseRate, overrideRateMetersPerSecond, valid ? final : 0d, valid ? "Movement rate resolved." : "Movement rate is invalid.");
        }

        private static double EffectiveMovementRateOverride(TravelJourneyRecordData journey, double requestedOverride)
        {
            return requestedOverride > 0d ? requestedOverride : journey?.movementRateOverrideMetersPerSecond ?? -1d;
        }

        public TravelJourneyRuntimeSaveData CreateSaveData()
        {
            return new TravelJourneyRuntimeSaveData
            {
                schemaVersion = TravelJourneyRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                journeys = journeysById.Values.OrderBy(item => item.journeyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                steps = stepsById.Values.OrderBy(item => item.journeyId, StringComparer.Ordinal).ThenBy(item => item.sequenceIndex).ThenBy(item => item.journeyStepId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                history = historyById.Values.OrderBy(item => item.worldTime).ThenBy(item => item.historyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                transactions = transactionsById.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray()
            };
        }

        public TravelJourneyOperationResult RestoreFromSaveData(TravelJourneyRuntimeSaveData saveData, DefinitionRegistry definitionRegistry = null, LocationRuntime locationRuntime = null, EntityLocationRuntime entityLocationRuntime = null, LocationConnectionRuntime connectionRuntime = null, LocationRouteRuntime routeRuntime = null, string expectedWorldId = PersistenceService.LocalWorldId, bool restoring = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry ?? registry, locationRuntime ?? locations, entityLocationRuntime ?? entityLocations, connectionRuntime ?? connections, routeRuntime ?? routes, expectedWorldId, out string failure)) return Fail(TravelJourneyMutationStatus.PersistenceInvalid, failure, before);
            TravelJourneyRuntimeSaveData rollback = CreateSaveData();
            try
            {
                RestoreInternal(saveData ?? new TravelJourneyRuntimeSaveData());
                registry = definitionRegistry ?? registry;
                locations = locationRuntime ?? locations;
                entityLocations = entityLocationRuntime ?? entityLocations;
                connections = connectionRuntime ?? connections;
                routes = routeRuntime ?? routes;
                worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
                disposed = false;
                RebuildIndexes();
                IsDirty = false;
                return TravelJourneyOperationResult.Success(null, restoring ? "Travel journeys restored." : "Travel journey save data loaded.", before, Revision);
            }
            catch (Exception ex)
            {
                RestoreInternal(rollback);
                RebuildIndexes();
                return Fail(TravelJourneyMutationStatus.RestoreFailed, $"Travel journey restore failed: {ex.Message}", before);
            }
        }

        public bool ValidateCurrent(out string failure)
        {
            return ValidateSaveData(CreateSaveData(), registry, locations, entityLocations, connections, routes, worldId, out failure);
        }

        public static bool ValidateSaveData(TravelJourneyRuntimeSaveData saveData, DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, LocationConnectionRuntime connections, LocationRouteRuntime routes, string expectedWorldId, out string failure)
        {
            List<string> errors = new List<string>();
            string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            if (saveData == null) errors.Add("Travel journey save data is missing.");
            else
            {
                if (saveData.schemaVersion != TravelJourneyRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported travel journey schema version {saveData.schemaVersion}.");
                if (!string.Equals(N(saveData.worldId), world, StringComparison.Ordinal)) errors.Add($"Travel journey world '{saveData.worldId}' does not match expected '{world}'.");
                HashSet<string> journeyIds = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> stepIds = new HashSet<string>(StringComparer.Ordinal);
                Dictionary<string, int> activeByTraveler = new Dictionary<string, int>(StringComparer.Ordinal);
                Dictionary<string, List<TravelJourneyStepRecordData>> stepsByJourney = new Dictionary<string, List<TravelJourneyStepRecordData>>(StringComparer.Ordinal);

                foreach (TravelJourneyStepRecordData step in saveData.steps ?? Array.Empty<TravelJourneyStepRecordData>())
                {
                    if (step == null) continue;
                    string stepId = N(step.journeyStepId);
                    if (string.IsNullOrWhiteSpace(stepId)) errors.Add("Travel journey step has no ID.");
                    else if (!stepIds.Add(stepId)) errors.Add($"Duplicate travel journey step '{stepId}'.");
                    if (string.IsNullOrWhiteSpace(step.journeyId)) errors.Add($"Travel journey step '{stepId}' has no journey ID.");
                    if (!ValidFiniteNonNegative(step.distanceMeters)) errors.Add($"Travel journey step '{stepId}' has invalid distance.");
                    if (step.completedDistanceMillimeters < 0L || step.completedDistanceMillimeters > ToMillimeters(step.distanceMeters)) errors.Add($"Travel journey step '{stepId}' has invalid completed distance.");
                    if (step.edgeKind == RouteEdgeKind.LocalConnection && connections != null && !connections.TryGetConnection(step.edgeId, out _)) errors.Add($"Travel journey step '{stepId}' references missing connection '{step.edgeId}'.");
                    if (step.edgeKind == RouteEdgeKind.RouteSegment && routes != null && !routes.TryGetSegment(step.edgeId, out _)) errors.Add($"Travel journey step '{stepId}' references missing route segment '{step.edgeId}'.");
                    AddValidationStep(stepsByJourney, step.journeyId, step);
                }

                foreach (TravelJourneyRecordData journey in saveData.journeys ?? Array.Empty<TravelJourneyRecordData>())
                {
                    if (journey == null) continue;
                    string id = N(journey.journeyId);
                    if (string.IsNullOrWhiteSpace(id)) errors.Add("Travel journey has no ID.");
                    else if (!journeyIds.Add(id)) errors.Add($"Duplicate travel journey '{id}'.");
                    if (!string.Equals(N(journey.worldId), world, StringComparison.Ordinal)) errors.Add($"Travel journey '{id}' has wrong world '{journey.worldId}'.");
                    if (journey.traveler == null || string.IsNullOrWhiteSpace(journey.traveler.entityId)) errors.Add($"Travel journey '{id}' has no traveler.");
                    if (locations != null && !locations.TryGetSnapshot(journey.originLocationId, out _)) errors.Add($"Travel journey '{id}' references missing origin '{journey.originLocationId}'.");
                    if (locations != null && !locations.TryGetSnapshot(journey.destinationLocationId, out _)) errors.Add($"Travel journey '{id}' references missing destination '{journey.destinationLocationId}'.");
                    if (journey.currentStepIndex < 0) errors.Add($"Travel journey '{id}' has invalid current step index.");
                    if (journey.completedDistanceMillimeters < 0L || journey.completedDistanceMillimeters > journey.totalDistanceMillimeters) errors.Add($"Travel journey '{id}' has invalid completed distance.");
                    if (!stepsByJourney.TryGetValue(id, out List<TravelJourneyStepRecordData> journeySteps)) journeySteps = new List<TravelJourneyStepRecordData>();
                    if (!journey.IsTerminalState() && journey.currentStepIndex > journeySteps.Count) errors.Add($"Travel journey '{id}' current step index exceeds step count.");
                    if (IsActiveOrdinary(journey))
                    {
                        string key = journey.traveler?.StableKey ?? string.Empty;
                        activeByTraveler[key] = activeByTraveler.TryGetValue(key, out int count) ? count + 1 : 1;
                    }
                }

                foreach (TravelJourneyStepRecordData step in saveData.steps ?? Array.Empty<TravelJourneyStepRecordData>())
                {
                    if (step != null && !journeyIds.Contains(N(step.journeyId))) errors.Add($"Travel journey step '{step.journeyStepId}' references missing journey '{step.journeyId}'.");
                }

                foreach (KeyValuePair<string, int> active in activeByTraveler.Where(pair => pair.Value > 1))
                {
                    errors.Add($"Traveler '{active.Key}' has {active.Value} active ordinary journeys.");
                }
            }

            failure = string.Join(" | ", errors);
            return errors.Count == 0;
        }

        public void Reset()
        {
            journeysById.Clear();
            stepsById.Clear();
            stepIdsByJourneyId.Clear();
            historyById.Clear();
            transactionsById.Clear();
            activeJourneyIdByTravelerKey.Clear();
            journeyIdsByDestination.Clear();
            journeyIdsByCurrentEdge.Clear();
            Revision = 0L;
            IsDirty = false;
            disposed = false;
        }

        public void Dispose()
        {
            Reset();
            disposed = true;
        }

        private TravelJourneyOperationResult Transition(TravelJourneyLifecycleRequest request, TravelJourneyLifecycleState expected, TravelJourneyLifecycleState target, string operation, string message)
        {
            request ??= new TravelJourneyLifecycleRequest();
            long before = Revision;
            if (!Ready(before, out TravelJourneyOperationResult ready)) return ready;
            if (!TryGetJourneyRecord(request.journeyId, before, out TravelJourneyRecordData journey, out TravelJourneyOperationResult failure)) return failure;
            if (!ValidateJourneyRevision(journey, request.expectedRevision, before, out failure)) return failure;
            if (TryDuplicate(N(request.transactionId), journey.journeyId, $"journey.{operation}", before, out TravelJourneyOperationResult duplicate)) return duplicate;
            if (journey.lifecycleState != expected) return Fail(TravelJourneyMutationStatus.InvalidLifecycle, $"Journey '{journey.journeyId}' is {journey.lifecycleState}, not {expected}.", before);
            if (request.preview) return TravelJourneyOperationResult.Success(BuildSnapshot(journey), $"{message} preview.", before, before, preview: true);
            journey.lifecycleState = target;
            if (target == TravelJourneyLifecycleState.Paused) journey.pausedWorldTime = request.worldTime;
            journey.revision++;
            Complete(N(request.transactionId), $"journey.{operation}", journey.journeyId, journey.journeyId);
            AddHistory(journey, operation, request.worldTime, request.actor ?? journey.controller ?? journey.traveler, message);
            Touch();
            RebuildIndexes();
            return TravelJourneyOperationResult.Success(BuildSnapshot(journey), message, before, Revision, BuildStepSnapshot(journey.currentStepIndex, journey.journeyId));
        }

        private TravelJourneyOperationResult BlockJourney(TravelJourneyRecordData journey, TravelJourneyLifecycleRequest request, TravelJourneyBlockReason reason, string message, long before)
        {
            if (request.preview) return TravelJourneyOperationResult.Success(BuildSnapshot(journey), "Journey block preview.", before, before, preview: true);
            TravelJourneyStepRecordData step = CurrentStep(journey);
            SetBlocked(journey, step, reason, message, request.worldTime);
            Complete(N(request.transactionId), "journey.block", journey.journeyId, step?.journeyStepId ?? journey.journeyId);
            AddHistory(journey, "block", request.worldTime, request.actor ?? journey.controller ?? journey.traveler, message);
            Touch();
            RebuildIndexes();
            return TravelJourneyOperationResult.Failure(TravelJourneyMutationStatus.Blocked, message, before, BuildSnapshot(journey), BuildStepSnapshot(journey.currentStepIndex, journey.journeyId));
        }

        private void SetBlocked(TravelJourneyRecordData journey, TravelJourneyStepRecordData step, TravelJourneyBlockReason reason, string message, double worldTime)
        {
            journey.lifecycleState = TravelJourneyLifecycleState.Blocked;
            journey.blockReason = reason == TravelJourneyBlockReason.None ? TravelJourneyBlockReason.Unknown : reason;
            journey.blockMessage = message ?? string.Empty;
            journey.lastProgressWorldTime = worldTime;
            journey.revision++;
            if (step != null && step.lifecycleState != TravelJourneyStepLifecycleState.Completed)
            {
                step.lifecycleState = TravelJourneyStepLifecycleState.Blocked;
                step.revision++;
            }
        }

        private void CompleteStep(TravelJourneyRecordData journey, TravelJourneyStepRecordData step, double worldTime)
        {
            step.lifecycleState = TravelJourneyStepLifecycleState.Completed;
            step.completedWorldTime = worldTime;
            step.completedDistanceMillimeters = ToMillimeters(step.distanceMeters);
            step.revision++;
            journey.completedDistanceMillimeters = CompletedBeforeStep(journey) + step.completedDistanceMillimeters;
            journey.currentStepCompletedMillimeters = 0L;
            journey.currentStepIndex = step.sequenceIndex + 1;
            TravelJourneyStepRecordData next = CurrentStep(journey);
            if (next != null && next.lifecycleState == TravelJourneyStepLifecycleState.Pending)
            {
                next.lifecycleState = TravelJourneyStepLifecycleState.Ready;
                next.revision++;
            }
            else if (next == null)
            {
                CompleteJourney(journey, worldTime, journey.controller ?? journey.traveler, "Journey arrived.");
            }
        }

        private void CompleteJourney(TravelJourneyRecordData journey, double worldTime, EntityLocationReferenceData actor, string message)
        {
            if (journey.lifecycleState == TravelJourneyLifecycleState.Completed) return;
            journey.lifecycleState = TravelJourneyLifecycleState.Completed;
            journey.completedDistanceMillimeters = journey.totalDistanceMillimeters;
            journey.endedWorldTime = worldTime;
            journey.blockReason = TravelJourneyBlockReason.None;
            journey.blockMessage = string.Empty;
            journey.revision++;
            AddHistory(journey, "complete", worldTime, actor ?? journey.controller ?? journey.traveler, message);
        }

        private bool RelocateForRouteStep(TravelJourneyRecordData journey, TravelJourneyStepRecordData step, TravelJourneyLifecycleRequest request, out string failure)
        {
            EntityLocationOperationResult relocation = entityLocations.Relocate(new EntityRelocationRequest
            {
                transactionId = $"{N(request.transactionId)}.route-step.{step.sequenceIndex}",
                entity = journey.traveler.Clone(),
                expectedOriginLocationId = step.sourceLocationId,
                destinationLocationId = step.destinationLocationId,
                worldTime = request.worldTime,
                sourceEventId = request.sourceEventId,
                sourceRecordId = request.sourceRecordId,
                provenanceId = request.provenanceId
            });
            failure = relocation.Message;
            return relocation.Succeeded;
        }

        private LocationRoutePlan PlanRouteForCreate(TravelJourneyCreateRequest request, EntityLocationReferenceData traveler, string origin, string destination)
        {
            LocationRouteSearchResult result = routes.PlanRoute(BuildSearchRequest(request, traveler, origin, destination));
            return result.Succeeded ? result.Plan : null;
        }

        private LocationRouteRevalidationResult RevalidateJourney(TravelJourneyRecordData journey, TravelJourneyLifecycleRequest request)
        {
            LocationRoutePlan plan = journey.routePlan?.ToRoutePlan(StepsForJourney(journey.journeyId).Where(step =>
                step.lifecycleState != TravelJourneyStepLifecycleState.SkippedByReplan
                && (step.sequenceIndex >= journey.currentStepIndex || step.lifecycleState == TravelJourneyStepLifecycleState.Completed)));
            return routes.RevalidatePlan(plan, BuildSearchRequest(journey, request, journey.routePlan?.originLocationId, journey.routePlan?.destinationLocationId));
        }

        private TravelConditionOperationResult EvaluateCurrentStepConditions(TravelJourneyRecordData journey, TravelJourneyLifecycleRequest request, TravelMovementRateResult baseMovementRate, out TravelMovementRateResult adjustedMovementRate)
        {
            adjustedMovementRate = baseMovementRate;
            if (travelConditions == null || request.conditionEvaluationMode == TravelConditionEvaluationMode.IgnoreDynamicConditions) return null;
            TravelJourneyStepRecordData step = CurrentStep(journey);
            if (step == null) return null;
            TravelConditionTargetReferenceData target = new TravelConditionTargetReferenceData
            {
                scope = step.edgeKind == RouteEdgeKind.RouteSegment ? TravelConditionTargetScope.RouteSegment : TravelConditionTargetScope.Connection,
                targetId = step.edgeId,
                sourceLocationId = step.sourceLocationId,
                destinationLocationId = step.destinationLocationId,
                edgeKind = step.edgeKind,
                journeyId = journey.journeyId,
                traveler = journey.traveler?.Clone()
            };
            TravelConditionOperationResult checkpoint = travelConditions.EvaluateJourneyCheckpoint(journey.journeyId, target, journey.traveler, journey.travelModeDefinitionId, request.travelerCapabilityIds, request.travelerEquipmentDefinitionIds, request.worldTime);
            TravelConditionEvaluationResult evaluation = checkpoint?.Evaluation ?? travelConditions.Evaluate(new TravelConditionEvaluationRequest
            {
                evaluationMode = request.conditionEvaluationMode,
                target = target,
                traveler = journey.traveler?.Clone(),
                travelModeDefinitionId = journey.travelModeDefinitionId,
                travelerCapabilityIds = Clean(request.travelerCapabilityIds),
                travelerEquipmentDefinitionIds = Clean(request.travelerEquipmentDefinitionIds),
                knownConditionIds = Clean(request.knownConditionIds),
                knownEncounterIds = Clean(request.knownEncounterIds),
                knownHazardExposureIds = Clean(request.knownHazardExposureIds),
                includeHiddenDevelopmentConditions = request.includeHiddenDevelopmentConditions,
                worldTime = request.worldTime
            });
            if (evaluation != null && baseMovementRate != null && evaluation.Succeeded)
            {
                adjustedMovementRate = new TravelMovementRateResult(
                    baseMovementRate.TravelModeDefinitionId,
                    baseMovementRate.Category,
                    baseMovementRate.BaseRateMetersPerSecond,
                    baseMovementRate.OverrideRateMetersPerSecond,
                    Math.Max(0.0001d, baseMovementRate.FinalRateMetersPerSecond * evaluation.MovementRateMultiplier),
                    $"{baseMovementRate.Diagnostics} Conditions={evaluation.Diagnostics}");
            }

            return checkpoint ?? TravelConditionOperationResult.EvaluationSuccess(evaluation, evaluation?.Diagnostics ?? "Travel conditions evaluated.", travelConditions.Revision);
        }

        private LocationRouteSearchRequest BuildSearchRequest(TravelJourneyCreateRequest request, EntityLocationReferenceData traveler, string origin, string destination)
        {
            return new LocationRouteSearchRequest
            {
                requestId = First(request.transactionId, $"journey-plan.{traveler?.StableKey}.{origin}.{destination}"),
                traveler = traveler?.Clone(),
                originLocationId = origin,
                destinationLocationId = destination,
                travelModeDefinitionId = First(request.travelModeDefinitionId, PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId),
                objective = request.objective == RoutePlanningObjective.Unknown ? RoutePlanningObjective.ShortestDistance : request.objective,
                accessMode = request.accessMode == RouteAccessEvaluationMode.Unknown ? RouteAccessEvaluationMode.RequireCurrentAccess : request.accessMode,
                knowledgeMode = request.knowledgeMode == RouteKnowledgeMode.Unknown ? RouteKnowledgeMode.AuthoritativeDevelopment : request.knowledgeMode,
                accessContext = request.accessContext?.Clone(),
                travelerCapabilityIds = Clean(request.travelerCapabilityIds),
                travelerEquipmentDefinitionIds = Clean(request.travelerEquipmentDefinitionIds),
                conditionEvaluationMode = request.conditionEvaluationMode,
                knownConditionIds = Clean(request.knownConditionIds),
                knownEncounterIds = Clean(request.knownEncounterIds),
                knownHazardExposureIds = Clean(request.knownHazardExposureIds),
                includeHiddenDevelopmentConditions = request.includeHiddenDevelopmentConditions,
                worldTime = request.worldTime
            };
        }

        private LocationRouteSearchRequest BuildSearchRequest(TravelJourneyRecordData journey, TravelJourneyLifecycleRequest request, string origin, string destination)
        {
            return new LocationRouteSearchRequest
            {
                requestId = First(request.transactionId, $"journey-revalidate.{journey.journeyId}"),
                traveler = journey.traveler?.Clone(),
                originLocationId = First(origin, journey.originLocationId),
                destinationLocationId = First(destination, journey.destinationLocationId),
                travelModeDefinitionId = journey.travelModeDefinitionId,
                objective = journey.routePlan?.objective ?? RoutePlanningObjective.ShortestDistance,
                accessMode = RouteAccessEvaluationMode.RequireCurrentAccess,
                knowledgeMode = RouteKnowledgeMode.AuthoritativeDevelopment,
                accessContext = request.accessContext?.Clone(),
                travelerCapabilityIds = Clean(request.travelerCapabilityIds),
                travelerEquipmentDefinitionIds = Clean(request.travelerEquipmentDefinitionIds),
                conditionEvaluationMode = request.conditionEvaluationMode,
                knownConditionIds = Clean(request.knownConditionIds),
                knownEncounterIds = Clean(request.knownEncounterIds),
                knownHazardExposureIds = Clean(request.knownHazardExposureIds),
                includeHiddenDevelopmentConditions = request.includeHiddenDevelopmentConditions,
                worldTime = request.worldTime
            };
        }

        private LocationRouteSearchRequest BuildSearchRequest(TravelJourneyRecordData journey, TravelJourneyReplanRequest request, string origin, string destination)
        {
            return new LocationRouteSearchRequest
            {
                requestId = First(request.transactionId, $"journey-replan.{journey.journeyId}"),
                traveler = journey.traveler?.Clone(),
                originLocationId = origin,
                destinationLocationId = destination,
                travelModeDefinitionId = journey.travelModeDefinitionId,
                objective = request.objective == RoutePlanningObjective.Unknown ? RoutePlanningObjective.ShortestDistance : request.objective,
                accessMode = request.accessMode == RouteAccessEvaluationMode.Unknown ? RouteAccessEvaluationMode.RequireCurrentAccess : request.accessMode,
                knowledgeMode = request.knowledgeMode == RouteKnowledgeMode.Unknown ? RouteKnowledgeMode.AuthoritativeDevelopment : request.knowledgeMode,
                accessContext = request.accessContext?.Clone(),
                travelerCapabilityIds = Clean(request.travelerCapabilityIds),
                travelerEquipmentDefinitionIds = Clean(request.travelerEquipmentDefinitionIds),
                conditionEvaluationMode = request.conditionEvaluationMode,
                knownConditionIds = Clean(request.knownConditionIds),
                knownEncounterIds = Clean(request.knownEncounterIds),
                knownHazardExposureIds = Clean(request.knownHazardExposureIds),
                includeHiddenDevelopmentConditions = request.includeHiddenDevelopmentConditions,
                worldTime = request.worldTime
            };
        }

        private bool PlanMatchesRequest(LocationRoutePlan plan, EntityLocationReferenceData traveler, string origin, string destination)
        {
            return plan != null
                && string.Equals(plan.OriginLocationId, origin, StringComparison.Ordinal)
                && string.Equals(plan.DestinationLocationId, destination, StringComparison.Ordinal)
                && (plan.Traveler == null || string.Equals(plan.Traveler.StableKey, traveler?.StableKey, StringComparison.Ordinal));
        }

        private bool ValidateTravelerAtCurrentStepSource(TravelJourneyRecordData journey, long before, out TravelJourneyOperationResult failure)
        {
            failure = null;
            if (!entityLocations.TryGetActivePlacement(journey.traveler, out EntityPlacementSnapshot placement)) return SetFailure(TravelJourneyMutationStatus.MissingPlacement, "Traveler has no active exact placement.", before, out failure);
            TravelJourneyStepRecordData step = CurrentStep(journey);
            string expected = step?.sourceLocationId ?? journey.originLocationId;
            if (!string.Equals(placement.ExactLocationId, expected, StringComparison.Ordinal)) return SetFailure(TravelJourneyMutationStatus.InvalidRequest, $"Traveler is at '{placement.ExactLocationId}', not current journey step source '{expected}'.", before, out failure);
            return true;
        }

        private bool ValidateNoConflictingJourney(EntityLocationReferenceData traveler, TravelJourneyCategory category, long before, out TravelJourneyOperationResult failure, string excludingJourneyId = "")
        {
            failure = null;
            if (category != TravelJourneyCategory.OrdinaryTravel) return true;
            string key = traveler?.StableKey ?? string.Empty;
            if (activeJourneyIdByTravelerKey.TryGetValue(key, out string activeId) && !string.Equals(activeId, N(excludingJourneyId), StringComparison.Ordinal))
            {
                return SetFailure(TravelJourneyMutationStatus.ConflictingActiveJourney, $"Traveler '{key}' already has active journey '{activeId}'.", before, out failure);
            }
            return true;
        }

        private bool TryGetJourneyRecord(string journeyId, long before, out TravelJourneyRecordData journey, out TravelJourneyOperationResult failure)
        {
            journey = null;
            failure = null;
            string id = N(journeyId);
            if (string.IsNullOrWhiteSpace(id)) return SetFailure(TravelJourneyMutationStatus.InvalidRequest, "Journey ID is required.", before, out failure);
            if (!journeysById.TryGetValue(id, out journey)) return SetFailure(TravelJourneyMutationStatus.MissingRoute, $"Journey '{id}' is missing.", before, out failure);
            return true;
        }

        private bool ValidateJourneyRevision(TravelJourneyRecordData journey, long expected, long before, out TravelJourneyOperationResult failure)
        {
            failure = null;
            if (expected < 0L || journey == null || expected == journey.revision) return true;
            return SetFailure(TravelJourneyMutationStatus.RevisionConflict, $"Expected journey revision {expected}, actual {journey.revision}.", before, out failure);
        }

        private bool ValidateRevision(long expected, long actual, out TravelJourneyOperationResult failure)
        {
            failure = null;
            if (expected < 0L || expected == actual) return true;
            failure = TravelJourneyOperationResult.Failure(TravelJourneyMutationStatus.RevisionConflict, $"Expected travel journey runtime revision {expected}, actual {actual}.", actual);
            return false;
        }

        private bool Ready(long before, out TravelJourneyOperationResult failure)
        {
            failure = null;
            if (disposed) return SetFailure(TravelJourneyMutationStatus.Disposed, "Travel journey runtime is disposed.", before, out failure);
            if (registry == null || locations == null || entityLocations == null || connections == null || routes == null) return SetFailure(TravelJourneyMutationStatus.MissingRuntime, "Travel journey runtime requires definitions, locations, entity locations, connections, and routes.", before, out failure);
            return true;
        }

        private bool ValidateLocation(string locationId, long before, out LocationSnapshot snapshot)
        {
            snapshot = null;
            return !string.IsNullOrWhiteSpace(locationId) && locations != null && locations.TryGetSnapshot(locationId, out snapshot);
        }

        private TravelJourneyStepRecordData CurrentStep(TravelJourneyRecordData journey)
        {
            return StepsForJourney(journey?.journeyId).FirstOrDefault(step => step.sequenceIndex == journey.currentStepIndex);
        }

        private IEnumerable<TravelJourneyStepRecordData> StepsForJourney(string journeyId)
        {
            return GetIds(stepIdsByJourneyId, N(journeyId)).Select(id => stepsById.TryGetValue(id, out TravelJourneyStepRecordData step) ? step : null).Where(step => step != null).OrderBy(step => step.sequenceIndex).ThenBy(step => step.journeyStepId, StringComparer.Ordinal);
        }

        private TravelJourneyStepSnapshot BuildStepSnapshot(int sequenceIndex, string journeyId)
        {
            TravelJourneyStepRecordData step = StepsForJourney(journeyId).FirstOrDefault(item => item.sequenceIndex == sequenceIndex);
            return step == null ? null : new TravelJourneyStepSnapshot(step);
        }

        private TravelJourneySnapshot BuildSnapshot(TravelJourneyRecordData record)
        {
            return new TravelJourneySnapshot(record, StepsForJourney(record?.journeyId));
        }

        private void MarkCurrentStepActive(TravelJourneyRecordData journey, double worldTime)
        {
            TravelJourneyStepRecordData step = CurrentStep(journey);
            if (step == null) return;
            step.lifecycleState = TravelJourneyStepLifecycleState.Active;
            if (step.startedWorldTime < 0d) step.startedWorldTime = worldTime;
            step.revision++;
        }

        private long CompletedBeforeStep(TravelJourneyRecordData journey)
        {
            return StepsForJourney(journey.journeyId).Where(step => step.sequenceIndex < journey.currentStepIndex && step.lifecycleState == TravelJourneyStepLifecycleState.Completed).Sum(step => ToMillimeters(step.distanceMeters));
        }

        private void AddHistory(TravelJourneyRecordData journey, string operation, double worldTime, EntityLocationReferenceData actor, string message)
        {
            if (journey == null) return;
            string id = $"journey-history.{journey.journeyId}.{historyById.Count + 1:000000}.{N(operation)}";
            historyById[id] = new TravelJourneyHistoryRecordData
            {
                historyId = id,
                journeyId = journey.journeyId,
                operation = N(operation),
                lifecycleState = journey.lifecycleState,
                currentStepIndex = journey.currentStepIndex,
                worldTime = worldTime,
                actorKey = actor?.StableKey ?? string.Empty,
                message = message ?? string.Empty,
                revision = Revision + 1L
            };
        }

        private void Complete(string tx, string operation, string journeyId, string result)
        {
            if (string.IsNullOrWhiteSpace(tx)) return;
            transactionsById[tx] = new TravelJourneyTransactionRecordData { transactionId = tx, operation = operation ?? string.Empty, journeyId = journeyId ?? string.Empty, resultReferenceId = result ?? string.Empty, revision = Revision + 1L };
        }

        private bool TryDuplicate(string tx, string id, string operation, long before, out TravelJourneyOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(tx)) return false;
            if (!transactionsById.TryGetValue(tx, out TravelJourneyTransactionRecordData existing)) return false;
            if (string.Equals(existing.operation, operation, StringComparison.Ordinal) && (string.IsNullOrWhiteSpace(id) || string.Equals(existing.journeyId, id, StringComparison.Ordinal)))
            {
                TravelJourneySnapshot snapshot = journeysById.TryGetValue(existing.journeyId, out TravelJourneyRecordData journey) ? BuildSnapshot(journey) : null;
                result = TravelJourneyOperationResult.Success(snapshot, "Duplicate journey transaction.", before, before, duplicate: true);
                return true;
            }
            result = TravelJourneyOperationResult.Failure(TravelJourneyMutationStatus.InvalidRequest, $"Transaction '{tx}' already exists for '{existing.operation}'.", before);
            return true;
        }

        private void Touch()
        {
            Revision++;
            IsDirty = true;
        }

        private void RebuildIndexes()
        {
            activeJourneyIdByTravelerKey.Clear();
            journeyIdsByDestination.Clear();
            journeyIdsByCurrentEdge.Clear();
            stepIdsByJourneyId.Clear();
            foreach (TravelJourneyStepRecordData step in stepsById.Values.OrderBy(step => step.sequenceIndex).ThenBy(step => step.journeyStepId, StringComparer.Ordinal))
            {
                AddIndex(stepIdsByJourneyId, step.journeyId, step.journeyStepId);
            }
            foreach (TravelJourneyRecordData journey in journeysById.Values.OrderBy(item => item.journeyId, StringComparer.Ordinal))
            {
                AddIndex(journeyIdsByDestination, journey.destinationLocationId, journey.journeyId);
                TravelJourneyStepRecordData step = CurrentStep(journey);
                if (step != null) AddIndex(journeyIdsByCurrentEdge, step.edgeId, journey.journeyId);
                if (IsActiveOrdinary(journey)) activeJourneyIdByTravelerKey[journey.traveler?.StableKey ?? string.Empty] = journey.journeyId;
            }
        }

        private void RestoreInternal(TravelJourneyRuntimeSaveData saveData)
        {
            journeysById.Clear();
            stepsById.Clear();
            historyById.Clear();
            transactionsById.Clear();
            foreach (TravelJourneyRecordData journey in saveData.journeys ?? Array.Empty<TravelJourneyRecordData>()) journeysById[N(journey.journeyId)] = journey.Clone();
            foreach (TravelJourneyStepRecordData step in saveData.steps ?? Array.Empty<TravelJourneyStepRecordData>()) stepsById[N(step.journeyStepId)] = step.Clone();
            foreach (TravelJourneyHistoryRecordData history in saveData.history ?? Array.Empty<TravelJourneyHistoryRecordData>()) historyById[N(history.historyId)] = history.Clone();
            foreach (TravelJourneyTransactionRecordData tx in saveData.transactions ?? Array.Empty<TravelJourneyTransactionRecordData>()) transactionsById[N(tx.transactionId)] = tx.Clone();
            Revision = Math.Max(0L, saveData.revision);
            worldId = string.IsNullOrWhiteSpace(saveData.worldId) ? worldId : saveData.worldId.Trim();
        }

        private static TravelJourneyBlockReason RevalidationBlockReason(LocationRouteRevalidationResult result)
        {
            return result?.Status == RoutePlanRevalidationStatus.ChangedAccess ? TravelJourneyBlockReason.RouteAccessDenied : TravelJourneyBlockReason.RouteStale;
        }

        private static bool IsActiveOrdinary(TravelJourneyRecordData journey)
        {
            return journey != null
                && journey.category == TravelJourneyCategory.OrdinaryTravel
                && (journey.lifecycleState == TravelJourneyLifecycleState.Active
                    || journey.lifecycleState == TravelJourneyLifecycleState.Paused
                    || journey.lifecycleState == TravelJourneyLifecycleState.Blocked
                    || journey.lifecycleState == TravelJourneyLifecycleState.Replanning
                    || journey.lifecycleState == TravelJourneyLifecycleState.Suspended);
        }

        private static bool ValidTraveler(EntityLocationReferenceData traveler)
        {
            return traveler != null && traveler.entityType != LocationOccupantEntityType.Unknown && !string.IsNullOrWhiteSpace(traveler.entityId);
        }

        private static EntityLocationReferenceData NormalizeTraveler(EntityLocationReferenceData traveler)
        {
            if (traveler == null) return null;
            EntityLocationReferenceData clone = traveler.Clone();
            if (string.IsNullOrWhiteSpace(clone.worldId)) clone.worldId = PersistenceService.LocalWorldId;
            return clone;
        }

        private static bool SetFailure(TravelJourneyMutationStatus status, string message, long before, out TravelJourneyOperationResult failure)
        {
            failure = TravelJourneyOperationResult.Failure(status, message, before);
            return false;
        }

        private static TravelJourneyOperationResult Fail(TravelJourneyMutationStatus status, string message, long before) => TravelJourneyOperationResult.Failure(status, message, before);
        private static long ToMillimeters(double meters) => (long)Math.Round(Math.Max(0d, meters) * MillimetersPerMeter, MidpointRounding.AwayFromZero);
        private static bool ValidFiniteNonNegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        private static string First(string first, string fallback) => string.IsNullOrWhiteSpace(first) ? N(fallback) : first.Trim();
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static IReadOnlyList<string> GetIds(IReadOnlyDictionary<string, List<string>> index, string key) => index != null && index.TryGetValue(N(key), out List<string> ids) ? ids.OrderBy(id => id, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        private static void AddIndex(IDictionary<string, List<string>> index, string key, string id)
        {
            key = N(key);
            id = N(id);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(id)) return;
            if (!index.TryGetValue(key, out List<string> values))
            {
                values = new List<string>();
                index[key] = values;
            }
            if (!values.Contains(id, StringComparer.Ordinal)) values.Add(id);
        }

        private static void AddValidationStep(IDictionary<string, List<TravelJourneyStepRecordData>> index, string key, TravelJourneyStepRecordData step)
        {
            key = N(key);
            if (!index.TryGetValue(key, out List<TravelJourneyStepRecordData> values))
            {
                values = new List<TravelJourneyStepRecordData>();
                index[key] = values;
            }
            values.Add(step);
        }
    }

    internal static class TravelJourneyRecordExtensions
    {
        public static bool IsTerminalState(this TravelJourneyRecordData journey)
        {
            return journey != null
                && (journey.lifecycleState == TravelJourneyLifecycleState.Completed
                    || journey.lifecycleState == TravelJourneyLifecycleState.Cancelled
                    || journey.lifecycleState == TravelJourneyLifecycleState.Failed
                    || journey.lifecycleState == TravelJourneyLifecycleState.Historical);
        }
    }
}
