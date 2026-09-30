using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Combat
{
    public sealed class RuntimeResistanceCollection
    {
        public const float MinimumResistance = -1f;
        public const float MaximumResistance = 1f;

        private readonly Dictionary<string, ResistanceValue> baseValues = new Dictionary<string, ResistanceValue>();
        private readonly Dictionary<StatModifierSource, List<RuntimeResistanceModifier>> modifiersBySource = new Dictionary<StatModifierSource, List<RuntimeResistanceModifier>>();

        public event Action<DamageTypeDefinition, float> ResistanceChanged;

        public void SetBaseResistance(DamageTypeDefinition damageType, float resistance)
        {
            if (damageType == null || !IsSupportedResistance(resistance))
            {
                return;
            }

            float previous = GetEffectiveResistance(damageType);
            baseValues[damageType.Id] = new ResistanceValue(damageType, resistance);
            NotifyIfChanged(damageType, previous);
        }

        public void ClearBaseResistances()
        {
            List<DamageTypeDefinition> changedTypes = new List<DamageTypeDefinition>();
            foreach (ResistanceValue value in baseValues.Values)
            {
                changedTypes.Add(value.DamageType);
            }

            baseValues.Clear();
            for (int i = 0; i < changedTypes.Count; i++)
            {
                ResistanceChanged?.Invoke(changedTypes[i], 0f);
            }
        }

        public float GetDirectResistance(DamageTypeDefinition damageType)
        {
            if (damageType == null)
            {
                return 0f;
            }

            return GetDirectResistanceById(damageType.Id);
        }

        public float GetEffectiveResistance(DamageTypeDefinition damageType)
        {
            if (damageType == null)
            {
                return 0f;
            }

            float total = 0f;
            bool found = false;
            foreach (DamageTypeDefinition candidate in damageType.EnumerateSelfAndAncestors())
            {
                if (candidate == null)
                {
                    continue;
                }

                if (TryGetDirectResistanceById(candidate.Id, out float resistance))
                {
                    total += resistance;
                    found = true;
                }
            }

            return found ? Mathf.Clamp(total, MinimumResistance, MaximumResistance) : 0f;
        }

        public bool AddModifier(RuntimeResistanceModifier modifier)
        {
            if (!modifier.IsValid)
            {
                return false;
            }

            if (!modifiersBySource.TryGetValue(modifier.Source, out List<RuntimeResistanceModifier> sourceModifiers))
            {
                sourceModifiers = new List<RuntimeResistanceModifier>();
                modifiersBySource.Add(modifier.Source, sourceModifiers);
            }

            for (int i = 0; i < sourceModifiers.Count; i++)
            {
                RuntimeResistanceModifier existing = sourceModifiers[i];
                if (existing.DamageType.Id == modifier.DamageType.Id && existing.Priority == modifier.Priority)
                {
                    return false;
                }
            }

            float previous = GetEffectiveResistance(modifier.DamageType);
            sourceModifiers.Add(modifier);
            NotifyIfChanged(modifier.DamageType, previous);
            return true;
        }

        public bool RemoveModifiersFromSource(StatModifierSource source)
        {
            if (!modifiersBySource.TryGetValue(source, out List<RuntimeResistanceModifier> sourceModifiers))
            {
                return false;
            }

            List<DamageTypeDefinition> changedTypes = new List<DamageTypeDefinition>();
            Dictionary<string, float> previousValues = new Dictionary<string, float>();
            for (int i = 0; i < sourceModifiers.Count; i++)
            {
                DamageTypeDefinition damageType = sourceModifiers[i].DamageType;
                if (damageType == null || previousValues.ContainsKey(damageType.Id))
                {
                    continue;
                }

                previousValues.Add(damageType.Id, GetEffectiveResistance(damageType));
                changedTypes.Add(damageType);
            }

            modifiersBySource.Remove(source);
            for (int i = 0; i < changedTypes.Count; i++)
            {
                NotifyIfChanged(changedTypes[i], previousValues[changedTypes[i].Id]);
            }

            return true;
        }

        public IReadOnlyList<RuntimeResistanceModifier> GetModifiers()
        {
            List<RuntimeResistanceModifier> modifiers = new List<RuntimeResistanceModifier>();
            foreach (List<RuntimeResistanceModifier> sourceModifiers in modifiersBySource.Values)
            {
                modifiers.AddRange(sourceModifiers);
            }

            return modifiers;
        }

        public static bool IsSupportedResistance(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= MinimumResistance && value <= MaximumResistance;
        }

        private float GetDirectResistanceById(string damageTypeId)
        {
            return TryGetDirectResistanceById(damageTypeId, out float resistance) ? resistance : 0f;
        }

        private bool TryGetDirectResistanceById(string damageTypeId, out float resistance)
        {
            if (string.IsNullOrWhiteSpace(damageTypeId))
            {
                resistance = 0f;
                return false;
            }

            bool found = baseValues.TryGetValue(damageTypeId, out ResistanceValue baseValue);
            float total = found ? baseValue.Value : 0f;
            foreach (List<RuntimeResistanceModifier> sourceModifiers in modifiersBySource.Values)
            {
                for (int i = 0; i < sourceModifiers.Count; i++)
                {
                    RuntimeResistanceModifier modifier = sourceModifiers[i];
                    if (modifier.DamageType != null && modifier.DamageType.Id == damageTypeId)
                    {
                        found = true;
                        total += modifier.Value;
                    }
                }
            }

            resistance = Mathf.Clamp(total, MinimumResistance, MaximumResistance);
            return found;
        }

        private void NotifyIfChanged(DamageTypeDefinition damageType, float previous)
        {
            float current = GetEffectiveResistance(damageType);
            if (!Mathf.Approximately(previous, current))
            {
                ResistanceChanged?.Invoke(damageType, current);
            }
        }

        private readonly struct ResistanceValue
        {
            public ResistanceValue(DamageTypeDefinition damageType, float value)
            {
                DamageType = damageType;
                Value = value;
            }

            public DamageTypeDefinition DamageType { get; }
            public float Value { get; }
        }
    }
}
