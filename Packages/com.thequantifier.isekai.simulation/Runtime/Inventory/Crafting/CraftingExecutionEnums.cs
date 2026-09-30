namespace UnityIsekaiGame.Inventory.Crafting
{
    public enum CraftingExecutionStatus
    {
        Succeeded = 0,
        Preview = 10,
        Duplicate = 20,
        MissingRuntime = 30,
        MissingRecipe = 40,
        RequirementFailed = 50,
        ReservationFailed = 60,
        StalePlan = 70,
        InputConsumptionFailed = 80,
        OutputCreationFailed = 90,
        ToolWearFailed = 100,
        RollbackFailed = 110,
        InvalidRequest = 120,
        ValidationFailed = 130,
        RestoreFailed = 140
    }

    public enum CraftingOperationState
    {
        Prepared = 0,
        Reserved = 10,
        Executing = 20,
        Completed = 30,
        Failed = 40,
        RolledBack = 50,
        Cancelled = 60
    }

    public enum CraftingFailurePolicy
    {
        FullRollback = 0,
        KeepFailureOutputs = 10,
        DamagePrimaryOutput = 20
    }

    public enum CraftingOutputKind
    {
        Primary = 0,
        Secondary = 10,
        Byproduct = 20,
        Waste = 30,
        Scrap = 40,
        RecoveredInput = 50,
        FailureOutput = 60
    }
}
