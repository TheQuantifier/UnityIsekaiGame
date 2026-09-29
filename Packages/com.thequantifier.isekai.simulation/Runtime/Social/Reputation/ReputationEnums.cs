namespace UnityIsekaiGame.Social.Reputation
{
    public enum ReputationAudienceCategory
    {
        GlobalPublic = 0,
        PlacePopulation = 1,
        Jurisdiction = 2,
        Faction = 3,
        Organization = 4,
        Government = 5,
        Profession = 6,
        Culture = 7,
        SocialClass = 8,
        CustomGroup = 9
    }

    public enum ReputationAudienceScope
    {
        Global = 0,
        Contextual = 1
    }

    public enum ReputationDimensionCategory
    {
        Recognition = 0,
        Regard = 1,
        Infamy = 2,
        Credibility = 3,
        Threat = 4,
        Honor = 5,
        Custom = 6
    }

    public enum ReputationLifecycleState
    {
        Active = 0,
        Historical = 1,
        Suppressed = 2,
        Archived = 3
    }

    public enum ReputationMutationKind
    {
        EnsureRecord = 0,
        SetBaseline = 1,
        AdjustBaseline = 2,
        ClearBaseline = 3,
        AddOrReplaceContribution = 4,
        RemoveContribution = 5,
        ArchiveRecord = 6
    }

    public enum ReputationContributionSourceCategory
    {
        Unknown = 0,
        HistoricalEvent = 1,
        WitnessedDeed = 2,
        Accusation = 3,
        Conviction = 4,
        Achievement = 5,
        Title = 6,
        PublicSpeech = 7,
        ProfessionWork = 8,
        Rumor = 9,
        Propaganda = 10,
        RelationshipOutcome = 11,
        Quest = 12,
        Scripted = 13,
        TestLab = 14
    }

    public enum ReputationAuthenticity
    {
        Unknown = 0,
        Verified = 1,
        Alleged = 2,
        Disputed = 3,
        Fabricated = 4,
        Propaganda = 5,
        Outdated = 6
    }

    public enum ReputationOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        MissingDefinitionRegistry = 3,
        MissingAudienceDefinition = 4,
        MissingDimensionDefinition = 5,
        MissingTransactionId = 6,
        MissingRecordId = 7,
        DuplicateRecordId = 8,
        DuplicateSubjectAudience = 9,
        MissingSubject = 10,
        UnknownSubject = 11,
        MissingAudience = 12,
        InvalidRequest = 13,
        InvalidSource = 14,
        UnknownSource = 15,
        RestoreFailed = 16,
        ValidationFailed = 17,
        RuntimeNotReady = 18,
        HierarchyCycle = 19
    }

    public enum ReputationThresholdComparison
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
