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
    public sealed class NarrativeStateRuntimeTests
    {
        [Test]
        public void PrototypeNarrativeStateDefinitionsRegisterAndValidate()
        {
            DefinitionRegistry registry = Registry();
            Assert.That(PrototypeNarrativeStateDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out NarrativeStateDefinition _)), Is.True);

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (NarrativeStateDefinition definition in PrototypeNarrativeStateDefinitionFactory.CreateMissingNarrativeStateDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
        }

        [Test]
        public void PersonScopedBranchTransitionsAreExclusiveAndHistoryPreserving()
        {
            NarrativeStateRuntime runtime = new NarrativeStateRuntime(Registry());
            NarrativeStateTransitionResult preview = runtime.RequestTransition(Request(PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId, "tx.guild.preview", "person.prototype.player", preview: true));
            NarrativeStateTransitionResult guild = runtime.RequestTransition(Request(PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId, "tx.guild.choose", "person.prototype.player"));
            NarrativeStateTransitionResult duplicate = runtime.RequestTransition(Request(PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId, "tx.guild.choose", "person.prototype.player"));
            NarrativeStateTransitionResult merchant = runtime.RequestTransition(Request(PrototypeNarrativeStateDefinitionFactory.ChooseMerchantTransitionId, "tx.guild.merchant", "person.prototype.player"));
            NarrativeStateTransitionResult otherPerson = runtime.RequestTransition(Request(PrototypeNarrativeStateDefinitionFactory.ChooseMerchantTransitionId, "tx.guild.merchant.other", "person.prototype.other"));

            Assert.That(preview.Preview, Is.True);
            Assert.That(guild.Succeeded, Is.True, guild.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(merchant.Status, Is.EqualTo(NarrativeStateTransitionStatus.SourceValueMismatch));
            Assert.That(otherPerson.Succeeded, Is.True, otherPerson.Message);
            Assert.That(runtime.TransitionCount, Is.EqualTo(2));
            Assert.That(runtime.TryGetSnapshot(PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyDefinitionId, NarrativeStateScope.Person, "person.prototype.player", out NarrativeStateSnapshot player), Is.True);
            Assert.That(player.TryGetValue(PrototypeNarrativeStateDefinitionFactory.GuildLoyaltyVariableId, out NarrativeVariableValueData value), Is.True);
            Assert.That(value.tokenValue, Is.EqualTo(PrototypeNarrativeStateDefinitionFactory.GuildLoyalValueId));
        }

        [Test]
        public void BranchMergeAndTerminalStateKeepHistoricalValues()
        {
            NarrativeStateRuntime runtime = new NarrativeStateRuntime(Registry());
            Assert.That(runtime.RequestTransition(Request(PrototypeNarrativeStateDefinitionFactory.SupportHeirTransitionId, "tx.royal.heir", string.Empty, NarrativeStateScope.World)).Succeeded, Is.True);
            Assert.That(runtime.RequestTransition(Request(PrototypeNarrativeStateDefinitionFactory.ReconcileSuccessionTransitionId, "tx.royal.merge", string.Empty, NarrativeStateScope.World)).Succeeded, Is.True);
            Assert.That(runtime.RequestTransition(Request(PrototypeNarrativeStateDefinitionFactory.CrownHeirTransitionId, "tx.royal.crown", string.Empty, NarrativeStateScope.World)).Succeeded, Is.True);
            NarrativeStateTransitionResult afterTerminal = runtime.RequestTransition(Request(PrototypeNarrativeStateDefinitionFactory.SupportRivalTransitionId, "tx.royal.rival", string.Empty, NarrativeStateScope.World));

            Assert.That(afterTerminal.Status, Is.EqualTo(NarrativeStateTransitionStatus.SourceValueMismatch).Or.EqualTo(NarrativeStateTransitionStatus.TerminalState));
            Assert.That(runtime.ValueAt(PrototypeNarrativeStateDefinitionFactory.RoyalSuccessionDefinitionId, PrototypeNarrativeStateDefinitionFactory.RoyalBranchVariableId, NarrativeStateScope.World, PersistenceService.LocalWorldId, 1d).tokenValue, Is.EqualTo(PrototypeNarrativeStateDefinitionFactory.RoyalSupportHeirValueId));
            Assert.That(runtime.ValueAt(PrototypeNarrativeStateDefinitionFactory.RoyalSuccessionDefinitionId, PrototypeNarrativeStateDefinitionFactory.RoyalBranchVariableId, NarrativeStateScope.World, PersistenceService.LocalWorldId, 2d).tokenValue, Is.EqualTo(PrototypeNarrativeStateDefinitionFactory.RoyalReconciledValueId));
            Assert.That(runtime.ValueAt(PrototypeNarrativeStateDefinitionFactory.RoyalSuccessionDefinitionId, PrototypeNarrativeStateDefinitionFactory.RoyalBranchVariableId, NarrativeStateScope.World, PersistenceService.LocalWorldId, 3d).tokenValue, Is.EqualTo(PrototypeNarrativeStateDefinitionFactory.RoyalTerminalValueId));
        }

        [Test]
        public void DialogueChoiceAndNarrativeEventRequestTransitionsThroughOwnerRuntime()
        {
            DefinitionRegistry registry = Registry();
            NarrativeStateRuntime states = new NarrativeStateRuntime(registry);
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, null, new NarrativeStateDialogueExecutor(states));
            DialogueEffectExecutionResult dialogue = new NarrativeStateDialogueExecutor(states).Execute(new DialogueEffectExecutionRequest
            {
                flowId = "flow.test",
                conversationId = "conversation.test",
                actorPersonId = "person.prototype.player",
                effect = new DialogueEffectData { effectId = "effect.test.guild", kind = DialogueEffectKind.RequestNarrativeStateTransition, targetId = PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId },
                worldTime = 4d
            });

            NarrativeEventRuntime events = new NarrativeEventRuntime(registry, new NarrativeEventRuntimeIntegrations
            {
                NarrativeStateTransitionExecutor = states.RequestTransition
            });
            NarrativeEventOperationResult eventAction = events.EmitSignal(new NarrativeSignalRequest
            {
                transactionId = "tx.state.event",
                signalDefinitionId = "narrative-signal-definition.test.state-action",
                actorPersonId = "person.prototype.other",
                worldTime = 5d,
                conditionContext = new NarrativeConditionContextData { actorPersonId = "person.prototype.other", narrativeStateIds = new[] { "state.test" } }
            });

            Assert.That(dialogue.Succeeded, Is.True, dialogue.Message);
            Assert.That(flows.Count, Is.Zero);
            Assert.That(eventAction.Succeeded, Is.True, eventAction.Message);
            Assert.That(states.TransitionCount, Is.EqualTo(2));
        }

        [Test]
        public void HiddenStateRedactsProjectionWithoutGrantingKnowledge()
        {
            NarrativeStateRuntime runtime = new NarrativeStateRuntime(Registry());
            NarrativeStateTransitionResult opened = runtime.RequestTransition(Request(PrototypeNarrativeStateDefinitionFactory.OpenInvestigationTransitionId, "tx.mayor.open", string.Empty, NarrativeStateScope.World));

            Assert.That(opened.Succeeded, Is.True, opened.Message);
            Assert.That(runtime.TryGetSnapshot(PrototypeNarrativeStateDefinitionFactory.MayorInvestigationDefinitionId, NarrativeStateScope.World, PersistenceService.LocalWorldId, out NarrativeStateSnapshot development), Is.True);
            Assert.That(runtime.TryGetSnapshot(PrototypeNarrativeStateDefinitionFactory.MayorInvestigationDefinitionId, NarrativeStateScope.World, PersistenceService.LocalWorldId, out NarrativeStateSnapshot publicView, developmentView: false), Is.True);
            Assert.That(development.IsHidden, Is.True);
            Assert.That(development.Variables.Count, Is.GreaterThan(0));
            Assert.That(publicView.Variables.Count, Is.Zero);
        }

        [Test]
        public void SaveRestorePreservesStateAndHistoryWithoutReplayingConsequences()
        {
            DefinitionRegistry registry = Registry();
            int signalCount = 0;
            NarrativeStateRuntime runtime = new NarrativeStateRuntime(registry, new NarrativeStateRuntimeIntegrations
            {
                ConsequenceExecutor = (_, _) => (++signalCount).ToString()
            });
            NarrativeStateTransitionResult transition = runtime.RequestTransition(Request(PrototypeNarrativeStateDefinitionFactory.ChooseGuildTransitionId, "tx.persist.guild", "person.prototype.player"));
            NarrativeStatePersistenceParticipant participant = new NarrativeStatePersistenceParticipant(runtime, () => registry);
            PersistenceParticipantSaveResult save = participant.CapturePayload();

            NarrativeStateRuntime restored = new NarrativeStateRuntime(registry, new NarrativeStateRuntimeIntegrations
            {
                ConsequenceExecutor = (_, _) => (++signalCount).ToString()
            });
            NarrativeStatePersistenceParticipant restoredParticipant = new NarrativeStatePersistenceParticipant(restored, () => registry);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, NarrativeStatePersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload);
            NarrativeStateRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.states[0].variables[0].value = NarrativeVariableValueData.Integer(42);
            int beforeReject = restored.TransitionCount;
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), NarrativeStatePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(transition.Succeeded, Is.True, transition.Message);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.TransitionCount, Is.EqualTo(beforeReject));
            Assert.That(signalCount, Is.Zero);
        }

        private static NarrativeStateTransitionRequest Request(string transitionId, string tx, string actor, NarrativeStateScope scope = NarrativeStateScope.Person, bool preview = false)
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
                worldTime = tx.Contains("merge") ? 2d : tx.Contains("crown") ? 3d : 1d,
                preview = preview
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
            registry = AddStateActionProbe(registry);
            return registry;
        }

        private static DefinitionRegistry AddStateActionProbe(DefinitionRegistry baseRegistry)
        {
            var definitions = baseRegistry.DefinitionsById.Values.ToList();
            NarrativeEventDefinition definition = ScriptableObject.CreateInstance<NarrativeEventDefinition>();
            definition.DevelopmentConfigure(new NarrativeEventDefinitionData
            {
                eventDefinitionId = "narrative-event-definition.test.state-action",
                displayName = "State Action Probe",
                category = NarrativeEventCategory.Scripted,
                scope = NarrativeEventScope.OncePerPerson,
                armingPolicy = NarrativeArmingPolicy.OnWorldInitialization,
                triggers = new[] { new NarrativeTriggerDefinitionData { triggerDefinitionId = "trigger.test.state-action", category = NarrativeTriggerCategory.ExplicitSignal, requiredSourceId = "narrative-signal-definition.test.state-action" } },
                actions = new[] { new NarrativeActionDefinitionData { actionDefinitionId = "action.test.state-transition", category = NarrativeActionCategory.RequestNarrativeStateTransition, targetId = PrototypeNarrativeStateDefinitionFactory.ChooseMerchantTransitionId } }
            });
            definitions.Add(definition);
            return new DefinitionRegistry(definitions);
        }

        private sealed class NarrativeStateDialogueExecutor : IDialogueEffectExecutor
        {
            private readonly NarrativeStateRuntime runtime;

            public NarrativeStateDialogueExecutor(NarrativeStateRuntime runtime)
            {
                this.runtime = runtime;
            }

            public DialogueEffectExecutionResult Execute(DialogueEffectExecutionRequest request)
            {
                if (request.effect.kind != DialogueEffectKind.RequestNarrativeStateTransition) return DialogueEffectExecutionResult.Success("test", string.Empty);
                NarrativeStateTransitionResult result = runtime.RequestTransition(new NarrativeStateTransitionRequest
                {
                    transactionId = $"dialogue.{request.flowId}.{request.effect.effectId}",
                    transitionDefinitionId = request.effect.targetId,
                    sourceKind = NarrativeTransitionSourceKind.DialogueChoice,
                    sourceId = request.choiceId,
                    actorPersonId = request.actorPersonId,
                    conversationId = request.conversationId,
                    worldTime = request.worldTime,
                    preview = request.preview
                });
                return result.Succeeded ? DialogueEffectExecutionResult.Success("NarrativeStateRuntime", result.Transition?.TransitionId ?? string.Empty, result.Duplicate) : DialogueEffectExecutionResult.Failure(result.Message);
            }
        }
    }
}
