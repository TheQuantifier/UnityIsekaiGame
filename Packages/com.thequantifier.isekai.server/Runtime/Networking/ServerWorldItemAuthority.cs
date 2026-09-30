using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Networking.Server
{
    [DisallowMultipleComponent]
    public sealed class ServerWorldItemAuthority : MonoBehaviour
    {
        private NetworkManager networkManager;
        private GameObject pickupPrefab;
        private Func<ulong, ServerPlayerInventoryAuthority> playerInventoryResolver;
        private float maximumPickupDistance = 3.5f;
        private bool configured;
        private readonly List<WorldItemPickup> suppressedScenePickups = new List<WorldItemPickup>();
        private readonly Dictionary<ulong, TokenBucketRateLimiter> collectionRateLimiters =
            new Dictionary<ulong, TokenBucketRateLimiter>();

        public GameObject PickupPrefab => pickupPrefab;
        public float MaximumPickupDistance => maximumPickupDistance;

        public void Configure(
            NetworkManager manager,
            GameObject authoritativePickupPrefab,
            Func<ulong, ServerPlayerInventoryAuthority> inventoryResolver,
            float pickupDistance = 3.5f)
        {
            networkManager = manager ?? throw new ArgumentNullException(nameof(manager));
            pickupPrefab = authoritativePickupPrefab ?? throw new ArgumentNullException(nameof(authoritativePickupPrefab));
            playerInventoryResolver = inventoryResolver ?? throw new ArgumentNullException(nameof(inventoryResolver));
            maximumPickupDistance = Mathf.Max(0.5f, pickupDistance);
            collectionRateLimiters.Clear();

            if (pickupPrefab.GetComponent<NetworkObject>() == null || pickupPrefab.GetComponent<NetworkWorldItemPickup>() == null)
                throw new ArgumentException("The authoritative pickup prefab must contain NetworkObject and NetworkWorldItemPickup.", nameof(authoritativePickupPrefab));
            configured = true;
        }

        public bool TrySpawnDrop(
            ServerPlayerInventoryAuthority owner,
            ItemDefinition item,
            string itemInstanceId,
            int quantity,
            NetworkWorldItemStorageMode storageMode,
            out NetworkWorldItemPickup pickup,
            out string failure)
        {
            pickup = null;
            if (!configured || networkManager == null || !networkManager.IsServer || !networkManager.IsListening)
            {
                failure = "World item authority is not running.";
                return false;
            }

            if (owner == null || item == null)
            {
                failure = "A world drop requires an authoritative owner and item definition.";
                return false;
            }

            NetworkWorldItemState pickupState = new NetworkWorldItemState(
                item.ItemId,
                itemInstanceId,
                item.DisplayName,
                quantity,
                storageMode);
            if (!NetworkWorldItemStateValidator.TryValidate(pickupState, out failure)) return false;

            Vector3 forward = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude <= 0.0001f) forward = Vector3.forward;
            Vector3 spawnPosition = owner.transform.position + forward * 1.25f + Vector3.up * 0.35f;
            return TrySpawnPickupState(pickupState, spawnPosition, Quaternion.identity, out pickup, out failure);
        }

        public bool TrySpawnScenePickups(out int spawnedCount, out string failure)
        {
            spawnedCount = 0;
            failure = string.Empty;
            if (!configured || networkManager == null || !networkManager.IsServer || !networkManager.IsListening)
            {
                failure = "World item authority is not running.";
                return false;
            }

            WorldItemPickup[] sources = FindObjectsByType<WorldItemPickup>(FindObjectsInactive.Exclude);
            var spawned = new List<NetworkWorldItemPickup>();
            var suppressed = new List<WorldItemPickup>();
            foreach (WorldItemPickup source in sources)
            {
                if (source == null || source.Item == null || source.Quantity < 1) continue;
                bool stateful = source.Item.InstanceMode == ItemInstanceMode.AlwaysInstanced ||
                    (source.Item.InstanceMode == ItemInstanceMode.OptionalInstance && !source.Item.Stackable);
                int pickupCount = stateful ? source.Quantity : 1;
                int quantityPerPickup = stateful ? 1 : source.Quantity;
                for (int index = 0; index < pickupCount; index++)
                {
                    string itemInstanceId = index == 0 && !string.IsNullOrWhiteSpace(source.RuntimeItemInstanceId)
                        ? source.RuntimeItemInstanceId
                        : ItemInstanceId.Generate();
                    Vector3 offset = pickupCount <= 1
                        ? Vector3.zero
                        : Quaternion.Euler(0f, index * (360f / pickupCount), 0f) * Vector3.forward * 0.2f;
                    NetworkWorldItemState state = new NetworkWorldItemState(
                        source.Item.ItemId,
                        itemInstanceId,
                        source.Item.DisplayName,
                        quantityPerPickup,
                        stateful ? NetworkWorldItemStorageMode.StatefulInstance : NetworkWorldItemStorageMode.DefinitionStack);
                    if (!TrySpawnPickupState(state, source.transform.position + offset, source.transform.rotation, out NetworkWorldItemPickup pickup, out failure))
                    {
                        foreach (NetworkWorldItemPickup created in spawned) RollBackSpawn(created);
                        foreach (WorldItemPickup hidden in suppressed)
                            if (hidden != null) hidden.gameObject.SetActive(true);
                        spawnedCount = 0;
                        return false;
                    }

                    spawned.Add(pickup);
                    spawnedCount++;
                }

                source.gameObject.SetActive(false);
                suppressed.Add(source);
            }

            suppressedScenePickups.AddRange(suppressed);
            return true;
        }

        public void RestoreScenePickupSources()
        {
            foreach (WorldItemPickup source in suppressedScenePickups)
                if (source != null) source.gameObject.SetActive(true);
            suppressedScenePickups.Clear();
        }

        private bool TrySpawnPickupState(
            NetworkWorldItemState pickupState,
            Vector3 spawnPosition,
            Quaternion spawnRotation,
            out NetworkWorldItemPickup pickup,
            out string failure)
        {
            pickup = null;
            if (!NetworkWorldItemStateValidator.TryValidate(pickupState, out failure)) return false;
            GameObject instance = null;
            try
            {
                instance = Instantiate(pickupPrefab, spawnPosition, spawnRotation);
                instance.name = $"Network Pickup ({pickupState.DisplayName})";
                pickup = instance.GetComponent<NetworkWorldItemPickup>();
                NetworkObject networkObject = instance.GetComponent<NetworkObject>();
                pickup.ConfigureServer(pickupState, TryCollect);
                networkObject.Spawn(true);
                if (!networkObject.IsSpawned) throw new InvalidOperationException("Netcode did not spawn the world pickup.");
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (instance != null) Destroy(instance);
                pickup = null;
                failure = $"The server could not spawn the world pickup: {exception.Message}";
                return false;
            }
        }

        public void RollBackSpawn(NetworkWorldItemPickup pickup)
        {
            if (pickup == null) return;
            NetworkObject networkObject = pickup.NetworkObject;
            if (networkObject != null && networkObject.IsSpawned && pickup.IsServer)
                networkObject.Despawn(true);
            else
                Destroy(pickup.gameObject);
        }

        private WorldItemCollectionResult TryCollect(ulong clientId, NetworkWorldItemPickup pickup)
        {
            if (!configured || pickup == null || !pickup.IsSpawned)
                return WorldItemCollectionResult.Reject("The pickup is no longer available.");
            if (!collectionRateLimiters.TryGetValue(clientId, out TokenBucketRateLimiter limiter))
            {
                limiter = new TokenBucketRateLimiter(5d, 5d);
                collectionRateLimiters[clientId] = limiter;
            }
            if (!limiter.TryConsume(Time.realtimeSinceStartupAsDouble))
                return WorldItemCollectionResult.Reject("Pickup requests are arriving too quickly.");

            ServerPlayerInventoryAuthority inventoryAuthority = playerInventoryResolver(clientId);
            if (inventoryAuthority == null)
                return WorldItemCollectionResult.Reject("The server could not resolve your inventory.");

            float distanceSquared = (inventoryAuthority.transform.position - pickup.transform.position).sqrMagnitude;
            if (distanceSquared > maximumPickupDistance * maximumPickupDistance)
                return WorldItemCollectionResult.Reject("Move closer to pick up this item.");

            return inventoryAuthority.TryCollectWorldPickup(pickup.State, out string message)
                ? WorldItemCollectionResult.Success(message)
                : WorldItemCollectionResult.Reject(message);
        }
    }
}
