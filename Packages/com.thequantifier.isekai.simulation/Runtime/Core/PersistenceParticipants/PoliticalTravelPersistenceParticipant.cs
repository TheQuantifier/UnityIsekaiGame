using System;
using UnityEngine;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PoliticalTravelPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.political-travel";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly PoliticalTravelRuntime runtime;
        private readonly Func<GovernmentRuntime> governmentProvider;
        private readonly Func<LegalRuntime> legalProvider;
        private readonly Func<CrimeRuntime> crimeProvider;
        private readonly Func<LocationRuntime> locationProvider;
        private readonly Func<LocationRouteRuntime> routeProvider;
        private readonly string ownerId;

        public PoliticalTravelPersistenceParticipant(
            PoliticalTravelRuntime runtime,
            Func<GovernmentRuntime> governmentProvider,
            Func<LegalRuntime> legalProvider,
            Func<CrimeRuntime> crimeProvider,
            Func<LocationRuntime> locationProvider,
            Func<LocationRouteRuntime> routeProvider,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.governmentProvider = governmentProvider;
            this.legalProvider = legalProvider;
            this.crimeProvider = crimeProvider;
            this.locationProvider = locationProvider;
            this.routeProvider = routeProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId.Trim();
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 42;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[]
        {
            GovernmentPersistenceParticipant.Key,
            LegalPersistenceParticipant.Key,
            LocationPersistenceParticipant.Key,
            LocationRoutePersistenceParticipant.Key
        };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[] { CrimePersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => false;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Political travel runtime is missing.");
            PoliticalTravelRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Political travel snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported political travel participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Political travel payload is empty.");
            PoliticalTravelRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PoliticalTravelRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Political travel payload is malformed JSON.");
            }

            if (!PoliticalTravelRuntime.ValidateSaveData(saveData, governmentProvider?.Invoke(), legalProvider?.Invoke(), crimeProvider?.Invoke(), locationProvider?.Invoke(), routeProvider?.Invoke(), ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Political travel runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared political travel payload has the wrong type.");
            PoliticalTravelRuntimeSaveData rollback = runtime.CreateSaveData();
            PoliticalTravelOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, governmentProvider?.Invoke(), legalProvider?.Invoke(), crimeProvider?.Invoke(), locationProvider?.Invoke(), routeProvider?.Invoke(), ownerId, restoring: true);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Political travel restored.");

            runtime.RestoreFromSaveData(rollback, governmentProvider?.Invoke(), legalProvider?.Invoke(), crimeProvider?.Invoke(), locationProvider?.Invoke(), routeProvider?.Invoke(), ownerId, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Political travel commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PoliticalTravelRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public PoliticalTravelRuntimeSaveData SaveData { get; }
        }
    }
}
