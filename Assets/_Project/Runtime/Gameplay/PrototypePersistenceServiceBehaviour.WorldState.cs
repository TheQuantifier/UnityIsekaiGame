using System;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Narrative;
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
        private NarrativeArcRuntime worldNarrativeArcs;

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
        public QuestRuntime WorldQuests => worldQuests;
        public ConversationRuntime WorldConversations => worldConversations;
        public NarrativeEventRuntime WorldNarrativeEvents => worldNarrativeEvents;

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
                new DialogueFlowPersistenceParticipant(worldDialogue, GetDefinitionRegistry, () => worldConversations, ownerId: worldId),
                new NarrativeEventPersistenceParticipant(worldNarrativeEvents, GetDefinitionRegistry, () => narrativeEventIntegrations, ownerId: worldId),
                new NarrativeStatePersistenceParticipant(worldNarrativeState, GetDefinitionRegistry, ownerId: worldId),
                new NarrativeArcPersistenceParticipant(worldNarrativeArcs, GetDefinitionRegistry, ownerId: worldId)
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
            PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(worldLocationConnections, registry, worldLocations, worldEntityLocations, worldInteractionPoints, worldId);
            PrototypeLocationRouteDefinitionFactory.SeedPrototypeRoutes(worldLocationRoutes, registry, worldLocations, worldLocationConnections, worldId);
            worldTravelConditions.Configure(registry, worldLocationRoutes, worldTravelJourneys, worldId);
            worldLocationRoutes.Configure(registry, worldLocations, worldLocationConnections, worldId, worldTravelConditions);
            worldTravelJourneys.Configure(registry, worldLocations, worldEntityLocations, worldLocationConnections, worldLocationRoutes, worldId, worldTravelConditions);
            worldPoliticalTravel.Configure(registry, Governments, Laws, Crimes, Justice, worldLocations, worldLocationRoutes, worldId);
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
            narrativeEventIntegrations = new NarrativeEventRuntimeIntegrations
            {
                QuestRuntime = worldQuests,
                QuestSourceRuntime = worldQuestSources,
                ConversationRuntime = worldConversations,
                ContextualSocialActionExecutor = ExecuteNarrativeSocialAction
            };
            worldNarrativeEvents ??= new NarrativeEventRuntime(registry, narrativeEventIntegrations, runtimeWorldId: worldId);
            worldNarrativeEvents.Configure(registry, narrativeEventIntegrations, worldId);
            worldNarrativeState ??= new NarrativeStateRuntime(registry, runtimeWorldId: worldId);
            worldNarrativeArcs ??= new NarrativeArcRuntime(registry, runtimeWorldId: worldId);
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

            WorldSceneBindingValidationReport bindingReport = WorldSceneBindingRuntime.Default.Validate();
            return bindingReport.Succeeded
                ? PersistenceConsistencyAuditReport.Success($"World locations and scene bindings are consistent. {bindingReport.Summary}")
                : PersistenceConsistencyAuditReport.Critical("InvalidSceneBindings", bindingReport.Summary, LocationPersistenceParticipant.Key);
        }
    }
}
