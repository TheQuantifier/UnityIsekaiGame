using System;
using UnityEngine;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Inventory.Identity
{
    public sealed class PlayerItemIdentitySynchronizer : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private DefinitionCatalog definitionCatalog;
        [SerializeField] private string ownerPersonId = PersistenceService.LocalPlayerId;
        [SerializeField] private string synchronizationNamespace = "runtime.player.inventory-equipment";
        [SerializeField] private bool synchronizeOnAwake;

        private ItemInstanceIdentityRuntime runtime;
        private Func<DefinitionRegistry> registryProvider;
        private bool subscribed;
        private PlayerInventory subscribedInventory;
        private PlayerEquipment subscribedEquipment;
        private bool synchronizing;
        private int automaticSynchronizationPauseDepth;
        private string lastFailure;
        private string lastProjectionFingerprint;
        private long lastSynchronizedRuntimeRevision = -1L;

        public ItemInstanceIdentityRuntime Runtime => runtime;
        public string LastFailure => lastFailure ?? string.Empty;

        private void Awake()
        {
            ResolveReferences();
            Subscribe();
            if (synchronizeOnAwake)
            {
                SynchronizeNow();
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            PlayerInventory playerInventory,
            PlayerEquipment playerEquipment,
            ItemInstanceIdentityRuntime itemRuntime,
            Func<DefinitionRegistry> registry,
            string ownerId,
            string syncNamespace = "")
        {
            Unsubscribe();
            inventory = playerInventory;
            equipment = playerEquipment;
            runtime = itemRuntime;
            lastProjectionFingerprint = null;
            lastSynchronizedRuntimeRevision = -1L;
            registryProvider = registry;
            ownerPersonId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
            if (!string.IsNullOrWhiteSpace(syncNamespace))
            {
                synchronizationNamespace = syncNamespace;
            }

            ResolveReferences();
            Subscribe();
        }

        public ItemIdentityInventoryBridgeResult SynchronizeNow()
        {
            if (synchronizing)
            {
                return ItemIdentityInventoryBridgeResult.Failure("ReentrantSynchronization", "Item identity synchronization is already running.");
            }

            ResolveReferences();
            Subscribe();
            if (inventory == null || equipment == null)
            {
                lastFailure = "Inventory or equipment is missing.";
                return ItemIdentityInventoryBridgeResult.Failure("MissingReference", lastFailure);
            }

            if (runtime == null)
            {
                runtime = new ItemInstanceIdentityRuntime();
            }

            DefinitionRegistry registry = ResolveRegistry();
            if (registry == null)
            {
                lastFailure = "Definition registry is missing.";
                return ItemIdentityInventoryBridgeResult.Failure("MissingRegistry", lastFailure);
            }

            try
            {
                synchronizing = true;
                PlayerInventoryEquipmentSaveData saveData = CreateProjectionSaveData();
                string projectionFingerprint = PersistenceSerialization.Serialize(saveData);
                if (string.Equals(projectionFingerprint, lastProjectionFingerprint, StringComparison.Ordinal)
                    && runtime.Revision == lastSynchronizedRuntimeRevision
                    && string.IsNullOrWhiteSpace(lastFailure))
                {
                    return ItemIdentityInventoryBridgeResult.Success(runtime.CreateSaveData(), "Inventory/equipment identity projection is already synchronized.");
                }

                ItemIdentityInventoryBridgeResult result = ItemIdentityInventoryBridge.SynchronizeInventoryEquipmentRuntime(
                    runtime,
                    saveData,
                    registry,
                    ownerPersonId,
                    synchronizationNamespace);
                lastFailure = result.Succeeded ? string.Empty : result.Message;
                if (result.Succeeded)
                {
                    lastProjectionFingerprint = projectionFingerprint;
                    lastSynchronizedRuntimeRevision = runtime.Revision;
                }
                return result;
            }
            finally
            {
                synchronizing = false;
            }
        }

        public ItemIdentityInventoryBridgeResult ValidateCurrentProjection()
        {
            ResolveReferences();
            if (runtime == null)
            {
                return ItemIdentityInventoryBridgeResult.Failure("MissingRuntime", "Item identity runtime is missing.");
            }

            DefinitionRegistry registry = ResolveRegistry();
            if (registry == null)
            {
                return ItemIdentityInventoryBridgeResult.Failure("MissingRegistry", "Definition registry is missing.");
            }

            return ItemIdentityInventoryBridge.ValidateSynchronizedProjection(
                CreateProjectionSaveData(),
                runtime.CreateSaveData(),
                registry,
                ownerPersonId,
                synchronizationNamespace);
        }

        public IDisposable PauseAutomaticSynchronization()
        {
            automaticSynchronizationPauseDepth++;
            return new AutomaticSynchronizationPause(this);
        }

        public PlayerInventoryEquipmentSaveData CreateProjectionSaveData()
        {
            return new PlayerInventoryEquipmentSaveData
            {
                schemaVersion = PlayerInventoryEquipmentPersistenceParticipant.CurrentParticipantSchemaVersion,
                inventory = inventory == null ? new InventorySaveData() : inventory.CreateSaveData(),
                equipment = equipment == null ? new EquipmentSaveData() : equipment.CreateSaveData()
            };
        }

        private void ResolveReferences()
        {
            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
            }

            if (equipment == null)
            {
                equipment = GetComponent<PlayerEquipment>();
            }
        }

        private DefinitionRegistry ResolveRegistry()
        {
            if (registryProvider != null)
            {
                return registryProvider();
            }

            return definitionCatalog == null ? null : definitionCatalog.CreateRegistry();
        }

        private void Subscribe()
        {
            ResolveReferences();
            if (subscribed && subscribedInventory == inventory && subscribedEquipment == equipment)
            {
                return;
            }

            Unsubscribe();
            if (inventory != null)
            {
                inventory.InventoryChanged += OnInventoryOrEquipmentChanged;
                subscribedInventory = inventory;
            }

            if (equipment != null)
            {
                equipment.EquipmentChanged += OnInventoryOrEquipmentChanged;
                subscribedEquipment = equipment;
            }

            subscribed = subscribedInventory != null || subscribedEquipment != null;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (subscribedInventory != null)
            {
                subscribedInventory.InventoryChanged -= OnInventoryOrEquipmentChanged;
            }

            if (subscribedEquipment != null)
            {
                subscribedEquipment.EquipmentChanged -= OnInventoryOrEquipmentChanged;
            }

            subscribed = false;
            subscribedInventory = null;
            subscribedEquipment = null;
        }

        private void OnInventoryOrEquipmentChanged()
        {
            if (automaticSynchronizationPauseDepth > 0)
            {
                return;
            }

            ItemIdentityInventoryBridgeResult result = SynchronizeNow();
            if (!result.Succeeded)
            {
                Debug.LogWarning($"Item identity synchronization failed: {result.Message}");
            }
        }

        private sealed class AutomaticSynchronizationPause : IDisposable
        {
            private PlayerItemIdentitySynchronizer owner;

            public AutomaticSynchronizationPause(PlayerItemIdentitySynchronizer owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (owner == null)
                {
                    return;
                }

                owner.automaticSynchronizationPauseDepth = Math.Max(0, owner.automaticSynchronizationPauseDepth - 1);
                owner = null;
            }
        }
    }
}
