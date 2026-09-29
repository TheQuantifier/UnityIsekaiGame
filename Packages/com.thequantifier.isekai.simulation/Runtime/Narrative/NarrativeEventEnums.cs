namespace UnityIsekaiGame.Narrative
{
    public enum NarrativeEventCategory
    {
        Unknown,
        Quest,
        Dialogue,
        World,
        Location,
        Travel,
        Combat,
        Social,
        Organization,
        Government,
        Political,
        Legal,
        Economic,
        Discovery,
        Investigation,
        Tutorial,
        Scripted,
        Custom
    }

    public enum NarrativeEventScope
    {
        Unknown,
        OncePerWorld,
        OncePerPerson,
        OncePerQuest,
        OncePerConversation,
        OncePerLocation,
        PerSubject,
        Repeatable,
        Custom
    }

    public enum NarrativeRepeatPolicy
    {
        Unknown,
        Once,
        OncePerScope,
        Repeatable,
        RearmExplicitly,
        RepeatAfterCooldown,
        RepeatUntilCondition,
        Custom
    }

    public enum NarrativeArmingPolicy
    {
        Unknown,
        OnWorldInitialization,
        Explicit,
        AfterAnotherEvent,
        QuestBegins,
        ConversationStarts,
        AtAuthoritativeTime,
        Development,
        Custom
    }

    public enum NarrativeEventLifecycle
    {
        Unknown,
        Created,
        Armed,
        Waiting,
        Triggered,
        Executing,
        Resolved,
        Failed,
        Cancelled,
        Disarmed,
        Historical,
        Invalid
    }

    public enum NarrativeTriggerCategory
    {
        Unknown,
        DomainEvent,
        StateChanged,
        CurrentStateSatisfied,
        HistoricalOccurrence,
        AuthoritativeTime,
        QuestEvent,
        QuestOutcome,
        ObjectiveState,
        DialogueChoice,
        ConversationState,
        LocationEntered,
        LocationExited,
        InteractionCompleted,
        JourneyState,
        TravelEncounter,
        TerritoryCrossed,
        CombatOutcome,
        PersonState,
        ItemState,
        OrganizationState,
        GovernmentState,
        SocialState,
        LegalState,
        ExplicitSignal,
        Custom
    }

    public enum NarrativeConditionCategory
    {
        Unknown,
        Always,
        AuthoritativeTruth,
        ActorKnowledge,
        ParticipantKnowledge,
        InstitutionalKnowledge,
        Belief,
        QuestState,
        DialogueState,
        LocationState,
        ItemState,
        CharacterState,
        OrganizationState,
        SocialState,
        EconomicState,
        LegalState,
        TimeState,
        HistoricalState,
        NarrativeState,
        NarrativeArc,
        Custom
    }

    public enum NarrativeConditionGroupPolicy
    {
        All,
        Any,
        None,
        AtLeastN
    }

    public enum NarrativeTriggerMode
    {
        Unknown,
        TriggerImmediatelyWhenMatched,
        QueueForExecution,
        TriggerAfterDelay,
        RequireExplicitActivationAfterMatch,
        Custom
    }

    public enum NarrativeDelayedRevalidationPolicy
    {
        Revalidate,
        SnapshotAtTrigger,
        MixedExplicit
    }

    public enum NarrativeActionCategory
    {
        Unknown,
        None,
        InstantiateQuest,
        PublishQuestListing,
        CreateQuestOffer,
        DirectAssignQuest,
        SuspendQuest,
        RetireQuest,
        StartConversation,
        EndConversation,
        EmitNarrativeSignal,
        GrantInformation,
        CreateObservation,
        ActivateTravelCondition,
        ResolveTravelCondition,
        TriggerTravelEncounter,
        RequestConnectionStateChange,
        GrantAccess,
        InvokeInteractionService,
        TriggerSocialInteraction,
        RequestOrganizationMembership,
        RequestRankChange,
        RequestOfficeAction,
        RequestPermit,
        CreateIncidentReport,
        HistoricalEventRequest,
        RequestNarrativeStateTransition,
        RequestNarrativeArcProgression,
        ScheduleNarrativeEvent,
        ArmNarrativeEvent,
        DisarmNarrativeEvent,
        Custom
    }

    public enum NarrativeActionRequirement
    {
        Required,
        OptionalBestEffort,
        Deferred
    }

    public enum NarrativeActionAtomicityPolicy
    {
        AtomicAllActions,
        OrderedIndependent,
        RequiredAtomicOptionalIndependent,
        Custom
    }

    public enum NarrativeActionLifecycle
    {
        Unknown,
        Pending,
        Prepared,
        Committed,
        Failed,
        SkippedOptional,
        RolledBack,
        Historical
    }

    public enum NarrativeRetryPolicy
    {
        NeverRetryAutomatically,
        RetryExplicitly,
        RetryAfterCondition
    }

    public enum NarrativeEventVisibility
    {
        Unknown,
        Public,
        ParticipantKnown,
        OrganizationKnown,
        GovernmentKnown,
        Restricted,
        Secret,
        Hidden,
        Diagnostic
    }

    public enum NarrativeOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinitionRegistry,
        MissingDefinition,
        DefinitionInvalid,
        MissingRuntimeIntegration,
        ConditionFailed,
        TriggerIgnored,
        NotArmed,
        AlreadyResolved,
        ActionFailed,
        OptionalActionFailed,
        AtomicityRejected,
        CascadeLimitReached,
        RevisionConflict,
        WrongWorld,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }

    public enum NarrativeSignalSourceKind
    {
        Unknown,
        NarrativeSystem,
        DialogueEffect,
        QuestOutcome,
        NarrativeStateTransition,
        NarrativeArcProgression,
        Development,
        ScriptedAuthoritative,
        Custom
    }

    public enum NarrativeDiagnosticCategory
    {
        Definition,
        Scope,
        Arming,
        Trigger,
        Condition,
        Signal,
        Action,
        ActionDependency,
        Atomicity,
        Cascade,
        Visibility,
        Scheduler,
        Persistence,
        Integration,
        HistoricalBoundary
    }
}
