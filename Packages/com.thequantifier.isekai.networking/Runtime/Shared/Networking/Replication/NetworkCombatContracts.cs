using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    public struct NetworkCombatCommand : INetworkSerializable, IEquatable<NetworkCombatCommand>
    {
        public NetworkCombatCommand(uint sequence, CombatAuthorityCommandType commandType, Vector3 aimDirection, string actionId = "")
        {
            Sequence = sequence;
            CommandType = commandType;
            AimDirection = aimDirection;
            ActionId = actionId ?? string.Empty;
        }

        public uint Sequence;
        public CombatAuthorityCommandType CommandType;
        public Vector3 AimDirection;
        public FixedString128Bytes ActionId;
        public string ActionIdText => ActionId.ToString();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref CommandType);
            serializer.SerializeValue(ref AimDirection);
            serializer.SerializeValue(ref ActionId);
        }

        public bool Equals(NetworkCombatCommand other) => Sequence == other.Sequence
            && CommandType == other.CommandType
            && AimDirection.Equals(other.AimDirection)
            && ActionId.Equals(other.ActionId);
        public override bool Equals(object obj) => obj is NetworkCombatCommand other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Sequence, (byte)CommandType, AimDirection, ActionId);
    }

    public readonly struct CombatCommandValidationResult
    {
        public CombatCommandValidationResult(bool succeeded, CombatAuthorityFailure failure, string message)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public CombatAuthorityFailure Failure { get; }
        public string Message { get; }
        public static CombatCommandValidationResult Success() => new CombatCommandValidationResult(true, CombatAuthorityFailure.None, string.Empty);
        public static CombatCommandValidationResult Reject(CombatAuthorityFailure failure, string message) => new CombatCommandValidationResult(false, failure, message);
    }

    public static class NetworkCombatCommandValidator
    {
        public static CombatCommandValidationResult Validate(NetworkCombatCommand command, uint lastAcceptedSequence)
        {
            if (!NetworkInventoryCommandValidator.IsNewer(command.Sequence, lastAcceptedSequence))
            {
                return CombatCommandValidationResult.Reject(CombatAuthorityFailure.ReplayedCommand,
                    $"Combat command sequence {command.Sequence} is not newer than {lastAcceptedSequence}.");
            }

            if (!Enum.IsDefined(typeof(CombatAuthorityCommandType), command.CommandType)
                || command.CommandType == CombatAuthorityCommandType.None)
            {
                return CombatCommandValidationResult.Reject(CombatAuthorityFailure.InvalidCommand, "Combat command type is invalid.");
            }

            Vector3 aim = command.AimDirection;
            float magnitude = aim.magnitude;
            if (!IsFinite(aim.x) || !IsFinite(aim.y) || !IsFinite(aim.z)
                || magnitude < CombatAuthorityLimits.MinimumAimMagnitude
                || magnitude > CombatAuthorityLimits.MaximumAimMagnitude)
            {
                return CombatCommandValidationResult.Reject(CombatAuthorityFailure.InvalidAim, "Combat aim must be a finite normalized direction.");
            }

            string actionId = command.ActionIdText;
            if (command.CommandType == CombatAuthorityCommandType.CastAbility && string.IsNullOrWhiteSpace(actionId))
            {
                return CombatCommandValidationResult.Reject(CombatAuthorityFailure.ActionUnavailable, "Ability casts require a stable action ID.");
            }

            if (command.CommandType == CombatAuthorityCommandType.PrimaryAttack && !string.IsNullOrEmpty(actionId))
            {
                return CombatCommandValidationResult.Reject(CombatAuthorityFailure.InvalidCommand, "Primary attacks must not supply an ability ID.");
            }

            return CombatCommandValidationResult.Success();
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public struct NetworkCombatCommandResult : INetworkSerializable, IEquatable<NetworkCombatCommandResult>
    {
        public NetworkCombatCommandResult(
            uint sequence,
            bool succeeded,
            CombatAuthorityFailure failure,
            string actionId,
            string targetEntityId,
            float appliedAmount,
            string message)
        {
            Sequence = sequence;
            Succeeded = succeeded;
            Failure = failure;
            ActionId = actionId ?? string.Empty;
            TargetEntityId = targetEntityId ?? string.Empty;
            AppliedAmount = appliedAmount;
            Message = LimitMessage(message);
        }

        public uint Sequence;
        public bool Succeeded;
        public CombatAuthorityFailure Failure;
        public FixedString128Bytes ActionId;
        public FixedString128Bytes TargetEntityId;
        public float AppliedAmount;
        public FixedString512Bytes Message;
        public string MessageText => Message.ToString();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref Succeeded);
            serializer.SerializeValue(ref Failure);
            serializer.SerializeValue(ref ActionId);
            serializer.SerializeValue(ref TargetEntityId);
            serializer.SerializeValue(ref AppliedAmount);
            serializer.SerializeValue(ref Message);
        }

        public bool Equals(NetworkCombatCommandResult other) => Sequence == other.Sequence
            && Succeeded == other.Succeeded
            && Failure == other.Failure
            && ActionId.Equals(other.ActionId)
            && TargetEntityId.Equals(other.TargetEntityId)
            && AppliedAmount.Equals(other.AppliedAmount)
            && Message.Equals(other.Message);
        public override bool Equals(object obj) => obj is NetworkCombatCommandResult other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Sequence, Succeeded, (byte)Failure, ActionId, TargetEntityId, AppliedAmount, Message);

        public static NetworkCombatCommandResult Success(uint sequence, string actionId, string targetEntityId, float appliedAmount, string message)
            => new NetworkCombatCommandResult(sequence, true, CombatAuthorityFailure.None, actionId, targetEntityId, appliedAmount, message);

        public static NetworkCombatCommandResult Reject(uint sequence, CombatAuthorityFailure failure, string message, string actionId = "")
            => new NetworkCombatCommandResult(sequence, false, failure, actionId, string.Empty, 0f, message);

        private static string LimitMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            if (Encoding.UTF8.GetByteCount(message) <= CombatAuthorityLimits.MaximumResultMessageBytes) return message;
            int length = Math.Min(message.Length, CombatAuthorityLimits.MaximumResultMessageBytes);
            while (length > 0 && Encoding.UTF8.GetByteCount(message, 0, length) > CombatAuthorityLimits.MaximumResultMessageBytes) length--;
            return message.Substring(0, length);
        }
    }

    public struct NetworkCombatantState : INetworkSerializable, IEquatable<NetworkCombatantState>
    {
        public NetworkCombatantState(string entityId, Vector3 position, Quaternion rotation, float health, float maximumHealth, bool defeated)
        {
            EntityId = entityId ?? string.Empty;
            Position = position;
            Rotation = rotation;
            Health = health;
            MaximumHealth = maximumHealth;
            Defeated = defeated;
        }

        public FixedString128Bytes EntityId;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Health;
        public float MaximumHealth;
        public bool Defeated;
        public string EntityIdText => EntityId.ToString();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref EntityId);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaximumHealth);
            serializer.SerializeValue(ref Defeated);
        }

        public bool Equals(NetworkCombatantState other) => EntityId.Equals(other.EntityId)
            && Position.Equals(other.Position)
            && Rotation.Equals(other.Rotation)
            && Health.Equals(other.Health)
            && MaximumHealth.Equals(other.MaximumHealth)
            && Defeated == other.Defeated;
        public override bool Equals(object obj) => obj is NetworkCombatantState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(EntityId, Position, Rotation, Health, MaximumHealth, Defeated);
    }

    public static class NetworkCombatantSnapshotValidator
    {
        public static bool TryValidate(IReadOnlyList<NetworkCombatantState> combatants, out string failure)
        {
            if (combatants == null || combatants.Count > CombatAuthorityLimits.MaximumWorldCombatants)
            {
                failure = "Combatant snapshot is missing or exceeds the supported world limit.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < combatants.Count; i++)
            {
                NetworkCombatantState state = combatants[i];
                string id = state.EntityIdText;
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                {
                    failure = $"Combatant snapshot contains a missing or duplicate entity ID at index {i}.";
                    return false;
                }

                if (!IsFinite(state.Position.x) || !IsFinite(state.Position.y) || !IsFinite(state.Position.z)
                    || !IsFinite(state.Rotation.x) || !IsFinite(state.Rotation.y) || !IsFinite(state.Rotation.z) || !IsFinite(state.Rotation.w)
                    || !IsFinite(state.Health) || !IsFinite(state.MaximumHealth)
                    || state.MaximumHealth <= 0f || state.Health < 0f || state.Health > state.MaximumHealth + 0.001f)
                {
                    failure = $"Combatant '{id}' contains invalid transform or health state.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
