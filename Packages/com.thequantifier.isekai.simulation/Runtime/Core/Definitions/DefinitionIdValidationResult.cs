using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.GameData
{
    public sealed class DefinitionIdValidationResult
    {
        private readonly List<DefinitionIdValidationMessage> messages = new List<DefinitionIdValidationMessage>();

        public IReadOnlyList<DefinitionIdValidationMessage> Messages => messages;
        public string NormalizedSuggestion { get; private set; }
        public bool IsValid => ErrorCount == 0;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }

        public void Add(DefinitionIdValidationSeverity severity, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            messages.Add(new DefinitionIdValidationMessage(severity, message));

            if (severity == DefinitionIdValidationSeverity.Error)
            {
                ErrorCount++;
            }
            else if (severity == DefinitionIdValidationSeverity.Warning)
            {
                WarningCount++;
            }
        }

        public void SetNormalizedSuggestion(string suggestion)
        {
            NormalizedSuggestion = suggestion;
        }

        public void AddRange(DefinitionIdValidationResult result)
        {
            if (result == null)
            {
                return;
            }

            foreach (DefinitionIdValidationMessage message in result.Messages)
            {
                Add(message.Severity, message.Message);
            }

            if (!string.IsNullOrWhiteSpace(result.NormalizedSuggestion))
            {
                SetNormalizedSuggestion(result.NormalizedSuggestion);
            }
        }

        public string GetSummary()
        {
            if (messages.Count == 0)
            {
                return "No validation messages.";
            }

            string[] lines = new string[messages.Count];
            for (int i = 0; i < messages.Count; i++)
            {
                lines[i] = $"{messages[i].Severity}: {messages[i].Message}";
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
