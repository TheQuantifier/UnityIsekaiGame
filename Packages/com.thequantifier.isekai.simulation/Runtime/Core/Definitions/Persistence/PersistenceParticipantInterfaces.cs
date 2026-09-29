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
