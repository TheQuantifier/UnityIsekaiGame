using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class PlayerInventoryEquipmentPersistenceTests
    {
        private const string SwordInstanceId = "22222222-2222-4222-8222-222222222222";
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "UnityIsekaiGameInventoryEquipmentPersistenceTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(testRoot) && Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, true);
            }
        }

        [Test]
        public void ParticipantDeclaresPlayerScopeOwnerAndStableIdentity()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            object participant = CreateParticipant(fixture.Inventory, fixture.Equipment, fixture.Registry);

            Assert.That(Get<string>(participant, "ParticipantKey"), Is.EqualTo("player.inventory-equipment"));
            Assert.That(Get<int>(participant, "ParticipantSchemaVersion"), Is.EqualTo(2));
            Assert.That(Get<bool>(participant, "IsRequired"), Is.True);
            Assert.That(Get<PersistenceScope>(participant, "Scope"), Is.EqualTo(PersistenceScope.Player));
            Assert.That(Get<string>(participant, "OwnerId"), Is.EqualTo(PersistenceService.LocalPlayerId));
        }

        [Test]
        public void SaveLoadRoundTripRestoresInventoryEquipmentAndInstanceIdentity()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            Invoke(fixture.Inventory, "AddItem", fixture.Potion, 4);
            Invoke(fixture.Inventory, "AddExistingItemIdentity", fixture.Sword, SwordInstanceId, 1);
            Assert.That(Get<bool>(Invoke(fixture.Equipment, "EquipFromInventorySlot", 1), "Succeeded"), Is.True);

            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            Invoke(fixture.Inventory, "RemoveItemAt", 0, 2);
            Assert.That(Get<bool>(Invoke(fixture.Equipment, "Unequip", MainHandValue()), "Succeeded"), Is.True);

            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.True, load.Message);
            Assert.That(Get<int>(GetSlot(fixture.Inventory, 0), "Quantity"), Is.EqualTo(4));
            Assert.That(Get<bool>(GetSlot(fixture.Inventory, 1), "IsEmpty"), Is.True);
            object mainHand = Invoke(fixture.Equipment, "GetSlot", MainHandValue());
            Assert.That(Get<string>(mainHand, "ItemInstanceId"), Is.EqualTo(SwordInstanceId));
        }

        [Test]
        public void PrepareRejectsSameInstanceInInventoryAndEquipmentWithoutMutation()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            Invoke(fixture.Inventory, "AddItem", fixture.Potion, 2);
            object participant = CreateParticipant(fixture.Inventory, fixture.Equipment, fixture.Registry);
            object payload = CreatePayload(2);
            AddInventoryStatefulEntry(payload, fixture.Sword, SwordInstanceId);
            AddEquipmentStatefulEntry(payload, fixture.Sword, SwordInstanceId);

            object result = Invoke(participant, "PreparePayload", JsonUtility.ToJson(payload), 2);

            Assert.That(Get<bool>(result, "Succeeded"), Is.False);
            Assert.That(Get<string>(result, "Message"), Does.Contain("both inventory and equipment"));
            Assert.That(Get<int>(GetSlot(fixture.Inventory, 0), "Quantity"), Is.EqualTo(2));
        }

        [Test]
        public void PrepareRejectsUnsupportedSchemaVersion()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            object participant = CreateParticipant(fixture.Inventory, fixture.Equipment, fixture.Registry);
            object payload = CreatePayload(3);

            object futureResult = Invoke(participant, "PreparePayload", JsonUtility.ToJson(payload), 3);
            SetField(payload, "schemaVersion", 1);
            object oldResult = Invoke(participant, "PreparePayload", JsonUtility.ToJson(payload), 1);

            Assert.That(Get<bool>(futureResult, "Succeeded"), Is.False);
            Assert.That(Get<bool>(oldResult, "Succeeded"), Is.False);
        }

        [Test]
        public void LoadRejectsParticipantOwnerMismatchBeforeCommit()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            Invoke(fixture.Inventory, "AddItem", fixture.Potion, 3);
            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            SaveSlotPaths paths = Paths("slot-0001");
            GameSaveEnvelope envelope = JsonUtility.FromJson<GameSaveEnvelope>(File.ReadAllText(paths.PrimaryPath));
            envelope.participants[0].ownerId = "other-player";
            envelope.contentChecksum = PersistenceService.ComputeChecksum(envelope);
            File.WriteAllText(paths.PrimaryPath, JsonUtility.ToJson(envelope, true));

            Invoke(fixture.Inventory, "RemoveItemAt", 0, 1);
            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.False);
            Assert.That(load.Status, Is.EqualTo(PersistenceLoadStatus.ParticipantPrepareFailed));
            Assert.That(Get<int>(GetSlot(fixture.Inventory, 0), "Quantity"), Is.EqualTo(2));
        }

        private PersistenceService CreateService(RuntimeFixture fixture)
        {
            PersistenceService service = new PersistenceService(new PersistencePathProvider(testRoot));
            object participant = CreateParticipant(fixture.Inventory, fixture.Equipment, fixture.Registry);
            Assert.That(service.RegisterParticipant((IPersistenceParticipant)participant, out string failureReason), Is.True, failureReason);
            return service;
        }

        private SaveSlotPaths Paths(string slotId)
        {
            PersistencePathProvider provider = new PersistencePathProvider(testRoot);
            Assert.That(provider.TryGetPaths(slotId, out SaveSlotPaths paths, out string failure), Is.True, failure);
            return paths;
        }

        private static object CreateParticipant(Component inventory, Component equipment, DefinitionRegistry registry)
        {
            Type participantType = RequiredType("UnityIsekaiGame.Persistence.PlayerInventoryEquipmentPersistenceParticipant");
            Func<DefinitionRegistry> registryProvider = () => registry;
            return Activator.CreateInstance(participantType, new object[]
            {
                inventory,
                equipment,
                registryProvider,
                PersistenceService.LocalPlayerId,
                null,
                "test.player.inventory-equipment"
            });
        }

        private static object CreatePayload(int slotCapacity)
        {
            object payload = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Persistence.PlayerInventoryEquipmentSaveData"));
            object inventorySave = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Inventory.InventorySaveData"));
            SetField(inventorySave, "slotCapacity", slotCapacity);
            SetField(payload, "inventory", inventorySave);
            SetField(payload, "equipment", Activator.CreateInstance(RequiredType("UnityIsekaiGame.Equipment.EquipmentSaveData")));
            return payload;
        }

        private static void AddInventoryStatefulEntry(object payload, ScriptableObject item, string instanceId)
        {
            object inventorySave = payload.GetType().GetField("inventory").GetValue(payload);
            IList entries = (IList)inventorySave.GetType().GetField("entries").GetValue(inventorySave);
            object entry = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Inventory.InventoryEntrySaveData"));
            SetField(entry, "mode", Enum.Parse(RequiredType("UnityIsekaiGame.Inventory.InventoryEntrySaveMode"), "StatefulInstance"));
            SetField(entry, "definitionId", ((IGameDefinition)item).Id);
            SetField(entry, "itemInstanceId", instanceId);
            SetField(entry, "quantity", 1);
            entries.Add(entry);
        }

        private static void AddEquipmentStatefulEntry(object payload, ScriptableObject item, string instanceId)
        {
            object equipmentSave = payload.GetType().GetField("equipment").GetValue(payload);
            IList slots = (IList)equipmentSave.GetType().GetField("slots").GetValue(equipmentSave);
            object entry = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Equipment.EquipmentSlotSaveData"));
            SetField(entry, "slotType", MainHandValue());
            SetField(entry, "mode", Enum.Parse(RequiredType("UnityIsekaiGame.Equipment.EquipmentEntrySaveMode"), "StatefulInstance"));
            SetField(entry, "definitionId", ((IGameDefinition)item).Id);
            SetField(entry, "itemInstanceId", instanceId);
            slots.Add(entry);
        }

        private static Component CreateInventory(int slotCapacity)
        {
            GameObject gameObject = new GameObject("Inventory Equipment Persistence Test");
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
            List<object> slots = new List<object>();
            foreach (object slot in (IEnumerable)Get<object>(inventory, "Slots"))
            {
                slots.Add(slot);
            }

            return slots[slotIndex];
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

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName).SetValue(target, value);
        }

        private sealed class RuntimeFixture : IDisposable
        {
            public Component Inventory { get; private set; }
            public Component Equipment { get; private set; }
            public ScriptableObject Potion { get; private set; }
            public ScriptableObject Sword { get; private set; }
            public DefinitionRegistry Registry { get; private set; }

            public static RuntimeFixture Create()
            {
                RuntimeFixture fixture = new RuntimeFixture
                {
                    Potion = CreateItem("item.health-potion", "Health Potion", ItemInstanceMode.DefinitionOnly, true, 10),
                    Sword = CreateItem("item.prototype-sword", "Prototype Sword", ItemInstanceMode.AlwaysInstanced, false, 1, true),
                    Inventory = CreateInventory(4)
                };
                fixture.Equipment = fixture.Inventory.gameObject.AddComponent(RequiredType("UnityIsekaiGame.Equipment.PlayerEquipment"));
                AssignObject(fixture.Equipment, "inventory", fixture.Inventory);
                fixture.Registry = new DefinitionRegistry(new IGameDefinition[] { (IGameDefinition)fixture.Potion, (IGameDefinition)fixture.Sword });
                return fixture;
            }

            public void Dispose()
            {
                if (Inventory != null)
                {
                    UnityEngine.Object.DestroyImmediate(Inventory.gameObject);
                }

                if (Potion != null)
                {
                    UnityEngine.Object.DestroyImmediate(Potion);
                }

                if (Sword != null)
                {
                    UnityEngine.Object.DestroyImmediate(Sword);
                }
            }
        }

        private static void AssignObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
