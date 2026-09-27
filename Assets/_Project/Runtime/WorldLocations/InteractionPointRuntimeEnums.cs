namespace UnityIsekaiGame.WorldLocations
{
    public enum InteractionPointCategory
    {
        Unknown,
        ServiceCounter,
        AdministrationDesk,
        GuildCounter,
        GovernmentDesk,
        RecordsDesk,
        QuestBoard,
        MerchantCounter,
        SalesCounter,
        StorageAccess,
        Workstation,
        CraftingStation,
        MeetingPoint,
        Seat,
        Bed,
        DetentionPoint,
        PrisonCellPoint,
        CourtDesk,
        RegistrationPoint,
        InformationPoint,
        InventoryDisplayPoint,
        ContainerAccess,
        JobStation,
        Custom
    }

    public enum InteractionServiceCategory
    {
        Unknown,
        Information,
        Registration,
        MembershipAdministration,
        RankAdministration,
        OfficeAdministration,
        QuestAccess,
        QuestSubmission,
        GovernmentService,
        RecordAccess,
        MerchantService,
        Purchase,
        Sale,
        PermitAdministration,
        StorageAccess,
        Crafting,
        EmploymentService,
        CourtService,
        DetentionService,
        MeetingService,
        Rest,
        Custom
    }

    public enum InteractionPointLifecycleState
    {
        Unknown,
        Proposed,
        Active,
        Inactive,
        Disabled,
        Closed,
        Broken,
        Destroyed,
        Historical,
        Invalid
    }

    public enum InteractionPointVisibility
    {
        Public,
        LocallyKnown,
        OrganizationKnown,
        MemberKnown,
        StaffKnown,
        GovernmentKnown,
        Restricted,
        Secret,
        Hidden,
        Diagnostic
    }

    public enum InteractionPointOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinition,
        MissingService,
        MissingPoint,
        MissingHostLocation,
        WrongWorld,
        InvalidHostLocation,
        InvalidLifecycleTransition,
        InvalidServiceBinding,
        InvalidSubjectLink,
        InvalidProvider,
        ProviderAbsent,
        ConsumerAbsent,
        PresenceFailed,
        CapacityFull,
        ReservationConflict,
        SessionConflict,
        RevisionConflict,
        DestinationRuntimeUnavailable,
        MissingAuthorization,
        LegalRestriction,
        RequirementFailed,
        VisibilityDenied,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }

    public enum InteractionPointUseState
    {
        Unknown,
        Free,
        InUse,
        Reserved,
        Blocked,
        Disabled,
        Unavailable,
        Full,
        Custom
    }

    public enum InteractionUseSessionLifecycle
    {
        Unknown,
        Proposed,
        Active,
        Suspended,
        Completed,
        Cancelled,
        Expired,
        Historical,
        Invalid
    }

    public enum InteractionReservationLifecycle
    {
        Unknown,
        Proposed,
        Active,
        Consumed,
        Released,
        Expired,
        Cancelled,
        Historical
    }

    public enum InteractionSubjectLinkRole
    {
        Unknown,
        RepresentedOrganization,
        RepresentedGovernment,
        RepresentedOffice,
        RepresentedBusiness,
        AssociatedProperty,
        AssociatedInventory,
        AssociatedTreasury,
        AssociatedCourt,
        AssociatedCustodyLocation,
        AssociatedRecordsCollection,
        AssociatedQuestSource,
        ServiceProviderOrganization,
        Custom
    }

    public enum InteractionProviderRequirementKind
    {
        Unknown,
        NoProvider,
        AnyAuthorizedMember,
        SpecificOfficeholder,
        SpecificRank,
        EmployeeWithPosition,
        BusinessOwner,
        AssignedClerk,
        AssignedPerson,
        AutomatedService,
        Custom
    }

    public enum InteractionPhysicalPresencePolicy
    {
        Unknown,
        NotRequired,
        SameExactLocation,
        WithinHostLocation,
        WithinImmediateParent,
        ProviderAndConsumerSameLocation,
        RemoteAllowed,
        Custom
    }

    public enum InteractionDestinationRuntime
    {
        Unknown,
        InteractionPoint,
        OrganizationMembership,
        OrganizationAuthority,
        Legal,
        KnowledgeRecords,
        ItemInventory,
        BusinessTrade,
        Justice,
        Quest,
        Crafting,
        Profession,
        Social,
        Custom
    }

    public enum InteractionSceneBindingCategory
    {
        None,
        PrototypeMarker,
        ServiceCounterMarker,
        DeskMarker,
        BoardMarker,
        StorageMarker,
        WorkstationMarker,
        SeatMarker,
        BedMarker,
        Custom
    }
}
