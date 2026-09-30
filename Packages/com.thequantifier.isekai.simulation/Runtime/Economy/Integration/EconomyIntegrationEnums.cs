namespace UnityIsekaiGame.Economy.Integration
{
    public enum EconomicDomainAuthorityId
    {
        CurrencyTransactions,
        Markets,
        Trade,
        Payroll,
        Businesses,
        Property,
        Contracts,
        InstitutionalRevenue,
        RegionalFlow,
        ExternalPersonsOrganizations,
        ExternalItemsInventory,
        ExternalProfessions,
        ExternalKnowledgeHistoryAccess,
        ExternalWorldTimeLocations
    }

    public enum EconomicIntegrationDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum EconomicIntegrationDiagnosticCode
    {
        None,
        MissingRuntime,
        MissingDefinitionRegistry,
        MissingDefinition,
        DuplicateAuthority,
        MissingAuthority,
        InvalidSaveGraph,
        PersistenceDependencyCycle,
        ConservationMismatch,
        AccessProjectionUnavailable,
        Step12SignalInvalid,
        SceneHostUnavailable
    }

    public enum EconomicBoundaryInvariantId
    {
        MoneyMutatesOnlyThroughEconomyRuntime,
        ItemsMutateOnlyThroughItemIdentityRuntime,
        MarketsReadMoneyAndItemsButOwnOnlyPrices,
        TradeCoordinatesMoneyAndItemsAtomically,
        PayrollUsesEconomyRuntimeForPayment,
        BusinessesReferenceAccountsInventoriesAndProduction,
        PropertyOwnsTitleNotAccountsOrItems,
        ContractsUseEconomyRuntimeForSettlement,
        RevenueUsesEconomyRuntimeForCollection,
        RegionalFlowOwnsAggregatePoolsOnly,
        AccessRuntimeOwnsRedaction
    }

    public enum EconomicSignalCategory
    {
        MarketPressure,
        LaborPressure,
        Liquidity,
        Shortage,
        ObligationPressure,
        PropertyPressure,
        BusinessPerformance,
        RegionalFlow,
        Step12Contract
    }
}
