namespace UnityIsekaiGame.Social.Decisions
{
    public enum SocialIntentionCategory
    {
        Custom = 0,
        Affiliate = 10,
        MaintainRelationship = 20,
        RepairRelationship = 30,
        SeekSupport = 40,
        OfferSupport = 50,
        SeekInformation = 60,
        ShareInformation = 70,
        Warn = 80,
        Confront = 90,
        Threaten = 100,
        Apologize = 110,
        Reconcile = 120,
        FulfillObligation = 130,
        ProtectSecret = 140,
        ImproveStanding = 150,
        DefendReputation = 160,
        SupportGroup = 170,
        AvoidPerson = 180,
        RespondToInteraction = 190
    }

    public enum SocialDecisionExecutionMode
    {
        Disabled = 0,
        EvaluateOnly = 10,
        SelectOnly = 20,
        SubmitForExecution = 30,
        AwaitExternalApproval = 40
    }

    public enum SocialDecisionStatus
    {
        RuntimeNotReady = 0,
        InvalidRequest = 10,
        MissingProfile = 20,
        Disabled = 30,
        EvaluationCooldown = 40,
        NoAction = 50,
        CandidateSelected = 60,
        Submitted = 70,
        ExecutionRejected = 80,
        StaleDecision = 90,
        Restored = 100,
        ValidationFailed = 110
    }

    public enum SocialDecisionLifecycleState
    {
        Idle = 0,
        Evaluating = 10,
        IntentionActive = 20,
        CandidateSelected = 30,
        AwaitingExecution = 40,
        InteractionPending = 50,
        Completed = 60,
        Failed = 70,
        Deferred = 80,
        Cancelled = 90,
        Expired = 100,
        Invalid = 110
    }

    public enum SocialDecisionActorControlPolicy
    {
        AutonomousNpc = 0,
        PlayerControlled = 10,
        TemporarilyAiControlled = 20,
        ScriptedActor = 30,
        Disabled = 40
    }

    public enum SocialDecisionTargetSource
    {
        Explicit = 0,
        AvailableContext = 10,
        Relationship = 20,
        SocialGraph = 30,
        GroupMembership = 40,
        PendingInteraction = 50
    }

    public enum SocialDecisionConsiderationInput
    {
        Constant = 0,
        TrustTowardTarget = 10,
        AffectionTowardTarget = 20,
        RespectTowardTarget = 30,
        FearTowardTarget = 40,
        LoyaltyTowardTarget = 50,
        HostilityTowardTarget = 60,
        TargetTrustTowardActor = 70,
        RelationshipExists = 80,
        SharedGroupMembership = 90,
        TargetIsolation = 100,
        GraphDistance = 110,
        ReputationEsteem = 120,
        ReputationDanger = 130,
        PendingRequest = 140,
        RepetitionPenalty = 150,
        Cooldown = 160,
        ScriptedPriority = 170
    }

    public enum SocialDecisionResponseCurve
    {
        Linear = 0,
        InverseLinear = 10,
        Step = 20,
        Threshold = 30,
        Quadratic = 40
    }

    public enum SocialDecisionMissingDataPolicy
    {
        Neutral = 0,
        Zero = 10,
        RejectCandidate = 20,
        IgnoreConsideration = 30
    }
}
