using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace UnityIsekaiGame.Editor
{
    public static class BatchEditModeTestRunner
    {
        private const string DefaultResultsPath = "Logs/M11EditModeTestResults.xml";

        public static void RunEditModeTests()
        {
            string resultsPath = ResolveResultsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(resultsPath) ?? "Logs");

            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            BatchCallbacks callbacks = new BatchCallbacks();
            api.RegisterCallbacks(callbacks);

            try
            {
                Filter filter = new Filter
                {
                    testMode = TestMode.EditMode,
                    assemblyNames = new[] { "UnityIsekaiGame.EditModeTests" }
                };

                string testFilter = ResolveArgument("-testFilter");
                if (!string.IsNullOrWhiteSpace(testFilter))
                {
                    filter.groupNames = new[] { testFilter };
                }

                ExecutionSettings settings = new ExecutionSettings(filter)
                {
                    runSynchronously = true
                };

                api.Execute(settings);

                if (callbacks.Result == null)
                {
                    throw new InvalidOperationException("EditMode test run completed without a result.");
                }

                TestRunnerApi.SaveResultToFile(callbacks.Result, resultsPath);
                Debug.Log($"EditMode test run finished. Passed={callbacks.Result.PassCount}, Failed={callbacks.Result.FailCount}, Skipped={callbacks.Result.SkipCount}, Inconclusive={callbacks.Result.InconclusiveCount}. Results: {resultsPath}");

                if (callbacks.Result.FailCount > 0)
                {
                    EditorApplication.Exit(1);
                }
                else if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            finally
            {
                api.UnregisterCallbacks(callbacks);
                UnityEngine.Object.DestroyImmediate(api);
            }
        }

        private static string ResolveResultsPath()
        {
            return ResolveArgument("-testResults") ?? DefaultResultsPath;
        }

        private static string ResolveArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private sealed class BatchCallbacks : ICallbacks
        {
            public ITestResultAdaptor Result { get; private set; }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Result = result;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }
    }
}
