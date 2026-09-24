namespace UnityIsekaiGame.Professions
{
    public enum ProfessionCategory
    {
        Custom = 0,
        Craft = 1,
        Trade = 2,
        Academic = 3,
        Medical = 4,
        Military = 5,
        Government = 6,
        Religious = 7,
        Agricultural = 8,
        Commercial = 9,
        Service = 10,
        Artistic = 11,
        Exploration = 12,
        Criminal = 13,
        Magical = 14,
        Technical = 15,
        Labor = 16
    }

    public enum ProfessionRecognitionForm
    {
        Either = 0,
        Formal = 1,
        Informal = 2
    }

    public enum ProfessionRelationshipState
    {
        Interested = 0,
        Aspiring = 1,
        StudentFoundation = 2,
        ApprenticeFoundation = 3,
        Practicing = 4,
        RecognizedPractitioner = 5,
        Inactive = 6,
        Suspended = 7,
        Revoked = 8,
        Abandoned = 9,
        Retired = 10,
        Former = 11,
        Secret = 12,
        Disputed = 13,
        Custom = 14
    }

    public enum ProfessionProjectionAudience
    {
        AuthoritativeInternal = 0,
        PrivilegedDebug = 1,
        Self = 2,
        PublicInspection = 3,
        Biography = 4,
        KnowledgeProjection = 5
    }

    public enum ProfessionOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        MissingRuntime = 3,
        MissingDefinition = 4,
        MissingPerson = 5,
        MissingRelationship = 6,
        DuplicateRelationshipId = 7,
        DuplicateActiveRelationship = 8,
        InvalidRequest = 9,
        InvalidState = 10,
        InvalidSpecialization = 11,
        MissingRecognitionAuthority = 12,
        MultiplePrimary = 13,
        RestoreFailed = 14,
        ValidationFailed = 15
    }

    public enum ProfessionHistoryHookKind
    {
        BeganPracticing = 0,
        Recognized = 1,
        SpecializationAdopted = 2,
        MadePrimary = 3,
        Stopped = 4,
        Retired = 5,
        RecognitionSuspended = 6,
        RecognitionRevoked = 7,
        SecretRevealed = 8,
        Corrected = 9
    }

    public enum ProfessionEntryType
    {
        SelfDeclaredPractice = 0,
        InformalApprenticeship = 1,
        FormalStudy = 2,
        GuildApplication = 3,
        RecognitionApplication = 4,
        MilitaryOrGovernmentAppointment = 5,
        ReligiousVocation = 6,
        Specialization = 7,
        Reentry = 8,
        Custom = 9
    }

    public enum ProfessionEntryFormality
    {
        Informal = 0,
        Formal = 1,
        Either = 2
    }

    public enum ProfessionSelfDeclarationPolicy
    {
        Disallowed = 0,
        Allowed = 1,
        Required = 2
    }

    public enum ProfessionReentryPolicy
    {
        NotApplicable = 0,
        AllowFormerInactiveAbandonedRetired = 1,
        AllowSuspendedWithAuthority = 2,
        AllowRevokedWithExplicitReinstatement = 3
    }

    public enum ProfessionEntryRequestState
    {
        Draft = 0,
        Submitted = 1,
        UnderReview = 2,
        Approved = 3,
        Rejected = 4,
        Withdrawn = 5,
        Expired = 6,
        Cancelled = 7,
        Invalid = 8,
        Custom = 9
    }

    public enum ProfessionEligibilityStatus
    {
        Succeeded = 0,
        Preview = 1,
        MissingRuntime = 2,
        MissingDefinition = 3,
        MissingPerson = 4,
        MissingEntryPath = 5,
        ProfessionMismatch = 6,
        SpecializationMismatch = 7,
        FormalityMismatch = 8,
        SelfDeclarationBlocked = 9,
        MissingAuthority = 10,
        InvalidAuthority = 11,
        RequirementFailed = 12,
        MissingSkill = 13,
        MissingKnowledge = 14,
        MissingCapability = 15,
        MissingTrait = 16,
        MissingStatus = 17,
        MissingOrganization = 18,
        AccessDenied = 19,
        AgeOrLifeStageBlocked = 20,
        Conflict = 21,
        DuplicateActiveRelationship = 22,
        StaleEvaluation = 23,
        InvalidRequest = 24,
        RestoreFailed = 25,
        ValidationFailed = 26
    }

    public enum ProfessionEntryOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        MissingRuntime = 3,
        MissingDefinition = 4,
        MissingRequest = 5,
        InvalidRequest = 6,
        InvalidState = 7,
        EligibilityFailed = 8,
        StaleEvaluation = 9,
        DuplicateRelationship = 10,
        MissingAuthority = 11,
        InvalidAuthority = 12,
        RestoreFailed = 13,
        ValidationFailed = 14
    }

    public enum ProfessionEntryProjectionAudience
    {
        AuthoritativeInternal = 0,
        PrivilegedDebug = 1,
        Applicant = 2,
        Sponsor = 3,
        Authority = 4,
        PublicInspection = 5
    }

    public enum ProfessionEntryHistoryHookKind
    {
        EligibilityEvaluated = 0,
        InformalEntry = 1,
        RequestSubmitted = 2,
        RequestApproved = 3,
        RequestRejected = 4,
        RequestWithdrawn = 5,
        SpecializationEntered = 6,
        ProfessionResumed = 7,
        RecognitionReinstated = 8,
        SecretRequirementRevealed = 9,
        Corrected = 10
    }

    public enum TrainingProgramCategory
    {
        Custom = 0,
        AcademicEducation = 1,
        VocationalTraining = 2,
        Apprenticeship = 3,
        Mentorship = 4,
        MilitaryTraining = 5,
        ReligiousInstruction = 6,
        ProfessionalInduction = 7,
        SafetyTraining = 8,
        ContinuingEducation = 9,
        RemedialTraining = 10
    }

    public enum TrainingProgramFormality
    {
        Informal = 0,
        Formal = 1,
        Either = 2
    }

    public enum TrainingTeachingMethod
    {
        Custom = 0,
        Lecture = 1,
        Reading = 2,
        Demonstration = 3,
        GuidedPractice = 4,
        SupervisedWork = 5,
        Discussion = 6,
        ExaminationPreparation = 7,
        FieldStudy = 8,
        IndependentStudy = 9
    }

    public enum TrainingEnrollmentState
    {
        Applied = 0,
        Accepted = 1,
        Enrolled = 2,
        Active = 3,
        Paused = 4,
        Suspended = 5,
        Withdrawn = 6,
        Dismissed = 7,
        Failed = 8,
        Completed = 9,
        Cancelled = 10,
        Expired = 11,
        Custom = 12
    }

    public enum TrainingInstructorRoleKind
    {
        Instructor = 0,
        Mentor = 1,
        Master = 2,
        Supervisor = 3,
        Evaluator = 4,
        AssistantInstructor = 5,
        GuestInstructor = 6,
        Custom = 7
    }

    public enum TrainingAssignmentActivityCategory
    {
        Custom = 0,
        Crafting = 1,
        ProductionJob = 2,
        Repair = 3,
        Salvage = 4,
        Experiment = 5,
        ResearchRecord = 6,
        WorkOrder = 7,
        Combat = 8,
        Service = 9
    }

    public enum TrainingSupervisionLevel
    {
        Custom = 0,
        ObservationOnly = 1,
        DirectInstruction = 2,
        CloselySupervised = 3,
        PeriodicallySupervised = 4,
        IndependentWithReview = 5
    }

    public enum TrainingSessionCompletionState
    {
        Planned = 0,
        Attended = 1,
        Completed = 2,
        Partial = 3,
        Failed = 4,
        Cancelled = 5
    }

    public enum TrainingWorkOutcome
    {
        Unknown = 0,
        Succeeded = 1,
        Partial = 2,
        Failed = 3,
        Rejected = 4
    }

    public enum TrainingOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        MissingRuntime = 3,
        MissingDefinition = 4,
        MissingPerson = 5,
        MissingEnrollment = 6,
        InvalidRequest = 7,
        InvalidState = 8,
        InvalidTransition = 9,
        InvalidInstructor = 10,
        InvalidModule = 11,
        InvalidLesson = 12,
        InvalidAssignment = 13,
        DuplicateActivity = 14,
        RequirementBlocked = 15,
        StaleProgress = 16,
        TeachingFailed = 17,
        RestoreFailed = 18,
        ValidationFailed = 19
    }

    public enum TrainingProjectionAudience
    {
        AuthoritativeInternal = 0,
        PrivilegedDebug = 1,
        Learner = 2,
        Instructor = 3,
        Mentor = 4,
        Supervisor = 5,
        Institution = 6,
        ProfessionAuthority = 7,
        PublicInspection = 8,
        PersonKnown = 9,
        PersonBelieved = 10
    }

    public enum TrainingHistoryHookKind
    {
        ProgramEntered = 0,
        ApprenticeshipBegun = 1,
        InstructorAssigned = 2,
        MajorAssignmentCompleted = 3,
        ProgramPaused = 4,
        LearnerDismissed = 5,
        ProgramFailed = 6,
        ProgramCompleted = 7,
        ApprenticeshipCompleted = 8,
        Corrected = 9
    }

    public enum ProfessionalActivityCategory
    {
        Custom = 0,
        Crafting = 1,
        Production = 2,
        Repair = 3,
        Maintenance = 4,
        ItemRecovery = 5,
        Experimentation = 6,
        Research = 7,
        Teaching = 8,
        SupervisedPractice = 9,
        Combat = 10,
        MilitaryService = 11,
        MedicalService = 12,
        GovernmentService = 13,
        ReligiousService = 14,
        Trade = 15,
        Exploration = 16,
        ArtisticWork = 17,
        AgriculturalWork = 18,
        CriminalActivity = 19
    }

    public enum ProfessionalActivitySourceType
    {
        Custom = 0,
        CraftingOperation = 1,
        ProductionJob = 2,
        ProductionStage = 3,
        WorkOrder = 4,
        RepairOperation = 5,
        ItemRecoveryOperation = 6,
        ExperimentTrial = 7,
        DiscoveryClaim = 8,
        TrainingPracticalAssignment = 9,
        TrainingSupervisedWork = 10,
        TeachingSession = 11,
        CombatEncounter = 12,
        DamageActivity = 13,
        HealingActivity = 14,
        OrganizationService = 15,
        ExplorationRecord = 16
    }

    public enum ProfessionalActivityState
    {
        Proposed = 0,
        Recorded = 1,
        PendingValidation = 2,
        Validated = 3,
        Rejected = 4,
        Disputed = 5,
        Corrected = 6,
        Revoked = 7,
        Archived = 8,
        Invalid = 9,
        Custom = 10
    }

    public enum ProfessionalActivityOutcomeState
    {
        Unknown = 0,
        Successful = 1,
        PartialSuccess = 2,
        Failed = 3,
        Rejected = 4,
        Revoked = 5,
        DangerousMistake = 6,
        RecoveredFailure = 7,
        Innovative = 8
    }

    public enum ProfessionalExperienceCategory
    {
        Custom = 0,
        Observation = 1,
        AssistedWork = 2,
        SupervisedWork = 3,
        IndependentWork = 4,
        Leadership = 5,
        Teaching = 6,
        Research = 7,
        RoutineWork = 8,
        ComplexWork = 9,
        HighRiskWork = 10,
        FailedAttempt = 11,
        RecoveryFromFailure = 12,
        Innovation = 13,
        Administration = 14,
        Service = 15
    }

    public enum ProfessionalResponsibilityLevel
    {
        Observer = 0,
        Assistant = 1,
        SupervisedWorker = 2,
        IndependentWithReview = 3,
        IndependentPractitioner = 4,
        Supervisor = 5,
        Leader = 6,
        Instructor = 7,
        Custom = 8
    }

    public enum ProfessionalActivityDifficulty
    {
        Unknown = 0,
        Trivial = 1,
        Routine = 2,
        Skilled = 3,
        Advanced = 4,
        Dangerous = 5,
        Unusual = 6,
        Innovative = 7,
        MasterworkFoundation = 8,
        Custom = 9
    }

    public enum ProfessionalSupervisionPolicy
    {
        Any = 0,
        ObservationOnly = 1,
        RequiresSupervision = 2,
        AllowsIndependent = 3,
        RequiresIndependent = 4,
        RequiresLeadership = 5
    }

    public enum ProfessionalIndependentWorkPolicy
    {
        Any = 0,
        Disallowed = 1,
        Allowed = 2,
        Required = 3
    }

    public enum ProfessionalFailureCreditPolicy
    {
        NoCredit = 0,
        RecordOnly = 1,
        CountsAsFailedAttempt = 2,
        CountsWithRecovery = 3,
        Custom = 4
    }

    public enum ProfessionalRepetitionPolicy
    {
        PreserveAll = 0,
        DiminishBySignature = 1,
        RequireNoveltyForBreadth = 2,
        Custom = 3
    }

    public enum ProfessionalCreditPolicy
    {
        Exclusive = 0,
        Shared = 1,
        RoleWeighted = 2,
        ObservationOnly = 3,
        NoProfessionalCredit = 4,
        Custom = 5
    }

    public enum ProfessionalActivityOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        MissingRuntime = 3,
        MissingDefinition = 4,
        MissingPerson = 5,
        MissingProfession = 6,
        MissingProfessionRelationship = 7,
        MissingActivity = 8,
        MissingSource = 9,
        SourceActorMismatch = 10,
        SourceInvalidState = 11,
        ProfessionMismatch = 12,
        SpecializationMismatch = 13,
        RequirementBlocked = 14,
        DuplicateExclusiveSource = 15,
        InvalidRequest = 16,
        InvalidState = 17,
        EvidenceRejected = 18,
        RestoreFailed = 19,
        ValidationFailed = 20
    }

    public enum ProfessionalActivityProjectionAudience
    {
        AuthoritativeInternal = 0,
        PrivilegedDebug = 1,
        SubjectPerson = 2,
        Supervisor = 3,
        Instructor = 4,
        ProfessionAuthority = 5,
        Organization = 6,
        Employer = 7,
        PublicInspection = 8,
        PersonKnown = 9,
        PersonBelieved = 10
    }

    public enum ProfessionalActivityHistoryHookKind
    {
        FirstProfessionalActivity = 0,
        MajorIndependentWork = 1,
        ImportantFailure = 2,
        SignificantRecovery = 3,
        MasterworkFoundation = 4,
        LeadershipOfMajorWork = 5,
        ImportantInnovation = 6,
        ExperienceRecordDisputed = 7,
        ExperienceRecordCorrected = 8,
        ExperienceRevoked = 9
    }

    public enum CredentialCategory
    {
        Custom = 0,
        Certificate = 1,
        License = 2,
        Commission = 3,
        Ordination = 4,
        Clearance = 5,
        Qualification = 6,
        Authorization = 7,
        Endorsement = 8,
        Recommendation = 9,
        AwardFoundation = 10
    }

    public enum CredentialIssuerAuthorityKind
    {
        Custom = 0,
        Guild = 1,
        Government = 2,
        Military = 3,
        School = 4,
        University = 5,
        Religion = 6,
        ProfessionalOrganization = 7,
        EmployerAuthority = 8,
        AuthorizedIndividual = 9
    }

    public enum CredentialExpirationPolicy
    {
        NeverExpires = 0,
        FixedDuration = 1,
        ExplicitWorldTime = 2,
        RequiresRenewal = 3,
        Custom = 4
    }

    public enum CredentialRenewalPolicy
    {
        NotRenewable = 0,
        RenewWithCurrentQualification = 1,
        RenewWithContinuingEducation = 2,
        RenewWithRecentExperience = 3,
        RenewWithNewExamination = 4,
        Custom = 5
    }

    public enum CredentialLifecyclePolicy
    {
        NotAllowed = 0,
        AllowedByIssuer = 1,
        RequiresReason = 2,
        RequiresAuthorityAndReason = 3,
        Custom = 4
    }

    public enum CredentialTransferability
    {
        NonTransferable = 0,
        TransferableWithIssuerApproval = 1,
        Transferable = 2,
        Custom = 3
    }

    public enum CredentialApplicationState
    {
        Draft = 0,
        Submitted = 1,
        UnderReview = 2,
        AwaitingExamination = 3,
        AwaitingEvidence = 4,
        Approved = 5,
        Rejected = 6,
        Withdrawn = 7,
        Expired = 8,
        Cancelled = 9,
        Invalid = 10,
        Custom = 11
    }

    public enum CredentialAssessmentCategory
    {
        Custom = 0,
        Written = 1,
        Oral = 2,
        Practical = 3,
        Observed = 4,
        Mixed = 5
    }

    public enum CredentialExaminationAttemptState
    {
        Draft = 0,
        InProgress = 1,
        Passed = 2,
        Failed = 3,
        Incomplete = 4,
        Invalid = 5,
        Disputed = 6,
        Custom = 7
    }

    public enum CredentialState
    {
        Pending = 0,
        Active = 1,
        Expired = 2,
        Suspended = 3,
        Revoked = 4,
        Surrendered = 5,
        Replaced = 6,
        Invalid = 7,
        Disputed = 8,
        ForgedClaimFoundation = 9,
        Custom = 10
    }

    public enum CredentialAuthenticityState
    {
        Unknown = 0,
        Authoritative = 1,
        VerifiedAuthentic = 2,
        Disputed = 3,
        ForgedClaim = 4,
        InvalidDocument = 5,
        RevokedButDocumentExists = 6
    }

    public enum CredentialPermissionStatePolicy
    {
        ActiveOnly = 0,
        ActiveOrGracePeriod = 1,
        HistoricalOnly = 2,
        AnyNonRevoked = 3,
        Custom = 4
    }

    public enum CredentialOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        MissingRuntime = 3,
        MissingDefinition = 4,
        MissingPerson = 5,
        MissingAuthority = 6,
        UnauthorizedIssuer = 7,
        UnauthorizedEvaluator = 8,
        MissingQualification = 9,
        StaleQualification = 10,
        MissingApplication = 11,
        MissingExamination = 12,
        MissingCredential = 13,
        InvalidRequest = 14,
        InvalidState = 15,
        InvalidTransition = 16,
        DuplicateActiveCredential = 17,
        DuplicateApplication = 18,
        DuplicateRegistrationNumber = 19,
        PermissionDenied = 20,
        ForgedClaimRejected = 21,
        RestoreFailed = 22,
        ValidationFailed = 23
    }

    public enum CredentialProjectionAudience
    {
        AuthoritativeInternal = 0,
        PrivilegedDebug = 1,
        Holder = 2,
        Applicant = 3,
        Issuer = 4,
        Evaluator = 5,
        ProfessionAuthority = 6,
        Employer = 7,
        PublicInspection = 8,
        PersonKnown = 9,
        PersonBelieved = 10
    }

    public enum CredentialHistoryHookKind
    {
        ApplicationSubmitted = 0,
        ExaminationAttempted = 1,
        ExaminationPassed = 2,
        ExaminationFailed = 3,
        CredentialIssued = 4,
        CredentialRenewed = 5,
        CredentialExpired = 6,
        CredentialSuspended = 7,
        CredentialReinstated = 8,
        CredentialRevoked = 9,
        CredentialSurrendered = 10,
        CredentialDisputed = 11,
        CredentialReplaced = 12,
        ForgedCredentialExposed = 13,
        Corrected = 14
    }

    public enum ProfessionalRankCategory
    {
        Custom = 0,
        Novice = 1,
        Student = 2,
        Apprentice = 3,
        JuniorPractitioner = 4,
        QualifiedPractitioner = 5,
        Journeyman = 6,
        SeniorPractitioner = 7,
        Expert = 8,
        Master = 9,
        GrandmasterFoundation = 10,
        Veteran = 11,
        OfficerFoundation = 12,
        AcademicRankFoundation = 13,
        ReligiousRankFoundation = 14,
        InformalRecognizedRank = 15
    }

    public enum ProfessionalRankTrackKind
    {
        Formal = 0,
        Informal = 1,
        Either = 2
    }

    public enum ProfessionalRankState
    {
        Proposed = 0,
        PendingEvaluation = 1,
        Active = 2,
        Provisional = 3,
        Suspended = 4,
        Demoted = 5,
        Revoked = 6,
        ExpiredFoundation = 7,
        Retired = 8,
        Former = 9,
        Disputed = 10,
        Replaced = 11,
        Invalid = 12,
        Custom = 13
    }

    public enum ProfessionalRankApplicationState
    {
        Draft = 0,
        Submitted = 1,
        UnderReview = 2,
        AwaitingEvidence = 3,
        AwaitingExamination = 4,
        Approved = 5,
        Rejected = 6,
        Withdrawn = 7,
        Expired = 8,
        Cancelled = 9,
        Invalid = 10,
        Custom = 11
    }

    public enum ProfessionalRankOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        MissingDefinition = 3,
        MissingLadder = 4,
        MissingPerson = 5,
        MissingProfession = 6,
        MissingRank = 7,
        MissingApplication = 8,
        MissingCredential = 9,
        MissingTraining = 10,
        MissingExperience = 11,
        MissingExamination = 12,
        MissingMastery = 13,
        MissingAchievement = 14,
        MissingQualification = 15,
        UnauthorizedAuthority = 16,
        StaleEvaluation = 17,
        InvalidRequest = 18,
        InvalidState = 19,
        InvalidTransition = 20,
        InvalidSpecialization = 21,
        InvalidRankSkipping = 22,
        DuplicateActiveApplication = 23,
        DuplicateActiveRank = 24,
        DuplicateActiveMastery = 25,
        ValidationFailed = 26,
        CorruptSave = 27
    }

    public enum ProfessionalRankProjectionAudience
    {
        Holder = 0,
        Applicant = 1,
        RecognizingAuthority = 2,
        ProfessionOrganization = 3,
        InstructorOrSupervisor = 4,
        EmployerFoundation = 5,
        Public = 6,
        PersonKnown = 7,
        PersonBelieved = 8,
        PrivilegedDebug = 9,
        Redacted = 10
    }

    public enum ProfessionalRankHistoryHookKind
    {
        Unknown = 0,
        ApplicationSubmitted = 1,
        PromotionApproved = 2,
        PersonPromoted = 3,
        InformalRankRecognized = 4,
        LateralRankChanged = 5,
        MasteryRecognized = 6,
        RankSuspended = 7,
        RankReinstated = 8,
        PersonDemoted = 9,
        RankRevoked = 10,
        PersonRetired = 11,
        SecretRankRevealed = 12,
        RankDisputeResolved = 13,
        RankReplaced = 14,
        RankCorrected = 15
    }

    public enum PositionCategory
    {
        Custom = 0,
        Worker = 1,
        Specialist = 2,
        Supervisor = 3,
        Manager = 4,
        Executive = 5,
        Officer = 6,
        Administrator = 7,
        Instructor = 8,
        Researcher = 9,
        Military = 10,
        Religious = 11,
        Government = 12,
        Guild = 13,
        Contractor = 14,
        Volunteer = 15,
        ElectedOfficeFoundation = 16,
        AppointedOffice = 17
    }

    public enum DutyCategory
    {
        Custom = 0,
        Production = 1,
        Crafting = 2,
        Repair = 3,
        Service = 4,
        Teaching = 5,
        Supervision = 6,
        Administration = 7,
        Security = 8,
        Combat = 9,
        Medical = 10,
        Research = 11,
        Inspection = 12,
        Recordkeeping = 13,
        CustomerInteraction = 14,
        QuestRequestFoundation = 15
    }

    public enum EmploymentClassification
    {
        Custom = 0,
        Permanent = 1,
        Temporary = 2,
        ContractFoundation = 3,
        PartTime = 4,
        FullTime = 5,
        Seasonal = 6,
        ApprenticeEmployment = 7,
        Volunteer = 8,
        Appointed = 9,
        ElectedFoundation = 10,
        MilitaryService = 11,
        ReligiousService = 12,
        IndependentServiceFoundation = 13
    }

    public enum PositionInstanceState
    {
        Planned = 0,
        Vacant = 1,
        RecruitingFoundation = 2,
        Filled = 3,
        PartiallyFilled = 4,
        Suspended = 5,
        Frozen = 6,
        Closed = 7,
        Abolished = 8,
        Invalid = 9,
        Custom = 10
    }

    public enum EmploymentState
    {
        Proposed = 0,
        Applied = 1,
        Offered = 2,
        Accepted = 3,
        Active = 4,
        Probationary = 5,
        OnLeaveFoundation = 6,
        Suspended = 7,
        Resigned = 8,
        Dismissed = 9,
        LaidOffFoundation = 10,
        ContractEnded = 11,
        Retired = 12,
        DeceasedFoundation = 13,
        Former = 14,
        Cancelled = 15,
        Invalid = 16,
        Custom = 17
    }

    public enum PositionRequestType
    {
        Application = 0,
        DirectAppointment = 1,
        PromotionFoundation = 2,
        Transfer = 3,
        TemporaryAssignment = 4,
        VolunteerAssignment = 5,
        ElectionResultFoundation = 6,
        Custom = 7
    }

    public enum PositionRequestState
    {
        Draft = 0,
        Submitted = 1,
        UnderReview = 2,
        Offered = 3,
        Accepted = 4,
        Approved = 5,
        Rejected = 6,
        Withdrawn = 7,
        Expired = 8,
        Cancelled = 9,
        Invalid = 10,
        Custom = 11
    }

    public enum DutyAssignmentState
    {
        Assigned = 0,
        Active = 1,
        Completed = 2,
        Failed = 3,
        NeglectedFoundation = 4,
        Delegated = 5,
        Suspended = 6,
        Cancelled = 7,
        Archived = 8,
        Custom = 9
    }

    public enum PositionEmploymentOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        MissingDefinition = 3,
        MissingPerson = 4,
        MissingOrganization = 5,
        MissingPosition = 6,
        MissingEmployment = 7,
        MissingApplication = 8,
        MissingDuty = 9,
        MissingRequirement = 10,
        UnauthorizedAuthority = 11,
        StaleEvaluation = 12,
        InvalidRequest = 13,
        InvalidState = 14,
        InvalidTransition = 15,
        CapacityExceeded = 16,
        EmploymentConflict = 17,
        ReportingCycle = 18,
        ValidationFailed = 19,
        CorruptSave = 20
    }

    public enum PositionEmploymentProjectionAudience
    {
        Employee = 0,
        Applicant = 1,
        Supervisor = 2,
        Employer = 3,
        OrganizationMember = 4,
        ProfessionAuthority = 5,
        Public = 6,
        PersonKnown = 7,
        PersonBelieved = 8,
        PrivilegedDebug = 9,
        Redacted = 10
    }

    public enum PositionEmploymentHistoryHookKind
    {
        Unknown = 0,
        PositionCreated = 1,
        PersonApplied = 2,
        OfferMade = 3,
        PersonAppointed = 4,
        EmploymentBegan = 5,
        DutyAssigned = 6,
        PersonTransferred = 7,
        EmploymentSuspended = 8,
        PersonResigned = 9,
        PersonDismissed = 10,
        PersonRetired = 11,
        PositionClosed = 12,
        SecretPositionRevealed = 13,
        EmploymentDisputed = 14,
        Corrected = 15
    }

    public enum CareerEpisodeCategory
    {
        Custom = 0,
        InformalPractice = 1,
        FormalProfessionalPractice = 2,
        Education = 3,
        Apprenticeship = 4,
        Employment = 5,
        MilitaryService = 6,
        ReligiousService = 7,
        GovernmentService = 8,
        IndependentPractice = 9,
        ContractWorkFoundation = 10,
        CareerGap = 11,
        Retirement = 12,
        Suspension = 13
    }

    public enum CareerEpisodeState
    {
        Planned = 0,
        Active = 1,
        Suspended = 2,
        Ended = 3,
        Retired = 4,
        Corrected = 5,
        Disputed = 6,
        Invalid = 7,
        Custom = 8
    }

    public enum CareerClassification
    {
        Custom = 0,
        Primary = 1,
        Secondary = 2,
        FullTime = 3,
        PartTime = 4,
        Temporary = 5,
        Volunteer = 6,
        Service = 7,
        Informal = 8,
        Education = 9,
        Gap = 10,
        Retirement = 11
    }

    public enum CareerTransitionCategory
    {
        Custom = 0,
        ProfessionEntered = 1,
        ProfessionLeft = 2,
        EmploymentStarted = 3,
        EmploymentEnded = 4,
        Promotion = 5,
        Demotion = 6,
        Transfer = 7,
        SpecializationEntered = 8,
        SpecializationLeft = 9,
        Suspension = 10,
        Reinstatement = 11,
        Resignation = 12,
        Dismissal = 13,
        Retirement = 14,
        ReturnFromRetirement = 15,
        CareerChange = 16,
        ConcurrentCareerStarted = 17,
        CareerGapStarted = 18,
        CareerGapEnded = 19,
        Achievement = 20,
        Setback = 21,
        Correction = 22
    }

    public enum CareerTransitionSourceRecordType
    {
        None = 0,
        ProfessionRelationship = 1,
        Employment = 2,
        Position = 3,
        Rank = 4,
        Credential = 5,
        TrainingEnrollment = 6,
        ProfessionalActivity = 7,
        ExperienceEvidence = 8,
        CareerEpisode = 9,
        CareerTransition = 10,
        HistoricalRecordFoundation = 11,
        Custom = 12
    }

    public enum CareerMilestoneKind
    {
        Custom = 0,
        Achievement = 1,
        Setback = 2,
        FirstProfessionalWork = 3,
        MajorPromotion = 4,
        MasteryRecognition = 5,
        CredentialEarned = 6,
        ProductionAchievement = 7,
        ImportantDiscovery = 8,
        ApprenticeshipSuccess = 9,
        LeadershipRole = 10,
        Dismissal = 11,
        CredentialRevocation = 12,
        ProfessionalFailure = 13,
        Suspension = 14,
        CareerAffectingInjuryFoundation = 15,
        PublicDispute = 16
    }

    public enum CareerHistoryOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        MissingDefinition = 3,
        MissingPerson = 4,
        MissingEpisode = 5,
        MissingTransition = 6,
        MissingSourceRecord = 7,
        MissingRequirement = 8,
        UnauthorizedAuthority = 9,
        StaleState = 10,
        InvalidRequest = 11,
        InvalidState = 12,
        InvalidTransition = 13,
        InvalidDateRange = 14,
        PrimaryCareerConflict = 15,
        ExclusiveCareerConflict = 16,
        ValidationFailed = 17,
        CorruptSave = 18
    }

    public enum CareerHistoryProjectionAudience
    {
        SubjectPerson = 0,
        Employer = 1,
        Supervisor = 2,
        Organization = 3,
        ProfessionAuthority = 4,
        Public = 5,
        PersonKnown = 6,
        PersonBelieved = 7,
        PrivilegedDebug = 8,
        Redacted = 9
    }

    public enum CareerHistoryHookKind
    {
        Unknown = 0,
        EpisodeStarted = 1,
        EpisodeEnded = 2,
        TransitionRecorded = 3,
        PrimaryCareerChanged = 4,
        CareerGapStarted = 5,
        CareerGapEnded = 6,
        RetirementRecorded = 7,
        ReturnRecorded = 8,
        AchievementRecorded = 9,
        SetbackRecorded = 10,
        Corrected = 11
    }

    public enum LifePathState
    {
        Forming = 0,
        Active = 1,
        Stable = 2,
        Transitional = 3,
        Conflicted = 4,
        Interrupted = 5,
        Recovering = 6,
        RetiredFoundation = 7,
        FulfilledFoundation = 8,
        Abandoned = 9,
        Unknown = 10,
        Custom = 11
    }

    public enum FormativeReferenceKind
    {
        Custom = 0,
        PlaceOrCommunityOrigin = 1,
        Origin = PlaceOrCommunityOrigin,
        HouseholdOrFamilyBackground = 2,
        SocialStatusOrigin = 3,
        EarlyEducation = 4,
        ChildhoodOccupationExposure = 5,
        CulturalUpbringing = 6,
        ReligiousUpbringing = 7,
        MilitaryUpbringing = 8,
        InstitutionalUpbringing = 9,
        MajorFormativeEvent = 10,
        MentorOrInfluentialPerson = 11,
        CustomBackgroundSubject = 12
    }

    public enum AspirationCategory
    {
        Custom = 0,
        EnterProfession = 1,
        CompleteEducation = 2,
        EarnCredential = 3,
        ReachRank = 4,
        AchieveMastery = 5,
        GainSpecialization = 6,
        ObtainPosition = 7,
        LeadOrganizationFoundation = 8,
        CompleteImportantWork = 9,
        CreateMasterwork = 10,
        MakeDiscovery = 11,
        TeachOthers = 12,
        RetireSuccessfully = 13,
        ChangeCareer = 14,
        RestoreLostStatus = 15,
        PersonalService = 16,
        ProfessionalIdentity = 17
    }

    public enum LifeGoalCategory
    {
        Custom = 0,
        EnterProfession = 1,
        CompleteProgram = 2,
        CompleteApprenticeship = 3,
        GainExperience = 4,
        EarnCredential = 5,
        PassExamination = 6,
        ReachRank = 7,
        GainMastery = 8,
        ObtainPosition = 9,
        CompleteWorkOrder = 10,
        ProduceQualifyingItem = 11,
        TeachLearner = 12,
        MakeDiscovery = 13,
        ResumeCareer = 14,
        Retire = 15
    }

    public enum LifePathTargetSubjectType
    {
        Custom = 0,
        Profession = 1,
        Specialization = 2,
        Rank = 3,
        Credential = 4,
        Position = 5,
        Organization = 6,
        TrainingProgram = 7,
        ProfessionalActivity = 8,
        CareerEpisode = 9,
        CareerTransition = 10,
        Item = 11,
        Achievement = 12,
        Discovery = 13,
        Person = 14,
        HistoricalRecord = 15
    }

    public enum LifePathCompletionPolicy
    {
        Manual = 0,
        AllRequiredGoalsComplete = 1,
        AnyAlternativeGoalComplete = 2,
        AuthoritativeTargetExists = 3,
        Custom = 4
    }

    public enum LifePathFailurePolicy
    {
        Manual = 0,
        AnyRequiredGoalFailed = 1,
        DeadlineExpired = 2,
        AuthoritativeTargetImpossible = 3,
        Custom = 4
    }

    public enum PersonAspirationState
    {
        Desired = 0,
        Considering = 1,
        Active = 2,
        Paused = 3,
        Blocked = 4,
        Conflicted = 5,
        Fulfilled = 6,
        Failed = 7,
        Abandoned = 8,
        Replaced = 9,
        Superseded = 10,
        Secret = 11,
        Disputed = 12,
        Custom = 13
    }

    public enum PersonGoalState
    {
        Planned = 0,
        Active = 1,
        Paused = 2,
        Blocked = 3,
        Completed = 4,
        Failed = 5,
        Abandoned = 6,
        Replaced = 7,
        Cancelled = 8,
        Expired = 9,
        Secret = 10,
        Custom = 11
    }

    public enum LifeGoalProgressState
    {
        Unknown = 0,
        NotStarted = 1,
        InProgress = 2,
        Blocked = 3,
        Satisfied = 4,
        Failed = 5,
        Redacted = 6,
        Custom = 7
    }

    public enum ProfessionalIdentityKind
    {
        Primary = 0,
        Secondary = 1,
        Aspirational = 2,
        Former = 3,
        Retired = 4,
        Secret = 5,
        Disputed = 6,
        PublicFoundation = 7,
        Custom = 8
    }

    public enum ProfessionalIdentityAlignmentState
    {
        Aligned = 0,
        PartiallyAligned = 1,
        Aspirational = 2,
        Outdated = 3,
        Conflicted = 4,
        Disputed = 5,
        Secret = 6,
        Lost = 7,
        Rebuilding = 8,
        Unknown = 9,
        Custom = 10
    }

    public enum LifePathAchievementSetbackKind
    {
        Custom = 0,
        Achievement = 1,
        Setback = 2,
        ProfessionEntered = 3,
        ProgramCompleted = 4,
        CredentialEarned = 5,
        RankGained = 6,
        MasteryRecognized = 7,
        ImportantPositionObtained = 8,
        MajorWorkCompleted = 9,
        MasterworkProduced = 10,
        DiscoveryConfirmed = 11,
        Promotion = 12,
        Dismissal = 13,
        CredentialRevoked = 14,
        RankLost = 15,
        FailedExamination = 16,
        MajorProfessionalFailure = 17,
        CareerAffectingInjuryFoundation = 18,
        Retirement = 19,
        ReturnFromRetirement = 20
    }

    public enum LifePathOperationStatus
    {
        Succeeded = 0,
        Preview = 1,
        Duplicate = 2,
        MissingDefinition = 3,
        MissingPerson = 4,
        MissingLifePath = 5,
        MissingAspiration = 6,
        MissingGoal = 7,
        MissingIdentity = 8,
        MissingSourceRecord = 9,
        MissingRequirement = 10,
        StaleProgress = 11,
        InvalidRequest = 12,
        InvalidState = 13,
        InvalidTransition = 14,
        InvalidDateRange = 15,
        Conflict = 16,
        Cycle = 17,
        ValidationFailed = 18,
        CorruptSave = 19
    }

    public enum LifePathProjectionAudience
    {
        SubjectPerson = 0,
        Mentor = 1,
        Instructor = 2,
        EmployerFoundation = 3,
        Organization = 4,
        FriendOrFamilyFoundation = 5,
        Public = 6,
        PersonKnown = 7,
        PersonBelieved = 8,
        PrivilegedDebug = 9,
        Redacted = 10
    }

    public enum LifePathHookKind
    {
        Unknown = 0,
        LifePathCreated = 1,
        AspirationAdopted = 2,
        MajorGoalBegun = 3,
        GoalCompleted = 4,
        GoalFailed = 5,
        AspirationFulfilled = 6,
        AspirationAbandoned = 7,
        ProfessionalIdentityChanged = 8,
        IdentityConflictRecorded = 9,
        IdentityConflictResolved = 10,
        MajorSetback = 11,
        AchievementRecorded = 12,
        LifeDirectionChanged = 13,
        PersonRetired = 14,
        PersonReturnedToProfessionalLife = 15,
        Corrected = 16
    }
}
