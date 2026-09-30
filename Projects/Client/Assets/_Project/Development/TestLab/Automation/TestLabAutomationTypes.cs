#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Development.Automation
{
    public enum TestLabAutomationCategory
    {
        Quick = 0,
        Standard = 1,
        Extended = 2
    }

    public enum TestLabAutomationStatus
    {
        NotRun = 0,
        Running = 1,
        Passed = 2,
        Failed = 3,
        Skipped = 4,
        Cancelled = 5,
        Error = 6
    }

    public enum TestLabAutomationRunMode
    {
        SelectedScenario = 0,
        CurrentSuite = 1,
        AllQuickSuites = 2,
        AllSuites = 3,
        RerunFailed = 4
    }

    public enum TestLabAutomationScenarioOrder
    {
        Normal = 0,
        Reverse = 1,
        Shuffled = 2
    }

    public enum TestLabAutomationRunSurface
    {
        InGame = 0,
        CommandLine = 1
    }

    public enum TestLabCommandLineSupport
    {
        Supported = 0,
        RequiresScene = 1,
        Unsupported = 2
    }

    [Flags]
    public enum TestLabRuntimeArea
    {
        None = 0,
        KnowledgeHistory = 1 << 0,
        Character = 1 << 1,
        Combat = 1 << 2,
        Biology = 1 << 3,
        Persistence = 1 << 4,
        Items = 1 << 5,
        Professions = 1 << 6,
        Economy = 1 << 7,
        Social = 1 << 8,
        Organizations = 1 << 9,
        OrganizationMemberships = 1 << 10,
        OrganizationAuthority = 1 << 11,
        OrganizationResources = 1 << 12,
        OrganizationDecisions = 1 << 13,
        Factions = 1 << 14,
        Diplomacy = 1 << 15,
        Governments = 1 << 16,
        Laws = 1 << 17,
        Crimes = 1 << 18,
        Justice = 1 << 19,
        WorldLocations = 1 << 20,
        Quests = 1 << 21
    }

    [Flags]
    public enum TestLabHostFeature
    {
        None = 0,
        DefinitionContext = 1 << 0,
        SceneReset = 1 << 1,
        SnapshotRestore = 1 << 2,
        SharedRuntime = 1 << 3,
        PersistentFixture = 1 << 4,
        DeterministicTime = 1 << 5,
        FixtureFingerprinting = 1 << 6,
        DirtyStateInspection = 1 << 7,
        DomainEventInspection = 1 << 8,
        VisibleUi = 1 << 9,
        AutomatedExecution = 1 << 10,
        DevelopmentOnly = 1 << 11,
        Persistence = 1 << 12
    }

    public sealed class TestLabAutomationOptions
    {
        public static readonly TestLabAutomationOptions Default = new TestLabAutomationOptions();

        public bool StopOnFirstFailure { get; set; }
        public bool IncludeExtended { get; set; } = true;
        public int MaximumFrameWait { get; set; } = 120;
        public TestLabAutomationScenarioOrder ScenarioOrder { get; set; } = TestLabAutomationScenarioOrder.Normal;
        public int ShuffleSeed { get; set; } = 8675309;
        public TestLabAutomationRunSurface RunSurface { get; set; } = TestLabAutomationRunSurface.InGame;
        public bool CommandLineSceneAvailable { get; set; }
    }

    public sealed class TestLabAutomationStepResult
    {
        public TestLabAutomationStepResult(
            string stepId,
            string displayName,
            TestLabAutomationStatus status,
            string assertionType,
            string expected,
            string actual,
            string actorId,
            string transactionId,
            string diagnostics,
            Exception exception = null)
        {
            StepId = string.IsNullOrWhiteSpace(stepId) ? "step" : stepId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? StepId : displayName;
            Status = status;
            AssertionType = assertionType ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Diagnostics = diagnostics ?? string.Empty;
            ExceptionType = exception == null ? string.Empty : exception.GetType().FullName;
            ExceptionMessage = exception == null ? string.Empty : exception.Message;
        }

        public string StepId { get; }
        public string DisplayName { get; }
        public TestLabAutomationStatus Status { get; }
        public string AssertionType { get; }
        public string Expected { get; }
        public string Actual { get; }
        public string ActorId { get; }
        public string TransactionId { get; }
        public string Diagnostics { get; }
        public string ExceptionType { get; }
        public string ExceptionMessage { get; }
        public bool Succeeded => Status == TestLabAutomationStatus.Passed || Status == TestLabAutomationStatus.Skipped;
    }

    public sealed class TestLabScenarioResult
    {
        public TestLabScenarioResult(
            string suiteId,
            string scenarioId,
            string displayName,
            TestLabAutomationStatus status,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            IEnumerable<TestLabAutomationStepResult> steps)
        {
            SuiteId = suiteId ?? string.Empty;
            ScenarioId = scenarioId ?? string.Empty;
            DisplayName = displayName ?? ScenarioId;
            Status = status;
            StartedAtUtc = startedAtUtc;
            EndedAtUtc = endedAtUtc;
            Steps = (steps ?? Array.Empty<TestLabAutomationStepResult>()).ToArray();
        }

        public string SuiteId { get; }
        public string ScenarioId { get; }
        public string DisplayName { get; }
        public TestLabAutomationStatus Status { get; }
        public DateTime StartedAtUtc { get; }
        public DateTime EndedAtUtc { get; }
        public TimeSpan Elapsed => EndedAtUtc >= StartedAtUtc ? EndedAtUtc - StartedAtUtc : TimeSpan.Zero;
        public IReadOnlyList<TestLabAutomationStepResult> Steps { get; }
    }

    public sealed class TestLabAutomationResult
    {
        public TestLabAutomationResult(
            string runId,
            TestLabAutomationRunMode runMode,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            bool cancelled,
            IEnumerable<TestLabScenarioResult> scenarios,
            TestLabAutomationScenarioOrder scenarioOrder = TestLabAutomationScenarioOrder.Normal,
            int shuffleSeed = 0)
        {
            RunId = string.IsNullOrWhiteSpace(runId) ? "run" : runId;
            RunMode = runMode;
            StartedAtUtc = startedAtUtc;
            EndedAtUtc = endedAtUtc;
            Cancelled = cancelled;
            Scenarios = (scenarios ?? Array.Empty<TestLabScenarioResult>()).ToArray();
            ScenarioOrder = scenarioOrder;
            ShuffleSeed = shuffleSeed;
        }

        public string RunId { get; }
        public TestLabAutomationRunMode RunMode { get; }
        public DateTime StartedAtUtc { get; }
        public DateTime EndedAtUtc { get; }
        public TimeSpan Elapsed => EndedAtUtc >= StartedAtUtc ? EndedAtUtc - StartedAtUtc : TimeSpan.Zero;
        public bool Cancelled { get; }
        public TestLabAutomationScenarioOrder ScenarioOrder { get; }
        public int ShuffleSeed { get; }
        public IReadOnlyList<TestLabScenarioResult> Scenarios { get; }
        public int TotalScenarios => Scenarios.Count;
        public int PassedScenarios => Scenarios.Count(scenario => scenario.Status == TestLabAutomationStatus.Passed);
        public int FailedScenarios => Scenarios.Count(scenario => scenario.Status == TestLabAutomationStatus.Failed);
        public int ErrorScenarios => Scenarios.Count(scenario => scenario.Status == TestLabAutomationStatus.Error);
        public int SkippedScenarios => Scenarios.Count(scenario => scenario.Status == TestLabAutomationStatus.Skipped);
        public int CancelledScenarios => Scenarios.Count(scenario => scenario.Status == TestLabAutomationStatus.Cancelled);
        public int TotalSteps => Scenarios.Sum(scenario => scenario.Steps.Count);
        public bool HasFailures => FailedScenarios > 0 || ErrorScenarios > 0;
    }
}
#endif
