using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Persistence
{
    public sealed class ProfessionalActivityPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "person.professional-activity";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly ProfessionalActivityRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<PersonProfessionRuntime> professionRuntimeProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly string ownerId;

        public ProfessionalActivityPersistenceParticipant(
            ProfessionalActivityRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<PersonProfessionRuntime> professionRuntimeProvider,
            Func<string[]> knownPersonProvider,
            string ownerId = PersistenceService.LocalPlayerId)
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
        public int LoadPriority => 85;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { PlayerIdentityProgressionPersistenceParticipant.Key, PersonProfessionPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            ProfessionEntryPersistenceParticipant.Key,
            TrainingPersistenceParticipant.Key,
            CraftingExecutionPersistenceParticipant.Key,
            ProductionWorkflowPersistenceParticipant.Key,
            ExperimentationPersistenceParticipant.Key
        };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Professional activity runtime is missing.");
            }

            ProfessionalActivityRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Professional activity snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported professional activity participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Success(new PreparedPayload(new ProfessionalActivityRuntimeSaveData()));
            }

            ProfessionalActivityRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ProfessionalActivityRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Professional activity payload is malformed JSON.");
            }

            if (!ProfessionalActivityRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), professionRuntimeProvider?.Invoke(), knownPersonProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Professional activity runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared professional activity payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            PersonProfessionRuntime professions = professionRuntimeProvider?.Invoke();
            string[] knownPersons = knownPersonProvider?.Invoke();
            ProfessionalActivityRuntimeSaveData rollback = runtime.CreateSaveData();
            ProfessionalActivityOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, professions, knownPersons, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Professional activity state restored.");
            }

            runtime.RestoreFromSaveData(rollback, registry, professions, knownPersons, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Professional activity commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ProfessionalActivityRuntimeSaveData saveData)
            {
                SaveData = saveData ?? new ProfessionalActivityRuntimeSaveData();
            }

            public ProfessionalActivityRuntimeSaveData SaveData { get; }
        }
    }
}
