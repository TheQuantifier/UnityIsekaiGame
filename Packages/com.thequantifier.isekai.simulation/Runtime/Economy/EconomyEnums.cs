namespace UnityIsekaiGame.Economy
{
    public enum EconomyAccountKind
    {
        Unknown = 0,
        PersonWallet = 10,
        OrganizationAccount = 20,
        ContainerWallet = 30,
        SystemTreasury = 40
    }

    public enum EconomyAccountState
    {
        Unknown = 0,
        Active = 10,
        Frozen = 20,
        Closed = 30
    }

    public enum EconomyReservationState
    {
        Unknown = 0,
        Active = 10,
        Released = 20,
        Committed = 30,
        Expired = 40
    }

    public enum EconomyTransactionKind
    {
        Unknown = 0,
        Issuance = 10,
        Destruction = 20,
        Transfer = 30,
        Payment = 40,
        Refund = 50,
        Reversal = 60,
        PhysicalToAbstract = 70,
        AbstractToPhysical = 80
    }

    public enum EconomyTransactionState
    {
        Unknown = 0,
        Preview = 10,
        Committed = 20,
        Refunded = 30,
        Reversed = 40,
        Rejected = 50
    }

    public enum EconomyLedgerEntryKind
    {
        Debit = 0,
        Credit = 1
    }

    public enum EconomyResultCode
    {
        Success = 0,
        Preview = 1,
        Duplicate = 2,
        InvalidRequest = 10,
        MissingCurrency = 20,
        MissingAccount = 21,
        MissingTransaction = 22,
        MissingReservation = 23,
        MissingItemInstance = 24,
        AccountClosed = 30,
        AccountFrozen = 31,
        CurrencyMismatch = 32,
        InsufficientFunds = 33,
        ReservationUnavailable = 34,
        NonConservingTransaction = 35,
        ValidationFailed = 40,
        RestoreFailed = 50,
        AccessDenied = 60
    }
}
