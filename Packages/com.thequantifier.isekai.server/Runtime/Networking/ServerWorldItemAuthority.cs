using System;
using Unity.Netcode;
using UnityEngine;
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
            GameObject instance = null;
            try
            {
                instance = Instantiate(pickupPrefab, spawnPosition, Quaternion.identity);
                instance.name = $"Network Pickup ({item.DisplayName})";
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
