using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Networking.Client
{
    [DisallowMultipleComponent]
    public sealed class LocalPlayerInventoryBridge : MonoBehaviour
    {
        public const string InventorySmokeFlag = "--inventory-smoke";

        [SerializeField] private LocalGameClient client;
        [SerializeField] private PlayerInventory localInventory;
        [SerializeField] private PlayerEquipment localEquipment;
        [SerializeField] private DefinitionCatalog definitionCatalog;

        private NetworkPlayerInventory networkInventory;
        private DefinitionRegistry registry;
        private bool smokeEnabled;
        private InventorySmokePhase smokePhase;
        private int smokeDropInitialQuantity;
        private int smokeDropSlotIndex = -1;
        private int smokeEquipSlotIndex = -1;
        private string smokeEquipInstanceId = string.Empty;
        private EquipmentSlotType smokeEquipmentSlot;
        private int smokeUseInitialQuantity;
        private int smokeUseSlotIndex = -1;
        private uint pendingSnapshotRevision;
        private uint appliedSnapshotRevision;
        private readonly List<NetworkInventorySlotState> inventorySnapshot = new List<NetworkInventorySlotState>();
        private readonly List<NetworkEquipmentReferenceState> equipmentSnapshot = new List<NetworkEquipmentReferenceState>();

        public event Action ReplicaChanged;
        public event Action<string> FeedbackReceived;

        public NetworkPlayerInventory BoundInventory => networkInventory;
        public bool IsServerAuthorityActive => networkInventory != null && networkInventory.IsSpawned && networkInventory.IsOwner;

        private void Awake()
        {
            ResolveReferences();
            smokeEnabled = Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, InventorySmokeFlag, StringComparison.OrdinalIgnoreCase));
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (client == null) return;
            client.LocalPlayerActorChanged += OnLocalPlayerActorChanged;
            OnLocalPlayerActorChanged(client.LocalPlayerActor);
        }

        private void OnDisable()
        {
            if (client != null) client.LocalPlayerActorChanged -= OnLocalPlayerActorChanged;
            Bind(null);
        }

        private void LateUpdate()
        {
            if (pendingSnapshotRevision != 0u)
            {
                uint revision = pendingSnapshotRevision;
                pendingSnapshotRevision = 0u;
                ApplySnapshot(revision);
            }

            AdvanceSmoke();
        }

        public void Configure(LocalGameClient localClient, PlayerInventory inventory, PlayerEquipment equipment, DefinitionCatalog catalog)
        {
            client = localClient;
            localInventory = inventory;
            localEquipment = equipment;
            definitionCatalog = catalog;
            registry = null;
        }

        public bool RequestUse(int slotIndex) => networkInventory != null && networkInventory.RequestUse(slotIndex);
        public bool RequestEquip(int slotIndex, EquipmentSlotType equipmentSlot) => networkInventory != null && networkInventory.RequestEquip(slotIndex, (int)equipmentSlot);
        public bool RequestUnequip(EquipmentSlotType equipmentSlot) => networkInventory != null && networkInventory.RequestUnequip((int)equipmentSlot);
        public bool RequestDrop(int slotIndex, int quantity) => networkInventory != null && networkInventory.RequestDrop(slotIndex, quantity);

        private void OnLocalPlayerActorChanged(NetworkPlayerActor actor)
        {
            Bind(actor == null ? null : actor.GetComponent<NetworkPlayerInventory>());
        }

        private void Bind(NetworkPlayerInventory inventory)
        {
            if (ReferenceEquals(networkInventory, inventory)) return;
            if (networkInventory != null)
            {
                networkInventory.SnapshotChanged -= OnSnapshotChanged;
                networkInventory.CommandResultChanged -= OnCommandResultChanged;
            }

            localEquipment?.SetExternalReplicaAuthority(false);
            localInventory?.SetExternalReplicaAuthority(false);
            networkInventory = inventory;
            ResetSmoke();
            pendingSnapshotRevision = 0u;
            appliedSnapshotRevision = 0u;
            if (networkInventory == null) return;

            localInventory?.SetExternalReplicaAuthority(true);
            localEquipment?.SetExternalReplicaAuthority(true);
            networkInventory.SnapshotChanged += OnSnapshotChanged;
            networkInventory.CommandResultChanged += OnCommandResultChanged;
            if (networkInventory.HasSnapshot) QueueSnapshot(networkInventory.SnapshotRevision);
        }

        private void OnSnapshotChanged(uint revision)
        {
            QueueSnapshot(revision);
        }

        private void QueueSnapshot(uint revision)
        {
            NetworkSnapshotRevisionQueue.TryQueue(revision, appliedSnapshotRevision, ref pendingSnapshotRevision);
        }

        private void ApplySnapshot(uint revision)
        {
            if (networkInventory == null || localInventory == null || localEquipment == null || definitionCatalog == null)
            {
                return;
            }

            networkInventory.CopySnapshotTo(inventorySnapshot, equipmentSnapshot);
            if (!NetworkInventorySnapshotValidator.TryValidate(
                    inventorySnapshot,
                    equipmentSnapshot,
                    networkInventory.InventoryCapacity,
                    out string failure))
            {
                Debug.LogError($"[Network Inventory] Client rejected snapshot {revision}: {failure}", this);
                return;
            }

            registry ??= definitionCatalog.CreateRegistry();
            if (!TryValidateCatalogSnapshot(registry, inventorySnapshot, equipmentSnapshot, out failure))
            {
                Debug.LogError($"[Network Inventory] Client rejected snapshot {revision}: {failure}", this);
                return;
            }

            InventorySaveData inventorySave = new InventorySaveData { slotCapacity = networkInventory.InventoryCapacity };
            for (int i = 0; i < inventorySnapshot.Count; i++)
            {
                NetworkInventorySlotState slot = inventorySnapshot[i];
                InventoryEntrySaveData entry = new InventoryEntrySaveData();
                if (slot.IsEmpty)
                {
                    entry.mode = InventoryEntrySaveMode.Empty;
                }
                else
                {
                    entry.mode = slot.IsStateful ? InventoryEntrySaveMode.StatefulInstance : InventoryEntrySaveMode.DefinitionStack;
                    entry.definitionId = slot.DefinitionId.ToString();
                    entry.itemInstanceId = slot.ItemInstanceId.ToString();
                    entry.quantity = slot.Quantity;
                }

                inventorySave.entries.Add(entry);
            }

            InventoryRestoreResult inventoryRestore = localInventory.ApplyExternalReplicaSnapshot(inventorySave, registry, false);
            if (!inventoryRestore.Succeeded)
            {
                Debug.LogError($"[Network Inventory] Client could not apply inventory snapshot {revision}: {inventoryRestore.Message}", this);
                return;
            }

            EquipmentSaveData equipmentSave = new EquipmentSaveData();
            for (int i = 0; i < equipmentSnapshot.Count; i++)
            {
                NetworkEquipmentReferenceState slot = equipmentSnapshot[i];
                equipmentSave.slots.Add(new EquipmentSlotSaveData
                {
                    slotType = (EquipmentSlotType)slot.EquipmentSlot,
                    mode = slot.IsEmpty ? EquipmentEntrySaveMode.Empty : EquipmentEntrySaveMode.InventoryReference,
                    definitionId = slot.DefinitionId.ToString(),
                    itemInstanceId = slot.ItemInstanceId.ToString()
                });
            }

            EquipmentRestoreResult equipmentRestore = localEquipment.ApplyExternalReplicaSnapshot(equipmentSave, registry, false);
            if (!equipmentRestore.Succeeded)
            {
                Debug.LogError($"[Network Inventory] Client could not apply equipment snapshot {revision}: {equipmentRestore.Message}", this);
                return;
            }

            appliedSnapshotRevision = revision;
            localInventory.NotifyExternalReplicaChanged();
            localEquipment.NotifyExternalReplicaChanged();
            ReplicaChanged?.Invoke();
            AdvanceSmoke();
        }

        private static bool TryValidateCatalogSnapshot(
            DefinitionRegistry definitions,
            IReadOnlyList<NetworkInventorySlotState> inventoryState,
            IReadOnlyList<NetworkEquipmentReferenceState> equipmentState,
            out string failure)
        {
            for (int i = 0; i < inventoryState.Count; i++)
            {
                NetworkInventorySlotState slot = inventoryState[i];
                if (slot.IsEmpty) continue;
                string definitionId = slot.DefinitionId.ToString();
                if (definitions == null || !definitions.TryGet(definitionId, out ItemDefinition item))
                {
                    failure = $"Item definition '{definitionId}' is unavailable in the client catalog.";
                    return false;
                }

                if (!ItemInstanceId.IsValid(slot.ItemInstanceId.ToString()))
                {
                    failure = $"Inventory slot {slot.SlotIndex} has a non-canonical item identity.";
                    return false;
                }

                if (slot.Quantity > item.MaximumStackSize
                    || (!slot.IsStateful && item.InstanceMode == ItemInstanceMode.AlwaysInstanced))
                {
                    failure = $"Inventory slot {slot.SlotIndex} conflicts with definition '{definitionId}'.";
                    return false;
                }

                bool advertisedUsable = (slot.Flags & NetworkInventoryItemFlags.Usable) != 0;
                bool advertisedEquippable = (slot.Flags & NetworkInventoryItemFlags.Equippable) != 0;
                if (advertisedUsable != item.IsUsable || advertisedEquippable != item.IsEquippable)
                {
                    failure = $"Inventory slot {slot.SlotIndex} has definition flags that do not match '{definitionId}'.";
                    return false;
                }
            }

            for (int i = 0; i < equipmentState.Count; i++)
            {
                NetworkEquipmentReferenceState slot = equipmentState[i];
                if (slot.IsEmpty) continue;
                string definitionId = slot.DefinitionId.ToString();
                if (!definitions.TryGet(definitionId, out ItemDefinition item)
                    || !item.IsEquippable
                    || (int)item.Equipment.SlotType != slot.EquipmentSlot)
                {
                    failure = $"Equipment slot {slot.EquipmentSlot} conflicts with definition '{definitionId}'.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private void OnCommandResultChanged(NetworkInventoryCommandResult result)
        {
            string message = result.MessageText;
            if (result.Succeeded)
            {
                Debug.Log($"[Network Inventory] {message}", this);
            }
            else
            {
                Debug.LogWarning($"[Network Inventory] Server rejected command {result.Sequence}: {message}", this);
            }

            FeedbackReceived?.Invoke(message);
        }

        private void AdvanceSmoke()
        {
            if (!smokeEnabled || networkInventory == null || localInventory == null || localEquipment == null) return;

            switch (smokePhase)
            {
                case InventorySmokePhase.WaitingForInitialSnapshot:
                    BeginSmoke();
                    break;
                case InventorySmokePhase.WaitingForDrop:
                    ObserveDropAndRequestPickup();
                    break;
                case InventorySmokePhase.WaitingForPickupSpawn:
                    RequestSpawnedPickup();
                    break;
                case InventorySmokePhase.WaitingForPickup:
                    ObservePickupAndRequestEquip();
                    break;
                case InventorySmokePhase.WaitingForEquip:
                    ObserveEquipAndRequestUnequip();
                    break;
                case InventorySmokePhase.WaitingForUnequip:
                    ObserveUnequipAndRequestUse();
                    break;
                case InventorySmokePhase.WaitingForUse:
                    ObserveUseAndComplete();
                    break;
            }
        }

        private void BeginSmoke()
        {
            smokeDropSlotIndex = FindSmokeSlot("item.wood-log");
            smokeEquipSlotIndex = FindSmokeSlot("item.prototype-sword");
            smokeUseSlotIndex = FindSmokeSlot("item.stamina-potion");
            InventorySlot drop = localInventory.GetSlot(smokeDropSlotIndex);
            InventorySlot equip = localInventory.GetSlot(smokeEquipSlotIndex);
            InventorySlot use = localInventory.GetSlot(smokeUseSlotIndex);
            if (drop == null || drop.IsEmpty || equip == null || equip.IsEmpty || use == null || use.IsEmpty
                || !equip.IsStateful || !equip.Item.IsEquippable || !use.Item.IsUsable)
            {
                Debug.LogError("[Network Inventory] Smoke test requires Wood Log, Prototype Sword, and Stamina Potion server seed items.", this);
                smokePhase = InventorySmokePhase.Failed;
                return;
            }

            smokeDropInitialQuantity = drop.Quantity;
            smokeEquipInstanceId = equip.ItemInstanceId;
            smokeEquipmentSlot = equip.Item.Equipment.SlotType;
            smokeUseInitialQuantity = use.Quantity;
            if (RequestDrop(smokeDropSlotIndex, 1))
            {
                smokePhase = InventorySmokePhase.WaitingForDrop;
                Debug.Log($"[Network Inventory] Client requested authoritative removal from slot {smokeDropSlotIndex} at quantity {smokeDropInitialQuantity}.", this);
            }
        }

        private void ObserveDropAndRequestPickup()
        {
            int currentQuantity = GetQuantity(smokeDropSlotIndex);
            if (currentQuantity >= smokeDropInitialQuantity) return;
            Debug.Log($"[Network Inventory] Client observed authoritative quantity change: {smokeDropInitialQuantity} -> {currentQuantity}.", this);
            smokePhase = InventorySmokePhase.WaitingForPickupSpawn;
            RequestSpawnedPickup();
        }

        private void RequestSpawnedPickup()
        {
            NetworkWorldItemPickup[] pickups = FindObjectsByType<NetworkWorldItemPickup>(FindObjectsInactive.Exclude);
            for (int i = 0; i < pickups.Length; i++)
            {
                NetworkWorldItemPickup pickup = pickups[i];
                if (pickup == null || !string.Equals(pickup.DefinitionId, "item.wood-log", StringComparison.Ordinal)) continue;
                if (!pickup.RequestCollection()) return;
                smokePhase = InventorySmokePhase.WaitingForPickup;
                Debug.Log($"[Network Inventory] Client requested authoritative collection of replicated pickup {pickup.ItemInstanceId}.", this);
                return;
            }
        }

        private void ObservePickupAndRequestEquip()
        {
            if (GetTotalQuantity("item.wood-log") < smokeDropInitialQuantity) return;
            Debug.Log("[Network Inventory] Client observed the exact replicated world pickup return to authoritative inventory.", this);
            if (RequestEquip(smokeEquipSlotIndex, smokeEquipmentSlot))
            {
                smokePhase = InventorySmokePhase.WaitingForEquip;
            }
        }

        private void ObserveEquipAndRequestUnequip()
        {
            if (!localEquipment.IsItemEquipped(smokeEquipInstanceId)) return;
            Debug.Log("[Network Inventory] Client observed authoritative equipment reference.", this);
            if (RequestUnequip(smokeEquipmentSlot))
            {
                smokePhase = InventorySmokePhase.WaitingForUnequip;
            }
        }

        private void ObserveUnequipAndRequestUse()
        {
            if (localEquipment.IsItemEquipped(smokeEquipInstanceId)) return;
            NetworkPlayerVitals vitals = networkInventory.GetComponent<NetworkPlayerVitals>();
            if (vitals == null || vitals.CurrentState.Stamina >= vitals.CurrentState.MaximumStamina) return;
            Debug.Log("[Network Inventory] Client observed authoritative unequip and requested item use.", this);
            if (RequestUse(smokeUseSlotIndex))
            {
                smokePhase = InventorySmokePhase.WaitingForUse;
            }
        }

        private void ObserveUseAndComplete()
        {
            int currentQuantity = GetQuantity(smokeUseSlotIndex);
            if (currentQuantity >= smokeUseInitialQuantity) return;
            smokePhase = InventorySmokePhase.Complete;
            Debug.Log($"[Network Inventory] Authoritative inventory smoke completed: replicated drop/pickup, equip, unequip, and use; consumable {smokeUseInitialQuantity} -> {currentQuantity}.", this);
            client?.Disconnect();
        }

        private int FindSmokeSlot(string itemId)
        {
            for (int i = 0; i < localInventory.Slots.Count; i++)
            {
                InventorySlot slot = localInventory.Slots[i];
                if (slot?.Item != null && string.Equals(slot.Item.ItemId, itemId, StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        private int GetQuantity(int slotIndex)
        {
            InventorySlot slot = localInventory.GetSlot(slotIndex);
            return slot == null || slot.IsEmpty ? 0 : slot.Quantity;
        }

        private int GetTotalQuantity(string itemId)
        {
            int quantity = 0;
            for (int i = 0; i < localInventory.Slots.Count; i++)
            {
                InventorySlot slot = localInventory.Slots[i];
                if (slot?.Item != null && string.Equals(slot.Item.ItemId, itemId, StringComparison.Ordinal))
                    quantity += slot.Quantity;
            }

            return quantity;
        }

        private void ResetSmoke()
        {
            smokePhase = smokeEnabled ? InventorySmokePhase.WaitingForInitialSnapshot : InventorySmokePhase.None;
            smokeDropInitialQuantity = 0;
            smokeDropSlotIndex = -1;
            smokeEquipSlotIndex = -1;
            smokeEquipInstanceId = string.Empty;
            smokeUseInitialQuantity = 0;
            smokeUseSlotIndex = -1;
        }

        private void ResolveReferences()
        {
            client = client == null ? GetComponent<LocalGameClient>() : client;
            if (localInventory == null) localInventory = FindAnyObjectByType<PlayerInventory>();
            if (localEquipment == null && localInventory != null) localEquipment = localInventory.GetComponent<PlayerEquipment>();
        }

        private enum InventorySmokePhase : byte
        {
            None,
            WaitingForInitialSnapshot,
            WaitingForDrop,
            WaitingForPickupSpawn,
            WaitingForPickup,
            WaitingForEquip,
            WaitingForUnequip,
            WaitingForUse,
            Complete,
            Failed
        }
    }
}
