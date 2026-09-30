namespace UnityIsekaiGame.Beings.Biology.Condition
{
    public enum BodyConditionReadinessState
    {
        Uninitialized,
        WaitingForBody,
        WaitingForAnatomy,
        BuildingConditionState,
        Ready,
        Restoring,
        Invalid,
        Disposed
    }

    public enum StructureFunctionalState
    {
        Unknown,
        Normal,
        Reduced,
        SeverelyReduced,
        Disabled,
        Destroyed,
        Absent
    }

    public enum StructureDamageState
    {
        Unknown,
        Intact,
        Damaged,
        Fractured,
        Ruptured,
        Burned,
        Crushed,
        Penetrated,
        Severed,
        Destroyed,
        IncorporealDisrupted
    }

    public enum RuntimeStructurePresenceState
    {
        Unknown,
        Present,
        AuthoredAbsent,
        Missing,
        Severed,
        Destroyed
    }

    public enum InjurySeverity
    {
        Trivial,
        Minor,
        Moderate,
        Serious,
        Severe,
        Critical,
        Catastrophic
    }

    public enum InjuryRecordState
    {
        Active,
        Resolved,
        Inactive
    }

    public enum LocalizedDamageResultCode
    {
        Success,
        Preview,
        Duplicate,
        RuntimeNotReady,
        MissingActorBody,
        MissingAnatomy,
        MissingAnatomyNode,
        MissingInjuryDefinition,
        MissingCompatibility,
        MissingInteraction,
        IncompatibleInjury,
        InvalidRequest,
        InvalidIntegrity,
        InvalidRestore,
        StaleBody,
        StaleAnatomy,
        TargetUnavailable,
        AlreadyDestroyed
    }

    public enum StructuralRecoveryResultCode
    {
        Success,
        Preview,
        Duplicate,
        RuntimeNotReady,
        MissingActorBody,
        MissingAnatomyNode,
        MissingInjury,
        InvalidRequest,
        InvalidAmount,
        StaleBody,
        StaleCondition,
        TargetUnavailable,
        AlreadyComplete,
        RecoveryLimitReached
    }
}
