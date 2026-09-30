using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Networking.Client;

namespace UnityIsekaiGame.Tests
{
    public sealed class WorldItemPickupVisualFactoryTests
    {
        private const string CatalogPath =
            "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset";

        private static readonly string[] AuthoredPickupItemIds =
        {
            "item.health-potion",
            "item.mana-potion",
            "item.stamina-potion",
            "item.wood-log",
            "item.prototype-iron-ore",
            "item.prototype-arrow",
            "item.prototype-bow",
            "item.prototype-sword"
        };

        [Test]
        public void AuthoredPickupItems_CreatePresentationOnlyNetworkVisuals()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            DefinitionRegistry definitions = catalog.CreateRegistry();
            var created = new List<GameObject>();

            try
            {
                foreach (string itemId in AuthoredPickupItemIds)
                {
                    Assert.That(definitions.TryGet(itemId, out ItemDefinition item), Is.True, itemId);
                    Assert.That(item.WorldPickupPrefab, Is.Not.Null, itemId);

                    var authoritativePickup = new GameObject($"Authoritative {item.DisplayName}");
                    created.Add(authoritativePickup);
                    authoritativePickup.transform.localScale = Vector3.one * 0.35f;

                    Assert.That(
                        WorldItemPickupVisualFactory.TryCreate(item, authoritativePickup.transform, out GameObject visual),
                        Is.True,
                        itemId);
                    Assert.That(visual, Is.Not.Null, itemId);
                    Assert.That(visual.GetComponentsInChildren<Renderer>(true), Is.Not.Empty, itemId);
                    Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, itemId);
                    Assert.That(visual.GetComponentsInChildren<WorldItemPickup>(true), Is.Empty, itemId);
                    Assert.That(visual.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty, itemId);
                }
            }
            finally
            {
                for (int i = 0; i < created.Count; i++)
                {
                    if (created[i] != null) Object.DestroyImmediate(created[i]);
                }
            }
        }
    }
}
