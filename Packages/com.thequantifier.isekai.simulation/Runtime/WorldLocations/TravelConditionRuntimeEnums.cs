namespace UnityIsekaiGame.WorldLocations
{
    public enum TravelConditionCategory
    {
        Unknown,
        Terrain,
        Weather,
        Obstruction,
        AccessRestriction,
        Requirement,
        HazardRisk,
        EncounterRisk,
        Magical,
        Social,
        Custom
    }

    public enum TravelConditionTargetScope
    {
        Unknown,
        Location,
        Connection,
        RouteSegment,
        RouteNetwork,
        Journey,
        Traveler,
        RouteEdge,
        Custom
    }

    public enum TravelConditionLifecycleState
    {
        Unknown,
        Scheduled,
        Active,
        Expired,
        Resolved,
        Historical,
        Invalid
    }

    public enum TravelConditionSeverity
    {
        Unknown,
        Trivial,
        Minor,
        Moderate,
        Major,
        Severe,
        Critical
    }

    public enum TravelConditionStackingPolicy
    {
        Unknown,
        Multiplicative,
        Additive,
        HighestOnly,
        ReplaceLowerPriority,
        NonStacking
    }

    public enum TravelConditionVisibility
    {
        Public,
        LocallyKnown,
        Restricted,
        Secret,
        Hidden,
        Diagnostic
    }

    public enum TravelConditionEvaluationMode
    {
        IgnoreDynamicConditions,
        CurrentConditions,
        KnowledgeSafeCurrentConditions
    }

    public enum TravelConditionMutationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingRuntime,
        MissingDefinition,
        MissingCondition,
        MissingHazard,
        MissingEncounter,
        MissingTarget,
        Blocked,
        RevisionConflict,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }

    public enum TravelHazardCategory
    {
        Unknown,
        Terrain,
        Biological,
        Environmental,
        Trap,
        Magical,
        CombatLinked,
        Custom
    }

    public enum TravelHazardTriggerPolicy
    {
        Unknown,
        ExplicitOnly,
        JourneyCheckpoint,
        RouteEntry,
        DevelopmentDeterministic
    }

    public enum TravelHazardExposureLifecycleState
    {
        Unknown,
        Potential,
        Triggered,
        Resolved,
        Expired,
        Historical
    }

    public enum TravelHazardOutcome
    {
        None,
        Exposed,
        Avoided,
        DelegatedToBiology,
        DelegatedToCombat,
        Cancelled
    }

    public enum TravelEncounterCategory
    {
        Unknown,
        Neutral,
        Social,
        Merchant,
        Discovery,
        Obstacle,
        Hostile,
        CombatLinked,
        Custom
    }

    public enum TravelEncounterLifecycleState
    {
        Unknown,
        Opportunity,
        Triggered,
        Active,
        Resolved,
        Expired,
        Historical
    }

    public enum TravelEncounterRepeatPolicy
    {
        Unknown,
        OncePerCondition,
        OncePerJourney,
        OncePerRouteEdge,
        Repeatable
    }

    public enum TravelEncounterTriggerPolicy
    {
        Unknown,
        ExplicitOnly,
        JourneyCheckpoint,
        RouteEntry,
        DevelopmentDeterministic
    }

    public enum TravelEncounterInterruptionPolicy
    {
        None,
        PauseJourney,
        BlockJourney,
        CancelJourney
    }

    public enum TravelEncounterResolution
    {
        None,
        Accepted,
        Avoided,
        ResolvedPeacefully,
        DelegatedToCombat,
        Cancelled
    }
}
