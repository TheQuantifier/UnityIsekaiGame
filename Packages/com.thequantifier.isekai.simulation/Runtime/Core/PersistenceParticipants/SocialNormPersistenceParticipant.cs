using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Social.Norms;

namespace UnityIsekaiGame.Persistence
{
    public sealed class SocialNormPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.social-norms";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly SocialNormRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly string ownerId;

        public SocialNormPersistenceParticipant(SocialNormRuntime runtime, Func<DefinitionRegistry> registryProvider, Func<string[]> knownPersonProvider, string ownerId = PersistenceService.LocalWorldId)
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
        public int LoadPriority => 100;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            RelationshipPersistenceParticipant.Key,
            InterpersonalAttitudePersistenceParticipant.Key,
            ReputationPersistenceParticipant.Key,
            RumorPersistenceParticipant.Key,
            SocialInteractionPersistenceParticipant.Key,
            PersonKnowledgePersistenceParticipant.Key,
            PersonMemoryPersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Social Norm runtime is missing.");
            }

            SocialNormRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Social Norm snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported Social Norm participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Social Norm payload is empty.");
            }

            SocialNormRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<SocialNormRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Social Norm payload is malformed JSON.");
            }

            if (!SocialNormRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Social Norm runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared Social Norm payload has the wrong type.");
            }

            SocialNormRuntimeSaveData rollback = runtime.CreateSaveData();
            SocialNormEvaluationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), restoringState: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Social norms restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), restoringState: true);
            return PersistenceParticipantCommitResult.Failure($"Social Norm commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(SocialNormRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public SocialNormRuntimeSaveData SaveData { get; }
        }
    }
}
