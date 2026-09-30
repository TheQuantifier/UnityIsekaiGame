using System;

namespace UnityIsekaiGame.Factions
{
    public enum PoliticalFactionCategory
    {
        Custom,
        InternalInstitutionalBloc,
        ReformMovement,
        TraditionalistBloc,
        LeadershipSupportBloc,
        OppositionBloc,
        IdeologicalMovement,
        EconomicInterestBloc,
        ReligiousBloc,
        MilitaryBloc,
        RegionalBloc,
        EthnicOrCulturalInterestBloc,
        ClaimantSupportFaction,
        RevolutionaryMovement,
        SecretPoliticalSociety,
        CrossOrganizationalCoalition,
        IndependentPoliticalMovement
    }

    public enum FactionHostContextKind
    {
        Unknown,
        SingleOrganization,
        OrganizationBranch,
        OrganizationSubtree,
        MultipleOrganizations,
        PlaceOrRegion,
        PopulationAudience,
        Independent,
        Global
    }

    public enum FactionLifecyclePolicy
    {
        HistoricalRecordsPreserved,
        DissolutionAllowed,
        SplitAndMergeAllowed,
        DormancyAllowed
    }

    public enum FactionLifecycleState
    {
        Invalid,
        Forming,
        Active,
        Dormant,
        Suppressed,
        Underground,
        Split,
        Merged,
        Dissolved,
        Archived
    }

    public enum FactionNameCategory
    {
        Official,
        Public,
        Internal,
        Abbreviation,
        SecretCodeName,
        Historical,
        Derogatory,
        Disputed
    }

    public enum FactionAffiliationCategory
    {
        Custom,
        FormalMember,
        ProvisionalMember,
        Supporter,
        Sympathizer,
        DonorOrPatron,
        Organizer,
        LeadershipMember,
        PublicEndorser,
        SecretMember,
        Infiltrator,
        Informant,
        Opponent,
        HostileOpponent,
        FormerMember
    }

    public enum FactionAffiliationStatus
    {
        Invalid,
        Proposed,
        Invited,
        Applied,
        Active,
        Inactive,
        Suspended,
        SecretActive,
        Defected,
        Resigned,
        Removed,
        Expelled,
        Exposed,
        Former,
        Historical
    }

    public enum FactionAffiliationConsentPolicy
    {
        NoConsentRequired,
        ExplicitConsentRequired,
        InvitationThenAcceptance,
        CovertOperationRequired
    }

    public enum FactionPublicAlignmentKind
    {
        None,
        PubliclyAligned,
        PubliclyOpposed,
        PubliclyNeutral,
        Suspected,
        FalsePublicAlignment,
        Concealed
    }

    public enum FactionRoleCategory
    {
        Custom,
        Member,
        Organizer,
        Spokesperson,
        Strategist,
        Coordinator,
        Treasurer,
        Recruiter,
        LocalLeader,
        SeniorLeader,
        Founder,
        Patron,
        Agent,
        Informant,
        Infiltrator
    }

    public enum FactionRoleAssignmentState
    {
        Invalid,
        Active,
        Acting,
        Suspended,
        Ended,
        Historical
    }

    public enum FactionPositionTargetKind
    {
        Custom,
        OrganizationPolicy,
        OrganizationGoal,
        OrganizationProposal,
        OrganizationOffice,
        OrganizationResource,
        Person,
        Faction,
        AlignmentAxis,
        PlaceOrRegion
    }

    public enum FactionPositionStance
    {
        Neutral,
        Supports,
        Opposes,
        Prefers,
        Rejects,
        Contests,
        Claims,
        Abstains
    }

    public enum FactionVoteRecommendationKind
    {
        Support,
        Oppose,
        Abstain,
        FreeVote,
        SecretRecommendation
    }

    public enum FactionDispositionKind
    {
        Neutral,
        Cooperative,
        Competitive,
        Opposed,
        Hostile,
        Sympathetic,
        Distrustful
    }

    public enum FactionProjectionAccess
    {
        Denied,
        Concealed,
        Redacted,
        Full,
        Development
    }

    public enum FactionOperationCode
    {
        Success,
        Preview,
        Duplicate,
        MissingRuntime,
        MissingDefinition,
        MissingFaction,
        MissingOrganization,
        MissingPerson,
        MissingAffiliation,
        MissingProposal,
        InvalidRequest,
        InvalidLifecycle,
        InvalidHost,
        InvalidEligibility,
        MissingConsent,
        InvalidConflict,
        PersistenceInvalid,
        Unauthorized,
        Disposed
    }

    [Flags]
    public enum FactionInfluenceInputKind
    {
        None = 0,
        ActiveMembership = 1 << 0,
        PublicSupport = 1 << 1,
        SecretSupport = 1 << 2,
        OfficePenetration = 1 << 3,
        AuthorityPenetration = 1 << 4,
        ResourceSupportReference = 1 << 5,
        SocialNetworkReach = 1 << 6,
        ProposalActivity = 1 << 7
    }
}
