namespace UnityIsekaiGame.Inventory.Production
{
    public enum ProductionToolCategory
    {
        Unknown = 0,
        Cutting = 10,
        Hammering = 20,
        Measuring = 30,
        Heating = 40,
        Holding = 50,
        Alchemy = 60,
        Magical = 70,
        General = 100
    }

    public enum ProductionToolRole
    {
        Unknown = 0,
        Primary = 10,
        Secondary = 20,
        Precision = 30,
        Safety = 40,
        Finishing = 50
    }

    public enum ProductionStationCategory
    {
        Unknown = 0,
        Workbench = 10,
        Forge = 20,
        Anvil = 30,
        AlchemyBench = 40,
        EnchantingFocus = 50,
        Field = 60,
        General = 100
    }

    public enum ProductionRequirementType
    {
        Unknown = 0,
        Tool = 10,
        Station = 20,
        SkillCapability = 30,
        Knowledge = 40,
        Resource = 50,
        Item = 60,
        Material = 70,
        Environment = 80,
        Access = 90,
        Body = 100
    }

    public enum ProductionRequirementStrictness
    {
        Required = 0,
        Optional = 10,
        Enhancing = 20
    }

    public enum ProductionRequirementEvaluationStatus
    {
        Succeeded = 0,
        Preview = 10,
        Partial = 20,
        MissingRequirement = 30,
        MissingTool = 40,
        MissingStation = 50,
        MissingCapability = 60,
        MissingKnowledge = 70,
        MissingResource = 80,
        MissingItem = 90,
        MissingMaterial = 100,
        AccessDenied = 110,
        Conflict = 120,
        StalePlan = 130,
        ValidationFailed = 140,
        RestoreFailed = 150
    }

    public enum ProductionEvaluationPerspective
    {
        Authoritative = 0,
        Perceived = 10
    }

    public enum ProductionPlanStatus
    {
        Planned = 0,
        Reserved = 10,
        Invalidated = 20,
        Released = 30,
        Completed = 40
    }

    public enum ProductionReservationStatus
    {
        Active = 0,
        Released = 10,
        Expired = 20
    }

    public enum ProductionQuantityUnit
    {
        Count = 0,
        Charge = 10,
        WorkUnit = 20,
        Liter = 30,
        Kilogram = 40
    }
}
