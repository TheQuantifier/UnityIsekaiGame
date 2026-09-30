using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Editor;
using UnityIsekaiGame.UI.Authentication;

namespace UnityIsekaiGame.Tests
{
    public sealed class StartupLoginFlowTests
    {
        [Test]
        public void Startup_scene_is_first_and_login_media_is_client_only()
        {
            Assert.That(EditorBuildSettings.scenes, Is.Not.Empty);
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(StartupSceneAuthoring.StartupScenePath));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(StartupSceneAuthoring.StartupScenePath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Presentation/Login/Resources/Login/backgroundimage.jpg"), Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(Application.streamingAssetsPath, "Startup/startup_scene.mp4")), Is.True);
        }

        [Test]
        public void Background_cover_crop_preserves_aspect_ratio_without_letterboxing()
        {
            var landscape = new Texture2D(738, 411);
            try
            {
                Rect wide = AccountLoginScreenController.CalculateCoverUv(landscape, new Vector2(1920f, 1080f));
                Rect tall = AccountLoginScreenController.CalculateCoverUv(landscape, new Vector2(1080f, 1920f));

                Assert.That(Mathf.Max(wide.width, wide.height), Is.EqualTo(1f).Within(0.001f));
                Assert.That(wide.width, Is.LessThanOrEqualTo(1f));
                Assert.That(wide.height, Is.LessThanOrEqualTo(1f));
                Assert.That(tall.height, Is.EqualTo(1f).Within(0.001f));
                Assert.That(tall.width, Is.LessThan(1f));
            }
            finally
            {
                Object.DestroyImmediate(landscape);
            }
        }
    }
}
