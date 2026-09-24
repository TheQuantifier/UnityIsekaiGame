using System;
using UnityEngine;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Gameplay
{
    /// <summary>Player-facing Stamina view and sprint spender. CharacterResourceCollection owns values and regeneration.</summary>
    [RequireComponent(typeof(CharacterResourceCollection))]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float sprintDrainPerSecond = 20f;
        [SerializeField, Min(0f)] private float restartThreshold = 20f;
        [SerializeField] private CharacterResourceCollection resources;

        private bool subscribed;
        private bool exhausted;

        public float CurrentStamina => HasStamina ? resources.GetCurrent(ResourceIds.Stamina) : 0f;
        public float MaximumStamina => HasStamina ? resources.GetMaximum(ResourceIds.Stamina) : 0f;
        public bool CanSprint => HasStamina && !exhausted;
        public event Action<float, float> StaminaChanged;
        public event Action<float, float> CommittedStaminaChanged;
        private bool HasStamina => ResolveResources() && resources.HasResource(ResourceIds.Stamina);

        private void Awake()
        {
            ResolveResources();
            RefreshExhaustion();
        }

        private void OnEnable() { Subscribe(); Publish(); }
        private void OnDisable() => Unsubscribe();
        private void OnValidate() { sprintDrainPerSecond = Mathf.Max(0f, sprintDrainPerSecond); restartThreshold = Mathf.Max(0f, restartThreshold); }

        public bool EvaluateSprint(bool wantsSprint, bool isMoving, bool gameplayInputBlocked, float deltaTime)
        {
            RefreshExhaustion();
            if (gameplayInputBlocked || !wantsSprint || !isMoving || !CanSprint) return false;

            float amount = sprintDrainPerSecond * Mathf.Max(0f, deltaTime);
            if (amount <= CharacterResourceCollection.Epsilon) return true;

            ResourceChangeResult result = resources.TrySpend(ResourceIds.Stamina, amount, "player.stamina", "Sprint", allowPartial: false);
            RefreshExhaustion();
            return result != null && result.Succeeded;
        }

        public VitalChangeResult Restore(float amount)
        {
            VitalChangeResult result = ToVitalChangeResult(HasStamina ? resources.TryGain(ResourceIds.Stamina, amount, "player.stamina", "Stamina restore") : null, "stamina");
            RefreshExhaustion();
            return result;
        }

        public void RestoreToMaximum()
        {
            if (!HasStamina) return;
            resources.SetCurrent(ResourceIds.Stamina, resources.GetMaximum(ResourceIds.Stamina), "player.stamina", "Restore to maximum", restoration: true);
            exhausted = false;
        }

        public bool TryRestoreForPersistence(float restoredStamina, out string failureReason)
        {
            failureReason = string.Empty;
            if (!HasStamina)
            {
                failureReason = "Player Stamina resource is not configured.";
                return false;
            }

            if (float.IsNaN(restoredStamina) || float.IsInfinity(restoredStamina) || restoredStamina < resources.GetMinimum(ResourceIds.Stamina))
            {
                failureReason = $"Stamina value {restoredStamina} is invalid for save restoration.";
                return false;
            }

            resources.SetCurrent(ResourceIds.Stamina, Mathf.Clamp(restoredStamina, resources.GetMinimum(ResourceIds.Stamina), MaximumStamina), "player.stamina", "Persistence restore", restoration: true);
            RefreshExhaustion();
            return true;
        }

        public bool CanSpend(float amount) => amount <= 0f || HasStamina && resources.CanSpend(ResourceIds.Stamina, amount);

        public VitalChangeResult Spend(float amount, string reason)
        {
            VitalChangeResult result = ToVitalChangeResult(HasStamina ? resources.TrySpend(ResourceIds.Stamina, amount, "player.stamina", reason, allowPartial: false) : null, "stamina");
            RefreshExhaustion();
            return result;
        }

        public void FlushPendingSprintResourceSpend() { }
        public void RefreshResourceRuntime() { ResolveResources(); Subscribe(); RefreshExhaustion(); Publish(); }
        private bool ResolveResources() { resources ??= GetComponent<CharacterResourceCollection>(); return resources != null; }

        private void RefreshExhaustion()
        {
            if (!HasStamina) { exhausted = true; return; }
            float minimum = resources.GetMinimum(ResourceIds.Stamina);
            if (CurrentStamina <= minimum + CharacterResourceCollection.Epsilon) exhausted = true;
            else if (CurrentStamina > Mathf.Max(minimum, restartThreshold)) exhausted = false;
        }

        private void Subscribe()
        {
            if (subscribed || !ResolveResources() || !isActiveAndEnabled) return;
            resources.ResourceChanged += OnChanged;
            resources.ResourceMaximumChanged += OnMaximumChanged;
            resources.ResourcesRestored += OnRestored;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || resources == null) return;
            resources.ResourceChanged -= OnChanged;
            resources.ResourceMaximumChanged -= OnMaximumChanged;
            resources.ResourcesRestored -= OnRestored;
            subscribed = false;
        }

        private void OnChanged(CharacterResourceCollection collection, ResourceChangeResult result) { if (result.Request.ResourceId == ResourceIds.Stamina) { RefreshExhaustion(); Publish(); } }
        private void OnMaximumChanged(CharacterResourceCollection collection, ResourceSnapshot snapshot, float oldMaximum, bool restoring) { if (snapshot.ResourceId == ResourceIds.Stamina) { RefreshExhaustion(); Publish(); } }
        private void OnRestored(CharacterResourceCollection collection, bool restoring) { RefreshExhaustion(); Publish(); }

        private void Publish()
        {
            StaminaChanged?.Invoke(CurrentStamina, MaximumStamina);
            CommittedStaminaChanged?.Invoke(CurrentStamina, MaximumStamina);
        }

        private static VitalChangeResult ToVitalChangeResult(ResourceChangeResult result, string resourceName)
        {
            if (result == null) return VitalChangeResult.Failure(0f, $"Unable to change {resourceName}; its resource is not configured.");
            return result.Succeeded ? VitalChangeResult.Success(result.RequestedAmount, result.AppliedAmount, result.Message) : VitalChangeResult.Failure(result.RequestedAmount, result.Message);
        }
    }
}
