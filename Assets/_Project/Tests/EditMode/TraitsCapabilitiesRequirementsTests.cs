using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Tests
{
    public sealed class TraitsCapabilitiesRequirementsTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string LocalPlayerId = "player.local";
        private const string PrototypePersonId = "person.prototype-player";

        [Test]
        public void PrototypeCatalog_ResolvesAlphaTraitsCapabilitiesRequirementsAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(report.WarningCount, Is.Zero, report.ToString());

            DefinitionRegistry registry = catalog.CreateRegistry();
            AssertResolves(registry, "trait.ambidextrous", "UnityIsekaiGame.Traits.TraitDefinition");
            AssertResolves(registry, "trait.reincarnated-memory", "UnityIsekaiGame.Traits.TraitDefinition");
            AssertResolves(registry, "trait.mana-sensitive", "UnityIsekaiGame.Traits.TraitDefinition");
            AssertResolves(registry, "capability.sense.mana", "UnityIsekaiGame.Capabilities.CapabilityDefinition");
            AssertResolves(registry, "capability.knowledge.otherworld", "UnityIsekaiGame.Capabilities.CapabilityDefinition");
            AssertResolves(registry, "requirement.fire-ritual", "UnityIsekaiGame.Requirements.RequirementSetDefinition");
        }

        [Test]
        public void Traits_RespectDiscoveryVisibilityAndAggregateCapabilities()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = CreateConfiguredOwner(registry, out _, out _, out _, out _, out Component traits);
            try
            {
                object hiddenGrant = GrantTrait(traits, "trait.reincarnated-memory", "test.hidden-memory", "Active", "Undiscovered");
                AssertResultSucceeded(hiddenGrant);
                Assert.That(KnownTraitSnapshots(traits).Cast<object>().Any(snapshot => Field<string>(Property<object>(snapshot, "Record"), "traitDefinitionId") == "trait.reincarnated-memory"), Is.False);
                Assert.That(CapabilityBoolean(traits, "capability.knowledge.otherworld"), Is.True);

                object suspected = Invoke(traits, "SetDiscoveryState", "trait.reincarnated-memory", EnumValue("UnityIsekaiGame.Traits.TraitDiscoveryState", "Suspected"));
                AssertResultSucceeded(suspected);
                Assert.That(KnownTraitSnapshots(traits).Cast<object>().Any(snapshot => Property<string>(snapshot, "PresentationName") == "Unknown Peculiarity"), Is.False, "Secret Traits should not appear in the Character UI until discovered.");

                object discovered = Invoke(traits, "SetDiscoveryState", "trait.reincarnated-memory", EnumValue("UnityIsekaiGame.Traits.TraitDiscoveryState", "Discovered"));
                AssertResultSucceeded(discovered);
                Assert.That(KnownTraitSnapshots(traits).Cast<object>().Any(snapshot => Property<string>(snapshot, "PresentationName") == "Reincarnated Memory"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TraitConflicts_RejectByDefaultAndExplicitReplacementIsAtomic()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = CreateConfiguredOwner(registry, out _, out _, out _, out _, out Component traits);
            try
            {
                object brave = GrantActiveDiscovered(traits, "trait.brave", "test.brave");
                AssertResultSucceeded(brave);

                object rejected = GrantTrait(traits, "trait.cowardly", "test.cowardly", "Active", "Discovered");
                Assert.That(Property<bool>(rejected, "Succeeded"), Is.False);
                Assert.That(Property<string>(rejected, "Code"), Is.EqualTo("TraitConflict"));

                object[] tryGetArgs = { "trait.brave", null };
                Assert.That(Invoke<bool>(traits, "TryGetTrait", tryGetArgs), Is.True);
                Assert.That(EnumName(Field<int>(tryGetArgs[1], "lifecycleState"), "UnityIsekaiGame.Traits.TraitLifecycleState"), Is.EqualTo("Active"));

                object replacement = GrantTrait(traits, "trait.cowardly", "test.cowardly", "Active", "Discovered", allowReplacement: true, authorizedReplacements: new[] { "trait.brave" });
                AssertResultSucceeded(replacement);
                Assert.That(((IEnumerable)Property<object>(replacement, "ReplacedTraitIds")).Cast<object>().Select(value => value.ToString()), Does.Contain("trait.brave"));

                tryGetArgs = new object[] { "trait.brave", null };
                Assert.That(Invoke<bool>(traits, "TryGetTrait", tryGetArgs), Is.True);
                Assert.That(EnumName(Field<int>(tryGetArgs[1], "lifecycleState"), "UnityIsekaiGame.Traits.TraitLifecycleState"), Is.EqualTo("Historical"));
                Assert.That(Invoke<bool>(traits, "HasTrait", "trait.cowardly", false, false), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void LinkedTraitsAndRequirementEvaluation_DoNotMutateResources()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = CreateConfiguredOwner(registry, out _, out _, out Component resources, out _, out Component traits);
            try
            {
                object beastkin = GrantActiveDiscovered(traits, "trait.beastkin", "test.beastkin");
                AssertResultSucceeded(beastkin);
                Assert.That(((IEnumerable)Property<object>(beastkin, "LinkedGrantIds")).Cast<object>().Select(value => value.ToString()), Does.Contain("trait.night-vision"));
                Assert.That(CapabilityBoolean(traits, "capability.vision.low-light"), Is.True);

                object fireRitual = Resolve(registry, "requirement.fire-ritual");
                float manaBefore = Invoke<float>(resources, "GetCurrent", "resource.mana");
                object failed = EvaluateRequirement(fireRitual, resources, traits);
                Assert.That(Property<bool>(failed, "Passed"), Is.False);
                Assert.That(Invoke<float>(resources, "GetCurrent", "resource.mana"), Is.EqualTo(manaBefore).Within(0.001f), "Requirement checks must be pure and must not spend resources.");

                object manaSensitive = GrantActiveDiscovered(traits, "trait.mana-sensitive", "test.mana-sensitive");
                AssertResultSucceeded(manaSensitive);
                object passed = EvaluateRequirement(fireRitual, resources, traits);
                Assert.That(Property<bool>(passed, "Passed"), Is.True, string.Join("; ", ((IEnumerable)Property<object>(passed, "TestLabFailureReasons")).Cast<object>().Select(value => value.ToString())));
                Assert.That(Invoke<float>(resources, "GetCurrent", "resource.mana"), Is.EqualTo(manaBefore).Within(0.001f), "Passing requirement checks must also leave resources unchanged.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PlayerTraitsPersistence_RoundTripsAndRejectsUnsafeData()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = CreateConfiguredOwner(registry, out _, out Component stats, out _, out Component skills, out Component traits);
            GameObject restoredOwner = null;
            try
            {
                AssertResultSucceeded(GrantActiveDiscovered(traits, "trait.ambidextrous", "test.ambidextrous"));
                AssertResultSucceeded(GrantActiveDiscovered(traits, "trait.frail", "test.frail"));

                object saveData = Invoke(traits, "CreateSaveData", LocalPlayerId, PrototypePersonId);
                object[] validateArgs = { saveData, registry, LocalPlayerId, null };
                Assert.That(InvokeStaticByRef(RequiredType("UnityIsekaiGame.Traits.CharacterTraitCollection"), "ValidateSaveData", validateArgs), Is.True, validateArgs[3] as string);

                restoredOwner = CreateConfiguredOwner(registry, out _, out Component restoredStats, out _, out Component restoredSkills, out Component restoredTraits);
                object[] restoreArgs = { saveData, registry, restoredStats, restoredSkills, LocalPlayerId, null, true };
                Assert.That(Invoke<bool>(restoredTraits, "RestoreFromSaveData", restoreArgs), Is.True, restoreArgs[5] as string);
                Assert.That(Invoke<bool>(restoredTraits, "HasTrait", "trait.ambidextrous", false, false), Is.True);
                Assert.That(Invoke<bool>(restoredTraits, "HasTrait", "trait.frail", false, false), Is.True);

                IList records = (IList)Field<object>(saveData, "traits");
                object duplicate = InvokeStatic(RequiredType("UnityIsekaiGame.Traits.TraitRuntimeCloner"), "Clone", records[0]);
                records.Add(duplicate);
                validateArgs = new object[] { saveData, registry, LocalPlayerId, null };
                Assert.That(InvokeStaticByRef(RequiredType("UnityIsekaiGame.Traits.CharacterTraitCollection"), "ValidateSaveData", validateArgs), Is.False);
                Assert.That(validateArgs[3] as string, Does.Contain("Duplicate Trait record"));

                object unknown = Invoke(traits, "CreateSaveData", LocalPlayerId, PrototypePersonId);
                IList unknownRecords = (IList)Field<object>(unknown, "traits");
                SetField(unknownRecords[0], "traitDefinitionId", "trait.obsolete-memory");
                validateArgs = new object[] { unknown, registry, LocalPlayerId, null };
                Assert.That(InvokeStaticByRef(RequiredType("UnityIsekaiGame.Traits.CharacterTraitCollection"), "ValidateSaveData", validateArgs), Is.False);
                Assert.That(validateArgs[3] as string, Does.Contain("unknown TraitDefinition"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                if (restoredOwner != null)
                {
                    UnityEngine.Object.DestroyImmediate(restoredOwner);
                }
            }
        }

        private static object GrantActiveDiscovered(Component traits, string traitId, string sourceId)
        {
            return GrantTrait(traits, traitId, sourceId, "Active", "Discovered");
        }

        private static object GrantTrait(Component traits, string traitId, string sourceId, string lifecycle, string discovery, bool allowReplacement = false, string[] authorizedReplacements = null)
        {
            object request = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Traits.TraitGrantRequest"));
            SetProperty(request, "TraitDefinitionId", traitId);
            SetProperty(request, "RequestedLifecycle", EnumValue("UnityIsekaiGame.Traits.TraitLifecycleState", lifecycle));
            SetProperty(request, "RequestedDiscovery", EnumValue("UnityIsekaiGame.Traits.TraitDiscoveryState", discovery));
            SetProperty(request, "SourceId", sourceId);
            SetProperty(request, "AllowConflictReplacement", allowReplacement);
            if (authorizedReplacements != null)
            {
                SetProperty(request, "TraitsAuthorizedForReplacement", authorizedReplacements);
            }

            return Invoke(traits, "GrantTrait", request);
        }

        private static object EvaluateRequirement(object requirementSet, Component resources, Component traits)
        {
            object context = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Requirements.RequirementEvaluationContext"));
            SetProperty(context, "Resources", resources);
            SetProperty(context, "Traits", traits);
            SetProperty(context, "Capabilities", traits.GetComponent(RequiredType("UnityIsekaiGame.Capabilities.CharacterCapabilityCollection")));
            return InvokeStatic(RequiredType("UnityIsekaiGame.Requirements.CapabilityRequirementEvaluator"), "Evaluate", requirementSet, context);
        }

        private static bool CapabilityBoolean(Component traits, string capabilityId)
        {
            object capabilities = traits.GetComponent(RequiredType("UnityIsekaiGame.Capabilities.CharacterCapabilityCollection"));
            object snapshot = Invoke(capabilities, "Evaluate", capabilityId);
            Assert.That(snapshot, Is.Not.Null, capabilityId);
            return Property<bool>(snapshot, "BooleanValue");
        }

        private static IEnumerable KnownTraitSnapshots(Component traits)
        {
            return (IEnumerable)Invoke(traits, "GetKnownTraits");
        }

        private static GameObject CreateConfiguredOwner(
            DefinitionRegistry registry,
            out Component attributes,
            out Component stats,
            out Component resources,
            out Component skills,
            out Component traits)
        {
            GameObject owner = new GameObject("Traits Capabilities Requirements Test Owner");
            attributes = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
            stats = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CalculatedStatCollection"));
            resources = owner.AddComponent(RequiredType("UnityIsekaiGame.ResourceSystem.CharacterResourceCollection"));
            skills = owner.AddComponent(RequiredType("UnityIsekaiGame.Skills.CharacterSkillCollection"));
            traits = owner.AddComponent(RequiredType("UnityIsekaiGame.Traits.CharacterTraitCollection"));
            Component capabilities = owner.AddComponent(RequiredType("UnityIsekaiGame.Capabilities.CharacterCapabilityCollection"));

            Invoke(attributes, "Configure", registry);
            Invoke(stats, "Configure", registry, attributes);
            Invoke(resources, "Configure", registry, stats, LocalPlayerId);
            Invoke(capabilities, "Configure", registry);
            Invoke(skills, "Configure", registry, stats, null);
            Invoke(traits, "Configure", registry, stats, skills, capabilities, LocalPlayerId);
            return owner;
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }

        private static DefinitionRegistry LoadRegistry()
        {
            return LoadCatalog().CreateRegistry();
        }

        private static object Resolve(DefinitionRegistry registry, string id)
        {
            Assert.That(registry.TryGet(id, out IGameDefinition definition), Is.True, id);
            return definition;
        }

        private static void AssertResolves(DefinitionRegistry registry, string id, string fullTypeName)
        {
            object definition = Resolve(registry, id);
            Assert.That(definition.GetType().FullName, Is.EqualTo(fullTypeName));
            Assert.That(((IGameDefinition)definition).Id, Is.EqualTo(id));
        }

        private static void AssertResultSucceeded(object result)
        {
            Assert.That(Property<bool>(result, "Succeeded"), Is.True, Property<string>(result, "Message"));
        }

        private static Type RequiredType(string fullName)
        {
            Type type = TestTypeResolver.RequiredType(fullName);
            Assert.That(type, Is.Not.Null, $"Type '{fullName}' is missing.");
            return type;
        }

        private static object EnumValue(string enumTypeName, string value)
        {
            return Enum.Parse(RequiredType(enumTypeName), value);
        }

        private static string EnumName(int value, string enumTypeName)
        {
            return Enum.ToObject(RequiredType(enumTypeName), value).ToString();
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

        private static object InvokeStatic(Type targetType, string methodName, params object[] args)
        {
            return FindMethod(targetType, methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, args)
                .Invoke(null, args);
        }

        private static bool InvokeStaticByRef(Type targetType, string methodName, object[] args)
        {
            return (bool)InvokeStatic(targetType, methodName, args);
        }

        private static T Property<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static T Field<T>(object target, string fieldName)
        {
            return (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static MethodInfo FindMethod(Type targetType, string methodName, BindingFlags bindingFlags, object[] args)
        {
            MethodInfo[] methods = targetType.GetMethods(bindingFlags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
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
                for (int p = 0; p < parameters.Length; p++)
                {
                    if (args[p] == null)
                    {
                        continue;
                    }

                    Type parameterType = parameters[p].ParameterType.IsByRef ? parameters[p].ParameterType.GetElementType() : parameters[p].ParameterType;
                    if (!parameterType.IsInstanceOfType(args[p]))
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

            Assert.Fail($"Method '{methodName}' with {args.Length} argument(s) was not found on {targetType.FullName}.");
            return null;
        }
    }
}
