using UnityIsekaiGame.Combat;
using UnityIsekaiGame.CharacterSystem;
using UnityEngine;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Contracts;
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
using UnityIsekaiGame.UI.Contracts;
using UnityIsekaiGame.UI.Quests;

namespace UnityIsekaiGame.UI.Inventory
{
    public sealed class InventoryScreenController : MonoBehaviour, IPlayerMenuController
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private PlayerSpellLoadout spellLoadout;
        [SerializeField] private PlayerContractJournal contractJournal;
        [SerializeField] private PlayerQuestLog questLog;
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
        [SerializeField] private ContractJournalView contractJournalView;
        [SerializeField] private QuestJournalView questJournalView;
        [SerializeField] private GameObject itemUser;
        [Header("Save/Load")]
        [SerializeField] private DefinitionCatalog saveLoadDefinitionCatalog;
        [SerializeField] private PrototypePersistenceServiceBehaviour saveLoadPersistence;
        [SerializeField, Min(1)] private int columns = 4;

        private CursorLockMode previousLockState;
        private bool previousCursorVisible;
        private bool isOpen;
        private int selectedSlotIndex;
        private int hoveredSlotIndex = -1;
        private EquipmentSlotType selectedEquipmentSlot;
        private int selectedKnownSpellIndex;
        private int selectedContractIndex;
        private int selectedQuestIndex;
        private bool refreshing;

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

            if (contractJournal == null && inventory != null)
            {
                contractJournal = inventory.GetComponent<PlayerContractJournal>();
            }

            if (questLog == null && inventory != null)
            {
                questLog = inventory.GetComponent<PlayerQuestLog>();
            }

            if (itemUser == null && inventory != null)
            {
                itemUser = inventory.gameObject;
            }

            ResolveCharacterSources();

            if (view != null)
            {
                view.Initialize(SelectSlot, UseSelectedItem, SelectEquipmentSlot, EquipSelectedItem, UnequipSelectedEquipment, HoverSlot);
            }

            if (spellManagementView != null)
            {
                spellManagementView.Initialize(SelectKnownSpell, AssignSelectedSpellToSlot, ClearSpellSlot);
            }

            if (contractJournalView != null)
            {
                contractJournalView.Initialize(SelectContract, AbandonSelectedContract, ClaimSelectedContractReward);
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

            if (contractJournal != null)
            {
                contractJournal.JournalChanged += RefreshIfOpen;
            }

            if (questLog != null)
            {
                questLog.QuestLogChanged += RefreshIfOpen;
            }

            SubscribeCharacterSources();
        }

        private void Update()
        {
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

            if (isOpen && input.ConsumeCancel())
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
                UseSelectedItem();
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

            if (contractJournal != null)
            {
                contractJournal.JournalChanged -= RefreshIfOpen;
            }

            if (questLog != null)
            {
                questLog.QuestLogChanged -= RefreshIfOpen;
            }

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
                input.SetGameplayInputBlocked(true);
                input.ClearGameplayActionQueues();
                input.ClearInventoryUiActions();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
                input.SetGameplayInputBlocked(true);
                input.ClearGameplayActionQueues();
                input.ClearInventoryUiActions();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
            view?.Show();
        }

        private void Open()
        {
            if (isOpen)
            {
                return;
            }

            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            isOpen = true;
            saveLoadPersistence?.PlayTime?.SetMenuOpen(true);

            if (input != null)
            {
                input.SetGameplayInputBlocked(true);
                input.ClearCancel();
                input.ClearInventoryUiActions();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
                input.SetGameplayInputBlocked(false);
            }

            if (view != null)
            {
                view.Hide();
            }

            if (restoreCursor)
            {
                Cursor.lockState = previousLockState;
                Cursor.visible = previousCursorVisible;
            }

            isOpen = false;
            saveLoadPersistence?.PlayTime?.SetMenuOpen(false);
        }

        private void Refresh()
        {
            if (refreshing)
            {
                return;
            }

            refreshing = true;
            try
            {
                if (view != null && inventory != null)
                {
                    view.Render(inventory.Slots);
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

                if (contractJournalView != null)
                {
                    ClampContractSelection();
                    contractJournalView.Render(contractJournal == null ? null : contractJournal.Contracts, selectedContractIndex);
                }

                if (questJournalView != null)
                {
                    ClampQuestSelection();
                    questJournalView.Render(questLog == null ? null : questLog.Quests, selectedQuestIndex);
                }

                view?.RefreshSaveLoad();

                view?.RefreshMenuExtensions();
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
                Refresh();
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
        public PlayerQuestLog QuestLog => questLog;
        public PlayerContractJournal ContractJournal => contractJournal;
        public GameObject ItemUser => itemUser;
        public DefinitionCatalog RuntimeDefinitionCatalog => ResolveSaveLoadCatalog();
        public CharacterSkillCollection RuntimeSkills => ResolveCharacterSkills();
        public CharacterTraitCollection RuntimeTraits => ResolveCharacterTraits();
        public CharacterSystemCoordinator RuntimeCharacterSystem => ResolveCharacterSystem();

        public PrototypePersistenceServiceBehaviour ResolveRuntimePersistence()
        {
            return ResolveSaveLoadPersistence(ResolveSaveLoadCatalog());
        }

        public void UseSelectedItem()
        {
            if (!isOpen || inventory == null)
            {
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

        private void SelectSlot(int slotIndex)
        {
            selectedSlotIndex = Mathf.Max(0, slotIndex);

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

        private void RenderHoveredSlotDetails()
        {
            if (view == null || inventory == null || hoveredSlotIndex < 0)
            {
                view?.RenderSelectedItemDetails(null);
                return;
            }

            InventorySlot hoveredSlot = inventory.GetSlot(hoveredSlotIndex);
            view.RenderSelectedItemDetails(hoveredSlot, includeDescription: true);
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
                delta = direction.y > 0f ? -columns : columns;
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
        }

        private void UpdateEquipmentActions()
        {
            if (view == null)
            {
                return;
            }

            InventorySlot selectedInventorySlot = inventory == null ? null : inventory.GetSlot(selectedSlotIndex);
            bool canEquip = selectedInventorySlot != null
                && !selectedInventorySlot.IsEmpty
                && selectedInventorySlot.Item != null
                && selectedInventorySlot.Item.IsEquippable;

            EquipmentSlotState selectedEquipment = equipment == null ? null : equipment.GetSlot(selectedEquipmentSlot);
            bool canUnequip = selectedEquipment != null && !selectedEquipment.IsEmpty;

            view.SetEquipmentActions(canEquip, canUnequip);
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

        private void SelectContract(int contractIndex)
        {
            selectedContractIndex = Mathf.Max(0, contractIndex);
            contractJournalView?.SetFeedback(string.Empty);
            Refresh();
        }

        private void AbandonSelectedContract()
        {
            ContractInstance contract = GetSelectedContract();
            ContractOperationResult result = contractJournal == null
                ? ContractOperationResult.Failure("No contract journal found.")
                : contractJournal.AbandonContract(contract);
            contractJournalView?.SetFeedback(result.Message);
            Refresh();
        }

        private void ClaimSelectedContractReward()
        {
            ContractInstance contract = GetSelectedContract();
            ContractOperationResult result = contractJournal == null
                ? ContractOperationResult.Failure("No contract journal found.")
                : contractJournal.ClaimReward(contract);
            contractJournalView?.SetFeedback(result.Message);
            Refresh();
        }

        private void ClampContractSelection()
        {
            int contractCount = contractJournal == null || contractJournal.Contracts == null ? 0 : contractJournal.Contracts.Count;
            selectedContractIndex = contractCount <= 0 ? 0 : Mathf.Clamp(selectedContractIndex, 0, contractCount - 1);
        }

        private ContractInstance GetSelectedContract()
        {
            if (contractJournal == null || selectedContractIndex < 0 || selectedContractIndex >= contractJournal.Contracts.Count)
            {
                return null;
            }

            return contractJournal.Contracts[selectedContractIndex];
        }

        private void SelectQuest(int questIndex)
        {
            selectedQuestIndex = Mathf.Max(0, questIndex);
            questJournalView?.SetFeedback(string.Empty);
            Refresh();
        }

        private void AbandonSelectedQuest()
        {
            QuestInstance quest = GetSelectedQuest();
            QuestOperationResult result = questLog == null
                ? QuestOperationResult.Failure("No quest log found.")
                : questLog.AbandonQuest(quest);
            questJournalView?.SetFeedback(result.Message);
            Refresh();
        }

        private void ClaimSelectedQuestReward()
        {
            QuestInstance quest = GetSelectedQuest();
            QuestOperationResult result = questLog == null
                ? QuestOperationResult.Failure("No quest log found.")
                : questLog.ClaimReward(quest);
            questJournalView?.SetFeedback(result.Message);
            Refresh();
        }

        private void ClampQuestSelection()
        {
            int questCount = questLog == null || questLog.Quests == null ? 0 : questLog.Quests.Count;
            selectedQuestIndex = questCount <= 0 ? 0 : Mathf.Clamp(selectedQuestIndex, 0, questCount - 1);
        }

        private QuestInstance GetSelectedQuest()
        {
            if (questLog == null || selectedQuestIndex < 0 || selectedQuestIndex >= questLog.Quests.Count)
            {
                return null;
            }

            return questLog.Quests[selectedQuestIndex];
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

            DefinitionCatalog catalog = ResolveSaveLoadCatalog();
            if (characterSystem != null && catalog != null && !characterSystem.IsReady)
            {
                characterSystem.InitializeFromRegistry(catalog.CreateRegistry(), restoring: false);
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
                playerStats.StatsChanged += Refresh;
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
                playerStats.StatsChanged -= Refresh;
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

            DefinitionCatalog catalog = ResolveSaveLoadCatalog();
            PrototypePersistenceServiceBehaviour persistence = ResolveSaveLoadPersistence(catalog);
            view.InitializeSaveLoad(persistence);
        }

        private DefinitionCatalog ResolveSaveLoadCatalog()
        {
            if (saveLoadDefinitionCatalog != null)
            {
                return saveLoadDefinitionCatalog;
            }

            PrototypePersistenceServiceBehaviour persistence = ResolveExistingSaveLoadPersistence();
            if (persistence != null)
            {
                saveLoadPersistence = persistence;
                saveLoadDefinitionCatalog = persistence.DefinitionCatalog;
            }

            return saveLoadDefinitionCatalog;
        }

        private PrototypePersistenceServiceBehaviour ResolveSaveLoadPersistence(DefinitionCatalog catalog)
        {
            if (saveLoadPersistence == null)
            {
                saveLoadPersistence = ResolveExistingSaveLoadPersistence();
            }

            bool activateCreatedPersistence = false;
            if (saveLoadPersistence == null && Application.isPlaying)
            {
                GameObject persistenceObject = new GameObject("Prototype Persistence Service");
                persistenceObject.SetActive(false);
                saveLoadPersistence = persistenceObject.AddComponent<PrototypePersistenceServiceBehaviour>();
                activateCreatedPersistence = true;
            }

            ConfigurePersistence(saveLoadPersistence, catalog, activateCreatedPersistence);
            return saveLoadPersistence;
        }

        private static PrototypePersistenceServiceBehaviour ResolveExistingSaveLoadPersistence()
        {
            PrototypePersistenceServiceBehaviour[] persistenceServices = UnityEngine.Object.FindObjectsByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
            if (persistenceServices == null || persistenceServices.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < persistenceServices.Length; i++)
            {
                if (persistenceServices[i] != null && persistenceServices[i].DefinitionCatalog != null)
                {
                    return persistenceServices[i];
                }
            }

            return persistenceServices[0];
        }

        private void ConfigurePersistence(PrototypePersistenceServiceBehaviour persistence, DefinitionCatalog catalog, bool activateCreatedPersistence)
        {
            if (persistence == null)
            {
                return;
            }

            persistence.ConfigurePlayerPersistence(
                catalog,
                inventory,
                equipment,
                playerStats,
                playerHealth,
                playerMana,
                playerStamina,
                statusEffects,
                identityProgression,
                questLog,
                contractJournal);

            if (activateCreatedPersistence)
            {
                persistence.gameObject.SetActive(true);
            }
            else
            {
                persistence.EnsureInitialized();
            }
        }

    }
}
