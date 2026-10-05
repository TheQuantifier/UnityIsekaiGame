using System;

namespace UnityIsekaiGame.GameData.Persistence
{
    public enum PersistenceContextKind
    {
        Player = 0,
        World = 100,
        Account = 200
    }

    public sealed class PersistenceParticipantDescriptor
    {
        public PersistenceParticipantDescriptor(
            string key,
            int schemaVersion,
            bool required,
            PersistenceScope scope,
            string ownerId,
            PersistenceLoadPhase loadPhase,
            int loadPriority,
            System.Collections.Generic.IReadOnlyList<string> requiredDependencies,
            System.Collections.Generic.IReadOnlyList<string> optionalDependencies,
            bool supportsRollback,
            bool requiresSceneReadiness,
            bool requiresDefinitionRegistry,
            bool requiresWorldEntityRegistry)
        {
            Key = key ?? string.Empty;
            SchemaVersion = schemaVersion;
            Required = required;
            Scope = scope;
            OwnerId = ownerId ?? string.Empty;
            LoadPhase = loadPhase;
            LoadPriority = loadPriority;
            RequiredDependencies = requiredDependencies ?? System.Array.Empty<string>();
            OptionalDependencies = optionalDependencies ?? System.Array.Empty<string>();
            SupportsRollback = supportsRollback;
            RequiresSceneReadiness = requiresSceneReadiness;
            RequiresDefinitionRegistry = requiresDefinitionRegistry;
            RequiresWorldEntityRegistry = requiresWorldEntityRegistry;
        }

        public string Key { get; }
        public int SchemaVersion { get; }
        public bool Required { get; }
        public PersistenceScope Scope { get; }
        public string OwnerId { get; }
        public PersistenceLoadPhase LoadPhase { get; }
        public int LoadPriority { get; }
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies { get; }
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies { get; }
        public bool SupportsRollback { get; }
        public bool RequiresSceneReadiness { get; }
        public bool RequiresDefinitionRegistry { get; }
        public bool RequiresWorldEntityRegistry { get; }

        public static PersistenceParticipantDescriptor From(IPersistenceParticipant participant)
        {
            if (participant == null)
            {
                return null;
            }

            IPersistenceParticipantDependencies dependencies = participant as IPersistenceParticipantDependencies;
            return new PersistenceParticipantDescriptor(
                participant.ParticipantKey,
                participant.ParticipantSchemaVersion,
                participant.IsRequired,
                participant.Scope,
                participant.OwnerId,
                participant.LoadPhase,
                participant.LoadPriority,
                dependencies?.RequiredDependencies,
                dependencies?.OptionalDependencies,
                dependencies?.SupportsRollback ?? true,
                dependencies?.RequiresSceneReadiness ?? false,
                dependencies?.RequiresDefinitionRegistry ?? false,
                dependencies?.RequiresWorldEntityRegistry ?? false);
        }
    }

    public interface IPersistenceConsistencyValidator
    {
        string ValidatorKey { get; }
        bool IsRequired { get; }
        PersistenceConsistencyAuditReport Validate();
    }

    public sealed class SaveMetadataSnapshot
    {
        public string SceneId { get; set; } = string.Empty;
        public string PlaceId { get; set; } = string.Empty;
        public string PlayerSummary { get; set; } = string.Empty;
    }

    public interface ISaveMetadataProvider
    {
        SaveMetadataSnapshot CaptureMetadata();
    }

    public interface IPersistenceParticipant
    {
        string ParticipantKey { get; }
        int ParticipantSchemaVersion { get; }
        bool IsRequired { get; }
        PersistenceScope Scope { get; }
        string OwnerId { get; }
        PersistenceLoadPhase LoadPhase { get; }
        int LoadPriority { get; }

        PersistenceParticipantSaveResult CapturePayload();
        PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion);
        PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload);
        void DiscardPreparedPayload(object preparedPayload);
    }

    /// <summary>
    /// Captures an immutable object graph on the simulation thread and returns work that is safe to
    /// execute on a background thread. Implementations may defer validation, deterministic ordering,
    /// and serialization, but must not retain references to mutable runtime collections or Unity objects.
    /// </summary>
    public interface IDeferredPersistenceParticipant
    {
        DeferredPersistenceParticipantCapture CaptureDeferredPayload();
    }

    /// <summary>
    /// Supplies a monotonic content revision. A successful checkpoint may reuse the previous validated
    /// payload while this revision is unchanged. Implementations must advance the revision for every
    /// persisted mutation and after restore.
    /// </summary>
    public interface IPersistenceRevisionParticipant
    {
        long PersistenceRevision { get; }
    }

    /// <summary>
    /// Creates a bounded, simulation-thread capture which may span multiple frames. Each continuation
    /// must retain only detached data from work completed during that call.
    /// </summary>
    public interface IIncrementalPersistenceParticipant
    {
        IIncrementalPersistenceCapture BeginIncrementalCapture();
    }

    public interface IIncrementalPersistenceCapture
    {
        bool TryContinue(
            out bool completed,
            out DeferredPersistenceParticipantCapture completedCapture,
            out string failureReason);
    }

    public sealed class DeferredPersistenceParticipantCapture
    {
        private readonly Func<PersistenceParticipantSaveResult> serialize;

        private DeferredPersistenceParticipantCapture(bool succeeded, string message, Func<PersistenceParticipantSaveResult> serialize)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            this.serialize = serialize;
        }

        public bool Succeeded { get; }
        public string Message { get; }

        public PersistenceParticipantSaveResult SerializePayload()
        {
            if (!Succeeded || serialize == null)
            {
                return PersistenceParticipantSaveResult.Failure(string.IsNullOrWhiteSpace(Message) ? "Deferred participant capture is invalid." : Message);
            }

            try
            {
                return serialize.Invoke();
            }
            catch (Exception exception)
            {
                return PersistenceParticipantSaveResult.Failure($"Deferred participant serialization failed: {exception.Message}");
            }
        }

        public static DeferredPersistenceParticipantCapture Success(Func<PersistenceParticipantSaveResult> serialize)
        {
            return new DeferredPersistenceParticipantCapture(true, "Participant snapshot captured.", serialize ?? throw new ArgumentNullException(nameof(serialize)));
        }

        public static DeferredPersistenceParticipantCapture Failure(string message)
        {
            return new DeferredPersistenceParticipantCapture(false, message, null);
        }
    }

    public interface IPersistenceParticipantDependencies
    {
        System.Collections.Generic.IReadOnlyList<string> RequiredDependencies { get; }
        System.Collections.Generic.IReadOnlyList<string> OptionalDependencies { get; }
        bool SupportsRollback { get; }
        bool RequiresSceneReadiness { get; }
        bool RequiresDefinitionRegistry { get; }
        bool RequiresWorldEntityRegistry { get; }
    }

    public sealed class PersistenceParticipantSaveResult
    {
        private PersistenceParticipantSaveResult(bool succeeded, string payloadJson, string message)
        {
            Succeeded = succeeded;
            PayloadJson = payloadJson;
            Message = message;
        }

        public bool Succeeded { get; }
        public string PayloadJson { get; }
        public string Message { get; }

        public static PersistenceParticipantSaveResult Success(string payloadJson)
        {
            return new PersistenceParticipantSaveResult(true, payloadJson, "Participant payload captured.");
        }

        public static PersistenceParticipantSaveResult Failure(string message)
        {
            return new PersistenceParticipantSaveResult(false, string.Empty, message);
        }
    }

    public sealed class PersistenceParticipantPrepareResult
    {
        private PersistenceParticipantPrepareResult(bool succeeded, object preparedPayload, string message)
        {
            Succeeded = succeeded;
            PreparedPayload = preparedPayload;
            Message = message;
        }

        public bool Succeeded { get; }
        public object PreparedPayload { get; }
        public string Message { get; }

        public static PersistenceParticipantPrepareResult Success(object preparedPayload)
        {
            return new PersistenceParticipantPrepareResult(true, preparedPayload, "Participant payload prepared.");
        }

        public static PersistenceParticipantPrepareResult Failure(string message)
        {
            return new PersistenceParticipantPrepareResult(false, null, message);
        }
    }

    public sealed class PersistenceParticipantCommitResult
    {
        private PersistenceParticipantCommitResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public bool Succeeded { get; }
        public string Message { get; }

        public static PersistenceParticipantCommitResult Success(string message = "Participant payload committed.")
        {
            return new PersistenceParticipantCommitResult(true, message);
        }

        public static PersistenceParticipantCommitResult Failure(string message)
        {
            return new PersistenceParticipantCommitResult(false, message);
        }
    }
}
