namespace UnityIsekaiGame.Inventory.Experimentation
{
    public enum ExperimentCategory
    {
        Unknown = 0,
        RecipeReconstruction = 10,
        RecipeFragmentTesting = 20,
        IngredientTesting = 30,
        QuantityTesting = 40,
        ProcedureOrderTesting = 50,
        ProcedureStepTesting = 60,
        SubstitutionTesting = 70,
        MaterialPropertyTesting = 80,
        MaterialCompatibilityTesting = 90,
        ToolCapabilityTesting = 100,
        StationCapabilityTesting = 110,
        QualityTesting = 120,
        AffixTesting = 130,
        DurabilityTesting = 140,
        BreakageTesting = 150,
        RepairTesting = 160,
        SalvageTesting = 170,
        DestructiveTesting = 180,
        ReverseEngineering = 190,
        FailureAnalysis = 200,
        ReproducibilityStudy = 210,
        ComparativeExperiment = 220,
        Custom = 1000
    }

    public enum ExperimentRunState
    {
        Draft = 0,
        Planned = 10,
        AwaitingRequirements = 20,
        AwaitingApproval = 30,
        Approved = 40,
        Rejected = 45,
        Ready = 50,
        Running = 60,
        Paused = 70,
        Interrupted = 80,
        Completed = 90,
        Inconclusive = 100,
        Failed = 110,
        Unsafe = 120,
        Cancelled = 130,
        Archived = 140,
        Invalid = 150,
        Custom = 1000
    }

    public enum ExperimentPlanMode
    {
        Controlled = 0,
        Exploratory = 10,
        UncontrolledObservation = 20,
        DestructiveTest = 30,
        ReverseEngineering = 40,
        ReproductionAttempt = 50,
        Comparative = 60,
        Custom = 1000
    }

    public enum ExperimentVariableCategory
    {
        Unknown = 0,
        IngredientIdentity = 10,
        IngredientQuantity = 20,
        MaterialPurity = 30,
        MaterialForm = 40,
        ToolIdentity = 50,
        ToolQuality = 60,
        ToolDurability = 70,
        StationIdentity = 80,
        StationCapability = 90,
        TemperatureFoundation = 100,
        Duration = 110,
        StepOrder = 120,
        WorkerSkill = 130,
        WorkerCount = 140,
        OptionalInput = 150,
        CatalystPresence = 160,
        BatchSize = 170,
        Environment = 180,
        Custom = 1000
    }

    public enum ExperimentVariableRole
    {
        Independent = 0,
        Dependent = 10,
        Controlled = 20,
        Observed = 30,
        Confounding = 40
    }

    public enum ExperimentValueType
    {
        None = 0,
        Boolean = 10,
        StableId = 20,
        Numeric = 30,
        Range = 40,
        Qualitative = 50,
        Text = 60
    }

    public enum HypothesisClaimType
    {
        Unknown = 0,
        RecipeRequiresInput = 10,
        RecipeQuantityWithinRange = 20,
        RecipeStepOrder = 30,
        MaterialSubstitutesForMaterial = 40,
        MaterialHasProperty = 50,
        MaterialCompatibilityOutcome = 60,
        ToolSatisfiesRole = 70,
        StationProvidesCapability = 80,
        AffixAppearsUnderCondition = 90,
        QualityChangesWithVariable = 100,
        DurabilityChangesUnderExposure = 110,
        HiddenCatalystExists = 120,
        FailureBelowThreshold = 130,
        OutputDefinitionIs = 140,
        ProcedureVariantProducesOutput = 150,
        Custom = 1000
    }

    public enum HypothesisStatus
    {
        Proposed = 0,
        Testable = 10,
        Testing = 20,
        Supported = 30,
        WeaklySupported = 40,
        Contradicted = 50,
        Falsified = 60,
        Inconclusive = 70,
        ConfirmedUnderPolicy = 80,
        Superseded = 90,
        Withdrawn = 100,
        Custom = 1000
    }

    public enum HypothesisTestabilityState
    {
        Unknown = 0,
        Testable = 10,
        NeedsControls = 20,
        NeedsInputs = 30,
        NeedsObservation = 40,
        Unsafe = 50,
        Untestable = 60
    }

    public enum ExperimentTrialKind
    {
        Control = 0,
        Experimental = 10,
        Reproduction = 20,
        Observation = 30,
        Destructive = 40,
        ReverseEngineering = 50
    }

    public enum ExperimentTrialOutcome
    {
        Unknown = 0,
        ExpectedSuccess = 10,
        UnexpectedSuccess = 20,
        ExpectedFailure = 30,
        UnexpectedFailure = 40,
        PartialSuccess = 50,
        DifferentValidOutput = 60,
        DamagedOutput = 70,
        DefectiveOutput = 80,
        AccidentalByproduct = 90,
        NoReaction = 100,
        UnsafeOutcome = 110,
        Inconclusive = 120,
        Interrupted = 130,
        Invalid = 140,
        Custom = 1000
    }

    public enum ExperimentMeasurementKind
    {
        ObservationQuality = 0,
        Quantity = 10,
        MaterialProperty = 20,
        ToolCapability = 30,
        StationCapability = 40,
        Quality = 50,
        Affix = 60,
        Durability = 70,
        Safety = 80,
        Output = 90,
        Custom = 1000
    }

    public enum ExperimentEvidenceRole
    {
        Supporting = 0,
        Contradicting = 10,
        Corrective = 20,
        Neutral = 30
    }

    public enum ExperimentInferenceType
    {
        RecipeFragment = 0,
        QuantityRange = 10,
        ProcedureStep = 20,
        ProcedureOrder = 30,
        Substitution = 40,
        Variant = 50,
        MaterialProperty = 60,
        ToolCapability = 70,
        StationCapability = 80,
        QualityRelationship = 90,
        AffixCondition = 100,
        DurabilityBehavior = 110,
        FailureCondition = 120,
        SafetyFinding = 130,
        Custom = 1000
    }

    public enum DiscoveryClaimStatus
    {
        Draft = 0,
        Proposed = 10,
        UnderReview = 20,
        ReproductionRequested = 30,
        ProvisionallyAccepted = 40,
        Confirmed = 50,
        Rejected = 60,
        Superseded = 70,
        Withdrawn = 80
    }

    public enum DiscoveryReviewDecision
    {
        None = 0,
        AcceptProvisionally = 10,
        RequestReproduction = 20,
        Confirm = 30,
        Reject = 40,
        Withdraw = 50
    }

    public enum ExperimentSafetyState
    {
        Unknown = 0,
        Safe = 10,
        Caution = 20,
        Hazardous = 30,
        UnsafeBlocked = 40,
        AuthorizedDangerous = 50
    }

    public enum ExperimentOperationStatus
    {
        Success = 0,
        Preview = 10,
        Duplicate = 20,
        InvalidRequest = 30,
        MissingDefinition = 40,
        MissingPlan = 50,
        MissingRun = 60,
        MissingTrial = 70,
        MissingHypothesis = 80,
        MissingRuntime = 90,
        RequirementFailed = 100,
        ReservationFailed = 110,
        ExecutionFailed = 120,
        KnowledgeRejected = 130,
        AccessDenied = 140,
        InvalidState = 150,
        ValidationFailed = 160,
        RestoreFailed = 170
    }

    public enum ExperimentProjectionDecision
    {
        FullAccess = 0,
        RedactedAccess = 10,
        Concealed = 20,
        Denied = 30
    }
}
