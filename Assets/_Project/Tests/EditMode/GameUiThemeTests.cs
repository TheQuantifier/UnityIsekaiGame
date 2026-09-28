using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.UI;

namespace UnityIsekaiGame.Tests.EditMode
{
    public sealed class GameUiThemeTests
    {
        [TestCase("Journal Title", GameUiTextRole.Title)]
        [TestCase("Quest Heading", GameUiTextRole.Heading)]
        [TestCase("Save Feedback", GameUiTextRole.Feedback)]
        [TestCase("Error Message", GameUiTextRole.Danger)]
        [TestCase("Item Description", GameUiTextRole.Muted)]
        [TestCase("Name", GameUiTextRole.Body)]
        public void TextRolesAreInferredFromSemanticNames(string objectName, GameUiTextRole expected)
        {
            Assert.That(GameUiTheme.InferTextRole(objectName), Is.EqualTo(expected));
        }

        [TestCase("Delete Save", GameUiButtonTone.Danger)]
        [TestCase("Abandon Quest", GameUiButtonTone.Danger)]
        [TestCase("Claim Reward", GameUiButtonTone.Positive)]
        [TestCase("Equip Item", GameUiButtonTone.Primary)]
        [TestCase("Next", GameUiButtonTone.Neutral)]
        public void ButtonTonesAreInferredFromActions(string objectName, GameUiButtonTone expected)
        {
            Assert.That(GameUiTheme.InferButtonTone(objectName), Is.EqualTo(expected));
        }

        [Test]
        public void ConfigureCanvasUsesResolutionIndependentScalingWithoutDuplicatingScaler()
        {
            GameObject gameObject = new GameObject("Canvas", typeof(Canvas));
            try
            {
                Canvas canvas = gameObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                GameUiTheme.ConfigureCanvas(canvas);
                GameUiTheme.ConfigureCanvas(canvas);

                CanvasScaler[] scalers = gameObject.GetComponents<CanvasScaler>();
                Assert.That(scalers, Has.Length.EqualTo(1));
                Assert.That(scalers[0].uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scalers[0].referenceResolution, Is.EqualTo(GameUiTheme.ReferenceResolution));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void StyleButtonProvidesDistinctInteractionStates()
        {
            GameObject gameObject = new GameObject("Accept Quest", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                Button button = gameObject.GetComponent<Button>();
                GameUiTheme.StyleButton(button, GameUiButtonTone.Positive);

                Assert.That(button.colors.highlightedColor, Is.Not.EqualTo(button.colors.normalColor));
                Assert.That(button.colors.pressedColor, Is.Not.EqualTo(button.colors.normalColor));
                Assert.That(button.colors.disabledColor.a, Is.LessThan(button.colors.normalColor.a));
                Assert.That(gameObject.GetComponent<Image>().color, Is.EqualTo(button.colors.normalColor));
                Assert.That(gameObject.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase(1024, 768, 0f)]
        [TestCase(2560, 1080, 1f)]
        public void CanvasMatchAdaptsAcrossSupportedAspectRatios(int width, int height, float expected)
        {
            Assert.That(GameUiTheme.CalculateCanvasMatch(width, height), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void StandardWidescreenUsesBalancedResponsiveScaling()
        {
            float match = GameUiTheme.CalculateCanvasMatch(1920, 1080);
            Assert.That(match, Is.GreaterThan(0f));
            Assert.That(match, Is.LessThan(1f));
        }

        [Test]
        public void NestedCanvasUsesItsRootCanvasForScalingAndSafeArea()
        {
            GameObject rootObject = new GameObject("Root Canvas", typeof(RectTransform), typeof(Canvas));
            GameObject nestedObject = new GameObject("Nested Canvas", typeof(RectTransform), typeof(Canvas));
            nestedObject.transform.SetParent(rootObject.transform, false);
            try
            {
                Canvas root = rootObject.GetComponent<Canvas>();
                Canvas nested = nestedObject.GetComponent<Canvas>();
                root.renderMode = RenderMode.ScreenSpaceOverlay;
                nested.renderMode = RenderMode.ScreenSpaceOverlay;

                Assert.That(GameUiThemeApplicator.FindRootScreenSpaceCanvas(nested), Is.SameAs(root));
                Assert.That(GameUiThemeApplicator.FindRootScreenSpaceCanvas(root), Is.SameAs(root));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void TavernPaletteUsesWarmBrownAndGoldSurfaces()
        {
            Assert.That(GameUiTheme.Panel.r, Is.GreaterThan(GameUiTheme.Panel.b));
            Assert.That(GameUiTheme.PanelRaised.r, Is.GreaterThan(GameUiTheme.PanelRaised.b));
            Assert.That(GameUiTheme.SlotEmpty.r, Is.GreaterThan(GameUiTheme.SlotEmpty.b));
            Assert.That(GameUiTheme.SlotOccupied.r, Is.GreaterThan(GameUiTheme.SlotOccupied.b));
            Assert.That(GameUiTheme.Accent.r, Is.GreaterThan(GameUiTheme.Accent.b));
            Assert.That(GameUiTheme.Accent.g, Is.GreaterThan(GameUiTheme.Accent.b));
            Assert.That(GameUiTheme.TextPrimary.r, Is.GreaterThan(GameUiTheme.TextPrimary.b));
        }

        [Test]
        public void ItemSlotThemeUsesWarmStateColorsAndSelectionBorder()
        {
            GameObject gameObject = new GameObject("Inventory Slot", typeof(RectTransform), typeof(Image));
            try
            {
                Image image = gameObject.GetComponent<Image>();
                GameUiTheme.StyleSlot(image, occupied: false, hovered: false, selected: false, accent: GameUiTheme.Border);

                Assert.That(image.color, Is.EqualTo(GameUiTheme.SlotEmpty));
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(gameObject.GetComponent<Outline>(), Is.Not.Null);

                GameUiTheme.StyleSlot(image, occupied: true, hovered: false, selected: true, accent: Color.cyan);

                Assert.That(image.color, Is.EqualTo(GameUiTheme.SlotSelected));
                Assert.That(gameObject.GetComponent<Outline>().effectColor, Is.EqualTo(GameUiTheme.AccentBright));
                Assert.That(gameObject.GetComponents<Outline>(), Has.Length.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TextInputUsesReadableTavernThemeStates()
        {
            GameObject gameObject = new GameObject("Character Name", typeof(RectTransform), typeof(Image), typeof(InputField));
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(gameObject.transform, false);
            placeholderObject.transform.SetParent(gameObject.transform, false);
            try
            {
                InputField inputField = gameObject.GetComponent<InputField>();
                inputField.targetGraphic = gameObject.GetComponent<Image>();
                inputField.textComponent = textObject.GetComponent<Text>();
                inputField.placeholder = placeholderObject.GetComponent<Text>();

                GameUiTheme.StyleInputField(inputField);

                Assert.That(inputField.customCaretColor, Is.True);
                Assert.That(inputField.textComponent.color, Is.EqualTo(GameUiTheme.TextPrimary));
                Assert.That(((Text)inputField.placeholder).color, Is.EqualTo(GameUiTheme.TextMuted));
                Assert.That(gameObject.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
