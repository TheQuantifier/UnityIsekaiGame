namespace UnityIsekaiGame.Inventory.Production
{
    public enum ProductionChainLifecycleState
    {
        Draft = 0,
        Active = 10,
        Deprecated = 20,
        Retired = 30
    }

    public enum ProductionStageCategory
    {
        Unknown = 0,
        Preparation = 10,
        Processing = 20,
        Assembly = 30,
        Refinement = 40,
        QualityControl = 50,
        Packaging = 60,
        Custom = 100
    }

    public enum ProductionProgressModel
    {
        TimeBased = 0,
        WorkUnitBased = 10,
        StepCompletion = 20,
        InstantWhenReady = 30,
        Custom = 100
    }

    public enum ProductionInputConsumptionPolicy
    {
        ReservedAtStartConsumedAtCompletion = 0,
        ConsumeAtStart = 10,
        ConsumeAtCompletion = 20,
        Incremental = 30,
        Mixed = 40
    }

    public enum ProductionToolWearTiming
    {
        None = 0,
        StageStart = 10,
        StageCompletion = 20,
        Checkpoint = 30,
        Incremental = 40
    }

    public enum ProductionPartialBatchPolicy
    {
        AllOrNothing = 0,
        PerUnitCommit = 10,
        SubBatchCommit = 20,
        PreserveRemainingWip = 30,
        Custom = 100
    }

    public enum ProductionBatchConsistencyPolicy
    {
        IdenticalAuthoritativeState = 0,
        SharedQualityWithUnitVariance = 10,
        SharedCompositionWithUnitQuality = 20,
        SharedAffixPool = 30,
        IndependentUnits = 40,
        BulkMaterialLot = 50,
        Custom = 100
    }

    public enum ProductionWorkOrderState
    {
        Draft = 0,
        Submitted = 10,
        Approved = 20,
        Rejected = 30,
        Planned = 40,
        Queued = 50,
        InProgress = 60,
        Paused = 70,
        Blocked = 80,
        Completed = 90,
        PartiallyCompleted = 100,
        Failed = 110,
        Cancelled = 120,
        Archived = 130,
        Invalid = 140,
        Custom = 1000
    }

    public enum ProductionJobState
    {
        Created = 0,
        Planned = 10,
        AwaitingRequirements = 20,
        AwaitingResources = 30,
        AwaitingWorker = 40,
        AwaitingStation = 50,
        Queued = 60,
        Ready = 70,
        Running = 80,
        Paused = 90,
        Interrupted = 100,
        Blocked = 110,
        Completing = 120,
        Completed = 130,
        PartiallyCompleted = 140,
        Failed = 150,
        Cancelled = 160,
        RolledBack = 170,
        Invalid = 180,
        Unresolved = 190,
        AwaitingCollection = 200,
        Custom = 1000
    }

    public enum ProductionStageRuntimeState
    {
        Pending = 0,
        Ready = 10,
        Running = 20,
        Paused = 30,
        Blocked = 40,
        Interrupted = 50,
        ReadyToComplete = 60,
        Completed = 70,
        Skipped = 80,
        Failed = 90,
        Cancelled = 100
    }

    public enum ProductionQueueState
    {
        Active = 0,
        Paused = 10,
        Archived = 20
    }

    public enum ProductionQueuePolicy
    {
        PriorityThenFifo = 0,
        StrictPriority = 10,
        FirstInFirstOut = 20,
        ManualOrder = 30,
        DeadlineFoundation = 40,
        DependencyFirst = 50,
        Custom = 100
    }

    public enum ProductionOccupancyState
    {
        Active = 0,
        Released = 10,
        Interrupted = 20,
        Cancelled = 30
    }

    public enum ProductionWorkerAssignmentState
    {
        Assigned = 0,
        Active = 10,
        Paused = 20,
        Released = 30,
        Invalid = 40
    }

    public enum ProductionWorkerRole
    {
        PrimaryCrafter = 0,
        Assistant = 10,
        Supervisor = 20,
        Inspector = 30,
        ToolOperator = 40,
        StationOperator = 50,
        MaterialHandler = 60,
        SafetyObserver = 70,
        Apprentice = 80,
        Specialist = 90,
        Disassembler = 100,
        Salvager = 110,
        Custom = 1000
    }

    public enum ProductionIntermediateState
    {
        Available = 0,
        Reserved = 10,
        Consumed = 20,
        PreservedByCancellation = 30,
        Waste = 40,
        Invalid = 50
    }

    public enum ProductionLotState
    {
        Active = 0,
        Split = 10,
        Merged = 20,
        Consumed = 30,
        Archived = 40,
        Invalid = 50
    }

    public enum ProductionOutputCollectionState
    {
        NotReady = 0,
        Ready = 10,
        PartiallyCollected = 20,
        Collected = 30,
        DeliveryBlocked = 40,
        DestinationFull = 50,
        AccessDenied = 60,
        Custom = 1000
    }

    public enum ProductionWorkflowStatus
    {
        Succeeded = 0,
        Preview = 10,
        Duplicate = 20,
        InvalidRequest = 30,
        MissingDefinition = 40,
        MissingWorkOrder = 50,
        MissingJob = 60,
        MissingQueue = 70,
        MissingStage = 80,
        InvalidState = 90,
        DependencyBlocked = 100,
        RequirementFailed = 110,
        ReservationFailed = 120,
        OccupancyFailed = 130,
        WorkerRejected = 140,
        TimeRejected = 150,
        CraftingFailed = 160,
        CollectionFailed = 170,
        ValidationFailed = 180,
        RestoreFailed = 190,
        RollbackFailed = 200
    }

    public enum ProductionProjectionAudience
    {
        PrivilegedDebug = 0,
        InternalAuthority = 10,
        WorkOrderRequester = 20,
        JobOwner = 30,
        AssignedWorker = 40,
        PublicObserver = 50
    }

    public enum ProductionProjectionDecision
    {
        FullAccess = 0,
        RedactedAccess = 10,
        Concealed = 20,
        Denied = 30
    }
}
