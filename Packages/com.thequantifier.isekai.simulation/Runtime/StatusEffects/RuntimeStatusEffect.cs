using System;
using UnityEngine;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.StatusEffects
{
    public sealed class RuntimeStatusEffect
    {
        private float remainingDuration;
        private float elapsedDuration;
        private int stackCount;
        private bool expired;
        private bool removed;

        public RuntimeStatusEffect(
            StatusEffectDefinition definition,
            string applicationId,
            string sourceId,
            GameObject source,
            GameObject target,
            float duration,
            float appliedAt)
        {
            Definition = definition;
            ApplicationId = string.IsNullOrWhiteSpace(applicationId) ? Guid.NewGuid().ToString("D") : applicationId;
            SourceId = string.IsNullOrWhiteSpace(sourceId) ? "source.unknown" : sourceId;
            Source = source;
            Target = target;
            remainingDuration = Mathf.Max(0f, duration);
            AppliedAt = appliedAt;
            stackCount = 1;
        }

        public StatusEffectDefinition Definition { get; }
        public string ApplicationId { get; }
        public string SourceId { get; }
        public GameObject Source { get; }
        public GameObject Target { get; }
        public float RemainingDuration => remainingDuration;
        public float ElapsedDuration => elapsedDuration;
        public int StackCount => stackCount;
        public float AppliedAt { get; }
        public bool IsExpired => expired;
        public bool IsRemoved => removed;
        public bool IsActive => !expired && !removed;
        public StatModifierSource ModifierSource => new StatModifierSource(StatModifierSourceType.StatusEffect, ApplicationId);

        public bool AddStack()
        {
            if (!IsActive || stackCount >= Definition.MaximumStacks)
            {
                return false;
            }

            stackCount++;
            return true;
        }

        public void Refresh(float duration)
        {
            if (!IsActive || Definition.DurationModel != StatusDurationModel.Timed)
            {
                return;
            }

            remainingDuration = Mathf.Max(0f, duration);
        }

        public bool Advance(float deltaTime)
        {
            if (!IsActive || Definition.DurationModel != StatusDurationModel.Timed)
            {
                return false;
            }

            float clampedDelta = Mathf.Max(0f, deltaTime);
            elapsedDuration += clampedDelta;
            remainingDuration = Mathf.Max(0f, remainingDuration - clampedDelta);
            if (remainingDuration > 0f)
            {
                return false;
            }

            expired = true;
            return true;
        }

        public bool Remove()
        {
            if (!IsActive)
            {
                return false;
            }

            removed = true;
            return true;
        }

        public StatusEffectSaveData CreateSaveData()
        {
            return new StatusEffectSaveData
            {
                statusDefinitionId = Definition == null ? string.Empty : Definition.Id,
                applicationId = ApplicationId,
                sourceId = SourceId,
                remainingDuration = remainingDuration,
                elapsedDuration = elapsedDuration,
                stackCount = stackCount,
                durationModel = Definition == null ? StatusDurationModel.Instant : Definition.DurationModel,
                persistencePolicy = Definition == null ? StatusPersistencePolicy.DoNotSave : Definition.PersistencePolicy
            };
        }

        public void RestoreStackCount(int restoredStackCount)
        {
            stackCount = Mathf.Clamp(restoredStackCount, 1, Definition.MaximumStacks);
        }

        public void RestoreElapsed(float restoredElapsedDuration)
        {
            elapsedDuration = Mathf.Max(0f, restoredElapsedDuration);
        }
    }
}
