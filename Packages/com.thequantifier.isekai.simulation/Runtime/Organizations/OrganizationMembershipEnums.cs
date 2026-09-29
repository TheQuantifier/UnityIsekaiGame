namespace UnityIsekaiGame.Organizations
{
    public enum OrganizationMembershipCategory
    {
        Unknown,
        FullMember,
        AssociateMember,
        ProvisionalMember,
        Applicant,
        Invitee,
        Affiliate,
        HonoraryMember,
        EmployeeMember,
        ContractorAffiliate,
        StudentMember,
        ClergyMember,
        MilitaryMember,
        CivicMember,
        SecretMember,
        Custom
    }

    public enum OrganizationMembershipStatus
    {
        Unknown,
        Applied,
        Invited,
        PendingAcceptance,
        Provisional,
        Active,
        Inactive,
        Suspended,
        Resigned,
        Removed,
        Expelled,
        Expired,
        Historical,
        Invalid
    }

    public enum OrganizationMembershipSourceKind
    {
        Unknown,
        Application,
        Invitation,
        Founder,
        Appointment,
        EmploymentReference,
        Transfer,
        WorldSetup,
        ScriptedEvent,
        Custom
    }

    public enum OrganizationMembershipMultiplicityPolicy
    {
        OneActivePerPersonOrganizationDefinition,
        OneActivePerPersonOrganization,
        MultipleHistoricalOnly,
        MultipleActiveAllowed
    }

    public enum OrganizationMembershipEndingPolicy
    {
        FailIfActiveAssignments,
        EndActiveAssignments,
        SuspendActiveAssignments
    }

    public enum OrganizationRankAssignmentState
    {
        Unknown,
        Proposed,
        Active,
        Suspended,
        Superseded,
        Revoked,
        Ended,
        Historical
    }

    public enum OrganizationOfficeState
    {
        Unknown,
        Planned,
        Active,
        Dormant,
        Closed,
        Historical
    }

    public enum OrganizationOfficeAssignmentState
    {
        Unknown,
        Proposed,
        Acting,
        Active,
        Suspended,
        Ended,
        Removed,
        Historical
    }

    public enum OrganizationProjectionKind
    {
        Public,
        Member,
        Privileged
    }

    public enum OrganizationMembershipProjectionAccess
    {
        Full,
        Redacted,
        Concealed,
        Denied
    }

    public enum OrganizationMembershipOperationStatus
    {
        Succeeded,
        Preview,
        Duplicate,
        InvalidRequest,
        MissingDefinition,
        MissingOrganization,
        MissingMembership,
        MissingPerson,
        MissingRank,
        MissingRankTrack,
        MissingOffice,
        DuplicateRecordId,
        DuplicateActiveMembership,
        Ineligible,
        ConsentRequired,
        InvalidTransition,
        InvalidDependency,
        CapacityFull,
        ActiveAssignmentsBlockEnding,
        PersistenceInvalid,
        RestoreFailed
    }
}
