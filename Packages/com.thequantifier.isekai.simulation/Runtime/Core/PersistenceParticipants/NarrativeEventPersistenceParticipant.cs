using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Narrative;

namespace UnityIsekaiGame.Persistence
{
    public sealed class NarrativeEventPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.narrative-events";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly NarrativeEventRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<NarrativeEventRuntimeIntegrations> integrationsProvider;
        private readonly string ownerId;

        public NarrativeEventPersistenceParticipant(
            NarrativeEventRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<NarrativeEventRuntimeIntegrations> integrationsProvider = null,
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
        public int LoadPriority => 192;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[]
        {
            QuestRuntimePersistenceParticipant.Key,
            ConversationPersistenceParticipant.Key
        };

        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            DialogueFlowPersistenceParticipant.Key,
            QuestParticipationRuntimePersistenceParticipant.Key,
            QuestObjectiveProgressPersistenceParticipant.Key,
            QuestOutcomePersistenceParticipant.Key,
            QuestSourcePersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key,
            KnowledgeRecordPersistenceParticipant.Key,
            PersonKnowledgePersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key,
            TravelConditionPersistenceParticipant.Key,
            LocationConnectionPersistenceParticipant.Key,
            LocationPersistenceParticipant.Key,
            InteractionPointPersistenceParticipant.Key,
            RelationshipPersistenceParticipant.Key,
            SocialInteractionPersistenceParticipant.Key,
            OrganizationMembershipPersistenceParticipant.Key,
            GovernmentPersistenceParticipant.Key,
            LegalPersistenceParticipant.Key,
            CrimePersistenceParticipant.Key,
            JusticePersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Narrative event runtime is missing.");
            NarrativeEventRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Narrative event snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported narrative event participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Narrative event payload is empty.");

            NarrativeEventRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<NarrativeEventRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Narrative event payload is malformed JSON.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!NarrativeEventRuntime.ValidateSaveData(saveData, registry, ownerId, out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Narrative event runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared narrative event payload has the wrong type.");

            DefinitionRegistry registry = registryProvider?.Invoke();
            NarrativeEventRuntimeIntegrations integrations = integrationsProvider?.Invoke();
            NarrativeEventRuntimeSaveData rollback = runtime.CreateSaveData();
            NarrativeEventOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, integrations, ownerId);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Narrative events restored.");

            runtime.RestoreFromSaveData(rollback, registry, integrations, ownerId);
            return PersistenceParticipantCommitResult.Failure($"Narrative event commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(NarrativeEventRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public NarrativeEventRuntimeSaveData SaveData { get; }
        }
    }
}
