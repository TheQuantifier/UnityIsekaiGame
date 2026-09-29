using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Persistence
{
    public sealed class QuestParticipationRuntimePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.quest-participation";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly QuestParticipationRuntime runtime;
        private readonly Func<QuestRuntime> questRuntimeProvider;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public QuestParticipationRuntimePersistenceParticipant(
            QuestParticipationRuntime runtime,
            Func<QuestRuntime> questRuntimeProvider,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.questRuntimeProvider = questRuntimeProvider;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 181;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { QuestRuntimePersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            LocationPersistenceParticipant.Key,
            InteractionPointPersistenceParticipant.Key,
            OrganizationPersistenceParticipant.Key,
            GovernmentPersistenceParticipant.Key,
            LegalPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Quest participation runtime is missing.");
            }

            QuestParticipationRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Quest participation snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported quest participation participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Quest participation payload is empty.");
            }

            QuestParticipationRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<QuestParticipationRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Quest participation payload is malformed JSON.");
            }

            QuestRuntime questRuntime = questRuntimeProvider?.Invoke();
            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!QuestParticipationRuntime.ValidateSaveData(saveData, questRuntime, registry, ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Quest participation runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared quest participation payload has the wrong type.");
            }

            QuestRuntime questRuntime = questRuntimeProvider?.Invoke();
            DefinitionRegistry registry = registryProvider?.Invoke();
            QuestParticipationRuntimeSaveData rollback = runtime.CreateSaveData();
            QuestParticipationOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, questRuntime, registry, ownerId);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Quest participation restored.");
            }

            runtime.RestoreFromSaveData(rollback, questRuntime, registry, ownerId);
            return PersistenceParticipantCommitResult.Failure($"Quest participation commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(QuestParticipationRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public QuestParticipationRuntimeSaveData SaveData { get; }
        }
    }
}
