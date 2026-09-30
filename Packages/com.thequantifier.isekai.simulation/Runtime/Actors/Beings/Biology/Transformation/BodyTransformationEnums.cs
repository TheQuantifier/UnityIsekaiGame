using System;

namespace UnityIsekaiGame.Beings.Biology.Transformation
{
    public enum TransformationCategory
    {
        Unknown,
        TemporaryPolymorph,
        PermanentSpeciesChange,
        BodyFormChange,
        BodyReplacement,
        BodySwap,
        Possession,
        Reincarnation,
        ResurrectionBody,
        Embodiment,
        Disembodiment,
        StructureReplacement,
        OrganReplacement,
        LimbReplacement,
        ConstructComponentReplacement
    }

    public enum TransformationReadinessState
    {
        Uninitialized,
        WaitingForPerson,
        WaitingForActor,
        WaitingForSourceBody,
        WaitingForTargetBody,
        WaitingForDefinitions,
        WaitingForCompatibility,
        Planning,
        Ready,
        Executing,
        Reverting,
        Restoring,
        Invalid,
        Disposed
    }

    public enum TransformationResultCode
    {
        Success,
        Preview,
        Duplicate,
        RuntimeNotReady,
        MissingMethod,
        MissingPerson,
        MissingSourceBody,
        MissingTargetBody,
        MissingTargetSpecies,
        MissingTargetBodyForm,
        MissingCompatibility,
        Incompatible,
        Suppressed,
        Immune,
        InvalidRequest,
        InvalidPlan,
        StaleBody,
        StaleDependency,
        ExecutionFailed,
        RollbackFailed,
        NoActiveTransformation,
        RestoreFailed,
        UnsupportedCategory
    }

    public enum TransformationTransferPolicy
    {
        MethodDefault,
        KeepWithOriginalBody,
        TransferPersonOwnedOnly,
        TransferControllerOnly,
        TransferNone,
        Explicit
    }

    public enum TransformationReconciliationPolicy
    {
        MethodDefault,
        Rebuild,
        PreserveIfCompatible,
        Cancel,
        Clear,
        RemapByStableNodeId,
        InitializeClean
    }

    public enum TransformationEquipmentPolicy
    {
        PreserveIfCompatible,
        UnequipIncompatible,
        BlockIfIncompatible,
        IgnoreForPrototype
    }

    public enum TransformationLifecyclePolicy
    {
        Preserve,
        RequireActive,
        BlockDead,
        ResetBodyState,
        Explicit
    }

    public enum TransformationControllerPolicy
    {
        PreserveController,
        TransferController,
        DetachController,
        Explicit
    }

    public enum TransformationAssociationPolicy
    {
        PreserveBody,
        ReassignPersonToTargetBody,
        SwapPersons,
        TemporaryPossession,
        Explicit
    }

    public enum TransformationReversionPolicy
    {
        None,
        RestoreCapturedBodyState,
        ReassignOriginalBody,
        Explicit
    }

    public enum TransformationStateOwnership
    {
        Unknown,
        PersonOwned,
        ActorOwned,
        BodyOwned,
        ControllerOwned,
        SoulOwned,
        SessionOnly
    }

    [Flags]
    public enum TransformationPlanFlags
    {
        None = 0,
        SpeciesChange = 1 << 0,
        BodyFormChange = 1 << 1,
        ExistingBodyTarget = 1 << 2,
        Temporary = 1 << 3,
        RequiresReversionState = 1 << 4,
        StructureReplacement = 1 << 5,
        ControllerReassignment = 1 << 6,
        PersonBodyReassociation = 1 << 7
    }
}
