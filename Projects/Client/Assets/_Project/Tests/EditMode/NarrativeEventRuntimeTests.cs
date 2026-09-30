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
    public sealed class NarrativeEventRuntimeTests
    {
        [Test]
        public void PrototypeNarrativeEventsRegisterAndValidate()
        {
            DefinitionRegistry registry = Registry();
            Assert.That(PrototypeNarrativeEventDefinitionFactory.PrototypeDefinitionIds.All(id => registry.TryGet(id, out NarrativeEventDefinition _)), Is.True);

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (NarrativeEventDefinition definition in PrototypeNarrativeEventDefinitionFactory.CreateMissingNarrativeEventDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
        }

        [Test]
        public void LocationTriggerCreatesScopedEventAndDelegatesQuestCreationOnce()
        {
            DefinitionRegistry registry = Registry();
            QuestRuntime quests = new QuestRuntime(registry, PersistenceService.LocalWorldId);
            NarrativeEventRuntime runtime = Runtime(registry, quests);
            NarrativeTriggerRequest trigger = DungeonTrigger("tx.narrative.location");

            NarrativeEventOperationResult first = runtime.RouteTrigger(trigger);
            NarrativeEventOperationResult duplicate = runtime.RouteTrigger(DungeonTrigger("tx.narrative.location.duplicate"));

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(runtime.Count, Is.EqualTo(1));
            Assert.That(quests.Count, Is.EqualTo(1));
            NarrativeEventSnapshot snapshot = runtime.Query(new NarrativeEventQuery { definitionId = PrototypeNarrativeEventDefinitionFactory.DungeonEntryQuestDefinitionId }).Single();
            Assert.That(snapshot.Lifecycle, Is.EqualTo(NarrativeEventLifecycle.Resolved));
            Assert.That(snapshot.ActionExecutions.Select(action => action.actionExecutionId).Distinct().Count(), Is.EqualTo(snapshot.ActionExecutions.Count));
        }

        [Test]
        public void TruthKnowledgeAndBeliefConditionsRemainDistinct()
        {
            NarrativeConditionContextData context = new NarrativeConditionContextData
            {
                actorPersonId = "person.prototype.player",
                subjectId = "subject.prototype.hidden-dungeon",
                knownSubjectIds = new[] { "subject.prototype.hidden-dungeon" },
                authoritativeTruthIds = Array.Empty<string>(),
                beliefIds = Array.Empty<string>(),
                worldTime = 1d
            };
            DefinitionRegistry registry = RegistryWithConditionProbe();
            NarrativeEventRuntime runtime = Runtime(registry);

            NarrativeEventOperationResult failedTruth = runtime.EmitSignal(Signal("tx.condition.truth", "narrative-signal-definition.prototype.condition.truth", context));
            NarrativeEventOperationResult matchedKnowledge = runtime.EmitSignal(Signal("tx.condition.knowledge", "narrative-signal-definition.prototype.condition.knowledge", context));
            NarrativeEventOperationResult failedBelief = runtime.EmitSignal(Signal("tx.condition.belief", "narrative-signal-definition.prototype.condition.belief", context));

            Assert.That(failedTruth.Snapshots, Is.Empty);
            Assert.That(matchedKnowledge.Snapshots.Count, Is.EqualTo(1));
            Assert.That(failedBelief.Snapshots, Is.Empty);
            Assert.That(runtime.Query(new NarrativeEventQuery { definitionId = "narrative-event-definition.test.knowledge-condition" }).Single().Lifecycle, Is.EqualTo(NarrativeEventLifecycle.Resolved));
        }

        [Test]
        public void RequiredOwnerActionFailurePersistsFailedEventWithoutOwnerMutation()
        {
            DefinitionRegistry registry = Registry();
            NarrativeEventRuntime runtime = Runtime(registry);

            NarrativeEventOperationResult result = runtime.RouteTrigger(DungeonTrigger("tx.narrative.required-failure"));

            Assert.That(result.Status, Is.EqualTo(NarrativeOperationStatus.ActionFailed), result.Message);
            NarrativeEventSnapshot snapshot = runtime.Query(new NarrativeEventQuery { definitionId = PrototypeNarrativeEventDefinitionFactory.DungeonEntryQuestDefinitionId }).Single();
            Assert.That(snapshot.Lifecycle, Is.EqualTo(NarrativeEventLifecycle.Failed));
            Assert.That(snapshot.ActionExecutions.Any(action => action.category == NarrativeActionCategory.InstantiateQuest && action.lifecycle == NarrativeActionLifecycle.Failed), Is.True);
        }

        [Test]
        public void PersistenceRoundTripAndFailedPrepareLeaveRuntimeUnchanged()
        {
            DefinitionRegistry registry = Registry();
            NarrativeEventRuntime runtime = Runtime(registry);
            NarrativeEventOperationResult cascade = runtime.EmitSignal(Signal("tx.narrative.persist.cascade", PrototypeNarrativeEventDefinitionFactory.CascadeStartSignalId, Context()));
            Assert.That(cascade.Succeeded, Is.True, cascade.Message);

            NarrativeEventPersistenceParticipant participant = new NarrativeEventPersistenceParticipant(runtime, () => registry, () => new NarrativeEventRuntimeIntegrations());
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            Assert.That(save.Succeeded, Is.True, save.Message);

            NarrativeEventRuntime restored = Runtime(registry);
            NarrativeEventPersistenceParticipant restoredParticipant = new NarrativeEventPersistenceParticipant(restored, () => registry, () => new NarrativeEventRuntimeIntegrations());
            PersistenceParticipantPrepareResult prepare = restoredParticipant.PreparePayload(save.PayloadJson, NarrativeEventPersistenceParticipant.CurrentParticipantSchemaVersion);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(restoredParticipant.CommitPreparedPayload(prepare.PreparedPayload).Succeeded, Is.True);
            Assert.That(restored.Count, Is.EqualTo(runtime.Count));

            NarrativeEventRuntimeSaveData corrupt = restored.CreateSaveData();
            corrupt.events[0].eventDefinitionId = "narrative-event-definition.prototype.missing";
            int beforeReject = restored.Count;
            PersistenceParticipantPrepareResult rejected = restoredParticipant.PreparePayload(JsonUtility.ToJson(corrupt), NarrativeEventPersistenceParticipant.CurrentParticipantSchemaVersion);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.Count, Is.EqualTo(beforeReject));
        }

        private static DefinitionRegistry Registry()
        {
            return PrototypeNarrativeEventDefinitionFactory.AddMissingPrototypeNarrativeEventDefinitions(
                PrototypeDialogueGraphDefinitionFactory.AddMissingPrototypeDialogueGraphDefinitions(
                    PrototypeConversationDefinitionFactory.AddMissingPrototypeConversationDefinitions(
                        PrototypeQuestSourceDefinitionFactory.AddMissingPrototypeQuestSourceDefinitions(
                            PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()))))));
        }

        private static NarrativeEventRuntime Runtime(DefinitionRegistry registry, QuestRuntime quests = null)
        {
            return new NarrativeEventRuntime(registry, new NarrativeEventRuntimeIntegrations
            {
                QuestRuntime = quests,
                InformationGrantExecutor = target => !string.IsNullOrWhiteSpace(target),
                TravelConditionExecutor = target => !string.IsNullOrWhiteSpace(target),
                ConnectionChangeExecutor = target => !string.IsNullOrWhiteSpace(target),
                SocialActionExecutor = target => !string.IsNullOrWhiteSpace(target),
                OrganizationActionExecutor = target => !string.IsNullOrWhiteSpace(target),
                LegalActionExecutor = target => !string.IsNullOrWhiteSpace(target)
            }, PersistenceService.LocalWorldId);
        }

        private static NarrativeTriggerRequest DungeonTrigger(string transactionId)
        {
            return new NarrativeTriggerRequest
            {
                transactionId = transactionId,
                source = new NarrativeTriggerSourceData
                {
                    category = NarrativeTriggerCategory.LocationEntered,
                    sourceId = PrototypeNarrativeEventDefinitionFactory.DungeonEntrySignalId,
                    sourceTransactionId = "source.location.dungeon-entry.001",
                    actorPersonId = "person.prototype.player",
                    targetId = "location.prototype.dungeon-entry",
                    subjectId = "location.prototype.dungeon-entry",
                    committed = true,
                    worldTime = 10d
                },
                conditionContext = Context()
            };
        }

        private static NarrativeConditionContextData Context()
        {
            return new NarrativeConditionContextData
            {
                actorPersonId = "person.prototype.player",
                locationId = "location.prototype.dungeon-entry",
                organizationStateIds = new[] { "organization.prototype.adventurers-guild" },
                subjectId = "subject.prototype.cascade",
                worldTime = 10d
            };
        }

        private static NarrativeSignalRequest Signal(string transactionId, string signalId, NarrativeConditionContextData context)
        {
            return new NarrativeSignalRequest
            {
                transactionId = transactionId,
                signalDefinitionId = signalId,
                actorPersonId = "person.prototype.player",
                subjectIds = new[] { context.subjectId },
                conditionContext = context,
                worldTime = context.worldTime
            };
        }

        private static DefinitionRegistry RegistryWithConditionProbe()
        {
            DefinitionRegistry registry = Registry();
            IGameDefinition[] definitions = registry.DefinitionsById.Values.Concat(new IGameDefinition[]
            {
                Definition("narrative-event-definition.test.truth-condition", "narrative-signal-definition.prototype.condition.truth", NarrativeConditionCategory.AuthoritativeTruth),
                Definition("narrative-event-definition.test.knowledge-condition", "narrative-signal-definition.prototype.condition.knowledge", NarrativeConditionCategory.ActorKnowledge),
                Definition("narrative-event-definition.test.belief-condition", "narrative-signal-definition.prototype.condition.belief", NarrativeConditionCategory.Belief)
            }).ToArray();
            return new DefinitionRegistry(definitions);
        }

        private static NarrativeEventDefinition Definition(string definitionId, string signalId, NarrativeConditionCategory condition)
        {
            NarrativeEventDefinition definition = ScriptableObject.CreateInstance<NarrativeEventDefinition>();
            definition.DevelopmentConfigure(new NarrativeEventDefinitionData
            {
                eventDefinitionId = definitionId,
                displayName = definitionId,
                scope = NarrativeEventScope.OncePerPerson,
                scopeSelectorId = "actorPersonId",
                armingPolicy = NarrativeArmingPolicy.OnWorldInitialization,
                triggers = new[]
                {
                    new NarrativeTriggerDefinitionData { triggerDefinitionId = "trigger." + definitionId, category = NarrativeTriggerCategory.ExplicitSignal, requiredSourceId = signalId, requiredSubjectId = "subject.prototype.hidden-dungeon" }
                },
                conditions = new[]
                {
                    new NarrativeConditionDefinitionData { conditionDefinitionId = "condition." + definitionId, category = condition, requiredId = "subject.prototype.hidden-dungeon" }
                },
                actions = new[] { new NarrativeActionDefinitionData { actionDefinitionId = "action." + definitionId, category = NarrativeActionCategory.None } }
            });
            return definition;
        }
    }
}
