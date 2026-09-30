using System;

namespace UnityIsekaiGame.Economy.Markets
{
    public enum MarketCategory
    {
        Unknown = 0,
        LocalSettlement = 1,
        Regional = 2,
        OrganizationInternal = 3,
        Guild = 4,
        Wholesale = 5,
        RetailReference = 6,
        BlackMarketFoundation = 7,
        VirtualOrSystem = 8,
        Custom = 100
    }

    public enum MarketScopeType
    {
        Unknown = 0,
        GeographicRegion = 1,
        Settlement = 2,
        Organization = 3,
        Station = 4,
        Virtual = 5,
        Custom = 100
    }

    public enum MarketSubjectKind
    {
        Unknown = 0,
        ItemDefinition = 1,
        MaterialDefinition = 2,
        ItemCategory = 3,
        ServiceDefinitionFoundation = 4,
        LaborCategoryFoundation = 5,
        PropertyCategoryFoundation = 6,
        ProductionInput = 7,
        ProductionOutput = 8,
        Custom = 100
    }

    public enum MarketQuantityUnit
    {
        Unknown = 0,
        Each = 1,
        Stack = 2,
        Kilogram = 3,
        Liter = 4,
        Hour = 5,
        Service = 6,
        Lot = 7,
        Custom = 100
    }

    public enum MarketSupplySourceCategory
    {
        Unknown = 0,
        MerchantInventory = 1,
        OrganizationInventory = 2,
        MarketStorage = 3,
        ProductionOutput = 4,
        MaterialLot = 5,
        AuthoredBaseline = 6,
        ImportedAggregate = 7,
        TransactionObservation = 8,
        Custom = 100
    }

    public enum MarketDemandCategory
    {
        Unknown = 0,
        Consumer = 1,
        MerchantRestock = 2,
        ProductionInput = 3,
        OrganizationRequest = 4,
        GovernmentFoundation = 5,
        EmergencyFoundation = 6,
        SpeculativeFoundation = 7,
        AuthoredBaseline = 8,
        TransactionObservation = 9,
        Custom = 100
    }

    public enum MarketScarcityClass
    {
        Unknown = 0,
        Oversupplied = 1,
        Abundant = 2,
        Available = 3,
        Balanced = 4,
        Limited = 5,
        Scarce = 6,
        VeryScarce = 7,
        Critical = 8,
        Custom = 100
    }

    public enum MarketPriceFormationKind
    {
        Unknown = 0,
        FixedFallbackOnly = 1,
        DefaultSupplyDemand = 2,
        AuthoredReference = 3,
        Custom = 100
    }

    public enum MarketUpdatePolicyKind
    {
        Unknown = 0,
        ManualOnly = 1,
        ExplicitWorldTimeBoundary = 2,
        Custom = 100
    }

    public enum MerchantQuoteDirection
    {
        Unknown = 0,
        MerchantBuys = 1,
        MerchantSells = 2
    }

    public enum MarketObservationPrivacy
    {
        Public = 0,
        Protected = 1,
        Private = 2,
        Secret = 3
    }

    public enum MarketResultCode
    {
        Success = 0,
        Preview = 1,
        Duplicate = 2,
        InvalidRequest = 3,
        MissingDefinition = 4,
        MissingMarket = 5,
        MissingSubject = 6,
        MissingPrice = 7,
        MissingObservation = 8,
        CurrencyMismatch = 9,
        UnitMismatch = 10,
        InvalidQuantity = 11,
        Expired = 12,
        StaleRevision = 13,
        InsufficientData = 14,
        ValidationFailed = 15,
        AccessDenied = 16,
        ClosedMarket = 17
    }

    [Flags]
    public enum MarketTransactionObservationPolicy
    {
        None = 0,
        IncludeCommitted = 1 << 0,
        IncludeRefunded = 1 << 1,
        IncludeReversed = 1 << 2,
        IncludePrivate = 1 << 3
    }
}
