using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityIsekaiGame.Editor
{
    public static class LocalNetworkBuildAutomation
    {
        public const string DefaultClientPath = "../../Builds/LocalClient/UnityIsekaiClient.exe";

        [MenuItem("Tools/Unity Isekai Game/Networking/Build Windows Local Client")]
        public static void BuildWindowsLocalClient() => Build(DefaultClientPath, StandaloneBuildSubtarget.Player);

        // Entry points for Unity's -executeMethod command-line option.
        public static void BuildWindowsLocalClientCommandLine() => BuildWindowsLocalClient();

        private static void Build(string relativeOutputPath, StandaloneBuildSubtarget subtarget)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("At least one enabled scene is required in Build Settings.");
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath)
                ?? throw new InvalidOperationException("The Unity project root could not be resolved.");
            string outputPath = Path.GetFullPath(Path.Combine(projectRoot, relativeOutputPath));
            using var output = new BuildOutputTransaction(outputPath);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output.StagingExecutablePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)subtarget,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"{subtarget} build failed with {report.summary.totalErrors} error(s). See the Unity Editor log for details.");
            }

            output.ValidateManagedAssemblies(new[] { "UnityIsekaiGame.Networking.Server.dll" });
            output.Commit();
            Debug.Log($"Built {subtarget} player at '{outputPath}' ({report.summary.totalSize:N0} bytes). ");
        }
    }
}
