namespace UnityIsekaiGame.WorldLocations
{
    public enum RouteSegmentCategory
    {
        Unknown,
        Road,
        Street,
        Path,
        Trail,
        Corridor,
        Bridge,
        Tunnel,
        StairRoute,
        WildernessRoute,
        DungeonRoute,
        RegionalRoad,
        TradeRoad,
        MountainPass,
        RiverCrossingPlaceholder,
        FerryPlaceholder,
        PortalRoutePlaceholder,
        Custom
    }

    public enum RouteNetworkCategory
    {
        Unknown,
        StreetNetwork,
        RoadNetwork,
        TrailNetwork,
        DungeonNetwork,
        BuildingNetwork,
        TradeRouteNetwork,
        Custom
    }

    public enum TravelModeCategory
    {
        Unknown,
        Walking,
        RunningPlaceholder,
        MountedPlaceholder,
        CartPlaceholder,
        VehiclePlaceholder,
        SwimmingPlaceholder,
        ClimbingPlaceholder,
        FlyingPlaceholder,
        TeleportPlaceholder,
        Custom
    }

    public enum RouteSegmentLifecycleState
    {
        Unknown,
        Proposed,
        Active,
        Inactive,
        Closed,
        Blocked,
        Destroyed,
        Historical,
        Invalid
    }

    public enum RouteSegmentBlockageState
    {
        Unknown,
        Clear,
        TemporarilyBlocked,
        PermanentlyBlocked,
        Collapsed,
        ObstructedPlaceholder
    }

    public enum RouteVisibility
    {
        Public,
        LocallyKnown,
        OrganizationKnown,
        GovernmentKnown,
        Restricted,
        Secret,
        Hidden,
        Diagnostic
    }

    public enum RouteEdgeKind
    {
        Unknown,
        LocalConnection,
        RouteSegment,
        TransitPlaceholder,
        PortalPlaceholder
    }

    public enum RoutePlanningObjective
    {
        Unknown,
        ShortestDistance,
        LowestCost,
        FewestEdges,
        AnyValidRoute
    }

    public enum RouteAccessEvaluationMode
    {
        Unknown,
        StructuralOnly,
        RequireCurrentAccess,
        PermitUnlockableConnections,
        IgnoreTravelerAccessDevelopment,
        KnowledgeSafeCurrentAccess
    }

    public enum RouteKnowledgeMode
    {
        Unknown,
        AuthoritativeDevelopment,
        PublicKnownOnly,
        KnownToTraveler
    }

    public enum RoutePlanningStatus
    {
        Succeeded,
        SelfRoute,
        Preview,
        InvalidRequest,
        MissingRuntime,
        MissingDefinition,
        MissingOrigin,
        MissingDestination,
        ModeUnsupported,
        NoRoute,
        SearchBudgetExceeded,
        AccessPrevented,
        UnknownUnderKnowledgeView,
        StalePlan,
        InvalidPlan,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }

    public enum RouteMutationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinition,
        MissingLocation,
        MissingSegment,
        MissingNetwork,
        WrongWorld,
        InvalidDirection,
        InvalidLifecycleTransition,
        InvalidDistance,
        InvalidCost,
        InvalidTravelMode,
        InvalidAccessPolicy,
        RevisionConflict,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }

    public enum RoutePlanRevalidationStatus
    {
        Valid,
        RequiresReplanning,
        InvalidEdge,
        ChangedCost,
        ChangedAccess,
        StaleGraphRevision,
        InvalidPlan
    }

    public enum RouteRequirementKind
    {
        Unknown,
        OpenAction,
        UnlockAction,
        Key,
        Permit,
        Membership,
        Rank,
        Office,
        Authority,
        CustodyRole,
        HiddenRouteKnowledge,
        TravelModeCapability,
        Custom
    }
}
