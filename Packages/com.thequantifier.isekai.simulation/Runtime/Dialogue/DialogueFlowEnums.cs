namespace UnityIsekaiGame.Dialogue
{
    public enum DialogueNodeCategory
    {
        Unknown,
        Line,
        Narration,
        Information,
        ChoicePrompt,
        Branch,
        Action,
        QuestOffer,
        QuestTurnIn,
        RewardClaim,
        SocialInteraction,
        InstitutionalService,
        End,
        Redirect,
        Custom
    }

    public enum DialogueChoiceCategory
    {
        Unknown,
        Continue,
        Question,
        Response,
        Accept,
        Refuse,
        QuestAccept,
        QuestRefuse,
        QuestTurnIn,
        RewardClaim,
        ServiceRequest,
        InformationRequest,
        SocialResponse,
        EndConversation,
        Custom
    }

    public enum DialogueFlowState
    {
        Unknown,
        NotStarted,
        AwaitingAdvance,
        AwaitingChoice,
        Transitioning,
        Suspended,
        Ended,
        Invalid
    }

    public enum DialogueChoiceAvailabilityState
    {
        Unknown,
        Available,
        UnavailableVisible,
        Hidden,
        AlreadyUsed,
        Invalid
    }

    public enum DialogueConditionKind
    {
        Unknown,
        Always,
        QuestExists,
        QuestOfferActive,
        QuestAssignmentActive,
        QuestObjectiveReady,
        QuestOutcomeCompleted,
        RewardClaimable,
        Knowledge,
        Belief,
        OrganizationMembership,
        OrganizationRank,
        Office,
        Authority,
        Reputation,
        Relationship,
        ItemPossessed,
        ItemEquipped,
        Location,
        InteractionPoint,
        Permit,
        LegalStatus,
        NarrativeState,
        LocalFlag,
        LocalCounter,
        Custom,
        PartySize,
        PartyHasMember,
        PartyLeader,
        PartyReady
    }

    public enum DialogueConditionEvaluationMode
    {
        Unknown,
        AuthoritativeTruth,
        SpeakerKnowledge,
        ListenerKnowledge,
        ActorKnowledge,
        InstitutionalKnowledge,
        ConversationKnownState,
        Custom
    }

    public enum DialogueValueComparison
    {
        Exists,
        NotExists,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Equal
    }

    public enum DialogueChoiceRepeatPolicy
    {
        Unknown,
        Repeatable,
        OneShotPerConversation,
        OneShotPerActor
    }

    public enum DialogueTransitionCategory
    {
        Unknown,
        Automatic,
        Redirect,
        ChoiceSelected,
        Fallback,
        Failure,
        End,
        Custom
    }

    public enum DialogueEffectKind
    {
        Unknown,
        None,
        SetLocalFlag,
        IncrementLocalCounter,
        RevealInformation,
        TransferInformation,
        CreateQuestOffer,
        AcceptQuestOffer,
        RefuseQuestOffer,
        CompleteQuest,
        ClaimQuestReward,
        SocialInteraction,
        ReputationChange,
        RelationshipChange,
        RequestNarrativeStateTransition,
        GrantMembership,
        GrantRank,
        GrantPermit,
        InteractionService,
        RecordIncident,
        Custom
    }

    public enum DialogueEffectRequirement
    {
        Optional,
        Required
    }

    public enum DialogueSpeakerSelectorKind
    {
        Unknown,
        None,
        ConversationInitiator,
        ActiveSpeaker,
        ParticipantRole,
        SpecificPerson,
        Provider,
        OfficeRepresentative,
        OrganizationRepresentative,
        Custom
    }

    public enum DialogueListenerSelectorKind
    {
        Unknown,
        None,
        AllParticipants,
        ParticipantRole,
        SpecificPerson,
        Initiator,
        Provider,
        Custom
    }

    public enum DialogueFlowEventKind
    {
        Unknown,
        FlowStarted,
        NodeEntered,
        ChoiceSelected,
        NodeExited,
        FlowSuspended,
        FlowResumed,
        FlowEnded,
        Restore
    }

    public enum DialogueFlowOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinitionRegistry,
        MissingConversationRuntime,
        MissingConversation,
        MissingGraph,
        MissingNode,
        MissingChoice,
        MissingParticipant,
        SpeakerResolutionFailed,
        ListenerResolutionFailed,
        ConditionFailed,
        ChoiceHidden,
        ChoiceUnavailable,
        ChoiceAlreadyUsed,
        EffectFailed,
        NoValidTransition,
        AutomaticLoopRejected,
        GraphInvalid,
        RevisionConflict,
        WrongWorld,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }
}
