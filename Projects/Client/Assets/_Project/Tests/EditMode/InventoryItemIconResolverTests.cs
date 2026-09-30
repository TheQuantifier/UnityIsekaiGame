using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.UI.Inventory;

namespace UnityIsekaiGame.Tests
{
    public sealed class InventoryItemIconResolverTests
    {
        [Test]
        public void MissingAuthoredIconGetsStableGeneratedArtwork()
        {
            ItemDefinition item = CreateItem("item.test.iron-sword", "Iron Sword");
            try
            {
                Sprite first = InventoryItemIconResolver.Resolve(item);
                Sprite second = InventoryItemIconResolver.Resolve(item);

                Assert.That(first, Is.Not.Null);
                Assert.That(first.texture, Is.Not.Null);
                Assert.That(first.texture.width, Is.EqualTo(96));
                Assert.That(second, Is.SameAs(first));
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void AuthoredIconAlwaysTakesPriority()
        {
            Texture2D texture = new Texture2D(2, 2);
            Sprite authored = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            ItemDefinition item = CreateItem("item.test.authored", "Authored Item");
            try
            {
                SerializedObject serialized = new SerializedObject(item);
                serialized.FindProperty("icon").objectReferenceValue = authored;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(InventoryItemIconResolver.Resolve(item), Is.SameAs(authored));
            }
            finally
            {
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(authored);
                Object.DestroyImmediate(texture);
            }
        }

        private static ItemDefinition CreateItem(string id, string displayName)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }
    }
}
