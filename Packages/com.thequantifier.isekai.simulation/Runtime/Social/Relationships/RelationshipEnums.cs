namespace UnityIsekaiGame.Social.Relationships
{
    public enum RelationshipCategory
    {
        Social = 0,
        Family = 1,
        Personal = 2,
        Professional = 3,
        Alliance = 4,
        Conflict = 5,
        Custom = 100
    }

    public enum RelationshipDirectionality
    {
        Symmetric = 0,
        Directed = 1,
        ReciprocalRoleDistinct = 2
    }

    public enum RelationshipLifecycleStatus
    {
        Active = 0,
        Ended = 1
    }

    public enum RelationshipDuplicatePolicy
    {
        AllowMultipleActive = 0,
        OneActiveBetweenParticipants = 1,
        OneActivePerDefinitionAndRoles = 2
    }

    public enum RelationshipOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        InvalidRequest = 10,
        MissingDefinitionRegistry = 11,
        MissingDefinition = 12,
        DuplicateRecordId = 13,
        DuplicateActiveRelationship = 14,
        MissingParticipant = 15,
        UnknownPerson = 16,
        SelfRelationshipNotAllowed = 17,
        InvalidRole = 18,
        InvalidLifecycle = 19,
        InvalidTimeRange = 20,
        MissingRelationship = 21,
        CannotEndRelationship = 22,
        RestoreFailed = 30,
        ValidationFailed = 31
    }
}
