using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class OrganizationPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.organizations";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly OrganizationRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly Func<string[]> knownPlaceProvider;
        private readonly string ownerId;

        public OrganizationPersistenceParticipant(
            OrganizationRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalWorldId,
            Func<string[]> knownPersonProvider = null,
            Func<string[]> knownPlaceProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
            this.knownPersonProvider = knownPersonProvider;
            this.knownPlaceProvider = knownPlaceProvider;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Institutions;
        public int LoadPriority => 40;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            InformationAccessPersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Organization runtime is missing.");
            }

            OrganizationRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Organization snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported organization participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Organization payload is empty.");
            }

            OrganizationRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<OrganizationRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Organization payload is malformed JSON.");
            }

            if (!OrganizationRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), ownerId, knownPersonProvider?.Invoke(), knownPlaceProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Organization runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared organization payload has the wrong type.");
            }

            OrganizationRuntimeSaveData rollback = runtime.CreateSaveData();
            OrganizationOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), ownerId, knownPersonProvider?.Invoke(), knownPlaceProvider?.Invoke(), restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Organizations restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), ownerId, knownPersonProvider?.Invoke(), knownPlaceProvider?.Invoke(), restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Organization commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(OrganizationRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public OrganizationRuntimeSaveData SaveData { get; }
        }
    }
}
