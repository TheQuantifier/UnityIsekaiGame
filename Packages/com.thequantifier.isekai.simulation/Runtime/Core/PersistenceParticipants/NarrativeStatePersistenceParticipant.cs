using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Narrative;

namespace UnityIsekaiGame.Persistence
{
    public sealed class NarrativeStatePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.narrative-state";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly NarrativeStateRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<NarrativeStateRuntimeIntegrations> integrationsProvider;
        private readonly string ownerId;

        public NarrativeStatePersistenceParticipant(
            NarrativeStateRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<NarrativeStateRuntimeIntegrations> integrationsProvider = null,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.integrationsProvider = integrationsProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 194;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[]
        {
            NarrativeEventPersistenceParticipant.Key
        };

        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            QuestRuntimePersistenceParticipant.Key,
            QuestParticipationRuntimePersistenceParticipant.Key,
            QuestOutcomePersistenceParticipant.Key,
            QuestSourcePersistenceParticipant.Key,
            ConversationPersistenceParticipant.Key,
            DialogueFlowPersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key,
            KnowledgeRecordPersistenceParticipant.Key,
            PersonKnowledgePersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Narrative state runtime is missing.");
            NarrativeStateRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Narrative state snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported narrative state participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Narrative state payload is empty.");

            NarrativeStateRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<NarrativeStateRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Narrative state payload is malformed JSON.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!NarrativeStateRuntime.ValidateSaveData(saveData, registry, ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Narrative state runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared narrative state payload has the wrong type.");

            DefinitionRegistry registry = registryProvider?.Invoke();
            NarrativeStateRuntimeIntegrations integrations = integrationsProvider?.Invoke();
            NarrativeStateRuntimeSaveData rollback = runtime.CreateSaveData();
            NarrativeStateTransitionResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, integrations, ownerId);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Narrative state restored.");

            runtime.RestoreFromSaveData(rollback, registry, integrations, ownerId);
            return PersistenceParticipantCommitResult.Failure($"Narrative state commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(NarrativeStateRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public NarrativeStateRuntimeSaveData SaveData { get; }
        }
    }
}
