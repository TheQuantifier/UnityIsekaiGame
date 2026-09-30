using System;

namespace UnityIsekaiGame.Knowledge.Sources
{
    public enum InformationSourceCategory
    {
        Unknown,
        DirectObservation,
        DirectParticipation,
        Examination,
        Identification,
        Diagnosis,
        Experiment,
        Inference,
        PersonalTestimony,
        ExpertTestimony,
        AnonymousTestimony,
        Hearsay,
        WrittenRecord,
        OfficialRecord,
        PrivateRecord,
        HistoricalRecord,
        Book,
        Journal,
        Letter,
        Map,
        PhysicalEvidence,
        ToolOutput,
        MagicalDetection,
        TechnologicalDetection,
        InstitutionalReport,
        PublicAnnouncement,
        Memory,
        CopiedSource,
        Translation,
        Summary,
        RumorOriginSource,
        UnknownSource,
        Custom
    }

    public enum InformationSourceReferenceType
    {
        None,
        Person,
        Organization,
        Institution,
        Item,
        Document,
        Letter,
        Journal,
        PublicNotice,
        Archive,
        Map,
        Object,
        Body,
        Location,
        ObservationMethod,
        ExaminationMethod,
        DiagnosticMethod,
        Tool,
        MagicalMethod,
        TechnicalMethod,
        HistoricalEvent,
        Memory,
        Evidence,
        Custom
    }

    public enum InformationSourceTransformationType
    {
        None,
        Copy,
        Translation,
        Summary,
        Correction,
        Supersession,
        Inference
    }

    public enum SourceVerificationState
    {
        Unknown,
        Unverified,
        Claimed,
        PartiallyVerified,
        Verified,
        Disputed,
        Forged,
        Superseded
    }

    public enum ReliabilityDimension
    {
        GeneralDependability,
        DomainExpertise,
        FirsthandProximity,
        MethodQuality,
        Authenticity,
        IdentityCertainty,
        ObservationQuality,
        RecordIntegrity,
        Recency,
        TransmissionIntegrity,
        Independence,
        Corroboration,
        InternalConsistency,
        ErrorRisk,
        DeceptionRisk,
        BiasRisk,
        Completeness,
        Precision,
        ContextFit
    }

    public enum InformationSourceResultCode
    {
        Success,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinition,
        MissingSource,
        MissingPerson,
        InvalidSourceChain,
        InvalidAssessment,
        InvalidReliabilityDimension,
        PrivateSourceBlocked,
        RestoreFailed,
        ValidationFailed
    }

    public enum SourcePrivacyLevel
    {
        Public,
        Shared,
        Personal,
        Private,
        Hidden,
        Secret
    }

    public enum SourceIndependenceState
    {
        Unknown,
        SameSource,
        Dependent,
        PartiallyIndependent,
        Independent
    }
}
