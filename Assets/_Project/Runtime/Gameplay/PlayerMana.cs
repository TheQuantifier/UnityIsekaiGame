using System;
using UnityEngine;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Gameplay
{
    /// <summary>Player-facing Mana view. CharacterResourceCollection is the only mutable owner.</summary>
    [RequireComponent(typeof(CharacterResourceCollection))]
    public sealed class PlayerMana : MonoBehaviour
    {
        [SerializeField] private CharacterResourceCollection resources;
        private bool subscribed;

        public float CurrentMana => HasMana ? resources.GetCurrent(ResourceIds.Mana) : 0f;
        public float MaximumMana => HasMana ? resources.GetMaximum(ResourceIds.Mana) : 0f;
        public event Action<float, float> ManaChanged;
        private bool HasMana => ResolveResources() && resources.HasResource(ResourceIds.Mana);

        private void Awake() => ResolveResources();
        private void OnEnable() { Subscribe(); Publish(); }
        private void OnDisable() => Unsubscribe();

        public bool CanSpend(float amount) => amount <= 0f || HasMana && resources.CanSpend(ResourceIds.Mana, amount);

        public VitalChangeResult Spend(float amount)
        {
            return ToVitalChangeResult(HasMana ? resources.TrySpend(ResourceIds.Mana, amount, "player.mana", "Mana spend", allowPartial: false) : null, "mana");
        }

        public VitalChangeResult Restore(float amount)
        {
            return ToVitalChangeResult(HasMana ? resources.TryGain(ResourceIds.Mana, amount, "player.mana", "Mana restore") : null, "mana");
        }

        public void RestoreToMaximum()
        {
            if (HasMana)
            {
                resources.SetCurrent(ResourceIds.Mana, resources.GetMaximum(ResourceIds.Mana), "player.mana", "Restore to maximum", restoration: true);
            }
        }

        public bool TryRestoreForPersistence(float restoredMana, out string failureReason)
        {
            failureReason = string.Empty;
            if (!HasMana)
            {
                failureReason = "Player Mana resource is not configured.";
                return false;
            }

            if (float.IsNaN(restoredMana) || float.IsInfinity(restoredMana) || restoredMana < resources.GetMinimum(ResourceIds.Mana))
            {
                failureReason = $"Mana value {restoredMana} is invalid for save restoration.";
                return false;
            }

            resources.SetCurrent(ResourceIds.Mana, Mathf.Clamp(restoredMana, resources.GetMinimum(ResourceIds.Mana), MaximumMana), "player.mana", "Persistence restore", restoration: true);
            return true;
        }

        public void RefreshResourceRuntime() { ResolveResources(); Subscribe(); Publish(); }
        private bool ResolveResources() { resources ??= GetComponent<CharacterResourceCollection>(); return resources != null; }

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

        private void OnChanged(CharacterResourceCollection collection, ResourceChangeResult result) { if (result.Request.ResourceId == ResourceIds.Mana) Publish(); }
        private void OnMaximumChanged(CharacterResourceCollection collection, ResourceSnapshot snapshot, float oldMaximum, bool restoring) { if (snapshot.ResourceId == ResourceIds.Mana) Publish(); }
        private void OnRestored(CharacterResourceCollection collection, bool restoring) => Publish();
        private void Publish() => ManaChanged?.Invoke(CurrentMana, MaximumMana);

        private static VitalChangeResult ToVitalChangeResult(ResourceChangeResult result, string resourceName)
        {
            if (result == null) return VitalChangeResult.Failure(0f, $"Unable to change {resourceName}; its resource is not configured.");
            return result.Succeeded ? VitalChangeResult.Success(result.RequestedAmount, result.AppliedAmount, result.Message) : VitalChangeResult.Failure(result.RequestedAmount, result.Message);
        }
    }
}
