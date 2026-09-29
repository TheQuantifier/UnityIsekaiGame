using System.Linq;
using NUnit.Framework;
using UnityEditor;
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
        public void ResourceBarUsesRoundedResizableLiquidFillAndTranslucentHighlight()
        {
            GameObject root = new GameObject("Resource Bar", typeof(RectTransform), typeof(Image), typeof(HudResourceBarView));
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            GameObject highlightObject = new GameObject("Liquid Highlight", typeof(RectTransform), typeof(Image));
            GameObject nameObject = new GameObject("Name", typeof(RectTransform), typeof(Text));
            GameObject valueObject = new GameObject("Value", typeof(RectTransform), typeof(Text));
            fillObject.transform.SetParent(root.transform, false);
            highlightObject.transform.SetParent(fillObject.transform, false);
            nameObject.transform.SetParent(root.transform, false);
            valueObject.transform.SetParent(root.transform, false);
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(200f, 35f);
                Image track = root.GetComponent<Image>();
                Image fill = fillObject.GetComponent<Image>();
                Image highlight = highlightObject.GetComponent<Image>();
                HudResourceBarView view = root.GetComponent<HudResourceBarView>();
                view.Configure(track, fill, highlight, nameObject.GetComponent<Text>(), valueObject.GetComponent<Text>());

                view.Render("HEALTH", 50f, 100f, Color.red);

                Assert.That(view.DisplayedFill, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(track.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(fill.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(fill.rectTransform.rect.width, Is.EqualTo(97f).Within(0.01f));
                Assert.That(fill.color.a, Is.EqualTo(0.9f).Within(0.001f));
                Assert.That(highlight.color.a, Is.GreaterThan(0f).And.LessThan(0.3f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
            GameObject legacyObject = new GameObject("Interaction Prompt Text", typeof(RectTransform), typeof(Text));
            keyObject.transform.SetParent(root.transform, false);
            promptObject.transform.SetParent(root.transform, false);
            legacyObject.transform.SetParent(root.transform, false);
            try
            {
                InteractionPromptView view = root.GetComponent<InteractionPromptView>();
                view.Configure(root.GetComponent<CanvasGroup>(), root.GetComponent<Image>(), keyObject.GetComponent<Text>(), promptObject.GetComponent<Text>());
                view.Show("Open the chest");
                Assert.That(view.IsVisible, Is.True);
                Assert.That(view.DisplayedPrompt, Is.EqualTo("Open the chest"));
                Assert.That(promptObject.GetComponent<Text>().text, Is.EqualTo("Open the chest"));
                Assert.That(legacyObject.GetComponent<Text>().enabled, Is.False, "Unbound legacy labels must not draw over the active prompt.");
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

                PlayerVitalsHudView vitals = FindAll<PlayerVitalsHudView>(scene).Single();
                Assert.That(vitals.GetComponent<Image>()?.enabled, Is.False, "Vitals should not draw a backing panel.");
                Assert.That(vitals.transform.Find("Vitals Heading")?.gameObject.activeSelf, Is.False, "Vitals should not show a job title.");
                foreach (HudResourceBarView bar in FindAll<HudResourceBarView>(scene))
                {
                    Image track = bar.GetComponent<Image>();
                    Image fill = bar.transform.Find("Fill")?.GetComponent<Image>();
                    Image highlight = bar.transform.Find("Fill/Liquid Highlight")?.GetComponent<Image>();
                    Assert.That(track, Is.Not.Null);
                    Assert.That(fill, Is.Not.Null);
                    Assert.That(highlight, Is.Not.Null);
                    Assert.That(track.type, Is.EqualTo(Image.Type.Sliced));
                    Assert.That(fill.type, Is.EqualTo(Image.Type.Sliced));
                    Assert.That(track.sprite, Is.SameAs(fill.sprite), $"{bar.name} track and fill should use the same rounded surface.");
                    Assert.That(highlight.color.a, Is.GreaterThan(0f).And.LessThan(0.3f));
                }

                InteractionPromptView interactionPrompt = FindAll<InteractionPromptView>(scene).Single();
                Text[] directPromptLabels = interactionPrompt.transform.Cast<Transform>()
                    .Select(child => child.GetComponent<Text>())
                    .Where(label => label != null)
                    .ToArray();
                Assert.That(directPromptLabels.Select(label => label.name), Is.EquivalentTo(new[] { "Prompt Text" }));
                Assert.That(interactionPrompt.transform.Find("Interaction Prompt Text"), Is.Null, "The obsolete static prompt label would overlap the live interaction text.");
                RectTransform promptRect = directPromptLabels.Single().rectTransform;
                RectTransform badgeRect = interactionPrompt.transform.Find("Input Badge") as RectTransform;
                Assert.That(badgeRect, Is.Not.Null);
                Assert.That(promptRect.offsetMin.x, Is.GreaterThan(badgeRect.offsetMax.x), "The prompt copy must begin to the right of the input badge.");

                QuestTrackerHudView questTracker = FindAll<QuestTrackerHudView>(scene).Single();
                CombatTargetHudView targetFrame = FindAll<CombatTargetHudView>(scene).Single();
                GameHudNotificationView notifications = FindAll<GameHudNotificationView>(scene).Single();
                AssertTransientHudIsWiredAndHidden(questTracker);
                AssertTransientHudIsWiredAndHidden(targetFrame);
                AssertTransientHudIsWiredAndHidden(notifications);

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

        private static void AssertTransientHudIsWiredAndHidden(MonoBehaviour view)
        {
            CanvasGroup group = view.GetComponent<CanvasGroup>();
            Assert.That(group, Is.Not.Null, $"{view.name} needs a CanvasGroup so empty HUD state can be hidden.");
            SerializedObject serializedView = new SerializedObject(view);
            SerializedProperty groupProperty = serializedView.FindProperty("canvasGroup");
            Assert.That(groupProperty, Is.Not.Null, view.name);
            Assert.That(groupProperty.objectReferenceValue, Is.SameAs(group), $"{view.name} must serialize its CanvasGroup reference.");
            Assert.That(group.alpha, Is.Zero, $"{view.name} should be hidden until it has content.");
            Assert.That(group.interactable, Is.False);
            Assert.That(group.blocksRaycasts, Is.False);
        }
    }
}
