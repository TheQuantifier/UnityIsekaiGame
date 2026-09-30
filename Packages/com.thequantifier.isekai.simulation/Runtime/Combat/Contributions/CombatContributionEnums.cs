namespace UnityIsekaiGame.Combat.Contributions
{
    public enum CombatContributionType
    {
        DamageApplied,
        HealingApplied,
        DamagePrevented,
        SuccessfulBlock,
        SuccessfulParry,
        SuccessfulDodge,
        ResourceSupport,
        OngoingDamageApplied,
        OngoingHealingApplied,
        ReactionDamageApplied,
        ReactionHealingApplied,
        DefeatCaused,
        DeathCaused,
        RecoveryProvided,
        RevivalProvided,
        HostileEngagementParticipation,
        SupportParticipation
    }

    public enum CombatContributionSourceKind
    {
        Unknown,
        Direct,
        Attack,
        Ability,
        Item,
        Equipment,
        OngoingEffect,
        Reaction,
        Defense,
        Lifecycle,
        CombatExecution,
        Environment,
        Development
    }

    public enum CombatCreditType
    {
        Defeat,
        Kill,
        Assist,
        Participation
    }

    public enum CombatRewardEligibilityCategory
    {
        FutureExperience,
        FutureSkillProgression,
        FutureQuestHook,
        FutureLootEligibility,
        FutureReputationHook,
        DiagnosticOnly
    }

    public enum CombatContributionRecordCompressionPolicy
    {
        KeepAllUntilFinalized,
        KeepLatestPerTransaction,
        Reserved
    }
}
