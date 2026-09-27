namespace UnityIsekaiGame.Quests
{
    public enum QuestAvailabilityState
    {
        Unknown,
        Available,
        TemporarilyUnavailable,
        Suspended,
        Exhausted,
        ExclusivelyAssigned,
        NotYetAvailable,
        Retired,
        Historical,
        Invalid
    }

    public enum QuestEligibilityRequirementKind
    {
        Unknown,
        PersonActive,
        Capability,
        Skill,
        Trait,
        ItemPossessed,
        ItemEquipped,
        Profession,
        Qualification,
        Credential,
        Employment,
        OrganizationMembership,
        OrganizationRank,
        Office,
        InstitutionalAuthority,
        FactionAffiliation,
        Reputation,
        Relationship,
        Citizenship,
        Residency,
        LegalStatus,
        Permit,
        Location,
        InteractionPointPresence,
        Knowledge,
        PriorQuestState,
        WorldHistoryFact,
        NarrativeState,
        TimeWindow,
        Custom
    }

    public enum QuestEligibilityGroupPolicy
    {
        Unknown,
        All,
        Any,
        None,
        AtLeast
    }

    public enum QuestRequirementComparison
    {
        Unknown,
        Exists,
        NotExists,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Equal
    }

    public enum QuestAssignmentPolicy
    {
        Unknown,
        Exclusive,
        CapacityLimited,
        Nonexclusive,
        DirectOnly
    }

    public enum QuestConsentPolicy
    {
        Unknown,
        ExplicitRecipientConsentRequired,
        OptionalRecipientConsent,
        DirectInstitutionalAssignmentAllowed
    }

    public enum QuestRefusalPolicy
    {
        Unknown,
        MayReoffer,
        RefusalClosesOffer,
        RefusalClosesQuestForRecipient
    }

    public enum QuestAbandonmentPolicy
    {
        Unknown,
        AllowedReleasesCapacity,
        AllowedKeepsCapacityReserved,
        NotAllowed
    }

    public enum QuestOfferChannel
    {
        Unknown,
        DirectPerson,
        DirectInstitution,
        InteractionPoint,
        QuestBoard,
        GovernmentDesk,
        GuildCounter,
        Letter,
        Record,
        TravelEncounter,
        NarrativeEvent,
        SystemGenerated,
        Custom
    }

    public enum QuestOfferLifecycleState
    {
        Unknown,
        Proposed,
        Active,
        Accepted,
        Refused,
        Withdrawn,
        Expired,
        Superseded,
        Cancelled,
        Historical,
        Invalid
    }

    public enum QuestAssignmentLifecycleState
    {
        Unknown,
        Assigned,
        Active,
        Suspended,
        Resumed,
        Abandoned,
        Withdrawn,
        Historical,
        Invalid
    }

    public enum QuestAssignmentCategory
    {
        Unknown,
        AcceptedOffer,
        DirectInstitutional,
        SystemAssigned,
        ImportedHistorical,
        Custom
    }

    public enum QuestParticipationEventKind
    {
        Unknown,
        AvailabilityEvaluated,
        EligibilityEvaluated,
        OfferCreated,
        OfferAccepted,
        OfferRefused,
        OfferWithdrawn,
        OfferExpired,
        AssignmentCreated,
        AssignmentSuspended,
        AssignmentResumed,
        AssignmentAbandoned,
        AssignmentWithdrawn,
        Restore
    }

    public enum QuestParticipationOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinitionRegistry,
        MissingQuestRuntime,
        MissingQuest,
        MissingDefinition,
        Unavailable,
        Ineligible,
        UnauthorizedProvider,
        ConsentRequired,
        MissingOffer,
        OfferNotActive,
        OfferExpired,
        MissingAssignment,
        InvalidLifecycleTransition,
        DuplicateOffer,
        DuplicateAssignment,
        CapacityExceeded,
        ExclusiveAssignmentExists,
        Refused,
        WithdrawalNotAllowed,
        AbandonmentNotAllowed,
        WrongWorld,
        RevisionConflict,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }
}
