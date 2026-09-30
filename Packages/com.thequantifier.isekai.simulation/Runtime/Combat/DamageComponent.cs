namespace UnityIsekaiGame.Combat
{
    public readonly struct DamageComponent
    {
        public DamageComponent(DamageTypeDefinition damageType, float amount, AttackPowerScalingPolicy attackPowerScaling = AttackPowerScalingPolicy.IgnoreSourceAttackPower)
        {
            DamageType = damageType;
            Amount = amount;
            AttackPowerScaling = attackPowerScaling;
        }

        public DamageTypeDefinition DamageType { get; }
        public float Amount { get; }
        public AttackPowerScalingPolicy AttackPowerScaling { get; }
        public bool IsValid => Amount > 0f && DamageType != null;
    }
}
