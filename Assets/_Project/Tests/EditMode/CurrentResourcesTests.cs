using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Tests
{
    public sealed class CurrentResourcesTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string ResourceHealth = "resource.health";
        private const string ResourceMana = "resource.mana";
        private const string ResourceStamina = "resource.stamina";
        private const string MaximumHealth = "calculated-stat.maximum-health";
        private const string MaximumMana = "calculated-stat.maximum-mana";
        private const string MaximumStamina = "calculated-stat.maximum-stamina";

        [Test]
        public void PrototypeCatalog_ResolvesAlphaResourcesAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(report.WarningCount, Is.Zero, report.ToString());

            DefinitionRegistry registry = catalog.CreateRegistry();
            AssertResource(registry, ResourceHealth, MaximumHealth);
            AssertResource(registry, ResourceStamina, MaximumStamina);
            AssertResource(registry, ResourceMana, MaximumMana);
        }

        [Test]
        public void ResourceCollection_InitializesAndAppliesTransactions()
        {
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            GameObject owner = CreateConfiguredOwner(registry, out _, out Component stats, out Component resources);
            try
            {
                Assert.That(GetCurrent(resources, ResourceHealth), Is.EqualTo(GetMaximum(resources, ResourceHealth)).Within(0.001f));
                Assert.That(GetCurrent(resources, ResourceMana), Is.EqualTo(GetMaximum(resources, ResourceMana)).Within(0.001f));
                Assert.That(GetCurrent(resources, ResourceStamina), Is.EqualTo(GetMaximum(resources, ResourceStamina)).Within(0.001f));

                object spend = Invoke(resources, "TrySpend", ResourceMana, 25f, "test", "Spell", "event.mana.1", false);
                Assert.That(GetProperty<bool>(spend, "Succeeded"), Is.True, GetProperty<string>(spend, "Message"));
                Assert.That(GetCurrent(resources, ResourceMana), Is.EqualTo(GetMaximum(resources, ResourceMana) - 25f).Within(0.001f));

                object duplicate = Invoke(resources, "TrySpend", ResourceMana, 25f, "test", "Duplicate", "event.mana.1", false);
                Assert.That(GetProperty<bool>(duplicate, "Succeeded"), Is.True, GetProperty<string>(duplicate, "Message"));
                Assert.That(GetProperty<float>(duplicate, "AppliedAmount"), Is.Zero);

                object overspend = Invoke(resources, "TrySpend", ResourceMana, GetMaximum(resources, ResourceMana) * 10f, string.Empty, string.Empty, string.Empty, false);
                Assert.That(GetProperty<bool>(overspend, "Succeeded"), Is.False);

                object damage = Invoke(resources, "ApplyDamage", ResourceHealth, 10f, string.Empty, string.Empty, string.Empty);
                Assert.That(GetProperty<bool>(damage, "Succeeded"), Is.True, GetProperty<string>(damage, "Message"));
                object heal = Invoke(resources, "ApplyHealing", ResourceHealth, 3f, string.Empty, string.Empty, string.Empty);
                Assert.That(GetProperty<bool>(heal, "Succeeded"), Is.True, GetProperty<string>(heal, "Message"));
                Assert.That(GetCurrent(resources, ResourceHealth), Is.EqualTo(GetMaximum(resources, ResourceHealth) - 7f).Within(0.001f));

                float currentBeforeMaxIncrease = GetCurrent(resources, ResourceHealth);
                float maxBeforeIncrease = GetMaximum(resources, ResourceHealth);
                object contribution = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Stats.RuntimeCalculatedStatContribution"));
                SetField(contribution, "contributionId", "test.max-health");
                SetField(contribution, "statId", MaximumHealth);
                SetField(contribution, "sourceId", "test.max-health");
                SetField(contribution, "sourceCategory", EnumInt("UnityIsekaiGame.Stats.CalculatedStatContributionSourceCategory", "Development"));
                SetField(contribution, "kind", EnumInt("UnityIsekaiGame.Stats.CalculatedStatContributionKind", "Flat"));
                SetField(contribution, "direction", EnumInt("UnityIsekaiGame.Stats.CalculatedStatContributionDirection", "Improve"));
                SetField(contribution, "magnitude", 50f);
                object[] addArgs = { contribution, null, false };
                Assert.That(Invoke<bool>(stats, "AddContribution", addArgs), Is.True, addArgs[1] as string);

                Assert.That(GetMaximum(resources, ResourceHealth), Is.EqualTo(maxBeforeIncrease + 50f).Within(0.001f));
                Assert.That(GetCurrent(resources, ResourceHealth), Is.EqualTo(currentBeforeMaxIncrease).Within(0.001f), "Max increases must not refill current resource.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ResourceCollection_RegenHonorsDelayAndPersistsRoundTrip()
        {
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            GameObject owner = CreateConfiguredOwner(registry, out _, out Component stats, out Component resources);
            GameObject restoredOwner = null;
            try
            {
                float manaMax = GetMaximum(resources, ResourceMana);
                object spend = Invoke(resources, "TrySpend", ResourceMana, 40f, string.Empty, string.Empty, string.Empty, false);
                Assert.That(GetProperty<bool>(spend, "Succeeded"), Is.True, GetProperty<string>(spend, "Message"));

                Invoke(resources, "TickResources", 1f, 0.5f);
                Assert.That(GetCurrent(resources, ResourceMana), Is.EqualTo(manaMax - 40f).Within(0.001f), "Mana regen should wait for the spend delay.");

                Invoke(resources, "TickResources", 1f, 2f);
                Assert.That(GetCurrent(resources, ResourceMana), Is.GreaterThan(manaMax - 40f));

                object saveData = Invoke(resources, "CreateSaveData", "player.local", "person.prototype-player");
                object[] validateArgs = { saveData, registry, stats, "player.local", null };
                Assert.That(InvokeStaticByRef(RequiredType("UnityIsekaiGame.ResourceSystem.CharacterResourceCollection"), "ValidateSaveData", validateArgs), Is.True, validateArgs[4] as string);

                restoredOwner = CreateConfiguredOwner(registry, out _, out Component restoredStats, out Component restoredResources);
                object[] restoreArgs = { saveData, registry, restoredStats, "player.local", null, true };
                Assert.That(Invoke<bool>(restoredResources, "RestoreFromSaveData", restoreArgs), Is.True, restoreArgs[4] as string);
                Assert.That(GetCurrent(restoredResources, ResourceMana), Is.EqualTo(GetCurrent(resources, ResourceMana)).Within(0.001f));
                Assert.That(GetCurrent(restoredResources, ResourceHealth), Is.EqualTo(GetCurrent(resources, ResourceHealth)).Within(0.001f));
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

        [Test]
        public void ResourceCollection_ResetToDefinitionDefaultsRestoresAllResourcesAndClearsEventDedupe()
        {
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            GameObject owner = CreateConfiguredOwner(registry, out _, out _, out Component resources);
            try
            {
                object healthDamage = Invoke(resources, "ApplyDamage", ResourceHealth, 45f, "test", "Damage", "event.reset.health");
                object manaSpend = Invoke(resources, "TrySpend", ResourceMana, 25f, "test", "Spend", "event.reset.mana", false);
                object staminaSpend = Invoke(resources, "TrySpend", ResourceStamina, 15f, "test", "Spend", "event.reset.stamina", false);

                Assert.That(GetProperty<bool>(healthDamage, "Succeeded"), Is.True, GetProperty<string>(healthDamage, "Message"));
                Assert.That(GetProperty<bool>(manaSpend, "Succeeded"), Is.True, GetProperty<string>(manaSpend, "Message"));
                Assert.That(GetProperty<bool>(staminaSpend, "Succeeded"), Is.True, GetProperty<string>(staminaSpend, "Message"));
                Assert.That(GetCurrent(resources, ResourceHealth), Is.LessThan(GetMaximum(resources, ResourceHealth)));
                Assert.That(GetCurrent(resources, ResourceMana), Is.LessThan(GetMaximum(resources, ResourceMana)));
                Assert.That(GetCurrent(resources, ResourceStamina), Is.LessThan(GetMaximum(resources, ResourceStamina)));

                Invoke(resources, "ResetToDefinitionDefaults", "test.reset", "Reset test.", true);

                Assert.That(GetCurrent(resources, ResourceHealth), Is.EqualTo(GetMaximum(resources, ResourceHealth)).Within(0.001f));
                Assert.That(GetCurrent(resources, ResourceMana), Is.EqualTo(GetMaximum(resources, ResourceMana)).Within(0.001f));
                Assert.That(GetCurrent(resources, ResourceStamina), Is.EqualTo(GetMaximum(resources, ResourceStamina)).Within(0.001f));

                object spendAfterReset = Invoke(resources, "TrySpend", ResourceMana, 5f, "test", "Spend after reset", "event.reset.mana", false);
                Assert.That(GetProperty<bool>(spendAfterReset, "Succeeded"), Is.True, GetProperty<string>(spendAfterReset, "Message"));
                Assert.That(GetProperty<bool>(spendAfterReset, "DuplicateEvent"), Is.False, "Reset must clear processed resource transaction IDs.");
                Assert.That(GetCurrent(resources, ResourceMana), Is.EqualTo(GetMaximum(resources, ResourceMana) - 5f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ResourceBackedPlayerStaminaCommitsSprintSpendsToCanonicalResource()
        {
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            GameObject owner = CreateConfiguredOwner(registry, out _, out _, out Component resourcesComponent);
            try
            {
                CharacterResourceCollection resources = (CharacterResourceCollection)resourcesComponent;
                PlayerStamina stamina = owner.AddComponent<PlayerStamina>();
                stamina.RefreshResourceRuntime();
                int staminaEvents = 0;
                int staminaResourceEvents = 0;
                int committedStaminaEvents = 0;
                stamina.StaminaChanged += (_, _) => staminaEvents++;
                stamina.CommittedStaminaChanged += (_, _) => committedStaminaEvents++;
                resources.ResourceChanged += (_, result) =>
                {
                    if (string.Equals(result.Request.ResourceId, ResourceStamina, StringComparison.Ordinal))
                    {
                        staminaResourceEvents++;
                    }
                };

                float maximum = GetMaximum(resources, ResourceStamina);
                for (int i = 0; i < 10; i++)
                {
                    Assert.That(stamina.EvaluateSprint(true, true, false, 0.016f), Is.True);
                }

                Assert.That(staminaEvents, Is.EqualTo(10), "Every accepted sprint spend should publish the authoritative resource value.");
                Assert.That(staminaResourceEvents, Is.EqualTo(10), "Sprint must mutate the one canonical stamina resource rather than a local projection.");
                Assert.That(committedStaminaEvents, Is.EqualTo(10));
                Assert.That(stamina.CurrentStamina, Is.EqualTo(maximum - 3.2f).Within(0.001f));

                stamina.FlushPendingSprintResourceSpend();

                Assert.That(staminaResourceEvents, Is.EqualTo(10), "The compatibility flush is intentionally a no-op because spends are already committed.");
                Assert.That(committedStaminaEvents, Is.EqualTo(10));
                Assert.That(GetCurrent(resources, ResourceStamina), Is.EqualTo(maximum - 3.2f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CharacterResourceCollectionOwnsStaminaRegeneration()
        {
            DefinitionRegistry registry = LoadCatalog().CreateRegistry();
            GameObject owner = CreateConfiguredOwner(registry, out _, out _, out Component resourcesComponent);
            try
            {
                CharacterResourceCollection resources = (CharacterResourceCollection)resourcesComponent;
                float maximum = GetMaximum(resources, ResourceStamina);
                object spend = Invoke(resources, "TrySpend", ResourceStamina, 20f, "test", "Spend", string.Empty, false);
                Assert.That(GetProperty<bool>(spend, "Succeeded"), Is.True, GetProperty<string>(spend, "Message"));

                PlayerStamina stamina = owner.AddComponent<PlayerStamina>();
                Assert.That(stamina.CurrentStamina, Is.EqualTo(maximum - 20f).Within(0.001f));
                Assert.That(resources.IsAutomaticResourceTickSuppressed(ResourceStamina), Is.False);

                Invoke(resources, "TickResources", 10f, 100f);

                Assert.That(GetCurrent(resources, ResourceStamina), Is.GreaterThan(maximum - 20f), "The canonical resource collection owns stamina regeneration.");
                Assert.That(stamina.CurrentStamina, Is.EqualTo(GetCurrent(resources, ResourceStamina)).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PlayerStaminaRejectsSprintWhenCanonicalResourceIsUnconfigured()
        {
            GameObject owner = new GameObject("local-stamina-sprint-test");
            try
            {
                PlayerStamina stamina = owner.AddComponent<PlayerStamina>();
                int staminaEvents = 0;
                int committedStaminaEvents = 0;
                stamina.StaminaChanged += (_, _) => staminaEvents++;
                stamina.CommittedStaminaChanged += (_, _) => committedStaminaEvents++;

                for (int i = 0; i < 10; i++)
                {
                    Assert.That(stamina.EvaluateSprint(true, true, false, 0.016f), Is.False);
                }

                Assert.That(staminaEvents, Is.Zero);
                Assert.That(committedStaminaEvents, Is.Zero);
                Assert.That(stamina.CurrentStamina, Is.Zero);
                Assert.That(stamina.MaximumStamina, Is.Zero);

                stamina.FlushPendingSprintResourceSpend();

                Assert.That(committedStaminaEvents, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static void AssertResource(DefinitionRegistry registry, string resourceId, string expectedMaximumStatId)
        {
            Assert.That(registry.TryGet(resourceId, out IGameDefinition resource), Is.True, resourceId);
            Assert.That(resource.GetType().FullName, Is.EqualTo("UnityIsekaiGame.ResourceSystem.ResourceDefinition"));
            Assert.That(GetProperty<string>(resource, "LinkedMaximumStatId"), Is.EqualTo(expectedMaximumStatId));
            object linkedMaximum = GetProperty<object>(resource, "LinkedMaximumStat");
            Assert.That(GetProperty<bool>(linkedMaximum, "IsResourceMaximum"), Is.True);
            Assert.That(GetProperty<string>(linkedMaximum, "LinkedFutureResourceId"), Is.EqualTo(resourceId));
            Assert.That(GetProperty<object>(resource, "PersistencePolicy").ToString(), Is.EqualTo("Persist"));
            Assert.That(GetProperty<object>(resource, "Authority").ToString(), Is.EqualTo("ServerAuthoritativeFuture"));
        }

        private static GameObject CreateConfiguredOwner(
            DefinitionRegistry registry,
            out Component attributes,
            out Component stats,
            out Component resources)
        {
            GameObject owner = new GameObject("Current Resources Test Owner");
            attributes = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CharacterAttributes"));
            stats = owner.AddComponent(RequiredType("UnityIsekaiGame.Stats.CalculatedStatCollection"));
            resources = owner.AddComponent(RequiredType("UnityIsekaiGame.ResourceSystem.CharacterResourceCollection"));

            Invoke(attributes, "Configure", registry);
            Invoke(stats, "Configure", registry, attributes);
            Invoke(resources, "Configure", registry, stats, "player.local");
            return owner;
        }

        private static float GetCurrent(object resources, string resourceId)
        {
            return Invoke<float>(resources, "GetCurrent", resourceId);
        }

        private static float GetMaximum(object resources, string resourceId)
        {
            return Invoke<float>(resources, "GetMaximum", resourceId);
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
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

        private static bool InvokeStaticByRef(Type targetType, string methodName, object[] args)
        {
            return (bool)FindMethod(targetType, methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, args)
                .Invoke(null, args);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static int EnumInt(string enumTypeName, string value)
        {
            return Convert.ToInt32(Enum.Parse(RequiredType(enumTypeName), value));
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
                if (parameters.Length != (args == null ? 0 : args.Length))
                {
                    continue;
                }

                bool matches = true;
                for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    Type parameterType = parameters[parameterIndex].ParameterType;
                    if (parameterType.IsByRef)
                    {
                        parameterType = parameterType.GetElementType();
                    }

                    object argument = args[parameterIndex];
                    if (argument == null)
                    {
                        if (parameterType != null && parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null && !parameters[parameterIndex].IsOut)
                        {
                            matches = false;
                            break;
                        }

                        continue;
                    }

                    if (parameterType == null || !parameterType.IsAssignableFrom(argument.GetType()))
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

            Assert.Fail($"Method '{methodName}' with {args?.Length ?? 0} argument(s) was not found on {targetType.FullName}.");
            return null;
        }
    }
}
