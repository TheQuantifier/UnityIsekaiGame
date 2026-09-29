using System;
using Unity.Netcode;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    public enum NetworkActorLifeState : byte
    {
        Active = 0,
        Defeated = 1
    }

    public struct NetworkVitalsState : INetworkSerializable, IEquatable<NetworkVitalsState>
    {
        public NetworkVitalsState(
            float health,
            float maximumHealth,
            float stamina,
            float maximumStamina,
            float mana,
            float maximumMana,
            NetworkActorLifeState lifeState,
            uint revision)
        {
            MaximumHealth = Mathf.Max(0f, maximumHealth);
            MaximumStamina = Mathf.Max(0f, maximumStamina);
            MaximumMana = Mathf.Max(0f, maximumMana);
            Health = Mathf.Clamp(health, 0f, MaximumHealth);
            Stamina = Mathf.Clamp(stamina, 0f, MaximumStamina);
            Mana = Mathf.Clamp(mana, 0f, MaximumMana);
            LifeState = lifeState;
            Revision = revision;
        }

        public float Health;
        public float MaximumHealth;
        public float Stamina;
        public float MaximumStamina;
        public float Mana;
        public float MaximumMana;
        public NetworkActorLifeState LifeState;
        public uint Revision;

        public bool IsDefeated => LifeState == NetworkActorLifeState.Defeated;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaximumHealth);
            serializer.SerializeValue(ref Stamina);
            serializer.SerializeValue(ref MaximumStamina);
            serializer.SerializeValue(ref Mana);
            serializer.SerializeValue(ref MaximumMana);
            serializer.SerializeValue(ref LifeState);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetworkVitalsState other) => Health.Equals(other.Health)
            && MaximumHealth.Equals(other.MaximumHealth)
            && Stamina.Equals(other.Stamina)
            && MaximumStamina.Equals(other.MaximumStamina)
            && Mana.Equals(other.Mana)
            && MaximumMana.Equals(other.MaximumMana)
            && LifeState == other.LifeState
            && Revision == other.Revision;

        public override bool Equals(object obj) => obj is NetworkVitalsState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Health, MaximumHealth, Stamina, MaximumStamina, Mana, MaximumMana, LifeState, Revision);
    }

    [Serializable]
    public struct AuthoritativeVitalsTuning
    {
        public AuthoritativeVitalsTuning(
            float healthRegenerationPerSecond,
            float staminaRegenerationPerSecond,
            float manaRegenerationPerSecond,
            float sprintDrainPerSecond,
            float sprintRestartThreshold,
            float staminaRegenerationDelayAfterSpend,
            float manaRegenerationDelayAfterSpend)
        {
            HealthRegenerationPerSecond = Mathf.Max(0f, healthRegenerationPerSecond);
            StaminaRegenerationPerSecond = Mathf.Max(0f, staminaRegenerationPerSecond);
            ManaRegenerationPerSecond = Mathf.Max(0f, manaRegenerationPerSecond);
            SprintDrainPerSecond = Mathf.Max(0f, sprintDrainPerSecond);
            SprintRestartThreshold = Mathf.Max(0f, sprintRestartThreshold);
            StaminaRegenerationDelayAfterSpend = Mathf.Max(0f, staminaRegenerationDelayAfterSpend);
            ManaRegenerationDelayAfterSpend = Mathf.Max(0f, manaRegenerationDelayAfterSpend);
        }

        public float HealthRegenerationPerSecond;
        public float StaminaRegenerationPerSecond;
        public float ManaRegenerationPerSecond;
        public float SprintDrainPerSecond;
        public float SprintRestartThreshold;
        public float StaminaRegenerationDelayAfterSpend;
        public float ManaRegenerationDelayAfterSpend;
    }
}
