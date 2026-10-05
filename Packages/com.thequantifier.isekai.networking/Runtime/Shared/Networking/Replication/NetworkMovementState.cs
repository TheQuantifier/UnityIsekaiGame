using System;
using Unity.Netcode;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    /// <summary>
    /// One atomic authoritative movement result. The acknowledged input sequence and
    /// position must travel together; consuming them from separate replication channels
    /// can pair a current acknowledgement with an older transform and cause false snaps.
    /// </summary>
    public struct NetworkMovementState : INetworkSerializable, IEquatable<NetworkMovementState>
    {
        public NetworkMovementState(
            bool isInitialized,
            ulong simulationTick,
            uint inputSequence,
            Vector3 position,
            float yawDegrees,
            float horizontalSpeed,
            float verticalVelocity,
            bool grounded,
            uint lastProcessedJumpSequence = 0u,
            uint lastExecutedJumpSequence = 0u)
        {
            IsInitialized = isInitialized;
            SimulationTick = simulationTick;
            InputSequence = inputSequence;
            Position = position;
            YawDegrees = yawDegrees;
            HorizontalSpeed = horizontalSpeed;
            VerticalVelocity = verticalVelocity;
            Grounded = grounded;
            LastProcessedJumpSequence = lastProcessedJumpSequence;
            LastExecutedJumpSequence = lastExecutedJumpSequence;
        }

        public bool IsInitialized;
        public ulong SimulationTick;
        public uint InputSequence;
        public Vector3 Position;
        public float YawDegrees;
        public float HorizontalSpeed;
        public float VerticalVelocity;
        public bool Grounded;
        public uint LastProcessedJumpSequence;
        public uint LastExecutedJumpSequence;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsInitialized);
            serializer.SerializeValue(ref SimulationTick);
            serializer.SerializeValue(ref InputSequence);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref YawDegrees);
            serializer.SerializeValue(ref HorizontalSpeed);
            serializer.SerializeValue(ref VerticalVelocity);
            serializer.SerializeValue(ref Grounded);
            serializer.SerializeValue(ref LastProcessedJumpSequence);
            serializer.SerializeValue(ref LastExecutedJumpSequence);
        }

        public bool Equals(NetworkMovementState other) => IsInitialized == other.IsInitialized
            && SimulationTick == other.SimulationTick
            && InputSequence == other.InputSequence
            && Position == other.Position
            && YawDegrees.Equals(other.YawDegrees)
            && HorizontalSpeed.Equals(other.HorizontalSpeed)
            && VerticalVelocity.Equals(other.VerticalVelocity)
            && Grounded == other.Grounded
            && LastProcessedJumpSequence == other.LastProcessedJumpSequence
            && LastExecutedJumpSequence == other.LastExecutedJumpSequence;

        public override bool Equals(object obj) => obj is NetworkMovementState other && Equals(other);

        public override int GetHashCode()
        {
            var first = HashCode.Combine(
                IsInitialized,
                SimulationTick,
                InputSequence,
                Position,
                YawDegrees,
                HorizontalSpeed,
                VerticalVelocity);
            return HashCode.Combine(first, Grounded, LastProcessedJumpSequence, LastExecutedJumpSequence);
        }
    }
}
