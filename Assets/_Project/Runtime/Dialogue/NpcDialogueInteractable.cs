using UnityEngine;
using System;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.People;
using UnityIsekaiGame.Social.Interactions;

namespace UnityIsekaiGame.Dialogue
{
    public sealed class NpcDialogueInteractable : MonoBehaviour, IInteractable, IDialogueParticipant
    {
        [SerializeField] private string interactionPrompt = "Talk";
        [SerializeField] private PersonIdentity personIdentity;
        [SerializeField] private DialogueController dialogueController;
        [SerializeField] private DialogueNodeDefinition startingNode;
        [SerializeField] private PrototypePersistenceServiceBehaviour services;

        public string InteractionPrompt => personIdentity != null && personIdentity.HasValidIdentity
            ? $"Talk to {personIdentity.DisplayName}"
            : interactionPrompt;
        public PersonIdentity PersonIdentity => personIdentity;
        public string DialogueDisplayName => personIdentity == null ? string.Empty : personIdentity.DisplayName;
        public event Action<NpcDialogueInteractable> DialogueStarted;

        private void Awake()
        {
            if (personIdentity == null)
            {
                personIdentity = GetComponent<PersonIdentity>();
            }

            if (dialogueController == null)
            {
                dialogueController = FindAnyObjectByType<DialogueController>();
            }

            if (services == null)
            {
                services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            }

            if (dialogueController != null)
            {
                dialogueController.ChoiceSelected += HandleChoiceSelected;
            }
        }

        private void OnDestroy()
        {
            if (dialogueController != null)
            {
                dialogueController.ChoiceSelected -= HandleChoiceSelected;
            }
        }

        public bool CanInteract(in InteractionContext context)
        {
            if (!enabled || !isActiveAndEnabled || startingNode == null || dialogueController == null || dialogueController.IsActive)
            {
                return false;
            }

            PlayerInputReader input = context.Interactor == null ? null : context.Interactor.GetComponentInParent<PlayerInputReader>();
            if (input != null && input.GameplayInputBlocked)
            {
                return false;
            }

            PlayerHealth health = context.Interactor == null ? null : context.Interactor.GetComponentInParent<PlayerHealth>();
            return health == null || !health.IsDefeated;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            DialogueOperationResult result = dialogueController.StartDialogue(startingNode, DialogueDisplayName, personIdentity == null ? null : personIdentity.Portrait, personIdentity == null ? string.Empty : personIdentity.PersonId);
            Debug.Log(result.Message);
            if (result.Succeeded)
            {
                RecordSocialInteraction(PrototypeSocialInteractionDefinitionFactory.GreetId, "dialogue.start");
                DialogueStarted?.Invoke(this);
            }

            if (!result.Succeeded)
            {
                PrototypeHudMessageBus.Show(result.Message);
            }
        }

        private void HandleChoiceSelected(DialogueChoice choice)
        {
            if (choice == null || dialogueController == null || personIdentity == null
                || !string.Equals(dialogueController.ActiveParticipantPersonId, personIdentity.PersonId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(choice.SocialInteractionDefinitionId))
            {
                return;
            }

            RecordSocialInteraction(choice.SocialInteractionDefinitionId, "dialogue.choice");
        }

        private void RecordSocialInteraction(string definitionId, string source)
        {
            if (services == null || personIdentity == null || !personIdentity.HasValidIdentity)
            {
                return;
            }

            SocialInteractionResult result = services.RecordSocialInteraction(
                definitionId,
                services.PlayerPersonId,
                personIdentity.PersonId,
                source);
            if (!result.Succeeded && result.Status != SocialInteractionStatus.CooldownActive)
            {
                Debug.LogWarning($"Dialogue social consequence was rejected: {result.Message}", this);
            }
        }
    }
}
