using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Social.Interactions;

namespace UnityIsekaiGame.Persistence
{
    public sealed class SocialInteractionPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.social-interactions";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly SocialInteractionRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly string ownerId;

        public SocialInteractionPersistenceParticipant(SocialInteractionRuntime runtime, Func<DefinitionRegistry> registryProvider, Func<string[]> knownPersonProvider, string ownerId = PersistenceService.LocalWorldId)
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
        public int LoadPriority => 99;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            RelationshipPersistenceParticipant.Key,
            InterpersonalAttitudePersistenceParticipant.Key,
            ReputationPersistenceParticipant.Key,
            RumorPersistenceParticipant.Key,
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
                return PersistenceParticipantSaveResult.Failure("Social Interaction runtime is missing.");
            }

            SocialInteractionRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Social Interaction snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported Social Interaction participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Social Interaction payload is empty.");
            }

            SocialInteractionRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<SocialInteractionRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Social Interaction payload is malformed JSON.");
            }

            if (!SocialInteractionRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Social Interaction runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared Social Interaction payload has the wrong type.");
            }

            SocialInteractionRuntimeSaveData rollback = runtime.CreateSaveData();
            SocialInteractionResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), restoringState: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Social interactions restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), knownPersonProvider?.Invoke(), restoringState: true);
            return PersistenceParticipantCommitResult.Failure($"Social Interaction commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(SocialInteractionRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public SocialInteractionRuntimeSaveData SaveData { get; }
        }
    }
}
