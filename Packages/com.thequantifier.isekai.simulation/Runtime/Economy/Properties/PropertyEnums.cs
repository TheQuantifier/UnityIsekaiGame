namespace UnityIsekaiGame.Economy.Properties
{
    public enum PropertyCategory
    {
        Unknown = 0,
        LandParcel = 10,
        AgriculturalField = 20,
        ResidentialBuilding = 30,
        CommercialBuilding = 40,
        IndustrialBuilding = 50,
        PublicBuilding = 60,
        House = 70,
        ApartmentBuilding = 80,
        ApartmentUnit = 90,
        Room = 100,
        ShopPremises = 110,
        MarketStallLocation = 120,
        Workshop = 130,
        Forge = 140,
        Warehouse = 150,
        Farm = 160,
        MineSiteFoundation = 170,
        Office = 180,
        InnRoomFoundation = 190,
        StorageUnit = 200,
        SharedFacility = 210,
        Custom = 1000
    }

    public enum PropertyState
    {
        Unknown = 0,
        Planned = 10,
        Available = 20,
        Occupied = 30,
        Vacant = 40,
        Restricted = 50,
        UnderMaintenance = 60,
        Damaged = 70,
        UninhabitableFoundation = 80,
        CondemnedFoundation = 90,
        Closed = 100,
        DestroyedFoundation = 110,
        Disputed = 120,
        Invalid = 130,
        Custom = 1000
    }

    public enum PropertyOwnershipModel
    {
        Unknown = 0,
        Sole = 10,
        SharedFractional = 20,
        JointFoundation = 30,
        Organization = 40,
        Business = 50,
        InstitutionalFoundation = 60,
        GovernmentFoundation = 70,
        TrustOrCustodialFoundation = 80,
        Unowned = 90,
        Disputed = 100,
        Custom = 1000
    }

    public enum PropertySubjectKind
    {
        Unknown = 0,
        Person = 10,
        Organization = 20,
        Business = 30,
        Position = 40,
        InstitutionFoundation = 50,
        GovernmentFoundation = 60,
        System = 70,
        Custom = 1000
    }

    public enum PropertyTransferCategory
    {
        Unknown = 0,
        Sale = 10,
        Gift = 20,
        OwnershipShareTransfer = 30,
        BusinessContribution = 40,
        BusinessDistribution = 50,
        OrganizationTransfer = 60,
        Inheritance = 70,
        CourtOrGovernmentFoundation = 80,
        SeizureFoundation = 90,
        Correction = 100,
        Custom = 1000
    }

    public enum PropertyRecordCategory
    {
        Unknown = 0,
        Deed = 10,
        TitleCertificate = 20,
        TransferRecord = 30,
        GiftRecord = 40,
        InheritanceRecord = 50,
        OccupancyRecord = 60,
        TenancyRecord = 70,
        RentReceipt = 80,
        AccessRightRecord = 90,
        InspectionRecord = 100,
        MaintenanceRecord = 110,
        ConditionReport = 120,
        Custom = 1000
    }

    public enum PossessionCategory
    {
        Unknown = 0,
        OwnerPossession = 10,
        TenantPossession = 20,
        BusinessPossession = 30,
        CustodialPossession = 40,
        TemporaryPossession = 50,
        InstitutionalControl = 60,
        DisputedPossession = 70,
        Custom = 1000
    }

    public enum OccupancyCategory
    {
        Unknown = 0,
        Residence = 10,
        BusinessOperation = 20,
        Storage = 30,
        Production = 40,
        AgriculturalUse = 50,
        TemporaryLodgingFoundation = 60,
        InstitutionalUse = 70,
        EmploymentProvidedUse = 80,
        GuestUse = 90,
        Custom = 1000
    }

    public enum PropertyUseCategory
    {
        Unknown = 0,
        Residential = 10,
        Commercial = 20,
        Industrial = 30,
        Agricultural = 40,
        Storage = 50,
        Production = 60,
        Retail = 70,
        Office = 80,
        Public = 90,
        ReligiousFoundation = 100,
        MilitaryFoundation = 110,
        Educational = 120,
        MedicalFoundation = 130,
        MixedUse = 140,
        Vacant = 150,
        Custom = 1000
    }

    public enum TenancyModel
    {
        Unknown = 0,
        None = 10,
        FixedTerm = 20,
        OpenEnded = 30,
        LicenseFoundation = 40,
        LodgingFoundation = 50,
        BusinessLeaseFoundation = 60,
        Custom = 1000
    }

    public enum TenancyState
    {
        Unknown = 0,
        Proposed = 10,
        Active = 20,
        Suspended = 30,
        Ending = 40,
        Ended = 50,
        Expired = 60,
        Terminated = 70,
        BreachedFoundation = 80,
        Disputed = 90,
        Invalid = 100,
        Custom = 1000
    }

    public enum PropertyAccessCategory
    {
        Unknown = 0,
        Enter = 10,
        Occupy = 20,
        Reside = 30,
        OperateBusiness = 40,
        StoreItems = 50,
        UseEquipment = 60,
        ProduceGoods = 70,
        Inspect = 80,
        Maintain = 90,
        Manage = 100,
        AdmitGuestsFoundation = 110,
        ExcludeOthersFoundation = 120,
        CollectRent = 130,
        TransferProperty = 140,
        PublicView = 150,
        Custom = 1000
    }

    public enum PropertyAccessDecision
    {
        Unknown = 0,
        Allowed = 10,
        Denied = 20,
        Redacted = 30,
        MissingAuthority = 40,
        Expired = 50,
        Revoked = 60,
        Restricted = 70
    }

    public enum RentObligationState
    {
        Unknown = 0,
        Open = 10,
        PartiallyPaid = 20,
        Paid = 30,
        Overdue = 40,
        Cancelled = 50,
        Disputed = 60
    }

    public enum PropertyConditionState
    {
        Unknown = 0,
        Excellent = 10,
        Good = 20,
        Worn = 30,
        Damaged = 40,
        Unsafe = 50,
        Uninhabitable = 60,
        DestroyedFoundation = 70
    }

    public enum MaintenanceObligationState
    {
        Unknown = 0,
        Proposed = 10,
        Required = 20,
        InProgress = 30,
        Completed = 40,
        Failed = 50,
        Cancelled = 60
    }

    public enum PropertyProjectionKind
    {
        Public = 0,
        Owner = 10,
        Tenant = 20,
        Business = 30,
        Privileged = 100
    }

    public enum PropertyOperationCode
    {
        Success = 0,
        Preview = 1,
        Duplicate = 2,
        InvalidRequest = 10,
        MissingDefinition = 20,
        MissingProperty = 21,
        MissingOwnership = 22,
        MissingTitle = 23,
        MissingTenancy = 24,
        MissingAccessRight = 25,
        MissingRent = 26,
        MissingMaintenance = 27,
        MissingExternalReference = 28,
        InvalidHierarchy = 30,
        InvalidShare = 31,
        InvalidState = 32,
        PolicyViolation = 40,
        MissingAuthority = 50,
        AccessDenied = 51,
        PaymentFailed = 60,
        TransferFailed = 70,
        MaintenanceFailed = 80,
        ValidationFailed = 90,
        RestoreFailed = 100
    }
}
