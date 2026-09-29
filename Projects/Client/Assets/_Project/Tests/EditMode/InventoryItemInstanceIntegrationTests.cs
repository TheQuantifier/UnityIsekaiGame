using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Tests
{
    public sealed class InventoryItemInstanceIntegrationTests
    {
        private const string SwordInstanceId = "11111111-1111-4111-8111-111111111111";

        [Test]
        public void AddItemOrInstances_CreatesSeparatePersistentEntriesForAlwaysInstancedItems()
        {
            ScriptableObject sword = CreateItem("item.prototype-sword", "Prototype Sword", ItemInstanceMode.AlwaysInstanced, false, 1, true);
            Component inventory = CreateInventory(4);

            object result = Invoke(inventory, "AddItemOrInstances", sword, 2);

            Assert.That(Get<bool>(result, "AddedAll"), Is.True);
            Assert.That(Get<bool>(GetSlot(inventory, 0), "IsStateful"), Is.True);
            Assert.That(Get<bool>(GetSlot(inventory, 1), "IsStateful"), Is.True);
            Assert.That(Get<string>(GetSlot(inventory, 0), "ItemInstanceId"), Is.Not.EqualTo(Get<string>(GetSlot(inventory, 1), "ItemInstanceId")));
            Assert.That(ItemInstanceId.IsValid(Get<string>(GetSlot(inventory, 0), "ItemInstanceId")), Is.True);

            UnityEngine.Object.DestroyImmediate(inventory.gameObject);
        }

        [Test]
        public void EquipAndUnequip_PreservesItemInstanceIdentity()
        {
            ScriptableObject sword = CreateItem("item.prototype-sword", "Prototype Sword", ItemInstanceMode.AlwaysInstanced, false, 1, true);
            Component inventory = CreateInventory(4);
            Component equipment = inventory.gameObject.AddComponent(RequiredType("UnityIsekaiGame.Equipment.PlayerEquipment"));
            AssignObject(equipment, "inventory", inventory);

            Invoke(inventory, "AddExistingItemIdentity", sword, SwordInstanceId, 1);

            object equipResult = Invoke(equipment, "EquipFromInventorySlot", 0);
            object mainHand = Invoke(equipment, "GetSlot", MainHandValue());

            Assert.That(Get<bool>(equipResult, "Succeeded"), Is.True);
            Assert.That(Get<string>(mainHand, "ItemInstanceId"), Is.EqualTo(SwordInstanceId));
            Assert.That(Get<string>(GetSlot(inventory, 0), "ItemInstanceId"), Is.EqualTo(SwordInstanceId));

            object unequipResult = Invoke(equipment, "Unequip", MainHandValue());

            Assert.That(Get<bool>(unequipResult, "Succeeded"), Is.True);
            Assert.That(Get<string>(GetSlot(inventory, 0), "ItemInstanceId"), Is.EqualTo(SwordInstanceId));
            Assert.That(Get<bool>(mainHand, "IsEmpty"), Is.True);

            UnityEngine.Object.DestroyImmediate(inventory.gameObject);
        }

        [Test]
        public void ReplacingEquipmentKeepsBothInstancesInCanonicalInventory()
        {
            ScriptableObject sword = CreateItem("item.prototype-sword", "Prototype Sword", ItemInstanceMode.AlwaysInstanced, false, 1, true);
            Component inventory = CreateInventory(4);
            Component equipment = inventory.gameObject.AddComponent(RequiredType("UnityIsekaiGame.Equipment.PlayerEquipment"));
            AssignObject(equipment, "inventory", inventory);
            string replacementId = "33333333-3333-4333-8333-333333333333";

            Invoke(inventory, "AddExistingItemIdentity", sword, SwordInstanceId, 1);
            Invoke(inventory, "AddExistingItemIdentity", sword, replacementId, 1);
            Assert.That(Get<bool>(Invoke(equipment, "EquipFromInventorySlot", 0), "Succeeded"), Is.True);
            Assert.That(Get<bool>(Invoke(equipment, "EquipFromInventorySlot", 1), "Succeeded"), Is.True);

            object mainHand = Invoke(equipment, "GetSlot", MainHandValue());
            Assert.That(Get<string>(mainHand, "ItemInstanceId"), Is.EqualTo(replacementId));
            Assert.That(Get<string>(GetSlot(inventory, 0), "ItemInstanceId"), Is.EqualTo(SwordInstanceId));
            Assert.That(Get<string>(GetSlot(inventory, 1), "ItemInstanceId"), Is.EqualTo(replacementId));

            UnityEngine.Object.DestroyImmediate(inventory.gameObject);
        }

        [Test]
        public void RemovingEquippedInventoryInstanceClearsDanglingEquipmentReference()
        {
            ScriptableObject sword = CreateItem("item.prototype-sword", "Prototype Sword", ItemInstanceMode.AlwaysInstanced, false, 1, true);
            Component inventory = CreateInventory(2);
            Component equipment = inventory.gameObject.AddComponent(RequiredType("UnityIsekaiGame.Equipment.PlayerEquipment"));
            AssignObject(equipment, "inventory", inventory);

            Invoke(inventory, "AddExistingItemIdentity", sword, SwordInstanceId, 1);
            Assert.That(Get<bool>(Invoke(equipment, "EquipFromInventorySlot", 0), "Succeeded"), Is.True);
            Assert.That((bool)Invoke(inventory, "RemoveItemAt", 0, 1), Is.True);

            object mainHand = Invoke(equipment, "GetSlot", MainHandValue());
            Assert.That(Get<bool>(mainHand, "IsEmpty"), Is.True);

            UnityEngine.Object.DestroyImmediate(inventory.gameObject);
        }

        [Test]
        public void AddingMultipleInstancedItemsPublishesOneCoherentInventoryChange()
        {
            ItemDefinition sword = (ItemDefinition)CreateItem("item.prototype-sword", "Prototype Sword", ItemInstanceMode.AlwaysInstanced, false, 1, true);
            PlayerInventory inventory = (PlayerInventory)CreateInventory(4);
            int inventoryChanges = 0;
            int addedEvents = 0;
            int reportedQuantity = 0;
            inventory.InventoryChanged += () => inventoryChanges++;
            inventory.ItemAdded += (_, quantity) =>
            {
                addedEvents++;
                reportedQuantity += quantity;
            };

            InventoryAddResult result = inventory.AddItemOrInstances(sword, 3);

            Assert.That(result.AddedAll, Is.True);
            Assert.That(inventoryChanges, Is.EqualTo(1));
            Assert.That(addedEvents, Is.EqualTo(1));
            Assert.That(reportedQuantity, Is.EqualTo(3));
            Assert.That(inventory.DevelopmentOccupiedSlotCount(), Is.EqualTo(3));
            UnityEngine.Object.DestroyImmediate(inventory.gameObject);
        }

        [Test]
        public void IdentityExtractionRejectsQuantityStacksWithoutMutatingThem()
        {
            ItemDefinition potion = (ItemDefinition)CreateItem("item.health-potion", "Health Potion", ItemInstanceMode.DefinitionOnly, true, 10);
            PlayerInventory inventory = (PlayerInventory)CreateInventory(2);
            Assert.That(inventory.AddItem(potion, 3).AddedAll, Is.True);

            bool extracted = inventory.TryExtractSlotIdentity(0, out _, out _, out string failure);

            Assert.That(extracted, Is.False);
            StringAssert.Contains("quantity-aware", failure);
            Assert.That(inventory.GetSlot(0).Quantity, Is.EqualTo(3));
            UnityEngine.Object.DestroyImmediate(inventory.gameObject);
        }

        [Test]
        public void EquipmentRejectsAmbiguousStackedItems()
        {
            ScriptableObject stackedEquipment = CreateItem("item.invalid-stack-equipment", "Stacked Equipment", ItemInstanceMode.DefinitionOnly, true, 10, true);
            Component inventory = CreateInventory(2);
            Component equipment = inventory.gameObject.AddComponent(RequiredType("UnityIsekaiGame.Equipment.PlayerEquipment"));
            AssignObject(equipment, "inventory", inventory);
            Invoke(inventory, "AddItem", stackedEquipment, 2);

            object result = Invoke(equipment, "EquipFromInventorySlot", 0);

            Assert.That(Get<bool>(result, "Succeeded"), Is.False);
            Assert.That(Get<string>(result, "Message"), Does.Contain("stack"));
            Assert.That(Get<bool>(Invoke(equipment, "GetSlot", MainHandValue()), "IsEmpty"), Is.True);
            UnityEngine.Object.DestroyImmediate(inventory.gameObject);
        }

        [Test]
        public void TrackedStacksPreserveIdentityQuantityAndModeAcrossSaveRestore()
        {
            ItemDefinition material = (ItemDefinition)CreateItem("item.tracked-material", "Tracked Material", ItemInstanceMode.AlwaysInstanced, true, 10);
            PlayerInventory inventory = (PlayerInventory)CreateInventory(2);
            string stackId = "44444444-4444-4444-8444-444444444444";
            Assert.That(inventory.AddExistingItemIdentity(material, stackId, 3).Succeeded, Is.True);
            Assert.That(inventory.GetSlot(0).IsStateful, Is.True);
            Assert.That(inventory.RemoveItemAt(0, 1), Is.True);
            Assert.That(inventory.GetSlot(0).Quantity, Is.EqualTo(2));

            InventorySaveData save = inventory.CreateSaveData();
            Assert.That(save.entries[0].mode, Is.EqualTo(InventoryEntrySaveMode.StatefulInstance));
            Assert.That(save.entries[0].quantity, Is.EqualTo(2));
            PlayerInventory restored = (PlayerInventory)CreateInventory(1);
            InventoryRestoreResult result = restored.TryRestoreFromSaveData(save, new DefinitionRegistry(new IGameDefinition[] { material }));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(restored.GetSlot(0).IsStateful, Is.True);
            Assert.That(restored.GetSlot(0).ItemInstanceId, Is.EqualTo(stackId));
            Assert.That(restored.GetSlot(0).Quantity, Is.EqualTo(2));
            UnityEngine.Object.DestroyImmediate(inventory.gameObject);
            UnityEngine.Object.DestroyImmediate(restored.gameObject);
        }

        [Test]
        public void InventorySaveRestore_RestoresStacksAndStatefulInstances()
        {
            ScriptableObject potion = CreateItem("item.health-potion", "Health Potion", ItemInstanceMode.DefinitionOnly, true, 10);
            ScriptableObject sword = CreateItem("item.prototype-sword", "Prototype Sword", ItemInstanceMode.AlwaysInstanced, false, 1, true);
            Component inventory = CreateInventory(4);
            Invoke(inventory, "AddItem", potion, 3);
            Invoke(inventory, "AddExistingItemIdentity", sword, SwordInstanceId, 1);
            object saveData = Invoke(inventory, "CreateSaveData");
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { (IGameDefinition)potion, (IGameDefinition)sword });

            Component restored = CreateInventory(1);
            object restoreResult = Invoke(restored, "TryRestoreFromSaveData", saveData, registry);

            Assert.That(Get<bool>(restoreResult, "Succeeded"), Is.True);
            Assert.That(Get<int>(restored, "SlotCapacity"), Is.EqualTo(4));
            Assert.That(Get<object>(GetSlot(restored, 0), "Item"), Is.SameAs(potion));
            Assert.That(Get<int>(GetSlot(restored, 0), "Quantity"), Is.EqualTo(3));
            Assert.That(Get<string>(GetSlot(restored, 1), "ItemInstanceId"), Is.EqualTo(SwordInstanceId));

            UnityEngine.Object.DestroyImmediate(inventory.gameObject);
            UnityEngine.Object.DestroyImmediate(restored.gameObject);
        }

        [Test]
        public void InventoryRestore_FailureDoesNotChangeExistingSlots()
        {
            ScriptableObject potion = CreateItem("item.health-potion", "Health Potion", ItemInstanceMode.DefinitionOnly, true, 10);
            Component inventory = CreateInventory(2);
            Invoke(inventory, "AddItem", potion, 2);
            object badSave = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Inventory.InventorySaveData"));
            SetField(badSave, "slotCapacity", 2);
            IList entries = (IList)badSave.GetType().GetField("entries").GetValue(badSave);
            object badEntry = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Inventory.InventoryEntrySaveData"));
            SetField(badEntry, "mode", Enum.Parse(RequiredType("UnityIsekaiGame.Inventory.InventoryEntrySaveMode"), "DefinitionStack"));
            SetField(badEntry, "definitionId", "item.missing");
            SetField(badEntry, "quantity", 1);
            entries.Add(badEntry);

            object result = Invoke(inventory, "TryRestoreFromSaveData", badSave, new DefinitionRegistry(new IGameDefinition[] { (IGameDefinition)potion }));

            Assert.That(Get<bool>(result, "Succeeded"), Is.False);
            Assert.That(Get<object>(GetSlot(inventory, 0), "Item"), Is.SameAs(potion));
            Assert.That(Get<int>(GetSlot(inventory, 0), "Quantity"), Is.EqualTo(2));

            UnityEngine.Object.DestroyImmediate(inventory.gameObject);
        }

        private static Component CreateInventory(int slotCapacity)
        {
            GameObject gameObject = new GameObject("Inventory Test");
            Component inventory = gameObject.AddComponent(RequiredType("UnityIsekaiGame.Inventory.PlayerInventory"));
            AssignInt(inventory, "slotCapacity", slotCapacity);
            return inventory;
        }

        private static ScriptableObject CreateItem(
            string id,
            string displayName,
            ItemInstanceMode instanceMode,
            bool stackable,
            int maximumStackSize,
            bool equippable = false)
        {
            Type itemDefinitionType = RequiredType("UnityIsekaiGame.Inventory.ItemDefinition");
            ScriptableObject item = ScriptableObject.CreateInstance(itemDefinitionType);
            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.FindProperty("itemId").stringValue = id;
            serializedItem.FindProperty("displayName").stringValue = displayName;
            serializedItem.FindProperty("instanceMode").enumValueIndex = (int)instanceMode;
            serializedItem.FindProperty("stackable").boolValue = stackable;
            serializedItem.FindProperty("maximumStackSize").intValue = maximumStackSize;

            SerializedProperty equipment = serializedItem.FindProperty("equipment");
            equipment.FindPropertyRelative("equippable").boolValue = equippable;
            equipment.FindPropertyRelative("slotType").enumValueIndex = Convert.ToInt32(MainHandValue());

            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static object GetSlot(Component inventory, int slotIndex)
        {
            IReadOnlyList<object> slots = ReadSlots(inventory);
            return slots[slotIndex];
        }

        private static IReadOnlyList<object> ReadSlots(Component inventory)
        {
            List<object> slots = new List<object>();
            foreach (object slot in (IEnumerable)Get<object>(inventory, "Slots"))
            {
                slots.Add(slot);
            }

            return slots;
        }

        private static object MainHandValue()
        {
            return Enum.Parse(RequiredType("UnityIsekaiGame.Equipment.EquipmentSlotType"), "MainHand");
        }

        private static Type RequiredType(string fullName)
        {
            Type type = TestTypeResolver.RequiredType(fullName);
            Assert.That(type, Is.Not.Null, $"Expected runtime type {fullName} to exist in loaded project assemblies.");
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            return target.GetType().GetMethod(methodName).Invoke(target, args);
        }

        private static T Get<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static void AssignInt(UnityEngine.Object target, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName).SetValue(target, value);
        }
    }
}
