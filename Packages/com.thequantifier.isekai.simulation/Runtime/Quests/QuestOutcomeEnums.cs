namespace UnityIsekaiGame.Quests
{
    public enum QuestOutcomeScope
    {
        Unknown,
        Assignment,
        Quest
    }

    public enum QuestTerminalOutcomeKind
    {
        Unknown,
        Completed,
        Failed,
        Cancelled,
        Expired
    }

    public enum QuestCompletionPolicy
    {
        Unknown,
        AutoCompleteWhenRequiredObjectivesSatisfied,
        RequireTurnIn,
        RequireIssuerVerification,
        ExplicitSystemCompletion
    }

    public enum QuestFailureReasonCode
    {
        Unknown,
        DeadlineExpired,
        ProtectedTargetLost,
        RequiredItemLost,
        ActorDied,
        IssuerCancelled,
        SystemRejected,
        Custom
    }

    public enum QuestFailureTriggerKind
    {
        Unknown,
        ExplicitRequest,
        DomainEvent,
        StateEvaluation,
        Deadline
    }

    public enum QuestDeadlineStartKind
    {
        Unknown,
        AssignmentAccepted,
        QuestCreated,
        AbsoluteWorldTime
    }

    public enum QuestDeadlineExpirationPolicy
    {
        Unknown,
        FailAssignment,
        FailQuest,
        LockCompletion,
        AdvisoryOnly
    }

    public enum QuestRewardCategory
    {
        Unknown,
        Currency,
        Item,
        Reputation,
        Relationship,
        MembershipRank,
        ProfessionQualification,
        LegalPermitStatus,
        Knowledge,
        Custom
    }

    public enum QuestRewardDeliveryPolicy
    {
        Unknown,
        GrantOnCompletion,
        ClaimAfterCompletion,
        ManualIssuerDelivery
    }

    public enum QuestRewardPackageAtomicityPolicy
    {
        Unknown,
        AllOrNothing,
        AllowPartial
    }

    public enum QuestRewardEntitlementState
    {
        Unknown,
        Pending,
        Claimable,
        Granted,
        PartiallyGranted,
        Failed,
        Cancelled
    }

    public enum QuestRewardGrantState
    {
        Unknown,
        Prepared,
        Granted,
        Failed,
        Duplicate
    }

    public enum QuestOutcomeEventKind
    {
        Unknown,
        CompletionEvaluated,
        DeadlineCreated,
        DeadlineExpired,
        TerminalOutcomeRecorded,
        RewardEntitlementCreated,
        RewardGranted,
        RewardGrantFailed,
        Restore
    }

    public enum QuestOutcomeOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinitionRegistry,
        MissingQuestRuntime,
        MissingParticipationRuntime,
        MissingObjectiveRuntime,
        MissingQuest,
        MissingAssignment,
        MissingDefinition,
        MissingObjectives,
        ObjectivesIncomplete,
        CompletionNotReady,
        TurnInRequired,
        IssuerVerificationRequired,
        ExplicitCompletionRequired,
        WrongWorld,
        AlreadyTerminal,
        DeadlineExpired,
        MissingDeadline,
        MissingReward,
        RewardNotClaimable,
        RewardUnsupported,
        RewardOwnerRejected,
        PersistenceInvalid,
        RestoreFailed,
        RevisionConflict,
        Disposed
    }
}
