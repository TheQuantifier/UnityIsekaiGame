using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Persistence
{
    public sealed class InformationAccessPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "person.information-access";
        public const string WorldKey = "world.information-access";
        public const int CurrentParticipantSchemaVersion = InformationAccessSaveData.CurrentSchemaVersion;

        private readonly InformationAccessRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;
        private readonly string participantKey;
        private readonly PersistenceScope scope;
        private readonly bool required;

        public InformationAccessPersistenceParticipant(
            InformationAccessRuntime runtime,
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
        public int LoadPriority => 86;
        public IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public IReadOnlyList<string> OptionalDependencies => scope == PersistenceScope.Player
            ? new[] { PersonKnowledgePersistenceParticipant.Key, PersonMemoryPersistenceParticipant.Key, InformationSourcePersistenceParticipant.Key, InformationTransferPersistenceParticipant.Key }
            : new[] { InformationSourcePersistenceParticipant.WorldKey, InformationTransferPersistenceParticipant.WorldKey };
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

            InformationAccessSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Information Access snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion < 1 || payloadSchemaVersion > CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported Information Access participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Information Access payload is empty.");
            }

            InformationAccessSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<InformationAccessSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Information Access payload is malformed JSON.");
            }

            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!InformationAccessRuntime.ValidateSaveData(saveData, registry, runtime.OwnerId, out failureReason))
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
                return PersistenceParticipantCommitResult.Failure("Prepared Information Access payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            InformationAccessSaveData rollback = runtime.CreateSaveData();
            InformationAccessOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registry, runtime.OwnerId, restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Information Access restored.");
            }

            runtime.RestoreFromSaveData(rollback, registry, rollback.ownerId, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Information Access commit failed; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (runtime == null)
            {
                failureReason = "Information Access runtime is missing.";
                return false;
            }

            if (registryProvider?.Invoke() == null)
            {
                failureReason = "Definition registry is not available for Information Access persistence.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(InformationAccessSaveData saveData)
            {
                SaveData = saveData;
            }

            public InformationAccessSaveData SaveData { get; }
        }
    }
}
