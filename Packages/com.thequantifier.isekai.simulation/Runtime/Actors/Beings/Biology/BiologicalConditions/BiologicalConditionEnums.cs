using System;

namespace UnityIsekaiGame.Beings.Biology.BiologicalConditions
{
    public enum BiologicalConditionReadinessState
    {
        Uninitialized,
        WaitingForBody,
        WaitingForDefinitions,
        WaitingForCompatibility,
        Ready,
        Restoring,
        Invalid,
        Disposed
    }

    public enum BiologicalConditionFamily
    {
        Unknown,
        Disease,
        ViralInfection,
        BacterialInfection,
        FungalInfection,
        ParasiticInfection,
        GeneralInfection,
        Poison,
        Venom,
        Toxin,
        Intoxication,
        Alcohol,
        DrugEffect,
        Fever,
        InflammatoryResponse,
        AutoimmuneCondition,
        Deficiency,
        ChronicCondition,
        DormantCondition,
        CarrierState,
        MagicalCorruptionFoundation,
        RadiationLikeConditionFoundation
    }

    public enum BiologicalConditionStage
    {
        Unknown,
        Exposed,
        Incubating,
        Establishing,
        Active,
        Worsening,
        Peak,
        Recovering,
        Remission,
        Dormant,
        Chronic,
        Carrier,
        Cleared,
        Resolved,
        Suppressed,
        Invalid
    }

    public enum BiologicalConditionSeverity
    {
        Trace,
        Minor,
        Moderate,
        Serious,
        Severe,
        Critical,
        Catastrophic
    }

    public enum BiologicalExposureRoute
    {
        Unknown,
        Contact,
        Inhalation,
        Ingestion,
        Injection,
        Wound,
        Bite,
        Sting,
        Blood,
        Environmental,
        Magical,
        Scripted
    }

    public enum BiologicalConditionSourceCategory
    {
        Unknown,
        Environment,
        Body,
        Injury,
        Item,
        Ability,
        Treatment,
        Transmission,
        Scripted,
        Development
    }

    public enum BiologicalConditionResultCode
    {
        Success,
        Preview,
        Duplicate,
        RuntimeNotReady,
        MissingBody,
        MissingDefinition,
        MissingCompatibility,
        MissingInteraction,
        InvalidRequest,
        InvalidDose,
        InvalidRoute,
        InvalidAnatomyTarget,
        MissingRequiredInjury,
        Incompatible,
        Immune,
        Suppressed,
        StaleBody,
        StaleDependency,
        MissingInstance,
        MissingTreatment,
        TreatmentNotAllowed,
        TransmissionDeferred,
        RestoreFailed
    }

    public enum BiologicalConditionStackingPolicy
    {
        MergeByDefinitionAndStrain,
        MergeBySource,
        IndependentInstances,
        StrongestDose,
        NonStacking
    }

    public enum BiologicalConditionTreatmentKind
    {
        GeneralMedicine,
        Antidote,
        Antiviral,
        Antibiotic,
        Antifungal,
        Antiparasitic,
        FeverReducer,
        SupportiveCare,
        MagicalCleansing
    }

    public enum BiologicalConditionTransmissionMode
    {
        None,
        Contact,
        Airborne,
        Fluid,
        Bite,
        ParasiteVector,
        Environmental,
        Scripted
    }

    public enum BiologicalConditionReconciliationPolicy
    {
        Clear,
        PreserveIfCompatible,
        Preserve,
        Suppress,
        ConvertToMemory
    }

    [Flags]
    public enum BiologicalConditionConsequenceFlags
    {
        None = 0,
        VitalPressure = 1 << 0,
        HazardRequest = 1 << 1,
        Step6DamagePlan = 1 << 2,
        RecoveryModifier = 1 << 3,
        Fever = 1 << 4,
        Symptom = 1 << 5,
        Transmission = 1 << 6
    }
}
