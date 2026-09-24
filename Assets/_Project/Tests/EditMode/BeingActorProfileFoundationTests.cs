using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Beings;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Tests
{
    public sealed class BeingActorProfileFoundationTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string PlayerProfilePath = "Assets/_Project/Prototype/Content/GameData/ActorProfiles/PlayerPrototypeActorProfile.asset";
        private const string EnemyProfilePath = "Assets/_Project/Prototype/Content/GameData/ActorProfiles/EnemyPrototypeActorProfile.asset";

        [Test]
        public void BeingDefinition_ExposesStableClassificationMetadata()
        {
            CategoryDefinition category = CreateCategory("category.being.person", CategoryDomain.Being);
            TagDefinition humanoid = CreateTag("tag.being.humanoid", CategoryDomain.Being);
            BeingDefinition being = CreateBeing("being.person", "Person", category, new[] { humanoid });

            Assert.That(being.Id, Is.EqualTo("being.person"));
            Assert.That(being.Intelligence.ToString(), Is.EqualTo("Sapient"));
            Assert.That(being.SocialCapability.ToString(), Is.EqualTo("Institutional"));
            Assert.That(Convert.ToInt32(being.LocomotionCapabilities), Is.EqualTo(1));
            Assert.That(ClassificationUtility.IsInCategory(being, "category.being.person"), Is.True);
            Assert.That(ClassificationUtility.HasTag(being, "tag.being.humanoid"), Is.True);
        }

        [Test]
        public void PrototypeActorProfiles_UseCanonicalCalculatedStatContributionsOnly()
        {
            ActorProfileDefinition player = AssetDatabase.LoadAssetAtPath<ActorProfileDefinition>(PlayerProfilePath);
            ActorProfileDefinition enemy = AssetDatabase.LoadAssetAtPath<ActorProfileDefinition>(EnemyProfilePath);

            Assert.That(player, Is.Not.Null);
            Assert.That(enemy, Is.Not.Null);
            Assert.That(player.StatContributions, Is.Not.Empty);
            Assert.That(enemy.StatContributions, Is.Not.Empty);
            Assert.That(enemy.StatContributions.All(entry => entry.IsValid && entry.Stat.Id.StartsWith("calculated-stat.")), Is.True);
            Assert.That(typeof(ActorProfileDefinition).GetField("baseMaximumHealth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), Is.Null);
            Assert.That(typeof(ActorProfileDefinition).GetField("baseDefense", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), Is.Null);
        }

        [Test]
        public void Catalog_RegistersBeingAndActorProfileForTypedLookup()
        {
            BeingDefinition being = CreateBeing("being.person", "Person");
            ActorProfileDefinition profile = CreateProfile("actor-profile.player-prototype", "Player", being);
            DefinitionRegistry registry = ClassificationTestFactory.CreateCatalog(being, profile).CreateRegistry();

            Assert.That(registry.TryGet("being.person", out BeingDefinition foundBeing), Is.True);
            Assert.That(foundBeing, Is.SameAs(being));
            Assert.That(registry.TryGet("actor-profile.player-prototype", out ActorProfileDefinition foundProfile), Is.True);
            Assert.That(foundProfile, Is.SameAs(profile));
            Assert.That(registry.TryGet("actor-profile.missing", out IGameDefinition missing), Is.False);
            Assert.That(missing, Is.Null);
        }

        [Test]
        public void CatalogValidation_ReportsDuplicateIdsAndMissingBeingReference()
        {
            BeingDefinition first = CreateBeing("being.person", "Person");
            BeingDefinition duplicate = CreateBeing("being.person", "Duplicate Person");
            ActorProfileDefinition missingBeing = CreateProfile("actor-profile.invalid", "Invalid", null);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(ClassificationTestFactory.CreateCatalog(first, duplicate, missingBeing));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("Duplicate definition ID 'being.person'"));
            Assert.That(report.GetSummary(), Does.Contain("missing a BeingDefinition reference"));
        }

        [Test]
        public void ActorStats_InitializesFromProfileAndPreservesRuntimeContributions()
        {
            ActorProfileDefinition profile = AssetDatabase.LoadAssetAtPath<ActorProfileDefinition>(PlayerProfilePath);
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            GameObject actor = new GameObject("Profile Actor");
            CharacterAttributes attributes = actor.AddComponent<CharacterAttributes>();
            CalculatedStatCollection calculated = actor.AddComponent<CalculatedStatCollection>();
            ActorStats stats = actor.AddComponent<ActorStats>();
            SetObject(stats, "actorProfile", profile);

            stats.ConfigureDerivedStats(registry);
            Assert.That(stats.MaximumHealth, Is.EqualTo(100f));
            Assert.That(stats.AttackPower, Is.EqualTo(5f));
            Assert.That(stats.TryInitializeBaseStats().Status, Is.EqualTo(ActorProfileInitializationStatus.AlreadyInitialized));

            RuntimeCalculatedStatContribution modifier = new RuntimeCalculatedStatContribution
            {
                contributionId = "status.test.physical-power",
                statId = CalculatedStatIds.PhysicalPower,
                sourceId = "status.test",
                sourceCategory = (int)CalculatedStatContributionSourceCategory.CombatStatus,
                kind = (int)CalculatedStatContributionKind.Flat,
                direction = (int)CalculatedStatContributionDirection.Improve,
                magnitude = 3f
            };
            Assert.That(stats.AddCalculatedStatContribution(modifier), Is.True);
            Assert.That(stats.AttackPower, Is.EqualTo(8f));
            Assert.That(stats.TryInitializeBaseStats().Status, Is.EqualTo(ActorProfileInitializationStatus.AlreadyInitialized));
            Assert.That(stats.AttackPower, Is.EqualTo(8f));
            Assert.That(attributes, Is.Not.Null);
            Assert.That(calculated.IsConfigured, Is.True);
            UnityEngine.Object.DestroyImmediate(actor);
        }

        [Test]
        public void ActorSaveData_StoresStableIdsWithoutAssetReferences()
        {
            ActorSaveData saveData = new ActorSaveData
            {
                actorProfileId = "actor-profile.player-prototype",
                beingDefinitionId = "being.person",
                personDefinitionId = "person.prototype-npc"
            };

            Assert.That(saveData.actorProfileId, Is.EqualTo("actor-profile.player-prototype"));
            Assert.That(ActorSaveRestoreOrder.ResolveBeingDefinition, Is.LessThan(ActorSaveRestoreOrder.InitializeActorStatsBaseValues));
            Assert.That(ActorSaveRestoreOrder.RestoreStatuses, Is.LessThan(ActorSaveRestoreOrder.RestoreCurrentVitals));
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog;
        }

        private static BeingDefinition CreateBeing(string id, string displayName, CategoryDefinition category = null, TagDefinition[] tags = null)
        {
            BeingDefinition being = ScriptableObject.CreateInstance<BeingDefinition>();
            SetString(being, "beingId", id);
            SetString(being, "displayName", displayName);
            SetObject(being, "primaryCategory", category);
            SetObjectArray(being, "tags", tags ?? Array.Empty<TagDefinition>());
            SetEnumValue(being, "intelligence", (int)BeingIntelligenceLevel.Sapient);
            SetEnumValue(being, "socialCapability", (int)BeingSocialCapability.Institutional);
            SetEnumValue(being, "locomotionCapabilities", 1);
            SetEnumValue(being, "nature", 1);
            return being;
        }

        private static ActorProfileDefinition CreateProfile(string id, string displayName, BeingDefinition being)
        {
            ActorProfileDefinition profile = ScriptableObject.CreateInstance<ActorProfileDefinition>();
            SetString(profile, "actorProfileId", id);
            SetString(profile, "displayName", displayName);
            SetObject(profile, "beingDefinition", being);
            return profile;
        }

        private static CategoryDefinition CreateCategory(string id, CategoryDomain domain)
        {
            CategoryDefinition category = ScriptableObject.CreateInstance<CategoryDefinition>();
            SetString(category, "categoryId", id);
            SetString(category, "displayName", id);
            SetEnumValue(category, "domain", (int)domain);
            return category;
        }

        private static TagDefinition CreateTag(string id, CategoryDomain domain)
        {
            TagDefinition tag = ScriptableObject.CreateInstance<TagDefinition>();
            SetString(tag, "tagId", id);
            SetString(tag, "displayName", id);
            SetEnumValue(tag, "domain", (int)domain);
            return tag;
        }

        private static void SetString(UnityEngine.Object target, string fieldName, string value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(UnityEngine.Object target, string fieldName, UnityEngine.Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnumValue(UnityEngine.Object target, string fieldName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
