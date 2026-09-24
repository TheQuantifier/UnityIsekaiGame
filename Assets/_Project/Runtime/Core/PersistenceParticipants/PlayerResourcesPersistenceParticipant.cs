using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerResourcesPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "player.resources";
        public const int CurrentParticipantSchemaVersion = PlayerResourcesSaveData.CurrentSchemaVersion;

        private readonly CharacterResourceCollection resources;
        private readonly PlayerIdentityProgression identity;
        private readonly CalculatedStatCollection calculatedStats;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public PlayerResourcesPersistenceParticipant(
            CharacterResourceCollection resources,
            PlayerIdentityProgression identity,
            CalculatedStatCollection calculatedStats,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.resources = resources;
            this.identity = identity;
            this.calculatedStats = calculatedStats;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Vitals;
        public int LoadPriority => 0;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { PlayerAttributesPersistenceParticipant.Key, PlayerStatusEffectsPersistenceParticipant.Key };
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

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantSaveResult.Failure("Definition registry is not available for player resources capture.");
            }

            if (!resources.IsConfigured)
            {
                return PersistenceParticipantSaveResult.Failure("Character Resources are not initialized; persistence cannot bootstrap runtime systems.");
            }

            string personId = identity == null ? string.Empty : identity.PersonId;
            PlayerResourcesSaveData saveData = resources.CreateSaveData(ownerId, personId);
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Player resources snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported player resources participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Player resources payload is empty.");
            }

            PlayerResourcesSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PlayerResourcesSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Player resources payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Player resources payload did not parse.");
            }

            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!CharacterResourceCollection.ValidateSaveData(saveData, registry, calculatedStats, ownerId, out failureReason))
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
                return PersistenceParticipantCommitResult.Failure("Prepared player resources payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantCommitResult.Failure("Definition registry is not available for player resources commit.");
            }

            PlayerResourcesSaveData rollback = resources.CreateSaveData(ownerId, identity == null ? string.Empty : identity.PersonId);
            if (resources.RestoreFromSaveData(prepared.SaveData, registry, calculatedStats, ownerId, out failureReason, restoring: true))
            {
                return PersistenceParticipantCommitResult.Success("Player resources restored.");
            }

            resources.RestoreFromSaveData(rollback, registry, calculatedStats, ownerId, out _, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Player resources commit failed; rollback attempted: {failureReason}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (resources == null)
            {
                failureReason = "Player resource collection is missing.";
                return false;
            }

            if (calculatedStats == null)
            {
                failureReason = "Player calculated stats are missing for resource maximum resolution.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PlayerResourcesSaveData saveData)
            {
                SaveData = saveData;
            }

            public PlayerResourcesSaveData SaveData { get; }
        }
    }
}
