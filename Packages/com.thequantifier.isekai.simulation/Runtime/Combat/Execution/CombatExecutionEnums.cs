namespace UnityIsekaiGame.Combat.Execution
{
    public enum CombatExecutionActionType
    {
        None,
        Attack,
        Ability,
        Defense,
        Effect,
        Custom
    }

    public enum CombatExecutionPhase
    {
        None,
        WindUp,
        ReadyToCommit,
        Executing,
        Recovery,
        Completed,
        Cancelled,
        Interrupted
    }

    public enum CombatCommitmentCategory
    {
        None,
        Attack,
        Spellcast,
        Defense,
        ItemUse,
        Interaction,
        MovementRestricted,
        FullActionLock
    }

    public enum CombatExecutionCostType
    {
        Resource,
        InventoryItem,
        Currency,
        Ammunition,
        Charge,
        RequirementOnly
    }

    public enum CombatExecutionCostCommitPoint
    {
        OnBegin,
        OnExecution,
        OnSuccessfulExecution,
        OnCompletion
    }

    public enum CombatExecutionCooldownStartPoint
    {
        OnBegin,
        OnExecution,
        OnCompletion
    }

    public enum CombatExecutionCooldownScope
    {
        Definition,
        CooldownGroup,
        GlobalGroup
    }

    public enum CombatExecutionCancellationReason
    {
        PlayerOrAIRequest,
        TargetInvalid,
        RequirementsFailed,
        EquipmentChanged,
        InsufficientCost,
        LifecycleChanged,
        InterruptedByDamage,
        InterruptedByDefense,
        Scripted,
        AuthorityOverride,
        Restore
    }

    public enum CombatExecutionInterruptionPolicy
    {
        NotInterruptible,
        InterruptDuringWindUp,
        InterruptBeforeExecution,
        InterruptAnytimeBeforeCompletion
    }

    public enum CombatExecutionRefundPolicy
    {
        NoRefund,
        RefundIfCancelledBeforeExecution,
        RefundIfUnderlyingExecutionFails
    }
}
