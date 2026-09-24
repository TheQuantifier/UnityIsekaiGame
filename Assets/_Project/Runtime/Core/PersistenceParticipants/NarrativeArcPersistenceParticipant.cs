using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Narrative;

namespace UnityIsekaiGame.Persistence
{
    public sealed class NarrativeArcPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.narrative-arcs";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly NarrativeArcRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<NarrativeArcRuntimeIntegrations> integrationsProvider;
        private readonly string ownerId;

        public NarrativeArcPersistenceParticipant(
            NarrativeArcRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<NarrativeArcRuntimeIntegrations> integrationsProvider = null,
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
        public int LoadPriority => 196;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[]
        {
            QuestRuntimePersistenceParticipant.Key,
            NarrativeEventPersistenceParticipant.Key,
            NarrativeStatePersistenceParticipant.Key
        };

        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            QuestParticipationRuntimePersistenceParticipant.Key,
            QuestOutcomePersistenceParticipant.Key,
            QuestSourcePersistenceParticipant.Key,
            ConversationPersistenceParticipant.Key,
            DialogueFlowPersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key,
            KnowledgeRecordPersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Narrative arc runtime is missing.");
            NarrativeArcRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Narrative arc snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported narrative arc participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Narrative arc payload is empty.");

            NarrativeArcRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<NarrativeArcRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Narrative arc payload is malformed JSON.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!NarrativeArcRuntime.ValidateSaveData(saveData, registry, ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Narrative arc runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared narrative arc payload has the wrong type.");

            DefinitionRegistry registry = registryProvider?.Invoke();
            NarrativeArcRuntimeIntegrations integrations = integrationsProvider?.Invoke();
            NarrativeArcRuntimeSaveData rollback = runtime.CreateSaveData();
            NarrativeArcOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, integrations, ownerId);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Narrative arcs restored.");

            runtime.RestoreFromSaveData(rollback, registry, integrations, ownerId);
            return PersistenceParticipantCommitResult.Failure($"Narrative arc commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(NarrativeArcRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public NarrativeArcRuntimeSaveData SaveData { get; }
        }
    }
}
