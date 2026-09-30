#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Development.Automation
{
    public sealed class TestLabAutomationRegistry
    {
        private readonly List<ITestLabAutomationSuite> suites = new List<ITestLabAutomationSuite>();

        public IReadOnlyList<ITestLabAutomationSuite> Suites => suites
            .OrderBy(suite => suite.Order)
            .ThenBy(suite => suite.SuiteId, StringComparer.Ordinal)
            .ToArray();

        public bool TryRegister(ITestLabAutomationSuite suite, out string failure)
        {
            failure = string.Empty;
            if (suite == null)
            {
                failure = "Suite is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(suite.SuiteId))
            {
                failure = "Suite ID is missing.";
                return false;
            }

            if (suites.Any(existing => string.Equals(existing.SuiteId, suite.SuiteId, StringComparison.Ordinal)))
            {
                failure = $"Duplicate suite ID '{suite.SuiteId}'.";
                return false;
            }

            suites.Add(suite);
            suites.Sort((left, right) =>
            {
                int order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : string.CompareOrdinal(left.SuiteId, right.SuiteId);
            });
            return true;
        }

        public void Clear()
        {
            suites.Clear();
        }

        public bool TryGetSuite(string suiteId, out ITestLabAutomationSuite suite)
        {
            suite = suites.FirstOrDefault(candidate => string.Equals(candidate.SuiteId, suiteId, StringComparison.Ordinal));
            return suite != null;
        }

        public bool TryGetScenario(string suiteId, string scenarioId, out ITestLabAutomationSuite suite, out ITestLabAutomationScenario scenario)
        {
            scenario = null;
            if (!TryGetSuite(suiteId, out suite))
            {
                return false;
            }

            scenario = suite.Scenarios.FirstOrDefault(candidate => string.Equals(candidate.ScenarioId, scenarioId, StringComparison.Ordinal));
            return scenario != null;
        }
    }
}
#endif
