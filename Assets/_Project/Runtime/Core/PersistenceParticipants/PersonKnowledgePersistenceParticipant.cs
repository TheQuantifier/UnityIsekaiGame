using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PersonKnowledgePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "person.knowledge";
        public const int CurrentParticipantSchemaVersion = PersonKnowledgeSaveData.CurrentSchemaVersion;

        private readonly PersonKnowledgeRuntime knowledge;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public PersonKnowledgePersistenceParticipant(
            PersonKnowledgeRuntime knowledge,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.knowledge = knowledge;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Notification;
        public int LoadPriority => 80;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[] { PlayerBodyPersistenceParticipant.Key };
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

            PersonKnowledgeSaveData saveData = knowledge.CreateSaveData();
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Person Knowledge snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion < 1 || payloadSchemaVersion > CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported Person Knowledge participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Person Knowledge payload is empty.");
            }

            PersonKnowledgeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PersonKnowledgeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Person Knowledge payload is malformed JSON.");
            }

            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!PersonKnowledgeRuntime.ValidateSaveData(saveData, registry, knowledge.PersonId, out failureReason))
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
                return PersistenceParticipantCommitResult.Failure("Prepared Person Knowledge payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            PersonKnowledgeSaveData rollback = knowledge.CreateSaveData();
            KnowledgeOperationResult result = knowledge.RestoreFromSaveData(prepared.SaveData, registry, knowledge.PersonId, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Person Knowledge restored.");
            }

            knowledge.RestoreFromSaveData(rollback, registry, rollback.personId, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Person Knowledge commit failed; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (knowledge == null)
            {
                failureReason = "Person Knowledge runtime is missing.";
                return false;
            }

            if (registryProvider?.Invoke() == null)
            {
                failureReason = "Definition registry is not available for Person Knowledge persistence.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(knowledge.PersonId))
            {
                failureReason = "Person Knowledge runtime has no Person ID.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PersonKnowledgeSaveData saveData)
            {
                SaveData = saveData;
            }

            public PersonKnowledgeSaveData SaveData { get; }
        }
    }
}
