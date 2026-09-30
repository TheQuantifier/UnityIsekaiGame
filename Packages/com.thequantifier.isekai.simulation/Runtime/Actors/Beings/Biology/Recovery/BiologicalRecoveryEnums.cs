namespace UnityIsekaiGame.Beings.Biology.Recovery
{
    public enum RecoveryReadinessState
    {
        Uninitialized,
        WaitingForBody,
        WaitingForAnatomy,
        WaitingForBodyCondition,
        WaitingForVitalProcesses,
        WaitingForHazards,
        WaitingForCompatibility,
        ResolvingDefinitions,
        BuildingRecoveryState,
        Ready,
        Restoring,
        Invalid,
        Disposed
    }

    public enum RecoveryCategory
    {
        Unknown,
        NaturalHealing,
        WoundClosure,
        TissueRepair,
        FractureHealing,
        OrganRecovery,
        ResourceRestoration,
        RestRecovery,
        Regeneration,
        ConstructRepair,
        SpiritRestoration,
        MagicalHealing,
        HolyHealing,
        NecroticRestoration,
        Reattachment,
        Replacement
    }

    public enum RecoveryTargetCategory
    {
        Unknown,
        Injury,
        AnatomyNode,
        StructuralIntegrity,
        VitalResource,
        Hazard,
        Condition
    }

    public enum RecoveryProcessState
    {
        Pending,
        Eligible,
        Active,
        Paused,
        Suppressed,
        Blocked,
        Interrupted,
        Completed,
        Cancelled,
        Invalid,
        Unknown
    }

    public enum RecoveryLimit
    {
        Unknown,
        NoRecovery,
        StabilizeOnly,
        ReduceSeverityOnly,
        RestorePartialIntegrity,
        RestoreFullIntegrity,
        RestoreFunctionOnly,
        CloseWound,
        RepairFracture,
        RestoreDestroyedStructure,
        RestoreMissingStructure,
        MethodSpecific
    }

    public enum RecoveryAllocationPolicy
    {
        ProfileDefined,
        EqualShare,
        HighestSeverityFirst,
        LowestSeverityFirst,
        VitalStructuresFirst,
        OldestInjuryFirst,
        NewestInjuryFirst,
        PriorityWeighted,
        ExplicitTargetOnly
    }

    public enum RecoveryRestType
    {
        NotResting,
        Resting,
        Sleeping,
        Meditating,
        RepairStation,
        SpiritSanctuary,
        MagicalRecovery,
        Unknown
    }

    public enum RecoveryInterruptionPolicy
    {
        PauseAndPreserveProgress,
        PauseAndDecayProgress,
        CancelAndPreserveOutcome,
        CancelAndLoseProgress,
        Reevaluate,
        MethodSpecific
    }

    public enum RecoveryPermanentOutcome
    {
        None,
        HealedWithoutScar,
        CosmeticScar,
        StructuralScar,
        ReducedMaximumIntegrity,
        PersistentFunctionalReduction,
        PermanentImpairment,
        UnrecoverableDestroyedState,
        UnrecoverableMissingState,
        MethodSpecific
    }

    public enum BiologicalRecoveryResultCode
    {
        Success,
        Preview,
        Duplicate,
        RuntimeNotReady,
        MissingBody,
        MissingProfile,
        MissingMethod,
        MissingTarget,
        MissingCompatibility,
        Incompatible,
        Suppressed,
        Blocked,
        RequirementFailed,
        InvalidRequest,
        InvalidElapsedTime,
        InvalidRestore,
        StaleBody,
        StaleTarget,
        StaleDependency,
        RecoveryLimitReached,
        UnsupportedTarget,
        OwningSystemFailure
    }
}
