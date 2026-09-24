namespace UnityIsekaiGame.Stats
{
    public static class CalculatedStatContributionSourceUtility
    {
        public static CalculatedStatContributionSourceCategory Map(StatModifierSourceType sourceType)
        {
            return sourceType switch
            {
                StatModifierSourceType.Equipment => CalculatedStatContributionSourceCategory.Equipment,
                StatModifierSourceType.StatusEffect => CalculatedStatContributionSourceCategory.CombatStatus,
                StatModifierSourceType.Ability => CalculatedStatContributionSourceCategory.Ability,
                StatModifierSourceType.Role => CalculatedStatContributionSourceCategory.Role,
                StatModifierSourceType.SocialStatus => CalculatedStatContributionSourceCategory.SocialStatus,
                StatModifierSourceType.Origin => CalculatedStatContributionSourceCategory.Origin,
                StatModifierSourceType.BirthGift => CalculatedStatContributionSourceCategory.BirthGift,
                StatModifierSourceType.Progression or StatModifierSourceType.Debug => CalculatedStatContributionSourceCategory.Development,
                _ => CalculatedStatContributionSourceCategory.Other
            };
        }
    }
}
