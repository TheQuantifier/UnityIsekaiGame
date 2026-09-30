namespace UnityIsekaiGame.Economy.Trading
{
    public enum TradePolicyCategory
    {
        Unknown,
        FixedPriceRetail,
        MerchantPurchase,
        DirectPersonToPerson,
        Barter,
        OrganizationProcurementFoundation,
        OrganizationSaleFoundation,
        AuctionSettlementFoundation,
        Custom
    }

    public enum TradeParticipantKind
    {
        Unknown,
        Person,
        Organization,
        BusinessFoundation,
        AuthorizedRepresentative,
        Custom
    }

    public enum TradeParticipantRole
    {
        Unknown,
        Buyer,
        Seller,
        Trader,
        Merchant,
        OrganizationRepresentative,
        BrokerFoundation,
        ObserverFoundation,
        Custom
    }

    public enum TradeSessionState
    {
        Unknown,
        Proposed,
        Open,
        AwaitingResponse,
        AcceptedPendingExecution,
        Executing,
        Completed,
        Rejected,
        Withdrawn,
        Expired,
        Cancelled,
        Failed,
        DisputedFoundation,
        Invalid,
        Custom
    }

    public enum TradeOfferState
    {
        Unknown,
        DraftFoundation,
        Submitted,
        UnderConsideration,
        Accepted,
        Rejected,
        Withdrawn,
        Superseded,
        Expired,
        Invalid,
        Custom
    }

    public enum TradeAssetKind
    {
        Unknown,
        ItemInstance,
        StackQuantity,
        MultipleItemInstances,
        Money,
        PhysicalCurrency,
        ServiceFoundation,
        AccessRightFoundation,
        PropertyTransferFoundation,
        ContractObligationFoundation,
        CustomSubject
    }

    public enum TradeReservationPolicyKind
    {
        Unknown,
        None,
        ReserveOnSubmit,
        ReserveOnAccept,
        ReserveBeforeExecution
    }

    public enum TradeValuationPolicyKind
    {
        Unknown,
        MarketReference,
        MerchantQuote,
        ParticipantRelative,
        FixedPrice,
        Custom
    }

    public enum TradeDisclosureState
    {
        Unknown,
        Public,
        ParticipantOnly,
        Claimed,
        Verified,
        Withheld,
        Confidential,
        Redacted
    }

    public enum TradeValuationClassification
    {
        Unknown,
        StronglyFavorable,
        Favorable,
        ApproximatelyBalanced,
        Unfavorable,
        StronglyUnfavorable,
        Incomparable,
        Custom
    }

    public enum TradeOperationCode
    {
        Success,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingPolicy,
        MissingSession,
        MissingParticipant,
        MissingOffer,
        MissingAsset,
        MissingRuntime,
        MissingAccount,
        MissingItem,
        MissingQuote,
        Unauthorized,
        InvalidState,
        InvalidTransition,
        InvalidAsset,
        InvalidQuantity,
        CurrencyMismatch,
        InsufficientFunds,
        ReservationUnavailable,
        StaleItem,
        StaleAccount,
        StaleQuote,
        PolicyViolation,
        ValidationFailed,
        ExecutionFailed,
        RestoreFailed,
        Redacted,
        Denied
    }
}
