using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.GameData.Persistence
{
    public enum PersistenceLoadPhase
    {
        Bootstrap = 0,
        ActorBase = 100,
        IdentityAndProgression = 150,
        Attributes = 175,
        Skills = 190,
        Inventory = 200,
        Equipment = 300,
        Statuses = 400,
        Vitals = 500,
        QuestsAndContracts = 600,
        PositionAndPlace = 700,
        Notification = 800
    }

    public enum PersistenceScope
    {
        Player = 0,
        SharedWorld = 100,
        RegionOrScene = 200,
        Account = 300,
        SessionOnly = 400
    }

    public enum PersistenceSaveStatus
    {
        Success,
        OperationAlreadyRunning,
        InvalidSlotId,
        NoParticipants,
        DependencyValidationFailed,
        UnsafeRuntimeState,
        ParticipantCaptureFailed,
        ParticipantValidationFailed,
        SerializationFailed,
        DirectoryCreationFailed,
        TemporaryWriteFailed,
        BackupFailed,
        ReplacementFailed,
        UnknownException
    }

    public enum PersistenceLoadStatus
    {
        Success,
        OperationAlreadyRunning,
        InvalidSlotId,
        FileMissing,
        ReadFailed,
        MalformedJson,
        WrongFormatIdentifier,
        UnsupportedSchemaVersion,
        UnsupportedGameVersion,
        WrongSaveContext,
        WrongSlot,
        WrongWorld,
        WrongPlayer,
        WrongAccount,
        IncompleteWrite,
        ChecksumMismatch,
        DuplicateParticipantKey,
        MissingRequiredParticipantPayload,
        MissingRuntimeParticipant,
        ParticipantPayloadParseFailed,
        ParticipantPrepareFailed,
        ParticipantCommitFailed,
        DependencyValidationFailed,
        RollbackCaptureFailed,
        ParticipantCommitFailedRollbackSucceeded,
        ParticipantCommitFailedRollbackFailed,
        ConsistencyAuditFailedRollbackSucceeded,
        ConsistencyAuditFailedRollbackFailed,
        UnsafeRuntimeState,
        BackupAvailable,
        BackupLoadFailed,
        UnknownException
    }

    public enum PersistenceDeleteStatus
    {
        Success,
        OperationAlreadyRunning,
        InvalidSlotId,
        DeleteFailed,
        UnknownException
    }

    public enum PersistenceValidationStatus
    {
        Valid,
        InvalidSlotId,
        FileMissing,
        ReadFailed,
        MalformedJson,
        WrongFormatIdentifier,
        UnsupportedSchemaVersion,
        UnsupportedGameVersion,
        WrongSaveContext,
        WrongSlot,
        WrongWorld,
        WrongPlayer,
        WrongAccount,
        IncompleteWrite,
        ChecksumMismatch,
        DuplicateParticipantKey,
        DependencyValidationFailed,
        MissingRequiredParticipantPayload,
        MissingRuntimeParticipant,
        ParticipantPayloadParseFailed,
        ParticipantPrepareFailed,
        BackupAvailable,
        UnknownException
    }

    public enum SaveSlotKind
    {
        Manual = 0,
        Autosave = 100,
        Quicksave = 200,
        Recovery = 300
    }

    public enum SaveCompatibilityStatus
    {
        Compatible = 0,
        FutureVersion = 300,
        WrongWorld = 500,
        WrongPlayer = 600,
        WrongContext = 650,
        Corrupted = 700,
        Empty = 800
    }

    public enum PersistenceOperationState
    {
        Idle = 0,
        Capturing = 100,
        Validating = 200,
        Writing = 300,
        RotatingAutosaves = 400,
        PreparingLoad = 500,
        CommittingParticipants = 600,
        RecoveringBackup = 700,
        Deleting = 800,
        ValidatingSlot = 900,
        RollingBack = 950,
        ConsistencyAudit = 975,
        Failed = 1000
    }

    public enum PersistenceTransactionPhase
    {
        Idle = 0,
        Eligibility = 100,
        Capture = 200,
        ValidateCapture = 300,
        BuildEnvelope = 400,
        WriteTemporary = 500,
        VerifyTemporary = 600,
        PreservePrevious = 700,
        PromotePrimary = 800,
        UpdateMetadata = 900,
        Read = 1000,
        VerifyIntegrity = 1100,
        ValidateEnvelope = 1200,
        ResolveDependencies = 1300,
        PrepareParticipants = 1400,
        PrepareScene = 1500,
        CaptureRollback = 1600,
        CommitParticipants = 1700,
        CommitLocation = 1800,
        ConsistencyAudit = 1900,
        RefreshRuntime = 2000,
        RollingBack = 2100,
        Finalize = 2200,
        Failed = 2300
    }

    public enum PersistenceRuntimeSafety
    {
        Safe = 0,
        Restoring = 100,
        RolledBack = 200,
        Degraded = 300,
        Unsafe = 400
    }

    public enum PersistenceConsistencySeverity
    {
        Info = 0,
        Warning = 100,
        Recoverable = 200,
        Critical = 300
    }

    public enum SaveRecoverySource
    {
        RequestedPrimary = 0,
        RequestedBackup = 100,
        AutosaveNewest = 200,
        AutosavePrevious = 300,
        ValidTemporary = 400,
        InMemoryRollback = 500
    }

    public enum RecoveryRecommendationAction
    {
        None = 0,
        LoadBackup = 100,
        PromoteBackup = 200,
        LoadAutosave = 300,
        InspectTemporary = 400,
        QuarantinePrimary = 500,
        RestartPlayMode = 600
    }

    public enum PersistenceFaultInjectionPoint
    {
        None = 0,
        SaveCapture = 100,
        TemporaryVerification = 200,
        BackupPreservation = 300,
        PrimaryPromotion = 400,
        LoadPrepare = 500,
        LoadCommit = 600,
        ConsistencyAudit = 700,
        RollbackCommit = 800
    }

    public enum SaveEligibilityStatus
    {
        Allowed = 0,
        OperationInProgress = 100,
        InvalidPlayerState = 200,
        SceneTransition = 300,
        NoActivePlayer = 400,
        ModalBlocked = 500,
        ParticipantCaptureFailed = 600,
        Unknown = 900
    }

    [Serializable]
    public sealed class GameSaveEnvelope
    {
        public string formatIdentifier;
        public int schemaVersion;
        public int persistenceContext;
        public string gameVersion;
        public string saveId;
        public string slotId;
        public string displayName;
        public string worldId;
        public string playerId;
        public string accountId;
        public string createdUtc;
        public string lastWrittenUtc;
        public double playtimeSeconds;
        public string sceneSummary;
        public string placeSummary;
        public string playerSummary;
        public string transactionId;
        public string parentTransactionId;
        public int saveRevision;
        public bool completedWriteMarker;
        public string contentChecksum;
        public List<SaveParticipantRecord> participants = new List<SaveParticipantRecord>();
    }

    [Serializable]
    public sealed class SaveParticipantRecord
    {
        public string participantKey;
        public int participantSchemaVersion;
        public bool required;
        public int persistenceScope;
        public string ownerId;
        public int loadPhase;
        public int loadPriority;
        public string payloadJson;
    }

    [Serializable]
    public sealed class SaveSlotMetadata
    {
        public string slotId;
        public string displayName;
        public string saveId;
        public string createdUtc;
        public string lastWrittenUtc;
        public string modifiedUtc;
        public double playtimeSeconds;
        public string sceneSummary;
        public string currentPlaceSummary;
        public string playerSummary;
        public string worldId;
        public string playerId;
        public string accountId;
        public int schemaVersion;
        public string gameVersion;
        public string transactionId;
        public string parentTransactionId;
        public int saveRevision;
        public bool completedWriteMarker;
        public long fileSizeBytes;
        public bool hasPrimary;
        public bool hasBackup;
        public bool isValid;
        public string status;
        public string message;
    }

    [Serializable]
    public sealed class SaveSlotDescriptor
    {
        public string slotId;
        public SaveSlotKind slotKind;
        public string displayName;
        public bool exists;
        public bool isValid;
        public bool primaryExists;
        public bool backupExists;
        public int schemaVersion;
        public string createdAtUtc;
        public string lastSavedAtUtc;
        public double playTimeSeconds;
        public string sceneKey;
        public string placeId;
        public string placeDisplayName;
        public string playerDisplayName;
        public string currentHealthSummary;
        public PersistenceValidationStatus validationStatus;
        public SaveCompatibilityStatus compatibilityStatus;
        public string lastErrorCode;
        public string message;
        public long fileSizeBytes;
        public int saveGeneration;
        public bool isNewestAutosave;
        public string transactionId;
        public int saveRevision;

        public bool CanSave => slotKind == SaveSlotKind.Manual;
        public bool CanLoad => exists && isValid && compatibilityStatus == SaveCompatibilityStatus.Compatible;
        public bool CanDelete => slotKind == SaveSlotKind.Manual && exists;
        public bool CanValidate => exists || backupExists;
        public bool CanLoadBackup => backupExists;
    }

    public readonly struct SaveEligibilityResult
    {
        private SaveEligibilityResult(bool allowed, SaveEligibilityStatus status, string message)
        {
            Allowed = allowed;
            Status = status;
            Message = string.IsNullOrWhiteSpace(message) ? status.ToString() : message;
        }

        public bool Allowed { get; }
        public SaveEligibilityStatus Status { get; }
        public string Message { get; }

        public static SaveEligibilityResult Allow(string message = "Saving is allowed.")
        {
            return new SaveEligibilityResult(true, SaveEligibilityStatus.Allowed, message);
        }

        public static SaveEligibilityResult Block(SaveEligibilityStatus status, string message)
        {
            return new SaveEligibilityResult(false, status, message);
        }
    }

    public sealed class PersistenceSaveResult
    {
        private PersistenceSaveResult(bool succeeded, PersistenceSaveStatus status, string slotId, string path, string message, Exception exception, string transactionId, PersistenceTransactionPhase phase)
        {
            Succeeded = succeeded;
            Status = status;
            SlotId = slotId;
            Path = path;
            Message = message;
            Exception = exception;
            TransactionId = transactionId ?? string.Empty;
            Phase = phase;
        }

        public bool Succeeded { get; }
        public PersistenceSaveStatus Status { get; }
        public string SlotId { get; }
        public string Path { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public string TransactionId { get; }
        public PersistenceTransactionPhase Phase { get; }

        public static PersistenceSaveResult Success(string slotId, string path, string message, string transactionId = "", PersistenceTransactionPhase phase = PersistenceTransactionPhase.Finalize)
        {
            return new PersistenceSaveResult(true, PersistenceSaveStatus.Success, slotId, path, message, null, transactionId, phase);
        }

        public static PersistenceSaveResult Failure(PersistenceSaveStatus status, string slotId, string path, string message, Exception exception = null, string transactionId = "", PersistenceTransactionPhase phase = PersistenceTransactionPhase.Failed)
        {
            return new PersistenceSaveResult(false, status, slotId, path, message, exception, transactionId, phase);
        }
    }

    public sealed class PersistenceLoadResult
    {
        private PersistenceLoadResult(bool succeeded, PersistenceLoadStatus status, string slotId, string path, bool loadedBackup, bool backupAvailable, bool rollbackAttempted, bool rollbackSucceeded, bool fallbackApplied, string fallbackCode, string message, Exception exception, string transactionId, PersistenceTransactionPhase phase, PersistenceRuntimeSafety runtimeSafety)
        {
            Succeeded = succeeded;
            Status = status;
            SlotId = slotId;
            Path = path;
            LoadedBackup = loadedBackup;
            BackupAvailable = backupAvailable;
            RollbackAttempted = rollbackAttempted;
            RollbackSucceeded = rollbackSucceeded;
            FallbackApplied = fallbackApplied;
            FallbackCode = fallbackCode ?? string.Empty;
            Message = message;
            Exception = exception;
            TransactionId = transactionId ?? string.Empty;
            Phase = phase;
            RuntimeSafety = runtimeSafety;
        }

        public bool Succeeded { get; }
        public PersistenceLoadStatus Status { get; }
        public string SlotId { get; }
        public string Path { get; }
        public bool LoadedBackup { get; }
        public bool BackupAvailable { get; }
        public bool RollbackAttempted { get; }
        public bool RollbackSucceeded { get; }
        public bool FallbackApplied { get; }
        public string FallbackCode { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public string TransactionId { get; }
        public PersistenceTransactionPhase Phase { get; }
        public PersistenceRuntimeSafety RuntimeSafety { get; }

        public static PersistenceLoadResult Success(string slotId, string path, bool loadedBackup, string message, string transactionId = "", PersistenceTransactionPhase phase = PersistenceTransactionPhase.Finalize, bool fallbackApplied = false, string fallbackCode = "")
        {
            return new PersistenceLoadResult(true, PersistenceLoadStatus.Success, slotId, path, loadedBackup, false, false, false, fallbackApplied, fallbackCode, message, null, transactionId, phase, fallbackApplied ? PersistenceRuntimeSafety.Degraded : PersistenceRuntimeSafety.Safe);
        }

        public static PersistenceLoadResult Failure(PersistenceLoadStatus status, string slotId, string path, string message, bool backupAvailable = false, Exception exception = null, string transactionId = "", PersistenceTransactionPhase phase = PersistenceTransactionPhase.Failed, bool rollbackAttempted = false, bool rollbackSucceeded = false, PersistenceRuntimeSafety runtimeSafety = PersistenceRuntimeSafety.Safe)
        {
            return new PersistenceLoadResult(false, status, slotId, path, false, backupAvailable, rollbackAttempted, rollbackSucceeded, false, string.Empty, message, exception, transactionId, phase, runtimeSafety);
        }
    }

    public sealed class PersistenceDeleteResult
    {
        private PersistenceDeleteResult(bool succeeded, PersistenceDeleteStatus status, string slotId, string message, Exception exception)
        {
            Succeeded = succeeded;
            Status = status;
            SlotId = slotId;
            Message = message;
            Exception = exception;
        }

        public bool Succeeded { get; }
        public PersistenceDeleteStatus Status { get; }
        public string SlotId { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public static PersistenceDeleteResult Success(string slotId, string message)
        {
            return new PersistenceDeleteResult(true, PersistenceDeleteStatus.Success, slotId, message, null);
        }

        public static PersistenceDeleteResult Failure(PersistenceDeleteStatus status, string slotId, string message, Exception exception = null)
        {
            return new PersistenceDeleteResult(false, status, slotId, message, exception);
        }
    }

    public sealed class PersistenceValidationResult
    {
        private PersistenceValidationResult(bool succeeded, PersistenceValidationStatus status, string slotId, string path, bool backupAvailable, GameSaveEnvelope envelope, string message, Exception exception)
        {
            Succeeded = succeeded;
            Status = status;
            SlotId = slotId;
            Path = path;
            BackupAvailable = backupAvailable;
            Envelope = envelope;
            Message = message;
            Exception = exception;
        }

        public bool Succeeded { get; }
        public PersistenceValidationStatus Status { get; }
        public string SlotId { get; }
        public string Path { get; }
        public bool BackupAvailable { get; }
        public GameSaveEnvelope Envelope { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public static PersistenceValidationResult Success(string slotId, string path, GameSaveEnvelope envelope, string message)
        {
            return new PersistenceValidationResult(true, PersistenceValidationStatus.Valid, slotId, path, false, envelope, message, null);
        }

        public static PersistenceValidationResult Failure(PersistenceValidationStatus status, string slotId, string path, string message, bool backupAvailable = false, Exception exception = null)
        {
            return new PersistenceValidationResult(false, status, slotId, path, backupAvailable, null, message, exception);
        }
    }

    public sealed class PersistenceParticipantDependencyMetadata
    {
        public string participantKey;
        public string[] requiredDependencies = Array.Empty<string>();
        public string[] optionalDependencies = Array.Empty<string>();
        public bool supportsRollback;
        public bool requiresSceneReadiness;
        public bool requiresDefinitionRegistry;
        public bool requiresWorldEntityRegistry;
    }

    public sealed class PersistenceDependencyReport
    {
        public bool succeeded;
        public string message;
        public string[] orderedParticipantKeys = Array.Empty<string>();
        public string[] missingRequiredDependencies = Array.Empty<string>();
        public string[] missingOptionalDependencies = Array.Empty<string>();
        public string[] circularDependencies = Array.Empty<string>();
        public PersistenceParticipantDependencyMetadata[] participants = Array.Empty<PersistenceParticipantDependencyMetadata>();
    }

    public sealed class PersistenceConsistencyFinding
    {
        public PersistenceConsistencySeverity severity;
        public string code;
        public string message;
        public string participantKey;
    }

    public sealed class PersistenceConsistencyAuditReport
    {
        public bool succeeded;
        public string message;
        public PersistenceConsistencyFinding[] findings = Array.Empty<PersistenceConsistencyFinding>();

        public bool HasCriticalFinding
        {
            get
            {
                if (findings == null)
                {
                    return false;
                }

                for (int i = 0; i < findings.Length; i++)
                {
                    if (findings[i] != null && findings[i].severity == PersistenceConsistencySeverity.Critical)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static PersistenceConsistencyAuditReport Success(string message = "Persistence consistency audit passed.")
        {
            return new PersistenceConsistencyAuditReport { succeeded = true, message = message };
        }

        public static PersistenceConsistencyAuditReport Critical(string code, string message, string participantKey = "")
        {
            return new PersistenceConsistencyAuditReport
            {
                succeeded = false,
                message = message,
                findings = new[]
                {
                    new PersistenceConsistencyFinding
                    {
                        severity = PersistenceConsistencySeverity.Critical,
                        code = code,
                        message = message,
                        participantKey = participantKey
                    }
                }
            };
        }
    }

    public sealed class SaveRecoveryCandidate
    {
        public SaveRecoverySource source;
        public RecoveryRecommendationAction action;
        public string slotId;
        public string path;
        public bool valid;
        public string message;
    }

    public sealed class SaveRecoveryScanReport
    {
        public string scannedAtUtc;
        public SaveRecoveryCandidate[] candidates = Array.Empty<SaveRecoveryCandidate>();
        public string[] staleTemporaryFiles = Array.Empty<string>();
        public string[] corruptFiles = Array.Empty<string>();
        public string recommendation;
    }

    public sealed class PersistenceTransactionDiagnostics
    {
        public string transactionId;
        public PersistenceTransactionPhase phase;
        public PersistenceOperationState operationState;
        public PersistenceRuntimeSafety runtimeSafety;
        public bool operationInProgress;
        public string[] participantOrder = Array.Empty<string>();
        public string lastRecoveryRecommendation;
        public string lastConsistencyAudit;
    }

    public sealed class PersistenceFaultInjection
    {
        public PersistenceFaultInjectionPoint nextFailurePoint;
        public string message = "Injected persistence failure.";

        public bool Consume(PersistenceFaultInjectionPoint point, out string failureMessage)
        {
            failureMessage = string.Empty;
            if (nextFailurePoint != point)
            {
                return false;
            }

            nextFailurePoint = PersistenceFaultInjectionPoint.None;
            failureMessage = string.IsNullOrWhiteSpace(message) ? "Injected persistence failure." : message;
            return true;
        }
    }

    public sealed class PersistenceReadinessReport
    {
        public bool succeeded;
        public string context;
        public string message;
        public string[] requiredParticipants = Array.Empty<string>();
        public string[] registeredParticipants = Array.Empty<string>();
        public string[] failures = Array.Empty<string>();
    }

    public static class PersistenceRestorationGuard
    {
        private static int depth;
        private static bool rollingBack;
        private static string transactionId;

        public static bool IsActive => depth > 0;
        public static bool IsRollingBack => IsActive && rollingBack;
        public static string TransactionId => transactionId ?? string.Empty;

        public static Scope Enter(string activeTransactionId, bool rollback)
        {
            depth++;
            transactionId = activeTransactionId ?? string.Empty;
            rollingBack = rollback;
            return new Scope();
        }

        public readonly struct Scope : IDisposable
        {
            public void Dispose()
            {
                if (depth > 0)
                {
                    depth--;
                }

                if (depth == 0)
                {
                    transactionId = string.Empty;
                    rollingBack = false;
                }
            }
        }
    }
}
