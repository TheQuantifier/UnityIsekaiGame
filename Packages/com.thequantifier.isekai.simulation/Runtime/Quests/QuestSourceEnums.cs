namespace UnityIsekaiGame.Quests
{
    public enum QuestSourceCategory
    {
        Unknown,
        QuestBoard,
        GuildCounter,
        GovernmentDesk,
        Office,
        NPC,
        Organization,
        Business,
        Faction,
        PublicNotice,
        Record,
        Letter,
        TravelEncounter,
        WorldEvent,
        System,
        Custom
    }

    public enum QuestSourceLifecycleState
    {
        Unknown,
        Active,
        Inactive,
        Suspended,
        Closed,
        Retired,
        Historical,
        Invalid
    }

    public enum QuestListingLifecycleState
    {
        Unknown,
        Draft,
        Published,
        Suspended,
        Claimed,
        Unlisted,
        Expired,
        Retired,
        Historical,
        Invalid
    }

    public enum QuestSourceVisibility
    {
        Unknown,
        Public,
        LocallyKnown,
        OrganizationMembers,
        RankRestricted,
        RecipientOnly,
        FactionKnown,
        GovernmentOfficial,
        Restricted,
        Secret,
        Hidden,
        Diagnostic
    }

    public enum QuestSourceDiscoveryPolicy
    {
        Unknown,
        KnownByDefault,
        RequiresNearbyPresence,
        RequiresInteraction,
        RequiresPriorKnowledge,
        PrivilegedOnly
    }

    public enum QuestListingDiscoveryPolicy
    {
        Unknown,
        BrowseRevealsListing,
        InspectRevealsDetails,
        RequiresEligibility,
        RequiresPriorKnowledge,
        NoAutomaticDiscovery
    }

    public enum QuestEligibilityDisplayPolicy
    {
        Unknown,
        OnlyEligible,
        VisibleIneligibleWithPublicReason,
        VisibleIneligibleRedacted,
        RankLockedVisible,
        DiagnosticOnly
    }

    public enum QuestAcceptedListingDisplayPolicy
    {
        Unknown,
        HideWhenAccepted,
        ShowAsTaken,
        KeepVisible,
        SourceSpecific
    }

    public enum QuestRepeatableListingDisplayPolicy
    {
        Unknown,
        KeepListed,
        RelistAfterCompletion,
        HideUntilRelisted,
        SourceSpecific
    }

    public enum QuestListingDuplicatePolicy
    {
        Unknown,
        RejectActiveDuplicate,
        AllowMultipleListings,
        ReplaceOnlyByExplicitUnlist
    }

    public enum QuestListingExpirationPolicy
    {
        Unknown,
        NeverExpires,
        ExpiresAtTime,
        SourceDefaultDuration
    }

    public enum QuestSourceProviderRequirementKind
    {
        Unknown,
        NoProvider,
        Person,
        OrganizationMembership,
        OrganizationRank,
        Office,
        Authority,
        BusinessRole,
        FactionMembership,
        Custom
    }

    public enum QuestSourceRole
    {
        Unknown,
        Discovery,
        Listing,
        Offer,
        Acceptance,
        TurnIn,
        RewardClaim,
        SpecialtyAssignment,
        InformationUnlock,
        Custom
    }

    public enum QuestSourceEventKind
    {
        Unknown,
        SourceCreated,
        SourceLifecycleChanged,
        ListingPublished,
        ListingSuspended,
        ListingUnlisted,
        ListingExpired,
        ListingClaimed,
        ListingRelisted,
        SourceBrowsed,
        ListingInspected,
        DiscoveryRecorded,
        SourceAssociationRecorded,
        Restore
    }

    public enum QuestSourceDiscoveryKind
    {
        Unknown,
        SourceKnown,
        ListingKnown,
        QuestKnown,
        ListingDetailsKnown,
        EligibilityKnown
    }

    public enum QuestSourceOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinitionRegistry,
        MissingDefinition,
        MissingQuestRuntime,
        MissingParticipationRuntime,
        MissingQuest,
        MissingSource,
        MissingListing,
        SourceInactive,
        ListingInactive,
        SourceCapacityExceeded,
        SourceFilterRejected,
        UnauthorizedPublisher,
        UnauthorizedRemoval,
        VisibilityDenied,
        EligibilityDenied,
        OfferRejected,
        AcceptanceRejected,
        AlreadyClaimed,
        AlreadyTerminal,
        RevisionConflict,
        WrongWorld,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }
}
