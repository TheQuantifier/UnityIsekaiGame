using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.UI.Startup;

namespace UnityIsekaiGame.Editor
{
    public static class StartupSceneAuthoring
    {
        public const string StartupScenePath = "Assets/_Project/Scenes/Production/Startup/StartupScene.unity";
        public const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        private const string LegacyStartupSceneFolder = "Assets/_Project/Scenes/Startup";

        [MenuItem("Tools/Unity Isekai Game/UI/Author Startup Scene")]
        public static void AuthorStartupScene()
        {
            string directory = Path.GetDirectoryName(StartupScenePath)
                ?? throw new InvalidOperationException("The startup scene directory could not be resolved.");
            Directory.CreateDirectory(directory);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "StartupScene";

            GameObject cameraObject = new GameObject("Startup Camera", typeof(Camera), typeof(AudioListener));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;

            GameObject sequenceObject = new GameObject("Startup Sequence");
            sequenceObject.AddComponent<StartupSequenceController>();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, StartupScenePath))
            {
                throw new InvalidOperationException($"Could not save startup scene to '{StartupScenePath}'.");
            }

            if (AssetDatabase.IsValidFolder(LegacyStartupSceneFolder))
            {
                AssetDatabase.DeleteAsset(LegacyStartupSceneFolder);
            }

            var orderedScenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(StartupScenePath, true),
                new EditorBuildSettingsScene(PrototypeScenePath, true)
            };
            orderedScenes.AddRange(EditorBuildSettings.scenes.Where(existing =>
                !string.Equals(existing.path, StartupScenePath, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(existing.path, PrototypeScenePath, StringComparison.OrdinalIgnoreCase)
                && !existing.path.StartsWith(LegacyStartupSceneFolder + "/", StringComparison.OrdinalIgnoreCase)));
            EditorBuildSettings.scenes = orderedScenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Authored startup scene at '{StartupScenePath}' and placed it first in client Build Settings.");
        }

        public static void AuthorStartupSceneCommandLine()
        {
            AuthorStartupScene();
        }
    }
}
