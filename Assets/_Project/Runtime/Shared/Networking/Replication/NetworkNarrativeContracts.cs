using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;

namespace UnityIsekaiGame.Networking
{
    public struct NetworkNarrativeCommand : INetworkSerializable, IEquatable<NetworkNarrativeCommand>
    {
        public NetworkNarrativeCommand(
            uint sequence,
            NarrativeAuthorityCommandType commandType,
            string primaryId = "",
            string secondaryId = "",
            int value = 0,
            int secondaryValue = 0)
        {
            Sequence = sequence;
            CommandType = commandType;
            PrimaryId = primaryId ?? string.Empty;
            SecondaryId = secondaryId ?? string.Empty;
            Value = value;
            SecondaryValue = secondaryValue;
        }

        public uint Sequence;
        public NarrativeAuthorityCommandType CommandType;
        public FixedString128Bytes PrimaryId;
        public FixedString128Bytes SecondaryId;
        public int Value;
        public int SecondaryValue;
        public string PrimaryIdText => PrimaryId.ToString();
        public string SecondaryIdText => SecondaryId.ToString();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref CommandType);
            serializer.SerializeValue(ref PrimaryId);
            serializer.SerializeValue(ref SecondaryId);
            serializer.SerializeValue(ref Value);
            serializer.SerializeValue(ref SecondaryValue);
        }

        public bool Equals(NetworkNarrativeCommand other) => Sequence == other.Sequence
            && CommandType == other.CommandType
            && PrimaryId.Equals(other.PrimaryId)
            && SecondaryId.Equals(other.SecondaryId)
            && Value == other.Value
            && SecondaryValue == other.SecondaryValue;
        public override bool Equals(object obj) => obj is NetworkNarrativeCommand other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Sequence, (byte)CommandType, PrimaryId, SecondaryId, Value, SecondaryValue);
    }

    public readonly struct NarrativeCommandValidationResult
    {
        public NarrativeCommandValidationResult(bool succeeded, NarrativeAuthorityFailure failure, string message)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public NarrativeAuthorityFailure Failure { get; }
        public string Message { get; }
        public static NarrativeCommandValidationResult Success() => new NarrativeCommandValidationResult(true, NarrativeAuthorityFailure.None, string.Empty);
        public static NarrativeCommandValidationResult Reject(NarrativeAuthorityFailure failure, string message) => new NarrativeCommandValidationResult(false, failure, message);
    }

    public static class NetworkNarrativeCommandValidator
    {
        public static NarrativeCommandValidationResult Validate(NetworkNarrativeCommand command, uint lastAcceptedSequence)
        {
            if (!NetworkInventoryCommandValidator.IsNewer(command.Sequence, lastAcceptedSequence))
            {
                return NarrativeCommandValidationResult.Reject(
                    NarrativeAuthorityFailure.ReplayedCommand,
                    $"Narrative command sequence {command.Sequence} is not newer than {lastAcceptedSequence}.");
            }

            if (!Enum.IsDefined(typeof(NarrativeAuthorityCommandType), command.CommandType)
                || command.CommandType == NarrativeAuthorityCommandType.None)
            {
                return NarrativeCommandValidationResult.Reject(NarrativeAuthorityFailure.InvalidCommand, "Narrative command type is invalid.");
            }

            if (!ValidIdentifier(command.PrimaryIdText) || !ValidIdentifier(command.SecondaryIdText))
            {
                return NarrativeCommandValidationResult.Reject(NarrativeAuthorityFailure.InvalidIdentifier, "Narrative command identifiers are invalid or too large.");
            }

            if (RequiresPrimary(command.CommandType) && string.IsNullOrWhiteSpace(command.PrimaryIdText))
            {
                return NarrativeCommandValidationResult.Reject(NarrativeAuthorityFailure.InvalidIdentifier, "This narrative command requires a stable target identifier.");
            }

            if (command.Value < 0 || command.SecondaryValue < 0)
            {
                return NarrativeCommandValidationResult.Reject(NarrativeAuthorityFailure.InvalidCommand, "Narrative command values cannot be negative.");
            }

            return NarrativeCommandValidationResult.Success();
        }

        private static bool RequiresPrimary(NarrativeAuthorityCommandType type)
        {
            return type != NarrativeAuthorityCommandType.CreateParty
                && type != NarrativeAuthorityCommandType.SetPartySettings;
        }

        private static bool ValidIdentifier(string value)
        {
            return value != null
                && Encoding.UTF8.GetByteCount(value) <= NarrativeAuthorityLimits.MaximumIdentifierBytes
                && value.IndexOfAny(new[] { '\r', '\n', '\0' }) < 0;
        }
    }

    public struct NetworkNarrativeCommandResult : INetworkSerializable, IEquatable<NetworkNarrativeCommandResult>
    {
        public NetworkNarrativeCommandResult(
            uint sequence,
            NarrativeAuthorityCommandType commandType,
            bool succeeded,
            NarrativeAuthorityFailure failure,
            NarrativePresentationAction presentation,
            string primaryId,
            string secondaryId,
            string message)
        {
            Sequence = sequence;
            CommandType = commandType;
            Succeeded = succeeded;
            Failure = failure;
            Presentation = presentation;
            PrimaryId = primaryId ?? string.Empty;
            SecondaryId = secondaryId ?? string.Empty;
            Message = LimitMessage(message);
        }

        public uint Sequence;
        public NarrativeAuthorityCommandType CommandType;
        public bool Succeeded;
        public NarrativeAuthorityFailure Failure;
        public NarrativePresentationAction Presentation;
        public FixedString128Bytes PrimaryId;
        public FixedString128Bytes SecondaryId;
        public FixedString512Bytes Message;
        public string PrimaryIdText => PrimaryId.ToString();
        public string SecondaryIdText => SecondaryId.ToString();
        public string MessageText => Message.ToString();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref CommandType);
            serializer.SerializeValue(ref Succeeded);
            serializer.SerializeValue(ref Failure);
            serializer.SerializeValue(ref Presentation);
            serializer.SerializeValue(ref PrimaryId);
            serializer.SerializeValue(ref SecondaryId);
            serializer.SerializeValue(ref Message);
        }

        public bool Equals(NetworkNarrativeCommandResult other) => Sequence == other.Sequence
            && CommandType == other.CommandType
            && Succeeded == other.Succeeded
            && Failure == other.Failure
            && Presentation == other.Presentation
            && PrimaryId.Equals(other.PrimaryId)
            && SecondaryId.Equals(other.SecondaryId)
            && Message.Equals(other.Message);
        public override bool Equals(object obj) => obj is NetworkNarrativeCommandResult other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Sequence, (byte)CommandType, Succeeded, (byte)Failure, (byte)Presentation, PrimaryId, SecondaryId, Message);

        public static NetworkNarrativeCommandResult Success(
            NetworkNarrativeCommand command,
            NarrativePresentationAction presentation,
            string message,
            string primaryId = null,
            string secondaryId = null)
        {
            return new NetworkNarrativeCommandResult(
                command.Sequence,
                command.CommandType,
                true,
                NarrativeAuthorityFailure.None,
                presentation,
                primaryId ?? command.PrimaryIdText,
                secondaryId ?? command.SecondaryIdText,
                message);
        }

        public static NetworkNarrativeCommandResult Reject(NetworkNarrativeCommand command, NarrativeAuthorityFailure failure, string message)
            => new NetworkNarrativeCommandResult(command.Sequence, command.CommandType, false, failure, NarrativePresentationAction.None, command.PrimaryIdText, command.SecondaryIdText, message);

        private static string LimitMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            if (Encoding.UTF8.GetByteCount(message) <= NarrativeAuthorityLimits.MaximumResultMessageBytes) return message;
            int length = Math.Min(message.Length, NarrativeAuthorityLimits.MaximumResultMessageBytes);
            while (length > 0 && Encoding.UTF8.GetByteCount(message, 0, length) > NarrativeAuthorityLimits.MaximumResultMessageBytes) length--;
            return message.Substring(0, length);
        }
    }
}
