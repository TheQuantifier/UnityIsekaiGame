using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerTraitsPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "player.traits";
        public const int CurrentParticipantSchemaVersion = PlayerTraitsSaveData.CurrentSchemaVersion;

        private readonly CharacterTraitCollection traits;
        private readonly PlayerIdentityProgression identity;
        private readonly CalculatedStatCollection calculatedStats;
        private readonly CharacterSkillCollection skills;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public PlayerTraitsPersistenceParticipant(
            CharacterTraitCollection traits,
            PlayerIdentityProgression identity,
            CalculatedStatCollection calculatedStats,
            CharacterSkillCollection skills,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.traits = traits;
            this.identity = identity;
            this.calculatedStats = calculatedStats;
            this.skills = skills;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Skills;
        public int LoadPriority => 25;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[] { PlayerIdentityProgressionPersistenceParticipant.Key, PlayerAttributesPersistenceParticipant.Key, PlayerSkillsPersistenceParticipant.Key };
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
                return PersistenceParticipantSaveResult.Failure("Definition registry is not available for player Traits capture.");
            }

            if (!traits.IsConfigured)
            {
                traits.Configure(registry, calculatedStats, skills, ownerId);
            }

            PlayerTraitsSaveData saveData = traits.CreateSaveData(ownerId, identity == null ? string.Empty : identity.PersonId);
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Player Traits snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported player Traits participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Player Traits payload is empty.");
            }

            PlayerTraitsSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PlayerTraitsSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Player Traits payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Player Traits payload did not parse.");
            }

            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!CharacterTraitCollection.ValidateSaveData(saveData, registry, ownerId, out failureReason))
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
                return PersistenceParticipantCommitResult.Failure("Prepared player Traits payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantCommitResult.Failure("Definition registry is not available for player Traits commit.");
            }

            PlayerTraitsSaveData rollback = traits.CreateSaveData(ownerId, identity == null ? string.Empty : identity.PersonId);
            if (traits.RestoreFromSaveData(prepared.SaveData, registry, calculatedStats, skills, ownerId, out failureReason, restoring: true))
            {
                return PersistenceParticipantCommitResult.Success("Player Traits restored.");
            }

            traits.RestoreFromSaveData(rollback, registry, calculatedStats, skills, ownerId, out _, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Player Traits commit failed; rollback attempted: {failureReason}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (traits == null)
            {
                failureReason = "Player Trait collection is missing.";
                return false;
            }

            if (calculatedStats == null)
            {
                failureReason = "Player calculated stats are missing for Trait effect rebuild.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PlayerTraitsSaveData saveData)
            {
                SaveData = saveData;
            }

            public PlayerTraitsSaveData SaveData { get; }
        }
    }
}
