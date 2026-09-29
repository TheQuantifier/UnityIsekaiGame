namespace UnityIsekaiGame.Inventory.Quality
{
    public enum QualityValueState
    {
        Unknown = 0,
        NotApplicable = 1,
        Known = 2
    }

    public enum WorkmanshipDimension
    {
        Unknown = 0,
        Overall = 1,
        Structural = 2,
        EdgeOrSurface = 3,
        Assembly = 4,
        Fit = 5,
        Finish = 6,
        Balance = 7,
        Precision = 8,
        Decoration = 9,
        MagicalInscription = 10,
        ComponentSpecific = 11,
        Custom = 100
    }

    public enum ItemQualityDimension
    {
        Unknown = 0,
        Overall = 1,
        Structural = 2,
        Functional = 3,
        Material = 4,
        Component = 5,
        Workmanship = 6,
        Balance = 7,
        Precision = 8,
        SharpnessPotential = 9,
        DefensiveConstruction = 10,
        Comfort = 11,
        Stability = 12,
        Efficiency = 13,
        Purity = 14,
        Magical = 15,
        Decorative = 16,
        Authenticity = 17,
        Consistency = 18,
        DefectSeverity = 19,
        Custom = 100
    }

    public enum ItemQualityRecordSource
    {
        Unknown = 0,
        Authored = 1,
        DefinitionDefault = 2,
        SceneAuthored = 3,
        ProductionGenerated = 4,
        WorkmanshipDerived = 5,
        CompositionDerived = 6,
        Modification = 8,
        MagicalAlteration = 9,
        TestLab = 10,
        Custom = 100
    }

    public enum ItemDefectCategory
    {
        Unknown = 0,
        Cracked = 1,
        Warped = 2,
        PoorlyBalanced = 3,
        Dull = 4,
        Misaligned = 5,
        Contaminated = 6,
        WeakBinding = 7,
        LooseComponent = 8,
        IncompleteEnchantment = 9,
        CosmeticFlaw = 10,
        StructuralFlaw = 11,
        HiddenDefect = 12,
        Custom = 100
    }

    public enum ItemAffixClassification
    {
        Unknown = 0,
        Implicit = 1,
        Intrinsic = 2,
        Prefix = 3,
        Suffix = 4,
        Crafted = 5,
        Enchanted = 6,
        Cursed = 7,
        Blessed = 8,
        MaterialDerived = 9,
        QualityDerived = 10,
        ComponentDerived = 11,
        TemporaryFoundation = 12,
        Hidden = 13,
        Sealed = 14,
        Unique = 15,
        Custom = 100
    }

    public enum ItemAffixSource
    {
        Unknown = 0,
        Authored = 1,
        Generated = 2,
        Crafted = 3,
        MaterialDerived = 4,
        QualityDerived = 5,
        MagicalAlteration = 7,
        TestLab = 8,
        Custom = 100
    }

    public enum ItemQualityAffixOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        MissingRuntime = 2,
        MissingItem = 3,
        MissingDefinition = 4,
        MissingQuality = 5,
        DuplicateRecord = 6,
        DuplicateAffix = 7,
        InvalidRequest = 8,
        InvalidValue = 9,
        Ineligible = 10,
        Conflict = 11,
        MaximumExceeded = 12,
        ValidationFailed = 13,
        RestoreFailed = 14,
        AtomicCommitFailed = 15,
        AccessDenied = 16
    }

    public enum ItemRaritySource
    {
        Unknown = 0,
        DefinitionDefault = 1,
        Derived = 2,
        AuthoredOverride = 3,
        Generated = 4,
        TestLab = 5,
        Custom = 100
    }
}
