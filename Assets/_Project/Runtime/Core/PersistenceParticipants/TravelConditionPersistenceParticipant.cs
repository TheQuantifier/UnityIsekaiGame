using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class TravelConditionPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.travel-conditions";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly TravelConditionRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<LocationRouteRuntime> routeRuntimeProvider;
        private readonly Func<TravelJourneyRuntime> journeyRuntimeProvider;
        private readonly string ownerId;

        public TravelConditionPersistenceParticipant(
            TravelConditionRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<LocationRouteRuntime> routeRuntimeProvider,
            Func<TravelJourneyRuntime> journeyRuntimeProvider,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.routeRuntimeProvider = routeRuntimeProvider;
            this.journeyRuntimeProvider = journeyRuntimeProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId.Trim();
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 41;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[]
        {
            LocationRoutePersistenceParticipant.Key,
            TravelJourneyPersistenceParticipant.Key
        };

        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => false;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Travel condition runtime is missing.");
            TravelConditionRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Travel condition snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported travel condition participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Travel condition payload is empty.");
            TravelConditionRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<TravelConditionRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Travel condition payload is malformed JSON.");
            }

            if (!TravelConditionRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), routeRuntimeProvider?.Invoke(), ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Travel condition runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared travel condition payload has the wrong type.");
            TravelConditionRuntimeSaveData rollback = runtime.CreateSaveData();
            TravelConditionOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), routeRuntimeProvider?.Invoke(), journeyRuntimeProvider?.Invoke(), ownerId, restoring: true);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Travel conditions restored.");

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), routeRuntimeProvider?.Invoke(), journeyRuntimeProvider?.Invoke(), ownerId, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Travel condition commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(TravelConditionRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public TravelConditionRuntimeSaveData SaveData { get; }
        }
    }
}
