namespace UnityIsekaiGame.Economy.RegionalFlow
{
    public enum EconomicRegionCategory
    {
        Unknown = 0,
        SettlementEconomy = 1,
        RuralRegion = 2,
        AgriculturalRegion = 3,
        MiningRegion = 4,
        IndustrialRegion = 5,
        CommercialHub = 6,
        PortFoundation = 7,
        BorderRegionFoundation = 8,
        OrganizationControlledRegion = 9,
        InstitutionControlledRegion = 10,
        IsolatedRegion = 11,
        AbstractTestRegion = 12,
        Custom = 100
    }

    public enum EconomicRegionState
    {
        Planned = 0,
        Active = 1,
        Restricted = 2,
        Disrupted = 3,
        Isolated = 4,
        Recovering = 5,
        Dormant = 6,
        Closed = 7,
        Invalid = 8,
        Custom = 100
    }

    public enum RegionalSimulationFidelity
    {
        Unknown = 0,
        SummaryOnly = 1,
        AggregatePools = 2,
        AggregateWithNamedActors = 3,
        ExactOnly = 4,
        Custom = 100
    }

    public enum CommodityCategory
    {
        Unknown = 0,
        Food = 1,
        WaterFoundation = 2,
        Fuel = 3,
        Ore = 4,
        Metal = 5,
        Wood = 6,
        Stone = 7,
        Cloth = 8,
        Leather = 9,
        Medicine = 10,
        Tools = 11,
        Weapons = 12,
        Armor = 13,
        CraftingMaterials = 14,
        ConstructionMaterials = 15,
        LuxuryGoods = 16,
        AgriculturalOutput = 17,
        LivestockFoundation = 18,
        EnergyFoundation = 19,
        ServiceCapacity = 20,
        LaborCapacity = 21,
        Custom = 100
    }

    public enum CommodityUnit
    {
        Unknown = 0,
        Each = 1,
        WeightUnit = 2,
        VolumeUnit = 3,
        Bundle = 4,
        LaborHour = 5,
        ServiceUnit = 6,
        Custom = 100
    }

    public enum CommodityFungibilityPolicy
    {
        Unknown = 0,
        FullyFungible = 1,
        QualityBandFungible = 2,
        ConditionBandFungible = 3,
        PolicyRestricted = 4,
        ExactOnly = 5,
        Custom = 100
    }

    public enum CommodityMaterializationPolicy
    {
        Unknown = 0,
        NeverMaterialize = 1,
        ExplicitOnly = 2,
        AuthorizedOnly = 3,
        Custom = 100
    }

    public enum CommodityAggregationPolicy
    {
        Unknown = 0,
        ExplicitEligibleExactItemsOnly = 1,
        ExactInventoryObservationOnly = 2,
        AggregateOnly = 3,
        Custom = 100
    }

    public enum CommodityPoolPurpose
    {
        Unknown = 0,
        GeneralRegionalSupply = 1,
        ConsumerSupply = 2,
        ProducerInput = 3,
        MerchantAggregateStock = 4,
        InstitutionalReserve = 5,
        EmergencyReserve = 6,
        ExportStock = 7,
        ImportBuffer = 8,
        WorkInProgress = 9,
        Custom = 100
    }

    public enum AggregateQuantityOperationKind
    {
        Unknown = 0,
        Add = 1,
        Remove = 2,
        Reserve = 3,
        ReleaseReservation = 4,
        Consume = 5,
        Move = 6,
        MarkInbound = 7,
        MarkOutbound = 8,
        RecordLoss = 9,
        RecordSpoilageFoundation = 10,
        CorrectQuantity = 11,
        Materialize = 12,
        AggregateExactItems = 13
    }

    public enum EconomicCohortCategory
    {
        Unknown = 0,
        GeneralConsumers = 1,
        SubsistenceHouseholdsFoundation = 2,
        Farmers = 3,
        Craftspeople = 4,
        Laborers = 5,
        Merchants = 6,
        Miners = 7,
        SoldiersFoundation = 8,
        ScholarsFoundation = 9,
        NobilityFoundation = 10,
        InstitutionWorkers = 11,
        UnemployedLaborPool = 12,
        VisitorsFoundation = 13,
        Custom = 100
    }

    public enum LaborCategory
    {
        Unknown = 0,
        GeneralLabor = 1,
        AgriculturalLabor = 2,
        MiningLabor = 3,
        CraftLabor = 4,
        MerchantLabor = 5,
        AdministrativeLabor = 6,
        GuardLaborFoundation = 7,
        ScholarLaborFoundation = 8,
        Custom = 100
    }

    public enum WagePressureState
    {
        Unknown = 0,
        Downward = 1,
        Balanced = 2,
        Upward = 3,
        SevereShortage = 4,
        SevereSurplus = 5
    }

    public enum ProductionProfileCategory
    {
        Unknown = 0,
        Farming = 1,
        Mining = 2,
        Logging = 3,
        Smelting = 4,
        Smithing = 5,
        FoodProcessing = 6,
        TextileProduction = 7,
        ConstructionMaterialProduction = 8,
        WorkshopManufacturing = 9,
        ServiceCapacity = 10,
        Custom = 100
    }

    public enum ConsumptionProfileCategory
    {
        Unknown = 0,
        HouseholdNeed = 1,
        ProducerInput = 2,
        InstitutionalUse = 3,
        Maintenance = 4,
        SpoilageFoundation = 5,
        Custom = 100
    }

    public enum ShortageKind
    {
        Unknown = 0,
        Commodity = 1,
        Labor = 2,
        Liquidity = 3,
        Affordability = 4
    }

    public enum ShortageState
    {
        Unknown = 0,
        Shortage = 1,
        Balanced = 2,
        Surplus = 3
    }

    public enum TradeConnectionState
    {
        Planned = 0,
        Active = 1,
        Restricted = 2,
        Closed = 3,
        Invalid = 4
    }

    public enum FlowOrderState
    {
        Planned = 0,
        Reserved = 1,
        InTransit = 2,
        Delivered = 3,
        Cancelled = 4,
        Failed = 5
    }

    public enum EconomicModifierKind
    {
        Unknown = 0,
        Production = 1,
        Consumption = 2,
        Labor = 3,
        ConnectionCapacity = 4,
        TransferCost = 5,
        Custom = 100
    }

    public enum EconomicCycleStage
    {
        Unknown = 0,
        Production = 1,
        Consumption = 2,
        Labor = 3,
        Shortage = 4,
        Flow = 5,
        MarketPublication = 6,
        ConservationAudit = 7,
        Complete = 8
    }

    public enum RegionalFlowResultCode
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        InvalidRequest = 3,
        MissingDefinition = 4,
        MissingRegion = 5,
        MissingCommodity = 6,
        MissingPool = 7,
        MissingCohort = 8,
        MissingConnection = 9,
        InsufficientQuantity = 10,
        InsufficientCapacity = 11,
        UnitMismatch = 12,
        ConservationFailed = 13,
        PolicyViolation = 14,
        StaleBoundary = 15,
        RolledBack = 16,
        ValidationFailed = 17,
        AccessDenied = 18
    }
}
