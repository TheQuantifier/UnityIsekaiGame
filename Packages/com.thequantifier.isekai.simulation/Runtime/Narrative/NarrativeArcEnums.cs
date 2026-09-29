namespace UnityIsekaiGame.Narrative
{
    public enum NarrativeArcScope
    {
        Unknown,
        World,
        Person
    }

    public enum NarrativeArcLifecycle
    {
        Unknown,
        Eligible,
        Active,
        Completed,
        Failed,
        Cancelled,
        Historical,
        Invalid
    }

    public enum NarrativeArcStageLifecycle
    {
        Unknown,
        Locked,
        Eligible,
        Active,
        Completed,
        Skipped,
        Failed,
        Blocked,
        Historical
    }

    public enum NarrativeArcDependencyKind
    {
        Unknown,
        StageCompleted,
        StageSkipped,
        StageResolved,
        AllStagesResolved,
        AnyStageResolved,
        AtLeastNStagesResolved,
        QuestOutcome,
        NarrativeState,
        DialogueChoice,
        NarrativeEvent,
        CurrentWorldCondition,
        ArcCompleted,
        ArcResolved,
        Custom
    }

    public enum NarrativeArcQuestBindingMode
    {
        Unknown,
        ReferenceExistingQuest,
        InstantiateOnStageActivation,
        InstantiateAndPublish,
        InstantiateAndDirectOffer,
        ObserveAnyQuestFromDefinition,
        Custom
    }

    public enum NarrativeArcSignalCategory
    {
        Unknown,
        Explicit,
        StageResolved,
        QuestOutcome,
        NarrativeState,
        DialogueChoice,
        NarrativeEvent,
        CurrentWorldCondition,
        Custom
    }

    public enum NarrativeArcOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinitionRegistry,
        MissingDefinition,
        DefinitionInvalid,
        MissingRuntimeIntegration,
        DependencyBlocked,
        StageBlocked,
        ActionFailed,
        QuestBindingFailed,
        CascadeLimitReached,
        RevisionConflict,
        WrongWorld,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }
}
