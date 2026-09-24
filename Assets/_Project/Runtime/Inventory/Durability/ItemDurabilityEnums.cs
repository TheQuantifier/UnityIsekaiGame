namespace UnityIsekaiGame.Inventory.Durability
{
    public enum ItemDurabilityOperationStatus
    {
        Succeeded,
        Preview,
        MissingRuntime,
        MissingItem,
        MissingDefinition,
        MissingDurability,
        DuplicateRecord,
        InvalidRequest,
        InvalidValue,
        Ineligible,
        ValidationFailed,
        RestoreFailed,
        AtomicCommitFailed,
        AccessDenied
    }

    public enum ItemDurabilityRecordSource
    {
        Unknown,
        DefinitionDefault,
        Migration,
        Authored,
        Generated,
        Repair,
        Salvage,
        TestLab,
        SceneAuthored,
        Custom
    }

    public enum ItemFunctionalState
    {
        Unknown,
        FullyFunctional,
        Impaired,
        PartiallyDisabled,
        Broken,
        Destroyed
    }

    public enum ItemBreakageState
    {
        None,
        Minor,
        Major,
        Broken,
        Destroyed
    }

    public enum ItemMaintenanceState
    {
        Unknown,
        Maintained,
        Due,
        Overdue,
        Neglected
    }

    public enum ItemDamageChannel
    {
        GeneralWear,
        Impact,
        Cutting,
        Piercing,
        Crushing,
        Abrasion,
        Fatigue,
        Corrosion,
        Heat,
        Fire,
        Cold,
        Water,
        Moisture,
        Chemical,
        Acid,
        Electrical,
        Magical,
        Biological,
        Contamination,
        Pressure,
        Overload,
        ImproperUse,
        Environmental,
        Custom
    }

    public enum ItemComponentCriticality
    {
        Noncritical,
        Supporting,
        Functional,
        Major,
        Critical,
        Essential,
        Decorative,
        Custom
    }

    public enum ItemRepairQuality
    {
        Unknown,
        Poor,
        Adequate,
        Good,
        Excellent,
        Masterwork
    }

    public enum ItemSalvageState
    {
        None,
        Eligible,
        Salvaged,
        Destroyed
    }
}
