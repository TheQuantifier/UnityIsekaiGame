using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.Contributions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class CombatContributionsTests
    {
        private const string CatalogPath = "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PreviewContributionDoesNotMutateOrEmitEvents()
        {
            using Fixture fixture = Fixture.Create();
            int recorded = 0;
            fixture.Service.ContributionRecorded += _ => recorded++;

            CombatContributionRecordResult result = fixture.Service.PreviewContribution(new CombatContributionRecordRequest(
                "tx.preview",
                CombatContributionType.DamageApplied,
                "actor.source",
                "person.source",
                string.Empty,
                "actor.target",
                string.Empty,
                25f,
                10f,
                0f,
                fixture.Service.SimulationTime,
                preview: true));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(fixture.Service.Revision, Is.Zero);
            Assert.That(fixture.Service.GetLedgerSnapshots(), Is.Empty);
            Assert.That(recorded, Is.Zero);
        }

        [Test]
        public void CommittedDamageRecordsActualHealthDamageOnly()
        {
            using Fixture fixture = Fixture.Create();

            CombatContributionRecordResult result = fixture.Service.RecordDamage(fixture.Damage("tx.damage", "actor.source", "actor.target", requested: 100f, actual: 12f, overkill: 88f));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Record.ActualAmount, Is.EqualTo(12f).Within(0.001f));
            Assert.That(result.Record.RequestedAmount, Is.EqualTo(100f).Within(0.001f));
            Assert.That(fixture.Service.GetLedgerSnapshots().Single().Summaries.Single().TotalActualDamage, Is.EqualTo(12f).Within(0.001f));
        }

        [Test]
        public void DuplicateDamageTransactionDoesNotCreateSecondRecordOrEvent()
        {
            using Fixture fixture = Fixture.Create();
            int recorded = 0;
            fixture.Service.ContributionRecorded += _ => recorded++;
            DamageApplicationResult damage = fixture.Damage("tx.duplicate", "actor.source", "actor.target", 10f, 10f);

            CombatContributionRecordResult first = fixture.Service.RecordDamage(damage);
            CombatContributionRecordResult second = fixture.Service.RecordDamage(damage);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.Duplicate, Is.True);
            Assert.That(fixture.Service.GetLedgerSnapshots().Single().Records.Count, Is.EqualTo(1));
            Assert.That(recorded, Is.EqualTo(1));
        }

        [Test]
        public void FullyPreventedDamageCreatesNoAttackerDamageContribution()
        {
            using Fixture fixture = Fixture.Create();

            CombatContributionRecordResult result = fixture.Service.RecordDamage(fixture.Damage("tx.prevented", "actor.source", "actor.target", 25f, 0f));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(CombatContributionResultCode.ZeroEffectiveContribution));
            Assert.That(fixture.Service.GetLedgerSnapshots(), Is.Empty);
        }

        [Test]
        public void DirectOngoingReactionAndEnvironmentalDamageRemainDistinguishable()
        {
            using Fixture fixture = Fixture.Create();

            fixture.Service.RecordDamage(fixture.Damage("tx.direct", "actor.direct", "actor.target", 10f, 10f), sourceKind: CombatContributionSourceKind.Direct);
            fixture.Service.RecordDamage(fixture.Damage("tx.ongoing", "actor.ongoing", "actor.target", 10f, 10f), sourceKind: CombatContributionSourceKind.OngoingEffect, rootTransactionId: "tx.root.ongoing");
            fixture.Service.RecordDamage(fixture.Damage("tx.reaction", "actor.reaction", "actor.target", 10f, 10f), sourceKind: CombatContributionSourceKind.Reaction, rootTransactionId: "tx.root.reaction", parentTransactionId: "tx.parent.reaction");
            fixture.Service.RecordContribution(new CombatContributionRecordRequest("tx.environment", CombatContributionType.DamageApplied, string.Empty, string.Empty, string.Empty, "actor.target", string.Empty, 10f, 10f, 0f, fixture.Service.SimulationTime, CombatContributionSourceKind.Environment));

            CombatContributionLedgerSnapshot snapshot = fixture.Service.GetLedgerSnapshots().Single();

            Assert.That(snapshot.Records.Select(record => record.ContributionType), Does.Contain(CombatContributionType.DamageApplied));
            Assert.That(snapshot.Records.Select(record => record.ContributionType), Does.Contain(CombatContributionType.OngoingDamageApplied));
            Assert.That(snapshot.Records.Select(record => record.ContributionType), Does.Contain(CombatContributionType.ReactionDamageApplied));
            Assert.That(snapshot.Records.Select(record => record.SourceKind), Does.Contain(CombatContributionSourceKind.Environment));
        }

        [Test]
        public void HealingSupportRecordsEffectiveHealingOnly()
        {
            using Fixture fixture = Fixture.Create();

            CombatContributionRecordResult result = fixture.Service.RecordHealing(fixture.Healing("tx.healing", "actor.healer", "actor.ally", requested: 100f, actual: 7f, overheal: 93f));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Record.ActualAmount, Is.EqualTo(7f).Within(0.001f));
            Assert.That(result.Record.RequestedAmount, Is.EqualTo(100f).Within(0.001f));
            Assert.That(fixture.Service.GetLedgerSnapshots().Single().Summaries.Single().TotalEffectiveHealing, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void SelfDamageDoesNotCountForHostileCreditUnderAlphaPolicy()
        {
            using Fixture fixture = Fixture.Create();

            CombatContributionRecordResult result = fixture.Service.RecordDamage(fixture.Damage("tx.self", "actor.same", "actor.same", 10f, 10f));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(CombatContributionResultCode.ZeroEffectiveContribution));
            Assert.That(fixture.Service.GetLedgerSnapshots(), Is.Empty);
        }

        [Test]
        public void DefeatCreditUsesLatestThenDamageThenStableActorTieBreak()
        {
            using Fixture fixture = Fixture.Create();
            fixture.Service.RecordDamage(fixture.Damage("tx.old-large", "actor.a", "actor.target", 50f, 50f));
            fixture.Service.AdvanceClock(1f);
            fixture.Service.RecordDamage(fixture.Damage("tx.latest-small", "actor.b", "actor.target", 10f, 10f));

            CombatCreditResolutionResult latest = fixture.Service.ResolveDefeatCredit(fixture.Defeat("actor.target", "tx.defeat.latest"));

            Assert.That(latest.Succeeded, Is.True, latest.Message);
            Assert.That(latest.PrimaryContributorActorId, Is.EqualTo("actor.b"));
            Assert.That(latest.GrantsConcreteRewards, Is.False);

            using Fixture tie = Fixture.Create();
            tie.Service.RecordDamage(tie.Damage("tx.tie-b", "actor.b", "actor.target", 20f, 20f));
            tie.Service.RecordDamage(tie.Damage("tx.tie-a", "actor.a", "actor.target", 20f, 20f));

            CombatCreditResolutionResult tied = tie.Service.ResolveDefeatCredit(tie.Defeat("actor.target", "tx.defeat.tie"));

            Assert.That(tied.PrimaryContributorActorId, Is.EqualTo("actor.a"));
        }

        [Test]
        public void KillCreditIncludesAssistsAndRejectsHealingOnlyPrimary()
        {
            using Fixture fixture = Fixture.Create();
            fixture.Service.RecordHealing(fixture.Healing("tx.heal", "actor.healer", "actor.target", 20f, 20f));
            fixture.Service.RecordDamage(fixture.Damage("tx.damage.old", "actor.assist", "actor.target", 10f, 10f));
            fixture.Service.AdvanceClock(1f);
            fixture.Service.RecordDamage(fixture.Damage("tx.damage.latest", "actor.killer", "actor.target", 5f, 5f));

            CombatCreditResolutionResult result = fixture.Service.ResolveKillCredit(fixture.Death("actor.target", "tx.death"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.PrimaryContributorActorId, Is.EqualTo("actor.killer"));
            Assert.That(result.Assists.Select(summary => summary.ContributorActorId), Does.Contain("actor.assist"));
            Assert.That(result.Assists.Select(summary => summary.ContributorActorId), Does.Contain("actor.healer"));
            Assert.That(result.PrimaryContributorActorId, Is.Not.EqualTo("actor.healer"));
        }

        [Test]
        public void DuplicateLifecycleCreditIsIdempotentAndRecoveryRevivalPreserveCredit()
        {
            using Fixture fixture = Fixture.Create();
            fixture.Service.RecordDamage(fixture.Damage("tx.damage.kill", "actor.killer", "actor.target", 10f, 10f));
            ActorLifecycleResult death = fixture.Death("actor.target", "tx.death.once");

            CombatCreditResolutionResult first = fixture.Service.ResolveKillCredit(death);
            CombatCreditResolutionResult duplicate = fixture.Service.ResolveKillCredit(death);
            fixture.Service.RecordContribution(new CombatContributionRecordRequest("tx.revival", CombatContributionType.RevivalProvided, "actor.healer", string.Empty, "actor.target", string.Empty, string.Empty, 20f, 20f, 0f, fixture.Service.SimulationTime, CombatContributionSourceKind.Lifecycle));

            CombatCreditResolutionResult afterRevival = fixture.Service.ResolveKillCredit(death, transactionId: "tx.death.once.after-revival");

            Assert.That(first.PrimaryContributorActorId, Is.EqualTo("actor.killer"));
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(afterRevival.Duplicate, Is.True);
            Assert.That(afterRevival.PrimaryContributorActorId, Is.EqualTo("actor.killer"));
        }

        [Test]
        public void ExpiredContributionDoesNotAssignPrimaryCredit()
        {
            using Fixture fixture = Fixture.Create();
            fixture.Service.RecordDamage(fixture.Damage("tx.expired", "actor.source", "actor.target", 10f, 10f));
            fixture.Service.AdvanceClock(31f);

            CombatCreditResolutionResult result = fixture.Service.ResolveDefeatCredit(fixture.Defeat("actor.target", "tx.defeat.expired"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Code, Is.EqualTo(CombatContributionResultCode.NoEligibleContributor));
            Assert.That(result.PrimaryContributorActorId, Is.Empty);
        }

        [Test]
        public void EncounterMergeCombinesLedgersDeterministically()
        {
            using Fixture fixture = Fixture.Create();
            fixture.Service.RecordContribution(fixture.Record("tx.merge.a", "actor.a", "actor.target", "encounter.a", 10f));
            fixture.Service.RecordContribution(fixture.Record("tx.merge.b", "actor.b", "actor.target", "encounter.b", 5f));
            int mergedEvents = 0;
            fixture.Service.LedgersMerged += _ => mergedEvents++;

            CombatContributionLedgerMergeResult result = fixture.Service.MergeEncounterLedgers(new CombatEncounterSnapshot(
                "encounter.a",
                true,
                0f,
                0f,
                new[] { "actor.a", "actor.b", "actor.target" },
                Array.Empty<CombatEngagementSnapshot>(),
                1L,
                CombatEncounterCompletionReason.Forced));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Snapshot.Records.Select(record => record.SourceTransactionId), Is.EquivalentTo(new[] { "tx.merge.a", "tx.merge.b" }));
            Assert.That(fixture.Service.GetLedgerSnapshot("encounter.encounter.b"), Is.Null);
            Assert.That(mergedEvents, Is.EqualTo(1));
        }

        [Test]
        public void EncounterSplitPartitionsActiveEligibilityWhilePreservingHistory()
        {
            using Fixture fixture = Fixture.Create();
            fixture.Service.RecordContribution(fixture.Record("tx.split.a", "actor.a", "actor.target-a", "encounter.original", 10f));
            fixture.Service.RecordContribution(fixture.Record("tx.split.b", "actor.b", "actor.target-b", "encounter.original", 10f));
            fixture.Service.RecordContribution(fixture.Record("tx.split.cross", "actor.a", "actor.target-b", "encounter.original", 10f));

            CombatContributionLedgerPartitionResult result = fixture.Service.PartitionEncounterLedgers(SplitResult(
                "encounter.original",
                new CombatEncounterSplitComponentSnapshot("encounter.original", new[] { "actor.a", "actor.target-a" }, Array.Empty<string>(), true, 0f, true),
                new CombatEncounterSplitComponentSnapshot("encounter.new", new[] { "actor.b", "actor.target-b" }, Array.Empty<string>(), false, 0f, true)));

            CombatContributionLedgerSnapshot survivor = result.ComponentSnapshots.Single(snapshot => snapshot.EncounterId == "encounter.original");
            CombatContributionLedgerSnapshot created = result.ComponentSnapshots.Single(snapshot => snapshot.EncounterId == "encounter.new");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.HistoricalSnapshots.SelectMany(snapshot => snapshot.Records).Select(record => record.SourceTransactionId), Does.Contain("tx.split.cross"));
            Assert.That(survivor.Summaries.Select(summary => summary.ContributorActorId), Is.EquivalentTo(new[] { "actor.a" }));
            Assert.That(created.Summaries.Select(summary => summary.ContributorActorId), Is.EquivalentTo(new[] { "actor.b" }));
        }

        [Test]
        public void FinalizedLedgerRejectsFurtherMutation()
        {
            using Fixture fixture = Fixture.Create();
            fixture.Service.RecordDamage(fixture.Damage("tx.first", "actor.source", "actor.target", 10f, 10f));
            CombatContributionLedgerSnapshot snapshot = fixture.Service.GetLedgerSnapshots().Single();

            CombatContributionLedgerSnapshot finalized = fixture.Service.FinalizeLedger(snapshot.LedgerId);
            CombatContributionRecordResult after = fixture.Service.RecordDamage(fixture.Damage("tx.after", "actor.source", "actor.target", 10f, 10f));

            Assert.That(finalized.Finalized, Is.True);
            Assert.That(after.Succeeded, Is.False);
            Assert.That(after.Code, Is.EqualTo(CombatContributionResultCode.LedgerFinalized));
            Assert.That(fixture.Service.GetLedgerSnapshots().Single().Records.Count, Is.EqualTo(1));
        }

        [Test]
        public void RestoreClearRemovesTransientStateWithoutEvents()
        {
            using Fixture fixture = Fixture.Create();
            fixture.Service.RecordDamage(fixture.Damage("tx.restore", "actor.source", "actor.target", 10f, 10f));
            fixture.Service.ResolveDefeatCredit(fixture.Defeat("actor.target", "tx.defeat.restore"));
            int records = 0;
            int credits = 0;
            int finalizes = 0;
            fixture.Service.ContributionRecorded += _ => records++;
            fixture.Service.CreditResolved += _ => credits++;
            fixture.Service.LedgerFinalized += _ => finalizes++;

            fixture.Service.ClearTransientStateForRestore();

            Assert.That(fixture.Service.Revision, Is.Zero);
            Assert.That(fixture.Service.SimulationTime, Is.Zero);
            Assert.That(fixture.Service.GetLedgerSnapshots(), Is.Empty);
            Assert.That(records + credits + finalizes, Is.Zero);
        }

        [Test]
        public void EventBridgeRecordsCommittedDamageAutomatically()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            DamageHealingService damageHealing = new DamageHealingService();
            fixture.Bridge.Attach(damageHealing);

            DamageApplicationResult result = damageHealing.ApplyDamage(new DamageApplicationRequest("tx.bridge.damage", fixture.AttackerActorId, fixture.Attacker, fixture.TargetActorId, fixture.Target, fixture.DamageType, 9f, "bridge-test", true));

            CombatContributionLedgerSnapshot snapshot = fixture.Service.GetLedgerSnapshots().Single();

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(snapshot.Records.Single().ActualAmount, Is.EqualTo(9f).Within(0.001f));
            Assert.That(snapshot.Records.Single().SourceKind, Is.EqualTo(CombatContributionSourceKind.Direct));
        }

        [Test]
        public void RewardEligibilityGrantsNoConcreteRewards()
        {
            using Fixture fixture = Fixture.Create();
            fixture.Service.RecordDamage(fixture.Damage("tx.reward.damage", "actor.killer", "actor.target", 10f, 10f));

            CombatCreditResolutionResult result = fixture.Service.ResolveKillCredit(fixture.Death("actor.target", "tx.reward.death"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.GrantsConcreteRewards, Is.False);
            Assert.That(result.Contributors.Single().Eligibility, Does.Contain(CombatRewardEligibilityCategory.FutureExperience));
            Assert.That(result.Contributors.Single().Eligibility, Does.Contain(CombatRewardEligibilityCategory.DiagnosticOnly));
        }

        [Test]
        public void RuntimeGameplayAssemblyDoesNotReferenceDevelopmentForContribution()
        {
            string asmdef = System.IO.File.ReadAllText("Packages/com.thequantifier.isekai.simulation/Runtime/UnityIsekaiGame.Gameplay.asmdef");

            Assert.That(asmdef, Does.Not.Contain("Development"));
            Assert.That(asmdef, Does.Not.Contain("Tests"));
        }

        private sealed class Fixture : IDisposable
        {
            private Fixture(GameObject owner, CombatContributionPolicyDefinition policy)
            {
                Owner = owner;
                Policy = policy;
                Service = owner.AddComponent<CombatContributionService>();
                Service.Configure(policy);
            }

            public GameObject Owner { get; }
            public CombatContributionPolicyDefinition Policy { get; }
            public CombatContributionService Service { get; }

            public static Fixture Create()
            {
                return new Fixture(new GameObject("Contribution Service Test"), CreatePolicy());
            }

            public static CombatContributionPolicyDefinition CreatePolicy()
            {
                CombatContributionPolicyDefinition policy = ScriptableObject.CreateInstance<CombatContributionPolicyDefinition>();
                SerializedObject serialized = new SerializedObject(policy);
                serialized.FindProperty("policyId").stringValue = "combat-contribution-policy.test";
                serialized.FindProperty("displayName").stringValue = "Test Contribution Policy";
                serialized.FindProperty("contributionWindowSeconds").floatValue = 30f;
                serialized.FindProperty("minimumDamageContribution").floatValue = 0.001f;
                serialized.FindProperty("minimumHealingAssistContribution").floatValue = 5f;
                serialized.FindProperty("minimumDefensiveAssistContribution").floatValue = 1f;
                serialized.FindProperty("maximumRetainedRecordsPerLedger").intValue = 16;
                serialized.FindProperty("damageScoreWeight").floatValue = 1f;
                serialized.FindProperty("healingScoreWeight").floatValue = 0.5f;
                serialized.FindProperty("defensiveScoreWeight").floatValue = 0.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return policy;
            }

            public DamageApplicationResult Damage(string transactionId, string sourceActorId, string targetActorId, float requested, float actual, float overkill = 0f)
            {
                DamageApplicationRequest request = new DamageApplicationRequest(transactionId, sourceActorId, null, targetActorId, null, null, requested, "test", true);
                return DamageApplicationResult.Create(false, "Applied", "Applied.", request, targetActorId, requested, 0f, 0f, 0f, 0f, actual, 100f, 100f - actual, 0f, 100f, false, false, false, actual > 0f, false, overkill, null);
            }

            public HealingApplicationResult Healing(string transactionId, string sourceActorId, string targetActorId, float requested, float actual, float overheal = 0f)
            {
                HealingApplicationRequest request = new HealingApplicationRequest(transactionId, sourceActorId, null, targetActorId, null, requested, "test", true);
                return HealingApplicationResult.Create(false, "Healed", "Healed.", request, targetActorId, requested, actual, overheal, 50f, 50f + actual, 0f, 100f, false, actual > 0f, false, null);
            }

            public ActorLifecycleResult Defeat(string targetActorId, string transactionId)
            {
                return ActorLifecycleResult.Create(true, false, false, "Success", "Defeated.", transactionId, "actor.source", targetActorId, "defeat-policy.test", LifecycleTransitionKind.Defeat, LifecycleTriggerKind.ExplicitDefeat, ActorLifecycleState.Active, ActorLifecycleState.Defeated, DefeatPolicyOutcome.RemainDefeated, 0f, 0f, 0f, 100f, 0f, 0f, 0f, string.Empty, 1L);
            }

            public ActorLifecycleResult Death(string targetActorId, string transactionId)
            {
                return ActorLifecycleResult.Create(true, false, false, "Success", "Dead.", transactionId, "actor.source", targetActorId, "defeat-policy.test", LifecycleTransitionKind.Death, LifecycleTriggerKind.ExplicitDeath, ActorLifecycleState.Defeated, ActorLifecycleState.Dead, DefeatPolicyOutcome.RemainDefeated, 0f, 0f, 0f, 100f, 0f, 0f, 0f, string.Empty, 1L);
            }

            public CombatContributionRecordRequest Record(string transactionId, string sourceActorId, string targetActorId, string encounterId, float actual)
            {
                return new CombatContributionRecordRequest(transactionId, CombatContributionType.DamageApplied, sourceActorId, string.Empty, string.Empty, targetActorId, encounterId, actual, actual, 0f, Service.SimulationTime, CombatContributionSourceKind.Direct, transactionId, string.Empty, "test", string.Empty, preview: false, authorityValidated: true);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Owner);
                UnityEngine.Object.DestroyImmediate(Policy);
            }
        }

        private sealed class RuntimeFixture : IDisposable
        {
            private RuntimeFixture(GameObject owner, GameObject attacker, GameObject target, CombatContributionPolicyDefinition policy, DamageTypeDefinition damageType, CombatContributionService service, CombatContributionEventBridge bridge, string attackerActorId, string targetActorId)
            {
                Owner = owner;
                Attacker = attacker;
                Target = target;
                Policy = policy;
                DamageType = damageType;
                Service = service;
                Bridge = bridge;
                AttackerActorId = attackerActorId;
                TargetActorId = targetActorId;
            }

            public GameObject Owner { get; }
            public GameObject Attacker { get; }
            public GameObject Target { get; }
            public CombatContributionPolicyDefinition Policy { get; }
            public DamageTypeDefinition DamageType { get; }
            public CombatContributionService Service { get; }
            public CombatContributionEventBridge Bridge { get; }
            public string AttackerActorId { get; }
            public string TargetActorId { get; }

            public static RuntimeFixture Create()
            {
                DefinitionRegistry registry = LoadCatalog().CreateRegistry();
                CombatContributionPolicyDefinition policy = Fixture.CreatePolicy();
                DamageTypeDefinition damageType = CreateDamageType();
                GameObject owner = new GameObject("Contribution Bridge Owner");
                CombatContributionService service = owner.AddComponent<CombatContributionService>();
                service.Configure(policy);
                CombatContributionEventBridge bridge = owner.AddComponent<CombatContributionEventBridge>();
                bridge.Configure(service);
                GameObject attacker = new GameObject("Contribution Bridge Attacker");
                GameObject target = new GameObject("Contribution Bridge Target");
                ConfigureActor(attacker, registry, "attacker", out string attackerActorId);
                ConfigureActor(target, registry, "target", out string targetActorId);
                return new RuntimeFixture(owner, attacker, target, policy, damageType, service, bridge, attackerActorId, targetActorId);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Owner);
                UnityEngine.Object.DestroyImmediate(Attacker);
                UnityEngine.Object.DestroyImmediate(Target);
                UnityEngine.Object.DestroyImmediate(Policy);
                UnityEngine.Object.DestroyImmediate(DamageType);
            }
        }

        private static CombatEncounterSplitResult SplitResult(string originalEncounterId, params CombatEncounterSplitComponentSnapshot[] components)
        {
            return new CombatEncounterSplitResult(true, false, false, "Success", "Split.", "tx.split", originalEncounterId, originalEncounterId, components.Where(component => !component.RetainedOriginalEncounterId).Select(component => component.EncounterId).ToList(), components.SelectMany(component => component.ParticipantIds).Distinct().ToList(), components, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<CombatExitResult>(), Array.Empty<CombatEncounterSnapshot>(), Array.Empty<CombatParticipantReassignmentResult>(), CombatExitReason.Explicit, 0L, 1L, 0f);
        }

        private static DamageTypeDefinition CreateDamageType()
        {
            DamageTypeDefinition damageType = ScriptableObject.CreateInstance<DamageTypeDefinition>();
            SerializedObject serialized = new SerializedObject(damageType);
            serialized.FindProperty("damageTypeId").stringValue = "damage.test-contribution";
            serialized.FindProperty("displayName").stringValue = "Test Contribution";
            serialized.FindProperty("family").enumValueIndex = (int)DamageFamily.Physical;
            serialized.FindProperty("generalDefenseApplies").boolValue = false;
            serialized.FindProperty("enforceMinimumDamage").boolValue = false;
            serialized.FindProperty("minimumDamage").floatValue = 0f;
            serialized.FindProperty("canonicalAlphaDamageType").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return damageType;
        }

        private static CharacterResourceCollection ConfigureActor(GameObject owner, DefinitionRegistry registry, string name, out string actorId)
        {
            CharacterAttributes attributes = owner.AddComponent<CharacterAttributes>();
            CalculatedStatCollection stats = owner.AddComponent<CalculatedStatCollection>();
            CharacterResourceCollection resources = owner.AddComponent<CharacterResourceCollection>();
            WorldEntityIdentity identity = owner.AddComponent<WorldEntityIdentity>();
            Assert.That(identity.TrySetAuthoredIdentity($"combat-contribution-{name}-{Guid.NewGuid():N}", "scene.test", PersistenceScope.RegionOrScene, "test.combat-contribution", out string failureReason), Is.True, failureReason);
            actorId = identity.EntityId;
            attributes.Configure(registry);
            stats.Configure(registry, attributes);
            resources.Configure(registry, stats, "player.local");
            return resources;
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }
    }
}
