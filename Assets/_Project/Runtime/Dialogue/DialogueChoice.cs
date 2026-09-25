using System;
using UnityEngine;

namespace UnityIsekaiGame.Dialogue
{
    [Serializable]
    public sealed class DialogueChoice
    {
        [SerializeField] private string choiceText;
        [SerializeField] private DialogueNodeDefinition destination;
        [SerializeField] private string conditionKey;
        [SerializeField] private string socialInteractionDefinitionId;

        public string ChoiceText => string.IsNullOrWhiteSpace(choiceText) ? "Continue" : choiceText;
        public DialogueNodeDefinition Destination => destination;
        public string ConditionKey => conditionKey;
        public string SocialInteractionDefinitionId => socialInteractionDefinitionId;
    }
}
