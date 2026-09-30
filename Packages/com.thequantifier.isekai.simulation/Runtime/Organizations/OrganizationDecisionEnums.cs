namespace UnityIsekaiGame.Organizations
{
    public enum OrganizationGoalCategory
    {
        Unknown = 0,
        MembershipGrowth = 10,
        Recruitment = 20,
        FinancialReserve = 30,
        Revenue = 40,
        DebtReduction = 50,
        PropertyAcquisition = 60,
        BranchExpansion = 70,
        Production = 80,
        Research = 90,
        Training = 100,
        Reputation = 110,
        Security = 120,
        Service = 130,
        ReligiousMission = 140,
        Political = 150,
        MilitaryPreparedness = 160,
        Custom = 1000
    }

    public enum OrganizationGoalLifecycleState
    {
        Unknown = 0,
        Proposed = 5,
        Approved = 10,
        Active = 20,
        Suspended = 30,
        Completed = 40,
        Failed = 50,
        Cancelled = 60,
        Expired = 70,
        Superseded = 80,
        Historical = 90,
        Invalid = 100
    }

    public enum OrganizationGoalCompletionPolicy
    {
        Unknown = 0,
        Automatic = 10,
        ExplicitConfirmation = 20
    }

    public enum OrganizationGoalProgressSourceKind
    {
        Unknown = 0,
        ExplicitContribution = 10,
        ActiveMembershipCount = 20,
        TreasuryBalance = 30,
        BranchCount = 40,
        PropertyOwnership = 50,
        HistoricalEventCount = 60,
        Custom = 1000
    }

    public enum OrganizationPolicyCategory
    {
        Unknown = 0,
        Membership = 10,
        RankAndPromotion = 20,
        OfficeAndAppointment = 30,
        Confidentiality = 40,
        InformationAccess = 50,
        FinancialControl = 60,
        Budgeting = 70,
        Spending = 80,
        RevenueRouting = 90,
        MembershipDues = 100,
        PropertyUse = 110,
        InventoryAccess = 120,
        BranchAdministration = 130,
        ProfessionalConduct = 140,
        Discipline = 150,
        GovernanceProcedure = 160,
        ExternalRepresentation = 170,
        Custom = 1000
    }

    public enum OrganizationPolicyLifecycleState
    {
        Unknown = 0,
        Proposed = 5,
        Adopted = 10,
        Scheduled = 20,
        Active = 30,
        Suspended = 40,
        Expired = 50,
        Revoked = 60,
        Superseded = 70,
        Historical = 80,
        Invalid = 90
    }

    public enum OrganizationPolicyParameterType
    {
        Unknown = 0,
        Boolean = 10,
        Integer = 20,
        FixedPoint = 30,
        StringIdentifier = 40,
        StableDefinitionId = 50,
        OrganizationId = 60,
        OfficeId = 70,
        RankId = 80,
        MembershipDefinitionId = 90,
        PermissionId = 100,
        CurrencyId = 110,
        Amount = 120,
        PercentageBasisPoints = 130,
        Duration = 140,
        TypedSubjectReference = 150,
        EnumValue = 160
    }

    public enum OrganizationPolicyScopeType
    {
        Unknown = 0,
        EntireOrganization = 10,
        SpecificBranch = 20,
        OrganizationSubtree = 30,
        SpecificMembershipType = 40,
        SpecificRankTrack = 50,
        SpecificOffice = 60,
        SpecificTreasury = 70,
        SpecificAccount = 80,
        SpecificProperty = 90,
        SpecificBusiness = 100,
        SpecificAction = 110,
        SpecificSubject = 120,
        Custom = 1000
    }

    public enum OrganizationProposalCategory
    {
        Unknown = 0,
        AdoptPolicy = 10,
        AmendPolicy = 20,
        RevokePolicy = 30,
        EstablishGoal = 40,
        AmendGoal = 50,
        CancelGoal = 60,
        ApproveBudget = 70,
        AmendBudget = 80,
        AuthorizeExpense = 90,
        CreateBranch = 100,
        RenameOrganization = 110,
        ChangeHeadquarters = 120,
        AdmitMember = 130,
        PromoteMember = 140,
        CreateOffice = 150,
        AppointOfficeholder = 160,
        DelegateAuthority = 170,
        AcquireProperty = 180,
        DisposeProperty = 190,
        EnterContract = 200,
        TakeLoan = 210,
        DissolveOrganization = 220,
        Declaration = 230,
        Custom = 1000
    }

    public enum OrganizationProposalLifecycleState
    {
        Unknown = 0,
        Draft = 5,
        Submitted = 10,
        UnderReview = 20,
        OpenForAmendment = 30,
        OpenForVoting = 40,
        VotingClosed = 50,
        Passed = 60,
        Failed = 70,
        Withdrawn = 80,
        Rejected = 90,
        Vetoed = 100,
        Expired = 110,
        Superseded = 120,
        ExecutionPending = 130,
        Executed = 140,
        ExecutionFailed = 150,
        Historical = 160,
        Invalid = 170
    }

    public enum OrganizationAmendmentLifecycleState
    {
        Unknown = 0,
        Proposed = 10,
        Accepted = 20,
        Rejected = 30,
        Superseded = 40,
        Withdrawn = 50
    }

    public enum OrganizationDecisionProcedureKind
    {
        Unknown = 0,
        SingleAuthorizedDecision = 10,
        SimpleMajority = 20,
        AbsoluteMajority = 30,
        Supermajority = 40,
        Unanimity = 50,
        JointApproval = 60,
        Emergency = 70,
        Custom = 1000
    }

    public enum OrganizationVoterEligibilityKind
    {
        Unknown = 0,
        ActiveMembers = 10,
        SpecificOfficeHolders = 20,
        SpecificRankHolders = 30,
        AuthorityPermissionHolders = 40,
        ExplicitPersons = 50
    }

    public enum OrganizationVoteChoice
    {
        Unknown = 0,
        Approve = 10,
        Reject = 20,
        Abstain = 30
    }

    public enum OrganizationVoteLifecycleState
    {
        Unknown = 0,
        Active = 10,
        Replaced = 20,
        Withdrawn = 30,
        Invalid = 40
    }

    public enum OrganizationVoteWeightKind
    {
        Unknown = 0,
        OnePersonOneVote = 10,
        OfficeBased = 20,
        RankBased = 30,
        FixedWeight = 40
    }

    public enum OrganizationQuorumKind
    {
        None = 0,
        MinimumCount = 10,
        PercentageEligible = 20,
        RequiredOfficePresence = 30
    }

    public enum OrganizationPassageThresholdKind
    {
        Unknown = 0,
        SimpleMajorityVotesCast = 10,
        MajorityOfEligible = 20,
        AbsoluteMajorityVotesCast = 30,
        TwoThirdsVotesCast = 40,
        TwoThirdsEligible = 50,
        Unanimity = 60,
        FixedWeightedThreshold = 70
    }

    public enum OrganizationTiePolicy
    {
        Fail = 0,
        ChairBreaksTie = 10,
        ReopenVoting = 20
    }

    public enum OrganizationResolutionOutcome
    {
        Unknown = 0,
        Adopted = 10,
        Rejected = 20,
        FailedQuorum = 30,
        Tied = 40,
        Vetoed = 50,
        OverrideFailed = 60,
        OverrideSucceeded = 70,
        Advisory = 80,
        Emergency = 90
    }

    public enum OrganizationResolutionLifecycleState
    {
        Unknown = 0,
        Adopted = 10,
        Rejected = 20,
        Vetoed = 30,
        OverridePending = 40,
        ExecutionPending = 50,
        Executing = 60,
        Executed = 70,
        ExecutionFailed = 80,
        Historical = 90
    }

    public enum OrganizationDecisionExecutionOperationKind
    {
        Unknown = 0,
        AdoptPolicy = 10,
        EstablishGoal = 20,
        ApproveBudget = 30,
        AuthorizeExpense = 40,
        CreateBranch = 50,
        AppointOfficeholder = 60,
        RecordDeclaration = 70,
        Custom = 1000
    }

    public enum OrganizationDecisionExecutionState
    {
        Unknown = 0,
        Planned = 10,
        Previewed = 20,
        Pending = 30,
        Succeeded = 40,
        Failed = 50,
        SkippedOptional = 60
    }

    public enum OrganizationDecisionOperationCode
    {
        Success = 0,
        Preview = 1,
        Duplicate = 2,
        InvalidRequest = 10,
        MissingDependency = 20,
        MissingOrganization = 21,
        MissingDefinition = 22,
        MissingProposal = 23,
        MissingProcedure = 24,
        MissingVote = 25,
        MissingResolution = 26,
        Unauthorized = 40,
        IneligibleVoter = 41,
        DuplicateVote = 42,
        QuorumNotMet = 43,
        ThresholdNotMet = 44,
        InvalidLifecycle = 50,
        InvalidWindow = 51,
        InvalidParameter = 52,
        InvalidConflict = 53,
        ExecutionFailed = 60,
        PersistenceInvalid = 70,
        RestoreFailed = 71,
        Disposed = 80
    }

    public enum OrganizationDecisionProjectionAccess
    {
        Denied = 0,
        Concealed = 10,
        Redacted = 20,
        Full = 30
    }
}
