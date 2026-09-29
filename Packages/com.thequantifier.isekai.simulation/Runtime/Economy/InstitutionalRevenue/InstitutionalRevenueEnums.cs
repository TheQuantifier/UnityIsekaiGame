namespace UnityIsekaiGame.Economy.InstitutionalRevenue
{
    public enum InstitutionalRevenueCategory
    {
        Unknown = 0,
        TransactionTax = 10,
        SalesTaxFoundation = 11,
        PurchaseTaxFoundation = 12,
        PayrollTax = 20,
        EmployerContribution = 21,
        EmployeeContribution = 22,
        IncomeTaxFoundation = 23,
        BusinessRevenueTax = 30,
        BusinessProfitTax = 31,
        ProductionTaxFoundation = 32,
        PropertyTax = 40,
        LandTax = 41,
        ImportTariff = 50,
        ExportTariff = 51,
        CustomsFee = 52,
        Toll = 60,
        GateFee = 61,
        RoadUseFee = 62,
        MarketStallFee = 70,
        LicenseFee = 80,
        PermitFee = 81,
        RegistrationFee = 82,
        FilingFee = 83,
        InspectionFee = 84,
        ServiceFee = 85,
        MembershipLevyFoundation = 90,
        GuildDueFoundation = 91,
        Fine = 100,
        LateFee = 101,
        AdministrativePenalty = 102,
        InstitutionalContribution = 110,
        Custom = 1000
    }

    public enum InstitutionKind
    {
        Unknown = 0,
        GovernmentFoundation = 10,
        Faction = 20,
        Guild = 30,
        Organization = 40,
        SettlementFoundation = 50,
        ReligiousInstitutionFoundation = 60,
        MilitaryInstitutionFoundation = 70,
        Business = 80,
        CourtFoundation = 90,
        Custom = 1000
    }

    public enum InstitutionalRevenueAuthorityCategory
    {
        Unknown = 0,
        DefineChargeFoundation = 10,
        Assess = 20,
        ApproveAssessment = 30,
        Collect = 40,
        ReceiveRemittance = 50,
        IssueRefund = 60,
        Waive = 70,
        Adjust = 80,
        Audit = 90,
        RecordViolationDerivedCharge = 100,
        AllocateRevenue = 110,
        Custom = 1000
    }

    public enum RevenueAccountPurpose
    {
        Unknown = 0,
        GeneralTreasury = 10,
        TaxCollection = 20,
        PayrollContributionCollection = 30,
        CustomsCollection = 40,
        TollCollection = 50,
        LicenseRevenue = 60,
        FineCollection = 70,
        RefundFunding = 80,
        EscrowFoundation = 90,
        RevenueDistribution = 100,
        Custom = 1000
    }

    public enum RevenueSubjectKind
    {
        Unknown = 0,
        Person = 10,
        Organization = 20,
        Business = 30,
        Employment = 40,
        Employee = 41,
        Employer = 42,
        TradeParticipant = 50,
        Buyer = 51,
        Seller = 52,
        PropertyOwner = 60,
        Tenant = 61,
        AccountHolder = 70,
        ContractParty = 80,
        Borrower = 81,
        Lender = 82,
        ItemOwner = 90,
        ImporterFoundation = 100,
        ExporterFoundation = 101,
        TravellerFoundation = 110,
        LicenseApplicant = 120,
        LicenseHolder = 121,
        ViolationSubjectFoundation = 130,
        Institution = 140,
        Custom = 1000
    }

    public enum RevenueSubjectRole
    {
        Unknown = 0,
        AssessedParty = 10,
        EconomicBearer = 20,
        Payer = 30,
        WithholdingAgent = 40,
        RemittingParty = 50,
        ReceivingInstitution = 60,
        ReportingParty = 70,
        Beneficiary = 80,
        Custom = 1000
    }

    public enum TaxableEventCategory
    {
        Unknown = 0,
        CompletedTrade = 10,
        CommittedTransaction = 20,
        PayrollCalculation = 30,
        PayrollPayment = 31,
        BusinessRevenueRecognition = 40,
        BusinessProfitStatement = 41,
        PropertyValuation = 50,
        PropertyOwnershipPeriod = 51,
        PropertyTransfer = 52,
        RentPaymentFoundation = 53,
        ProductionOutput = 60,
        ItemImportFoundation = 70,
        ItemExportFoundation = 71,
        RouteUseFoundation = 80,
        FacilityUseFoundation = 81,
        LicenseApplication = 90,
        LicenseRenewal = 91,
        AdministrativeServiceRequest = 100,
        ExternalFineDecision = 110,
        Custom = 1000
    }

    public enum TaxBaseKind
    {
        Unknown = 0,
        FixedAmount = 10,
        TransactionGrossAmount = 20,
        TransactionNetAmount = 21,
        ItemPrice = 30,
        TotalTradeConsideration = 31,
        PayrollGrossPay = 40,
        EmployeeNetPayFoundation = 41,
        EmployerPayrollCost = 42,
        BusinessRevenue = 50,
        BusinessProfit = 51,
        PropertyAssessedValue = 60,
        PropertyOwnershipShare = 61,
        RentAmountFoundation = 62,
        LoanPrincipalFoundation = 70,
        InterestAmountFoundation = 71,
        ProductionQuantity = 80,
        ItemQuantity = 81,
        ItemWeightFoundation = 82,
        ItemMarketValue = 83,
        LandAreaFoundation = 90,
        BuildingCategory = 91,
        RouteUsageCount = 100,
        DistanceTravelledFoundation = 101,
        LicenseDuration = 110,
        AdministrativeServiceOccurrence = 120,
        ExternalFineAmount = 130,
        CustomExactQuantity = 1000
    }

    public enum RevenueRateKind
    {
        Unknown = 0,
        FixedAmount = 10,
        FlatProportional = 20,
        PerUnit = 30,
        ProgressiveBracket = 40,
        ThresholdCharge = 50,
        TieredFixedCharge = 60,
        MinimumCharge = 70,
        MaximumCharge = 80,
        CappedProportionalCharge = 90,
        PercentagePlusFixedAmount = 100,
        ValueBand = 110,
        QuantityBand = 120,
        Custom = 1000
    }

    public enum ProgressiveCalculationKind
    {
        Unknown = 0,
        Marginal = 10,
        WholeBase = 20,
        TieredFixed = 30,
        ThresholdTriggered = 40,
        Custom = 1000
    }

    public enum RevenueRoundingMode
    {
        Down = 0,
        Up = 1,
        ToNearest = 2
    }

    public enum AssessmentPeriodKind
    {
        Unknown = 0,
        PerEvent = 10,
        PerTransaction = 11,
        PerPayrollPeriod = 20,
        Daily = 30,
        Weekly = 31,
        FixedDays = 32,
        MonthlyFoundation = 40,
        AnnualFoundation = 50,
        PropertyOwnershipPeriod = 60,
        LicenseTerm = 70,
        FilingPeriod = 80,
        Custom = 1000
    }

    public enum AssessmentPeriodState
    {
        Unknown = 0,
        Open = 10,
        AwaitingFilingFoundation = 20,
        ReadyForAssessment = 30,
        Assessed = 40,
        Due = 50,
        PartiallyPaid = 60,
        Paid = 70,
        Overdue = 80,
        Corrected = 90,
        Closed = 100,
        Disputed = 110,
        Invalid = 120,
        Custom = 1000
    }

    public enum RevenueAssessmentState
    {
        Unknown = 0,
        DraftFoundation = 10,
        Calculated = 20,
        Approved = 30,
        Issued = 40,
        Due = 50,
        PartiallyPaid = 60,
        Paid = 70,
        Overdue = 80,
        Corrected = 90,
        Replaced = 100,
        Waived = 110,
        Cancelled = 120,
        Disputed = 130,
        Invalid = 140,
        Custom = 1000
    }

    public enum InstitutionalObligationState
    {
        Unknown = 0,
        Pending = 10,
        Due = 20,
        PartiallyPaid = 30,
        Paid = 40,
        Overdue = 50,
        Waived = 60,
        Refunded = 70,
        Cancelled = 80,
        Custom = 1000
    }

    public enum WithholdingState
    {
        Unknown = 0,
        Withheld = 10,
        PartiallyRemitted = 20,
        Remitted = 30,
        Refunded = 40,
        Cancelled = 50
    }

    public enum RevenueFilingState
    {
        Unknown = 0,
        Draft = 10,
        Submitted = 20,
        Corrected = 30,
        Superseded = 40,
        Rejected = 50
    }

    public enum RevenueAuditFindingKind
    {
        Unknown = 0,
        Match = 10,
        MissingEventFoundation = 20,
        DuplicateEventFoundation = 30,
        AmountMismatchFoundation = 40,
        UnsupportedClaim = 50,
        Custom = 1000
    }

    public enum RevenueOperationCode
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        InvalidRequest = 10,
        MissingDefinition = 20,
        MissingAuthority = 21,
        MissingAccount = 22,
        MissingEvent = 23,
        MissingAssessment = 24,
        MissingObligation = 25,
        MissingWithholding = 26,
        MissingRevenueRecord = 27,
        MissingCurrency = 28,
        CurrencyMismatch = 30,
        Unauthorized = 40,
        Immutable = 50,
        AlreadyAssessed = 60,
        InsufficientFunds = 70,
        OverpaymentRejected = 80,
        ArithmeticOverflow = 90,
        RolledBack = 100,
        PersistenceRejected = 110,
        AccessDenied = 120
    }
}
