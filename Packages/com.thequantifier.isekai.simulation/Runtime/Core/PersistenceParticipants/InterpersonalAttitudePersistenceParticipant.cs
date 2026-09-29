using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Social.Attitudes;

namespace UnityIsekaiGame.Persistence
{
    public sealed class InterpersonalAttitudePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.interpersonal-attitudes";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly InterpersonalAttitudeRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly string ownerId;

        public InterpersonalAttitudePersistenceParticipant(InterpersonalAttitudeRuntime runtime, Func<DefinitionRegistry> registryProvider, Func<string[]> knownPersonProvider, string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.knownPersonProvider = knownPersonProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 96;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { RelationshipPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[] { RelationshipPersistenceParticipant.Key, AuthoritativeHistoryPersistenceParticipant.Key, InformationAccessPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Interpersonal attitude runtime is missing.");
            }

            InterpersonalAttitudeRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Interpersonal attitude snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported interpersonal attitude participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Interpersonal attitude payload is empty.");
            }

            InterpersonalAttitudeRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<InterpersonalAttitudeRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Interpersonal attitude payload is malformed JSON.");
            }

            if (!InterpersonalAttitudeRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Interpersonal attitude runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared interpersonal attitude payload has the wrong type.");
            }

            InterpersonalAttitudeRuntimeSaveData rollback = runtime.CreateSaveData();
            AttitudeMutationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), restoringState: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Interpersonal attitudes restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), restoringState: true);
            return PersistenceParticipantCommitResult.Failure($"Interpersonal attitude commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(InterpersonalAttitudeRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public InterpersonalAttitudeRuntimeSaveData SaveData { get; }
        }
    }
}
