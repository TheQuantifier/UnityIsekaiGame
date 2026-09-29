namespace UnityIsekaiGame.Narrative
{
    public enum NarrativeVariableKind
    {
        Unknown,
        Boolean,
        Integer,
        StateToken,
        StableSubjectReference,
        OptionalStableSubjectReference,
        SmallCounter
    }

    public enum NarrativeStateScope
    {
        Unknown,
        World,
        Person,
        Quest,
        Organization,
        Faction,
        Government,
        Location,
        CustomSubject
    }

    public enum NarrativeStateVisibility
    {
        Unknown,
        Public,
        ParticipantKnown,
        Restricted,
        Secret,
        Hidden,
        Diagnostic
    }

    public enum NarrativeVariableMutabilityPolicy
    {
        Unknown,
        TransitionOnly,
        DevelopmentOverride
    }

    public enum NarrativeTransitionSourceKind
    {
        Unknown,
        DialogueChoice,
        QuestOutcome,
        QuestObjective,
        NarrativeEvent,
        ExplicitNarrativeSignal,
        Development,
        ScriptedAuthoritative,
        Custom
    }

    public enum NarrativeStateTransitionStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinitionRegistry,
        MissingDefinition,
        DefinitionInvalid,
        InvalidValueType,
        InvalidScope,
        ConditionFailed,
        SourceValueMismatch,
        TerminalState,
        ExclusionRejected,
        ConsequenceFailed,
        OptionalConsequenceFailed,
        RevisionConflict,
        StaleState,
        WrongWorld,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }

    public enum NarrativeStateLifecycle
    {
        Unknown,
        DefaultProjected,
        Active,
        Historical,
        Invalid
    }

    public enum NarrativeTransitionRepeatPolicy
    {
        Unknown,
        OncePerScope,
        IdempotentSameTarget,
        Repeatable
    }

    public enum NarrativeTransitionReentryPolicy
    {
        Unknown,
        Allow,
        RejectSameValue,
        RejectAfterTerminal
    }
}
