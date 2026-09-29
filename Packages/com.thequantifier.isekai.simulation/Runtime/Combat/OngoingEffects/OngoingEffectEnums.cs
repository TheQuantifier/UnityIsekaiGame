namespace UnityIsekaiGame.Combat.OngoingEffects
{
    public enum OngoingEffectOperationType
    {
        Damage,
        Healing,
        ResourceGain,
        ResourceSpend
    }

    public enum OngoingEffectInstanceState
    {
        Pending,
        Active,
        Paused,
        Completed,
        Cancelled,
        Invalid
    }

    public enum OngoingEffectStackingPolicy
    {
        IndependentInstances,
        RefreshDuration,
        ReplaceExisting,
        AddStacks,
        RejectDuplicate
    }

    public enum OngoingEffectSourceOwnership
    {
        SourceAgnostic,
        SourceSpecific,
        OriginSpecific
    }

    public enum OngoingEffectUnconsciousPolicy
    {
        ContinueWhileUnconscious,
        PauseWhileUnconscious,
        CancelWhenUnconscious
    }

    public enum OngoingEffectDeathPolicy
    {
        CancelOnDeath,
        PauseAfterDeath,
        ContinueAfterDeath
    }

    public enum OngoingEffectApplicationOutcome
    {
        Created,
        Refreshed,
        Replaced,
        StackAdded,
        DuplicateRejected,
        Preview
    }

    public enum OngoingEffectTickOutcome
    {
        Applied,
        Prevented,
        Skipped,
        Paused,
        Cancelled,
        Failed,
        Duplicate
    }
}
