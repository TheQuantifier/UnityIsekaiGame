using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.Contributions;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.Combat.Integration;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.Combat.Reactions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class CombatSystemIntegrationFinalizationTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void FacadeReadiness_ReportsMissingRequiredServices()
        {
            using Fixture fixture = Fixture.Create();
            CombatRuntimeFacade facade = new CombatRuntimeFacade(fixture.Registry, fixture.Actor, new DamageHealingService(), new DefensiveActionService(), null, new CombatExecutionService(), null, null, null);

            CombatReadinessResult result = facade.EvaluateReadiness(fixture.Actor);

            Assert.That(result.State, Is.EqualTo(CombatRuntimeReadinessState.Invalid));
            Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "MissingCombatStateService"), Is.True);
            Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "MissingOngoingEffectService"), Is.True);
            Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "MissingCombatContributionService"), Is.True);
        }

        [Test]
        public void FacadeReadiness_ReadyWithComposedStep6Services()
        {
            using Fixture fixture = Fixture.Create();

            CombatReadinessResult result = fixture.Facade.EvaluateReadiness(fixture.Actor);

            Assert.That(result.State, Is.EqualTo(CombatRuntimeReadinessState.Ready), string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Severity == CombatIntegritySeverity.Error), Is.False);
        }

        [Test]
        public void CombinedSnapshot_IsImmutableAndDoesNotExposeReactionGameObjects()
        {
            using Fixture fixture = Fixture.Create();

            CombatRuntimeSnapshot snapshot = fixture.Facade.CreateSnapshot(fixture.Actor);

            Assert.That(snapshot.Resources, Is.Not.SameAs(fixture.Resources.GetSnapshots()));
            Assert.That(typeof(CombatReactionSourceSnapshot).GetProperties().Any(property => typeof(GameObject).IsAssignableFrom(property.PropertyType)), Is.False);
            Assert.That(snapshot.ActorId, Is.EqualTo(fixture.ActorId));
        }

        [Test]
        public void PreviewAttack_UsesSharedCalculationWithoutMutationOrTrace()
        {
            using Fixture fixture = Fixture.Create();
            float healthBefore = fixture.Resources.GetCurrent(ResourceIds.Health);

            AttackResolutionResult preview = fixture.Facade.PreviewAttack(fixture.Attack("tx.integration.preview", 25f));

            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(fixture.Resources.GetCurrent(ResourceIds.Health), Is.EqualTo(healthBefore).Within(0.001f));
            Assert.That(fixture.Facade.LastTransactionTrace.RootTransactionId, Is.Empty);
        }

        [Test]
        public void ExecuteAttack_AppliesDamageOnceAndRecordsTrace()
        {
            using Fixture fixture = Fixture.Create();
            float healthBefore = fixture.Resources.GetCurrent(ResourceIds.Health);

            AttackResolutionResult result = fixture.Facade.ExecuteAttack(fixture.Attack("tx.integration.execute", 25f));
            AttackResolutionResult duplicate = fixture.Facade.ExecuteAttack(fixture.Attack("tx.integration.execute", 25f));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.DamageResult, Is.Not.Null);
            Assert.That(fixture.Resources.GetCurrent(ResourceIds.Health), Is.LessThan(healthBefore));
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(fixture.Resources.GetCurrent(ResourceIds.Health), Is.EqualTo(result.DamageResult.NewHealth).Within(0.001f));
            Assert.That(fixture.Facade.LastTransactionTrace.RootTransactionId, Is.EqualTo("tx.integration.execute"));
            Assert.That(fixture.Facade.LastTransactionTrace.DamageTransactionId, Is.Not.Empty);
        }

        [Test]
        public void RestoreClear_RemovesTransientCombatRuntimeStateSilently()
        {
            using Fixture fixture = Fixture.Create();
            fixture.Contributions.RecordContribution(new CombatContributionRecordRequest(
                "tx.integration.contribution",
                CombatContributionType.DamageApplied,
                fixture.ActorId,
                string.Empty,
                string.Empty,
                fixture.ActorId,
                "encounter.integration",
                10f,
                10f,
                0f,
                10f,
                CombatContributionSourceKind.Direct,
                "tx.integration.damage",
                string.Empty,
                "test.integration",
                fixture.DamageType.Id,
                authorityValidated: true));

            fixture.Facade.ClearTransientStateForRestore(fixture.ActorId);
            CombatRuntimeSnapshot snapshot = fixture.Facade.CreateSnapshot(fixture.Actor);

            Assert.That(snapshot.ContributionLedgers, Is.Empty);
            Assert.That(snapshot.ActiveDefense, Is.Null);
            Assert.That(snapshot.ActiveExecution, Is.Null);
            Assert.That(snapshot.ActiveOngoingEffects, Is.Empty);
            Assert.That(snapshot.ReactionSources, Is.Empty);
        }

        [Test]
        public void TransactionValidator_ReportsUnexpectedChildAncestry()
        {
            CombatTransactionTraceSnapshot trace = new CombatTransactionTraceSnapshot("tx.root", string.Empty, "tx.other.attack", string.Empty, "tx.root.damage", string.Empty, string.Empty, null);

            CombatIntegrityReport report = CombatTransactionValidator.Validate(trace);

            Assert.That(report.Passed, Is.True);
            Assert.That(report.Diagnostics.Any(diagnostic => diagnostic.Code == "UnexpectedTransactionAncestry"), Is.True);
        }

        [Test]
        public void GameplayAssembly_DoesNotReferenceDevelopmentTestLabOrUi()
        {
            string gameplayAsmdef = System.IO.File.ReadAllText("Assets/_Project/Runtime/UnityIsekaiGame.Gameplay.asmdef");

            Assert.That(gameplayAsmdef, Does.Not.Contain("UnityIsekaiGame.Development"));
            Assert.That(gameplayAsmdef, Does.Not.Contain("UnityIsekaiGame.Tests"));
            Assert.That(gameplayAsmdef, Does.Not.Contain("UnityIsekaiGame.UI"));
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject host;

            private Fixture(DefinitionRegistry registry, GameObject actor, GameObject target, string actorId, string targetId, DamageTypeDefinition damageType, CharacterResourceCollection resources, CombatRuntimeFacade facade, CombatContributionService contributions, GameObject host)
            {
                Registry = registry;
                Actor = actor;
                Target = target;
                ActorId = actorId;
                TargetId = targetId;
                DamageType = damageType;
                Resources = resources;
                Facade = facade;
                Contributions = contributions;
                this.host = host;
            }

            public DefinitionRegistry Registry { get; }
            public GameObject Actor { get; }
            public GameObject Target { get; }
            public string ActorId { get; }
            public string TargetId { get; }
            public DamageTypeDefinition DamageType { get; }
            public CharacterResourceCollection Resources { get; }
            public CombatRuntimeFacade Facade { get; }
            public CombatContributionService Contributions { get; }

            public static Fixture Create()
            {
                DefinitionRegistry registry = LoadCatalog().CreateRegistry();
                Assert.That(registry.TryGet("damage.physical", out DamageTypeDefinition damageType), Is.True);
                GameObject host = new GameObject("Combat Integration Host");
                GameObject actor = new GameObject("Combat Integration Actor");
                GameObject target = new GameObject("Combat Integration Target");
                ConfigureActor(registry, actor, "actor", out string actorId);
                CharacterResourceCollection targetResources = ConfigureActor(registry, target, "target", out string targetId);
                CombatStateService combatState = host.AddComponent<CombatStateService>();
                combatState.Configure(registry.DefinitionsById.Values.OfType<CombatStatePolicyDefinition>().FirstOrDefault());
                OngoingEffectService ongoing = host.AddComponent<OngoingEffectService>();
                ongoing.Configure(null);
                CombatReactionService reactions = host.AddComponent<CombatReactionService>();
                CombatContributionService contributions = host.AddComponent<CombatContributionService>();
                contributions.Configure(registry.DefinitionsById.Values.OfType<CombatContributionPolicyDefinition>().FirstOrDefault());
                DamageHealingService damageHealing = new DamageHealingService();
                DefensiveActionService defense = new DefensiveActionService();
                CombatExecutionService execution = new CombatExecutionService();
                AttackResolutionService attack = new AttackResolutionService(damageHealing, defense, combatState);
                CombatRuntimeFacade facade = new CombatRuntimeFacade(registry, actor, damageHealing, defense, combatState, execution, ongoing, reactions, contributions, attack);
                return new Fixture(registry, actor, target, actorId, targetId, damageType, targetResources, facade, contributions, host);
            }

            public AttackResolutionRequest Attack(string transactionId, float amount)
            {
                return new AttackResolutionRequest(
                    transactionId,
                    AttackSourceType.Weapon,
                    Actor,
                    ActorId,
                    Target,
                    TargetId,
                    DamageType,
                    amount,
                    hitRoll: 0.1f,
                    criticalRoll: 0.99f,
                    baseHitChance: 0.95f,
                    criticalChance: 0f,
                    criticalMultiplier: 1.5f,
                    hasSuppliedDistance: true,
                    suppliedDistance: 1f,
                    hasMaximumRange: true,
                    maximumRange: 2f,
                    originatingActionId: "test.integration.attack",
                    authorityValidated: true);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(Actor);
                UnityEngine.Object.DestroyImmediate(Target);
            }

            private static CharacterResourceCollection ConfigureActor(DefinitionRegistry registry, GameObject owner, string label, out string actorId)
            {
                CharacterAttributes attributes = owner.AddComponent<CharacterAttributes>();
                CalculatedStatCollection stats = owner.AddComponent<CalculatedStatCollection>();
                CharacterResourceCollection resources = owner.AddComponent<CharacterResourceCollection>();
                ActorLifecycleController lifecycle = owner.AddComponent<ActorLifecycleController>();
                WorldEntityIdentity identity = owner.AddComponent<WorldEntityIdentity>();
                Assert.That(identity.TrySetAuthoredIdentity($"combat-integration-{label}-{Guid.NewGuid():N}", "scene.test", PersistenceScope.RegionOrScene, "test.combat-integration", out string failureReason), Is.True, failureReason);
                actorId = identity.EntityId;
                attributes.Configure(registry);
                stats.Configure(registry, attributes);
                resources.Configure(registry, stats, "player.local");
                lifecycle.Configure(null, resources, null, null);
                return resources;
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
