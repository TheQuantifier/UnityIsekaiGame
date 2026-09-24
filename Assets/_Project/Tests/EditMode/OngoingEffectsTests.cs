using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class OngoingEffectsTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeCatalog_ResolvesCanonicalOngoingEffectsAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(report.WarningCount, Is.Zero, report.ToString());

            DefinitionRegistry registry = catalog.CreateRegistry();
            Assert.That(registry.TryGet("ongoing-effect.poison", out OngoingEffectDefinition poison), Is.True);
            Assert.That(poison.OperationType, Is.EqualTo(OngoingEffectOperationType.Damage));
            Assert.That(poison.DamageType.Id, Is.EqualTo("damage.magic.poison"));
            Assert.That(registry.TryGet("ongoing-effect.burning", out OngoingEffectDefinition burning), Is.True);
            Assert.That(burning.TickImmediately, Is.True);
            Assert.That(registry.TryGet("ongoing-effect.health-regeneration", out OngoingEffectDefinition healthRegen), Is.True);
            Assert.That(healthRegen.OperationType, Is.EqualTo(OngoingEffectOperationType.Healing));
            Assert.That(registry.TryGet("ongoing-effect.mana-regeneration", out OngoingEffectDefinition manaRegen), Is.True);
            Assert.That(manaRegen.TargetResourceId, Is.EqualTo(ResourceIds.Mana));
            Assert.That(registry.TryGet("ongoing-effect.stamina-regeneration", out OngoingEffectDefinition staminaRegen), Is.True);
            Assert.That(staminaRegen.TargetResourceId, Is.EqualTo(ResourceIds.Stamina));
        }

        [Test]
        public void PreviewApplication_CreatesNoInstanceMutatesNoResourceAndEmitsNoEvents()
        {
            using OngoingFixture fixture = OngoingFixture.Create("preview");
            OngoingEffectDefinition poison = fixture.Get<OngoingEffectDefinition>("ongoing-effect.poison");
            int resourceEvents = 0;
            int applyEvents = 0;
            int tickEvents = 0;
            fixture.Resources.ResourceChanged += (_, _) => resourceEvents++;
            fixture.Service.OngoingEffectApplied += _ => applyEvents++;
            fixture.Service.OngoingEffectTickProcessed += _ => tickEvents++;

            OngoingEffectApplicationResult result = fixture.Service.PreviewApplyOngoingEffect(fixture.CreateRequest(poison, "tx.preview"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(fixture.Service.ActiveInstances.Count, Is.Zero);
            Assert.That(fixture.Health, Is.EqualTo(fixture.MaximumHealth).Within(0.001f));
            Assert.That(resourceEvents, Is.Zero);
            Assert.That(applyEvents, Is.Zero);
            Assert.That(tickEvents, Is.Zero);
        }

        [Test]
        public void ValidApplicationCreatesOneInstanceAndDuplicateApplicationDoesNotCreateSecond()
        {
            using OngoingFixture fixture = OngoingFixture.Create("duplicate-application");
            OngoingEffectDefinition poison = fixture.Get<OngoingEffectDefinition>("ongoing-effect.poison");

            OngoingEffectApplicationResult first = fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(poison, "tx.apply.same"));
            OngoingEffectApplicationResult duplicate = fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(poison, "tx.apply.same"));

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.Outcome, Is.EqualTo(OngoingEffectApplicationOutcome.Created));
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(fixture.Service.ActiveInstances.Count, Is.EqualTo(1));
        }

        [Test]
        public void ImmediateTickAppliesExactlyOnceAndNonImmediateWaitsForBoundary()
        {
            using OngoingFixture fixture = OngoingFixture.Create("immediate");
            OngoingEffectDefinition burning = fixture.Get<OngoingEffectDefinition>("ongoing-effect.burning");
            OngoingEffectDefinition poison = fixture.Get<OngoingEffectDefinition>("ongoing-effect.poison");

            OngoingEffectApplicationResult immediate = fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(burning, "tx.burning"));
            float afterImmediate = fixture.Health;
            OngoingEffectApplicationResult delayed = fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(poison, "tx.poison"));
            OngoingEffectProcessResult beforeBoundary = fixture.Service.AdvanceTime(0.5f);
            OngoingEffectProcessResult atBoundary = fixture.Service.AdvanceTime(0.5f);

            Assert.That(immediate.ImmediateTicks.Count, Is.EqualTo(1));
            Assert.That(afterImmediate, Is.LessThan(fixture.MaximumHealth));
            Assert.That(delayed.Succeeded, Is.True, delayed.Message);
            Assert.That(beforeBoundary.ProcessedTicks, Is.Zero);
            Assert.That(atBoundary.ProcessedTicks, Is.EqualTo(2), "Burning's second tick and Poison's first tick are both due at elapsed time 1.");
        }

        [Test]
        public void LargeElapsedUpdatesProcessDueTicksWithinCapAndReportCap()
        {
            using OngoingFixture fixture = OngoingFixture.Create("cap");
            OngoingEffectDefinition poison = fixture.Get<OngoingEffectDefinition>("ongoing-effect.poison");
            fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(poison, "tx.cap"));

            OngoingEffectProcessResult result = fixture.Service.AdvanceTime(100f);

            Assert.That(result.ProcessedTicks, Is.EqualTo(5));
            Assert.That(result.Capped, Is.False);

            using OngoingFixture cappedFixture = OngoingFixture.Create("capped");
            OngoingEffectDefinition longEffect = cappedFixture.CreateTemporaryEffect("ongoing-effect.test-cap", OngoingEffectOperationType.ResourceGain, ResourceIds.Mana, null, OngoingEffectStackingPolicy.IndependentInstances, tickCount: 40, duration: 40f);
            cappedFixture.Service.ApplyOngoingEffect(cappedFixture.CreateRequest(longEffect, "tx.cap2"));
            OngoingEffectProcessResult capped = cappedFixture.Service.AdvanceTime(40f);

            Assert.That(capped.Capped, Is.True);
            Assert.That(capped.ProcessedTicks, Is.EqualTo(cappedFixture.Service.MaximumTicksPerProcess));
            UnityEngine.Object.DestroyImmediate(longEffect);
        }

        [Test]
        public void TickIdsAreDeterministicDistinctAndDuplicateTickDoesNotApplyTwice()
        {
            using OngoingFixture fixture = OngoingFixture.Create("tick-id");
            OngoingEffectDefinition poison = fixture.Get<OngoingEffectDefinition>("ongoing-effect.poison");
            fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(poison, "tx.tick-id"));
            RuntimeOngoingEffectInstance instance = fixture.Service.ActiveInstances[0];

            string tickZero = fixture.Service.BuildTickTransactionId(instance, 0);
            string tickOne = fixture.Service.BuildTickTransactionId(instance, 1);
            OngoingEffectProcessResult first = fixture.Service.AdvanceTime(1f);
            float healthAfterFirst = fixture.Health;
            OngoingEffectProcessResult duplicateWindow = fixture.Service.ProcessDueTicks(0f);

            Assert.That(tickZero, Is.EqualTo(fixture.Service.BuildTickTransactionId(instance, 0)));
            Assert.That(tickZero, Is.Not.EqualTo(tickOne));
            Assert.That(first.ProcessedTicks, Is.EqualTo(1));
            Assert.That(duplicateWindow.ProcessedTicks, Is.Zero);
            Assert.That(fixture.Health, Is.EqualTo(healthAfterFirst).Within(0.001f));
        }

        [Test]
        public void DamageHealingAndResourceTicksUseApprovedPipelines()
        {
            using OngoingFixture fixture = OngoingFixture.Create("pipelines");
            fixture.Resources.TrySpend(ResourceIds.Mana, 30f, "test", "setup");
            fixture.Resources.TrySpend(ResourceIds.Stamina, 30f, "test", "setup");

            OngoingEffectDefinition poison = fixture.Get<OngoingEffectDefinition>("ongoing-effect.poison");
            OngoingEffectDefinition regen = fixture.Get<OngoingEffectDefinition>("ongoing-effect.health-regeneration");
            OngoingEffectDefinition mana = fixture.Get<OngoingEffectDefinition>("ongoing-effect.mana-regeneration");
            OngoingEffectDefinition stamina = fixture.Get<OngoingEffectDefinition>("ongoing-effect.stamina-regeneration");
            fixture.Resources.ApplyDamage(ResourceIds.Health, 30f, "test", "setup");

            fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(poison, "tx.pipeline.poison"));
            fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(regen, "tx.pipeline.regen"));
            fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(mana, "tx.pipeline.mana"));
            fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(stamina, "tx.pipeline.stamina"));
            OngoingEffectProcessResult result = fixture.Service.AdvanceTime(1f);

            Assert.That(result.TickResults, Has.Some.Matches<OngoingEffectTickResult>(tick => tick.DamageResult != null && tick.DamageResult.Request.DamageType.Id == "damage.magic.poison"));
            Assert.That(result.TickResults, Has.Some.Matches<OngoingEffectTickResult>(tick => tick.HealingResult != null && tick.HealingResult.FinalHealingAmount > 0f));
            Assert.That(result.TickResults, Has.Some.Matches<OngoingEffectTickResult>(tick => tick.ResourceResult != null && tick.ResourceResult.Request.ResourceId == ResourceIds.Mana));
            Assert.That(result.TickResults, Has.Some.Matches<OngoingEffectTickResult>(tick => tick.ResourceResult != null && tick.ResourceResult.Request.ResourceId == ResourceIds.Stamina));
            Assert.That(fixture.Resources.GetCurrent(ResourceIds.Mana), Is.LessThanOrEqualTo(fixture.Resources.GetMaximum(ResourceIds.Mana)));
            Assert.That(fixture.Resources.GetCurrent(ResourceIds.Stamina), Is.LessThanOrEqualTo(fixture.Resources.GetMaximum(ResourceIds.Stamina)));
        }

        [Test]
        public void MitigationResistanceAndImmunityAreReevaluatedEachDamageTick()
        {
            using OngoingFixture fixture = OngoingFixture.Create("reevaluate");
            OngoingEffectDefinition poison = fixture.Get<OngoingEffectDefinition>("ongoing-effect.poison");
            fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(poison, "tx.reevaluate"));

            OngoingEffectProcessResult first = fixture.Service.AdvanceTime(1f);
            fixture.GrantResistance(poison.DamageType, 1f, "trait.test.poison-resistance");
            OngoingEffectProcessResult second = fixture.Service.AdvanceTime(1f);
            fixture.GrantImmunity(poison.DamageType, "trait.test.poison-immunity");
            OngoingEffectProcessResult third = fixture.Service.AdvanceTime(1f);

            Assert.That(first.TickResults[0].DamageResult.FinalDamageAmount, Is.GreaterThan(0f));
            Assert.That(second.TickResults[0].DamageResult.FinalDamageAmount, Is.Zero);
            Assert.That(third.TickResults[0].DamageResult.Immune, Is.True);
            Assert.That(third.TickResults[0].DamageResult.HealthChanged, Is.False);
        }

        [Test]
        public void OngoingDamageReachingZeroTriggersLifecycleOnceAndHealingDoesNotRecover()
        {
            using OngoingFixture fixture = OngoingFixture.Create("lifecycle");
            OngoingEffectDefinition trueDamage = fixture.Get<OngoingEffectDefinition>("ongoing-effect.true-exposure");
            OngoingEffectDefinition regen = fixture.Get<OngoingEffectDefinition>("ongoing-effect.health-regeneration");
            int lifecycleEvents = 0;
            fixture.Lifecycle.ActorBecameUnconscious += _ => lifecycleEvents++;

            fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(trueDamage, "tx.true-damage", amount: fixture.MaximumHealth + 5f, tickCount: 1));
            fixture.Service.AdvanceTime(1f);
            fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(regen, "tx.heal-unconscious", amount: 25f, tickCount: 1));
            fixture.Service.AdvanceTime(1f);

            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Unconscious));
            Assert.That(lifecycleEvents, Is.EqualTo(1));
            Assert.That(fixture.Health, Is.GreaterThan(0f), "Healing-over-time may restore Health but must not recover lifecycle.");
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Unconscious));
        }

        [Test]
        public void StackingPoliciesAndCancellationBehaveDeterministically()
        {
            using OngoingFixture fixture = OngoingFixture.Create("stacking");
            OngoingEffectDefinition poison = fixture.Get<OngoingEffectDefinition>("ongoing-effect.poison");
            OngoingEffectDefinition reject = fixture.CreateTemporaryEffect("ongoing-effect.test-reject", OngoingEffectOperationType.Healing, ResourceIds.Health, null, OngoingEffectStackingPolicy.RejectDuplicate);
            try
            {
                fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(poison, "tx.stack.1", stacks: 1));
                OngoingEffectApplicationResult stacked = fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(poison, "tx.stack.2", stacks: 5));
                fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(reject, "tx.reject.1"));
                OngoingEffectApplicationResult rejected = fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(reject, "tx.reject.2"));
                RuntimeOngoingEffectInstance instance = fixture.Service.ActiveInstances[0];
                OngoingEffectCancellationResult cancel = fixture.Service.CancelOngoingEffect(new OngoingEffectCancellationRequest("tx.cancel", instance.InstanceId, instance.TargetActorId, instance.TargetObject));
                float healthBefore = fixture.Health;
                fixture.Service.AdvanceTime(2f);

                Assert.That(stacked.ResultingStackCount, Is.EqualTo(poison.MaximumStacks));
                Assert.That(rejected.Succeeded, Is.False);
                Assert.That(rejected.Code, Is.EqualTo(OngoingEffectResultCode.DuplicateRejected));
                Assert.That(cancel.Succeeded, Is.True, cancel.Message);
                Assert.That(fixture.Health, Is.EqualTo(healthBefore).Within(0.001f), "Cancelled damage instance must not tick later.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(reject);
            }
        }

        [Test]
        public void DeathPolicyCancelsEffectsAndDoesNotTransferToReplacementBody()
        {
            using OngoingFixture fixture = OngoingFixture.Create("death");
            using OngoingFixture replacement = OngoingFixture.Create("replacement");
            OngoingEffectDefinition poison = fixture.Get<OngoingEffectDefinition>("ongoing-effect.poison");
            fixture.Service.ApplyOngoingEffect(fixture.CreateRequest(poison, "tx.death"));

            fixture.Lifecycle.ExecuteDeath(new LifecycleDeathRequest("tx.death.lifecycle", "test", null, fixture.ActorId, fixture.Owner, LifecycleTriggerKind.ExplicitDeath));
            OngoingEffectProcessResult result = fixture.Service.AdvanceTime(1f);
            OngoingEffectsSaveData saveData = fixture.Service.CreateSaveData("player.local", fixture.ActorId);
            bool restoreToReplacement = replacement.Service.RestoreFromSaveData(saveData, fixture.Registry, replacement.Owner, "player.local", replacement.ActorId, out string failureReason, restoring: true);

            Assert.That(result.TickResults, Has.Some.Matches<OngoingEffectTickResult>(tick => tick.Outcome == OngoingEffectTickOutcome.Cancelled));
            Assert.That(fixture.Service.ActiveInstances.Count, Is.Zero);
            Assert.That(restoreToReplacement, Is.False);
            Assert.That(failureReason, Does.Contain("does not match"));
        }

        [Test]
        public void RestoreRecreatesActiveInstancesWithoutReplayOrOfflineTicks()
        {
            using OngoingFixture source = OngoingFixture.Create("restore-source");
            OngoingEffectDefinition poison = source.Get<OngoingEffectDefinition>("ongoing-effect.poison");
            source.Service.ApplyOngoingEffect(source.CreateRequest(poison, "tx.restore"));
            source.Service.AdvanceTime(0.5f);
            OngoingEffectsSaveData saveData = source.Service.CreateSaveData("player.local", source.ActorId);
            int resourceEvents = 0;
            source.Resources.ResourceChanged += (_, _) => resourceEvents++;

            bool success = source.Service.RestoreFromSaveData(saveData, source.Registry, source.Owner, "player.local", source.ActorId, out string failureReason, restoring: true);
            OngoingEffectProcessResult offline = source.Service.AdvanceTime(0f);

            Assert.That(success, Is.True, failureReason);
            Assert.That(resourceEvents, Is.Zero, "Restore must not replay application or tick mutations.");
            Assert.That(offline.ProcessedTicks, Is.Zero, "Offline time is not simulated; zero advance should not tick.");
            Assert.That(source.Service.ActiveInstances.Count, Is.EqualTo(1));
        }

        private sealed class OngoingFixture : System.IDisposable
        {
            private OngoingFixture(GameObject owner, DefinitionRegistry registry, CharacterResourceCollection resources, CharacterTraitCollection traits, CharacterCapabilityCollection capabilities, ActorLifecycleController lifecycle, OngoingEffectService service)
            {
                Owner = owner;
                Registry = registry;
                Resources = resources;
                Traits = traits;
                Capabilities = capabilities;
                Lifecycle = lifecycle;
                Service = service;
            }

            public GameObject Owner { get; }
            public DefinitionRegistry Registry { get; }
            public CharacterResourceCollection Resources { get; }
            public CharacterTraitCollection Traits { get; }
            public CharacterCapabilityCollection Capabilities { get; }
            public ActorLifecycleController Lifecycle { get; }
            public OngoingEffectService Service { get; }
            public string ActorId => Lifecycle.ActorId;
            public float Health => Resources.GetCurrent(ResourceIds.Health);
            public float MaximumHealth => Resources.GetMaximum(ResourceIds.Health);
            private readonly List<TraitDefinition> temporaryTraits = new List<TraitDefinition>();

            public static OngoingFixture Create(string id)
            {
                DefinitionRegistry registry = LoadCatalog().CreateRegistry();
                GameObject owner = new GameObject($"Ongoing Effects Test {id}");
                WorldEntityIdentity identity = owner.AddComponent<WorldEntityIdentity>();
                identity.TryInitializeRuntime($"entity.scene.prototype.ongoing-test.{id}", "scene.prototype", PersistenceService.LocalWorldId, PersistenceScope.RegionOrScene, "test.ongoing", out _);

                CharacterAttributes attributes = owner.AddComponent<CharacterAttributes>();
                CalculatedStatCollection stats = owner.AddComponent<CalculatedStatCollection>();
                CharacterTraitCollection traits = owner.AddComponent<CharacterTraitCollection>();
                UnityIsekaiGame.Capabilities.CharacterCapabilityCollection capabilities = owner.AddComponent<UnityIsekaiGame.Capabilities.CharacterCapabilityCollection>();
                CharacterResourceCollection resources = owner.AddComponent<CharacterResourceCollection>();
                ActorLifecycleController lifecycle = owner.AddComponent<ActorLifecycleController>();
                OngoingEffectService service = owner.AddComponent<OngoingEffectService>();

                attributes.Configure(registry);
                stats.Configure(registry, attributes);
                capabilities.Configure(registry);
                traits.Configure(registry, stats, null, capabilities, "player.local");
                resources.Configure(registry, stats, "player.local");
                lifecycle.Configure(null, resources, null, traits);
                service.Configure(null);
                service.SetClock(0f);
                return new OngoingFixture(owner, registry, resources, traits, capabilities, lifecycle, service);
            }

            public TDefinition Get<TDefinition>(string id)
                where TDefinition : class, IGameDefinition
            {
                Assert.That(Registry.TryGet(id, out TDefinition definition), Is.True, id);
                return definition;
            }

            public OngoingEffectApplicationRequest CreateRequest(OngoingEffectDefinition definition, string transactionId, float amount = 0f, int tickCount = 0, int stacks = 1)
            {
                return new OngoingEffectApplicationRequest(transactionId, definition, "test.source", null, ActorId, Owner, "test.origin", amount, 0f, 0f, tickCount, stacks, authorityValidated: true);
            }

            public OngoingEffectDefinition CreateTemporaryEffect(string id, OngoingEffectOperationType operation, string resourceId, DamageTypeDefinition damageType, OngoingEffectStackingPolicy stacking, int tickCount = 1, float duration = 1f)
            {
                OngoingEffectDefinition definition = ScriptableObject.CreateInstance<OngoingEffectDefinition>();
                SerializedObject serialized = new SerializedObject(definition);
                serialized.FindProperty("ongoingEffectId").stringValue = id;
                serialized.FindProperty("displayName").stringValue = id;
                serialized.FindProperty("operationType").enumValueIndex = (int)operation;
                serialized.FindProperty("targetResource").objectReferenceValue = Get<ResourceDefinition>(resourceId);
                serialized.FindProperty("damageType").objectReferenceValue = damageType;
                serialized.FindProperty("amountPerTick").floatValue = 1f;
                serialized.FindProperty("tickInterval").floatValue = 1f;
                serialized.FindProperty("initialDelay").floatValue = 1f;
                serialized.FindProperty("totalDuration").floatValue = duration;
                serialized.FindProperty("useFiniteTickCount").boolValue = tickCount > 0;
                serialized.FindProperty("finiteTickCount").intValue = Mathf.Max(1, tickCount);
                serialized.FindProperty("stackingPolicy").enumValueIndex = (int)stacking;
                serialized.FindProperty("maximumStacks").intValue = stacking == OngoingEffectStackingPolicy.AddStacks ? 3 : 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return definition;
            }

            public void GrantResistance(DamageTypeDefinition damageType, float resistance, string traitId)
            {
                TraitDefinition trait = CreateTemporaryResistanceTrait(damageType, resistance, immunity: false, traitId);
                temporaryTraits.Add(trait);
                Traits.Configure(temporaryTraits, null, null, Capabilities, "player.local");
                TraitOperationResult result = Traits.GrantTrait(new TraitGrantRequest
                {
                    OwnerId = "player.local",
                    TraitDefinitionId = trait.Id,
                    SourceId = trait.Id,
                    SourceCategory = TraitSourceCategory.Development,
                    RequestedLifecycle = TraitLifecycleState.Active,
                    RequestedDiscovery = TraitDiscoveryState.Discovered
                });
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public void GrantImmunity(DamageTypeDefinition damageType, string traitId)
            {
                TraitDefinition trait = CreateTemporaryResistanceTrait(damageType, 1f, immunity: true, traitId);
                temporaryTraits.Add(trait);
                Traits.Configure(temporaryTraits, null, null, Capabilities, "player.local");
                TraitOperationResult result = Traits.GrantTrait(new TraitGrantRequest
                {
                    OwnerId = "player.local",
                    TraitDefinitionId = trait.Id,
                    SourceId = trait.Id,
                    SourceCategory = TraitSourceCategory.Development,
                    RequestedLifecycle = TraitLifecycleState.Active,
                    RequestedDiscovery = TraitDiscoveryState.Discovered
                });
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            private static TraitDefinition CreateTemporaryResistanceTrait(DamageTypeDefinition damageType, float resistance, bool immunity, string traitId)
            {
                TraitDefinition trait = ScriptableObject.CreateInstance<TraitDefinition>();
                SerializedObject serialized = new SerializedObject(trait);
                serialized.FindProperty("traitId").stringValue = traitId;
                serialized.FindProperty("displayName").stringValue = traitId;
                SerializedProperty grants = serialized.FindProperty(immunity ? "immunityGrants" : "resistanceGrants");
                grants.arraySize = 1;
                SerializedProperty grant = grants.GetArrayElementAtIndex(0);
                grant.FindPropertyRelative("entryId").stringValue = $"{traitId}.grant";
                grant.FindPropertyRelative("damageType").objectReferenceValue = damageType;
                grant.FindPropertyRelative("resistanceFraction").floatValue = Mathf.Clamp01(resistance);
                grant.FindPropertyRelative("immunity").boolValue = immunity;
                grant.FindPropertyRelative("alphaEnabled").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return trait;
            }

            public void Dispose()
            {
                for (int i = 0; i < temporaryTraits.Count; i++)
                {
                    UnityEngine.Object.DestroyImmediate(temporaryTraits[i]);
                }

                UnityEngine.Object.DestroyImmediate(Owner);
            }
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }
    }
}
