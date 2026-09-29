using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Narrative
{
    public sealed class Step15NarrativeHistoricalService
    {
        private static readonly Step15NarrativeRestorePhase[] RestorePhases =
        {
            Step15NarrativeRestorePhase.ReadEnvelope,
            Step15NarrativeRestorePhase.ValidateSchema,
            Step15NarrativeRestorePhase.DeserializeCandidate,
            Step15NarrativeRestorePhase.ResolveDefinitions,
            Step15NarrativeRestorePhase.ResolveDependencies,
            Step15NarrativeRestorePhase.PrepareIndexes,
            Step15NarrativeRestorePhase.CrossValidate,
            Step15NarrativeRestorePhase.CommitAuthoritativeState,
            Step15NarrativeRestorePhase.RebuildDerivedState,
            Step15NarrativeRestorePhase.RestoreScheduler,
            Step15NarrativeRestorePhase.RestoreSubscriptions,
            Step15NarrativeRestorePhase.Reconcile,
            Step15NarrativeRestorePhase.ValidateFinalState,
            Step15NarrativeRestorePhase.PublishReady,
            Step15NarrativeRestorePhase.SceneRebind
        };

        private static readonly Step15NarrativeOwnershipEntry[] Ownership =
        {
            new Step15NarrativeOwnershipEntry("Quest records", nameof(QuestRuntime), QuestRuntimePersistenceParticipant.Key, false, "Includes subject links, issuer, recipient, origin, lifecycle, and quest history events."),
            new Step15NarrativeOwnershipEntry("Quest offers", nameof(QuestParticipationRuntime), QuestParticipationRuntimePersistenceParticipant.Key, false, "Includes offer lifecycle history and recipient/provider references."),
            new Step15NarrativeOwnershipEntry("Quest assignments", nameof(QuestParticipationRuntime), QuestParticipationRuntimePersistenceParticipant.Key, false, "Includes assignment lifecycle history and assignee references."),
            new Step15NarrativeOwnershipEntry("Quest objective progress", nameof(QuestObjectiveProgressRuntime), QuestObjectiveProgressPersistenceParticipant.Key, false, "Includes objective records, progress evidence, counted source events, and objective events."),
            new Step15NarrativeOwnershipEntry("Quest outcomes and deadlines", nameof(QuestOutcomeRuntime), QuestOutcomePersistenceParticipant.Key, false, "Includes terminal outcomes, deadlines, reward entitlements, grants, and outcome events."),
            new Step15NarrativeOwnershipEntry("Quest sources and listings", nameof(QuestSourceRuntime), QuestSourcePersistenceParticipant.Key, false, "Includes source/listing lifecycle, discovery, and source associations."),
            new Step15NarrativeOwnershipEntry("Conversations", nameof(ConversationRuntime), ConversationPersistenceParticipant.Key, false, "Includes participant records, subject links, lifecycle, and conversation events."),
            new Step15NarrativeOwnershipEntry("Dialogue flow", nameof(DialogueFlowRuntime), DialogueFlowPersistenceParticipant.Key, false, "Includes current node, visits, choices, local variables, and flow events."),
            new Step15NarrativeOwnershipEntry("Narrative events and signals", nameof(NarrativeEventRuntime), NarrativeEventPersistenceParticipant.Key, false, "Includes trigger state, signal records, action executions, and processed trigger keys."),
            new Step15NarrativeOwnershipEntry("Narrative state", nameof(NarrativeStateRuntime), NarrativeStatePersistenceParticipant.Key, false, "Includes typed current variables and immutable transition history."),
            new Step15NarrativeOwnershipEntry("Narrative arcs", nameof(NarrativeArcRuntime), NarrativeArcPersistenceParticipant.Key, false, "Includes arc lifecycle, stage state, bound quest IDs, and processed signal keys."),
            new Step15NarrativeOwnershipEntry("Derived indexes", nameof(Step15NarrativeHistoricalService), string.Empty, true, "Rebuilt from owner records during validation, restore, and historical queries."),
            new Step15NarrativeOwnershipEntry("Scene bindings", "WorldLocations.SceneBinding", string.Empty, true, "Presentation-only; scene objects rebind to authoritative records after restore.")
        };

        public Step15NarrativePersistenceSnapshot Capture(
            QuestRuntime quests,
            QuestParticipationRuntime participation,
            QuestObjectiveProgressRuntime objectives,
            QuestOutcomeRuntime outcomes,
            QuestSourceRuntime sources,
            ConversationRuntime conversations,
            DialogueFlowRuntime dialogueFlows,
            NarrativeEventRuntime narrativeEvents,
            NarrativeStateRuntime narrativeStates,
            NarrativeArcRuntime narrativeArcs,
            string saveSlotId = "",
            double saveWorldTime = 0d,
            string worldId = PersistenceService.LocalWorldId)
        {
            return new Step15NarrativePersistenceSnapshot
            {
                WorldId = N(worldId),
                SaveSlotId = N(saveSlotId),
                SaveWorldTime = saveWorldTime,
                Quests = quests?.CreateSaveData(),
                Participation = participation?.CreateSaveData(),
                Objectives = objectives?.CreateSaveData(),
                Outcomes = outcomes?.CreateSaveData(),
                Sources = sources?.CreateSaveData(),
                Conversations = conversations?.CreateSaveData(),
                DialogueFlows = dialogueFlows?.CreateSaveData(),
                NarrativeEvents = narrativeEvents?.CreateSaveData(),
                NarrativeStates = narrativeStates?.CreateSaveData(),
                NarrativeArcs = narrativeArcs?.CreateSaveData()
            }.Clone();
        }

        public IReadOnlyList<Step15NarrativeOwnershipEntry> OwnershipMap => Ownership.Select(value => new Step15NarrativeOwnershipEntry(value.Category, value.AuthoritativeOwner, value.ParticipantKey, value.Derived, value.Notes)).ToArray();

        public Step15NarrativePersistenceManifest BuildManifest(Step15NarrativePersistenceSnapshot snapshot, Step15NarrativeReadinessState readiness = Step15NarrativeReadinessState.Ready)
        {
            snapshot = snapshot?.Clone() ?? new Step15NarrativePersistenceSnapshot();
            Dictionary<string, int> schemas = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [QuestRuntimePersistenceParticipant.Key] = snapshot.Quests?.schemaVersion ?? 0,
                [QuestParticipationRuntimePersistenceParticipant.Key] = snapshot.Participation?.schemaVersion ?? 0,
                [QuestObjectiveProgressPersistenceParticipant.Key] = snapshot.Objectives?.schemaVersion ?? 0,
                [QuestOutcomePersistenceParticipant.Key] = snapshot.Outcomes?.schemaVersion ?? 0,
                [QuestSourcePersistenceParticipant.Key] = snapshot.Sources?.schemaVersion ?? 0,
                [ConversationPersistenceParticipant.Key] = snapshot.Conversations?.schemaVersion ?? 0,
                [DialogueFlowPersistenceParticipant.Key] = snapshot.DialogueFlows?.schemaVersion ?? 0,
                [NarrativeEventPersistenceParticipant.Key] = snapshot.NarrativeEvents?.schemaVersion ?? 0,
                [NarrativeStatePersistenceParticipant.Key] = snapshot.NarrativeStates?.schemaVersion ?? 0,
                [NarrativeArcPersistenceParticipant.Key] = snapshot.NarrativeArcs?.schemaVersion ?? 0
            };

            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["quests"] = snapshot.Quests?.quests?.Count ?? 0,
                ["questEvents"] = snapshot.Quests?.events?.Count ?? 0,
                ["offers"] = snapshot.Participation?.offers?.Count ?? 0,
                ["assignments"] = snapshot.Participation?.assignments?.Count ?? 0,
                ["objectives"] = snapshot.Objectives?.objectives?.Count ?? 0,
                ["objectiveEvents"] = snapshot.Objectives?.events?.Count ?? 0,
                ["outcomes"] = snapshot.Outcomes?.terminalOutcomes?.Count ?? 0,
                ["rewardEntitlements"] = snapshot.Outcomes?.rewardEntitlements?.Count ?? 0,
                ["questSources"] = snapshot.Sources?.sources?.Count ?? 0,
                ["questListings"] = snapshot.Sources?.listings?.Count ?? 0,
                ["conversations"] = snapshot.Conversations?.conversations?.Count ?? 0,
                ["dialogueFlows"] = snapshot.DialogueFlows?.flows?.Count ?? 0,
                ["dialogueSelections"] = snapshot.DialogueFlows?.flows?.Sum(flow => flow?.selections?.Length ?? 0) ?? 0,
                ["narrativeEvents"] = snapshot.NarrativeEvents?.events?.Count ?? 0,
                ["narrativeSignals"] = snapshot.NarrativeEvents?.signals?.Count ?? 0,
                ["narrativeStates"] = snapshot.NarrativeStates?.states?.Length ?? 0,
                ["narrativeTransitions"] = snapshot.NarrativeStates?.transitions?.Length ?? 0,
                ["narrativeArcs"] = snapshot.NarrativeArcs?.arcs?.Count ?? 0
            };

            return new Step15NarrativePersistenceManifest
            {
                WorldId = N(snapshot.WorldId),
                SaveSlotId = N(snapshot.SaveSlotId),
                SaveWorldTime = snapshot.SaveWorldTime,
                Readiness = readiness,
                RestorePhases = RestorePhases.ToArray(),
                Ownership = OwnershipMap,
                ParticipantSchemaVersions = schemas,
                RecordCounts = counts,
                DeterministicFingerprint = BuildFingerprint(snapshot, counts)
            };
        }

        public Step15NarrativeValidationReport Validate(Step15NarrativePersistenceSnapshot snapshot, string expectedWorldId = PersistenceService.LocalWorldId)
        {
            snapshot = snapshot?.Clone() ?? new Step15NarrativePersistenceSnapshot();
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            List<NarrativeRecoveryIssue> recoveries = new List<NarrativeRecoveryIssue>();
            string expectedWorld = N(expectedWorldId);

            CheckSchema(snapshot.Quests?.schemaVersion, QuestRuntimeSaveData.CurrentSchemaVersion, QuestRuntimePersistenceParticipant.Key, errors);
            CheckSchema(snapshot.Participation?.schemaVersion, QuestParticipationRuntimeSaveData.CurrentSchemaVersion, QuestParticipationRuntimePersistenceParticipant.Key, errors);
            CheckSchema(snapshot.Objectives?.schemaVersion, QuestObjectiveProgressRuntimeSaveData.CurrentSchemaVersion, QuestObjectiveProgressPersistenceParticipant.Key, errors);
            CheckSchema(snapshot.Outcomes?.schemaVersion, QuestOutcomeRuntimeSaveData.CurrentSchemaVersion, QuestOutcomePersistenceParticipant.Key, errors);
            CheckSchema(snapshot.Sources?.schemaVersion, QuestSourceRuntimeSaveData.CurrentSchemaVersion, QuestSourcePersistenceParticipant.Key, errors);
            CheckSchema(snapshot.Conversations?.schemaVersion, ConversationRuntimeSaveData.CurrentSchemaVersion, ConversationPersistenceParticipant.Key, errors);
            CheckSchema(snapshot.DialogueFlows?.schemaVersion, DialogueFlowRuntimeSaveData.CurrentSchemaVersion, DialogueFlowPersistenceParticipant.Key, errors);
            CheckSchema(snapshot.NarrativeEvents?.schemaVersion, NarrativeEventRuntimeSaveData.CurrentSchemaVersion, NarrativeEventPersistenceParticipant.Key, errors);
            CheckSchema(snapshot.NarrativeStates?.schemaVersion, 1, NarrativeStatePersistenceParticipant.Key, errors);
            CheckSchema(snapshot.NarrativeArcs?.schemaVersion, NarrativeArcRuntimeSaveData.CurrentSchemaVersion, NarrativeArcPersistenceParticipant.Key, errors);

            foreach (string world in Worlds(snapshot))
            {
                if (!string.IsNullOrWhiteSpace(expectedWorld) && !string.Equals(world, expectedWorld, StringComparison.Ordinal))
                {
                    errors.Add($"Step 15 participant world '{world}' does not match expected world '{expectedWorld}'.");
                }
            }

            HashSet<string> questIds = Ids(snapshot.Quests?.quests, quest => quest.questId, "Quest", errors);
            HashSet<string> offerIds = Ids(snapshot.Participation?.offers, offer => offer.offerId, "Quest offer", errors);
            HashSet<string> assignmentIds = Ids(snapshot.Participation?.assignments, assignment => assignment.assignmentId, "Quest assignment", errors);
            HashSet<string> objectiveIds = Ids(snapshot.Objectives?.objectives, objective => objective.objectiveId, "Quest objective", errors);
            HashSet<string> outcomeIds = Ids(snapshot.Outcomes?.terminalOutcomes, outcome => outcome.terminalOutcomeId, "Quest terminal outcome", errors);
            HashSet<string> entitlementIds = Ids(snapshot.Outcomes?.rewardEntitlements, reward => reward.entitlementId, "Quest reward entitlement", errors);
            HashSet<string> sourceIds = Ids(snapshot.Sources?.sources, source => source.questSourceId, "Quest source", errors);
            HashSet<string> listingIds = Ids(snapshot.Sources?.listings, listing => listing.questListingId, "Quest listing", errors);
            HashSet<string> conversationIds = Ids(snapshot.Conversations?.conversations, conversation => conversation.conversationId, "Conversation", errors);
            HashSet<string> flowIds = Ids(snapshot.DialogueFlows?.flows, flow => flow.flowId, "Dialogue flow", errors);
            HashSet<string> narrativeEventIds = Ids(snapshot.NarrativeEvents?.events, record => record.narrativeEventId, "Narrative event", errors);
            HashSet<string> narrativeStateIds = Ids(snapshot.NarrativeStates?.states, state => state.narrativeStateId, "Narrative state", errors);
            HashSet<string> narrativeArcIds = Ids(snapshot.NarrativeArcs?.arcs, arc => arc.narrativeArcId, "Narrative arc", errors);

            foreach (QuestOfferRecordData offer in snapshot.Participation?.offers ?? new List<QuestOfferRecordData>())
            {
                RequireRef(questIds, offer.questId, $"Quest offer '{offer.offerId}' references missing quest", errors);
            }

            foreach (QuestAssignmentRecordData assignment in snapshot.Participation?.assignments ?? new List<QuestAssignmentRecordData>())
            {
                RequireRef(questIds, assignment.questId, $"Quest assignment '{assignment.assignmentId}' references missing quest", errors);
                if (!string.IsNullOrWhiteSpace(assignment.offerId)) RequireRef(offerIds, assignment.offerId, $"Quest assignment '{assignment.assignmentId}' references missing offer", errors);
            }

            foreach (QuestObjectiveRecordData objective in snapshot.Objectives?.objectives ?? new List<QuestObjectiveRecordData>())
            {
                RequireRef(questIds, objective.questId, $"Quest objective '{objective.objectiveId}' references missing quest", errors);
                if (!string.IsNullOrWhiteSpace(objective.assignmentId)) RequireRef(assignmentIds, objective.assignmentId, $"Quest objective '{objective.objectiveId}' references missing assignment", errors);
            }

            foreach (QuestTerminalOutcomeRecordData outcome in snapshot.Outcomes?.terminalOutcomes ?? new List<QuestTerminalOutcomeRecordData>())
            {
                RequireRef(questIds, outcome.questId, $"Quest outcome '{outcome.terminalOutcomeId}' references missing quest", errors);
                if (!string.IsNullOrWhiteSpace(outcome.assignmentId)) RequireRef(assignmentIds, outcome.assignmentId, $"Quest outcome '{outcome.terminalOutcomeId}' references missing assignment", errors);
            }

            foreach (QuestRewardEntitlementRecordData reward in snapshot.Outcomes?.rewardEntitlements ?? new List<QuestRewardEntitlementRecordData>())
            {
                RequireRef(questIds, reward.questId, $"Reward entitlement '{reward.entitlementId}' references missing quest", errors);
                if (!string.IsNullOrWhiteSpace(reward.terminalOutcomeId)) RequireRef(outcomeIds, reward.terminalOutcomeId, $"Reward entitlement '{reward.entitlementId}' references missing terminal outcome", errors);
            }

            foreach (QuestListingRecordData listing in snapshot.Sources?.listings ?? new List<QuestListingRecordData>())
            {
                RequireRef(sourceIds, listing.questSourceId, $"Quest listing '{listing.questListingId}' references missing source", errors);
                RequireRef(questIds, listing.questId, $"Quest listing '{listing.questListingId}' references missing quest", errors);
            }

            foreach (ConversationRecordData conversation in snapshot.Conversations?.conversations ?? new List<ConversationRecordData>())
            {
                if (!string.IsNullOrWhiteSpace(conversation.questId)) RequireRef(questIds, conversation.questId, $"Conversation '{conversation.conversationId}' references missing quest", errors);
                if (!string.IsNullOrWhiteSpace(conversation.questSourceId)) RequireRef(sourceIds, conversation.questSourceId, $"Conversation '{conversation.conversationId}' references missing quest source", errors);
                if (!string.IsNullOrWhiteSpace(conversation.questListingId)) RequireRef(listingIds, conversation.questListingId, $"Conversation '{conversation.conversationId}' references missing quest listing", errors);
            }

            foreach (DialogueFlowRecordData flow in snapshot.DialogueFlows?.flows ?? new List<DialogueFlowRecordData>())
            {
                RequireRef(conversationIds, flow.conversationId, $"Dialogue flow '{flow.flowId}' references missing conversation", errors);
                if (!string.IsNullOrWhiteSpace(flow.currentNodeId) && (flow.visits == null || flow.visits.All(visit => !string.Equals(visit.nodeId, flow.currentNodeId, StringComparison.Ordinal))))
                {
                    recoveries.Add(Recoverable(NarrativeRecoveryIssueKind.StaleDerivedIndex, nameof(DialogueFlowRuntime), flow.flowId, "Current node has no matching visit; presentation can resync from flow state but history is incomplete."));
                }
            }

            foreach (NarrativeEventRecordData narrativeEvent in snapshot.NarrativeEvents?.events ?? new List<NarrativeEventRecordData>())
            {
                if (!string.IsNullOrWhiteSpace(narrativeEvent.questId)) RequireRef(questIds, narrativeEvent.questId, $"Narrative event '{narrativeEvent.narrativeEventId}' references missing quest", errors);
                if (!string.IsNullOrWhiteSpace(narrativeEvent.conversationId)) RequireRef(conversationIds, narrativeEvent.conversationId, $"Narrative event '{narrativeEvent.narrativeEventId}' references missing conversation", errors);
            }

            foreach (NarrativeStateTransitionRecordData transition in snapshot.NarrativeStates?.transitions ?? Array.Empty<NarrativeStateTransitionRecordData>())
            {
                RequireRef(narrativeStateIds, transition.narrativeStateId, $"Narrative state transition '{transition.transitionId}' references missing state", errors);
                if (!string.IsNullOrWhiteSpace(transition.questId)) RequireRef(questIds, transition.questId, $"Narrative state transition '{transition.transitionId}' references missing quest", errors);
                if (!string.IsNullOrWhiteSpace(transition.conversationId)) RequireRef(conversationIds, transition.conversationId, $"Narrative state transition '{transition.transitionId}' references missing conversation", errors);
                if (!string.IsNullOrWhiteSpace(transition.narrativeEventId)) RequireRef(narrativeEventIds, transition.narrativeEventId, $"Narrative state transition '{transition.transitionId}' references missing narrative event", errors);
            }

            foreach (NarrativeArcRecordData arc in snapshot.NarrativeArcs?.arcs ?? new List<NarrativeArcRecordData>())
            {
                foreach (NarrativeArcStageRecordData stage in arc.stages ?? Array.Empty<NarrativeArcStageRecordData>())
                {
                    foreach (NarrativeArcBoundQuestRecordData boundQuest in stage.boundQuests ?? Array.Empty<NarrativeArcBoundQuestRecordData>())
                    {
                        RequireRef(questIds, boundQuest.questId, $"Narrative arc '{arc.narrativeArcId}' stage '{stage.stageDefinitionId}' references missing bound quest", errors);
                    }
                }
            }

            if (snapshot.NarrativeArcs?.arcs != null && narrativeArcIds.Count != snapshot.NarrativeArcs.arcs.Count)
            {
                recoveries.Add(NonRecoverable(NarrativeRecoveryIssueKind.AuthoritativeCorruption, nameof(NarrativeArcRuntime), string.Empty, "Duplicate narrative arc IDs require explicit save repair."));
            }

            return new Step15NarrativeValidationReport(errors, warnings, recoveries);
        }

        public NarrativeTimelinePage QueryTimeline(Step15NarrativePersistenceSnapshot snapshot, NarrativeTimelineQuery query)
        {
            query ??= new NarrativeTimelineQuery();
            int limit = Math.Max(1, Math.Min(500, query.Limit));
            IEnumerable<NarrativeTimelineEntry> entries = BuildTimeline(snapshot?.Clone() ?? new Step15NarrativePersistenceSnapshot())
                .Where(entry => Visible(entry, query))
                .Where(entry => entry.WorldTime >= query.StartWorldTime && entry.WorldTime <= query.EndWorldTime)
                .Where(entry => string.IsNullOrWhiteSpace(query.AfterCursor) || string.CompareOrdinal(entry.Cursor, query.AfterCursor) > 0)
                .OrderBy(entry => entry.WorldTime)
                .ThenBy(entry => entry.Sequence)
                .ThenBy(entry => (int)entry.Category)
                .ThenBy(entry => entry.StableSourceReference, StringComparer.Ordinal);

            List<NarrativeTimelineEntry> page = entries.Take(limit + 1).Select(entry => entry.Clone()).ToList();
            bool hasMore = page.Count > limit;
            if (hasMore)
            {
                page.RemoveAt(page.Count - 1);
            }

            return new NarrativeTimelinePage(page, page.Count == 0 ? string.Empty : page[^1].Cursor, hasMore);
        }

        public HistoricalQuestSnapshot GetQuestAt(Step15NarrativePersistenceSnapshot snapshot, string questId, double worldTime, NarrativeHistoricalAccessMode accessMode = NarrativeHistoricalAccessMode.Development)
        {
            snapshot = snapshot?.Clone() ?? new Step15NarrativePersistenceSnapshot();
            questId = N(questId);
            QuestRecordData quest = snapshot.Quests?.quests?.FirstOrDefault(value => string.Equals(value.questId, questId, StringComparison.Ordinal));
            if (quest == null)
            {
                return new HistoricalQuestSnapshot { QuestId = questId, Existed = false, Gap = NarrativeHistoricalGapKind.MissingRecord };
            }

            if (!QuestVisible(quest.visibility, accessMode))
            {
                return new HistoricalQuestSnapshot { QuestId = string.Empty, Existed = false, Gap = NarrativeHistoricalGapKind.HiddenByAccess };
            }

            if (worldTime < quest.createdWorldTime)
            {
                return new HistoricalQuestSnapshot { QuestId = questId, Existed = false, Gap = NarrativeHistoricalGapKind.BeforeCreation };
            }

            QuestRuntimeLifecycleState lifecycle = LifecycleAt(quest, snapshot.Quests?.events, worldTime);
            return new HistoricalQuestSnapshot
            {
                QuestId = questId,
                Existed = true,
                Lifecycle = lifecycle,
                Offers = (snapshot.Participation?.offers ?? new List<QuestOfferRecordData>())
                    .Where(value => string.Equals(value.questId, questId, StringComparison.Ordinal) && value.createdWorldTime <= worldTime && QuestVisible(value.visibility, accessMode))
                    .OrderBy(value => value.offerId, StringComparer.Ordinal)
                    .Select(value => new QuestOfferLifecycleAtTime { OfferId = value.offerId, State = OfferStateAt(value, snapshot.Participation?.events, worldTime) })
                    .ToArray(),
                Assignments = (snapshot.Participation?.assignments ?? new List<QuestAssignmentRecordData>())
                    .Where(value => string.Equals(value.questId, questId, StringComparison.Ordinal) && value.assignedWorldTime <= worldTime && QuestVisible(value.visibility, accessMode))
                    .OrderBy(value => value.assignmentId, StringComparer.Ordinal)
                    .Select(value => new QuestAssignmentLifecycleAtTime { AssignmentId = value.assignmentId, State = AssignmentStateAt(value, snapshot.Participation?.events, worldTime) })
                    .ToArray(),
                Objectives = (snapshot.Objectives?.objectives ?? new List<QuestObjectiveRecordData>())
                    .Where(value => string.Equals(value.questId, questId, StringComparison.Ordinal) && value.activatedWorldTime <= worldTime && ObjectiveVisible(value.visibility, accessMode))
                    .OrderBy(value => value.objectiveId, StringComparer.Ordinal)
                    .Select(value => ObjectiveAt(value, snapshot.Objectives?.events, worldTime))
                    .ToArray(),
                Outcome = OutcomeAt(snapshot.Outcomes?.terminalOutcomes, questId, worldTime, accessMode),
                Rewards = (snapshot.Outcomes?.rewardEntitlements ?? new List<QuestRewardEntitlementRecordData>())
                    .Where(value => string.Equals(value.questId, questId, StringComparison.Ordinal) && value.createdWorldTime <= worldTime && (!value.hidden || accessMode == NarrativeHistoricalAccessMode.Development))
                    .OrderBy(value => value.entitlementId, StringComparer.Ordinal)
                    .Select(value => new QuestRewardStateAtTime { EntitlementId = value.entitlementId, State = RewardStateAt(value, snapshot.Outcomes?.events, worldTime) })
                    .ToArray(),
                ActiveListingIds = (snapshot.Sources?.listings ?? new List<QuestListingRecordData>())
                    .Where(value => string.Equals(value.questId, questId, StringComparison.Ordinal) && value.publishedWorldTime <= worldTime && (value.endedWorldTime < 0d || value.endedWorldTime > worldTime) && SourceVisible(value.visibility, accessMode))
                    .Select(value => value.questListingId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        public HistoricalPersonQuestSnapshot GetPersonQuestSnapshotAt(Step15NarrativePersistenceSnapshot snapshot, string personId, double worldTime, NarrativeHistoricalAccessMode accessMode = NarrativeHistoricalAccessMode.PersonSafe)
        {
            snapshot = snapshot?.Clone() ?? new Step15NarrativePersistenceSnapshot();
            personId = N(personId);
            QuestAssignmentRecordData[] assignments = (snapshot.Participation?.assignments ?? new List<QuestAssignmentRecordData>())
                .Where(value => string.Equals(value.assigneePersonId, personId, StringComparison.Ordinal) && value.assignedWorldTime <= worldTime && QuestVisible(value.visibility, accessMode))
                .ToArray();
            QuestRewardEntitlementRecordData[] rewards = (snapshot.Outcomes?.rewardEntitlements ?? new List<QuestRewardEntitlementRecordData>())
                .Where(value => string.Equals(value.recipientPersonId, personId, StringComparison.Ordinal) && value.createdWorldTime <= worldTime && (!value.hidden || accessMode == NarrativeHistoricalAccessMode.Development))
                .ToArray();

            return new HistoricalPersonQuestSnapshot
            {
                PersonId = personId,
                PendingOfferIds = (snapshot.Participation?.offers ?? new List<QuestOfferRecordData>())
                    .Where(value => value.createdWorldTime <= worldTime && string.Equals(value.recipient?.recipientId, personId, StringComparison.Ordinal) && OfferStateAt(value, snapshot.Participation?.events, worldTime) == QuestOfferLifecycleState.Active && QuestVisible(value.visibility, accessMode))
                    .Select(value => value.offerId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                ActiveAssignmentIds = assignments
                    .Where(value => AssignmentStateAt(value, snapshot.Participation?.events, worldTime) == QuestAssignmentLifecycleState.Active)
                    .Select(value => value.assignmentId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                CompletedQuestIds = assignments
                    .Where(value => OutcomeAt(snapshot.Outcomes?.terminalOutcomes, value.questId, worldTime, accessMode) == QuestTerminalOutcomeKind.Completed)
                    .Select(value => value.questId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                FailedQuestIds = assignments
                    .Where(value => OutcomeAt(snapshot.Outcomes?.terminalOutcomes, value.questId, worldTime, accessMode) == QuestTerminalOutcomeKind.Failed)
                    .Select(value => value.questId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                ClaimableRewardIds = rewards
                    .Where(value => RewardStateAt(value, snapshot.Outcomes?.events, worldTime) == QuestRewardEntitlementState.Claimable)
                    .Select(value => value.entitlementId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        public HistoricalConversationSnapshot GetConversationAt(Step15NarrativePersistenceSnapshot snapshot, string conversationId, double worldTime, NarrativeHistoricalAccessMode accessMode = NarrativeHistoricalAccessMode.Development)
        {
            snapshot = snapshot?.Clone() ?? new Step15NarrativePersistenceSnapshot();
            conversationId = N(conversationId);
            ConversationRecordData conversation = snapshot.Conversations?.conversations?.FirstOrDefault(value => string.Equals(value.conversationId, conversationId, StringComparison.Ordinal));
            if (conversation == null)
            {
                return new HistoricalConversationSnapshot { ConversationId = conversationId, Existed = false, Gap = NarrativeHistoricalGapKind.MissingRecord };
            }

            if (!ConversationVisible(conversation.visibility, accessMode))
            {
                return new HistoricalConversationSnapshot { Gap = NarrativeHistoricalGapKind.HiddenByAccess };
            }

            DialogueFlowRecordData flow = snapshot.DialogueFlows?.flows?.FirstOrDefault(value => string.Equals(value.conversationId, conversationId, StringComparison.Ordinal));
            DialogueChoiceSelectionRecordData latestChoice = flow?.selections?
                .Where(value => value.worldTime <= worldTime)
                .OrderByDescending(value => value.worldTime)
                .ThenByDescending(value => value.selectionId, StringComparer.Ordinal)
                .FirstOrDefault();
            DialogueNodeVisitRecordData node = flow?.visits?
                .Where(value => value.enteredWorldTime <= worldTime && (value.exitedWorldTime < 0d || value.exitedWorldTime > worldTime))
                .OrderByDescending(value => value.enteredWorldTime)
                .ThenByDescending(value => value.sequence)
                .FirstOrDefault();

            return new HistoricalConversationSnapshot
            {
                ConversationId = conversationId,
                Existed = conversation.startedWorldTime <= worldTime,
                Lifecycle = ConversationStateAt(conversation, snapshot.Conversations?.events, worldTime),
                ActiveDialogueNodeId = node?.nodeId ?? flow?.currentNodeId ?? string.Empty,
                LatestChoiceId = latestChoice?.choiceId ?? string.Empty,
                ParticipantPersonIds = (conversation.participants ?? Array.Empty<ConversationParticipantRecordData>())
                    .Where(value => accessMode == NarrativeHistoricalAccessMode.Development || !value.hidden)
                    .Select(value => value.personId)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        public HistoricalNarrativeStateSnapshot GetNarrativeStateAt(Step15NarrativePersistenceSnapshot snapshot, string narrativeStateId, double worldTime, NarrativeHistoricalAccessMode accessMode = NarrativeHistoricalAccessMode.Development)
        {
            snapshot = snapshot?.Clone() ?? new Step15NarrativePersistenceSnapshot();
            narrativeStateId = N(narrativeStateId);
            NarrativeStateRecordData state = snapshot.NarrativeStates?.states?.FirstOrDefault(value => string.Equals(value.narrativeStateId, narrativeStateId, StringComparison.Ordinal));
            if (state == null)
            {
                return new HistoricalNarrativeStateSnapshot { NarrativeStateId = narrativeStateId, Gap = NarrativeHistoricalGapKind.MissingRecord };
            }

            Dictionary<string, string> values = (state.variables ?? Array.Empty<NarrativeStateVariableRecordData>())
                .ToDictionary(value => value.variableDefinitionId, value => ValueAt(state, value.variableDefinitionId, snapshot.NarrativeStates?.transitions, worldTime), StringComparer.Ordinal);
            return new HistoricalNarrativeStateSnapshot
            {
                NarrativeStateId = state.narrativeStateId,
                StateDefinitionId = state.stateDefinitionId,
                VariableValues = values
            };
        }

        public HistoricalNarrativeArcSnapshot GetNarrativeArcAt(Step15NarrativePersistenceSnapshot snapshot, string narrativeArcId, double worldTime, NarrativeHistoricalAccessMode accessMode = NarrativeHistoricalAccessMode.Development)
        {
            snapshot = snapshot?.Clone() ?? new Step15NarrativePersistenceSnapshot();
            narrativeArcId = N(narrativeArcId);
            NarrativeArcRecordData arc = snapshot.NarrativeArcs?.arcs?.FirstOrDefault(value => string.Equals(value.narrativeArcId, narrativeArcId, StringComparison.Ordinal));
            if (arc == null)
            {
                return new HistoricalNarrativeArcSnapshot { NarrativeArcId = narrativeArcId, Existed = false, Gap = NarrativeHistoricalGapKind.MissingRecord };
            }

            if (worldTime < arc.startedWorldTime)
            {
                return new HistoricalNarrativeArcSnapshot { NarrativeArcId = narrativeArcId, Existed = false, Gap = NarrativeHistoricalGapKind.BeforeCreation };
            }

            NarrativeArcStageRecordData[] stages = arc.stages ?? Array.Empty<NarrativeArcStageRecordData>();
            return new HistoricalNarrativeArcSnapshot
            {
                NarrativeArcId = arc.narrativeArcId,
                Existed = true,
                Lifecycle = ArcLifecycleAt(arc, worldTime),
                ActiveStageDefinitionIds = stages.Where(value => value.activatedWorldTime <= worldTime && (value.resolvedWorldTime < 0d || value.resolvedWorldTime > worldTime) && value.lifecycle == NarrativeArcStageLifecycle.Active).Select(value => value.stageDefinitionId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                CompletedStageDefinitionIds = stages.Where(value => value.resolvedWorldTime >= 0d && value.resolvedWorldTime <= worldTime && value.lifecycle == NarrativeArcStageLifecycle.Completed).Select(value => value.stageDefinitionId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                BoundQuestIds = stages.SelectMany(value => value.boundQuests ?? Array.Empty<NarrativeArcBoundQuestRecordData>()).Where(value => value.worldTime <= worldTime).Select(value => value.questId).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
        }

        private IEnumerable<NarrativeTimelineEntry> BuildTimeline(Step15NarrativePersistenceSnapshot snapshot)
        {
            long sequence = 0L;
            foreach (QuestRuntimeEventData evt in snapshot.Quests?.events ?? new List<QuestRuntimeEventData>())
            {
                yield return Entry(evt.worldTime, ++sequence, evt.eventKind == QuestRuntimeEventKind.Instantiated ? NarrativeTimelineCategory.QuestInstantiated : NarrativeTimelineCategory.QuestLifecycleChanged, nameof(QuestRuntime), evt.eventId, questId: evt.questId);
            }

            foreach (QuestParticipationEventData evt in snapshot.Participation?.events ?? new List<QuestParticipationEventData>())
            {
                yield return Entry(evt.worldTime, ++sequence, ParticipationCategory(evt.eventKind), nameof(QuestParticipationRuntime), evt.eventId, questId: evt.questId, offerId: evt.offerId, assignmentId: evt.assignmentId, personId: evt.personId);
            }

            foreach (QuestObjectiveRuntimeEventData evt in snapshot.Objectives?.events ?? new List<QuestObjectiveRuntimeEventData>())
            {
                yield return Entry(evt.worldTime, ++sequence, ObjectiveCategory(evt.eventKind), nameof(QuestObjectiveProgressRuntime), evt.eventId, questId: evt.questId, assignmentId: evt.assignmentId, objectiveId: evt.objectiveId);
            }

            foreach (QuestOutcomeEventData evt in snapshot.Outcomes?.events ?? new List<QuestOutcomeEventData>())
            {
                yield return Entry(evt.worldTime, ++sequence, OutcomeCategory(evt.eventKind), nameof(QuestOutcomeRuntime), evt.eventId, questId: evt.questId, assignmentId: evt.assignmentId);
            }

            foreach (QuestSourceEventData evt in snapshot.Sources?.events ?? new List<QuestSourceEventData>())
            {
                yield return Entry(evt.worldTime, ++sequence, evt.eventKind == QuestSourceEventKind.ListingPublished ? NarrativeTimelineCategory.QuestListed : NarrativeTimelineCategory.QuestListingChanged, nameof(QuestSourceRuntime), evt.eventId, questId: evt.questId);
            }

            foreach (ConversationEventData evt in snapshot.Conversations?.events ?? new List<ConversationEventData>())
            {
                yield return Entry(evt.worldTime, ++sequence, evt.eventKind == ConversationEventKind.ConversationStarted ? NarrativeTimelineCategory.ConversationStarted : NarrativeTimelineCategory.ConversationChanged, nameof(ConversationRuntime), evt.eventId, conversationId: evt.conversationId, personId: evt.personId);
            }

            foreach (DialogueFlowRecordData flow in snapshot.DialogueFlows?.flows ?? new List<DialogueFlowRecordData>())
            {
                foreach (DialogueNodeVisitRecordData visit in flow.visits ?? Array.Empty<DialogueNodeVisitRecordData>())
                {
                    yield return Entry(visit.enteredWorldTime, ++sequence, NarrativeTimelineCategory.DialogueNodeEntered, nameof(DialogueFlowRuntime), visit.visitId, conversationId: visit.conversationId, personId: visit.speakerPersonId, hidden: IsHidden(visit.visibility));
                }

                foreach (DialogueChoiceSelectionRecordData selection in flow.selections ?? Array.Empty<DialogueChoiceSelectionRecordData>())
                {
                    yield return Entry(selection.worldTime, ++sequence, NarrativeTimelineCategory.DialogueChoiceSelected, nameof(DialogueFlowRuntime), selection.selectionId, conversationId: selection.conversationId, personId: selection.actorPersonId);
                }
            }

            foreach (NarrativeEventRecordData evt in snapshot.NarrativeEvents?.events ?? new List<NarrativeEventRecordData>())
            {
                yield return Entry(evt.triggerTime >= 0d ? evt.triggerTime : evt.armTime, ++sequence, NarrativeTimelineCategory.NarrativeEventTriggered, nameof(NarrativeEventRuntime), evt.narrativeEventId, questId: evt.questId, conversationId: evt.conversationId, narrativeEventId: evt.narrativeEventId, personId: evt.actorPersonId, locationId: evt.locationId, organizationId: evt.organizationId, hidden: IsHidden(evt.visibility));
                foreach (NarrativeActionExecutionRecordData action in evt.actionExecutions ?? Array.Empty<NarrativeActionExecutionRecordData>())
                {
                    if (action.lifecycle == NarrativeActionLifecycle.Committed)
                    {
                        yield return Entry(action.worldTime, ++sequence, NarrativeTimelineCategory.NarrativeActionCommitted, nameof(NarrativeEventRuntime), action.actionExecutionId, narrativeEventId: evt.narrativeEventId, hidden: IsHidden(evt.visibility) || action.category == NarrativeActionCategory.Unknown);
                    }
                }
            }

            foreach (NarrativeSignalRecordData signal in snapshot.NarrativeEvents?.signals ?? new List<NarrativeSignalRecordData>())
            {
                yield return Entry(signal.worldTime, ++sequence, NarrativeTimelineCategory.NarrativeSignalEmitted, nameof(NarrativeEventRuntime), signal.narrativeSignalId, personId: signal.actorPersonId);
            }

            foreach (NarrativeStateTransitionRecordData transition in snapshot.NarrativeStates?.transitions ?? Array.Empty<NarrativeStateTransitionRecordData>())
            {
                yield return Entry(transition.worldTime, ++sequence, NarrativeTimelineCategory.NarrativeStateTransitioned, nameof(NarrativeStateRuntime), transition.transitionId, questId: transition.questId, conversationId: transition.conversationId, narrativeEventId: transition.narrativeEventId, narrativeStateId: transition.narrativeStateId, personId: transition.actorPersonId, hidden: IsHidden(transition.visibility));
            }

            foreach (NarrativeArcRecordData arc in snapshot.NarrativeArcs?.arcs ?? new List<NarrativeArcRecordData>())
            {
                yield return Entry(arc.startedWorldTime, ++sequence, NarrativeTimelineCategory.ArcStarted, nameof(NarrativeArcRuntime), arc.narrativeArcId, narrativeArcId: arc.narrativeArcId, personId: arc.actorPersonId);
                foreach (NarrativeArcStageRecordData stage in arc.stages ?? Array.Empty<NarrativeArcStageRecordData>())
                {
                    if (stage.activatedWorldTime >= 0d)
                    {
                        yield return Entry(stage.activatedWorldTime, ++sequence, NarrativeTimelineCategory.ArcStageActivated, nameof(NarrativeArcRuntime), stage.stageRuntimeId, narrativeArcId: arc.narrativeArcId, personId: arc.actorPersonId);
                    }

                    if (stage.resolvedWorldTime >= 0d)
                    {
                        yield return Entry(stage.resolvedWorldTime, ++sequence, stage.lifecycle == NarrativeArcStageLifecycle.Completed ? NarrativeTimelineCategory.ArcStageCompleted : NarrativeTimelineCategory.ArcCompleted, nameof(NarrativeArcRuntime), stage.stageRuntimeId, narrativeArcId: arc.narrativeArcId, personId: arc.actorPersonId);
                    }
                }
            }
        }

        private static NarrativeTimelineEntry Entry(double time, long sequence, NarrativeTimelineCategory category, string runtime, string sourceId, string questId = "", string offerId = "", string assignmentId = "", string objectiveId = "", string conversationId = "", string narrativeEventId = "", string narrativeStateId = "", string narrativeArcId = "", string personId = "", string locationId = "", string organizationId = "", bool hidden = false)
        {
            sourceId = N(sourceId);
            runtime = N(runtime);
            return new NarrativeTimelineEntry
            {
                WorldTime = time < 0d ? 0d : time,
                Sequence = sequence,
                Category = category,
                SourceRuntime = runtime,
                SourceId = sourceId,
                StableSourceReference = $"{runtime}:{sourceId}",
                QuestId = N(questId),
                OfferId = N(offerId),
                AssignmentId = N(assignmentId),
                ObjectiveId = N(objectiveId),
                ConversationId = N(conversationId),
                NarrativeEventId = N(narrativeEventId),
                NarrativeStateId = N(narrativeStateId),
                NarrativeArcId = N(narrativeArcId),
                PersonId = N(personId),
                LocationId = N(locationId),
                OrganizationId = N(organizationId),
                Hidden = hidden
            };
        }

        private static bool Visible(NarrativeTimelineEntry entry, NarrativeTimelineQuery query)
        {
            if (entry.Hidden && query.AccessMode != NarrativeHistoricalAccessMode.Development)
            {
                return false;
            }

            if (query.AccessMode == NarrativeHistoricalAccessMode.PersonSafe && !string.IsNullOrWhiteSpace(query.RequesterPersonId) && !string.IsNullOrWhiteSpace(entry.PersonId) && !string.Equals(query.RequesterPersonId, entry.PersonId, StringComparison.Ordinal))
            {
                return false;
            }

            return (string.IsNullOrWhiteSpace(query.PersonId) || string.Equals(query.PersonId, entry.PersonId, StringComparison.Ordinal))
                && (string.IsNullOrWhiteSpace(query.QuestId) || string.Equals(query.QuestId, entry.QuestId, StringComparison.Ordinal))
                && (string.IsNullOrWhiteSpace(query.ConversationId) || string.Equals(query.ConversationId, entry.ConversationId, StringComparison.Ordinal))
                && (string.IsNullOrWhiteSpace(query.NarrativeEventId) || string.Equals(query.NarrativeEventId, entry.NarrativeEventId, StringComparison.Ordinal))
                && (string.IsNullOrWhiteSpace(query.NarrativeStateId) || string.Equals(query.NarrativeStateId, entry.NarrativeStateId, StringComparison.Ordinal))
                && (string.IsNullOrWhiteSpace(query.NarrativeArcId) || string.Equals(query.NarrativeArcId, entry.NarrativeArcId, StringComparison.Ordinal))
                && (string.IsNullOrWhiteSpace(query.LocationId) || string.Equals(query.LocationId, entry.LocationId, StringComparison.Ordinal))
                && (string.IsNullOrWhiteSpace(query.OrganizationId) || string.Equals(query.OrganizationId, entry.OrganizationId, StringComparison.Ordinal))
                && (!query.Category.HasValue || query.Category.Value == entry.Category);
        }

        private static QuestRuntimeLifecycleState LifecycleAt(QuestRecordData quest, IEnumerable<QuestRuntimeEventData> events, double time)
        {
            QuestRuntimeEventData latest = (events ?? Array.Empty<QuestRuntimeEventData>())
                .Where(value => string.Equals(value.questId, quest.questId, StringComparison.Ordinal) && value.worldTime <= time)
                .OrderByDescending(value => value.worldTime)
                .ThenByDescending(value => value.runtimeRevision)
                .FirstOrDefault();
            return latest != null && latest.afterState != QuestRuntimeLifecycleState.Unknown ? latest.afterState : quest.lifecycleState;
        }

        private static QuestOfferLifecycleState OfferStateAt(QuestOfferRecordData offer, IEnumerable<QuestParticipationEventData> events, double time)
        {
            QuestParticipationEventData latest = (events ?? Array.Empty<QuestParticipationEventData>())
                .Where(value => string.Equals(value.offerId, offer.offerId, StringComparison.Ordinal) && value.worldTime <= time)
                .OrderByDescending(value => value.worldTime)
                .ThenByDescending(value => value.runtimeRevision)
                .FirstOrDefault();
            return latest?.eventKind switch
            {
                QuestParticipationEventKind.OfferCreated => QuestOfferLifecycleState.Active,
                QuestParticipationEventKind.OfferAccepted => QuestOfferLifecycleState.Accepted,
                QuestParticipationEventKind.OfferRefused => QuestOfferLifecycleState.Refused,
                QuestParticipationEventKind.OfferWithdrawn => QuestOfferLifecycleState.Withdrawn,
                QuestParticipationEventKind.OfferExpired => QuestOfferLifecycleState.Expired,
                _ => offer.expirationWorldTime >= 0d && offer.expirationWorldTime <= time && offer.lifecycleState == QuestOfferLifecycleState.Active ? QuestOfferLifecycleState.Expired : offer.lifecycleState
            };
        }

        private static QuestAssignmentLifecycleState AssignmentStateAt(QuestAssignmentRecordData assignment, IEnumerable<QuestParticipationEventData> events, double time)
        {
            QuestParticipationEventData latest = (events ?? Array.Empty<QuestParticipationEventData>())
                .Where(value => string.Equals(value.assignmentId, assignment.assignmentId, StringComparison.Ordinal) && value.worldTime <= time)
                .OrderByDescending(value => value.worldTime)
                .ThenByDescending(value => value.runtimeRevision)
                .FirstOrDefault();
            return latest?.eventKind switch
            {
                QuestParticipationEventKind.AssignmentCreated => QuestAssignmentLifecycleState.Active,
                QuestParticipationEventKind.AssignmentSuspended => QuestAssignmentLifecycleState.Suspended,
                QuestParticipationEventKind.AssignmentResumed => QuestAssignmentLifecycleState.Active,
                QuestParticipationEventKind.AssignmentAbandoned => QuestAssignmentLifecycleState.Abandoned,
                QuestParticipationEventKind.AssignmentWithdrawn => QuestAssignmentLifecycleState.Withdrawn,
                _ => assignment.endedWorldTime >= 0d && assignment.endedWorldTime <= time && assignment.lifecycleState == QuestAssignmentLifecycleState.Active ? QuestAssignmentLifecycleState.Historical : assignment.lifecycleState
            };
        }

        private static QuestObjectiveProgressAtTime ObjectiveAt(QuestObjectiveRecordData objective, IEnumerable<QuestObjectiveRuntimeEventData> events, double time)
        {
            QuestObjectiveRuntimeEventData latest = (events ?? Array.Empty<QuestObjectiveRuntimeEventData>())
                .Where(value => string.Equals(value.objectiveId, objective.objectiveId, StringComparison.Ordinal) && value.worldTime <= time)
                .OrderByDescending(value => value.worldTime)
                .ThenByDescending(value => value.runtimeRevision)
                .FirstOrDefault();
            bool satisfiedByHistory = latest != null
                && (latest.eventKind == QuestObjectiveEventKind.ObjectiveSatisfied
                    || latest.afterState == QuestObjectiveLifecycleState.Satisfied);
            return new QuestObjectiveProgressAtTime
            {
                ObjectiveId = objective.objectiveId,
                State = latest != null && latest.afterState != QuestObjectiveLifecycleState.Unknown ? latest.afterState : objective.lifecycleState,
                CurrentValue = latest?.afterValue ?? objective.currentValue,
                TargetValue = objective.targetValue,
                Satisfied = satisfiedByHistory || (objective.satisfiedWorldTime >= 0d && objective.satisfiedWorldTime <= time)
            };
        }

        private static QuestTerminalOutcomeKind OutcomeAt(IEnumerable<QuestTerminalOutcomeRecordData> outcomes, string questId, double time, NarrativeHistoricalAccessMode accessMode)
        {
            return (outcomes ?? Array.Empty<QuestTerminalOutcomeRecordData>())
                .Where(value => string.Equals(value.questId, questId, StringComparison.Ordinal) && value.worldTime <= time && (!value.hidden || accessMode == NarrativeHistoricalAccessMode.Development))
                .OrderByDescending(value => value.worldTime)
                .ThenBy(value => value.terminalOutcomeId, StringComparer.Ordinal)
                .Select(value => value.outcomeKind)
                .FirstOrDefault();
        }

        private static QuestRewardEntitlementState RewardStateAt(QuestRewardEntitlementRecordData reward, IEnumerable<QuestOutcomeEventData> events, double time)
        {
            QuestOutcomeEventData latest = (events ?? Array.Empty<QuestOutcomeEventData>())
                .Where(value => string.Equals(value.rewardEntitlementId, reward.entitlementId, StringComparison.Ordinal) && value.worldTime <= time)
                .OrderByDescending(value => value.worldTime)
                .ThenByDescending(value => value.runtimeRevision)
                .FirstOrDefault();
            return latest?.eventKind switch
            {
                QuestOutcomeEventKind.RewardEntitlementCreated => QuestRewardEntitlementState.Claimable,
                QuestOutcomeEventKind.RewardGranted => QuestRewardEntitlementState.Granted,
                QuestOutcomeEventKind.RewardGrantFailed => QuestRewardEntitlementState.Failed,
                _ => reward.grantedWorldTime >= 0d && reward.grantedWorldTime <= time ? QuestRewardEntitlementState.Granted : reward.state
            };
        }

        private static ConversationLifecycleState ConversationStateAt(ConversationRecordData conversation, IEnumerable<ConversationEventData> events, double time)
        {
            ConversationEventData latest = (events ?? Array.Empty<ConversationEventData>())
                .Where(value => string.Equals(value.conversationId, conversation.conversationId, StringComparison.Ordinal) && value.worldTime <= time)
                .OrderByDescending(value => value.worldTime)
                .ThenByDescending(value => value.runtimeRevision)
                .FirstOrDefault();
            return latest != null && latest.afterState != ConversationLifecycleState.Unknown ? latest.afterState : conversation.lifecycleState;
        }

        private static string ValueAt(NarrativeStateRecordData state, string variableDefinitionId, IEnumerable<NarrativeStateTransitionRecordData> transitions, double time)
        {
            NarrativeStateTransitionRecordData transition = (transitions ?? Array.Empty<NarrativeStateTransitionRecordData>())
                .Where(value => string.Equals(value.narrativeStateId, state.narrativeStateId, StringComparison.Ordinal) && string.Equals(value.variableDefinitionId, variableDefinitionId, StringComparison.Ordinal) && value.worldTime <= time)
                .OrderByDescending(value => value.worldTime)
                .ThenByDescending(value => value.sequence)
                .FirstOrDefault();
            if (transition != null)
            {
                return transition.newValue?.StableText ?? string.Empty;
            }

            return (state.variables ?? Array.Empty<NarrativeStateVariableRecordData>()).FirstOrDefault(value => string.Equals(value.variableDefinitionId, variableDefinitionId, StringComparison.Ordinal))?.value?.StableText ?? string.Empty;
        }

        private static NarrativeArcLifecycle ArcLifecycleAt(NarrativeArcRecordData arc, double time)
        {
            if (arc.resolvedWorldTime >= 0d && arc.resolvedWorldTime <= time)
            {
                return arc.lifecycle;
            }

            return arc.lifecycle == NarrativeArcLifecycle.Completed || arc.lifecycle == NarrativeArcLifecycle.Failed || arc.lifecycle == NarrativeArcLifecycle.Cancelled ? NarrativeArcLifecycle.Active : arc.lifecycle;
        }

        private static NarrativeTimelineCategory ParticipationCategory(QuestParticipationEventKind kind)
        {
            return kind switch
            {
                QuestParticipationEventKind.OfferCreated => NarrativeTimelineCategory.QuestOffered,
                QuestParticipationEventKind.OfferAccepted => NarrativeTimelineCategory.QuestAccepted,
                QuestParticipationEventKind.AssignmentCreated => NarrativeTimelineCategory.QuestAccepted,
                QuestParticipationEventKind.AssignmentSuspended or QuestParticipationEventKind.AssignmentResumed or QuestParticipationEventKind.AssignmentAbandoned or QuestParticipationEventKind.AssignmentWithdrawn => NarrativeTimelineCategory.QuestAssignmentChanged,
                _ => NarrativeTimelineCategory.QuestOfferChanged
            };
        }

        private static NarrativeTimelineCategory ObjectiveCategory(QuestObjectiveEventKind kind)
        {
            return kind switch
            {
                QuestObjectiveEventKind.ObjectiveActivated => NarrativeTimelineCategory.ObjectiveActivated,
                QuestObjectiveEventKind.ObjectiveSatisfied => NarrativeTimelineCategory.ObjectiveSatisfied,
                _ => NarrativeTimelineCategory.ObjectiveProgressed
            };
        }

        private static NarrativeTimelineCategory OutcomeCategory(QuestOutcomeEventKind kind)
        {
            return kind switch
            {
                QuestOutcomeEventKind.TerminalOutcomeRecorded => NarrativeTimelineCategory.QuestCompleted,
                QuestOutcomeEventKind.RewardEntitlementCreated => NarrativeTimelineCategory.RewardEntitled,
                QuestOutcomeEventKind.RewardGranted => NarrativeTimelineCategory.RewardClaimed,
                _ => NarrativeTimelineCategory.QuestFailed
            };
        }

        private static void CheckSchema(int? actual, int expected, string participant, List<string> errors)
        {
            if (actual.HasValue && actual.Value != expected)
            {
                errors.Add($"{participant} has unsupported schema version {actual.Value}; expected {expected}.");
            }
        }

        private static IEnumerable<string> Worlds(Step15NarrativePersistenceSnapshot snapshot)
        {
            string[] worlds =
            {
                snapshot.WorldId,
                snapshot.Quests?.worldId,
                snapshot.Participation?.worldId,
                snapshot.Objectives?.worldId,
                snapshot.Outcomes?.worldId,
                snapshot.Sources?.worldId,
                snapshot.Conversations?.worldId,
                snapshot.DialogueFlows?.worldId,
                snapshot.NarrativeEvents?.worldId,
                snapshot.NarrativeStates?.worldId,
                snapshot.NarrativeArcs?.worldId
            };
            return worlds.Where(value => !string.IsNullOrWhiteSpace(value)).Select(N).Distinct(StringComparer.Ordinal);
        }

        private static HashSet<string> Ids<T>(IEnumerable<T> records, Func<T, string> selector, string label, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (T record in records ?? Array.Empty<T>())
            {
                string id = N(selector(record));
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add($"{label} record is missing an ID.");
                    continue;
                }

                if (!ids.Add(id))
                {
                    errors.Add($"Duplicate {label} ID '{id}'.");
                }
            }

            return ids;
        }

        private static void RequireRef(HashSet<string> ids, string id, string message, List<string> errors)
        {
            id = N(id);
            if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
            {
                errors.Add($"{message} '{id}'.");
            }
        }

        private static bool QuestVisible(QuestVisibility visibility, NarrativeHistoricalAccessMode access)
        {
            return access == NarrativeHistoricalAccessMode.Development || (visibility != QuestVisibility.Hidden && visibility != QuestVisibility.Secret && visibility != QuestVisibility.Diagnostic && visibility != QuestVisibility.Development);
        }

        private static bool ObjectiveVisible(QuestObjectiveVisibility visibility, NarrativeHistoricalAccessMode access)
        {
            return access == NarrativeHistoricalAccessMode.Development || (visibility != QuestObjectiveVisibility.Hidden && visibility != QuestObjectiveVisibility.Secret && visibility != QuestObjectiveVisibility.Diagnostic);
        }

        private static bool SourceVisible(QuestSourceVisibility visibility, NarrativeHistoricalAccessMode access)
        {
            return access == NarrativeHistoricalAccessMode.Development || (visibility != QuestSourceVisibility.Hidden && visibility != QuestSourceVisibility.Secret && visibility != QuestSourceVisibility.Diagnostic);
        }

        private static bool ConversationVisible(ConversationVisibility visibility, NarrativeHistoricalAccessMode access)
        {
            return access == NarrativeHistoricalAccessMode.Development || (visibility != ConversationVisibility.Hidden && visibility != ConversationVisibility.Secret && visibility != ConversationVisibility.Diagnostic && visibility != ConversationVisibility.Private);
        }

        private static bool IsHidden(ConversationVisibility visibility)
        {
            return visibility == ConversationVisibility.Hidden || visibility == ConversationVisibility.Secret || visibility == ConversationVisibility.Diagnostic || visibility == ConversationVisibility.Private;
        }

        private static bool IsHidden(NarrativeEventVisibility visibility)
        {
            return visibility == NarrativeEventVisibility.Hidden || visibility == NarrativeEventVisibility.Secret || visibility == NarrativeEventVisibility.Diagnostic;
        }

        private static bool IsHidden(NarrativeStateVisibility visibility)
        {
            return visibility == NarrativeStateVisibility.Hidden || visibility == NarrativeStateVisibility.Secret || visibility == NarrativeStateVisibility.Diagnostic;
        }

        private static NarrativeRecoveryIssue Recoverable(NarrativeRecoveryIssueKind kind, string runtime, string sourceId, string message)
        {
            return new NarrativeRecoveryIssue { Kind = kind, SourceRuntime = runtime, SourceId = sourceId, Message = message, Recoverable = true };
        }

        private static NarrativeRecoveryIssue NonRecoverable(NarrativeRecoveryIssueKind kind, string runtime, string sourceId, string message)
        {
            return new NarrativeRecoveryIssue { Kind = kind, SourceRuntime = runtime, SourceId = sourceId, Message = message, Recoverable = false };
        }

        private static string BuildFingerprint(Step15NarrativePersistenceSnapshot snapshot, IReadOnlyDictionary<string, int> counts)
        {
            string ids = string.Join("|", new[]
            {
                string.Join(",", (snapshot.Quests?.quests ?? new List<QuestRecordData>()).Select(value => value.questId).OrderBy(value => value, StringComparer.Ordinal)),
                string.Join(",", (snapshot.Participation?.assignments ?? new List<QuestAssignmentRecordData>()).Select(value => value.assignmentId).OrderBy(value => value, StringComparer.Ordinal)),
                string.Join(",", (snapshot.Objectives?.objectives ?? new List<QuestObjectiveRecordData>()).Select(value => value.objectiveId).OrderBy(value => value, StringComparer.Ordinal)),
                string.Join(",", (snapshot.Conversations?.conversations ?? new List<ConversationRecordData>()).Select(value => value.conversationId).OrderBy(value => value, StringComparer.Ordinal)),
                string.Join(",", (snapshot.NarrativeArcs?.arcs ?? new List<NarrativeArcRecordData>()).Select(value => value.narrativeArcId).OrderBy(value => value, StringComparer.Ordinal))
            });
            string countText = string.Join(";", counts.OrderBy(value => value.Key, StringComparer.Ordinal).Select(value => $"{value.Key}={value.Value}"));
            return $"{N(snapshot.WorldId)}:{snapshot.SaveWorldTime:0.###}:{countText}:{ids}".GetHashCode().ToString("X8");
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }
}
