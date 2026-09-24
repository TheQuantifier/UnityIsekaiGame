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
    public sealed class DamageTypeResistanceFoundationTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void DamageTypeDefinition_RegistersAndRejectsSelfParent()
        {
            ScriptableObject physical = CreateDamageType("damage.physical", "Physical", null, true);
            ScriptableObject slashing = CreateDamageType("damage.physical.slashing", "Slashing", physical, true);

            DefinitionValidationReport valid = DefinitionCatalogValidator.Validate(ClassificationTestFactory.CreateCatalog(physical, slashing));
            Assert.That(valid.HasErrors, Is.False, valid.GetSummary());

            SetObject(slashing, "parentDamageType", slashing);
            DefinitionValidationReport invalid = DefinitionCatalogValidator.Validate(ClassificationTestFactory.CreateCatalog(physical, slashing));
            Assert.That(invalid.HasErrors, Is.True);
            Assert.That(invalid.GetSummary(), Does.Contain("cannot be its own parent"));
        }

        [Test]
        public void DamageTypeDefinition_DetectsCircularParentChain()
        {
            ScriptableObject physical = CreateDamageType("damage.physical", "Physical", null, true);
            ScriptableObject slashing = CreateDamageType("damage.physical.slashing", "Slashing", physical, true);
            SetObject(physical, "parentDamageType", slashing);

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(ClassificationTestFactory.CreateCatalog(physical, slashing));
            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.GetSummary(), Does.Contain("circular parent hierarchy"));
        }

        [Test]
        public void RuntimeResistanceCollection_ComposesExactAndAncestorResistance()
        {
            ScriptableObject physical = CreateDamageType("damage.physical", "Physical", null, true);
            ScriptableObject slashing = CreateDamageType("damage.physical.slashing", "Slashing", physical, true);
            object resistances = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Combat.RuntimeResistanceCollection"));

            Invoke(resistances, "SetBaseResistance", physical, 0.2f);
            Assert.That((float)Invoke(resistances, "GetEffectiveResistance", slashing), Is.EqualTo(0.2f).Within(0.001f));

            Invoke(resistances, "SetBaseResistance", slashing, -0.25f);
            Assert.That((float)Invoke(resistances, "GetEffectiveResistance", slashing), Is.EqualTo(-0.05f).Within(0.001f));
            Assert.That((float)Invoke(resistances, "GetEffectiveResistance", physical), Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test]
        public void DamageCalculator_AppliesDefenseBeforeTypedResistanceAndWeakness()
        {
            ScriptableObject slashing = CreateDamageType("damage.physical.slashing", "Slashing", null, true);
            GameObject actor = CreateActor("Target", 50f, 0f);
            object stats = actor.GetComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));
            Invoke(stats, "AddResistanceModifier", CreateResistanceModifier(slashing, 0.25f, "Debug", "test.resistance"));

            object packet = CreatePacket(CreateComponent(slashing, 20f));
            object calculation = InvokeStatic("UnityIsekaiGame.Combat.DamageCalculator", "CalculatePacket", packet, 4f, stats);
            object firstComponent = GetComponentResult(calculation, 0);

            Assert.That(Get<float>(calculation, "PreMitigationAmount"), Is.EqualTo(20f));
            Assert.That(Get<float>(firstComponent, "DefenseMitigation"), Is.EqualTo(4f));
            Assert.That(Get<float>(firstComponent, "ResistanceMitigation"), Is.EqualTo(4f));
            Assert.That(Get<float>(calculation, "FinalAmount"), Is.EqualTo(12f));

            Invoke(stats, "RemoveResistanceModifiersFromSource", CreateSource("Debug", "test.resistance"));
            Invoke(stats, "AddResistanceModifier", CreateResistanceModifier(slashing, -0.25f, "Debug", "test.weakness"));
            object weakness = InvokeStatic("UnityIsekaiGame.Combat.DamageCalculator", "CalculatePacket", packet, 4f, stats);
            object weaknessComponent = GetComponentResult(weakness, 0);
            Assert.That(Get<float>(weaknessComponent, "WeaknessAmplification"), Is.EqualTo(4f));
            Assert.That(Get<float>(weakness, "FinalAmount"), Is.EqualTo(20f));

            UnityEngine.Object.DestroyImmediate(actor);
        }

        [Test]
        public void DamageCalculator_ImmunityOverridesMinimumDamage()
        {
            ScriptableObject arcane = CreateDamageType("damage.magic.arcane", "Arcane", null, false);
            GameObject actor = CreateActor("Target", 50f, 0f);
            object stats = actor.GetComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));
            Invoke(stats, "AddResistanceModifier", CreateResistanceModifier(arcane, 1f, "Debug", "test.immunity"));

            object calculation = InvokeStatic("UnityIsekaiGame.Combat.DamageCalculator", "CalculatePacket", CreatePacket(CreateComponent(arcane, 5f)), 99f, stats);
            object component = GetComponentResult(calculation, 0);

            Assert.That(Get<float>(calculation, "FinalAmount"), Is.EqualTo(0f));
            Assert.That(Get<bool>(component, "Immune"), Is.True);

            UnityEngine.Object.DestroyImmediate(actor);
        }

        [Test]
        public void DamageCalculator_MultiComponentPacketReportsTotals()
        {
            ScriptableObject slashing = CreateDamageType("damage.physical.slashing", "Slashing", null, true);
            ScriptableObject arcane = CreateDamageType("damage.magic.arcane", "Arcane", null, false);
            GameObject actor = CreateActor("Target", 50f, 0f);
            object stats = actor.GetComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));
            Invoke(stats, "AddResistanceModifier", CreateResistanceModifier(arcane, 0.5f, "Debug", "test.arcane-resistance"));

            object packet = CreatePacket(CreateComponent(slashing, 10f), CreateComponent(arcane, 10f));
            object calculation = InvokeStatic("UnityIsekaiGame.Combat.DamageCalculator", "CalculatePacket", packet, 2f, stats);

            Assert.That(Get<int>(Get<object>(calculation, "ComponentResults"), "Count"), Is.EqualTo(2));
            Assert.That(Get<float>(calculation, "FinalAmount"), Is.EqualTo(13f));
            Assert.That(Get<float>(calculation, "ResistanceMitigation"), Is.EqualTo(5f));

            UnityEngine.Object.DestroyImmediate(actor);
        }

        [Test]
        public void StatusResistanceModifier_ExpiresWithoutRemovingEquipmentResistance()
        {
            ScriptableObject arcane = CreateDamageType("damage.magic.arcane", "Arcane", null, false);
            GameObject actor = CreateActor("Actor", 50f, 0f);
            object stats = actor.GetComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));
            Invoke(stats, "AddResistanceModifier", CreateResistanceModifier(arcane, 0.1f, "Equipment", "equipment.slot.Head"));
            Component controller = actor.AddComponent(RequiredType("UnityIsekaiGame.StatusEffects.StatusEffectController"));
            InvokeNonPublic(controller, "Awake");
            ScriptableObject status = CreateResistanceStatus("status.arcane-resistant", arcane, 0.25f, 1f);

            object result = Invoke(controller, "ApplyStatus", CreateStatusRequest(status, actor, "status-source", "app-1", 0f, 0f));
            Assert.That(Get<bool>(result, "Succeeded"), Is.True);
            Assert.That((float)Invoke(stats, "GetEffectiveResistance", arcane), Is.EqualTo(0.35f).Within(0.001f));

            Invoke(controller, "UpdateStatuses", 1.1f);
            Assert.That((float)Invoke(stats, "GetEffectiveResistance", arcane), Is.EqualTo(0.1f).Within(0.001f));

            UnityEngine.Object.DestroyImmediate(status);
            UnityEngine.Object.DestroyImmediate(actor);
        }

        [Test]
        public void DamageEffect_TypedArcaneCanBypassGeneralDefense()
        {
            ScriptableObject arcane = CreateDamageType("damage.magic.arcane", "Arcane", null, false);
            GameObject source = CreateActor("Source", 50f, 0f);
            GameObject target = CreateActor("Target", 50f, 5f);
            Component health = target.AddComponent(RequiredType("UnityIsekaiGame.Combat.EnemyHealth"));

            ScriptableObject effect = CreateDamageEffect("effect.arcane", 10f, arcane);
            object context = CreateEffectContext(null, source, target);
            object result = Invoke(effect, "Execute", context);

            Assert.That(Get<bool>(result, "Succeeded"), Is.True);
            Assert.That(Get<float>(result, "AppliedMagnitude"), Is.EqualTo(10f));
            Assert.That(Get<float>(health, "CurrentHealth"), Is.EqualTo(40f));

            UnityEngine.Object.DestroyImmediate(effect);
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static GameObject CreateActor(string name, float maximumHealth, float defense)
        {
            GameObject actor = new GameObject(name);
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            CharacterAttributes attributes = actor.AddComponent<CharacterAttributes>();
            CalculatedStatCollection calculatedStats = actor.AddComponent<CalculatedStatCollection>();
            CharacterResourceCollection resources = actor.AddComponent<CharacterResourceCollection>();
            WorldEntityIdentity identity = actor.AddComponent<WorldEntityIdentity>();
            Assert.That(identity.TrySetAuthoredIdentity($"damage-type-{Guid.NewGuid():N}", "scene.test", PersistenceScope.RegionOrScene, "test.damage-type", out string identityFailure), Is.True, identityFailure);
            actor.AddComponent<ActorStats>();
            attributes.Configure(registry);
            calculatedStats.Configure(registry, attributes);
            SetCalculatedStat(calculatedStats, CalculatedStatIds.MaximumHealth, maximumHealth, $"{name}.health");
            SetCalculatedStat(calculatedStats, CalculatedStatIds.PhysicalDefense, defense, $"{name}.defense");
            SetCalculatedStat(calculatedStats, CalculatedStatIds.MagicalDefense, defense, $"{name}.magical-defense");
            resources.Configure(registry, calculatedStats, identity.EntityId);
            return actor;
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }

        private static void SetCalculatedStat(CalculatedStatCollection stats, string statId, float desiredValue, string sourceId)
        {
            float delta = desiredValue - stats.GetValue(statId);
            if (Mathf.Approximately(delta, 0f)) return;
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

        private static ScriptableObject CreateDamageType(string id, string displayName, UnityEngine.Object parent, bool defenseApplies)
        {
            ScriptableObject damageType = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Combat.DamageTypeDefinition"));
            SerializedObject serialized = new SerializedObject(damageType);
            serialized.FindProperty("damageTypeId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("parentDamageType").objectReferenceValue = parent;
            serialized.FindProperty("generalDefenseApplies").boolValue = defenseApplies;
            serialized.FindProperty("enforceMinimumDamage").boolValue = true;
            serialized.FindProperty("minimumDamage").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return damageType;
        }

        private static ScriptableObject CreateResistanceStatus(string id, UnityEngine.Object damageType, float resistance, float duration)
        {
            ScriptableObject status = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.StatusEffects.StatusEffectDefinition"));
            SerializedObject serialized = new SerializedObject(status);
            serialized.FindProperty("statusId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("durationModel").enumValueIndex = EnumIndex("UnityIsekaiGame.StatusEffects.StatusDurationModel", "Timed");
            serialized.FindProperty("defaultDuration").floatValue = duration;
            SerializedProperty modifiers = serialized.FindProperty("resistanceModifiers");
            modifiers.arraySize = 1;
            SerializedProperty modifier = modifiers.GetArrayElementAtIndex(0);
            modifier.FindPropertyRelative("damageType").objectReferenceValue = damageType;
            modifier.FindPropertyRelative("resistance").floatValue = resistance;
            modifier.FindPropertyRelative("scaleWithStacks").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return status;
        }

        private static ScriptableObject CreateDamageEffect(string id, float amount, UnityEngine.Object typedDamageType)
        {
            ScriptableObject effect = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Abilities.DamageEffectDefinition"));
            SerializedObject serialized = new SerializedObject(effect);
            serialized.FindProperty("effectId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("baseAmount").floatValue = amount;
            serialized.FindProperty("typedDamageType").objectReferenceValue = typedDamageType;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        private static object CreateComponent(UnityEngine.Object damageType, float amount)
        {
            return Activator.CreateInstance(
                RequiredType("UnityIsekaiGame.Combat.DamageComponent"),
                damageType,
                amount,
                EnumValue("UnityIsekaiGame.Combat.AttackPowerScalingPolicy", "IgnoreSourceAttackPower"));
        }

        private static object CreatePacket(params object[] componentValues)
        {
            Type componentType = RequiredType("UnityIsekaiGame.Combat.DamageComponent");
            Array components = Array.CreateInstance(componentType, componentValues.Length);
            for (int i = 0; i < componentValues.Length; i++)
            {
                components.SetValue(componentValues[i], i);
            }

            return Activator.CreateInstance(
                RequiredType("UnityIsekaiGame.Combat.DamagePacket"),
                null,
                null,
                null,
                components);
        }

        private static object CreateResistanceModifier(UnityEngine.Object damageType, float value, string sourceType, string sourceId)
        {
            return Activator.CreateInstance(
                RequiredType("UnityIsekaiGame.Combat.RuntimeResistanceModifier"),
                damageType,
                value,
                CreateSource(sourceType, sourceId),
                0);
        }

        private static object CreateSource(string sourceType, string sourceId)
        {
            return Activator.CreateInstance(
                RequiredType("UnityIsekaiGame.Stats.StatModifierSource"),
                EnumValue("UnityIsekaiGame.Stats.StatModifierSourceType", sourceType),
                sourceId);
        }

        private static object CreateStatusRequest(ScriptableObject status, GameObject target, string sourceId, string applicationId, float durationOverride, float now)
        {
            return Activator.CreateInstance(
                RequiredType("UnityIsekaiGame.StatusEffects.StatusEffectApplicationRequest"),
                status, target, sourceId, durationOverride, applicationId, now);
        }

        private static object CreateEffectContext(ScriptableObject ability, GameObject source, GameObject target)
        {
            return Activator.CreateInstance(
                RequiredType("UnityIsekaiGame.Abilities.EffectExecutionContext"),
                ability, source, target, source.transform.position, target.transform.position, Vector3.forward, null, null, 1f);
        }

        private static object GetComponentResult(object calculation, int index)
        {
            object results = Get<object>(calculation, "ComponentResults");
            return results.GetType().GetProperty("Item").GetValue(results, new object[] { index });
        }

        private static void SetObject(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
            foreach (MethodInfo method in RequiredType(typeName).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name == methodName && method.GetParameters().Length == args.Length)
                {
                    return method.Invoke(null, args);
                }
            }

            Assert.Fail($"No static method {typeName}.{methodName} with {args.Length} parameters found.");
            return null;
        }

        private static void InvokeNonPublic(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, Array.Empty<object>());
        }

        private static T Get<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
        }
    }
}
