using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Persistence
{
    public sealed class ProfessionEntryPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "person.profession-entry-requests";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly ProfessionEntryRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<PersonProfessionRuntime> professionRuntimeProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly string ownerId;

        public ProfessionEntryPersistenceParticipant(ProfessionEntryRuntime runtime, Func<DefinitionRegistry> registryProvider, Func<PersonProfessionRuntime> professionRuntimeProvider, Func<string[]> knownPersonProvider, string ownerId = PersistenceService.LocalPlayerId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.professionRuntimeProvider = professionRuntimeProvider;
            this.knownPersonProvider = knownPersonProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 82;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { PlayerIdentityProgressionPersistenceParticipant.Key, PersonProfessionPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Profession entry runtime is missing.");
            }

            ProfessionEntryRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Profession entry snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported profession entry participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Profession entry payload is empty.");
            }

            ProfessionEntryRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ProfessionEntryRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Profession entry payload is malformed JSON.");
            }

            if (!ProfessionEntryRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), professionRuntimeProvider?.Invoke(), knownPersonProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Profession entry runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared profession entry payload has the wrong type.");
            }

            ProfessionEntryRuntimeSaveData rollback = runtime.CreateSaveData();
            ProfessionEntryOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), professionRuntimeProvider?.Invoke(), knownPersonProvider?.Invoke(), restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Profession entry requests restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), professionRuntimeProvider?.Invoke(), knownPersonProvider?.Invoke(), restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Profession entry commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ProfessionEntryRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public ProfessionEntryRuntimeSaveData SaveData { get; }
        }
    }
}
