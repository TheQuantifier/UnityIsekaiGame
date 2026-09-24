using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Tests
{
    public sealed class BeingActorProfileFoundationTests
    {
        [Test]
        public void BeingDefinition_ExposesStableClassificationMetadata()
        {
            CategoryDefinition category = CreateCategory("category.being.person", CategoryDomain.Being);
            TagDefinition humanoid = CreateTag("tag.being.humanoid", CategoryDomain.Being);
            ScriptableObject being = CreateBeing("being.person", "Person", category, new[] { humanoid });

            Assert.That(Get<string>(being, "Id"), Is.EqualTo("being.person"));
            Assert.That(Get<object>(being, "Intelligence").ToString(), Is.EqualTo("Sapient"));
            Assert.That(Get<object>(being, "SocialCapability").ToString(), Is.EqualTo("Institutional"));
            Assert.That(Convert.ToInt32(Get<object>(being, "LocomotionCapabilities")), Is.EqualTo(1));
            Assert.That(ClassificationUtility.IsInCategory((ICategorizableDefinition)being, "category.being.person"), Is.True);
            Assert.That(ClassificationUtility.HasTag((ITaggedDefinition)being, "tag.being.humanoid"), Is.True);
        }

        [Test]
        public void ActorProfileDefinition_ExposesBeingAndBaseStats()
        {
            ScriptableObject being = CreateBeing("being.prototype-enemy", "Prototype Enemy");
            ScriptableObject profile = CreateProfile("actor-profile.enemy-prototype", "Enemy", being, 65f, 0f, 0f, 0f, 1f, 1.8f);

            Assert.That(Get<string>(profile, "Id"), Is.EqualTo("actor-profile.enemy-prototype"));
            Assert.That(Get<UnityEngine.Object>(profile, "BeingDefinition"), Is.SameAs(being));
            Assert.That(Get<float>(profile, "BaseMaximumHealth"), Is.EqualTo(65f));
            Assert.That(Get<float>(profile, "BaseDefense"), Is.EqualTo(1f));
            Assert.That(Get<float>(profile, "BaseMovementSpeed"), Is.EqualTo(1.8f));
            Assert.That(Get<bool>(profile, "HasValidBaseStats"), Is.True);
        }

        [Test]
        public void Catalog_RegistersBeingAndActorProfileForTypedLookup()
        {
            ScriptableObject being = CreateBeing("being.person", "Person");
            ScriptableObject profile = CreateProfile("actor-profile.player-prototype", "Player", being, 100f, 100f, 100f, 5f, 0f, 0f);
            DefinitionCatalog catalog = CreateCatalog(being, profile);
            DefinitionRegistry registry = catalog.CreateRegistry();

            Assert.That(registry.TryGet("being.person", out IGameDefinition foundBeing), Is.True);
            Assert.That(foundBeing, Is.SameAs(being));
            Assert.That(registry.TryGet("actor-profile.player-prototype", out IGameDefinition foundProfile), Is.True);
            Assert.That(foundProfile, Is.SameAs(profile));
            Assert.That(foundProfile.GetType(), Is.Not.EqualTo(RequiredType("UnityIsekaiGame.Beings.BeingDefinition")));
            Assert.That(registry.TryGet("actor-profile.missing", out IGameDefinition missing), Is.False);
            Assert.That(missing, Is.Null);
        }

        [Test]
        public void CatalogValidation_ReportsDuplicateIdsAndMissingBeingReference()
        {
            ScriptableObject first = CreateBeing("being.person", "Person");
            ScriptableObject duplicate = CreateBeing("being.person", "Duplicate Person");
            ScriptableObject missingBeing = CreateProfile("actor-profile.invalid", "Invalid", null, 100f, 0f, 0f, 0f, 0f, 0f);
            DefinitionCatalog catalog = CreateCatalog(first, duplicate, missingBeing);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("Duplicate definition ID 'being.person'"));
            Assert.That(report.GetSummary(), Does.Contain("missing a BeingDefinition reference"));
        }

        [Test]
        public void CatalogValidation_ReportsInvalidActorProfileBaseStats()
        {
            ScriptableObject being = CreateBeing("being.person", "Person");
            ScriptableObject profile = CreateProfile("actor-profile.invalid", "Invalid", being, 100f, 0f, 0f, 0f, 0f, 0f);
            SetPrivateFloat(profile, "baseMaximumHealth", -1f);
            SetPrivateFloat(profile, "baseDefense", float.NaN);
            DefinitionCatalog catalog = CreateCatalog(being, profile);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("base maximum health"));
            Assert.That(report.GetSummary(), Does.Contain("base defense"));
        }

        [Test]
        public void ActorStats_InitializesFromProfileWithProfilePrecedence()
        {
            ScriptableObject profile = CreateProfile("actor-profile.player-prototype", "Player", CreateBeing("being.person", "Person"), 100f, 100f, 100f, 5f, 0f, 0f);
            GameObject actor = new GameObject("Profile Actor");
            Component stats = actor.AddComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));
            SetObject(stats, "actorProfile", profile);
            SetFloat(stats, "baseMaximumHealth", 25f);
            SetFloat(stats, "baseAttackPower", 1f);

            object result = Invoke(stats, "TryInitializeBaseStats");

            Assert.That(Get<object>(result, "Status").ToString(), Is.EqualTo("InitializedFromProfile"));
            Assert.That(Get<float>(stats, "MaximumHealth"), Is.EqualTo(100f));
            Assert.That(Get<float>(stats, "AttackPower"), Is.EqualTo(5f));
            Assert.That(Get<bool>(stats, "HasProfileLegacyConflict"), Is.True);
            UnityEngine.Object.DestroyImmediate(actor);
        }

        [Test]
        public void ActorStats_UsesLegacyFallbackWhenNoProfileIsAssigned()
        {
            GameObject actor = new GameObject("Legacy Actor");
            Component stats = actor.AddComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));
            SetFloat(stats, "baseMaximumHealth", 65f);
            SetFloat(stats, "baseDefense", 1f);

            object result = Invoke(stats, "TryInitializeBaseStats");

            Assert.That(Get<object>(result, "Status").ToString(), Is.EqualTo("InitializedFromLegacyFallback"));
            Assert.That(Get<float>(stats, "MaximumHealth"), Is.EqualTo(65f));
            Assert.That(Get<float>(stats, "Defense"), Is.EqualTo(1f));
            UnityEngine.Object.DestroyImmediate(actor);
        }

        [Test]
        public void ActorStats_RepeatedInitializationDoesNotResetRuntimeModifiers()
        {
            ScriptableObject profile = CreateProfile("actor-profile.player-prototype", "Player", CreateBeing("being.person", "Person"), 100f, 0f, 0f, 5f, 0f, 0f);
            GameObject actor = new GameObject("Modified Actor");
            Component stats = actor.AddComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));
            SetObject(stats, "actorProfile", profile);
            Assert.That(Get<object>(Invoke(stats, "TryInitializeBaseStats"), "Status").ToString(), Is.EqualTo("InitializedFromProfile"));

            object source = CreateSource("StatusEffect", "status.test");
            object modifier = CreateRuntimeModifier("AttackPower", "FlatAdd", 3f, source);
            Assert.That((bool)Invoke(stats, "AddModifier", modifier), Is.True);
            object repeated = Invoke(stats, "TryInitializeBaseStats");

            Assert.That(Get<object>(repeated, "Status").ToString(), Is.EqualTo("AlreadyInitialized"));
            Assert.That(Get<float>(stats, "AttackPower"), Is.EqualTo(8f));
            UnityEngine.Object.DestroyImmediate(actor);
        }

        [Test]
        public void PersonDefinition_CanReferenceBeingWithoutActorProfile()
        {
            ScriptableObject being = CreateBeing("being.person", "Person");
            ScriptableObject person = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.People.PersonDefinition"));
            SetString(person, "personId", "person.prototype-npc");
            SetString(person, "displayName", "Prototype NPC");
            SetObject(person, "beingDefinition", being);
            DefinitionCatalog catalog = CreateCatalog(being, person);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(Get<UnityEngine.Object>(person, "BeingDefinition"), Is.SameAs(being));
            Assert.That(Get<UnityEngine.Object>(person, "ActorProfile"), Is.Null);
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
        }

        [Test]
        public void ActorSaveData_StoresStableIdsWithoutAssetReferences()
        {
            Type saveDataType = RequiredType("UnityIsekaiGame.Beings.ActorSaveData");
            object saveData = Activator.CreateInstance(saveDataType);
            saveDataType.GetField("actorProfileId").SetValue(saveData, "actor-profile.player-prototype");
            saveDataType.GetField("beingDefinitionId").SetValue(saveData, "being.person");
            saveDataType.GetField("personDefinitionId").SetValue(saveData, "person.prototype-npc");

            Assert.That(saveDataType.GetField("actorProfileId").GetValue(saveData), Is.EqualTo("actor-profile.player-prototype"));
            Assert.That(Convert.ToInt32(EnumValue("UnityIsekaiGame.Beings.ActorSaveRestoreOrder", "ResolveBeingDefinition")), Is.LessThan(Convert.ToInt32(EnumValue("UnityIsekaiGame.Beings.ActorSaveRestoreOrder", "InitializeActorStatsBaseValues"))));
            Assert.That(Convert.ToInt32(EnumValue("UnityIsekaiGame.Beings.ActorSaveRestoreOrder", "RestoreStatuses")), Is.LessThan(Convert.ToInt32(EnumValue("UnityIsekaiGame.Beings.ActorSaveRestoreOrder", "RestoreCurrentVitals"))));
        }

        [Test]
        public void ProfileAsset_RemainsImmutableWhenRuntimeModifiersApply()
        {
            ScriptableObject profile = CreateProfile("actor-profile.enemy-prototype", "Enemy", CreateBeing("being.prototype-enemy", "Enemy"), 65f, 0f, 0f, 0f, 1f, 1.8f);
            GameObject actor = new GameObject("Enemy");
            Component stats = actor.AddComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));
            SetObject(stats, "actorProfile", profile);
            Invoke(stats, "TryInitializeBaseStats");

            object source = CreateSource("StatusEffect", "status.weakened");
            object modifier = CreateRuntimeModifier("Defense", "FlatAdd", 2f, source);
            Assert.That((bool)Invoke(stats, "AddModifier", modifier), Is.True);

            Assert.That(Get<float>(stats, "Defense"), Is.EqualTo(3f));
            Assert.That(Get<float>(profile, "BaseDefense"), Is.EqualTo(1f));
            UnityEngine.Object.DestroyImmediate(actor);
        }

        private static ScriptableObject CreateBeing(string id, string displayName, CategoryDefinition category = null, TagDefinition[] tags = null)
        {
            ScriptableObject being = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Beings.BeingDefinition"));
            SetString(being, "beingId", id);
            SetString(being, "displayName", displayName);
            SetObject(being, "primaryCategory", category);
            SetObjectArray(being, "tags", tags ?? Array.Empty<TagDefinition>());
            SetEnum(being, "intelligence", "UnityIsekaiGame.Beings.BeingIntelligenceLevel", "Sapient");
            SetEnum(being, "socialCapability", "UnityIsekaiGame.Beings.BeingSocialCapability", "Institutional");
            SetEnumValue(being, "locomotionCapabilities", 1);
            SetEnumValue(being, "nature", 1);
            return being;
        }

        private static ScriptableObject CreateProfile(
            string id,
            string displayName,
            ScriptableObject being,
            float maximumHealth,
            float maximumStamina,
            float maximumMana,
            float attackPower,
            float defense,
            float movementSpeed)
        {
            ScriptableObject profile = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Beings.ActorProfileDefinition"));
            SetString(profile, "actorProfileId", id);
            SetString(profile, "displayName", displayName);
            SetObject(profile, "beingDefinition", being);
            SetFloat(profile, "baseMaximumHealth", maximumHealth);
            SetFloat(profile, "baseMaximumStamina", maximumStamina);
            SetFloat(profile, "baseMaximumMana", maximumMana);
            SetFloat(profile, "baseAttackPower", attackPower);
            SetFloat(profile, "baseDefense", defense);
            SetFloat(profile, "baseMovementSpeed", movementSpeed);
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

        private static DefinitionCatalog CreateCatalog(params ScriptableObject[] definitions)
        {
            return ClassificationTestFactory.CreateCatalog(definitions);
        }

        private static object CreateSource(string sourceType, string sourceId)
        {
            return Activator.CreateInstance(
                RequiredType("UnityIsekaiGame.Stats.StatModifierSource"),
                EnumValue("UnityIsekaiGame.Stats.StatModifierSourceType", sourceType),
                sourceId);
        }

        private static object CreateRuntimeModifier(string stat, string operation, float value, object source)
        {
            return Activator.CreateInstance(
                RequiredType("UnityIsekaiGame.Stats.RuntimeStatModifier"),
                EnumValue("UnityIsekaiGame.Stats.StatType", stat),
                EnumValue("UnityIsekaiGame.Stats.StatModifierOperation", operation),
                value,
                source,
                0);
        }

        private static void SetString(UnityEngine.Object target, string fieldName, string value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string fieldName, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateFloat(object target, string fieldName, float value)
        {
            target.GetType()
                .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);
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
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(UnityEngine.Object target, string fieldName, string fullName, string enumValue)
        {
            SetEnumValue(target, fieldName, Convert.ToInt32(EnumValue(fullName, enumValue)));
        }

        private static void SetEnumValue(UnityEngine.Object target, string fieldName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            property.enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Get<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            return target.GetType().GetMethod(methodName).Invoke(target, args);
        }

        private static object EnumValue(string fullName, string name)
        {
            return Enum.Parse(RequiredType(fullName), name);
        }

        private static Type RequiredType(string fullName)
        {
            Type type = TestTypeResolver.RequiredType(fullName);
            Assert.That(type, Is.Not.Null, $"Expected runtime type {fullName} to exist in loaded project assemblies.");
            return type;
        }
    }
}
