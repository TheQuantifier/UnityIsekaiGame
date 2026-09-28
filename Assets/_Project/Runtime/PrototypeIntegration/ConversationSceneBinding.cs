using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.PrototypeIntegration
{
    [DisallowMultipleComponent]
    public sealed class ConversationSceneBinding : MonoBehaviour, IInteractionPointDestinationHandler
    {
        [SerializeField] private string conversationDefinitionId;
        [SerializeField] private string providerPersonId;
        [SerializeField] private string hostLocationId;
        [SerializeField] private string displayName;

        public string ConversationDefinitionId => conversationDefinitionId ?? string.Empty;
        public string ProviderPersonId => providerPersonId ?? string.Empty;
        public string HostLocationId => hostLocationId ?? string.Empty;
        public string InteractionPrompt => $"Speak with {(string.IsNullOrWhiteSpace(displayName) ? "the attendant" : displayName)}";

        public bool CanHandleInteraction(in InteractionContext context, InteractionPointSnapshot point)
        {
            PrototypePersistenceServiceBehaviour services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            return services?.NarrativeCoordinator != null
                && !GameUiModalState.IsModalActive
                && !string.IsNullOrWhiteSpace(conversationDefinitionId)
                && !string.IsNullOrWhiteSpace(providerPersonId);
        }

        public void HandleInteraction(in InteractionContext context, InteractionPointSnapshot point)
        {
            PrototypeDialoguePanel panel = FindAnyObjectByType<PrototypeDialoguePanel>();
            if (panel == null)
            {
                PrototypeHudMessageBus.Show("Dialogue presentation is unavailable.");
                return;
            }

            DialogueFlowOperationResult result = panel.Open(
                conversationDefinitionId,
                providerPersonId,
                hostLocationId,
                point?.InteractionPointId ?? string.Empty,
                string.Empty,
                string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName,
                context.Interactor);
            if (!result.Succeeded) PrototypeHudMessageBus.Show(result.Message);
        }

        public void Configure(string definitionId, string providerId, string locationId, string speakerName)
        {
            conversationDefinitionId = Normalize(definitionId);
            providerPersonId = Normalize(providerId);
            hostLocationId = Normalize(locationId);
            displayName = Normalize(speakerName);
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
