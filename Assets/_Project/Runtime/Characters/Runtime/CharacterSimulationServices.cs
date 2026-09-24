using System;
using UnityEngine;

namespace UnityIsekaiGame.CharacterSystem
{
    public interface ICharacterSimulationClock
    {
        double WorldTimeSeconds { get; }
        float FrameDeltaSeconds { get; }
    }

    public interface ICharacterTransactionIdProvider
    {
        string Next(string actorId, string operation, double worldTimeSeconds);
    }

    public sealed class UnityCharacterSimulationClock : ICharacterSimulationClock
    {
        public double WorldTimeSeconds => Time.timeAsDouble;
        public float FrameDeltaSeconds => Time.deltaTime;
    }

    public sealed class SequentialCharacterTransactionIdProvider : ICharacterTransactionIdProvider
    {
        private long sequence;

        public string Next(string actorId, string operation, double worldTimeSeconds)
        {
            sequence++;
            return $"character-transaction.{Normalize(actorId)}.{Normalize(operation)}.{worldTimeSeconds:0.000000}.{sequence:D8}";
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant().Replace(' ', '-');
    }
}
