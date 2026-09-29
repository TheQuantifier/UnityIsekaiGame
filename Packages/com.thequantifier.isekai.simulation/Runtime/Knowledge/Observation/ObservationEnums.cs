using System;

namespace UnityIsekaiGame.Knowledge.Observation
{
    public enum ObservationMethodCategory
    {
        Unknown,
        OrdinaryVisualObservation,
        CloseInspection,
        SelfSensation,
        PhysicalExamination,
        MedicalExamination,
        AnatomicalInspection,
        MagicalAnalysisFoundation,
        ToolAssistedScanFoundation,
        RecordExamination,
        TestimonyIntake,
        HistoricalRecordReview
    }

    public enum ExaminationMethodCategory
    {
        Unknown,
        SurfaceBodyExamination,
        InjuryExamination,
        MedicalExamination,
        RespiratoryExaminationFoundation,
        PoisonAnalysisFoundation,
        DiseaseAnalysisFoundation,
        SpeciesAnalysisFoundation,
        MagicalTransformationDetectionFoundation
    }

    public enum IdentificationMethodCategory
    {
        Unknown,
        PersonIdentity,
        ActorIdentity,
        BodyIdentity,
        Species,
        BodyForm,
        InjuryType,
        Hazard,
        BiologicalConditionFamily,
        BiologicalConditionDefinition,
        Poison,
        Venom,
        Toxin,
        ItemDefinitionFoundation,
        LocationFoundation,
        FactionSymbolFoundation,
        MagicalEffectFoundation
    }

    public enum DiagnosticMethodCategory
    {
        Unknown,
        SymptomBasedDiagnosis,
        InjuryDiagnosis,
        PoisonDiagnosis,
        InfectionDiagnosis,
        MedicalExaminationDiagnosis,
        ToolAssistedBiologicalDiagnosisFoundation,
        MagicalDiagnosisFoundation
    }

    public enum SensoryChannel
    {
        Unknown,
        Vision,
        Hearing,
        Touch,
        Smell,
        Taste,
        Proprioception,
        PainSensationFoundation,
        TemperatureSensation,
        MagicalDetectionFoundation,
        ToolSensorFoundation,
        RecordReading,
        Testimony
    }

    public enum ObservationTargetType
    {
        Unknown,
        Person,
        Actor,
        Body,
        Species,
        AnatomyNode,
        Injury,
        Hazard,
        BiologicalCondition,
        Poison,
        Venom,
        Toxin,
        Compatibility,
        Transformation,
        Item,
        Location,
        Event
    }

    public enum ObservationVisibilityState
    {
        Unknown,
        Clear,
        Dim,
        Obstructed,
        Hidden,
        DiagnosticOnly
    }

    public enum ConcealmentState
    {
        None,
        Minor,
        Moderate,
        Major,
        Complete,
        Disguised,
        TransformedAppearance
    }

    public enum ObservationAccessLevel
    {
        Public,
        Consent,
        Medical,
        Private,
        Confidential,
        Diagnostic,
        AuthorizedTruth,
        Development
    }

    public enum ObservationConsentState
    {
        Unknown,
        NotRequired,
        Granted,
        Denied,
        IncapacitatedAccess
    }

    public enum KnowledgeTrackingPolicy
    {
        None,
        NpcFullTracking,
        PlayerMechanicalOnly,
        RemotePlayerMechanicalOnly,
        DevelopmentObserverNoMutation
    }

    public enum ObservationOutcomeCode
    {
        Success,
        Preview,
        Duplicate,
        NotTracked,
        MissingObserver,
        MissingMethod,
        MissingProjection,
        InvalidContext,
        InvalidQuality,
        AccessDenied,
        PrivacyBlocked,
        Concealed,
        BelowThreshold,
        StaleTarget,
        MissingKnowledgeRuntime,
        KnowledgeRejected
    }

    public enum IdentificationResultState
    {
        Unresolved,
        Partial,
        Exact,
        Misidentified,
        CategoryOnly
    }

    public enum DiagnosticResultState
    {
        Unresolved,
        Differential,
        Likely,
        Exact,
        Misdiagnosis
    }

    public enum RepeatedObservationPolicy
    {
        MergeDuplicateTransaction,
        AllowDistinctEvidence,
        CapRepeatedEvidence,
        PreviewOnly
    }
}
