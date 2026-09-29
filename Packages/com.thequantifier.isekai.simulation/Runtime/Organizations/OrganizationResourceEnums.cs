namespace UnityIsekaiGame.Organizations
{
    public enum OrganizationResourceCategory
    {
        Unknown = 0,
        Currency = 10,
        ItemInventory = 20,
        Equipment = 30,
        ConsumableSupplies = 40,
        RawMaterials = 50,
        FinishedGoods = 60,
        Property = 70,
        Land = 80,
        Building = 90,
        Facility = 100,
        BusinessInterest = 110,
        ContractualRight = 120,
        Receivable = 130,
        Obligation = 140,
        RestrictedFund = 150,
        ReservedResource = 160,
        CustodiedAsset = 170,
        Custom = 1000
    }

    public enum OrganizationAssetReferenceKind
    {
        Unknown = 0,
        Treasury = 10,
        Account = 20,
        CurrencyBalance = 30,
        Inventory = 40,
        ItemInstance = 50,
        Property = 60,
        Building = 70,
        LandParcel = 80,
        Business = 90,
        Contract = 100,
        Loan = 110,
        Receivable = 120,
        Obligation = 130,
        Custom = 1000
    }

    public enum OrganizationTreasuryCategory
    {
        Unknown = 0,
        GeneralTreasury = 10,
        BranchTreasury = 20,
        OperatingTreasury = 30,
        ReserveTreasury = 40,
        EmergencyFund = 50,
        RestrictedFundTreasury = 60,
        ProjectTreasury = 70,
        TrustOrCustodyTreasury = 80,
        PettyCashTreasury = 90,
        Custom = 1000
    }

    public enum OrganizationTreasuryLifecycleState
    {
        Unknown = 0,
        Active = 10,
        Frozen = 20,
        Closed = 30,
        Archived = 40
    }

    public enum OrganizationAccountCategory
    {
        Unknown = 0,
        GeneralOperating = 10,
        Revenue = 20,
        Payroll = 30,
        Reserve = 40,
        Emergency = 50,
        Restricted = 60,
        Earmarked = 70,
        EscrowOrCustody = 80,
        PettyCash = 90,
        BranchOperating = 100,
        Project = 110,
        DebtService = 120,
        Custom = 1000
    }

    public enum OrganizationAccountLifecycleState
    {
        Unknown = 0,
        Proposed = 5,
        Active = 10,
        Frozen = 20,
        Suspended = 30,
        Closed = 40,
        Archived = 50,
        Invalid = 60
    }

    public enum OrganizationFundRestrictionLifecycleState
    {
        Unknown = 0,
        Proposed = 5,
        Active = 10,
        Satisfied = 20,
        Released = 30,
        Expired = 40,
        Revoked = 50,
        Historical = 60
    }

    public enum OrganizationBudgetCategory
    {
        Unknown = 0,
        GeneralOperations = 10,
        Payroll = 20,
        Procurement = 30,
        Maintenance = 40,
        Construction = 50,
        Emergency = 60,
        Research = 70,
        CharitableActivity = 80,
        Security = 90,
        BranchAllocation = 100,
        DebtService = 110,
        Custom = 1000
    }

    public enum OrganizationBudgetEnforcementPolicy
    {
        Unknown = 0,
        InformationalOnly = 10,
        WarnWhenExceeded = 20,
        HardMaximum = 30,
        RequiresAdditionalAuthorization = 40,
        RestrictedToPurpose = 50,
        Custom = 1000
    }

    public enum OrganizationBudgetLifecycleState
    {
        Unknown = 0,
        Proposed = 5,
        Active = 10,
        Exhausted = 20,
        Closed = 30,
        Expired = 40,
        Historical = 50
    }

    public enum OrganizationReservationCategory
    {
        Unknown = 0,
        General = 10,
        Budget = 20,
        Procurement = 30,
        Payroll = 40,
        Contract = 50,
        Loan = 60,
        Obligation = 70,
        ItemCheckout = 80,
        Custom = 1000
    }

    public enum OrganizationReservationLifecycleState
    {
        Unknown = 0,
        Proposed = 5,
        Active = 10,
        Consumed = 20,
        Released = 30,
        Expired = 40,
        Cancelled = 50,
        Historical = 60
    }

    public enum OrganizationInventoryCategory
    {
        Unknown = 0,
        GeneralStores = 10,
        Armory = 20,
        Warehouse = 30,
        WorkshopMaterials = 40,
        FinishedGoods = 50,
        OfficeSupplies = 60,
        MedicalSupplies = 70,
        FoodStores = 80,
        RestrictedArchive = 90,
        EvidenceStorage = 100,
        BranchInventory = 110,
        Custom = 1000
    }

    public enum OrganizationPropertyAssociationCategory
    {
        Unknown = 0,
        Owner = 10,
        CoOwner = 20,
        Tenant = 30,
        Lessee = 40,
        Operator = 50,
        Administrator = 60,
        Custodian = 70,
        Maintainer = 80,
        Beneficiary = 90,
        LicenseHolder = 100,
        Occupant = 110,
        Headquarters = 120,
        BranchLocation = 130,
        StorageFacility = 140,
        ProductionFacility = 150,
        Custom = 1000
    }

    public enum OrganizationBusinessAssociationCategory
    {
        Unknown = 0,
        Owner = 10,
        PartialOwner = 20,
        Operator = 30,
        Beneficiary = 40,
        ParentInstitution = 50,
        Custom = 1000
    }

    public enum OrganizationCustodyLifecycleState
    {
        Unknown = 0,
        Proposed = 5,
        CheckedOut = 10,
        InCustody = 20,
        Returned = 30,
        Lost = 40,
        Damaged = 50,
        Transferred = 60,
        Overdue = 70,
        Historical = 80
    }

    public enum OrganizationRevenueRoutingLifecycleState
    {
        Unknown = 0,
        Proposed = 5,
        Active = 10,
        Suspended = 20,
        Ended = 30,
        Expired = 40,
        Historical = 50
    }

    public enum OrganizationResourceOperationCode
    {
        Success = 0,
        Preview = 1,
        Duplicate = 2,
        InvalidRequest = 10,
        MissingDependency = 20,
        MissingOrganization = 21,
        MissingTreasury = 22,
        MissingAccount = 23,
        MissingResource = 24,
        MissingCurrency = 25,
        MissingRestriction = 26,
        MissingBudget = 27,
        MissingReservation = 28,
        MissingAssociation = 29,
        MissingCustody = 30,
        Unauthorized = 40,
        FinanciallyInvalid = 41,
        InsufficientFunds = 42,
        AccountFrozen = 43,
        AccountClosed = 44,
        RestrictionMismatch = 45,
        BudgetExceeded = 46,
        ReservationUnavailable = 47,
        InvalidLifecycle = 48,
        CrossWorldReference = 49,
        ValidationFailed = 60,
        ReconciliationFailed = 61,
        PersistenceInvalid = 70,
        RestoreFailed = 71,
        Disposed = 80
    }

    public enum OrganizationResourceProjectionAccess
    {
        Denied = 0,
        Concealed = 10,
        Redacted = 20,
        Full = 30
    }

    public enum OrganizationReconciliationSeverity
    {
        Information = 0,
        Warning = 10,
        Error = 20
    }

    public enum OrganizationLiabilitySourceKind
    {
        Unknown = 0,
        ContractObligation = 10,
        Loan = 20,
        PayrollObligation = 30,
        WageDebt = 40
    }

    public enum OrganizationDissolutionAssetInstructionKind
    {
        Unknown = 0,
        PreserveUnresolved = 10,
        TransferToOrganization = 20,
        TransferToAccount = 30
    }

    public enum OrganizationDissolutionPlanLifecycleState
    {
        Proposed = 0,
        Approved = 10,
        Executed = 20,
        Cancelled = 30
    }
}
