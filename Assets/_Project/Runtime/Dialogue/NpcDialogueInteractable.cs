using System;
using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.People;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Dialogue
{
    public sealed class NpcDialogueInteractable : MonoBehaviour, IInteractable, IDialogueParticipant
    {
        [SerializeField] private string interactionPrompt = "Talk";
        [SerializeField] private PersonIdentity personIdentity;
        [SerializeField] private PrototypePersistenceServiceBehaviour services;
        [SerializeField] private string conversationDefinitionId;
        [SerializeField] private string hostLocationId;
        [SerializeField] private string interactionPointId;
        [SerializeField] private string questSourceId;

        public string InteractionPrompt => personIdentity != null && personIdentity.HasValidIdentity
            ? $"Talk to {personIdentity.DisplayName}"
            : interactionPrompt;
        public PersonIdentity PersonIdentity => personIdentity;
        public string DialogueDisplayName => personIdentity == null ? string.Empty : personIdentity.DisplayName;
        public event Action<NpcDialogueInteractable> DialogueStarted;

        private void Awake() => ResolveReferences();

        public bool CanInteract(in InteractionContext context)
        {
            ResolveReferences();
            if (services == null || !services.OwnsPlayerInteractor(context.Interactor))
                services = PrototypePersistenceServiceBehaviour.FindForInteractor(context.Interactor);
            if (!enabled || !isActiveAndEnabled || services?.NarrativeCoordinator == null || !services.OwnsPlayerInteractor(context.Interactor)) return false;
            PlayerInputReader input = context.Interactor == null ? null : context.Interactor.GetComponentInParent<PlayerInputReader>();
            if (input != null && input.GameplayInputBlocked) return false;
            PlayerHealth health = context.Interactor == null ? null : context.Interactor.GetComponentInParent<PlayerHealth>();
            return (health == null || !health.IsDefeated) && !string.IsNullOrWhiteSpace(ResolveConversationDefinitionId());
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context)) return;
            PrototypeDialoguePanel panel = services.GetComponent<PrototypeDialoguePanel>();
            if (panel == null)
            {
                GameHudMessageBus.Show("Dialogue presentation is unavailable.");
                return;
            }

            string personId = personIdentity?.PersonId ?? string.Empty;
            DialogueFlowOperationResult result = panel.Open(
                ResolveConversationDefinitionId(),
                personId,
                ResolveLocationId(),
                interactionPointId,
                questSourceId,
                DialogueDisplayName,
                context.Interactor);
            if (!result.Succeeded)
            {
                GameHudMessageBus.Show(result.Message);
                return;
            }

            Social.Interactions.SocialInteractionResult social = services.RecordSocialInteraction(
                Social.Interactions.PrototypeSocialInteractionDefinitionFactory.GreetId,
                services.PlayerPersonId,
                personId,
                "dialogue.start");
            if (!social.Succeeded && social.Status != Social.Interactions.SocialInteractionStatus.CooldownActive)
                Debug.LogWarning($"Dialogue social consequence was rejected: {social.Message}", this);
            DialogueStarted?.Invoke(this);
        }

        public void ConfigureNarrativeConversation(string definitionId, string locationId, string pointId = "", string sourceId = "")
        {
            conversationDefinitionId = definitionId?.Trim() ?? string.Empty;
            hostLocationId = locationId?.Trim() ?? string.Empty;
            interactionPointId = pointId?.Trim() ?? string.Empty;
            questSourceId = sourceId?.Trim() ?? string.Empty;
        }

        private void ResolveReferences()
        {
            if (personIdentity == null) personIdentity = GetComponent<PersonIdentity>();
        }

        private string ResolveConversationDefinitionId()
        {
            if (!string.IsNullOrWhiteSpace(conversationDefinitionId)) return conversationDefinitionId;
            return personIdentity?.PersonId switch
            {
                PrototypeEntityLocationFactory.AdventurersGuildReceptionistPersonId => PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId,
                PrototypeEntityLocationFactory.MerchantGuildReceptionistPersonId => PrototypeConversationDefinitionFactory.MerchantGuildCounterDefinitionId,
                PrototypeEntityLocationFactory.MerchantPersonId => PrototypeConversationDefinitionFactory.MerchantGuildCounterDefinitionId,
                PrototypeEntityLocationFactory.GuildMasterPersonId => PrototypeConversationDefinitionFactory.GuildHeadOfficeDefinitionId,
                PrototypeEntityLocationFactory.RecordsClerkPersonId => PrototypeConversationDefinitionFactory.RecordsDeskDefinitionId,
                PrototypeEntityLocationFactory.PrisonerPersonId => PrototypeConversationDefinitionFactory.PrisonerInterviewDefinitionId,
                _ => string.Empty
            };
        }

        private string ResolveLocationId()
        {
            if (!string.IsNullOrWhiteSpace(hostLocationId)) return hostLocationId;
            if (services?.WorldEntityLocations == null || personIdentity == null) return string.Empty;
            return services.WorldEntityLocations.ResolvePhysicalLocation(PrototypeEntityLocationFactory.Person(personIdentity.PersonId))?.LocationId ?? string.Empty;
        }
    }
}
