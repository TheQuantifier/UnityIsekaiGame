using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class InteractionPointPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.interaction-points";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly InteractionPointRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<LocationRuntime> locationRuntimeProvider;
        private readonly Func<EntityLocationRuntime> entityLocationRuntimeProvider;
        private readonly string ownerId;

        public InteractionPointPersistenceParticipant(
            InteractionPointRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<LocationRuntime> locationRuntimeProvider,
            Func<EntityLocationRuntime> entityLocationRuntimeProvider,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.locationRuntimeProvider = locationRuntimeProvider;
            this.entityLocationRuntimeProvider = entityLocationRuntimeProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId.Trim();
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 37;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { LocationPersistenceParticipant.Key, EntityLocationPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => false;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Interaction point runtime is missing.");
            }

            InteractionPointRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Interaction point snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported interaction point participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Interaction point payload is empty.");
            }

            InteractionPointRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<InteractionPointRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Interaction point payload is malformed JSON.");
            }

            if (!InteractionPointRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), locationRuntimeProvider?.Invoke(), entityLocationRuntimeProvider?.Invoke(), ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Interaction point runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared interaction point payload has the wrong type.");
            }

            InteractionPointRuntimeSaveData rollback = runtime.CreateSaveData();
            InteractionPointOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, locationRuntimeProvider?.Invoke(), entityLocationRuntimeProvider?.Invoke(), ownerId, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Interaction points restored.");
            }

            runtime.RestoreFromSaveData(rollback, locationRuntimeProvider?.Invoke(), entityLocationRuntimeProvider?.Invoke(), ownerId, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Interaction point commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(InteractionPointRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public InteractionPointRuntimeSaveData SaveData { get; }
        }
    }
}
