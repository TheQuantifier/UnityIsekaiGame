using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class CombatExecutionCostsCooldownsTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeCatalog_ResolvesCombatExecutionDefinitionsAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            DefinitionRegistry registry = catalog.CreateRegistry();

            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
            AssertExecution(registry, "combat-execution.basic-attack", CombatExecutionActionType.Attack);
            AssertExecution(registry, "combat-execution.arcane-spell", CombatExecutionActionType.Ability);
            AssertExecution(registry, "combat-execution.quick-guard", CombatExecutionActionType.Defense);
        }

        [Test]
        public void PreviewBeginDoesNotMutateResourcesEventsOrCooldowns()
        {
            using Fixture fixture = Fixture.Create();
            CombatExecutionDefinition definition = fixture.CreateExecution("combat-execution.test-preview", ResourceIds.Stamina, 10f);
            CountingHandler handler = new CountingHandler();
            CombatExecutionService service = new CombatExecutionService(new[] { handler });
            int began = 0;
            int costs = 0;
            service.ExecutionBegan += _ => began++;
            service.CostCommitted += _ => costs++;
            float before = fixture.Resources.GetCurrent(ResourceIds.Stamina);

            CombatExecutionResult result = service.PreviewBeginExecution(fixture.Begin("exec.preview.begin", definition));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(handler.PreviewCalls, Is.EqualTo(1));
            Assert.That(handler.ExecuteCalls, Is.Zero);
            Assert.That(began, Is.Zero);
            Assert.That(costs, Is.Zero);
            Assert.That(fixture.Resources.GetCurrent(ResourceIds.Stamina), Is.EqualTo(before).Within(0.001f));
            Assert.That(service.GetExecutionState(fixture.ActorId), Is.Null);
            Assert.That(service.GetCooldownState(fixture.ActorId, definition.ResolveCooldownKey()), Is.Null);
        }

        [Test]
        public void CommitSpendsResourceAndExecutesHandlerExactlyOnce()
        {
            using Fixture fixture = Fixture.Create();
            CombatExecutionDefinition definition = fixture.CreateExecution("combat-execution.test-once", ResourceIds.Stamina, 12f, windUp: 0.25f, recovery: 0.25f);
            CountingHandler handler = new CountingHandler();
            CombatExecutionService service = new CombatExecutionService(new[] { handler });
            float before = fixture.Resources.GetCurrent(ResourceIds.Stamina);

            CombatExecutionResult begin = service.BeginExecution(fixture.Begin("exec.once.begin", definition, now: 1f));
            CombatExecutionResult tooEarly = service.CommitExecution(fixture.Commit("exec.once.commit.early", begin.State.ExecutionInstanceId, now: 1.1f));
            CombatExecutionResult commit = service.CommitExecution(fixture.Commit("exec.once.commit", begin.State.ExecutionInstanceId, now: 1.25f));
            CombatExecutionResult duplicate = service.CommitExecution(fixture.Commit("exec.once.commit", begin.State.ExecutionInstanceId, now: 1.25f));

            Assert.That(begin.Succeeded, Is.True, begin.Message);
            Assert.That(tooEarly.Succeeded, Is.False);
            Assert.That(tooEarly.Code, Is.EqualTo(CombatExecutionResultCode.ExecutionTooEarly));
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(handler.ExecuteCalls, Is.EqualTo(1));
            Assert.That(fixture.Resources.GetCurrent(ResourceIds.Stamina), Is.EqualTo(before - 12f).Within(0.001f));
        }

        [Test]
        public void MultiResourceCostIsAtomicWhenSecondResourceFails()
        {
            using Fixture fixture = Fixture.Create();
            CombatExecutionDefinition definition = fixture.CreateExecution("combat-execution.test-atomic", new[]
            {
                fixture.CreateResourceCost(ResourceIds.Stamina, 10f),
                fixture.CreateResourceCost(ResourceIds.Mana, 10000f)
            });
            CombatExecutionService service = new CombatExecutionService(new[] { new CountingHandler() });
            float staminaBefore = fixture.Resources.GetCurrent(ResourceIds.Stamina);

            CombatExecutionResult begin = service.BeginExecution(fixture.Begin("exec.atomic.begin", definition));
            CombatExecutionResult commit = service.CommitExecution(fixture.Commit("exec.atomic.commit", begin.State.ExecutionInstanceId));

            Assert.That(begin.Succeeded, Is.True, begin.Message);
            Assert.That(commit.Succeeded, Is.False);
            Assert.That(commit.Code, Is.EqualTo(CombatExecutionResultCode.InsufficientResource));
            Assert.That(fixture.Resources.GetCurrent(ResourceIds.Stamina), Is.EqualTo(staminaBefore).Within(0.001f));
        }

        [Test]
        public void UnsupportedInventoryCurrencyAndAmmoCostsRejectWithoutMutation()
        {
            using Fixture fixture = Fixture.Create();
            foreach (CombatExecutionCostType type in new[] { CombatExecutionCostType.InventoryItem, CombatExecutionCostType.Currency, CombatExecutionCostType.Ammunition })
            {
                CombatExecutionDefinition beginDefinition = fixture.CreateExecution($"combat-execution.test-unsupported-begin-{type.ToString().ToLowerInvariant()}", new[] { fixture.CreateUnsupportedCost(type, CombatExecutionCostCommitPoint.OnBegin) });
                CombatExecutionService beginService = new CombatExecutionService(new[] { new CountingHandler() });
                CombatExecutionResult beginPreview = beginService.PreviewBeginExecution(fixture.Begin($"exec.unsupported.begin.{type}", beginDefinition));

                Assert.That(beginPreview.Succeeded, Is.False);
                Assert.That(beginPreview.Code, Is.EqualTo(CombatExecutionResultCode.UnsupportedCostType));

                CombatExecutionDefinition executionDefinition = fixture.CreateExecution($"combat-execution.test-unsupported-execution-{type.ToString().ToLowerInvariant()}", new[] { fixture.CreateUnsupportedCost(type, CombatExecutionCostCommitPoint.OnExecution) });
                CombatExecutionService executionService = new CombatExecutionService(new[] { new CountingHandler() });
                CombatExecutionResult begin = executionService.BeginExecution(fixture.Begin($"exec.unsupported.execution.begin.{type}", executionDefinition));
                CombatExecutionResult commitPreview = executionService.PreviewCommitExecution(fixture.Commit($"exec.unsupported.execution.commit.{type}", begin.State.ExecutionInstanceId));

                Assert.That(begin.Succeeded, Is.True, begin.Message);
                Assert.That(commitPreview.Succeeded, Is.False);
                Assert.That(commitPreview.Code, Is.EqualTo(CombatExecutionResultCode.UnsupportedCostType));
            }
        }

        [Test]
        public void CooldownAndChargesUseExactReadyBoundary()
        {
            using Fixture fixture = Fixture.Create();
            CombatExecutionDefinition definition = fixture.CreateExecution("combat-execution.test-charges", ResourceIds.Stamina, 1f, cooldown: 2f, charges: 2, chargeRecovery: 2f);
            CombatExecutionService service = new CombatExecutionService(new[] { new CountingHandler() });

            CombatExecutionResult first = BeginAndCommit(service, fixture, definition, "exec.charge.first", 1f);
            CombatExecutionResult second = BeginAndCommit(service, fixture, definition, "exec.charge.second", 1.1f, allowOverlap: true);
            CombatExecutionResult blocked = service.BeginExecution(fixture.Begin("exec.charge.blocked", definition, now: 1.2f));

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(blocked.Code, Is.EqualTo(CombatExecutionResultCode.NoChargesAvailable));

            service.ProcessExecutionTime(3.1f);
            CombatExecutionResult ready = service.BeginExecution(fixture.Begin("exec.charge.ready", definition, now: 3.1f));

            Assert.That(ready.Succeeded, Is.True, ready.Message);
        }

        [Test]
        public void CancelBeforeExecutionRefundsBeginCostsAndDoesNotRunHandler()
        {
            using Fixture fixture = Fixture.Create();
            CombatExecutionDefinition definition = fixture.CreateExecution("combat-execution.test-cancel", new[] { fixture.CreateResourceCost(ResourceIds.Stamina, 8f, CombatExecutionCostCommitPoint.OnBegin) });
            CountingHandler handler = new CountingHandler();
            CombatExecutionService service = new CombatExecutionService(new[] { handler });
            float before = fixture.Resources.GetCurrent(ResourceIds.Stamina);

            CombatExecutionResult begin = service.BeginExecution(fixture.Begin("exec.cancel.begin", definition, now: 1f));
            CombatExecutionResult cancel = service.CancelExecution(fixture.Cancel("exec.cancel.cancel", begin.State.ExecutionInstanceId, now: 1.1f));

            Assert.That(begin.Succeeded, Is.True, begin.Message);
            Assert.That(cancel.Succeeded, Is.True, cancel.Message);
            Assert.That(handler.ExecuteCalls, Is.Zero);
            Assert.That(fixture.Resources.GetCurrent(ResourceIds.Stamina), Is.EqualTo(before).Within(0.001f));
            Assert.That(service.GetExecutionState(fixture.ActorId), Is.Null);
        }

        [Test]
        public void RestoreClearsTransientCommitmentsButKeepsCooldownDataAndEmitsNoEvents()
        {
            using Fixture fixture = Fixture.Create();
            CombatExecutionDefinition definition = fixture.CreateExecution("combat-execution.test-restore", ResourceIds.Stamina, 1f, cooldown: 5f);
            CombatExecutionService service = new CombatExecutionService(new[] { new CountingHandler() });
            int cancelled = 0;
            int interrupted = 0;
            service.ExecutionCancelled += _ => cancelled++;
            service.ExecutionInterrupted += _ => interrupted++;

            CombatExecutionResult commit = BeginAndCommit(service, fixture, definition, "exec.restore", 1f);
            CombatExecutionSaveData save = service.CreateSaveData("player.local", "person.local");
            CombatExecutionResult active = service.BeginExecution(fixture.Begin("exec.restore.active", definition, now: 1.5f));

            Assert.That(commit.Succeeded, Is.True);
            Assert.That(active.Succeeded, Is.False, "The active attempt should be blocked by cooldown before restore.");

            bool restored = service.RestoreFromSaveData(save, "player.local", out string failureReason, restoring: true);

            Assert.That(restored, Is.True, failureReason);
            Assert.That(service.GetExecutionState(fixture.ActorId), Is.Null);
            Assert.That(service.GetCooldownState(fixture.ActorId, definition.ResolveCooldownKey()), Is.Not.Null);
            Assert.That(cancelled + interrupted, Is.Zero);
        }

        [Test]
        public void CombatExecutionPersistenceParticipantRegistersAndOrdersAfterResources()
        {
            CombatExecutionService combatExecution = new CombatExecutionService(new[] { new CountingHandler() });
            PlayerResourcesPersistenceParticipant resources = new PlayerResourcesPersistenceParticipant(null, null, null, null);
            PlayerActorLifecyclePersistenceParticipant lifecycle = new PlayerActorLifecyclePersistenceParticipant(null, null);
            PlayerCombatExecutionPersistenceParticipant participant = new PlayerCombatExecutionPersistenceParticipant(combatExecution);
            PersistenceService persistence = new PersistenceService();

            persistence.RegisterParticipant(new DependencyParticipant(PlayerAttributesPersistenceParticipant.Key), out _);
            persistence.RegisterParticipant(new DependencyParticipant(PlayerStatusEffectsPersistenceParticipant.Key), out _);
            Assert.That(persistence.RegisterParticipant(resources, out string resourceFailure), Is.True, resourceFailure);
            Assert.That(persistence.RegisterParticipant(lifecycle, out string lifecycleFailure), Is.True, lifecycleFailure);
            Assert.That(persistence.RegisterParticipant(participant, out string failureReason), Is.True, failureReason);

            PersistenceDependencyReport report = persistence.BuildParticipantDependencyReport();

            Assert.That(report.succeeded, Is.True, report.message);
            Assert.That(report.orderedParticipantKeys, Does.Contain(PlayerCombatExecutionPersistenceParticipant.Key));
            Assert.That(Array.IndexOf(report.orderedParticipantKeys, PlayerResourcesPersistenceParticipant.Key), Is.LessThan(Array.IndexOf(report.orderedParticipantKeys, PlayerCombatExecutionPersistenceParticipant.Key)));
            Assert.That(Array.IndexOf(report.orderedParticipantKeys, PlayerActorLifecyclePersistenceParticipant.Key), Is.LessThan(Array.IndexOf(report.orderedParticipantKeys, PlayerCombatExecutionPersistenceParticipant.Key)));
        }

        private sealed class DependencyParticipant : IPersistenceParticipant
        {
            public DependencyParticipant(string key) => ParticipantKey = key;
            public string ParticipantKey { get; }
            public int ParticipantSchemaVersion => 1;
            public bool IsRequired => true;
            public PersistenceScope Scope => PersistenceScope.Player;
            public string OwnerId => PersistenceService.LocalPlayerId;
            public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Bootstrap;
            public int LoadPriority => -100;
            public PersistenceParticipantSaveResult CapturePayload() => PersistenceParticipantSaveResult.Success("{}");
            public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion) => PersistenceParticipantPrepareResult.Success(new object());
            public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload) => PersistenceParticipantCommitResult.Success();
            public void DiscardPreparedPayload(object preparedPayload) { }
        }

        [Test]
        public void ParticipantPrepareCommitRestoresCooldownsClearsCommitmentsSilentlyAndDoesNotProgressOffline()
        {
            using Fixture fixture = Fixture.Create();
            CombatExecutionDefinition cooldownDefinition = fixture.CreateExecution("combat-execution.test-participant-restore-cooldown", ResourceIds.Stamina, 1f, cooldown: 10f);
            CombatExecutionDefinition activeDefinition = fixture.CreateExecution("combat-execution.test-participant-restore-active", ResourceIds.Stamina, 1f, windUp: 5f);
            CombatExecutionService service = new CombatExecutionService(new[] { new CountingHandler() });
            PlayerCombatExecutionPersistenceParticipant participant = new PlayerCombatExecutionPersistenceParticipant(service);

            CombatExecutionResult committed = BeginAndCommit(service, fixture, cooldownDefinition, "exec.participant.restore.cooldown", 1f);
            CombatExecutionSaveData saveData = service.CreateSaveData(PersistenceService.LocalPlayerId, "person.local");
            CombatExecutionResult active = service.BeginExecution(fixture.Begin("exec.participant.restore.active", activeDefinition, now: 2f));
            int gameplayEvents = 0;
            service.ExecutionBegan += _ => gameplayEvents++;
            service.ExecutionCommitted += _ => gameplayEvents++;
            service.CombatExecutionCommitted += _ => gameplayEvents++;
            service.ExecutionCancelled += _ => gameplayEvents++;
            service.ExecutionInterrupted += _ => gameplayEvents++;
            service.CooldownChanged += _ => gameplayEvents++;

            PersistenceParticipantPrepareResult prepare = participant.PreparePayload(JsonUtility.ToJson(saveData), PlayerCombatExecutionPersistenceParticipant.CurrentParticipantSchemaVersion);
            CombatExecutionStateSnapshot stillActiveAfterPrepare = service.GetExecutionState(fixture.ActorId, active.State.ExecutionInstanceId);
            PersistenceParticipantCommitResult commit = participant.CommitPreparedPayload(prepare.PreparedPayload);
            CombatExecutionCooldownSnapshot cooldown = service.GetCooldownState(fixture.ActorId, cooldownDefinition.ResolveCooldownKey());

            Assert.That(committed.Succeeded, Is.True, committed.Message);
            Assert.That(active.Succeeded, Is.True, active.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(stillActiveAfterPrepare, Is.Not.Null, "Prepare must not mutate active commitments.");
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(service.GetExecutionState(fixture.ActorId, active.State.ExecutionInstanceId), Is.Null);
            Assert.That(cooldown, Is.Not.Null);
            Assert.That(cooldown.CurrentCharges, Is.EqualTo(0));
            Assert.That(cooldown.CooldownReadyAt, Is.EqualTo(11f).Within(0.001f));
            Assert.That(gameplayEvents, Is.Zero);
        }

        [Test]
        public void CommitPublishesImmutableCommittedEventWithReactionMetadata()
        {
            using Fixture fixture = Fixture.Create();
            using Fixture target = Fixture.Create();
            CombatExecutionDefinition definition = fixture.CreateExecution("combat-execution.test-committed-event", ResourceIds.Stamina, 3f, cooldown: 2f);
            TargetingHandler handler = new TargetingHandler();
            CombatExecutionService service = new CombatExecutionService(new ICombatExecutionHandler[] { handler });
            CombatExecutionCommitted committedEvent = null;
            AttackResolutionRequest payload = new AttackResolutionRequest(
                "attack.metadata",
                AttackSourceType.Weapon,
                fixture.Actor,
                fixture.ActorId,
                target.Actor,
                target.ActorId,
                null,
                1f,
                0f,
                0f,
                originatingActionId: "action.test",
                metadata: new Dictionary<string, string> { ["source"] = "test" },
                authorityValidated: true);
            service.CombatExecutionCommitted += committed => committedEvent = committed;

            CombatExecutionResult begin = service.BeginExecution(fixture.Begin("exec.metadata.begin", definition, payload: payload));
            CombatExecutionResult commit = service.CommitExecution(fixture.Commit("exec.metadata.commit", begin.State.ExecutionInstanceId, payload: payload));

            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(committedEvent, Is.Not.Null);
            Assert.That(committedEvent.TransactionId, Is.EqualTo("exec.metadata.commit"));
            Assert.That(committedEvent.BeginTransactionId, Is.EqualTo("exec.metadata.begin"));
            Assert.That(committedEvent.ExecutionInstanceId, Is.EqualTo(begin.State.ExecutionInstanceId));
            Assert.That(committedEvent.ActorId, Is.EqualTo(fixture.ActorId));
            Assert.That(committedEvent.ActorBodyId, Is.EqualTo(begin.ActorBodyId));
            Assert.That(committedEvent.TargetActorId, Is.EqualTo(target.ActorId));
            Assert.That(committedEvent.DefinitionId, Is.EqualTo(definition.Id));
            Assert.That(committedEvent.UnderlyingResult, Is.SameAs(handler.LastExecutionPayloadResult));
            Assert.That(committedEvent.Costs, Has.Count.EqualTo(1));
            Assert.That(committedEvent.Cooldown.CurrentCharges, Is.Zero);
            Assert.That(committedEvent.CurrentCharges, Is.Zero);
            Assert.That(committedEvent.MaximumCharges, Is.EqualTo(1));
            Assert.That(ContainsContext(committedEvent.Context, "attack.transaction", "attack.metadata"), Is.True);
            Assert.That(ContainsContext(committedEvent.Context, "execution.actionType", CombatExecutionActionType.Custom.ToString()), Is.True);
        }

        [Test]
        public void LifecycleInvalidationAndReplacementBodyClearOrRejectCommitments()
        {
            using Fixture fixture = Fixture.Create();
            CombatExecutionDefinition definition = fixture.CreateExecution("combat-execution.test-lifecycle", ResourceIds.Stamina, 1f, windUp: 1f);
            CombatExecutionService service = new CombatExecutionService(new[] { new CountingHandler() });
            CombatExecutionResult begin = service.BeginExecution(fixture.Begin("exec.lifecycle.begin", definition, now: 1f));
            fixture.SetLifecycleState(ActorLifecycleState.Dead);

            CombatExecutionResult commit = service.CommitExecution(fixture.Commit("exec.lifecycle.commit", begin.State.ExecutionInstanceId, now: 2f));

            Assert.That(commit.Succeeded, Is.False);
            Assert.That(commit.Code, Is.EqualTo(CombatExecutionResultCode.ActorCannotAct));
            Assert.That(service.GetExecutionState(fixture.ActorId), Is.Null);

            using Fixture replacement = Fixture.Create();
            CombatExecutionResult secondBegin = service.BeginExecution(replacement.Begin("exec.body.begin", definition, now: 3f));
            GameObject replacementBody = replacement.CreateReplacementBody();
            try
            {
                CombatExecutionResult stale = service.CommitExecution(new CombatExecutionCommitRequest("exec.body.commit", secondBegin.State.ExecutionInstanceId, replacementBody, replacement.ActorId, 4f, true));
                Assert.That(stale.Succeeded, Is.False);
                Assert.That(stale.Code, Is.EqualTo(CombatExecutionResultCode.StaleBody));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(replacementBody);
            }
        }

        [Test]
        public void RuntimeAssemblyDoesNotReferenceDevelopmentOrTests()
        {
            AssemblyName[] references = typeof(CombatExecutionService).Assembly.GetReferencedAssemblies();

            Assert.That(Array.Exists(references, reference => reference.Name.Contains("Development") || reference.Name.Contains("Tests")), Is.False);
        }

        private static CombatExecutionResult BeginAndCommit(CombatExecutionService service, Fixture fixture, CombatExecutionDefinition definition, string prefix, float now, bool allowOverlap = false)
        {
            CombatExecutionResult begin = service.BeginExecution(fixture.Begin($"{prefix}.begin", definition, now));
            if (!begin.Succeeded)
            {
                return begin;
            }

            CombatExecutionResult commit = service.CommitExecution(fixture.Commit($"{prefix}.commit", begin.State.ExecutionInstanceId, now + definition.WindUpDuration));
            service.ProcessExecutionTime(now + definition.WindUpDuration + definition.RecoveryDuration);
            return commit;
        }

        private static void AssertExecution(DefinitionRegistry registry, string id, CombatExecutionActionType expectedType)
        {
            Assert.That(registry.TryGet(id, out CombatExecutionDefinition definition), Is.True, id);
            Assert.That(definition.ActionType, Is.EqualTo(expectedType));
            Assert.That(definition.Costs.Count, Is.GreaterThanOrEqualTo(1));
        }

        private sealed class CountingHandler : ICombatExecutionHandler
        {
            public int PreviewCalls { get; private set; }
            public int ExecuteCalls { get; private set; }
            public bool FailExecution { get; set; }
            public string HandlerId => "test.counting";

            public bool CanHandle(CombatExecutionDefinition definition, object payload)
            {
                return payload == null || payload is CountingHandler;
            }

            public CombatExecutionHandlerResult Preview(CombatExecutionDefinition definition, object payload, string transactionId)
            {
                PreviewCalls++;
                return CombatExecutionHandlerResult.Success("Counting preview.");
            }

            public CombatExecutionHandlerResult Execute(CombatExecutionDefinition definition, object payload, string transactionId)
            {
                ExecuteCalls++;
                return FailExecution
                    ? CombatExecutionHandlerResult.Failure("TestFailure", "Counting execute failed.")
                    : CombatExecutionHandlerResult.Success("Counting execute.");
            }
        }

        private sealed class TargetingHandler : ICombatExecutionHandler
        {
            public object LastExecutionPayloadResult { get; private set; }
            public string HandlerId => "test.targeting";

            public bool CanHandle(CombatExecutionDefinition definition, object payload)
            {
                return payload is AttackResolutionRequest;
            }

            public CombatExecutionHandlerResult Preview(CombatExecutionDefinition definition, object payload, string transactionId)
            {
                return CombatExecutionHandlerResult.Success("Targeting preview.", payload);
            }

            public CombatExecutionHandlerResult Execute(CombatExecutionDefinition definition, object payload, string transactionId)
            {
                LastExecutionPayloadResult = payload;
                return CombatExecutionHandlerResult.Success("Targeting execute.", payload);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly DefinitionRegistry registry;

            private Fixture(DefinitionRegistry registry, GameObject actor, string actorId, CharacterResourceCollection resources, ActorLifecycleController lifecycle)
            {
                this.registry = registry;
                Actor = actor;
                ActorId = actorId;
                Resources = resources;
                Lifecycle = lifecycle;
            }

            public GameObject Actor { get; }
            public string ActorId { get; }
            public CharacterResourceCollection Resources { get; }
            public ActorLifecycleController Lifecycle { get; }

            public static Fixture Create()
            {
                DefinitionRegistry registry = LoadCatalog().CreateRegistry();
                GameObject actor = new GameObject($"Execution Actor {Guid.NewGuid():N}");
                CharacterAttributes attributes = actor.AddComponent<CharacterAttributes>();
                CalculatedStatCollection stats = actor.AddComponent<CalculatedStatCollection>();
                CharacterResourceCollection resources = actor.AddComponent<CharacterResourceCollection>();
                attributes.Configure(registry);
                stats.Configure(registry, attributes);
                resources.Configure(registry, stats, "player.local");
                WorldEntityIdentity identity = actor.AddComponent<WorldEntityIdentity>();
                Assert.That(identity.TrySetAuthoredIdentity($"execution-test-{Guid.NewGuid():N}", "scene.test", PersistenceScope.RegionOrScene, "test.execution", out string failureReason), Is.True, failureReason);
                ActorLifecycleController lifecycle = actor.AddComponent<ActorLifecycleController>();
                lifecycle.Configure(null, resources, null, null);
                return new Fixture(registry, actor, identity.EntityId, resources, lifecycle);
            }

            public CombatExecutionBeginRequest Begin(string transactionId, CombatExecutionDefinition definition, float now = 1f, object payload = null)
            {
                return new CombatExecutionBeginRequest(transactionId, definition, Actor, ActorId, now, authorityValidated: true, payload: payload);
            }

            public CombatExecutionCommitRequest Commit(string transactionId, string executionInstanceId, float now = 1f, object payload = null)
            {
                return new CombatExecutionCommitRequest(transactionId, executionInstanceId, Actor, ActorId, now, authorityValidated: true, payload: payload);
            }

            public CombatExecutionCancelRequest Cancel(string transactionId, string executionInstanceId, float now = 1f)
            {
                return new CombatExecutionCancelRequest(transactionId, executionInstanceId, Actor, ActorId, CombatExecutionCancellationReason.PlayerOrAIRequest, now);
            }

            public CombatExecutionDefinition CreateExecution(string id, string resourceId, float amount, float windUp = 0f, float recovery = 0f, float cooldown = 0f, int charges = 1, float chargeRecovery = 0f)
            {
                return CreateExecution(id, new[] { CreateResourceCost(resourceId, amount) }, windUp, recovery, cooldown, charges, chargeRecovery);
            }

            public CombatExecutionDefinition CreateExecution(string id, IReadOnlyList<CombatExecutionCostDefinition> costs, float windUp = 0f, float recovery = 0f, float cooldown = 0f, int charges = 1, float chargeRecovery = 0f)
            {
                CombatExecutionDefinition definition = ScriptableObject.CreateInstance<CombatExecutionDefinition>();
                SetField(definition, "executionId", id);
                SetField(definition, "displayName", id);
                SetField(definition, "actionType", CombatExecutionActionType.Custom);
                SetField(definition, "windUpDuration", windUp);
                SetField(definition, "recoveryDuration", recovery);
                SetField(definition, "cooldownDuration", cooldown);
                SetField(definition, "maximumCharges", charges);
                SetField(definition, "chargeRecoveryDuration", chargeRecovery);
                SetField(definition, "commitmentCategory", CombatCommitmentCategory.None);
                SetField(definition, "cooldownStartPoint", CombatExecutionCooldownStartPoint.OnExecution);
                SetField(definition, "costs", costs == null ? Array.Empty<CombatExecutionCostDefinition>() : new List<CombatExecutionCostDefinition>(costs).ToArray());
                return definition;
            }

            public CombatExecutionCostDefinition CreateResourceCost(string resourceId, float amount, CombatExecutionCostCommitPoint commitPoint = CombatExecutionCostCommitPoint.OnExecution)
            {
                Assert.That(registry.TryGet(resourceId, out ResourceDefinition resource), Is.True, resourceId);
                CombatExecutionCostDefinition cost = new CombatExecutionCostDefinition();
                SetField(cost, "costType", CombatExecutionCostType.Resource);
                SetField(cost, "resource", resource);
                SetField(cost, "amount", amount);
                SetField(cost, "commitPoint", commitPoint);
                return cost;
            }

            public CombatExecutionCostDefinition CreateUnsupportedCost(CombatExecutionCostType type, CombatExecutionCostCommitPoint commitPoint)
            {
                CombatExecutionCostDefinition cost = new CombatExecutionCostDefinition();
                SetField(cost, "costType", type);
                SetField(cost, "amount", 1f);
                SetField(cost, "commitPoint", commitPoint);
                return cost;
            }

            public void SetLifecycleState(ActorLifecycleState state)
            {
                typeof(ActorLifecycleController).GetField("lifecycleState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(Lifecycle, state);
            }

            public GameObject CreateReplacementBody()
            {
                GameObject replacement = new GameObject($"Execution Replacement {Guid.NewGuid():N}");
                CharacterSystemCoordinator coordinator = replacement.AddComponent<CharacterSystemCoordinator>();
                SetField(coordinator, "actorIdOverride", ActorId);
                return replacement;
            }

            public void Dispose()
            {
                if (Actor != null)
                {
                    UnityEngine.Object.DestroyImmediate(Actor);
                }
            }
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static bool ContainsContext(IReadOnlyList<KeyValuePair<string, string>> context, string key, string value)
        {
            for (int i = 0; i < context.Count; i++)
            {
                if (context[i].Key == key && context[i].Value == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
