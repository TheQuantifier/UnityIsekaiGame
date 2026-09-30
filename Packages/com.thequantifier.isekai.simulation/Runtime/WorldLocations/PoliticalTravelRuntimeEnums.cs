namespace UnityIsekaiGame.WorldLocations
{
    public enum TravelLegalComplianceMode
    {
        Unknown = 0,
        RequireLegalTravel = 1,
        PreferLegalTravel = 2,
        AllowIllegalTravel = 3,
        StructuralOnlyDevelopment = 4
    }

    public enum PoliticalTravelOperationCode
    {
        Unknown = 0,
        Succeeded = 1,
        Preview = 2,
        Duplicate = 3,
        InvalidRequest = 4,
        MissingRuntime = 5,
        MissingTraveler = 6,
        MissingLocation = 7,
        MissingTerritory = 8,
        MissingCheckpoint = 9,
        MissingAuthorization = 10,
        LegalBlocked = 11,
        PhysicalBlocked = 12,
        ValidationFailed = 13,
        PersistenceInvalid = 14,
        RestoreFailed = 15,
        Disposed = 16
    }

    public enum PoliticalTravelCrossingClassification
    {
        Unknown = 0,
        InternalMovement = 1,
        TerritoryEntry = 2,
        TerritoryExit = 3,
        BorderCrossing = 4,
        JurisdictionChange = 5,
        ContestedBorderCrossing = 6,
        UnclaimedOrUnknownTerritory = 7
    }

    public enum PoliticalTravelLegalState
    {
        Unknown = 0,
        NotEvaluated = 1,
        AllowedByDefault = 2,
        Authorized = 3,
        Required = 4,
        Exempt = 5,
        Prohibited = 6,
        Conflict = 7,
        MissingAuthorization = 8,
        AccessDenied = 9
    }

    public enum PhysicalLegalTravelState
    {
        Unknown = 0,
        TravelableAndLegal = 1,
        TravelableWithLegalRequirement = 2,
        IllegalButPhysicallyPossible = 3,
        LegallyBlocked = 4,
        PhysicallyBlocked = 5,
        DevelopmentStructuralOnly = 6,
        Unresolved = 7
    }

    public enum BorderCheckpointLifecycleState
    {
        Unknown = 0,
        Planned = 1,
        Active = 2,
        Suspended = 3,
        Closed = 4,
        Historical = 5
    }

    public enum BorderCheckpointPolicy
    {
        Unknown = 0,
        ObserveOnly = 1,
        RequireInspection = 2,
        RequireAuthorization = 3,
        ClosedToOrdinaryTravel = 4
    }

    public enum BorderCheckpointEvaluationState
    {
        Unknown = 0,
        NoCheckpoint = 1,
        PassAllowed = 2,
        InspectionRequired = 3,
        AuthorizationMissing = 4,
        Closed = 5,
        HiddenFromRequester = 6
    }

    public enum PoliticalTravelVisibilityMode
    {
        TravelerSafe = 0,
        PublicOnly = 1,
        Privileged = 2,
        Development = 3
    }

    public enum PoliticalTravelCrossingLifecycleState
    {
        Unknown = 0,
        Planned = 1,
        Authorized = 2,
        Completed = 3,
        IllegalRecorded = 4,
        Rejected = 5,
        Historical = 6
    }
}
