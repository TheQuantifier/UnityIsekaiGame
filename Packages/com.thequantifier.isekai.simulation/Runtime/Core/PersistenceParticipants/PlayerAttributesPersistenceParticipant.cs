using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerAttributesPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "player.attributes";
        public const int CurrentParticipantSchemaVersion = PlayerAttributesSaveData.CurrentSchemaVersion;

        private readonly CharacterAttributes attributes;
        private readonly PlayerIdentityProgression identity;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public PlayerAttributesPersistenceParticipant(
            CharacterAttributes attributes,
            PlayerIdentityProgression identity,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.attributes = attributes;
            this.identity = identity;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Attributes;
        public int LoadPriority => 0;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { PlayerIdentityProgressionPersistenceParticipant.Key };
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
                return PersistenceParticipantSaveResult.Failure("Definition registry is not available for player attributes capture.");
            }

            if (!attributes.IsConfigured)
            {
                return PersistenceParticipantSaveResult.Failure("Character Attributes are not initialized; persistence cannot bootstrap runtime systems.");
            }

            PlayerAttributesSaveData saveData = attributes.CreateSaveData(identity.PlayerId, identity.PersonId);
            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Player attributes snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported player attributes participant schema version {payloadSchemaVersion}. Development saves from before Feature 5.2 are intentionally rejected.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Player attributes payload is empty.");
            }

            PlayerAttributesSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PlayerAttributesSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Player attributes payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Player attributes payload did not parse.");
            }

            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!CharacterAttributes.ValidateSaveData(saveData, registry, out failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            if (!string.IsNullOrWhiteSpace(saveData.playerId) && !string.Equals(saveData.playerId, identity.PlayerId, StringComparison.Ordinal))
            {
                return PersistenceParticipantPrepareResult.Failure($"Saved player attributes owner '{saveData.playerId}' does not match current player '{identity.PlayerId}'.");
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
                return PersistenceParticipantCommitResult.Failure("Prepared player attributes payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantCommitResult.Failure("Definition registry is not available for player attributes commit.");
            }

            PlayerAttributesSaveData rollback = attributes.CreateSaveData(identity.PlayerId, identity.PersonId);
            if (attributes.RestoreFromSaveData(prepared.SaveData, registry, out failureReason, restoring: true))
            {
                return PersistenceParticipantCommitResult.Success("Player attributes restored.");
            }

            attributes.RestoreFromSaveData(rollback, registry, out _, restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Player attributes commit failed; rollback attempted: {failureReason}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (attributes == null)
            {
                failureReason = "Player attributes component is missing.";
                return false;
            }

            if (identity == null)
            {
                failureReason = "Player identity/progression component is missing for attribute ownership.";
                return false;
            }

            return true;
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PlayerAttributesSaveData saveData)
            {
                SaveData = saveData;
            }

            public PlayerAttributesSaveData SaveData { get; }
        }
    }
}
