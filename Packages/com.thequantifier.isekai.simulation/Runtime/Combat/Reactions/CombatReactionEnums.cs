namespace UnityIsekaiGame.Combat.Reactions
{
    public enum CombatReactionTriggerType
    {
        None,
        AttackAttempted,
        AttackMissed,
        AttackHit,
        CriticalHit,
        DamageApplied,
        DamageFullyPrevented,
        HealingApplied,
        OverhealingOccurred,
        DefenseSucceeded,
        AttackBlocked,
        AttackParried,
        AttackDodged,
        OngoingEffectApplied,
        OngoingEffectTicked,
        HealthReachedZero,
        ActorDefeated,
        ActorBecameUnconscious,
        ActorDied,
        ActorRecovered,
        ActorRevived,
        ActorEnteredCombat,
        ActorLeftCombat,
        EncounterEnded,
        CombatExecutionCommitted,
        CombatExecutionInterrupted
    }

    public enum CombatReactionOperationType
    {
        NoOpDiagnostic,
        ApplyDamage,
        ApplyHealing,
        ApplyOngoingEffect,
        ModifyResource,
        ApplyStatusEffect,
        RemoveStatusEffect,
        ApplyCondition,
        RemoveCondition,
        TriggerImmediateAbility
    }

    public enum CombatReactionTargetPolicy
    {
        None,
        OriginalSource,
        OriginalTarget,
        ReactionOwner,
        OtherCombatant,
        Self,
        ExplicitActor
    }

    public enum CombatReactionOwnershipSide
    {
        Any,
        Source,
        Target,
        ReactionOwner
    }

    public enum CombatReactionSourceKind
    {
        Unknown,
        Trait,
        Skill,
        Equipment,
        Item,
        Status,
        Condition,
        Role,
        SocialStatus,
        Ability,
        OngoingEffect,
        ActorProfile,
        Development,
        Runtime
    }

    public enum CombatReactionRecursionPolicy
    {
        OncePerSourcePerChain,
        AllowRecursive
    }
}
