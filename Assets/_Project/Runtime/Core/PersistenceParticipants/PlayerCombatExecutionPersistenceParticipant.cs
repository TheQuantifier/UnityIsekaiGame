using System;
using UnityEngine;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerCombatExecutionPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "player.combat-execution";
        public const int CurrentParticipantSchemaVersion = CombatExecutionSaveData.CurrentSchemaVersion;

        private readonly CombatExecutionService combatExecution;
        private readonly string ownerId;
        private readonly Func<string> personIdProvider;

        public PlayerCombatExecutionPersistenceParticipant(
            CombatExecutionService combatExecution,
            string ownerId = PersistenceService.LocalPlayerId,
            Func<string> personIdProvider = null)
        {
            this.combatExecution = combatExecution;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
            this.personIdProvider = personIdProvider;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Vitals;
        public int LoadPriority => 0;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { PlayerResourcesPersistenceParticipant.Key, PlayerActorLifecyclePersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => false;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (combatExecution == null)
            {
                return PersistenceParticipantSaveResult.Failure("Combat execution service is missing.");
            }

            CombatExecutionSaveData saveData = combatExecution.CreateSaveData(ownerId, ResolvePersonId());
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Combat execution snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported combat execution participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Combat execution payload is empty.");
            }

            CombatExecutionSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<CombatExecutionSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Combat execution payload is malformed JSON.");
            }

            if (!CombatExecutionService.ValidateSaveData(saveData, ownerId, out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (combatExecution == null)
            {
                return PersistenceParticipantCommitResult.Failure("Combat execution service is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared || prepared.SaveData == null)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared combat execution payload has the wrong type.");
            }

            CombatExecutionSaveData rollback = combatExecution.CreateSaveData(ownerId, ResolvePersonId());
            if (combatExecution.RestoreFromSaveData(prepared.SaveData, ownerId, out string failureReason, restoring: true))
            {
                return PersistenceParticipantCommitResult.Success("Combat execution cooldowns and charges restored.");
            }

            combatExecution.RestoreFromSaveData(rollback, ownerId, out _, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Combat execution commit failed; rollback attempted: {failureReason}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private string ResolvePersonId()
        {
            return personIdProvider == null ? string.Empty : personIdProvider() ?? string.Empty;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(CombatExecutionSaveData saveData)
            {
                SaveData = saveData;
            }

            public CombatExecutionSaveData SaveData { get; }
        }
    }
}
