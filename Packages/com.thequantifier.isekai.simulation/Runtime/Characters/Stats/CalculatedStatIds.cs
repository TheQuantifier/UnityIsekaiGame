namespace UnityIsekaiGame.Stats
{
    public static class CalculatedStatIds
    {
        public const string PhysicalPower = "calculated-stat.physical-power";
        public const string MagicalPower = "calculated-stat.magical-power";
        public const string HealingPower = "calculated-stat.healing-power";
        public const string SupportPower = "calculated-stat.support-power";
        public const string PhysicalDefense = "calculated-stat.physical-defense";
        public const string MagicalDefense = "calculated-stat.magical-defense";
        public const string MaximumHealth = "calculated-stat.maximum-health";
        public const string MaximumStamina = "calculated-stat.maximum-stamina";
        public const string MaximumMana = "calculated-stat.maximum-mana";
        public const string MovementSpeed = "calculated-stat.movement-speed";
        public const string CarryingCapacity = "calculated-stat.carrying-capacity";
        public const string Accuracy = "calculated-stat.accuracy";
        public const string Evasion = "calculated-stat.evasion";

        public const string ResourceHealth = "resource.health";
        public const string ResourceStamina = "resource.stamina";
        public const string ResourceMana = "resource.mana";

        public static readonly string[] AlphaCalculatedStatIds =
        {
            PhysicalPower,
            MagicalPower,
            HealingPower,
            SupportPower,
            PhysicalDefense,
            MagicalDefense,
            MaximumHealth,
            MaximumStamina,
            MaximumMana,
            MovementSpeed,
            CarryingCapacity,
            Accuracy,
            Evasion
        };

        public static readonly string[] CoreResourceIds =
        {
            ResourceHealth,
            ResourceStamina,
            ResourceMana
        };

        public static bool IsCoreResourceId(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return false;
            }

            for (int i = 0; i < CoreResourceIds.Length; i++)
            {
                if (string.Equals(CoreResourceIds[i], resourceId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
