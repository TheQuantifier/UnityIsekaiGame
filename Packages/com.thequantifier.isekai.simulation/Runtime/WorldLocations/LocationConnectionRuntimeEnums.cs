namespace UnityIsekaiGame.WorldLocations
{
    public enum LocationConnectionCategory
    {
        Unknown,
        OpenPassage,
        Doorway,
        Door,
        LockedDoor,
        Gate,
        CellDoor,
        Stair,
        Ladder,
        CorridorLink,
        Archway,
        BuildingEntrance,
        DungeonEntrance,
        HiddenPassage,
        SecretDoor,
        Hatch,
        BridgeConnection,
        PortalPlaceholder,
        OneWayDropPlaceholder,
        Custom
    }

    public enum LocationConnectionDirectionality
    {
        Unknown,
        Bidirectional,
        SourceToDestinationOnly,
        DestinationToSourceOnly,
        Custom
    }

    public enum LocationConnectionLifecycleState
    {
        Unknown,
        Proposed,
        Active,
        Inactive,
        Disabled,
        Blocked,
        Destroyed,
        Historical,
        Invalid
    }

    public enum LocationConnectionOpenState
    {
        Unknown,
        NotApplicable,
        Open,
        Closed
    }

    public enum LocationConnectionLockState
    {
        Unknown,
        NotLockable,
        Unlocked,
        Locked,
        JammedPlaceholder,
        BrokenLockPlaceholder
    }

    public enum LocationConnectionBlockageState
    {
        Unknown,
        Clear,
        TemporarilyBlocked,
        PermanentlyBlocked,
        Collapsed,
        BarricadedPlaceholder,
        ObstructedPlaceholder
    }

    public enum LocationConnectionVisibility
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

    public enum LocationConnectionEndpointRole
    {
        Unknown,
        Source,
        Destination,
        ExteriorSide,
        InteriorSide,
        PublicSide,
        RestrictedSide,
        Origin,
        Custom
    }

    public enum LocationConnectionOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinition,
        MissingConnection,
        MissingEndpoint,
        WrongWorld,
        InvalidEndpoint,
        InvalidEndpointCategory,
        InvalidDirection,
        InvalidLifecycleTransition,
        InvalidOpenState,
        InvalidLockState,
        InvalidBlockageState,
        InvalidPolicy,
        MissingPolicy,
        MissingActor,
        MissingPlacement,
        WrongOrigin,
        DeniedByPolicy,
        DeniedByLaw,
        DeniedByDirection,
        DeniedByLifecycle,
        DeniedByOpenState,
        DeniedByLock,
        DeniedByBlockage,
        MissingKey,
        MissingPermit,
        MissingAuthority,
        MissingMembership,
        CustodyRestricted,
        DestinationUnavailable,
        RevisionConflict,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }

    public enum LocationAccessPolicyCategory
    {
        Unknown,
        Public,
        PrivateOwnerOnly,
        OrganizationMembers,
        MinimumRank,
        SpecificOffice,
        AuthorizedStaff,
        GovernmentOfficial,
        CustodyAuthorized,
        LegalPermitRequired,
        WarrantRequired,
        KeyRequired,
        CredentialRequired,
        ExplicitWhitelist,
        ExplicitBlacklistPlaceholder,
        Conditional,
        Custom
    }

    public enum LocationConnectionAccessState
    {
        Unknown,
        Allowed,
        AllowedIfOpened,
        AllowedIfUnlocked,
        AllowedIfOpenedAndUnlocked,
        DeniedByPolicy,
        DeniedByLaw,
        DeniedByDirection,
        DeniedByLifecycle,
        DeniedByBlockage,
        MissingKey,
        MissingPermit,
        MissingAuthority,
        MissingMembership,
        CustodyRestricted,
        Invalid
    }

    public enum LocationAccessGrantLifecycleState
    {
        Unknown,
        Active,
        Revoked,
        Expired,
        Historical,
        Invalid
    }

    public enum LocationConnectionSceneBindingCategory
    {
        None,
        PrototypeMarker,
        DoorMarker,
        GateMarker,
        PassageMarker,
        StairMarker,
        DungeonEntranceMarker,
        Custom
    }
}
