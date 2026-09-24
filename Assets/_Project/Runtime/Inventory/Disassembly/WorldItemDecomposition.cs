using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Inventory.Disassembly
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldItemPickup))]
    public sealed class WorldItemDecomposition : MonoBehaviour
    {
        [SerializeField] private string itemInstanceId;
        [SerializeField, Min(0.1f)] private float evaluationIntervalSeconds = 1f;
        private PrototypePersistenceServiceBehaviour services;
        private float nextEvaluationTime;

        public string ItemInstanceId => itemInstanceId ?? string.Empty;
        public WorldItemPickup Pickup => GetComponent<WorldItemPickup>();
        public string WorldEntityId => GetComponent<WorldEntityIdentity>()?.EntityId ?? $"world-item.{ItemInstanceId}";
        public string SceneKey => GetComponent<WorldEntityIdentity>()?.SceneKey ?? gameObject.scene.name;

        public void Configure(string trackedItemInstanceId)
        {
            itemInstanceId = trackedItemInstanceId?.Trim() ?? string.Empty;
            nextEvaluationTime = 0f;
        }

        private void Start()
        {
            EvaluateNow();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextEvaluationTime) return;
            EvaluateNow();
        }

        public bool EvaluateNow()
        {
            nextEvaluationTime = Time.unscaledTime + Mathf.Max(0.1f, evaluationIntervalSeconds);
            if (string.IsNullOrWhiteSpace(ItemInstanceId)) return false;
            services ??= FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
            if (services == null) return false;
            bool decomposed = services.TryAdvanceNaturalDecomposition(this);
            if (decomposed) Destroy(gameObject);
            return decomposed;
        }
    }
}
