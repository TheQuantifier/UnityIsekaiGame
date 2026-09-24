using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class GenericActorStatsCombatIntegrationTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void ActorStats_InitializesConfiguredBaseValues()
        {
            GameObject actor = CreateActor("Actor", 65f, 0f, 0f, 4f, 2f);
            object stats = actor.GetComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));

            Assert.That(Get<float>(stats, "MaximumHealth"), Is.EqualTo(65f));
            Assert.That(Get<float>(stats, "AttackPower"), Is.EqualTo(4f));
            Assert.That(Get<float>(stats, "Defense"), Is.EqualTo(2f));
            Destroy(actor);
        }

        [Test]
        public void CombatStatUtility_AddsAttackPowerWhenConfigured()
        {
            GameObject actor = CreateActor("Attacker", 10f, 0f, 0f, 4f, 0f);

            object ignore = EnumValue("UnityIsekaiGame.Combat.AttackPowerScalingPolicy", "IgnoreSourceAttackPower");
            object add = EnumValue("UnityIsekaiGame.Combat.AttackPowerScalingPolicy", "AddSourceAttackPower");

            Assert.That((float)InvokeStatic("UnityIsekaiGame.Combat.CombatStatUtility", "CalculatePreMitigationDamage", 12f, actor, ignore), Is.EqualTo(12f));
            Assert.That((float)InvokeStatic("UnityIsekaiGame.Combat.CombatStatUtility", "CalculatePreMitigationDamage", 12f, actor, add), Is.EqualTo(16f));
            Destroy(actor);
        }

        [Test]
        public void ActorStats_RejectsDuplicateModifierFromSameSourceStatOperationAndPriority()
        {
            GameObject actor = CreateActor("Actor", 10f, 0f, 0f, 4f, 0f);
            object stats = actor.GetComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));
            object source = CreateSource("StatusEffect", "status.duplicate");
            object first = CreateRuntimeModifier("AttackPower", "FlatAdd", 2f, source);
            object second = CreateRuntimeModifier("AttackPower", "FlatAdd", 3f, source);

            Assert.That((bool)Invoke(stats, "AddModifier", first), Is.True);
            Assert.That((bool)Invoke(stats, "AddModifier", second), Is.False);
            Assert.That(Get<float>(stats, "AttackPower"), Is.EqualTo(6f));
            Destroy(actor);
        }

        [Test]
        public void DamageCalculator_AppliesFlatDefenseAndMinimumDamage()
        {
            object calculation = InvokeStatic("UnityIsekaiGame.Combat.DamageCalculator", "Calculate", 12f, 5f, 1f);
            object minimum = InvokeStatic("UnityIsekaiGame.Combat.DamageCalculator", "Calculate", 2f, 8f, 1f);

            Assert.That(Get<float>(calculation, "FinalAmount"), Is.EqualTo(7f));
            Assert.That(Get<float>(calculation, "MitigatedAmount"), Is.EqualTo(5f));
            Assert.That(Get<float>(minimum, "FinalAmount"), Is.EqualTo(1f));
        }

        [Test]
        public void EnemyHealth_UsesActorStatsForMaximumHealthAndDefense()
        {
            GameObject enemy = CreateActor("Enemy", 20f, 0f, 0f, 0f, 2f);
            object health = enemy.AddComponent(RequiredType("UnityIsekaiGame.Combat.EnemyHealth"));
            InvokeNonPublic(health, "Awake");
            InvokeNonPublic(health, "OnEnable");

            object damageInfo = CreateDamageInfo(5f, null, enemy);
            object result = Invoke(health, "ApplyDamage", damageInfo);

            Assert.That(Get<bool>(result, "Applied"), Is.True);
            Assert.That(Get<float>(result, "Defense"), Is.EqualTo(2f));
            Assert.That(Get<float>(result, "AppliedAmount"), Is.EqualTo(3f));
            Assert.That(Get<float>(health, "CurrentHealth"), Is.EqualTo(17f));

            object source = CreateSource("StatusEffect", "status.max-health-test");
            object modifier = CreateRuntimeModifier("MaximumHealth", "FlatAdd", -16f, source);
            Invoke(enemy.GetComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats")), "AddModifier", modifier);

            Assert.That(Get<float>(health, "MaximumHealth"), Is.EqualTo(4f));
            Assert.That(Get<float>(health, "CurrentHealth"), Is.EqualTo(4f));
            Destroy(enemy);
        }

        [Test]
        public void EnemyHealth_RejectsInvalidDamageAndDefeatsOnce()
        {
            GameObject enemy = CreateActor("Enemy", 5f, 0f, 0f, 0f, 0f);
            object health = enemy.AddComponent(RequiredType("UnityIsekaiGame.Combat.EnemyHealth"));
            InvokeNonPublic(health, "Awake");
            InvokeNonPublic(health, "OnEnable");

            int defeatedEvents = 0;
            EventInfo defeated = health.GetType().GetEvent("Defeated");
            Action handler = () => defeatedEvents++;
            defeated.AddEventHandler(health, handler);

            object invalid = Invoke(health, "ApplyDamage", CreateDamageInfo(0f, null, enemy));
            object lethal = Invoke(health, "ApplyDamage", CreateDamageInfo(99f, null, enemy));
            object afterDefeat = Invoke(health, "ApplyDamage", CreateDamageInfo(99f, null, enemy));

            Assert.That(Get<bool>(invalid, "Applied"), Is.False);
            Assert.That(Get<bool>(lethal, "Applied"), Is.True);
            Assert.That(Get<bool>(afterDefeat, "Applied"), Is.False);
            Assert.That(defeatedEvents, Is.EqualTo(1));
            defeated.RemoveEventHandler(health, handler);
            Destroy(enemy);
        }

        [Test]
        public void DamageEffect_DefaultIgnoresSourceAttackPowerButCanOptIn()
        {
            GameObject source = CreateActor("Source", 10f, 0f, 0f, 5f, 0f);
            GameObject target = CreateActor("Target", 50f, 0f, 0f, 0f, 1f);
            object health = target.AddComponent(RequiredType("UnityIsekaiGame.Combat.EnemyHealth"));
            InvokeNonPublic(health, "Awake");
            InvokeNonPublic(health, "OnEnable");

            ScriptableObject effect = CreateDamageEffect(10f, "IgnoreSourceAttackPower");
            object result = Invoke(effect, "Execute", CreateEffectContext(null, source, target));

            Assert.That(Get<float>(result, "AppliedMagnitude"), Is.EqualTo(9f));

            Invoke(health, "ResetToMaximum");
            SetEnum(effect, "attackPowerScaling", "UnityIsekaiGame.Combat.AttackPowerScalingPolicy", "AddSourceAttackPower");
            object scaledResult = Invoke(effect, "Execute", CreateEffectContext(null, source, target));

            Assert.That(Get<float>(scaledResult, "AppliedMagnitude"), Is.EqualTo(14f));
            Destroy(source);
            Destroy(target);
            UnityEngine.Object.DestroyImmediate(effect);
        }

        [Test]
        public void DamageEffect_AddSourceAttackPowerRequiresSourceStats()
        {
            GameObject source = new GameObject("Source Without Stats");
            GameObject target = CreateActor("Target", 50f, 0f, 0f, 0f, 0f);
            object health = target.AddComponent(RequiredType("UnityIsekaiGame.Combat.EnemyHealth"));
            InvokeNonPublic(health, "Awake");
            InvokeNonPublic(health, "OnEnable");

            ScriptableObject effect = CreateDamageEffect(10f, "AddSourceAttackPower");
            object result = Invoke(effect, "CanExecute", CreateEffectContext(null, source, target));

            Assert.That(Get<bool>(result, "Succeeded"), Is.False);
            Destroy(source);
            Destroy(target);
            UnityEngine.Object.DestroyImmediate(effect);
        }

        [Test]
        public void StatusModifier_ChangesEnemyDefenseAndExpirationRestoresCombatOutput()
        {
            GameObject enemy = CreateActor("Enemy", 50f, 0f, 0f, 0f, 5f);
            object health = enemy.AddComponent(RequiredType("UnityIsekaiGame.Combat.EnemyHealth"));
            InvokeNonPublic(health, "Awake");
            InvokeNonPublic(health, "OnEnable");
            object controller = enemy.AddComponent(RequiredType("UnityIsekaiGame.StatusEffects.StatusEffectController"));
            InvokeNonPublic(controller, "Awake");

            ScriptableObject status = CreateStatus("status.test-weaken", "Weaken", 1f, "Defense", "FlatAdd", -3f);
            object applyResult = Invoke(controller, "ApplyStatus", CreateRequest(status, enemy, "source.status", "app-status", 0f, 0f));

            Assert.That(Get<bool>(applyResult, "Succeeded"), Is.True);
            object weakened = Invoke(health, "ApplyDamage", CreateDamageInfo(10f, null, enemy));
            Assert.That(Get<float>(weakened, "Defense"), Is.EqualTo(2f));
            Assert.That(Get<float>(weakened, "AppliedAmount"), Is.EqualTo(8f));

            Invoke(controller, "UpdateStatuses", 1.1f);
            object restored = Invoke(health, "ApplyDamage", CreateDamageInfo(10f, null, enemy));
            Assert.That(Get<float>(restored, "Defense"), Is.EqualTo(5f));
            Assert.That(Get<float>(restored, "AppliedAmount"), Is.EqualTo(5f));

            Destroy(enemy);
            UnityEngine.Object.DestroyImmediate(status);
        }

        private static GameObject CreateActor(string name, float maxHealth, float maxStamina, float maxMana, float attackPower, float defense)
        {
            GameObject actor = new GameObject(name);
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            CharacterAttributes attributes = actor.AddComponent<CharacterAttributes>();
            CalculatedStatCollection calculatedStats = actor.AddComponent<CalculatedStatCollection>();
            CharacterResourceCollection resources = actor.AddComponent<CharacterResourceCollection>();
            WorldEntityIdentity identity = actor.AddComponent<WorldEntityIdentity>();
            Assert.That(identity.TrySetAuthoredIdentity($"generic-actor-{Guid.NewGuid():N}", "scene.test", PersistenceScope.RegionOrScene, "test.generic-actor", out string failure), Is.True, failure);
            ActorStats actorStats = actor.AddComponent<ActorStats>();

            attributes.Configure(registry);
            calculatedStats.Configure(registry, attributes);
            SetCalculatedStat(calculatedStats, CalculatedStatIds.MaximumHealth, maxHealth, $"{name}.health");
            SetCalculatedStat(calculatedStats, CalculatedStatIds.MaximumStamina, maxStamina, $"{name}.stamina");
            SetCalculatedStat(calculatedStats, CalculatedStatIds.MaximumMana, maxMana, $"{name}.mana");
            SetCalculatedStat(calculatedStats, CalculatedStatIds.PhysicalPower, attackPower, $"{name}.power");
            SetCalculatedStat(calculatedStats, CalculatedStatIds.PhysicalDefense, defense, $"{name}.defense");
            resources.Configure(registry, calculatedStats, identity.EntityId);
            InvokeNonPublic(actorStats, "Awake");
            InvokeNonPublic(actorStats, "OnEnable");
            return actor;
        }

        private static ScriptableObject CreateDamageEffect(float baseAmount, string attackPowerScaling)
        {
            ScriptableObject effect = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Abilities.DamageEffectDefinition"));
            SerializedObject serialized = new SerializedObject(effect);
            serialized.FindProperty("effectId").stringValue = "effect.test-damage";
            serialized.FindProperty("displayName").stringValue = "Test Damage";
            serialized.FindProperty("baseAmount").floatValue = baseAmount;
            serialized.FindProperty("typedDamageType").objectReferenceValue = GetDamageType();
            serialized.FindProperty("attackPowerScaling").enumValueIndex = EnumIndex("UnityIsekaiGame.Combat.AttackPowerScalingPolicy", attackPowerScaling);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        private static ScriptableObject CreateStatus(string id, string displayName, float duration, string stat, string operation, float value)
        {
            ScriptableObject status = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.StatusEffects.StatusEffectDefinition"));
            SerializedObject serialized = new SerializedObject(status);
            serialized.FindProperty("statusId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("durationModel").enumValueIndex = EnumIndex("UnityIsekaiGame.StatusEffects.StatusDurationModel", "Timed");
            serialized.FindProperty("defaultDuration").floatValue = duration;
            serialized.FindProperty("stackingPolicy").enumValueIndex = EnumIndex("UnityIsekaiGame.StatusEffects.StatusStackingPolicy", "RefreshDuration");
            serialized.FindProperty("refreshPolicy").enumValueIndex = EnumIndex("UnityIsekaiGame.StatusEffects.StatusRefreshPolicy", "ResetToFullDuration");
            serialized.FindProperty("maximumStacks").intValue = 1;
            SerializedProperty modifiers = serialized.FindProperty("statModifiers");
            modifiers.arraySize = 1;
            SerializedProperty modifier = modifiers.GetArrayElementAtIndex(0);
            modifier.FindPropertyRelative("statType").enumValueIndex = EnumIndex("UnityIsekaiGame.Stats.StatType", stat);
            modifier.FindPropertyRelative("operation").enumValueIndex = EnumIndex("UnityIsekaiGame.Stats.StatModifierOperation", operation);
            modifier.FindPropertyRelative("value").floatValue = value;
            modifier.FindPropertyRelative("scaleWithStacks").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return status;
        }

        private static object CreateRequest(ScriptableObject status, GameObject target, string sourceId, string applicationId, float durationOverride, float now)
        {
            return Activator.CreateInstance(
                RequiredType("UnityIsekaiGame.StatusEffects.StatusEffectApplicationRequest"),
                status, target, sourceId, durationOverride, applicationId, now);
        }

        private static object CreateEffectContext(ScriptableObject ability, GameObject source, GameObject target)
        {
            return Activator.CreateInstance(
                RequiredType("UnityIsekaiGame.Abilities.EffectExecutionContext"),
                ability, source, target, source.transform.position, target.transform.position, source.transform.forward, null, null, 1f);
        }

        private static object CreateDamageInfo(float amount, GameObject source, GameObject target)
        {
            DamagePacket packet = DamagePacket.Single(source, new DamageComponent(GetDamageType(), amount));
            return new DamageInfo(packet, target == null ? Vector3.zero : target.transform.position, Vector3.forward);
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }

        private static DamageTypeDefinition GetDamageType()
        {
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            Assert.That(registry.TryGet("damage.physical", out DamageTypeDefinition damageType), Is.True);
            return damageType;
        }

        private static void SetCalculatedStat(CalculatedStatCollection stats, string statId, float desiredValue, string sourceId)
        {
            float delta = desiredValue - stats.GetValue(statId);
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            RuntimeCalculatedStatContribution contribution = new RuntimeCalculatedStatContribution
            {
                contributionId = sourceId,
                statId = statId,
                sourceId = sourceId,
                sourceCategory = (int)CalculatedStatContributionSourceCategory.Development,
                kind = (int)CalculatedStatContributionKind.Flat,
                direction = (int)(delta >= 0f ? CalculatedStatContributionDirection.Improve : CalculatedStatContributionDirection.Reduce),
                magnitude = Mathf.Abs(delta)
            };
            Assert.That(stats.AddContribution(contribution, out string failure), Is.True, failure);
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

        private static object EnumValue(string fullName, string name)
        {
            return Enum.Parse(RequiredType(fullName), name);
        }

        private static int EnumIndex(string fullName, string name)
        {
            return Convert.ToInt32(EnumValue(fullName, name));
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

        private static object InvokeStatic(string typeName, string methodName, params object[] args)
        {
            return RequiredType(typeName).GetMethod(methodName).Invoke(null, args);
        }

        private static void InvokeNonPublic(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, Array.Empty<object>());
        }

        private static T Get<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static void SetObjectReference(object target, string fieldName, Component value)
        {
            SerializedObject serialized = new SerializedObject((UnityEngine.Object)target);
            serialized.FindProperty(fieldName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(ScriptableObject target, string fieldName, string enumType, string enumValue)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).enumValueIndex = EnumIndex(enumType, enumValue);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Destroy(GameObject gameObject)
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
