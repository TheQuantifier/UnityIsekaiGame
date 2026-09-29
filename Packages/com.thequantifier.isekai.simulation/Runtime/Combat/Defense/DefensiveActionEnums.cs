namespace UnityIsekaiGame.Combat.Defense
{
    public enum DefensiveActionType
    {
        None,
        Guard,
        Block,
        Parry,
        Dodge
    }

    public enum DefensiveActionState
    {
        Inactive,
        Guarding,
        BlockWindow,
        ParryWindow,
        DodgeWindow
    }

    public enum DefensiveDamageReductionMode
    {
        None,
        FlatReduction,
        PercentageReduction,
        FullPrevention
    }

    public enum DefenseResolutionOutcome
    {
        None,
        NoDefense,
        Ineligible,
        Expired,
        InsufficientStamina,
        DodgeSucceeded,
        DodgeFailed,
        ParrySucceeded,
        ParryFailed,
        BlockSucceeded,
        BlockFailed,
        GuardReduced,
        Prevented
    }

    public enum DefenseCancellationReason
    {
        Explicit,
        Replaced,
        Consumed,
        Expired,
        LifecycleInvalidated,
        Restore
    }
}
