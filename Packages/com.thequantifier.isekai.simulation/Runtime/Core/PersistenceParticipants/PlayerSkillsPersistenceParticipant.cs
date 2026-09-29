using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Skills;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerSkillsPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "player.skills";
        public const int CurrentParticipantSchemaVersion = PlayerSkillsSaveData.CurrentSchemaVersion;

        private readonly CharacterSkillCollection skills;
        private readonly PlayerIdentityProgression identity;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public PlayerSkillsPersistenceParticipant(
            CharacterSkillCollection skills,
            PlayerIdentityProgression identity,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.skills = skills;
            this.identity = identity;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Skills;
        public int LoadPriority => 0;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { PlayerIdentityProgressionPersistenceParticipant.Key, PlayerAttributesPersistenceParticipant.Key };
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
                return PersistenceParticipantSaveResult.Failure("Definition registry is not available for player Skills capture.");
            }

            if (!skills.IsConfigured)
            {
                return PersistenceParticipantSaveResult.Failure("Character Skills are not initialized; persistence cannot bootstrap runtime systems.");
            }

            PlayerSkillsSaveData saveData = skills.CreateSaveData(identity.PlayerId, identity.PersonId);
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Player Skills snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported player Skills participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Player Skills payload is empty.");
            }

            PlayerSkillsSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PlayerSkillsSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Player Skills payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Player Skills payload did not parse.");
            }

            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!CharacterSkillCollection.ValidateSaveData(saveData, registry, out failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            if (!string.IsNullOrWhiteSpace(saveData.playerId) && !string.Equals(saveData.playerId, identity.PlayerId, StringComparison.Ordinal))
            {
                return PersistenceParticipantPrepareResult.Failure($"Saved Skills owner '{saveData.playerId}' does not match current player '{identity.PlayerId}'.");
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
                return PersistenceParticipantCommitResult.Failure("Prepared player Skills payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantCommitResult.Failure("Definition registry is not available for player Skills commit.");
            }

            PlayerSkillsSaveData rollback = skills.CreateSaveData(identity.PlayerId, identity.PersonId);
            if (skills.RestoreFromSaveData(prepared.SaveData, registry, out failureReason, restoring: true))
            {
                return PersistenceParticipantCommitResult.Success("Player Skills restored.");
            }

            skills.RestoreFromSaveData(rollback, registry, out _, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Player Skills commit failed; rollback attempted: {failureReason}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (skills == null)
            {
                failureReason = "Player Skill collection component is missing.";
                return false;
            }

            if (identity == null)
            {
                failureReason = "Player identity/progression component is missing for Skill ownership.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PlayerSkillsSaveData saveData)
            {
                SaveData = saveData;
            }

            public PlayerSkillsSaveData SaveData { get; }
        }
    }
}
