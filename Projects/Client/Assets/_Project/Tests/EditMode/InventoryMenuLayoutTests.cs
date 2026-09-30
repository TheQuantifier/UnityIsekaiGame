using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.UI.Inventory;

namespace UnityIsekaiGame.Tests
{
    public sealed class InventoryMenuLayoutTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [TestCase(620f, 5)]
        [TestCase(460f, 4)]
        [TestCase(300f, 2)]
        [TestCase(120f, 1)]
        public void InventorySlotsWrapToAvailableWidth(float width, int expectedColumns)
        {
            Assert.That(InventoryScreenView.CalculateInventoryColumnCount(width), Is.EqualTo(expectedColumns));
        }

        [Test]
        public void ResolvingCharacterForMenuDisplayDoesNotInitializeOrMutateCharacter()
        {
            GameObject player = new GameObject("Menu Read Only Character");
            try
            {
                CharacterSystemCoordinator character = player.AddComponent<CharacterSystemCoordinator>();
                InventoryScreenController controller = player.AddComponent<InventoryScreenController>();
                DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(
                    "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset");
                Assert.That(catalog, Is.Not.Null);

                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(InventoryScreenController).GetField("itemUser", flags)?.SetValue(controller, player);
                typeof(InventoryScreenController).GetField("saveLoadDefinitionCatalog", flags)?.SetValue(controller, catalog);

                Assert.That(character.Readiness, Is.EqualTo(CharacterReadinessState.Uninitialized));
                Assert.That(controller.RuntimeCharacterSystem, Is.SameAs(character));
                Assert.That(character.Readiness, Is.EqualTo(CharacterReadinessState.Uninitialized),
                    "A menu data lookup must never initialize or rebuild authoritative character state.");
                Assert.That(character.Revision, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [TestCase(400f, 3)]
        [TestCase(180f, 3)]
        [TestCase(112f, 2)]
        [TestCase(55f, 1)]
        public void ItemActionsWrapToAvailableWidth(float width, int expectedColumns)
        {
            Assert.That(InventoryScreenView.CalculateActionColumnCount(width), Is.EqualTo(expectedColumns));
        }

        [Test]
        public void InventorySlotDropRoutesTheDraggedSourceAndDestinationIndexes()
        {
            GameObject root = new GameObject("Inventory Drag Test", typeof(Canvas));
            GameObject sourceObject = new GameObject("Source", typeof(RectTransform), typeof(Image), typeof(InventorySlotView));
            GameObject destinationObject = new GameObject("Destination", typeof(RectTransform), typeof(Image), typeof(InventorySlotView));
            sourceObject.transform.SetParent(root.transform, false);
            destinationObject.transform.SetParent(root.transform, false);
            try
            {
                ItemDefinition arrows = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    "Packages/com.thequantifier.isekai.content/Content/Items/Definitions/PrototypeArrow.asset");
                InventorySlotView source = sourceObject.GetComponent<InventorySlotView>();
                InventorySlotView destination = destinationObject.GetComponent<InventorySlotView>();
                int routedSource = -1;
                int routedDestination = -1;
                source.Initialize(2, null, null, (from, to) => { routedSource = from; routedDestination = to; });
                destination.Initialize(7, null, null, (from, to) => { routedSource = from; routedDestination = to; });
                source.Render(CreateOccupiedSlot(arrows, 2));
                destination.RenderEmpty();

                PointerEventData eventData = new PointerEventData(null) { pointerDrag = sourceObject };
                destination.OnDrop(eventData);

                Assert.That(source, Is.InstanceOf<IBeginDragHandler>());
                Assert.That(source, Is.InstanceOf<IDragHandler>());
                Assert.That(source, Is.InstanceOf<IEndDragHandler>());
                Assert.That(destination, Is.InstanceOf<IDropHandler>());
                Assert.That(routedSource, Is.EqualTo(2));
                Assert.That(routedDestination, Is.EqualTo(7));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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

                GridLayoutGroup serializedGrid = view.GetComponentsInChildren<GridLayoutGroup>(true)
                    .Single(candidate => candidate.GetComponentInChildren<InventorySlotView>(true) != null);
                Assert.That(serializedGrid.constraintCount, Is.EqualTo(5));
                Assert.That(serializedGrid.cellSize, Is.EqualTo(new Vector2(108f, 108f)));

                view.Initialize(null, null);
                view.RenderCharacter(null, null, null, null, null);
                Canvas.ForceUpdateCanvases();

                Transform panelTransform = FindDescendant(view.transform, "Inventory Panel");
                Assert.That(panelTransform, Is.Not.Null);
                RectTransform panel = panelTransform.GetComponent<RectTransform>();
                Assert.That(panel.anchorMin.x, Is.EqualTo(0.18f).Within(0.001f));
                Assert.That(panel.anchorMax.x, Is.EqualTo(0.92f).Within(0.001f));
                Image panelImage = panelTransform.GetComponent<Image>();
                Assert.That(panelImage.color, Is.EqualTo(GameUiTheme.Panel));
                Outline panelOutline = panelTransform.GetComponent<Outline>();
                Assert.That(panelOutline == null || !panelOutline.enabled, Is.True);

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
                Assert.That(navigationRoot.parent, Is.EqualTo(panelTransform));
                Assert.That(navigationRect.anchorMin.x, Is.EqualTo(1f));
                Assert.That(navigationRect.anchorMax.x, Is.EqualTo(1f));
                Assert.That(navigationRect.pivot.x, Is.EqualTo(1f));
                Assert.That(navigationRect.offsetMin.x, Is.EqualTo(-202f).Within(0.01f));
                Assert.That(navigationRect.offsetMax.x, Is.EqualTo(-18f).Within(0.01f));
                Assert.That(navigationButtons.All(button => button.GetComponent<RectTransform>().anchoredPosition.x == 0f), Is.True);

                RectTransform contentRect = FindDescendant(view.transform, "Center Content").GetComponent<RectTransform>();
                Assert.That(contentRect.offsetMax.x, Is.EqualTo(navigationRect.offsetMin.x).Within(0.01f));

                GridLayoutGroup grid = view.GetComponentsInChildren<GridLayoutGroup>(true)
                    .Single(candidate => candidate.GetComponentInChildren<InventorySlotView>(true) != null);
                Assert.That(grid, Is.Not.Null);
                Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
                Assert.That(grid.constraintCount, Is.InRange(1, 5));
                Assert.That(grid.cellSize.x, Is.EqualTo(grid.cellSize.y).Within(0.01f));
                RectTransform gridRect = grid.GetComponent<RectTransform>();
                Assert.That(gridRect.anchorMin.y, Is.EqualTo(1f).Within(0.001f));
                Assert.That(gridRect.anchorMax.y, Is.EqualTo(1f).Within(0.001f));
                Assert.That(view.GetComponentsInChildren<InventorySlotView>(true).All(slot => slot.transform.Find("Equipped Marker") != null), Is.True);
                InventorySlotView emptySlot = view.GetComponentsInChildren<InventorySlotView>(true).First();
                emptySlot.RenderEmpty();
                Text emptyLabel = emptySlot.GetComponentsInChildren<Text>(true).Single(text => text.text == "Empty");
                Image emptyIcon = emptySlot.GetComponentsInChildren<Image>(true)
                    .First(image => image.gameObject != emptySlot.gameObject && image.name.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.That(emptyLabel.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(emptyIcon.enabled, Is.False, "Empty slots must not render Unity's white no-sprite image.");
                Assert.That(emptyLabel.rectTransform.anchorMin.x, Is.GreaterThanOrEqualTo(0f));
                Assert.That(emptyLabel.rectTransform.anchorMin.y, Is.GreaterThanOrEqualTo(0f));
                Assert.That(emptyLabel.rectTransform.anchorMax.x, Is.LessThanOrEqualTo(1f));
                Assert.That(emptyLabel.rectTransform.anchorMax.y, Is.LessThanOrEqualTo(1f));
                Assert.That(emptyLabel.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(emptyLabel.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));

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
                Assert.That(details.gameObject.activeSelf, Is.True, "The empty inspector should remain visible as useful guidance.");
                Assert.That(details.GetComponentsInChildren<Text>(true).Any(text => text.text == "Select an Item"), Is.True);
                RectTransform detailsRect = details.GetComponent<RectTransform>();
                Assert.That(detailsRect.anchorMin.x, Is.GreaterThanOrEqualTo(0.65f));
                Assert.That(detailsRect.anchorMax.x, Is.LessThanOrEqualTo(1f));
                Assert.That(details.GetComponentInChildren<ScrollRect>(true), Is.Not.Null);
                Assert.That(FindDescendant(details, "Item Artwork")?.GetComponent<Image>(), Is.Not.Null);
                Transform itemStatus = FindDescendant(details, "Item Status");
                Assert.That(itemStatus, Is.Not.Null);
                Assert.That(itemStatus.gameObject.activeSelf, Is.False);
                Assert.That(FindDescendant(itemStatus, "Durability Fill")?.GetComponent<Image>(), Is.Not.Null);
                Text detailsText = FindDescendant(details, "Item Details")?.GetComponent<Text>();
                Text statusText = FindDescendant(itemStatus, "Status Label")?.GetComponent<Text>();
                Assert.That(detailsText, Is.Not.Null);
                Assert.That(statusText, Is.Not.Null);
                Assert.That(detailsText.fontSize, Is.GreaterThanOrEqualTo(14));
                Assert.That(statusText.fontSize, Is.GreaterThanOrEqualTo(14));
                Transform comparisonTooltip = FindDescendant(details, "Equipment Comparison Tooltip");
                Assert.That(comparisonTooltip, Is.Not.Null);
                Assert.That(comparisonTooltip.gameObject.activeSelf, Is.False);
                Text comparisonText = FindDescendant(comparisonTooltip, "Comparison Details")?.GetComponent<Text>();
                Assert.That(comparisonText, Is.Not.Null);
                Assert.That(comparisonText.fontSize, Is.GreaterThanOrEqualTo(14));
                string originalDetails = detailsText.text;
                view.ShowEquipmentComparison("<b>Equipped:</b> Test Sword");
                Assert.That(comparisonTooltip.gameObject.activeSelf, Is.True);
                StringAssert.Contains("Test Sword", comparisonText.text);
                Assert.That(detailsText.text, Is.EqualTo(originalDetails), "Comparison hover must not replace the selected-item details.");
                view.HideEquipmentComparison();
                Assert.That(comparisonTooltip.gameObject.activeSelf, Is.False);
                ScrollRect detailsScroll = details.GetComponentInChildren<ScrollRect>(true);
                Assert.That(detailsScroll.GetComponent<RectTransform>().anchorMin.y, Is.GreaterThanOrEqualTo(0.2f));
                Assert.That(detailsScroll.GetComponent<RectTransform>().anchorMax.y, Is.LessThanOrEqualTo(0.6f));

                Button[] actionButtons = details.GetComponentsInChildren<Button>(true);
                Transform actionsRoot = FindDescendant(details, "Item Actions");
                Assert.That(actionsRoot, Is.Not.Null);
                GridLayoutGroup actionGrid = actionsRoot.GetComponent<GridLayoutGroup>();
                Assert.That(actionGrid, Is.Not.Null);
                Button[] itemActions = actionsRoot.GetComponentsInChildren<Button>(true);
                Assert.That(itemActions, Has.Length.EqualTo(3));
                Button primaryAction = itemActions.Single(button => button.name.Contains("Item Action Button"));
                Button dropAction = itemActions.Single(button => button.name == "Drop Button");
                Button dropAllAction = itemActions.Single(button => button.name == "Drop All Button");
                Assert.That(primaryAction.gameObject.activeSelf, Is.False, "Actions should not appear until an item is selected.");
                Assert.That(dropAction.gameObject.activeSelf, Is.False, "Actions should not appear until an item is selected.");
                Image primaryIcon = primaryAction.transform.Find("Icon")?.GetComponent<Image>();
                Image dropIcon = dropAction.transform.Find("Icon")?.GetComponent<Image>();
                Image dropAllIcon = dropAllAction.transform.Find("Icon")?.GetComponent<Image>();
                Assert.That(primaryIcon, Is.Not.Null);
                Assert.That(dropIcon, Is.Not.Null);
                Assert.That(dropAllIcon, Is.Not.Null);
                Assert.That(primaryIcon.sprite?.name, Is.EqualTo("action-use-equip"));
                Assert.That(dropIcon.sprite?.name, Is.EqualTo("action-drop"));
                Assert.That(dropAllIcon.sprite, Is.SameAs(dropIcon.sprite));
                Assert.That(primaryIcon.rectTransform.sizeDelta, Is.EqualTo(new Vector2(34f, 34f)));
                Assert.That(dropAllAction.transform.Find("All Label")?.GetComponent<Text>()?.text, Is.EqualTo("ALL"));
                Assert.That(dropAllAction.gameObject.activeSelf, Is.False);
                Assert.That(actionGrid.constraintCount, Is.InRange(1, 3));
                Assert.That(actionGrid.cellSize, Is.EqualTo(new Vector2(50f, 50f)));
                Assert.That(actionsRoot.GetComponent<RectTransform>().anchorMax.y, Is.LessThanOrEqualTo(0.2f));

                int useInvocations = 0;
                int equipInvocations = 0;
                int unequipInvocations = 0;
                int dropInvocations = 0;
                int dropAllInvocations = 0;
                int comparisonEnterInvocations = 0;
                int comparisonExitInvocations = 0;
                view.Initialize(null, () => useInvocations++, null, () => equipInvocations++, () => unequipInvocations++, null, () => dropInvocations++, () => dropAllInvocations++, hovering =>
                {
                    if (hovering) comparisonEnterInvocations++;
                    else comparisonExitInvocations++;
                });
                view.SetInventoryActions(canUse: true, canEquip: false, canDrop: true, canDropAll: true);
                Assert.That(dropAllAction.gameObject.activeSelf, Is.True);
                primaryAction.onClick.Invoke();

                ItemDefinition healthPotion = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Packages/com.thequantifier.isekai.content/Content/Items/Definitions/HealthPotion.asset");
                Assert.That(healthPotion, Is.Not.Null);
                Assert.That(healthPotion.ConsumablePresentation, Is.EqualTo(ConsumablePresentationType.Potion));
                InventorySlot potionSlot = CreateOccupiedSlot(healthPotion, 2);
                view.RenderSelectedItemDetails(potionSlot, includeDescription: true);
                Assert.That(itemStatus.gameObject.activeSelf, Is.True);
                StringAssert.Contains("Stack  2 /", statusText.text);
                StringAssert.Contains("Health", detailsText.text);
                view.SetInventoryActions(canUse: true, canEquip: false, canDrop: true, actionItem: healthPotion);
                Assert.That(primaryIcon.sprite?.name, Is.EqualTo("action-consume-potion"));

                ItemDefinition food = ScriptableObject.CreateInstance<ItemDefinition>();
                typeof(ItemDefinition).GetField("consumablePresentation", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(food, ConsumablePresentationType.Food);
                view.SetInventoryActions(canUse: true, canEquip: false, canDrop: true, actionItem: food);
                Assert.That(primaryIcon.sprite?.name, Is.EqualTo("action-consume-food"));
                Object.DestroyImmediate(food);

                dropAction.onClick.Invoke();
                dropAllAction.onClick.Invoke();
                view.SetInventoryActions(canUse: false, canEquip: true, canDrop: true);
                Assert.That(dropAllAction.gameObject.activeSelf, Is.False);
                EventTrigger primaryTrigger = primaryAction.GetComponent<EventTrigger>();
                Assert.That(primaryTrigger, Is.Not.Null);
                Assert.That(primaryTrigger.triggers.Count(entry => entry.eventID == EventTriggerType.PointerEnter), Is.EqualTo(1));
                Assert.That(primaryTrigger.triggers.Count(entry => entry.eventID == EventTriggerType.PointerExit), Is.EqualTo(1));
                primaryTrigger.triggers.Single(entry => entry.eventID == EventTriggerType.PointerEnter).callback.Invoke(null);
                primaryTrigger.triggers.Single(entry => entry.eventID == EventTriggerType.PointerExit).callback.Invoke(null);
                primaryAction.onClick.Invoke();
                view.SetInventoryActions(canUse: false, canEquip: false, canDrop: false, canUnequip: true);
                Assert.That(primaryIcon.sprite?.name, Is.EqualTo("action-unequip"));
                primaryAction.onClick.Invoke();
                Assert.That(useInvocations, Is.EqualTo(1));
                Assert.That(equipInvocations, Is.EqualTo(1));
                Assert.That(unequipInvocations, Is.EqualTo(1));
                Assert.That(dropInvocations, Is.EqualTo(1));
                Assert.That(dropAllInvocations, Is.EqualTo(1));
                Assert.That(comparisonEnterInvocations, Is.EqualTo(1));
                Assert.That(comparisonExitInvocations, Is.GreaterThanOrEqualTo(1));
                Assert.That(dropAllAction.transform.Find("Label"), Is.Null);
                Assert.That(primaryAction.GetComponentsInChildren<Text>(true), Is.Empty, "The sprite-only primary action must not retain a legacy text or emoji layer.");
                Assert.That(dropAction.GetComponentsInChildren<Text>(true), Is.Empty, "The sprite-only drop action must not retain a legacy text layer.");
                Assert.That(dropAllAction.GetComponentsInChildren<Text>(true).Count(text => text.name == "All Label"), Is.EqualTo(1));

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

        [Test]
        public void EquippedItemsRemainInTheirCanonicalInventorySlots()
        {
            ItemDefinition item = AssetDatabase.FindAssets(
                    "t:ItemDefinition",
                    new[] { "Packages/com.thequantifier.isekai.content/Content" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                .First(candidate => candidate != null && candidate.IsEquippable);
            string equippedItemId = System.Guid.NewGuid().ToString();
            EquipmentSlotState equipped = CreateEquipmentState(item, equippedItemId);
            InventorySlot occupiedA = CreateOccupiedSlot(item, 1, equippedItemId);
            InventorySlot occupiedB = CreateOccupiedSlot(item);

            Dictionary<int, EquipmentSlotType> plan = InventoryDisplaySlotPlanner.Plan(
                new[] { occupiedA, occupiedB },
                new[] { equipped });
            Assert.That(plan.Single().Key, Is.EqualTo(0));

            string previousScenePath = SceneManager.GetActiveScene().path;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                InventoryScreenView view = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<InventoryScreenView>(true))
                    .Single();
                view.Initialize(null, null);
                int baseCount = view.BaseSlotCount;
                InventorySlot[] inventorySlots = Enumerable.Range(0, baseCount).Select(_ => CreateOccupiedSlot(item)).ToArray();
                inventorySlots[0] = occupiedA;
                view.Render(inventorySlots, new[] { equipped }, plan);

                Assert.That(view.SlotCount, Is.EqualTo(baseCount));
                InventorySlotView equippedView = view.GetComponentsInChildren<InventorySlotView>(true)
                    .Single(slot => slot.transform.Find("Equipped Marker")?.gameObject.activeSelf == true);
                Assert.That(equippedView.transform.Find("Equipped Marker")?.gameObject.activeSelf, Is.True);

                view.Render(Enumerable.Range(0, baseCount).Select(_ => new InventorySlot()).ToArray());
                Assert.That(view.SlotCount, Is.EqualTo(baseCount));

                view.EnsureInventorySlotCapacity(baseCount + 1);
                Assert.That(view.BaseSlotCount, Is.EqualTo(baseCount + 1));
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previousScenePath)) EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void EquipmentComparisonShowsCurrentAndColorCodedStatChanges()
        {
            StatModifiers equipped = CreateModifiers(maximumHealth: 1f, maximumMana: 1f, attackPower: 2f);
            StatModifiers desired = CreateModifiers(maximumHealth: 2f, maximumMana: -1f, attackPower: 2f, defense: 3f);

            string comparison = EquipmentComparisonFormatter.Format("Old <Sword>", equipped, "New & Better", desired);

            StringAssert.Contains("Equipped: Old &lt;Sword&gt;", comparison);
            StringAssert.Contains("Desired: New &amp; Better", comparison);
            StringAssert.Contains("Max Health: +1  <color=#73D67A>+1</color>", comparison);
            StringAssert.Contains("Max Mana: +1  <color=#FF6B6B>-2</color>", comparison);
            StringAssert.Contains("Attack: +2  <color=#D7B36A>+0</color>", comparison);
            StringAssert.Contains("Defense: +0  <color=#73D67A>+3</color>", comparison);
            StringAssert.DoesNotContain("Max Stamina", comparison);

            string emptyComparison = EquipmentComparisonFormatter.Format("Plain Item", default, "Other Plain Item", default);
            StringAssert.Contains("Neither item has numeric equipment bonuses to compare.", emptyComparison);
            StringAssert.DoesNotContain("Max Health:", emptyComparison);
            StringAssert.DoesNotContain("Attack:", emptyComparison);
        }

        [Test]
        public void EquipmentComparisonIncludesOnlyWeaponPropertiesPresentOnEitherItem()
        {
            ItemDefinition sword = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Packages/com.thequantifier.isekai.content/Content/Items/Definitions/PrototypeSword.asset");
            ItemDefinition bow = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Packages/com.thequantifier.isekai.content/Content/Items/Definitions/PrototypeBow.asset");

            Assert.That(sword, Is.Not.Null);
            Assert.That(bow, Is.Not.Null);
            string comparison = EquipmentComparisonFormatter.Format(sword, bow);

            StringAssert.Contains("Attack: +5  <color=#FF6B6B>-2</color>", comparison);
            StringAssert.Contains("Melee Damage: +10  <color=#FF6B6B>-10</color>", comparison);
            StringAssert.Contains("Ranged Damage: +0  <color=#73D67A>+8</color>", comparison);
            StringAssert.Contains("Projectile Speed: +0  <color=#73D67A>+26</color>", comparison);
            StringAssert.DoesNotContain("Max Health", comparison);
            StringAssert.DoesNotContain("Defense", comparison);
        }

        [Test]
        public void SelectedItemDetailsContainOnlyPlayerFacingStatsAndEffects()
        {
            ItemDefinition sword = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Packages/com.thequantifier.isekai.content/Content/Items/Definitions/PrototypeSword.asset");
            ItemDefinition potion = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Packages/com.thequantifier.isekai.content/Content/Items/Definitions/HealthPotion.asset");

            Assert.That(sword, Is.Not.Null);
            Assert.That(potion, Is.Not.Null);
            string equipmentDetails = InventoryItemDetailsFormatter.FormatDetails(sword, 1, "instance-1", true, includeDescription: true);
            string consumableDetails = InventoryItemDetailsFormatter.FormatDetails(potion, 2, string.Empty, false, includeDescription: true);

            StringAssert.Contains("STAT CHANGES", equipmentDetails);
            StringAssert.Contains("Melee Damage", equipmentDetails);
            StringAssert.Contains("SKILLS, TALENTS &amp; ABILITIES", equipmentDetails);
            StringAssert.Contains("Prototype Slash", equipmentDetails);
            StringAssert.Contains("EFFECTS", consumableDetails);
            StringAssert.Contains("Health", consumableDetails);
            StringAssert.DoesNotContain("Definition ID", equipmentDetails);
            StringAssert.DoesNotContain("Unique Instance ID", equipmentDetails);
            StringAssert.DoesNotContain("Tags", consumableDetails);
            StringAssert.DoesNotContain("Rarity", consumableDetails);
        }

        [Test]
        public void DurabilityColorMovesFromRedThroughYellowToGreen()
        {
            Color red = InventoryScreenView.GetDurabilityColor(0.1f);
            Color yellow = InventoryScreenView.GetDurabilityColor(0.5f);
            Color green = InventoryScreenView.GetDurabilityColor(1f);

            Assert.That(red.r, Is.GreaterThan(red.g));
            Assert.That(yellow.r, Is.GreaterThan(yellow.g));
            Assert.That(yellow.g, Is.GreaterThan(yellow.b));
            Assert.That(green.g, Is.GreaterThan(green.r));
            Assert.That(InventoryScreenView.GetDurabilityColor(-1f), Is.EqualTo(red));
            Assert.That(InventoryScreenView.GetDurabilityColor(2f), Is.EqualTo(green));
        }

        private static EquipmentSlotState CreateEquipmentState(ItemDefinition item, string itemInstanceId = null)
        {
            EquipmentSlotState state = new EquipmentSlotState();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(EquipmentSlotState).GetField("slotType", flags)?.SetValue(state, item.Equipment.SlotType);
            typeof(EquipmentSlotState).GetField("item", flags)?.SetValue(state, item);
            typeof(EquipmentSlotState).GetField("itemInstanceId", flags)?.SetValue(state, itemInstanceId ?? System.Guid.NewGuid().ToString());
            return state;
        }

        private static InventorySlot CreateOccupiedSlot(ItemDefinition item, int quantity = 1, string itemInstanceId = null)
        {
            InventorySlot slot = new InventorySlot();
            if (string.IsNullOrWhiteSpace(itemInstanceId))
            {
                typeof(InventorySlot).GetMethod("Set", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(slot, new object[] { item, quantity });
            }
            else
            {
                typeof(InventorySlot).GetMethod("SetIdentity", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(slot, new object[] { item, itemInstanceId, quantity });
            }
            return slot;
        }

        private static StatModifiers CreateModifiers(float maximumHealth = 0f, float maximumStamina = 0f, float maximumMana = 0f, float attackPower = 0f, float defense = 0f)
        {
            object modifiers = new StatModifiers();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(StatModifiers).GetField("maximumHealth", flags)?.SetValue(modifiers, maximumHealth);
            typeof(StatModifiers).GetField("maximumStamina", flags)?.SetValue(modifiers, maximumStamina);
            typeof(StatModifiers).GetField("maximumMana", flags)?.SetValue(modifiers, maximumMana);
            typeof(StatModifiers).GetField("attackPower", flags)?.SetValue(modifiers, attackPower);
            typeof(StatModifiers).GetField("defense", flags)?.SetValue(modifiers, defense);
            return (StatModifiers)modifiers;
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
