#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityIsekaiGame.Development;
using UnityIsekaiGame.Development.Automation;

namespace UnityIsekaiGame.Editor.Tools.TestLabAutomation
{
    public static class TestLabAutomationBatchCommand
    {
        private const string BatchHostId = "host.batch.scene-independent";
        private const string DefaultAutomationScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        public static void Run()
        {
            TestLabAutomationCommandLineOptions options = TestLabAutomationCommandLineOptions.Parse(Environment.GetCommandLineArgs());
            TestLabAutomationBatchCommandResult commandResult = Run(options);

            if (commandResult.ExitCode == 0)
            {
                Debug.Log(commandResult.Message);
            }
            else
            {
                Debug.LogWarning(commandResult.Message);
            }

            if (Application.isBatchMode || options.ExitUnity)
            {
                EditorApplication.Exit(commandResult.ExitCode);
            }
        }

        public static TestLabAutomationBatchCommandResult Run(TestLabAutomationCommandLineOptions options)
        {
            options ??= TestLabAutomationCommandLineOptions.Parse(Array.Empty<string>());
            if (options.HelpRequested)
            {
                return TestLabAutomationBatchCommandResult.Success(TestLabAutomationCommandLineOptions.Usage(), Array.Empty<string>(), null);
            }

            if (!options.Valid)
            {
                return TestLabAutomationBatchCommandResult.Fail(2, $"{options.Error} {TestLabAutomationCommandLineOptions.Usage()}", Array.Empty<string>(), null);
            }

            PrototypeTestLabService sceneService = null;
            try
            {
                PrototypeTestLabAutomationCatalogValidationResult catalogValidation = PrototypeTestLabAutomationCatalog.Validate();
                if (!catalogValidation.Succeeded)
                {
                    return TestLabAutomationBatchCommandResult.Fail(3, $"{catalogValidation.ToSummary()} {string.Join(" | ", catalogValidation.Errors)}", Array.Empty<string>(), null);
                }

                TestLabAutomationRegistry registry = PrototypeTestLabAutomationCatalog.CreateDefaultRegistry(options.StepFilter);
                string scenePath = ResolveAutomationScenePath(options, registry);
                if (!string.IsNullOrWhiteSpace(scenePath))
                {
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                    {
                        if (!string.IsNullOrWhiteSpace(options.ScenePath))
                        {
                            return TestLabAutomationBatchCommandResult.Fail(2, $"Test Lab automation scene was not found at '{options.ScenePath}'.", Array.Empty<string>(), null);
                        }
                    }
                    else
                    {
                        EditorSceneManager.OpenScene(scenePath);
                        sceneService = CreateSceneService();
                    }
                }

                TestLabDefinitionContext definitions = PrototypeTestLabService.CreateDefaultAutomationDefinitionContext();
                string batchHostId = $"{BatchHostId}.{Guid.NewGuid():N}";
                TestLabSceneIndependentAutomationHost batchHost = new TestLabSceneIndependentAutomationHost(definitions.Registry, batchHostId);
                if (!TestLabAutomationHostRegistry.Register(batchHost, out string registrationFailure))
                {
                    return TestLabAutomationBatchCommandResult.Fail(3, $"Test Lab automation batch host registration failed: {registrationFailure}", Array.Empty<string>(), null);
                }

                TestLabAutomationRunner runner = new TestLabAutomationRunner(
                    registry,
                    new TestLabAutomationHostResetCoordinator(),
                    requiredHostId => ResolveBatchHost(batchHost, requiredHostId),
                    definitions);

                TestLabAutomationReportExporter exporter = new TestLabAutomationReportExporter();
                if (options.Action == TestLabAutomationCommandAction.List)
                {
                    IReadOnlyList<string> catalogPaths = exporter.ExportCatalog(registry, CatalogReportFormat(options.ReportFormat), options.OutputDirectory, options.OutputPath);
                    return TestLabAutomationBatchCommandResult.Success($"Test Lab automation catalog exported. Suites={registry.Suites.Count} Reports={string.Join(", ", catalogPaths)}.", catalogPaths, null);
                }

                if (options.Action == TestLabAutomationCommandAction.Compatibility)
                {
                    TestLabSuiteCompatibilityReport compatibility = runner.PreviewCompatibility(options.SuiteId);
                    IReadOnlyList<string> compatibilityPaths = exporter.ExportCompatibility(compatibility, CatalogReportFormat(options.ReportFormat), options.OutputDirectory, options.OutputPath);
                    string message = $"Test Lab automation compatibility exported. {compatibility.ToDiagnostic()} Reports={string.Join(", ", compatibilityPaths)}.";
                    return compatibility.Compatible
                        ? TestLabAutomationBatchCommandResult.Success(message, compatibilityPaths, null)
                        : TestLabAutomationBatchCommandResult.Fail(1, message, compatibilityPaths, null);
                }

                TestLabAutomationResult result = Execute(runner, options, sceneService != null);
                IReadOnlyList<string> reportPaths = exporter.Export(result, options.ReportFormat, options.OutputDirectory, options.OutputPath);
                LogFailures(result);

                string summary = $"Test Lab automation finished. {FormatResult(result)} Reports={string.Join(", ", reportPaths)}.";
                return result.HasFailures || result.CancelledScenarios > 0
                    ? TestLabAutomationBatchCommandResult.Fail(1, summary, reportPaths, result)
                    : TestLabAutomationBatchCommandResult.Success(summary, reportPaths, result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return TestLabAutomationBatchCommandResult.Fail(3, $"Test Lab automation command failed: {exception.GetType().Name}: {exception.Message}", Array.Empty<string>(), null);
            }
            finally
            {
                if (sceneService != null)
                {
                    PrototypeTestLabServiceLocator.Unregister(sceneService);
                    sceneService.UnregisterAutomationHost();
                }
            }
        }

        private static TestLabAutomationResult Execute(TestLabAutomationRunner runner, TestLabAutomationCommandLineOptions options, bool sceneAvailable)
        {
            TestLabAutomationOptions automationOptions = options.ToAutomationOptions();
            automationOptions.CommandLineSceneAvailable = sceneAvailable;
            return options.RunMode switch
            {
                TestLabAutomationRunMode.SelectedScenario => runner.RunScenario(options.SuiteId, options.ScenarioId, automationOptions),
                TestLabAutomationRunMode.CurrentSuite => runner.RunSuite(options.SuiteId, automationOptions),
                TestLabAutomationRunMode.AllSuites => runner.RunAll(quickOnly: false, automationOptions),
                _ => runner.RunAll(quickOnly: true, automationOptions)
            };
        }

        private static TestLabAutomationReportFormat CatalogReportFormat(TestLabAutomationReportFormat requested)
        {
            TestLabAutomationReportFormat format = requested == 0 ? TestLabAutomationReportFormat.Both : requested;
            format &= ~TestLabAutomationReportFormat.JUnit;
            return format == 0 ? TestLabAutomationReportFormat.Json : format;
        }

        private static TestLabAutomationHostResolution ResolveBatchHost(TestLabSceneIndependentAutomationHost batchHost, string requiredHostId)
        {
            if (!string.IsNullOrWhiteSpace(requiredHostId)
                && !string.Equals(requiredHostId, batchHost.HostId, StringComparison.Ordinal)
                && !string.Equals(requiredHostId, BatchHostId, StringComparison.Ordinal))
            {
                return TestLabAutomationHostRegistry.ResolveActive(requiredHostId);
            }

            TestLabAutomationHostCapabilities capabilities = batchHost.GetCapabilities();
            return TestLabAutomationHostResolution.Success(batchHost, capabilities, TestLabAutomationHostRegistry.Revision);
        }

        private static string ResolveAutomationScenePath(TestLabAutomationCommandLineOptions options, TestLabAutomationRegistry registry)
        {
            if (!string.IsNullOrWhiteSpace(options.ScenePath))
            {
                return options.ScenePath;
            }

            return ShouldOpenDefaultScene(options, registry) ? DefaultAutomationScenePath : string.Empty;
        }

        private static bool ShouldOpenDefaultScene(TestLabAutomationCommandLineOptions options, TestLabAutomationRegistry registry)
        {
            if (options == null || registry == null || options.Action == TestLabAutomationCommandAction.List)
            {
                return false;
            }

            return SelectedScenarios(registry, options)
                .Any(scenario => scenario != null && scenario.CommandLineSupport == TestLabCommandLineSupport.RequiresScene);
        }

        private static IEnumerable<ITestLabAutomationScenario> SelectedScenarios(TestLabAutomationRegistry registry, TestLabAutomationCommandLineOptions options)
        {
            if (registry == null || options == null)
            {
                yield break;
            }

            switch (options.RunMode)
            {
                case TestLabAutomationRunMode.SelectedScenario:
                    if (registry.TryGetScenario(options.SuiteId, options.ScenarioId, out _, out ITestLabAutomationScenario selected))
                    {
                        yield return selected;
                    }
                    break;
                case TestLabAutomationRunMode.CurrentSuite:
                    if (registry.TryGetSuite(options.SuiteId, out ITestLabAutomationSuite suite))
                    {
                        foreach (ITestLabAutomationScenario scenario in suite.Scenarios)
                        {
                            yield return scenario;
                        }
                    }
                    break;
                case TestLabAutomationRunMode.AllSuites:
                    foreach (ITestLabAutomationScenario scenario in registry.Suites
                        .Where(suite => suite.IncludeInRunAll)
                        .SelectMany(suite => suite.Scenarios))
                    {
                        yield return scenario;
                    }
                    break;
                default:
                    foreach (ITestLabAutomationScenario scenario in registry.Suites
                        .Where(suite => suite.IncludeInRunAll)
                        .SelectMany(suite => suite.Scenarios)
                        .Where(scenario => scenario.IncludeInQuickRun || scenario.Category == TestLabAutomationCategory.Quick))
                    {
                        yield return scenario;
                    }
                    break;
            }
        }

        private static PrototypeTestLabService CreateSceneService()
        {
            PrototypeTestLabService service = new PrototypeTestLabService();
            service.Configure(PrototypeTestLabSceneContextResolver.Resolve());
            PrototypeTestLabServiceLocator.Register(service);
            return service;
        }

        private static void LogFailures(TestLabAutomationResult result)
        {
            if (result == null)
            {
                return;
            }

            foreach (TestLabScenarioResult scenario in result.Scenarios.Where(scenario => scenario.Status == TestLabAutomationStatus.Failed || scenario.Status == TestLabAutomationStatus.Error))
            {
                TestLabAutomationStepResult failedStep = scenario.Steps.FirstOrDefault(step => step.Status == TestLabAutomationStatus.Failed || step.Status == TestLabAutomationStatus.Error)
                    ?? scenario.Steps.FirstOrDefault(step => step.Status != TestLabAutomationStatus.Passed && step.Status != TestLabAutomationStatus.Skipped);
                string message = failedStep == null
                    ? $"Automation failed: {scenario.SuiteId}/{scenario.ScenarioId} - {scenario.DisplayName}. Status={scenario.Status}."
                    : $"Automation failed: {scenario.SuiteId}/{scenario.ScenarioId} - {scenario.DisplayName}. Status={scenario.Status}. Step={failedStep.StepId}. Expected='{failedStep.Expected}' Actual='{failedStep.Actual}'. Assertion={failedStep.AssertionType}. Tx='{failedStep.TransactionId}'. Diagnostics: {failedStep.Diagnostics}";
                Debug.LogWarning(message);
            }
        }

        private static string FormatResult(TestLabAutomationResult result)
        {
            return result == null
                ? "No result."
                : $"Run {result.RunId}: {result.PassedScenarios} passed, {result.FailedScenarios} failed, {result.ErrorScenarios} error, {result.SkippedScenarios} skipped, {result.CancelledScenarios} cancelled, {result.TotalSteps} steps. Mode={result.RunMode} Order={result.ScenarioOrder} Seed={result.ShuffleSeed}";
        }
    }

    public sealed class TestLabAutomationBatchCommandResult
    {
        private TestLabAutomationBatchCommandResult(int exitCode, string message, IReadOnlyList<string> reportPaths, TestLabAutomationResult automationResult)
        {
            ExitCode = exitCode;
            Message = message ?? string.Empty;
            ReportPaths = reportPaths ?? Array.Empty<string>();
            AutomationResult = automationResult;
        }

        public int ExitCode { get; }
        public string Message { get; }
        public IReadOnlyList<string> ReportPaths { get; }
        public TestLabAutomationResult AutomationResult { get; }

        public static TestLabAutomationBatchCommandResult Success(string message, IReadOnlyList<string> reportPaths, TestLabAutomationResult automationResult)
        {
            return new TestLabAutomationBatchCommandResult(0, message, reportPaths, automationResult);
        }

        public static TestLabAutomationBatchCommandResult Fail(int exitCode, string message, IReadOnlyList<string> reportPaths, TestLabAutomationResult automationResult)
        {
            return new TestLabAutomationBatchCommandResult(exitCode == 0 ? 1 : exitCode, message, reportPaths, automationResult);
        }
    }
}
#endif
