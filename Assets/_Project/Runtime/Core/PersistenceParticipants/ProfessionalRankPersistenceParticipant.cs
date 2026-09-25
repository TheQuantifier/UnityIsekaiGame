using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Persistence
{
    public sealed class ProfessionalRankPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.professional-ranks";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly ProfessionalRankRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<PersonProfessionRuntime> professionRuntimeProvider;
        private readonly Func<TrainingRuntime> trainingRuntimeProvider;
        private readonly Func<ProfessionalActivityRuntime> activityRuntimeProvider;
        private readonly Func<CredentialRuntime> credentialRuntimeProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly Func<string[]> knownAuthorityProvider;
        private readonly string ownerId;

        public ProfessionalRankPersistenceParticipant(
            ProfessionalRankRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<PersonProfessionRuntime> professionRuntimeProvider,
            Func<TrainingRuntime> trainingRuntimeProvider,
            Func<ProfessionalActivityRuntime> activityRuntimeProvider,
            Func<CredentialRuntime> credentialRuntimeProvider,
            Func<string[]> knownPersonProvider,
            Func<string[]> knownAuthorityProvider,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.professionRuntimeProvider = professionRuntimeProvider;
            this.trainingRuntimeProvider = trainingRuntimeProvider;
            this.activityRuntimeProvider = activityRuntimeProvider;
            this.credentialRuntimeProvider = credentialRuntimeProvider;
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
        public int LoadPriority => 87;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[] { TrainingPersistenceParticipant.Key, ProfessionalActivityPersistenceParticipant.Key, CredentialPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Professional rank runtime is missing.");
            }

            ProfessionalRankRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Professional rank snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported professional rank participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Success(new PreparedPayload(new ProfessionalRankRuntimeSaveData()));
            }

            ProfessionalRankRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ProfessionalRankRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Professional rank payload is malformed JSON.");
            }

            if (!ProfessionalRankRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), professionRuntimeProvider?.Invoke(), trainingRuntimeProvider?.Invoke(), activityRuntimeProvider?.Invoke(), credentialRuntimeProvider?.Invoke(), knownPersonProvider?.Invoke(), knownAuthorityProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Professional rank runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared professional rank payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            PersonProfessionRuntime professions = professionRuntimeProvider?.Invoke();
            TrainingRuntime training = trainingRuntimeProvider?.Invoke();
            ProfessionalActivityRuntime activities = activityRuntimeProvider?.Invoke();
            CredentialRuntime credentials = credentialRuntimeProvider?.Invoke();
            string[] knownPersons = knownPersonProvider?.Invoke();
            string[] authorities = knownAuthorityProvider?.Invoke();
            ProfessionalRankRuntimeSaveData rollback = runtime.CreateSaveData();
            ProfessionalRankOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, professions, training, activities, credentials, knownPersons, authorities, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Professional rank state restored.");
            }

            runtime.RestoreFromSaveData(rollback, registry, professions, training, activities, credentials, knownPersons, authorities, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Professional rank commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ProfessionalRankRuntimeSaveData saveData)
            {
                SaveData = saveData ?? new ProfessionalRankRuntimeSaveData();
            }

            public ProfessionalRankRuntimeSaveData SaveData { get; }
        }
    }
}
