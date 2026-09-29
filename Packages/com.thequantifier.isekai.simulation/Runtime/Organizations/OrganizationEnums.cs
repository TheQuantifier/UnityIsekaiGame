namespace UnityIsekaiGame.Organizations
{
    public enum OrganizationCategory
    {
        Unknown,
        Guild,
        Company,
        Institution,
        ReligiousOrder,
        MilitaryOrder,
        CivicBody,
        SecretSociety,
        CriminalOrganization,
        Branch,
        Household,
        Government,
        Custom
    }

    public enum OrganizationLifecycleState
    {
        Unknown,
        Forming,
        Active,
        Dormant,
        Dissolved,
        Archived
    }

    public enum OrganizationVisibility
    {
        Public,
        Restricted,
        Secret,
        Hidden
    }

    public enum OrganizationNameCategory
    {
        Official,
        FormerOfficial,
        Alias,
        SecretAlias,
        Abbreviation
    }

    public enum OrganizationFounderKind
    {
        Unknown,
        Person,
        Organization,
        Collective,
        ScriptedWorldSetup
    }

    public enum OrganizationReferenceKind
    {
        Unknown,
        Headquarters,
        OperatingArea,
        Property,
        Business,
        Other
    }

    public enum OrganizationLinkKind
    {
        Parent,
        Branch,
        Affiliate,
        Predecessor,
        Successor,
        SplitFrom,
        MergedFrom,
        Custom
    }

    public enum OrganizationProjectionAccess
    {
        Full,
        Redacted,
        Concealed,
        Denied
    }

    public enum OrganizationOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinition,
        MissingOrganization,
        DuplicateOrganizationId,
        DuplicateRecordId,
        InvalidName,
        InvalidLifecycleTransition,
        InvalidReference,
        UnsupportedByDefinition,
        CycleDetected,
        PersistenceInvalid,
        RestoreFailed
    }
}
