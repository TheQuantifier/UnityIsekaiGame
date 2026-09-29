#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Development.Automation
{
    public sealed class TestLabAutomationRunner : ITestLabAutomationRunner
    {
        private readonly TestLabAutomationRegistry registry;
        private readonly ITestLabAutomationResetCoordinator resetCoordinator;
        private readonly Func<string, TestLabAutomationHostResolution> hostResolver;
        private readonly TestLabDefinitionContext defaultDefinitionContext;
        private readonly TestLabAutomationTransactionIds transactionIds = new TestLabAutomationTransactionIds();
        private readonly List<(string SuiteId, string ScenarioId)> failedSelections = new List<(string SuiteId, string ScenarioId)>();
        private int runCounter;

        public TestLabAutomationRunner(
            TestLabAutomationRegistry registry,
            ITestLabAutomationResetCoordinator resetCoordinator,
            Func<string, TestLabAutomationHostResolution> hostResolver = null,
            TestLabDefinitionContext defaultDefinitionContext = null)
        {
            this.registry = registry;
            this.resetCoordinator = resetCoordinator;
            this.hostResolver = hostResolver ?? TestLabAutomationHostRegistry.ResolveActive;
            this.defaultDefinitionContext = defaultDefinitionContext ?? new TestLabDefinitionContext(new DefinitionRegistry(Array.Empty<IGameDefinition>()), "definitions.empty", "Empty automation definitions", catalogAuthored: false, fallbackDefinitionsAvailable: false, revision: 0);
        }

        public TestLabAutomationResult LastResult { get; private set; }
        public bool IsCancellationRequested { get; private set; }

        public TestLabAutomationResult RunScenario(string suiteId, string scenarioId, TestLabAutomationOptions options)
        {
            if (!registry.TryGetScenario(suiteId, scenarioId, out ITestLabAutomationSuite suite, out ITestLabAutomationScenario scenario))
            {
                return CompleteRun(CreateRunId(), TestLabAutomationRunMode.SelectedScenario, DateTime.UtcNow, false, new[]
                {
                    MissingScenarioResult(suiteId, scenarioId)
                });
            }

            return RunSelected(TestLabAutomationRunMode.SelectedScenario, new[] { (suite, scenario) }, options);
        }

        public TestLabAutomationResult RunSuite(string suiteId, TestLabAutomationOptions options)
        {
            if (!registry.TryGetSuite(suiteId, out ITestLabAutomationSuite suite))
            {
                return CompleteRun(CreateRunId(), TestLabAutomationRunMode.CurrentSuite, DateTime.UtcNow, false, new[]
                {
                    MissingScenarioResult(suiteId, string.Empty)
                });
            }

            return RunSelected(TestLabAutomationRunMode.CurrentSuite, suite.Scenarios
                .OrderBy(scenario => scenario.Order)
                .ThenBy(scenario => scenario.ScenarioId, StringComparer.Ordinal)
                .Select(scenario => (suite, scenario)), options);
        }

        public TestLabAutomationResult RunAll(bool quickOnly, TestLabAutomationOptions options)
        {
            IEnumerable<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)> selections =
                from suite in registry.Suites
                where suite.IncludeInRunAll
                from scenario in suite.Scenarios.OrderBy(scenario => scenario.Order).ThenBy(scenario => scenario.ScenarioId, StringComparer.Ordinal)
                where !quickOnly || scenario.IncludeInQuickRun || scenario.Category == TestLabAutomationCategory.Quick
                select (suite, scenario);

            return RunSelected(quickOnly ? TestLabAutomationRunMode.AllQuickSuites : TestLabAutomationRunMode.AllSuites, selections, options);
        }

        public TestLabAutomationResult RerunFailed(TestLabAutomationOptions options)
        {
            List<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)> selections = new List<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)>();
            foreach ((string suiteId, string scenarioId) in failedSelections.ToArray())
            {
                if (registry.TryGetScenario(suiteId, scenarioId, out ITestLabAutomationSuite suite, out ITestLabAutomationScenario scenario))
                {
                    selections.Add((suite, scenario));
                }
            }

            return RunSelected(TestLabAutomationRunMode.RerunFailed, selections, options);
        }

        public TestLabSuiteCompatibilityReport PreviewCompatibility(string suiteId = "")
        {
            IEnumerable<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)> selections;
            if (string.IsNullOrWhiteSpace(suiteId))
            {
                selections = from suite in registry.Suites
                             from scenario in suite.Scenarios.OrderBy(scenario => scenario.Order).ThenBy(scenario => scenario.ScenarioId, StringComparer.Ordinal)
                             select (suite, scenario);
            }
            else if (registry.TryGetSuite(suiteId, out ITestLabAutomationSuite suite))
            {
                selections = suite.Scenarios
                    .OrderBy(scenario => scenario.Order)
                    .ThenBy(scenario => scenario.ScenarioId, StringComparer.Ordinal)
                    .Select(scenario => (suite, scenario));
            }
            else
            {
                selections = Array.Empty<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)>();
            }

            return TestLabAutomationCompatibility.Preview(selections, hostResolver, defaultDefinitionContext);
        }

        public void Cancel()
        {
            IsCancellationRequested = true;
        }

        private TestLabAutomationResult RunSelected(TestLabAutomationRunMode mode, IEnumerable<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)> selections, TestLabAutomationOptions options)
        {
            options = options ?? TestLabAutomationOptions.Default;
            IsCancellationRequested = false;
            string runId = CreateRunId();
            DateTime started = DateTime.UtcNow;
            List<TestLabScenarioResult> results = new List<TestLabScenarioResult>();
            TestLabAutomationContext context = new TestLabAutomationContext(null, registry, resetCoordinator, transactionIds, new TestLabAutomationEventCapture(), runId);
            HashSet<ITestLabAutomationHost> usedHosts = new HashSet<ITestLabAutomationHost>();
            bool compatibilityFailed = false;

            try
            {
                (ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)[] orderedSelections = ApplyScenarioOrder(selections, options).ToArray();
                List<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)> runnableSelections = new List<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)>();
                foreach ((ITestLabAutomationSuite suite, ITestLabAutomationScenario scenario) in orderedSelections)
                {
                    TestLabScenarioResult skipped = CommandLineSkippedScenarioResult(suite, scenario, options);
                    if (skipped != null)
                    {
                        results.Add(skipped);
                    }
                    else
                    {
                        runnableSelections.Add((suite, scenario));
                    }
                }

                TestLabSuiteCompatibilityReport compatibility = TestLabAutomationCompatibility.Preview(runnableSelections, hostResolver, defaultDefinitionContext);
                if (!compatibility.Compatible)
                {
                    results.AddRange(compatibility.Scenarios.Where(scenario => !scenario.Compatible).Select(IncompatibleScenarioResult));
                    compatibilityFailed = true;
                }

                if (!compatibilityFailed)
                {
                    foreach ((ITestLabAutomationSuite suite, ITestLabAutomationScenario scenario) in runnableSelections)
                    {
                        if (IsCancellationRequested)
                        {
                            results.Add(CancelledScenarioResult(suite, scenario));
                            continue;
                        }

                        TestLabScenarioResult result = RunOne(context, suite, scenario, usedHosts);
                        results.Add(result);
                        if (options.StopOnFirstFailure && (result.Status == TestLabAutomationStatus.Failed || result.Status == TestLabAutomationStatus.Error))
                        {
                            IsCancellationRequested = true;
                        }
                    }
                }
            }
            finally
            {
                foreach (ITestLabAutomationHost host in usedHosts.ToArray())
                {
                    host.ResetEnvironment(new TestLabEnvironmentResetRequest(runId, string.Empty, string.Empty, "Clearing automation run scopes.", TestLabRuntimeArea.None));
                }
            }

            context.EventCapture.Dispose();
            failedSelections.Clear();
            failedSelections.AddRange(results
                .Where(result => result.Status == TestLabAutomationStatus.Failed || result.Status == TestLabAutomationStatus.Error)
                .Select(result => (result.SuiteId, result.ScenarioId)));

            return CompleteRun(runId, mode, started, IsCancellationRequested, results, options);
        }

        private TestLabScenarioResult RunOne(TestLabAutomationContext context, ITestLabAutomationSuite suite, ITestLabAutomationScenario scenario, ISet<ITestLabAutomationHost> usedHosts)
        {
            DateTime started = DateTime.UtcNow;
            List<TestLabAutomationStepResult> steps = new List<TestLabAutomationStepResult>();
            context.CurrentSuiteId = suite.SuiteId;
            context.CurrentScenarioId = scenario.ScenarioId;
            context.CurrentStepIndex = 0;
            context.CancellationRequested = false;

            try
            {
                TestLabAutomationStepResult hostValidation = ResolveAndValidateHost(context, suite, scenario);
                AddIfProblem(steps, hostValidation);
                if (hostValidation != null && !hostValidation.Succeeded)
                {
                    return new TestLabScenarioResult(suite.SuiteId, scenario.ScenarioId, scenario.DisplayName, ResolveScenarioStatus(steps), started, DateTime.UtcNow, steps);
                }

                if (context.Host != null)
                {
                    usedHosts?.Add(context.Host);
                }

                AddIfMeaningful(steps, context.ResetCoordinator.Reset(context, $"Preparing {suite.SuiteId}/{scenario.ScenarioId}."));
                TestLabAutomationStepResult createContext = TryCreateScenarioContext(context, suite, scenario, out TestLabScenarioContext scenarioContext);
                AddIfProblem(steps, createContext);
                if (createContext != null && !createContext.Succeeded)
                {
                    return new TestLabScenarioResult(suite.SuiteId, scenario.ScenarioId, scenario.DisplayName, ResolveScenarioStatus(steps), started, DateTime.UtcNow, steps);
                }

                context.BeginScenarioScope(scenarioContext);
                (context.Host as ITestLabAutomationScenarioScopeHost)?.SetActiveScenarioContext(context.ScenarioContext);
                AddIfProblem(steps, context.ScenarioContext?.Preflight());
                AddIfMeaningful(steps, suite.Setup(context));
                AddIfMeaningful(steps, scenario.Setup(context));

                foreach (ITestLabScenarioStep step in scenario.Steps)
                {
                    if (IsCancellationRequested)
                    {
                        steps.Add(TestLabAssertions.Cancelled(step.StepId, step.DisplayName, "Run cancellation requested."));
                        break;
                    }

                    TestLabAutomationStepResult hostContinuity = ValidateHostContinuity(context, suite, scenario);
                    AddIfProblem(steps, hostContinuity);
                    if (hostContinuity != null && !hostContinuity.Succeeded)
                    {
                        IsCancellationRequested = true;
                        break;
                    }

                    context.CurrentStepIndex++;
                    TestLabAutomationStepResult stepResult = RunStep(context, step);
                    steps.Add(stepResult);
                    if (!stepResult.Succeeded)
                    {
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                steps.Add(TestLabAssertions.Error("scenario.exception", scenario.DisplayName, exception));
            }
            finally
            {
                AddIfMeaningful(steps, SafeRun("scenario.cleanup", "Scenario cleanup", () => scenario.Cleanup(context)));
                AddIfMeaningful(steps, SafeRun("suite.teardown", "Suite teardown", () => suite.Teardown(context)));
                AddIfProblem(steps, SafeRun("fixture.audit", "Fixture mutation audit", () => context.ScenarioContext?.AuditMutationsBeforeRestore()));
                AddIfProblem(steps, SafeRun("fixture.restore", "Fixture restore", () => context.ScenarioContext?.RestoreIsolation()));
                AddIfProblem(steps, SafeRun("fixture.integrity", "Fixture baseline integrity", () => context.ScenarioContext?.VerifyRestoredBaseline()));
                AddIfMeaningful(steps, SafeRun("reset.cleanup", "Reset after scenario", () => context.ResetCoordinator.Reset(context, $"Cleaning {suite.SuiteId}/{scenario.ScenarioId}.")));
                (context.Host as ITestLabAutomationScenarioScopeHost)?.ClearActiveScenarioContext(context.ScenarioContext);
                context.EndScenarioScope();
                context.SetHost(null);
            }

            TestLabAutomationStatus status = ResolveScenarioStatus(steps);
            return new TestLabScenarioResult(suite.SuiteId, scenario.ScenarioId, scenario.DisplayName, status, started, DateTime.UtcNow, steps);
        }

        private static TestLabAutomationStepResult RunStep(TestLabAutomationContext context, ITestLabScenarioStep step)
        {
            try
            {
                return step.Run(context);
            }
            catch (Exception exception)
            {
                return TestLabAssertions.Error(step.StepId, step.DisplayName, exception);
            }
        }

        private TestLabAutomationStepResult ResolveAndValidateHost(TestLabAutomationContext context, ITestLabAutomationSuite suite, ITestLabAutomationScenario scenario)
        {
            if (scenario == null || !scenario.RequiresSceneHost)
            {
                context.SetHost(null);
                return null;
            }

            TestLabAutomationHostResolution resolution = hostResolver(scenario.RequiredHostId);
            if (resolution == null || !resolution.Succeeded)
            {
                return TestLabAutomationHostValidation.ResolutionFailure(resolution, suite?.SuiteId ?? string.Empty, scenario?.ScenarioId ?? string.Empty);
            }

            TestLabAutomationStepResult validation = TestLabAutomationHostValidation.ValidateHostForScenario(
                resolution.Capabilities,
                scenario.IsolationMode,
                scenario.RequiredRuntimeAreas,
                scenario.RequiredHostFeatures);
            if (!validation.Succeeded)
            {
                return validation;
            }

            TestLabDefinitionContext definitions = resolution.Host.GetDefinitionContext();
            if (definitions == null || !definitions.HasDefinitions)
            {
                return TestLabAssertions.Fail(
                    "host.validation",
                    "Automation host validation",
                    "DefinitionContext",
                    "Available",
                    "MissingDefinitions",
                    $"Suite={suite?.SuiteId} Scenario={scenario?.ScenarioId} Host={resolution.Capabilities.HostId}. The selected automation host does not provide definitions.");
            }

            context.SetHost(resolution.Host, resolution.RegistryRevision);
            return validation;
        }

        private static TestLabAutomationStepResult ValidateHostContinuity(TestLabAutomationContext context, ITestLabAutomationSuite suite, ITestLabAutomationScenario scenario)
        {
            if (context?.Host == null)
            {
                return null;
            }

            if (TestLabAutomationHostRegistry.IsRegistered(context.Host, context.HostRegistryRevision))
            {
                return null;
            }

            return TestLabAssertions.Fail(
                "host.continuity",
                "Automation host continuity",
                "SelectedHostStillRegistered",
                "Registered",
                "HostRemoved",
                $"Suite={suite?.SuiteId} Scenario={scenario?.ScenarioId} SelectedHost={context.Host.HostId} SelectedRegistryRevision={context.HostRegistryRevision} CurrentRegistryRevision={TestLabAutomationHostRegistry.Revision}. Host was removed or registry changed while automation was active; no replacement host was selected.");
        }

        private TestLabAutomationStepResult TryCreateScenarioContext(TestLabAutomationContext context, ITestLabAutomationSuite suite, ITestLabAutomationScenario scenario, out TestLabScenarioContext scenarioContext)
        {
            scenarioContext = null;
            if (context.Host == null)
            {
                DefinitionRegistry registry = defaultDefinitionContext?.Registry;
                if (registry == null)
                {
                    return TestLabAssertions.Fail(
                        "host.runtime-bundle",
                        "Automation runtime bundle",
                        "RuntimeBundle",
                        "Created",
                        "MissingDefinitions",
                        $"Suite={suite.SuiteId} Scenario={scenario.ScenarioId} has no scene host and no default definition context.");
                }

                TestLabRuntimeBundle runtimeBundle = TestLabRuntimeBundle.CreateFresh(registry, $"person.testlab.{context.RunId}", "world.testlab", Array.Empty<string>(), Array.Empty<string>());
                scenarioContext = new TestLabScenarioContext(
                    context.RunId,
                    suite.SuiteId,
                    scenario.ScenarioId,
                    scenario.IsolationMode,
                    runtimeBundle,
                    runtimeBundle,
                    requiredRuntimeAreas: scenario.RequiredRuntimeAreas,
                    requiredFixtureIds: scenario.RequiredFixtureIds);
                return TestLabAssertions.Pass("host.runtime-bundle", "Automation runtime bundle", $"Created scene-independent FreshRuntime bundle from {defaultDefinitionContext.SourceId}.");
            }

            TestLabRuntimeBundleRequest request = new TestLabRuntimeBundleRequest(
                context.RunId,
                suite.SuiteId,
                scenario.ScenarioId,
                scenario.IsolationMode,
                scenario.RequiredRuntimeAreas,
                scenario.RequiredFixtureIds,
                context.TransactionIds == null ? 0 : context.TransactionIds.GetHashCode(),
                allowSceneObjects: scenario.RequiresSceneHost,
                visibleUiRequired: (scenario.RequiredHostFeatures & TestLabHostFeature.VisibleUi) != 0,
                persistenceRequired: (scenario.RequiredRuntimeAreas & TestLabRuntimeArea.Persistence) != 0,
                snapshotRequired: scenario.IsolationMode == TestLabScenarioIsolationMode.SnapshotRestore);
            TestLabRuntimeBundleResult result = context.Host.CreateRuntimeBundle(request);
            if (result == null || !result.Succeeded)
            {
                return TestLabAssertions.Fail(
                    "host.runtime-bundle",
                    "Automation runtime bundle",
                    "RuntimeBundle",
                    "Created",
                    result?.FailureCode ?? "Failed",
                    $"Host={context.Host.HostId} Suite={suite.SuiteId} Scenario={scenario.ScenarioId}. {result?.Message}");
            }

            scenarioContext = new TestLabScenarioContext(
                context.RunId,
                suite.SuiteId,
                scenario.ScenarioId,
                scenario.IsolationMode,
                result.Bundle,
                result.OwnedBundle,
                scenario.RequiredRuntimeAreas,
                scenario.RequiredFixtureIds,
                context.Host.CaptureFingerprint);
            return TestLabAssertions.Pass("host.runtime-bundle", "Automation runtime bundle", $"Created runtime bundle from host '{context.Host.HostId}'.");
        }

        private static TestLabAutomationStepResult SafeRun(string stepId, string displayName, Func<TestLabAutomationStepResult> action)
        {
            try
            {
                return action == null ? TestLabAssertions.Pass(stepId, displayName) : action();
            }
            catch (Exception exception)
            {
                return TestLabAssertions.Error(stepId, displayName, exception);
            }
        }

        private static void AddIfMeaningful(List<TestLabAutomationStepResult> steps, TestLabAutomationStepResult step)
        {
            if (step != null)
            {
                steps.Add(step);
            }
        }

        private static void AddIfProblem(List<TestLabAutomationStepResult> steps, TestLabAutomationStepResult step)
        {
            if (step != null && !step.Succeeded)
            {
                steps.Add(step);
            }
        }

        private static TestLabAutomationStatus ResolveScenarioStatus(IReadOnlyList<TestLabAutomationStepResult> steps)
        {
            if (steps.Any(step => step.Status == TestLabAutomationStatus.Error))
            {
                return TestLabAutomationStatus.Error;
            }

            if (steps.Any(step => step.Status == TestLabAutomationStatus.Failed))
            {
                return TestLabAutomationStatus.Failed;
            }

            if (steps.Any(step => step.Status == TestLabAutomationStatus.Cancelled))
            {
                return TestLabAutomationStatus.Cancelled;
            }

            return steps.Count == 0 || steps.All(step => step.Status == TestLabAutomationStatus.Skipped)
                ? TestLabAutomationStatus.Skipped
                : TestLabAutomationStatus.Passed;
        }

        private TestLabAutomationResult CompleteRun(string runId, TestLabAutomationRunMode mode, DateTime started, bool cancelled, IEnumerable<TestLabScenarioResult> scenarios, TestLabAutomationOptions options = null)
        {
            options ??= TestLabAutomationOptions.Default;
            LastResult = new TestLabAutomationResult(runId, mode, started, DateTime.UtcNow, cancelled, scenarios, options.ScenarioOrder, options.ShuffleSeed);
            return LastResult;
        }

        private string CreateRunId()
        {
            runCounter++;
            return $"run-{runCounter:0000}";
        }

        private static IReadOnlyList<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)> ApplyScenarioOrder(
            IEnumerable<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)> selections,
            TestLabAutomationOptions options)
        {
            List<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)> ordered = (selections ?? Enumerable.Empty<(ITestLabAutomationSuite Suite, ITestLabAutomationScenario Scenario)>()).ToList();
            switch (options?.ScenarioOrder ?? TestLabAutomationScenarioOrder.Normal)
            {
                case TestLabAutomationScenarioOrder.Reverse:
                    ordered.Reverse();
                    return ordered;
                case TestLabAutomationScenarioOrder.Shuffled:
                    return ordered
                        .Select((selection, index) => (selection, index, key: ShuffleKey(options.ShuffleSeed, selection.Suite.SuiteId, selection.Scenario.ScenarioId, index)))
                        .OrderBy(item => item.key)
                        .ThenBy(item => item.index)
                        .Select(item => item.selection)
                        .ToArray();
                default:
                    return ordered;
            }
        }

        private static int ShuffleKey(int seed, string suiteId, string scenarioId, int index)
        {
            unchecked
            {
                int hash = seed == 0 ? 8675309 : seed;
                string text = $"{suiteId}:{scenarioId}:{index}";
                for (int i = 0; i < text.Length; i++)
                {
                    hash = (hash * 16777619) ^ text[i];
                }

                return hash;
            }
        }

        private static TestLabScenarioResult MissingScenarioResult(string suiteId, string scenarioId)
        {
            DateTime now = DateTime.UtcNow;
            string id = string.IsNullOrWhiteSpace(scenarioId) ? "missing-suite" : scenarioId;
            return new TestLabScenarioResult(suiteId, id, id, TestLabAutomationStatus.Failed, now, now, new[]
            {
                TestLabAssertions.Fail("select", "Resolve automation selection", "NotNull", "registered suite/scenario", "missing", $"Suite '{suiteId}' scenario '{scenarioId}' was not registered.")
            });
        }

        private static TestLabScenarioResult IncompatibleScenarioResult(TestLabScenarioCompatibilityResult scenario)
        {
            TestLabAutomationStepResult step = TestLabAssertions.Fail(
                "host.compatibility",
                "Automation host compatibility",
                "CompatibleScenario",
                "Compatible",
                scenario?.FailureCode ?? "Incompatible",
                scenario?.Diagnostics ?? "Scenario is incompatible with the selected automation host.");
            return new TestLabScenarioResult(
                scenario?.SuiteId ?? string.Empty,
                scenario?.ScenarioId ?? string.Empty,
                scenario?.DisplayName ?? "Incompatible scenario",
                TestLabAutomationStatus.Failed,
                DateTime.UtcNow,
                DateTime.UtcNow,
                new[] { step });
        }

        private static TestLabScenarioResult CancelledScenarioResult(ITestLabAutomationSuite suite, ITestLabAutomationScenario scenario)
        {
            DateTime now = DateTime.UtcNow;
            return new TestLabScenarioResult(suite.SuiteId, scenario.ScenarioId, scenario.DisplayName, TestLabAutomationStatus.Cancelled, now, now, new[]
            {
                TestLabAssertions.Cancelled("cancelled", "Scenario not run", "Run cancellation requested before scenario started.")
            });
        }

        private static TestLabScenarioResult CommandLineSkippedScenarioResult(ITestLabAutomationSuite suite, ITestLabAutomationScenario scenario, TestLabAutomationOptions options)
        {
            if (options == null || options.RunSurface != TestLabAutomationRunSurface.CommandLine || scenario == null)
            {
                return null;
            }

            string reason = string.Empty;
            switch (scenario.CommandLineSupport)
            {
                case TestLabCommandLineSupport.Supported:
                    return null;
                case TestLabCommandLineSupport.RequiresScene:
                    if (options.CommandLineSceneAvailable)
                    {
                        return null;
                    }

                    reason = string.IsNullOrWhiteSpace(scenario.CommandLineUnsupportedReason)
                        ? "Scenario requires -testLabScene for command-line execution."
                        : scenario.CommandLineUnsupportedReason;
                    break;
                case TestLabCommandLineSupport.Unsupported:
                    reason = string.IsNullOrWhiteSpace(scenario.CommandLineUnsupportedReason)
                        ? "Scenario is not supported from command-line automation."
                        : scenario.CommandLineUnsupportedReason;
                    break;
                default:
                    reason = $"Scenario declares unsupported command-line support mode '{scenario.CommandLineSupport}'.";
                    break;
            }

            DateTime now = DateTime.UtcNow;
            return new TestLabScenarioResult(
                suite?.SuiteId ?? string.Empty,
                scenario.ScenarioId,
                scenario.DisplayName,
                TestLabAutomationStatus.Skipped,
                now,
                now,
                new[]
                {
                    TestLabAssertions.Skip("command-line.support", "Command-line support", reason)
                });
        }
    }
}
#endif
