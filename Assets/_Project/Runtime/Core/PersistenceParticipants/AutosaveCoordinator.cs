using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Gameplay;

namespace UnityIsekaiGame.Persistence
{
    public sealed class AutosaveCoordinator : MonoBehaviour
    {
        [SerializeField, Min(5f)] private float intervalSeconds = 300f;
        [SerializeField, Min(1f)] private float retryDelaySeconds = 30f;
        [SerializeField, Min(1f)] private float debounceSeconds = 8f;

        private PrototypePersistenceServiceBehaviour persistence;
        private float nextTimerAutosaveAt;
        private float pendingAutosaveAt = -1f;
        private readonly HashSet<string> pendingReasons = new HashSet<string>(StringComparer.Ordinal);
        private string lastTrigger = "None";
        private string lastResult = "None";

        public float IntervalSeconds => intervalSeconds;
        public string LastTrigger => lastTrigger;
        public string LastResult => lastResult;
        public bool HasPendingAutosave => pendingReasons.Count > 0;

        public void Configure(PrototypePersistenceServiceBehaviour persistenceService, float interval)
        {
            persistence = persistenceService;
            intervalSeconds = Mathf.Max(5f, interval);
            ResetTimer();
        }

        private void Update()
        {
            if (persistence == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (pendingReasons.Count > 0 && now >= pendingAutosaveAt)
            {
                RunAutosave("Pending: " + string.Join(", ", pendingReasons), force: false);
                pendingReasons.Clear();
                pendingAutosaveAt = -1f;
                return;
            }

            if (now >= nextTimerAutosaveAt)
            {
                RunAutosave("Timer", force: false);
            }
        }

        public void RequestAutosave(string reason)
        {
            string normalized = string.IsNullOrWhiteSpace(reason) ? "Event" : reason;
            pendingReasons.Add(normalized);
            pendingAutosaveAt = Time.unscaledTime + debounceSeconds;
            lastTrigger = normalized;
        }

        public PersistenceSaveResult ForceAutosave(string reason = "DevelopmentCommand")
        {
            pendingReasons.Clear();
            pendingAutosaveAt = -1f;
            return RunAutosave(reason, force: true);
        }

        public void SetIntervalForTesting(float seconds)
        {
            intervalSeconds = Mathf.Max(5f, seconds);
            ResetTimer();
        }

        private PersistenceSaveResult RunAutosave(string reason, bool force)
        {
            if (persistence == null)
            {
                lastResult = "Autosave skipped: persistence service missing.";
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.UnknownException, string.Empty, string.Empty, lastResult);
            }

            if (!force && (persistence.DirtyTracker == null || !persistence.DirtyTracker.IsDirty))
            {
                lastTrigger = reason;
                lastResult = "Autosave skipped: no persistent state has changed.";
                nextTimerAutosaveAt = Time.unscaledTime + intervalSeconds;
                return PersistenceSaveResult.Success(PrototypeSaveSlotCatalog.AutosaveSlotId(0), string.Empty, lastResult);
            }

            SaveEligibilityResult eligibility = persistence.CheckSaveEligibility(showDetailedPlayerMessage: false, allowOpenMenu: false);
            if (!eligibility.Allowed)
            {
                lastTrigger = reason;
                lastResult = $"Autosave blocked: {eligibility.Status}";
                nextTimerAutosaveAt = Time.unscaledTime + retryDelaySeconds;
                return PersistenceSaveResult.Failure(PersistenceSaveStatus.OperationAlreadyRunning, PrototypeSaveSlotCatalog.AutosaveSlotId(0), string.Empty, lastResult);
            }

            lastTrigger = reason;
            PrototypeHudMessageBus.Show("Autosaving...");
            PersistenceSaveResult result = persistence.SaveAutosave(reason);
            lastResult = result.Message;
            PrototypeHudMessageBus.Show(result.Succeeded ? "Autosave complete" : "Autosave failed");
            nextTimerAutosaveAt = Time.unscaledTime + (result.Succeeded ? intervalSeconds : retryDelaySeconds);
            return result;
        }

        public void ResetTimer()
        {
            nextTimerAutosaveAt = Time.unscaledTime + intervalSeconds;
        }
    }
}
