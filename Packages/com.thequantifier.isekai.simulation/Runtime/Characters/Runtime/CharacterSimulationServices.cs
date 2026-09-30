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
        // Character simulation is authoritative world state. Local menus and presentation
        // time scaling must never suspend resources, biology, or other actor simulation.
        public double WorldTimeSeconds => Time.unscaledTimeAsDouble;
        public float FrameDeltaSeconds => Time.unscaledDeltaTime;
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
