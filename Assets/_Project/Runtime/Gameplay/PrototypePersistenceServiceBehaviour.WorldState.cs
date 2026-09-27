using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SceneZoneTool;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Gameplay
{
    public sealed partial class PrototypePersistenceServiceBehaviour
    {
        private LocationRuntime worldLocations;
        private EntityLocationRuntime worldEntityLocations;
        private InteractionPointRuntime worldInteractionPoints;
        private LocationConnectionRuntime worldLocationConnections;
        private LocationRouteRuntime worldLocationRoutes;
        private TravelJourneyRuntime worldTravelJourneys;
        private TravelConditionRuntime worldTravelConditions;
        private PoliticalTravelRuntime worldPoliticalTravel;
        private SpatialTerritoryBoundaryRuntime worldSpatialTerritories;
        private SpatialTerritoryBoundaryTracker playerSpatialTerritoryTracker;
        private GameObject spatialBoundaryVisualizationRoot;
        private WorldTravelCoordinator worldTravelCoordinator;

        private QuestRuntime worldQuests;
        private QuestParticipationRuntime worldQuestParticipation;
        private QuestObjectiveProgressRuntime worldQuestObjectives;
        private QuestOutcomeRuntime worldQuestOutcomes;
        private IQuestRewardEffectExecutor questRewardEffectExecutor;
        private QuestSourceRuntime worldQuestSources;
        private ConversationRuntime worldConversations;
        private DialogueFlowRuntime worldDialogue;
        private NarrativeEventRuntime worldNarrativeEvents;
        private NarrativeEventRuntimeIntegrations narrativeEventIntegrations;
        private NarrativeStateRuntime worldNarrativeState;
        private NarrativeStateRuntimeIntegrations narrativeStateIntegrations;
        private NarrativeArcRuntime worldNarrativeArcs;
        private NarrativeArcRuntimeIntegrations narrativeArcIntegrations;
        private PrototypeNarrativeCoordinator worldNarrativeCoordinator;

        private bool worldLocationAndNarrativeRegistered;
        private bool consistencyValidatorsRegistered;
        private IPersistenceParticipant[] worldLocationAndNarrativeParticipants = Array.Empty<IPersistenceParticipant>();

        public LocationRuntime WorldLocations => worldLocations;
        public EntityLocationRuntime WorldEntityLocations => worldEntityLocations;
        public InteractionPointRuntime WorldInteractionPoints => worldInteractionPoints;
        public LocationConnectionRuntime WorldLocationConnections => worldLocationConnections;
        public LocationRouteRuntime WorldLocationRoutes => worldLocationRoutes;
        public TravelJourneyRuntime WorldTravelJourneys => worldTravelJourneys;
        public TravelConditionRuntime WorldTravelConditions => worldTravelConditions;
        public PoliticalTravelRuntime WorldPoliticalTravel => worldPoliticalTravel;
        public SpatialTerritoryBoundaryRuntime WorldSpatialTerritories => worldSpatialTerritories;
        public WorldTravelCoordinator WorldTravel => worldTravelCoordinator;
        public QuestRuntime WorldQuests => worldQuests;
        public QuestParticipationRuntime WorldQuestParticipation => worldQuestParticipation;
        public QuestObjectiveProgressRuntime WorldQuestObjectives => worldQuestObjectives;
        public QuestOutcomeRuntime WorldQuestOutcomes => worldQuestOutcomes;
        public QuestSourceRuntime WorldQuestSources => worldQuestSources;
        public ConversationRuntime WorldConversations => worldConversations;
        public DialogueFlowRuntime WorldDialogue => worldDialogue;
        public NarrativeEventRuntime WorldNarrativeEvents => worldNarrativeEvents;
        public NarrativeStateRuntime WorldNarrativeState => worldNarrativeState;
        public NarrativeArcRuntime WorldNarrativeArcs => worldNarrativeArcs;
        public PrototypeNarrativeCoordinator NarrativeCoordinator => worldNarrativeCoordinator;

        private void EnsureWorldLocationAndNarrativePersistence()
        {
            if (worldLocationAndNarrativeRegistered)
            {
                return;
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            if (registry == null)
            {
                Debug.LogWarning("World location and narrative persistence was not registered because the definition registry is unavailable.");
                return;
            }

            string worldId = worldService.WorldId;
            CreateWorldLocationRuntimes(registry, worldId);
            ConfigurePlayerSpatialTerritoryTracking(registry);
            CreateWorldNarrativeRuntimes(registry, worldId);

            worldLocationAndNarrativeParticipants = new IPersistenceParticipant[]
            {
                new LocationPersistenceParticipant(worldLocations, GetDefinitionRegistry, worldId),
                new EntityLocationPersistenceParticipant(worldEntityLocations, () => worldLocations, worldId),
                new InteractionPointPersistenceParticipant(worldInteractionPoints, GetDefinitionRegistry, () => worldLocations, () => worldEntityLocations, worldId),
                new LocationConnectionPersistenceParticipant(worldLocationConnections, GetDefinitionRegistry, () => worldLocations, () => worldEntityLocations, () => worldInteractionPoints, worldId),
                new LocationRoutePersistenceParticipant(worldLocationRoutes, GetDefinitionRegistry, () => worldLocations, () => worldLocationConnections, worldId),
                new TravelJourneyPersistenceParticipant(worldTravelJourneys, GetDefinitionRegistry, () => worldLocations, () => worldEntityLocations, () => worldLocationConnections, () => worldLocationRoutes, worldId),
                new TravelConditionPersistenceParticipant(worldTravelConditions, GetDefinitionRegistry, () => worldLocationRoutes, () => worldTravelJourneys, worldId),
                new PoliticalTravelPersistenceParticipant(worldPoliticalTravel, () => Governments, () => Laws, () => Crimes, () => worldLocations, () => worldLocationRoutes, worldId),
                new QuestRuntimePersistenceParticipant(worldQuests, GetDefinitionRegistry, worldId),
                new QuestParticipationRuntimePersistenceParticipant(worldQuestParticipation, () => worldQuests, GetDefinitionRegistry, worldId),
                new QuestObjectiveProgressPersistenceParticipant(worldQuestObjectives, () => worldQuests, () => worldQuestParticipation, GetDefinitionRegistry, worldId),
                new QuestOutcomePersistenceParticipant(worldQuestOutcomes, () => worldQuests, () => worldQuestParticipation, () => worldQuestObjectives, GetDefinitionRegistry, () => questRewardEffectExecutor, ownerId: worldId),
                new QuestSourcePersistenceParticipant(worldQuestSources, () => worldQuests, () => worldQuestParticipation, GetDefinitionRegistry, worldId),
                new ConversationPersistenceParticipant(worldConversations, GetDefinitionRegistry, worldId),
                new DialogueFlowPersistenceParticipant(worldDialogue, GetDefinitionRegistry, () => worldConversations, () => worldNarrativeCoordinator, ownerId: worldId),
                new NarrativeEventPersistenceParticipant(worldNarrativeEvents, GetDefinitionRegistry, () => narrativeEventIntegrations, ownerId: worldId),
                new NarrativeStatePersistenceParticipant(worldNarrativeState, GetDefinitionRegistry, () => narrativeStateIntegrations, ownerId: worldId),
                new NarrativeArcPersistenceParticipant(worldNarrativeArcs, GetDefinitionRegistry, () => narrativeArcIntegrations, ownerId: worldId)
            };

            bool succeeded = true;
            for (int i = 0; i < worldLocationAndNarrativeParticipants.Length; i++)
            {
                if (!RegisterParticipant(worldLocationAndNarrativeParticipants[i], out string failureReason))
                {
                    succeeded = false;
                    Debug.LogWarning(failureReason);
                }
            }

            worldLocationAndNarrativeRegistered = succeeded;
            if (succeeded)
            {
                WorldSceneBindingRuntime.Default.Configure(
                    worldLocations,
                    worldEntityLocations,
                    worldInteractionPoints,
                    worldLocationConnections,
                    worldLocationRoutes,
                    worldTravelJourneys,
                    worldPoliticalTravel,
                    worldId);
            }
        }

        private void UnregisterWorldLocationAndNarrativePersistence()
        {
            for (int i = 0; i < worldLocationAndNarrativeParticipants.Length; i++)
            {
                UnregisterParticipant(worldLocationAndNarrativeParticipants[i]);
            }

            worldLocationAndNarrativeParticipants = Array.Empty<IPersistenceParticipant>();
            worldLocationAndNarrativeRegistered = false;
            worldNarrativeCoordinator?.Dispose();
            worldNarrativeCoordinator = null;
            if (playerSpatialTerritoryTracker != null) playerSpatialTerritoryTracker.BoundaryCrossed -= OnPlayerSpatialBoundaryCrossed;
            WorldSceneBindingRuntime.Default.ClearConfiguration();
        }

        private void CreateWorldLocationRuntimes(DefinitionRegistry registry, string worldId)
        {
            worldLocations ??= new LocationRuntime();
            worldEntityLocations ??= new EntityLocationRuntime();
            worldInteractionPoints ??= new InteractionPointRuntime();
            worldLocationConnections ??= new LocationConnectionRuntime();
            worldLocationRoutes ??= new LocationRouteRuntime();
            worldTravelJourneys ??= new TravelJourneyRuntime();
            worldTravelConditions ??= new TravelConditionRuntime();
            worldPoliticalTravel ??= new PoliticalTravelRuntime();

            PrototypeLocationDefinitionFactory.SeedPrototypeLocations(worldLocations, registry, worldId);
            worldLocations.Configure(registry, worldId);
            PrototypeEntityLocationFactory.SeedPrototypePlacements(worldEntityLocations, worldLocations, worldId);
            PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(worldInteractionPoints, registry, worldLocations, worldEntityLocations, worldId);
            worldInteractionPoints.Configure(registry, worldLocations, worldEntityLocations, worldId, new PrototypeInteractionRequirementResolver(OrganizationMemberships, OrganizationAuthority));
            PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(worldLocationConnections, registry, worldLocations, worldEntityLocations, worldInteractionPoints, worldId);
            PrototypeLocationRouteDefinitionFactory.SeedPrototypeRoutes(worldLocationRoutes, registry, worldLocations, worldLocationConnections, worldId);
            PrototypeTravelConditionDefinitionFactory.SeedPrototypeTravelConditions(worldTravelConditions, registry, worldLocationRoutes, worldTravelJourneys, worldId);
            worldTravelConditions.HazardTriggered -= OnTravelHazardTriggered;
            worldTravelConditions.HazardTriggered += OnTravelHazardTriggered;
            worldTravelConditions.EncounterTriggered -= OnTravelEncounterTriggered;
            worldTravelConditions.EncounterTriggered += OnTravelEncounterTriggered;
            worldLocationRoutes.Configure(registry, worldLocations, worldLocationConnections, worldId, worldTravelConditions);
            worldTravelJourneys.Configure(registry, worldLocations, worldEntityLocations, worldLocationConnections, worldLocationRoutes, worldId, worldTravelConditions);
            worldPoliticalTravel.Configure(registry, Governments, Laws, Crimes, Justice, worldLocations, worldLocationRoutes, worldId);
            PrototypePoliticalTravelFactory.SeedPrototypeCheckpoints(worldPoliticalTravel);
            worldSpatialTerritories ??= new SpatialTerritoryBoundaryRuntime();
            worldSpatialTerritories.Configure(registry, Governments);
            RefreshSpatialBoundaryVisualizations();
            worldTravelCoordinator = new WorldTravelCoordinator(registry, worldEntityLocations, worldLocationRoutes, worldTravelJourneys, worldPoliticalTravel, BuildWorldLocationAccessContext);
        }

        private void ConfigurePlayerSpatialTerritoryTracking(DefinitionRegistry registry)
        {
            worldSpatialTerritories ??= new SpatialTerritoryBoundaryRuntime();
            worldSpatialTerritories.Configure(registry, Governments);
            ResolvePlayerPersistenceReferences();
            if (playerRoot == null || currentPlaceTracker == null) return;
            playerSpatialTerritoryTracker = playerRoot.GetComponent<SpatialTerritoryBoundaryTracker>();
            if (playerSpatialTerritoryTracker == null) playerSpatialTerritoryTracker = playerRoot.gameObject.AddComponent<SpatialTerritoryBoundaryTracker>();
            playerSpatialTerritoryTracker.BoundaryCrossed -= OnPlayerSpatialBoundaryCrossed;
            playerSpatialTerritoryTracker.BoundaryCrossed += OnPlayerSpatialBoundaryCrossed;
            playerSpatialTerritoryTracker.Configure(
                worldSpatialTerritories,
                currentPlaceTracker,
                sceneKey,
                () => playTimeTracker == null ? 0d : playTimeTracker.CumulativeSeconds);
        }

        private void OnPlayerSpatialBoundaryCrossed(SpatialTerritoryTransition transition)
        {
            if (transition == null) return;
            double worldTime = playTimeTracker == null ? 0d : playTimeTracker.CumulativeSeconds;
            if (!SynchronizePlayerAuthoritativeLocation(transition, worldTime)) return;
            if (!transition.CrossesPoliticalBorder || worldPoliticalTravel == null) return;
            string origin = transition.Previous?.LocationId ?? string.Empty;
            string destination = transition.Current?.LocationId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination) || string.Equals(origin, destination, StringComparison.Ordinal)) return;
            string eventId = Guid.NewGuid().ToString("N");
            PoliticalTravelOperationResult result = worldPoliticalTravel.RecordCrossing(new PoliticalTravelCrossingRequest
            {
                transactionId = $"spatial-border-crossing.{eventId}",
                crossingId = $"political-crossing.spatial.{eventId}",
                travelerPersonId = ResolvePlayerPersonId(),
                originLocationId = origin,
                destinationLocationId = destination,
                physicalTravelPossible = true,
                legalComplianceMode = TravelLegalComplianceMode.StructuralOnlyDevelopment,
                visibilityMode = PoliticalTravelVisibilityMode.TravelerSafe,
                worldTime = worldTime,
                sourceEventId = $"event.spatial-border-crossing.{eventId}",
                provenanceId = "spatial-territory-boundary-tracker"
            });
            if (result.Succeeded)
            {
                dirtyTracker?.MarkDirty(transition.CrossesNationalBorder ? "Player crossed a national border." : "Player crossed an administrative border.");
            }
            else
            {
                Debug.LogWarning($"Physical territory crossing could not be recorded: {result.Message}");
            }
        }

        private bool SynchronizePlayerAuthoritativeLocation(SpatialTerritoryTransition transition, double worldTime)
        {
            string destination = transition?.Current?.LocationId ?? string.Empty;
            if (worldEntityLocations == null || string.IsNullOrWhiteSpace(destination)) return true;

            string worldId = worldService?.WorldId ?? PersistenceService.LocalWorldId;
            EntityLocationReferenceData playerBody = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, worldId);
            if (!worldEntityLocations.TryGetActivePlacement(playerBody, out EntityPlacementSnapshot active))
            {
                Debug.LogWarning("Physical territory transition could not update the player because the authoritative body placement is missing.");
                return false;
            }

            if (string.Equals(active.ExactLocationId, destination, StringComparison.Ordinal)) return true;
            string eventId = Guid.NewGuid().ToString("N");
            EntityLocationOperationResult relocation = worldEntityLocations.Relocate(new EntityRelocationRequest
            {
                transactionId = $"spatial-location-transition.{eventId}",
                entity = playerBody,
                expectedOriginLocationId = active.ExactLocationId,
                destinationLocationId = destination,
                category = EntityPlacementCategory.Present,
                worldTime = worldTime,
                sourceEventId = $"event.spatial-location-transition.{eventId}",
                provenanceId = "spatial-territory-boundary-tracker"
            });
            if (!relocation.Succeeded)
            {
                Debug.LogWarning($"Physical territory transition could not update authoritative player location: {relocation.Message}");
                return false;
            }

            dirtyTracker?.MarkDirty("Player crossed an authored location boundary.");
            return true;
        }

        private void RefreshSpatialBoundaryVisualizations()
        {
            if (spatialBoundaryVisualizationRoot != null) Destroy(spatialBoundaryVisualizationRoot);
            SpatialTerritoryBoundaryDefinition[] visible = worldSpatialTerritories?.Boundaries.Where(value => value != null && value.ShowInGame && value.ZoneBoundary != null).ToArray() ?? Array.Empty<SpatialTerritoryBoundaryDefinition>();
            if (visible.Length == 0) return;
            spatialBoundaryVisualizationRoot = new GameObject("Runtime Territory Boundary Visualizations");
            spatialBoundaryVisualizationRoot.transform.SetParent(transform, false);
            foreach (SpatialTerritoryBoundaryDefinition definition in visible)
            {
                GameObject lineObject = new GameObject(definition.DisplayName);
                lineObject.transform.SetParent(spatialBoundaryVisualizationRoot.transform, false);
                lineObject.AddComponent<SceneZoneLineRenderer>().Configure(definition.ZoneBoundary);
            }
        }

        public LocationConnectionAccessContextData BuildWorldLocationAccessContext(EntityLocationReferenceData actor, double worldTime)
        {
            string personId = worldEntityLocations != null && worldEntityLocations.TryResolvePersonId(actor, out string resolvedPersonId)
                ? resolvedPersonId
                : actor?.entityType == LocationOccupantEntityType.Person ? actor.entityId : string.Empty;
            OrganizationMembershipSnapshot[] memberships = string.IsNullOrWhiteSpace(personId) || OrganizationMemberships == null
                ? Array.Empty<OrganizationMembershipSnapshot>()
                : OrganizationMemberships.QueryMemberships(personId, activeOnly: true).ToArray();
            string[] organizations = CleanAccessIds(memberships.Select(value => value.OrganizationId));
            string[] ranks = CleanAccessIds(memberships.SelectMany(value => value.RankAssignments).Where(value => value.IsActive).Select(value => value.rankDefinitionId));
            string[] offices = CleanAccessIds(memberships.SelectMany(value => value.OfficeAssignments).Where(value => value.IsActive).SelectMany(value =>
            {
                List<string> ids = new List<string> { value.officeId };
                if (OrganizationMemberships.TryGetOffice(value.officeId, out OrganizationOfficeSnapshot office)) ids.Add(office.Data.officeDefinitionId);
                return ids;
            }));
            string[] employments = CleanAccessIds(memberships.SelectMany(value => value.OfficeAssignments).Where(value => value.IsActive).Select(value => value.linkedEmploymentId));
            string[] authorities = CleanAccessIds(memberships.SelectMany(value => OrganizationAuthority == null
                ? Array.Empty<OrganizationEffectivePermissionSourceData>()
                : OrganizationAuthority.QueryEffectiveAuthority(personId, value.OrganizationId, worldTime, includeDelegated: true, privileged: false).Sources)
                .Where(value => !value.denied)
                .Select(value => value.permissionDefinitionId));
            GovernmentPermitRecordData[] permits = string.IsNullOrWhiteSpace(personId) || Governments == null
                ? Array.Empty<GovernmentPermitRecordData>()
                : Governments.Permits.Where(value => value.holderCategory == GovernmentPermitHolderCategory.Person && value.holderId == personId && value.IsActiveAt(worldTime)).ToArray();

            return new LocationConnectionAccessContextData
            {
                actor = actor?.Clone(),
                personId = personId,
                organizationIds = organizations,
                rankIds = ranks,
                officeIds = offices,
                authorityIds = authorities,
                employmentIds = employments,
                permitIds = CleanAccessIds(permits.SelectMany(value => new[] { value.permitId, value.permitDefinitionId }))
            };
        }

        private static string[] CleanAccessIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private void AdvanceWorldTravel()
        {
            if (worldTravelCoordinator == null || playTimeTracker == null) return;
            long entityRevisionBefore = worldEntityLocations?.Revision ?? -1L;
            IReadOnlyList<WorldTravelResult> results = worldTravelCoordinator.AdvanceActiveJourneys(playTimeTracker.CumulativeSeconds);
            if (!results.Any(result => result.Succeeded)) return;

            dirtyTracker?.MarkDirty("Authoritative world travel advanced.");
            if (worldEntityLocations != null && worldEntityLocations.Revision != entityRevisionBefore)
            {
                WorldSceneBindingRuntime.Default.SyncAllFromAuthoritative();
                // Presentation just moved from authoritative travel. Refresh the zone projection
                // without emitting a second physical border-crossing record for the same move.
                playerSpatialTerritoryTracker?.SampleNow(isRestoration: true);
            }
        }

        private void OnTravelHazardTriggered(TravelHazardExposureSnapshot hazard)
        {
            dirtyTracker?.MarkDirty("Travel hazard triggered.");
            PrototypeHudMessageBus.Show($"Travel hazard: {hazard?.HazardDefinitionId ?? "unknown"}");
        }

        private void OnTravelEncounterTriggered(TravelEncounterSnapshot encounter)
        {
            dirtyTracker?.MarkDirty("Travel encounter triggered.");
            PrototypeHudMessageBus.Show($"Travel encounter: {encounter?.EncounterDefinitionId ?? "unknown"}");
        }

        private void CreateWorldNarrativeRuntimes(DefinitionRegistry registry, string worldId)
        {
            worldQuests ??= new QuestRuntime(registry, worldId);
            worldQuestParticipation ??= new QuestParticipationRuntime(worldQuests, registry, worldId);
            worldQuestObjectives ??= new QuestObjectiveProgressRuntime(worldQuests, worldQuestParticipation, registry, worldId);
            questRewardEffectExecutor ??= new PrototypeQuestRewardEffectExecutor(this);
            worldQuestOutcomes ??= new QuestOutcomeRuntime(worldQuests, worldQuestParticipation, worldQuestObjectives, registry, questRewardEffectExecutor, runtimeWorldId: worldId);
            worldQuestOutcomes.Configure(worldQuests, worldQuestParticipation, worldQuestObjectives, registry, questRewardEffectExecutor, worldId);
            worldQuestSources ??= new QuestSourceRuntime(worldQuests, worldQuestParticipation, registry, worldId);
            worldConversations ??= new ConversationRuntime(registry, worldId);
            worldDialogue ??= new DialogueFlowRuntime(registry, worldConversations, runtimeWorldId: worldId);
            worldNarrativeEvents ??= new NarrativeEventRuntime(registry, runtimeWorldId: worldId);
            worldNarrativeState ??= new NarrativeStateRuntime(registry, runtimeWorldId: worldId);
            worldNarrativeArcs ??= new NarrativeArcRuntime(registry, runtimeWorldId: worldId);
            worldNarrativeCoordinator ??= new PrototypeNarrativeCoordinator(this, registry);

            narrativeStateIntegrations = new NarrativeStateRuntimeIntegrations
            {
                NarrativeEventRuntime = worldNarrativeEvents,
                ConsequenceValidator = (action, request) => action != null,
                ConsequenceExecutor = worldNarrativeCoordinator.ExecuteNarrativeStateConsequence
            };
            narrativeArcIntegrations = new NarrativeArcRuntimeIntegrations
            {
                QuestRuntime = worldQuests,
                QuestSourceRuntime = worldQuestSources,
                QuestOutcomeRuntime = worldQuestOutcomes,
                NarrativeEventRuntime = worldNarrativeEvents,
                NarrativeStateRuntime = worldNarrativeState,
                ActionExecutor = worldNarrativeCoordinator.ExecuteNarrativeArcAction
            };
            narrativeEventIntegrations = new NarrativeEventRuntimeIntegrations
            {
                QuestRuntime = worldQuests,
                QuestSourceRuntime = worldQuestSources,
                ConversationRuntime = worldConversations,
                InformationGrantExecutor = worldNarrativeCoordinator.GrantInformation,
                TravelConditionExecutor = worldNarrativeCoordinator.ExecuteTravelCondition,
                ConnectionChangeExecutor = worldNarrativeCoordinator.ExecuteConnectionChange,
                ContextualSocialActionExecutor = ExecuteNarrativeSocialAction,
                OrganizationActionExecutor = worldNarrativeCoordinator.ExecuteOrganizationAction,
                LegalActionExecutor = worldNarrativeCoordinator.ExecuteLegalAction,
                NarrativeStateTransitionExecutor = worldNarrativeState.RequestTransition,
                NarrativeStateConditionEvaluator = worldNarrativeCoordinator.EvaluateNarrativeStateCondition,
                NarrativeArcSignalExecutor = worldNarrativeArcs.ApplySignal,
                NarrativeArcConditionEvaluator = worldNarrativeArcs.EvaluateCondition
            };
            worldNarrativeEvents.Configure(registry, narrativeEventIntegrations, worldId);
            worldNarrativeState.Configure(registry, narrativeStateIntegrations, worldId);
            worldNarrativeArcs.Configure(registry, narrativeArcIntegrations, worldId);
            worldDialogue.Configure(registry, worldConversations, worldNarrativeCoordinator, worldId);
            worldDialogue.NarrativeStateConditionEvaluator = worldNarrativeCoordinator.EvaluateNarrativeStateCondition;
            worldNarrativeCoordinator.InitializePrototypeContent();
        }

        private bool ExecuteNarrativeSocialAction(NarrativeSocialActionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.InteractionDefinitionId)
                || string.IsNullOrWhiteSpace(request.ActorPersonId) || string.IsNullOrWhiteSpace(request.TargetPersonId))
            {
                return false;
            }

            return RecordSocialInteraction(
                request.InteractionDefinitionId,
                request.ActorPersonId,
                request.TargetPersonId,
                request.NarrativeEventId,
                $"social.narrative.{request.NarrativeEventId}.{request.ActionDefinitionId}").Succeeded;
        }

        private void EnsurePersistenceConsistencyValidators()
        {
            if (consistencyValidatorsRegistered || playerService == null || worldService == null)
            {
                return;
            }

            playerService.RegisterConsistencyValidator(
                new DelegatePersistenceConsistencyValidator("player.authoritative-state", true, ValidatePlayerPersistenceState),
                out string playerFailure);
            worldService.RegisterConsistencyValidator(
                new DelegatePersistenceConsistencyValidator("world.location-bindings", true, ValidateWorldPersistenceState),
                out string worldFailure);

            if (!string.IsNullOrWhiteSpace(playerFailure))
            {
                Debug.LogWarning(playerFailure);
            }

            if (!string.IsNullOrWhiteSpace(worldFailure))
            {
                Debug.LogWarning(worldFailure);
            }

            consistencyValidatorsRegistered = string.IsNullOrWhiteSpace(playerFailure) && string.IsNullOrWhiteSpace(worldFailure);
        }

        private PersistenceConsistencyAuditReport ValidatePlayerPersistenceState()
        {
            if (playerIdentityProgression == null || playerAttributes == null || playerResources == null || playerActorLifecycle == null)
            {
                return PersistenceConsistencyAuditReport.Critical(
                    "MissingPlayerAuthority",
                    "The restored player is missing an authoritative identity, attributes, resources, or lifecycle component.");
            }

            if (!CharacterResourceCollection.ValidateSaveData(
                    playerResources.CreateSaveData(playerService.PlayerId, playerIdentityProgression.PersonId),
                    GetDefinitionRegistry(),
                    playerCalculatedStats,
                    playerService.PlayerId,
                    out string resourceFailure))
            {
                return PersistenceConsistencyAuditReport.Critical("InvalidPlayerResources", resourceFailure, PlayerResourcesPersistenceParticipant.Key);
            }

            return PersistenceConsistencyAuditReport.Success("Player identity, attributes, resources, and lifecycle are consistent.");
        }

        private PersistenceConsistencyAuditReport ValidateWorldPersistenceState()
        {
            if (worldLocations == null || worldEntityLocations == null)
            {
                return PersistenceConsistencyAuditReport.Critical("MissingWorldAuthority", "World location authority is unavailable.");
            }

            LocationValidationReport locationReport = worldLocations.ValidateRuntime();
            if (!locationReport.Succeeded)
            {
                return PersistenceConsistencyAuditReport.Critical("InvalidWorldLocations", locationReport.Summary, LocationPersistenceParticipant.Key);
            }

            if (!worldEntityLocations.ValidateRuntime(out string entityFailure))
            {
                return PersistenceConsistencyAuditReport.Critical("InvalidEntityLocations", entityFailure, EntityLocationPersistenceParticipant.Key);
            }

            string interactionFailure = "Interaction point authority is unavailable.";
            if (worldInteractionPoints == null || !worldInteractionPoints.ValidateCurrent(out interactionFailure))
            {
                return PersistenceConsistencyAuditReport.Critical("InvalidInteractionPoints", interactionFailure ?? "Interaction point authority is unavailable.", InteractionPointPersistenceParticipant.Key);
            }

            string connectionFailure = "Location connection authority is unavailable.";
            if (worldLocationConnections == null || !worldLocationConnections.ValidateCurrent(out connectionFailure))
            {
                return PersistenceConsistencyAuditReport.Critical("InvalidLocationConnections", connectionFailure ?? "Location connection authority is unavailable.", LocationConnectionPersistenceParticipant.Key);
            }

            string routeFailure = "Location route authority is unavailable.";
            if (worldLocationRoutes == null || !worldLocationRoutes.ValidateCurrent(out routeFailure))
            {
                return PersistenceConsistencyAuditReport.Critical("InvalidLocationRoutes", routeFailure ?? "Location route authority is unavailable.", LocationRoutePersistenceParticipant.Key);
            }

            string journeyFailure = "Travel journey authority is unavailable.";
            if (worldTravelJourneys == null || !worldTravelJourneys.ValidateCurrent(out journeyFailure))
            {
                return PersistenceConsistencyAuditReport.Critical("InvalidTravelJourneys", journeyFailure ?? "Travel journey authority is unavailable.", TravelJourneyPersistenceParticipant.Key);
            }

            string conditionFailure = "Travel condition authority is unavailable.";
            if (worldTravelConditions == null || !worldTravelConditions.ValidateCurrent(out conditionFailure))
            {
                return PersistenceConsistencyAuditReport.Critical("InvalidTravelConditions", conditionFailure ?? "Travel condition authority is unavailable.", TravelConditionPersistenceParticipant.Key);
            }

            string politicalFailure = "Political travel authority is unavailable.";
            if (worldPoliticalTravel == null || !PoliticalTravelRuntime.ValidateSaveData(worldPoliticalTravel.CreateSaveData(), Governments, Laws, Crimes, worldLocations, worldLocationRoutes, worldService.WorldId, out politicalFailure))
            {
                return PersistenceConsistencyAuditReport.Critical("InvalidPoliticalTravel", politicalFailure ?? "Political travel authority is unavailable.", PoliticalTravelPersistenceParticipant.Key);
            }

            WorldSceneBindingValidationReport bindingReport = WorldSceneBindingRuntime.Default.Validate();
            return bindingReport.Succeeded
                ? PersistenceConsistencyAuditReport.Success($"World locations and scene bindings are consistent. {bindingReport.Summary}")
                : PersistenceConsistencyAuditReport.Critical("InvalidSceneBindings", bindingReport.Summary, LocationPersistenceParticipant.Key);
        }
    }
}
