using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.UI.Inventory;

namespace UnityIsekaiGame.Tests
{
    public sealed class InventoryMenuLayoutTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [Test]
        public void PrototypeInventoryMenuUsesResponsiveNonClippingLayout()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                InventoryScreenView view = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<InventoryScreenView>(true))
                    .Single();

                view.Initialize(null, null);
                view.RenderCharacter(null, null, null, null, null);
                Canvas.ForceUpdateCanvases();

                RectTransform panel = view.GetComponent<RectTransform>();
                Assert.That(panel.anchorMin.x, Is.EqualTo(0.075f).Within(0.001f));
                Assert.That(panel.anchorMax.x, Is.EqualTo(0.925f).Within(0.001f));

                Transform navigationRoot = FindDescendant(view.transform, "Right Menu");
                Assert.That(navigationRoot, Is.Not.Null);
                VerticalLayoutGroup navigation = navigationRoot.GetComponent<VerticalLayoutGroup>();
                Assert.That(navigation, Is.Not.Null);
                Assert.That(navigation.enabled, Is.False);
                Button[] navigationButtons = navigation.GetComponentsInChildren<Button>(true);
                Assert.That(navigationButtons.Length, Is.GreaterThanOrEqualTo(5));
                Assert.That(navigationButtons.All(button =>
                {
                    RectTransform rect = button.GetComponent<RectTransform>();
                    return rect.anchorMin.x >= 0f && rect.anchorMax.x <= 1f && rect.anchorMax.x > rect.anchorMin.x;
                }), Is.True);

                GridLayoutGroup grid = view.GetComponentInChildren<GridLayoutGroup>(true);
                Assert.That(grid, Is.Not.Null);
                Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
                Assert.That(grid.constraintCount, Is.EqualTo(4));
                RectTransform gridRect = grid.GetComponent<RectTransform>();
                Assert.That(gridRect.anchorMin.x, Is.EqualTo(0.02f).Within(0.001f));
                Assert.That(gridRect.anchorMax.x, Is.EqualTo(0.63f).Within(0.001f));
                Assert.That(gridRect.anchorMax.y, Is.LessThan(0.9f));

                Transform details = FindDescendant(view.transform, "Selected Item Details");
                Assert.That(details, Is.Not.Null);
                RectTransform detailsRect = details.GetComponent<RectTransform>();
                Assert.That(detailsRect.anchorMin.x, Is.GreaterThanOrEqualTo(0.65f));
                Assert.That(detailsRect.anchorMax.x, Is.LessThanOrEqualTo(1f));
                Assert.That(details.GetComponentInChildren<ScrollRect>(true), Is.Not.Null);
                Assert.That(FindDescendant(details, "Item Artwork")?.GetComponent<Image>(), Is.Not.Null);

                Transform characterStats = FindDescendant(view.transform, "Character Stats And Status");
                Assert.That(characterStats, Is.Not.Null);
                Assert.That(characterStats.GetComponentInChildren<ScrollRect>(true), Is.Not.Null);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previousScenePath))
                {
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            return descendants.FirstOrDefault(candidate => candidate != null && candidate.name == objectName);
        }
    }
}
