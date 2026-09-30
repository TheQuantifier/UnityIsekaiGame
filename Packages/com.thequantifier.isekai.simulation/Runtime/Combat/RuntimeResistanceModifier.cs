using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Combat
{
    public readonly struct RuntimeResistanceModifier
    {
        public RuntimeResistanceModifier(DamageTypeDefinition damageType, float value, StatModifierSource source, int priority = 0)
        {
            DamageType = damageType;
            Value = value;
            Source = source;
            Priority = priority;
        }

        public DamageTypeDefinition DamageType { get; }
        public float Value { get; }
        public StatModifierSource Source { get; }
        public int Priority { get; }
        public bool IsValid => DamageType != null && Source.IsValid && RuntimeResistanceCollection.IsSupportedResistance(Value);
    }
}
