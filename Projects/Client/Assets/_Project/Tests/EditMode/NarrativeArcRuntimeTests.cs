using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Tests
{
    public sealed class NarrativeArcRuntimeTests
    {
        [Test]
        public void PrototypeNarrativeArcDefinitionsRegisterAndValidate()
        {
            DefinitionRegistry registry = Registry();
            Assert.That(PrototypeNarrativeArcDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out NarrativeArcDefinition _)), Is.True);

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (NarrativeArcDefinition definition in PrototypeNarrativeArcDefinitionFactory.CreateMissingNarrativeArcDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            NarrativeArcValidationReport graph = NarrativeArcDefinitionValidator.ValidateGraph(registry.DefinitionsById.Values.OfType<NarrativeArcDefinition>().Select(definition => definition.ToRecordData()));
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
            Assert.That(graph.Succeeded, Is.True, string.Join(" | ", graph.Errors));
        }

        [Test]
        public void ArcStartPreviewAndDuplicateAreIdempotentAndSnapshotsImmutable()
        {
            NarrativeArcRuntime runtime = ArcRuntime(out _, out _);
            NarrativeArcOperationResult preview = runtime.StartArc(Start(PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId, "person.prototype.player", "tx.arc.preview", preview: true));
            NarrativeArcOperationResult start = runtime.StartArc(Start(PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId, "person.prototype.player", "tx.arc.start"));
            NarrativeArcOperationResult duplicate = runtime.StartArc(Start(PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId, "person.prototype.player", "tx.arc.start"));
            NarrativeArcSnapshot before = start.Snapshot;

            runtime.ApplySignal(Signal("tx.parallel.a", category: NarrativeArcSignalCategory.Custom, customState: "signal.parallel.a", actor: "person.prototype.player", arcDefinitionId: PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId));

            Assert.That(preview.Preview, Is.True);
            Assert.That(runtime.Count, Is.EqualTo(1));
            Assert.That(start.Succeeded, Is.True, start.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(before.Stages.Count(stage => stage.Lifecycle == NarrativeArcStageLifecycle.Completed), Is.Zero);
            Assert.That(runtime.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId }).Single().Stages.Count(stage => stage.Lifecycle == NarrativeArcStageLifecycle.Completed), Is.EqualTo(1));
        }

        [Test]
        public void StateDependencyCompletesStageAndInstantiatesQuestThroughQuestRuntime()
        {
            NarrativeArcRuntime runtime = ArcRuntime(out NarrativeStateRuntime states, out QuestRuntime quests);
            NarrativeArcOperationResult start = runtime.StartArc(Start(PrototypeNarrativeArcDefinitionFactory.GuildIntroArcDefinitionId, "person.prototype.player", "tx.guild.arc"));
            states.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId, "tx.guild.state", "person.prototype.player"));
            NarrativeArcOperationResult stateSignal = runtime.ApplySignal(Signal("tx.guild.signal.state", category: NarrativeArcSignalCategory.NarrativeState, sourceId: PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyDefinitionId, actor: "person.prototype.player", arcDefinitionId: PrototypeNarrativeArcDefinitionFactory.GuildIntroArcDefinitionId));

            Assert.That(start.Succeeded, Is.True, start.Message);
            Assert.That(stateSignal.Succeeded, Is.True, stateSignal.Message);
            NarrativeArcSnapshot snapshot = runtime.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.GuildIntroArcDefinitionId }).Single();
            Assert.That(snapshot.Stages.Single(stage => stage.StageDefinitionId == PrototypeNarrativeArcDefinitionFactory.GuildIntroJoinStageId).Lifecycle, Is.EqualTo(NarrativeArcStageLifecycle.Completed));
            Assert.That(snapshot.Stages.Single(stage => stage.StageDefinitionId == PrototypeNarrativeArcDefinitionFactory.GuildIntroPostingStageId).Lifecycle, Is.EqualTo(NarrativeArcStageLifecycle.Active));
            Assert.That(quests.Query(new QuestQuery { definitionId = PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Count, Is.EqualTo(1));
        }

        [Test]
        public void QuestOutcomeAndFailureSignalsBranchStagesWithoutOwningQuestState()
        {
            NarrativeArcRuntime runtime = ArcRuntime(out _, out QuestRuntime quests);
            runtime.StartArc(Start(PrototypeNarrativeArcDefinitionFactory.MerchantGuildArcDefinitionId, "person.prototype.player", "tx.merchant.arc"));
            QuestSnapshot quest = quests.Query(new QuestQuery { definitionId = PrototypeQuestDefinitionFactory.MerchantDeliveryDefinitionId, access = QuestVisibilityAccess.PrivilegedDiagnostic }).Single();
            NarrativeArcOperationResult failed = runtime.ApplySignal(Signal("tx.merchant.failed", category: NarrativeArcSignalCategory.QuestOutcome, questId: quest.QuestId, questDefinitionId: quest.QuestDefinitionId, outcome: QuestTerminalOutcomeKind.Failed, actor: "person.prototype.player", arcDefinitionId: PrototypeNarrativeArcDefinitionFactory.MerchantGuildArcDefinitionId));

            NarrativeArcSnapshot snapshot = runtime.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.MerchantGuildArcDefinitionId }).Single();
            Assert.That(failed.Succeeded, Is.True, failed.Message);
            Assert.That(snapshot.Stages.Single().Lifecycle, Is.EqualTo(NarrativeArcStageLifecycle.Skipped));
            Assert.That(quest.LifecycleState, Is.EqualTo(QuestRuntimeLifecycleState.Available));
        }

        [Test]
        public void ParallelBranchesConvergeOnlyAfterAtLeastNResolved()
        {
            NarrativeArcRuntime runtime = ArcRuntime(out _, out _);
            runtime.StartArc(Start(PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId, "person.prototype.player", "tx.parallel.arc"));
            runtime.ApplySignal(Signal("tx.parallel.a", category: NarrativeArcSignalCategory.Custom, customState: "signal.parallel.a", actor: "person.prototype.player", arcDefinitionId: PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId));
            NarrativeArcSnapshot afterOne = runtime.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId }).Single();
            runtime.ApplySignal(Signal("tx.parallel.b", category: NarrativeArcSignalCategory.Custom, customState: "signal.parallel.b", actor: "person.prototype.player", arcDefinitionId: PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId));
            NarrativeArcSnapshot afterTwo = runtime.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId }).Single();

            Assert.That(afterOne.Stages.Single(stage => stage.StageDefinitionId == PrototypeNarrativeArcDefinitionFactory.ParallelJoinStageId).Lifecycle, Is.EqualTo(NarrativeArcStageLifecycle.Locked));
            Assert.That(afterTwo.Stages.Single(stage => stage.StageDefinitionId == PrototypeNarrativeArcDefinitionFactory.ParallelJoinStageId).Lifecycle, Is.EqualTo(NarrativeArcStageLifecycle.Active));
        }

        [Test]
        public void HiddenArcRedactsStagesWithoutLeakingCounts()
        {
            NarrativeArcRuntime runtime = ArcRuntime(out NarrativeStateRuntime states, out _);
            runtime.StartArc(Start(PrototypeNarrativeArcDefinitionFactory.MayorInvestigationArcDefinitionId, string.Empty, "tx.mayor.arc", NarrativeArcScope.World));
            states.RequestTransition(StateRequest(PrototypeNarrativeStateDefinitionFactory.OpenInvestigationTransitionId, "tx.mayor.open", string.Empty, NarrativeStateScope.World));
            NarrativeArcSnapshot development = runtime.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.MayorInvestigationArcDefinitionId, developmentView = true }).Single();
            NarrativeArcSnapshot publicView = runtime.Query(new NarrativeArcQuery { arcDefinitionId = PrototypeNarrativeArcDefinitionFactory.MayorInvestigationArcDefinitionId, developmentView = false }).Single();

            Assert.That(development.IsHidden, Is.True);
            Assert.That(development.Stages.Count, Is.GreaterThan(0));
            Assert.That(publicView.Stages.Count, Is.Zero);
        }

        [Test]
        public void SaveRestoreRejectsCorruptArcWithoutReplayingActionsOrMutatingLiveState()
        {
            DefinitionRegistry registry = Registry();
            int actions = 0;
            NarrativeArcRuntime runtime = new NarrativeArcRuntime(registry, new NarrativeArcRuntimeIntegrations { QuestRuntime = new QuestRuntime(registry), ActionExecutor = (_, _) => ++actions >= 0 });
            runtime.StartArc(Start(PrototypeNarrativeArcDefinitionFactory.ParallelSupportArcDefinitionId, "person.prototype.player", "tx.persist.arc"));
            NarrativeArcPersistenceParticipant participant = new NarrativeArcPersistenceParticipant(runtime, () => registry, () => new NarrativeArcRuntimeIntegrations { QuestRuntime = new QuestRuntime(registry), ActionExecutor = (_, _) => ++actions >= 0 });
            PersistenceParticipantSaveResult save = participant.CapturePayload();

            NarrativeArcRuntime restored = new NarrativeArcRuntime(registry, new NarrativeArcRuntimeIntegrations { QuestRuntime = new QuestRuntime(registry), ActionExecutor = (_, _) => ++actions >= 0 });
            NarrativeArcPersistenceParticipant restoredParticipant = new NarrativeArcPersistenceParticipant(restored, () => registry, () => new NarrativeArcRuntimeIntegrations { QuestRuntime = new QuestRuntime(registry), ActionExecutor = (_, _) => ++actions >= 0 });
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, NarrativeArcPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            NarrativeArcRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.arcs[0].stages[0].stageRuntimeId = "wrong";
            int beforeReject = restored.Count;
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), NarrativeArcPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.Count, Is.EqualTo(beforeReject));
            Assert.That(actions, Is.Zero);
        }

        [Test]
        public void DefinitionValidatorRejectsStageAndCrossArcCycles()
        {
            NarrativeArcDefinitionData cyclic = new NarrativeArcDefinitionData
            {
                arcDefinitionId = "narrative-arc-definition.test.cycle",
                displayName = "Cycle",
                scope = NarrativeArcScope.Person,
                visibility = NarrativeEventVisibility.Public,
                stages = new[]
                {
                    new NarrativeArcStageDefinitionData
                    {
                        stageDefinitionId = "narrative-arc-stage-definition.test.a",
                        entryDependencies = new[] { new NarrativeArcDependencyDefinitionData { dependencyDefinitionId = "dependency.a", kind = NarrativeArcDependencyKind.StageResolved, requiredId = "narrative-arc-stage-definition.test.b" } }
                    },
                    new NarrativeArcStageDefinitionData
                    {
                        stageDefinitionId = "narrative-arc-stage-definition.test.b",
                        entryDependencies = new[] { new NarrativeArcDependencyDefinitionData { dependencyDefinitionId = "dependency.b", kind = NarrativeArcDependencyKind.StageResolved, requiredId = "narrative-arc-stage-definition.test.a" } }
                    }
                }
            };

            NarrativeArcDefinitionData crossA = new NarrativeArcDefinitionData { arcDefinitionId = "narrative-arc-definition.test.cross-a", scope = NarrativeArcScope.World, visibility = NarrativeEventVisibility.Public, stages = new[] { CrossStage("narrative-arc-stage-definition.test.cross-a.stage", "narrative-arc-definition.test.cross-b") } };
            NarrativeArcDefinitionData crossB = new NarrativeArcDefinitionData { arcDefinitionId = "narrative-arc-definition.test.cross-b", scope = NarrativeArcScope.World, visibility = NarrativeEventVisibility.Public, stages = new[] { CrossStage("narrative-arc-stage-definition.test.cross-b.stage", "narrative-arc-definition.test.cross-a") } };

            Assert.That(NarrativeArcDefinitionValidator.Validate(cyclic).Succeeded, Is.False);
            Assert.That(NarrativeArcDefinitionValidator.ValidateGraph(new[] { crossA, crossB }).Succeeded, Is.False);
        }

        private static NarrativeArcRuntime ArcRuntime(out NarrativeStateRuntime states, out QuestRuntime quests)
        {
            DefinitionRegistry registry = Registry();
            states = new NarrativeStateRuntime(registry);
            quests = new QuestRuntime(registry);
            return new NarrativeArcRuntime(registry, new NarrativeArcRuntimeIntegrations
            {
                QuestRuntime = quests,
                NarrativeStateRuntime = states
            });
        }

        private static NarrativeArcStartRequest Start(string arcDefinitionId, string actor, string tx, NarrativeArcScope scope = NarrativeArcScope.Person, bool preview = false)
        {
            return new NarrativeArcStartRequest
            {
                transactionId = tx,
                arcDefinitionId = arcDefinitionId,
                actorPersonId = actor,
                scopeKey = scope == NarrativeArcScope.World ? PersistenceService.LocalWorldId : actor,
                subjectId = actor,
                worldTime = 1d,
                preview = preview
            };
        }

        private static NarrativeArcSignalRequest Signal(string tx, NarrativeArcSignalCategory category, string sourceId = "", string customState = "", string questId = "", string questDefinitionId = "", QuestTerminalOutcomeKind outcome = QuestTerminalOutcomeKind.Unknown, string actor = "person.prototype.player", string arcDefinitionId = "")
        {
            return new NarrativeArcSignalRequest
            {
                transactionId = tx,
                arcDefinitionId = arcDefinitionId,
                category = category,
                signalId = tx,
                sourceId = string.IsNullOrWhiteSpace(sourceId) ? customState : sourceId,
                value = customState,
                questId = questId,
                questDefinitionId = questDefinitionId,
                questOutcomeKind = outcome,
                actorPersonId = actor,
                scopeKey = string.IsNullOrWhiteSpace(actor) ? PersistenceService.LocalWorldId : actor,
                conditionContext = new NarrativeConditionContextData
                {
                    actorPersonId = actor,
                    narrativeStateIds = string.IsNullOrWhiteSpace(sourceId) ? Array.Empty<string>() : new[] { sourceId },
                    customStateIds = string.IsNullOrWhiteSpace(customState) ? Array.Empty<string>() : new[] { customState },
                    worldTime = 2d
                },
                worldTime = 2d
            };
        }

        private static NarrativeStateTransitionRequest StateRequest(string transitionId, string tx, string actor, NarrativeStateScope scope = NarrativeStateScope.Person)
        {
            return new NarrativeStateTransitionRequest
            {
                transactionId = tx,
                transitionDefinitionId = transitionId,
                scope = scope,
                scopeKey = scope == NarrativeStateScope.World ? PersistenceService.LocalWorldId : actor,
                sourceKind = NarrativeTransitionSourceKind.Development,
                sourceId = "test",
                actorPersonId = actor,
                worldTime = 2d
            };
        }

        private static NarrativeArcStageDefinitionData CrossStage(string stageId, string arcId)
        {
            return new NarrativeArcStageDefinitionData
            {
                stageDefinitionId = stageId,
                initial = true,
                entryDependencies = new[]
                {
                    new NarrativeArcDependencyDefinitionData
                    {
                        dependencyDefinitionId = $"dependency.{NarrativeModelUtility.SanitizeForId(stageId)}",
                        kind = NarrativeArcDependencyKind.ArcCompleted,
                        requiredId = arcId
                    }
                }
            };
        }

        private static DefinitionRegistry Registry()
        {
            DefinitionRegistry registry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            registry = PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(registry);
            registry = PrototypeConversationDefinitionFactory.AddMissingPrototypeConversationDefinitions(registry);
            registry = PrototypeDialogueGraphDefinitionFactory.AddMissingPrototypeDialogueGraphDefinitions(registry);
            registry = PrototypeNarrativeEventDefinitionFactory.AddMissingPrototypeNarrativeEventDefinitions(registry);
            registry = PrototypeNarrativeStateDefinitionFactory.AddMissingPrototypeNarrativeStateDefinitions(registry);
            registry = PrototypeNarrativeArcDefinitionFactory.AddMissingPrototypeNarrativeArcDefinitions(registry);
            return registry;
        }
    }
}
