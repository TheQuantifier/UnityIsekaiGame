#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Development.Automation
{
    public interface ITestLabAutomationSuite
    {
        string SuiteId { get; }
        string DisplayName { get; }
        string Feature { get; }
        string Description { get; }
        int Order { get; }
        TestLabAutomationCategory Category { get; }
        bool IncludeInRunAll { get; }
        IReadOnlyList<string> RequiredServices { get; }
        IReadOnlyList<ITestLabAutomationScenario> Scenarios { get; }
        TestLabAutomationStepResult Setup(TestLabAutomationContext context);
        TestLabAutomationStepResult Teardown(TestLabAutomationContext context);
    }

    public interface ITestLabAutomationScenario
    {
        string ScenarioId { get; }
        string DisplayName { get; }
        string Description { get; }
        int Order { get; }
        TestLabAutomationCategory Category { get; }
        bool IncludeInQuickRun { get; }
        TestLabScenarioIsolationMode IsolationMode { get; }
        TestLabRuntimeArea RequiredRuntimeAreas { get; }
        bool RequiresSceneHost { get; }
        TestLabCommandLineSupport CommandLineSupport { get; }
        string CommandLineUnsupportedReason { get; }
        string RequiredHostId { get; }
        TestLabHostFeature RequiredHostFeatures { get; }
        IReadOnlyList<string> RequiredDefinitionIds { get; }
        IReadOnlyList<string> RequiredFixtureIds { get; }
        IReadOnlyList<ITestLabScenarioStep> Steps { get; }
        TestLabAutomationStepResult Setup(TestLabAutomationContext context);
        TestLabAutomationStepResult Cleanup(TestLabAutomationContext context);
    }

    public interface ITestLabScenarioStep
    {
        string StepId { get; }
        string DisplayName { get; }
        TestLabAutomationStepResult Run(TestLabAutomationContext context);
    }

    public interface ITestLabAutomationResetCoordinator
    {
        TestLabAutomationStepResult Reset(TestLabAutomationContext context, string reason);
    }

    public interface ITestLabAutomationRunner
    {
        TestLabAutomationResult LastResult { get; }
        bool IsCancellationRequested { get; }
        TestLabAutomationResult RunScenario(string suiteId, string scenarioId, TestLabAutomationOptions options);
        TestLabAutomationResult RunSuite(string suiteId, TestLabAutomationOptions options);
        TestLabAutomationResult RunAll(bool quickOnly, TestLabAutomationOptions options);
        TestLabAutomationResult RerunFailed(TestLabAutomationOptions options);
        TestLabSuiteCompatibilityReport PreviewCompatibility(string suiteId = "");
        void Cancel();
    }

    public sealed class TestLabAutomationContext
    {
        public TestLabAutomationContext(
            ITestLabAutomationHost host,
            TestLabAutomationRegistry registry,
            ITestLabAutomationResetCoordinator resetCoordinator,
            TestLabAutomationTransactionIds transactionIds,
            TestLabAutomationEventCapture eventCapture,
            string runId)
        {
            Host = host;
            Registry = registry;
            ResetCoordinator = resetCoordinator;
            TransactionIds = transactionIds;
            EventCapture = eventCapture;
            RunId = runId ?? string.Empty;
        }

        public ITestLabAutomationHost Host { get; private set; }
        public TestLabAutomationRegistry Registry { get; }
        public ITestLabAutomationResetCoordinator ResetCoordinator { get; }
        public TestLabAutomationTransactionIds TransactionIds { get; }
        public TestLabAutomationEventCapture EventCapture { get; }
        public string RunId { get; }
        public long HostRegistryRevision { get; private set; }
        public string CurrentSuiteId { get; set; }
        public string CurrentScenarioId { get; set; }
        public int CurrentStepIndex { get; set; }
        public bool CancellationRequested { get; set; }
        public TestLabScenarioContext ScenarioContext { get; private set; }

        public T GetHost<T>() where T : class
        {
            return Host as T;
        }

        internal void SetHost(ITestLabAutomationHost host, long registryRevision = 0L)
        {
            Host = host;
            HostRegistryRevision = registryRevision;
        }

        internal void BeginScenarioScope(TestLabScenarioContext scenarioContext)
        {
            ScenarioContext?.Dispose();
            ScenarioContext = scenarioContext;
        }

        internal void EndScenarioScope()
        {
            ScenarioContext?.Dispose();
            ScenarioContext = null;
        }
    }

    public sealed class TestLabAutomationSuite : ITestLabAutomationSuite
    {
        private readonly IReadOnlyList<ITestLabAutomationScenario> scenarios;
        private readonly IReadOnlyList<string> requiredServices;
        private readonly Func<TestLabAutomationContext, TestLabAutomationStepResult> setup;
        private readonly Func<TestLabAutomationContext, TestLabAutomationStepResult> teardown;

        public TestLabAutomationSuite(
            string suiteId,
            string displayName,
            string feature,
            string description,
            int order,
            TestLabAutomationCategory category,
            bool includeInRunAll,
            IEnumerable<string> requiredServices,
            IEnumerable<ITestLabAutomationScenario> scenarios,
            Func<TestLabAutomationContext, TestLabAutomationStepResult> setup = null,
            Func<TestLabAutomationContext, TestLabAutomationStepResult> teardown = null)
        {
            SuiteId = suiteId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Feature = feature ?? string.Empty;
            Description = description ?? string.Empty;
            Order = order;
            Category = category;
            IncludeInRunAll = includeInRunAll;
            this.requiredServices = new List<string>(requiredServices ?? Array.Empty<string>()).AsReadOnly();
            this.scenarios = new List<ITestLabAutomationScenario>(scenarios ?? Array.Empty<ITestLabAutomationScenario>()).AsReadOnly();
            this.setup = setup;
            this.teardown = teardown;
        }

        public string SuiteId { get; }
        public string DisplayName { get; }
        public string Feature { get; }
        public string Description { get; }
        public int Order { get; }
        public TestLabAutomationCategory Category { get; }
        public bool IncludeInRunAll { get; }
        public IReadOnlyList<string> RequiredServices => requiredServices;
        public IReadOnlyList<ITestLabAutomationScenario> Scenarios => scenarios;
        public TestLabAutomationStepResult Setup(TestLabAutomationContext context) => setup == null ? TestLabAssertions.Pass("suite.setup", "Suite setup") : setup(context);
        public TestLabAutomationStepResult Teardown(TestLabAutomationContext context) => teardown == null ? TestLabAssertions.Pass("suite.teardown", "Suite teardown") : teardown(context);
    }

    public sealed class TestLabAutomationScenario : ITestLabAutomationScenario
    {
        private readonly IReadOnlyList<ITestLabScenarioStep> steps;
        private readonly Func<TestLabAutomationContext, TestLabAutomationStepResult> setup;
        private readonly Func<TestLabAutomationContext, TestLabAutomationStepResult> cleanup;

        public TestLabAutomationScenario(
            string scenarioId,
            string displayName,
            string description,
            int order,
            TestLabAutomationCategory category,
            bool includeInQuickRun,
            IEnumerable<ITestLabScenarioStep> steps,
            Func<TestLabAutomationContext, TestLabAutomationStepResult> setup = null,
            Func<TestLabAutomationContext, TestLabAutomationStepResult> cleanup = null,
            TestLabScenarioIsolationMode isolationMode = TestLabScenarioIsolationMode.FreshRuntime,
            TestLabRuntimeArea requiredRuntimeAreas = TestLabRuntimeArea.KnowledgeHistory,
            IEnumerable<string> requiredFixtureIds = null,
            bool? requiresSceneHost = null,
            string requiredHostId = "",
            TestLabHostFeature requiredHostFeatures = TestLabHostFeature.AutomatedExecution,
            IEnumerable<string> requiredDefinitionIds = null,
            TestLabCommandLineSupport? commandLineSupport = null,
            string commandLineUnsupportedReason = "")
        {
            ScenarioId = scenarioId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Order = order;
            Category = category;
            IncludeInQuickRun = includeInQuickRun;
            IsolationMode = isolationMode;
            RequiredRuntimeAreas = requiredRuntimeAreas;
            RequiresSceneHost = requiresSceneHost ?? RequiresHostByDefault(isolationMode, requiredRuntimeAreas, requiredHostId, requiredHostFeatures);
            CommandLineSupport = commandLineSupport ?? DefaultCommandLineSupport(RequiresSceneHost);
            CommandLineUnsupportedReason = string.IsNullOrWhiteSpace(commandLineUnsupportedReason)
                ? DefaultCommandLineUnsupportedReason(CommandLineSupport)
                : commandLineUnsupportedReason.Trim();
            RequiredHostId = requiredHostId ?? string.Empty;
            RequiredHostFeatures = requiredHostFeatures;
            RequiredDefinitionIds = (requiredDefinitionIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
            RequiredFixtureIds = (requiredFixtureIds ?? TestLabScenarioContext.DefaultRequiredFixtureIds).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
            this.steps = new List<ITestLabScenarioStep>(steps ?? Array.Empty<ITestLabScenarioStep>()).AsReadOnly();
            this.setup = setup;
            this.cleanup = cleanup;
        }

        public string ScenarioId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int Order { get; }
        public TestLabAutomationCategory Category { get; }
        public bool IncludeInQuickRun { get; }
        public TestLabScenarioIsolationMode IsolationMode { get; }
        public TestLabRuntimeArea RequiredRuntimeAreas { get; }
        public bool RequiresSceneHost { get; }
        public TestLabCommandLineSupport CommandLineSupport { get; }
        public string CommandLineUnsupportedReason { get; }
        public string RequiredHostId { get; }
        public TestLabHostFeature RequiredHostFeatures { get; }
        public IReadOnlyList<string> RequiredDefinitionIds { get; }
        public IReadOnlyList<string> RequiredFixtureIds { get; }
        public IReadOnlyList<ITestLabScenarioStep> Steps => steps;
        public TestLabAutomationStepResult Setup(TestLabAutomationContext context) => setup == null ? TestLabAssertions.Pass("scenario.setup", "Scenario setup") : setup(context);
        public TestLabAutomationStepResult Cleanup(TestLabAutomationContext context) => cleanup == null ? TestLabAssertions.Pass("scenario.cleanup", "Scenario cleanup") : cleanup(context);

        private static bool RequiresHostByDefault(
            TestLabScenarioIsolationMode isolationMode,
            TestLabRuntimeArea requiredRuntimeAreas,
            string requiredHostId,
            TestLabHostFeature requiredHostFeatures)
        {
            if (!string.IsNullOrWhiteSpace(requiredHostId))
            {
                return true;
            }

            if (requiredHostFeatures != TestLabHostFeature.None && requiredHostFeatures != TestLabHostFeature.AutomatedExecution)
            {
                return true;
            }

            if (isolationMode != TestLabScenarioIsolationMode.FreshRuntime)
            {
                return true;
            }

            const TestLabRuntimeArea hostlessFreshAreas = TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.Economy | TestLabRuntimeArea.Social | TestLabRuntimeArea.Organizations | TestLabRuntimeArea.OrganizationMemberships | TestLabRuntimeArea.OrganizationAuthority | TestLabRuntimeArea.OrganizationResources | TestLabRuntimeArea.OrganizationDecisions | TestLabRuntimeArea.Factions | TestLabRuntimeArea.Diplomacy | TestLabRuntimeArea.Governments | TestLabRuntimeArea.Laws | TestLabRuntimeArea.Crimes | TestLabRuntimeArea.Justice | TestLabRuntimeArea.WorldLocations | TestLabRuntimeArea.Quests;
            return (requiredRuntimeAreas & ~hostlessFreshAreas) != TestLabRuntimeArea.None;
        }

        private static TestLabCommandLineSupport DefaultCommandLineSupport(bool requiresSceneHost)
        {
            return requiresSceneHost ? TestLabCommandLineSupport.RequiresScene : TestLabCommandLineSupport.Supported;
        }

        private static string DefaultCommandLineUnsupportedReason(TestLabCommandLineSupport support)
        {
            return support switch
            {
                TestLabCommandLineSupport.RequiresScene => "This scenario requires a registered scene automation host; the command runner could not provide one from the default or explicit scene.",
                TestLabCommandLineSupport.Unsupported => "This scenario is only supported from the in-game Test Lab runner.",
                _ => string.Empty
            };
        }
    }

    public sealed class TestLabScenarioStep : ITestLabScenarioStep
    {
        private readonly Func<TestLabAutomationContext, TestLabAutomationStepResult> action;

        public TestLabScenarioStep(string stepId, string displayName, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            StepId = stepId ?? string.Empty;
            DisplayName = displayName ?? StepId;
            this.action = action;
        }

        public string StepId { get; }
        public string DisplayName { get; }
        public TestLabAutomationStepResult Run(TestLabAutomationContext context) => action == null
            ? TestLabAssertions.Skip(StepId, DisplayName, "No step action is registered.")
            : action(context);
    }
}
#endif
