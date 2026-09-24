using System;
using UnityEngine;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerQuestContractPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "player.quests-contracts";
        public const int CurrentParticipantSchemaVersion = 2;

        private readonly PlayerQuestLog questLog;
        private readonly PlayerContractJournal contractJournal;
        private readonly PlayerInventory inventory;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public PlayerQuestContractPersistenceParticipant(
            PlayerQuestLog questLog,
            PlayerContractJournal contractJournal,
            PlayerInventory inventory,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalPlayerId)
        {
            this.questLog = questLog;
            this.contractJournal = contractJournal;
            this.inventory = inventory;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.QuestsAndContracts;
        public int LoadPriority => 0;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[]
        {
            PlayerInventoryEquipmentPersistenceParticipant.Key,
            PlayerResourcesPersistenceParticipant.Key,
            PlayerStatusEffectsPersistenceParticipant.Key
        };
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

            PlayerQuestContractSaveData saveData = new PlayerQuestContractSaveData
            {
                schemaVersion = CurrentParticipantSchemaVersion,
                quests = questLog.CreateSaveData(),
                contracts = contractJournal.CreateSaveData()
            };

            PersistenceParticipantPrepareResult validation = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (validation == null || !validation.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(validation?.Message ?? "Quest/contract snapshot failed validation.");
            }

            DiscardPreparedPayload(validation.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported quest/contract participant schema version {payloadSchemaVersion}; schema 1 index-based objective saves are rejected because they cannot be restored safely after stage or objective reordering.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Quest/contract payload is empty.");
            }

            PlayerQuestContractSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<PlayerQuestContractSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Quest/contract payload is malformed JSON.");
            }

            if (saveData == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Quest/contract payload did not parse.");
            }

            if (saveData.schemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported quest/contract payload schema version {saveData.schemaVersion}.");
            }

            if (!ValidateRuntimeReferences(out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantPrepareResult.Failure("Definition registry is not available for quest/contract restore.");
            }

            if (!ValidateOnTemporaryRuntime(saveData, registry, out failureReason))
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
                return PersistenceParticipantCommitResult.Failure("Prepared quest/contract payload has the wrong type.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                return PersistenceParticipantCommitResult.Failure("Definition registry is not available for quest/contract commit.");
            }

            PlayerQuestContractSaveData rollback = new PlayerQuestContractSaveData
            {
                schemaVersion = CurrentParticipantSchemaVersion,
                quests = questLog.CreateSaveData(),
                contracts = contractJournal.CreateSaveData()
            };

            if (!questLog.TryRestoreFromSaveData(prepared.SaveData.quests, registry, out failureReason)
                || !contractJournal.TryRestoreFromSaveData(prepared.SaveData.contracts, registry, out failureReason))
            {
                questLog.TryRestoreFromSaveData(rollback.quests, registry, out _);
                contractJournal.TryRestoreFromSaveData(rollback.contracts, registry, out _);
                return PersistenceParticipantCommitResult.Failure($"Quest/contract commit failed after preparation; rollback attempted: {failureReason}");
            }

            return PersistenceParticipantCommitResult.Success("Player quests and contracts restored.");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private bool ValidateRuntimeReferences(out string failureReason)
        {
            failureReason = string.Empty;
            if (questLog == null)
            {
                failureReason = "Player quest log is missing.";
                return false;
            }

            if (contractJournal == null)
            {
                failureReason = "Player contract journal is missing.";
                return false;
            }

            if (inventory == null)
            {
                failureReason = "Player inventory is missing for quest/contract persistence.";
                return false;
            }

            return true;
        }

        private static bool ValidateOnTemporaryRuntime(PlayerQuestContractSaveData saveData, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            GameObject temporary = new GameObject("Quest Contract Persistence Validation");
            temporary.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                PlayerInventory temporaryInventory = temporary.AddComponent<PlayerInventory>();
                PlayerQuestLog temporaryQuestLog = temporary.AddComponent<PlayerQuestLog>();
                PlayerContractJournal temporaryContractJournal = temporary.AddComponent<PlayerContractJournal>();

                if (!temporaryQuestLog.TryRestoreFromSaveData(saveData.quests, registry, out failureReason))
                {
                    failureReason = $"Quest restore validation failed: {failureReason}";
                    return false;
                }

                if (!temporaryContractJournal.TryRestoreFromSaveData(saveData.contracts, registry, out failureReason))
                {
                    failureReason = $"Contract restore validation failed: {failureReason}";
                    return false;
                }

                return temporaryInventory != null;
            }
            finally
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(temporary);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(temporary);
                }
            }
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(PlayerQuestContractSaveData saveData)
            {
                SaveData = saveData;
            }

            public PlayerQuestContractSaveData SaveData { get; }
        }
    }
}
