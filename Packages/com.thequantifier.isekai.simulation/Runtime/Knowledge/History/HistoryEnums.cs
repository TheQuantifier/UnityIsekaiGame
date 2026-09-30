namespace UnityIsekaiGame.Knowledge.History
{
    public enum HistoricalEventCategory
    {
        Unknown,
        Identity,
        BirthOrCreation,
        BodyTransition,
        Location,
        Travel,
        Relationship,
        Affiliation,
        EmploymentOrRole,
        Combat,
        Injury,
        Recovery,
        Disease,
        Diagnosis,
        Treatment,
        Crime,
        Discovery,
        Ownership,
        Reputation,
        QuestRelevant,
        Political,
        Social,
        DeathOrDisappearance,
        CustomWorldEvent
    }

    public enum HistoricalEventPayloadKind
    {
        None,
        Generic,
        BodyTransition,
        Location,
        Organization,
        Condition,
        Discovery,
        Correction
    }

    public enum LifeEventCategory
    {
        None,
        BirthOrCreation,
        Death,
        Disappearance,
        ReturnOrResurrection,
        Identity,
        BodyTransition,
        FamilyOrLineage,
        Discovery,
        Education,
        Training,
        SkillMilestone,
        Role,
        Title,
        SocialStatus,
        Affiliation,
        Employment,
        RelationshipMilestone,
        Combat,
        Injury,
        Recovery,
        Disease,
        Diagnosis,
        Treatment,
        Crime,
        Arrest,
        Trial,
        Conviction,
        Punishment,
        Ownership,
        Property,
        Travel,
        Migration,
        Citizenship,
        Political,
        Religious,
        Economic,
        QuestRelated,
        PublicAchievement,
        PublicFailure,
        PersonalAchievement,
        PersonalFailure,
        Custom
    }

    public enum LifeEventParticipantRole
    {
        Unknown,
        Subject,
        Participant,
        Witness,
        Instigator,
        Victim,
        Beneficiary,
        Authority,
        Teacher,
        Student,
        Employer,
        Employee,
        Attacker,
        Defender,
        Healer,
        Diagnostician,
        Owner,
        Recipient,
        Discoverer,
        Accused,
        ConvictedPerson,
        AppointingAuthority,
        Custom
    }

    public enum LifeEventPayloadKind
    {
        None,
        Generic,
        BirthOrCreation,
        DeathOrDisappearance,
        RoleOrTitleTransition,
        AffiliationTransition,
        CombatParticipation,
        InjuryDiagnosisRecovery,
        Discovery,
        OwnershipTransfer,
        BodyTransition,
        Legal,
        TravelOrMigration,
        Custom
    }

    public enum LifeEventSignificance
    {
        Trivial,
        Routine,
        Notable,
        Major,
        LifeDefining,
        Historic
    }

    public enum LifeEventBiographyRelevance
    {
        NotRelevant,
        Optional,
        NormallyIncluded,
        MajorBiographyEvent,
        IdentityDefining,
        PublicBiographyEvent,
        PrivateBiographyEvent,
        RestrictedBiographyEvent
    }

    public enum LifeEventPublicRecordRelevance
    {
        None,
        PersonalOnly,
        LocallyRecordable,
        OrganizationRecord,
        PublicRecord,
        HistoricalArchive
    }

    public enum LifeEventOutcome
    {
        Unknown,
        Started,
        Completed,
        Succeeded,
        Failed,
        Confirmed,
        Presumed,
        Disputed,
        Corrected,
        Resolved,
        Ongoing
    }

    public enum LifeEventRelationshipType
    {
        Related,
        Cause,
        Consequence,
        Continuation,
        Escalation,
        Resolution,
        Correction,
        Supersession,
        PartOf,
        MilestoneWithin,
        TriggeredBy,
        ResultedIn,
        BeginningPair,
        EndingPair
    }

    public enum LifeEventSequenceStatus
    {
        Unknown,
        Planned,
        Active,
        Completed,
        Interrupted,
        Cancelled,
        Superseded
    }

    public enum HistoricalEventStatus
    {
        Active,
        Superseded,
        Correction
    }

    public enum HistoryMemorySource
    {
        Unknown,
        DirectParticipation,
        DirectObservation,
        WitnessTestimony,
        WrittenRecord,
        Investigation,
        Examination,
        Diagnosis,
        Inference,
        KnowledgeSharing,
        PreviousBody,
        ScriptedSetup,
        DevelopmentFixture
    }

    public enum MemoryState
    {
        Accessible,
        Inaccessible,
        Uncertain,
        Disputed,
        Corrected,
        Forgotten,
        Difficult,
        Suppressed,
        Dormant,
        Altered,
        Recovered
    }

    public enum MemoryRecallOutcome
    {
        FullyRecalled,
        PartiallyRecalled,
        Uncertain,
        Altered,
        Conflicting,
        BlockedBySuppression,
        Inaccessible,
        Forgotten,
        NoMatch,
        CueAssisted,
        Recovered,
        AccessDenied,
        Preview
    }

    public enum MemoryAccessContext
    {
        OrdinaryRecall,
        InternalSystem,
        Persistence,
        Validation,
        Debug
    }

    public enum MemoryCueKind
    {
        Unknown,
        Person,
        Location,
        Body,
        Organization,
        Item,
        Symbol,
        SoundClassification,
        SmellClassification,
        HistoricalEvent,
        Fact,
        KnowledgeDomain,
        Tag,
        Memory,
        AuthoredCue
    }

    public enum MemoryDetailKind
    {
        Unknown,
        Event,
        Participant,
        Time,
        Location,
        Body,
        Organization,
        Item,
        Cause,
        Sequence,
        Quantity,
        Source,
        Note
    }

    public enum MemoryDetailState
    {
        Remembered,
        Unavailable,
        Uncertain,
        Altered,
        Recovered,
        Suppressed
    }

    public enum MemoryAlterationType
    {
        None,
        Correction,
        Reconstruction,
        NaturalDegradation,
        DetailLoss,
        DetailAddition,
        Distortion,
        DeliberateManipulation,
        NewEvidenceRevision,
        SourceAttributionChange,
        IdentityAssociationChange,
        Recovery,
        Suppression,
        SuppressionRemoval,
        Reinforcement
    }

    public enum MemoryReinforcementSource
    {
        Unknown,
        SuccessfulRecall,
        RepeatedObservation,
        RepeatedExamination,
        RepeatedTestimony,
        Reading,
        Study,
        Practice,
        Teaching,
        Demonstration,
        CorroboratingEvidence,
        ContextualCue,
        AuthoredEffect,
        DevelopmentFixture
    }

    public enum HistoryResultCode
    {
        Success,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinition,
        MissingEvent,
        MissingPerson,
        MissingBody,
        MissingMemory,
        InvalidTimeRange,
        InvalidCorrection,
        CircularCorrection,
        PrivateHistoryBlocked,
        KnowledgeRejected,
        RestoreFailed,
        AccessDenied,
        InvalidTransition,
        Suppressed,
        Forgotten,
        NoMatch,
        InvalidSuppression,
        InvalidRevision,
        CircularRevision,
        InvalidLifeEvent,
        InvalidParticipantRole,
        InvalidRelationship,
        InvalidSequence
    }
}
