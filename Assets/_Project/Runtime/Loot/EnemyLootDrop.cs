using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Loot
{
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyLootDrop : MonoBehaviour
    {
        [SerializeField] private EnemyHealth enemyHealth;
        [SerializeField] private LootTable lootTable;
        [SerializeField] private Transform dropOrigin;
        [SerializeField] private Transform dropParent;
        [SerializeField, Min(0f)] private float spawnRadius = 1.25f;
        [SerializeField, Min(0f)] private float colliderClearance = 0.5f;
        [SerializeField, Min(0f)] private float spawnHeightOffset = 0.25f;
        [SerializeField] private Material pickupMaterial;
        [SerializeField] private string sceneKey = "scene.prototype";
        [SerializeField] private string worldId = PersistenceService.LocalWorldId;
        [SerializeField] private bool assignPersistentWorldEntityIdsToDrops = true;

        private readonly List<GameObject> spawnedLoot = new List<GameObject>();
        private readonly ILootRandom random = new UnityLootRandom();
        private bool rolledForCurrentDefeat;

        private void Awake()
        {
            if (enemyHealth == null)
            {
                enemyHealth = GetComponent<EnemyHealth>();
            }

            if (dropOrigin == null)
            {
                dropOrigin = transform;
            }
        }

        private void OnEnable()
        {
            if (enemyHealth != null)
            {
                enemyHealth.Defeated += HandleDefeated;
            }
        }

        private void OnDisable()
        {
            if (enemyHealth != null)
            {
                enemyHealth.Defeated -= HandleDefeated;
            }
        }

        private void OnValidate()
        {
            spawnRadius = Mathf.Max(0f, spawnRadius);
            colliderClearance = Mathf.Max(0f, colliderClearance);
            spawnHeightOffset = Mathf.Max(0f, spawnHeightOffset);
        }

        public void ResetLootState()
        {
            rolledForCurrentDefeat = false;

            for (int i = spawnedLoot.Count - 1; i >= 0; i--)
            {
                if (spawnedLoot[i] != null)
                {
                    Destroy(spawnedLoot[i]);
                }
            }

            spawnedLoot.Clear();
        }

        private void HandleDefeated()
        {
            if (rolledForCurrentDefeat)
            {
                return;
            }

            rolledForCurrentDefeat = true;

            if (lootTable == null)
            {
                Debug.Log($"{name} has no loot table configured.");
                return;
            }

            List<LootRoll> rolls = lootTable.Roll(random);
            if (rolls.Count == 0)
            {
                Debug.Log($"{name} dropped no loot.");
                PrototypeHudMessageBus.Show($"{name} dropped no loot");
                return;
            }

            List<string> dropMessages = new List<string>();
            for (int i = 0; i < rolls.Count; i++)
            {
                string dropMessage = SpawnPickup(rolls[i], i, rolls.Count);
                if (!string.IsNullOrWhiteSpace(dropMessage))
                {
                    dropMessages.Add(dropMessage);
                }
            }

            if (dropMessages.Count > 0)
            {
                PrototypeHudMessageBus.Show($"{name} dropped {string.Join(", ", dropMessages)}");
            }
        }

        private string SpawnPickup(in LootRoll roll, int index, int totalCount)
        {
            if (roll.Item == null || roll.Quantity <= 0)
            {
                return string.Empty;
            }

            Vector3 spawnPosition = GetSpawnPosition(index, totalCount);
            PrototypePersistenceServiceBehaviour services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
            bool trackForDecomposition = roll.Quantity == 1
                && roll.Item.InstanceMode == UnityIsekaiGame.GameData.ItemInstanceMode.AlwaysInstanced
                && roll.Item.DefaultCompositionTemplate != null
                && !roll.Item.DefaultCompositionTemplate.IsEmpty
                && services != null;
            PrototypeItemDropResult tracked = trackForDecomposition
                ? services.CreateTrackedWorldDrop(roll.Item, spawnPosition, Quaternion.identity, dropParent, pickupMaterial)
                : null;
            WorldItemPickup worldPickup = tracked?.Succeeded == true
                ? tracked.Pickup
                : WorldItemPickupFactory.Create(roll.Item, roll.Quantity, spawnPosition, Quaternion.identity, dropParent, pickupMaterial);
            if (worldPickup == null)
            {
                return string.Empty;
            }

            GameObject pickup = worldPickup.gameObject;
            if (assignPersistentWorldEntityIdsToDrops && pickup.GetComponent<WorldEntityIdentity>() == null)
            {
                WorldEntitySpawnResult identityResult = WorldEntityIdentityFactory.CreateRuntimeIdentity(pickup, sceneKey, worldId, roll.Item.Id);
                if (!identityResult.Succeeded)
                {
                    Debug.LogWarning($"Dropped loot world-entity identity failed: {identityResult.Message}");
                }
            }

            spawnedLoot.Add(pickup);

            string message = $"{name} dropped {roll.Quantity} x {roll.Item.DisplayName}.";
            Debug.Log(message);
            return $"{roll.Quantity} x {roll.Item.DisplayName}";
        }

        private Vector3 GetSpawnPosition(int index, int totalCount)
        {
            Vector3 origin = dropOrigin == null ? transform.position : dropOrigin.position;
            origin.y += spawnHeightOffset;

            float spawnDistance = GetSpawnDistance();
            if (spawnDistance <= 0f || totalCount <= 1)
            {
                return origin + transform.forward * Mathf.Max(0.75f, spawnDistance);
            }

            float angle = (Mathf.PI * 2f * index) / totalCount;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnDistance;
            return origin + offset;
        }

        private float GetSpawnDistance()
        {
            float distance = spawnRadius;
            if (TryGetComponent(out Collider enemyCollider))
            {
                Vector3 extents = enemyCollider.bounds.extents;
                distance = Mathf.Max(distance, Mathf.Max(extents.x, extents.z) + colliderClearance);
            }

            return distance;
        }
    }
}
