namespace UnityIsekaiGame.StatusEffects
{
    public enum StatusApplicationStatus
    {
        Success,
        MissingDefinition,
        InvalidTarget,
        UnsupportedTarget,
        DuplicateRejected,
        MaximumStacksReached,
        InvalidDuration,
        InvalidModifier,
        TargetLacksStat,
        ControllerUnavailable,
        AlreadyRemoved,
        MalformedRestoreData,
        DuplicateApplicationId,
        RestoreValidationFailed
    }
}
