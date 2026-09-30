using System;
using UnityEngine;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat.OngoingEffects
{
    public sealed class RuntimeOngoingEffectInstance
    {
        private float elapsedSeconds;
        private float nextTickElapsedSeconds;
        private int completedTicks;
        private int stackCount;
        private OngoingEffectInstanceState state;
        private long revision;

        internal RuntimeOngoingEffectInstance(
            OngoingEffectDefinition definition,
            string instanceId,
            string applicationTransactionId,
            string sourceActorId,
            GameObject sourceObject,
            string targetActorId,
            GameObject targetObject,
            string originId,
            float amountPerTick,
            float tickInterval,
            float totalDuration,
            int finiteTickCount,
            int initialStacks)
        {
            Definition = definition;
            InstanceId = string.IsNullOrWhiteSpace(instanceId) ? $"ongoing.runtime.{Guid.NewGuid():N}" : instanceId;
            ApplicationTransactionId = applicationTransactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            SourceObject = sourceObject;
            TargetActorId = targetActorId ?? string.Empty;
            TargetObject = targetObject;
            OriginId = originId ?? string.Empty;
            AmountPerTick = Mathf.Max(0f, amountPerTick);
            TickInterval = Mathf.Max(0.001f, tickInterval);
            TotalDuration = Mathf.Max(0f, totalDuration);
            FiniteTickCount = Mathf.Max(0, finiteTickCount);
            stackCount = Mathf.Clamp(initialStacks, 1, definition == null ? 1 : definition.MaximumStacks);
            nextTickElapsedSeconds = definition != null && definition.TickImmediately ? 0f : definition == null ? TickInterval : definition.InitialDelay;
            state = OngoingEffectInstanceState.Active;
            revision = 1L;
        }

        public OngoingEffectDefinition Definition { get; }
        public string InstanceId { get; }
        public string ApplicationTransactionId { get; }
        public string SourceActorId { get; }
        public GameObject SourceObject { get; }
        public string TargetActorId { get; }
        public GameObject TargetObject { get; }
        public string OriginId { get; }
        public float AmountPerTick { get; }
        public float TickInterval { get; }
        public float TotalDuration { get; }
        public int FiniteTickCount { get; }
        public float ElapsedSeconds => elapsedSeconds;
        public float NextTickElapsedSeconds => nextTickElapsedSeconds;
        public int CompletedTicks => completedTicks;
        public int StackCount => stackCount;
        public OngoingEffectInstanceState State => state;
        public long Revision => revision;
        public bool IsTerminal => state == OngoingEffectInstanceState.Completed || state == OngoingEffectInstanceState.Cancelled || state == OngoingEffectInstanceState.Invalid;
        public float RemainingDuration => TotalDuration <= 0f ? 0f : Mathf.Max(0f, TotalDuration - elapsedSeconds);

        internal float CurrentTickAmount => Definition == null ? 0f : Definition.ResolveAmount(AmountPerTick, stackCount);
        internal bool HasDurationLimit => TotalDuration > 0f;
        internal bool HasTickCountLimit => FiniteTickCount > 0;
        internal bool HasDueTick => !IsTerminal && state == OngoingEffectInstanceState.Active && nextTickElapsedSeconds <= elapsedSeconds + CharacterResourceCollection.Epsilon && IsTickLegalAt(nextTickElapsedSeconds);

        internal bool IsTickLegalAt(float scheduledElapsed)
        {
            if (HasTickCountLimit && completedTicks >= FiniteTickCount)
            {
                return false;
            }

            return !HasDurationLimit || scheduledElapsed <= TotalDuration + CharacterResourceCollection.Epsilon;
        }

        internal void AdvanceElapsed(float deltaSeconds)
        {
            if (state == OngoingEffectInstanceState.Active)
            {
                elapsedSeconds += Mathf.Max(0f, deltaSeconds);
                revision++;
            }
        }

        internal int ReserveDueTick()
        {
            int tickIndex = completedTicks;
            completedTicks++;
            nextTickElapsedSeconds += TickInterval;
            revision++;
            return tickIndex;
        }

        internal void RollbackReservedTick()
        {
            completedTicks = Mathf.Max(0, completedTicks - 1);
            nextTickElapsedSeconds = Mathf.Max(0f, nextTickElapsedSeconds - TickInterval);
            revision++;
        }

        internal void RefreshDuration()
        {
            if (TotalDuration <= 0f)
            {
                return;
            }

            elapsedSeconds = 0f;
            nextTickElapsedSeconds = Definition != null && Definition.TickImmediately ? 0f : Definition == null ? TickInterval : Definition.InitialDelay;
            completedTicks = 0;
            state = OngoingEffectInstanceState.Active;
            revision++;
        }

        internal bool AddStacks(int count)
        {
            int previous = stackCount;
            int maximum = Definition == null ? 1 : Definition.MaximumStacks;
            stackCount = Mathf.Clamp(stackCount + Mathf.Max(1, count), 1, maximum);
            if (previous == stackCount)
            {
                return false;
            }

            revision++;
            return true;
        }

        internal void Pause()
        {
            if (state == OngoingEffectInstanceState.Active)
            {
                state = OngoingEffectInstanceState.Paused;
                revision++;
            }
        }

        internal void Resume()
        {
            if (state == OngoingEffectInstanceState.Paused)
            {
                state = OngoingEffectInstanceState.Active;
                revision++;
            }
        }

        internal void Complete()
        {
            if (!IsTerminal)
            {
                state = OngoingEffectInstanceState.Completed;
                revision++;
            }
        }

        internal void Cancel()
        {
            if (!IsTerminal)
            {
                state = OngoingEffectInstanceState.Cancelled;
                revision++;
            }
        }

        internal void Invalidate()
        {
            if (!IsTerminal)
            {
                state = OngoingEffectInstanceState.Invalid;
                revision++;
            }
        }

        internal void RestoreMutableState(float elapsed, float nextTickElapsed, int restoredCompletedTicks, int restoredStackCount, OngoingEffectInstanceState restoredState, long restoredRevision)
        {
            elapsedSeconds = Mathf.Max(0f, elapsed);
            nextTickElapsedSeconds = Mathf.Max(0f, nextTickElapsed);
            completedTicks = Mathf.Max(0, restoredCompletedTicks);
            stackCount = Mathf.Clamp(restoredStackCount, 1, Definition == null ? 1 : Definition.MaximumStacks);
            state = restoredState;
            revision = Math.Max(1L, restoredRevision);
        }
    }
}
