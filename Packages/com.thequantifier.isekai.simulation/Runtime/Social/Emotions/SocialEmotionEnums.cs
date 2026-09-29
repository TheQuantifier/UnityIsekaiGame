namespace UnityIsekaiGame.Social.Emotions
{
    public enum SocialEmotionCategory
    {
        Joy = 0,
        Sadness = 1,
        Anger = 2,
        Fear = 3,
        Relief = 4,
        Gratitude = 5,
        Guilt = 6,
        Shame = 7,
        Pride = 8,
        Anxiety = 9,
        Disgust = 10,
        Envy = 11,
        Resentment = 12,
        Hope = 13,
        Disappointment = 14,
        Custom = 15
    }

    public enum SocialEmotionValence
    {
        Negative = -1,
        Neutral = 0,
        Positive = 1
    }

    public enum SocialEmotionArousal
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public enum SocialEmotionDecayPolicy
    {
        None = 0,
        Linear = 1,
        StepAtExpiration = 2
    }

    public enum SocialEmotionStackingPolicy
    {
        ReplaceWeaker = 0,
        ReinforceExisting = 1,
        KeepSeparate = 2
    }

    public enum SocialEmotionTargetPolicy
    {
        Self = 0,
        OtherPerson = 1,
        PersonOrSubject = 2,
        SubjectOnly = 3,
        None = 4
    }

    public enum SocialEmotionCauseCategory
    {
        Interaction = 0,
        BeliefAccepted = 1,
        BeliefRejected = 2,
        DeceptionDetected = 3,
        RumorHeard = 4,
        NormViolation = 5,
        ReputationChange = 6,
        RelationshipChange = 7,
        MemoryRecall = 8,
        Threat = 9,
        Loss = 10,
        Achievement = 11,
        Custom = 12
    }

    public enum SocialEmotionResponsibility
    {
        Unknown = 0,
        Self = 1,
        Target = 2,
        ThirdParty = 3,
        Circumstance = 4
    }

    public enum SocialEmotionVisibility
    {
        Internal = 0,
        Observable = 1,
        Public = 2,
        DevelopmentOnly = 3
    }

    public enum SocialEmotionProjectionAccess
    {
        Full = 0,
        Redacted = 1,
        Concealed = 2,
        Denied = 3
    }

    public enum SocialEmotionStatus
    {
        RuntimeNotReady = 0,
        InvalidRequest = 1,
        MissingEmotionDefinition = 2,
        MissingAppraisalRule = 3,
        MissingMoodDefinition = 4,
        MissingPerson = 5,
        Preview = 6,
        Succeeded = 7,
        Duplicate = 8,
        Suppressed = 9,
        RestoreFailed = 10,
        ValidationFailed = 11,
        Restored = 12
    }

    public enum SocialMoodDimensionCategory
    {
        Valence = 0,
        Arousal = 1,
        Anxiety = 2,
        SocialOpenness = 3,
        Aggression = 4,
        Morale = 5,
        Custom = 6
    }
}
