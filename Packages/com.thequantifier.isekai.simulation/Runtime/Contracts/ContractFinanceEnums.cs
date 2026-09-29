namespace UnityIsekaiGame.Contracts
{
    public enum EconomicContractCategory
    {
        General = 0,
        Sale = 100,
        Service = 200,
        Rental = 300,
        EmploymentReference = 400,
        Credit = 500,
        Loan = 600,
        Guarantee = 700,
        Collateral = 800,
        Custom = 9000
    }

    public enum ContractPartyKind
    {
        Person = 0,
        Organization = 100,
        Business = 200,
        Property = 300,
        Position = 400,
        Institution = 500,
        System = 600,
        Custom = 9000
    }

    public enum ContractPartyRole
    {
        Unknown = 0,
        Offeror = 100,
        Offeree = 200,
        Debtor = 300,
        Creditor = 400,
        Lender = 500,
        Borrower = 600,
        Guarantor = 700,
        CollateralProvider = 800,
        Witness = 900,
        Administrator = 1000,
        Custom = 9000
    }

    public enum EconomicContractState
    {
        Draft = 0,
        Proposed = 100,
        Active = 200,
        Suspended = 300,
        Fulfilled = 400,
        Breached = 500,
        Defaulted = 600,
        Cancelled = 700,
        Superseded = 800,
        Expired = 900
    }

    public enum ContractProposalState
    {
        Draft = 0,
        Offered = 100,
        Accepted = 200,
        Rejected = 300,
        Withdrawn = 400,
        Expired = 500,
        Activated = 600
    }

    public enum ContractTermCategory
    {
        General = 0,
        Payment = 100,
        Delivery = 200,
        Service = 300,
        Rent = 400,
        Repayment = 500,
        Collateral = 600,
        Guarantee = 700,
        Access = 800,
        Custom = 9000
    }

    public enum ContractObligationCategory
    {
        MonetaryPayment = 0,
        GoodsDelivery = 100,
        ServicePerformance = 200,
        RentPayment = 300,
        LoanRepayment = 400,
        InterestPayment = 500,
        CollateralMaintenance = 600,
        GuaranteeSupport = 700,
        Custom = 9000
    }

    public enum ContractObligationState
    {
        Pending = 0,
        PartiallySatisfied = 100,
        Satisfied = 200,
        Late = 300,
        Defaulted = 400,
        Waived = 500,
        Cancelled = 600,
        Superseded = 700
    }

    public enum ContractPerformanceState
    {
        Submitted = 0,
        Accepted = 100,
        Rejected = 200,
        Disputed = 300,
        Corrected = 400
    }

    public enum ContractAmendmentState
    {
        Proposed = 0,
        Accepted = 100,
        Rejected = 200,
        Withdrawn = 300,
        Superseded = 400
    }

    public enum CreditAgreementState
    {
        Draft = 0,
        Active = 100,
        Suspended = 200,
        Closed = 300,
        Defaulted = 400,
        Cancelled = 500
    }

    public enum LoanState
    {
        Draft = 0,
        Approved = 100,
        Disbursed = 200,
        Current = 300,
        Delinquent = 400,
        Defaulted = 500,
        Cured = 600,
        Restructured = 700,
        PaidOff = 800,
        Cancelled = 900
    }

    public enum LoanInstallmentState
    {
        Scheduled = 0,
        PartiallyPaid = 100,
        Paid = 200,
        Late = 300,
        Defaulted = 400,
        Waived = 500,
        Superseded = 600
    }

    public enum CollateralAssetKind
    {
        ItemInstance = 0,
        Property = 100,
        BusinessAsset = 200,
        AccountReserve = 300,
        Custom = 9000
    }

    public enum CollateralState
    {
        Proposed = 0,
        Pledged = 100,
        Released = 200,
        Replaced = 300,
        Disputed = 400,
        ForfeitureEligible = 500
    }

    public enum ContractRoundingMode
    {
        Floor = 0,
        Ceiling = 100,
        Nearest = 200
    }

    public enum ContractOperationCode
    {
        Succeeded = 0,
        Preview = 100,
        Duplicate = 200,
        InvalidRequest = 300,
        MissingDefinition = 400,
        MissingContract = 500,
        MissingProposal = 600,
        MissingObligation = 700,
        MissingAccount = 800,
        MissingLoan = 900,
        InvalidState = 1000,
        CurrencyMismatch = 1100,
        InsufficientFunds = 1200,
        PersistenceRejected = 1300,
        RolledBack = 1400,
        AccessDenied = 1500,
        ArithmeticOverflow = 1600
    }
}
