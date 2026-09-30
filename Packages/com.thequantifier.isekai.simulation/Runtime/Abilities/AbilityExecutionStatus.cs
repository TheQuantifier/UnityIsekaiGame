namespace UnityIsekaiGame.Abilities
{
    public enum AbilityExecutionStatus
    {
        Success,
        MissingAbility,
        InvalidSource,
        InvalidTarget,
        BlockedGameplayState,
        OutOfRange,
        InsufficientResource,
        ResourceCommitFailed,
        CooldownActive,
        InvalidDeliveryConfiguration,
        NoEffects,
        EffectValidationFailure,
        EffectExecutionFailure
    }
}
