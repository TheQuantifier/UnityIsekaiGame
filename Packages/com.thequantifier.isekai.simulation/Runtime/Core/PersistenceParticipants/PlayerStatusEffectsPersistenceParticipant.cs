using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Beings;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.StatusEffects;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerStatusEffectsPersistenceParticipant : TypedPersistenceParticipant<PlayerStatusEffectsSaveData>
    {
        public const string Key = "player.status-effects";
        public const int CurrentParticipantSchemaVersion = PlayerStatusEffectsSaveData.CurrentSchemaVersion;

        private readonly PlayerStats stats;
        private readonly StatusEffectController statusController;
        private readonly Func<DefinitionRegistry> registryProvider;

        public PlayerStatusEffectsPersistenceParticipant(
            PlayerStats stats,
            StatusEffectController statusController,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalPlayerId)
            : base(new PersistenceParticipantDescriptor(
                Key,
                CurrentParticipantSchemaVersion,
                required: true,
                PersistenceScope.Player,
                string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalPlayerId : ownerId,
                PersistenceLoadPhase.Statuses,
                loadPriority: 0,
                requiredDependencies: new[] { PlayerInventoryEquipmentPersistenceParticipant.Key },
                optionalDependencies: Array.Empty<string>(),
                supportsRollback: true,
                requiresSceneReadiness: false,
                requiresDefinitionRegistry: true,
                requiresWorldEntityRegistry: false))
        {
            this.stats = stats;
            this.statusController = statusController;
            this.registryProvider = registryProvider;
        }

        protected override bool TryCapture(out PlayerStatusEffectsSaveData saveData, out string failureReason)
        {
            saveData = null;
            if (!ValidateRuntime(out failureReason))
            {
                return false;
            }

            saveData = new PlayerStatusEffectsSaveData
            {
                actorProfileId = stats.ActorProfile == null ? string.Empty : stats.ActorProfile.Id,
                statuses = statusController.CreateSaveData(saveEligibleOnly: true)
            };
            return true;
        }

        protected override bool TryValidate(PlayerStatusEffectsSaveData saveData, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null || saveData.schemaVersion != CurrentParticipantSchemaVersion)
            {
                failureReason = "Player status-effects payload has an unsupported schema.";
                return false;
            }

            if (!ValidateRuntime(out failureReason))
            {
                return false;
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                failureReason = "Definition registry is unavailable for status-effect persistence.";
                return false;
            }

            string currentProfileId = stats.ActorProfile == null ? string.Empty : stats.ActorProfile.Id;
            if (!string.IsNullOrWhiteSpace(saveData.actorProfileId)
                && (!registry.TryGet(saveData.actorProfileId, out ActorProfileDefinition _)
                    || !string.Equals(saveData.actorProfileId, currentProfileId, StringComparison.Ordinal)))
            {
                failureReason = $"Saved actor profile '{saveData.actorProfileId}' does not match active profile '{currentProfileId}'.";
                return false;
            }

            HashSet<string> applicationIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<StatusEffectSaveData> statuses = saveData.statuses == null
                ? Array.Empty<StatusEffectSaveData>()
                : saveData.statuses;
            for (int i = 0; i < statuses.Count; i++)
            {
                StatusEffectSaveData entry = statuses[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.applicationId) || !applicationIds.Add(entry.applicationId))
                {
                    failureReason = "Status application IDs must be present and unique.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(entry.statusDefinitionId)
                    || !registry.TryGet(entry.statusDefinitionId, out StatusEffectDefinition definition))
                {
                    failureReason = $"Status definition '{entry.statusDefinitionId}' was not found.";
                    return false;
                }

                bool saveEligible = definition.DurationModel != StatusDurationModel.Instant
                    && (definition.PersistencePolicy == StatusPersistencePolicy.SaveRemainingDuration
                        || definition.PersistencePolicy == StatusPersistencePolicy.PersistentUntilRemoved);
                if (!saveEligible || entry.persistencePolicy != definition.PersistencePolicy
                    || entry.stackCount < 1 || entry.stackCount > definition.MaximumStacks)
                {
                    failureReason = $"Status '{definition.DisplayName}' contains invalid persistence data.";
                    return false;
                }

                bool validDuration = definition.DurationModel == StatusDurationModel.Timed
                    ? IsFinite(entry.remainingDuration) && entry.remainingDuration > 0f && entry.durationModel == StatusDurationModel.Timed
                    : IsFinite(entry.remainingDuration) && entry.remainingDuration >= 0f && entry.durationModel == definition.DurationModel;
                if (!validDuration)
                {
                    failureReason = $"Status '{definition.DisplayName}' contains invalid duration data.";
                    return false;
                }
            }

            return true;
        }

        protected override PlayerStatusEffectsSaveData CaptureRollback()
        {
            return new PlayerStatusEffectsSaveData
            {
                actorProfileId = stats.ActorProfile == null ? string.Empty : stats.ActorProfile.Id,
                statuses = statusController.CreateSaveData()
            };
        }

        protected override bool TryRestore(PlayerStatusEffectsSaveData saveData, out string failureReason)
        {
            failureReason = string.Empty;
            DefinitionRegistry registry = registryProvider?.Invoke();
            if (registry == null)
            {
                failureReason = "Definition registry is unavailable for status-effect restore.";
                return false;
            }

            statusController.ClearAllStatuses();
            StatusEffectRestoreResult result = StatusEffectRestoreUtility.Restore(statusController, saveData.statuses, registry, statusController.gameObject, Time.time);
            failureReason = result.Message;
            return result.Succeeded;
        }

        private bool ValidateRuntime(out string failureReason)
        {
            failureReason = string.Empty;
            if (stats == null || statusController == null)
            {
                failureReason = "Player stats and status-effect controller are required for status persistence.";
                return false;
            }

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
