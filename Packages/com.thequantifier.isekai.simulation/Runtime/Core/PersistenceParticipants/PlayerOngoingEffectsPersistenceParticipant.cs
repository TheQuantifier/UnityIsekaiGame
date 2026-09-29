using System;
using UnityEngine;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerOngoingEffectsPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "player.ongoing-effects";
        public const int CurrentParticipantSchemaVersion = OngoingEffectsSaveData.CurrentSchemaVersion;

        private readonly OngoingEffectService ongoingEffects;
        private readonly GameObject targetObject;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<string> actorIdProvider;
        private readonly string ownerId;

        public PlayerOngoingEffectsPersistenceParticipant(
            OngoingEffectService ongoingEffects,
            GameObject targetObject,
            Func<DefinitionRegistry> registryProvider,
            Func<string> actorIdProvider,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.ongoingEffects = ongoingEffects;
            this.targetObject = targetObject;
            this.registryProvider = registryProvider;
            this.actorIdProvider = actorIdProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Statuses;
        public int LoadPriority => 50;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { PlayerResourcesPersistenceParticipant.Key, PlayerActorLifecyclePersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
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

            string actorId = actorIdProvider?.Invoke() ?? string.Empty;
            OngoingEffectsSaveData saveData = ongoingEffects.CreateSaveData(ownerId, actorId);
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Ongoing effects snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported ongoing effects participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Ongoing effects payload is empty.");
            }

            OngoingEffectsSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<OngoingEffectsSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Ongoing effects payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Ongoing effects payload did not parse.");
            }

            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!OngoingEffectService.ValidateSaveData(saveData, registry, ownerId, actorIdProvider?.Invoke() ?? string.Empty, out failureReason))
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
                return PersistenceParticipantCommitResult.Failure("Prepared ongoing effects payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantCommitResult.Failure("Definition registry is not available for ongoing effects commit.");
            }

            string actorId = actorIdProvider?.Invoke() ?? string.Empty;
            OngoingEffectsSaveData rollback = ongoingEffects.CreateSaveData(ownerId, actorId);
            if (ongoingEffects.RestoreFromSaveData(prepared.SaveData, registry, targetObject, ownerId, actorId, out failureReason, restoring: true))
            {
                return PersistenceParticipantCommitResult.Success("Player ongoing effects restored.");
            }

            ongoingEffects.RestoreFromSaveData(rollback, registry, targetObject, ownerId, actorId, out _, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Ongoing effects commit failed; rollback attempted: {failureReason}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (ongoingEffects == null)
            {
                failureReason = "Player ongoing effects service is missing.";
                return false;
            }

            if (targetObject == null)
            {
                failureReason = "Player ongoing effects target object is missing.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(OngoingEffectsSaveData saveData)
            {
                SaveData = saveData;
            }

            public OngoingEffectsSaveData SaveData { get; }
        }
    }
}
