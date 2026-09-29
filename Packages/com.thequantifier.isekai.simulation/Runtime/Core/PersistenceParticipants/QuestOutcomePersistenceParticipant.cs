using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Persistence
{
    public sealed class QuestOutcomePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.quest-outcomes";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly QuestOutcomeRuntime runtime;
        private readonly Func<QuestRuntime> questRuntimeProvider;
        private readonly Func<QuestParticipationRuntime> participationRuntimeProvider;
        private readonly Func<QuestObjectiveProgressRuntime> objectiveRuntimeProvider;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<IQuestRewardEffectExecutor> rewardExecutorProvider;
        private readonly string ownerId;

        public QuestOutcomePersistenceParticipant(
            QuestOutcomeRuntime runtime,
            Func<QuestRuntime> questRuntimeProvider,
            Func<QuestParticipationRuntime> participationRuntimeProvider,
            Func<QuestObjectiveProgressRuntime> objectiveRuntimeProvider,
            Func<DefinitionRegistry> registryProvider,
            Func<IQuestRewardEffectExecutor> rewardExecutorProvider = null,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.questRuntimeProvider = questRuntimeProvider;
            this.participationRuntimeProvider = participationRuntimeProvider;
            this.objectiveRuntimeProvider = objectiveRuntimeProvider;
            this.registryProvider = registryProvider;
            this.rewardExecutorProvider = rewardExecutorProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 183;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { QuestRuntimePersistenceParticipant.Key, QuestParticipationRuntimePersistenceParticipant.Key, QuestObjectiveProgressPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            EconomyPersistenceParticipant.Key,
            ItemInstanceIdentityPersistenceParticipant.Key,
            ReputationPersistenceParticipant.Key,
            RelationshipPersistenceParticipant.Key,
            OrganizationMembershipPersistenceParticipant.Key,
            CredentialPersistenceParticipant.Key,
            LegalPersistenceParticipant.Key,
            KnowledgeRecordPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Quest outcome runtime is missing.");
            }

            QuestOutcomeRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Quest outcome snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported quest outcome participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Quest outcome payload is empty.");
            }

            QuestOutcomeRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<QuestOutcomeRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Quest outcome payload is malformed JSON.");
            }

            if (!QuestOutcomeRuntime.ValidateSaveData(saveData, questRuntimeProvider?.Invoke(), participationRuntimeProvider?.Invoke(), objectiveRuntimeProvider?.Invoke(), registryProvider?.Invoke(), ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Quest outcome runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared quest outcome payload has the wrong type.");
            }

            QuestRuntime quests = questRuntimeProvider?.Invoke();
            QuestParticipationRuntime participation = participationRuntimeProvider?.Invoke();
            QuestObjectiveProgressRuntime objectives = objectiveRuntimeProvider?.Invoke();
            DefinitionRegistry registry = registryProvider?.Invoke();
            IQuestRewardEffectExecutor executor = rewardExecutorProvider?.Invoke();
            QuestOutcomeRuntimeSaveData rollback = runtime.CreateSaveData();
            QuestOutcomeOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, quests, participation, objectives, registry, executor, ownerId);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Quest outcomes restored.");
            }

            runtime.RestoreFromSaveData(rollback, quests, participation, objectives, registry, executor, ownerId);
            return PersistenceParticipantCommitResult.Failure($"Quest outcome commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(QuestOutcomeRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public QuestOutcomeRuntimeSaveData SaveData { get; }
        }
    }
}
