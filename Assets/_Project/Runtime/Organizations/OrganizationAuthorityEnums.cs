using System;

namespace UnityIsekaiGame.Organizations
{
    public enum OrganizationPermissionCategory
    {
        Unknown = 0,
        ViewInformation = 10,
        ManageInformation = 20,
        ManageMembership = 30,
        ManageRanks = 40,
        ManageOffices = 50,
        IssueInstitutionalOrders = 60,
        RepresentOrganization = 70,
        ManageAccess = 80,
        ManagePropertyAssociation = 90,
        ManageResources = 100,
        ManagePolicy = 110,
        ParticipateInGovernance = 120,
        ExerciseLegalAuthority = 130,
        Custom = 1000
    }

    public enum InstitutionalActionCategory
    {
        Unknown = 0,
        Membership = 10,
        Rank = 20,
        Office = 30,
        OrganizationIdentity = 40,
        OrganizationHierarchy = 50,
        InformationAccess = 60,
        Delegation = 70,
        Command = 80,
        Financial = 85,
        Governance = 90,
        Legal = 100,
        Custom = 1000
    }

    public enum OrganizationPermissionCombinationPolicy
    {
        Unknown = 0,
        AnyRequiredPermission = 10,
        AllRequiredPermissions = 20,
        OneOfEachPermissionGroup = 30,
        ExplicitApprovalSet = 40,
        JointApproval = 50,
        Quorum = 60,
        Custom = 1000
    }

    public enum OrganizationAuthoritySourceType
    {
        Unknown = 0,
        MembershipDefinition = 10,
        RankDefinition = 20,
        OfficeDefinition = 30,
        OfficeAssignment = 40,
        DirectGrant = 50,
        Delegation = 60,
        TemporaryAppointment = 70,
        ParentOrganizationGrant = 80,
        ExternalContract = 90,
        GovernmentOrLaw = 100,
        Custom = 1000
    }

    public enum OrganizationAuthorityBindingSourceType
    {
        Unknown = 0,
        MembershipDefinition = 10,
        RankDefinition = 20,
        OfficeDefinition = 30,
        OfficeAssignment = 40,
        OrganizationOverride = 50,
        ActingOfficeAssignment = 60,
        ParentBranchRelationship = 70,
        DirectGrant = 80,
        Delegation = 90,
        Custom = 1000
    }

    public enum OrganizationAuthorityGrantLifecycleState
    {
        Unknown = 0,
        Proposed = 5,
        PendingAcceptance = 10,
        Active = 20,
        Suspended = 30,
        Expired = 40,
        Revoked = 50,
        Declined = 60,
        Ended = 70,
        Historical = 80,
        Invalid = 90
    }

    public enum OrganizationAuthorityScopeType
    {
        Unknown = 0,
        EntireOrganization = 10,
        OrganizationBranch = 20,
        SpecificOrganizationSubtree = 30,
        SpecificOffice = 40,
        SpecificRankTrack = 50,
        SpecificMembershipType = 60,
        SpecificPerson = 70,
        SpecificPlace = 80,
        SpecificPropertyReference = 90,
        SpecificRecord = 100,
        SpecificAction = 110,
        CustomSubject = 1000
    }

    public enum OrganizationAuthorityScopeMatch
    {
        ExactOnly = 0,
        IncludeDescendants = 10
    }

    public enum OrganizationAuthorityDelegationPolicy
    {
        None = 0,
        NonDelegable = 10,
        DelegableNoRedelegation = 20,
        Redelegable = 30
    }

    public enum OrganizationAuthorityConflictPolicy
    {
        Unknown = 0,
        GrantOnly = 10,
        DenyOverridesGrant = 20,
        HigherPriorityWins = 30,
        StableIdTieBreak = 40
    }

    public enum OrganizationAuthorizationStatus
    {
        Unknown = 0,
        Authorized = 10,
        Preview = 15,
        MissingActor = 20,
        MissingOrganization = 30,
        MissingAction = 40,
        MissingPermission = 50,
        ScopeMismatch = 60,
        SourceInactive = 70,
        Expired = 80,
        Suspended = 90,
        Revoked = 100,
        DeniedPermission = 110,
        CapabilityMissing = 120,
        QualificationMissing = 130,
        JointApprovalMissing = 140,
        DuplicateApproval = 150,
        ApprovalConsumed = 160,
        InvalidRequest = 170,
        InvalidDependency = 180,
        PersistenceInvalid = 190,
        RestoreFailed = 200,
        Duplicate = 210
    }

    public enum OrganizationApprovalLifecycleState
    {
        Unknown = 0,
        Active = 10,
        Withdrawn = 20,
        Expired = 30,
        Consumed = 40,
        Rejected = 50
    }

    public enum OrganizationAuthorityAuditPolicy
    {
        None = 0,
        SuccessfulActions = 10,
        FailedAuthorizations = 20,
        Always = 30
    }

    public enum OrganizationAuthorityProjectionAccess
    {
        Denied = 0,
        Concealed = 10,
        Redacted = 20,
        Full = 30
    }
}
