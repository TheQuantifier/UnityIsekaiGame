using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Persistence
{
    public sealed class CredentialPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.professional-credentials";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly CredentialRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<PersonProfessionRuntime> professionRuntimeProvider;
        private readonly Func<TrainingRuntime> trainingRuntimeProvider;
        private readonly Func<ProfessionalActivityRuntime> activityRuntimeProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly Func<string[]> knownAuthorityProvider;
        private readonly string ownerId;

        public CredentialPersistenceParticipant(
            CredentialRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<PersonProfessionRuntime> professionRuntimeProvider,
            Func<TrainingRuntime> trainingRuntimeProvider,
            Func<ProfessionalActivityRuntime> activityRuntimeProvider,
            Func<string[]> knownPersonProvider,
            Func<string[]> knownAuthorityProvider,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.professionRuntimeProvider = professionRuntimeProvider;
            this.trainingRuntimeProvider = trainingRuntimeProvider;
            this.activityRuntimeProvider = activityRuntimeProvider;
            this.knownPersonProvider = knownPersonProvider;
            this.knownAuthorityProvider = knownAuthorityProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 86;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[] { TrainingPersistenceParticipant.Key, ProfessionalActivityPersistenceParticipant.Key, ProfessionEntryPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Credential runtime is missing.");
            }

            CredentialRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Credential snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported credential participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Success(new PreparedPayload(new CredentialRuntimeSaveData()));
            }

            CredentialRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<CredentialRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Credential payload is malformed JSON.");
            }

            if (!CredentialRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), professionRuntimeProvider?.Invoke(), trainingRuntimeProvider?.Invoke(), activityRuntimeProvider?.Invoke(), knownPersonProvider?.Invoke(), knownAuthorityProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Credential runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared credential payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            PersonProfessionRuntime professions = professionRuntimeProvider?.Invoke();
            TrainingRuntime training = trainingRuntimeProvider?.Invoke();
            ProfessionalActivityRuntime activities = activityRuntimeProvider?.Invoke();
            string[] knownPersons = knownPersonProvider?.Invoke();
            string[] authorities = knownAuthorityProvider?.Invoke();
            CredentialRuntimeSaveData rollback = runtime.CreateSaveData();
            CredentialOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, professions, training, activities, knownPersons, authorities, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Credential state restored.");
            }

            runtime.RestoreFromSaveData(rollback, registry, professions, training, activities, knownPersons, authorities, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Credential commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(CredentialRuntimeSaveData saveData)
            {
                SaveData = saveData ?? new CredentialRuntimeSaveData();
            }

            public CredentialRuntimeSaveData SaveData { get; }
        }
    }
}
