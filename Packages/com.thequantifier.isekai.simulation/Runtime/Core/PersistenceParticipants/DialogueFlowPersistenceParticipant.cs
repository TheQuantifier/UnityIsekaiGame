using System;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Persistence
{
    public sealed class DialogueFlowPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.dialogue-flows";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly DialogueFlowRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<ConversationRuntime> conversationProvider;
        private readonly Func<IDialogueEffectExecutor> effectExecutorProvider;
        private readonly string ownerId;

        public DialogueFlowPersistenceParticipant(
            DialogueFlowRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<ConversationRuntime> conversationProvider,
            Func<IDialogueEffectExecutor> effectExecutorProvider = null,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.conversationProvider = conversationProvider;
            this.effectExecutorProvider = effectExecutorProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 191;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { ConversationPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            QuestRuntimePersistenceParticipant.Key,
            QuestParticipationRuntimePersistenceParticipant.Key,
            QuestObjectiveProgressPersistenceParticipant.Key,
            QuestOutcomePersistenceParticipant.Key,
            QuestSourcePersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key,
            InformationTransferPersistenceParticipant.Key,
            KnowledgeRecordPersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key,
            RelationshipPersistenceParticipant.Key,
            InterpersonalAttitudePersistenceParticipant.Key,
            ReputationPersistenceParticipant.Key,
            OrganizationPersistenceParticipant.Key,
            OrganizationMembershipPersistenceParticipant.Key,
            OrganizationAuthorityPersistenceParticipant.Key,
            GovernmentPersistenceParticipant.Key,
            LegalPersistenceParticipant.Key,
            LocationPersistenceParticipant.Key,
            InteractionPointPersistenceParticipant.Key,
            EntityLocationPersistenceParticipant.Key,
            PlayerInventoryEquipmentPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Dialogue flow runtime is missing.");
            DialogueFlowRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Dialogue flow snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported dialogue flow participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Dialogue flow payload is empty.");

            DialogueFlowRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<DialogueFlowRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Dialogue flow payload is malformed JSON.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            ConversationRuntime conversations = conversationProvider?.Invoke();
            if (!DialogueFlowRuntime.ValidateSaveData(saveData, registry, conversations, ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Dialogue flow runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared dialogue flow payload has the wrong type.");

            DefinitionRegistry registry = registryProvider?.Invoke();
            ConversationRuntime conversations = conversationProvider?.Invoke();
            DialogueFlowRuntimeSaveData rollback = runtime.CreateSaveData();
            DialogueFlowOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, conversations, effectExecutorProvider?.Invoke(), ownerId);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Dialogue flows restored.");

            runtime.RestoreFromSaveData(rollback, registry, conversations, effectExecutorProvider?.Invoke(), ownerId);
            return PersistenceParticipantCommitResult.Failure($"Dialogue flow commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(DialogueFlowRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public DialogueFlowRuntimeSaveData SaveData { get; }
        }
    }
}
