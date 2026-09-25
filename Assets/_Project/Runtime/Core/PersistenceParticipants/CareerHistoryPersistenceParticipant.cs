using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Persistence
{
    public sealed class CareerHistoryPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.career-history";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly CareerHistoryRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<PersonProfessionRuntime> professionRuntimeProvider;
        private readonly Func<TrainingRuntime> trainingRuntimeProvider;
        private readonly Func<ProfessionalActivityRuntime> activityRuntimeProvider;
        private readonly Func<CredentialRuntime> credentialRuntimeProvider;
        private readonly Func<ProfessionalRankRuntime> rankRuntimeProvider;
        private readonly Func<PositionEmploymentRuntime> positionEmploymentRuntimeProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly Func<string[]> knownOrganizationProvider;
        private readonly Func<string[]> knownAuthorityProvider;
        private readonly string ownerId;

        public CareerHistoryPersistenceParticipant(
            CareerHistoryRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<PersonProfessionRuntime> professionRuntimeProvider,
            Func<TrainingRuntime> trainingRuntimeProvider,
            Func<ProfessionalActivityRuntime> activityRuntimeProvider,
            Func<CredentialRuntime> credentialRuntimeProvider,
            Func<ProfessionalRankRuntime> rankRuntimeProvider,
            Func<PositionEmploymentRuntime> positionEmploymentRuntimeProvider,
            Func<string[]> knownPersonProvider,
            Func<string[]> knownOrganizationProvider,
            Func<string[]> knownAuthorityProvider,
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
            this.knownPersonProvider = knownPersonProvider;
            this.knownOrganizationProvider = knownOrganizationProvider;
            this.knownAuthorityProvider = knownAuthorityProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 89;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[] { TrainingPersistenceParticipant.Key, ProfessionalActivityPersistenceParticipant.Key, CredentialPersistenceParticipant.Key, ProfessionalRankPersistenceParticipant.Key, PositionEmploymentPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Career history runtime is missing.");
            }

            CareerHistoryRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Career history snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported career history participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Success(new PreparedPayload(new CareerHistoryRuntimeSaveData()));
            }

            CareerHistoryRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<CareerHistoryRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Career history payload is malformed JSON.");
            }

            if (!CareerHistoryRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), professionRuntimeProvider?.Invoke(), trainingRuntimeProvider?.Invoke(), activityRuntimeProvider?.Invoke(), credentialRuntimeProvider?.Invoke(), rankRuntimeProvider?.Invoke(), positionEmploymentRuntimeProvider?.Invoke(), knownPersonProvider?.Invoke(), knownOrganizationProvider?.Invoke(), knownAuthorityProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Career history runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared career history payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            PersonProfessionRuntime professions = professionRuntimeProvider?.Invoke();
            TrainingRuntime training = trainingRuntimeProvider?.Invoke();
            ProfessionalActivityRuntime activities = activityRuntimeProvider?.Invoke();
            CredentialRuntime credentials = credentialRuntimeProvider?.Invoke();
            ProfessionalRankRuntime ranks = rankRuntimeProvider?.Invoke();
            PositionEmploymentRuntime positions = positionEmploymentRuntimeProvider?.Invoke();
            string[] knownPersons = knownPersonProvider?.Invoke();
            string[] organizations = knownOrganizationProvider?.Invoke();
            string[] authorities = knownAuthorityProvider?.Invoke();
            CareerHistoryRuntimeSaveData rollback = runtime.CreateSaveData();
            CareerHistoryOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, professions, training, activities, credentials, ranks, positions, knownPersons, organizations, authorities, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Career history state restored.");
            }

            runtime.RestoreFromSaveData(rollback, registry, professions, training, activities, credentials, ranks, positions, knownPersons, organizations, authorities, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Career history commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(CareerHistoryRuntimeSaveData saveData)
            {
                SaveData = saveData ?? new CareerHistoryRuntimeSaveData();
            }

            public CareerHistoryRuntimeSaveData SaveData { get; }
        }
    }
}
