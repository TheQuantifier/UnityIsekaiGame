using System;
using UnityEngine;

namespace UnityIsekaiGame.Gameplay
{
    [Serializable]
    public sealed class VitalResource
    {
        [SerializeField, Min(1f)] private float maximumValue = 100f;
        [SerializeField, Min(0f)] private float startingValue;

        private float currentValue;

        public float CurrentValue => currentValue;
        public float MaximumValue => maximumValue;
        public bool IsEmpty => currentValue <= 0f;
        public bool IsAtMaximum => currentValue >= maximumValue;
        public event Action<float, float> ValueChanged;

        public void Initialize()
        {
            currentValue = startingValue > 0f ? Mathf.Min(startingValue, maximumValue) : maximumValue;
            ValueChanged?.Invoke(currentValue, maximumValue);
        }

        public void Validate()
        {
            maximumValue = Mathf.Max(1f, maximumValue);
            startingValue = Mathf.Clamp(startingValue, 0f, maximumValue);
        }

        public bool CanSpend(float amount)
        {
            return amount > 0f && currentValue >= amount;
        }

        public VitalChangeResult Spend(float amount, string resourceName)
        {
            if (amount <= 0f)
            {
                return VitalChangeResult.Failure(amount, $"{resourceName} spend amount must be greater than zero.");
            }

            if (currentValue < amount)
            {
                return VitalChangeResult.Failure(amount, $"Not enough {resourceName.ToLowerInvariant()}.");
            }

            SetCurrent(currentValue - amount);
            return VitalChangeResult.Success(amount, amount, $"Spent {amount:0.#} {resourceName.ToLowerInvariant()}.");
        }

        public VitalChangeResult Restore(float amount, string resourceName)
        {
            if (amount <= 0f)
            {
                return VitalChangeResult.Failure(amount, $"{resourceName} restore amount must be greater than zero.");
            }

            if (IsAtMaximum)
            {
                return VitalChangeResult.Failure(amount, $"{resourceName} is already full.");
            }

            float previousValue = currentValue;
            SetCurrent(currentValue + amount);
            return VitalChangeResult.Success(amount, currentValue - previousValue, $"Restored {currentValue - previousValue:0.#} {resourceName.ToLowerInvariant()}.");
        }

        public void SetCurrent(float value)
        {
            float clampedValue = Mathf.Clamp(value, 0f, maximumValue);
            if (Mathf.Approximately(currentValue, clampedValue))
            {
                return;
            }

            currentValue = clampedValue;
            ValueChanged?.Invoke(currentValue, maximumValue);
        }

        public void SetMaximum(float value)
        {
            float clampedMaximum = Mathf.Max(1f, value);
            if (Mathf.Approximately(maximumValue, clampedMaximum))
            {
                return;
            }

            maximumValue = clampedMaximum;
            currentValue = Mathf.Clamp(currentValue, 0f, maximumValue);
            ValueChanged?.Invoke(currentValue, maximumValue);
        }
    }
}
