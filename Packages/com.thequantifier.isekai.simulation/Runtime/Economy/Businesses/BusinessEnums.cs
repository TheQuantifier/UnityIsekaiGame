namespace UnityIsekaiGame.Economy.Businesses
{
    public enum BusinessCategory
    {
        Unknown = 0,
        MerchantShop = 10,
        MarketStall = 20,
        Workshop = 30,
        Forge = 40,
        Farm = 50,
        MineOperatorFoundation = 60,
        InnFoundation = 70,
        TavernFoundation = 80,
        ServiceProvider = 90,
        TransportProviderFoundation = 100,
        TradingCompany = 110,
        ManufacturingBusiness = 120,
        GuildOwnedEnterprise = 130,
        GovernmentEnterpriseFoundation = 140,
        InformalSoleTrader = 150,
        Cooperative = 160,
        Partnership = 170,
        Custom = 1000
    }

    public enum BusinessOwnerSubjectKind
    {
        Unknown = 0,
        Person = 10,
        Organization = 20,
        Custom = 1000
    }

    public enum BusinessState
    {
        Invalid = 0,
        Planned = 10,
        Forming = 20,
        Active = 30,
        Suspended = 40,
        Dormant = 50,
        InsolventFoundation = 60,
        Closing = 70,
        Closed = 80,
        DissolvedFoundation = 90,
        SeizedFoundation = 100,
        Custom = 1000
    }

    public enum BusinessOwnershipCategory
    {
        Unknown = 0,
        SoleOwner = 10,
        Partner = 20,
        CooperativeMember = 30,
        OrganizationOwner = 40,
        Founder = 50,
        InvestorFoundation = 60,
        TrusteeFoundation = 70,
        StateOwnerFoundation = 80,
        Custom = 1000
    }

    public enum BusinessAuthorityKind
    {
        Unknown = 0,
        ViewBusinessState = 10,
        ManageAccounts = 20,
        SpendBusinessFunds = 30,
        ApproveMajorExpenses = 40,
        ManageInventory = 50,
        BuyStock = 60,
        SellStock = 70,
        StartProduction = 80,
        ApproveRecipes = 90,
        AssignTools = 100,
        HireEmployees = 110,
        ManagePositions = 120,
        ApprovePayroll = 130,
        OpenOrCloseEstablishments = 140,
        SuspendOperations = 150,
        CloseBusiness = 160,
        ViewPrivateRecords = 170,
        AuditBusiness = 180,
        Custom = 1000
    }

    public enum BusinessEstablishmentType
    {
        Unknown = 0,
        Shop = 10,
        Stall = 20,
        Workshop = 30,
        Forge = 40,
        Farm = 50,
        Warehouse = 60,
        Office = 70,
        Branch = 80,
        ProductionSite = 90,
        ServiceSite = 100,
        MobileEstablishmentFoundation = 110,
        Custom = 1000
    }

    public enum BusinessEstablishmentState
    {
        Unknown = 0,
        Planned = 10,
        Open = 20,
        Suspended = 30,
        Closed = 40,
        Custom = 1000
    }

    public enum BusinessAccountPurpose
    {
        Unknown = 0,
        OperatingFunds = 10,
        MerchantTill = 20,
        Payroll = 30,
        TaxReserveFoundation = 40,
        Capital = 50,
        SavingsFoundation = 60,
        EscrowFoundation = 70,
        PettyCashFoundation = 80,
        EstablishmentSpecificOperation = 90,
        Custom = 1000
    }

    public enum BusinessInventoryPurpose
    {
        Unknown = 0,
        RetailStock = 10,
        PurchaseIntake = 20,
        ProductionInput = 30,
        WorkInProgress = 40,
        FinishedGoods = 50,
        ToolsAndEquipment = 60,
        MaintenanceSupplies = 70,
        EmployeeIssuedEquipmentFoundation = 80,
        WasteOrSalvage = 90,
        ReservedOrdersFoundation = 100,
        Custom = 1000
    }

    public enum BusinessStockCategory
    {
        Unknown = 0,
        ForSale = 10,
        ProductionInput = 20,
        WorkInProgress = 30,
        FinishedProduct = 40,
        Tool = 50,
        Equipment = 60,
        Consumable = 70,
        MaintenanceSupply = 80,
        Damaged = 90,
        Returned = 100,
        Salvage = 110,
        Waste = 120,
        OwnerProvided = 130,
        ConsignedFoundation = 140,
        Custom = 1000
    }

    public enum ProductionOutputOwnerPolicy
    {
        Unknown = 0,
        BusinessOwnsOutputs = 10,
        CustomerOwnsSuppliedInputsAndOutput = 20,
        OrganizationOwnsOutputEstablishmentCustodies = 30,
        PartnerPercentageFoundation = 40,
        SponsorOwnsForSale = 50,
        ExplicitSubject = 60,
        Custom = 1000
    }

    public enum BusinessRevenueCategory
    {
        Unknown = 0,
        RetailSale = 10,
        WholesaleSaleFoundation = 20,
        ServiceIncome = 30,
        ProductionContractFoundation = 40,
        RentalIncomeFoundation = 50,
        CommissionFoundation = 60,
        MembershipIncomeFoundation = 70,
        SubsidyFoundation = 80,
        CapitalContributionExclusion = 90,
        RefundAdjustment = 100,
        Custom = 1000
    }

    public enum BusinessExpenseCategory
    {
        Unknown = 0,
        InventoryPurchase = 10,
        MaterialPurchase = 20,
        ToolPurchase = 30,
        PayrollExpense = 40,
        Reimbursement = 50,
        Maintenance = 60,
        RentFoundation = 70,
        UtilityFoundation = 80,
        TransportFoundation = 90,
        TaxFoundation = 100,
        Fee = 110,
        InterestFoundation = 120,
        LossOrSpoilage = 130,
        Refund = 140,
        OwnerWithdrawalExclusion = 150,
        Custom = 1000
    }

    public enum BusinessDistributionCategory
    {
        Unknown = 0,
        OwnerDraw = 10,
        ProfitDistribution = 20,
        ReturnOfCapitalFoundation = 30,
        AssetDistribution = 40,
        PartnershipDistribution = 50,
        Custom = 1000
    }

    public enum AccountingPeriodState
    {
        Unknown = 0,
        Open = 10,
        Closing = 20,
        Closed = 30,
        Corrected = 40,
        Superseded = 50,
        Invalid = 60,
        Custom = 1000
    }

    public enum BusinessOperationCode
    {
        Succeeded = 0,
        Preview = 1,
        InvalidRequest = 10,
        MissingDefinition = 20,
        MissingBusiness = 30,
        MissingAuthority = 40,
        MissingExternalReference = 50,
        Duplicate = 60,
        InvalidState = 70,
        PolicyViolation = 80,
        CurrencyMismatch = 90,
        CalculationMismatch = 100,
        ValidationFailed = 110,
        RestoreFailed = 120
    }

    public enum BusinessProjectionKind
    {
        Public = 0,
        Owner = 10,
        Controller = 20,
        Manager = 30,
        Employee = 40,
        AccountantFoundation = 50,
        AuditorFoundation = 60,
        TradeParticipant = 70,
        PrivilegedDebug = 1000
    }
}
