using System;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    public sealed class AuthoritativeVitalsModel
    {
        private const float Epsilon = 0.0001f;

        private AuthoritativeVitalsTuning tuning;
        private NetworkVitalsState state;
        private double staminaRegenerationBlockedUntil;
        private double manaRegenerationBlockedUntil;
        private bool sprintExhausted;

        public AuthoritativeVitalsModel(NetworkVitalsState initialState, AuthoritativeVitalsTuning configuredTuning)
        {
            tuning = configuredTuning;
            state = Normalize(initialState);
            sprintExhausted = state.Stamina <= Epsilon;
        }

        public NetworkVitalsState State => state;

        public bool EvaluateSprint(bool requested, bool moving, float deltaSeconds, double now)
        {
            if (!requested || !moving || state.IsDefeated || deltaSeconds <= 0f)
            {
                return false;
            }

            if (sprintExhausted)
            {
                if (state.Stamina <= Mathf.Min(state.MaximumStamina, tuning.SprintRestartThreshold) + Epsilon)
                {
                    return false;
                }

                sprintExhausted = false;
            }

            float cost = tuning.SprintDrainPerSecond * deltaSeconds;
            if (cost <= Epsilon)
            {
                return true;
            }

            float available = state.Stamina;
            if (available <= Epsilon)
            {
                sprintExhausted = true;
                return false;
            }

            SetStamina(Mathf.Max(0f, available - cost));
            staminaRegenerationBlockedUntil = Math.Max(staminaRegenerationBlockedUntil, now + tuning.StaminaRegenerationDelayAfterSpend);
            if (state.Stamina <= Epsilon)
            {
                sprintExhausted = true;
            }

            return true;
        }

        public void Advance(float deltaSeconds, double now)
        {
            if (deltaSeconds <= 0f || state.IsDefeated)
            {
                return;
            }

            if (tuning.HealthRegenerationPerSecond > 0f)
            {
                SetHealth(state.Health + tuning.HealthRegenerationPerSecond * deltaSeconds);
            }

            if (now >= staminaRegenerationBlockedUntil && tuning.StaminaRegenerationPerSecond > 0f)
            {
                SetStamina(state.Stamina + tuning.StaminaRegenerationPerSecond * deltaSeconds);
            }

            if (now >= manaRegenerationBlockedUntil && tuning.ManaRegenerationPerSecond > 0f)
            {
                SetMana(state.Mana + tuning.ManaRegenerationPerSecond * deltaSeconds);
            }
        }

        public bool TryDamage(float amount)
        {
            if (!IsPositiveFinite(amount) || state.IsDefeated)
            {
                return false;
            }

            SetHealth(state.Health - amount);
            if (state.Health <= Epsilon)
            {
                NetworkVitalsState next = state;
                next.LifeState = NetworkActorLifeState.Defeated;
                Commit(next);
            }

            return true;
        }

        public bool TryHeal(float amount)
        {
            if (!IsPositiveFinite(amount) || state.IsDefeated || state.Health >= state.MaximumHealth - Epsilon)
            {
                return false;
            }

            SetHealth(state.Health + amount);
            return true;
        }

        public bool TrySpendMana(float amount, double now)
        {
            if (!IsPositiveFinite(amount) || state.IsDefeated || state.Mana + Epsilon < amount)
            {
                return false;
            }

            SetMana(state.Mana - amount);
            manaRegenerationBlockedUntil = Math.Max(manaRegenerationBlockedUntil, now + tuning.ManaRegenerationDelayAfterSpend);
            return true;
        }

        public bool TrySpendStamina(float amount, double now)
        {
            if (!IsPositiveFinite(amount) || state.IsDefeated || state.Stamina + Epsilon < amount)
            {
                return false;
            }

            SetStamina(state.Stamina - amount);
            staminaRegenerationBlockedUntil = Math.Max(staminaRegenerationBlockedUntil, now + tuning.StaminaRegenerationDelayAfterSpend);
            if (state.Stamina <= Epsilon)
            {
                sprintExhausted = true;
            }

            return true;
        }

        public bool TryRestoreMana(float amount)
        {
            if (!IsPositiveFinite(amount) || state.IsDefeated || state.Mana >= state.MaximumMana - Epsilon)
            {
                return false;
            }

            SetMana(state.Mana + amount);
            return true;
        }

        public bool TryRestoreStamina(float amount)
        {
            if (!IsPositiveFinite(amount) || state.IsDefeated || state.Stamina >= state.MaximumStamina - Epsilon)
            {
                return false;
            }

            SetStamina(state.Stamina + amount);
            return true;
        }

        public bool ReviveToMaximum()
        {
            if (!state.IsDefeated)
            {
                return false;
            }

            NetworkVitalsState next = state;
            next.Health = next.MaximumHealth;
            next.Stamina = next.MaximumStamina;
            next.Mana = next.MaximumMana;
            next.LifeState = NetworkActorLifeState.Active;
            sprintExhausted = false;
            staminaRegenerationBlockedUntil = 0d;
            manaRegenerationBlockedUntil = 0d;
            Commit(next);
            return true;
        }

        private void SetHealth(float value)
        {
            NetworkVitalsState next = state;
            next.Health = Mathf.Clamp(value, 0f, next.MaximumHealth);
            Commit(next);
        }

        private void SetStamina(float value)
        {
            NetworkVitalsState next = state;
            next.Stamina = Mathf.Clamp(value, 0f, next.MaximumStamina);
            Commit(next);
        }

        private void SetMana(float value)
        {
            NetworkVitalsState next = state;
            next.Mana = Mathf.Clamp(value, 0f, next.MaximumMana);
            Commit(next);
        }

        private void Commit(NetworkVitalsState next)
        {
            if (SameValues(state, next))
            {
                return;
            }

            next.Revision = state.Revision == uint.MaxValue ? 1u : state.Revision + 1u;
            state = next;
        }

        private static NetworkVitalsState Normalize(NetworkVitalsState value)
        {
            return new NetworkVitalsState(
                value.Health,
                value.MaximumHealth,
                value.Stamina,
                value.MaximumStamina,
                value.Mana,
                value.MaximumMana,
                value.Health <= Epsilon ? NetworkActorLifeState.Defeated : value.LifeState,
                value.Revision);
        }

        private static bool SameValues(NetworkVitalsState left, NetworkVitalsState right)
        {
            return Mathf.Approximately(left.Health, right.Health)
                && Mathf.Approximately(left.Stamina, right.Stamina)
                && Mathf.Approximately(left.Mana, right.Mana)
                && left.LifeState == right.LifeState;
        }

        private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
