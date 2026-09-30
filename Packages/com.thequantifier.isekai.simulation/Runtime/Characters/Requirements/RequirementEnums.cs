namespace UnityIsekaiGame.Requirements
{
    public enum RequirementLogicalOperator
    {
        All,
        Any,
        None
    }

    public enum RequirementNodeType
    {
        BaseAttribute,
        CalculatedStat,
        ResourceCurrent,
        ResourceMaximum,
        ResourceNormalized,
        SkillGrade,
        TraitLifecycle,
        Role,
        SocialStatus,
        Origin,
        BirthGift,
        Title,
        InventoryItem,
        EquippedItem,
        Ability,
        ConditionPresent,
        ConditionAbsent,
        Currency,
        CapabilityBoolean,
        CapabilityNumeric,
        Species,
        BiologicalClassification,
        BodyForm,
        LifecycleState,
        BiologicalConditionPresent,
        BiologicalConditionAbsent
    }

    public enum RequirementComparison
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterOrEqual,
        LessThan,
        LessOrEqual
    }

    public enum RequirementFailureVisibility
    {
        Visible,
        Obscured,
        Hidden
    }
}
