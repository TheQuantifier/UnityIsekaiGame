using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class WorldEntityIdentityTests
    {
        [SetUp]
        public void SetUp()
        {
            InvokeStatic(RegistryType, "ClearForTests");
        }

        [TearDown]
        public void TearDown()
        {
            InvokeStatic(RegistryType, "ClearForTests");
            foreach (GameObject gameObject in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (gameObject.name.StartsWith("WorldEntityTest", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }
        }

        [Test]
        public void AuthoredIdentityComposesStableSceneScopedId()
        {
            Component identity = CreateIdentity("WorldEntityTest Authored");
            object result = Invoke(identity, "TrySetAuthoredIdentity", "enemy.primary", "scene.prototype", PersistenceScope.RegionOrScene, "being.prototype-enemy", null);

            Assert.That(result, Is.True);
            Assert.That(Get<string>(identity, "EntityId"), Is.EqualTo("entity.scene.prototype.enemy.primary"));
            Assert.That(Get<string>(identity, "EntityId"), Is.EqualTo("entity.scene.prototype.enemy.primary"), "Reading EntityId should not generate a new value.");
            Assert.That(Get<object>(identity, "IdentityKind").ToString(), Is.EqualTo("Authored"));
        }

        [Test]
        public void RegistryRejectsDuplicateWorldEntityIds()
        {
            Component first = CreateAuthoredRegistered("WorldEntityTest Duplicate A", "enemy.primary");
            Component second = CreateIdentity("WorldEntityTest Duplicate B");
            Invoke(second, "TrySetAuthoredIdentity", "enemy.primary", "scene.prototype", PersistenceScope.RegionOrScene, null, null);

            object registration = Invoke(second, "TryRegister", null);

            Assert.That(Get<bool>(registration, "Succeeded"), Is.False);
            Assert.That(Get<string>(registration, "Code"), Is.EqualTo("DuplicateId"));
            Assert.That(Get<int>(RegistryType, "Count"), Is.EqualTo(1));
            Assert.That(Get<bool>(first, "IsRegistered"), Is.True);
        }

        [Test]
        public void RuntimeIdentityCreatesUniqueIdAndRestoreKeepsSavedId()
        {
            GameObject spawned = CreateGameObject("WorldEntityTest Spawned");
            object spawnResult = InvokeStatic(FactoryType, "CreateRuntimeIdentity", spawned, "scene.prototype", "local-world", "item.health-potion", PersistenceScope.RegionOrScene);

            Assert.That(Get<bool>(spawnResult, "Succeeded"), Is.True, Get<string>(spawnResult, "Message"));
            Component spawnedIdentity = Get<Component>(spawnResult, "Identity");
            string savedId = Get<string>(spawnedIdentity, "EntityId");
            Assert.That(savedId, Does.StartWith("entity.local-world.runtime."));

            InvokeStatic(RegistryType, "Unregister", spawnedIdentity);
            UnityEngine.Object.DestroyImmediate(spawned);

            GameObject restored = CreateGameObject("WorldEntityTest Restored");
            object restoreResult = InvokeStatic(FactoryType, "RestoreRuntimeIdentity", restored, savedId, "scene.prototype", "local-world", "item.health-potion", PersistenceScope.RegionOrScene);

            Assert.That(Get<bool>(restoreResult, "Succeeded"), Is.True, Get<string>(restoreResult, "Message"));
            Component restoredIdentity = Get<Component>(restoreResult, "Identity");
            Assert.That(Get<string>(restoredIdentity, "EntityId"), Is.EqualTo(savedId));
            Assert.That(Get<object>(restoredIdentity, "IdentityKind").ToString(), Is.EqualTo("RestoredRuntime"));
        }

        [Test]
        public void WorldEntityReferenceRoundTripsAndRejectsWrongType()
        {
            Component identity = CreateAuthoredRegistered("WorldEntityTest Reference", "npc.primary");
            identity.gameObject.AddComponent<BoxCollider>();

            object reference = Invoke(identity, "CreateReference", "BoxCollider");
            string json = Invoke<string>(reference, "ToJson");
            object[] parseArgs = { json, null, null };
            bool parsed = InvokeStatic<bool>(ReferenceType, "TryFromJson", parseArgs);
            object parsedReference = parseArgs[1];

            Assert.That(parsed, Is.True, (string)parseArgs[2]);
            object resolve = Invoke(parsedReference, "Resolve");
            Assert.That(Get<bool>(resolve, "Succeeded"), Is.True, Get<string>(resolve, "Message"));

            SetField(parsedReference, "expectedEntityType", "CharacterController");
            object wrongType = Invoke(parsedReference, "Resolve");
            Assert.That(Get<bool>(wrongType, "Succeeded"), Is.False);
            Assert.That(Get<string>(wrongType, "Code"), Is.EqualTo("WrongType"));
        }

        [Test]
        public void TransientIdentityCannotCreatePersistentReference()
        {
            Component identity = CreateIdentity("WorldEntityTest Transient");
            Invoke(identity, "TryMarkTransient", null);
            object reference = InvokeStatic(ReferenceType, "FromIdentity", identity, null);
            object valid = Invoke(reference, "ValidateForPersistence", null);

            Assert.That(valid, Is.False);
        }

        [Test]
        public void DisabledRegisteredEntityStillResolvesUntilDestroyed()
        {
            Component identity = CreateAuthoredRegistered("WorldEntityTest Disabled", "loot.disabled");
            string entityId = Get<string>(identity, "EntityId");
            identity.gameObject.SetActive(false);

            object[] resolveArgs = { entityId, null };
            Assert.That(InvokeStatic<bool>(RegistryType, "TryResolve", resolveArgs), Is.True);
            Assert.That(resolveArgs[1], Is.SameAs(identity));

            UnityEngine.Object.DestroyImmediate(identity.gameObject);
            object[] destroyedResolveArgs = { entityId, null };
            Assert.That(InvokeStatic<bool>(RegistryType, "TryResolve", destroyedResolveArgs), Is.False);
        }

        [Test]
        public void PickupWorldEntityIdIsSeparateFromItemInstanceId()
        {
            Component identity = CreateAuthoredRegistered("WorldEntityTest Pickup", "pickup.prototype-sword");
            string entityId = Get<string>(identity, "EntityId");
            string itemInstanceId = "item-instance.prototype-sword.001";

            Assert.That(entityId, Does.StartWith("entity."));
            Assert.That(itemInstanceId, Does.StartWith("item-instance."));
            Assert.That(entityId, Is.Not.EqualTo(itemInstanceId));
        }

        private static Component CreateAuthoredRegistered(string name, string localId)
        {
            Component identity = CreateIdentity(name);
            bool configured = Invoke<bool>(identity, "TrySetAuthoredIdentity", localId, "scene.prototype", PersistenceScope.RegionOrScene, null, null);
            Assert.That(configured, Is.True);
            object registration = Invoke(identity, "TryRegister", null);
            Assert.That(Get<bool>(registration, "Succeeded"), Is.True, Get<string>(registration, "Message"));
            return identity;
        }

        private static Component CreateIdentity(string name)
        {
            return CreateGameObject(name).AddComponent(IdentityType);
        }

        private static GameObject CreateGameObject(string name)
        {
            return new GameObject(name);
        }

        private static Type IdentityType => RequiredType("UnityIsekaiGame.WorldEntities.WorldEntityIdentity");
        private static Type RegistryType => RequiredType("UnityIsekaiGame.WorldEntities.WorldEntityRegistry");
        private static Type FactoryType => RequiredType("UnityIsekaiGame.WorldEntities.WorldEntityIdentityFactory");
        private static Type ReferenceType => RequiredType("UnityIsekaiGame.WorldEntities.WorldEntityReference");

        private static Type RequiredType(string typeName)
        {
            Type type = TestTypeResolver.RequiredType(typeName);
            Assert.That(type, Is.Not.Null, $"Required type not found: {typeName}");
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            args ??= new object[] { null };
            MethodInfo method = null;
            if (AllArgumentsHaveTypes(args))
            {
                method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ArgumentTypes(args), null);
            }

            method ??= target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} not found on {target.GetType().Name}");
            NormalizeOutArguments(method, args);
            return method.Invoke(target, args);
        }

        private static T Invoke<T>(object target, string methodName, params object[] args)
        {
            return (T)Invoke(target, methodName, args);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            args ??= new object[] { null };
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} not found on {type.Name}");
            NormalizeOutArguments(method, args);
            return method.Invoke(null, args);
        }

        private static T InvokeStatic<T>(Type type, string methodName, params object[] args)
        {
            return (T)InvokeStatic(type, methodName, args);
        }

        private static Type[] ArgumentTypes(object[] args)
        {
            Type[] argumentTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                argumentTypes[i] = args[i]?.GetType();
            }

            return argumentTypes;
        }

        private static bool AllArgumentsHaveTypes(object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void NormalizeOutArguments(MethodInfo method, object[] args)
        {
            ParameterInfo[] parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length && i < args.Length; i++)
            {
                if (parameters[i].ParameterType.IsByRef && args[i] == null)
                {
                    Type elementType = parameters[i].ParameterType.GetElementType();
                    args[i] = elementType == typeof(string) ? string.Empty : null;
                }
            }
        }

        private static T Get<T>(object target, string memberName)
        {
            return (T)Get(target, memberName);
        }

        private static object Get(object target, string memberName)
        {
            Type type = target is Type staticType ? staticType : target.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (target is Type ? BindingFlags.Static : BindingFlags.Instance);
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null)
            {
                return property.GetValue(target is Type ? null : target);
            }

            FieldInfo field = type.GetField(memberName, flags);
            Assert.That(field, Is.Not.Null, $"{memberName} not found on {type.Name}");
            return field.GetValue(target is Type ? null : target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
