using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Tests
{
    public sealed class Step15NarrativePersistenceHistoricalTests
    {
        [Test]
        public void OwnershipMap_DeclaresSingleAuthoritativeOwnerPerStep15Category()
        {
            Step15NarrativeHistoricalService service = new Step15NarrativeHistoricalService();
            Step15NarrativeOwnershipEntry[] ownership = service.OwnershipMap.ToArray();

            Assert.That(ownership, Has.Length.GreaterThanOrEqualTo(13));
            Assert.That(ownership.Where(entry => !entry.Derived).Select(entry => entry.Category), Is.Unique);
            Assert.That(ownership.Where(entry => !entry.Derived).All(entry => !string.IsNullOrWhiteSpace(entry.AuthoritativeOwner) && !string.IsNullOrWhiteSpace(entry.ParticipantKey)), Is.True);
            Assert.That(ownership.Where(entry => entry.Derived).All(entry => string.IsNullOrWhiteSpace(entry.ParticipantKey)), Is.True);
            Assert.That(ownership.Any(entry => entry.Category == "Scene bindings" && entry.Derived), Is.True);
        }

        [Test]
        public void Manifest_RecordsSchemasCountsRestorePhasesAndStableFingerprint()
        {
            Step15NarrativeHistoricalService service = new Step15NarrativeHistoricalService();
            Step15NarrativePersistenceSnapshot snapshot = SampleSnapshot();

            Step15NarrativePersistenceManifest first = service.BuildManifest(snapshot, Step15NarrativeReadinessState.Ready);
            Step15NarrativePersistenceManifest second = service.BuildManifest(snapshot.Clone(), Step15NarrativeReadinessState.Ready);

            Assert.That(first.Readiness, Is.EqualTo(Step15NarrativeReadinessState.Ready));
            Assert.That(first.RestorePhases, Is.EqualTo(Enum.GetValues(typeof(Step15NarrativeRestorePhase)).Cast<Step15NarrativeRestorePhase>()));
            Assert.That(first.ParticipantSchemaVersions.Values.All(value => value == 1), Is.True);
            Assert.That(first.RecordCounts["quests"], Is.EqualTo(1));
            Assert.That(first.RecordCounts["conversations"], Is.EqualTo(1));
            Assert.That(first.RecordCounts["narrativeArcs"], Is.EqualTo(1));
            Assert.That(first.DeterministicFingerprint, Is.EqualTo(second.DeterministicFingerprint));
        }

        [Test]
        public void Validation_CatchesCrossRuntimeReferenceDriftWithoutMutatingSnapshot()
        {
            Step15NarrativeHistoricalService service = new Step15NarrativeHistoricalService();
            Step15NarrativePersistenceSnapshot valid = SampleSnapshot();
            Step15NarrativePersistenceSnapshot corrupt = valid.Clone();
            corrupt.Participation.offers[0].questId = "quest.prototype.missing";

            Step15NarrativeValidationReport report = service.Validate(corrupt);

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Errors.Any(error => error.Contains("references missing quest", StringComparison.Ordinal)), Is.True);
            Assert.That(valid.Participation.offers[0].questId, Is.EqualTo("quest.prototype.guild-posting"));
            Assert.That(service.Validate(valid).Succeeded, Is.True);
        }

        [Test]
        public void HistoricalQuestAndPersonSnapshots_ReconstructStateWithoutReplay()
        {
            Step15NarrativeHistoricalService service = new Step15NarrativeHistoricalService();
            Step15NarrativePersistenceSnapshot snapshot = SampleSnapshot();

            HistoricalQuestSnapshot quest = service.GetQuestAt(snapshot, "quest.prototype.guild-posting", 12d);
            HistoricalPersonQuestSnapshot person = service.GetPersonQuestSnapshotAt(snapshot, "person.prototype.hero", 12d);

            Assert.That(quest.Existed, Is.True);
            Assert.That(quest.Lifecycle, Is.EqualTo(QuestRuntimeLifecycleState.Retired));
            Assert.That(quest.Offers.Single().State, Is.EqualTo(QuestOfferLifecycleState.Accepted));
            Assert.That(quest.Assignments.Single().State, Is.EqualTo(QuestAssignmentLifecycleState.Active));
            Assert.That(quest.Objectives.Single().Satisfied, Is.True);
            Assert.That(quest.Outcome, Is.EqualTo(QuestTerminalOutcomeKind.Completed));
            Assert.That(quest.Rewards.Single().State, Is.EqualTo(QuestRewardEntitlementState.Claimable));
            Assert.That(quest.ActiveListingIds, Is.Empty);
            Assert.That(person.CompletedQuestIds, Is.EqualTo(new[] { "quest.prototype.guild-posting" }));
            Assert.That(person.ClaimableRewardIds, Is.EqualTo(new[] { "reward.prototype.guild-posting.coin" }));
        }

        [Test]
        public void Timeline_IsDeterministicPagedAndDoesNotLeakHiddenEntries()
        {
            Step15NarrativeHistoricalService service = new Step15NarrativeHistoricalService();
            Step15NarrativePersistenceSnapshot snapshot = SampleSnapshot();

            NarrativeTimelinePage publicPage = service.QueryTimeline(snapshot, new NarrativeTimelineQuery
            {
                AccessMode = NarrativeHistoricalAccessMode.PersonSafe,
                RequesterPersonId = "person.prototype.hero",
                Limit = 500
            });
            NarrativeTimelinePage developmentFirst = service.QueryTimeline(snapshot, new NarrativeTimelineQuery { AccessMode = NarrativeHistoricalAccessMode.Development, Limit = 3 });
            NarrativeTimelinePage developmentSecond = service.QueryTimeline(snapshot, new NarrativeTimelineQuery { AccessMode = NarrativeHistoricalAccessMode.Development, Limit = 3, AfterCursor = developmentFirst.NextCursor });

            Assert.That(publicPage.Entries.Any(entry => entry.Hidden), Is.False);
            Assert.That(publicPage.Entries.Any(entry => entry.NarrativeEventId == "narrative-event.prototype.hidden"), Is.False);
            Assert.That(developmentFirst.HasMore, Is.True);
            Assert.That(developmentFirst.Entries.Count, Is.EqualTo(3));
            Assert.That(developmentSecond.Entries.First().Cursor, Is.GreaterThan(developmentFirst.NextCursor));
            Assert.That(service.QueryTimeline(snapshot.Clone(), new NarrativeTimelineQuery { AccessMode = NarrativeHistoricalAccessMode.Development, Limit = 500 }).Entries.Select(entry => entry.Cursor),
                Is.EqualTo(service.QueryTimeline(snapshot, new NarrativeTimelineQuery { AccessMode = NarrativeHistoricalAccessMode.Development, Limit = 500 }).Entries.Select(entry => entry.Cursor)));
        }

        [Test]
        public void ConversationStateAndNarrativeArcSnapshots_ReconstructHistoricalViews()
        {
            Step15NarrativeHistoricalService service = new Step15NarrativeHistoricalService();
            Step15NarrativePersistenceSnapshot snapshot = SampleSnapshot();

            HistoricalConversationSnapshot conversation = service.GetConversationAt(snapshot, "conversation.prototype.guild-counter", 8d);
            HistoricalNarrativeStateSnapshot state = service.GetNarrativeStateAt(snapshot, "narrative-state.prototype.guild", 11d);
            HistoricalNarrativeArcSnapshot arc = service.GetNarrativeArcAt(snapshot, "narrative-arc.prototype.guild-intro", 13d);

            Assert.That(conversation.Existed, Is.True);
            Assert.That(conversation.ActiveDialogueNodeId, Is.EqualTo("node.report"));
            Assert.That(conversation.LatestChoiceId, Is.EqualTo("choice.accept"));
            Assert.That(conversation.ParticipantPersonIds, Is.EqualTo(new[] { "person.prototype.guild-clerk", "person.prototype.hero" }));
            Assert.That(state.VariableValues["guild_intro_stage"], Is.EqualTo("reported"));
            Assert.That(arc.Lifecycle, Is.EqualTo(NarrativeArcLifecycle.Completed));
            Assert.That(arc.CompletedStageDefinitionIds, Is.EqualTo(new[] { "stage.accept", "stage.report" }));
            Assert.That(arc.BoundQuestIds, Is.EqualTo(new[] { "quest.prototype.guild-posting" }));
        }

        [Test]
        public void RecoveryDiagnostics_ReportRecoverableDerivedGapsAndHardCorruption()
        {
            Step15NarrativeHistoricalService service = new Step15NarrativeHistoricalService();
            Step15NarrativePersistenceSnapshot snapshot = SampleSnapshot();
            snapshot.DialogueFlows.flows[0].currentNodeId = "node.not-visited";
            snapshot.NarrativeArcs.arcs.Add(snapshot.NarrativeArcs.arcs[0].Clone());

            Step15NarrativeValidationReport report = service.Validate(snapshot);

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.RecoveryIssues.Any(issue => issue.Kind == NarrativeRecoveryIssueKind.StaleDerivedIndex && issue.Recoverable), Is.True);
            Assert.That(report.RecoveryIssues.Any(issue => issue.Kind == NarrativeRecoveryIssueKind.AuthoritativeCorruption && !issue.Recoverable), Is.True);
        }

        internal static Step15NarrativePersistenceSnapshot SampleSnapshot()
        {
            const string World = PersistenceService.LocalWorldId;
            const string QuestId = "quest.prototype.guild-posting";
            const string OfferId = "offer.prototype.guild-posting";
            const string AssignmentId = "assignment.prototype.guild-posting";
            const string ObjectiveId = "objective.prototype.guild-report";
            const string PersonId = "person.prototype.hero";
            const string SourceId = "quest-source.prototype.guild-board";
            const string ListingId = "quest-listing.prototype.guild-posting";
            const string ConversationId = "conversation.prototype.guild-counter";
            const string FlowId = "dialogue-flow.prototype.guild-counter";
            const string ArcId = "narrative-arc.prototype.guild-intro";

            return new Step15NarrativePersistenceSnapshot
            {
                WorldId = World,
                SaveSlotId = "slot.prototype.step15",
                SaveWorldTime = 20d,
                Quests = new QuestRuntimeSaveData
                {
                    schemaVersion = QuestRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    quests = new List<QuestRecordData>
                    {
                        new QuestRecordData
                        {
                            questId = QuestId,
                            questDefinitionId = "quest-definition.prototype.guild-posting",
                            worldId = World,
                            lifecycleState = QuestRuntimeLifecycleState.Available,
                            issuer = new QuestIssuerReferenceData { issuerType = QuestIssuerType.Organization, issuerId = "organization.prototype.adventurers-guild" },
                            intendedRecipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = PersonId },
                            origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.QuestBoard, locationId = "location.prototype.guild", interactionPointId = "interaction-point.prototype.guild-board" },
                            subjectLinks = new[] { new QuestSubjectLinkData { role = QuestSubjectRole.Location, subject = new InformationSubjectReferenceData { subjectType = InformationSubjectType.Location, subjectId = "location.prototype.dungeon" } } },
                            visibility = QuestVisibility.Public,
                            createdWorldTime = 1d,
                            revision = 2
                        }
                    },
                    events = new List<QuestRuntimeEventData>
                    {
                        new QuestRuntimeEventData { eventId = "quest-event.001", questId = QuestId, eventKind = QuestRuntimeEventKind.Instantiated, afterState = QuestRuntimeLifecycleState.Available, worldTime = 1d, runtimeRevision = 1 },
                        new QuestRuntimeEventData { eventId = "quest-event.002", questId = QuestId, eventKind = QuestRuntimeEventKind.LifecycleChanged, beforeState = QuestRuntimeLifecycleState.Available, afterState = QuestRuntimeLifecycleState.Retired, worldTime = 10d, runtimeRevision = 2 }
                    }
                },
                Participation = new QuestParticipationRuntimeSaveData
                {
                    schemaVersion = QuestParticipationRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    offers = new List<QuestOfferRecordData>
                    {
                        new QuestOfferRecordData { offerId = OfferId, questId = QuestId, worldId = World, recipient = new QuestRecipientReferenceData { recipientScope = QuestRecipientScope.Person, recipientId = PersonId }, lifecycleState = QuestOfferLifecycleState.Active, createdWorldTime = 2d, visibility = QuestVisibility.Public }
                    },
                    assignments = new List<QuestAssignmentRecordData>
                    {
                        new QuestAssignmentRecordData { assignmentId = AssignmentId, offerId = OfferId, questId = QuestId, worldId = World, assigneePersonId = PersonId, lifecycleState = QuestAssignmentLifecycleState.Active, assignedWorldTime = 3d, visibility = QuestVisibility.Public }
                    },
                    events = new List<QuestParticipationEventData>
                    {
                        new QuestParticipationEventData { eventId = "participation-event.001", eventKind = QuestParticipationEventKind.OfferCreated, offerId = OfferId, questId = QuestId, personId = PersonId, worldTime = 2d, runtimeRevision = 1 },
                        new QuestParticipationEventData { eventId = "participation-event.002", eventKind = QuestParticipationEventKind.OfferAccepted, offerId = OfferId, assignmentId = AssignmentId, questId = QuestId, personId = PersonId, worldTime = 3d, runtimeRevision = 2 },
                        new QuestParticipationEventData { eventId = "participation-event.003", eventKind = QuestParticipationEventKind.AssignmentCreated, assignmentId = AssignmentId, questId = QuestId, personId = PersonId, worldTime = 3d, runtimeRevision = 2 }
                    }
                },
                Objectives = new QuestObjectiveProgressRuntimeSaveData
                {
                    schemaVersion = QuestObjectiveProgressRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    objectives = new List<QuestObjectiveRecordData>
                    {
                        new QuestObjectiveRecordData { objectiveId = ObjectiveId, objectiveDefinitionId = "objective-definition.prototype.report", questId = QuestId, assignmentId = AssignmentId, assigneePersonId = PersonId, worldId = World, lifecycleState = QuestObjectiveLifecycleState.Active, visibility = QuestObjectiveVisibility.Public, currentValue = 1, targetValue = 1, satisfied = false, activatedWorldTime = 4d, satisfiedWorldTime = -1d }
                    },
                    events = new List<QuestObjectiveRuntimeEventData>
                    {
                        new QuestObjectiveRuntimeEventData { eventId = "objective-event.001", objectiveId = ObjectiveId, questId = QuestId, assignmentId = AssignmentId, eventKind = QuestObjectiveEventKind.ObjectiveActivated, beforeState = QuestObjectiveLifecycleState.Locked, afterState = QuestObjectiveLifecycleState.Active, worldTime = 4d, runtimeRevision = 1 },
                        new QuestObjectiveRuntimeEventData { eventId = "objective-event.002", objectiveId = ObjectiveId, questId = QuestId, assignmentId = AssignmentId, eventKind = QuestObjectiveEventKind.ObjectiveSatisfied, beforeValue = 0, afterValue = 1, beforeState = QuestObjectiveLifecycleState.Active, afterState = QuestObjectiveLifecycleState.Satisfied, worldTime = 9d, runtimeRevision = 2 }
                    }
                },
                Outcomes = new QuestOutcomeRuntimeSaveData
                {
                    schemaVersion = QuestOutcomeRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    terminalOutcomes = new List<QuestTerminalOutcomeRecordData>
                    {
                        new QuestTerminalOutcomeRecordData { outcomeId = "outcome.prototype.guild-posting", terminalOutcomeId = "terminal-outcome.prototype.guild-posting", questId = QuestId, assignmentId = AssignmentId, worldId = World, outcomeKind = QuestTerminalOutcomeKind.Completed, actorPersonId = PersonId, worldTime = 10d }
                    },
                    rewardEntitlements = new List<QuestRewardEntitlementRecordData>
                    {
                        new QuestRewardEntitlementRecordData { entitlementId = "reward.prototype.guild-posting.coin", terminalOutcomeId = "terminal-outcome.prototype.guild-posting", questId = QuestId, assignmentId = AssignmentId, recipientPersonId = PersonId, worldId = World, category = QuestRewardCategory.Currency, targetDefinitionId = "currency.prototype.coin", quantity = 25, state = QuestRewardEntitlementState.Claimable, createdWorldTime = 10d }
                    },
                    events = new List<QuestOutcomeEventData>
                    {
                        new QuestOutcomeEventData { eventId = "outcome-event.001", eventKind = QuestOutcomeEventKind.TerminalOutcomeRecorded, questId = QuestId, assignmentId = AssignmentId, worldTime = 10d, runtimeRevision = 1 },
                        new QuestOutcomeEventData { eventId = "outcome-event.002", eventKind = QuestOutcomeEventKind.RewardEntitlementCreated, questId = QuestId, assignmentId = AssignmentId, rewardEntitlementId = "reward.prototype.guild-posting.coin", worldTime = 10d, runtimeRevision = 2 }
                    }
                },
                Sources = new QuestSourceRuntimeSaveData
                {
                    schemaVersion = QuestSourceRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    sources = new List<QuestSourceRecordData>
                    {
                        new QuestSourceRecordData { questSourceId = SourceId, questSourceDefinitionId = "quest-source-definition.prototype.guild-board", worldId = World, visibility = QuestSourceVisibility.Public, createdWorldTime = 1d }
                    },
                    listings = new List<QuestListingRecordData>
                    {
                        new QuestListingRecordData { questListingId = ListingId, questId = QuestId, questSourceId = SourceId, worldId = World, visibility = QuestSourceVisibility.Public, lifecycleState = QuestListingLifecycleState.Claimed, publishedWorldTime = 2d, endedWorldTime = 3d }
                    },
                    events = new List<QuestSourceEventData>
                    {
                        new QuestSourceEventData { eventId = "source-event.001", questSourceId = SourceId, questListingId = ListingId, questId = QuestId, eventKind = QuestSourceEventKind.ListingPublished, worldTime = 2d, runtimeRevision = 1 }
                    }
                },
                Conversations = new ConversationRuntimeSaveData
                {
                    schemaVersion = ConversationRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 1,
                    conversations = new List<ConversationRecordData>
                    {
                        new ConversationRecordData
                        {
                            conversationId = ConversationId,
                            conversationDefinitionId = "conversation-definition.prototype.guild-counter",
                            worldId = World,
                            lifecycleState = ConversationLifecycleState.Active,
                            visibility = ConversationVisibility.Public,
                            participants = new[]
                            {
                                new ConversationParticipantRecordData { participantId = "participant.hero", personId = PersonId, role = ConversationParticipantRole.Initiator },
                                new ConversationParticipantRecordData { participantId = "participant.clerk", personId = "person.prototype.guild-clerk", role = ConversationParticipantRole.Provider }
                            },
                            questId = QuestId,
                            questSourceId = SourceId,
                            questListingId = ListingId,
                            startedWorldTime = 5d
                        }
                    },
                    events = new List<ConversationEventData>
                    {
                        new ConversationEventData { eventId = "conversation-event.001", conversationId = ConversationId, personId = PersonId, eventKind = ConversationEventKind.ConversationStarted, afterState = ConversationLifecycleState.Active, worldTime = 5d, runtimeRevision = 1 }
                    }
                },
                DialogueFlows = new DialogueFlowRuntimeSaveData
                {
                    schemaVersion = DialogueFlowRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    flows = new List<DialogueFlowRecordData>
                    {
                        new DialogueFlowRecordData
                        {
                            flowId = FlowId,
                            conversationId = ConversationId,
                            graphId = "dialogue-graph.prototype.guild-counter",
                            worldId = World,
                            state = DialogueFlowState.AwaitingChoice,
                            currentNodeId = "node.report",
                            currentVisitId = "visit.002",
                            visits = new[]
                            {
                                new DialogueNodeVisitRecordData { visitId = "visit.001", conversationId = ConversationId, graphId = "dialogue-graph.prototype.guild-counter", nodeId = "node.start", speakerPersonId = "person.prototype.guild-clerk", enteredWorldTime = 5d, exitedWorldTime = 7d, selectedChoiceId = "choice.accept", sequence = 1 },
                                new DialogueNodeVisitRecordData { visitId = "visit.002", conversationId = ConversationId, graphId = "dialogue-graph.prototype.guild-counter", nodeId = "node.report", speakerPersonId = PersonId, enteredWorldTime = 7d, exitedWorldTime = -1d, sequence = 2 }
                            },
                            selections = new[]
                            {
                                new DialogueChoiceSelectionRecordData { selectionId = "selection.001", conversationId = ConversationId, graphId = "dialogue-graph.prototype.guild-counter", nodeId = "node.start", choiceId = "choice.accept", actorPersonId = PersonId, targetNodeId = "node.report", worldTime = 7d, runtimeRevision = 2 }
                            }
                        }
                    }
                },
                NarrativeEvents = new NarrativeEventRuntimeSaveData
                {
                    schemaVersion = NarrativeEventRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    events = new List<NarrativeEventRecordData>
                    {
                        new NarrativeEventRecordData { narrativeEventId = "narrative-event.prototype.reported", eventDefinitionId = "narrative-event-definition.prototype.reported", worldId = World, lifecycle = NarrativeEventLifecycle.Resolved, actorPersonId = PersonId, questId = QuestId, conversationId = ConversationId, triggerTime = 8d, visibility = NarrativeEventVisibility.Public, actionExecutions = new[] { new NarrativeActionExecutionRecordData { actionExecutionId = "action.001", narrativeEventId = "narrative-event.prototype.reported", actionDefinitionId = "action-definition.prototype.state", category = NarrativeActionCategory.RequestNarrativeStateTransition, lifecycle = NarrativeActionLifecycle.Committed, worldTime = 8d } } },
                        new NarrativeEventRecordData { narrativeEventId = "narrative-event.prototype.hidden", eventDefinitionId = "narrative-event-definition.prototype.hidden", worldId = World, lifecycle = NarrativeEventLifecycle.Resolved, actorPersonId = "person.prototype.hidden", triggerTime = 8.5d, visibility = NarrativeEventVisibility.Hidden }
                    },
                    signals = new List<NarrativeSignalRecordData>
                    {
                        new NarrativeSignalRecordData { narrativeSignalId = "signal.prototype.reported", signalDefinitionId = "signal-definition.prototype.reported", actorPersonId = PersonId, sourceId = QuestId, worldTime = 8d, runtimeRevision = 1 }
                    }
                },
                NarrativeStates = new NarrativeStateRuntimeSaveData
                {
                    schemaVersion = 1,
                    worldId = World,
                    revision = 2,
                    states = new[]
                    {
                        new NarrativeStateRecordData { narrativeStateId = "narrative-state.prototype.guild", stateDefinitionId = "narrative-state-definition.prototype.guild", worldId = World, variables = new[] { new NarrativeStateVariableRecordData { variableDefinitionId = "guild_intro_stage", value = NarrativeVariableValueData.Token("reported"), changedWorldTime = 8d } }, createdWorldTime = 1d, updatedWorldTime = 8d, revision = 2 }
                    },
                    transitions = new[]
                    {
                        new NarrativeStateTransitionRecordData { transitionId = "state-transition.001", narrativeStateId = "narrative-state.prototype.guild", stateDefinitionId = "narrative-state-definition.prototype.guild", variableDefinitionId = "guild_intro_stage", worldId = World, actorPersonId = PersonId, questId = QuestId, conversationId = ConversationId, narrativeEventId = "narrative-event.prototype.reported", oldValue = NarrativeVariableValueData.Token("accepted"), newValue = NarrativeVariableValueData.Token("reported"), worldTime = 8d, revisionBefore = 1, revisionAfter = 2, sequence = 1, visibility = NarrativeStateVisibility.Public }
                    }
                },
                NarrativeArcs = new NarrativeArcRuntimeSaveData
                {
                    schemaVersion = NarrativeArcRuntimeSaveData.CurrentSchemaVersion,
                    worldId = World,
                    revision = 2,
                    arcs = new List<NarrativeArcRecordData>
                    {
                        new NarrativeArcRecordData
                        {
                            narrativeArcId = ArcId,
                            arcDefinitionId = "narrative-arc-definition.prototype.guild-intro",
                            worldId = World,
                            lifecycle = NarrativeArcLifecycle.Completed,
                            actorPersonId = PersonId,
                            startedWorldTime = 1d,
                            resolvedWorldTime = 12d,
                            stages = new[]
                            {
                                new NarrativeArcStageRecordData { stageRuntimeId = "arc-stage.001", stageDefinitionId = "stage.accept", lifecycle = NarrativeArcStageLifecycle.Completed, activatedWorldTime = 2d, resolvedWorldTime = 3d, boundQuests = new[] { new NarrativeArcBoundQuestRecordData { bindingDefinitionId = "binding.guild-posting", questId = QuestId, questDefinitionId = "quest-definition.prototype.guild-posting", mode = NarrativeArcQuestBindingMode.ReferenceExistingQuest, worldTime = 2d } } },
                                new NarrativeArcStageRecordData { stageRuntimeId = "arc-stage.002", stageDefinitionId = "stage.report", lifecycle = NarrativeArcStageLifecycle.Completed, activatedWorldTime = 7d, resolvedWorldTime = 12d }
                            }
                        }
                    }
                }
            }.Clone();
        }
    }
}
