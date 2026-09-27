using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.CharacterSystem;
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

namespace UnityIsekaiGame.UI.Inventory
{
    public sealed class InventoryScreenView : MonoBehaviour
    {
        private const float NavigationButtonHeight = 42f;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private InventorySlotView[] slotViews;
        [SerializeField] private EquipmentSlotView[] equipmentSlotViews;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button useButton;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button unequipButton;
        [SerializeField] private GameObject selectedItemDetailsRoot;
        [SerializeField] private Image selectedItemIconImage;
        [SerializeField] private Text selectedItemIconFallbackText;
        [SerializeField] private Text selectedItemHeaderText;
        [SerializeField] private Text selectedItemDetailsText;
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
        [SerializeField] private Color inactiveMenuColor = new Color(0.12f, 0.14f, 0.16f, 0.95f);
        [SerializeField] private Color activeMenuColor = new Color(0.2f, 0.42f, 0.55f, 1f);

        private Action useSelected;
        private Action equipSelected;
        private Action unequipSelected;
        private InventoryMenuSection activeSection = InventoryMenuSection.Inventory;
        private InventoryMenuExtensionBinding activeExtension;
        private readonly List<InventoryMenuExtensionBinding> menuExtensions = new List<InventoryMenuExtensionBinding>();
        private PrototypePersistenceServiceBehaviour economyServices;
        private ScrollRect selectedItemDetailsScroll;
        private ScrollRect characterStatsScroll;
        private GridLayoutGroup inventorySlotGridLayout;
        private RectTransform inventorySlotGridRect;
        private float lastInventoryGridWidth = -1f;
        private float lastInventoryGridHeight = -1f;
        private string inspectedItemKey = string.Empty;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            inactiveMenuColor = PrototypeUiTheme.PanelRaised;
            activeMenuColor = PrototypeUiTheme.AccentSoft;
            EnsureItemDetailsPanel();
            ApplyPrototypeMenuLayout();
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

        public int SlotCount => slotViews == null ? 0 : slotViews.Length;

        public void Initialize(Action<int> onSlotSelected, Action onUseSelected, Action<EquipmentSlotType> onEquipmentSlotSelected = null, Action onEquipSelected = null, Action onUnequipSelected = null, Action<int, bool> onSlotHovered = null)
        {
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
                useButton.onClick.RemoveListener(InvokeUseSelected);
                useButton.onClick.AddListener(InvokeUseSelected);
            }

            useSelected = onUseSelected;

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

            if (equipButton != null)
            {
                equipButton.onClick.RemoveListener(InvokeEquipSelected);
                equipButton.onClick.AddListener(InvokeEquipSelected);
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
            ApplyPrototypeMenuLayout();
            ApplyTheme();
            ApplyActiveSection();
            Canvas.ForceUpdateCanvases();
            UpdateResponsiveInventoryGrid(force: true);
        }

        public void InitializeSaveLoad(PrototypePersistenceServiceBehaviour persistence)
        {
            EnsureSaveLoadMenuObjects();
            saveLoadView?.Initialize(persistence);
            ApplyActiveSection();
        }

        public void RefreshSaveLoad()
        {
            saveLoadView?.Refresh();
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
            ApplyPrototypeMenuLayout();
            ApplyActiveSection();
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
                ApplyPrototypeMenuLayout();
                ApplyActiveSection();
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

        public void Render(IReadOnlyList<InventorySlot> slots)
        {
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

                if (slots != null && i < slots.Count)
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
                if (selectedItemDetailsRoot != null)
                {
                    selectedItemDetailsRoot.SetActive(false);
                }

                inspectedItemKey = string.Empty;

                return;
            }

            EnsureItemDetailsPanel();

            if (selectedItemDetailsRoot != null)
            {
                selectedItemDetailsRoot.SetActive(true);
            }

            Sprite itemIcon = InventoryItemIconResolver.Resolve(slot.Item);
            if (selectedItemIconImage != null)
            {
                selectedItemIconImage.sprite = itemIcon;
                selectedItemIconImage.enabled = itemIcon != null;
                selectedItemIconImage.preserveAspect = true;
            }

            if (selectedItemIconFallbackText != null)
            {
                selectedItemIconFallbackText.gameObject.SetActive(itemIcon == null);
                selectedItemIconFallbackText.text = GetItemMonogram(slot.Item);
            }

            if (selectedItemHeaderText != null)
            {
                selectedItemHeaderText.text = InventoryItemDetailsFormatter.GetHeader(slot);
            }

            if (selectedItemDetailsText != null)
            {
                selectedItemDetailsText.text = InventoryItemDetailsFormatter.FormatDetails(slot, includeDescription);
            }

            string nextItemKey = string.IsNullOrWhiteSpace(slot.ItemInstanceId) ? slot.Item.ItemId : slot.ItemInstanceId;
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
                economyServices ??= FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
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
            if (equipButton != null)
            {
                equipButton.gameObject.SetActive(canEquip);
            }

            if (unequipButton != null)
            {
                unequipButton.gameObject.SetActive(canUnequip);
            }
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
            if (canvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void InvokeUseSelected()
        {
            useSelected?.Invoke();
        }

        private void InvokeEquipSelected()
        {
            equipSelected?.Invoke();
        }

        private void InvokeUnequipSelected()
        {
            unequipSelected?.Invoke();
        }

        private void ShowInventorySection()
        {
            activeSection = InventoryMenuSection.Inventory;
            ApplyActiveSection();
        }

        private void ShowSpellsSection()
        {
            activeSection = InventoryMenuSection.Spells;
            ApplyActiveSection();
        }

        private void ShowCharacterSection()
        {
            activeSection = InventoryMenuSection.Character;
            ApplyActiveSection();
        }

        private void ShowContractsSection()
        {
            activeSection = InventoryMenuSection.Contracts;
            ApplyActiveSection();
        }

        private void ShowSaveLoadSection()
        {
            activeSection = InventoryMenuSection.SaveLoad;
            saveLoadView?.Refresh();
            ApplyActiveSection();
        }

        private void ApplyActiveSection()
        {
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
        }

        private void ApplyPrototypeMenuLayout()
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
                    navigationRect.anchorMin = new Vector2(0f, 1f);
                    navigationRect.anchorMax = Vector2.one;
                    navigationRect.pivot = new Vector2(0.5f, 1f);
                    navigationRect.offsetMin = new Vector2(18f, -70f);
                    navigationRect.offsetMax = new Vector2(-18f, -16f);
                }

                if (navigationParent.TryGetComponent(out Image navigationImage))
                {
                    navigationImage.color = new Color(PrototypeUiTheme.PanelRaised.r, PrototypeUiTheme.PanelRaised.g, PrototypeUiTheme.PanelRaised.b, 0.82f);
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

            float step = 1f / buttons.Count;
            for (int i = 0; i < buttons.Count; i++)
            {
                RectTransform rect = buttons[i].GetComponent<RectTransform>();
                if (rect == null)
                {
                    continue;
                }

                rect.anchorMin = new Vector2(i * step, 0f);
                rect.anchorMax = new Vector2((i + 1) * step, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(i == 0 ? 6f : 3f, 5f);
                rect.offsetMax = new Vector2(i == buttons.Count - 1 ? -6f : -3f, -5f);
            }
        }

        private void ConfigureMainPanel()
        {
            if (transform is RectTransform panelRect)
            {
                panelRect.anchorMin = new Vector2(0.075f, 0.075f);
                panelRect.anchorMax = new Vector2(0.925f, 0.925f);
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
            }

            if (TryGetComponent(out Image panelImage))
            {
                panelImage.color = PrototypeUiTheme.Backdrop;
                EnsureOutline(gameObject, PrototypeUiTheme.Border);
            }

            Transform contentParent = inventoryContentRoot == null ? null : inventoryContentRoot.transform.parent;
            if (contentParent is RectTransform contentRect)
            {
                contentRect.anchorMin = Vector2.zero;
                contentRect.anchorMax = Vector2.one;
                contentRect.offsetMin = new Vector2(18f, 60f);
                contentRect.offsetMax = new Vector2(-18f, -82f);
                if (contentParent.TryGetComponent(out Image contentImage))
                {
                    contentImage.color = new Color(PrototypeUiTheme.Panel.r, PrototypeUiTheme.Panel.g, PrototypeUiTheme.Panel.b, 0.78f);
                }
            }

            if (feedbackText != null)
            {
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

            inventorySlotGridLayout = inventoryContentRoot.GetComponentInChildren<GridLayoutGroup>(true);
            inventorySlotGridRect = inventorySlotGridLayout == null ? null : inventorySlotGridLayout.GetComponent<RectTransform>();
            if (inventorySlotGridLayout != null && inventorySlotGridRect != null)
            {
                inventorySlotGridRect.anchorMin = new Vector2(0.02f, 0.14f);
                inventorySlotGridRect.anchorMax = new Vector2(0.63f, 0.885f);
                inventorySlotGridRect.offsetMin = Vector2.zero;
                inventorySlotGridRect.offsetMax = Vector2.zero;
                inventorySlotGridLayout.padding = new RectOffset(0, 0, 0, 0);
                inventorySlotGridLayout.spacing = new Vector2(9f, 9f);
                inventorySlotGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                inventorySlotGridLayout.constraintCount = 4;
                inventorySlotGridLayout.childAlignment = TextAnchor.UpperLeft;
            }

            ConfigureInventoryActionButton(useButton, new Vector2(0.02f, 0.03f), new Vector2(0.31f, 0.115f));
            ConfigureInventoryActionButton(equipButton, new Vector2(0.33f, 0.03f), new Vector2(0.62f, 0.115f));

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

        private void ConfigureInventoryActionButton(Button button, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (button == null || inventoryContentRoot == null || !button.transform.IsChildOf(inventoryContentRoot.transform))
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
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

                inventorySlotGridLayout = inventoryContentRoot.GetComponentInChildren<GridLayoutGroup>(true);
                inventorySlotGridRect = inventorySlotGridLayout == null ? null : inventorySlotGridLayout.GetComponent<RectTransform>();
            }

            if (inventorySlotGridLayout == null || inventorySlotGridRect == null)
            {
                return;
            }

            float width = inventorySlotGridRect.rect.width;
            float height = inventorySlotGridRect.rect.height;
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
            Vector2 spacing = inventorySlotGridLayout.spacing;
            float cellWidth = Mathf.Max(72f, Mathf.Floor((width - spacing.x * 3f) / 4f));
            float cellHeight = Mathf.Max(58f, Mathf.Floor((height - spacing.y * 3f) / 4f));
            inventorySlotGridLayout.cellSize = new Vector2(cellWidth, cellHeight);
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

        private static void EnsureVerticalScrollbar(ScrollRect scroll, RectTransform viewport)
        {
            if (scroll == null || viewport == null || scroll.verticalScrollbar != null)
            {
                return;
            }

            GameObject trackObject = new GameObject("Scrollbar Vertical", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            trackObject.transform.SetParent(scroll.transform, false);
            RectTransform trackRect = trackObject.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(1f, 0f);
            trackRect.anchorMax = Vector2.one;
            trackRect.pivot = new Vector2(1f, 0.5f);
            trackRect.offsetMin = new Vector2(-9f, 3f);
            trackRect.offsetMax = new Vector2(-2f, -3f);
            Image trackImage = trackObject.GetComponent<Image>();
            trackImage.color = new Color(0.02f, 0.025f, 0.03f, 0.92f);

            GameObject slidingAreaObject = new GameObject("Sliding Area", typeof(RectTransform));
            slidingAreaObject.transform.SetParent(trackObject.transform, false);
            RectTransform slidingArea = slidingAreaObject.GetComponent<RectTransform>();
            slidingArea.anchorMin = Vector2.zero;
            slidingArea.anchorMax = Vector2.one;
            slidingArea.offsetMin = new Vector2(1f, 1f);
            slidingArea.offsetMax = new Vector2(-1f, -1f);

            GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(slidingAreaObject.transform, false);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            Image handleImage = handleObject.GetComponent<Image>();
            handleImage.color = new Color(PrototypeUiTheme.Secondary.r, PrototypeUiTheme.Secondary.g, PrototypeUiTheme.Secondary.b, 0.9f);

            Scrollbar scrollbar = trackObject.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarSpacing = 4f;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        }

        private void ApplyTheme()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                PrototypeUiTheme.StyleButton(buttons[i], PrototypeUiTheme.InferButtonTone(buttons[i].name));
            }

            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                PrototypeUiTheme.StyleText(texts[i], PrototypeUiTheme.InferTextRole(texts[i].name));
            }

            if (feedbackText != null)
            {
                PrototypeUiTheme.StyleText(feedbackText, PrototypeUiTextRole.Feedback);
            }
            if (selectedItemHeaderText != null)
            {
                PrototypeUiTheme.StyleText(selectedItemHeaderText, PrototypeUiTextRole.Heading);
            }
            if (selectedItemDetailsText != null)
            {
                PrototypeUiTheme.StyleText(selectedItemDetailsText, PrototypeUiTextRole.Muted);
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
                text.fontSize = 13;
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
            PrototypeUiTheme.StyleButton(button);

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

            RectTransform rootRect = selectedItemDetailsRoot.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.anchorMin = new Vector2(0.66f, 0.04f);
                rootRect.anchorMax = new Vector2(0.985f, 0.96f);
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            Image rootImage = selectedItemDetailsRoot.GetComponent<Image>();
            PrototypeUiTheme.StylePanel(rootImage, raised: true);
            EnsureOutline(selectedItemDetailsRoot, PrototypeUiTheme.Border);

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
            artworkFrameRect.anchorMin = new Vector2(0.19f, 0.67f);
            artworkFrameRect.anchorMax = new Vector2(0.81f, 0.965f);
            artworkFrameRect.offsetMin = Vector2.zero;
            artworkFrameRect.offsetMax = Vector2.zero;
            artworkFrame.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.04f, 0.98f);
            Outline artworkOutline = artworkFrame.GetComponent<Outline>();
            artworkOutline.effectColor = new Color(PrototypeUiTheme.Accent.r, PrototypeUiTheme.Accent.g, PrototypeUiTheme.Accent.b, 0.65f);
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
            selectedItemIconFallbackText.color = PrototypeUiTheme.TextMuted;
            selectedItemIconFallbackText.raycastTarget = false;

            if (selectedItemHeaderText == null)
            {
                selectedItemHeaderText = CreateDetailsText("Item Header", selectedItemDetailsRoot.transform, font, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            }

            RectTransform headerRect = selectedItemHeaderText.rectTransform;
            headerRect.anchorMin = new Vector2(0.05f, 0.56f);
            headerRect.anchorMax = new Vector2(0.95f, 0.65f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;
            selectedItemHeaderText.alignment = TextAnchor.MiddleCenter;
            selectedItemHeaderText.horizontalOverflow = HorizontalWrapMode.Wrap;
            selectedItemHeaderText.verticalOverflow = VerticalWrapMode.Truncate;

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
                scrollRect.anchorMin = new Vector2(0.045f, 0.035f);
                scrollRect.anchorMax = new Vector2(0.955f, 0.545f);
                scrollRect.offsetMin = Vector2.zero;
                scrollRect.offsetMax = Vector2.zero;
                scrollObject.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.04f, 0.58f);

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
                scrollObject.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.04f, 0.42f);

                GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                viewport.transform.SetParent(scrollObject.transform, false);
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
            image.color = new Color(0.08f, 0.1f, 0.12f, 0.95f);

            Outline outline = root.GetComponent<Outline>();
            outline.effectColor = new Color(0.28f, 0.35f, 0.39f, 0.9f);
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
            image.color = new Color(0.06f, 0.08f, 0.09f, 0.92f);

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
            if (item == null)
            {
                return "No item selected";
            }

            return string.IsNullOrWhiteSpace(item.DisplayName) ? item.ItemId : item.DisplayName;
        }

        public static string FormatDetails(InventorySlot slot, bool includeDescription = false)
        {
            ItemDefinition item = slot == null || slot.IsEmpty ? null : slot.Item;
            if (item == null)
            {
                return "Select an inventory slot to inspect item type, tags, stack size, and stats.";
            }

            StringBuilder builder = new StringBuilder();
            AppendLine(builder, "Definition ID", string.IsNullOrWhiteSpace(item.ItemId) ? "Unassigned" : item.ItemId);
            AppendLine(builder, "Type", GetCategoryName(item.PrimaryCategory));
            AppendLine(builder, "Rarity", item.Rarity == null ? "Unassigned" : item.Rarity.DisplayName);
            AppendLine(builder, "Tags", FormatTags(item.Tags));
            AppendLine(builder, "Quantity", slot.Quantity.ToString());
            AppendLine(builder, "Stack", item.Stackable ? $"Stackable, max {item.MaximumStackSize}" : "Not stackable");
            AppendLine(builder, "Instance Mode", slot.IsStateful ? "Stateful instance" : item.InstanceMode.ToString());
            AppendLine(builder, "Capability", FormatCapabilities(item));
            AppendInstanceDetails(builder, slot.ItemInstanceId, item);

            if (includeDescription && !string.IsNullOrWhiteSpace(item.Description))
            {
                builder.AppendLine();
                builder.AppendLine(item.Description);
                AppendDescriptionInstanceId(builder, slot.ItemInstanceId);
            }

            AppendUseDetails(builder, item);
            AppendEquipmentDetails(builder, item.Equipment);

            return builder.ToString().TrimEnd();
        }

        private static void AppendInstanceDetails(StringBuilder builder, string itemInstanceId, ItemDefinition item)
        {
            if (string.IsNullOrWhiteSpace(itemInstanceId))
            {
                if (item != null && item.InstanceMode != UnityIsekaiGame.GameData.ItemInstanceMode.DefinitionOnly)
                {
                    AppendLine(builder, "Unique Instance ID", "Not instanced");
                }

                return;
            }

            AppendLine(builder, "Unique Instance ID", itemInstanceId);
        }

        private static void AppendDescriptionInstanceId(StringBuilder builder, string itemInstanceId)
        {
            if (string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return;
            }

            builder.AppendLine();
            AppendLine(builder, "Unique Instance ID", itemInstanceId);
        }

        private static void AppendUseDetails(StringBuilder builder, ItemDefinition item)
        {
            if (item == null || !item.IsUsable)
            {
                return;
            }

            builder.AppendLine();
            AppendLine(builder, "Use Effects", $"{item.UseEffectCount} configured");

            if (item.HasMissingUseEffect)
            {
                AppendLine(builder, "Use Warning", "Missing effect reference");
            }
        }

        private static void AppendEquipmentDetails(StringBuilder builder, EquipmentData equipment)
        {
            if (equipment == null || !equipment.Equippable)
            {
                return;
            }

            builder.AppendLine();
            AppendLine(builder, "Equip Slot", SplitPascalCase(equipment.SlotType.ToString()));
            AppendLine(builder, "Stats", FormatStats(equipment.StatModifiers));

            if (equipment.MeleeWeapon != null && equipment.MeleeWeapon.IsWeapon)
            {
                AppendLine(builder, "Attack", equipment.MeleeWeapon.AttackName);
                AppendLine(builder, "Damage", FormatNumber(equipment.MeleeWeapon.BaseDamage));
                AppendLine(builder, "Range", FormatNumber(equipment.MeleeWeapon.AttackRange));
                AppendLine(builder, "Cooldown", $"{FormatNumber(equipment.MeleeWeapon.AttackCooldown)}s");
                AppendLine(builder, "Stamina Cost", FormatNumber(equipment.MeleeWeapon.StaminaCost));
            }
        }

        private static string FormatCapabilities(ItemDefinition item)
        {
            bool usable = item != null && item.IsUsable;
            bool equippable = item != null && item.IsEquippable;

            if (usable && equippable)
            {
                return "Usable, equippable";
            }

            if (usable)
            {
                return "Usable";
            }

            if (equippable)
            {
                return "Equippable";
            }

            return "Inventory item";
        }

        private static string FormatStats(StatModifiers stats)
        {
            List<string> parts = new List<string>();
            AddStat(parts, "Max Health", stats.MaximumHealth);
            AddStat(parts, "Max Stamina", stats.MaximumStamina);
            AddStat(parts, "Max Mana", stats.MaximumMana);
            AddStat(parts, "Attack", stats.AttackPower);
            AddStat(parts, "Defense", stats.Defense);

            return parts.Count == 0 ? "None" : string.Join(", ", parts);
        }

        private static void AddStat(List<string> parts, string label, float value)
        {
            if (value == 0f)
            {
                return;
            }

            string sign = value > 0f ? "+" : string.Empty;
            parts.Add($"{label} {sign}{FormatNumber(value)}");
        }

        private static string FormatTags(IReadOnlyList<TagDefinition> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return "None";
            }

            List<string> names = new List<string>();
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] != null)
                {
                    names.Add(tags[i].DisplayName);
                }
            }

            return names.Count == 0 ? "None" : string.Join(", ", names);
        }

        private static string GetCategoryName(CategoryDefinition category)
        {
            if (category == null)
            {
                return "Uncategorized";
            }

            return category.DisplayName;
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
    }
}
