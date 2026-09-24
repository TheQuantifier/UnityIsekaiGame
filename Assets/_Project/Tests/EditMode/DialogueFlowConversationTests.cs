using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
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

        private static ConversationOperationResult StartGuildConversation(ConversationRuntime conversations)
        {
            return conversations.StartConversation(new ConversationStartRequest
            {
                transactionId = "tx.test.dialogue.conversation.start",
                conversationId = "conversation.test.dialogue.guild",
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
