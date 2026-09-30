#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UnityIsekaiGame.Development.Automation
{
    public enum TestLabAutomationCommandAction
    {
        Run = 0,
        List = 1,
        Compatibility = 2
    }

    public sealed class TestLabAutomationCommandLineOptions
    {
        public TestLabAutomationCommandAction Action { get; private set; } = TestLabAutomationCommandAction.Run;
        public TestLabAutomationRunMode RunMode { get; private set; } = TestLabAutomationRunMode.AllQuickSuites;
        public string SuiteId { get; private set; } = string.Empty;
        public string ScenarioId { get; private set; } = string.Empty;
        public int StepFilter { get; private set; }
        public string ScenePath { get; private set; } = string.Empty;
        public string OutputDirectory { get; private set; } = "Logs/TestLabAutomation";
        public string OutputPath { get; private set; } = string.Empty;
        public TestLabAutomationReportFormat ReportFormat { get; private set; } = TestLabAutomationReportFormat.Both;
        public TestLabAutomationScenarioOrder ScenarioOrder { get; private set; } = TestLabAutomationScenarioOrder.Normal;
        public int ShuffleSeed { get; private set; } = 8675309;
        public bool StopOnFirstFailure { get; private set; }
        public bool ExitUnity { get; private set; }
        public bool HelpRequested { get; private set; }
        public bool Valid { get; private set; } = true;
        public string Error { get; private set; } = string.Empty;

        public TestLabAutomationOptions ToAutomationOptions()
        {
            return new TestLabAutomationOptions
            {
                StopOnFirstFailure = StopOnFirstFailure,
                IncludeExtended = true,
                MaximumFrameWait = 120,
                ScenarioOrder = ScenarioOrder,
                ShuffleSeed = ShuffleSeed,
                RunSurface = TestLabAutomationRunSurface.CommandLine,
                CommandLineSceneAvailable = !string.IsNullOrWhiteSpace(ScenePath)
            };
        }

        public static TestLabAutomationCommandLineOptions Parse(string[] args)
        {
            TestLabAutomationCommandLineOptions options = new TestLabAutomationCommandLineOptions();
            args ??= Array.Empty<string>();

            for (int i = 0; i < args.Length; i++)
            {
                string name = args[i] ?? string.Empty;
                if (!name.StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                switch (name.ToLowerInvariant())
                {
                    case "-testlabhelp":
                    case "-testlab-help":
                        options.HelpRequested = true;
                        break;
                    case "-testlabmode":
                        if (!TryReadValue(args, ref i, name, options, out string mode) || !options.TrySetMode(mode))
                        {
                            return options;
                        }
                        break;
                    case "-testlabstep":
                        if (!TryReadValue(args, ref i, name, options, out string step) || !int.TryParse(step, out int parsedStep) || parsedStep <= 0)
                        {
                            return options.Fail($"Argument '{name}' must be a positive integer Step number.");
                        }

                        options.StepFilter = parsedStep;
                        break;
                    case "-testlabsuite":
                        if (!TryReadValue(args, ref i, name, options, out string suiteId))
                        {
                            return options;
                        }

                        options.SuiteId = suiteId;
                        break;
                    case "-testlabscenario":
                        if (!TryReadValue(args, ref i, name, options, out string scenarioId))
                        {
                            return options;
                        }

                        options.ScenarioId = scenarioId;
                        break;
                    case "-testlabscene":
                        if (!TryReadValue(args, ref i, name, options, out string scenePath))
                        {
                            return options;
                        }

                        options.ScenePath = scenePath;
                        break;
                    case "-testlaboutputdir":
                        if (!TryReadValue(args, ref i, name, options, out string outputDirectory))
                        {
                            return options;
                        }

                        options.OutputDirectory = outputDirectory;
                        break;
                    case "-testlaboutput":
                    case "-testlaboutputpath":
                        if (!TryReadValue(args, ref i, name, options, out string outputPath))
                        {
                            return options;
                        }

                        options.OutputPath = outputPath;
                        break;
                    case "-testlabformat":
                        if (!TryReadValue(args, ref i, name, options, out string format) || !options.TrySetFormat(format))
                        {
                            return options;
                        }
                        break;
                    case "-testlaborder":
                        if (!TryReadValue(args, ref i, name, options, out string order) || !options.TrySetOrder(order))
                        {
                            return options;
                        }
                        break;
                    case "-testlabseed":
                        if (!TryReadValue(args, ref i, name, options, out string seed) || !int.TryParse(seed, out int parsedSeed))
                        {
                            return options.Fail($"Argument '{name}' must be an integer.");
                        }

                        options.ShuffleSeed = parsedSeed;
                        break;
                    case "-testlabstoponfail":
                    case "-testlabstoponfirstfailure":
                        if (!TryReadValue(args, ref i, name, options, out string stopOnFail) || !TryParseBool(stopOnFail, out bool parsedStopOnFail))
                        {
                            return options.Fail($"Argument '{name}' must be true or false.");
                        }

                        options.StopOnFirstFailure = parsedStopOnFail;
                        break;
                    case "-testlabexit":
                        if (!TryReadValue(args, ref i, name, options, out string exit) || !TryParseBool(exit, out bool parsedExit))
                        {
                            return options.Fail($"Argument '{name}' must be true or false.");
                        }

                        options.ExitUnity = parsedExit;
                        break;
                }
            }

            return options.ValidateSelection();
        }

        public static string Usage()
        {
            return "Unity Test Lab automation: -executeMethod UnityIsekaiGame.Editor.Tools.TestLabAutomation.TestLabAutomationBatchCommand.Run "
                + "-testLabMode quick|all|suite|scenario|list|compatibility -testLabStep <step> -testLabSuite <suiteId> -testLabScenario <scenarioId> "
                + "-testLabOutputDir Logs/TestLabAutomation -testLabOutput <path> -testLabFormat json|markdown|junit|both|all "
                + "-testLabOrder normal|reverse|shuffled -testLabSeed <int> -testLabStopOnFail true|false.";
        }

        private TestLabAutomationCommandLineOptions ValidateSelection()
        {
            if (HelpRequested)
            {
                return this;
            }

            if (RunMode == TestLabAutomationRunMode.CurrentSuite && string.IsNullOrWhiteSpace(SuiteId))
            {
                return Fail("-testLabMode suite requires -testLabSuite.");
            }

            if (RunMode == TestLabAutomationRunMode.SelectedScenario)
            {
                if (string.IsNullOrWhiteSpace(SuiteId))
                {
                    return Fail("-testLabMode scenario requires -testLabSuite.");
                }

                if (string.IsNullOrWhiteSpace(ScenarioId))
                {
                    return Fail("-testLabMode scenario requires -testLabScenario.");
                }
            }

            return this;
        }

        private bool TrySetMode(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "quick":
                case "allquick":
                case "all-quick":
                    RunMode = TestLabAutomationRunMode.AllQuickSuites;
                    return true;
                case "all":
                    RunMode = TestLabAutomationRunMode.AllSuites;
                    return true;
                case "list":
                case "catalog":
                    Action = TestLabAutomationCommandAction.List;
                    return true;
                case "compat":
                case "compatibility":
                    Action = TestLabAutomationCommandAction.Compatibility;
                    return true;
                case "suite":
                    RunMode = TestLabAutomationRunMode.CurrentSuite;
                    return true;
                case "scenario":
                case "selectedscenario":
                case "selected-scenario":
                    RunMode = TestLabAutomationRunMode.SelectedScenario;
                    return true;
                default:
                    Fail($"Unsupported -testLabMode '{value}'.");
                    return false;
            }
        }

        private bool TrySetFormat(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "json":
                    ReportFormat = TestLabAutomationReportFormat.Json;
                    return true;
                case "md":
                case "markdown":
                    ReportFormat = TestLabAutomationReportFormat.Markdown;
                    return true;
                case "junit":
                case "xml":
                    ReportFormat = TestLabAutomationReportFormat.JUnit;
                    return true;
                case "both":
                    ReportFormat = TestLabAutomationReportFormat.Both;
                    return true;
                case "all":
                    ReportFormat = TestLabAutomationReportFormat.All;
                    return true;
                default:
                    Fail($"Unsupported -testLabFormat '{value}'.");
                    return false;
            }
        }

        private bool TrySetOrder(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "normal":
                    ScenarioOrder = TestLabAutomationScenarioOrder.Normal;
                    return true;
                case "reverse":
                    ScenarioOrder = TestLabAutomationScenarioOrder.Reverse;
                    return true;
                case "shuffle":
                case "shuffled":
                    ScenarioOrder = TestLabAutomationScenarioOrder.Shuffled;
                    return true;
                default:
                    Fail($"Unsupported -testLabOrder '{value}'.");
                    return false;
            }
        }

        private TestLabAutomationCommandLineOptions Fail(string error)
        {
            Valid = false;
            Error = error ?? "Invalid Test Lab automation command line.";
            return this;
        }

        private static bool TryReadValue(string[] args, ref int index, string name, TestLabAutomationCommandLineOptions options, out string value)
        {
            value = string.Empty;
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                options.Fail($"Argument '{name}' requires a value.");
                return false;
            }

            index++;
            value = args[index];
            return true;
        }

        private static bool TryParseBool(string value, out bool result)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "true":
                case "1":
                case "yes":
                case "y":
                    result = true;
                    return true;
                case "false":
                case "0":
                case "no":
                case "n":
                    result = false;
                    return true;
                default:
                    result = false;
                    return false;
            }
        }
    }
}
#endif
