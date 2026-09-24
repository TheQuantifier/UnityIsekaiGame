using UnityEngine;
using UnityIsekaiGame.Inventory.Disassembly;

namespace UnityIsekaiGame.Inventory
{
    public static class WorldItemPickupFactory
    {
        public static WorldItemPickup Create(
            ItemDefinition item,
            int quantity,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            Material fallbackMaterial = null)
        {
            if (item == null)
            {
                return null;
            }

            WorldItemPickup pickup = item.WorldPickupPrefab == null
                ? CreateFallbackPickup(item, position, rotation, fallbackMaterial)
                : Object.Instantiate(item.WorldPickupPrefab, position, rotation);

            pickup.name = $"Dropped {item.DisplayName}";
            if (parent != null)
            {
                pickup.transform.SetParent(parent, worldPositionStays: true);
            }

            pickup.Configure(item, quantity);
            return pickup;
        }

        public static WorldItemPickup CreateTrackedDrop(
            ItemDefinition item,
            int quantity,
            string itemInstanceId,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            Material fallbackMaterial = null,
            bool enableNaturalDecomposition = true)
        {
            WorldItemPickup pickup = Create(item, quantity, position, rotation, parent, fallbackMaterial);
            if (pickup == null) return null;
            pickup.ConfigureTrackedInstance(itemInstanceId);
            if (enableNaturalDecomposition)
            {
                WorldItemDecomposition decomposition = pickup.GetComponent<WorldItemDecomposition>() ?? pickup.gameObject.AddComponent<WorldItemDecomposition>();
                decomposition.Configure(itemInstanceId);
            }
            return pickup;
        }

        private static WorldItemPickup CreateFallbackPickup(ItemDefinition item, Vector3 position, Quaternion rotation, Material fallbackMaterial)
        {
            GameObject pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickupObject.transform.SetPositionAndRotation(position, rotation);
            pickupObject.transform.localScale = Vector3.one * 0.35f;

            if (fallbackMaterial != null && pickupObject.TryGetComponent(out MeshRenderer renderer))
            {
                renderer.sharedMaterial = fallbackMaterial;
            }

            return pickupObject.AddComponent<WorldItemPickup>();
        }
    }
}
