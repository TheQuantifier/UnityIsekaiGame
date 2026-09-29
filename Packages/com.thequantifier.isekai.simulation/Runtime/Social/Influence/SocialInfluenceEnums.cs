namespace UnityIsekaiGame.Social.Influence
{
    public enum SocialInfluenceCategory
    {
        RationalPersuasion = 0,
        EvidencePresentation = 1,
        EmotionalAppeal = 2,
        RelationshipAppeal = 3,
        DutyAppeal = 4,
        AuthorityAppeal = 5,
        ReputationAppeal = 6,
        Reassurance = 7,
        Inspiration = 8,
        Discouragement = 9,
        NegotiatedRequest = 10,
        Intimidation = 11,
        Deception = 12,
        Omission = 13,
        Misdirection = 14,
        Denial = 15,
        Confession = 16,
        Correction = 17,
        Custom = 18
    }

    public enum SocialInfluenceIntent
    {
        ChangeBelief = 0,
        IncreaseBeliefConfidence = 1,
        DecreaseBeliefConfidence = 2,
        CreateDoubt = 3,
        CorrectBelief = 4,
        GainAgreement = 5,
        GainCompliance = 6,
        GainPromise = 7,
        GainPermission = 8,
        DiscourageAction = 9,
        EncourageAction = 10,
        Reassure = 11,
        Intimidate = 12,
        ConcealTruth = 13,
        AvoidBlame = 14,
        RepairCredibility = 15,
        Custom = 16
    }

    public enum SocialInfluenceSubjectKind
    {
        Claim = 0,
        Person = 1,
        HistoricalEvent = 2,
        RelationshipRecord = 3,
        Rumor = 4,
        Promise = 5,
        InteractionDefinition = 6,
        Item = 7,
        Place = 8,
        Group = 9,
        Audience = 10,
        Decision = 11,
        Custom = 12
    }

    public enum SocialInfluenceStatus
    {
        RuntimeNotReady = 0,
        InvalidRequest = 1,
        MissingMethod = 2,
        MissingPerson = 3,
        MissingClaim = 4,
        UnsupportedIntent = 5,
        UnsupportedSubject = 6,
        DisclosureBlocked = 7,
        CooldownActive = 8,
        Preview = 9,
        Succeeded = 10,
        Duplicate = 11,
        BeliefRejected = 12,
        ComplianceRejected = 13,
        RestoreFailed = 14,
        ValidationFailed = 15,
        Restored = 16
    }

    public enum SocialInfluenceTruthStatus
    {
        Unknown = 0,
        True = 1,
        False = 2,
        Disputed = 3,
        PartiallyAccurate = 4,
        Outdated = 5
    }

    public enum SocialInfluenceSpeakerBeliefState
    {
        Unknown = 0,
        BelievesTrue = 1,
        BelievesFalse = 2,
        Uncertain = 3
    }

    public enum SocialInfluenceHonestyClassification
    {
        Indeterminate = 0,
        HonestTrue = 1,
        HonestError = 2,
        DirectLie = 3,
        MisleadingOmission = 4,
        Misdirection = 5
    }

    public enum SocialInfluenceDeceptionMode
    {
        NoDeception = 0,
        DirectFalseAssertion = 1,
        MisleadingOmission = 2,
        FalseSourceClaim = 3,
        FalseConfidence = 4,
        DenialOfKnownFact = 5,
        FabricatedEvidenceClaim = 6,
        SourceConcealment = 7,
        TechnicallyTrueMisdirection = 8,
        Unknown = 9
    }

    public enum SocialInfluenceBeliefOutcome
    {
        None = 0,
        AlreadyBelieved = 1,
        ConfidenceIncreased = 2,
        ConfidenceDecreased = 3,
        DoubtCreated = 4,
        Accepted = 5,
        Rejected = 6,
        OppositionStrengthened = 7,
        DeferredForEvidence = 8
    }

    public enum SocialInfluenceComplianceOutcome
    {
        None = 0,
        Refused = 1,
        Deferred = 2,
        VerbalAgreement = 3,
        AcceptedRequest = 4,
        PromiseAccepted = 5,
        FearBasedCompliance = 6,
        ImpossibleActionRejected = 7
    }

    public enum SocialInfluenceDetectionOutcome
    {
        NotApplicable = 0,
        NotDetected = 1,
        SuspicionRaised = 2,
        InconsistencyNoticed = 3,
        Detected = 4,
        Proven = 5
    }

    public enum SocialInfluenceMarginClass
    {
        Failure = 0,
        Partial = 1,
        Success = 2,
        Critical = 3
    }

    public enum SocialInfluenceVisibility
    {
        Private = 0,
        TargetOnly = 1,
        Witnessed = 2,
        Public = 3,
        DevelopmentOnly = 4
    }
}
