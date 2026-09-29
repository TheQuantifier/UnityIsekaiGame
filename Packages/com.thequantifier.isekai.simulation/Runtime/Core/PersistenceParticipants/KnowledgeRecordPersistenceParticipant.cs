using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Records;

namespace UnityIsekaiGame.Persistence
{
    public sealed class KnowledgeRecordPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "person.knowledge-records";
        public const string WorldKey = "world.knowledge-records";
        public const int CurrentParticipantSchemaVersion = KnowledgeRecordSaveData.CurrentSchemaVersion;

        private readonly KnowledgeRecordRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;
        private readonly string participantKey;
        private readonly PersistenceScope scope;
        private readonly bool required;

        public KnowledgeRecordPersistenceParticipant(
            KnowledgeRecordRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalPlayerId,
            PersistenceScope scope = PersistenceScope.Player,
            string participantKey = Key,
            bool required = false)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
            this.scope = scope;
            this.participantKey = string.IsNullOrWhiteSpace(participantKey) ? Key : participantKey;
            this.required = required;
        }

        public string ParticipantKey => participantKey;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => required;
        public PersistenceScope Scope => scope;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Notification;
        public int LoadPriority => 88;
        public IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public IReadOnlyList<string> OptionalDependencies => scope == PersistenceScope.Player
            ? new[]
            {
                PersonKnowledgePersistenceParticipant.Key,
                PersonMemoryPersistenceParticipant.Key,
                AuthoritativeHistoryPersistenceParticipant.Key,
                InformationSourcePersistenceParticipant.Key,
                InformationTransferPersistenceParticipant.Key,
                InformationAccessPersistenceParticipant.Key
            }
            : new[]
            {
                AuthoritativeHistoryPersistenceParticipant.Key,
                InformationSourcePersistenceParticipant.WorldKey,
                InformationTransferPersistenceParticipant.WorldKey,
                InformationAccessPersistenceParticipant.WorldKey
            };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantSaveResult.Failure(failureReason);
            }

            KnowledgeRecordSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Knowledge Record snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion < 1 || payloadSchemaVersion > CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported Knowledge Record participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Knowledge Record payload is empty.");
            }

            KnowledgeRecordSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<KnowledgeRecordSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Knowledge Record payload is malformed JSON.");
            }

            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!runtime.ValidateConfiguredSaveData(saveData, registry, runtime.OwnerId, out failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantCommitResult.Failure(failureReason);
            }

            if (preparedPayload is not PreparedPayload prepared || prepared.SaveData == null)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared Knowledge Record payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            KnowledgeRecordSaveData rollback = runtime.CreateSaveData();
            KnowledgeRecordOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, runtime.OwnerId, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Knowledge Records restored.");
            }

            runtime.RestoreFromSaveData(rollback, registry, rollback.ownerId, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Knowledge Record commit failed; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (runtime == null)
            {
                failureReason = "Knowledge Record runtime is missing.";
                return false;
            }

            if (registryProvider?.Invoke() == null)
            {
                failureReason = "Definition registry is not available for Knowledge Record persistence.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(KnowledgeRecordSaveData saveData)
            {
                SaveData = saveData;
            }

            public KnowledgeRecordSaveData SaveData { get; }
        }
    }
}
