using System;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Gameplay
{
    /// <summary>Player-facing Health view. CharacterResourceCollection is the only mutable owner.</summary>
    [RequireComponent(typeof(CharacterResourceCollection))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private CharacterResourceCollection resources;

        private bool resourceEventsSubscribed;
        private bool defeatPublished;

        public int CurrentHealth => Mathf.RoundToInt(GetCurrent());
        public int MaximumHealth => Mathf.RoundToInt(GetMaximum());
        public bool IsAtMaximum => GetCurrent() >= GetMaximum() - CharacterResourceCollection.Epsilon;
        public bool IsDefeated => HasHealth && GetCurrent() <= GetMinimum() + CharacterResourceCollection.Epsilon;
        public event Action<int, int> HealthChanged;
        public event Action Defeated;

        private bool HasHealth => ResolveResources() && resources.HasResource(ResourceIds.Health);

        private void Awake()
        {
            input ??= GetComponent<PlayerInputReader>();
            ResolveResources();
        }

        private void OnEnable()
        {
            SubscribeResourceEvents();
            PublishHealthChanged();
        }

        private void OnDisable() => UnsubscribeResourceEvents();

        public int Damage(int amount)
        {
            if (amount <= 0 || !HasHealth) return 0;
            ResourceChangeResult result = resources.ApplyDamage(ResourceIds.Health, amount, "player.health", "Damage");
            return result != null && result.Succeeded ? Mathf.RoundToInt(result.AppliedAmount) : 0;
        }

        public DamageResult ApplyDamage(in DamageInfo damageInfo)
        {
            return SceneCombatDamageBridge.ApplyDamage(gameObject, in damageInfo, "player-health.damage", "Player damage");
        }

        public int Heal(int amount)
        {
            if (amount <= 0 || !HasHealth) return 0;
            ResourceChangeResult result = resources.ApplyHealing(ResourceIds.Health, amount, "player.health", "Heal");
            return result != null && result.Succeeded ? Mathf.RoundToInt(result.AppliedAmount) : 0;
        }

        public void ResetToMaximum()
        {
            if (!HasHealth) return;
            defeatPublished = false;
            input?.SetDefeatedInputBlocked(false);
            resources.SetCurrent(ResourceIds.Health, resources.GetMaximum(ResourceIds.Health), "player.health", "Reset to maximum", restoration: true);
        }

        public bool TryRestoreForPersistence(int restoredHealth, out string failureReason)
        {
            failureReason = string.Empty;
            if (!HasHealth)
            {
                failureReason = "Player Health resource is not configured.";
                return false;
            }

            if (restoredHealth <= GetMinimum())
            {
                failureReason = "Defeated player health is not valid for prototype save restoration.";
                return false;
            }

            defeatPublished = false;
            input?.SetDefeatedInputBlocked(false);
            resources.SetCurrent(ResourceIds.Health, Mathf.Clamp(restoredHealth, GetMinimum(), GetMaximum()), "player.health", "Persistence restore", restoration: true);
            return true;
        }

        public void RefreshResourceRuntime()
        {
            ResolveResources();
            SubscribeResourceEvents();
            PublishHealthChanged();
        }

        private bool ResolveResources()
        {
            resources ??= GetComponent<CharacterResourceCollection>();
            return resources != null;
        }

        private float GetCurrent() => HasHealth ? resources.GetCurrent(ResourceIds.Health) : 0f;
        private float GetMaximum() => HasHealth ? resources.GetMaximum(ResourceIds.Health) : 0f;
        private float GetMinimum() => HasHealth ? resources.GetMinimum(ResourceIds.Health) : 0f;

        private void SubscribeResourceEvents()
        {
            if (resourceEventsSubscribed || !ResolveResources() || !isActiveAndEnabled) return;
            resources.ResourceChanged += OnResourceChanged;
            resources.ResourceMaximumChanged += OnResourceMaximumChanged;
            resources.ResourcesRestored += OnResourcesRestored;
            resourceEventsSubscribed = true;
        }

        private void UnsubscribeResourceEvents()
        {
            if (!resourceEventsSubscribed || resources == null) return;
            resources.ResourceChanged -= OnResourceChanged;
            resources.ResourceMaximumChanged -= OnResourceMaximumChanged;
            resources.ResourcesRestored -= OnResourcesRestored;
            resourceEventsSubscribed = false;
        }

        private void OnResourceChanged(CharacterResourceCollection collection, ResourceChangeResult result)
        {
            if (!string.Equals(result.Request.ResourceId, ResourceIds.Health, StringComparison.Ordinal)) return;
            PublishHealthChanged();
            EvaluateDefeat();
        }

        private void OnResourceMaximumChanged(CharacterResourceCollection collection, ResourceSnapshot snapshot, float oldMaximum, bool restoring)
        {
            if (!string.Equals(snapshot.ResourceId, ResourceIds.Health, StringComparison.Ordinal)) return;
            PublishHealthChanged();
            EvaluateDefeat();
        }

        private void OnResourcesRestored(CharacterResourceCollection collection, bool restoring)
        {
            defeatPublished = IsDefeated;
            input?.SetDefeatedInputBlocked(IsDefeated);
            PublishHealthChanged();
        }

        private void PublishHealthChanged() => HealthChanged?.Invoke(CurrentHealth, MaximumHealth);

        private void EvaluateDefeat()
        {
            if (!IsDefeated)
            {
                defeatPublished = false;
                return;
            }

            if (defeatPublished) return;
            defeatPublished = true;
            input?.SetDefeatedInputBlocked(true);
            Defeated?.Invoke();
            Debug.Log("Player defeated. Prototype gameplay input is blocked.");
            PrototypeHudMessageBus.Show("Defeated - Press R to reset");
        }
    }
}
