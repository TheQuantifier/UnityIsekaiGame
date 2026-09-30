using System;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerActorLifecyclePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "player.actor-lifecycle";
        public const int CurrentParticipantSchemaVersion = ActorLifecycleSaveData.CurrentSchemaVersion;

        private readonly ActorLifecycleController lifecycle;
        private readonly PlayerIdentityProgression identity;
        private readonly string ownerId;

        public PlayerActorLifecyclePersistenceParticipant(
            ActorLifecycleController lifecycle,
            PlayerIdentityProgression identity,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.lifecycle = lifecycle;
            this.identity = identity;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Vitals;
        public int LoadPriority => 25;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { PlayerResourcesPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => false;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantSaveResult.Failure(failureReason);
            }

            if (!lifecycle.ValidateHealthStateCoherence(out failureReason))
            {
                return PersistenceParticipantSaveResult.Failure(failureReason);
            }

            ActorLifecycleSaveData saveData = lifecycle.CreateSaveData(ownerId, identity == null ? string.Empty : identity.PersonId);
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Actor lifecycle snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported actor lifecycle participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Actor lifecycle payload is empty.");
            }

            ActorLifecycleSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ActorLifecycleSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Actor lifecycle payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Actor lifecycle payload did not parse.");
            }

            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            if (!ActorLifecycleController.ValidateSaveData(saveData, ownerId, lifecycle.ActorId, out failureReason))
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
                return PersistenceParticipantCommitResult.Failure("Prepared actor lifecycle payload has the wrong type.");
            }

            ActorLifecycleSaveData rollback = lifecycle.CreateSaveData(ownerId, identity == null ? string.Empty : identity.PersonId);
            if (lifecycle.RestoreFromSaveData(prepared.SaveData, ownerId, lifecycle.ActorId, out failureReason, restoring: true)
                && lifecycle.ValidateHealthStateCoherence(out failureReason))
            {
                return PersistenceParticipantCommitResult.Success("Player actor lifecycle restored.");
            }

            lifecycle.RestoreFromSaveData(rollback, ownerId, rollback.actorId, out _, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Actor lifecycle commit failed; rollback attempted: {failureReason}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (lifecycle == null)
            {
                failureReason = "Player actor lifecycle controller is missing.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ActorLifecycleSaveData saveData)
            {
                SaveData = saveData;
            }

            public ActorLifecycleSaveData SaveData { get; }
        }
    }
}
