namespace UnityIsekaiGame.Combat.CombatState
{
    public enum CombatStateValue
    {
        OutOfCombat,
        EnteringCombat,
        InCombat,
        Disengaging
    }

    public enum CombatActivityClassification
    {
        AttackAttempted,
        AttackHit,
        DamageApplied,
        DamagePrevented,
        HostileOngoingEffectApplied,
        HostileOngoingEffectTicked,
        ExplicitEngagement,
        ScriptedEngagement
    }

    public enum CombatExitReason
    {
        Timeout,
        Explicit,
        Forced,
        Dead,
        StaleBody,
        EncounterEnded
    }

    public enum CombatEncounterCompletionReason
    {
        None,
        Timeout,
        Explicit,
        Forced,
        NoActiveEngagements,
        StaleParticipants
    }
}
