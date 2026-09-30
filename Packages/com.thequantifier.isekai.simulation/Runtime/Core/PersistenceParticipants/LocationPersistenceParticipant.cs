using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class LocationPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.locations";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly LocationRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<string[]> knownPropertyProvider;
        private readonly Func<string[]> knownOrganizationProvider;
        private readonly Func<string[]> knownGovernmentProvider;
        private readonly Func<string[]> knownTerritoryProvider;
        private readonly string ownerId;

        public LocationPersistenceParticipant(
            LocationRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalWorldId,
            Func<string[]> knownPropertyProvider = null,
            Func<string[]> knownOrganizationProvider = null,
            Func<string[]> knownGovernmentProvider = null,
            Func<string[]> knownTerritoryProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
            this.knownPropertyProvider = knownPropertyProvider;
            this.knownOrganizationProvider = knownOrganizationProvider;
            this.knownGovernmentProvider = knownGovernmentProvider;
            this.knownTerritoryProvider = knownTerritoryProvider;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 35;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Location runtime is missing.");
            }

            LocationRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Location snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported location participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Location payload is empty.");
            }

            LocationRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<LocationRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Location payload is malformed JSON.");
            }

            if (!LocationRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), ownerId, knownPropertyProvider?.Invoke(), knownOrganizationProvider?.Invoke(), knownGovernmentProvider?.Invoke(), knownTerritoryProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Location runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared location payload has the wrong type.");
            }

            LocationRuntimeSaveData rollback = runtime.CreateSaveData();
            LocationOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), ownerId, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Locations restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), ownerId, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Location commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(LocationRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public LocationRuntimeSaveData SaveData { get; }
        }
    }
}
