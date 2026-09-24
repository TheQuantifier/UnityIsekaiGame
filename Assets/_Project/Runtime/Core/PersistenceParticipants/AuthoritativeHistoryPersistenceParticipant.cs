using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.History;

namespace UnityIsekaiGame.Persistence
{
    public sealed class AuthoritativeHistoryPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.authoritative-history";
        public const int CurrentParticipantSchemaVersion = AuthoritativeHistorySaveData.CurrentSchemaVersion;

        private readonly AuthoritativeHistoryRuntime history;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<IEnumerable<string>> knownPersonsProvider;
        private readonly Func<IEnumerable<string>> knownBodiesProvider;
        private readonly string ownerId;

        public AuthoritativeHistoryPersistenceParticipant(
            AuthoritativeHistoryRuntime history,
            Func<DefinitionRegistry> registryProvider,
            Func<IEnumerable<string>> knownPersonsProvider = null,
            Func<IEnumerable<string>> knownBodiesProvider = null,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.history = history;
            this.registryProvider = registryProvider;
            this.knownPersonsProvider = knownPersonsProvider;
            this.knownBodiesProvider = knownBodiesProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Notification;
        public int LoadPriority => 90;
        public IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantSaveResult.Failure(failureReason);
            }

            AuthoritativeHistorySaveData saveData = history.CreateSaveData();
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Authoritative History snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion < 1 || payloadSchemaVersion > CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported Authoritative History schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Authoritative History payload is empty.");
            }

            AuthoritativeHistorySaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<AuthoritativeHistorySaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Authoritative History payload is malformed JSON.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!AuthoritativeHistoryRuntime.ValidateSaveData(saveData, registry, knownPersonsProvider?.Invoke(), knownBodiesProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantCommitResult.Failure(failureReason);
            }

            if (preparedPayload is not PreparedPayload prepared || prepared.SaveData == null)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared Authoritative History payload has the wrong type.");
            }

            AuthoritativeHistorySaveData rollback = history.CreateSaveData();
            DefinitionRegistry registry = registryProvider?.Invoke();
            HistoryOperationResult result = history.RestoreFromSaveData(prepared.SaveData, registry, knownPersonsProvider?.Invoke(), knownBodiesProvider?.Invoke(), restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Authoritative History restored.");
            }

            history.RestoreFromSaveData(rollback, registry, knownPersonsProvider?.Invoke(), knownBodiesProvider?.Invoke(), restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Authoritative History commit failed; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (history == null)
            {
                failureReason = "Authoritative History runtime is missing.";
                return false;
            }

            if (registryProvider?.Invoke() == null)
            {
                failureReason = "Definition registry is not available for Authoritative History persistence.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(AuthoritativeHistorySaveData saveData)
            {
                SaveData = saveData;
            }

            public AuthoritativeHistorySaveData SaveData { get; }
        }
    }
}
