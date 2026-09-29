using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class LocationConnectionPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.location-connections";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly LocationConnectionRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<LocationRuntime> locationRuntimeProvider;
        private readonly Func<EntityLocationRuntime> entityLocationRuntimeProvider;
        private readonly Func<InteractionPointRuntime> interactionPointRuntimeProvider;
        private readonly string ownerId;

        public LocationConnectionPersistenceParticipant(
            LocationConnectionRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<LocationRuntime> locationRuntimeProvider,
            Func<EntityLocationRuntime> entityLocationRuntimeProvider,
            Func<InteractionPointRuntime> interactionPointRuntimeProvider,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.locationRuntimeProvider = locationRuntimeProvider;
            this.entityLocationRuntimeProvider = entityLocationRuntimeProvider;
            this.interactionPointRuntimeProvider = interactionPointRuntimeProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId.Trim();
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 38;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { LocationPersistenceParticipant.Key, EntityLocationPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[] { InteractionPointPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => false;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Location connection runtime is missing.");
            }

            LocationConnectionRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Location connection snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported location connection participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Location connection payload is empty.");
            }

            LocationConnectionRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<LocationConnectionRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Location connection payload is malformed JSON.");
            }

            if (!LocationConnectionRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), locationRuntimeProvider?.Invoke(), entityLocationRuntimeProvider?.Invoke(), interactionPointRuntimeProvider?.Invoke(), ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Location connection runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared location connection payload has the wrong type.");
            }

            LocationConnectionRuntimeSaveData rollback = runtime.CreateSaveData();
            LocationConnectionOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, locationRuntimeProvider?.Invoke(), entityLocationRuntimeProvider?.Invoke(), interactionPointRuntimeProvider?.Invoke(), ownerId, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Location connections restored.");
            }

            runtime.RestoreFromSaveData(rollback, locationRuntimeProvider?.Invoke(), entityLocationRuntimeProvider?.Invoke(), interactionPointRuntimeProvider?.Invoke(), ownerId, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Location connection commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(LocationConnectionRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public LocationConnectionRuntimeSaveData SaveData { get; }
        }
    }
}
