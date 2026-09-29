namespace UnityIsekaiGame.Progression
{
    public enum BirthGiftType
    {
        PermanentAttributeGrant,
        LatentSkill,
        GrowthAffinity
    }

    public enum BirthGiftAwakeningMode
    {
        ImmediateAutomatic,
        DelayedActivePlaytime,
        Manual
    }

    public enum BirthGiftRuntimeState
    {
        Dormant,
        Awakened
    }

    public enum RoleLifecycleState
    {
        Active,
        Suspended,
        Revoked,
        FormerHistorical
    }

    public enum SocialStatusContextKind
    {
        Global,
        Faction,
        Government,
        Place,
        Jurisdiction,
        Person,
        Organization
    }

    public enum SocialStatusLifecycleState
    {
        Active,
        ResolvedHistorical,
        RevokedHistorical,
        ExpiredHistorical
    }

    public enum ActivityOutcome
    {
        Success,
        Failure,
        Abandoned,
        Death
    }

    public enum ActivityType
    {
        Battle,
        Quest,
        Contract,
        ProfessionJob,
        OfficeDuty,
        WorldEvent,
        ExplorationMission,
        TradeAssignment,
        TransportAssignment,
        DevelopmentTest
    }
}
