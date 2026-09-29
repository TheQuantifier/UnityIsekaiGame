using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityIsekaiGame.ServerProject.Editor
{
    public static class ServerProjectBuildAutomation
    {
        public const string ServerScenePath = "Assets/Scenes/ServerPrototypeScene.unity";
        public const string DefaultOutputPath = "../../../Builds/LocalServer/UnityIsekaiServer.exe";

        [MenuItem("Tools/Unity Isekai Game/Build Windows Dedicated Server")]
        public static void BuildWindowsDedicatedServer() => Build(DefaultOutputPath);

        public static void BuildWindowsDedicatedServerCommandLine() => BuildWindowsDedicatedServer();

        private static void Build(string relativeOutputPath)
        {
            if (!File.Exists(ServerScenePath))
            {
                throw new BuildFailedException($"The authoritative server scene is missing at '{ServerScenePath}'.");
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath)
                ?? throw new InvalidOperationException("The server project root could not be resolved.");
            string outputPath = Path.GetFullPath(Path.Combine(projectRoot, relativeOutputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)
                ?? throw new InvalidOperationException("The server build output directory is invalid."));

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ServerScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Dedicated-server build failed with {report.summary.totalErrors} error(s). See the Editor log.");
            }

            Debug.Log($"Built dedicated server at '{outputPath}' ({report.summary.totalSize:N0} bytes).");
        }
    }
}
