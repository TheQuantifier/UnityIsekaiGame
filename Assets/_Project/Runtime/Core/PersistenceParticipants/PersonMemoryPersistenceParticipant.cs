using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.History;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PersonMemoryPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "person.memory";
        public const int CurrentParticipantSchemaVersion = PersonMemorySaveData.CurrentSchemaVersion;

        private readonly PersonMemoryRuntime memory;
        private readonly AuthoritativeHistoryRuntime history;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<IEnumerable<string>> knownPersonsProvider;
        private readonly string ownerId;

        public PersonMemoryPersistenceParticipant(
            PersonMemoryRuntime memory,
            AuthoritativeHistoryRuntime history,
            Func<DefinitionRegistry> registryProvider,
            Func<IEnumerable<string>> knownPersonsProvider = null,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.memory = memory;
            this.history = history;
            this.registryProvider = registryProvider;
            this.knownPersonsProvider = knownPersonsProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Notification;
        public int LoadPriority => 95;
        // History is world-owned and therefore lives in a separate persistence context.
        // Runtime validation still requires it, but it cannot be a same-context dependency.
        public IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public IReadOnlyList<string> OptionalDependencies => new[] { PersonKnowledgePersistenceParticipant.Key };
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

            PersonMemorySaveData saveData = memory.CreateSaveData();
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Person Memory snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported Person Memory schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Person Memory payload is empty.");
            }

            PersonMemorySaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PersonMemorySaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Person Memory payload is malformed JSON.");
            }

            if (!PersonMemoryRuntime.ValidateSaveData(saveData, history, knownPersonsProvider?.Invoke(), out string failureReason))
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
                return PersistenceParticipantCommitResult.Failure("Prepared Person Memory payload has the wrong type.");
            }

            PersonMemorySaveData rollback = memory.CreateSaveData();
            HistoryOperationResult result = memory.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), history, knownPersonsProvider?.Invoke(), restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Person Memory restored.");
            }

            memory.RestoreFromSaveData(rollback, registryProvider?.Invoke(), history, knownPersonsProvider?.Invoke(), restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Person Memory commit failed; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (memory == null)
            {
                failureReason = "Person Memory runtime is missing.";
                return false;
            }

            if (history == null)
            {
                failureReason = "Authoritative History runtime is missing for Person Memory persistence.";
                return false;
            }

            if (registryProvider?.Invoke() == null)
            {
                failureReason = "Definition registry is not available for Person Memory persistence.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PersonMemorySaveData saveData)
            {
                SaveData = saveData;
            }

            public PersonMemorySaveData SaveData { get; }
        }
    }
}
