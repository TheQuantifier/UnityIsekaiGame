using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PositionEmploymentPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.position-employment";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly PositionEmploymentRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<PersonProfessionRuntime> professionRuntimeProvider;
        private readonly Func<TrainingRuntime> trainingRuntimeProvider;
        private readonly Func<ProfessionalActivityRuntime> activityRuntimeProvider;
        private readonly Func<CredentialRuntime> credentialRuntimeProvider;
        private readonly Func<ProfessionalRankRuntime> rankRuntimeProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly Func<string[]> knownOrganizationProvider;
        private readonly Func<string[]> knownAuthorityProvider;
        private readonly string ownerId;

        public PositionEmploymentPersistenceParticipant(
            PositionEmploymentRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<PersonProfessionRuntime> professionRuntimeProvider,
            Func<TrainingRuntime> trainingRuntimeProvider,
            Func<ProfessionalActivityRuntime> activityRuntimeProvider,
            Func<CredentialRuntime> credentialRuntimeProvider,
            Func<ProfessionalRankRuntime> rankRuntimeProvider,
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
        public int LoadPriority => 88;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[] { TrainingPersistenceParticipant.Key, ProfessionalActivityPersistenceParticipant.Key, CredentialPersistenceParticipant.Key, ProfessionalRankPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Position employment runtime is missing.");
            }

            PositionEmploymentRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Position employment snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported position employment participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Success(new PreparedPayload(new PositionEmploymentRuntimeSaveData()));
            }

            PositionEmploymentRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PositionEmploymentRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Position employment payload is malformed JSON.");
            }

            if (!PositionEmploymentRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), professionRuntimeProvider?.Invoke(), trainingRuntimeProvider?.Invoke(), activityRuntimeProvider?.Invoke(), credentialRuntimeProvider?.Invoke(), rankRuntimeProvider?.Invoke(), knownPersonProvider?.Invoke(), knownOrganizationProvider?.Invoke(), knownAuthorityProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Position employment runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared position employment payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            PersonProfessionRuntime professions = professionRuntimeProvider?.Invoke();
            TrainingRuntime training = trainingRuntimeProvider?.Invoke();
            ProfessionalActivityRuntime activities = activityRuntimeProvider?.Invoke();
            CredentialRuntime credentials = credentialRuntimeProvider?.Invoke();
            ProfessionalRankRuntime ranks = rankRuntimeProvider?.Invoke();
            string[] knownPersons = knownPersonProvider?.Invoke();
            string[] organizations = knownOrganizationProvider?.Invoke();
            string[] authorities = knownAuthorityProvider?.Invoke();
            PositionEmploymentRuntimeSaveData rollback = runtime.CreateSaveData();
            PositionEmploymentOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, professions, training, activities, credentials, ranks, knownPersons, organizations, authorities, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Position employment state restored.");
            }

            runtime.RestoreFromSaveData(rollback, registry, professions, training, activities, credentials, ranks, knownPersons, organizations, authorities, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Position employment commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PositionEmploymentRuntimeSaveData saveData)
            {
                SaveData = saveData ?? new PositionEmploymentRuntimeSaveData();
            }

            public PositionEmploymentRuntimeSaveData SaveData { get; }
        }
    }
}
