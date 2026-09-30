using System;
using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class EntityLocationPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.entity-locations";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly EntityLocationRuntime runtime;
        private readonly Func<LocationRuntime> locationRuntimeProvider;
        private readonly string ownerId;

        public EntityLocationPersistenceParticipant(
            EntityLocationRuntime runtime,
            Func<LocationRuntime> locationRuntimeProvider,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.locationRuntimeProvider = locationRuntimeProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 36;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { LocationPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => false;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Entity location runtime is missing.");
            }

            EntityLocationRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Entity location snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported entity location participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Entity location payload is empty.");
            }

            EntityLocationRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<EntityLocationRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Entity location payload is malformed JSON.");
            }

            if (!EntityLocationRuntime.ValidateSaveData(saveData, locationRuntimeProvider?.Invoke(), ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Entity location runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared entity location payload has the wrong type.");
            }

            EntityLocationRuntimeSaveData rollback = runtime.CreateSaveData();
            EntityLocationOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, locationRuntimeProvider?.Invoke(), ownerId, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Entity locations restored.");
            }

            runtime.RestoreFromSaveData(rollback, locationRuntimeProvider?.Invoke(), ownerId, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Entity location commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(EntityLocationRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public EntityLocationRuntimeSaveData SaveData { get; }
        }
    }
}
