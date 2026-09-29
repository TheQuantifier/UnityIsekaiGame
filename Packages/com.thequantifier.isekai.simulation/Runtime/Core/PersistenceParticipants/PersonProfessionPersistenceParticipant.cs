using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PersonProfessionPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "person.profession-relationships";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly PersonProfessionRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly string ownerId;

        public PersonProfessionPersistenceParticipant(PersonProfessionRuntime runtime, Func<DefinitionRegistry> registryProvider, Func<string[]> knownPersonProvider, string ownerId = PersistenceService.LocalPlayerId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.knownPersonProvider = knownPersonProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 80;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { PlayerIdentityProgressionPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Profession runtime is missing.");
            }

            PersonProfessionRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Profession snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported profession participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Profession payload is empty.");
            }

            PersonProfessionRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PersonProfessionRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Profession payload is malformed JSON.");
            }

            if (!PersonProfessionRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Profession runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared profession payload has the wrong type.");
            }

            PersonProfessionRuntimeSaveData rollback = runtime.CreateSaveData();
            ProfessionOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Profession relationships restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Profession commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PersonProfessionRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public PersonProfessionRuntimeSaveData SaveData { get; }
        }
    }
}
