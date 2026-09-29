using UnityIsekaiGame.Combat;
using UnityIsekaiGame.CharacterSystem;
using UnityEngine;
using System.Collections.Generic;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.UI.Quests;

namespace UnityIsekaiGame.UI.Inventory
{
    public sealed class InventoryScreenController : MonoBehaviour, IPlayerMenuController
    {
        private const float QueuedRefreshIntervalSeconds = 0.1f;

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private PlayerSpellLoadout spellLoadout;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerStamina playerStamina;
        [SerializeField] private PlayerMana playerMana;
        [SerializeField] private StatusEffectController statusEffects;
        [SerializeField] private PlayerIdentityProgression identityProgression;
        [SerializeField] private CharacterSkillCollection playerSkills;
        [SerializeField] private CharacterTraitCollection playerTraits;
        [SerializeField] private CharacterSystemCoordinator characterSystem;
        [SerializeField] private InventoryScreenView view;
        [SerializeField] private SpellManagementView spellManagementView;
        [SerializeField] private QuestJournalView questJournalView;
        [SerializeField] private GameObject itemUser;
        [Header("Save/Load")]
        [SerializeField] private DefinitionCatalog saveLoadDefinitionCatalog;
        [SerializeField] private PrototypePersistenceServiceBehaviour saveLoadPersistence;
        [SerializeField, Min(1)] private int columns = 4;

        private bool isOpen;
        private int selectedSlotIndex;
        private int hoveredSlotIndex = -1;
        private EquipmentSlotType selectedEquipmentSlot;
        private int selectedKnownSpellIndex;
        private int selectedQuestIndex;
        private bool refreshing;
        private bool refreshPending;
        private float nextQueuedRefreshAt;
        private readonly Dictionary<int, EquipmentSlotType> equippedDisplaySlots = new Dictionary<int, EquipmentSlotType>();

        private void Awake()
        {
            if (equipment == null && inventory != null)
            {
                equipment = inventory.GetComponent<PlayerEquipment>();
            }

            if (spellLoadout == null && inventory != null)
            {
                spellLoadout = inventory.GetComponent<PlayerSpellLoadout>();
            }

            if (itemUser == null && inventory != null)
            {
                itemUser = inventory.gameObject;
            }

            ResolveCharacterSources();

            if (view != null)
            {
                view.Initialize(SelectSlot, UseSelectedItem, SelectEquipmentSlot, EquipSelectedItem, UnequipSelectedEquipment, HoverSlot, DropSelectedItem, DropAllSelectedItems, HoverPrimaryAction);
                view.ConfigureRuntimeServices(ResolveRuntimePersistence());
            }

            if (spellManagementView != null)
            {
                spellManagementView.Initialize(SelectKnownSpell, AssignSelectedSpellToSlot, ClearSpellSlot);
            }

            if (questJournalView != null)
            {
                questJournalView.Initialize(SelectQuest, AbandonSelectedQuest, ClaimSelectedQuestReward);
            }

            InitializeSaveLoad();

            Close(false);
            Refresh();
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged += RefreshIfOpen;
            }

            if (equipment != null)
            {
                equipment.EquipmentChanged += RefreshIfOpen;
            }

            if (spellLoadout != null)
            {
                spellLoadout.SlotChanged += OnSpellSlotChanged;
                spellLoadout.ActiveSlotChanged += OnActiveSpellSlotChanged;
            }

            if (ResolveNarrativeCoordinator() != null) ResolveNarrativeCoordinator().Changed += RefreshIfOpen;

            SubscribeCharacterSources();
        }

        private void Update()
        {
            if (isOpen && refreshPending && Time.unscaledTime >= nextQueuedRefreshAt)
            {
                Refresh();
            }

            if (input == null)
            {
                return;
            }

            if (input.ConsumeInventory())
            {
                if (!isOpen && input.GameplayInputBlocked)
                {
                    return;
                }

                SetOpen(!isOpen);
                return;
            }

            if (isOpen && PlayerCursorMode.TopMenuOwner == this && input.ConsumeCancel())
            {
                SetOpen(false);
                return;
            }

            if (!isOpen)
            {
                return;
            }

            if (input.ConsumeInventoryNavigate(out Vector2 direction))
            {
                MoveSelection(direction);
            }

            if (input.ConsumeInventoryUse())
            {
                ActivateSelectedInventoryItem();
            }
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= RefreshIfOpen;
            }

            if (equipment != null)
            {
                equipment.EquipmentChanged -= RefreshIfOpen;
            }

            if (spellLoadout != null)
            {
                spellLoadout.SlotChanged -= OnSpellSlotChanged;
                spellLoadout.ActiveSlotChanged -= OnActiveSpellSlotChanged;
            }

            if (ResolveNarrativeCoordinator() != null) ResolveNarrativeCoordinator().Changed -= RefreshIfOpen;

            UnsubscribeCharacterSources();

            if (isOpen)
            {
                Close(true);
            }
        }

        private void SetOpen(bool open)
        {
            if (open)
            {
                Open();
                return;
            }

            Close(true);
        }

        public void CloseForPrototypeReset()
        {
            if (isOpen)
            {
                Close(true);
            }
        }

        public void BeginPersistenceRestore()
        {
            if (!isOpen)
            {
                return;
            }

            if (input != null)
            {
                input.SetMenuInputBlocked(this, true, CloseFromCancel);
                input.ClearGameplayActionQueues();
                input.ClearInventoryUiActions();
            }
            else PlayerCursorMode.SetMenuOpen(this, true, CloseFromCancel);
            view?.Show();
        }

        public void CompletePersistenceRestore()
        {
            if (!isOpen)
            {
                return;
            }

            if (input != null)
            {
                input.SetMenuInputBlocked(this, true, CloseFromCancel);
                input.ClearGameplayActionQueues();
                input.ClearInventoryUiActions();
            }
            else PlayerCursorMode.SetMenuOpen(this, true, CloseFromCancel);
            Refresh();
            view?.Show();
        }

        private void Open()
        {
            if (isOpen)
            {
                return;
            }

            isOpen = true;

            if (input != null)
            {
                input.SetMenuInputBlocked(this, true, CloseFromCancel);
                input.ClearCancel();
                input.ClearInventoryUiActions();
            }
            else PlayerCursorMode.SetMenuOpen(this, true, CloseFromCancel);
            Refresh();

            if (view != null)
            {
                view.Show();
            }
        }

        private void Close(bool restoreCursor)
        {
            if (input != null)
            {
                input.ClearGameplayActionQueues();
                input.ClearInventoryUiActions();
                input.SetMenuInputBlocked(this, false);
            }
            else PlayerCursorMode.SetMenuOpen(this, false);

            if (view != null)
            {
                view.Hide();
            }

            isOpen = false;
            refreshPending = false;
        }

        private void CloseFromCancel()
        {
            SetOpen(false);
        }

        private void Refresh()
        {
            if (refreshing)
            {
                return;
            }

            refreshing = true;
            refreshPending = false;
            nextQueuedRefreshAt = Time.unscaledTime + QueuedRefreshIntervalSeconds;
            try
            {
                if (view != null && inventory != null)
                {
                    view.EnsureInventorySlotCapacity(inventory.Slots.Count);
                    RebuildEquippedDisplaySlots();
                    view.Render(inventory.Slots, equipment == null ? null : equipment.Slots, equippedDisplaySlots);
                    view.RenderEquipment(equipment == null ? null : equipment.Slots);
                    ResolveCharacterSkills();
                    view.RenderCharacter(playerStats, playerHealth, playerStamina, playerMana, statusEffects, playerStats == null ? null : playerStats.CharacterAttributes, playerStats == null ? null : playerStats.CalculatedStats, playerSkills, ResolveCharacterTraits(), characterSystem == null ? null : characterSystem.GetSnapshot(developmentView: false));
                    ClampSelection();
                    view.SetSelectedSlot(selectedSlotIndex);
                    view.SetSelectedEquipmentSlot(selectedEquipmentSlot);
                    RenderHoveredSlotDetails();
                    UpdateEquipmentActions();
                }

                if (spellManagementView != null)
                {
                    ClampKnownSpellSelection();
                    spellManagementView.Render(spellLoadout, selectedKnownSpellIndex);
                }

                if (questJournalView != null)
                {
                    System.Collections.Generic.IReadOnlyList<PrototypeQuestJournalEntry> journal = ResolveNarrativeCoordinator()?.GetJournal();
                    ClampQuestSelection();
                    questJournalView.Render(journal, selectedQuestIndex);
                }

                view?.RefreshSaveLoad();

                view?.RefreshActiveMenuExtension();
            }
            finally
            {
                refreshing = false;
            }
        }

        private void RefreshIfOpen()
        {
            if (isOpen)
            {
                refreshPending = true;
            }
        }

        public PlayerInventory Inventory => inventory;
        public PlayerEquipment Equipment => equipment;
        public bool IsOpen => isOpen;
        public PlayerStats PlayerStats => playerStats;
        public PlayerHealth PlayerHealth => playerHealth;
        public PlayerMana PlayerMana => playerMana;
        public PlayerStamina PlayerStamina => playerStamina;
        public StatusEffectController StatusEffects => statusEffects;
        public PlayerIdentityProgression IdentityProgression => identityProgression;
        public PlayerSpellLoadout SpellLoadout => spellLoadout;
        public GameObject ItemUser => itemUser;
        public DefinitionCatalog RuntimeDefinitionCatalog => ResolveSaveLoadCatalog();
        public CharacterSkillCollection RuntimeSkills => ResolveCharacterSkills();
        public CharacterTraitCollection RuntimeTraits => ResolveCharacterTraits();
        public CharacterSystemCoordinator RuntimeCharacterSystem => ResolveCharacterSystem();

        public PrototypePersistenceServiceBehaviour ResolveRuntimePersistence()
        {
            if (saveLoadPersistence == null)
            {
                saveLoadPersistence = ResolveExistingSaveLoadPersistence();
            }

            return saveLoadPersistence;
        }

        public void UseSelectedItem()
        {
            if (!isOpen || inventory == null)
            {
                return;
            }

            if (TryGetDisplayedEquipment(selectedSlotIndex, out _))
            {
                view?.SetFeedback("Unequip this item before using it.");
                return;
            }

            ItemUseResult result = inventory.UseItem(selectedSlotIndex, itemUser);
            if (!result.Succeeded)
            {
                Debug.Log(result.Message);
            }

            if (view != null)
            {
                view.SetFeedback(result.Message);
            }

            Refresh();
        }

        public void EquipSelectedItem()
        {
            if (!isOpen || equipment == null)
            {
                return;
            }

            EquipmentOperationResult result = equipment.EquipFromInventorySlot(selectedSlotIndex);
            Debug.Log(result.Message);

            if (view != null)
            {
                view.SetFeedback(result.Message);
            }

            Refresh();
        }

        public void UnequipSelectedEquipment()
        {
            if (!isOpen || equipment == null)
            {
                return;
            }

            EquipmentOperationResult result = equipment.Unequip(selectedEquipmentSlot);
            Debug.Log(result.Message);

            if (view != null)
            {
                view.SetFeedback(result.Message);
            }

            Refresh();
        }

        public void ActivateSelectedInventoryItem()
        {
            if (!isOpen || inventory == null) return;
            if (TryGetDisplayedEquipment(selectedSlotIndex, out EquipmentSlotState equippedSlot))
            {
                selectedEquipmentSlot = equippedSlot.SlotType;
                UnequipSelectedEquipment();
                return;
            }

            InventorySlot slot = inventory.GetSlot(selectedSlotIndex);
            if (slot == null || slot.IsEmpty || slot.Item == null)
            {
                view?.SetFeedback("Select an item first.");
                return;
            }

            if (slot.Item.IsUsable)
            {
                UseSelectedItem();
                return;
            }

            if (slot.Item.IsEquippable)
            {
                EquipSelectedItem();
                return;
            }

            view?.SetFeedback($"{slot.Item.DisplayName} has no available action.");
        }

        public void DropSelectedItem()
        {
            DropSelectedQuantity(dropEntireStack: false);
        }

        public void DropAllSelectedItems()
        {
            DropSelectedQuantity(dropEntireStack: true);
        }

        private void DropSelectedQuantity(bool dropEntireStack)
        {
            if (!isOpen || inventory == null)
            {
                return;
            }

            if (TryGetDisplayedEquipment(selectedSlotIndex, out _))
            {
                view?.SetFeedback("Unequip this item before dropping it.");
                return;
            }

            InventorySlot slot = inventory.GetSlot(selectedSlotIndex);
            if (slot == null || slot.IsEmpty || slot.Item == null)
            {
                view?.SetFeedback("Select an item to drop.");
                return;
            }

            Transform source = itemUser == null ? inventory.transform : itemUser.transform;
            ItemDefinition droppedItem = slot.Item;
            string droppedName = droppedItem.DisplayName;
            int quantity = dropEntireStack ? slot.Quantity : 1;
            Vector3 forward = Vector3.ProjectOnPlane(source.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Vector3 dropPosition = source.position + forward * 1.25f + Vector3.up * 0.35f;
            string message;

            PrototypePersistenceServiceBehaviour persistence = ResolveRuntimePersistence();
            if (persistence != null && !string.IsNullOrWhiteSpace(slot.ItemInstanceId))
            {
                PrototypeItemDropResult result = persistence.DropPrototypeItemToWorld(slot.ItemInstanceId, quantity, dropPosition, Quaternion.identity);
                message = result.Succeeded
                    ? FormatDropMessage(droppedName, quantity, dropEntireStack)
                    : result.Message;
            }
            else
            {
                WorldItemPickup pickup = WorldItemPickupFactory.Create(droppedItem, quantity, dropPosition, Quaternion.identity);
                if (pickup == null)
                {
                    message = "The selected item could not be dropped.";
                }
                else if (!inventory.RemoveItemAt(selectedSlotIndex, quantity))
                {
                    Destroy(pickup.gameObject);
                    message = "The selected item could not be removed from inventory.";
                }
                else
                {
                    message = FormatDropMessage(droppedName, quantity, dropEntireStack);
                }
            }

            Debug.Log(message);
            view?.SetFeedback(message);
            Refresh();
        }

        private static string FormatDropMessage(string itemName, int quantity, bool droppedEntireStack)
        {
            return droppedEntireStack && quantity > 1
                ? $"Dropped all {quantity} {itemName}."
                : $"Dropped 1 {itemName}.";
        }

        private void SelectSlot(int slotIndex)
        {
            selectedSlotIndex = Mathf.Max(0, slotIndex);
            if (equippedDisplaySlots.TryGetValue(selectedSlotIndex, out EquipmentSlotType equippedSlotType))
            {
                selectedEquipmentSlot = equippedSlotType;
            }

            if (view != null)
            {
                view.SetSelectedSlot(selectedSlotIndex);
                view.SetFeedback(string.Empty);
                RenderHoveredSlotDetails();
                UpdateEquipmentActions();
            }
        }

        private void SelectEquipmentSlot(EquipmentSlotType slotType)
        {
            selectedEquipmentSlot = slotType;

            if (view != null)
            {
                view.SetSelectedEquipmentSlot(selectedEquipmentSlot);
                view.SetFeedback(string.Empty);
                UpdateEquipmentActions();
            }
        }

        private void HoverSlot(int slotIndex, bool hovering)
        {
            if (view == null || inventory == null)
            {
                return;
            }

            if (hovering)
            {
                hoveredSlotIndex = slotIndex;
                RenderHoveredSlotDetails();
                return;
            }

            if (hoveredSlotIndex == slotIndex)
            {
                hoveredSlotIndex = -1;
            }

            RenderHoveredSlotDetails();
        }

        private void HoverPrimaryAction(bool hovering)
        {
            if (view == null || inventory == null)
            {
                return;
            }

            if (!hovering)
            {
                view.HideEquipmentComparison();
                return;
            }

            InventorySlot desiredSlot = inventory.GetSlot(selectedSlotIndex);
            if (desiredSlot == null
                || desiredSlot.IsEmpty
                || desiredSlot.Item == null
                || !desiredSlot.Item.IsEquippable
                || desiredSlot.Item.IsUsable
                || TryGetDisplayedEquipment(selectedSlotIndex, out _))
            {
                view.HideEquipmentComparison();
                return;
            }

            EquipmentSlotState currentlyEquipped = equipment?.GetSlot(desiredSlot.Item.Equipment.SlotType);
            ItemDefinition equippedItem = currentlyEquipped == null || currentlyEquipped.IsEmpty
                ? null
                : currentlyEquipped.Item;
            view.ShowEquipmentComparison(EquipmentComparisonFormatter.Format(equippedItem, desiredSlot.Item));
        }

        private void RenderHoveredSlotDetails()
        {
            if (view == null || inventory == null)
            {
                return;
            }

            int inspectedSlotIndex = hoveredSlotIndex >= 0 ? hoveredSlotIndex : selectedSlotIndex;
            if (TryGetDisplayedEquipment(inspectedSlotIndex, out EquipmentSlotState equippedSlot))
            {
                view.RenderSelectedItemDetails(equippedSlot, includeDescription: true);
                return;
            }

            InventorySlot inspectedSlot = inventory.GetSlot(inspectedSlotIndex);
            view.RenderSelectedItemDetails(inspectedSlot, includeDescription: true);
        }

        private void MoveSelection(Vector2 direction)
        {
            if (view == null || view.SlotCount <= 0)
            {
                return;
            }

            int delta;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                delta = direction.x > 0f ? 1 : -1;
            }
            else
            {
                int activeColumns = view == null ? columns : view.InventoryColumnCount;
                delta = direction.y > 0f ? -activeColumns : activeColumns;
            }

            selectedSlotIndex = Mathf.Clamp(selectedSlotIndex + delta, 0, view.SlotCount - 1);
            view.SetSelectedSlot(selectedSlotIndex);
            view.SetFeedback(string.Empty);
            RenderHoveredSlotDetails();
            UpdateEquipmentActions();
        }

        private void ClampSelection()
        {
            int slotCount = view == null ? 0 : view.SlotCount;
            selectedSlotIndex = slotCount <= 0 ? 0 : Mathf.Clamp(selectedSlotIndex, 0, slotCount - 1);
            if (hoveredSlotIndex >= slotCount) hoveredSlotIndex = -1;
        }

        private void UpdateEquipmentActions()
        {
            if (view == null)
            {
                return;
            }

            InventorySlot selectedInventorySlot = inventory == null ? null : inventory.GetSlot(selectedSlotIndex);
            bool selectedEquippedItem = TryGetDisplayedEquipment(selectedSlotIndex, out EquipmentSlotState displayedEquipmentSlot);
            bool canEquip = selectedInventorySlot != null
                && !selectedInventorySlot.IsEmpty
                && selectedInventorySlot.Item != null
                && selectedInventorySlot.Item.IsEquippable;
            bool canUse = selectedInventorySlot != null
                && !selectedInventorySlot.IsEmpty
                && selectedInventorySlot.Item != null
                && selectedInventorySlot.Item.IsUsable;
            bool canDrop = !selectedEquippedItem && selectedInventorySlot != null
                && !selectedInventorySlot.IsEmpty
                && selectedInventorySlot.Item != null;

            EquipmentSlotState selectedEquipment = equipment == null ? null : equipment.GetSlot(selectedEquipmentSlot);
            bool canUnequip = selectedEquipment != null && !selectedEquipment.IsEmpty;

            view.SetEquipmentActions(canEquip, canUnequip);
            bool canDropAll = canDrop && selectedInventorySlot.Quantity > 1;
            if (selectedEquippedItem)
            {
                selectedEquipmentSlot = displayedEquipmentSlot.SlotType;
                canUse = false;
                canEquip = false;
            }
            ItemDefinition actionItem = selectedEquippedItem ? displayedEquipmentSlot?.Item : selectedInventorySlot?.Item;
            view.SetInventoryActions(canUse, canEquip, canDrop, canDropAll, canUnequip: selectedEquippedItem, actionItem: actionItem);
        }

        private void RebuildEquippedDisplaySlots()
        {
            equippedDisplaySlots.Clear();
            if (inventory == null || equipment == null || view == null) return;
            Dictionary<int, EquipmentSlotType> planned = InventoryDisplaySlotPlanner.Plan(
                inventory.Slots,
                equipment.Slots);
            foreach (KeyValuePair<int, EquipmentSlotType> entry in planned) equippedDisplaySlots.Add(entry.Key, entry.Value);
        }

        private bool TryGetDisplayedEquipment(int displaySlotIndex, out EquipmentSlotState equipmentSlot)
        {
            equipmentSlot = null;
            if (equipment == null || !equippedDisplaySlots.TryGetValue(displaySlotIndex, out EquipmentSlotType slotType)) return false;
            equipmentSlot = equipment.GetSlot(slotType);
            return equipmentSlot != null && !equipmentSlot.IsEmpty;
        }

        private void SelectKnownSpell(int knownSpellIndex)
        {
            selectedKnownSpellIndex = Mathf.Max(0, knownSpellIndex);
            if (view != null)
            {
                view.SetFeedback(string.Empty);
            }

            Refresh();
        }

        private void AssignSelectedSpellToSlot(int slotIndex)
        {
            if (!isOpen || spellLoadout == null)
            {
                return;
            }

            SpellDefinition spell = selectedKnownSpellIndex >= 0 && selectedKnownSpellIndex < spellLoadout.KnownSpells.Count
                ? spellLoadout.KnownSpells[selectedKnownSpellIndex]
                : null;
            SpellLoadoutOperationResult result = spellLoadout.AssignSpell(slotIndex, spell);
            Debug.Log(result.Message);
            view?.SetFeedback(result.Message);
            Refresh();
        }

        private void ClearSpellSlot(int slotIndex)
        {
            if (!isOpen || spellLoadout == null)
            {
                return;
            }

            SpellLoadoutOperationResult result = spellLoadout.ClearSlot(slotIndex);
            Debug.Log(result.Message);
            view?.SetFeedback(result.Message);
            Refresh();
        }

        private void ClampKnownSpellSelection()
        {
            int knownCount = spellLoadout == null || spellLoadout.KnownSpells == null ? 0 : spellLoadout.KnownSpells.Count;
            selectedKnownSpellIndex = knownCount <= 0 ? 0 : Mathf.Clamp(selectedKnownSpellIndex, 0, knownCount - 1);
        }

        private void OnSpellSlotChanged(SpellLoadoutSlotChangedEventArgs args)
        {
            RefreshIfOpen();
        }

        private void OnActiveSpellSlotChanged(int slotIndex, SpellDefinition spell)
        {
            RefreshIfOpen();
        }

        private void SelectQuest(int questIndex)
        {
            selectedQuestIndex = Mathf.Max(0, questIndex);
            questJournalView?.SetFeedback(string.Empty);
            Refresh();
        }

        private void AbandonSelectedQuest()
        {
            PrototypeQuestJournalEntry quest = GetSelectedQuest();
            QuestParticipationOperationResult result = quest == null || ResolveNarrativeCoordinator() == null
                ? QuestParticipationOperationResult.Failure(QuestParticipationOperationStatus.InvalidRequest, "No quest assignment is selected.", 0L)
                : ResolveNarrativeCoordinator().AbandonAssignment(quest.AssignmentId);
            questJournalView?.SetFeedback(result?.Message ?? "Quest could not be abandoned.");
            Refresh();
        }

        private void ClaimSelectedQuestReward()
        {
            PrototypeQuestJournalEntry quest = GetSelectedQuest();
            QuestOutcomeOperationResult result = quest?.ClaimableReward == null || ResolveNarrativeCoordinator() == null
                ? QuestOutcomeOperationResult.Failure(QuestOutcomeOperationStatus.InvalidRequest, "No claimable quest reward is selected.", 0L)
                : ResolveNarrativeCoordinator().ClaimReward(quest.ClaimableReward.EntitlementId);
            questJournalView?.SetFeedback(result?.Message ?? "Reward could not be claimed.");
            Refresh();
        }

        private void ClampQuestSelection()
        {
            int questCount = ResolveNarrativeCoordinator()?.GetJournal()?.Count ?? 0;
            selectedQuestIndex = questCount <= 0 ? 0 : Mathf.Clamp(selectedQuestIndex, 0, questCount - 1);
        }

        private PrototypeQuestJournalEntry GetSelectedQuest()
        {
            System.Collections.Generic.IReadOnlyList<PrototypeQuestJournalEntry> quests = ResolveNarrativeCoordinator()?.GetJournal();
            return quests == null || selectedQuestIndex < 0 || selectedQuestIndex >= quests.Count ? null : quests[selectedQuestIndex];
        }

        private PrototypeNarrativeCoordinator ResolveNarrativeCoordinator()
        {
            return ResolveRuntimePersistence()?.NarrativeCoordinator;
        }

        private void ResolveCharacterSources()
        {
            GameObject source = itemUser != null ? itemUser : inventory == null ? null : inventory.gameObject;
            if (source == null)
            {
                return;
            }

            if (playerStats == null)
            {
                playerStats = source.GetComponentInParent<PlayerStats>();
            }

            if (playerHealth == null)
            {
                playerHealth = source.GetComponentInParent<PlayerHealth>();
            }

            if (playerStamina == null)
            {
                playerStamina = source.GetComponentInParent<PlayerStamina>();
            }

            if (playerMana == null)
            {
                playerMana = source.GetComponentInParent<PlayerMana>();
            }

            if (statusEffects == null)
            {
                statusEffects = source.GetComponentInParent<StatusEffectController>();
            }

            if (identityProgression == null)
            {
                identityProgression = source.GetComponentInParent<PlayerIdentityProgression>();
            }

            ResolveCharacterSkills();
            ResolveCharacterTraits();
            ResolveCharacterSystem();
        }

        private CharacterSystemCoordinator ResolveCharacterSystem()
        {
            GameObject source = itemUser != null ? itemUser : inventory == null ? null : inventory.gameObject;
            if (characterSystem == null && playerStats != null)
            {
                characterSystem = playerStats.GetComponent<CharacterSystemCoordinator>();
            }

            if (characterSystem == null && source != null)
            {
                characterSystem = source.GetComponentInParent<CharacterSystemCoordinator>();
            }

            return characterSystem;
        }

        private CharacterSkillCollection ResolveCharacterSkills()
        {
            GameObject source = itemUser != null ? itemUser : inventory == null ? null : inventory.gameObject;
            if (playerSkills == null)
            {
                playerSkills = playerStats == null ? null : playerStats.GetComponent<CharacterSkillCollection>();
            }

            if (playerSkills == null && source != null)
            {
                playerSkills = source.GetComponentInParent<CharacterSkillCollection>();
            }

            return playerSkills;
        }

        private CharacterTraitCollection ResolveCharacterTraits()
        {
            GameObject source = itemUser != null ? itemUser : inventory == null ? null : inventory.gameObject;
            if (playerTraits == null)
            {
                playerTraits = playerStats == null ? null : playerStats.GetComponent<CharacterTraitCollection>();
            }

            if (playerTraits == null && source != null)
            {
                playerTraits = source.GetComponentInParent<CharacterTraitCollection>();
            }

            return playerTraits;
        }

        private void SubscribeCharacterSources()
        {
            if (playerStats != null)
            {
                playerStats.StatsChanged += RefreshIfOpen;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged += OnHealthChanged;
            }

            if (playerStamina != null)
            {
                playerStamina.StaminaChanged += OnStaminaChanged;
            }

            if (playerMana != null)
            {
                playerMana.ManaChanged += OnManaChanged;
            }

            if (statusEffects != null)
            {
                statusEffects.StatusAdded += OnStatusChanged;
                statusEffects.StatusChanged += OnStatusChanged;
                statusEffects.StatusRemoved += OnStatusChanged;
                statusEffects.StatusExpired += OnStatusChanged;
            }

            if (identityProgression != null)
            {
                identityProgression.ProgressionChanged += OnIdentityProgressionChanged;
            }

            if (playerSkills != null)
            {
                playerSkills.SkillsChanged += OnSkillsChanged;
                playerSkills.HiddenProgressChanged += OnSkillHiddenProgressChanged;
            }

            if (playerTraits != null)
            {
                playerTraits.TraitsChanged += OnTraitsChanged;
                playerTraits.TraitRecordChanged += OnTraitRecordChanged;
            }

            if (characterSystem != null)
            {
                characterSystem.CharacterRevisionChanged += OnCharacterRevisionChanged;
                characterSystem.CharacterReady += OnCharacterReady;
                characterSystem.CharacterDisposed += OnCharacterDisposed;
            }
        }

        private void UnsubscribeCharacterSources()
        {
            if (playerStats != null)
            {
                playerStats.StatsChanged -= RefreshIfOpen;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= OnHealthChanged;
            }

            if (playerStamina != null)
            {
                playerStamina.StaminaChanged -= OnStaminaChanged;
            }

            if (playerMana != null)
            {
                playerMana.ManaChanged -= OnManaChanged;
            }

            if (statusEffects != null)
            {
                statusEffects.StatusAdded -= OnStatusChanged;
                statusEffects.StatusChanged -= OnStatusChanged;
                statusEffects.StatusRemoved -= OnStatusChanged;
                statusEffects.StatusExpired -= OnStatusChanged;
            }

            if (identityProgression != null)
            {
                identityProgression.ProgressionChanged -= OnIdentityProgressionChanged;
            }

            if (playerSkills != null)
            {
                playerSkills.SkillsChanged -= OnSkillsChanged;
                playerSkills.HiddenProgressChanged -= OnSkillHiddenProgressChanged;
            }

            if (playerTraits != null)
            {
                playerTraits.TraitsChanged -= OnTraitsChanged;
                playerTraits.TraitRecordChanged -= OnTraitRecordChanged;
            }

            if (characterSystem != null)
            {
                characterSystem.CharacterRevisionChanged -= OnCharacterRevisionChanged;
                characterSystem.CharacterReady -= OnCharacterReady;
                characterSystem.CharacterDisposed -= OnCharacterDisposed;
            }
        }

        private void OnHealthChanged(int current, int maximum)
        {
            RefreshIfOpen();
        }

        private void OnStaminaChanged(float current, float maximum)
        {
            RefreshIfOpen();
        }

        private void OnManaChanged(float current, float maximum)
        {
            RefreshIfOpen();
        }

        private void OnStatusChanged(RuntimeStatusEffect status)
        {
            RefreshIfOpen();
        }

        private void OnIdentityProgressionChanged(PlayerIdentityProgression progression, bool restoring)
        {
            RefreshIfOpen();
        }

        private void OnSkillsChanged(CharacterSkillCollection collection, bool restoring)
        {
            RefreshIfOpen();
        }

        private void OnSkillHiddenProgressChanged(CharacterSkillCollection collection, SkillLearningProgressRecord progress, bool restoring)
        {
            RefreshIfOpen();
        }

        private void OnTraitsChanged(CharacterTraitCollection collection, TraitOperationResult result, bool restoring)
        {
            RefreshIfOpen();
        }

        private void OnTraitRecordChanged(CharacterTraitCollection collection, RuntimeTraitRecord record, bool restoring)
        {
            RefreshIfOpen();
        }

        private void OnCharacterRevisionChanged(CharacterSystemCoordinator coordinator, long revision, bool restoring, string reason)
        {
            RefreshIfOpen();
        }

        private void OnCharacterReady(CharacterSystemCoordinator coordinator, bool restoring)
        {
            RefreshIfOpen();
        }

        private void OnCharacterDisposed(CharacterSystemCoordinator coordinator, bool restoring)
        {
            RefreshIfOpen();
        }

        private void InitializeSaveLoad()
        {
            if (view == null)
            {
                return;
            }

            PrototypePersistenceServiceBehaviour persistence = ResolveRuntimePersistence();
            view.InitializeSaveLoad(persistence);
        }

        private DefinitionCatalog ResolveSaveLoadCatalog()
        {
            if (saveLoadDefinitionCatalog != null)
            {
                return saveLoadDefinitionCatalog;
            }

            PrototypePersistenceServiceBehaviour persistence = ResolveRuntimePersistence();
            if (persistence != null)
            {
                saveLoadPersistence = persistence;
                saveLoadDefinitionCatalog = persistence.DefinitionCatalog;
            }

            return saveLoadDefinitionCatalog;
        }

        private PrototypePersistenceServiceBehaviour ResolveExistingSaveLoadPersistence()
        {
            GameObject actor = itemUser != null ? itemUser : inventory == null ? null : inventory.gameObject;
            PrototypePersistenceServiceBehaviour owned = PrototypePersistenceServiceBehaviour.FindForInteractor(actor);
            if (owned != null) return owned;

            PrototypePersistenceServiceBehaviour[] persistenceServices = UnityEngine.Object.FindObjectsByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < persistenceServices.Length; i++)
            {
                PrototypePersistenceServiceBehaviour candidate = persistenceServices[i];
                if (candidate != null
                    && ((inventory != null && ReferenceEquals(candidate.PlayerInventory, inventory))
                        || (equipment != null && ReferenceEquals(candidate.PlayerEquipment, equipment))))
                {
                    return candidate;
                }
            }
            return null;
        }

    }

    public static class InventoryDisplaySlotPlanner
    {
        public static Dictionary<int, EquipmentSlotType> Plan(
            IReadOnlyList<InventorySlot> inventorySlots,
            IReadOnlyList<EquipmentSlotState> equipmentSlots)
        {
            Dictionary<int, EquipmentSlotType> result = new Dictionary<int, EquipmentSlotType>();
            if (inventorySlots == null || equipmentSlots == null) return result;

            for (int equipmentIndex = 0; equipmentIndex < equipmentSlots.Count; equipmentIndex++)
            {
                EquipmentSlotState equippedSlot = equipmentSlots[equipmentIndex];
                if (equippedSlot == null || equippedSlot.IsEmpty) continue;
                for (int inventoryIndex = 0; inventoryIndex < inventorySlots.Count; inventoryIndex++)
                {
                    InventorySlot inventorySlot = inventorySlots[inventoryIndex];
                    if (inventorySlot == null || inventorySlot.IsEmpty
                        || !string.Equals(inventorySlot.ItemInstanceId, equippedSlot.ItemInstanceId, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    result[inventoryIndex] = equippedSlot.SlotType;
                    break;
                }
            }

            return result;
        }
    }
}
