namespace UnityIsekaiGame.WorldLocations
{
    public enum TravelJourneyCategory
    {
        Unknown,
        OrdinaryTravel,
        AdministrativeTransfer,
        CustodyTransfer,
        DeliveryTravelPlaceholder,
        EscortTravelPlaceholder,
        EmergencyTravelPlaceholder,
        ScriptedTravel,
        Custom
    }

    public enum TravelJourneyLifecycleState
    {
        Unknown,
        Planned,
        Ready,
        Active,
        Paused,
        Blocked,
        Replanning,
        Suspended,
        Cancelled,
        Completed,
        Failed,
        Historical,
        Invalid
    }

    public enum TravelJourneyStepLifecycleState
    {
        Unknown,
        Pending,
        Ready,
        Active,
        Completed,
        Blocked,
        SkippedByReplan,
        Cancelled,
        Historical,
        Invalid
    }

    public enum TravelJourneyProgressionMode
    {
        Unknown,
        AutomaticLogical,
        ExternalStepControl
    }

    public enum TravelJourneyVisibility
    {
        Public,
        Restricted,
        Secret,
        Hidden,
        Diagnostic
    }

    public enum TravelJourneyMutationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingRuntime,
        MissingTraveler,
        MissingPlacement,
        MissingLocation,
        MissingRoute,
        RouteInvalid,
        RouteStale,
        ConflictingActiveJourney,
        InvalidLifecycle,
        InvalidStep,
        Blocked,
        RevisionConflict,
        PersistenceInvalid,
        RestoreFailed,
        Disposed
    }

    public enum TravelJourneyBlockReason
    {
        None,
        MissingRuntime,
        MissingTraveler,
        MissingPlacement,
        RouteStale,
        RouteAccessDenied,
        EdgeUnavailable,
        CapabilityUnavailable,
        MovementRateInvalid,
        NoReplacementRoute,
        ExternalControlRequired,
        TravelConditionBlocked,
        EncounterInterrupted,
        HazardTriggered,
        PersistenceInvalid,
        Unknown
    }
}
