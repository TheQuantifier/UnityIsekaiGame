using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Persistence
{
    public sealed class QuestSourcePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.quest-sources";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly QuestSourceRuntime runtime;
        private readonly Func<QuestRuntime> questRuntimeProvider;
        private readonly Func<QuestParticipationRuntime> participationRuntimeProvider;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public QuestSourcePersistenceParticipant(
            QuestSourceRuntime runtime,
            Func<QuestRuntime> questRuntimeProvider,
            Func<QuestParticipationRuntime> participationRuntimeProvider,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.questRuntimeProvider = questRuntimeProvider;
            this.participationRuntimeProvider = participationRuntimeProvider;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 184;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { QuestRuntimePersistenceParticipant.Key, QuestParticipationRuntimePersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            QuestObjectiveProgressPersistenceParticipant.Key,
            QuestOutcomePersistenceParticipant.Key,
            LocationPersistenceParticipant.Key,
            InteractionPointPersistenceParticipant.Key,
            OrganizationPersistenceParticipant.Key,
            OrganizationMembershipPersistenceParticipant.Key,
            GovernmentPersistenceParticipant.Key,
            FactionPersistenceParticipant.Key,
            BusinessPersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key,
            KnowledgeRecordPersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Quest source runtime is missing.");
            }

            QuestSourceRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Quest source snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported quest source participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Quest source payload is empty.");
            }

            QuestSourceRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<QuestSourceRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Quest source payload is malformed JSON.");
            }

            QuestRuntime quests = questRuntimeProvider?.Invoke();
            QuestParticipationRuntime participation = participationRuntimeProvider?.Invoke();
            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!QuestSourceRuntime.ValidateSaveData(saveData, quests, participation, registry, ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Quest source runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared quest source payload has the wrong type.");
            }

            QuestRuntime quests = questRuntimeProvider?.Invoke();
            QuestParticipationRuntime participation = participationRuntimeProvider?.Invoke();
            DefinitionRegistry registry = registryProvider?.Invoke();
            QuestSourceRuntimeSaveData rollback = runtime.CreateSaveData();
            QuestSourceOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, quests, participation, registry, ownerId);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Quest sources restored.");
            }

            runtime.RestoreFromSaveData(rollback, quests, participation, registry, ownerId);
            return PersistenceParticipantCommitResult.Failure($"Quest source commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(QuestSourceRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public QuestSourceRuntimeSaveData SaveData { get; }
        }
    }
}
