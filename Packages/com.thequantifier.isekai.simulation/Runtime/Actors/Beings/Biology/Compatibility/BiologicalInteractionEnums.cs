namespace UnityIsekaiGame.Beings.Biology.Compatibility
{
    public enum BiologicalInteractionCategory
    {
        Unknown,
        Hazard,
        Injury,
        VitalResource,
        Metabolic,
        Recovery,
        Healing,
        Repair,
        Disease,
        Infection,
        Parasite,
        Poison,
        Toxin,
        Environmental,
        Elemental,
        MagicalBiological,
        Transformation,
        Replacement,
        Possession
    }

    public enum BiologicalInteractionDisposition
    {
        Contextual,
        Harmful,
        Beneficial
    }

    public enum BiologicalCompatibilityState
    {
        Compatible,
        Incompatible
    }

    public enum BiologicalInteractionRuleKind
    {
        CompatibilityOverride,
        Immunity,
        Resistance,
        Vulnerability,
        Affinity,
        Suppression,
        Conversion,
        Absorption,
        MaximumSeverityLimit,
        MinimumEffectFloor
    }

    public enum BiologicalCompatibilitySourceKind
    {
        InteractionDefault,
        SpeciesProfile,
        BodyFormProfile,
        ClassificationProfile,
        Trait,
        Condition,
        Capability,
        Anatomy,
        VitalProcess,
        Hazard,
        Equipment,
        Environment,
        Development,
        System
    }

    public enum BiologicalCompatibilityReadinessState
    {
        Uninitialized,
        WaitingForBody,
        WaitingForDefinitions,
        Ready,
        Invalid,
        Disposed
    }

    public enum BiologicalCompatibilityResultCode
    {
        Success,
        RuntimeNotReady,
        MissingBody,
        MissingInteraction,
        StaleBody,
        InvalidRequest,
        Duplicate,
        MissingContribution
    }
}
