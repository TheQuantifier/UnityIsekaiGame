using System;

namespace UnityIsekaiGame.GameData.Persistence
{
    public abstract class TypedPersistenceParticipant<TSaveData> : IPersistenceParticipant, IPersistenceParticipantDependencies
        where TSaveData : class
    {
        private readonly ISaveSerializer serializer;

        protected TypedPersistenceParticipant(PersistenceParticipantDescriptor descriptor, ISaveSerializer serializer = null)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            this.serializer = serializer ?? PersistenceSerialization.Serializer;
        }

        public PersistenceParticipantDescriptor Descriptor { get; }
        public string ParticipantKey => Descriptor.Key;
        public int ParticipantSchemaVersion => Descriptor.SchemaVersion;
        public bool IsRequired => Descriptor.Required;
        public PersistenceScope Scope => Descriptor.Scope;
        public string OwnerId => Descriptor.OwnerId;
        public PersistenceLoadPhase LoadPhase => Descriptor.LoadPhase;
        public int LoadPriority => Descriptor.LoadPriority;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Descriptor.RequiredDependencies;
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Descriptor.OptionalDependencies;
        public bool SupportsRollback => Descriptor.SupportsRollback;
        public bool RequiresSceneReadiness => Descriptor.RequiresSceneReadiness;
        public bool RequiresDefinitionRegistry => Descriptor.RequiresDefinitionRegistry;
        public bool RequiresWorldEntityRegistry => Descriptor.RequiresWorldEntityRegistry;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (!TryCapture(out TSaveData saveData, out string failureReason) || saveData == null)
            {
                return PersistenceParticipantSaveResult.Failure(string.IsNullOrWhiteSpace(failureReason) ? $"'{ParticipantKey}' capture returned no data." : failureReason);
            }

            if (!TryValidate(saveData, out failureReason))
            {
                return PersistenceParticipantSaveResult.Failure(failureReason);
            }

            try
            {
                return PersistenceParticipantSaveResult.Success(serializer.Serialize(saveData));
            }
            catch (Exception exception)
            {
                return PersistenceParticipantSaveResult.Failure($"'{ParticipantKey}' serialization failed: {exception.Message}");
            }
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != ParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported '{ParticipantKey}' schema version {payloadSchemaVersion}; expected {ParticipantSchemaVersion}.");
            }

            TSaveData saveData;
            try
            {
                saveData = serializer.Deserialize<TSaveData>(payloadJson);
            }
            catch (Exception exception)
            {
                return PersistenceParticipantPrepareResult.Failure($"'{ParticipantKey}' payload is invalid: {exception.Message}");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure($"'{ParticipantKey}' payload did not parse.");
            }

            if (!TryValidate(saveData, out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(string.IsNullOrWhiteSpace(failureReason) ? $"'{ParticipantKey}' payload is invalid." : failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(saveData);
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (preparedPayload is not TSaveData saveData)
            {
                return PersistenceParticipantCommitResult.Failure($"Prepared '{ParticipantKey}' payload has the wrong type.");
            }

            TSaveData rollback = CaptureRollback();
            if (TryRestore(saveData, out string failureReason))
            {
                return PersistenceParticipantCommitResult.Success($"'{ParticipantKey}' restored.");
            }

            if (rollback != null)
            {
                TryRestore(rollback, out _);
            }

            return PersistenceParticipantCommitResult.Failure($"'{ParticipantKey}' commit failed; rollback attempted: {failureReason}");
        }

        public virtual void DiscardPreparedPayload(object preparedPayload)
        {
        }

        protected abstract bool TryCapture(out TSaveData saveData, out string failureReason);
        protected abstract bool TryValidate(TSaveData saveData, out string failureReason);
        protected abstract TSaveData CaptureRollback();
        protected abstract bool TryRestore(TSaveData saveData, out string failureReason);
    }
}
