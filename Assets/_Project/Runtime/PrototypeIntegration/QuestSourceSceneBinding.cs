using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.PrototypeIntegration
{
    public sealed class QuestSourceSceneBinding : MonoBehaviour, IInteractionPointDestinationHandler
    {
        [SerializeField] private string questSourceId;
        [SerializeField] private string questSourceDefinitionId;
        [SerializeField] private string sceneBindingKey;
        [SerializeField] private string sceneKey = PrototypeSceneIntegrationIds.SceneKey;
        [SerializeField] private string worldId = PersistenceService.LocalWorldId;
        [SerializeField] private string displayName;
        [SerializeField] private string hostLocationId;
        [SerializeField] private string interactionPointId;
        [SerializeField] private string conversationDefinitionId;
        [SerializeField] private string providerPersonId;
        [SerializeField] private bool required = true;

        public string QuestSourceId => questSourceId ?? string.Empty;
        public string QuestSourceDefinitionId => questSourceDefinitionId ?? string.Empty;
        public string SceneBindingKey => sceneBindingKey ?? string.Empty;
        public string SceneKey => sceneKey ?? string.Empty;
        public string WorldId => worldId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public string HostLocationId => hostLocationId ?? string.Empty;
        public string InteractionPointId => interactionPointId ?? string.Empty;
        public string ConversationDefinitionId => conversationDefinitionId ?? string.Empty;
        public string ProviderPersonId => providerPersonId ?? string.Empty;
        public bool OpensConversation => !string.IsNullOrWhiteSpace(conversationDefinitionId);
        public bool Required => required;
        public string InteractionPrompt
        {
            get
            {
                if (IsGuildDesk) return interactionPointId == PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId
                    ? "Use Merchant Guild Desk"
                    : "Use Adventurers Guild Desk";
                return OpensConversation ? $"Speak at {DisplayName}" : $"Browse {DisplayName}";
            }
        }

        private bool IsGuildDesk => interactionPointId == PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId
            || interactionPointId == PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId;

        public bool CanHandleInteraction(in InteractionContext context, InteractionPointSnapshot point)
        {
            PrototypePersistenceServiceBehaviour services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            return services?.NarrativeCoordinator != null && !GameUiModalState.IsModalActive;
        }

        public void HandleInteraction(in InteractionContext context, InteractionPointSnapshot point)
        {
            PrototypePersistenceServiceBehaviour services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            if (IsGuildDesk)
            {
                PrototypeGuildDeskPanel desk = FindAnyObjectByType<PrototypeGuildDeskPanel>(FindObjectsInactive.Include);
                if (desk == null)
                {
                    PrototypeHudMessageBus.Show("Guild desk services are unavailable.");
                    return;
                }

                desk.Open(
                    string.IsNullOrWhiteSpace(interactionPointId) ? point?.InteractionPointId : interactionPointId,
                    questSourceId,
                    conversationDefinitionId,
                    providerPersonId,
                    hostLocationId,
                    DisplayName,
                    context.Interactor);
                return;
            }

            if (OpensConversation)
            {
                PrototypeDialoguePanel dialogue = FindAnyObjectByType<PrototypeDialoguePanel>();
                if (dialogue == null)
                {
                    PrototypeHudMessageBus.Show("Dialogue presentation is unavailable.");
                    return;
                }
                DialogueFlowOperationResult result = dialogue.Open(
                    conversationDefinitionId,
                    providerPersonId,
                    hostLocationId,
                    string.IsNullOrWhiteSpace(interactionPointId) ? point?.InteractionPointId : interactionPointId,
                    questSourceId,
                    DisplayName,
                    context.Interactor);
                if (!result.Succeeded) PrototypeHudMessageBus.Show(result.Message);
                return;
            }

            PrototypeQuestSourcePanel panel = FindAnyObjectByType<PrototypeQuestSourcePanel>();
            if (panel == null)
            {
                PrototypeHudMessageBus.Show("Quest-source presentation is unavailable.");
                return;
            }

            panel.Open(QuestSourceId, string.IsNullOrWhiteSpace(InteractionPointId) ? point?.InteractionPointId : InteractionPointId, DisplayName, context.Interactor);
        }

        public void ConfigureQuestSource(
            string sourceId,
            string definitionId,
            string bindingKey,
            string display,
            string hostLocation,
            string interactionPoint,
            string scene = PrototypeSceneIntegrationIds.SceneKey,
            string world = PersistenceService.LocalWorldId,
            bool requiredBinding = true)
        {
            questSourceId = N(sourceId);
            questSourceDefinitionId = N(definitionId);
            sceneBindingKey = N(bindingKey);
            displayName = N(display);
            hostLocationId = N(hostLocation);
            interactionPointId = N(interactionPoint);
            sceneKey = string.IsNullOrWhiteSpace(scene) ? PrototypeSceneIntegrationIds.SceneKey : scene.Trim();
            worldId = string.IsNullOrWhiteSpace(world) ? PersistenceService.LocalWorldId : world.Trim();
            required = requiredBinding;
        }

        public void ConfigureConversation(string definitionId, string providerId)
        {
            conversationDefinitionId = N(definitionId);
            providerPersonId = N(providerId);
        }

        public PrototypeQuestSourceSceneBindingSnapshot CreateSnapshot()
        {
            return new PrototypeQuestSourceSceneBindingSnapshot(
                questSourceId,
                questSourceDefinitionId,
                sceneBindingKey,
                sceneKey,
                worldId,
                DisplayName,
                hostLocationId,
                interactionPointId,
                required);
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
