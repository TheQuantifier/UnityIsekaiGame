using System;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Persistence
{
    public sealed class ConversationPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.conversations";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly ConversationRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public ConversationPersistenceParticipant(ConversationRuntime runtime, Func<DefinitionRegistry> registryProvider, string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 190;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            QuestRuntimePersistenceParticipant.Key,
            QuestSourcePersistenceParticipant.Key,
            LocationPersistenceParticipant.Key,
            InteractionPointPersistenceParticipant.Key,
            EntityLocationPersistenceParticipant.Key,
            OrganizationPersistenceParticipant.Key,
            OrganizationMembershipPersistenceParticipant.Key,
            OrganizationAuthorityPersistenceParticipant.Key,
            GovernmentPersistenceParticipant.Key,
            FactionPersistenceParticipant.Key,
            BusinessPersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key,
            InformationTransferPersistenceParticipant.Key,
            KnowledgeRecordPersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key,
            RelationshipPersistenceParticipant.Key,
            InterpersonalAttitudePersistenceParticipant.Key,
            ReputationPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Conversation runtime is missing.");
            ConversationRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Conversation snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported conversation participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Conversation payload is empty.");

            ConversationRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ConversationRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Conversation payload is malformed JSON.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!ConversationRuntime.ValidateSaveData(saveData, registry, ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Conversation runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared conversation payload has the wrong type.");

            DefinitionRegistry registry = registryProvider?.Invoke();
            ConversationRuntimeSaveData rollback = runtime.CreateSaveData();
            ConversationOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, ownerId);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Conversations restored.");

            runtime.RestoreFromSaveData(rollback, registry, ownerId);
            return PersistenceParticipantCommitResult.Failure($"Conversation commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ConversationRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public ConversationRuntimeSaveData SaveData { get; }
        }
    }
}
