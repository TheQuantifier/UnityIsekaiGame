using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.StatusEffects
{
    public static class StatusEffectRestoreUtility
    {
        public static StatusEffectRestoreResult Restore(
            StatusEffectController controller,
            IReadOnlyList<StatusEffectSaveData> saveData,
            DefinitionRegistry registry,
            GameObject sourceFallback,
            float now)
        {
            if (controller == null)
            {
                return StatusEffectRestoreResult.Failure(StatusApplicationStatus.ControllerUnavailable, "Status controller is missing.");
            }

            if (registry == null)
            {
                return StatusEffectRestoreResult.Failure(StatusApplicationStatus.MalformedRestoreData, "Definition registry is missing.");
            }

            List<StatusEffectApplicationRequest> requests = new List<StatusEffectApplicationRequest>();
            HashSet<string> applicationIds = new HashSet<string>();
            IReadOnlyList<StatusEffectSaveData> entries = saveData ?? System.Array.Empty<StatusEffectSaveData>();
            for (int i = 0; i < entries.Count; i++)
            {
                StatusEffectSaveData entry = entries[i];
                StatusEffectRestoreResult entryResult = TryBuildRequest(entry, registry, sourceFallback, now, applicationIds, out StatusEffectApplicationRequest request);
                if (!entryResult.Succeeded)
                {
                    return entryResult;
                }

                requests.Add(request);
            }

            List<StatusApplicationResult> results = new List<StatusApplicationResult>();
            for (int i = 0; i < requests.Count; i++)
            {
                StatusApplicationResult result = controller.ApplyStatus(requests[i]);
                if (!result.Succeeded)
                {
                    for (int j = 0; j < results.Count; j++)
                    {
                        if (results[j].StatusEffect != null)
                        {
                            controller.ForceRemoveStatus(results[j].StatusEffect.ApplicationId);
                        }
                    }

                    return StatusEffectRestoreResult.Failure(result.Status, result.Message);
                }

                StatusEffectSaveData entry = entries[i];
                result.StatusEffect.RestoreStackCount(entry.stackCount);
                result.StatusEffect.RestoreElapsed(entry.elapsedDuration);
                controller.RefreshStatusModifiers(result.StatusEffect);
                results.Add(result);
            }

            return StatusEffectRestoreResult.Success();
        }

        private static StatusEffectRestoreResult TryBuildRequest(
            StatusEffectSaveData entry,
            DefinitionRegistry registry,
            GameObject sourceFallback,
            float now,
            HashSet<string> applicationIds,
            out StatusEffectApplicationRequest request)
        {
            request = default;
            if (entry == null)
            {
                return StatusEffectRestoreResult.Failure(StatusApplicationStatus.MalformedRestoreData, "Status save entry is null.");
            }

            if (string.IsNullOrWhiteSpace(entry.statusDefinitionId))
            {
                return StatusEffectRestoreResult.Failure(StatusApplicationStatus.MalformedRestoreData, "Status save entry has no definition ID.");
            }

            if (string.IsNullOrWhiteSpace(entry.applicationId))
            {
                return StatusEffectRestoreResult.Failure(StatusApplicationStatus.MalformedRestoreData, "Status save entry has no application ID.");
            }

            if (!applicationIds.Add(entry.applicationId))
            {
                return StatusEffectRestoreResult.Failure(StatusApplicationStatus.DuplicateApplicationId, $"Duplicate status application ID '{entry.applicationId}' in save data.");
            }

            if (!registry.TryGet(entry.statusDefinitionId, out StatusEffectDefinition definition))
            {
                return StatusEffectRestoreResult.Failure(StatusApplicationStatus.MissingDefinition, $"Status definition '{entry.statusDefinitionId}' was not found.");
            }

            if (entry.stackCount < 1 || entry.stackCount > definition.MaximumStacks)
            {
                return StatusEffectRestoreResult.Failure(StatusApplicationStatus.MalformedRestoreData, $"Status '{definition.DisplayName}' has invalid stack count {entry.stackCount}.");
            }

            if (definition.DurationModel == StatusDurationModel.Timed && entry.remainingDuration <= 0f)
            {
                return StatusEffectRestoreResult.Failure(StatusApplicationStatus.InvalidDuration, $"Timed status '{definition.DisplayName}' has no remaining duration.");
            }

            request = new StatusEffectApplicationRequest(
                definition,
                sourceFallback,
                entry.sourceId,
                entry.remainingDuration,
                entry.applicationId,
                now);
            return StatusEffectRestoreResult.Success();
        }
    }
}
