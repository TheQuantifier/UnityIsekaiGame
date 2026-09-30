namespace UnityIsekaiGame.WorldLocations
{
    public enum LocationOccupantEntityType
    {
        Unknown,
        Person,
        Body,
        ItemInstance,
        WorldEntity,
        Container,
        Actor,
        Custom
    }

    public enum EntityPlacementCategory
    {
        Unknown,
        Present,
        ResidentPlaceholder,
        WorkingPlaceholder,
        Visiting,
        Detained,
        Stored,
        Dropped,
        DeployedPlaceholder,
        Spawned,
        TemporarilyPlaced,
        CorpsePresent,
        Custom
    }

    public enum EntityPlacementLifecycleState
    {
        Unknown,
        Proposed,
        Active,
        Ended,
        Superseded,
        Historical,
        Invalid
    }

    public enum EntityLocationOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingEntity,
        MissingBody,
        MissingLocation,
        WrongWorld,
        InactiveLocation,
        ConflictingActivePlacement,
        MissingPlacement,
        CapacityFull,
        OccupantTypeNotAllowed,
        InventoryConflict,
        RevisionConflict,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }

    public enum EntityPhysicalLocationResolutionStatus
    {
        ResolvedExact,
        ResolvedThroughBody,
        Unplaced,
        MissingEntity,
        MissingBody,
        BodyUnplaced,
        WrongWorld,
        InvalidRequest,
        CycleDetected,
        DepthLimitExceeded
    }
}
