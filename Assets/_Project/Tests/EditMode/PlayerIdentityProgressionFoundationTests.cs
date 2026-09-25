using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class PlayerIdentityProgressionFoundationTests
    {
        private const string PrototypeCatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        private GameObject runtimeRoot;

        [TearDown]
        public void TearDown()
        {
            if (runtimeRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimeRoot);
            }
        }

        [Test]
        public void PrototypeCatalogRegistersFeature51Definitions()
        {
            DefinitionRegistry registry = LoadRegistry();

            AssertResolve(registry, "origin-family.native-born", "UnityIsekaiGame.Progression.OriginFamilyDefinition");
            AssertResolve(registry, "origin-family.summoned-otherworlder", "UnityIsekaiGame.Progression.OriginFamilyDefinition");
            AssertResolve(registry, "origin-family.reincarnated-local", "UnityIsekaiGame.Progression.OriginFamilyDefinition");
            AssertResolve(registry, "origin.native-born.farmer-child", "UnityIsekaiGame.Progression.OriginDefinition");
            AssertResolve(registry, "origin.summoned-otherworlder.accidental", "UnityIsekaiGame.Progression.OriginDefinition");
            AssertResolve(registry, "origin.reincarnated-local.scholar-mage", "UnityIsekaiGame.Progression.OriginDefinition");
            AssertResolve(registry, "origin-generation.default", "UnityIsekaiGame.Progression.OriginGenerationPolicyDefinition");
            AssertResolve(registry, "birth-gift.arcane-spark", "UnityIsekaiGame.Progression.BirthGiftDefinition");
            AssertResolve(registry, "birth-gift.heroic-instinct", "UnityIsekaiGame.Progression.BirthGiftDefinition");
            AssertResolve(registry, "birth-gift.sturdy-soul", "UnityIsekaiGame.Progression.BirthGiftDefinition");
            AssertResolve(registry, "birth-gift.latent-arcane-bolt", "UnityIsekaiGame.Progression.BirthGiftDefinition");
            AssertResolve(registry, "birth-gift.merchant-instinct", "UnityIsekaiGame.Progression.BirthGiftDefinition");
            AssertResolve(registry, "birth-gift.patient-growth", "UnityIsekaiGame.Progression.BirthGiftDefinition");
            AssertResolve(registry, "role.commoner", "UnityIsekaiGame.Progression.RoleDefinition");
            AssertResolve(registry, "role.noble", "UnityIsekaiGame.Progression.RoleDefinition");
            AssertResolve(registry, "social-status.citizen", "UnityIsekaiGame.Progression.SocialStatusDefinition");
            AssertResolve(registry, "social-status.wanted", "UnityIsekaiGame.Progression.SocialStatusDefinition");
            AssertResolve(registry, "currency.gold", "UnityIsekaiGame.Progression.CurrencyDefinition");
            AssertResolve(registry, "title.lord", "UnityIsekaiGame.Progression.TitleDefinition");
            AssertResolve(registry, "overall-level.prototype", "UnityIsekaiGame.Progression.OverallLevelConfiguration");

            Assert.That(registry.Contains("Gold"), Is.False);
            Assert.That(registry.Contains("NativeFarmer"), Is.False);
            Assert.That(registry.Contains("origin.NativeFarmer"), Is.False);
        }

        [Test]
        public void OriginGiftAffinityIsAlwaysAppliedFromCatalogPolicy()
        {
            DefinitionRegistry registry = LoadRegistry();
            OriginFamilyDefinition family = (OriginFamilyDefinition)Definition(registry, "origin-family.native-born");
            OriginDefinition origin = (OriginDefinition)Definition(registry, "origin.native-born.farmer-child");
            CharacterOriginGenerator generator = new CharacterOriginGenerator(registry, new SeededProgressionRandomSource(1234));

            var weights = generator.BuildBirthGiftWeights(family, origin, out string failureReason);

            Assert.That(failureReason, Is.Empty);
            Assert.That(weights.Count, Is.EqualTo(6));
            Assert.That(weights.Sum(entry => entry.Probability), Is.EqualTo(1f).Within(0.0001f));

            BirthGiftWeightEntry sturdySoul = weights.Single(entry => entry.Gift.Id == "birth-gift.sturdy-soul");
            BirthGiftWeightEntry arcaneSpark = weights.Single(entry => entry.Gift.Id == "birth-gift.arcane-spark");
            Assert.That(sturdySoul.AffinityMultiplier, Is.EqualTo(2f));
            Assert.That(sturdySoul.FinalWeight, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(arcaneSpark.AffinityMultiplier, Is.EqualTo(0.75f));
            Assert.That(arcaneSpark.FinalWeight, Is.EqualTo(1.125f).Within(0.0001f));
        }

        [Test]
        public void OriginGenerationIsDeterministicForTheSameSeed()
        {
            DefinitionRegistry registry = LoadRegistry();
            CharacterOriginGenerationResult first = new CharacterOriginGenerator(registry, new SeededProgressionRandomSource(8675309)).Generate();
            CharacterOriginGenerationResult second = new CharacterOriginGenerator(registry, new SeededProgressionRandomSource(8675309)).Generate();

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(second.Family.Id, Is.EqualTo(first.Family.Id));
            Assert.That(second.Origin.Id, Is.EqualTo(first.Origin.Id));
            Assert.That(second.BirthGift.Id, Is.EqualTo(first.BirthGift.Id));
            Assert.That(second.StartingGold, Is.EqualTo(first.StartingGold));
        }

        [Test]
        public void EveryEnabledOriginProducesACompleteFiniteGiftDistribution()
        {
            DefinitionRegistry registry = LoadRegistry();
            CharacterOriginGenerator generator = new CharacterOriginGenerator(registry, new SeededProgressionRandomSource(1));

            foreach (OriginFamilyDefinition family in registry.DefinitionsById.Values.OfType<OriginFamilyDefinition>().Where(value => value.EnabledForAlpha))
            {
                foreach (OriginDefinition origin in family.AllowedOrigins.Where(value => value != null && value.EnabledForAlpha))
                {
                    var weights = generator.BuildBirthGiftWeights(family, origin, out string failureReason);
                    Assert.That(weights.Count, Is.EqualTo(6), $"{origin.Id}: {failureReason}");
                    Assert.That(weights.All(entry => entry.FinalWeight >= 0f && !float.IsNaN(entry.FinalWeight) && !float.IsInfinity(entry.FinalWeight)), Is.True, origin.Id);
                    Assert.That(weights.Sum(entry => entry.Probability), Is.EqualTo(1f).Within(0.0001f), origin.Id);
                }
            }
        }

        [Test]
        public void OriginAssignmentIsOnceOnlyAndAppliesStartingRewardsOnce()
        {
            DefinitionRegistry registry = LoadRegistry();
            Component progression = CreateProgression(registry);
            object family = Definition(registry, "origin-family.native-born");
            object origin = Definition(registry, "origin.native-born.farmer-child");
            object gift = Definition(registry, "birth-gift.sturdy-soul");
            object generated = InvokeStatic(
                RequiredType("UnityIsekaiGame.Progression.CharacterOriginGenerationResult"),
                "Success",
                family,
                origin,
                gift,
                33L);

            object assigned = Invoke(progression, "AssignGeneratedOrigin", generated, 1234, "test", false);

            Assert.That(GetProperty<bool>(assigned, "Succeeded"), Is.True, GetProperty<string>(assigned, "Message"));
            object saveData = Invoke(progression, "CreateSaveData");
            string saveJson = JsonUtility.ToJson(saveData);
            Assert.That(saveJson, Does.Contain("\"assigned\":true"));
            Assert.That(saveJson, Does.Contain("\"startingGoldAmount\":33"));
            Assert.That(saveJson, Does.Contain("\"startingCurrencyApplied\":false"));
            Assert.That(saveJson, Does.Not.Contain("walletBalances"), "Currency balances are owned only by EconomyRuntime.");
            Assert.That(saveJson, Does.Contain("\"roleDefinitionId\":\"role.commoner\""));
            Assert.That(saveJson, Does.Contain("\"socialStatusDefinitionId\":\"social-status.citizen\""));
            Assert.That(saveJson, Does.Contain("\"definitionId\":\"origin.native-born.farmer-child\""));
            Assert.That(saveJson, Does.Contain("\"definitionId\":\"birth-gift.sturdy-soul\""));
            object birthGiftRecord = saveData.GetType().GetField("birthGift").GetValue(saveData);
            string awakenedAtUtc = (string)birthGiftRecord.GetType().GetField("awakenedAtUtc").GetValue(birthGiftRecord);
            Assert.That(DateTime.TryParse(awakenedAtUtc, out _), Is.True, "Immediate birth gifts must record when they awakened.");

            object duplicate = Invoke(progression, "AssignGeneratedOrigin", generated, 5678, "test", false);

            Assert.That(GetProperty<bool>(duplicate, "Succeeded"), Is.False);
            Assert.That(GetProperty<string>(duplicate, "Code"), Is.EqualTo("OriginAlreadyAssigned"));
            Assert.That(JsonUtility.ToJson(Invoke(progression, "CreateSaveData")), Does.Contain("\"startingGoldAmount\":33"));
            Assert.That(JsonUtility.ToJson(Invoke(progression, "CreateSaveData")), Does.Not.Contain("\"amount\":999"));
        }

        [Test]
        public void RoleConflictRequiresExplicitAcceptanceAndPreservesHistory()
        {
            DefinitionRegistry registry = LoadRegistry();
            Component progression = CreateProgression(registry);
            object commoner = Definition(registry, "role.commoner");
            object noble = Definition(registry, "role.noble");

            object commonerResult = Invoke(progression, "AddRole", commoner, "development", string.Empty, false, false, false);
            Assert.That(GetProperty<bool>(commonerResult, "Succeeded"), Is.True, GetProperty<string>(commonerResult, "Message"));

            object rejected = Invoke(progression, "AddRole", noble, "development", string.Empty, false, false, false);
            Assert.That(GetProperty<bool>(rejected, "Succeeded"), Is.False);
            Assert.That(GetProperty<string>(rejected, "Code"), Is.EqualTo("RoleConflict"));

            object accepted = Invoke(progression, "AddRole", noble, "development", string.Empty, false, true, false);
            Assert.That(GetProperty<bool>(accepted, "Succeeded"), Is.True, GetProperty<string>(accepted, "Message"));
            string saveJson = JsonUtility.ToJson(Invoke(progression, "CreateSaveData"));
            Assert.That(saveJson, Does.Contain("\"roleDefinitionId\":\"role.commoner\""));
            Assert.That(saveJson, Does.Contain("\"lifecycleState\":3"));
            Assert.That(saveJson, Does.Contain("\"roleDefinitionId\":\"role.noble\""));
            Assert.That(saveJson, Does.Contain("\"lifecycleState\":0"));
        }

        [Test]
        public void IdentityProgressionParticipantRejectsWrongPlayerPayload()
        {
            DefinitionRegistry registry = LoadRegistry();
            Component progression = CreateProgression(registry);
            Type participantType = RequiredType("UnityIsekaiGame.Progression.PlayerIdentityProgressionPersistenceParticipant");
            Func<DefinitionRegistry> provider = () => registry;
            object participant = Activator.CreateInstance(
                participantType,
                progression,
                provider,
                PersistenceService.LocalPlayerId,
                PersistenceService.LocalAccountId);
            object saveData = Invoke(progression, "CreateSaveData");
            saveData.GetType().GetField("playerId").SetValue(saveData, "other-player");

            PersistenceParticipantPrepareResult prepare = (PersistenceParticipantPrepareResult)Invoke(
                participant,
                "PreparePayload",
                JsonUtility.ToJson(saveData),
                GetStatic<int>(participantType, "CurrentParticipantSchemaVersion"));

            Assert.That(prepare.Succeeded, Is.False);
            Assert.That(prepare.Message, Does.Contain("does not match participant owner"));
        }

        private static DefinitionRegistry LoadRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(PrototypeCatalogPath);
            Assert.That(catalog, Is.Not.Null, "Prototype catalog failed to load.");
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
            return catalog.CreateRegistry();
        }

        private Component CreateProgression(DefinitionRegistry registry)
        {
            runtimeRoot = new GameObject("Identity Progression Test Runtime");
            Component stats = runtimeRoot.AddComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));
            Component progression = runtimeRoot.AddComponent(RequiredType("UnityIsekaiGame.Progression.PlayerIdentityProgression"));
            Invoke(progression, "ConfigureIdentity", PersistenceService.LocalAccountId, PersistenceService.LocalPlayerId, "person.test");
            Invoke(progression, "ConfigureRuntimeReferences", stats, null, null, Definition(registry, "overall-level.prototype"));
            Invoke(progression, "ConfigureDefinitions", registry);
            object[] validateArgs = { null };
            bool valid = (bool)progression.GetType().GetMethod("ValidateIdentity").Invoke(progression, validateArgs);
            Assert.That(valid, Is.True, validateArgs[0] as string);
            return progression;
        }

        private static object Definition(DefinitionRegistry registry, string id)
        {
            Assert.That(registry.TryGet(id, out IGameDefinition definition), Is.True, id);
            return definition;
        }

        private static void AssertResolve(DefinitionRegistry registry, string id, string expectedType)
        {
            Assert.That(registry.TryGet(id, out IGameDefinition definition), Is.True, id);
            Assert.That(definition.GetType().FullName, Is.EqualTo(expectedType), id);
        }

        private static Type RequiredType(string typeName)
        {
            Type type = TestTypeResolver.RequiredType(typeName);
            Assert.That(type, Is.Not.Null, typeName);
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = FindMethod(target.GetType(), methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, args);
            Assert.That(method, Is.Not.Null, $"{target.GetType().FullName}.{methodName}");
            return method.Invoke(target, args);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            MethodInfo method = FindMethod(type, methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, args);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName}");
            return method.Invoke(null, args);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static T GetStatic<T>(Type type, string fieldName)
        {
            return (T)type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);
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
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                bool matches = true;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == null)
                    {
                        continue;
                    }

                    if (!parameters[i].ParameterType.IsAssignableFrom(args[i].GetType()))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return method;
                }
            }

            return null;
        }
    }
}
