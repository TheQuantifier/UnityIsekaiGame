using System;

namespace UnityIsekaiGame.Social.Attitudes
{
    public enum AttitudeDimensionCategory
    {
        Regard = 0,
        Attachment = 1,
        Threat = 2,
        Commitment = 3,
        Conflict = 4,
        Custom = 5
    }

    public enum AttitudeSemanticDirection
    {
        HigherMeansMoreOfDimension = 0,
        HigherMeansLessOfDimension = 1,
        Custom = 2
    }

    public enum AttitudeValuePrecision
    {
        Integer = 0
    }

    public enum AttitudeMutationKind
    {
        EnsureRecord = 0,
        SetBaseline = 1,
        AdjustBaseline = 2,
        ClearBaseline = 3,
        AddOrReplaceContribution = 4,
        RemoveContribution = 5
    }

    public enum AttitudeContributionSourceCategory
    {
        Unknown = 0,
        Relationship = 1,
        HistoricalEvent = 2,
        Dialogue = 3,
        Quest = 4,
        Scripted = 5,
        TestLab = 6
    }

    public enum AttitudeOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        MissingDefinitionRegistry = 3,
        MissingDimensionDefinition = 4,
        MissingTransactionId = 5,
        MissingRecordId = 6,
        DuplicateRecordId = 7,
        DuplicateOrderedPair = 8,
        MissingObserver = 9,
        MissingSubject = 10,
        UnknownObserver = 11,
        UnknownSubject = 12,
        SelfAttitudeNotAllowed = 13,
        InvalidRequest = 14,
        InvalidValue = 15,
        InvalidSource = 16,
        UnknownSource = 17,
        RestoreFailed = 18,
        ValidationFailed = 19,
        RuntimeNotReady = 20
    }

    public enum AttitudeThresholdComparison
    {
        Equal = 0,
        NotEqual = 1,
        LessThan = 2,
        LessThanOrEqual = 3,
        GreaterThan = 4,
        GreaterThanOrEqual = 5,
        WithinInclusiveRange = 6,
        OutsideInclusiveRange = 7
    }
}
