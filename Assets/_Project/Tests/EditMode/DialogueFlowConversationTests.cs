using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Tests
{
    public sealed class DialogueFlowConversationTests
    {
        [Test]
        public void PrototypeDialogueGraphsRegisterAndValidate()
        {
            DefinitionRegistry registry = Registry();
            Assert.That(PrototypeDialogueGraphDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out DialogueGraphDefinition _)), Is.True);
            Assert.That(registry.TryGet(PrototypeDialogueGraphDefinitionFactory.AdventurerGuildCounterGraphId, out DialogueGraphDefinition graph), Is.True);
            Assert.That(graph.ConversationDefinitionId, Is.EqualTo(PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId));

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (DialogueGraphDefinition definition in PrototypeDialogueGraphDefinitionFactory.CreateMissingDialogueGraphDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                UnityEngine.Object.DestroyImmediate(definition);
            }

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
        }

        [Test]
        public void SceneConversationBuilderSuppliesRequiredDeskRolesAndProviderContext()
        {
            DefinitionRegistry registry = Registry();
            MethodInfo builder = typeof(PrototypeNarrativeCoordinator).GetMethod(
                "BuildSceneConversationRequest",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(builder, Is.Not.Null);

            string[] definitionIds =
            {
                PrototypeConversationDefinitionFactory.RecordsDeskDefinitionId,
                PrototypeConversationDefinitionFactory.MayorDeskDefinitionId,
                PrototypeConversationDefinitionFactory.GuildHeadOfficeDefinitionId
            };
            for (int i = 0; i < definitionIds.Length; i++)
            {
                Assert.That(registry.TryGet(definitionIds[i], out ConversationDefinition definition), Is.True);
                ConversationStartRequest request = (ConversationStartRequest)builder.Invoke(null, new object[]
                {
                    definition,
                    definitionIds[i],
                    "person.prototype.player",
                    $"person.prototype.provider-{i}",
                    $"interaction-point.prototype.test-{i}",
                    $"location.prototype.test-{i}",
                    $"quest-source.prototype.test-{i}",
                    $"tx.test.scene-conversation.{i}",
                    $"conversation.test.scene-conversation.{i}",
                    $"prototype.scene.test-{i}",
                    1d
                });

                ConversationRuntime runtime = new ConversationRuntime(registry, PersistenceService.LocalWorldId);
                ConversationOperationResult result = runtime.StartConversation(request);

                Assert.That(result.Succeeded, Is.True, $"{definitionIds[i]}: {result.Message}");
                CollectionAssert.IsSubsetOf(
                    definition.RequiredRoles,
                    result.Snapshot.Participants.Select(value => value.role).ToArray());

                DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, runtime, null, PersistenceService.LocalWorldId);
                DialogueFlowOperationResult flow = flows.StartFlow(new DialogueFlowStartRequest
                {
                    transactionId = $"tx.test.scene-conversation.flow.{i}",
                    flowId = $"dialogue-flow.test.scene-conversation.{i}",
                    conversationId = result.Snapshot.ConversationId,
                    conditionContext = new DialogueConditionContext
                    {
                        actorPersonId = "person.prototype.player",
                        locationId = $"location.prototype.test-{i}",
                        interactionPointId = $"interaction-point.prototype.test-{i}"
                    },
                    worldTime = 1d
                });
                Assert.That(flow.Succeeded, Is.True, $"{definitionIds[i]} flow: {flow.Message}");
            }
        }

        [Test]
        public void StartFlowEntersCanonicalNodeAndShowsDeterministicChoices()
        {
            DefinitionRegistry registry = Registry();
            ConversationRuntime conversations = new ConversationRuntime(registry, PersistenceService.LocalWorldId);
            ConversationOperationResult conversation = StartGuildConversation(conversations);
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);

            DialogueFlowOperationResult start = flows.StartFlow(new DialogueFlowStartRequest
            {
                transactionId = "tx.test.dialogue.start",
                conversationId = conversation.Snapshot.ConversationId,
                conditionContext = Context(),
                worldTime = 1d
            });

            Assert.That(start.Succeeded, Is.True, start.Message);
            Assert.That(start.Snapshot.GraphId, Is.EqualTo(PrototypeDialogueGraphDefinitionFactory.AdventurerGuildCounterGraphId));
            Assert.That(start.Snapshot.CurrentNodeId, Is.EqualTo("guild.entry"));
            Assert.That(start.Snapshot.VisibleChoices.Select(choice => choice.ChoiceId), Is.EqualTo(new[] { "guild.choice.accept-posting", "guild.choice.ask-work", "guild.choice.leave" }));
        }

        [Test]
        public void ConditionsHideRestrictedChoicesUntilContextAllowsThem()
        {
            DefinitionRegistry registry = Registry();
            ConversationRuntime conversations = new ConversationRuntime(registry, PersistenceService.LocalWorldId);
            ConversationOperationResult conversation = StartGuildConversation(conversations);
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowOperationResult start = flows.StartFlow(new DialogueFlowStartRequest { transactionId = "tx.test.dialogue.conditions.start", conversationId = conversation.Snapshot.ConversationId, conditionContext = Context(), worldTime = 1d });

            Assert.That(start.Snapshot.VisibleChoices.Any(choice => choice.ChoiceId == "guild.choice.silver-rank"), Is.False);
            Assert.That(flows.TryGetSnapshot(start.Snapshot.FlowId, Context(rank: true), out DialogueFlowSnapshot ranked), Is.True);
            DialogueChoiceSnapshot silver = ranked.VisibleChoices.Single(choice => choice.ChoiceId == "guild.choice.silver-rank");
            Assert.That(silver.Evaluation.Selectable, Is.True);
        }

        [Test]
        public void ChoiceSelectionRecordsHistoryAndDoesNotMutateConversationRecord()
        {
            DefinitionRegistry registry = Registry();
            ConversationRuntime conversations = new ConversationRuntime(registry, PersistenceService.LocalWorldId);
            ConversationOperationResult conversation = StartGuildConversation(conversations);
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowOperationResult start = flows.StartFlow(new DialogueFlowStartRequest { transactionId = "tx.test.dialogue.choice.start", conversationId = conversation.Snapshot.ConversationId, conditionContext = Context(), worldTime = 1d });
            long conversationRevision = conversations.Revision;

            DialogueFlowOperationResult select = flows.SelectChoice(new DialogueChoiceSelectionRequest
            {
                transactionId = "tx.test.dialogue.choice.select",
                flowId = start.Snapshot.FlowId,
                choiceId = "guild.choice.ask-work",
                actorPersonId = "person.prototype.player",
                conditionContext = Context(),
                worldTime = 2d
            });
            DialogueFlowOperationResult duplicate = flows.SelectChoice(new DialogueChoiceSelectionRequest { transactionId = "tx.test.dialogue.choice.select" });

            Assert.That(select.Succeeded, Is.True, select.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(select.Snapshot.CurrentNodeId, Is.EqualTo("guild.entry"));
            Assert.That(select.Snapshot.Selections.Count, Is.EqualTo(1));
            Assert.That(select.Snapshot.LocalVariables.Any(value => value.variableId == "flag.guild.asked-work" && value.boolValue), Is.True);
            Assert.That(conversations.Revision, Is.EqualTo(conversationRevision));
        }

        [Test]
        public void EndingDialogueClosesConversationAndAllowsAnotherDeskConversation()
        {
            DefinitionRegistry registry = Registry();
            ConversationRuntime conversations = new ConversationRuntime(registry, PersistenceService.LocalWorldId);
            ConversationOperationResult firstConversation = StartGuildConversation(conversations);
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowOperationResult firstFlow = flows.StartFlow(new DialogueFlowStartRequest
            {
                transactionId = "tx.test.dialogue.lifecycle.start",
                conversationId = firstConversation.Snapshot.ConversationId,
                conditionContext = Context(),
                worldTime = 1d
            });

            DialogueFlowOperationResult ended = flows.SelectChoice(new DialogueChoiceSelectionRequest
            {
                transactionId = "tx.test.dialogue.lifecycle.leave",
                flowId = firstFlow.Snapshot.FlowId,
                choiceId = "guild.choice.leave",
                actorPersonId = "person.prototype.player",
                conditionContext = Context(),
                worldTime = 2d
            });
            Assert.That(ended.Succeeded, Is.True, ended.Message);
            Assert.That(ended.Snapshot.State, Is.EqualTo(DialogueFlowState.Ended));

            ConversationOperationResult completed = ConversationFlowLifecycle.CloseConversationForFlow(
                conversations,
                ended.Snapshot,
                ConversationLifecycleState.Completed,
                2d,
                "tx.test.dialogue.lifecycle.complete");
            Assert.That(completed.Succeeded, Is.True, completed.Message);
            Assert.That(completed.Snapshot.LifecycleState, Is.EqualTo(ConversationLifecycleState.Completed));

            ConversationOperationResult nextConversation = StartGuildConversation(
                conversations,
                "tx.test.dialogue.lifecycle.next",
                "conversation.test.dialogue.guild.next");
            Assert.That(nextConversation.Succeeded, Is.True, nextConversation.Message);
        }

        [Test]
        public void StartingNewDeskCanRecoverAnAbandonedOverlappingConversation()
        {
            DefinitionRegistry registry = Registry();
            ConversationRuntime conversations = new ConversationRuntime(registry, PersistenceService.LocalWorldId);
            ConversationOperationResult abandonedConversation = StartGuildConversation(conversations);
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowOperationResult abandonedFlow = flows.StartFlow(new DialogueFlowStartRequest
            {
                transactionId = "tx.test.dialogue.recovery.start",
                conversationId = abandonedConversation.Snapshot.ConversationId,
                conditionContext = Context(),
                worldTime = 1d
            });
            Assert.That(abandonedFlow.Succeeded, Is.True, abandonedFlow.Message);

            int recovered = ConversationFlowLifecycle.InterruptOpenConversationsForParticipant(
                conversations,
                flows,
                "person.prototype.player",
                2d,
                "tx.test.dialogue.recovery");

            Assert.That(recovered, Is.EqualTo(1));
            Assert.That(conversations.TryGetSnapshot(abandonedConversation.Snapshot.ConversationId, out ConversationSnapshot interrupted), Is.True);
            Assert.That(interrupted.LifecycleState, Is.EqualTo(ConversationLifecycleState.Interrupted));
            Assert.That(flows.TryGetSnapshot(abandonedFlow.Snapshot.FlowId, Context(), out DialogueFlowSnapshot endedFlow), Is.True);
            Assert.That(endedFlow.State, Is.EqualTo(DialogueFlowState.Ended));

            ConversationOperationResult nextConversation = StartGuildConversation(
                conversations,
                "tx.test.dialogue.recovery.next",
                "conversation.test.dialogue.guild.recovered");
            Assert.That(nextConversation.Succeeded, Is.True, nextConversation.Message);
        }

        [Test]
        public void FlowSnapshotsAndChoiceCollectionsAreImmutable()
        {
            DefinitionRegistry registry = Registry();
            ConversationRuntime conversations = new ConversationRuntime(registry, PersistenceService.LocalWorldId);
            ConversationOperationResult conversation = StartGuildConversation(conversations);
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowOperationResult start = flows.StartFlow(new DialogueFlowStartRequest { transactionId = "tx.test.dialogue.immutable.start", conversationId = conversation.Snapshot.ConversationId, conditionContext = Context(), worldTime = 1d });

            DialogueFlowRecordData mutated = start.Snapshot.ToSaveData();
            mutated.currentNodeId = "mutated";
            DialogueLocalVariableData[] variables = start.Snapshot.LocalVariables.ToArray();
            Array.Resize(ref variables, variables.Length + 1);
            variables[^1] = new DialogueLocalVariableData { variableId = "mutated", boolValue = true };

            Assert.That(flows.TryGetSnapshot(start.Snapshot.FlowId, Context(), out DialogueFlowSnapshot after), Is.True);
            Assert.That(after.CurrentNodeId, Is.EqualTo("guild.entry"));
            Assert.That(after.LocalVariables.Any(value => value.variableId == "mutated"), Is.False);
        }

        [Test]
        public void DialogueFlowPersistenceRoundTripAndFailedPrepareLeaveRuntimeUnchanged()
        {
            DefinitionRegistry registry = Registry();
            ConversationRuntime conversations = new ConversationRuntime(registry, PersistenceService.LocalWorldId);
            ConversationOperationResult conversation = StartGuildConversation(conversations);
            DialogueFlowRuntime flows = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowOperationResult start = flows.StartFlow(new DialogueFlowStartRequest { transactionId = "tx.test.dialogue.persistence.start", conversationId = conversation.Snapshot.ConversationId, conditionContext = Context(), worldTime = 1d });
            flows.SelectChoice(new DialogueChoiceSelectionRequest { transactionId = "tx.test.dialogue.persistence.choice", flowId = start.Snapshot.FlowId, choiceId = "guild.choice.ask-work", actorPersonId = "person.prototype.player", conditionContext = Context(), worldTime = 2d });
            DialogueFlowPersistenceParticipant participant = new DialogueFlowPersistenceParticipant(flows, () => registry, () => conversations);
            PersistenceParticipantSaveResult save = participant.CapturePayload();

            DialogueFlowRuntime restored = new DialogueFlowRuntime(registry, conversations, null, PersistenceService.LocalWorldId);
            DialogueFlowPersistenceParticipant restoredParticipant = new DialogueFlowPersistenceParticipant(restored, () => registry, () => conversations);
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, DialogueFlowPersistenceParticipant.CurrentParticipantSchemaVersion);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload).Succeeded, Is.True);
            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(restored.Events.Count, Is.EqualTo(flows.Events.Count));

            DialogueFlowRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.flows[0].graphId = "dialogue-graph.prototype.missing";
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), DialogueFlowPersistenceParticipant.CurrentParticipantSchemaVersion);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(restored.Events.Count, Is.EqualTo(flows.Events.Count));
        }

        private static DefinitionRegistry Registry()
        {
            DefinitionRegistry baseRegistry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            return PrototypeDialogueGraphDefinitionFactory.AddMissingPrototypeDialogueGraphDefinitions(PrototypeConversationDefinitionFactory.AddMissingPrototypeConversationDefinitions(PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(baseRegistry)));
        }

        private static ConversationOperationResult StartGuildConversation(
            ConversationRuntime conversations,
            string transactionId = "tx.test.dialogue.conversation.start",
            string conversationId = "conversation.test.dialogue.guild")
        {
            return conversations.StartConversation(new ConversationStartRequest
            {
                transactionId = transactionId,
                conversationId = conversationId,
                conversationDefinitionId = PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId,
                participants = new[]
                {
                    Participant("person.prototype.player", ConversationParticipantRole.Initiator),
                    Participant("person.prototype.guild-clerk", ConversationParticipantRole.Provider, organizationId: "organization.prototype.adventurers-guild"),
                    Participant("person.prototype.player", ConversationParticipantRole.QuestRecipient)
                },
                hostLocationId = "location.prototype.adventurers-guild",
                hostInteractionPointId = "interaction-point.prototype.adventurer-guild-counter",
                questId = "quest.prototype.guild.counter",
                questSourceId = "quest-source.prototype.guild-counter",
                questListingId = "quest-listing.prototype.guild-counter",
                operatingOrganizationId = "organization.prototype.adventurers-guild",
                worldTime = 1d
            });
        }

        private static ConversationParticipantRecordData Participant(string personId, ConversationParticipantRole role, string organizationId = "")
        {
            return new ConversationParticipantRecordData
            {
                personId = personId,
                role = role,
                currentLocationId = "location.prototype.adventurers-guild",
                currentInteractionPointId = "interaction-point.prototype.adventurer-guild-counter",
                representedOrganizationId = organizationId
            };
        }

        private static DialogueConditionContext Context(bool rank = false)
        {
            return new DialogueConditionContext
            {
                actorPersonId = "person.prototype.player",
                locationId = "location.prototype.adventurers-guild",
                interactionPointId = "interaction-point.prototype.adventurer-guild-counter",
                facts = new QuestEligibilityFactSet(
                    organizationMemberships: new[] { "organization.prototype.adventurers-guild" },
                    organizationRanks: rank ? new[] { "rank.prototype.adventurers.silver" } : Array.Empty<string>(),
                    authorityGrants: new[] { "authority.prototype.guild.quest-offer" },
                    knownSubjects: new[] { "subject.prototype.hidden-dungeon" }),
                activeQuestIds = new[] { "quest.prototype.guild.counter" },
                activeOfferIds = new[] { "offer.prototype.guild.counter" }
            };
        }
    }
}
