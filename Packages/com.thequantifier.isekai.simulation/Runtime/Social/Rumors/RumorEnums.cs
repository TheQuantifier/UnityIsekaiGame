namespace UnityIsekaiGame.Social.Rumors
{
    public enum RumorCategory
    {
        Unknown,
        PersonalConduct,
        Relationship,
        CrimeOrAccusation,
        Achievement,
        Political,
        Economic,
        Supernatural,
        Danger,
        Secret,
        PublicNews,
        Reputation,
        Custom
    }

    public enum RumorOriginCategory
    {
        Unknown,
        FirsthandObservation,
        HistoricalEvent,
        ExistingEvidence,
        DeliberateFabrication,
        Misunderstanding,
        ScriptedNarrative,
        PublicAnnouncement,
        ExternalWorldSetup
    }

    public enum RumorAuthenticity
    {
        Unknown,
        Unverified,
        Verified,
        Contradicted,
        Disputed,
        Fabricated,
        PartiallyAccurate,
        Outdated
    }

    public enum RumorDisclosure
    {
        Public,
        Shareable,
        Private,
        Confidential,
        Secret,
        Restricted,
        Hidden,
        Diagnostic
    }

    public enum RumorDistortionPolicy
    {
        None,
        DeterministicMetadataOnly,
        ForcedConfidenceDecrease,
        ForcedConfidenceIncrease,
        ForcedAnonymousSource
    }

    public enum RumorDistortionOperation
    {
        None,
        ConfidenceIncreased,
        ConfidenceDecreased,
        SourceConcealed,
        UncertaintyAdded,
        UncertaintyRemoved
    }

    public enum RumorLifecycleState
    {
        Active,
        Corrected,
        Retracted,
        Archived,
        Invalid
    }

    public enum RumorTransmissionOutcome
    {
        NotDelivered,
        Heard,
        Ignored,
        Remembered,
        Believed,
        PartiallyBelieved,
        Uncertain,
        Rejected,
        AlreadyKnown,
        ContradictedByExistingBelief,
        BlockedByDisclosure,
        Invalid
    }

    public enum RumorCommunicationChannelCategory
    {
        Unknown,
        Conversation,
        PublicSpeech,
        WrittenMessage,
        GuildNotice,
        TavernGossip,
        DevelopmentFixture
    }

    public enum RumorOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinition,
        MissingRumor,
        MissingPerson,
        DuplicateRumor,
        DuplicateTransmission,
        SpeakerUnaware,
        DisclosureBlocked,
        DistortionRejected,
        KnowledgeRejected,
        MemoryRejected,
        RestoreFailed,
        ValidationFailed,
        Disposed
    }
}
