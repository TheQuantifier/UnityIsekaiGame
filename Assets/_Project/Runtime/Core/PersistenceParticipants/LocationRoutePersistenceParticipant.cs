using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class LocationRoutePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.location-routes";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly LocationRouteRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<LocationRuntime> locationRuntimeProvider;
        private readonly Func<LocationConnectionRuntime> connectionRuntimeProvider;
        private readonly string ownerId;

        public LocationRoutePersistenceParticipant(
            LocationRouteRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<LocationRuntime> locationRuntimeProvider,
            Func<LocationConnectionRuntime> connectionRuntimeProvider,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.locationRuntimeProvider = locationRuntimeProvider;
            this.connectionRuntimeProvider = connectionRuntimeProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId.Trim();
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 39;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { LocationPersistenceParticipant.Key, LocationConnectionPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => false;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Location route runtime is missing.");
            }

            LocationRouteRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Location route snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported location route participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Location route payload is empty.");
            }

            LocationRouteRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<LocationRouteRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Location route payload is malformed JSON.");
            }

            if (!LocationRouteRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), locationRuntimeProvider?.Invoke(), connectionRuntimeProvider?.Invoke(), ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Location route runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared location route payload has the wrong type.");
            }

            LocationRouteRuntimeSaveData rollback = runtime.CreateSaveData();
            LocationRouteMutationResult result = runtime.RestoreFromSaveData(prepared.SaveData, locationRuntimeProvider?.Invoke(), connectionRuntimeProvider?.Invoke(), ownerId, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Location routes restored.");
            }

            runtime.RestoreFromSaveData(rollback, locationRuntimeProvider?.Invoke(), connectionRuntimeProvider?.Invoke(), ownerId, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Location route commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(LocationRouteRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public LocationRouteRuntimeSaveData SaveData { get; }
        }
    }
}
