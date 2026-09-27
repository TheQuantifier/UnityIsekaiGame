namespace UnityIsekaiGame.WorldLocations
{
    public enum LocationCategory
    {
        Unknown,
        World,
        Region,
        Settlement,
        District,
        Building,
        Room,
        FunctionalArea,
        Wilderness,
        Dungeon,
        RouteAnchor,
        InteractionPoint,
        Custom
    }

    public enum LocationLifecycleState
    {
        Unknown,
        Proposed,
        Active,
        Inactive,
        Closed,
        Destroyed,
        Historical,
        Removed
    }

    public enum LocationVisibility
    {
        Public,
        Restricted,
        Secret,
        Hidden
    }

    public enum LocationNameCategory
    {
        Official,
        Common,
        Alias,
        Historical,
        LocalLanguage,
        Internal,
        Secret
    }

    public enum LocationAssociationKind
    {
        Unknown,
        Place,
        Property,
        Organization,
        Government,
        Territory,
        SceneBinding,
        PrototypeMarker,
        Provenance,
        Custom
    }

    public enum LocationOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinition,
        MissingLocation,
        DuplicateLocationId,
        WrongWorld,
        InvalidName,
        InvalidLifecycleTransition,
        InvalidReference,
        InvalidHierarchy,
        CycleDetected,
        DepthLimitExceeded,
        ActiveParentConflict,
        MissingContainment,
        MissingSpatialRelationship,
        UnsupportedByDefinition,
        RevisionConflict,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }

    public enum LocationReferenceResolutionStatus
    {
        Resolved,
        MissingLocation,
        WrongWorld,
        Destroyed,
        InvalidRequest
    }

    public enum LocationContainmentKind
    {
        Unknown,
        Primary,
        Administrative,
        Structural,
        Interior,
        Site,
        Dungeon,
        Historical,
        Custom
    }

    public enum LocationLinkState
    {
        Unknown,
        Active,
        Ended,
        Historical,
        Invalid
    }

    public enum LocationSpatialRelationshipKind
    {
        Unknown,
        Adjacent,
        Near,
        Overlaps,
        Above,
        Below,
        NorthOf,
        SouthOf,
        EastOf,
        WestOf,
        Facing,
        AcrossFrom,
        PartOfComplex,
        SharesBoundary,
        Custom
    }

    public enum LocationSpatialDirectionality
    {
        Unknown,
        Directional,
        Symmetric
    }
}
