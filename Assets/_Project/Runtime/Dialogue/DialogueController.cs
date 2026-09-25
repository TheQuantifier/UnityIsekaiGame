using System;
using UnityEngine;

namespace UnityIsekaiGame.Dialogue
{
    public sealed class DialogueController : MonoBehaviour
    {
        private DialogueNodeDefinition currentNode;
        private string activeParticipantDisplayName;
        private Sprite activeParticipantPortrait;
        private string activeParticipantPersonId;
        private bool isActive;

        public bool IsActive => isActive;
        public DialogueNodeDefinition CurrentNode => currentNode;
        public bool IsAwaitingChoice => isActive && currentNode != null && currentNode.HasChoices;
        public string ActiveParticipantPersonId => activeParticipantPersonId ?? string.Empty;

        public event Action<DialogueNodeDefinition> DialogueStarted;
        public event Action<DialogueNodeDefinition> NodeChanged;
        public event Action DialogueEnded;
        public event Action<DialogueChoice> ChoiceSelected;

        public DialogueOperationResult StartDialogue(DialogueNodeDefinition startingNode)
        {
            return StartDialogue(startingNode, null, null, null);
        }

        public DialogueOperationResult StartDialogue(DialogueNodeDefinition startingNode, string participantDisplayName, Sprite participantPortrait)
        {
            return StartDialogue(startingNode, participantDisplayName, participantPortrait, null);
        }

        public DialogueOperationResult StartDialogue(DialogueNodeDefinition startingNode, string participantDisplayName, Sprite participantPortrait, string participantPersonId)
        {
            if (isActive)
            {
                return DialogueOperationResult.Failure("Already in dialogue.");
            }

            if (startingNode == null)
            {
                return DialogueOperationResult.Failure("Invalid dialogue definition.");
            }

            isActive = true;
            currentNode = startingNode;
            activeParticipantDisplayName = participantDisplayName;
            activeParticipantPortrait = participantPortrait;
            activeParticipantPersonId = participantPersonId;
            DialogueStarted?.Invoke(currentNode);
            NodeChanged?.Invoke(currentNode);
            return DialogueOperationResult.Success("Dialogue started.");
        }

        public string GetSpeakerName(DialogueNodeDefinition node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(activeParticipantDisplayName) && node.SpeakerName != "Player")
            {
                return activeParticipantDisplayName;
            }

            return node.SpeakerName;
        }

        public Sprite GetPortrait(DialogueNodeDefinition node)
        {
            if (node == null)
            {
                return null;
            }

            if (activeParticipantPortrait != null && node.SpeakerName != "Player")
            {
                return activeParticipantPortrait;
            }

            return node.Portrait;
        }

        public DialogueOperationResult Advance()
        {
            if (!isActive || currentNode == null)
            {
                return DialogueOperationResult.Failure("No active dialogue.");
            }

            if (currentNode.HasChoices)
            {
                return DialogueOperationResult.Failure("Awaiting dialogue choice.");
            }

            if (currentNode.EndsConversation || currentNode.NextNodes.Count == 0)
            {
                EndDialogue();
                return DialogueOperationResult.Success("Dialogue ended.");
            }

            DialogueNodeDefinition nextNode = currentNode.NextNodes[0];
            if (nextNode == null)
            {
                EndDialogue();
                return DialogueOperationResult.Failure("Dialogue ended because the next node is missing.");
            }

            currentNode = nextNode;
            NodeChanged?.Invoke(currentNode);
            return DialogueOperationResult.Success("Dialogue advanced.");
        }

        public DialogueOperationResult Choose(int choiceIndex)
        {
            if (!isActive || currentNode == null)
            {
                return DialogueOperationResult.Failure("No active dialogue.");
            }

            if (!currentNode.HasChoices)
            {
                return DialogueOperationResult.Failure("Dialogue is not awaiting a choice.");
            }

            if (choiceIndex < 0 || choiceIndex >= currentNode.Choices.Count)
            {
                return DialogueOperationResult.Failure("Invalid dialogue choice.");
            }

            DialogueChoice choice = currentNode.Choices[choiceIndex];
            if (choice == null || choice.Destination == null)
            {
                EndDialogue();
                return DialogueOperationResult.Failure("Dialogue ended because the selected choice has no destination.");
            }

            ChoiceSelected?.Invoke(choice);
            currentNode = choice.Destination;
            NodeChanged?.Invoke(currentNode);
            return DialogueOperationResult.Success("Dialogue choice selected.");
        }

        public DialogueOperationResult Cancel()
        {
            if (!isActive || currentNode == null)
            {
                return DialogueOperationResult.Failure("No active dialogue.");
            }

            if (!currentNode.CanCancel)
            {
                return DialogueOperationResult.Failure("Dialogue cannot be cancelled here.");
            }

            EndDialogue();
            return DialogueOperationResult.Success("Dialogue cancelled.");
        }

        public void EndDialogue()
        {
            if (!isActive)
            {
                return;
            }

            isActive = false;
            currentNode = null;
            activeParticipantDisplayName = null;
            activeParticipantPortrait = null;
            activeParticipantPersonId = null;
            DialogueEnded?.Invoke();
        }
    }
}
