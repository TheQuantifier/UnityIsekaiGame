namespace UnityIsekaiGame.Inventory.Identity
{
    public enum ItemInstanceClassification
    {
        Unknown,
        IndividuallyTracked,
        Fungible,
        StackableWhileEquivalent,
        BatchTracked,
        Unique,
        Serialized,
        WorldFixture,
        Virtual
    }

    public enum ItemLifecycleState
    {
        Created,
        Active,
        Stored,
        InInventory,
        Equipped,
        PlacedInWorld,
        InTransit,
        Reserved,
        Lost,
        Missing,
        StolenOrDisputed,
        Destroyed,
        Consumed,
        Depleted,
        Broken,
        Disassembled,
        Archived,
        Quarantined
    }

    public enum ItemLocationKind
    {
        Unassigned,
        Container,
        Inventory,
        Equipped,
        WorldPlacement,
        Transit,
        Reserved,
        ProductionReserved,
        Destroyed,
        Consumed
    }

    public enum ItemOwnershipKind
    {
        Unknown,
        Unowned,
        PersonOwned,
        OrganizationOwned,
        Shared,
        Disputed,
        Public,
        Communal,
        CustodialOnly,
        Custom
    }

    public enum ItemConditionState
    {
        Unknown,
        Pristine,
        Excellent,
        Good,
        Used,
        Worn,
        Damaged,
        SeverelyDamaged,
        Broken,
        Destroyed,
        Custom
    }

    public enum ItemQualityTier
    {
        Unknown,
        Poor,
        Common,
        Good,
        Fine,
        Excellent,
        Masterwork,
        Legendary,
        Custom
    }

    public enum ItemQualitySource
    {
        Unknown,
        Authored,
        Produced,
        Inherited,
        Generated,
        Appraised,
        Imported,
        Custom
    }

    public enum ItemAuthenticityStatus
    {
        Unknown,
        Authentic,
        Attributed,
        Questioned,
        Disputed,
        Forged,
        Redacted
    }

    public enum ItemAttributionStatus
    {
        Unknown,
        Unattributed,
        Claimed,
        Verified,
        Disputed,
        False,
        Redacted
    }

    public enum ItemProjectionAudience
    {
        AuthoritativeInternal,
        PrivilegedDebug,
        CurrentOwner,
        CurrentCustodian,
        PublicInspection,
        PersonKnown,
        PersonBelieved,
        PersonRecorded,
        InventoryUi,
        EquipmentUi,
        WorldInteractionPrompt,
        MerchantAppraisal,
        CraftingSelection
    }

    public enum ItemInstanceOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        MissingDefinition,
        MissingItem,
        DuplicateItemInstanceId,
        InvalidRequest,
        InvalidState,
        InvalidTransition,
        InvalidLocation,
        InvalidOwnership,
        InvalidCondition,
        InvalidQuality,
        ValidationFailed,
        RestoreFailed
    }
}
