using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Laws;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class PoliticalTravelRuntime : IDisposable
    {
        public const string EnterTerritoryActionId = "legal-action.travel.enter-territory";
        public const string ExitTerritoryActionId = "legal-action.travel.exit-territory";
        public const string CrossBorderActionId = "legal-action.travel.cross-political-border";
        public const string InternalTravelActionId = "legal-action.travel.internal-movement";
        public const string PassCheckpointActionId = "legal-action.travel.pass-border-checkpoint";

        private readonly Dictionary<string, BorderCheckpointRecordData> checkpoints = new Dictionary<string, BorderCheckpointRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TravelCrossingAuthorizationRecordData> authorizations = new Dictionary<string, TravelCrossingAuthorizationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PoliticalTravelCrossingRecordData> crossings = new Dictionary<string, PoliticalTravelCrossingRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PoliticalTravelTransactionRecordData> transactions = new Dictionary<string, PoliticalTravelTransactionRecordData>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private GovernmentRuntime governments;
        private LegalRuntime laws;
        private CrimeRuntime crimes;
        private JusticeRuntime justice;
        private LocationRuntime locations;
        private LocationRouteRuntime routes;
        private string worldId = PersistenceService.LocalWorldId;
        private bool disposed;

        public long Revision { get; private set; }
        public int CheckpointCount => checkpoints.Count;
        public int AuthorizationCount => authorizations.Count;
        public int CrossingCount => crossings.Count;
        public IReadOnlyList<BorderCheckpointSnapshot> Checkpoints => checkpoints.Values.OrderBy(item => item.effectiveWorldTime).ThenBy(item => item.checkpointId, StringComparer.Ordinal).Select(item => new BorderCheckpointSnapshot(item)).ToArray();
        public IReadOnlyList<TravelCrossingAuthorizationRecordData> Authorizations => authorizations.Values.OrderBy(item => item.effectiveWorldTime).ThenBy(item => item.authorizationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PoliticalTravelCrossingRecordData> Crossings => crossings.Values.OrderBy(item => item.worldTime).ThenBy(item => item.crossingId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public event Action<PoliticalTravelOperationResult> OperationCommitted;

        public void Configure(
            DefinitionRegistry definitionRegistry,
            GovernmentRuntime governmentRuntime,
            LegalRuntime legalRuntime,
            CrimeRuntime crimeRuntime,
            JusticeRuntime justiceRuntime,
            LocationRuntime locationRuntime,
            LocationRouteRuntime routeRuntime,
            string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            registry = definitionRegistry ?? registry;
            governments = governmentRuntime;
            laws = legalRuntime;
            crimes = crimeRuntime;
            justice = justiceRuntime;
            locations = locationRuntime;
            routes = routeRuntime;
            worldId = N(runtimeWorldId);
            if (string.IsNullOrWhiteSpace(worldId)) worldId = PersistenceService.LocalWorldId;
        }

        public bool TryGetCheckpoint(string checkpointId, out BorderCheckpointRecordData checkpoint)
        {
            if (checkpoints.TryGetValue(N(checkpointId), out BorderCheckpointRecordData found))
            {
                checkpoint = found.Clone();
                return true;
            }

            checkpoint = null;
            return false;
        }

        public PoliticalTravelOperationResult CreateCheckpoint(BorderCheckpointCreateRequest request)
        {
            request ??= new BorderCheckpointCreateRequest();
            long before = Revision;
            if (!Ready(out PoliticalTravelOperationResult failure)) return failure;
            string id = N(request.checkpointId);
            if (Duplicate(request.transactionId, "checkpoint", id, before, out PoliticalTravelOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(id) || checkpoints.ContainsKey(id)) return Fail(PoliticalTravelOperationCode.InvalidRequest, "Checkpoint identity is invalid or already exists.", before);
            if (request.lifecycleState == BorderCheckpointLifecycleState.Unknown) return Fail(PoliticalTravelOperationCode.InvalidRequest, "Checkpoint lifecycle is invalid.", before);
            if (request.policy == BorderCheckpointPolicy.Unknown) return Fail(PoliticalTravelOperationCode.InvalidRequest, "Checkpoint policy is invalid.", before);
            if (!string.IsNullOrWhiteSpace(request.locationId) && locations != null && !locations.TryGetSnapshot(request.locationId, out _)) return Fail(PoliticalTravelOperationCode.MissingLocation, $"Checkpoint location '{request.locationId}' is missing.", before);
            if (!ValidateTerritoryReference(request.sourceTerritoryId, allowEmpty: true)) return Fail(PoliticalTravelOperationCode.MissingTerritory, $"Source territory '{request.sourceTerritoryId}' is missing.", before);
            if (!ValidateTerritoryReference(request.destinationTerritoryId, allowEmpty: true)) return Fail(PoliticalTravelOperationCode.MissingTerritory, $"Destination territory '{request.destinationTerritoryId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.governingGovernmentId) && (governments == null || !governments.TryGetGovernment(request.governingGovernmentId, out _))) return Fail(PoliticalTravelOperationCode.InvalidRequest, $"Governing government '{request.governingGovernmentId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.jurisdictionId) && (governments == null || !governments.TryGetJurisdiction(request.jurisdictionId, out _))) return Fail(PoliticalTravelOperationCode.InvalidRequest, $"Jurisdiction '{request.jurisdictionId}' is missing.", before);

            BorderCheckpointRecordData record = new BorderCheckpointRecordData
            {
                checkpointId = id,
                worldId = worldId,
                displayName = string.IsNullOrWhiteSpace(request.displayName) ? id : request.displayName.Trim(),
                locationId = N(request.locationId),
                routeSegmentId = N(request.routeSegmentId),
                sourceTerritoryId = N(request.sourceTerritoryId),
                destinationTerritoryId = N(request.destinationTerritoryId),
                governingGovernmentId = N(request.governingGovernmentId),
                jurisdictionId = N(request.jurisdictionId),
                policy = request.policy,
                lifecycleState = request.lifecycleState,
                requiredActionIds = C((request.requiredActionIds ?? Array.Empty<string>()).Concat(new[] { PassCheckpointActionId })),
                requiredPermitIds = C(request.requiredPermitIds),
                visibility = request.visibility,
                effectiveWorldTime = request.worldTime,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };

            if (request.preview) return PoliticalTravelOperationResult.Success("Checkpoint previewed.", before, before, checkpoint: new BorderCheckpointSnapshot(record), preview: true);
            checkpoints[id] = record;
            Complete(request.transactionId, "checkpoint", id);
            Revision++;
            return Commit(PoliticalTravelOperationResult.Success("Checkpoint registered.", before, Revision, checkpoint: new BorderCheckpointSnapshot(record)));
        }

        public PoliticalTravelOperationResult GrantAuthorization(TravelCrossingAuthorizationRequest request)
        {
            request ??= new TravelCrossingAuthorizationRequest();
            long before = Revision;
            if (!Ready(out PoliticalTravelOperationResult failure)) return failure;
            string id = N(request.authorizationId);
            if (Duplicate(request.transactionId, "authorization", id, before, out PoliticalTravelOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(id) || authorizations.ContainsKey(id)) return Fail(PoliticalTravelOperationCode.InvalidRequest, "Travel authorization identity is invalid or already exists.", before);
            if (string.IsNullOrWhiteSpace(request.travelerPersonId)) return Fail(PoliticalTravelOperationCode.MissingTraveler, "Authorization requires a traveler Person.", before);
            if (!string.IsNullOrWhiteSpace(request.checkpointId) && !checkpoints.ContainsKey(N(request.checkpointId))) return Fail(PoliticalTravelOperationCode.MissingCheckpoint, $"Checkpoint '{request.checkpointId}' is missing.", before);
            if (!ValidateTerritoryReference(request.territoryId, allowEmpty: true)) return Fail(PoliticalTravelOperationCode.MissingTerritory, $"Territory '{request.territoryId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.jurisdictionId) && (governments == null || !governments.TryGetJurisdiction(request.jurisdictionId, out _))) return Fail(PoliticalTravelOperationCode.InvalidRequest, $"Jurisdiction '{request.jurisdictionId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.issuingGovernmentId) && (governments == null || !governments.TryGetGovernment(request.issuingGovernmentId, out _))) return Fail(PoliticalTravelOperationCode.InvalidRequest, $"Issuing government '{request.issuingGovernmentId}' is missing.", before);
            if (request.expirationWorldTime >= 0d && request.expirationWorldTime < request.effectiveWorldTime) return Fail(PoliticalTravelOperationCode.InvalidRequest, "Authorization expiration precedes its effective time.", before);

            TravelCrossingAuthorizationRecordData record = new TravelCrossingAuthorizationRecordData
            {
                authorizationId = id,
                worldId = worldId,
                travelerPersonId = N(request.travelerPersonId),
                checkpointId = N(request.checkpointId),
                territoryId = N(request.territoryId),
                jurisdictionId = N(request.jurisdictionId),
                issuingGovernmentId = N(request.issuingGovernmentId),
                authorizedActionIds = C(request.authorizedActionIds),
                sourceEntitlementId = N(request.sourceEntitlementId),
                effectiveWorldTime = request.effectiveWorldTime,
                expirationWorldTime = request.expirationWorldTime,
                visibility = request.visibility,
                revision = 1L
            };

            if (request.preview) return PoliticalTravelOperationResult.Success("Travel authorization previewed.", before, before, authorization: record, preview: true);
            authorizations[id] = record;
            Complete(request.transactionId, "authorization", id);
            Revision++;
            return Commit(PoliticalTravelOperationResult.Success("Travel authorization recorded.", before, Revision, authorization: record));
        }

        public PoliticalTravelEvaluationResult EvaluateCrossing(PoliticalTravelEvaluationRequest request)
        {
            request ??= new PoliticalTravelEvaluationRequest();
            if (disposed) return EvaluationFailure(PoliticalTravelOperationCode.Disposed, request, "Political travel runtime is disposed.");
            if (governments == null) return EvaluationFailure(PoliticalTravelOperationCode.MissingRuntime, request, "Government runtime is required for political travel evaluation.");
            string travelerId = TravelerId(request);
            if (string.IsNullOrWhiteSpace(travelerId)) return EvaluationFailure(PoliticalTravelOperationCode.MissingTraveler, request, "Traveler Person is required.");
            if (string.IsNullOrWhiteSpace(request.originLocationId) || string.IsNullOrWhiteSpace(request.destinationLocationId)) return EvaluationFailure(PoliticalTravelOperationCode.MissingLocation, request, "Origin and destination locations are required.");
            if (!request.physicalTravelPossible) return BuildEvaluation(request, travelerId, physicalTravelPossible: false, "Physical route is not currently travelable.");
            return BuildEvaluation(request, travelerId, physicalTravelPossible: true, "Political travel evaluated.");
        }

        public PoliticalTravelOperationResult RecordCrossing(PoliticalTravelCrossingRequest request)
        {
            request ??= new PoliticalTravelCrossingRequest();
            long before = Revision;
            if (!Ready(out PoliticalTravelOperationResult failure)) return failure;
            if (request.expectedRevision >= 0L && request.expectedRevision != Revision) return Fail(PoliticalTravelOperationCode.InvalidRequest, "Political travel revision conflict.", before);
            string id = N(request.crossingId);
            if (Duplicate(request.transactionId, "crossing", id, before, out PoliticalTravelOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(id) || crossings.ContainsKey(id)) return Fail(PoliticalTravelOperationCode.InvalidRequest, "Crossing identity is invalid or already exists.", before);
            PoliticalTravelEvaluationResult evaluation = EvaluateCrossing(request);
            if (!evaluation.Succeeded) return PoliticalTravelOperationResult.Failure(evaluation.Code, evaluation.Message, before, evaluation);
            if (evaluation.CombinedState == PhysicalLegalTravelState.PhysicallyBlocked) return PoliticalTravelOperationResult.Failure(PoliticalTravelOperationCode.PhysicalBlocked, evaluation.Message, before, evaluation);
            if (evaluation.CombinedState == PhysicalLegalTravelState.LegallyBlocked && request.legalComplianceMode == TravelLegalComplianceMode.RequireLegalTravel) return PoliticalTravelOperationResult.Failure(PoliticalTravelOperationCode.LegalBlocked, evaluation.Message, before, evaluation);

            PoliticalTravelCrossingRecordData record = new PoliticalTravelCrossingRecordData
            {
                crossingId = id,
                worldId = worldId,
                travelerPersonId = TravelerId(request),
                originLocationId = N(request.originLocationId),
                destinationLocationId = N(request.destinationLocationId),
                routeSegmentId = N(request.routeSegmentId),
                sourceTerritoryId = evaluation.OriginTerritory?.TerritoryId ?? string.Empty,
                destinationTerritoryId = evaluation.DestinationTerritory?.TerritoryId ?? string.Empty,
                sourceJurisdictionId = evaluation.OriginJurisdiction?.SelectedJurisdiction?.jurisdictionId ?? string.Empty,
                destinationJurisdictionId = evaluation.DestinationJurisdiction?.SelectedJurisdiction?.jurisdictionId ?? string.Empty,
                checkpointId = evaluation.Checkpoint?.Checkpoints?.FirstOrDefault()?.CheckpointId ?? string.Empty,
                authorizationId = string.IsNullOrWhiteSpace(request.authorizationId) ? evaluation.Checkpoint?.AuthorizationId ?? string.Empty : N(request.authorizationId),
                classification = evaluation.Classification,
                legalState = evaluation.Legal?.State ?? PoliticalTravelLegalState.NotEvaluated,
                combinedState = evaluation.CombinedState,
                lifecycleState = evaluation.IllegalCrossing ? PoliticalTravelCrossingLifecycleState.IllegalRecorded : PoliticalTravelCrossingLifecycleState.Completed,
                illegalCrossing = evaluation.IllegalCrossing,
                enforcementOpportunity = evaluation.EnforcementOpportunity,
                visibleWantedStatusIds = evaluation.Wanted?.VisibleWantedStatusIds?.ToArray() ?? Array.Empty<string>(),
                visibleWarrantIds = evaluation.Wanted?.VisibleWarrantIds?.ToArray() ?? Array.Empty<string>(),
                worldTime = request.worldTime,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };

            if (request.preview) return PoliticalTravelOperationResult.Success("Crossing previewed.", before, before, crossing: record, evaluation: evaluation, preview: true);
            crossings[id] = record;
            Complete(request.transactionId, "crossing", id);
            Revision++;
            return Commit(PoliticalTravelOperationResult.Success("Political travel crossing recorded.", before, Revision, crossing: record, evaluation: evaluation));
        }

        public RouteRequirementSummary BuildPoliticalRouteRequirements(LocationRoutePlan plan, PoliticalTravelEvaluationRequest template)
        {
            RouteRequirementSummary summary = plan?.Requirements?.Clone() ?? new RouteRequirementSummary();
            if (plan == null || template == null) return summary;
            List<string> legalActions = new List<string>(summary.requiredLegalTravelActions ?? Array.Empty<string>());
            List<string> checkpointIds = new List<string>(summary.requiredCheckpointIds ?? Array.Empty<string>());
            List<string> territoryIds = new List<string>(summary.requiredPoliticalTerritoryIds ?? Array.Empty<string>());
            foreach (LocationRoutePlanStep step in plan.Steps)
            {
                PoliticalTravelEvaluationRequest request = new PoliticalTravelEvaluationRequest
                {
                    traveler = template.traveler?.Clone(),
                    travelerPersonId = template.travelerPersonId,
                    originLocationId = step.SourceLocationId,
                    destinationLocationId = step.DestinationLocationId,
                    routeSegmentId = step.EdgeId,
                    physicalTravelPossible = true,
                    legalComplianceMode = template.legalComplianceMode,
                    visibilityMode = template.visibilityMode,
                    worldTime = template.worldTime,
                    legalStatusDefinitionIds = template.legalStatusDefinitionIds,
                    knownCheckpointIds = template.knownCheckpointIds
                };
                PoliticalTravelEvaluationResult result = EvaluateCrossing(request);
                if (result.Legal?.RequiredActionIds != null) legalActions.AddRange(result.Legal.RequiredActionIds);
                if (result.Checkpoint?.Checkpoints != null) checkpointIds.AddRange(result.Checkpoint.Checkpoints.Select(item => item.CheckpointId));
                if (!string.IsNullOrWhiteSpace(result.OriginTerritory?.TerritoryId)) territoryIds.Add(result.OriginTerritory.TerritoryId);
                if (!string.IsNullOrWhiteSpace(result.DestinationTerritory?.TerritoryId)) territoryIds.Add(result.DestinationTerritory.TerritoryId);
            }

            summary.requiredLegalTravelActions = C(legalActions);
            summary.requiredCheckpointIds = C(checkpointIds);
            summary.requiredPoliticalTerritoryIds = C(territoryIds);
            return summary;
        }

        public PoliticalTravelRuntimeSaveData CreateSaveData()
        {
            return new PoliticalTravelRuntimeSaveData
            {
                schemaVersion = PoliticalTravelRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                checkpoints = checkpoints.Values.OrderBy(item => item.checkpointId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                authorizations = authorizations.Values.OrderBy(item => item.authorizationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                crossings = crossings.Values.OrderBy(item => item.crossingId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                transactions = transactions.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray()
            };
        }

        public PoliticalTravelOperationResult RestoreFromSaveData(PoliticalTravelRuntimeSaveData saveData, GovernmentRuntime governmentRuntime = null, LegalRuntime legalRuntime = null, CrimeRuntime crimeRuntime = null, LocationRuntime locationRuntime = null, LocationRouteRuntime routeRuntime = null, string expectedWorldId = PersistenceService.LocalWorldId, bool restoring = true)
        {
            long before = Revision;
            PoliticalTravelRuntimeSaveData rollback = CreateSaveData();
            if (!ValidateSaveData(saveData, governmentRuntime ?? governments, legalRuntime ?? laws, crimeRuntime ?? crimes, locationRuntime ?? locations, routeRuntime ?? routes, expectedWorldId, out string failure)) return Fail(PoliticalTravelOperationCode.PersistenceInvalid, failure, before);
            try
            {
                RestoreInternal(saveData ?? new PoliticalTravelRuntimeSaveData());
                Configure(registry, governmentRuntime ?? governments, legalRuntime ?? laws, crimeRuntime ?? crimes, justice, locationRuntime ?? locations, routeRuntime ?? routes, string.IsNullOrWhiteSpace(expectedWorldId) ? worldId : expectedWorldId);
                return PoliticalTravelOperationResult.Success(restoring ? "Political travel state restored." : "Political travel state loaded.", before, Revision);
            }
            catch (Exception ex)
            {
                RestoreInternal(rollback);
                return Fail(PoliticalTravelOperationCode.RestoreFailed, $"Political travel restore failed and rolled back: {ex.Message}", before);
            }
        }

        public static bool ValidateSaveData(PoliticalTravelRuntimeSaveData saveData, GovernmentRuntime governments, LegalRuntime laws, CrimeRuntime crimes, LocationRuntime locations, LocationRouteRuntime routes, string expectedWorldId, out string failure)
        {
            failure = string.Empty;
            if (saveData == null) return true;
            if (saveData.schemaVersion != PoliticalTravelRuntimeSaveData.CurrentSchemaVersion) { failure = $"Unsupported political travel schema version {saveData.schemaVersion}."; return false; }
            string world = N(expectedWorldId);
            if (string.IsNullOrWhiteSpace(world)) world = PersistenceService.LocalWorldId;
            if (!string.IsNullOrWhiteSpace(saveData.worldId) && !string.Equals(N(saveData.worldId), world, StringComparison.Ordinal)) { failure = $"Political travel save world '{saveData.worldId}' does not match expected world '{world}'."; return false; }
            BorderCheckpointRecordData[] savedCheckpoints = saveData.checkpoints ?? Array.Empty<BorderCheckpointRecordData>();
            TravelCrossingAuthorizationRecordData[] savedAuthorizations = saveData.authorizations ?? Array.Empty<TravelCrossingAuthorizationRecordData>();
            PoliticalTravelCrossingRecordData[] savedCrossings = saveData.crossings ?? Array.Empty<PoliticalTravelCrossingRecordData>();
            PoliticalTravelTransactionRecordData[] savedTransactions = saveData.transactions ?? Array.Empty<PoliticalTravelTransactionRecordData>();
            if (savedCheckpoints.Any(item => item == null) || savedAuthorizations.Any(item => item == null) || savedCrossings.Any(item => item == null) || savedTransactions.Any(item => item == null)) { failure = "Political travel save contains a null record."; return false; }
            if (!Unique(savedCheckpoints.Select(item => item.checkpointId), "checkpoint", out failure) || !Unique(savedAuthorizations.Select(item => item.authorizationId), "authorization", out failure) || !Unique(savedCrossings.Select(item => item.crossingId), "crossing", out failure) || !Unique(savedTransactions.Select(item => item.transactionId), "transaction", out failure)) return false;
            HashSet<string> checkpointIds = savedCheckpoints.Select(item => N(item.checkpointId)).ToHashSet(StringComparer.Ordinal);
            HashSet<string> authorizationIds = savedAuthorizations.Select(item => N(item.authorizationId)).ToHashSet(StringComparer.Ordinal);
            foreach (BorderCheckpointRecordData checkpoint in savedCheckpoints)
            {
                if (!string.IsNullOrWhiteSpace(checkpoint.worldId) && !string.Equals(N(checkpoint.worldId), world, StringComparison.Ordinal)) { failure = $"Checkpoint '{checkpoint.checkpointId}' belongs to another world."; return false; }
                if (checkpoint.policy == BorderCheckpointPolicy.Unknown || checkpoint.lifecycleState == BorderCheckpointLifecycleState.Unknown) { failure = $"Checkpoint '{checkpoint.checkpointId}' has invalid policy or lifecycle."; return false; }
                if (!string.IsNullOrWhiteSpace(checkpoint.locationId) && locations != null && !locations.TryGetSnapshot(checkpoint.locationId, out _)) { failure = $"Checkpoint '{checkpoint.checkpointId}' references missing location '{checkpoint.locationId}'."; return false; }
                if (!TerritoryExists(governments, checkpoint.sourceTerritoryId, allowEmpty: true) || !TerritoryExists(governments, checkpoint.destinationTerritoryId, allowEmpty: true)) { failure = $"Checkpoint '{checkpoint.checkpointId}' references a missing territory."; return false; }
                if (!string.IsNullOrWhiteSpace(checkpoint.governingGovernmentId) && (governments == null || !governments.TryGetGovernment(checkpoint.governingGovernmentId, out _))) { failure = $"Checkpoint '{checkpoint.checkpointId}' references missing government '{checkpoint.governingGovernmentId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(checkpoint.jurisdictionId) && (governments == null || !governments.TryGetJurisdiction(checkpoint.jurisdictionId, out _))) { failure = $"Checkpoint '{checkpoint.checkpointId}' references missing jurisdiction '{checkpoint.jurisdictionId}'."; return false; }
            }

            foreach (TravelCrossingAuthorizationRecordData authorization in savedAuthorizations)
            {
                if (string.IsNullOrWhiteSpace(authorization.travelerPersonId)) { failure = $"Authorization '{authorization.authorizationId}' has no traveler."; return false; }
                if (!string.IsNullOrWhiteSpace(authorization.checkpointId) && !checkpointIds.Contains(N(authorization.checkpointId))) { failure = $"Authorization '{authorization.authorizationId}' references missing checkpoint '{authorization.checkpointId}'."; return false; }
                if (!TerritoryExists(governments, authorization.territoryId, allowEmpty: true)) { failure = $"Authorization '{authorization.authorizationId}' references missing territory '{authorization.territoryId}'."; return false; }
                if (authorization.expirationWorldTime >= 0d && authorization.expirationWorldTime < authorization.effectiveWorldTime) { failure = $"Authorization '{authorization.authorizationId}' has invalid dates."; return false; }
            }

            foreach (PoliticalTravelCrossingRecordData crossing in savedCrossings)
            {
                if (string.IsNullOrWhiteSpace(crossing.travelerPersonId) || string.IsNullOrWhiteSpace(crossing.originLocationId) || string.IsNullOrWhiteSpace(crossing.destinationLocationId)) { failure = $"Crossing '{crossing.crossingId}' has incomplete identity."; return false; }
                if (!string.IsNullOrWhiteSpace(crossing.checkpointId) && !checkpointIds.Contains(N(crossing.checkpointId))) { failure = $"Crossing '{crossing.crossingId}' references missing checkpoint '{crossing.checkpointId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(crossing.authorizationId) && !authorizationIds.Contains(N(crossing.authorizationId))) { failure = $"Crossing '{crossing.crossingId}' references missing authorization '{crossing.authorizationId}'."; return false; }
                if (!TerritoryExists(governments, crossing.sourceTerritoryId, allowEmpty: true) || !TerritoryExists(governments, crossing.destinationTerritoryId, allowEmpty: true)) { failure = $"Crossing '{crossing.crossingId}' references a missing territory."; return false; }
            }

            return true;
        }

        public void Dispose()
        {
            disposed = true;
            checkpoints.Clear();
            authorizations.Clear();
            crossings.Clear();
            transactions.Clear();
        }

        private PoliticalTravelEvaluationResult BuildEvaluation(PoliticalTravelEvaluationRequest request, string travelerId, bool physicalTravelPossible, string message)
        {
            TravelLegalComplianceMode compliance = request.legalComplianceMode == TravelLegalComplianceMode.Unknown ? TravelLegalComplianceMode.StructuralOnlyDevelopment : request.legalComplianceMode;
            PoliticalTravelTerritoryResolution origin = ResolveTerritory(request.originLocationId, request.worldTime);
            PoliticalTravelTerritoryResolution destination = ResolveTerritory(request.destinationLocationId, request.worldTime);
            PoliticalTravelCrossingClassification classification = Classify(origin, destination);
            JurisdictionResolutionResult originJurisdiction = ResolveJurisdiction(origin, request.originLocationId, travelerId, request.worldTime);
            JurisdictionResolutionResult destinationJurisdiction = ResolveJurisdiction(destination, request.destinationLocationId, travelerId, request.worldTime);
            PoliticalTravelLegalityResult legal = compliance == TravelLegalComplianceMode.StructuralOnlyDevelopment ? new PoliticalTravelLegalityResult(PoliticalTravelLegalState.NotEvaluated, null, Array.Empty<string>(), "Legal travel skipped in structural development mode.") : EvaluateLegal(request, travelerId, destination, classification);
            BorderCheckpointEvaluationResult checkpoint = EvaluateCheckpoint(request, travelerId, origin, destination);
            PoliticalTravelWantedSummary wanted = BuildWantedSummary(travelerId, origin, destination, request.visibilityMode, request.worldTime);
            bool checkpointBlocked = checkpoint.State == BorderCheckpointEvaluationState.Closed || checkpoint.State == BorderCheckpointEvaluationState.AuthorizationMissing && compliance == TravelLegalComplianceMode.RequireLegalTravel;
            bool legalBlocked = legal.State == PoliticalTravelLegalState.Prohibited || legal.State == PoliticalTravelLegalState.Conflict || legal.State == PoliticalTravelLegalState.MissingAuthorization;
            bool illegal = legalBlocked || checkpoint.State == BorderCheckpointEvaluationState.AuthorizationMissing;
            bool enforcement = illegal || wanted.VisibleWantedStatusIds.Count > 0 || wanted.VisibleWarrantIds.Count > 0 || wanted.HiddenRestrictedInformation && request.visibilityMode != PoliticalTravelVisibilityMode.TravelerSafe;
            PhysicalLegalTravelState combined = CombinedState(physicalTravelPossible, compliance, legal, checkpoint, checkpointBlocked);
            bool succeeded = physicalTravelPossible
                && (combined != PhysicalLegalTravelState.LegallyBlocked
                    || compliance == TravelLegalComplianceMode.AllowIllegalTravel
                    || compliance == TravelLegalComplianceMode.PreferLegalTravel
                    || compliance == TravelLegalComplianceMode.StructuralOnlyDevelopment);
            PoliticalTravelOperationCode code = !physicalTravelPossible ? PoliticalTravelOperationCode.PhysicalBlocked : combined == PhysicalLegalTravelState.LegallyBlocked ? PoliticalTravelOperationCode.LegalBlocked : PoliticalTravelOperationCode.Succeeded;
            return PoliticalTravelEvaluationResult.Create(succeeded, code, classification, combined, origin, destination, originJurisdiction, destinationJurisdiction, legal, checkpoint, wanted, physicalTravelPossible, illegal, enforcement, message, Revision);
        }

        private PoliticalTravelLegalityResult EvaluateLegal(PoliticalTravelEvaluationRequest request, string travelerId, PoliticalTravelTerritoryResolution destination, PoliticalTravelCrossingClassification classification)
        {
            string actionId = ActionFor(classification);
            if (laws == null) return new PoliticalTravelLegalityResult(PoliticalTravelLegalState.NotEvaluated, null, new[] { actionId }, "Legal runtime is unavailable.");
            LegalApplicabilityResult applicability = laws.Evaluate(new LegalApplicabilityRequest
            {
                personId = travelerId,
                territoryId = destination?.TerritoryId ?? string.Empty,
                placeId = request.destinationLocationId,
                actionId = actionId,
                subjectMatterId = "travel",
                legalStatusDefinitionIds = C((request.legalStatusDefinitionIds ?? Array.Empty<string>()).Concat(laws.GetStatusesForPerson(travelerId, request.worldTime).Select(item => item.statusDefinitionId))),
                worldTime = request.worldTime
            });
            PoliticalTravelLegalState state = applicability.Status switch
            {
                LegalApplicabilityStatus.NoApplicableLaw => PoliticalTravelLegalState.AllowedByDefault,
                LegalApplicabilityStatus.Permitted => PoliticalTravelLegalState.Authorized,
                LegalApplicabilityStatus.Required => PoliticalTravelLegalState.Required,
                LegalApplicabilityStatus.Exempt => PoliticalTravelLegalState.Exempt,
                LegalApplicabilityStatus.Immune => PoliticalTravelLegalState.Exempt,
                LegalApplicabilityStatus.Prohibited => PoliticalTravelLegalState.Prohibited,
                LegalApplicabilityStatus.Conflict => PoliticalTravelLegalState.Conflict,
                LegalApplicabilityStatus.AccessDenied => PoliticalTravelLegalState.AccessDenied,
                LegalApplicabilityStatus.InvalidRequest => PoliticalTravelLegalState.AccessDenied,
                _ => PoliticalTravelLegalState.Authorized
            };
            return new PoliticalTravelLegalityResult(state, applicability, new[] { actionId }, applicability.Message);
        }

        private BorderCheckpointEvaluationResult EvaluateCheckpoint(PoliticalTravelEvaluationRequest request, string travelerId, PoliticalTravelTerritoryResolution origin, PoliticalTravelTerritoryResolution destination)
        {
            string sourceTerritoryId = origin?.TerritoryId ?? string.Empty;
            string destinationTerritoryId = destination?.TerritoryId ?? string.Empty;
            string[] known = C(request.knownCheckpointIds);
            BorderCheckpointRecordData[] applicable = checkpoints.Values
                .Where(item => item.lifecycleState == BorderCheckpointLifecycleState.Active && PoliticalTravelModelUtility.Active(item.effectiveWorldTime, item.endedWorldTime, request.worldTime))
                .Where(item => Match(item.routeSegmentId, request.routeSegmentId) || Match(item.locationId, request.destinationLocationId) || TerritorialPairMatches(item, sourceTerritoryId, destinationTerritoryId))
                .OrderBy(item => item.checkpointId, StringComparer.Ordinal)
                .ToArray();
            BorderCheckpointRecordData[] visible = applicable.Where(item => CanSee(item.visibility, request.visibilityMode) || known.Contains(item.checkpointId)).ToArray();
            if (applicable.Length > 0 && visible.Length == 0) return new BorderCheckpointEvaluationResult(BorderCheckpointEvaluationState.HiddenFromRequester, Array.Empty<BorderCheckpointSnapshot>(), Array.Empty<string>(), string.Empty, "Checkpoint exists but is hidden from requester.");
            if (visible.Length == 0) return new BorderCheckpointEvaluationResult(BorderCheckpointEvaluationState.NoCheckpoint, Array.Empty<BorderCheckpointSnapshot>(), Array.Empty<string>(), string.Empty, "No applicable checkpoint.");
            if (visible.Any(item => item.policy == BorderCheckpointPolicy.ClosedToOrdinaryTravel)) return new BorderCheckpointEvaluationResult(BorderCheckpointEvaluationState.Closed, visible.Select(item => new BorderCheckpointSnapshot(item)), Array.Empty<string>(), string.Empty, "Checkpoint is closed to ordinary travel.");
            string[] required = C(visible.SelectMany(item => item.requiredActionIds ?? Array.Empty<string>()));
            string[] requiredPermitDefinitions = C(visible.SelectMany(item => item.requiredPermitIds ?? Array.Empty<string>()));
            foreach (BorderCheckpointRecordData checkpoint in visible)
            {
                foreach (string actionId in checkpoint.requiredActionIds ?? Array.Empty<string>())
                {
                    GovernmentPermitCheckResult permitCheck = governments.EvaluateRegulatedAction(travelerId, GovernmentPermitHolderCategory.Person, actionId, checkpoint.jurisdictionId, request.worldTime);
                    if (!permitCheck.Allowed) return new BorderCheckpointEvaluationResult(BorderCheckpointEvaluationState.AuthorizationMissing, visible.Select(item => new BorderCheckpointSnapshot(item)), required, string.Empty, permitCheck.Message);
                }
            }
            foreach (string permitDefinitionId in requiredPermitDefinitions)
            {
                bool found = visible.Any(checkpoint => governments.HasActivePermitDefinition(travelerId, GovernmentPermitHolderCategory.Person, permitDefinitionId, checkpoint.jurisdictionId, request.worldTime));
                if (!found) return new BorderCheckpointEvaluationResult(BorderCheckpointEvaluationState.AuthorizationMissing, visible.Select(item => new BorderCheckpointSnapshot(item)), required, string.Empty, $"Required government permit '{permitDefinitionId}' is missing.");
            }
            if (visible.Any(item => item.policy == BorderCheckpointPolicy.RequireAuthorization))
            {
                if (requiredPermitDefinitions.Length > 0) return new BorderCheckpointEvaluationResult(BorderCheckpointEvaluationState.PassAllowed, visible.Select(item => new BorderCheckpointSnapshot(item)), Array.Empty<string>(), string.Empty, "Government travel permit accepted.");
                TravelCrossingAuthorizationRecordData authorization = FindAuthorization(travelerId, visible, destinationTerritoryId, required, request.worldTime);
                if (authorization == null) return new BorderCheckpointEvaluationResult(BorderCheckpointEvaluationState.AuthorizationMissing, visible.Select(item => new BorderCheckpointSnapshot(item)), required, string.Empty, "Checkpoint authorization is missing.");
                return new BorderCheckpointEvaluationResult(BorderCheckpointEvaluationState.PassAllowed, visible.Select(item => new BorderCheckpointSnapshot(item)), Array.Empty<string>(), authorization.authorizationId, "Checkpoint authorization accepted.");
            }
            if (visible.Any(item => item.policy == BorderCheckpointPolicy.RequireInspection)) return new BorderCheckpointEvaluationResult(BorderCheckpointEvaluationState.InspectionRequired, visible.Select(item => new BorderCheckpointSnapshot(item)), required, string.Empty, "Checkpoint inspection is required.");
            return new BorderCheckpointEvaluationResult(BorderCheckpointEvaluationState.PassAllowed, visible.Select(item => new BorderCheckpointSnapshot(item)), Array.Empty<string>(), string.Empty, "Checkpoint allows travel.");
        }

        private PoliticalTravelWantedSummary BuildWantedSummary(string travelerId, PoliticalTravelTerritoryResolution origin, PoliticalTravelTerritoryResolution destination, PoliticalTravelVisibilityMode mode, double worldTime)
        {
            if (crimes == null) return new PoliticalTravelWantedSummary(Array.Empty<string>(), Array.Empty<string>(), false);
            string[] territoryIds = C(new[] { origin?.TerritoryId, destination?.TerritoryId });
            string[] placeIds = C(LocationAncestry(origin?.LocationId).Concat(LocationAncestry(destination?.LocationId)));
            bool hidden = false;
            List<string> wantedIds = new List<string>();
            foreach (WantedStatusRecordData wanted in crimes.WantedStatuses.Where(item => item.subjectId == travelerId && item.subjectType == "Person" && item.lifecycleState == WantedStatusLifecycleState.Active && PoliticalTravelModelUtility.Active(item.activeWorldTime, item.expirationWorldTime, worldTime)).OrderBy(item => item.wantedStatusId, StringComparer.Ordinal))
            {
                bool territoryApplies = string.IsNullOrWhiteSpace(wanted.territoryId) || territoryIds.Contains(wanted.territoryId);
                bool placeApplies = wanted.placeIds == null || wanted.placeIds.Length == 0 || wanted.placeIds.Any(placeIds.Contains);
                bool applies = territoryApplies && placeApplies;
                if (!applies) continue;
                if (CanSee(wanted.visibility, mode)) wantedIds.Add(wanted.wantedStatusId);
                else hidden = true;
            }

            List<string> warrantIds = new List<string>();
            foreach (WarrantRecordData warrant in crimes.Warrants.Where(item => item.scope != null && item.scope.kind == WarrantScopeKind.Person && item.scope.targetId == travelerId && (item.lifecycleState == WarrantLifecycleState.Issued || item.lifecycleState == WarrantLifecycleState.Active) && PoliticalTravelModelUtility.Active(item.activationWorldTime, item.expirationWorldTime, worldTime)).OrderBy(item => item.warrantId, StringComparer.Ordinal))
            {
                bool applies = warrant.scope.territoryIds == null || warrant.scope.territoryIds.Length == 0 || warrant.scope.territoryIds.Any(id => territoryIds.Contains(id));
                if (!applies) continue;
                if (CanSee(warrant.visibility, mode)) warrantIds.Add(warrant.warrantId);
                else hidden = true;
            }

            return new PoliticalTravelWantedSummary(wantedIds, warrantIds, hidden);
        }

        private PoliticalTravelTerritoryResolution ResolveTerritory(string locationId, double worldTime)
        {
            string[] candidatePlaces = LocationAncestry(locationId);
            TerritoryPlaceMembershipRecordData[] memberships = governments?.TerritoryPlaceMemberships
                .Where(item => candidatePlaces.Contains(item.placeId) && PoliticalTravelModelUtility.Active(item.effectiveWorldTime, item.endedWorldTime, worldTime))
                .OrderByDescending(item => Array.IndexOf(candidatePlaces, item.placeId) >= 0 ? candidatePlaces.Length - Array.IndexOf(candidatePlaces, item.placeId) : 0)
                .ThenBy(item => item.membershipId, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<TerritoryPlaceMembershipRecordData>();
            string territoryId = memberships.FirstOrDefault()?.territoryId;
            if (string.IsNullOrWhiteSpace(territoryId))
            {
                territoryId = governments?.Territories.FirstOrDefault(item => item.lifecycleState == TerritoryLifecycleState.Active && (item.placeIds ?? Array.Empty<string>()).Any(candidatePlaces.Contains))?.territoryId;
            }

            if (!string.IsNullOrWhiteSpace(territoryId) && governments.TryGetTerritory(territoryId, out PoliticalTerritoryRecordData territory))
            {
                bool contested = governments.Claims.Count(item => item.territoryId == territoryId && item.lifecycleState is TerritorialClaimLifecycleState.Asserted or TerritorialClaimLifecycleState.Disputed or TerritorialClaimLifecycleState.Contested) > 1
                    || governments.Controls.Count(item => item.territoryId == territoryId && item.state is TerritorialControlState.Contested or TerritorialControlState.PartiallyControlled or TerritorialControlState.Occupied) > 0;
                return new PoliticalTravelTerritoryResolution(locationId, territory, contested, "Territory resolved from government place membership.");
            }

            return new PoliticalTravelTerritoryResolution(locationId, null, false, "No active territory contains this location.");
        }

        private JurisdictionResolutionResult ResolveJurisdiction(PoliticalTravelTerritoryResolution territory, string placeId, string travelerId, double worldTime)
        {
            if (governments == null || string.IsNullOrWhiteSpace(territory?.TerritoryId)) return JurisdictionResolutionResult.Create(JurisdictionResolutionStatus.NoApplicableJurisdiction, Array.Empty<JurisdictionRecordData>(), null, "No territory jurisdiction.");
            return governments.ResolveJurisdiction(new JurisdictionResolutionRequest
            {
                territoryId = territory.TerritoryId,
                placeId = placeId,
                personId = travelerId,
                subjectMatter = JurisdictionSubjectMatter.BorderAdministration,
                worldTime = worldTime
            });
        }

        private string[] LocationAncestry(string locationId)
        {
            string id = N(locationId);
            if (string.IsNullOrWhiteSpace(id) || locations == null) return string.IsNullOrWhiteSpace(id) ? Array.Empty<string>() : new[] { id };
            return new[] { id }.Concat(locations.GetAncestors(id).Select(item => item.LocationId)).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
        }

        private static PoliticalTravelCrossingClassification Classify(PoliticalTravelTerritoryResolution origin, PoliticalTravelTerritoryResolution destination)
        {
            string source = origin?.TerritoryId ?? string.Empty;
            string target = destination?.TerritoryId ?? string.Empty;
            if (origin?.Contested == true || destination?.Contested == true) return PoliticalTravelCrossingClassification.ContestedBorderCrossing;
            if (string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(target)) return PoliticalTravelCrossingClassification.UnclaimedOrUnknownTerritory;
            if (string.Equals(source, target, StringComparison.Ordinal)) return PoliticalTravelCrossingClassification.InternalMovement;
            if (string.IsNullOrWhiteSpace(source)) return PoliticalTravelCrossingClassification.TerritoryEntry;
            if (string.IsNullOrWhiteSpace(target)) return PoliticalTravelCrossingClassification.TerritoryExit;
            return PoliticalTravelCrossingClassification.BorderCrossing;
        }

        private static string ActionFor(PoliticalTravelCrossingClassification classification)
        {
            return classification switch
            {
                PoliticalTravelCrossingClassification.TerritoryEntry => EnterTerritoryActionId,
                PoliticalTravelCrossingClassification.TerritoryExit => ExitTerritoryActionId,
                PoliticalTravelCrossingClassification.InternalMovement => InternalTravelActionId,
                _ => CrossBorderActionId
            };
        }

        private static PhysicalLegalTravelState CombinedState(bool physicalTravelPossible, TravelLegalComplianceMode compliance, PoliticalTravelLegalityResult legal, BorderCheckpointEvaluationResult checkpoint, bool checkpointBlocked)
        {
            if (!physicalTravelPossible) return PhysicalLegalTravelState.PhysicallyBlocked;
            if (compliance == TravelLegalComplianceMode.StructuralOnlyDevelopment) return PhysicalLegalTravelState.DevelopmentStructuralOnly;
            bool legalBad = legal.State == PoliticalTravelLegalState.Prohibited || legal.State == PoliticalTravelLegalState.Conflict || legal.State == PoliticalTravelLegalState.MissingAuthorization || checkpointBlocked;
            bool requirement = legal.State == PoliticalTravelLegalState.Required || checkpoint.State == BorderCheckpointEvaluationState.InspectionRequired;
            if (legalBad && compliance == TravelLegalComplianceMode.RequireLegalTravel) return PhysicalLegalTravelState.LegallyBlocked;
            if (legalBad) return PhysicalLegalTravelState.IllegalButPhysicallyPossible;
            if (requirement) return PhysicalLegalTravelState.TravelableWithLegalRequirement;
            return PhysicalLegalTravelState.TravelableAndLegal;
        }

        private TravelCrossingAuthorizationRecordData FindAuthorization(string travelerId, IEnumerable<BorderCheckpointRecordData> applicable, string territoryId, string[] requiredActionIds, double worldTime)
        {
            string[] checkpointIds = applicable.Select(item => item.checkpointId).ToArray();
            return authorizations.Values
                .Where(item => !item.revoked && item.travelerPersonId == travelerId && PoliticalTravelModelUtility.Active(item.effectiveWorldTime, item.expirationWorldTime, worldTime))
                .Where(item => string.IsNullOrWhiteSpace(item.checkpointId) || checkpointIds.Contains(item.checkpointId))
                .Where(item => string.IsNullOrWhiteSpace(item.territoryId) || item.territoryId == territoryId)
                .Where(item => requiredActionIds.Length == 0 || requiredActionIds.All(action => (item.authorizedActionIds ?? Array.Empty<string>()).Contains(action)))
                .OrderBy(item => item.expirationWorldTime < 0d ? double.MaxValue : item.expirationWorldTime)
                .ThenBy(item => item.authorizationId, StringComparer.Ordinal)
                .FirstOrDefault()?.Clone();
        }

        private static bool CanSee(PoliticalVisibility visibility, PoliticalTravelVisibilityMode mode)
        {
            if (mode == PoliticalTravelVisibilityMode.Development || mode == PoliticalTravelVisibilityMode.Privileged) return true;
            if (mode == PoliticalTravelVisibilityMode.PublicOnly) return visibility == PoliticalVisibility.Public;
            return visibility == PoliticalVisibility.Public || visibility == PoliticalVisibility.Restricted;
        }

        private static bool TerritorialPairMatches(BorderCheckpointRecordData checkpoint, string sourceTerritoryId, string destinationTerritoryId)
        {
            bool source = string.IsNullOrWhiteSpace(checkpoint.sourceTerritoryId) || checkpoint.sourceTerritoryId == sourceTerritoryId;
            bool destination = string.IsNullOrWhiteSpace(checkpoint.destinationTerritoryId) || checkpoint.destinationTerritoryId == destinationTerritoryId;
            return source && destination && (!string.IsNullOrWhiteSpace(checkpoint.sourceTerritoryId) || !string.IsNullOrWhiteSpace(checkpoint.destinationTerritoryId));
        }

        private static bool Match(string expected, string actual) => !string.IsNullOrWhiteSpace(expected) && string.Equals(N(expected), N(actual), StringComparison.Ordinal);
        private bool ValidateTerritoryReference(string territoryId, bool allowEmpty) => TerritoryExists(governments, territoryId, allowEmpty);
        private static bool TerritoryExists(GovernmentRuntime governments, string territoryId, bool allowEmpty) => allowEmpty && string.IsNullOrWhiteSpace(territoryId) || governments != null && governments.TryGetTerritory(territoryId, out _);

        private bool Ready(out PoliticalTravelOperationResult failure)
        {
            if (disposed) { failure = PoliticalTravelOperationResult.Failure(PoliticalTravelOperationCode.Disposed, "Political travel runtime is disposed.", Revision); return false; }
            if (governments == null) { failure = PoliticalTravelOperationResult.Failure(PoliticalTravelOperationCode.MissingRuntime, "Government runtime is required.", Revision); return false; }
            failure = null;
            return true;
        }

        private PoliticalTravelEvaluationResult EvaluationFailure(PoliticalTravelOperationCode code, PoliticalTravelEvaluationRequest request, string message)
        {
            return PoliticalTravelEvaluationResult.Create(false, code, PoliticalTravelCrossingClassification.Unknown, PhysicalLegalTravelState.Unresolved, null, null, null, null, null, null, new PoliticalTravelWantedSummary(Array.Empty<string>(), Array.Empty<string>(), false), request?.physicalTravelPossible ?? false, false, false, message, Revision);
        }

        private PoliticalTravelOperationResult Fail(PoliticalTravelOperationCode code, string message, long before) => PoliticalTravelOperationResult.Failure(code, message, before);

        private PoliticalTravelOperationResult Commit(PoliticalTravelOperationResult result)
        {
            OperationCommitted?.Invoke(result);
            return result;
        }

        private bool Duplicate(string transactionId, string operation, string subjectId, long before, out PoliticalTravelOperationResult duplicate)
        {
            duplicate = null;
            transactionId = N(transactionId);
            subjectId = N(subjectId);
            if (string.IsNullOrWhiteSpace(transactionId)) return false;
            if (!transactions.TryGetValue(transactionId, out PoliticalTravelTransactionRecordData existing)) return false;
            if (existing.operation == operation && existing.subjectId == subjectId)
            {
                duplicate = PoliticalTravelOperationResult.Success("Duplicate political travel transaction ignored.", before, before, duplicate: true);
                return true;
            }

            duplicate = PoliticalTravelOperationResult.Failure(PoliticalTravelOperationCode.InvalidRequest, $"Transaction '{transactionId}' was already used for another political travel operation.", before);
            return true;
        }

        private void Complete(string transactionId, string operation, string subjectId)
        {
            transactionId = N(transactionId);
            if (string.IsNullOrWhiteSpace(transactionId)) return;
            transactions[transactionId] = new PoliticalTravelTransactionRecordData { transactionId = transactionId, operation = operation ?? string.Empty, subjectId = N(subjectId), revision = Revision + 1L };
        }

        private void RestoreInternal(PoliticalTravelRuntimeSaveData saveData)
        {
            checkpoints.Clear();
            authorizations.Clear();
            crossings.Clear();
            transactions.Clear();
            PoliticalTravelRuntimeSaveData data = saveData?.Clone() ?? new PoliticalTravelRuntimeSaveData();
            worldId = string.IsNullOrWhiteSpace(data.worldId) ? worldId : data.worldId;
            Revision = data.revision;
            foreach (BorderCheckpointRecordData record in data.checkpoints ?? Array.Empty<BorderCheckpointRecordData>()) checkpoints[record.checkpointId] = record.Clone();
            foreach (TravelCrossingAuthorizationRecordData record in data.authorizations ?? Array.Empty<TravelCrossingAuthorizationRecordData>()) authorizations[record.authorizationId] = record.Clone();
            foreach (PoliticalTravelCrossingRecordData record in data.crossings ?? Array.Empty<PoliticalTravelCrossingRecordData>()) crossings[record.crossingId] = record.Clone();
            foreach (PoliticalTravelTransactionRecordData record in data.transactions ?? Array.Empty<PoliticalTravelTransactionRecordData>()) transactions[record.transactionId] = record.Clone();
        }

        private static bool Unique(IEnumerable<string> ids, string label, out string failure)
        {
            failure = string.Empty;
            string[] raw = (ids ?? Array.Empty<string>()).Select(N).ToArray();
            if (raw.Any(string.IsNullOrWhiteSpace) || raw.Distinct(StringComparer.Ordinal).Count() != raw.Length) { failure = $"Political travel {label} IDs must be non-empty and unique."; return false; }
            return true;
        }

        private static string TravelerId(PoliticalTravelEvaluationRequest request) => N(request.travelerPersonId) is string explicitId && !string.IsNullOrWhiteSpace(explicitId) ? explicitId : N(request.traveler?.entityId);
        private static string N(string value) => PoliticalTravelModelUtility.N(value);
        private static string[] C(IEnumerable<string> values) => PoliticalTravelModelUtility.C(values);
    }
}
