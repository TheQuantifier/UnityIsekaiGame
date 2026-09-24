using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Persistence
{
    public sealed class LifePathPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "person.life-path";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly LifePathRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<PersonProfessionRuntime> professionRuntimeProvider;
        private readonly Func<TrainingRuntime> trainingRuntimeProvider;
        private readonly Func<ProfessionalActivityRuntime> activityRuntimeProvider;
        private readonly Func<CredentialRuntime> credentialRuntimeProvider;
        private readonly Func<ProfessionalRankRuntime> rankRuntimeProvider;
        private readonly Func<PositionEmploymentRuntime> positionEmploymentRuntimeProvider;
        private readonly Func<CareerHistoryRuntime> careerHistoryRuntimeProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly Func<string[]> knownOrganizationProvider;
        private readonly string ownerId;

        public LifePathPersistenceParticipant(
            LifePathRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<PersonProfessionRuntime> professionRuntimeProvider,
            Func<TrainingRuntime> trainingRuntimeProvider,
            Func<ProfessionalActivityRuntime> activityRuntimeProvider,
            Func<CredentialRuntime> credentialRuntimeProvider,
            Func<ProfessionalRankRuntime> rankRuntimeProvider,
            Func<PositionEmploymentRuntime> positionEmploymentRuntimeProvider,
            Func<CareerHistoryRuntime> careerHistoryRuntimeProvider,
            Func<string[]> knownPersonProvider,
            Func<string[]> knownOrganizationProvider,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.professionRuntimeProvider = professionRuntimeProvider;
            this.trainingRuntimeProvider = trainingRuntimeProvider;
            this.activityRuntimeProvider = activityRuntimeProvider;
            this.credentialRuntimeProvider = credentialRuntimeProvider;
            this.rankRuntimeProvider = rankRuntimeProvider;
            this.positionEmploymentRuntimeProvider = positionEmploymentRuntimeProvider;
            this.careerHistoryRuntimeProvider = careerHistoryRuntimeProvider;
            this.knownPersonProvider = knownPersonProvider;
            this.knownOrganizationProvider = knownOrganizationProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 90;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { PlayerIdentityProgressionPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            PersonProfessionPersistenceParticipant.Key,
            TrainingPersistenceParticipant.Key,
            ProfessionalActivityPersistenceParticipant.Key,
            CredentialPersistenceParticipant.Key,
            ProfessionalRankPersistenceParticipant.Key,
            PositionEmploymentPersistenceParticipant.Key,
            CareerHistoryPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Life-path runtime is missing.");
            }

            LifePathRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Life-path snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported life-path participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Success(new PreparedPayload(new LifePathRuntimeSaveData()));
            }

            LifePathRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<LifePathRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Life-path payload is malformed JSON.");
            }

            if (!LifePathRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), professionRuntimeProvider?.Invoke(), trainingRuntimeProvider?.Invoke(), activityRuntimeProvider?.Invoke(), credentialRuntimeProvider?.Invoke(), rankRuntimeProvider?.Invoke(), positionEmploymentRuntimeProvider?.Invoke(), careerHistoryRuntimeProvider?.Invoke(), knownPersonProvider?.Invoke(), knownOrganizationProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Life-path runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared life-path payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            PersonProfessionRuntime professions = professionRuntimeProvider?.Invoke();
            TrainingRuntime training = trainingRuntimeProvider?.Invoke();
            ProfessionalActivityRuntime activities = activityRuntimeProvider?.Invoke();
            CredentialRuntime credentials = credentialRuntimeProvider?.Invoke();
            ProfessionalRankRuntime ranks = rankRuntimeProvider?.Invoke();
            PositionEmploymentRuntime positions = positionEmploymentRuntimeProvider?.Invoke();
            CareerHistoryRuntime careerHistory = careerHistoryRuntimeProvider?.Invoke();
            string[] knownPersons = knownPersonProvider?.Invoke();
            string[] organizations = knownOrganizationProvider?.Invoke();
            LifePathRuntimeSaveData rollback = runtime.CreateSaveData();
            LifePathOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, professions, training, activities, credentials, ranks, positions, careerHistory, knownPersons, organizations, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Life-path state restored.");
            }

            runtime.RestoreFromSaveData(rollback, registry, professions, training, activities, credentials, ranks, positions, careerHistory, knownPersons, organizations, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Life-path commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(LifePathRuntimeSaveData saveData)
            {
                SaveData = saveData ?? new LifePathRuntimeSaveData();
            }

            public LifePathRuntimeSaveData SaveData { get; }
        }
    }
}
