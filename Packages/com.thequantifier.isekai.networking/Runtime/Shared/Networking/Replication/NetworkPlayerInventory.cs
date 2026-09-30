using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerInventory : NetworkBehaviour
    {
        private readonly NetworkVariable<int> inventoryCapacity = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> snapshotRevision = new NetworkVariable<uint>(
            0u,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> lastAcceptedCommandSequence = new NetworkVariable<uint>(
            0u,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkInventoryCommandResult> latestCommandResult = new NetworkVariable<NetworkInventoryCommandResult>(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<NetworkInventorySlotState> inventorySlots = new NetworkList<NetworkInventorySlotState>(
            null,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<NetworkEquipmentReferenceState> equipmentSlots = new NetworkList<NetworkEquipmentReferenceState>(
            null,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        private readonly List<NetworkInventorySlotState> configuredInventory = new List<NetworkInventorySlotState>();
        private readonly List<NetworkEquipmentReferenceState> configuredEquipment = new List<NetworkEquipmentReferenceState>();
        private int configuredCapacity;
        private bool hasConfiguredInitialSnapshot;
        private uint localCommandSequence;
        private readonly TokenBucketRateLimiter commandRateLimiter = new TokenBucketRateLimiter(8d, 8d);

        public event Action<uint> SnapshotChanged;
        public event Action<NetworkInventoryCommandResult> CommandResultChanged;

        public Func<NetworkInventoryCommand, NetworkInventoryCommandResult> ServerCommandHandler { get; set; }
        public int InventoryCapacity => inventoryCapacity.Value;
        public uint SnapshotRevision => snapshotRevision.Value;
        public uint LastAcceptedCommandSequence => lastAcceptedCommandSequence.Value;
        public NetworkInventoryCommandResult LatestCommandResult => latestCommandResult.Value;
        public int InventorySlotCount => inventorySlots.Count;
        public int EquipmentSlotCount => equipmentSlots.Count;
        public bool HasSnapshot => snapshotRevision.Value != 0u;

        public void CopySnapshotTo(
            List<NetworkInventorySlotState> inventoryDestination,
            List<NetworkEquipmentReferenceState> equipmentDestination)
        {
            if (inventoryDestination == null) throw new ArgumentNullException(nameof(inventoryDestination));
            if (equipmentDestination == null) throw new ArgumentNullException(nameof(equipmentDestination));

            inventoryDestination.Clear();
            equipmentDestination.Clear();
            for (int i = 0; i < inventorySlots.Count; i++) inventoryDestination.Add(inventorySlots[i]);
            for (int i = 0; i < equipmentSlots.Count; i++) equipmentDestination.Add(equipmentSlots[i]);
        }

        public void ConfigureInitialSnapshotServer(
            IReadOnlyList<NetworkInventorySlotState> inventory,
            IReadOnlyList<NetworkEquipmentReferenceState> equipment,
            int capacity)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("Initial inventory must be configured before network spawn.");
            }

            if (!NetworkInventorySnapshotValidator.TryValidate(inventory, equipment, capacity, out string failure))
            {
                throw new ArgumentException(failure, nameof(inventory));
            }

            configuredInventory.Clear();
            configuredEquipment.Clear();
            for (int i = 0; i < inventory.Count; i++) configuredInventory.Add(inventory[i]);
            if (equipment != null)
            {
                for (int i = 0; i < equipment.Count; i++) configuredEquipment.Add(equipment[i]);
            }

            configuredCapacity = capacity;
            hasConfiguredInitialSnapshot = true;
        }

        public override void OnNetworkSpawn()
        {
            snapshotRevision.OnValueChanged += OnSnapshotRevisionChanged;
            latestCommandResult.OnValueChanged += OnCommandResultChanged;
            if (IsServer)
            {
                if (!hasConfiguredInitialSnapshot)
                {
                    throw new InvalidOperationException("Server inventory state must be configured before the player actor is spawned.");
                }

                PublishServerSnapshot(configuredInventory, configuredEquipment, configuredCapacity);
            }
            else if (HasSnapshot)
            {
                SnapshotChanged?.Invoke(snapshotRevision.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            snapshotRevision.OnValueChanged -= OnSnapshotRevisionChanged;
            latestCommandResult.OnValueChanged -= OnCommandResultChanged;
            ServerCommandHandler = null;
            localCommandSequence = 0u;
            commandRateLimiter.Reset();
        }

        public bool PublishServerSnapshot(
            IReadOnlyList<NetworkInventorySlotState> inventory,
            IReadOnlyList<NetworkEquipmentReferenceState> equipment,
            int capacity)
        {
            if (!IsServer)
            {
                return false;
            }

            if (!NetworkInventorySnapshotValidator.TryValidate(inventory, equipment, capacity, out string failure))
            {
                Debug.LogError($"[Network Inventory] Refused invalid server snapshot: {failure}", this);
                return false;
            }

            inventorySlots.Clear();
            equipmentSlots.Clear();
            for (int i = 0; i < inventory.Count; i++) inventorySlots.Add(inventory[i]);
            if (equipment != null)
            {
                for (int i = 0; i < equipment.Count; i++) equipmentSlots.Add(equipment[i]);
            }

            inventoryCapacity.Value = capacity;
            uint nextRevision = unchecked(snapshotRevision.Value + 1u);
            snapshotRevision.Value = nextRevision == 0u ? 1u : nextRevision;
            return true;
        }

        public bool RequestUse(int slotIndex) => SubmitLocalCommand(InventoryAuthorityCommandType.UseSlot, slotIndex, 1, -1);
        public bool RequestEquip(int slotIndex, int equipmentSlot) => SubmitLocalCommand(InventoryAuthorityCommandType.EquipSlot, slotIndex, 1, equipmentSlot);
        public bool RequestUnequip(int equipmentSlot) => SubmitLocalCommand(InventoryAuthorityCommandType.UnequipSlot, -1, 1, equipmentSlot);
        public bool RequestDrop(int slotIndex, int quantity) => SubmitLocalCommand(InventoryAuthorityCommandType.DropQuantity, slotIndex, quantity, -1);
        public bool RequestMove(int sourceSlotIndex, int destinationSlotIndex) =>
            SubmitLocalCommand(InventoryAuthorityCommandType.MoveSlot, sourceSlotIndex, 1, -1, destinationSlotIndex);

        private bool SubmitLocalCommand(
            InventoryAuthorityCommandType type,
            int slotIndex,
            int quantity,
            int equipmentSlot,
            int destinationSlotIndex = -1)
        {
            if (!IsSpawned || !IsOwner || IsServer || !HasSnapshot)
            {
                return false;
            }

            localCommandSequence = NextSequence(localCommandSequence);
            SubmitInventoryCommandRpc(new NetworkInventoryCommand(
                localCommandSequence,
                type,
                slotIndex,
                quantity,
                equipmentSlot,
                destinationSlotIndex));
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Reliable)]
        private void SubmitInventoryCommandRpc(NetworkInventoryCommand command, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }
            if (!commandRateLimiter.TryConsume(Time.realtimeSinceStartupAsDouble))
            {
                latestCommandResult.Value = NetworkInventoryCommandResult.Reject(
                    command.Sequence,
                    InventoryAuthorityFailure.ServerRejected,
                    "Inventory commands are arriving too quickly.");
                return;
            }

            InventoryCommandValidationResult validation = NetworkInventoryCommandValidator.Validate(
                command,
                lastAcceptedCommandSequence.Value,
                inventoryCapacity.Value);
            if (!validation.Succeeded)
            {
                latestCommandResult.Value = NetworkInventoryCommandResult.Reject(command.Sequence, validation.Failure, validation.Message);
                return;
            }

            lastAcceptedCommandSequence.Value = command.Sequence;
            NetworkInventoryCommandResult result = ServerCommandHandler == null
                ? NetworkInventoryCommandResult.Reject(command.Sequence, InventoryAuthorityFailure.ServerRejected, "Server inventory authority is unavailable.")
                : ServerCommandHandler(command);
            latestCommandResult.Value = result.Sequence == command.Sequence
                ? result
                : NetworkInventoryCommandResult.Reject(command.Sequence, InventoryAuthorityFailure.ServerRejected, "Server returned an invalid inventory command result.");
        }

        private static uint NextSequence(uint sequence)
        {
            uint next = unchecked(sequence + 1u);
            return next == 0u ? 1u : next;
        }

        private void OnSnapshotRevisionChanged(uint previous, uint current)
        {
            SnapshotChanged?.Invoke(current);
        }

        private void OnCommandResultChanged(NetworkInventoryCommandResult previous, NetworkInventoryCommandResult current)
        {
            if (current.Sequence != 0u)
            {
                CommandResultChanged?.Invoke(current);
            }
        }
    }
}
