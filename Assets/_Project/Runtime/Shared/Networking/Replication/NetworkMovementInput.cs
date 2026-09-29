using System;
using Unity.Netcode;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    public struct NetworkMovementInput : INetworkSerializable, IEquatable<NetworkMovementInput>
    {
        public NetworkMovementInput(uint sequence, Vector2 move, float yawDegrees, bool sprint)
        {
            Sequence = sequence;
            Move = move;
            YawDegrees = yawDegrees;
            Sprint = sprint;
        }

        public uint Sequence;
        public Vector2 Move;
        public float YawDegrees;
        public bool Sprint;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref Move);
            serializer.SerializeValue(ref YawDegrees);
            serializer.SerializeValue(ref Sprint);
        }

        public bool Equals(NetworkMovementInput other) => Sequence == other.Sequence
            && Move == other.Move
            && YawDegrees.Equals(other.YawDegrees)
            && Sprint == other.Sprint;

        public override bool Equals(object obj) => obj is NetworkMovementInput other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Sequence, Move, YawDegrees, Sprint);
    }

    public static class NetworkMovementInputValidator
    {
        public static bool TryNormalize(
            NetworkMovementInput requested,
            uint lastAcceptedSequence,
            out NetworkMovementInput normalized,
            out string failure)
        {
            normalized = default;
            if (!IsNewer(requested.Sequence, lastAcceptedSequence))
            {
                failure = $"Movement input sequence {requested.Sequence} is not newer than {lastAcceptedSequence}.";
                return false;
            }

            if (!IsFinite(requested.Move.x) || !IsFinite(requested.Move.y) || !IsFinite(requested.YawDegrees))
            {
                failure = "Movement input contains a non-finite value.";
                return false;
            }

            normalized = new NetworkMovementInput(
                requested.Sequence,
                Vector2.ClampMagnitude(requested.Move, 1f),
                Mathf.Repeat(requested.YawDegrees, 360f),
                requested.Sprint);
            failure = string.Empty;
            return true;
        }

        public static bool IsNewer(uint sequence, uint previous) => sequence != previous && unchecked((int)(sequence - previous)) > 0;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
