using System;
using UnityEngine;

namespace UnityIsekaiGame.Combat
{
    [Serializable]
    public sealed class DamageComponentDefinition
    {
        [SerializeField] private DamageTypeDefinition damageType;
        [SerializeField, Min(0f)] private float baseAmount;
        [SerializeField] private AttackPowerScalingPolicy attackPowerScaling = AttackPowerScalingPolicy.IgnoreSourceAttackPower;
        [SerializeField, Min(0f)] private float multiplier = 1f;

        public DamageTypeDefinition DamageType => damageType;
        public float BaseAmount => Mathf.Max(0f, baseAmount);
        public AttackPowerScalingPolicy AttackPowerScaling => attackPowerScaling;
        public float Multiplier => Mathf.Max(0f, multiplier);
        public bool IsValid => damageType != null && BaseAmount > 0f && Multiplier >= 0f;

        public DamageComponent CreateRuntimeComponent(GameObject source, float magnitudeMultiplier)
        {
            float scaledBase = BaseAmount * Multiplier * Mathf.Max(0f, magnitudeMultiplier);
            float amount = CombatStatUtility.CalculatePreMitigationDamage(scaledBase, source, attackPowerScaling);
            return new DamageComponent(damageType, amount, attackPowerScaling);
        }
    }
}
