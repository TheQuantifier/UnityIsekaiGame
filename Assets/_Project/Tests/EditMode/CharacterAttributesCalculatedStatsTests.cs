using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Tests
{
    public sealed class CharacterAttributesCalculatedStatsTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private static readonly string[] AlphaAttributeIds =
        {
            "attribute.strength",
            "attribute.agility",
            "attribute.endurance",
            "attribute.vitality",
            "attribute.intellect",
            "attribute.willpower",
            "attribute.perception",
            "attribute.charisma",
            "attribute.mana-capacity"
        };

        private static readonly string[] AlphaCalculatedStatIds =
        {
            "calculated-stat.physical-power",
            "calculated-stat.magical-power",
            "calculated-stat.healing-power",
            "calculated-stat.support-power",
            "calculated-stat.physical-defense",
            "calculated-stat.magical-defense",
            "calculated-stat.maximum-health",
            "calculated-stat.maximum-stamina",
            "calculated-stat.maximum-mana",
            "calculated-stat.movement-speed",
            "calculated-stat.carrying-capacity",
            "calculated-stat.accuracy",
            "calculated-stat.evasion"
        };

        private const string MaximumHealthStatId = "calculated-stat.maximum-health";
        private const string MaximumStaminaStatId = "calculated-stat.maximum-stamina";
        private const string MaximumManaStatId = "calculated-stat.maximum-mana";
        private const string FutureHealthResourceId = "resource.health";
        private const string FutureStaminaResourceId = "resource.stamina";
        private const string FutureManaResourceId = "resource.mana";

        [Test]
        public void PrototypeCatalog_ResolvesAlphaAttributesAndCalculatedStats()
        {
            DefinitionRegistry registry = LoadRegistry();

            foreach (string attributeId in AlphaAttributeIds)
            {
                Assert.That(registry.TryGet(attributeId, out IGameDefinition attribute), Is.True, attributeId);
                Assert.That(attribute.GetType().FullName, Is.EqualTo("UnityIsekaiGame.Stats.AttributeDefinition"));
                Assert.That(GetProperty<float>(attribute, "FoundationValue"), Is.EqualTo(1f));
            }

            foreach (string statId in AlphaCalculatedStatIds)
            {
                Assert.That(registry.TryGet(statId, out IGameDefinition stat), Is.True, statId);
                Assert.That(stat.GetType().FullName, Is.EqualTo("UnityIsekaiGame.Stats.CalculatedStatDefinition"));
                Assert.That(GetProperty<object>(stat, "Formula"), Is.Not.Null, statId);
            }

            Assert.That(registry.TryGet("calculated-stat.movement-speed", out IGameDefinition movementSpeed), Is.True);
            object movementFormula = GetProperty<object>(movementSpeed, "Formula");
            Assert.That(GetProperty<float>(movementFormula, "BaseValue"), Is.EqualTo(4f));
        }

        [Test]
        public void BaseAttributes_ArePermanentFractionalValuesWithFloorDisplay()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = new GameObject("Base Attribute Fixture");
            try
            {
                Component attributes = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
                Invoke(attributes, "Configure", registry);

                object contribution = CreateAttributeContribution("attribute.strength", "test.base-attribute.fractional", 0.75f);
                Array contributions = Array.CreateInstance(RequiredType("UnityIsekaiGame.Stats.RuntimeAttributeSourceContribution"), 1);
                contributions.SetValue(contribution, 0);
                object category = Enum.Parse(RequiredType("UnityIsekaiGame.Stats.AttributeGrowthEventCategory"), "Training");
                object[] args = { "test.base-attribute.fractional", category, contributions, "EditMode Test", null, false };

                Assert.That(Invoke<bool>(attributes, "TryRecordTrainingEvent", args), Is.True, args[4] as string);
                Assert.That(Invoke<float>(attributes, "GetValue", "attribute.strength"), Is.EqualTo(1.75f).Within(0.0001f));
                Assert.That(Invoke<int>(attributes, "GetDisplayedValue", "attribute.strength"), Is.EqualTo(1));
                Assert.That(registry.Contains("attribute.current-strength"), Is.False);
                Assert.That(registry.Contains("attribute.maximum-strength"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CalculatedStats_DeclarePurposeAndReservedResourceMaximumLinks()
        {
            DefinitionRegistry registry = LoadRegistry();

            AssertResourceMaximum(registry, MaximumHealthStatId, FutureHealthResourceId);
            AssertResourceMaximum(registry, MaximumStaminaStatId, FutureStaminaResourceId);
            AssertResourceMaximum(registry, MaximumManaStatId, FutureManaResourceId);

            foreach (string statId in AlphaCalculatedStatIds)
            {
                Assert.That(registry.TryGet(statId, out IGameDefinition stat), Is.True, statId);
                object purpose = GetProperty<object>(stat, "Purpose");
                Assert.That(Enum.IsDefined(RequiredType("UnityIsekaiGame.Stats.CalculatedStatPurpose"), purpose), Is.True, statId);
                if (!GetProperty<bool>(stat, "IsResourceMaximum"))
                {
                    Assert.That(GetProperty<string>(stat, "LinkedFutureResourceId"), Is.Empty, statId);
                }
            }
        }

        [Test]
        public void CalculatedStatValidation_RejectsMissingAndDuplicateResourceMappings()
        {
            DefinitionRegistry registry = LoadRegistry();
            Assert.That(registry.TryGet("calculated-stat-formula.physical-power", out IGameDefinition formula), Is.True);

            Type statDefinitionType = RequiredType("UnityIsekaiGame.Stats.CalculatedStatDefinition");
            ScriptableObject missing = ScriptableObject.CreateInstance(statDefinitionType);
            ScriptableObject duplicate = ScriptableObject.CreateInstance(statDefinitionType);
            try
            {
                SetField(missing, "statId", "calculated-stat.test-missing-resource");
                SetField(missing, "displayName", "Test Missing Resource");
                SetField(missing, "formula", formula);
                SetField(missing, "purpose", Enum.Parse(RequiredType("UnityIsekaiGame.Stats.CalculatedStatPurpose"), "ResourceMaximum"));

                SetField(duplicate, "statId", "calculated-stat.test-duplicate-health");
                SetField(duplicate, "displayName", "Test Duplicate Health");
                SetField(duplicate, "formula", formula);
                SetField(duplicate, "purpose", Enum.Parse(RequiredType("UnityIsekaiGame.Stats.CalculatedStatPurpose"), "ResourceMaximum"));
                SetField(duplicate, "linkedFutureResourceId", FutureHealthResourceId);

                Dictionary<string, IGameDefinition> definitions = new Dictionary<string, IGameDefinition>(registry.DefinitionsById)
                {
                    { GetProperty<string>(missing, "Id"), (IGameDefinition)missing },
                    { GetProperty<string>(duplicate, "Id"), (IGameDefinition)duplicate }
                };

                DefinitionValidationReport report = new DefinitionValidationReport();
                ((IDefinitionCatalogValidationParticipant)missing).ValidateCatalogDefinition(definitions, report);
                ((IDefinitionCatalogValidationParticipant)duplicate).ValidateCatalogDefinition(definitions, report);

                Assert.That(report.ErrorCount, Is.GreaterThanOrEqualTo(2), report.GetSummary());
                Assert.That(report.GetSummary(), Does.Contain("does not declare a linked future resource ID"));
                Assert.That(report.GetSummary(), Does.Contain("duplicates resource-maximum mapping"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(missing);
                UnityEngine.Object.DestroyImmediate(duplicate);
            }
        }

        [Test]
        public void TemporaryCalculatedStatContribution_DoesNotMutateBaseAttribute()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = new GameObject("Calculated Contribution Fixture");
            try
            {
                Component attributes = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
                Component calculatedStats = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CalculatedStatCollection"));
                Invoke(attributes, "Configure", registry);
                Invoke(calculatedStats, "Configure", registry, attributes);

                float initialStrength = Invoke<float>(attributes, "GetValue", "attribute.strength");
                float initialPower = Invoke<float>(calculatedStats, "GetValue", "calculated-stat.physical-power");
                object contribution = CreateCalculatedContribution("calculated-stat.physical-power", "test.temporary.source", 5f);
                object[] args = { contribution, null, false };

                Assert.That(Invoke<bool>(calculatedStats, "AddContribution", args), Is.True, args[1] as string);
                Assert.That(Invoke<float>(attributes, "GetValue", "attribute.strength"), Is.EqualTo(initialStrength));
                Assert.That(Invoke<float>(calculatedStats, "GetValue", "calculated-stat.physical-power"), Is.EqualTo(initialPower + 5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void MovementSpeedFormula_UsesBasePlusAgility()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = new GameObject("Movement Speed Formula Fixture");
            try
            {
                Component attributes = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
                Component calculatedStats = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CalculatedStatCollection"));
                Invoke(attributes, "Configure", registry);
                Invoke(calculatedStats, "Configure", registry, attributes);

                float agility = Invoke<float>(attributes, "GetValue", "attribute.agility");
                Assert.That(Invoke<float>(calculatedStats, "GetValue", "calculated-stat.movement-speed"), Is.EqualTo(4f + agility));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ActorMovementSpeed_ReadsCanonicalCalculatedMovement()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = new GameObject("Actor Movement Speed Composition Fixture");
            try
            {
                owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
                owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CalculatedStatCollection"));
                Component actorStats = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));

                Invoke(actorStats, "ConfigureDerivedStats", registry);

                float expectedCalculatedMovement = 4f + 1f;
                Assert.That(GetProperty<float>(actorStats, "MovementSpeed"), Is.EqualTo(expectedCalculatedMovement));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AttributeGrowth_RecalculatesDependentCalculatedStats()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = new GameObject("Attribute Growth Fixture");
            try
            {
                Component attributes = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
                Component calculatedStats = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CalculatedStatCollection"));
                Invoke(attributes, "Configure", registry);
                Invoke(calculatedStats, "Configure", registry, attributes);

                float initialPower = Invoke<float>(calculatedStats, "GetValue", "calculated-stat.physical-power");
                object contribution = CreateAttributeContribution("attribute.strength", "test.training.strength", 0.5f);
                Array contributions = Array.CreateInstance(RequiredType("UnityIsekaiGame.Stats.RuntimeAttributeSourceContribution"), 1);
                contributions.SetValue(contribution, 0);

                object category = Enum.Parse(RequiredType("UnityIsekaiGame.Stats.AttributeGrowthEventCategory"), "Training");
                object[] args = { "test.training.strength", category, contributions, "EditMode Test", null, false };
                bool recorded = Invoke<bool>(attributes, "TryRecordTrainingEvent", args);

                Assert.That(recorded, Is.True, args[4] as string);
                Assert.That(Invoke<float>(attributes, "GetValue", "attribute.strength"), Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(Invoke<int>(attributes, "GetDisplayedValue", "attribute.strength"), Is.EqualTo(1));
                Assert.That(Invoke<float>(calculatedStats, "GetValue", "calculated-stat.physical-power"), Is.GreaterThan(initialPower));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void InvalidAttributeGrowth_IsRejectedAtomically()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = new GameObject("Invalid Attribute Growth Fixture");
            try
            {
                Component attributes = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
                Invoke(attributes, "Configure", registry);
                object contribution = CreateAttributeContribution("attribute.strength", "test.training.invalid", -1f);
                Array contributions = Array.CreateInstance(RequiredType("UnityIsekaiGame.Stats.RuntimeAttributeSourceContribution"), 1);
                contributions.SetValue(contribution, 0);

                object category = Enum.Parse(RequiredType("UnityIsekaiGame.Stats.AttributeGrowthEventCategory"), "Training");
                object[] args = { "test.training.invalid", category, contributions, "EditMode Test", null, false };
                bool recorded = Invoke<bool>(attributes, "TryRecordTrainingEvent", args);

                Assert.That(recorded, Is.False);
                Assert.That(((ICollection)GetProperty<object>(attributes, "TrainingEvents")).Count, Is.EqualTo(0));
                Assert.That(Invoke<float>(attributes, "GetValue", "attribute.strength"), Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ActorStats_AcceptsCanonicalCalculatedStatContribution()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = new GameObject("Calculated Stat Fixture");
            try
            {
                owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
                owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CalculatedStatCollection"));
                Component actorStats = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.ActorStats"));
                Invoke(actorStats, "ConfigureDerivedStats", registry);
                float initialPower = GetProperty<float>(actorStats, "AttackPower");

                object modifier = CreateCalculatedContribution("calculated-stat.physical-power", "test.equipment.weapon", 5f, "Equipment");
                Assert.That(Invoke<bool>(actorStats, "AddCalculatedStatContribution", modifier), Is.True);
                Assert.That(GetProperty<float>(actorStats, "AttackPower"), Is.EqualTo(initialPower + 5f));

                object source = CreateStatModifierSource("Equipment", "test.equipment.weapon");
                Assert.That(Invoke<bool>(actorStats, "RemoveCalculatedStatContributions", source), Is.True);
                Assert.That(GetProperty<float>(actorStats, "AttackPower"), Is.EqualTo(initialPower));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PlayerAttributesSaveData_DuplicatePermanentContributionIsRejected()
        {
            DefinitionRegistry registry = LoadRegistry();
            Type saveDataType = RequiredType("UnityIsekaiGame.Stats.PlayerAttributesSaveData");
            Type contributionType = RequiredType("UnityIsekaiGame.Stats.RuntimeAttributeSourceContribution");
            Type listType = typeof(List<>).MakeGenericType(contributionType);
            object list = Activator.CreateInstance(listType);
            listType.GetMethod("Add").Invoke(list, new[] { CreateAttributeContribution("attribute.strength", "source.test", 1f) });
            listType.GetMethod("Add").Invoke(list, new[] { CreateAttributeContribution("attribute.strength", "source.test", 2f) });

            object saveData = Activator.CreateInstance(saveDataType);
            SetField(saveData, "permanentSourceContributions", list);

            object[] args = { saveData, registry, null };
            bool valid = (bool)RequiredType("UnityIsekaiGame.Stats.CharacterAttributes")
                .GetMethod("ValidateSaveData", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, args);

            Assert.That(valid, Is.False);
            Assert.That(args[2] as string, Does.Contain("Duplicate permanent attribute contribution"));
        }

        [Test]
        public void ObsoleteLegacyStatIds_DoNotResolveAsDefinitions()
        {
            DefinitionRegistry registry = LoadRegistry();
            Assert.That(registry.Contains("AttackPower"), Is.False);
            Assert.That(registry.Contains("MaximumHealth"), Is.False);
            Assert.That(registry.Contains("calculated-stat.attack-power"), Is.False);
            Assert.That(registry.Contains("base-attribute.strength"), Is.False);
        }

        private static void AssertResourceMaximum(DefinitionRegistry registry, string statId, string expectedResourceId)
        {
            Assert.That(registry.TryGet(statId, out IGameDefinition stat), Is.True, statId);
            Assert.That(GetProperty<object>(stat, "Purpose").ToString(), Is.EqualTo("ResourceMaximum"), statId);
            Assert.That(GetProperty<bool>(stat, "IsResourceMaximum"), Is.True, statId);
            Assert.That(GetProperty<string>(stat, "LinkedFutureResourceId"), Is.EqualTo(expectedResourceId), statId);
        }

        private static object CreateAttributeContribution(string attributeId, string sourceId, float amount)
        {
            object contribution = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Stats.RuntimeAttributeSourceContribution"));
            SetField(contribution, "attributeId", attributeId);
            SetField(contribution, "sourceId", sourceId);
            SetField(contribution, "sourceCategory", (int)Enum.Parse(RequiredType("UnityIsekaiGame.Stats.CalculatedStatContributionSourceCategory"), "Development"));
            SetField(contribution, "amount", amount);
            return contribution;
        }

        private static object CreateCalculatedContribution(string statId, string sourceId, float magnitude, string sourceCategory = "Development")
        {
            object contribution = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Stats.RuntimeCalculatedStatContribution"));
            SetField(contribution, "contributionId", $"{sourceId}.contribution");
            SetField(contribution, "statId", statId);
            SetField(contribution, "sourceId", sourceId);
            SetField(contribution, "sourceCategory", (int)Enum.Parse(RequiredType("UnityIsekaiGame.Stats.CalculatedStatContributionSourceCategory"), sourceCategory));
            SetField(contribution, "kind", (int)Enum.Parse(RequiredType("UnityIsekaiGame.Stats.CalculatedStatContributionKind"), "Flat"));
            SetField(contribution, "direction", (int)Enum.Parse(RequiredType("UnityIsekaiGame.Stats.CalculatedStatContributionDirection"), "Improve"));
            SetField(contribution, "magnitude", magnitude);
            return contribution;
        }

        private static object CreateStatModifierSource(string sourceTypeName, string sourceId)
        {
            Type sourceType = RequiredType("UnityIsekaiGame.Stats.StatModifierSource");
            Type sourceTypeEnum = RequiredType("UnityIsekaiGame.Stats.StatModifierSourceType");
            return Activator.CreateInstance(sourceType, Enum.Parse(sourceTypeEnum, sourceTypeName), sourceId);
        }

        private static DefinitionRegistry LoadRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
            return catalog.CreateRegistry();
        }

        private static Type RequiredType(string fullName)
        {
            Type type = TestTypeResolver.RequiredType(fullName);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static T Invoke<T>(object target, string methodName, params object[] args)
        {
            return (T)Invoke(target, methodName, args);
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = FindMethod(target.GetType(), methodName, args);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, args);
        }

        private static MethodInfo FindMethod(Type type, string methodName, object[] args)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
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

                bool matches = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;
                    if (parameterType.IsByRef)
                    {
                        continue;
                    }

                    if (args[i] != null && !parameterType.IsInstanceOfType(args[i]))
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

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
