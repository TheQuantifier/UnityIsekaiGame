using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;

namespace UnityIsekaiGame.Networking
{
    [Flags]
    public enum NetworkInventoryItemFlags : byte
    {
        None = 0,
        Stateful = 1 << 0,
        Usable = 1 << 1,
        Equippable = 1 << 2
    }

    public struct NetworkInventoryCommand : INetworkSerializable, IEquatable<NetworkInventoryCommand>
    {
        public NetworkInventoryCommand(
            uint sequence,
            InventoryAuthorityCommandType commandType,
            int inventorySlotIndex,
            int quantity = 1,
            int equipmentSlot = -1)
        {
            Sequence = sequence;
            CommandType = commandType;
            InventorySlotIndex = inventorySlotIndex;
            Quantity = quantity;
            EquipmentSlot = equipmentSlot;
        }

        public uint Sequence;
        public InventoryAuthorityCommandType CommandType;
        public int InventorySlotIndex;
        public int Quantity;
        public int EquipmentSlot;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref CommandType);
            serializer.SerializeValue(ref InventorySlotIndex);
            serializer.SerializeValue(ref Quantity);
            serializer.SerializeValue(ref EquipmentSlot);
        }

        public bool Equals(NetworkInventoryCommand other)
        {
            return Sequence == other.Sequence
                && CommandType == other.CommandType
                && InventorySlotIndex == other.InventorySlotIndex
                && Quantity == other.Quantity
                && EquipmentSlot == other.EquipmentSlot;
        }

        public override bool Equals(object obj) => obj is NetworkInventoryCommand other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Sequence, (byte)CommandType, InventorySlotIndex, Quantity, EquipmentSlot);
    }

    public readonly struct InventoryCommandValidationResult
    {
        public InventoryCommandValidationResult(bool succeeded, InventoryAuthorityFailure failure, string message)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public InventoryAuthorityFailure Failure { get; }
        public string Message { get; }

        public static InventoryCommandValidationResult Success() => new InventoryCommandValidationResult(true, InventoryAuthorityFailure.None, string.Empty);
        public static InventoryCommandValidationResult Reject(InventoryAuthorityFailure failure, string message) => new InventoryCommandValidationResult(false, failure, message);
    }

    public struct NetworkInventoryCommandResult : INetworkSerializable, IEquatable<NetworkInventoryCommandResult>
    {
        public NetworkInventoryCommandResult(uint sequence, bool succeeded, InventoryAuthorityFailure failure, string message)
        {
            Sequence = sequence;
            Succeeded = succeeded;
            Failure = failure;
            Message = LimitMessage(message);
        }

        public uint Sequence;
        public bool Succeeded;
        public InventoryAuthorityFailure Failure;
        public FixedString512Bytes Message;
        public string MessageText => Message.ToString();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref Succeeded);
            serializer.SerializeValue(ref Failure);
            serializer.SerializeValue(ref Message);
        }

        public bool Equals(NetworkInventoryCommandResult other)
        {
            return Sequence == other.Sequence
                && Succeeded == other.Succeeded
                && Failure == other.Failure
                && Message.Equals(other.Message);
        }

        public override bool Equals(object obj) => obj is NetworkInventoryCommandResult other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Sequence, Succeeded, (byte)Failure, Message);

        public static NetworkInventoryCommandResult Success(uint sequence, string message)
        {
            return new NetworkInventoryCommandResult(sequence, true, InventoryAuthorityFailure.None, message);
        }

        public static NetworkInventoryCommandResult Reject(uint sequence, InventoryAuthorityFailure failure, string message)
        {
            return new NetworkInventoryCommandResult(sequence, false, failure, message);
        }

        private static string LimitMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            if (Encoding.UTF8.GetByteCount(message) <= InventoryAuthorityLimits.MaximumResultMessageBytes) return message;

            int length = Math.Min(message.Length, InventoryAuthorityLimits.MaximumResultMessageBytes);
            while (length > 0 && Encoding.UTF8.GetByteCount(message, 0, length) > InventoryAuthorityLimits.MaximumResultMessageBytes)
            {
                length--;
            }

            return message.Substring(0, length);
        }
    }

    public static class NetworkInventoryCommandValidator
    {
        public static InventoryCommandValidationResult Validate(NetworkInventoryCommand command, uint lastAcceptedSequence, int inventoryCapacity)
        {
            if (!IsNewer(command.Sequence, lastAcceptedSequence))
            {
                return InventoryCommandValidationResult.Reject(
                    InventoryAuthorityFailure.ReplayedCommand,
                    $"Inventory command sequence {command.Sequence} is not newer than {lastAcceptedSequence}.");
            }

            if (!Enum.IsDefined(typeof(InventoryAuthorityCommandType), command.CommandType)
                || command.CommandType == InventoryAuthorityCommandType.None)
            {
                return InventoryCommandValidationResult.Reject(InventoryAuthorityFailure.InvalidCommand, "Inventory command type is invalid.");
            }

            int clampedCapacity = Math.Clamp(inventoryCapacity, 1, InventoryAuthorityLimits.MaximumSlots);
            bool needsInventorySlot = command.CommandType != InventoryAuthorityCommandType.UnequipSlot;
            if (needsInventorySlot && (command.InventorySlotIndex < 0 || command.InventorySlotIndex >= clampedCapacity))
            {
                return InventoryCommandValidationResult.Reject(InventoryAuthorityFailure.InvalidSlot, "Inventory slot is outside the authoritative container.");
            }

            if (command.CommandType == InventoryAuthorityCommandType.DropQuantity)
            {
                if (command.Quantity < 1 || command.Quantity > InventoryAuthorityLimits.MaximumQuantity)
                {
                    return InventoryCommandValidationResult.Reject(InventoryAuthorityFailure.InvalidQuantity, "Drop quantity is outside the supported range.");
                }
            }
            else if (command.Quantity != 1)
            {
                return InventoryCommandValidationResult.Reject(InventoryAuthorityFailure.InvalidQuantity, "This inventory command must operate on exactly one item.");
            }

            bool needsEquipmentSlot = command.CommandType is InventoryAuthorityCommandType.EquipSlot or InventoryAuthorityCommandType.UnequipSlot;
            if (needsEquipmentSlot && (command.EquipmentSlot < 0 || command.EquipmentSlot >= InventoryAuthorityLimits.MaximumEquipmentSlots))
            {
                return InventoryCommandValidationResult.Reject(InventoryAuthorityFailure.InvalidEquipmentSlot, "Equipment slot is outside the supported range.");
            }

            if (!needsEquipmentSlot && command.EquipmentSlot != -1)
            {
                return InventoryCommandValidationResult.Reject(InventoryAuthorityFailure.InvalidEquipmentSlot, "This inventory command must not specify an equipment slot.");
            }

            return InventoryCommandValidationResult.Success();
        }

        public static bool IsNewer(uint candidate, uint previous)
        {
            return candidate != previous && unchecked(candidate - previous) < 0x80000000u;
        }
    }

    /// <summary>
    /// Keeps deferred replica application pinned to the newest revision observed.
    /// Multiple network callbacks can arrive before the client's application pass;
    /// an older callback must never replace a newer pending snapshot.
    /// </summary>
    public static class NetworkSnapshotRevisionQueue
    {
        public static bool TryQueue(uint candidate, uint applied, ref uint pending)
        {
            if (candidate == 0u || !NetworkInventoryCommandValidator.IsNewer(candidate, applied))
            {
                return false;
            }

            if (pending != 0u && !NetworkInventoryCommandValidator.IsNewer(candidate, pending))
            {
                return false;
            }

            pending = candidate;
            return true;
        }
    }

    public struct NetworkInventorySlotState : INetworkSerializable, IEquatable<NetworkInventorySlotState>
    {
        public NetworkInventorySlotState(
            int slotIndex,
            string definitionId,
            string itemInstanceId,
            int quantity,
            NetworkInventoryItemFlags flags)
        {
            SlotIndex = slotIndex;
            DefinitionId = definitionId ?? string.Empty;
            ItemInstanceId = itemInstanceId ?? string.Empty;
            Quantity = quantity;
            Flags = flags;
        }

        public int SlotIndex;
        public FixedString128Bytes DefinitionId;
        public FixedString128Bytes ItemInstanceId;
        public int Quantity;
        public NetworkInventoryItemFlags Flags;

        public bool IsEmpty => string.IsNullOrEmpty(DefinitionId.ToString());
        public bool IsStateful => (Flags & NetworkInventoryItemFlags.Stateful) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SlotIndex);
            serializer.SerializeValue(ref DefinitionId);
            serializer.SerializeValue(ref ItemInstanceId);
            serializer.SerializeValue(ref Quantity);
            serializer.SerializeValue(ref Flags);
        }

        public bool Equals(NetworkInventorySlotState other)
        {
            return SlotIndex == other.SlotIndex
                && DefinitionId.Equals(other.DefinitionId)
                && ItemInstanceId.Equals(other.ItemInstanceId)
                && Quantity == other.Quantity
                && Flags == other.Flags;
        }

        public override bool Equals(object obj) => obj is NetworkInventorySlotState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SlotIndex, DefinitionId, ItemInstanceId, Quantity, (byte)Flags);
    }

    public struct NetworkEquipmentReferenceState : INetworkSerializable, IEquatable<NetworkEquipmentReferenceState>
    {
        public NetworkEquipmentReferenceState(int equipmentSlot, string definitionId, string itemInstanceId)
        {
            EquipmentSlot = equipmentSlot;
            DefinitionId = definitionId ?? string.Empty;
            ItemInstanceId = itemInstanceId ?? string.Empty;
        }

        public int EquipmentSlot;
        public FixedString128Bytes DefinitionId;
        public FixedString128Bytes ItemInstanceId;

        public bool IsEmpty => string.IsNullOrEmpty(DefinitionId.ToString());

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref EquipmentSlot);
            serializer.SerializeValue(ref DefinitionId);
            serializer.SerializeValue(ref ItemInstanceId);
        }

        public bool Equals(NetworkEquipmentReferenceState other)
        {
            return EquipmentSlot == other.EquipmentSlot
                && DefinitionId.Equals(other.DefinitionId)
                && ItemInstanceId.Equals(other.ItemInstanceId);
        }

        public override bool Equals(object obj) => obj is NetworkEquipmentReferenceState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(EquipmentSlot, DefinitionId, ItemInstanceId);
    }

    public static class NetworkInventorySnapshotValidator
    {
        public static bool TryValidate(
            IReadOnlyList<NetworkInventorySlotState> inventory,
            IReadOnlyList<NetworkEquipmentReferenceState> equipment,
            int capacity,
            out string failure)
        {
            if (capacity < 1 || capacity > InventoryAuthorityLimits.MaximumSlots)
            {
                failure = $"Inventory capacity {capacity} is outside the supported range.";
                return false;
            }

            if (inventory == null || inventory.Count != capacity)
            {
                failure = "Inventory snapshot must contain exactly one record for every slot.";
                return false;
            }

            HashSet<int> slotIndexes = new HashSet<int>();
            Dictionary<string, NetworkInventorySlotState> instances = new Dictionary<string, NetworkInventorySlotState>(StringComparer.Ordinal);
            for (int i = 0; i < inventory.Count; i++)
            {
                NetworkInventorySlotState slot = inventory[i];
                if (slot.SlotIndex < 0 || slot.SlotIndex >= capacity || !slotIndexes.Add(slot.SlotIndex))
                {
                    failure = $"Inventory snapshot contains duplicate or invalid slot index {slot.SlotIndex}.";
                    return false;
                }

                string definitionId = slot.DefinitionId.ToString();
                string instanceId = slot.ItemInstanceId.ToString();
                if (slot.IsEmpty)
                {
                    if (slot.Quantity != 0 || !string.IsNullOrEmpty(instanceId) || slot.Flags != NetworkInventoryItemFlags.None)
                    {
                        failure = $"Empty inventory slot {slot.SlotIndex} contains item state.";
                        return false;
                    }

                    continue;
                }

                if (slot.Quantity < 1 || slot.Quantity > InventoryAuthorityLimits.MaximumQuantity)
                {
                    failure = $"Inventory slot {slot.SlotIndex} has invalid quantity {slot.Quantity}.";
                    return false;
                }

                if (slot.IsStateful && string.IsNullOrWhiteSpace(instanceId))
                {
                    failure = $"Stateful inventory slot {slot.SlotIndex} has no item identity.";
                    return false;
                }

                if (!string.IsNullOrEmpty(instanceId) && !instances.TryAdd(instanceId, slot))
                {
                    failure = $"Inventory slot {slot.SlotIndex} has a duplicate item identity.";
                    return false;
                }
            }

            HashSet<int> equipmentSlots = new HashSet<int>();
            HashSet<string> equippedInstances = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<NetworkEquipmentReferenceState> equipmentRecords = equipment ?? Array.Empty<NetworkEquipmentReferenceState>();
            for (int i = 0; i < equipmentRecords.Count; i++)
            {
                NetworkEquipmentReferenceState entry = equipmentRecords[i];
                if (entry.EquipmentSlot < 0
                    || entry.EquipmentSlot >= InventoryAuthorityLimits.MaximumEquipmentSlots
                    || !equipmentSlots.Add(entry.EquipmentSlot))
                {
                    failure = $"Equipment snapshot contains duplicate or invalid slot {entry.EquipmentSlot}.";
                    return false;
                }

                if (entry.IsEmpty)
                {
                    if (!string.IsNullOrEmpty(entry.ItemInstanceId.ToString()))
                    {
                        failure = $"Empty equipment slot {entry.EquipmentSlot} contains an item identity.";
                        return false;
                    }

                    continue;
                }

                string instanceId = entry.ItemInstanceId.ToString();
                if (string.IsNullOrWhiteSpace(instanceId)
                    || !equippedInstances.Add(instanceId)
                    || !instances.TryGetValue(instanceId, out NetworkInventorySlotState owned)
                    || !owned.IsStateful
                    || !string.Equals(owned.DefinitionId.ToString(), entry.DefinitionId.ToString(), StringComparison.Ordinal))
                {
                    failure = $"Equipment slot {entry.EquipmentSlot} does not reference one matching inventory-owned item instance.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }
    }
}
