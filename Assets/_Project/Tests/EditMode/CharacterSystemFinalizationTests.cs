using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Tests
{
    public sealed class CharacterSystemFinalizationTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in createdObjects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void CharacterCoordinator_InitializesReadySnapshotWithDistinctPersonAndActor()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = CreateFullCharacterOwner("Character Coordinator Ready Test");

            Component identity = owner.GetComponent(RequiredType("UnityIsekaiGame.Progression.PlayerIdentityProgression"));
            Invoke(identity, "ConfigureIdentity", "account.test", "player.test", "person.test");

            Component coordinator = owner.GetComponent(RequiredType("UnityIsekaiGame.CharacterSystem.CharacterSystemCoordinator"));
            Assert.That(Invoke<bool>(coordinator, "InitializeFromRegistry", registry, false), Is.True);
            Assert.That(Property<object>(coordinator, "Readiness").ToString(), Is.EqualTo("Ready"));
            Assert.That(Property<bool>(coordinator, "IsReady"), Is.True);

            object snapshot = Invoke(coordinator, "GetSnapshot", false);
            object snapshotIdentity = Property<object>(snapshot, "Identity");
            Assert.That(Property<string>(snapshotIdentity, "PlayerId"), Is.EqualTo("player.test"));
            Assert.That(Property<string>(snapshotIdentity, "PersonId"), Is.EqualTo("person.test"));
            Assert.That(Property<string>(snapshotIdentity, "ActorId"), Is.Not.Empty);
            Assert.That(Property<string>(snapshotIdentity, "ActorId"), Is.Not.EqualTo("person.test"));

            string identitySaveJson = JsonUtility.ToJson(Invoke(identity, "CreateSaveData"));
            Assert.That(identitySaveJson, Does.Contain("\"assigned\":true"));
            Assert.That(identitySaveJson, Does.Contain("\"originId\":\"origin."));
            Assert.That(identitySaveJson, Does.Contain("\"giftDefinitionId\":\"birth-gift."));

            Assert.That(Invoke<bool>(coordinator, "InitializeFromRegistry", registry, false), Is.True);
            Assert.That(JsonUtility.ToJson(Invoke(identity, "CreateSaveData")), Is.EqualTo(identitySaveJson));
        }

        [Test]
        public void CharacterCoordinator_FullRebuildIncrementsRevisionWithoutDuplicatingRecords()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = CreateFullCharacterOwner("Character Coordinator Rebuild Test");
            Component coordinator = owner.GetComponent(RequiredType("UnityIsekaiGame.CharacterSystem.CharacterSystemCoordinator"));

            Assert.That(Invoke<bool>(coordinator, "InitializeFromRegistry", registry, false), Is.True);
            long firstRevision = Property<long>(coordinator, "Revision");
            int firstSkillCount = Count(Property<object>(Invoke(coordinator, "GetSnapshot", true), "Progression"), "LearnedSkills");
            int firstTraitCount = Count(Property<object>(Invoke(coordinator, "GetSnapshot", true), "Progression"), "Traits");

            Assert.That(Invoke<bool>(coordinator, "FullRebuild", false, "TestRebuild"), Is.True);

            Assert.That(Property<long>(coordinator, "Revision"), Is.GreaterThan(firstRevision));
            int secondSkillCount = Count(Property<object>(Invoke(coordinator, "GetSnapshot", true), "Progression"), "LearnedSkills");
            int secondTraitCount = Count(Property<object>(Invoke(coordinator, "GetSnapshot", true), "Progression"), "Traits");
            Assert.That(secondSkillCount, Is.EqualTo(firstSkillCount));
            Assert.That(secondTraitCount, Is.EqualTo(firstTraitCount));
        }

        [Test]
        public void CharacterQuery_NotReadyRequirementFailsClearly()
        {
            GameObject owner = CreateGameObject("Character Query Not Ready Test");
            Component coordinator = owner.AddComponent(RequiredType("UnityIsekaiGame.CharacterSystem.CharacterSystemCoordinator"));

            object query = Property<object>(coordinator, "Query");
            object result = Invoke(query, "EvaluateRequirement", new object[] { null });

            Assert.That(Property<bool>(result, "Passed"), Is.False);
            object reasons = Property<object>(result, "TestLabFailureReasons");
            Assert.That(string.Join(";", Enumerate(reasons)), Does.Contain("Character System is not Ready"));
        }

        [Test]
        public void CharacterIntegrity_DetectsDuplicateCoordinator()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = CreateFullCharacterOwner("Character Integrity Duplicate Test");
            Component first = owner.GetComponent(RequiredType("UnityIsekaiGame.CharacterSystem.CharacterSystemCoordinator"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.CharacterSystem.CharacterSystemCoordinator"));
            Invoke(first, "InitializeFromRegistry", registry, false);

            object report = Invoke(first, "ValidateIntegrity");
            Assert.That(Property<bool>(report, "Passed"), Is.False);
            Assert.That(Invoke<string>(report, "GetSummary"), Does.Contain("Duplicate CharacterSystemCoordinator"));
        }

        [Test]
        public void CharacterCoordinator_NpcLikeReducedCharacterCanInitializeWithoutAccountComponent()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = CreateGameObject("NPC Like Character Test");
            owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CalculatedStatCollection"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.ResourceSystem.CharacterResourceCollection"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Skills.CharacterSkillCollection"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Traits.CharacterTraitCollection"));
            Component coordinator = owner.AddComponent(RequiredType("UnityIsekaiGame.CharacterSystem.CharacterSystemCoordinator"));

            Assert.That(Invoke<bool>(coordinator, "InitializeFromRegistry", registry, false), Is.True);
            Assert.That(Property<bool>(coordinator, "IsReady"), Is.True);
            Assert.That(Property<string>(coordinator, "AccountId"), Is.Empty);
            Assert.That(Property<string>(coordinator, "ActorId"), Does.StartWith("actor.runtime."));
        }

        private GameObject CreateFullCharacterOwner(string name)
        {
            GameObject owner = CreateGameObject(name);
            owner.AddComponent(RequiredType("UnityIsekaiGame.Progression.PlayerIdentityProgression"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Equipment.PlayerStats"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Inventory.PlayerInventory"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Equipment.PlayerEquipment"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.StatusEffects.StatusEffectController"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CalculatedStatCollection"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.ResourceSystem.CharacterResourceCollection"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Skills.CharacterSkillCollection"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Abilities.CharacterAbilityCollection"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Capabilities.CharacterCapabilityCollection"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.Traits.CharacterTraitCollection"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.CharacterSystem.AuthoritativeCharacterSimulationDriver"));
            owner.AddComponent(RequiredType("UnityIsekaiGame.CharacterSystem.CharacterSystemCoordinator"));
            return owner;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject owner = new GameObject(name);
            createdObjects.Add(owner);
            return owner;
        }

        private static DefinitionRegistry LoadRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog.CreateRegistry();
        }

        private static int Count(object target, string propertyName)
        {
            System.Collections.IEnumerable values = (System.Collections.IEnumerable)Property<object>(target, propertyName);
            int count = 0;
            foreach (object _ in values)
            {
                count++;
            }

            return count;
        }

        private static string[] Enumerate(object values)
        {
            System.Collections.IEnumerable enumerable = (System.Collections.IEnumerable)values;
            System.Collections.Generic.List<string> result = new System.Collections.Generic.List<string>();
            foreach (object value in enumerable)
            {
                result.Add(value == null ? string.Empty : value.ToString());
            }

            return result.ToArray();
        }

        private static Type RequiredType(string fullName)
        {
            Type type = TestTypeResolver.RequiredType(fullName);
            Assert.That(type, Is.Not.Null, $"Type '{fullName}' is missing.");
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            return FindMethod(target.GetType(), methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, args)
                .Invoke(target, args);
        }

        private static T Invoke<T>(object target, string methodName, params object[] args)
        {
            return (T)Invoke(target, methodName, args);
        }

        private static T Property<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static MethodInfo FindMethod(Type type, string methodName, BindingFlags flags, object[] args)
        {
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != (args == null ? 0 : args.Length))
                {
                    continue;
                }

                bool compatible = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (args[i] != null && !parameters[i].ParameterType.IsInstanceOfType(args[i]))
                    {
                        compatible = false;
                        break;
                    }
                }

                if (compatible)
                {
                    return method;
                }
            }

            Assert.Fail($"Method '{methodName}' not found on '{type.FullName}'.");
            return null;
        }
    }
}
