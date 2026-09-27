using System;
using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat
{
    /// <summary>Enemy-facing Health view. CharacterResourceCollection is the only mutable owner.</summary>
    [RequireComponent(typeof(CharacterResourceCollection))]
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField] private CharacterResourceCollection resources;
        [SerializeField] private string questObjectiveTargetId = "encounter.prototype.dynamic-bounty-target";
        private bool subscribed;
        private bool defeatPublished;

        public float CurrentHealth => HasHealth ? resources.GetCurrent(ResourceIds.Health) : 0f;
        public float MaximumHealth => HasHealth ? resources.GetMaximum(ResourceIds.Health) : 0f;
        public bool IsDefeated => HasHealth && CurrentHealth <= resources.GetMinimum(ResourceIds.Health) + CharacterResourceCollection.Epsilon;
        public event Action<float, float> HealthChanged;
        public event Action Defeated;
        private bool HasHealth => ResolveResources() && resources.HasResource(ResourceIds.Health);

        private void Awake() => ResolveResources();
        private void OnEnable() { Subscribe(); Publish(); }
        private void OnDisable() => Unsubscribe();

        public DamageResult ApplyDamage(in DamageInfo damageInfo)
        {
            return SceneCombatDamageBridge.ApplyDamage(gameObject, in damageInfo, "enemy-health.damage", "Enemy damage");
        }

        public void ResetToMaximum()
        {
            if (!HasHealth) return;
            defeatPublished = false;
            resources.SetCurrent(ResourceIds.Health, resources.GetMaximum(ResourceIds.Health), "enemy.health", "Reset to maximum", restoration: true);
        }

        public void RefreshResourceRuntime() { ResolveResources(); Subscribe(); Publish(); EvaluateDefeat(); }
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

        private void OnChanged(CharacterResourceCollection collection, ResourceChangeResult result)
        {
            if (result.Request.ResourceId != ResourceIds.Health) return;
            Publish();
            EvaluateDefeat();
        }

        private void OnMaximumChanged(CharacterResourceCollection collection, ResourceSnapshot snapshot, float oldMaximum, bool restoring)
        {
            if (snapshot.ResourceId != ResourceIds.Health) return;
            Publish();
            EvaluateDefeat();
        }

        private void OnRestored(CharacterResourceCollection collection, bool restoring)
        {
            defeatPublished = IsDefeated;
            Publish();
        }

        private void Publish() => HealthChanged?.Invoke(CurrentHealth, MaximumHealth);

        private void EvaluateDefeat()
        {
            if (!IsDefeated)
            {
                defeatPublished = false;
                return;
            }

            if (defeatPublished) return;
            defeatPublished = true;
            Defeated?.Invoke();
            if (!string.IsNullOrWhiteSpace(questObjectiveTargetId))
            {
                PrototypePersistenceServiceBehaviour services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
                string actorId = services?.PlayerPersonId ?? "person.prototype.player";
                double worldTime = services?.PlayTime?.CumulativeSeconds ?? Time.unscaledTimeAsDouble;
                QuestObjectiveSignalBus.Report(QuestObjectiveCategory.DefeatTarget, questObjectiveTargetId.Trim(), actorId, worldTime, sourceEventId: $"enemy-defeat.{name}.{Time.frameCount}");
                QuestObjectiveSignalBus.Report(QuestObjectiveCategory.DefeatCount, "enemy-family.prototype.monster", actorId, worldTime, sourceEventId: $"enemy-defeat-count.{name}.{Time.frameCount}");
            }
            PrototypeHudMessageBus.Show($"{name} defeated");
        }
    }
}
