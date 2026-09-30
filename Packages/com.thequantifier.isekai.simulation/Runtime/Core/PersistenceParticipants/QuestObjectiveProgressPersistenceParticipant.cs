using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Persistence
{
    public sealed class QuestObjectiveProgressPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.quest-objective-progress";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly QuestObjectiveProgressRuntime runtime;
        private readonly Func<QuestRuntime> questRuntimeProvider;
        private readonly Func<QuestParticipationRuntime> participationRuntimeProvider;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public QuestObjectiveProgressPersistenceParticipant(
            QuestObjectiveProgressRuntime runtime,
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
        public int LoadPriority => 182;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { QuestRuntimePersistenceParticipant.Key, QuestParticipationRuntimePersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            LocationPersistenceParticipant.Key,
            InteractionPointPersistenceParticipant.Key,
            ItemInstanceIdentityPersistenceParticipant.Key,
            OrganizationPersistenceParticipant.Key,
            OrganizationMembershipPersistenceParticipant.Key,
            LegalPersistenceParticipant.Key,
            TravelJourneyPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Quest objective progress runtime is missing.");
            }

            QuestObjectiveProgressRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Quest objective progress snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported quest objective progress participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Quest objective progress payload is empty.");
            }

            QuestObjectiveProgressRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<QuestObjectiveProgressRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Quest objective progress payload is malformed JSON.");
            }

            QuestRuntime questRuntime = questRuntimeProvider?.Invoke();
            QuestParticipationRuntime participationRuntime = participationRuntimeProvider?.Invoke();
            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!QuestObjectiveProgressRuntime.ValidateSaveData(saveData, questRuntime, participationRuntime, registry, ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Quest objective progress runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared quest objective progress payload has the wrong type.");
            }

            QuestRuntime questRuntime = questRuntimeProvider?.Invoke();
            QuestParticipationRuntime participationRuntime = participationRuntimeProvider?.Invoke();
            DefinitionRegistry registry = registryProvider?.Invoke();
            QuestObjectiveProgressRuntimeSaveData rollback = runtime.CreateSaveData();
            QuestObjectiveOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, questRuntime, participationRuntime, registry, ownerId);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Quest objective progress restored.");
            }

            runtime.RestoreFromSaveData(rollback, questRuntime, participationRuntime, registry, ownerId);
            return PersistenceParticipantCommitResult.Failure($"Quest objective progress commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(QuestObjectiveProgressRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public QuestObjectiveProgressRuntimeSaveData SaveData { get; }
        }
    }
}
