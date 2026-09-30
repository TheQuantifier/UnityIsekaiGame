namespace UnityIsekaiGame.Social.Norms
{
    public enum SocialNormCategory
    {
        Courtesy = 0,
        Greeting = 1,
        AddressAndTitle = 2,
        Hospitality = 3,
        RespectAndDeference = 4,
        Confidentiality = 5,
        PromiseAndObligation = 6,
        PublicConduct = 7,
        PersonalBoundary = 8,
        ProfessionalConduct = 9,
        CulturalCustom = 10,
        ReligiousCustom = 11,
        MilitaryCustom = 12,
        CourtEtiquette = 13,
        MourningAndCeremony = 14,
        GiftAndReciprocity = 15,
        SpeechAndDisclosure = 16,
        Custom = 17
    }

    public enum SocialNormScope
    {
        Global = 0,
        Cultural = 1,
        PlaceBased = 2,
        AudienceBased = 3,
        RelationshipBased = 4,
        RoleBased = 5,
        Professional = 6,
        Religious = 7,
        Military = 8,
        CourtOrCeremonial = 9,
        Household = 10,
        CustomContext = 11
    }

    public enum SocialNormConductStrength
    {
        Required = 0,
        StronglyExpected = 1,
        Encouraged = 2,
        Neutral = 3,
        Discouraged = 4,
        StronglyDiscouraged = 5,
        Prohibited = 6
    }

    public enum SocialNormAssessmentClassification
    {
        NotApplicable = 0,
        Unknown = 1,
        Satisfied = 2,
        Exceeded = 3,
        MinorViolation = 4,
        Violation = 5,
        SeriousViolation = 6,
        Excused = 7,
        Disputed = 8,
        Indeterminate = 9
    }

    public enum SocialNormApplicabilityStatus
    {
        Unknown = 0,
        Applicable = 1,
        NotApplicable = 2,
        FailedHardContext = 3,
        MissingRequiredContext = 4,
        SuppressedByConflict = 5
    }

    public enum SocialNormActorKnowledgeState
    {
        Unknown = 0,
        Knew = 1,
        Believed = 2,
        Misunderstood = 3,
        Unaware = 4,
        Unavailable = 5,
        Irrelevant = 6
    }

    public enum SocialNormObserverAwarenessState
    {
        Unknown = 0,
        Observed = 1,
        DidNotObserve = 2,
        HeardRumor = 3,
        Misunderstood = 4,
        AudienceAggregate = 5
    }

    public enum SocialNormVisibility
    {
        Private = 0,
        Witnessed = 1,
        Public = 2,
        Development = 3
    }

    public enum SocialNormExceptionKind
    {
        None = 0,
        Emergency = 1,
        ExplicitPermission = 2,
        CloseRelationship = 3,
        PrivateContext = 4,
        Ceremony = 5,
        SuperiorOrder = 6,
        IgnoranceMitigation = 7,
        Incapacity = 8,
        ConflictingHigherDuty = 9,
        TargetWaiver = 10,
        Custom = 11
    }

    public enum SocialNormExceptionEffect
    {
        None = 0,
        MakeNotApplicable = 1,
        ReduceSeverity = 2,
        ExcuseViolation = 3,
        SuppressConsequences = 4,
        RedirectNorm = 5
    }

    public enum SocialNormConsequenceTargetRuntime
    {
        None = 0,
        InterpersonalAttitude = 1,
        Reputation = 2,
        Relationship = 3,
        Rumor = 4,
        MemoryReference = 5,
        HistoryReference = 6,
        SocialInteraction = 7,
        Promise = 8
    }

    public enum SocialNormConsequenceOperation
    {
        None = 0,
        AddOrReplaceAttitudeContribution = 1,
        AddOrReplaceReputationContribution = 2,
        CreateRelationshipReference = 3,
        CreateRumorReference = 4,
        CreateMemoryReference = 5,
        CreateHistoryReference = 6,
        ReferenceInteraction = 7,
        ReferencePromise = 8
    }

    public enum SocialNormConsequencePolicy
    {
        Required = 0,
        Optional = 1
    }

    public enum SocialNormOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        RuntimeNotReady = 3,
        MissingDefinitionRegistry = 4,
        MissingNormDefinition = 5,
        MissingTransactionId = 6,
        MissingAssessmentId = 7,
        DuplicateAssessmentId = 8,
        MissingActor = 9,
        UnknownActor = 10,
        UnknownTarget = 11,
        UnknownObserver = 12,
        InvalidRequest = 13,
        InvalidDefinition = 14,
        InvalidConsequence = 15,
        ConsequenceFailed = 16,
        RestoreFailed = 17,
        ValidationFailed = 18
    }
}
