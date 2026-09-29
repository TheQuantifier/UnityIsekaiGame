using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.CharacterSystem
{
    public sealed class CharacterIntegrityReport
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> infos = new List<string>();

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public IReadOnlyList<string> Infos => infos;
        public bool Passed => errors.Count == 0;

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                errors.Add(message);
            }
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                warnings.Add(message);
            }
        }

        public void AddInfo(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                infos.Add(message);
            }
        }

        public string GetSummary()
        {
            List<string> lines = new List<string>
            {
                $"Character integrity: {(Passed ? "Passed" : "Failed")} ({errors.Count} error(s), {warnings.Count} warning(s), {infos.Count} info message(s))."
            };
            lines.AddRange(errors.Select(error => $"Error: {error}"));
            lines.AddRange(warnings.Select(warning => $"Warning: {warning}"));
            lines.AddRange(infos.Select(info => $"Info: {info}"));
            return string.Join(Environment.NewLine, lines);
        }
    }
}
