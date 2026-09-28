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

        [TestCase(620f, 4)]
        [TestCase(460f, 3)]
        [TestCase(300f, 2)]
        [TestCase(120f, 1)]
        public void InventorySlotsWrapToAvailableWidth(float width, int expectedColumns)
        {
            Assert.That(InventoryScreenView.CalculateInventoryColumnCount(width), Is.EqualTo(expectedColumns));
        }

        [TestCase(400f, 2)]
        [TestCase(280f, 2)]
        [TestCase(180f, 1)]
        public void ItemActionsWrapToAvailableWidth(float width, int expectedColumns)
        {
            Assert.That(InventoryScreenView.CalculateActionColumnCount(width), Is.EqualTo(expectedColumns));
        }

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
                Assert.That(panel.anchorMax.x, Is.EqualTo(0.88f).Within(0.001f));

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
                    return rect.anchorMin.x == 0f && rect.anchorMax.x == 1f && rect.anchorMin.y == 1f && rect.anchorMax.y == 1f;
                }), Is.True);
                RectTransform navigationRect = navigationRoot.GetComponent<RectTransform>();
                Assert.That(navigationRect.anchorMin.x, Is.EqualTo(1f));
                Assert.That(navigationRect.anchorMax.x, Is.EqualTo(1f));

                GridLayoutGroup grid = view.GetComponentsInChildren<GridLayoutGroup>(true)
                    .Single(candidate => candidate.GetComponentInChildren<InventorySlotView>(true) != null);
                Assert.That(grid, Is.Not.Null);
                Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
                Assert.That(grid.constraintCount, Is.InRange(1, 4));
                RectTransform gridRect = grid.GetComponent<RectTransform>();
                Assert.That(gridRect.anchorMin.y, Is.EqualTo(1f).Within(0.001f));
                Assert.That(gridRect.anchorMax.y, Is.EqualTo(1f).Within(0.001f));

                Transform inventoryScrollRoot = FindDescendant(view.transform, "Inventory Slots Scroll");
                Assert.That(inventoryScrollRoot, Is.Not.Null);
                ScrollRect inventoryScroll = inventoryScrollRoot.GetComponent<ScrollRect>();
                Assert.That(inventoryScroll, Is.Not.Null);
                Assert.That(inventoryScroll.vertical, Is.True);
                Assert.That(inventoryScroll.horizontal, Is.False);
                Assert.That(inventoryScroll.verticalScrollbar, Is.Not.Null);
                RectTransform inventoryScrollRect = inventoryScrollRoot.GetComponent<RectTransform>();
                Assert.That(inventoryScrollRect.anchorMax.x, Is.EqualTo(0.63f).Within(0.001f));

                Transform details = FindDescendant(view.transform, "Selected Item Details");
                Assert.That(details, Is.Not.Null);
                RectTransform detailsRect = details.GetComponent<RectTransform>();
                Assert.That(detailsRect.anchorMin.x, Is.GreaterThanOrEqualTo(0.65f));
                Assert.That(detailsRect.anchorMax.x, Is.LessThanOrEqualTo(1f));
                Assert.That(details.GetComponentInChildren<ScrollRect>(true), Is.Not.Null);
                Assert.That(FindDescendant(details, "Item Artwork")?.GetComponent<Image>(), Is.Not.Null);
                ScrollRect detailsScroll = details.GetComponentInChildren<ScrollRect>(true);
                Assert.That(detailsScroll.GetComponent<RectTransform>().anchorMin.y, Is.GreaterThanOrEqualTo(0.2f));
                Assert.That(detailsScroll.GetComponent<RectTransform>().anchorMax.y, Is.LessThanOrEqualTo(0.6f));

                Button[] actionButtons = details.GetComponentsInChildren<Button>(true);
                Transform actionsRoot = FindDescendant(details, "Item Actions");
                Assert.That(actionsRoot, Is.Not.Null);
                GridLayoutGroup actionGrid = actionsRoot.GetComponent<GridLayoutGroup>();
                Assert.That(actionGrid, Is.Not.Null);
                Button[] itemActions = actionsRoot.GetComponentsInChildren<Button>(true);
                Assert.That(itemActions, Has.Length.EqualTo(2));
                Button primaryAction = itemActions.Single(button => button.name.Contains("Item Action Button"));
                Button dropAction = itemActions.Single(button => button.name == "Drop Button");
                Assert.That(primaryAction.GetComponentInChildren<Text>(true).text, Is.EqualTo("\u270A"));
                Assert.That(dropAction.GetComponentInChildren<Text>(true).text, Is.EqualTo("\u21B6"));
                Assert.That(dropAction.GetComponentInChildren<Text>(true).color, Is.EqualTo(UnityIsekaiGame.Presentation.GameUiTheme.Danger));
                Assert.That(actionGrid.constraintCount, Is.InRange(1, 2));
                Assert.That(actionsRoot.GetComponent<RectTransform>().anchorMax.y, Is.LessThanOrEqualTo(0.2f));

                int useInvocations = 0;
                int equipInvocations = 0;
                view.Initialize(null, () => useInvocations++, null, () => equipInvocations++);
                view.SetInventoryActions(canUse: true, canEquip: false, canDrop: true);
                primaryAction.onClick.Invoke();
                view.SetInventoryActions(canUse: false, canEquip: true, canDrop: true);
                primaryAction.onClick.Invoke();
                Assert.That(useInvocations, Is.EqualTo(1));
                Assert.That(equipInvocations, Is.EqualTo(1));

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
