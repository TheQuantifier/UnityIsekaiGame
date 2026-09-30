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

    [Serializable]
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
            MaximumHealth = NonNegativeFinite(maximumHealth);
            MaximumStamina = NonNegativeFinite(maximumStamina);
            MaximumMana = NonNegativeFinite(maximumMana);
            Health = Mathf.Clamp(NonNegativeFinite(health), 0f, MaximumHealth);
            Stamina = Mathf.Clamp(NonNegativeFinite(stamina), 0f, MaximumStamina);
            Mana = Mathf.Clamp(NonNegativeFinite(mana), 0f, MaximumMana);
            LifeState = IsKnownLifeState(lifeState) ? lifeState : NetworkActorLifeState.Active;
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

        private static float NonNegativeFinite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        }

        private static bool IsKnownLifeState(NetworkActorLifeState value)
        {
            return value == NetworkActorLifeState.Active || value == NetworkActorLifeState.Defeated;
        }
    }

    public static class NetworkVitalsStateValidator
    {
        public static bool TryValidate(NetworkVitalsState state, out string failure)
        {
            if (state.Revision == 0u)
            {
                failure = "Vitals revision must be non-zero.";
                return false;
            }

            if (state.LifeState != NetworkActorLifeState.Active && state.LifeState != NetworkActorLifeState.Defeated)
            {
                failure = "Vitals life state is invalid.";
                return false;
            }

            if (!IsFinite(state.Health) || !IsFinite(state.MaximumHealth)
                || !IsFinite(state.Stamina) || !IsFinite(state.MaximumStamina)
                || !IsFinite(state.Mana) || !IsFinite(state.MaximumMana))
            {
                failure = "Vitals contain a non-finite numeric value.";
                return false;
            }

            if (!WithinRange(state.Health, state.MaximumHealth)
                || !WithinRange(state.Stamina, state.MaximumStamina)
                || !WithinRange(state.Mana, state.MaximumMana))
            {
                failure = "A current vital is outside its zero-to-maximum range.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool WithinRange(float current, float maximum) => maximum >= 0f && current >= 0f && current <= maximum;
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
            HealthRegenerationPerSecond = NonNegativeFinite(healthRegenerationPerSecond);
            StaminaRegenerationPerSecond = NonNegativeFinite(staminaRegenerationPerSecond);
            ManaRegenerationPerSecond = NonNegativeFinite(manaRegenerationPerSecond);
            SprintDrainPerSecond = NonNegativeFinite(sprintDrainPerSecond);
            SprintRestartThreshold = NonNegativeFinite(sprintRestartThreshold);
            StaminaRegenerationDelayAfterSpend = NonNegativeFinite(staminaRegenerationDelayAfterSpend);
            ManaRegenerationDelayAfterSpend = NonNegativeFinite(manaRegenerationDelayAfterSpend);
        }

        public float HealthRegenerationPerSecond;
        public float StaminaRegenerationPerSecond;
        public float ManaRegenerationPerSecond;
        public float SprintDrainPerSecond;
        public float SprintRestartThreshold;
        public float StaminaRegenerationDelayAfterSpend;
        public float ManaRegenerationDelayAfterSpend;

        private static float NonNegativeFinite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        }
    }
}
