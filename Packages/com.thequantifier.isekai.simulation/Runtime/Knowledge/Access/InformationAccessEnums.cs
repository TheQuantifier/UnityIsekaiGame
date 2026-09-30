namespace UnityIsekaiGame.Knowledge.Access
{
    public enum InformationVisibilityClassification
    {
        Unknown,
        Public,
        Open,
        Personal,
        Private,
        Confidential,
        Restricted,
        OrganizationRestricted,
        RoleRestricted,
        ProfessionRestricted,
        Medical,
        Legal,
        Classified,
        Secret,
        HighlySecret,
        Hidden,
        Sealed,
        SourceProtected,
        RecipientOnly,
        OwnerOnly,
        ParticipantOnly,
        WitnessOnly,
        NeedToKnow,
        Custom
    }

    public enum InformationSubjectType
    {
        Unknown,
        FactDefinition,
        FactInstance,
        Proposition,
        Claim,
        Evidence,
        Belief,
        KnowledgeRecord,
        Memory,
        MemoryDetail,
        HistoricalEvent,
        LifeEvent,
        EventParticipant,
        Source,
        SourceIdentity,
        SourceChain,
        Transfer,
        TransferContent,
        Document,
        PersonIdentity,
        BodyIdentity,
        PreviousBodyContinuity,
        Location,
        Organization,
        Title,
        Role,
        Affiliation,
        Condition,
        Disease,
        Diagnosis,
        Treatment,
        Crime,
        LegalRecord,
        Ownership,
        Custom
    }

    public enum InformationAccessMode
    {
        Inspect,
        Query,
        Recall,
        Observe,
        Examine,
        Diagnose,
        Read,
        Receive,
        Share,
        Reshare,
        Teach,
        RevealSource,
        RevealProvenance,
        RevealDetails,
        Correct,
        Retract,
        Validate,
        Persist,
        DebugInspect,
        AuthorWorldSetup,
        Custom
    }

    public enum InformationAccessPurpose
    {
        Gameplay,
        PublicQuery,
        PersonalRecall,
        Observation,
        Medical,
        Legal,
        Organization,
        Transfer,
        Journal,
        Codex,
        InternalSimulation,
        Persistence,
        Validation,
        Debug,
        AuthoredSetup,
        Custom
    }

    public enum InformationAccessDecisionKind
    {
        FullAccess,
        RedactedAccess,
        PartialAccess,
        ConditionalAccess,
        Denied,
        Unknown,
        Expired,
        Revoked,
        NotDiscovered,
        MissingAuthorization
    }

    public enum InformationAccessDenialCode
    {
        None,
        MissingSubject,
        MissingRequester,
        MissingPolicy,
        Revoked,
        Expired,
        NotYetEffective,
        ClassificationRestriction,
        OrganizationRestriction,
        RoleRestriction,
        OwnerRestriction,
        ParticipantRestriction,
        WitnessRestriction,
        RecipientRestriction,
        SourceProtectionRestriction,
        DetailRestriction,
        DisclosureRestriction,
        ResharingRestriction,
        PurposeRestriction,
        NeedToKnowRestriction,
        MissingAuthorization,
        ExplicitDenial,
        Concealed,
        NotDiscovered,
        InvalidRequest
    }

    public enum InformationDisclosurePolicy
    {
        None,
        SameAsAccess,
        FreelyDisclose,
        RedactedOnly,
        SummaryOnly,
        NamedRecipientsOnly,
        OrganizationOnly,
        RoleOnly,
        ApprovalRequired,
        Never,
        Custom
    }

    public enum InformationResharingPolicy
    {
        None,
        FreelyReshareable,
        WithAttribution,
        WithoutSourceIdentity,
        NamedRecipientsOnly,
        OrganizationOnly,
        RoleOnly,
        NeedToKnowOnly,
        ApprovalRequired,
        OneTimeDisclosure,
        NoResharing,
        RedactedOnly,
        SummaryOnly,
        TimeDelayed,
        ExpiringPermission,
        Custom
    }

    public enum InformationSourceVisibilityPolicy
    {
        Reveal,
        HideImmediate,
        HideOriginal,
        HideFullProvenance,
        CategoryOnly,
        Pseudonymous,
        VerifiedButAnonymous,
        PrivilegedOnly
    }

    public enum InformationDetailVisibilityPolicy
    {
        All,
        Selected,
        Redacted,
        ExistenceOnly,
        ClassificationOnly,
        None
    }

    public enum InformationAuditPolicy
    {
        None,
        AuditDenied,
        AuditGranted,
        AuditDeniedAndGranted,
        AuditUnauthorizedOnly
    }

    public enum InformationContextKind
    {
        Gameplay,
        Public,
        PersonContextual,
        InternalSimulation,
        Persistence,
        Validation,
        Debug,
        AuthoredSetup
    }

    public enum InformationGranteeKind
    {
        Person,
        Organization,
        Role,
        Title,
        Status,
        Token,
        Public,
        Custom
    }

    public enum InformationConcealmentKind
    {
        Existence,
        RecordLocation,
        SourceIdentity,
        PersonIdentity,
        BodyIdentity,
        EventParticipant,
        Evidence,
        Cause,
        Ownership,
        Affiliation,
        PreviousBodyContinuity,
        Communication,
        AccessGrant,
        Classification,
        Custom
    }

    public enum SecretExistenceAwareness
    {
        None,
        SuspectsHiddenInformation,
        KnowsRecordExists,
        KnowsConcealment,
        KnowsClassificationOnly,
        KnowsPartialSecret,
        KnowsFullSecret,
        FalseBeliefSecretExists
    }

    public enum InformationRedactionState
    {
        Visible,
        Redacted,
        Hidden,
        Unknown,
        Inaccessible
    }

    public enum InformationAccessResultCode
    {
        Success,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingPolicy,
        MissingRecord,
        AccessDenied,
        RestoreFailed
    }
}
