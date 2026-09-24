using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class ActorLifecycleTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PreviewDefeat_DoesNotMutateOrEmitEvents()
        {
            using LifecycleFixture fixture = LifecycleFixture.Create("preview");
            int resourceEvents = 0;
            int lifecycleEvents = 0;
            fixture.Resources.ResourceChanged += (_, _) => resourceEvents++;
            fixture.Lifecycle.ActorBecameUnconscious += _ => lifecycleEvents++;

            ActorLifecycleResult preview = fixture.Lifecycle.PreviewDefeat(new DefeatResolutionRequest(string.Empty, "test", null, fixture.ActorId, fixture.Owner, LifecycleTriggerKind.ExplicitDefeat));

            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(preview.ResultingState, Is.EqualTo(ActorLifecycleState.Unconscious));
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Active));
            Assert.That(fixture.Health, Is.EqualTo(fixture.MaximumHealth).Within(0.001f));
            Assert.That(resourceEvents, Is.Zero);
            Assert.That(lifecycleEvents, Is.Zero);
        }

        [Test]
        public void HealthZeroExecution_TransitionsOnceAndDuplicateDoesNotReplay()
        {
            using LifecycleFixture fixture = LifecycleFixture.Create("zero-health");
            DamageHealingService service = new DamageHealingService();
            int lifecycleEvents = 0;
            fixture.Lifecycle.ActorBecameUnconscious += _ => lifecycleEvents++;

            DamageApplicationResult first = service.ApplyDamage(fixture.CreateDamageRequest(fixture.MaximumHealth + 50f, "tx.lifecycle.zero"));
            DamageApplicationResult duplicate = service.ApplyDamage(fixture.CreateDamageRequest(fixture.MaximumHealth + 50f, "tx.lifecycle.zero"));

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.BecameZero, Is.True);
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Unconscious));
            Assert.That(lifecycleEvents, Is.EqualTo(1));
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(lifecycleEvents, Is.EqualTo(1));
        }

        [Test]
        public void Recovery_UsesResourceTransactionOnceAndRestoresActiveState()
        {
            using LifecycleFixture fixture = LifecycleFixture.Create("recover");
            new DamageHealingService().ApplyDamage(fixture.CreateDamageRequest(fixture.MaximumHealth + 10f, "tx.lifecycle.recover.setup"));
            int resourceEvents = 0;
            fixture.Resources.ResourceChanged += (_, _) => resourceEvents++;

            ActorLifecycleResult first = fixture.Lifecycle.ExecuteRecovery(new LifecycleRecoveryRequest("tx.lifecycle.recover", "test", null, fixture.ActorId, fixture.Owner, 25f));
            ActorLifecycleResult duplicate = fixture.Lifecycle.ExecuteRecovery(new LifecycleRecoveryRequest("tx.lifecycle.recover", "test", null, fixture.ActorId, fixture.Owner, 25f));

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.ResourceResult, Is.Not.Null);
            Assert.That(first.ResourceResult.DuplicateEvent, Is.False);
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Active));
            Assert.That(fixture.Health, Is.GreaterThan(0f));
            Assert.That(resourceEvents, Is.EqualTo(1));
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(resourceEvents, Is.EqualTo(1));
        }

        [Test]
        public void StaleActorRequest_IsRejectedWithoutMutation()
        {
            using LifecycleFixture fixture = LifecycleFixture.Create("stale");

            ActorLifecycleResult result = fixture.Lifecycle.ExecuteDefeat(new DefeatResolutionRequest("tx.lifecycle.stale", "test", null, "entity.scene.prototype.some-other-body", fixture.Owner, LifecycleTriggerKind.ExplicitDefeat));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ActorLifecycleResultCode.StaleActor));
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Active));
            Assert.That(fixture.Health, Is.EqualTo(fixture.MaximumHealth).Within(0.001f));
        }

        [Test]
        public void DeathAndRevival_UseValidatedResourceTransactions()
        {
            using LifecycleFixture fixture = LifecycleFixture.Create("death-revival");

            ActorLifecycleResult death = fixture.Lifecycle.ExecuteDeath(new LifecycleDeathRequest("tx.lifecycle.death", "test", null, fixture.ActorId, fixture.Owner, LifecycleTriggerKind.ExplicitDeath));
            ActorLifecycleResult previewRevival = fixture.Lifecycle.PreviewRevival(new LifecycleRevivalRequest(string.Empty, "test", null, fixture.ActorId, fixture.Owner, 20f));

            Assert.That(death.Succeeded, Is.True, death.Message);
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Dead));
            Assert.That(fixture.Health, Is.Zero.Within(0.001f));
            Assert.That(previewRevival.Succeeded, Is.True, previewRevival.Message);
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Dead), "Preview must not mutate state.");

            ActorLifecycleResult revival = fixture.Lifecycle.ExecuteRevival(new LifecycleRevivalRequest("tx.lifecycle.revival", "test", null, fixture.ActorId, fixture.Owner, 20f));

            Assert.That(revival.Succeeded, Is.True, revival.Message);
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Active));
            Assert.That(fixture.Health, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void ResetToActiveForRestoreClearsLifecycleTransactionDedupeWithoutGameplayTransition()
        {
            using LifecycleFixture fixture = LifecycleFixture.Create("reset");
            int recoveryEvents = 0;
            fixture.Lifecycle.ActorRecovered += _ => recoveryEvents++;

            ActorLifecycleResult first = fixture.Lifecycle.ExecuteDefeat(new DefeatResolutionRequest("tx.lifecycle.reset", "test", null, fixture.ActorId, fixture.Owner, LifecycleTriggerKind.ExplicitDefeat));
            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Unconscious));

            fixture.Lifecycle.ResetToActiveForRestore();

            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Active));
            Assert.That(recoveryEvents, Is.Zero, "Prototype restore reset must not emit gameplay recovery events.");

            ActorLifecycleResult afterReset = fixture.Lifecycle.ExecuteDefeat(new DefeatResolutionRequest("tx.lifecycle.reset", "test", null, fixture.ActorId, fixture.Owner, LifecycleTriggerKind.ExplicitDefeat));
            Assert.That(afterReset.Succeeded, Is.True, afterReset.Message);
            Assert.That(afterReset.Duplicate, Is.False, "Reset must clear processed lifecycle transaction IDs.");
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Unconscious));
        }

        [Test]
        public void LifecycleSaveData_RestoresStateWithoutHealthReplayAndRejectsWrongActor()
        {
            using LifecycleFixture source = LifecycleFixture.Create("save-source");
            using LifecycleFixture restored = LifecycleFixture.Create("save-restored");
            new DamageHealingService().ApplyDamage(source.CreateDamageRequest(source.MaximumHealth + 1f, "tx.lifecycle.save.setup"));
            ActorLifecycleSaveData saveData = source.Lifecycle.CreateSaveData("player.local", "person.test");

            bool rejected = ActorLifecycleController.ValidateSaveData(saveData, "player.local", "entity.scene.prototype.wrong", out string failureReason);
            bool restoredState = source.Lifecycle.RestoreFromSaveData(saveData, "player.local", source.ActorId, out string restoreFailure, restoring: true);

            Assert.That(rejected, Is.False);
            Assert.That(failureReason, Does.Contain("does not match"));
            Assert.That(restoredState, Is.True, restoreFailure);
            Assert.That(source.Lifecycle.State, Is.EqualTo(ActorLifecycleState.Unconscious));
            Assert.That(source.Health, Is.Zero.Within(0.001f), "Lifecycle restore must not duplicate or replay Health persistence.");
        }

        [Test]
        public void DefeatPolicyValidation_RejectsContradictoryImmediateDeathPolicy()
        {
            DefeatPolicyDefinition policy = ScriptableObject.CreateInstance<DefeatPolicyDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(policy);
                serialized.FindProperty("policyId").stringValue = "defeat-policy.test-invalid";
                serialized.FindProperty("displayName").stringValue = "Invalid Policy";
                serialized.FindProperty("zeroHealthOutcome").enumValueIndex = (int)DefeatPolicyOutcome.DieImmediately;
                serialized.FindProperty("allowDeath").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DefinitionValidationReport report = DefinitionCatalogValidator.Validate(ClassificationTestFactory.CreateCatalog(policy));

                Assert.That(report.ErrorCount, Is.GreaterThan(0), report.GetSummary());
                Assert.That(report.GetSummary(), Does.Contain("cannot die immediately"));
            }
            finally
            {
                Object.DestroyImmediate(policy);
            }
        }

        private sealed class LifecycleFixture : System.IDisposable
        {
            private LifecycleFixture(GameObject owner, DamageTypeDefinition damageType, CharacterResourceCollection resources, ActorLifecycleController lifecycle)
            {
                Owner = owner;
                DamageType = damageType;
                Resources = resources;
                Lifecycle = lifecycle;
            }

            public GameObject Owner { get; }
            public DamageTypeDefinition DamageType { get; }
            public CharacterResourceCollection Resources { get; }
            public ActorLifecycleController Lifecycle { get; }
            public string ActorId => Lifecycle.ActorId;
            public float Health => Resources.GetCurrent(ResourceIds.Health);
            public float MaximumHealth => Resources.GetMaximum(ResourceIds.Health);

            public static LifecycleFixture Create(string id)
            {
                DefinitionRegistry registry = LoadCatalog().CreateRegistry();
                GameObject owner = new GameObject($"Lifecycle Test {id}");
                WorldEntityIdentity identity = owner.AddComponent<WorldEntityIdentity>();
                identity.TryInitializeRuntime($"entity.scene.prototype.lifecycle-test.{id}", "scene.prototype", PersistenceService.LocalWorldId, PersistenceScope.RegionOrScene, "test.lifecycle", out _);

                CharacterAttributes attributes = owner.AddComponent<CharacterAttributes>();
                CalculatedStatCollection stats = owner.AddComponent<CalculatedStatCollection>();
                CharacterTraitCollection traits = owner.AddComponent<CharacterTraitCollection>();
                CharacterResourceCollection resources = owner.AddComponent<CharacterResourceCollection>();
                ActorLifecycleController lifecycle = owner.AddComponent<ActorLifecycleController>();

                attributes.Configure(registry);
                stats.Configure(registry, attributes);
                traits.Configure(registry, stats, null, "player.local");
                resources.Configure(registry, stats, "player.local");
                lifecycle.Configure(null, resources, null, traits);

                Assert.That(registry.TryGet("damage.physical", out DamageTypeDefinition damageType), Is.True);
                return new LifecycleFixture(owner, damageType, resources, lifecycle);
            }

            public DamageApplicationRequest CreateDamageRequest(float amount, string transactionId)
            {
                return new DamageApplicationRequest(transactionId, "test.source", null, ActorId, Owner, DamageType, amount, "Lifecycle test", authorityValidated: true);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Owner);
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
