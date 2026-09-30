namespace UnityIsekaiGame.Inventory.Composition
{
    public enum MaterialCategory
    {
        Unknown = 0,
        Metal = 1,
        Wood = 2,
        Stone = 3,
        Cloth = 4,
        Leather = 5,
        Glass = 6,
        Liquid = 7,
        Organic = 8,
        Mineral = 9,
        Composite = 10,
        Arcane = 11
    }

    public enum MaterialEntryRole
    {
        Unknown = 0,
        PrimaryStructure = 1,
        Edge = 2,
        Core = 3,
        Binding = 4,
        Coating = 5,
        Fuel = 6,
        Decoration = 7,
        ConsumableContent = 8
    }

    public enum MaterialQuantityUnit
    {
        Unknown = 0,
        Count = 1,
        Gram = 2,
        Kilogram = 3,
        Milliliter = 4,
        Liter = 5,
        Percent = 6,
        Ratio = 7
    }

    public enum MaterialCompatibilityOutcome
    {
        Neutral = 0,
        Compatible = 1,
        Incompatible = 2,
        Degrades = 3,
        Reinforces = 4,
        RequiresBinder = 5
    }

    public enum ItemCompositionMassAuthority
    {
        AuthoredDefinition = 0,
        CompositionProjection = 1,
        CompositionAuthoritative = 2
    }

    public enum ItemCompositionMutationPurpose
    {
        RuntimeGameplay = 0,
        AuthoredSetup = 1,
        DefaultInitialization = 2,
        DebugTestLab = 3,
        CraftingProduction = 4,
        RepairModification = 5
    }

    public enum ItemCompositionCompleteness
    {
        Unknown = 0,
        Partial = 1,
        Complete = 2,
        Abstracted = 3
    }

    public enum ItemComponentKind
    {
        Unknown = 0,
        AbstractComponent = 1,
        TrackedItemInstance = 2,
        DefinitionReference = 3
    }

    public enum ItemCompositionOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        MissingRuntime = 2,
        MissingItem = 3,
        MissingDefinition = 4,
        MissingMaterial = 5,
        DuplicateComposition = 6,
        DuplicateEntry = 7,
        InvalidQuantity = 8,
        InvalidGraph = 9,
        InvalidRequest = 10,
        ValidationFailed = 11,
        RestoreFailed = 12,
        AccessDenied = 13,
        InvalidComponentLocation = 14,
        AtomicCommitFailed = 15
    }
}
