namespace UnityIsekaiGame.ActorLifecycle
{
    public enum ActorLifecycleState
    {
        Active,
        Defeated,
        Unconscious,
        Dead
    }

    public enum DefeatPolicyOutcome
    {
        BecomeUnconscious,
        DieImmediately,
        RemainDefeated,
        IgnoreDefeat
    }

    public enum LifecycleTransitionKind
    {
        None,
        Defeat,
        Unconsciousness,
        Death,
        Recovery,
        Revival
    }

    public enum LifecycleTriggerKind
    {
        HealthDepleted = 0,
        ExplicitDefeat = 1,
        ExplicitDeath = 2,
        Scripted = 3,
        Environmental = 4,
        Recovery = 5,
        Revival = 6,
        FatalInjury = 7,
        Execution = 8,
        CatastrophicDamage = 9,
        CriticalCondition = 10
    }
}
