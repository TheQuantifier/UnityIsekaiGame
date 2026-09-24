using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Tests
{
    public sealed class PrototypeTestLabTests
    {
        private const int HistoryLimit = 40;
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string PotionPath = "Assets/_Project/Prototype/Content/Items/HealthPotion.asset";
        private const string StatusPath = "Assets/_Project/Prototype/Content/StatusEffects/PrototypeMightStatus.asset";

        [Test]
        public void SelectorsLoadDefinitionsFromPrototypeCatalog()
        {
            object service = CreateService();

            Assert.That(Count(InvokeGeneric(service, "GetDefinitions", RequiredType("UnityIsekaiGame.Inventory.ItemDefinition"))), Is.GreaterThan(0));
            Assert.That(Count(InvokeGeneric(service, "GetDefinitions", RequiredType("UnityIsekaiGame.StatusEffects.StatusEffectDefinition"))), Is.GreaterThan(0));
        }

        [Test]
        public void GrantItemAndClearInventoryRequiresRepeatConfirmation()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            object service = CreateService(fixture);
            UnityEngine.Object potion = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PotionPath);

            object grant = Invoke(service, "GrantItem", potion, 2);
            Assert.That(Get<bool>(grant, "Succeeded"), Is.True, Get<string>(grant, "Message"));
            Assert.That((int)Invoke(fixture.Inventory, "DevelopmentOccupiedSlotCount"), Is.EqualTo(1));

            object firstClear = Invoke(service, "ClearInventory", false);
            Assert.That(Get<bool>(firstClear, "Succeeded"), Is.False);
            Assert.That(Get<string>(firstClear, "Code"), Is.EqualTo("ConfirmationRequired"));
            Assert.That((int)Invoke(fixture.Inventory, "DevelopmentOccupiedSlotCount"), Is.EqualTo(1));

            object secondClear = Invoke(service, "ClearInventory", false);
            Assert.That(Get<bool>(secondClear, "Succeeded"), Is.True, Get<string>(secondClear, "Message"));
            Assert.That((int)Invoke(fixture.Inventory, "DevelopmentOccupiedSlotCount"), Is.EqualTo(0));
        }

        [Test]
        public void SetHealthClampsToRuntimeMaximum()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            object service = CreateService(fixture);

            Invoke(service, "DamagePlayer", 50);
            object operation = Invoke(service, "SetHealth", 9999);

            Assert.That(Get<bool>(operation, "Succeeded"), Is.True, Get<string>(operation, "Message"));
            Assert.That(Get<int>(fixture.Health, "CurrentHealth"), Is.EqualTo(Get<int>(fixture.Health, "MaximumHealth")));
        }

        [Test]
        public void ConfigureCreatesAutomationEnemyWhenSceneHasNoAuthoredEnemy()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            object service = CreateService(fixture);
            object context = Get<object>(service, "context");

            Assert.That(Get<object>(context, "EnemyTransform"), Is.Not.Null);
            Assert.That(Get<object>(context, "EnemyHealth"), Is.Not.Null);
            Assert.That(Get<object>(context, "EnemyLifecycle"), Is.Not.Null);
            Assert.That(Get<object>(context, "EnemyStatuses"), Is.Not.Null);

            object damageType = First(InvokeGeneric(service, "GetDefinitions", RequiredType("UnityIsekaiGame.Combat.DamageTypeDefinition")));
            object attack = Invoke(service, "ExecuteAttackResolution", damageType, 5f, 0.95f, 0.1f, 0f, 0.99f, 1.5f, 1f, 2f, true, true, false);
            Assert.That(Get<bool>(attack, "Succeeded"), Is.True, Get<string>(attack, "Message"));
        }

        [Test]
        public void DuplicateWorldEntityProofRejectsCurrentSpawnedEntityAfterMultipleSpawns()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            object service = CreateService(fixture);
            UnityEngine.Object potion = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PotionPath);

            try
            {
                InvokeStatic(RequiredType("UnityIsekaiGame.WorldEntities.WorldEntityRegistry"), "ClearForTests");

                object firstSpawn = Invoke(service, "SpawnPersistentWorldLoot", potion);
                Assert.That(Get<bool>(firstSpawn, "Succeeded"), Is.True, Get<string>(firstSpawn, "Message"));

                object secondSpawn = Invoke(service, "SpawnPersistentWorldLoot", potion);
                Assert.That(Get<bool>(secondSpawn, "Succeeded"), Is.True, Get<string>(secondSpawn, "Message"));

                SetField(service, "lastSpawnedWorldEntityId", string.Empty);
                object duplicateProof = Invoke(service, "AttemptDuplicateWorldEntityRegistration");
                Assert.That(Get<bool>(duplicateProof, "Succeeded"), Is.True, Get<string>(duplicateProof, "Message"));
                Assert.That(Get<string>(duplicateProof, "Message"), Does.Contain("Duplicate rejected"));
            }
            finally
            {
                InvokeStatic(RequiredType("UnityIsekaiGame.WorldEntities.WorldEntityRegistry"), "ClearForTests");
                DestroyTestLabWorldLoot();
            }
        }

        [Test]
        public void ApplyAndRemoveStatusUsesRuntimeController()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            object service = CreateService(fixture);
            UnityEngine.Object status = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(StatusPath);

            object applied = Invoke(service, "ApplyStatus", status, false);
            Assert.That(Get<bool>(applied, "Succeeded"), Is.True, Get<string>(applied, "Message"));
            Assert.That(Count(Get<object>(fixture.Statuses, "ActiveStatuses")), Is.EqualTo(1));

            object removed = Invoke(service, "RemoveStatus", status, false);
            Assert.That(Get<bool>(removed, "Succeeded"), Is.True, Get<string>(removed, "Message"));
            Assert.That(Count(Get<object>(fixture.Statuses, "ActiveStatuses")), Is.EqualTo(0));
        }

        [Test]
        public void OperationHistoryIsBounded()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            object service = CreateService(fixture);

            for (int i = 0; i < HistoryLimit + 8; i++)
            {
                Invoke(service, "RestoreVitals");
            }

            Assert.That(Count(Get<object>(service, "History")), Is.EqualTo(HistoryLimit));
        }

        private static object CreateService(RuntimeFixture fixture = null)
        {
            object service = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Development.PrototypeTestLabService"));
            object context = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Development.PrototypeTestLabContext"));
            SetField(context, "DefinitionCatalog", AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath));
            SetField(context, "Inventory", fixture?.Inventory);
            SetField(context, "PlayerStats", fixture?.Stats);
            SetField(context, "PlayerHealth", fixture?.Health);
            SetField(context, "PlayerStatuses", fixture?.Statuses);
            SetField(context, "PlayerAttributes", fixture?.Attributes);
            SetField(context, "PlayerCalculatedStats", fixture?.CalculatedStats);
            SetField(context, "PlayerResources", fixture?.Resources);
            SetField(context, "PlayerSkills", fixture?.Skills);
            SetField(context, "PlayerTraits", fixture?.Traits);
            SetField(context, "CharacterSystem", fixture?.CharacterSystem);
            SetField(context, "PlayerTransform", fixture?.Root.transform);
            Invoke(service, "Configure", context);
            return service;
        }

        private static Type RequiredType(string typeName)
        {
            Type type = TestTypeResolver.RequiredType(typeName);
            Assert.That(type, Is.Not.Null, $"Required type not found: {typeName}");
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            Type[] argumentTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                argumentTypes[i] = args[i]?.GetType();
            }

            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} not found on {target.GetType().Name}");
            return method.Invoke(target, args);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} not found on {type.Name}");
            return method.Invoke(null, args);
        }

        private static object InvokeGeneric(object target, string methodName, Type genericArgument)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} not found on {target.GetType().Name}");
            return method.MakeGenericMethod(genericArgument).Invoke(target, Array.Empty<object>());
        }

        private static T Get<T>(object target, string memberName)
        {
            object value = GetMember(target, memberName);
            return value == null ? default : (T)value;
        }

        private static object GetMember(object target, string memberName)
        {
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(target);
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{memberName} not found on {type.Name}");
            return field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static int Count(object value)
        {
            if (value is ICollection collection)
            {
                return collection.Count;
            }

            int count = 0;
            foreach (object _ in (IEnumerable)value)
            {
                count++;
            }

            return count;
        }

        private static object First(object value)
        {
            foreach (object entry in (IEnumerable)value)
            {
                return entry;
            }

            Assert.Fail("Expected at least one value.");
            return null;
        }

        private static void DestroyTestLabWorldLoot()
        {
            foreach (GameObject gameObject in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (gameObject.name.StartsWith("Persistent Test Loot", StringComparison.Ordinal)
                    || gameObject.name.StartsWith("Restored Test Loot", StringComparison.Ordinal)
                    || gameObject.name.StartsWith("Transient Test Loot", StringComparison.Ordinal)
                    || gameObject.name.StartsWith("Duplicate World Entity Proof", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }
        }

        private static void InvokeAwake(Component component)
        {
            component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, Array.Empty<object>());
        }

        private sealed class RuntimeFixture : IDisposable
        {
            public GameObject Root { get; private set; }
            public Component Inventory { get; private set; }
            public Component Stats { get; private set; }
            public Component Health { get; private set; }
            public Component Statuses { get; private set; }
            public Component Attributes { get; private set; }
            public Component CalculatedStats { get; private set; }
            public Component Resources { get; private set; }
            public Component Skills { get; private set; }
            public Component Traits { get; private set; }
            public Component CharacterSystem { get; private set; }

            public static RuntimeFixture Create()
            {
                GameObject root = new GameObject("Prototype Test Lab Fixture");
                RuntimeFixture fixture = new RuntimeFixture
                {
                    Root = root,
                    Inventory = root.AddComponent(RequiredType("UnityIsekaiGame.Inventory.PlayerInventory")),
                    Stats = root.AddComponent(RequiredType("UnityIsekaiGame.Equipment.PlayerStats")),
                    Health = root.AddComponent(RequiredType("UnityIsekaiGame.Gameplay.PlayerHealth")),
                    Statuses = root.AddComponent(RequiredType("UnityIsekaiGame.StatusEffects.StatusEffectController")),
                    Attributes = root.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes")),
                    CalculatedStats = root.AddComponent(RequiredType("UnityIsekaiGame.Stats.CalculatedStatCollection")),
                    Resources = root.AddComponent(RequiredType("UnityIsekaiGame.ResourceSystem.CharacterResourceCollection")),
                    Skills = root.AddComponent(RequiredType("UnityIsekaiGame.Skills.CharacterSkillCollection")),
                    Traits = root.AddComponent(RequiredType("UnityIsekaiGame.Traits.CharacterTraitCollection"))
                };

                root.AddComponent(RequiredType("UnityIsekaiGame.Abilities.CharacterAbilityCollection"));
                root.AddComponent(RequiredType("UnityIsekaiGame.Capabilities.CharacterCapabilityCollection"));
                root.AddComponent(RequiredType("UnityIsekaiGame.CharacterSystem.AuthoritativeCharacterSimulationDriver"));
                fixture.CharacterSystem = root.AddComponent(RequiredType("UnityIsekaiGame.CharacterSystem.CharacterSystemCoordinator"));

                InvokeAwake(fixture.Inventory);
                InvokeAwake(fixture.Stats);
                InvokeAwake(fixture.Health);
                InvokeAwake(fixture.Statuses);
                return fixture;
            }

            public void Dispose()
            {
                if (Root != null)
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                }
            }
        }
    }
}
