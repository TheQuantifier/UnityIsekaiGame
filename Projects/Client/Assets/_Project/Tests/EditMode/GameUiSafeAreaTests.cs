using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.UI;

namespace UnityIsekaiGame.Tests.EditMode
{
    public sealed class GameUiSafeAreaTests
    {
        [Test]
        public void FullScreenSafeAreaPreservesAuthoredAnchors()
        {
            Vector2 originalMin = new Vector2(0.1f, 0.2f);
            Vector2 originalMax = new Vector2(0.8f, 0.9f);

            GameUiSafeArea.CalculateMappedAnchors(
                new Rect(0f, 0f, 1920f, 1080f),
                new Vector2Int(1920, 1080),
                originalMin,
                originalMax,
                out Vector2 mappedMin,
                out Vector2 mappedMax);

            Assert.That(mappedMin, Is.EqualTo(originalMin));
            Assert.That(mappedMax, Is.EqualTo(originalMax));
        }

        [Test]
        public void SafeAreaMapsLayoutInsidePlatformInsets()
        {
            GameUiSafeArea.CalculateMappedAnchors(
                new Rect(100f, 50f, 1800f, 980f),
                new Vector2Int(2000, 1080),
                Vector2.zero,
                Vector2.one,
                out Vector2 mappedMin,
                out Vector2 mappedMax);

            Assert.That(mappedMin.x, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(mappedMin.y, Is.EqualTo(50f / 1080f).Within(0.0001f));
            Assert.That(mappedMax.x, Is.EqualTo(0.95f).Within(0.0001f));
            Assert.That(mappedMax.y, Is.EqualTo(1030f / 1080f).Within(0.0001f));
        }

        [Test]
        public void InvalidScreenSizeLeavesLayoutUntouched()
        {
            Vector2 originalMin = new Vector2(0.25f, 0.25f);
            Vector2 originalMax = new Vector2(0.75f, 0.75f);

            GameUiSafeArea.CalculateMappedAnchors(
                Rect.zero,
                Vector2Int.zero,
                originalMin,
                originalMax,
                out Vector2 mappedMin,
                out Vector2 mappedMax);

            Assert.That(mappedMin, Is.EqualTo(originalMin));
            Assert.That(mappedMax, Is.EqualTo(originalMax));
        }
    }
}
