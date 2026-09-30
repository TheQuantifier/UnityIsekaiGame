namespace UnityIsekaiGame.Quests
{
    public enum QuestObjectiveCategory
    {
        Unknown,
        ReachLocation,
        VisitLocation,
        LeaveLocation,
        UseInteractionPoint,
        TraverseConnection,
        CompleteJourney,
        CrossTerritory,
        CrossCheckpoint,
        Encounter,
        ObtainItem,
        PossessItem,
        TransferItem,
        DeliverItem,
        CraftItem,
        RepairItem,
        SalvageItem,
        SpendCurrency,
        ReceiveCurrency,
        DefeatTarget,
        DefeatCount,
        SurviveCombat,
        HealTarget,
        LearnFact,
        DiscoverLocation,
        ObtainRecord,
        SpeakToPerson,
        SocialInteraction,
        RelationshipState,
        ReputationState,
        JoinOrganization,
        ReachOrganizationRank,
        HoldOffice,
        ObtainProfession,
        ObtainQualification,
        EmploymentState,
        ObtainPermit,
        LegalState,
        ReportIncident,
        InstitutionalAction,
        Custom
    }

    public enum QuestObjectiveProgressModel
    {
        Unknown,
        BooleanState,
        BooleanEvent,
        Counter,
        QuantityCurrent,
        QuantityCumulative,
        SetMembership,
        UniqueTargetCount,
        Threshold,
        Sequence,
        Composite,
        Custom
    }

    public enum QuestObjectiveProgressSource
    {
        Unknown,
        CurrentStateQuery,
        DomainEvent,
        HistoricalQuery,
        ExplicitNarrativeSignal,
        ManualDevelopment,
        Custom
    }

    public enum QuestObjectiveRequirementClassification
    {
        Unknown,
        Required,
        Optional,
        Bonus,
        HiddenRequired
    }

    public enum QuestObjectiveVisibility
    {
        Unknown,
        Public,
        RecipientKnown,
        Restricted,
        Secret,
        Hidden,
        Diagnostic
    }

    public enum QuestObjectiveSatisfactionPolicy
    {
        Unknown,
        StickyOnceSatisfied,
        DynamicWhileTrue
    }

    public enum QuestObjectiveRepetitionPolicy
    {
        Unknown,
        CountEveryCommittedEvent,
        CountSourceEventOnce,
        CountUniqueTargetOnce
    }

    public enum QuestObjectiveProgressBeforeActivationPolicy
    {
        Unknown,
        Ignore,
        EvaluateCurrentStateOnActivation,
        TrackWhileLocked
    }

    public enum QuestObjectiveOwnershipScope
    {
        Unknown,
        PerAssignment,
        SharedQuest,
        SharedGroup,
        Custom
    }

    public enum QuestObjectiveLifecycleState
    {
        Unknown,
        Locked,
        Active,
        Satisfied,
        Suspended,
        Abandoned,
        Withdrawn,
        Historical,
        Invalid
    }

    public enum QuestObjectiveGroupPolicy
    {
        Unknown,
        All,
        Any,
        AtLeast,
        OrderedAll,
        OptionalGroup
    }

    public enum QuestObjectiveOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinitionRegistry,
        MissingQuestRuntime,
        MissingParticipationRuntime,
        MissingQuest,
        MissingQuestDefinition,
        MissingAssignment,
        MissingObjective,
        MissingObjectiveDefinition,
        AssignmentNotActive,
        UnsupportedProgressModel,
        NotCommitted,
        EventTooEarly,
        EventNotMatched,
        AlreadyCounted,
        WrongWorld,
        RevisionConflict,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }

    public enum QuestObjectiveEventKind
    {
        Unknown,
        ObjectivesInstantiated,
        ObjectiveActivated,
        ProgressUpdated,
        ObjectiveSatisfied,
        ObjectiveUnsatisfied,
        ObjectiveSuspended,
        ObjectiveResumed,
        ObjectiveAbandoned,
        ObjectiveWithdrawn,
        Restore
    }
}
