using System;
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
    public sealed class AttackResolutionCombatOutcomeTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PreviewAttack_ReturnsDeterministicOutcomeAndDoesNotCallExecution()
        {
            using AttackFixture fixture = AttackFixture.Create(accuracy: 10f, evasion: 0f);
            FakeDamageHealingService damage = new FakeDamageHealingService();
            AttackResolutionService service = new AttackResolutionService(damage);

            AttackResolutionResult result = service.PreviewAttack(fixture.CreateRequest("attack.preview", hitRoll: 0.2f));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Outcome, Is.EqualTo(AttackOutcome.Hit));
            Assert.That(result.FinalHitChance, Is.EqualTo(0.85f).Within(0.001f));
            Assert.That(damage.PreviewDamageCalls, Is.EqualTo(1));
            Assert.That(damage.ApplyDamageCalls, Is.Zero);
        }

        [Test]
        public void PreviewAttack_EmitsNoExecutionEvents()
        {
            using AttackFixture fixture = AttackFixture.Create();
            AttackResolutionService service = new AttackResolutionService(new FakeDamageHealingService());
            int processed = 0;
            int hit = 0;
            service.AttackProcessed += _ => processed++;
            service.AttackHit += _ => hit++;

            service.PreviewAttack(fixture.CreateRequest("attack.preview.events", hitRoll: 0.1f));

            Assert.That(processed, Is.Zero);
            Assert.That(hit, Is.Zero);
        }

        [Test]
        public void ExecuteAttack_RechecksStateAfterPreview()
        {
            using AttackFixture fixture = AttackFixture.Create(accuracy: 0f, evasion: 0f);
            FakeDamageHealingService damage = new FakeDamageHealingService();
            AttackResolutionService service = new AttackResolutionService(damage);
            AttackResolutionRequest request = fixture.CreateRequest("attack.recheck", hitRoll: 0.7f);

            AttackResolutionResult preview = service.PreviewAttack(request);
            fixture.AddTargetEvasion(50f, "after-preview");
            AttackResolutionResult execute = service.ExecuteAttack(request);

            Assert.That(preview.Outcome, Is.EqualTo(AttackOutcome.Hit));
            Assert.That(execute.Outcome, Is.EqualTo(AttackOutcome.Miss));
            Assert.That(damage.ApplyDamageCalls, Is.Zero);
        }

        [Test]
        public void AccuracyIncreasesAndEvasionDecreasesHitChance()
        {
            using AttackFixture baseline = AttackFixture.Create(accuracy: 0f, evasion: 0f);
            using AttackFixture fixture = AttackFixture.Create(accuracy: 20f, evasion: 30f);
            AttackResolutionService service = new AttackResolutionService(new FakeDamageHealingService());

            AttackResolutionResult baselineResult = service.PreviewAttack(baseline.CreateRequest("attack.stats.baseline", hitRoll: 0.1f));
            AttackResolutionResult result = service.PreviewAttack(fixture.CreateRequest("attack.stats", hitRoll: 0.1f));

            Assert.That(result.AttackerAccuracy, Is.GreaterThan(baselineResult.AttackerAccuracy));
            Assert.That(result.TargetEvasion, Is.GreaterThan(baselineResult.TargetEvasion));
            Assert.That(result.NormalizedAccuracyContribution, Is.EqualTo(result.AttackerAccuracy / AttackResolutionService.WholeNumberStatScaleDivisor).Within(0.001f));
            Assert.That(result.NormalizedEvasionContribution, Is.EqualTo(result.TargetEvasion / AttackResolutionService.WholeNumberStatScaleDivisor).Within(0.001f));
            Assert.That(result.FinalHitChance, Is.EqualTo(Mathf.Clamp(result.BaseHitChance + result.NormalizedAccuracyContribution - result.NormalizedEvasionContribution, AttackResolutionService.DefaultMinimumHitChance, AttackResolutionService.DefaultMaximumHitChance)).Within(0.001f));
            Assert.That(result.FinalHitChance, Is.LessThan(baselineResult.FinalHitChance));
        }

        [Test]
        public void HitChanceClampsToMinimumAndMaximum()
        {
            using AttackFixture minimum = AttackFixture.Create(accuracy: 0f, evasion: 500f);
            using AttackFixture maximum = AttackFixture.Create(accuracy: 500f, evasion: 0f);
            AttackResolutionService service = new AttackResolutionService(new FakeDamageHealingService());

            AttackResolutionResult min = service.PreviewAttack(minimum.CreateRequest("attack.min", hitRoll: 0.01f));
            AttackResolutionResult max = service.PreviewAttack(maximum.CreateRequest("attack.max", hitRoll: 0.94f));

            Assert.That(min.FinalHitChance, Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(max.FinalHitChance, Is.EqualTo(0.95f).Within(0.001f));
        }

        [Test]
        public void InvalidRollIsRejected()
        {
            using AttackFixture fixture = AttackFixture.Create();

            AttackResolutionResult result = new AttackResolutionService(new FakeDamageHealingService()).PreviewAttack(fixture.CreateRequest("attack.bad-roll", hitRoll: 1f));

            Assert.That(result.Outcome, Is.EqualTo(AttackOutcome.Invalid));
            Assert.That(result.Code, Is.EqualTo(AttackResolutionResultCode.InvalidRoll));
        }

        [Test]
        public void RollBelowChanceHitsAndRollAtChanceMisses()
        {
            using AttackFixture fixture = AttackFixture.Create();
            AttackResolutionService service = new AttackResolutionService(new FakeDamageHealingService());

            AttackResolutionResult hit = service.PreviewAttack(fixture.CreateRequest("attack.boundary.hit", hitRoll: 0.749f, baseHitChance: 0.75f));
            AttackResolutionResult miss = service.PreviewAttack(fixture.CreateRequest("attack.boundary.miss", hitRoll: 0.75f, baseHitChance: 0.75f));

            Assert.That(hit.Outcome, Is.EqualTo(AttackOutcome.Hit));
            Assert.That(miss.Outcome, Is.EqualTo(AttackOutcome.Miss));
        }

        [Test]
        public void MissDoesNotCallDamageExecution()
        {
            using AttackFixture fixture = AttackFixture.Create();
            FakeDamageHealingService damage = new FakeDamageHealingService();

            AttackResolutionResult result = new AttackResolutionService(damage).ExecuteAttack(fixture.CreateRequest("attack.miss", hitRoll: 0.99f));

            Assert.That(result.Outcome, Is.EqualTo(AttackOutcome.Miss));
            Assert.That(damage.ApplyDamageCalls, Is.Zero);
        }

        [Test]
        public void ExecuteRejectsUnvalidatedAuthorityBeforeDamage()
        {
            using AttackFixture fixture = AttackFixture.Create();
            FakeDamageHealingService damage = new FakeDamageHealingService();

            AttackResolutionResult result = new AttackResolutionService(damage).ExecuteAttack(
                fixture.CreateRequest("attack.authority", hitRoll: 0.1f, authorityValidated: false));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(AttackResolutionResultCode.AuthorityRequired));
            Assert.That(damage.ApplyDamageCalls, Is.Zero);
        }

        [Test]
        public void HitCallsDamageHealingServiceExactlyOnce()
        {
            using AttackFixture fixture = AttackFixture.Create();
            FakeDamageHealingService damage = new FakeDamageHealingService();

            AttackResolutionResult result = new AttackResolutionService(damage).ExecuteAttack(fixture.CreateRequest("attack.hit.once", hitRoll: 0.1f));

            Assert.That(result.Outcome, Is.EqualTo(AttackOutcome.Hit));
            Assert.That(damage.ApplyDamageCalls, Is.EqualTo(1));
        }

        [Test]
        public void CriticalHitAppliesMultiplierBeforeMitigation()
        {
            using AttackFixture fixture = AttackFixture.Create();
            FakeDamageHealingService damage = new FakeDamageHealingService();

            AttackResolutionResult result = new AttackResolutionService(damage).ExecuteAttack(fixture.CreateRequest("attack.critical", baseDamage: 10f, hitRoll: 0.1f, criticalChance: 0.5f, criticalRoll: 0.1f, criticalMultiplier: 2f));

            Assert.That(result.Outcome, Is.EqualTo(AttackOutcome.CriticalHit));
            Assert.That(result.DamageAfterCritical, Is.EqualTo(20f).Within(0.001f));
            Assert.That(damage.LastDamageRequest.RequestedAmount, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void CriticalIsNotEvaluatedOnMiss()
        {
            using AttackFixture fixture = AttackFixture.Create();
            FakeDamageHealingService damage = new FakeDamageHealingService();

            AttackResolutionResult result = new AttackResolutionService(damage).ExecuteAttack(fixture.CreateRequest("attack.critical.miss", hitRoll: 0.99f, criticalChance: 1f, criticalRoll: 0f));

            Assert.That(result.Outcome, Is.EqualTo(AttackOutcome.Miss));
            Assert.That(result.Critical, Is.False);
            Assert.That(damage.ApplyDamageCalls, Is.Zero);
        }

        [Test]
        public void LogicalHitCanApplyZeroDamageWithoutBecomingMiss()
        {
            using AttackFixture fixture = AttackFixture.Create();
            FakeDamageHealingService damage = new FakeDamageHealingService { FinalDamageAmount = 0f, HealthChanged = false };

            AttackResolutionResult result = new AttackResolutionService(damage).ExecuteAttack(fixture.CreateRequest("attack.zero-damage", baseDamage: 0f, hitRoll: 0.1f));

            Assert.That(result.Outcome, Is.EqualTo(AttackOutcome.Hit));
            Assert.That(result.DamagePrevented, Is.True);
        }

        [Test]
        public void OutOfRangeAttackReturnsBlocked()
        {
            using AttackFixture fixture = AttackFixture.Create();

            AttackResolutionResult result = new AttackResolutionService(new FakeDamageHealingService()).PreviewAttack(fixture.CreateRequest("attack.range", hitRoll: 0.1f, distance: 10f, maximumRange: 2f));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(AttackOutcome.Blocked));
            Assert.That(result.Code, Is.EqualTo(AttackResolutionResultCode.OutOfRange));
        }

        [Test]
        public void MissingAndStaleTargetsAreInvalid()
        {
            using AttackFixture fixture = AttackFixture.Create();
            AttackResolutionService service = new AttackResolutionService(new FakeDamageHealingService());

            AttackResolutionResult missing = service.PreviewAttack(fixture.CreateRequest("attack.missing-target", hitRoll: 0.1f, missingTarget: true));
            AttackResolutionResult stale = service.PreviewAttack(fixture.CreateRequest("attack.stale-target", hitRoll: 0.1f, targetActorId: "entity.scene.old"));

            Assert.That(missing.Code, Is.EqualTo(AttackResolutionResultCode.MissingTarget));
            Assert.That(stale.Code, Is.EqualTo(AttackResolutionResultCode.StaleTarget));
        }

        [Test]
        public void MissingAttackerFailsForWeaponButEnvironmentalAttackCanProceed()
        {
            using AttackFixture fixture = AttackFixture.Create();
            AttackResolutionService service = new AttackResolutionService(new FakeDamageHealingService());

            AttackResolutionResult weapon = service.PreviewAttack(fixture.CreateRequest("attack.no-attacker", hitRoll: 0.1f, missingAttacker: true));
            AttackResolutionResult environmental = service.PreviewAttack(fixture.CreateRequest("attack.environmental", hitRoll: 0.1f, sourceType: AttackSourceType.Environmental, attackerObject: null, attackerActorId: string.Empty));

            Assert.That(weapon.Code, Is.EqualTo(AttackResolutionResultCode.MissingAttacker));
            Assert.That(environmental.Outcome, Is.EqualTo(AttackOutcome.Hit));
        }

        [Test]
        public void InvalidAmountsAndCriticalSettingsAreRejected()
        {
            using AttackFixture fixture = AttackFixture.Create();
            AttackResolutionService service = new AttackResolutionService(new FakeDamageHealingService());

            Assert.That(service.PreviewAttack(fixture.CreateRequest("attack.negative", baseDamage: -1f)).Outcome, Is.EqualTo(AttackOutcome.Invalid));
            Assert.That(service.PreviewAttack(fixture.CreateRequest("attack.nan", baseDamage: float.NaN)).Outcome, Is.EqualTo(AttackOutcome.Invalid));
            Assert.That(service.PreviewAttack(fixture.CreateRequest("attack.bad-crit-chance", criticalChance: -0.1f)).Outcome, Is.EqualTo(AttackOutcome.Invalid));
            Assert.That(service.PreviewAttack(fixture.CreateRequest("attack.bad-crit-mult", criticalMultiplier: 0.5f)).Outcome, Is.EqualTo(AttackOutcome.Invalid));
        }

        [Test]
        public void DuplicateAttackExecutionDoesNotApplyDamageOrEmitEventsTwice()
        {
            using AttackFixture fixture = AttackFixture.Create();
            FakeDamageHealingService damage = new FakeDamageHealingService();
            AttackResolutionService service = new AttackResolutionService(damage);
            int processed = 0;
            service.AttackProcessed += _ => processed++;
            AttackResolutionRequest request = fixture.CreateRequest("attack.duplicate", hitRoll: 0.1f);

            AttackResolutionResult first = service.ExecuteAttack(request);
            AttackResolutionResult second = service.ExecuteAttack(request);

            Assert.That(first.Duplicate, Is.False);
            Assert.That(second.Duplicate, Is.True);
            Assert.That(damage.ApplyDamageCalls, Is.EqualTo(1));
            Assert.That(processed, Is.EqualTo(1));
        }

        [Test]
        public void PreviewDoesNotConsumeTransactionId()
        {
            using AttackFixture fixture = AttackFixture.Create();
            FakeDamageHealingService damage = new FakeDamageHealingService();
            AttackResolutionService service = new AttackResolutionService(damage);
            AttackResolutionRequest request = fixture.CreateRequest("attack.preview-then-execute", hitRoll: 0.1f);

            service.PreviewAttack(request);
            AttackResolutionResult execute = service.ExecuteAttack(request);

            Assert.That(execute.Duplicate, Is.False);
            Assert.That(damage.ApplyDamageCalls, Is.EqualTo(1));
        }

        [Test]
        public void ParentAndChildTransactionIdsAreDeterministicAndDistinct()
        {
            using AttackFixture fixture = AttackFixture.Create();
            FakeDamageHealingService damage = new FakeDamageHealingService();

            AttackResolutionResult result = new AttackResolutionService(damage).ExecuteAttack(fixture.CreateRequest("attack.parent", hitRoll: 0.1f));

            Assert.That(result.AttackTransactionId, Is.EqualTo("attack.parent"));
            Assert.That(result.DamageTransactionId, Is.EqualTo("attack.parent.damage"));
            Assert.That(result.DamageTransactionId, Is.Not.EqualTo(result.AttackTransactionId));
            Assert.That(damage.LastDamageRequest.TransactionId, Is.EqualTo(result.DamageTransactionId));
        }

        [Test]
        public void AttackEventsEmitOnlyDuringExecutionAfterDamageResolution()
        {
            using AttackFixture fixture = AttackFixture.Create();
            FakeDamageHealingService damage = new FakeDamageHealingService();
            AttackResolutionService service = new AttackResolutionService(damage);
            int hitEvents = 0;
            service.AttackHit += _ =>
            {
                hitEvents++;
                Assert.That(damage.ApplyDamageCalls, Is.EqualTo(1));
            };

            service.PreviewAttack(fixture.CreateRequest("attack.event.preview", hitRoll: 0.1f));
            service.ExecuteAttack(fixture.CreateRequest("attack.event.execute", hitRoll: 0.1f));

            Assert.That(hitEvents, Is.EqualTo(1));
        }

        [Test]
        public void DamageFailureAfterLogicalHitIsReportedDistinctly()
        {
            using AttackFixture fixture = AttackFixture.Create();
            FakeDamageHealingService damage = new FakeDamageHealingService { FailDamage = true };

            AttackResolutionResult result = new AttackResolutionService(damage).ExecuteAttack(fixture.CreateRequest("attack.damage-failure", hitRoll: 0.1f));

            Assert.That(result.Outcome, Is.EqualTo(AttackOutcome.Hit));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(AttackResolutionResultCode.DamageFailed));
        }

        [Test]
        public void RealDamagePipelineIntegrationAppliesResourceDamageOnce()
        {
            using AttackFixture fixture = AttackFixture.Create(accuracy: 0f, evasion: 0f, resourceBacked: true);
            AttackResolutionService service = new AttackResolutionService(new DamageHealingService());
            int resourceEvents = 0;
            fixture.TargetResources.ResourceChanged += (_, _) => resourceEvents++;
            AttackResolutionRequest request = fixture.CreateRequest("attack.real-pipeline", baseDamage: 10f, hitRoll: 0.1f);

            AttackResolutionResult first = service.ExecuteAttack(request);
            AttackResolutionResult duplicate = service.ExecuteAttack(request);

            Assert.That(first.DamageResult.Succeeded, Is.True, first.DamageResult.Message);
            Assert.That(first.DamageResult.HealthChanged, Is.True);
            Assert.That(resourceEvents, Is.EqualTo(1));
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(resourceEvents, Is.EqualTo(1));
        }

        [Test]
        public void ResultDoesNotExposeMutableRuntimeObjects()
        {
            using AttackFixture fixture = AttackFixture.Create();
            AttackResolutionResult result = new AttackResolutionService(new FakeDamageHealingService()).PreviewAttack(fixture.CreateRequest("attack.immutable", hitRoll: 0.1f));

            Assert.That(result.RequirementFailureReasons, Is.Not.Null);
            Assert.That(result.DamageResult, Is.Not.Null);
            Assert.That(result.ResolvedTargetActorId, Is.EqualTo(fixture.TargetActorId));
        }

        private static DefinitionRegistry LoadRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog.CreateRegistry();
        }

        private sealed class FakeDamageHealingService : IDamageHealingService
        {
            public int PreviewDamageCalls { get; private set; }
            public int ApplyDamageCalls { get; private set; }
            public DamageApplicationRequest LastDamageRequest { get; private set; }
            public float FinalDamageAmount { get; set; } = 10f;
            public bool HealthChanged { get; set; } = true;
            public bool FailDamage { get; set; }

            public DamageApplicationResult PreviewDamage(DamageApplicationRequest request)
            {
                PreviewDamageCalls++;
                LastDamageRequest = request;
                return CreateResult(request, preview: true);
            }

            public DamageApplicationResult ApplyDamage(DamageApplicationRequest request)
            {
                ApplyDamageCalls++;
                LastDamageRequest = request;
                return FailDamage
                    ? DamageApplicationResult.Failure(request, ImmediateCombatResultCode.ResourceRejected, "Fake damage failure.", request.TargetActorId)
                    : CreateResult(request, preview: false);
            }

            public HealingApplicationResult PreviewHealing(HealingApplicationRequest request)
            {
                return HealingApplicationResult.Failure(request, "Unsupported", "Healing is not used by attack tests.");
            }

            public HealingApplicationResult ApplyHealing(HealingApplicationRequest request)
            {
                return HealingApplicationResult.Failure(request, "Unsupported", "Healing is not used by attack tests.");
            }

            private DamageApplicationResult CreateResult(DamageApplicationRequest request, bool preview)
            {
                float final = Mathf.Min(request.RequestedAmount, FinalDamageAmount);
                return DamageApplicationResult.Create(
                    preview,
                    preview ? ImmediateCombatResultCode.Preview : ImmediateCombatResultCode.Applied,
                    "Fake damage result.",
                    request,
                    request.TargetActorId,
                    request.RequestedAmount,
                    0f,
                    0f,
                    0f,
                    0f,
                    final,
                    100f,
                    HealthChanged ? 100f - final : 100f,
                    0f,
                    100f,
                    false,
                    false,
                    false,
                    HealthChanged,
                    false,
                    0f,
                    null);
            }
        }

        private sealed class AttackFixture : IDisposable
        {
            private DefinitionRegistry registry;

            public GameObject Attacker { get; private set; }
            public GameObject Target { get; private set; }
            public string AttackerActorId { get; private set; }
            public string TargetActorId { get; private set; }
            public DamageTypeDefinition DamageType { get; private set; }
            public CalculatedStatCollection AttackerStats { get; private set; }
            public CalculatedStatCollection TargetStats { get; private set; }
            public CharacterResourceCollection TargetResources { get; private set; }

            public static AttackFixture Create(float accuracy = 0f, float evasion = 0f, bool resourceBacked = false)
            {
                AttackFixture fixture = new AttackFixture
                {
                    registry = LoadRegistry(),
                    Attacker = new GameObject($"Attack Attacker {Guid.NewGuid():N}"),
                    Target = new GameObject($"Attack Target {Guid.NewGuid():N}")
                };
                Assert.That(fixture.registry.TryGet("damage.physical", out DamageTypeDefinition damageType), Is.True);
                fixture.DamageType = damageType;
                fixture.AttackerStats = ConfigureStats(fixture.Attacker, accuracy, CalculatedStatIds.Accuracy);
                fixture.TargetStats = ConfigureStats(fixture.Target, evasion, CalculatedStatIds.Evasion);
                fixture.AttackerActorId = AddIdentity(fixture.Attacker, "attacker");
                fixture.TargetActorId = AddIdentity(fixture.Target, "target");
                if (resourceBacked)
                {
                    fixture.TargetResources = fixture.Target.AddComponent<CharacterResourceCollection>();
                    fixture.TargetResources.Configure(fixture.registry, fixture.TargetStats, "player.local");
                }

                return fixture;
            }

            public AttackResolutionRequest CreateRequest(
                string transactionId,
                float baseDamage = 10f,
                float hitRoll = 0.1f,
                float criticalRoll = 0.5f,
                float baseHitChance = 0.75f,
                float criticalChance = 0f,
                float criticalMultiplier = 1.5f,
                float distance = 1f,
                float maximumRange = 2f,
                AttackSourceType sourceType = AttackSourceType.Weapon,
                GameObject attackerObject = null,
                string attackerActorId = null,
                GameObject targetObject = null,
                string targetActorId = null,
                bool missingAttacker = false,
                bool missingTarget = false,
                bool authorityValidated = true)
            {
                return new AttackResolutionRequest(
                    transactionId,
                    sourceType,
                    missingAttacker ? null : attackerObject == null && sourceType != AttackSourceType.Environmental ? Attacker : attackerObject,
                    attackerActorId ?? (sourceType == AttackSourceType.Environmental ? string.Empty : AttackerActorId),
                    missingTarget ? null : targetObject == null ? Target : targetObject,
                    targetActorId ?? TargetActorId,
                    DamageType,
                    baseDamage,
                    hitRoll,
                    criticalRoll,
                    baseHitChance,
                    criticalChance,
                    criticalMultiplier,
                    hasSuppliedDistance: true,
                    suppliedDistance: distance,
                    hasMaximumRange: true,
                    maximumRange: maximumRange,
                    authorityValidated: authorityValidated);
            }

            public void AddTargetEvasion(float amount, string sourceId)
            {
                AddStat(TargetStats, CalculatedStatIds.Evasion, amount, sourceId);
            }

            public void Dispose()
            {
                if (Attacker != null)
                {
                    UnityEngine.Object.DestroyImmediate(Attacker);
                }

                if (Target != null)
                {
                    UnityEngine.Object.DestroyImmediate(Target);
                }
            }

            private static CalculatedStatCollection ConfigureStats(GameObject owner, float amount, string statId)
            {
                DefinitionRegistry registry = LoadRegistry();
                CharacterAttributes attributes = owner.AddComponent<CharacterAttributes>();
                CalculatedStatCollection stats = owner.AddComponent<CalculatedStatCollection>();
                attributes.Configure(registry);
                stats.Configure(registry, attributes);
                if (amount > 0f)
                {
                    AddStat(stats, statId, amount, $"{owner.name}.{statId}");
                }

                return stats;
            }

            private static void AddStat(CalculatedStatCollection stats, string statId, float amount, string sourceId)
            {
                bool added = stats.AddContribution(new RuntimeCalculatedStatContribution
                {
                    contributionId = sourceId,
                    statId = statId,
                    sourceId = sourceId,
                    sourceCategory = (int)CalculatedStatContributionSourceCategory.Development,
                    kind = (int)CalculatedStatContributionKind.Flat,
                    direction = (int)CalculatedStatContributionDirection.Improve,
                    magnitude = amount
                }, out string failureReason);
                Assert.That(added, Is.True, failureReason);
            }

            private static string AddIdentity(GameObject owner, string label)
            {
                WorldEntityIdentity identity = owner.AddComponent<WorldEntityIdentity>();
                Assert.That(identity.TrySetAuthoredIdentity($"attack-resolution-{label}-{Guid.NewGuid():N}", "scene.test", PersistenceScope.RegionOrScene, "test.attack-resolution", out string failureReason), Is.True, failureReason);
                return identity.EntityId;
            }
        }
    }
}
