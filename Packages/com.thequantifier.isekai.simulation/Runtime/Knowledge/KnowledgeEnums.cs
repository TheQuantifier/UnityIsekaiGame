using System;

namespace UnityIsekaiGame.Knowledge
{
    public enum KnowledgeDomain
    {
        Unknown,
        Personal,
        Social,
        Species,
        Anatomy,
        Medical,
        Biological,
        Hazard,
        Compatibility,
        Transformation,
        Historical,
        Geographic,
        Political,
        Faction,
        Economic,
        Magical,
        Religious,
        Crafting,
        Professional,
        Narrative
    }

    public enum KnowledgePropositionType
    {
        Unknown,
        Identity,
        Capability,
        Immunity,
        Condition,
        Injury,
        Anatomy,
        Hazard,
        Compatibility,
        Transformation,
        Location,
        Relationship,
        Event,
        Symptom,
        History
    }

    public enum KnowledgeSubjectType
    {
        Unknown,
        Person,
        Actor,
        Body,
        Species,
        AnatomyNode,
        Item,
        Place,
        Faction,
        Event,
        Interaction,
        Organization
    }

    public enum KnowledgeValueType
    {
        None,
        Boolean,
        StableId,
        Qualitative,
        Numeric,
        Text
    }

    public enum KnowledgeVisibility
    {
        Public,
        PersonallyObservable,
        Private,
        Confidential,
        Hidden,
        Secret,
        DiagnosticOnly,
        DevelopmentOnly
    }

    public enum KnowledgeStalenessPolicy
    {
        NeverStale,
        EventInvalidated,
        TimeLimited,
        BodyRevisionSensitive,
        SpeciesRevisionSensitive,
        SourceRevisionSensitive,
        RequiresReverification,
        HistoricalOnly
    }

    public enum KnowledgeForgettingPolicy
    {
        NeverForget,
        ReduceConfidence,
        PreserveSummary,
        RemoveActiveBelief
    }

    public enum KnowledgeContradictionPolicy
    {
        KeepBoth,
        ReduceConfidence,
        HigherCredibilityWins,
        ReplaceWithAuthorizedCorrection
    }

    public enum KnowledgeBeliefState
    {
        Unknown,
        Suspected,
        Believed,
        StronglyBelieved,
        Known,
        Disputed,
        Contradicted,
        Misconception,
        Stale,
        Forgotten,
        Invalid
    }

    public enum KnowledgeEvidenceDirection
    {
        Supports,
        Opposes,
        Corrects
    }

    public enum KnowledgeProvenance
    {
        Unknown,
        DirectObservation,
        SelfSensation,
        Examination,
        Testimony,
        Document,
        Inference,
        Memory,
        SkillKnowledge,
        SpeciesKnowledge,
        CulturalKnowledge,
        MagicalDetectionFoundation,
        ScriptedDiscovery,
        DevelopmentFixture,
        AuthoritativeCorrection
    }

    public enum KnowledgeAcquisitionSource
    {
        Unknown,
        DirectObservation,
        Examination,
        Testimony,
        WrittenSource,
        SkillOrEducation,
        PersonalExperience,
        BodySensation,
        EventParticipation,
        ScriptedRevelation,
        DevelopmentFixture
    }

    public enum KnowledgeFreshnessState
    {
        Current,
        Stale,
        Forgotten,
        Historical,
        RequiresReverification
    }

    public enum KnowledgeTruthState
    {
        NotCompared,
        Aligned,
        Misconception,
        Unverifiable
    }

    public enum KnowledgeReadinessState
    {
        Uninitialized,
        WaitingForPerson,
        WaitingForDefinitions,
        WaitingForHistory,
        BuildingKnowledge,
        Ready,
        Restoring,
        Invalid,
        Disposed
    }

    public enum KnowledgeResultCode
    {
        Success,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingPerson,
        MissingDefinitions,
        MissingFactDefinition,
        InvalidProposition,
        InvalidEvidence,
        InvalidVisibility,
        PrivateFactBlocked,
        DiagnosticFactBlocked,
        UnauthorizedTruthAccess,
        MissingBelief,
        MissingEvidence,
        RestoreFailed,
        ValidationFailed
    }

    public static class KnowledgeConfidence
    {
        public const int Minimum = 0;
        public const int Maximum = 1000;
        public const int DefaultWeakEvidence = 250;
        public const int DefaultObservation = 550;
        public const int DefaultTrustedEvidence = 750;

        public static int Clamp(int value)
        {
            return Math.Max(Minimum, Math.Min(Maximum, value));
        }
    }
}
