namespace UnityIsekaiGame.Social.Interactions
{
    public enum SocialInteractionCategory
    {
        Greeting = 0,
        Introduction = 1,
        PositiveExpression = 2,
        NegativeExpression = 3,
        Request = 4,
        Response = 5,
        Apology = 6,
        Forgiveness = 7,
        Reconciliation = 8,
        Warning = 9,
        Threat = 10,
        Accusation = 11,
        Denial = 12,
        Disclosure = 13,
        Promise = 14,
        Support = 15,
        PublicStatement = 16,
        Custom = 17
    }

    public enum SocialInteractionRole
    {
        Initiator = 0,
        Target = 1,
        Witness = 2,
        Audience = 3,
        Subject = 4,
        Intermediary = 5,
        Recipient = 6,
        NamedThirdParty = 7
    }

    public enum SocialInteractionResponse
    {
        None = 0,
        Accept = 1,
        Refuse = 2,
        Ignore = 3,
        Counter = 4,
        Forgive = 5,
        Reject = 6,
        Acknowledge = 7,
        Deny = 8,
        Admit = 9,
        Defer = 10,
        Custom = 11
    }

    public enum SocialInteractionOutcome
    {
        Success = 0,
        Failure = 1,
        PartialSuccess = 2,
        Accepted = 3,
        Refused = 4,
        Ignored = 5,
        Countered = 6,
        Misunderstood = 7,
        Blocked = 8,
        Invalid = 9,
        Pending = 10,
        Expired = 11,
        Cancelled = 12
    }

    public enum SocialInteractionStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        Pending = 3,
        Refused = 4,
        InvalidRequest = 5,
        MissingDefinitionRegistry = 6,
        MissingDefinition = 7,
        MissingTransactionId = 8,
        MissingRecordId = 9,
        DuplicateRecordId = 10,
        MissingInitiator = 11,
        MissingTarget = 12,
        UnknownPerson = 13,
        SelfTargetNotAllowed = 14,
        InvalidRole = 15,
        DuplicateParticipant = 16,
        MissingResponse = 17,
        InvalidResponse = 18,
        PendingNotFound = 19,
        PendingAlreadyResolved = 20,
        PendingExpired = 21,
        CooldownActive = 22,
        ConsequenceRejected = 23,
        RestoreFailed = 24,
        ValidationFailed = 25,
        RuntimeNotReady = 26
    }

    public enum SocialInteractionSubjectKind
    {
        None = 0,
        Person = 1,
        Relationship = 2,
        HistoricalEvent = 3,
        Rumor = 4,
        Claim = 5,
        ReputationAudience = 6,
        Item = 7,
        EconomicReference = 8,
        Place = 9,
        Organization = 10,
        Faction = 11,
        Promise = 12,
        Custom = 13
    }

    public enum SocialInteractionCommunicationChannel
    {
        Unspecified = 0,
        Conversation = 1,
        PublicSpeech = 2,
        WrittenMessage = 3,
        Gesture = 4,
        Magical = 5,
        DevelopmentFixture = 6
    }

    public enum SocialInteractionVisibility
    {
        Private = 0,
        Witnessed = 1,
        Public = 2,
        Secret = 3,
        Diagnostic = 4
    }

    public enum SocialConsequenceTargetRuntime
    {
        None = 0,
        InteractionRecord = 1,
        Attitude = 2,
        Relationship = 3,
        Reputation = 4,
        Rumor = 5,
        Knowledge = 6,
        Memory = 7,
        History = 8,
        Promise = 9,
        Custom = 10
    }

    public enum SocialConsequenceOperation
    {
        None = 0,
        AddOrReplaceContribution = 1,
        CreateRelationship = 2,
        EndRelationship = 3,
        AddReputationContribution = 4,
        TransmitRumor = 5,
        CreateMemoryReference = 6,
        CreateHistoryReference = 7,
        CreatePromise = 8,
        AcceptPromise = 9,
        FulfillPromise = 10,
        BreachPromise = 11,
        CancelPromise = 12,
        ExternalHandled = 13
    }

    public enum SocialPromiseStatus
    {
        Proposed = 0,
        Active = 1,
        Fulfilled = 2,
        Breached = 3,
        Refused = 4,
        Cancelled = 5
    }

    public enum SocialInteractionCooldownScope
    {
        None = 0,
        InitiatorDefinition = 1,
        InitiatorTargetDefinition = 2,
        InitiatorTargetSubjectDefinition = 3
    }
}
