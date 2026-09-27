using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.Tests.EditMode
{
    public sealed class PrototypeUiThemeTests
    {
        [TestCase("Journal Title", PrototypeUiTextRole.Title)]
        [TestCase("Quest Heading", PrototypeUiTextRole.Heading)]
        [TestCase("Save Feedback", PrototypeUiTextRole.Feedback)]
        [TestCase("Error Message", PrototypeUiTextRole.Danger)]
        [TestCase("Item Description", PrototypeUiTextRole.Muted)]
        [TestCase("Name", PrototypeUiTextRole.Body)]
        public void TextRolesAreInferredFromSemanticNames(string objectName, PrototypeUiTextRole expected)
        {
            Assert.That(PrototypeUiTheme.InferTextRole(objectName), Is.EqualTo(expected));
        }

        [TestCase("Delete Save", PrototypeUiButtonTone.Danger)]
        [TestCase("Abandon Quest", PrototypeUiButtonTone.Danger)]
        [TestCase("Claim Reward", PrototypeUiButtonTone.Positive)]
        [TestCase("Equip Item", PrototypeUiButtonTone.Primary)]
        [TestCase("Next", PrototypeUiButtonTone.Neutral)]
        public void ButtonTonesAreInferredFromActions(string objectName, PrototypeUiButtonTone expected)
        {
            Assert.That(PrototypeUiTheme.InferButtonTone(objectName), Is.EqualTo(expected));
        }

        [Test]
        public void ConfigureCanvasUsesResolutionIndependentScalingWithoutDuplicatingScaler()
        {
            GameObject gameObject = new GameObject("Canvas", typeof(Canvas));
            try
            {
                Canvas canvas = gameObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                PrototypeUiTheme.ConfigureCanvas(canvas);
                PrototypeUiTheme.ConfigureCanvas(canvas);

                CanvasScaler[] scalers = gameObject.GetComponents<CanvasScaler>();
                Assert.That(scalers, Has.Length.EqualTo(1));
                Assert.That(scalers[0].uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scalers[0].referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
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
                PrototypeUiTheme.StyleButton(button, PrototypeUiButtonTone.Positive);

                Assert.That(button.colors.highlightedColor, Is.Not.EqualTo(button.colors.normalColor));
                Assert.That(button.colors.disabledColor.a, Is.LessThan(button.colors.normalColor.a));
                Assert.That(gameObject.GetComponent<Image>().color, Is.EqualTo(button.colors.normalColor));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
