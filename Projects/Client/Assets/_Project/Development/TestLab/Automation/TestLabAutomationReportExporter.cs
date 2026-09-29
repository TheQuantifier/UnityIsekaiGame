#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace UnityIsekaiGame.Development.Automation
{
    [Flags]
    public enum TestLabAutomationReportFormat
    {
        Json = 1,
        Markdown = 2,
        JUnit = 4,
        Both = Json | Markdown,
        All = Json | Markdown | JUnit
    }

    public sealed class TestLabAutomationReportExporter
    {
        private const string ReportDirectory = "Temp/TestLabAutomation";

        public string ExportJson(TestLabAutomationResult result)
        {
            string path = BuildReportPath(result, "json", ReportDirectory, string.Empty);
            File.WriteAllText(path, BuildJson(result), Encoding.UTF8);
            return path;
        }

        public string ExportMarkdown(TestLabAutomationResult result)
        {
            string path = BuildReportPath(result, "md", ReportDirectory, string.Empty);
            File.WriteAllText(path, BuildMarkdown(result), Encoding.UTF8);
            return path;
        }

        public IReadOnlyList<string> Export(TestLabAutomationResult result, TestLabAutomationReportFormat format, string outputDirectory = "", string outputPath = "")
        {
            if (format == 0)
            {
                format = TestLabAutomationReportFormat.Both;
            }

            string directory = string.IsNullOrWhiteSpace(outputDirectory) ? ReportDirectory : outputDirectory;
            List<string> paths = new List<string>();
            if ((format & TestLabAutomationReportFormat.Json) != 0)
            {
                string path = BuildReportPath(result, "json", directory, outputPath);
                File.WriteAllText(path, BuildJson(result), Encoding.UTF8);
                paths.Add(path);
            }

            if ((format & TestLabAutomationReportFormat.Markdown) != 0)
            {
                string path = BuildReportPath(result, "md", directory, outputPath);
                File.WriteAllText(path, BuildMarkdown(result), Encoding.UTF8);
                paths.Add(path);
            }

            if ((format & TestLabAutomationReportFormat.JUnit) != 0)
            {
                string path = BuildReportPath(result, "xml", directory, outputPath);
                File.WriteAllText(path, BuildJUnitXml(result), Encoding.UTF8);
                paths.Add(path);
            }

            return paths.AsReadOnly();
        }

        public IReadOnlyList<string> ExportCatalog(TestLabAutomationRegistry registry, TestLabAutomationReportFormat format, string outputDirectory = "", string outputPath = "")
        {
            if (format == 0)
            {
                format = TestLabAutomationReportFormat.Both;
            }

            string directory = string.IsNullOrWhiteSpace(outputDirectory) ? ReportDirectory : outputDirectory;
            List<string> paths = new List<string>();
            if ((format & TestLabAutomationReportFormat.Json) != 0)
            {
                string path = BuildReportPath(null, "json", directory, outputPath);
                File.WriteAllText(path, BuildCatalogJson(registry), Encoding.UTF8);
                paths.Add(path);
            }

            if ((format & TestLabAutomationReportFormat.Markdown) != 0)
            {
                string path = BuildReportPath(null, "md", directory, outputPath);
                File.WriteAllText(path, BuildCatalogMarkdown(registry), Encoding.UTF8);
                paths.Add(path);
            }

            return paths.AsReadOnly();
        }

        public IReadOnlyList<string> ExportCompatibility(TestLabSuiteCompatibilityReport report, TestLabAutomationReportFormat format, string outputDirectory = "", string outputPath = "")
        {
            if (format == 0)
            {
                format = TestLabAutomationReportFormat.Both;
            }

            string directory = string.IsNullOrWhiteSpace(outputDirectory) ? ReportDirectory : outputDirectory;
            List<string> paths = new List<string>();
            if ((format & TestLabAutomationReportFormat.Json) != 0)
            {
                string path = BuildReportPath(null, "json", directory, outputPath);
                File.WriteAllText(path, BuildCompatibilityJson(report), Encoding.UTF8);
                paths.Add(path);
            }

            if ((format & TestLabAutomationReportFormat.Markdown) != 0)
            {
                string path = BuildReportPath(null, "md", directory, outputPath);
                File.WriteAllText(path, BuildCompatibilityMarkdown(report), Encoding.UTF8);
                paths.Add(path);
            }

            return paths.AsReadOnly();
        }

        public string BuildJson(TestLabAutomationResult result)
        {
            if (result == null)
            {
                return "{}";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            AppendJsonProperty(builder, "projectName", Application.productName, 1, comma: true);
            AppendJsonProperty(builder, "unityVersion", Application.unityVersion, 1, comma: true);
            AppendJsonProperty(builder, "platform", Application.platform.ToString(), 1, comma: true);
            AppendProviderJson(builder, 1, comma: true);
            AppendJsonProperty(builder, "runId", result.RunId, 1, comma: true);
            AppendJsonProperty(builder, "runMode", result.RunMode.ToString(), 1, comma: true);
            AppendJsonProperty(builder, "scenarioOrder", result.ScenarioOrder.ToString(), 1, comma: true);
            AppendJsonProperty(builder, "shuffleSeed", result.ShuffleSeed.ToString(), 1, comma: true, quoteValue: false);
            AppendJsonProperty(builder, "startedAtUtc", result.StartedAtUtc.ToString("O"), 1, comma: true);
            AppendJsonProperty(builder, "endedAtUtc", result.EndedAtUtc.ToString("O"), 1, comma: true);
            AppendJsonProperty(builder, "elapsedMilliseconds", ((long)result.Elapsed.TotalMilliseconds).ToString(), 1, comma: true, quoteValue: false);
            AppendJsonProperty(builder, "cancelled", result.Cancelled ? "true" : "false", 1, comma: true, quoteValue: false);
            builder.AppendLine("  \"totals\": {");
            AppendJsonProperty(builder, "scenarios", result.TotalScenarios.ToString(), 2, comma: true, quoteValue: false);
            AppendJsonProperty(builder, "passed", result.PassedScenarios.ToString(), 2, comma: true, quoteValue: false);
            AppendJsonProperty(builder, "failed", result.FailedScenarios.ToString(), 2, comma: true, quoteValue: false);
            AppendJsonProperty(builder, "errors", result.ErrorScenarios.ToString(), 2, comma: true, quoteValue: false);
            AppendJsonProperty(builder, "skipped", result.SkippedScenarios.ToString(), 2, comma: true, quoteValue: false);
            AppendJsonProperty(builder, "cancelled", result.CancelledScenarios.ToString(), 2, comma: true, quoteValue: false);
            AppendJsonProperty(builder, "steps", result.TotalSteps.ToString(), 2, comma: false, quoteValue: false);
            builder.AppendLine("  },");
            builder.AppendLine("  \"scenarios\": [");
            for (int i = 0; i < result.Scenarios.Count; i++)
            {
                TestLabScenarioResult scenario = result.Scenarios[i];
                builder.AppendLine("    {");
                AppendJsonProperty(builder, "suiteId", scenario.SuiteId, 3, comma: true);
                AppendJsonProperty(builder, "scenarioId", scenario.ScenarioId, 3, comma: true);
                AppendJsonProperty(builder, "displayName", scenario.DisplayName, 3, comma: true);
                AppendJsonProperty(builder, "status", scenario.Status.ToString(), 3, comma: true);
                AppendJsonProperty(builder, "startedAtUtc", scenario.StartedAtUtc.ToString("O"), 3, comma: true);
                AppendJsonProperty(builder, "endedAtUtc", scenario.EndedAtUtc.ToString("O"), 3, comma: true);
                AppendJsonProperty(builder, "elapsedMilliseconds", ((long)scenario.Elapsed.TotalMilliseconds).ToString(), 3, comma: true, quoteValue: false);
                builder.AppendLine("      \"steps\": [");
                for (int stepIndex = 0; stepIndex < scenario.Steps.Count; stepIndex++)
                {
                    TestLabAutomationStepResult step = scenario.Steps[stepIndex];
                    builder.AppendLine("        {");
                    AppendJsonProperty(builder, "stepId", step.StepId, 5, comma: true);
                    AppendJsonProperty(builder, "displayName", step.DisplayName, 5, comma: true);
                    AppendJsonProperty(builder, "status", step.Status.ToString(), 5, comma: true);
                    AppendJsonProperty(builder, "assertionType", step.AssertionType, 5, comma: true);
                    AppendJsonProperty(builder, "expected", step.Expected, 5, comma: true);
                    AppendJsonProperty(builder, "actual", step.Actual, 5, comma: true);
                    AppendJsonProperty(builder, "actorId", step.ActorId, 5, comma: true);
                    AppendJsonProperty(builder, "transactionId", step.TransactionId, 5, comma: true);
                    AppendJsonProperty(builder, "diagnostics", step.Diagnostics, 5, comma: true);
                    AppendJsonProperty(builder, "exceptionType", step.ExceptionType, 5, comma: true);
                    AppendJsonProperty(builder, "exceptionMessage", step.ExceptionMessage, 5, comma: false);
                    builder.Append(stepIndex == scenario.Steps.Count - 1 ? "        }" : "        },");
                    builder.AppendLine();
                }

                builder.AppendLine("      ]");
                builder.Append(i == result.Scenarios.Count - 1 ? "    }" : "    },");
                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        public string BuildJUnitXml(TestLabAutomationResult result)
        {
            if (result == null)
            {
                return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<testsuite name=\"TestLabAutomation\" tests=\"0\" failures=\"0\" errors=\"0\" skipped=\"0\" />\n";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            builder.Append($"<testsuite name=\"TestLabAutomation\" tests=\"{result.TotalScenarios}\" failures=\"{result.FailedScenarios}\" errors=\"{result.ErrorScenarios}\" skipped=\"{result.SkippedScenarios + result.CancelledScenarios}\" time=\"{result.Elapsed.TotalSeconds:0.###}\">").AppendLine();
            foreach (TestLabScenarioResult scenario in result.Scenarios)
            {
                builder.Append($"  <testcase classname=\"{XmlEscape(scenario.SuiteId)}\" name=\"{XmlEscape(scenario.ScenarioId)}\" time=\"{scenario.Elapsed.TotalSeconds:0.###}\">").AppendLine();
                TestLabAutomationStepResult failedStep = scenario.Steps.FirstOrDefault(step => step.Status == TestLabAutomationStatus.Failed || step.Status == TestLabAutomationStatus.Error)
                    ?? scenario.Steps.FirstOrDefault(step => step.Status == TestLabAutomationStatus.Cancelled || step.Status == TestLabAutomationStatus.Skipped);
                if (scenario.Status == TestLabAutomationStatus.Failed)
                {
                    builder.Append($"    <failure type=\"{XmlEscape(failedStep?.AssertionType ?? "Failed")}\" message=\"{XmlEscape(failedStep?.Actual ?? scenario.Status.ToString())}\">{XmlEscape(failedStep?.Diagnostics ?? scenario.DisplayName)}</failure>").AppendLine();
                }
                else if (scenario.Status == TestLabAutomationStatus.Error)
                {
                    builder.Append($"    <error type=\"{XmlEscape(failedStep?.ExceptionType ?? "Error")}\" message=\"{XmlEscape(failedStep?.ExceptionMessage ?? failedStep?.Actual ?? "Error")}\">{XmlEscape(failedStep?.Diagnostics ?? scenario.DisplayName)}</error>").AppendLine();
                }
                else if (scenario.Status == TestLabAutomationStatus.Skipped || scenario.Status == TestLabAutomationStatus.Cancelled)
                {
                    builder.Append($"    <skipped message=\"{XmlEscape(scenario.Status.ToString())}\" />").AppendLine();
                }

                builder.AppendLine("  </testcase>");
            }

            builder.AppendLine("</testsuite>");
            return builder.ToString();
        }

        public string BuildCatalogJson(TestLabAutomationRegistry registry)
        {
            registry ??= PrototypeTestLabAutomationCatalog.CreateDefaultRegistry();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            AppendProviderJson(builder, 1, comma: true);
            builder.AppendLine("  \"suites\": [");
            IReadOnlyList<ITestLabAutomationSuite> suites = registry.Suites;
            Dictionary<string, PrototypeTestLabAutomationSuiteDescriptor> suiteOwners = PrototypeTestLabAutomationCatalog.DescribeSuites()
                .GroupBy(suite => suite.SuiteId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            for (int i = 0; i < suites.Count; i++)
            {
                ITestLabAutomationSuite suite = suites[i];
                suiteOwners.TryGetValue(suite.SuiteId, out PrototypeTestLabAutomationSuiteDescriptor owner);
                builder.AppendLine("    {");
                AppendJsonProperty(builder, "suiteId", suite.SuiteId, 3, comma: true);
                AppendJsonProperty(builder, "displayName", suite.DisplayName, 3, comma: true);
                AppendJsonProperty(builder, "feature", suite.Feature, 3, comma: true);
                AppendJsonProperty(builder, "providerStep", (owner?.Step ?? 0).ToString(), 3, comma: true, quoteValue: false);
                AppendJsonProperty(builder, "providerLabel", owner?.Label ?? string.Empty, 3, comma: true);
                AppendJsonProperty(builder, "providerName", owner?.ProviderName ?? string.Empty, 3, comma: true);
                AppendJsonProperty(builder, "order", suite.Order.ToString(), 3, comma: true, quoteValue: false);
                AppendJsonProperty(builder, "includeInRunAll", suite.IncludeInRunAll ? "true" : "false", 3, comma: true, quoteValue: false);
                builder.AppendLine("      \"scenarios\": [");
                for (int scenarioIndex = 0; scenarioIndex < suite.Scenarios.Count; scenarioIndex++)
                {
                    ITestLabAutomationScenario scenario = suite.Scenarios[scenarioIndex];
                    builder.AppendLine("        {");
                    AppendJsonProperty(builder, "scenarioId", scenario.ScenarioId, 5, comma: true);
                    AppendJsonProperty(builder, "displayName", scenario.DisplayName, 5, comma: true);
                    AppendJsonProperty(builder, "category", scenario.Category.ToString(), 5, comma: true);
                    AppendJsonProperty(builder, "includeInQuickRun", scenario.IncludeInQuickRun ? "true" : "false", 5, comma: true, quoteValue: false);
                    AppendJsonProperty(builder, "requiresSceneHost", scenario.RequiresSceneHost ? "true" : "false", 5, comma: true, quoteValue: false);
                    AppendJsonProperty(builder, "commandLineSupport", scenario.CommandLineSupport.ToString(), 5, comma: true);
                    AppendJsonProperty(builder, "commandLineUnsupportedReason", scenario.CommandLineUnsupportedReason, 5, comma: true);
                    AppendJsonProperty(builder, "requiredRuntimeAreas", scenario.RequiredRuntimeAreas.ToString(), 5, comma: false);
                    builder.Append(scenarioIndex == suite.Scenarios.Count - 1 ? "        }" : "        },").AppendLine();
                }

                builder.AppendLine("      ]");
                builder.Append(i == suites.Count - 1 ? "    }" : "    },").AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        public string BuildCatalogMarkdown(TestLabAutomationRegistry registry)
        {
            registry ??= PrototypeTestLabAutomationCatalog.CreateDefaultRegistry();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Test Lab Automation Catalog");
            builder.AppendLine();
            foreach (PrototypeTestLabAutomationProviderDescriptor provider in PrototypeTestLabAutomationCatalog.Providers)
            {
                builder.AppendLine($"- Step {provider.Step}: {provider.Label} (`{provider.Name}`)");
            }

            builder.AppendLine();
            foreach (ITestLabAutomationSuite suite in registry.Suites)
            {
                builder.AppendLine($"## {suite.SuiteId}");
                builder.AppendLine($"{suite.DisplayName} - {suite.Scenarios.Count} scenario(s)");
                foreach (ITestLabAutomationScenario scenario in suite.Scenarios)
                {
                    builder.AppendLine($"- `{scenario.ScenarioId}` {scenario.DisplayName} [{scenario.Category}] Host={scenario.RequiresSceneHost} CLI={scenario.CommandLineSupport} Areas={scenario.RequiredRuntimeAreas}");
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        public string BuildCompatibilityJson(TestLabSuiteCompatibilityReport report)
        {
            report ??= new TestLabSuiteCompatibilityReport(Array.Empty<TestLabScenarioCompatibilityResult>());
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            AppendJsonProperty(builder, "compatible", report.Compatible ? "true" : "false", 1, comma: true, quoteValue: false);
            AppendJsonProperty(builder, "compatibleCount", report.CompatibleCount.ToString(), 1, comma: true, quoteValue: false);
            AppendJsonProperty(builder, "sceneIndependentCount", report.SceneIndependentCount.ToString(), 1, comma: true, quoteValue: false);
            AppendJsonProperty(builder, "unsupportedCount", report.UnsupportedCount.ToString(), 1, comma: true, quoteValue: false);
            builder.AppendLine("  \"scenarios\": [");
            for (int i = 0; i < report.Scenarios.Count; i++)
            {
                TestLabScenarioCompatibilityResult scenario = report.Scenarios[i];
                builder.AppendLine("    {");
                AppendJsonProperty(builder, "suiteId", scenario.SuiteId, 3, comma: true);
                AppendJsonProperty(builder, "scenarioId", scenario.ScenarioId, 3, comma: true);
                AppendJsonProperty(builder, "displayName", scenario.DisplayName, 3, comma: true);
                AppendJsonProperty(builder, "compatible", scenario.Compatible ? "true" : "false", 3, comma: true, quoteValue: false);
                AppendJsonProperty(builder, "sceneIndependent", scenario.SceneIndependent ? "true" : "false", 3, comma: true, quoteValue: false);
                AppendJsonProperty(builder, "hostId", scenario.HostId, 3, comma: true);
                AppendJsonProperty(builder, "failureCode", scenario.FailureCode, 3, comma: true);
                AppendJsonProperty(builder, "diagnostics", scenario.Diagnostics, 3, comma: false);
                builder.Append(i == report.Scenarios.Count - 1 ? "    }" : "    },").AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        public string BuildCompatibilityMarkdown(TestLabSuiteCompatibilityReport report)
        {
            report ??= new TestLabSuiteCompatibilityReport(Array.Empty<TestLabScenarioCompatibilityResult>());
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Test Lab Automation Compatibility");
            builder.AppendLine();
            builder.AppendLine(report.ToDiagnostic());
            builder.AppendLine();
            foreach (TestLabScenarioCompatibilityResult scenario in report.Scenarios)
            {
                builder.AppendLine($"- {(scenario.Compatible ? "Compatible" : "Unsupported")}: `{scenario.SuiteId}/{scenario.ScenarioId}` Host=`{scenario.HostId}` Code=`{scenario.FailureCode}` {scenario.Diagnostics}");
            }

            return builder.ToString();
        }

        public string BuildMarkdown(TestLabAutomationResult result)
        {
            if (result == null)
            {
                return "# Test Lab Automation\n\nNo result available.\n";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Test Lab Automation Report");
            builder.AppendLine();
            builder.AppendLine($"Run: `{result.RunId}`");
            builder.AppendLine($"Mode: `{result.RunMode}`");
            builder.AppendLine($"Scenario Order: `{result.ScenarioOrder}`");
            builder.AppendLine($"Shuffle Seed: `{result.ShuffleSeed}`");
            builder.AppendLine($"Started UTC: `{result.StartedAtUtc:O}`");
            builder.AppendLine($"Ended UTC: `{result.EndedAtUtc:O}`");
            builder.AppendLine($"Elapsed: `{result.Elapsed}`");
            builder.AppendLine($"Totals: {result.PassedScenarios} passed, {result.FailedScenarios} failed, {result.ErrorScenarios} error, {result.SkippedScenarios} skipped, {result.CancelledScenarios} cancelled, {result.TotalSteps} steps.");
            builder.AppendLine();

            foreach (TestLabScenarioResult scenario in result.Scenarios)
            {
                builder.AppendLine($"## {scenario.SuiteId} / {scenario.ScenarioId} - {scenario.Status}");
                foreach (TestLabAutomationStepResult step in scenario.Steps)
                {
                    builder.AppendLine($"- `{step.Status}` {step.StepId}: {step.Diagnostics}");
                    if (step.Status == TestLabAutomationStatus.Failed || step.Status == TestLabAutomationStatus.Error)
                    {
                        builder.AppendLine($"  Expected: `{step.Expected}` Actual: `{step.Actual}` Assertion: `{step.AssertionType}`");
                    }
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static void EnsureReportDirectory()
        {
            if (!Directory.Exists(ReportDirectory))
            {
                Directory.CreateDirectory(ReportDirectory);
            }
        }

        private static void EnsureDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string BuildReportFileName(TestLabAutomationResult result, string extension)
        {
            string runId = result == null ? "no-run" : result.RunId;
            return $"test-lab-automation-{runId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{extension}";
        }

        private static string BuildReportPath(TestLabAutomationResult result, string extension, string outputDirectory, string outputPath)
        {
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                string directory = Path.GetDirectoryName(outputPath);
                string fileName = Path.GetFileName(outputPath);
                string pathExtension = Path.GetExtension(outputPath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    directory = string.IsNullOrWhiteSpace(outputDirectory) ? ReportDirectory : outputDirectory;
                }

                EnsureDirectory(directory);
                if (string.IsNullOrWhiteSpace(pathExtension))
                {
                    return Path.Combine(directory, $"{fileName}.{extension}");
                }

                if (string.Equals(pathExtension.TrimStart('.'), extension, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.Combine(directory, fileName);
                }

                return Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fileName)}.{extension}");
            }

            string directoryOnly = string.IsNullOrWhiteSpace(outputDirectory) ? ReportDirectory : outputDirectory;
            EnsureDirectory(directoryOnly);
            return Path.Combine(directoryOnly, BuildReportFileName(result, extension));
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value, int indent, bool comma, bool quoteValue = true)
        {
            builder.Append(' ', indent * 2);
            builder.Append('"').Append(Escape(name)).Append("\": ");
            if (quoteValue)
            {
                builder.Append('"').Append(Escape(value)).Append('"');
            }
            else
            {
                builder.Append(value);
            }

            if (comma)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        private static void AppendProviderJson(StringBuilder builder, int indent, bool comma)
        {
            builder.Append(' ', indent * 2);
            builder.AppendLine("\"providers\": [");
            IReadOnlyList<PrototypeTestLabAutomationProviderDescriptor> providers = PrototypeTestLabAutomationCatalog.Providers;
            for (int i = 0; i < providers.Count; i++)
            {
                PrototypeTestLabAutomationProviderDescriptor provider = providers[i];
                builder.Append(' ', (indent + 1) * 2);
                builder.Append("{ ");
                builder.Append("\"step\": ").Append(provider.Step).Append(", ");
                builder.Append("\"label\": \"").Append(Escape(provider.Label)).Append("\", ");
                builder.Append("\"order\": ").Append(provider.Order).Append(", ");
                builder.Append("\"provider\": \"").Append(Escape(provider.Name)).Append("\"");
                builder.Append(i == providers.Count - 1 ? " }" : " },");
                builder.AppendLine();
            }

            builder.Append(' ', indent * 2);
            builder.Append("]");
            if (comma)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string XmlEscape(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
#endif
