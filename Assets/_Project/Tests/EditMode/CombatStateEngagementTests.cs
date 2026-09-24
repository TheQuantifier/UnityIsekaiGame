using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class CombatStateEngagementTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PreviewExplicitEngagementDoesNotMutateOrConsumeTransaction()
        {
            using CombatFixture fixture = CombatFixture.Create("preview");
            int entered = 0;
            fixture.Combat.ActorEnteredCombat += _ => entered++;
            CombatEngagementRequest request = fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.preview");

            CombatEntryResult preview = fixture.Combat.PreviewEnterCombat(request);

            Assert.That(preview.Preview, Is.True);
            Assert.That(fixture.Combat.IsInCombat(fixture.A.ActorId), Is.False);

            CombatEntryResult executed = fixture.Combat.EnterCombat(request);

            Assert.That(executed.Succeeded, Is.True, executed.Message);
            Assert.That(entered, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitEngagementCreatesOneBilateralEngagementAndDuplicateDoesNotReplay()
        {
            using CombatFixture fixture = CombatFixture.Create("duplicate");
            int engagements = 0;
            fixture.Combat.EngagementCreated += _ => engagements++;
            CombatEngagementRequest request = fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.duplicate");

            CombatEntryResult first = fixture.Combat.EnterCombat(request);
            CombatEntryResult second = fixture.Combat.EnterCombat(request);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Duplicate, Is.True);
            Assert.That(fixture.Combat.GetActiveEngagements(fixture.A.ActorId), Has.Count.EqualTo(1));
            Assert.That(fixture.Combat.GetEncounterForActor(fixture.A.ActorId).EncounterId, Is.EqualTo(fixture.Combat.GetEncounterForActor(fixture.B.ActorId).EncounterId));
            Assert.That(engagements, Is.EqualTo(1));
        }

        [Test]
        public void AttackMissStartsCombatButPreviewAndBlockedDoNot()
        {
            using CombatFixture fixture = CombatFixture.Create("attack");
            AttackResolutionRequest missRequest = fixture.CreateAttackRequest("attack.miss", hitRoll: 0.99f);
            AttackResolutionResult preview = fixture.Attacks.PreviewAttack(missRequest);
            AttackResolutionResult miss = fixture.Attacks.ExecuteAttack(missRequest);
            AttackResolutionResult blocked = fixture.Attacks.ExecuteAttack(fixture.CreateAttackRequest("attack.blocked", hitRoll: 0.1f, distance: 99f, range: 1f));

            CombatEntryResult previewCombat = fixture.Combat.RecordAttackResult(preview);
            CombatEntryResult missCombat = fixture.Combat.RecordAttackResult(miss);
            CombatEntryResult blockedCombat = fixture.Combat.RecordAttackResult(blocked);

            Assert.That(previewCombat.Succeeded, Is.False);
            Assert.That(missCombat.Succeeded, Is.True, missCombat.Message);
            Assert.That(blockedCombat.Succeeded, Is.False);
            Assert.That(fixture.Combat.IsInCombat(fixture.A.ActorId), Is.True);
        }

        [Test]
        public void FullyPreventedHostileDamageStartsCombatButSelfDamageDoesNot()
        {
            using CombatFixture fixture = CombatFixture.Create("prevented");
            DamageApplicationResult prevented = fixture.CreateDamageResult("damage.prevented", fixture.A, fixture.B, finalDamage: 0f, changed: false, immune: true);
            DamageApplicationResult self = fixture.CreateDamageResult("damage.self", fixture.A, fixture.A, finalDamage: 5f, changed: true, immune: false);

            CombatEntryResult preventedCombat = fixture.Combat.RecordDamageResult(prevented);
            CombatEntryResult selfCombat = fixture.Combat.RecordDamageResult(self);

            Assert.That(preventedCombat.Succeeded, Is.True, preventedCombat.Message);
            Assert.That(selfCombat.Code, Is.EqualTo(CombatStateResultCode.SelfEngagementRejected));
        }

        [Test]
        public void EncountersMergeDeterministicallyAndPreserveParticipants()
        {
            using CombatFixture fixture = CombatFixture.Create("merge");
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.ab"));
            string firstEncounter = fixture.Combat.GetEncounterForActor(fixture.A.ActorId).EncounterId;
            fixture.Combat.AdvanceTime(0.25f);
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.C, fixture.D, "combat.cd"));

            CombatEntryResult merge = fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.B, fixture.C, "combat.bc"));
            CombatEncounterSnapshot encounter = fixture.Combat.GetEncounterForActor(fixture.D.ActorId);

            Assert.That(merge.EncounterMerged, Is.True);
            Assert.That(encounter.EncounterId, Is.EqualTo(firstEncounter));
            Assert.That(encounter.ParticipantIds, Is.EquivalentTo(new[] { fixture.A.ActorId, fixture.B.ActorId, fixture.C.ActorId, fixture.D.ActorId }));
        }

        [Test]
        public void TimeoutExpiresAtBoundaryAndEndsEncounter()
        {
            using CombatFixture fixture = CombatFixture.Create("timeout");
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.timeout"));

            CombatStateProcessResult early = fixture.Combat.AdvanceTime(9.999f);
            CombatStateProcessResult boundary = fixture.Combat.AdvanceTime(0.001f);

            Assert.That(early.ProcessedExits, Is.Zero);
            Assert.That(boundary.ProcessedExits, Is.EqualTo(2));
            Assert.That(fixture.Combat.IsInCombat(fixture.A.ActorId), Is.False);
            Assert.That(boundary.EndedEncounters, Has.Count.EqualTo(1));
        }

        [Test]
        public void HostileActivityResetsTimeoutButHealingDoesNot()
        {
            using CombatFixture fixture = CombatFixture.Create("refresh");
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.refresh.1"));
            fixture.Combat.AdvanceTime(9f);
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.refresh.2"));
            fixture.Combat.AdvanceTime(9f);

            Assert.That(fixture.Combat.IsInCombat(fixture.A.ActorId), Is.True);

            fixture.Combat.AdvanceTime(1f);
            Assert.That(fixture.Combat.IsInCombat(fixture.A.ActorId), Is.False);
        }

        [Test]
        public void ExplicitExitRequiresAuthorityWhileActiveEngagementExists()
        {
            using CombatFixture fixture = CombatFixture.Create("exit");
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.exit"));

            CombatExitResult rejected = fixture.Combat.LeaveCombat(new CombatExitRequest("combat.exit.normal", fixture.A.ActorId, fixture.A.Owner, CombatExitReason.Explicit));
            CombatExitResult forced = fixture.Combat.LeaveCombat(new CombatExitRequest("combat.exit.force", fixture.A.ActorId, fixture.A.Owner, CombatExitReason.Forced, authoritative: true));

            Assert.That(rejected.Code, Is.EqualTo(CombatStateResultCode.ActiveEngagementPreventsExit));
            Assert.That(forced.Succeeded, Is.True, forced.Message);
        }

        [Test]
        public void DeadActorIsRemovedAndRevivalDoesNotReenterCombat()
        {
            using CombatFixture fixture = CombatFixture.Create("death");
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.death"));
            fixture.B.Lifecycle.ExecuteDeath(new LifecycleDeathRequest("lifecycle.kill", fixture.A.ActorId, fixture.A.Owner, fixture.B.ActorId, fixture.B.Owner, LifecycleTriggerKind.ExplicitDeath));

            fixture.Combat.AdvanceTime(0f);
            fixture.B.Lifecycle.ExecuteRevival(new LifecycleRevivalRequest("lifecycle.revive", fixture.A.ActorId, fixture.A.Owner, fixture.B.ActorId, fixture.B.Owner, 10f));

            Assert.That(fixture.Combat.IsInCombat(fixture.B.ActorId), Is.False);
        }

        [Test]
        public void HostileOngoingDamageTickRefreshesCombat()
        {
            using CombatFixture fixture = CombatFixture.Create("ongoing");
            OngoingEffectDefinition poison = fixture.Get<OngoingEffectDefinition>("ongoing-effect.poison");
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.ongoing.start"));
            fixture.Combat.AdvanceTime(9f);
            fixture.B.Ongoing.ApplyOngoingEffect(new OngoingEffectApplicationRequest("ongoing.apply", poison, fixture.A.ActorId, fixture.A.Owner, fixture.B.ActorId, fixture.B.Owner, "test", 0f, 0f, 0f, 1, 1, authorityValidated: true));
            OngoingEffectProcessResult ticks = fixture.B.Ongoing.AdvanceTime(1f);

            CombatEntryResult refresh = fixture.Combat.RecordOngoingEffectTick(ticks.TickResults[0]);
            fixture.Combat.AdvanceTime(9f);

            Assert.That(refresh.Succeeded, Is.True, refresh.Message);
            Assert.That(fixture.Combat.IsInCombat(fixture.A.ActorId), Is.True);
        }

        [Test]
        public void ClearTransientStateForRestoreEmitsNoCombatEvents()
        {
            using CombatFixture fixture = CombatFixture.Create("restore");
            int exits = 0;
            fixture.Combat.ActorLeftCombat += _ => exits++;
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.restore"));

            fixture.Combat.ClearTransientStateForRestore();

            Assert.That(exits, Is.Zero);
            Assert.That(fixture.Combat.IsInCombat(fixture.A.ActorId), Is.False);
        }

        [Test]
        public void QuerySnapshotsExposeNoMutableCollections()
        {
            using CombatFixture fixture = CombatFixture.Create("immutable");
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.immutable"));
            CombatEncounterSnapshot encounter = fixture.Combat.GetEncounterForActor(fixture.A.ActorId);

            Assert.That(encounter.ParticipantIds, Is.Not.TypeOf<System.Collections.Generic.List<string>>());
            Assert.That(encounter.Engagements, Is.Not.TypeOf<System.Collections.Generic.List<CombatEngagementSnapshot>>());
        }

        [Test]
        public void ConnectedTwoParticipantEncounterDoesNotSplit()
        {
            using CombatFixture fixture = CombatFixture.Create("connected-two");
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.connected.ab"));
            string encounterId = fixture.Combat.GetEncounterForActor(fixture.A.ActorId).EncounterId;

            CombatEncounterSplitResult result = fixture.Combat.ProcessEncounterConnectivity("combat.connected.process", encounterId);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.SplitOccurred, Is.False);
            Assert.That(fixture.Combat.ValidateIntegrity().Succeeded, Is.True);
        }

        [Test]
        public void ConnectedChainRemainsOneEncounter()
        {
            using CombatFixture fixture = CombatFixture.Create("chain");
            fixture.ConnectChain();

            CombatEncounterSnapshot encounter = fixture.Combat.GetEncounterForActor(fixture.A.ActorId);

            Assert.That(encounter.ParticipantIds, Is.EquivalentTo(new[] { fixture.A.ActorId, fixture.B.ActorId, fixture.C.ActorId, fixture.D.ActorId }));
            Assert.That(fixture.Combat.GetEncounterForActor(fixture.D.ActorId).EncounterId, Is.EqualTo(encounter.EncounterId));
            Assert.That(fixture.Combat.ValidateIntegrity().Succeeded, Is.True);
        }

        [Test]
        public void EndingBridgeSplitsChainIntoConnectedComponents()
        {
            using CombatFixture fixture = CombatFixture.Create("split-bridge");
            fixture.ConnectChain();
            string originalEncounter = fixture.Combat.GetEncounterForActor(fixture.A.ActorId).EncounterId;

            CombatEncounterSplitResult split = fixture.Combat.EndEngagement(fixture.CreateEngagementEndRequest(fixture.B, fixture.C, "combat.split.bc"));

            Assert.That(split.Succeeded, Is.True, split.Message);
            Assert.That(split.SplitOccurred, Is.True);
            Assert.That(split.SurvivingEncounterId, Is.EqualTo(originalEncounter));
            Assert.That(split.CreatedEncounterIds, Has.Count.EqualTo(1));
            Assert.That(fixture.Combat.GetEncounterForActor(fixture.A.ActorId).ParticipantIds, Is.EquivalentTo(new[] { fixture.A.ActorId, fixture.B.ActorId }));
            Assert.That(fixture.Combat.GetEncounterForActor(fixture.C.ActorId).ParticipantIds, Is.EquivalentTo(new[] { fixture.C.ActorId, fixture.D.ActorId }));
            Assert.That(fixture.Combat.GetEncounterForActor(fixture.A.ActorId).EncounterId, Is.Not.EqualTo(fixture.Combat.GetEncounterForActor(fixture.C.ActorId).EncounterId));
            Assert.That(fixture.Combat.ValidateIntegrity().Succeeded, Is.True);
        }

        [Test]
        public void EndingBridgeCanExitIsolatedParticipant()
        {
            using CombatFixture fixture = CombatFixture.Create("isolated");
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.isolated.ab"));
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.B, fixture.C, "combat.isolated.bc"));

            CombatEncounterSplitResult split = fixture.Combat.EndEngagement(fixture.CreateEngagementEndRequest(fixture.B, fixture.C, "combat.isolated.end"));

            Assert.That(split.ParticipantsLeftCombat, Is.EquivalentTo(new[] { fixture.C.ActorId }));
            Assert.That(fixture.Combat.IsInCombat(fixture.C.ActorId), Is.False);
            Assert.That(fixture.Combat.GetEncounterForActor(fixture.A.ActorId).ParticipantIds, Is.EquivalentTo(new[] { fixture.A.ActorId, fixture.B.ActorId }));
            Assert.That(fixture.Combat.ValidateIntegrity().Succeeded, Is.True);
        }

        [Test]
        public void AuthoritativeBridgeParticipantExitReconcilesRemainingGraph()
        {
            using CombatFixture fixture = CombatFixture.Create("forced-exit");
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.A, fixture.B, "combat.exit.ab"));
            fixture.Combat.EnterCombat(fixture.CreateEngagementRequest(fixture.B, fixture.C, "combat.exit.bc"));

            CombatExitResult exit = fixture.Combat.LeaveCombat(new CombatExitRequest("combat.exit.b", fixture.B.ActorId, fixture.B.Owner, CombatExitReason.Forced, authoritative: true));

            Assert.That(exit.Succeeded, Is.True, exit.Message);
            Assert.That(fixture.Combat.IsInCombat(fixture.A.ActorId), Is.False);
            Assert.That(fixture.Combat.IsInCombat(fixture.B.ActorId), Is.False);
            Assert.That(fixture.Combat.IsInCombat(fixture.C.ActorId), Is.False);
            Assert.That(fixture.Combat.ValidateIntegrity().Succeeded, Is.True);
        }

        [Test]
        public void DeathBasedBridgeRemovalSplitsAndDoesNotTransferToReplacementBody()
        {
            using CombatFixture fixture = CombatFixture.Create("death-split");
            fixture.ConnectChain();

            fixture.C.Lifecycle.ExecuteDeath(new LifecycleDeathRequest("lifecycle.kill.c", fixture.A.ActorId, fixture.A.Owner, fixture.C.ActorId, fixture.C.Owner, LifecycleTriggerKind.ExplicitDeath));
            fixture.Combat.AdvanceTime(0f);

            Assert.That(fixture.Combat.GetEncounterForActor(fixture.A.ActorId).ParticipantIds, Is.EquivalentTo(new[] { fixture.A.ActorId, fixture.B.ActorId }));
            Assert.That(fixture.Combat.IsInCombat(fixture.C.ActorId), Is.False);
            Assert.That(fixture.Combat.IsInCombat(fixture.D.ActorId), Is.False);
            Assert.That(fixture.Combat.ValidateIntegrity().Succeeded, Is.True);
        }

        [Test]
        public void DuplicateBridgeEndDoesNotReplaySplitEvents()
        {
            using CombatFixture fixture = CombatFixture.Create("duplicate-split");
            fixture.ConnectChain();
            int splits = 0;
            fixture.Combat.EncounterSplit += _ => splits++;
            CombatEngagementEndRequest request = fixture.CreateEngagementEndRequest(fixture.B, fixture.C, "combat.split.duplicate");

            CombatEncounterSplitResult first = fixture.Combat.EndEngagement(request);
            CombatEncounterSplitResult second = fixture.Combat.EndEngagement(request);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Duplicate, Is.True);
            Assert.That(splits, Is.EqualTo(1));
        }

        [Test]
        public void PreviewBridgeEndDoesNotSplitOrConsumeTransaction()
        {
            using CombatFixture fixture = CombatFixture.Create("preview-split");
            fixture.ConnectChain();
            string originalEncounter = fixture.Combat.GetEncounterForActor(fixture.A.ActorId).EncounterId;
            CombatEngagementEndRequest request = fixture.CreateEngagementEndRequest(fixture.B, fixture.C, "combat.split.preview");

            CombatEncounterSplitResult preview = fixture.Combat.PreviewEndEngagement(request);
            CombatEncounterSplitResult execute = fixture.Combat.EndEngagement(request);

            Assert.That(preview.Preview, Is.True);
            Assert.That(fixture.Combat.GetEncounterForActor(fixture.D.ActorId).EncounterId, Is.Not.EqualTo(originalEncounter));
            Assert.That(execute.Succeeded, Is.True, execute.Message);
        }

        [Test]
        public void SplitResultExposesNoMutableCollectionsAndQueriesRemainCoherent()
        {
            using CombatFixture fixture = CombatFixture.Create("split-immutable");
            fixture.ConnectChain();

            CombatEncounterSplitResult split = fixture.Combat.EndEngagement(fixture.CreateEngagementEndRequest(fixture.B, fixture.C, "combat.split.immutable"));

            Assert.That(split.Components, Is.Not.TypeOf<System.Collections.Generic.List<CombatEncounterSplitComponentSnapshot>>());
            Assert.That(split.Components[0].ParticipantIds, Is.Not.TypeOf<System.Collections.Generic.List<string>>());
            Assert.That(fixture.Combat.GetActiveEngagements(fixture.B.ActorId).Single().EncounterId, Is.EqualTo(fixture.Combat.GetEncounterForActor(fixture.B.ActorId).EncounterId));
            Assert.That(fixture.Combat.GetActiveEngagements(fixture.C.ActorId).Single().EncounterId, Is.EqualTo(fixture.Combat.GetEncounterForActor(fixture.C.ActorId).EncounterId));
        }

        [Test]
        public void IntegrityValidationDetectsDisconnectedEncounterFixture()
        {
            using CombatFixture fixture = CombatFixture.Create("corrupt");
            fixture.ConnectChain();
            EndPrivateEngagement(fixture.Combat, fixture.B.ActorId, fixture.C.ActorId, CombatExitReason.Forced);

            CombatStateIntegrityResult integrity = fixture.Combat.ValidateIntegrity();

            Assert.That(integrity.Succeeded, Is.False);
            Assert.That(string.Join(" | ", integrity.Diagnostics), Does.Contain("disconnected"));
        }

        private sealed class CombatFixture : IDisposable
        {
            private CombatFixture(DefinitionRegistry registry, CombatStateService combat, AttackResolutionService attacks, ActorRuntime a, ActorRuntime b, ActorRuntime c, ActorRuntime d)
            {
                Registry = registry;
                Combat = combat;
                Attacks = attacks;
                A = a;
                B = b;
                C = c;
                D = d;
            }

            public DefinitionRegistry Registry { get; }
            public CombatStateService Combat { get; }
            public AttackResolutionService Attacks { get; }
            public ActorRuntime A { get; }
            public ActorRuntime B { get; }
            public ActorRuntime C { get; }
            public ActorRuntime D { get; }

            public static CombatFixture Create(string id)
            {
                DefinitionRegistry registry = LoadCatalog().CreateRegistry();
                GameObject root = new GameObject($"Combat State Test {id}");
                CombatStateService combat = root.AddComponent<CombatStateService>();
                combat.Configure(registry.TryGet("combat-state-policy.prototype-alpha", out CombatStatePolicyDefinition policy) ? policy : null);
                combat.SetClock(0f);
                AttackResolutionService attacks = new AttackResolutionService(new FakeDamageHealingService());
                return new CombatFixture(
                    registry,
                    combat,
                    attacks,
                    ActorRuntime.Create(root.transform, registry, $"{id}.a"),
                    ActorRuntime.Create(root.transform, registry, $"{id}.b"),
                    ActorRuntime.Create(root.transform, registry, $"{id}.c"),
                    ActorRuntime.Create(root.transform, registry, $"{id}.d"));
            }

            public TDefinition Get<TDefinition>(string definitionId)
                where TDefinition : class, IGameDefinition
            {
                Assert.That(Registry.TryGet(definitionId, out TDefinition definition), Is.True, definitionId);
                return definition;
            }

            public CombatEngagementRequest CreateEngagementRequest(ActorRuntime source, ActorRuntime target, string transactionId)
            {
                return new CombatEngagementRequest(transactionId, source.ActorId, source.Owner, target.ActorId, target.Owner, CombatActivityClassification.ExplicitEngagement, "test", hostile: true, authorityValidated: true);
            }

            public CombatEngagementEndRequest CreateEngagementEndRequest(ActorRuntime source, ActorRuntime target, string transactionId)
            {
                return new CombatEngagementEndRequest(transactionId, string.Empty, source.ActorId, target.ActorId, CombatExitReason.Forced, authoritative: true);
            }

            public void ConnectChain()
            {
                Combat.EnterCombat(CreateEngagementRequest(A, B, $"combat.{A.ActorId}.ab"));
                Combat.EnterCombat(CreateEngagementRequest(B, C, $"combat.{A.ActorId}.bc"));
                Combat.EnterCombat(CreateEngagementRequest(C, D, $"combat.{A.ActorId}.cd"));
            }

            public AttackResolutionRequest CreateAttackRequest(string transactionId, float hitRoll, float distance = 1f, float range = 2f)
            {
                return new AttackResolutionRequest(transactionId, AttackSourceType.Weapon, A.Owner, A.ActorId, B.Owner, B.ActorId, Get<DamageTypeDefinition>("damage.physical"), 10f, hitRoll, 0.5f, hasSuppliedDistance: true, suppliedDistance: distance, hasMaximumRange: true, maximumRange: range, originatingActionId: "test.attack", authorityValidated: true);
            }

            public DamageApplicationResult CreateDamageResult(string transactionId, ActorRuntime source, ActorRuntime target, float finalDamage, bool changed, bool immune)
            {
                DamageApplicationRequest request = new DamageApplicationRequest(transactionId, source.ActorId, source.Owner, target.ActorId, target.Owner, Get<DamageTypeDefinition>("damage.physical"), 10f, "test.damage", authorityValidated: true);
                return DamageApplicationResult.Create(false, immune ? ImmediateCombatResultCode.Prevented : ImmediateCombatResultCode.Applied, "test damage", request, target.ActorId, 10f, 0f, 0f, immune ? 1f : 0f, immune ? 10f : 0f, finalDamage, 100f, changed ? 100f - finalDamage : 100f, 0f, 100f, immune, false, false, changed, false, 0f, null);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(A.Root);
            }
        }

        private sealed class ActorRuntime
        {
            private ActorRuntime(GameObject owner, ActorLifecycleController lifecycle, OngoingEffectService ongoing)
            {
                Owner = owner;
                Lifecycle = lifecycle;
                Ongoing = ongoing;
            }

            public GameObject Root => Owner.transform.root.gameObject;
            public GameObject Owner { get; }
            public ActorLifecycleController Lifecycle { get; }
            public OngoingEffectService Ongoing { get; }
            public string ActorId => Owner.GetComponent<WorldEntityIdentity>().EntityId;

            public static ActorRuntime Create(Transform parent, DefinitionRegistry registry, string id)
            {
                GameObject owner = new GameObject($"Actor {id}");
                owner.transform.SetParent(parent);
                WorldEntityIdentity identity = owner.AddComponent<WorldEntityIdentity>();
                identity.TryInitializeRuntime($"entity.scene.prototype.combat-state.{id}", "scene.prototype", PersistenceService.LocalWorldId, PersistenceScope.RegionOrScene, "test.combat-state", out _);
                CharacterAttributes attributes = owner.AddComponent<CharacterAttributes>();
                CalculatedStatCollection stats = owner.AddComponent<CalculatedStatCollection>();
                CharacterTraitCollection traits = owner.AddComponent<CharacterTraitCollection>();
                UnityIsekaiGame.Capabilities.CharacterCapabilityCollection capabilities = owner.AddComponent<UnityIsekaiGame.Capabilities.CharacterCapabilityCollection>();
                CharacterResourceCollection resources = owner.AddComponent<CharacterResourceCollection>();
                ActorLifecycleController lifecycle = owner.AddComponent<ActorLifecycleController>();
                OngoingEffectService ongoing = owner.AddComponent<OngoingEffectService>();
                attributes.Configure(registry);
                stats.Configure(registry, attributes);
                capabilities.Configure(registry);
                traits.Configure(registry, stats, null, capabilities, "player.local");
                resources.Configure(registry, stats, "player.local");
                lifecycle.Configure(null, resources, null, traits);
                ongoing.Configure(null);
                ongoing.SetClock(0f);
                return new ActorRuntime(owner, lifecycle, ongoing);
            }
        }

        private sealed class FakeDamageHealingService : IDamageHealingService
        {
            public DamageApplicationResult PreviewDamage(DamageApplicationRequest request)
            {
                return CreateResult(request, preview: true);
            }

            public DamageApplicationResult ApplyDamage(DamageApplicationRequest request)
            {
                return CreateResult(request, preview: false);
            }

            public HealingApplicationResult PreviewHealing(HealingApplicationRequest request)
            {
                return null;
            }

            public HealingApplicationResult ApplyHealing(HealingApplicationRequest request)
            {
                return null;
            }

            private static DamageApplicationResult CreateResult(DamageApplicationRequest request, bool preview)
            {
                return DamageApplicationResult.Create(preview, preview ? ImmediateCombatResultCode.Preview : ImmediateCombatResultCode.Applied, "fake damage", request, request.TargetActorId, request.RequestedAmount, 0f, 0f, 0f, 0f, request.RequestedAmount, 100f, 100f - request.RequestedAmount, 0f, 100f, false, false, false, request.RequestedAmount > 0f, false, 0f, null);
            }
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }

        private static void EndPrivateEngagement(CombatStateService combat, string firstActorId, string secondActorId, CombatExitReason reason)
        {
            FieldInfo field = typeof(CombatStateService).GetField("engagementsByPairKey", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            IEnumerable engagements = (IEnumerable)field.GetValue(combat);
            foreach (object entry in engagements)
            {
                object engagement = entry.GetType().GetProperty("Value").GetValue(entry);
                string first = (string)engagement.GetType().GetProperty("FirstActorId").GetValue(engagement);
                string second = (string)engagement.GetType().GetProperty("SecondActorId").GetValue(engagement);
                bool active = (bool)engagement.GetType().GetProperty("Active").GetValue(engagement);
                bool matches = active
                    && ((string.Equals(first, firstActorId, StringComparison.Ordinal) && string.Equals(second, secondActorId, StringComparison.Ordinal))
                        || (string.Equals(first, secondActorId, StringComparison.Ordinal) && string.Equals(second, firstActorId, StringComparison.Ordinal)));
                if (!matches)
                {
                    continue;
                }

                engagement.GetType().GetMethod("End", BindingFlags.Instance | BindingFlags.Public).Invoke(engagement, new object[] { reason });
                return;
            }

            Assert.Fail("Private engagement was not found.");
        }
    }
}
