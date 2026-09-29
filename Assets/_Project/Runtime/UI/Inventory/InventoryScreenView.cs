using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.UI;
using CategoryDefinition = UnityIsekaiGame.GameData.CategoryDefinition;
using InventorySlot = UnityIsekaiGame.Inventory.InventorySlot;
using ItemDefinition = UnityIsekaiGame.Inventory.ItemDefinition;
using TagDefinition = UnityIsekaiGame.GameData.TagDefinition;
using ConsumablePresentationType = UnityIsekaiGame.Inventory.ConsumablePresentationType;

namespace UnityIsekaiGame.UI.Inventory
{
    public sealed class InventoryScreenView : MonoBehaviour
    {
        private const float NavigationButtonHeight = 42f;
        private const float NavigationButtonSpacing = 3f;
        private const float NavigationColumnInset = 18f;
        private const float NavigationColumnWidth = 202f;
        private const float InventoryActionButtonSize = 50f;
        private const float InventoryActionIconSize = 34f;
        private const float InventoryActionSpacing = 8f;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private InventorySlotView[] slotViews;
        [SerializeField] private EquipmentSlotView[] equipmentSlotViews;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button useButton;
        [SerializeField] private Button dropButton;
        [SerializeField] private Button dropAllButton;
        [SerializeField] private Button unequipButton;
        [SerializeField] private Sprite useEquipActionIcon;
        [SerializeField] private Sprite unequipActionIcon;
        [SerializeField] private Sprite dropActionIcon;
        [SerializeField] private Sprite consumeFoodActionIcon;
        [SerializeField] private Sprite consumePotionActionIcon;
        [SerializeField] private GameObject selectedItemDetailsRoot;
        [SerializeField] private Image selectedItemIconImage;
        [SerializeField] private Text selectedItemIconFallbackText;
        [SerializeField] private Text selectedItemHeaderText;
        [SerializeField] private GameObject selectedItemStatusRoot;
        [SerializeField] private Image selectedItemStatusBackground;
        [SerializeField] private Image selectedItemDurabilityFill;
        [SerializeField] private Text selectedItemStatusText;
        [SerializeField] private Text selectedItemDetailsText;
        [SerializeField] private GameObject equipmentComparisonTooltipRoot;
        [SerializeField] private Text equipmentComparisonTooltipText;
        [SerializeField] private GameObject inventoryContentRoot;
        [SerializeField] private GameObject characterContentRoot;
        [SerializeField] private GameObject characterStatsRoot;
        [SerializeField] private Text characterStatsText;
        [SerializeField] private StatusEffectReadoutView statusReadoutView;
        [SerializeField] private Text statusReadoutText;
        [SerializeField] private GameObject spellsContentRoot;
        [SerializeField] private GameObject contractsContentRoot;
        [SerializeField] private GameObject saveLoadContentRoot;
        [SerializeField] private SaveLoadMenuView saveLoadView;
        [SerializeField] private Button inventoryMenuButton;
        [SerializeField] private Button characterMenuButton;
        [SerializeField] private Button spellsMenuButton;
        [SerializeField] private Button contractsMenuButton;
        [SerializeField] private Button saveLoadMenuButton;
        [SerializeField] private Image inventoryMenuButtonImage;
        [SerializeField] private Image characterMenuButtonImage;
        [SerializeField] private Image spellsMenuButtonImage;
        [SerializeField] private Image contractsMenuButtonImage;
        [SerializeField] private Image saveLoadMenuButtonImage;
        [SerializeField] private Color inactiveMenuColor = new Color(0.26f, 0.17f, 0.1f, 0.98f);
        [SerializeField] private Color activeMenuColor = new Color(0.47f, 0.32f, 0.15f, 1f);

        private Action useSelected;
        private Action equipSelected;
        private Action dropSelected;
        private Action dropAllSelected;
        private Action unequipSelected;
        private bool primaryActionUsesItem;
        private bool primaryActionUnequipsItem;
        private InventoryMenuSection activeSection = InventoryMenuSection.Inventory;
        private InventoryMenuExtensionBinding activeExtension;
        private InventoryMenuSection appliedSection;
        private InventoryMenuExtensionBinding appliedExtension;
        private bool hasAppliedSection;
        private readonly List<InventoryMenuExtensionBinding> menuExtensions = new List<InventoryMenuExtensionBinding>();
        private PrototypePersistenceServiceBehaviour economyServices;

        public void ConfigureRuntimeServices(PrototypePersistenceServiceBehaviour services)
        {
            economyServices = services;
        }
        private ScrollRect selectedItemDetailsScroll;
        private ScrollRect equipmentComparisonTooltipScroll;
        private ScrollRect characterStatsScroll;
        private ScrollRect inventorySlotsScroll;
        private GridLayoutGroup inventorySlotGridLayout;
        private RectTransform inventorySlotGridRect;
        private GridLayoutGroup inventoryActionGridLayout;
        private RectTransform inventoryActionGridRect;
        private float lastInventoryGridWidth = -1f;
        private float lastInventoryGridHeight = -1f;
        private string inspectedItemKey = string.Empty;
        private Action<int> inventorySlotSelected;
        private Action<int, bool> inventorySlotHovered;
        private Action<bool> primaryActionHovered;
        private bool primaryActionShowsComparison;
        private bool primaryActionIsHovered;
        private EventTrigger.Entry primaryActionEnterEntry;
        private EventTrigger.Entry primaryActionExitEntry;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            inactiveMenuColor = GameUiTheme.PanelRaised;
            activeMenuColor = GameUiTheme.SlotSelected;
            EnsureItemDetailsPanel();
            ApplyProductionMenuLayout();
            EnsureResponsiveCanvas();
            ApplyTheme();
        }

        private void LateUpdate()
        {
            if (canvasGroup != null && canvasGroup.alpha <= 0f)
            {
                return;
            }

            UpdateResponsiveInventoryGrid();
        }

        public int BaseSlotCount => slotViews == null ? 0 : slotViews.Length;
        public int SlotCount => BaseSlotCount;
        public int InventoryColumnCount => inventorySlotGridLayout == null ? 1 : Mathf.Max(1, inventorySlotGridLayout.constraintCount);

        public static int CalculateInventoryColumnCount(float availableWidth, float spacing = 9f)
        {
            const float comfortableSlotWidth = 108f;
            return Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(0f, availableWidth) + spacing) / (comfortableSlotWidth + spacing)), 1, 5);
        }

        public static int CalculateActionColumnCount(float availableWidth, int visibleActionCount = 3)
        {
            int fittingColumns = Mathf.FloorToInt((Mathf.Max(0f, availableWidth) + InventoryActionSpacing) / (InventoryActionButtonSize + InventoryActionSpacing));
            return Mathf.Clamp(fittingColumns, 1, Mathf.Max(1, visibleActionCount));
        }

        public void ConfigureInventoryActionIcons(Sprite useEquip, Sprite unequip, Sprite drop, Sprite consumeFood, Sprite consumePotion)
        {
            useEquipActionIcon = useEquip;
            unequipActionIcon = unequip;
            dropActionIcon = drop;
            consumeFoodActionIcon = consumeFood;
            consumePotionActionIcon = consumePotion;
            RefreshInventoryActionIcons(primaryActionUnequipsItem);
        }

        public void EnsureInventorySlotCapacity(int requiredCount)
        {
            requiredCount = Mathf.Max(0, requiredCount);
            if (slotViews != null && slotViews.Length >= requiredCount && slotViews.All(view => view != null)) return;
            List<InventorySlotView> baseViews = slotViews == null
                ? new List<InventorySlotView>()
                : slotViews.Where(view => view != null).ToList();
            InventorySlotView template = baseViews.FirstOrDefault();
            while (baseViews.Count < requiredCount && template != null)
            {
                GameObject clone = Instantiate(template.gameObject, template.transform.parent);
                clone.name = $"Inventory Slot {baseViews.Count + 1}";
                clone.transform.SetSiblingIndex(baseViews.Count);
                InventorySlotView slotView = clone.GetComponent<InventorySlotView>();
                slotView.Initialize(baseViews.Count, inventorySlotSelected, inventorySlotHovered);
                baseViews.Add(slotView);
            }

            slotViews = baseViews.ToArray();
        }

        /// <summary>
        /// Creates and serializes the complete base menu hierarchy used at runtime, then applies the
        /// same layout and theme so the Scene and Game views are accurate outside Play Mode.
        /// Runtime-only menu extensions are still registered when play begins.
        /// </summary>
        public void BakeAuthoringLayout()
        {
            if (Application.isPlaying)
            {
                return;
            }

            inactiveMenuColor = GameUiTheme.PanelRaised;
            activeMenuColor = GameUiTheme.SlotSelected;
            EnsureSaveLoadMenuObjects();
            EnsureItemDetailsPanel();
            EnsureCharacterStatsPanel();
            ApplyProductionMenuLayout();
            EnsureResponsiveCanvas();
            ApplyTheme();

            activeSection = InventoryMenuSection.Inventory;
            activeExtension = null;
            hasAppliedSection = false;
            ApplyActiveSection(force: true);
            if (slotViews != null)
            {
                for (int i = 0; i < slotViews.Length; i++)
                {
                    slotViews[i]?.RenderEmpty();
                }
            }
            RenderSelectedItemDetails((InventorySlot)null);
            Canvas.ForceUpdateCanvases();
            UpdateResponsiveInventoryGrid(force: true);
        }

        public void Initialize(Action<int> onSlotSelected, Action onUseSelected, Action<EquipmentSlotType> onEquipmentSlotSelected = null, Action onEquipSelected = null, Action onUnequipSelected = null, Action<int, bool> onSlotHovered = null, Action onDropSelected = null, Action onDropAllSelected = null, Action<bool> onPrimaryActionHovered = null)
        {
            inventorySlotSelected = onSlotSelected;
            inventorySlotHovered = onSlotHovered;
            primaryActionHovered = onPrimaryActionHovered;
            if (slotViews != null)
            {
                for (int i = 0; i < slotViews.Length; i++)
                {
                    if (slotViews[i] != null)
                    {
                        slotViews[i].Initialize(i, onSlotSelected, onSlotHovered);
                    }
                }
            }

            if (useButton != null)
            {
                useButton.onClick.RemoveListener(InvokePrimarySelected);
                useButton.onClick.AddListener(InvokePrimarySelected);
            }

            useSelected = onUseSelected;
            ConfigurePrimaryActionHover();

            if (dropButton != null)
            {
                dropButton.onClick.RemoveListener(InvokeDropSelected);
                dropButton.onClick.AddListener(InvokeDropSelected);
            }

            dropSelected = onDropSelected;

            if (dropAllButton != null)
            {
                dropAllButton.onClick.RemoveListener(InvokeDropAllSelected);
                dropAllButton.onClick.AddListener(InvokeDropAllSelected);
            }

            dropAllSelected = onDropAllSelected;

            if (equipmentSlotViews != null)
            {
                for (int i = 0; i < equipmentSlotViews.Length; i++)
                {
                    if (equipmentSlotViews[i] != null)
                    {
                        equipmentSlotViews[i].Initialize((EquipmentSlotType)i, onEquipmentSlotSelected);
                    }
                }
            }

            if (unequipButton != null)
            {
                unequipButton.onClick.RemoveListener(InvokeUnequipSelected);
                unequipButton.onClick.AddListener(InvokeUnequipSelected);
            }

            if (inventoryMenuButton != null)
            {
                inventoryMenuButton.onClick.RemoveListener(ShowInventorySection);
                inventoryMenuButton.onClick.AddListener(ShowInventorySection);
            }

            if (characterMenuButton != null)
            {
                characterMenuButton.onClick.RemoveListener(ShowCharacterSection);
                characterMenuButton.onClick.AddListener(ShowCharacterSection);
            }

            if (spellsMenuButton != null)
            {
                spellsMenuButton.onClick.RemoveListener(ShowSpellsSection);
                spellsMenuButton.onClick.AddListener(ShowSpellsSection);
            }

            if (contractsMenuButton != null)
            {
                contractsMenuButton.onClick.RemoveListener(ShowContractsSection);
                contractsMenuButton.onClick.AddListener(ShowContractsSection);
            }

            EnsureSaveLoadMenuObjects();
            if (saveLoadMenuButton != null)
            {
                saveLoadMenuButton.onClick.RemoveListener(ShowSaveLoadSection);
                saveLoadMenuButton.onClick.AddListener(ShowSaveLoadSection);
            }

            equipSelected = onEquipSelected;
            unequipSelected = onUnequipSelected;
            EnsureItemDetailsPanel();
            ApplyProductionMenuLayout();
            ApplyTheme();
            ApplyActiveSection(force: true);
            Canvas.ForceUpdateCanvases();
            UpdateResponsiveInventoryGrid(force: true);
        }

        public void InitializeSaveLoad(PrototypePersistenceServiceBehaviour persistence)
        {
            EnsureSaveLoadMenuObjects();
            saveLoadView?.Initialize(persistence);
            ApplyActiveSection(force: true);
        }

        public void RefreshSaveLoad()
        {
            saveLoadView?.RefreshIfNeeded();
        }

        public bool RegisterMenuExtension(IInventoryMenuExtension extension)
        {
            if (extension == null || string.IsNullOrWhiteSpace(extension.ExtensionId))
            {
                return false;
            }

            for (int i = 0; i < menuExtensions.Count; i++)
            {
                if (menuExtensions[i].Extension == extension)
                {
                    return false;
                }

                if (menuExtensions[i].Extension.ExtensionId == extension.ExtensionId)
                {
                    return false;
                }
            }

            InventoryMenuExtensionBinding binding = CreateExtensionBinding(extension);
            menuExtensions.Add(binding);
            menuExtensions.Sort((left, right) =>
            {
                int orderComparison = left.Extension.Order.CompareTo(right.Extension.Order);
                return orderComparison != 0 ? orderComparison : string.CompareOrdinal(left.Extension.DisplayName, right.Extension.DisplayName);
            });

            extension.Initialize(new InventoryMenuExtensionContext(this, binding.ContentRoot, ResolveFont()));
            ApplyProductionMenuLayout();
            ApplyActiveSection(force: true);
            return true;
        }

        public bool UnregisterMenuExtension(IInventoryMenuExtension extension)
        {
            if (extension == null)
            {
                return false;
            }

            for (int i = 0; i < menuExtensions.Count; i++)
            {
                InventoryMenuExtensionBinding binding = menuExtensions[i];
                if (binding.Extension != extension)
                {
                    continue;
                }

                if (activeExtension == binding)
                {
                    activeExtension.Extension.Hide();
                    activeExtension = null;
                    activeSection = InventoryMenuSection.Inventory;
                }

                binding.Extension.Dispose();
                if (binding.Button != null)
                {
                    DestroyMenuObject(binding.Button.gameObject);
                }

                if (binding.ContentRoot != null)
                {
                    DestroyMenuObject(binding.ContentRoot.gameObject);
                }

                menuExtensions.RemoveAt(i);
                ApplyProductionMenuLayout();
                ApplyActiveSection(force: true);
                return true;
            }

            return false;
        }

        private static void DestroyMenuObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        public void RefreshMenuExtensions()
        {
            for (int i = 0; i < menuExtensions.Count; i++)
            {
                if (menuExtensions[i].Extension.IsAvailable)
                {
                    menuExtensions[i].Extension.Refresh();
                }
            }
        }

        public void RefreshActiveMenuExtension()
        {
            if (activeSection == InventoryMenuSection.Extension
                && activeExtension != null
                && activeExtension.Extension.IsAvailable)
            {
                activeExtension.Extension.Refresh();
            }
        }

        public void Render(IReadOnlyList<InventorySlot> slots, IReadOnlyList<EquipmentSlotState> equippedSlots = null, IReadOnlyDictionary<int, EquipmentSlotType> equippedDisplaySlots = null)
        {
            EnsureInventorySlotCapacity(slots?.Count ?? 0);
            if (slotViews == null)
            {
                return;
            }

            for (int i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] == null)
                {
                    continue;
                }

                if (equippedDisplaySlots != null && equippedDisplaySlots.TryGetValue(i, out EquipmentSlotType equippedSlotType))
                {
                    EquipmentSlotState equippedSlot = equippedSlots?.FirstOrDefault(slot => slot != null && slot.SlotType == equippedSlotType);
                    if (equippedSlot != null && !equippedSlot.IsEmpty)
                    {
                        slotViews[i].RenderEquipped(equippedSlot);
                        continue;
                    }
                }

                if (slots != null && i < slots.Count && slots[i] != null && !slots[i].IsEmpty)
                {
                    slotViews[i].Render(slots[i]);
                    continue;
                }

                slotViews[i].RenderEmpty();
            }
        }

        public void RenderSelectedItemDetails(InventorySlot slot, bool includeDescription = false)
        {
            if (slot == null || slot.IsEmpty || slot.Item == null)
            {
                RenderEmptyItemDetails();
                return;
            }

            RenderSelectedItemDetails(slot.Item, slot.Quantity, slot.ItemInstanceId, slot.IsStateful, includeDescription, equipped: false);
        }

        private void RenderEmptyItemDetails()
        {
            EnsureItemDetailsPanel();
            HideEquipmentComparison();
            if (selectedItemDetailsRoot != null) selectedItemDetailsRoot.SetActive(true);
            if (selectedItemIconImage != null)
            {
                selectedItemIconImage.sprite = null;
                selectedItemIconImage.enabled = false;
            }
            if (selectedItemIconFallbackText != null)
            {
                selectedItemIconFallbackText.gameObject.SetActive(true);
                selectedItemIconFallbackText.text = "?";
            }
            if (selectedItemHeaderText != null) selectedItemHeaderText.text = "Select an Item";
            if (selectedItemStatusRoot != null) selectedItemStatusRoot.SetActive(false);
            if (selectedItemDetailsText != null)
            {
                selectedItemDetailsText.text = "Choose an inventory slot to view its stats, effects, and available actions.";
            }

            inspectedItemKey = string.Empty;
            SetInventoryActions(canUse: false, canEquip: false, canDrop: false);
            Canvas.ForceUpdateCanvases();
            if (selectedItemDetailsScroll != null) selectedItemDetailsScroll.verticalNormalizedPosition = 1f;
        }

        public void RenderSelectedItemDetails(EquipmentSlotState slot, bool includeDescription = false)
        {
            if (slot == null || slot.IsEmpty || slot.Item == null)
            {
                RenderSelectedItemDetails((InventorySlot)null, includeDescription);
                return;
            }

            RenderSelectedItemDetails(slot.Item, 1, slot.ItemInstanceId, slot.IsStateful, includeDescription, equipped: true);
        }

        public void ShowEquipmentComparison(string comparison)
        {
            if (string.IsNullOrWhiteSpace(comparison))
            {
                HideEquipmentComparison();
                return;
            }

            EnsureItemDetailsPanel();
            if (equipmentComparisonTooltipRoot != null)
            {
                equipmentComparisonTooltipRoot.SetActive(true);
                equipmentComparisonTooltipRoot.transform.SetAsLastSibling();
            }
            if (equipmentComparisonTooltipText != null)
            {
                equipmentComparisonTooltipText.supportRichText = true;
                equipmentComparisonTooltipText.text = comparison;
            }

            Canvas.ForceUpdateCanvases();
            if (equipmentComparisonTooltipScroll != null) equipmentComparisonTooltipScroll.verticalNormalizedPosition = 1f;
        }

        public void HideEquipmentComparison()
        {
            if (equipmentComparisonTooltipRoot != null)
            {
                equipmentComparisonTooltipRoot.SetActive(false);
            }
        }

        private void RenderSelectedItemDetails(ItemDefinition item, int quantity, string itemInstanceId, bool isStateful, bool includeDescription, bool equipped)
        {

            EnsureItemDetailsPanel();
            HideEquipmentComparison();

            if (selectedItemDetailsRoot != null)
            {
                selectedItemDetailsRoot.SetActive(true);
            }

            Sprite itemIcon = InventoryItemIconResolver.Resolve(item);
            if (selectedItemIconImage != null)
            {
                selectedItemIconImage.sprite = itemIcon;
                selectedItemIconImage.enabled = itemIcon != null;
                selectedItemIconImage.preserveAspect = true;
            }

            if (selectedItemIconFallbackText != null)
            {
                selectedItemIconFallbackText.gameObject.SetActive(itemIcon == null);
                selectedItemIconFallbackText.text = GetItemMonogram(item);
            }

            if (selectedItemHeaderText != null)
            {
                selectedItemHeaderText.text = InventoryItemDetailsFormatter.GetHeader(item) + (equipped ? "  \u2022  Equipped" : string.Empty);
            }

            RenderSelectedItemStatus(item, quantity, itemInstanceId);

            if (selectedItemDetailsText != null)
            {
                selectedItemDetailsText.supportRichText = true;
                selectedItemDetailsText.text = InventoryItemDetailsFormatter.FormatDetails(item, quantity, itemInstanceId, isStateful, includeDescription, equipped);
            }

            string nextItemKey = string.IsNullOrWhiteSpace(itemInstanceId) ? item.ItemId : itemInstanceId;
            if (!string.Equals(inspectedItemKey, nextItemKey, StringComparison.Ordinal))
            {
                inspectedItemKey = nextItemKey ?? string.Empty;
                Canvas.ForceUpdateCanvases();
                if (selectedItemDetailsScroll != null)
                {
                    selectedItemDetailsScroll.verticalNormalizedPosition = 1f;
                }
            }
        }

        public void SetSelectedSlot(int selectedIndex)
        {
            if (slotViews == null)
            {
                return;
            }

            for (int i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] != null)
                {
                    slotViews[i].SetSelected(i == selectedIndex);
                }
            }

        }

        private void RenderSelectedItemStatus(ItemDefinition item, int quantity, string itemInstanceId)
        {
            if (selectedItemStatusRoot == null || item == null)
            {
                return;
            }

            bool showDurability = item.IsEquippable;
            bool showStack = !showDurability && (item.IsUsable || item.Stackable);
            selectedItemStatusRoot.SetActive(showDurability || showStack);
            if (!showDurability && !showStack)
            {
                return;
            }

            if (showDurability)
            {
                float normalizedDurability = ResolveDurability(itemInstanceId);
                int durabilityPercent = Mathf.RoundToInt(normalizedDurability * 100f);
                if (selectedItemDurabilityFill != null)
                {
                    selectedItemDurabilityFill.gameObject.SetActive(true);
                    selectedItemDurabilityFill.fillAmount = normalizedDurability;
                    selectedItemDurabilityFill.color = GetDurabilityColor(normalizedDurability);
                }

                if (selectedItemStatusBackground != null)
                {
                    selectedItemStatusBackground.color = GameUiTheme.SurfaceInset;
                }

                if (selectedItemStatusText != null)
                {
                    selectedItemStatusText.text = $"Durability  {durabilityPercent}%";
                }

                return;
            }

            if (selectedItemDurabilityFill != null)
            {
                selectedItemDurabilityFill.gameObject.SetActive(false);
            }

            if (selectedItemStatusBackground != null)
            {
                selectedItemStatusBackground.color = GameUiTheme.PanelRaised;
            }

            if (selectedItemStatusText != null)
            {
                int maximum = item.Stackable ? item.MaximumStackSize : 1;
                selectedItemStatusText.text = item.Stackable
                    ? $"Stack  {Mathf.Max(0, quantity)} / {maximum}"
                    : $"Stack  {Mathf.Max(0, quantity)}";
            }
        }

        private float ResolveDurability(string itemInstanceId)
        {
            if (string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return 1f;
            }

            return economyServices != null
                && economyServices.ItemDurability.TryGetDurabilityForItem(itemInstanceId, out UnityIsekaiGame.Inventory.Durability.ItemDurabilitySnapshot durability)
                    ? Mathf.Clamp01(durability.NormalizedDurability)
                    : 1f;
        }

        public static Color GetDurabilityColor(float normalizedDurability)
        {
            float value = Mathf.Clamp01(normalizedDurability);
            Color red = new Color(0.86f, 0.20f, 0.16f, 1f);
            Color yellow = new Color(0.95f, 0.72f, 0.12f, 1f);
            Color green = new Color(0.25f, 0.72f, 0.29f, 1f);
            if (value <= 0.1f)
            {
                return red;
            }

            return value <= 0.5f
                ? Color.Lerp(red, yellow, (value - 0.1f) / 0.4f)
                : Color.Lerp(yellow, green, (value - 0.5f) / 0.5f);
        }

        public void RenderEquipment(IReadOnlyList<EquipmentSlotState> equipmentSlots)
        {
            if (equipmentSlotViews == null)
            {
                return;
            }

            for (int i = 0; i < equipmentSlotViews.Length; i++)
            {
                if (equipmentSlotViews[i] == null)
                {
                    continue;
                }

                equipmentSlotViews[i].Render(equipmentSlots != null && i < equipmentSlots.Count ? equipmentSlots[i] : null);
            }
        }

        public void RenderCharacter(
            PlayerStats stats,
            PlayerHealth health,
            PlayerStamina stamina,
            PlayerMana mana,
            StatusEffectController statusEffects,
            CharacterAttributes attributes = null,
            CalculatedStatCollection calculatedStats = null,
            CharacterSkillCollection skills = null,
            CharacterTraitCollection traits = null,
            CharacterFullSnapshot characterSnapshot = null)
        {
            EnsureCharacterStatsPanel();

            if (characterStatsRoot != null)
            {
                characterStatsRoot.SetActive(true);
            }

            if (characterStatsText == null)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            if (characterSnapshot != null)
            {
                builder.AppendLine("Identity");
                AppendLine(builder, "Readiness", characterSnapshot.Identity.Readiness.ToString());
                AppendLine(builder, "Revision", characterSnapshot.Revision.ToString());
                AppendLine(builder, "Player", string.IsNullOrWhiteSpace(characterSnapshot.Identity.PlayerId) ? "--" : characterSnapshot.Identity.PlayerId);
                AppendLine(builder, "Person", string.IsNullOrWhiteSpace(characterSnapshot.Identity.PersonId) ? "--" : characterSnapshot.Identity.PersonId);
                AppendLine(builder, "Actor", string.IsNullOrWhiteSpace(characterSnapshot.Identity.ActorId) ? "--" : characterSnapshot.Identity.ActorId);
                AppendLine(builder, "Origin", string.IsNullOrWhiteSpace(characterSnapshot.Identity.OriginId) ? "Unassigned" : FormatDefinitionName(characterSnapshot.Identity.OriginId));
                AppendLine(builder, "Birth Gift", string.IsNullOrWhiteSpace(characterSnapshot.Identity.BirthGiftId) ? "None" : FormatDefinitionName(characterSnapshot.Identity.BirthGiftId));
                AppendLine(builder, "Overall Level", characterSnapshot.Progression.OverallLevel.OverallLevel.ToString());
                builder.AppendLine();
            }

            builder.AppendLine("Vitals");
            AppendLine(builder, "Health", health == null ? "--" : $"{FormatNumber(health.CurrentHealth)}/{FormatNumber(health.MaximumHealth)}");
            AppendLine(builder, "Stamina", stamina == null ? "--" : $"{FormatNumber(stamina.CurrentStamina)}/{FormatNumber(stamina.MaximumStamina)}");
            AppendLine(builder, "Mana", mana == null ? "--" : $"{FormatNumber(mana.CurrentMana)}/{FormatNumber(mana.MaximumMana)}");

            builder.AppendLine();
            builder.AppendLine("Combat Summary");
            AppendLine(builder, "Physical Power", stats == null ? "--" : FormatNumber(stats.AttackPower));
            AppendLine(builder, "Physical Defense", stats == null ? "--" : FormatNumber(stats.Defense));

            if (attributes != null && attributes.IsConfigured)
            {
                builder.AppendLine();
                builder.AppendLine("Base Attributes");
                List<string> parts = new List<string>();
                foreach (RuntimeAttributeValueRecord record in attributes.GetOrderedValues())
                {
                    parts.Add($"{FormatDefinitionName(record.attributeId)} {Mathf.FloorToInt(record.currentValue)}");
                }

                AppendCompactPairs(builder, parts);
            }

            if (calculatedStats != null && calculatedStats.IsConfigured)
            {
                builder.AppendLine();
                builder.AppendLine("Calculated Stats");
                List<string> parts = new List<string>();
                foreach (CalculatedStatDefinition definition in calculatedStats.GetOrderedDefinitions(characterMenuOnly: true))
                {
                    string resource = definition.IsResourceMaximum ? $" [{definition.LinkedResourceId} max]" : string.Empty;
                    parts.Add($"{definition.DisplayName} {FormatNumber(calculatedStats.GetValue(definition.Id))}{resource}");
                }

                AppendCompactPairs(builder, parts);
            }

            if (skills != null)
            {
                builder.AppendLine();
                builder.AppendLine("Skills");
                IReadOnlyList<RuntimeSkillRecord> learned = skills.LearnedSkills;
                if (learned == null || learned.Count == 0)
                {
                    builder.AppendLine("None");
                }
                else
                {
                    List<string> parts = new List<string>();
                    foreach (RuntimeSkillRecord record in learned)
                    {
                        SkillGrade grade = SkillGradeUtility.Clamp((SkillGrade)record.currentGrade);
                        string progress = grade == SkillGrade.AAA ? "Mastered" : $"{record.currentXp} XP";
                        parts.Add($"{FormatDefinitionName(record.skillDefinitionId)} {grade} ({progress})");
                    }

                    AppendCompactPairs(builder, parts);
                }
            }

            if (traits != null)
            {
                builder.AppendLine();
                builder.AppendLine("Traits");
                IReadOnlyList<TraitSnapshot> knownTraits = traits.GetKnownTraits();
                if (knownTraits == null || knownTraits.Count == 0)
                {
                    builder.AppendLine("None");
                }
                else
                {
                    List<string> parts = new List<string>();
                    foreach (TraitSnapshot snapshot in knownTraits)
                    {
                        RuntimeTraitRecord record = snapshot.Record;
                        parts.Add($"{snapshot.PresentationName} {(TraitLifecycleState)record.lifecycleState}");
                    }

                    AppendCompactPairs(builder, parts);
                }
            }

            if (characterSnapshot != null)
            {
                builder.AppendLine();
                builder.AppendLine("Social");
                AppendLine(builder, "Roles", FormatRecordIds(characterSnapshot.Social.Roles, role => role.roleDefinitionId));
                AppendLine(builder, "Statuses", FormatRecordIds(characterSnapshot.Social.SocialStatuses, status => status.socialStatusDefinitionId));
                AppendLine(builder, "Titles", FormatRecordIds(characterSnapshot.Social.Titles, title => title.titleDefinitionId));
                AppendLine(builder, "Wallet", economyServices == null ? "Unavailable" : $"Gold {economyServices.GetPlayerBalance()}");
                AppendLine(builder, "Capabilities", characterSnapshot.Capabilities.Capabilities.Count.ToString());
            }

            characterStatsText.text = builder.ToString().TrimEnd();
            statusReadoutView?.SetStatusController(statusEffects);
        }

        private static string FormatRecordIds<T>(IReadOnlyList<T> records, Func<T, string> selector)
        {
            if (records == null || records.Count == 0)
            {
                return "None";
            }

            return string.Join(", ", records.Select(record => FormatDefinitionName(selector(record))).ToArray());
        }

        public void SetSelectedEquipmentSlot(EquipmentSlotType selectedSlot)
        {
            if (equipmentSlotViews == null)
            {
                return;
            }

            for (int i = 0; i < equipmentSlotViews.Length; i++)
            {
                if (equipmentSlotViews[i] != null)
                {
                    equipmentSlotViews[i].SetSelected(i == (int)selectedSlot);
                }
            }
        }

        public void SetEquipmentActions(bool canEquip, bool canUnequip)
        {
            if (unequipButton != null)
            {
                unequipButton.gameObject.SetActive(canUnequip);
            }
        }

        public void SetInventoryActions(bool canUse, bool canEquip, bool canDrop, bool canDropAll = false, bool canUnequip = false, ItemDefinition actionItem = null)
        {
            bool previousComparisonState = primaryActionShowsComparison;
            primaryActionUsesItem = canUse;
            primaryActionUnequipsItem = canUnequip;
            primaryActionShowsComparison = canEquip && !canUse && !canUnequip;
            if (!primaryActionShowsComparison)
            {
                HideEquipmentComparison();
            }
            if (primaryActionIsHovered && previousComparisonState != primaryActionShowsComparison)
                primaryActionHovered?.Invoke(primaryActionShowsComparison);
            if (useButton != null)
            {
                bool hasPrimaryAction = canUse || canEquip || canUnequip;
                useButton.gameObject.SetActive(hasPrimaryAction);
                useButton.interactable = canUse || canEquip || canUnequip;
                useButton.name = canUnequip ? "Unequip Item Action Button" : canUse ? "Use Item Action Button" : "Equip Item Action Button";
                SetInventoryActionIcon(useButton, canUnequip ? unequipActionIcon : canUse ? ResolveConsumeActionIcon(actionItem) : useEquipActionIcon);
            }

            if (dropButton != null)
            {
                dropButton.gameObject.SetActive(canDrop);
                dropButton.interactable = canDrop;
            }

            if (dropAllButton != null)
            {
                dropAllButton.gameObject.SetActive(canDropAll);
                dropAllButton.interactable = canDropAll;
            }

            UpdateResponsiveItemActions();
        }

        public void SetFeedback(string message)
        {
            if (feedbackText != null)
            {
                feedbackText.text = message;
            }
        }

        public void Show()
        {
            ApplyActiveSection();
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (!visible && primaryActionIsHovered)
            {
                primaryActionIsHovered = false;
                primaryActionHovered?.Invoke(false);
            }
            if (!visible)
            {
                HideEquipmentComparison();
            }
            if (canvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void InvokePrimarySelected()
        {
            if (primaryActionUnequipsItem)
            {
                unequipSelected?.Invoke();
            }
            else if (primaryActionUsesItem)
            {
                useSelected?.Invoke();
            }
            else
            {
                equipSelected?.Invoke();
            }
        }

        private void ConfigurePrimaryActionHover()
        {
            if (useButton == null) return;
            EventTrigger trigger = useButton.GetComponent<EventTrigger>();
            if (trigger == null) trigger = useButton.gameObject.AddComponent<EventTrigger>();
            trigger.triggers ??= new List<EventTrigger.Entry>();
            if (primaryActionEnterEntry != null) trigger.triggers.Remove(primaryActionEnterEntry);
            if (primaryActionExitEntry != null) trigger.triggers.Remove(primaryActionExitEntry);

            primaryActionEnterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            primaryActionEnterEntry.callback.AddListener(_ =>
            {
                primaryActionIsHovered = true;
                primaryActionHovered?.Invoke(primaryActionShowsComparison);
            });
            trigger.triggers.Add(primaryActionEnterEntry);

            primaryActionExitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            primaryActionExitEntry.callback.AddListener(_ =>
            {
                primaryActionIsHovered = false;
                primaryActionHovered?.Invoke(false);
            });
            trigger.triggers.Add(primaryActionExitEntry);
        }

        private void InvokeDropSelected()
        {
            dropSelected?.Invoke();
        }

        private void InvokeDropAllSelected()
        {
            dropAllSelected?.Invoke();
        }

        private void InvokeUnequipSelected()
        {
            unequipSelected?.Invoke();
        }

        private void ShowInventorySection()
        {
            SelectSection(InventoryMenuSection.Inventory);
        }

        private void ShowSpellsSection()
        {
            SelectSection(InventoryMenuSection.Spells);
        }

        private void ShowCharacterSection()
        {
            SelectSection(InventoryMenuSection.Character);
        }

        private void ShowContractsSection()
        {
            SelectSection(InventoryMenuSection.Contracts);
        }

        private void ShowSaveLoadSection()
        {
            if (SelectSection(InventoryMenuSection.SaveLoad))
            {
                saveLoadView?.RefreshIfNeeded();
            }
        }

        private bool SelectSection(InventoryMenuSection section)
        {
            if (activeSection == section && hasAppliedSection)
            {
                return false;
            }

            activeSection = section;
            ApplyActiveSection();
            return true;
        }

        private void ApplyActiveSection(bool force = false)
        {
            if (!force
                && hasAppliedSection
                && appliedSection == activeSection
                && appliedExtension == activeExtension)
            {
                return;
            }

            bool inventoryActive = activeSection == InventoryMenuSection.Inventory;
            bool characterActive = activeSection == InventoryMenuSection.Character;
            bool spellsActive = activeSection == InventoryMenuSection.Spells;
            bool contractsActive = activeSection == InventoryMenuSection.Contracts;
            bool saveLoadActive = activeSection == InventoryMenuSection.SaveLoad;
            bool extensionActive = activeSection == InventoryMenuSection.Extension && activeExtension != null;

            if (inventoryContentRoot != null)
            {
                inventoryContentRoot.SetActive(inventoryActive);
            }

            if (characterContentRoot != null)
            {
                characterContentRoot.SetActive(characterActive);
            }

            if (spellsContentRoot != null)
            {
                spellsContentRoot.SetActive(spellsActive);
            }

            if (contractsContentRoot != null)
            {
                contractsContentRoot.SetActive(contractsActive);
            }

            if (saveLoadContentRoot != null)
            {
                saveLoadContentRoot.SetActive(saveLoadActive);
            }

            for (int i = 0; i < menuExtensions.Count; i++)
            {
                InventoryMenuExtensionBinding binding = menuExtensions[i];
                bool isActive = extensionActive && binding == activeExtension && binding.Extension.IsAvailable;
                if (binding.ContentRoot != null)
                {
                    binding.ContentRoot.gameObject.SetActive(isActive);
                }

                if (isActive)
                {
                    binding.Extension.Show();
                    binding.Extension.Refresh();
                }
                else
                {
                    binding.Extension.Hide();
                }
            }

            if (feedbackText != null)
            {
                bool suppressFeedback = extensionActive && activeExtension.Extension.SuppressFeedbackText;
                feedbackText.gameObject.SetActive(!suppressFeedback);
            }

            if (inventoryMenuButtonImage != null)
            {
                inventoryMenuButtonImage.color = inventoryActive ? activeMenuColor : inactiveMenuColor;
            }

            if (characterMenuButtonImage != null)
            {
                characterMenuButtonImage.color = characterActive ? activeMenuColor : inactiveMenuColor;
            }

            if (spellsMenuButtonImage != null)
            {
                spellsMenuButtonImage.color = spellsActive ? activeMenuColor : inactiveMenuColor;
            }

            if (contractsMenuButtonImage != null)
            {
                contractsMenuButtonImage.color = contractsActive ? activeMenuColor : inactiveMenuColor;
            }

            if (saveLoadMenuButtonImage != null)
            {
                saveLoadMenuButtonImage.color = saveLoadActive ? activeMenuColor : inactiveMenuColor;
            }

            for (int i = 0; i < menuExtensions.Count; i++)
            {
                InventoryMenuExtensionBinding binding = menuExtensions[i];
                if (binding.ButtonImage != null)
                {
                    binding.ButtonImage.color = extensionActive && binding == activeExtension ? activeMenuColor : inactiveMenuColor;
                }
            }

            appliedSection = activeSection;
            appliedExtension = activeExtension;
            hasAppliedSection = true;
        }

        private void ApplyProductionMenuLayout()
        {
            Transform navigationParent = characterMenuButton == null ? inventoryMenuButton == null ? null : inventoryMenuButton.transform.parent : characterMenuButton.transform.parent;
            if (navigationParent != null)
            {
                VerticalLayoutGroup verticalLayout = navigationParent.GetComponent<VerticalLayoutGroup>();
                if (verticalLayout != null)
                {
                    verticalLayout.enabled = false;
                }

                if (navigationParent is RectTransform navigationRect)
                {
                    navigationRect.anchorMin = new Vector2(1f, 0f);
                    navigationRect.anchorMax = Vector2.one;
                    navigationRect.pivot = new Vector2(1f, 0.5f);
                    navigationRect.offsetMin = new Vector2(-NavigationColumnWidth, 60f);
                    navigationRect.offsetMax = new Vector2(-NavigationColumnInset, -82f);
                }

                if (navigationParent.TryGetComponent(out Image navigationImage))
                {
                    GameUiTheme.StylePanel(navigationImage, raised: true);
                    navigationImage.color = GameUiTheme.PanelRaised;
                    navigationImage.raycastTarget = false;
                }
            }

            ConfigureNavigationButton(inventoryMenuButton, "Inventory", 0);
            ConfigureNavigationButton(characterMenuButton, "Character", 1);
            ConfigureNavigationButton(spellsMenuButton, "Spells", 2);
            ConfigureNavigationButton(contractsMenuButton, "Journal", 3);
            ConfigureNavigationButton(saveLoadMenuButton, "Save/Load", 4);

            for (int i = 0; i < menuExtensions.Count; i++)
            {
                ConfigureNavigationButton(menuExtensions[i].Button, menuExtensions[i].Extension.DisplayName, 5 + i);
            }

            ConfigureNavigationStrip(navigationParent);

            ConfigureMainPanel();
            ConfigureInventorySectionLayout();
            ConfigureCharacterSectionLayout();
            UpdateResponsiveInventoryGrid(force: true);
        }

        private void EnsureResponsiveCanvas()
        {
            Canvas canvas = GetComponentInParent<Canvas>(true);
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            {
                return;
            }

            canvas = GameUiThemeApplicator.FindRootScreenSpaceCanvas(canvas);

            GameUiThemeApplicator applicator = canvas.GetComponent<GameUiThemeApplicator>();
            if (applicator == null)
            {
                applicator = canvas.gameObject.AddComponent<GameUiThemeApplicator>();
            }

            GameUiSafeArea safeArea = canvas.GetComponent<GameUiSafeArea>();
            if (safeArea == null)
            {
                safeArea = canvas.gameObject.AddComponent<GameUiSafeArea>();
            }

            safeArea.CaptureCurrentLayout();
            applicator.RequestRefresh();
        }

        private static void ConfigureNavigationStrip(Transform navigationParent)
        {
            if (navigationParent == null)
            {
                return;
            }

            List<Button> buttons = new List<Button>();
            for (int i = 0; i < navigationParent.childCount; i++)
            {
                Button button = navigationParent.GetChild(i).GetComponent<Button>();
                if (button != null && button.gameObject.activeSelf)
                {
                    buttons.Add(button);
                }
            }

            if (buttons.Count == 0)
            {
                return;
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                RectTransform rect = buttons[i].GetComponent<RectTransform>();
                if (rect == null)
                {
                    continue;
                }

                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * (NavigationButtonHeight + NavigationButtonSpacing));
                rect.sizeDelta = new Vector2(0f, NavigationButtonHeight);
            }
        }

        private void ConfigureMainPanel()
        {
            Transform contentParent = inventoryContentRoot == null ? null : inventoryContentRoot.transform.parent;
            Transform panelTransform = contentParent == null ? null : contentParent.parent;
            GameObject panelObject = panelTransform == null ? gameObject : panelTransform.gameObject;

            if (panelTransform is RectTransform panelRect)
            {
                panelRect.anchorMin = new Vector2(0.18f, 0.14f);
                panelRect.anchorMax = new Vector2(0.92f, 0.86f);
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
            }

            if (panelObject.TryGetComponent(out Image panelImage))
            {
                GameUiTheme.StylePanel(panelImage);
                panelImage.color = GameUiTheme.Panel;
                Outline panelOutline = panelObject.GetComponent<Outline>();
                if (panelOutline != null)
                {
                    panelOutline.enabled = false;
                }
                EnsureAccentBand(panelTransform, "Top Gold Accent", GameUiTheme.Accent, 4f);
            }

            if (contentParent is RectTransform contentRect)
            {
                contentRect.anchorMin = Vector2.zero;
                contentRect.anchorMax = Vector2.one;
                contentRect.offsetMin = new Vector2(18f, 60f);
                contentRect.offsetMax = new Vector2(-NavigationColumnWidth, -82f);
                if (contentParent.TryGetComponent(out Image contentImage))
                {
                    GameUiTheme.StylePanel(contentImage);
                    contentImage.color = new Color(GameUiTheme.Panel.r, GameUiTheme.Panel.g, GameUiTheme.Panel.b, 0.78f);
                }
            }

            if (feedbackText != null)
            {
                if (string.IsNullOrWhiteSpace(feedbackText.text) || feedbackText.text == "Select an item and press Use.")
                {
                    feedbackText.text = "Select an item to view its details and available actions.";
                }
                RectTransform feedbackRect = feedbackText.rectTransform;
                feedbackRect.anchorMin = new Vector2(0f, 0f);
                feedbackRect.anchorMax = new Vector2(1f, 0f);
                feedbackRect.pivot = new Vector2(0.5f, 0f);
                feedbackRect.offsetMin = new Vector2(24f, 13f);
                feedbackRect.offsetMax = new Vector2(-24f, 51f);
                feedbackText.alignment = TextAnchor.MiddleLeft;
                feedbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
                feedbackText.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private void ConfigureInventorySectionLayout()
        {
            if (inventoryContentRoot == null)
            {
                return;
            }

            Text title = inventoryContentRoot.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(candidate => candidate != null && candidate.name.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0);
            if (title != null)
            {
                title.text = "INVENTORY";
                title.alignment = TextAnchor.MiddleLeft;
                title.fontSize = 24;
                title.fontStyle = FontStyle.Bold;
                RectTransform titleRect = title.rectTransform;
                titleRect.anchorMin = new Vector2(0.02f, 0.89f);
                titleRect.anchorMax = new Vector2(0.63f, 0.985f);
                titleRect.offsetMin = Vector2.zero;
                titleRect.offsetMax = Vector2.zero;
            }

            inventorySlotGridLayout = FindInventorySlotGrid();
            inventorySlotGridRect = inventorySlotGridLayout == null ? null : inventorySlotGridLayout.GetComponent<RectTransform>();
            if (inventorySlotGridLayout != null && inventorySlotGridRect != null)
            {
                EnsureInventorySlotsScroll();
                inventorySlotGridRect.anchorMin = new Vector2(0f, 1f);
                inventorySlotGridRect.anchorMax = Vector2.one;
                inventorySlotGridRect.pivot = new Vector2(0.5f, 1f);
                inventorySlotGridRect.anchoredPosition = Vector2.zero;
                inventorySlotGridLayout.padding = new RectOffset(0, 0, 0, 0);
                inventorySlotGridLayout.spacing = new Vector2(9f, 9f);
                inventorySlotGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                inventorySlotGridLayout.constraintCount = 5;
                inventorySlotGridLayout.childAlignment = TextAnchor.UpperLeft;
                const float initialSquareSize = 108f;
                inventorySlotGridLayout.cellSize = new Vector2(initialSquareSize, initialSquareSize);
            }

            EnsureItemDetailsPanel();
        }

        private void ConfigureCharacterSectionLayout()
        {
            if (characterStatsRoot == null)
            {
                return;
            }

            RectTransform statsRect = characterStatsRoot.GetComponent<RectTransform>();
            if (statsRect != null)
            {
                statsRect.anchorMin = new Vector2(0.49f, 0.04f);
                statsRect.anchorMax = new Vector2(0.985f, 0.96f);
                statsRect.pivot = new Vector2(0.5f, 0.5f);
                statsRect.offsetMin = Vector2.zero;
                statsRect.offsetMax = Vector2.zero;
            }
        }

        private void UpdateResponsiveInventoryGrid(bool force = false)
        {
            if (inventorySlotGridLayout == null || inventorySlotGridRect == null)
            {
                if (inventoryContentRoot == null)
                {
                    return;
                }

                inventorySlotGridLayout = FindInventorySlotGrid();
                inventorySlotGridRect = inventorySlotGridLayout == null ? null : inventorySlotGridLayout.GetComponent<RectTransform>();
            }

            if (inventorySlotGridLayout == null || inventorySlotGridRect == null)
            {
                return;
            }

            RectTransform viewport = inventorySlotsScroll == null ? null : inventorySlotsScroll.viewport;
            float width = viewport == null ? inventorySlotGridRect.rect.width : viewport.rect.width;
            float height = viewport == null ? inventorySlotGridRect.rect.height : viewport.rect.height;
            if (width <= 1f || height <= 1f)
            {
                return;
            }

            if (!force && Mathf.Abs(width - lastInventoryGridWidth) < 0.5f && Mathf.Abs(height - lastInventoryGridHeight) < 0.5f)
            {
                return;
            }

            lastInventoryGridWidth = width;
            lastInventoryGridHeight = height;
            UpdateResponsiveItemActions();
            Vector2 spacing = inventorySlotGridLayout.spacing;
            int columnCount = CalculateInventoryColumnCount(width, spacing.x);
            inventorySlotGridLayout.constraintCount = columnCount;
            float cellWidth = Mathf.Max(72f, Mathf.Floor((width - spacing.x * (columnCount - 1)) / columnCount));
            float cellSize = cellWidth;
            inventorySlotGridLayout.cellSize = new Vector2(cellSize, cellSize);
            int childCount = BaseSlotCount;
            int rowCount = Mathf.Max(1, Mathf.CeilToInt(childCount / (float)columnCount));
            float contentHeight = Mathf.Max(height, rowCount * cellSize + Mathf.Max(0, rowCount - 1) * spacing.y);
            inventorySlotGridRect.sizeDelta = new Vector2(0f, contentHeight);
        }

        private GridLayoutGroup FindInventorySlotGrid()
        {
            if (inventoryContentRoot == null) return null;
            return inventoryContentRoot.GetComponentsInChildren<GridLayoutGroup>(true)
                .FirstOrDefault(candidate => candidate != null && candidate.GetComponentInChildren<InventorySlotView>(true) != null);
        }

        private void UpdateResponsiveItemActions()
        {
            if (inventoryActionGridLayout == null || inventoryActionGridRect == null) return;
            float width = inventoryActionGridRect.rect.width;
            float height = inventoryActionGridRect.rect.height;
            if (width <= 1f || height <= 1f) return;

            int visibleActionCount = 0;
            if (useButton != null && useButton.gameObject.activeSelf) visibleActionCount++;
            if (dropButton != null && dropButton.gameObject.activeSelf) visibleActionCount++;
            if (dropAllButton != null && dropAllButton.gameObject.activeSelf) visibleActionCount++;
            visibleActionCount = Mathf.Max(1, visibleActionCount);
            int columns = CalculateActionColumnCount(width, visibleActionCount);
            inventoryActionGridLayout.constraintCount = columns;
            inventoryActionGridLayout.cellSize = new Vector2(InventoryActionButtonSize, InventoryActionButtonSize);
        }

        private void EnsureInventorySlotsScroll()
        {
            if (inventoryContentRoot == null || inventorySlotGridRect == null) return;

            Transform existing = inventoryContentRoot.transform.Find("Inventory Slots Scroll");
            GameObject scrollObject;
            if (existing == null)
            {
                scrollObject = new GameObject("Inventory Slots Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
                scrollObject.transform.SetParent(inventoryContentRoot.transform, false);
            }
            else
            {
                scrollObject = existing.gameObject;
            }

            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.02f, 0.14f);
            scrollRect.anchorMax = new Vector2(0.63f, 0.885f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            Image background = scrollObject.GetComponent<Image>();
            GameUiTheme.StylePanel(background);
            background.color = new Color(GameUiTheme.SurfaceInset.r, GameUiTheme.SurfaceInset.g, GameUiTheme.SurfaceInset.b, 0.48f);

            Transform viewportTransform = scrollObject.transform.Find("Viewport");
            GameObject viewportObject;
            if (viewportTransform == null)
            {
                viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                viewportObject.transform.SetParent(scrollObject.transform, false);
            }
            else
            {
                viewportObject = viewportTransform.gameObject;
            }

            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(8f, 8f);
            viewport.offsetMax = new Vector2(-30f, -8f);
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
            viewportImage.raycastTarget = true;

            if (inventorySlotGridRect.parent != viewport)
            {
                inventorySlotGridRect.SetParent(viewport, false);
            }

            inventorySlotsScroll = scrollObject.GetComponent<ScrollRect>();
            inventorySlotsScroll.viewport = viewport;
            inventorySlotsScroll.content = inventorySlotGridRect;
            inventorySlotsScroll.horizontal = false;
            inventorySlotsScroll.vertical = true;
            inventorySlotsScroll.movementType = ScrollRect.MovementType.Clamped;
            inventorySlotsScroll.scrollSensitivity = 34f;
            EnsureVerticalScrollbar(inventorySlotsScroll, viewport);
        }

        private static void EnsureOutline(GameObject target, Color color)
        {
            if (target == null)
            {
                return;
            }

            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        private static void EnsureAccentBand(Transform parent, string objectName, Color color, float height, float normalizedY = 1f)
        {
            if (parent == null) return;
            Transform existing = parent.Find(objectName);
            GameObject bandObject;
            if (existing == null)
            {
                bandObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
                bandObject.transform.SetParent(parent, false);
            }
            else
            {
                bandObject = existing.gameObject;
            }

            RectTransform rect = bandObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, normalizedY);
            rect.anchorMax = new Vector2(1f, normalizedY);
            rect.pivot = new Vector2(0.5f, normalizedY >= 0.999f ? 1f : 0.5f);
            rect.offsetMin = new Vector2(0f, normalizedY >= 0.999f ? -height : -height * 0.5f);
            rect.offsetMax = new Vector2(0f, normalizedY >= 0.999f ? 0f : height * 0.5f);
            Image image = bandObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            bandObject.transform.SetAsLastSibling();
        }

        private static void EnsureVerticalScrollbar(ScrollRect scroll, RectTransform viewport)
        {
            if (scroll == null || viewport == null)
            {
                return;
            }

            Scrollbar scrollbar = scroll.verticalScrollbar;
            GameObject trackObject;
            if (scrollbar == null)
            {
                Transform existing = scroll.transform.Find("Scrollbar Vertical");
                trackObject = existing == null
                    ? new GameObject("Scrollbar Vertical", typeof(RectTransform), typeof(Image), typeof(Scrollbar))
                    : existing.gameObject;
                trackObject.transform.SetParent(scroll.transform, false);
                scrollbar = trackObject.GetComponent<Scrollbar>() ?? trackObject.AddComponent<Scrollbar>();
            }
            else
            {
                trackObject = scrollbar.gameObject;
            }

            RectTransform trackRect = trackObject.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(1f, 0f);
            trackRect.anchorMax = Vector2.one;
            trackRect.pivot = new Vector2(1f, 0.5f);
            trackRect.offsetMin = new Vector2(-18f, 4f);
            trackRect.offsetMax = new Vector2(-4f, -4f);
            Image trackImage = trackObject.GetComponent<Image>();
            if (trackImage == null) trackImage = trackObject.AddComponent<Image>();
            GameUiTheme.StylePanel(trackImage);
            trackImage.color = GameUiTheme.SurfaceInset;

            Transform existingSlidingArea = trackObject.transform.Find("Sliding Area");
            GameObject slidingAreaObject = existingSlidingArea == null
                ? new GameObject("Sliding Area", typeof(RectTransform))
                : existingSlidingArea.gameObject;
            slidingAreaObject.transform.SetParent(trackObject.transform, false);
            RectTransform slidingArea = slidingAreaObject.GetComponent<RectTransform>();
            slidingArea.anchorMin = Vector2.zero;
            slidingArea.anchorMax = Vector2.one;
            slidingArea.offsetMin = new Vector2(3f, 3f);
            slidingArea.offsetMax = new Vector2(-3f, -3f);

            Transform existingHandle = slidingAreaObject.transform.Find("Handle");
            GameObject handleObject = existingHandle == null
                ? new GameObject("Handle", typeof(RectTransform), typeof(Image))
                : existingHandle.gameObject;
            handleObject.transform.SetParent(slidingAreaObject.transform, false);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            Image handleImage = handleObject.GetComponent<Image>();
            if (handleImage == null) handleImage = handleObject.AddComponent<Image>();
            GameUiTheme.StylePanel(handleImage, raised: true);
            handleImage.color = new Color(GameUiTheme.Secondary.r, GameUiTheme.Secondary.g, GameUiTheme.Secondary.b, 0.9f);

            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarSpacing = 6f;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void ApplyTheme()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                GameUiTheme.StyleButton(buttons[i], GameUiTheme.InferButtonTone(buttons[i].name));
            }

            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                GameUiTheme.StyleText(texts[i], GameUiTheme.InferTextRole(texts[i].name));
            }

            if (feedbackText != null)
            {
                GameUiTheme.StyleText(feedbackText, GameUiTextRole.Feedback);
            }
            if (selectedItemHeaderText != null)
            {
                GameUiTheme.StyleText(selectedItemHeaderText, GameUiTextRole.Heading);
            }
            if (selectedItemDetailsText != null)
            {
                GameUiTheme.StyleText(selectedItemDetailsText, GameUiTextRole.Muted);
                selectedItemDetailsText.fontSize = Mathf.Max(14, selectedItemDetailsText.fontSize);
                selectedItemDetailsText.lineSpacing = Mathf.Max(1.1f, selectedItemDetailsText.lineSpacing);
                selectedItemDetailsText.supportRichText = true;
            }
            if (selectedItemStatusText != null)
            {
                GameUiTheme.StyleText(selectedItemStatusText, GameUiTextRole.Body);
                selectedItemStatusText.fontSize = Mathf.Max(14, selectedItemStatusText.fontSize);
                selectedItemStatusText.fontStyle = FontStyle.Bold;
                selectedItemStatusText.alignment = TextAnchor.MiddleCenter;
                selectedItemStatusText.color = Color.white;
            }
            StyleInventoryActionIcon(useButton);
            StyleInventoryActionIcon(dropButton);
            StyleDropAllActionIcon(dropAllButton);

            if (slotViews != null)
            {
                for (int i = 0; i < slotViews.Length; i++) slotViews[i]?.RefreshPresentation();
            }
            if (equipmentSlotViews != null)
            {
                for (int i = 0; i < equipmentSlotViews.Length; i++) equipmentSlotViews[i]?.RefreshPresentation();
            }
        }

        private static void ConfigureNavigationButton(Button button, string label, int siblingIndex)
        {
            if (button == null)
            {
                return;
            }

            button.transform.SetSiblingIndex(siblingIndex);
            LayoutElement layoutElement = button.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = button.gameObject.AddComponent<LayoutElement>();
            }

            ApplyNavigationButtonLayout(layoutElement);

            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
                text.fontSize = 16;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.raycastTarget = false;
                RectTransform textRect = text.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(5f, 2f);
                textRect.offsetMax = new Vector2(-5f, -2f);
            }
        }

        private InventoryMenuExtensionBinding CreateExtensionBinding(IInventoryMenuExtension extension)
        {
            Font font = ResolveFont();
            Transform buttonParent = contractsMenuButton == null ? transform : contractsMenuButton.transform.parent;
            GameObject buttonObject = new GameObject($"{extension.DisplayName} Menu Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(buttonParent, false);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = inactiveMenuColor;
            Button button = buttonObject.GetComponent<Button>();
            GameUiTheme.StyleButton(button);

            Text label = CreateDetailsText("Label", buttonObject.transform, font, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 2f);
            labelRect.offsetMax = new Vector2(-6f, -2f);
            label.text = extension.DisplayName;

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            ApplyNavigationButtonLayout(layout);

            Transform contentParent = contractsContentRoot == null ? transform : contractsContentRoot.transform.parent;
            GameObject contentObject = new GameObject($"{extension.DisplayName} Content", typeof(RectTransform));
            contentObject.transform.SetParent(contentParent, false);
            RectTransform contentRoot = contentObject.GetComponent<RectTransform>();
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.offsetMin = Vector2.zero;
            contentRoot.offsetMax = Vector2.zero;

            InventoryMenuExtensionBinding binding = new InventoryMenuExtensionBinding(extension, button, buttonImage, contentRoot);
            button.onClick.AddListener(() => ShowExtensionSection(binding));
            return binding;
        }

        private void ShowExtensionSection(InventoryMenuExtensionBinding binding)
        {
            if (binding == null || !binding.Extension.IsAvailable)
            {
                return;
            }

            activeExtension = binding;
            activeSection = InventoryMenuSection.Extension;
            ApplyActiveSection();
        }

        private Font ResolveFont()
        {
            Font font = feedbackText == null ? null : feedbackText.font;
            return font == null ? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") : font;
        }

        private static void ApplyNavigationButtonLayout(LayoutElement layoutElement)
        {
            if (layoutElement == null)
            {
                return;
            }

            layoutElement.minWidth = 92f;
            layoutElement.preferredWidth = 132f;
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = NavigationButtonHeight;
            layoutElement.preferredHeight = NavigationButtonHeight;
            layoutElement.flexibleHeight = 0f;
        }

        private void EnsureSaveLoadMenuObjects()
        {
            Font font = feedbackText == null ? null : feedbackText.font;
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (saveLoadMenuButton == null)
            {
                Transform buttonParent = contractsMenuButton == null ? transform : contractsMenuButton.transform.parent;
                GameObject buttonObject = new GameObject("Save Load Menu Button", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(buttonParent, false);
                saveLoadMenuButtonImage = buttonObject.GetComponent<Image>();
                saveLoadMenuButtonImage.color = inactiveMenuColor;
                saveLoadMenuButton = buttonObject.GetComponent<Button>();

                Text label = CreateDetailsText("Label", buttonObject.transform, font, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(6f, 2f);
                labelRect.offsetMax = new Vector2(-6f, -2f);
                label.text = "Save/Load";

                LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
                ApplyNavigationButtonLayout(layout);
            }

            if (saveLoadContentRoot == null)
            {
                Transform contentParent = contractsContentRoot == null ? transform : contractsContentRoot.transform.parent;
                saveLoadContentRoot = new GameObject("Save Load Content", typeof(RectTransform));
                saveLoadContentRoot.transform.SetParent(contentParent, false);
                RectTransform rectTransform = saveLoadContentRoot.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }

            if (saveLoadView == null)
            {
                saveLoadView = saveLoadContentRoot.GetComponent<SaveLoadMenuView>();
                if (saveLoadView == null)
                {
                    saveLoadView = saveLoadContentRoot.AddComponent<SaveLoadMenuView>();
                }
            }

            if (saveLoadMenuButtonImage == null && saveLoadMenuButton != null)
            {
                saveLoadMenuButtonImage = saveLoadMenuButton.GetComponent<Image>();
            }
        }

        private void EnsureItemDetailsPanel()
        {
            Transform parent = inventoryContentRoot == null ? transform : inventoryContentRoot.transform;
            Font font = feedbackText == null ? null : feedbackText.font;
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            selectedItemDetailsRoot = selectedItemDetailsRoot == null
                ? CreateDetailsRoot(parent)
                : selectedItemDetailsRoot;

            EnsureInventoryActionButtons(font);

            RectTransform rootRect = selectedItemDetailsRoot.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.anchorMin = new Vector2(0.66f, 0.04f);
                rootRect.anchorMax = new Vector2(0.985f, 0.96f);
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            Image rootImage = selectedItemDetailsRoot.GetComponent<Image>();
            GameUiTheme.StylePanel(rootImage, raised: true);
            EnsureOutline(selectedItemDetailsRoot, GameUiTheme.Border);

            Transform artworkFrameTransform = selectedItemDetailsRoot.transform.Find("Item Artwork Frame");
            GameObject artworkFrame;
            if (artworkFrameTransform == null)
            {
                artworkFrame = new GameObject("Item Artwork Frame", typeof(RectTransform), typeof(Image), typeof(Outline));
                artworkFrame.transform.SetParent(selectedItemDetailsRoot.transform, false);
            }
            else
            {
                artworkFrame = artworkFrameTransform.gameObject;
            }

            RectTransform artworkFrameRect = artworkFrame.GetComponent<RectTransform>();
            artworkFrameRect.anchorMin = new Vector2(0.19f, 0.685f);
            artworkFrameRect.anchorMax = new Vector2(0.81f, 0.965f);
            artworkFrameRect.offsetMin = Vector2.zero;
            artworkFrameRect.offsetMax = Vector2.zero;
            Image artworkImage = artworkFrame.GetComponent<Image>();
            GameUiTheme.StylePanel(artworkImage);
            artworkImage.color = GameUiTheme.SurfaceInset;
            Outline artworkOutline = artworkFrame.GetComponent<Outline>();
            artworkOutline.effectColor = new Color(GameUiTheme.Accent.r, GameUiTheme.Accent.g, GameUiTheme.Accent.b, 0.65f);
            artworkOutline.effectDistance = new Vector2(1f, -1f);

            if (selectedItemIconImage == null)
            {
                Transform iconTransform = artworkFrame.transform.Find("Item Artwork");
                if (iconTransform == null)
                {
                    GameObject iconObject = new GameObject("Item Artwork", typeof(RectTransform), typeof(Image));
                    iconObject.transform.SetParent(artworkFrame.transform, false);
                    selectedItemIconImage = iconObject.GetComponent<Image>();
                }
                else
                {
                    selectedItemIconImage = iconTransform.GetComponent<Image>();
                }
            }

            if (selectedItemIconImage != null)
            {
                RectTransform iconRect = selectedItemIconImage.rectTransform;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(10f, 10f);
                iconRect.offsetMax = new Vector2(-10f, -10f);
                selectedItemIconImage.preserveAspect = true;
                selectedItemIconImage.raycastTarget = false;
            }

            if (selectedItemIconFallbackText == null)
            {
                selectedItemIconFallbackText = CreateDetailsText("Item Artwork Fallback", artworkFrame.transform, font, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            }

            RectTransform fallbackRect = selectedItemIconFallbackText.rectTransform;
            fallbackRect.anchorMin = Vector2.zero;
            fallbackRect.anchorMax = Vector2.one;
            fallbackRect.offsetMin = new Vector2(8f, 8f);
            fallbackRect.offsetMax = new Vector2(-8f, -8f);
            selectedItemIconFallbackText.color = GameUiTheme.TextMuted;
            selectedItemIconFallbackText.raycastTarget = false;

            if (selectedItemHeaderText == null)
            {
                selectedItemHeaderText = CreateDetailsText("Item Header", selectedItemDetailsRoot.transform, font, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            }

            RectTransform headerRect = selectedItemHeaderText.rectTransform;
            headerRect.anchorMin = new Vector2(0.05f, 0.605f);
            headerRect.anchorMax = new Vector2(0.95f, 0.675f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;
            selectedItemHeaderText.alignment = TextAnchor.MiddleCenter;
            selectedItemHeaderText.horizontalOverflow = HorizontalWrapMode.Wrap;
            selectedItemHeaderText.verticalOverflow = VerticalWrapMode.Truncate;

            EnsureSelectedItemStatus(font);
            EnsureEquipmentComparisonTooltip(font);

            if (selectedItemDetailsScroll == null)
            {
                Transform existingScroll = selectedItemDetailsRoot.transform.Find("Item Details Scroll");
                GameObject scrollObject;
                if (existingScroll == null)
                {
                    scrollObject = new GameObject("Item Details Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
                    scrollObject.transform.SetParent(selectedItemDetailsRoot.transform, false);
                }
                else
                {
                    scrollObject = existingScroll.gameObject;
                }

                selectedItemDetailsScroll = scrollObject.GetComponent<ScrollRect>();
                RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
                scrollRect.anchorMin = new Vector2(0.045f, 0.21f);
                scrollRect.anchorMax = new Vector2(0.955f, 0.53f);
                scrollRect.offsetMin = Vector2.zero;
                scrollRect.offsetMax = Vector2.zero;
                Image detailsBackground = scrollObject.GetComponent<Image>();
                GameUiTheme.StylePanel(detailsBackground);
                detailsBackground.color = new Color(GameUiTheme.SurfaceInset.r, GameUiTheme.SurfaceInset.g, GameUiTheme.SurfaceInset.b, 0.76f);

                Transform viewportTransform = scrollObject.transform.Find("Viewport");
                GameObject viewport;
                if (viewportTransform == null)
                {
                    viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                    viewport.transform.SetParent(scrollObject.transform, false);
                }
                else
                {
                    viewport = viewportTransform.gameObject;
                }

                RectTransform viewportRect = viewport.GetComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = new Vector2(12f, 10f);
                viewportRect.offsetMax = new Vector2(-10f, -10f);
                viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);

                if (selectedItemDetailsText == null)
                {
                    selectedItemDetailsText = CreateDetailsText("Item Details", viewport.transform, font, 14, FontStyle.Normal, TextAnchor.UpperLeft);
                }
                else
                {
                    selectedItemDetailsText.transform.SetParent(viewport.transform, false);
                }

                RectTransform detailsRect = selectedItemDetailsText.rectTransform;
                detailsRect.anchorMin = new Vector2(0f, 1f);
                detailsRect.anchorMax = new Vector2(1f, 1f);
                detailsRect.pivot = new Vector2(0.5f, 1f);
                detailsRect.anchoredPosition = Vector2.zero;
                detailsRect.sizeDelta = Vector2.zero;
                selectedItemDetailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
                selectedItemDetailsText.verticalOverflow = VerticalWrapMode.Overflow;
                ContentSizeFitter fitter = selectedItemDetailsText.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                {
                    fitter = selectedItemDetailsText.gameObject.AddComponent<ContentSizeFitter>();
                }
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                selectedItemDetailsScroll.viewport = viewportRect;
                selectedItemDetailsScroll.content = detailsRect;
                selectedItemDetailsScroll.horizontal = false;
                selectedItemDetailsScroll.vertical = true;
                selectedItemDetailsScroll.movementType = ScrollRect.MovementType.Clamped;
                selectedItemDetailsScroll.scrollSensitivity = 24f;
                EnsureVerticalScrollbar(selectedItemDetailsScroll, viewportRect);
            }

            if (selectedItemDetailsScroll != null)
            {
                RectTransform scrollRect = selectedItemDetailsScroll.GetComponent<RectTransform>();
                scrollRect.anchorMin = new Vector2(0.045f, 0.21f);
                scrollRect.anchorMax = new Vector2(0.955f, 0.53f);
                scrollRect.offsetMin = Vector2.zero;
                scrollRect.offsetMax = Vector2.zero;
            }

            EnsureAccentBand(selectedItemDetailsRoot.transform, "Item Section Separator", GameUiTheme.Border, 2f, 0.595f);
        }

        private void EnsureEquipmentComparisonTooltip(Font font)
        {
            if (selectedItemDetailsRoot == null)
            {
                return;
            }

            bool created = false;
            Transform existingTooltip = selectedItemDetailsRoot.transform.Find("Equipment Comparison Tooltip");
            if (equipmentComparisonTooltipRoot == null)
            {
                equipmentComparisonTooltipRoot = existingTooltip == null
                    ? new GameObject("Equipment Comparison Tooltip", typeof(RectTransform), typeof(Image), typeof(Outline))
                    : existingTooltip.gameObject;
                created = existingTooltip == null;
            }

            if (equipmentComparisonTooltipRoot.transform.parent != selectedItemDetailsRoot.transform)
            {
                equipmentComparisonTooltipRoot.transform.SetParent(selectedItemDetailsRoot.transform, false);
            }

            RectTransform tooltipRect = equipmentComparisonTooltipRoot.GetComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(0f, 0.19f);
            tooltipRect.anchorMax = new Vector2(0f, 0.19f);
            tooltipRect.pivot = new Vector2(1f, 0f);
            tooltipRect.anchoredPosition = new Vector2(-12f, 0f);
            tooltipRect.sizeDelta = new Vector2(380f, 280f);
            Image tooltipImage = equipmentComparisonTooltipRoot.GetComponent<Image>();
            GameUiTheme.StylePanel(tooltipImage, raised: true);
            tooltipImage.color = new Color(GameUiTheme.PanelRaised.r, GameUiTheme.PanelRaised.g, GameUiTheme.PanelRaised.b, 0.99f);
            EnsureOutline(equipmentComparisonTooltipRoot, GameUiTheme.Accent);
            EnsureAccentBand(equipmentComparisonTooltipRoot.transform, "Comparison Top Accent", GameUiTheme.Accent, 3f);

            Transform existingHeading = equipmentComparisonTooltipRoot.transform.Find("Comparison Heading");
            Text heading = existingHeading == null
                ? CreateDetailsText("Comparison Heading", equipmentComparisonTooltipRoot.transform, font, 16, FontStyle.Bold, TextAnchor.MiddleLeft)
                : existingHeading.GetComponent<Text>();
            heading.text = "EQUIPMENT COMPARISON";
            heading.fontSize = Mathf.Max(16, heading.fontSize);
            heading.fontStyle = FontStyle.Bold;
            heading.color = GameUiTheme.AccentBright;
            heading.raycastTarget = false;
            RectTransform headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(0f, 0.84f);
            headingRect.anchorMax = Vector2.one;
            headingRect.offsetMin = new Vector2(16f, 0f);
            headingRect.offsetMax = new Vector2(-16f, -5f);

            if (equipmentComparisonTooltipScroll == null)
            {
                Transform existingScroll = equipmentComparisonTooltipRoot.transform.Find("Comparison Scroll");
                GameObject scrollObject = existingScroll == null
                    ? new GameObject("Comparison Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect))
                    : existingScroll.gameObject;
                if (existingScroll == null) scrollObject.transform.SetParent(equipmentComparisonTooltipRoot.transform, false);
                equipmentComparisonTooltipScroll = scrollObject.GetComponent<ScrollRect>();

                Transform existingViewport = scrollObject.transform.Find("Viewport");
                GameObject viewportObject = existingViewport == null
                    ? new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D))
                    : existingViewport.gameObject;
                if (existingViewport == null) viewportObject.transform.SetParent(scrollObject.transform, false);
                RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = new Vector2(12f, 10f);
                viewportRect.offsetMax = new Vector2(-28f, -10f);
                Image viewportImage = viewportObject.GetComponent<Image>();
                viewportImage.color = new Color(1f, 1f, 1f, 0.001f);

                if (equipmentComparisonTooltipText == null)
                {
                    equipmentComparisonTooltipText = CreateDetailsText("Comparison Details", viewportObject.transform, font, 14, FontStyle.Normal, TextAnchor.UpperLeft);
                }
                else
                {
                    equipmentComparisonTooltipText.transform.SetParent(viewportObject.transform, false);
                }

                RectTransform contentRect = equipmentComparisonTooltipText.rectTransform;
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = Vector2.zero;
                equipmentComparisonTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
                equipmentComparisonTooltipText.verticalOverflow = VerticalWrapMode.Overflow;
                equipmentComparisonTooltipText.supportRichText = true;
                ContentSizeFitter fitter = equipmentComparisonTooltipText.GetComponent<ContentSizeFitter>()
                    ?? equipmentComparisonTooltipText.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                equipmentComparisonTooltipScroll.viewport = viewportRect;
                equipmentComparisonTooltipScroll.content = contentRect;
                equipmentComparisonTooltipScroll.horizontal = false;
                equipmentComparisonTooltipScroll.vertical = true;
                equipmentComparisonTooltipScroll.movementType = ScrollRect.MovementType.Clamped;
                equipmentComparisonTooltipScroll.scrollSensitivity = 24f;
                EnsureVerticalScrollbar(equipmentComparisonTooltipScroll, viewportRect);
            }

            RectTransform comparisonScrollRect = equipmentComparisonTooltipScroll.GetComponent<RectTransform>();
            comparisonScrollRect.anchorMin = new Vector2(0.04f, 0.055f);
            comparisonScrollRect.anchorMax = new Vector2(0.96f, 0.82f);
            comparisonScrollRect.offsetMin = Vector2.zero;
            comparisonScrollRect.offsetMax = Vector2.zero;
            Image scrollBackground = equipmentComparisonTooltipScroll.GetComponent<Image>();
            GameUiTheme.StylePanel(scrollBackground);
            scrollBackground.color = new Color(GameUiTheme.SurfaceInset.r, GameUiTheme.SurfaceInset.g, GameUiTheme.SurfaceInset.b, 0.94f);

            if (equipmentComparisonTooltipText != null)
            {
                GameUiTheme.StyleText(equipmentComparisonTooltipText, GameUiTextRole.Body);
                equipmentComparisonTooltipText.fontSize = Mathf.Max(14, equipmentComparisonTooltipText.fontSize);
                equipmentComparisonTooltipText.lineSpacing = Mathf.Max(1.1f, equipmentComparisonTooltipText.lineSpacing);
            }

            if (created)
            {
                equipmentComparisonTooltipRoot.SetActive(false);
            }
        }

        private void EnsureSelectedItemStatus(Font font)
        {
            if (selectedItemDetailsRoot == null)
            {
                return;
            }

            Transform existingStatus = selectedItemDetailsRoot.transform.Find("Item Status");
            if (selectedItemStatusRoot == null)
            {
                selectedItemStatusRoot = existingStatus == null
                    ? new GameObject("Item Status", typeof(RectTransform), typeof(Image), typeof(Outline))
                    : existingStatus.gameObject;
            }

            if (selectedItemStatusRoot.transform.parent != selectedItemDetailsRoot.transform)
            {
                selectedItemStatusRoot.transform.SetParent(selectedItemDetailsRoot.transform, false);
            }

            RectTransform statusRect = selectedItemStatusRoot.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.045f, 0.54f);
            statusRect.anchorMax = new Vector2(0.955f, 0.585f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            selectedItemStatusBackground = selectedItemStatusRoot.GetComponent<Image>();
            GameUiTheme.StylePanel(selectedItemStatusBackground);
            selectedItemStatusBackground.color = GameUiTheme.SurfaceInset;
            EnsureOutline(selectedItemStatusRoot, GameUiTheme.Border);

            Transform existingFill = selectedItemStatusRoot.transform.Find("Durability Fill");
            if (selectedItemDurabilityFill == null)
            {
                GameObject fillObject = existingFill == null
                    ? new GameObject("Durability Fill", typeof(RectTransform), typeof(Image))
                    : existingFill.gameObject;
                if (existingFill == null) fillObject.transform.SetParent(selectedItemStatusRoot.transform, false);
                selectedItemDurabilityFill = fillObject.GetComponent<Image>();
            }

            RectTransform fillRect = selectedItemDurabilityFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            selectedItemDurabilityFill.type = Image.Type.Filled;
            selectedItemDurabilityFill.fillMethod = Image.FillMethod.Horizontal;
            selectedItemDurabilityFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            selectedItemDurabilityFill.fillAmount = 1f;
            selectedItemDurabilityFill.color = GetDurabilityColor(1f);
            selectedItemDurabilityFill.raycastTarget = false;

            if (selectedItemStatusText == null)
            {
                Transform existingLabel = selectedItemStatusRoot.transform.Find("Status Label");
                selectedItemStatusText = existingLabel == null
                    ? CreateDetailsText("Status Label", selectedItemStatusRoot.transform, font, 14, FontStyle.Bold, TextAnchor.MiddleCenter)
                    : existingLabel.GetComponent<Text>();
            }

            RectTransform labelRect = selectedItemStatusText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 1f);
            labelRect.offsetMax = new Vector2(-8f, -1f);
            selectedItemStatusText.fontSize = Mathf.Max(14, selectedItemStatusText.fontSize);
            selectedItemStatusText.fontStyle = FontStyle.Bold;
            selectedItemStatusText.alignment = TextAnchor.MiddleCenter;
            selectedItemStatusText.color = Color.white;
            selectedItemStatusText.raycastTarget = false;
        }

        private void EnsureInventoryActionButtons(Font font)
        {
            if (selectedItemDetailsRoot == null) return;

            Transform existingActions = selectedItemDetailsRoot.transform.Find("Item Actions");
            GameObject actionsObject;
            if (existingActions == null)
            {
                actionsObject = new GameObject("Item Actions", typeof(RectTransform), typeof(GridLayoutGroup));
                actionsObject.transform.SetParent(selectedItemDetailsRoot.transform, false);
            }
            else
            {
                actionsObject = existingActions.gameObject;
            }

            inventoryActionGridRect = actionsObject.GetComponent<RectTransform>();
            inventoryActionGridRect.anchorMin = new Vector2(0.045f, 0.025f);
            inventoryActionGridRect.anchorMax = new Vector2(0.955f, 0.18f);
            inventoryActionGridRect.offsetMin = Vector2.zero;
            inventoryActionGridRect.offsetMax = Vector2.zero;
            inventoryActionGridLayout = actionsObject.GetComponent<GridLayoutGroup>();
            inventoryActionGridLayout.padding = new RectOffset(0, 0, 0, 0);
            inventoryActionGridLayout.spacing = new Vector2(InventoryActionSpacing, 7f);
            inventoryActionGridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            inventoryActionGridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            inventoryActionGridLayout.childAlignment = TextAnchor.MiddleCenter;
            inventoryActionGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

            Transform obsoleteEquipButton = actionsObject.transform.Find("Equip Button");
            if (obsoleteEquipButton != null && (useButton == null || obsoleteEquipButton.gameObject != useButton.gameObject))
            {
                DestroyMenuObject(obsoleteEquipButton.gameObject);
            }

            useButton = EnsureInventoryActionButton(useButton, actionsObject.transform, "Use Item Action Button", useEquipActionIcon, GameUiButtonTone.Primary);
            dropButton = EnsureInventoryActionButton(dropButton, actionsObject.transform, "Drop Button", dropActionIcon, GameUiButtonTone.Danger);
            dropAllButton = EnsureDropAllActionButton(dropAllButton, actionsObject.transform, font);
            useButton.transform.SetSiblingIndex(0);
            dropButton.transform.SetSiblingIndex(1);
            dropAllButton.transform.SetSiblingIndex(2);
            StyleInventoryActionIcon(useButton);
            StyleInventoryActionIcon(dropButton);
            StyleDropAllActionIcon(dropAllButton);
            dropAllButton.gameObject.SetActive(false);
            UpdateResponsiveItemActions();
        }

        private Button EnsureDropAllActionButton(Button button, Transform actionParent, Font font)
        {
            button = EnsureInventoryActionButton(button, actionParent, "Drop All Button", dropActionIcon, GameUiButtonTone.Danger);
            Image icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(0f, 5f);
                iconRect.sizeDelta = new Vector2(30f, 30f);
            }

            Transform existingAllLabel = button.transform.Find("All Label");
            Text allLabel = existingAllLabel == null
                ? CreateDetailsText("All Label", button.transform, font, 10, FontStyle.Bold, TextAnchor.LowerCenter)
                : existingAllLabel.GetComponent<Text>();
            if (allLabel != null)
            {
                allLabel.text = "ALL";
                RectTransform allRect = allLabel.rectTransform;
                allRect.anchorMin = Vector2.zero;
                allRect.anchorMax = new Vector2(1f, 0.3f);
                allRect.offsetMin = new Vector2(4f, 1f);
                allRect.offsetMax = new Vector2(-4f, 0f);
                allLabel.raycastTarget = false;
            }

            return button;
        }

        private Button EnsureInventoryActionButton(Button button, Transform actionParent, string objectName, Sprite iconSprite, GameUiButtonTone tone)
        {
            if (button == null)
            {
                GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(actionParent, false);
                button = buttonObject.GetComponent<Button>();
            }
            else if (button.transform.parent != actionParent)
            {
                button.transform.SetParent(actionParent, false);
            }

            button.name = objectName;
            Text[] legacyLabels = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacyLabels.Length; i++)
            {
                Text legacyLabel = legacyLabels[i];
                if (legacyLabel == null || legacyLabel.name == "All Label") continue;
                legacyLabel.gameObject.SetActive(false);
                DestroyMenuObject(legacyLabel.gameObject);
            }

            Transform iconTransform = button.transform.Find("Icon");
            GameObject iconObject = iconTransform == null
                ? new GameObject("Icon", typeof(RectTransform), typeof(Image))
                : iconTransform.gameObject;
            if (iconTransform == null) iconObject.transform.SetParent(button.transform, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = iconSprite;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(InventoryActionIconSize, InventoryActionIconSize);
            GameUiTheme.StyleButton(button, tone);
            return button;
        }

        private static void StyleInventoryActionIcon(Button button)
        {
            if (button == null) return;
            Image icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon == null) return;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        private static void StyleDropAllActionIcon(Button button)
        {
            if (button == null) return;
            StyleInventoryActionIcon(button);
            Text[] labels = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                Text label = labels[i];
                label.fontStyle = FontStyle.Bold;
                label.color = GameUiTheme.Danger;
                label.fontSize = 10;
                label.alignment = TextAnchor.LowerCenter;
            }
        }

        private void RefreshInventoryActionIcons(bool showUnequip)
        {
            SetInventoryActionIcon(useButton, showUnequip ? unequipActionIcon : useEquipActionIcon);
            SetInventoryActionIcon(dropButton, dropActionIcon);
            SetInventoryActionIcon(dropAllButton, dropActionIcon);
        }

        private static void SetInventoryActionIcon(Button button, Sprite sprite)
        {
            Image icon = button == null ? null : button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null) icon.sprite = sprite;
        }

        private Sprite ResolveConsumeActionIcon(ItemDefinition item)
        {
            return ResolveConsumablePresentation(item) switch
            {
                ConsumablePresentationType.Food when consumeFoodActionIcon != null => consumeFoodActionIcon,
                ConsumablePresentationType.Potion when consumePotionActionIcon != null => consumePotionActionIcon,
                _ => useEquipActionIcon
            };
        }

        public static ConsumablePresentationType ResolveConsumablePresentation(ItemDefinition item)
        {
            if (item == null) return ConsumablePresentationType.Generic;
            if (item.ConsumablePresentation != ConsumablePresentationType.Generic) return item.ConsumablePresentation;

            string identity = $"{item.ItemId} {item.DisplayName}".ToLowerInvariant();
            if (identity.Contains("potion")) return ConsumablePresentationType.Potion;
            if (identity.Contains("food") || identity.Contains("meat") || identity.Contains("bread") || identity.Contains("stew") || identity.Contains("meal"))
            {
                return ConsumablePresentationType.Food;
            }

            return ConsumablePresentationType.Generic;
        }

        private void EnsureCharacterStatsPanel()
        {
            Transform parent = characterContentRoot == null ? transform : characterContentRoot.transform;
            Font font = feedbackText == null ? null : feedbackText.font;
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            characterStatsRoot = characterStatsRoot == null
                ? CreateCharacterStatsRoot(parent)
                : characterStatsRoot;

            if (characterStatsScroll == null)
            {
                Transform existingScroll = characterStatsRoot.transform.Find("Character Stats Scroll");
                GameObject scrollObject;
                if (existingScroll == null)
                {
                    scrollObject = new GameObject("Character Stats Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
                    scrollObject.transform.SetParent(characterStatsRoot.transform, false);
                }
                else
                {
                    scrollObject = existingScroll.gameObject;
                }

                characterStatsScroll = scrollObject.GetComponent<ScrollRect>();
                RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
                scrollRect.anchorMin = new Vector2(0f, 0.25f);
                scrollRect.anchorMax = Vector2.one;
                scrollRect.offsetMin = new Vector2(14f, 8f);
                scrollRect.offsetMax = new Vector2(-14f, -14f);
                Image statsBackground = scrollObject.GetComponent<Image>();
                GameUiTheme.StylePanel(statsBackground);
                statsBackground.color = new Color(GameUiTheme.SurfaceInset.r, GameUiTheme.SurfaceInset.g, GameUiTheme.SurfaceInset.b, 0.68f);

                Transform viewportTransform = scrollObject.transform.Find("Viewport");
                GameObject viewport;
                if (viewportTransform == null)
                {
                    viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                    viewport.transform.SetParent(scrollObject.transform, false);
                }
                else
                {
                    viewport = viewportTransform.gameObject;
                }

                RectTransform viewportRect = viewport.GetComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = new Vector2(10f, 8f);
                viewportRect.offsetMax = new Vector2(-8f, -8f);
                viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);

                if (characterStatsText == null)
                {
                    characterStatsText = CreateDetailsText("Character Stats", viewport.transform, font, 13, FontStyle.Normal, TextAnchor.UpperLeft);
                }
                else
                {
                    characterStatsText.transform.SetParent(viewport.transform, false);
                }

                RectTransform statsTextRect = characterStatsText.rectTransform;
                statsTextRect.anchorMin = new Vector2(0f, 1f);
                statsTextRect.anchorMax = new Vector2(1f, 1f);
                statsTextRect.pivot = new Vector2(0.5f, 1f);
                statsTextRect.anchoredPosition = Vector2.zero;
                statsTextRect.sizeDelta = Vector2.zero;
                characterStatsText.horizontalOverflow = HorizontalWrapMode.Wrap;
                characterStatsText.verticalOverflow = VerticalWrapMode.Overflow;
                ContentSizeFitter fitter = characterStatsText.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                {
                    fitter = characterStatsText.gameObject.AddComponent<ContentSizeFitter>();
                }
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                characterStatsScroll.viewport = viewportRect;
                characterStatsScroll.content = statsTextRect;
                characterStatsScroll.horizontal = false;
                characterStatsScroll.vertical = true;
                characterStatsScroll.movementType = ScrollRect.MovementType.Clamped;
                characterStatsScroll.scrollSensitivity = 24f;
                EnsureVerticalScrollbar(characterStatsScroll, viewportRect);
            }

            if (statusReadoutText == null)
            {
                statusReadoutText = CreateDetailsText("Status Effects", characterStatsRoot.transform, font, 14, FontStyle.Normal, TextAnchor.UpperLeft);
            }
            RectTransform statusRect = statusReadoutText.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0.02f);
            statusRect.anchorMax = new Vector2(1f, 0.23f);
            statusRect.offsetMin = new Vector2(18f, 10f);
            statusRect.offsetMax = new Vector2(-18f, -4f);

            if (statusReadoutView == null)
            {
                statusReadoutView = statusReadoutText.GetComponent<StatusEffectReadoutView>();
                if (statusReadoutView == null)
                {
                    statusReadoutView = statusReadoutText.gameObject.AddComponent<StatusEffectReadoutView>();
                }
            }

            ConfigureCharacterSectionLayout();
        }

        private static GameObject CreateCharacterStatsRoot(Transform parent)
        {
            GameObject root = new GameObject("Character Stats And Status", typeof(RectTransform), typeof(Image), typeof(Outline));
            root.transform.SetParent(parent, false);

            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.49f, 0.04f);
            rectTransform.anchorMax = new Vector2(0.985f, 0.96f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Image image = root.GetComponent<Image>();
            GameUiTheme.StylePanel(image, raised: true);

            Outline outline = root.GetComponent<Outline>();
            outline.effectColor = new Color(GameUiTheme.Border.r, GameUiTheme.Border.g, GameUiTheme.Border.b, 0.72f);
            outline.effectDistance = new Vector2(1f, -1f);

            return root;
        }

        private static void AppendLine(StringBuilder builder, string label, string value)
        {
            builder.Append(label);
            builder.Append(": ");
            builder.AppendLine(value);
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.##");
        }

        private static string FormatDefinitionName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "Unknown";
            }

            int index = id.IndexOf('.');
            string name = index >= 0 && index + 1 < id.Length ? id.Substring(index + 1) : id;
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Replace('-', ' '));
        }

        private static string GetItemMonogram(ItemDefinition item)
        {
            string label = item == null ? string.Empty : item.DisplayName;
            if (string.IsNullOrWhiteSpace(label) && item != null)
            {
                label = item.ItemId;
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                return "?";
            }

            string[] words = label.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1)
            {
                return words[0].Substring(0, Mathf.Min(2, words[0].Length)).ToUpperInvariant();
            }

            return string.Concat(words[0][0], words[1][0]).ToUpperInvariant();
        }

        private static void AppendCompactPairs(StringBuilder builder, IReadOnlyList<string> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                builder.AppendLine("None");
                return;
            }

            for (int i = 0; i < parts.Count; i += 2)
            {
                builder.Append(parts[i]);
                if (i + 1 < parts.Count)
                {
                    builder.Append(" | ");
                    builder.Append(parts[i + 1]);
                }

                builder.AppendLine();
            }
        }

        private static GameObject CreateDetailsRoot(Transform parent)
        {
            GameObject root = new GameObject("Selected Item Details", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);

            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.66f, 0.04f);
            rectTransform.anchorMax = new Vector2(0.985f, 0.96f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Image image = root.GetComponent<Image>();
            GameUiTheme.StylePanel(image, raised: true);
            image.color = new Color(
                GameUiTheme.PanelRaised.r,
                GameUiTheme.PanelRaised.g,
                GameUiTheme.PanelRaised.b,
                0.94f);

            return root;
        }

        private static Text CreateDetailsText(string name, Transform parent, Font font, int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = Color.white;

            return text;
        }

        private enum InventoryMenuSection
        {
            Inventory,
            Character,
            Spells,
            Contracts,
            SaveLoad,
            Extension
        }

        private sealed class InventoryMenuExtensionBinding
        {
            public InventoryMenuExtensionBinding(IInventoryMenuExtension extension, Button button, Image buttonImage, RectTransform contentRoot)
            {
                Extension = extension;
                Button = button;
                ButtonImage = buttonImage;
                ContentRoot = contentRoot;
            }

            public IInventoryMenuExtension Extension { get; }
            public Button Button { get; }
            public Image ButtonImage { get; }
            public RectTransform ContentRoot { get; }
        }
    }

    public static class InventoryItemDetailsFormatter
    {
        public static string GetHeader(InventorySlot slot)
        {
            ItemDefinition item = slot == null || slot.IsEmpty ? null : slot.Item;
            return GetHeader(item);
        }

        public static string GetHeader(ItemDefinition item)
        {
            if (item == null)
            {
                return "No item selected";
            }

            return string.IsNullOrWhiteSpace(item.DisplayName) ? item.ItemId : item.DisplayName;
        }

        public static string FormatDetails(InventorySlot slot, bool includeDescription = false)
        {
            ItemDefinition item = slot == null || slot.IsEmpty ? null : slot.Item;
            return FormatDetails(item, slot?.Quantity ?? 0, slot?.ItemInstanceId ?? string.Empty, slot != null && slot.IsStateful, includeDescription, equipped: false);
        }

        public static string FormatDetails(ItemDefinition item, int quantity, string itemInstanceId, bool isStateful, bool includeDescription = false, bool equipped = false)
        {
            if (item == null)
            {
                return "Select an inventory slot to inspect its stats and effects.";
            }

            StringBuilder builder = new StringBuilder();
            List<string> statChanges = new List<string>();
            List<string> grantedBenefits = new List<string>();
            AppendEquipmentDetails(item.Equipment, statChanges, grantedBenefits);
            AppendUseDetails(item, statChanges, grantedBenefits);

            AppendSection(builder, item.IsUsable && !item.IsEquippable ? "EFFECTS" : "STAT CHANGES", statChanges);
            AppendSection(builder, item.IsUsable && !item.IsEquippable ? "SKILLS & TALENTS" : "SKILLS, TALENTS & ABILITIES", grantedBenefits);

            if (builder.Length == 0 && includeDescription && !string.IsNullOrWhiteSpace(item.Description))
            {
                builder.Append(EscapeRichText(item.Description.Trim()));
            }

            if (builder.Length == 0)
            {
                builder.Append("No stat changes or granted effects.");
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendUseDetails(ItemDefinition item, List<string> statChanges, List<string> grantedBenefits)
        {
            if (item == null || !item.IsUsable || item.UseEffects == null)
            {
                return;
            }

            for (int i = 0; i < item.UseEffects.Count; i++)
            {
                UnityIsekaiGame.Inventory.ItemUseEffect effect = item.UseEffects[i];
                switch (effect)
                {
                    case UnityIsekaiGame.Inventory.RestoreHealthItemUseEffect health:
                        AddSignedStat(statChanges, "Health", health.HealingAmount);
                        break;
                    case UnityIsekaiGame.Inventory.RestoreVitalItemUseEffect vital:
                        string vitalName = vital.RestoreEffect == null ? "Vital Resource" : SplitPascalCase(vital.RestoreEffect.VitalType.ToString());
                        float restoreAmount = vital.RestoreEffect == null ? vital.RestoreAmount : vital.RestoreEffect.Amount;
                        AddSignedStat(statChanges, vitalName, restoreAmount);
                        break;
                    case null:
                        break;
                    default:
                        string effectName = SplitPascalCase(effect.name);
                        if (!string.IsNullOrWhiteSpace(effectName) && !grantedBenefits.Contains(effectName))
                        {
                            grantedBenefits.Add(EscapeRichText(effectName));
                        }
                        break;
                }
            }
        }

        private static void AppendEquipmentDetails(EquipmentData equipment, List<string> statChanges, List<string> grantedBenefits)
        {
            if (equipment == null || !equipment.Equippable)
            {
                return;
            }

            StatModifiers stats = equipment.StatModifiers;
            AddSignedStat(statChanges, "Max Health", stats.MaximumHealth);
            AddSignedStat(statChanges, "Max Stamina", stats.MaximumStamina);
            AddSignedStat(statChanges, "Max Mana", stats.MaximumMana);
            AddSignedStat(statChanges, "Attack", stats.AttackPower);
            AddSignedStat(statChanges, "Defense", stats.Defense);

            IReadOnlyList<ResistanceModifierDefinition> resistances = equipment.ResistanceModifiers;
            for (int i = 0; i < resistances.Count; i++)
            {
                ResistanceModifierDefinition resistance = resistances[i];
                if (resistance == null || resistance.DamageType == null) continue;
                AddSignedStat(statChanges, $"{resistance.DamageType.DisplayName} Resistance", resistance.Resistance * 100f, "%");
            }

            if (equipment.MeleeWeapon != null && equipment.MeleeWeapon.IsWeapon)
            {
                AddSignedStat(statChanges, "Melee Damage", equipment.MeleeWeapon.BaseDamage);
                AddSignedStat(statChanges, "Attack Range", equipment.MeleeWeapon.AttackRange);
                AddSignedStat(statChanges, "Hit Radius", equipment.MeleeWeapon.HitRadius);
                AddAbility(grantedBenefits, equipment.MeleeWeapon.AttackName);
            }

            if (equipment.RangedWeapon != null && equipment.RangedWeapon.IsWeapon)
            {
                AddSignedStat(statChanges, "Ranged Damage", equipment.RangedWeapon.BaseDamage);
                AddSignedStat(statChanges, "Projectile Speed", equipment.RangedWeapon.ProjectileSpeed);
                AddSignedStat(statChanges, "Projectile Lifetime", equipment.RangedWeapon.ProjectileLifetime, "s");
                AddSignedStat(statChanges, "Projectile Radius", equipment.RangedWeapon.ProjectileHitRadius);
                AddAbility(grantedBenefits, equipment.RangedWeapon.AttackName);
            }
        }

        private static void AddSignedStat(List<string> output, string label, float value, string suffix = "")
        {
            if (Mathf.Abs(value) <= 0.0001f)
            {
                return;
            }

            string color = value > 0f ? "#73D67A" : "#FF6B6B";
            string sign = value > 0f ? "+" : string.Empty;
            output.Add($"{EscapeRichText(label)}  <color={color}>{sign}{FormatNumber(value)}{suffix}</color>");
        }

        private static void AddAbility(List<string> output, string abilityName)
        {
            if (string.IsNullOrWhiteSpace(abilityName))
            {
                return;
            }

            string escaped = EscapeRichText(abilityName.Trim());
            if (!output.Contains(escaped)) output.Add(escaped);
        }

        private static void AppendSection(StringBuilder builder, string title, IReadOnlyList<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return;
            }

            if (builder.Length > 0) builder.AppendLine().AppendLine();
            builder.Append("<b>").Append(EscapeRichText(title)).AppendLine("</b>");
            for (int i = 0; i < lines.Count; i++)
            {
                builder.Append("\u2022 ").AppendLine(lines[i]);
            }
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.##");
        }

        private static string SplitPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && !char.IsWhiteSpace(value[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(value[i]);
            }

            return builder.ToString();
        }

        private static string EscapeRichText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }

    public static class EquipmentComparisonFormatter
    {
        private const float DisplayEpsilon = 0.0001f;
        private const string ImprovementColor = "#73D67A";
        private const string ReductionColor = "#FF6B6B";
        private const string UnchangedColor = "#D7B36A";

        public static string Format(ItemDefinition equippedItem, ItemDefinition desiredItem)
        {
            string equippedName = equippedItem == null ? "Nothing equipped" : InventoryItemDetailsFormatter.GetHeader(equippedItem);
            string desiredName = desiredItem == null ? "No item selected" : InventoryItemDetailsFormatter.GetHeader(desiredItem);
            return Format(equippedName, equippedItem?.Equipment, desiredName, desiredItem?.Equipment);
        }

        public static string Format(string equippedName, StatModifiers equippedStats, string desiredName, StatModifiers desiredStats)
        {
            return FormatCore(equippedName, equippedStats, desiredName, desiredStats, null, null);
        }

        private static string Format(string equippedName, EquipmentData equipped, string desiredName, EquipmentData desired)
        {
            StatModifiers equippedStats = equipped == null ? default : equipped.StatModifiers;
            StatModifiers desiredStats = desired == null ? default : desired.StatModifiers;
            return FormatCore(equippedName, equippedStats, desiredName, desiredStats, equipped, desired);
        }

        private static string FormatCore(string equippedName, StatModifiers equippedStats, string desiredName, StatModifiers desiredStats, EquipmentData equipped, EquipmentData desired)
        {
            StringBuilder builder = new StringBuilder(320);
            builder.AppendLine("<b>EQUIPMENT COMPARISON</b>");
            builder.Append("Equipped: ").AppendLine(EscapeRichText(equippedName));
            builder.Append("Desired: ").AppendLine(EscapeRichText(desiredName));
            builder.AppendLine();
            StringBuilder rows = new StringBuilder(320);
            int rowCount = 0;
            rowCount += AppendStat(rows, "Max Health", equippedStats.MaximumHealth, desiredStats.MaximumHealth);
            rowCount += AppendStat(rows, "Max Stamina", equippedStats.MaximumStamina, desiredStats.MaximumStamina);
            rowCount += AppendStat(rows, "Max Mana", equippedStats.MaximumMana, desiredStats.MaximumMana);
            rowCount += AppendStat(rows, "Attack", equippedStats.AttackPower, desiredStats.AttackPower);
            rowCount += AppendStat(rows, "Defense", equippedStats.Defense, desiredStats.Defense);
            rowCount += AppendWeaponStats(rows, equipped, desired);
            rowCount += AppendResistanceStats(rows, equipped, desired);

            if (rowCount == 0)
            {
                builder.AppendLine("Neither item has numeric equipment bonuses to compare.");
            }
            else
            {
                builder.AppendLine("<b>Current value     Change</b>");
                builder.Append(rows);
            }

            return builder.ToString().TrimEnd();
        }

        private static int AppendWeaponStats(StringBuilder builder, EquipmentData equipped, EquipmentData desired)
        {
            MeleeWeaponData equippedMelee = ActiveMelee(equipped);
            MeleeWeaponData desiredMelee = ActiveMelee(desired);
            RangedWeaponData equippedRanged = ActiveRanged(equipped);
            RangedWeaponData desiredRanged = ActiveRanged(desired);
            int rows = 0;

            rows += AppendStat(builder, "Melee Damage", equippedMelee?.BaseDamage ?? 0f, desiredMelee?.BaseDamage ?? 0f);
            rows += AppendStat(builder, "Melee Range", equippedMelee?.AttackRange ?? 0f, desiredMelee?.AttackRange ?? 0f);
            rows += AppendStat(builder, "Melee Cooldown", equippedMelee?.AttackCooldown ?? 0f, desiredMelee?.AttackCooldown ?? 0f, lowerIsBetter: true, suffix: "s");
            rows += AppendStat(builder, "Melee Stamina Cost", equippedMelee?.StaminaCost ?? 0f, desiredMelee?.StaminaCost ?? 0f, lowerIsBetter: true);
            rows += AppendStat(builder, "Melee Hit Radius", equippedMelee?.HitRadius ?? 0f, desiredMelee?.HitRadius ?? 0f);

            rows += AppendStat(builder, "Ranged Damage", equippedRanged?.BaseDamage ?? 0f, desiredRanged?.BaseDamage ?? 0f);
            rows += AppendStat(builder, "Ranged Cooldown", equippedRanged?.AttackCooldown ?? 0f, desiredRanged?.AttackCooldown ?? 0f, lowerIsBetter: true, suffix: "s");
            rows += AppendStat(builder, "Ranged Stamina Cost", equippedRanged?.StaminaCost ?? 0f, desiredRanged?.StaminaCost ?? 0f, lowerIsBetter: true);
            rows += AppendStat(builder, "Projectile Speed", equippedRanged?.ProjectileSpeed ?? 0f, desiredRanged?.ProjectileSpeed ?? 0f);
            rows += AppendStat(builder, "Projectile Lifetime", equippedRanged?.ProjectileLifetime ?? 0f, desiredRanged?.ProjectileLifetime ?? 0f, suffix: "s");
            rows += AppendStat(builder, "Projectile Hit Radius", equippedRanged?.ProjectileHitRadius ?? 0f, desiredRanged?.ProjectileHitRadius ?? 0f);
            return rows;
        }

        private static int AppendResistanceStats(StringBuilder builder, EquipmentData equipped, EquipmentData desired)
        {
            Dictionary<string, ResistanceValue> equippedValues = CollectResistances(equipped);
            Dictionary<string, ResistanceValue> desiredValues = CollectResistances(desired);
            SortedSet<string> ids = new SortedSet<string>(equippedValues.Keys, StringComparer.OrdinalIgnoreCase);
            ids.UnionWith(desiredValues.Keys);
            int rows = 0;
            foreach (string id in ids)
            {
                equippedValues.TryGetValue(id, out ResistanceValue equippedValue);
                desiredValues.TryGetValue(id, out ResistanceValue desiredValue);
                string label = !string.IsNullOrWhiteSpace(desiredValue.Label) ? desiredValue.Label : equippedValue.Label;
                rows += AppendStat(builder, $"{label} Resistance", equippedValue.Value * 100f, desiredValue.Value * 100f, suffix: "%");
            }

            return rows;
        }

        private static Dictionary<string, ResistanceValue> CollectResistances(EquipmentData equipment)
        {
            Dictionary<string, ResistanceValue> values = new Dictionary<string, ResistanceValue>(StringComparer.Ordinal);
            if (equipment == null) return values;
            IReadOnlyList<ResistanceModifierDefinition> modifiers = equipment.ResistanceModifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                ResistanceModifierDefinition modifier = modifiers[i];
                if (modifier == null || !modifier.IsValid || modifier.DamageType == null) continue;
                string id = modifier.DamageType.Id;
                if (string.IsNullOrWhiteSpace(id)) continue;
                values.TryGetValue(id, out ResistanceValue aggregate);
                aggregate.Label = modifier.DamageType.DisplayName;
                aggregate.Value += modifier.Resistance;
                values[id] = aggregate;
            }

            return values;
        }

        private static MeleeWeaponData ActiveMelee(EquipmentData equipment)
        {
            return equipment?.MeleeWeapon != null && equipment.MeleeWeapon.IsWeapon ? equipment.MeleeWeapon : null;
        }

        private static RangedWeaponData ActiveRanged(EquipmentData equipment)
        {
            return equipment?.RangedWeapon != null && equipment.RangedWeapon.IsWeapon ? equipment.RangedWeapon : null;
        }

        private static int AppendStat(StringBuilder builder, string label, float equipped, float desired, bool lowerIsBetter = false, string suffix = "")
        {
            if (Mathf.Abs(equipped) <= DisplayEpsilon && Mathf.Abs(desired) <= DisplayEpsilon) return 0;
            float change = desired - equipped;
            float improvement = lowerIsBetter ? -change : change;
            string color = improvement > DisplayEpsilon
                ? ImprovementColor
                : improvement < -DisplayEpsilon
                    ? ReductionColor
                    : UnchangedColor;
            builder.Append(label)
                .Append(": ")
                .Append(FormatSigned(equipped))
                .Append(suffix)
                .Append("  <color=")
                .Append(color)
                .Append('>')
                .Append(FormatSigned(change))
                .Append(suffix)
                .AppendLine("</color>");
            return 1;
        }

        private static string FormatSigned(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "Invalid";
            if (Mathf.Abs(value) <= 0.0001f) return "+0";
            return value > 0f ? $"+{value:0.##}" : value.ToString("0.##");
        }

        private static string EscapeRichText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unnamed item";
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private struct ResistanceValue
        {
            public string Label;
            public float Value;
        }
    }
}
