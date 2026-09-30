using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;
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
            PrototypePersistenceServiceBehaviour services = PrototypePersistenceServiceBehaviour.FindForInteractor(context.Interactor);
            PlayerInputReader input = context.Interactor == null ? null : context.Interactor.GetComponentInParent<PlayerInputReader>();
            return services?.NarrativeCoordinator != null
                && services.OwnsPlayerInteractor(context.Interactor)
                && input != null
                && !input.GameplayInputBlocked
                && !string.IsNullOrWhiteSpace(conversationDefinitionId)
                && !string.IsNullOrWhiteSpace(providerPersonId);
        }

        public void HandleInteraction(in InteractionContext context, InteractionPointSnapshot point)
        {
            PrototypePersistenceServiceBehaviour services = PrototypePersistenceServiceBehaviour.FindForInteractor(context.Interactor);
            PrototypeDialoguePanel panel = services == null ? null : services.GetComponent<PrototypeDialoguePanel>();
            if (panel == null)
            {
                GameHudMessageBus.Show("Dialogue presentation is unavailable.");
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
            if (!result.Succeeded) GameHudMessageBus.Show(result.Message);
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
