namespace UnityIsekaiGame.Inventory.Recipes
{
    public enum RecipeCategory
    {
        Unknown = 0,
        Crafting = 10,
        Assembly = 20,
        Smithing = 30,
        Forging = 40,
        Smelting = 50,
        Refining = 60,
        Cooking = 70,
        Brewing = 80,
        Alchemy = 90,
        Enchanting = 100,
        Sewing = 110,
        Weaving = 120,
        Leatherworking = 130,
        Woodworking = 140,
        Masonry = 150,
        ConstructionFoundation = 160,
        RepairProcedure = 170,
        MaintenanceProcedure = 180,
        SalvageProcedure = 190,
        ComponentReplacement = 200,
        MedicalPreparation = 210,
        ChemicalPreparation = 220,
        MagicalRitual = 230,
        ResearchProcedure = 240,
        Custom = 1000
    }

    public enum RecipeLifecycleState
    {
        Active = 0,
        Deprecated = 10,
        Disabled = 20
    }

    public enum RecipeInputRole
    {
        Unknown = 0,
        PrimaryMaterial = 10,
        SecondaryMaterial = 20,
        StructuralComponent = 30,
        FunctionalComponent = 40,
        DecorativeComponent = 50,
        BindingMaterial = 60,
        Coating = 70,
        Filling = 80,
        Catalyst = 90,
        Fuel = 100,
        Solvent = 110,
        Reagent = 120,
        Stabilizer = 130,
        ConsumableTool = 140,
        ReusableInput = 150,
        ReplacementComponent = 160,
        Packaging = 170,
        MagicalFocus = 180,
        SafetyMaterial = 190,
        OptionalEnhancer = 200,
        Custom = 1000
    }

    public enum RecipeInputClassification
    {
        Consumable = 0,
        Catalyst = 10,
        ReusableInput = 20,
        ToolLike = 30,
        StationProvided = 40
    }

    public enum RecipeRequirementState
    {
        Required = 0,
        Optional = 10,
        Conditional = 20
    }

    public enum RecipeOutputRole
    {
        Unknown = 0,
        PrimaryOutput = 10,
        SecondaryOutput = 20,
        Byproduct = 30,
        Waste = 40,
        Scrap = 50,
        RecoveredInput = 60,
        ReusableComponent = 70,
        FailedResult = 80,
        DamagedResult = 90,
        RecordOrBlueprintFoundation = 100,
        Custom = 1000
    }

    public enum RecipeBatchScalingPolicy
    {
        Fixed = 0,
        Discrete = 10,
        Continuous = 20,
        NoScaling = 30
    }

    public enum RecipeTransferPolicy
    {
        Unknown = 0,
        None = 10,
        Fixed = 20,
        InputDerived = 30,
        PreserveTrackedComponent = 40,
        LossAdjusted = 50,
        PolicyDerived = 60
    }

    public enum RecipeQualityPolicy
    {
        Unknown = 0,
        FixedAuthored = 10,
        MaterialInfluenced = 20,
        SkillToolStationInfluenced = 30,
        PolicyReference = 40
    }

    public enum RecipeAffixPolicy
    {
        None = 0,
        FixedAuthored = 10,
        PoolReference = 20,
        MaterialDerived = 30,
        PolicyReference = 40
    }

    public enum RecipeDurabilityPolicy
    {
        Unknown = 0,
        FixedPercentage = 10,
        QualityDerived = 20,
        MaterialDerived = 30,
        RepairOutput = 40,
        SalvageOutput = 50,
        PolicyReference = 60
    }

    public enum RecipeProcedureStepKind
    {
        Unknown = 0,
        PrepareInput = 10,
        TransformMaterial = 20,
        AssembleComponent = 30,
        Heat = 40,
        Cool = 50,
        Mix = 60,
        Shape = 70,
        Finish = 80,
        Inspect = 90,
        Custom = 1000
    }

    public enum RecipeResolutionStatus
    {
        Succeeded = 0,
        Preview = 10,
        MissingRecipe = 20,
        MissingVersion = 30,
        MissingVariant = 40,
        InvalidBatch = 50,
        InvalidProcedure = 60,
        HiddenRequirement = 70,
        RequirementFailed = 80,
        ValidationFailed = 90,
        RestoreFailed = 100
    }

    public enum RecipeKnowledgeCompleteness
    {
        Unknown = 0,
        ExistenceOnly = 10,
        IngredientsOnly = 20,
        ProcedureOnly = 30,
        Partial = 40,
        Complete = 50,
        Incorrect = 60,
        Outdated = 70
    }

    public enum RecipeProjectionAccessLevel
    {
        Ordinary = 0,
        RecordBacked = 10,
        Privileged = 20
    }
}
