using System;
using UnityEngine;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Combat
{
    [Serializable]
    public sealed class ResistanceModifierDefinition
    {
        [SerializeField] private DamageTypeDefinition damageType;
        [SerializeField, Range(RuntimeResistanceCollection.MinimumResistance, RuntimeResistanceCollection.MaximumResistance)] private float resistance;
        [SerializeField] private bool scaleWithStacks = true;
        [SerializeField] private int priority;

        public DamageTypeDefinition DamageType => damageType;
        public float Resistance => resistance;
        public bool ScaleWithStacks => scaleWithStacks;
        public int Priority => priority;
        public bool IsValid => damageType != null && RuntimeResistanceCollection.IsSupportedResistance(resistance);

        public RuntimeResistanceModifier CreateRuntimeModifier(StatModifierSource source, int stackCount = 1)
        {
            float scaledValue = scaleWithStacks ? resistance * Mathf.Max(1, stackCount) : resistance;
            return new RuntimeResistanceModifier(damageType, scaledValue, source, priority);
        }
    }
}
