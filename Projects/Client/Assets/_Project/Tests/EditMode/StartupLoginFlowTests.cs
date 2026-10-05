using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
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
                "Assets/_Project/Presentation/Login/Resources/Login/backgroundimage-v2.png"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Presentation/Login/Resources/Login/isekai-reality-crest.png"), Is.Not.Null);
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
                Rect ultraWide = AccountLoginScreenController.CalculateCoverUv(landscape, new Vector2(2560f, 1080f));

                Assert.That(Mathf.Max(wide.width, wide.height), Is.EqualTo(1f).Within(0.001f));
                Assert.That(wide.width, Is.LessThanOrEqualTo(1f));
                Assert.That(wide.height, Is.LessThanOrEqualTo(1f));
                Assert.That(tall.height, Is.EqualTo(1f).Within(0.001f));
                Assert.That(tall.width, Is.LessThan(1f));
                Assert.That(ultraWide.x, Is.Zero.Within(0.001f));
                Assert.That(ultraWide.width, Is.EqualTo(1f).Within(0.001f),
                    "A wide login viewport must retain the background's complete horizontal span.");
                Assert.That(ultraWide.height, Is.LessThan(1f));
                float bottomCrop = ultraWide.y;
                float topCrop = 1f - ultraWide.yMax;
                Assert.That(topCrop, Is.EqualTo(bottomCrop).Within(0.001f),
                    "The login background must crop equal amounts from its top and bottom.");
            }
            finally
            {
                Object.DestroyImmediate(landscape);
            }
        }

        [Test]
        public void Password_field_only_raises_submit_for_an_explicit_submit_event()
        {
            var eventSystemObject = new GameObject("Login Submit Event System", typeof(EventSystem));
            var fieldObject = new GameObject("Password Field", typeof(RectTransform));
            try
            {
                ExplicitSubmitInputField field = fieldObject.AddComponent<ExplicitSubmitInputField>();
                int submitted = 0;
                field.Submitted += () => submitted++;
                var eventData = new BaseEventData(eventSystemObject.GetComponent<EventSystem>());

                field.OnDeselect(eventData);
                Assert.That(submitted, Is.Zero, "Moving focus to Create Account must not implicitly submit Login.");

                field.OnSubmit(eventData);
                Assert.That(submitted, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(fieldObject);
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void Login_form_routes_enter_from_both_fields_through_the_login_button()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string source = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Packages/com.thequantifier.isekai.client/Runtime/UI/Authentication/AccountLoginScreenController.cs"));

            StringAssert.Contains("usernameSubmit.Submitted += InvokeLoginButton", source);
            StringAssert.Contains("passwordSubmit.Submitted += InvokeLoginButton", source);
            StringAssert.Contains("loginButton.onClick.Invoke()", source);
            StringAssert.Contains("root.gameObject.AddComponent<ExplicitSubmitInputField>()", source);
        }

        [Test]
        public void Login_submit_reconnects_an_expired_app_connection_before_authenticating()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string screenSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Packages/com.thequantifier.isekai.client/Runtime/UI/Authentication/AccountLoginScreenController.cs"));
            string clientSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Packages/com.thequantifier.isekai.client/Runtime/Networking/LocalGameClient.cs"));

            StringAssert.Contains("submitAfterReconnect = true", screenSource);
            StringAssert.Contains("client.ReconnectApplication()", screenSource);
            StringAssert.Contains("if (submitAfterReconnect)", screenSource);
            StringAssert.Contains("public bool ReconnectApplication()", clientSource);
        }

        [Test]
        public void Login_settings_support_visible_dropdowns_and_a_game_quit_tab()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string screenSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Packages/com.thequantifier.isekai.client/Runtime/UI/Authentication/AccountLoginScreenController.cs"));
            string ornamentSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Packages/com.thequantifier.isekai.client/Runtime/UI/Authentication/LoginOrnamentGraphic.cs"));
            string toggleDropdownSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Packages/com.thequantifier.isekai.client/Runtime/UI/Authentication/ToggleDropdown.cs"));

            StringAssert.Contains("canvas.sortingOrder = 20000", screenSource);
            StringAssert.Contains("ControllerIcon", screenSource);
            StringAssert.Contains("QUIT GAME", screenSource);
            StringAssert.Contains("Application.Quit()", screenSource);
            StringAssert.Contains("DrawControllerIcon", ornamentSource);
            Assert.That(
                screenSource.IndexOf("gameTabButton = CreateSettingsTab", System.StringComparison.Ordinal),
                Is.LessThan(screenSource.IndexOf("graphicsTabButton = CreateSettingsTab", System.StringComparison.Ordinal)));
            StringAssert.Contains("ShowSettingsPage(false)", screenSource);
            StringAssert.Contains("Scrollbar Vertical", screenSource);
            StringAssert.Contains("ScrollRect.ScrollbarVisibility.Permanent", screenSource);
            StringAssert.Contains("GameUiThemeOptOut", screenSource);
            StringAssert.Contains("AddComponent<ToggleDropdown>()", screenSource);
            StringAssert.Contains("HasVisiblePopup()", toggleDropdownSource);
            StringAssert.Contains("CreateBlocker(Canvas rootCanvas)", toggleDropdownSource);
            StringAssert.Contains("RectangleContainsScreenPoint", toggleDropdownSource);
            StringAssert.Contains("Hide();", toggleDropdownSource);
            StringAssert.Contains("Show();", toggleDropdownSource);
        }
    }
}
