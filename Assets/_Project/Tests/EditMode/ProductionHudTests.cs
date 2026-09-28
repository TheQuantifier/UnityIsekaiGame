using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.UI;

namespace UnityIsekaiGame.Tests.EditMode
{
    public sealed class ProductionHudTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [TestCase(50f, 100f, 0.5f)]
        [TestCase(-10f, 100f, 0f)]
        [TestCase(200f, 100f, 1f)]
        [TestCase(20f, 0f, 0f)]
        public void ResourceBarNormalizationIsSafeAndClamped(float current, float maximum, float expected)
        {
            Assert.That(HudResourceBarView.Normalize(current, maximum), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void HudMessageBusTrimsMessagesAndCarriesTone()
        {
            GameHudMessage received = default;
            int count = 0;
            void Capture(GameHudMessage message) { received = message; count++; }
            GameHudMessageBus.MessageRequested += Capture;
            try
            {
                GameHudMessageBus.Show("  Quest updated  ", GameHudMessageTone.Success, 3f);
                GameHudMessageBus.Show("   ");
                Assert.That(count, Is.EqualTo(1));
                Assert.That(received.Text, Is.EqualTo("Quest updated"));
                Assert.That(received.Tone, Is.EqualTo(GameHudMessageTone.Success));
                Assert.That(received.Duration, Is.EqualTo(3f));
            }
            finally
            {
                GameHudMessageBus.MessageRequested -= Capture;
            }
        }

        [Test]
        public void InteractionPromptDoesNotRewriteOrRemainVisibleWhenHidden()
        {
            GameObject root = new GameObject("Prompt", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(InteractionPromptView));
            GameObject keyObject = new GameObject("Key", typeof(RectTransform), typeof(Text));
            GameObject promptObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            keyObject.transform.SetParent(root.transform, false);
            promptObject.transform.SetParent(root.transform, false);
            try
            {
                InteractionPromptView view = root.GetComponent<InteractionPromptView>();
                view.Configure(root.GetComponent<CanvasGroup>(), root.GetComponent<Image>(), keyObject.GetComponent<Text>(), promptObject.GetComponent<Text>());
                view.Show("Open the chest");
                Assert.That(view.IsVisible, Is.True);
                Assert.That(view.DisplayedPrompt, Is.EqualTo("Open the chest"));
                view.Hide();
                Assert.That(view.IsVisible, Is.False);
                Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HudLabelsProduceReadablePlayerFacingText()
        {
            Assert.That(QuestTrackerHudView.Humanize("objective.prototype.defeat-bandits"), Is.EqualTo("Defeat bandits"));
            Assert.That(CombatTargetHudView.CleanDisplayName("Prototype Forest Spider(Clone)"), Is.EqualTo("Forest Spider"));
            Assert.That(SpellQuickSlotView.FormatCost(-2f), Is.EqualTo("0 MP"));
        }

        [Test]
        public void PrototypeSceneContainsCompleteProductionHud()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Assert.That(FindAll<PlayerVitalsHudView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<HudResourceBarView>(scene), Has.Length.EqualTo(3));
                Assert.That(FindAll<GameHudNotificationView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<QuestTrackerHudView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<CombatTargetHudView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<SpellQuickSlotView>(scene), Has.Length.EqualTo(4));
                Assert.That(FindAll<InteractionPromptView>(scene), Has.Length.EqualTo(1));

                Canvas[] canvases = FindAll<Canvas>(scene).Where(value => value.name == "HUD Canvas" || value.name == "Interaction Prompt Canvas").ToArray();
                Assert.That(canvases, Has.Length.EqualTo(2));
                Assert.That(canvases.All(value => value.GetComponent<GameUiSafeArea>() != null), Is.True);
                Assert.That(canvases.All(value => value.GetComponent<GameUiThemeApplicator>() != null), Is.True);

                Image[] accentBands = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Image>(true))
                    .Where(image => image.name == "Top Accent")
                    .ToArray();
                Assert.That(accentBands.Length, Is.GreaterThanOrEqualTo(6));
                Assert.That(accentBands.All(image => !image.raycastTarget), Is.True);
                Assert.That(accentBands.Any(image => image.color == GameUiTheme.Accent), Is.True);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previousScenePath)) EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
