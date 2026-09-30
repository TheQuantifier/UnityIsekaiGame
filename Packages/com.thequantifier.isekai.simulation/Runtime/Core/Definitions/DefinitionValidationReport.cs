using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.GameData
{
    public sealed class DefinitionValidationReport
    {
        private readonly List<DefinitionIdValidationMessage> messages = new List<DefinitionIdValidationMessage>();

        public IReadOnlyList<DefinitionIdValidationMessage> Messages => messages;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public int InfoCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;

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
            else
            {
                InfoCount++;
            }
        }

        public void AddError(string message)
        {
            Add(DefinitionIdValidationSeverity.Error, message);
        }

        public void AddWarning(string message)
        {
            Add(DefinitionIdValidationSeverity.Warning, message);
        }

        public void AddInfo(string message)
        {
            Add(DefinitionIdValidationSeverity.Info, message);
        }

        public void AddRange(DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            foreach (DefinitionIdValidationMessage message in report.Messages)
            {
                Add(message.Severity, message.Message);
            }
        }

        public string GetSummary()
        {
            if (messages.Count == 0)
            {
                return "Definition validation passed with no messages.";
            }

            string[] lines = new string[messages.Count + 1];
            lines[0] = $"Definition validation finished with {ErrorCount} error(s), {WarningCount} warning(s), and {InfoCount} info message(s).";

            for (int i = 0; i < messages.Count; i++)
            {
                lines[i + 1] = $"{messages[i].Severity}: {messages[i].Message}";
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
