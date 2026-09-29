#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Text.RegularExpressions;

namespace UnityIsekaiGame.Development.Automation
{
    public sealed class TestLabAutomationTransactionIds
    {
        private static readonly Regex InvalidCharacters = new Regex("[^a-zA-Z0-9_.-]", RegexOptions.Compiled);

        public string Create(string suiteId, string scenarioId, string runId, int stepIndex, string operation)
        {
            string suite = Sanitize(suiteId);
            string scenario = Sanitize(scenarioId);
            string run = Sanitize(runId);
            string op = Sanitize(operation);
            return $"testlab.{suite}.{scenario}.{run}.step-{Math.Max(0, stepIndex):000}.{op}";
        }

        public string CreateDuplicateOf(string suiteId, string scenarioId, string runId, int stepIndex, string operation)
        {
            return Create(suiteId, scenarioId, runId, stepIndex, operation);
        }

        private static string Sanitize(string value)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();
            return InvalidCharacters.Replace(text, "-").ToLowerInvariant();
        }
    }
}
#endif
