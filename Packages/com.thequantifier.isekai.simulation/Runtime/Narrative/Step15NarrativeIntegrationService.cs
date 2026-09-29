using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Narrative
{
    public sealed class Step15NarrativeIntegrationService
    {
        public const int DefaultQueryLimit = 100;
        public const int MaxQueryLimit = 500;
        public const int DefaultCascadeBudget = 32;

        private readonly Step15NarrativeHistoricalService historicalService;

        public Step15NarrativeIntegrationService(Step15NarrativeHistoricalService historical = null)
        {
            historicalService = historical ?? new Step15NarrativeHistoricalService();
        }

        public Step15NarrativeReadinessSnapshot BuildReadiness(
            Step15NarrativePersistenceSnapshot snapshot,
            Step15NarrativeSceneBindingSummary sceneBindings = null,
            string expectedWorldId = PersistenceService.LocalWorldId)
        {
            snapshot = snapshot?.Clone() ?? new Step15NarrativePersistenceSnapshot();
            Step15NarrativeIntegrationValidationReport validation = ValidateFinalization(snapshot, sceneBindings, expectedWorldId);
            Step15NarrativePersistenceManifest manifest = historicalService.BuildManifest(snapshot, validation.Succeeded ? Step15NarrativeReadinessState.Ready : Step15NarrativeReadinessState.Failed);
            Step15NarrativeRuntimeReadiness[] runtimes =
            {
                Runtime("QuestRuntime", nameof(QuestRuntime), QuestRuntimePersistenceParticipant.Key, Step15NarrativeRuntimeRequirement.Required, snapshot.Quests != null, snapshot.Quests?.schemaVersion ?? 0, snapshot.Quests?.quests?.Count ?? 0),
                Runtime("QuestParticipationRuntime", nameof(QuestParticipationRuntime), QuestParticipationRuntimePersistenceParticipant.Key, Step15NarrativeRuntimeRequirement.Required, snapshot.Participation != null, snapshot.Participation?.schemaVersion ?? 0, (snapshot.Participation?.offers?.Count ?? 0) + (snapshot.Participation?.assignments?.Count ?? 0)),
                Runtime("QuestObjectiveProgressRuntime", nameof(QuestObjectiveProgressRuntime), QuestObjectiveProgressPersistenceParticipant.Key, Step15NarrativeRuntimeRequirement.Required, snapshot.Objectives != null, snapshot.Objectives?.schemaVersion ?? 0, snapshot.Objectives?.objectives?.Count ?? 0),
                Runtime("QuestOutcomeRuntime", nameof(QuestOutcomeRuntime), QuestOutcomePersistenceParticipant.Key, Step15NarrativeRuntimeRequirement.Required, snapshot.Outcomes != null, snapshot.Outcomes?.schemaVersion ?? 0, (snapshot.Outcomes?.terminalOutcomes?.Count ?? 0) + (snapshot.Outcomes?.rewardEntitlements?.Count ?? 0)),
                Runtime("QuestSourceRuntime", nameof(QuestSourceRuntime), QuestSourcePersistenceParticipant.Key, Step15NarrativeRuntimeRequirement.Required, snapshot.Sources != null, snapshot.Sources?.schemaVersion ?? 0, (snapshot.Sources?.sources?.Count ?? 0) + (snapshot.Sources?.listings?.Count ?? 0)),
                Runtime("ConversationRuntime", nameof(ConversationRuntime), ConversationPersistenceParticipant.Key, Step15NarrativeRuntimeRequirement.Required, snapshot.Conversations != null, snapshot.Conversations?.schemaVersion ?? 0, snapshot.Conversations?.conversations?.Count ?? 0),
                Runtime("DialogueFlowRuntime", nameof(DialogueFlowRuntime), DialogueFlowPersistenceParticipant.Key, Step15NarrativeRuntimeRequirement.Required, snapshot.DialogueFlows != null, snapshot.DialogueFlows?.schemaVersion ?? 0, snapshot.DialogueFlows?.flows?.Count ?? 0),
                Runtime("NarrativeEventRuntime", nameof(NarrativeEventRuntime), NarrativeEventPersistenceParticipant.Key, Step15NarrativeRuntimeRequirement.Required, snapshot.NarrativeEvents != null, snapshot.NarrativeEvents?.schemaVersion ?? 0, (snapshot.NarrativeEvents?.events?.Count ?? 0) + (snapshot.NarrativeEvents?.signals?.Count ?? 0)),
                Runtime("NarrativeStateRuntime", nameof(NarrativeStateRuntime), NarrativeStatePersistenceParticipant.Key, Step15NarrativeRuntimeRequirement.Required, snapshot.NarrativeStates != null, snapshot.NarrativeStates?.schemaVersion ?? 0, snapshot.NarrativeStates?.states?.Length ?? 0),
                Runtime("NarrativeArcRuntime", nameof(NarrativeArcRuntime), NarrativeArcPersistenceParticipant.Key, Step15NarrativeRuntimeRequirement.Required, snapshot.NarrativeArcs != null, snapshot.NarrativeArcs?.schemaVersion ?? 0, snapshot.NarrativeArcs?.arcs?.Count ?? 0),
                Runtime("HistoricalQueryService", nameof(Step15NarrativeHistoricalService), string.Empty, Step15NarrativeRuntimeRequirement.Derived, true, 1, manifest.RecordCounts.Values.Sum(), "Derived from owner runtime save records."),
                Runtime("SceneBindingState", "WorldLocations.SceneBinding", string.Empty, Step15NarrativeRuntimeRequirement.Optional, sceneBindings == null || !sceneBindings.LoadedSceneRequired, 1, sceneBindings == null ? 0 : sceneBindings.LocationBindingCount + sceneBindings.InteractionPointBindingCount + sceneBindings.QuestSourceBindingCount, sceneBindings == null ? "No loaded scene binding summary supplied; core Step 15 remains scene-independent." : "Scene binding summary supplied.")
            };

            Step15NarrativeReadinessState state = runtimes.All(value => value.RequiredAndReady) && validation.Succeeded
                ? Step15NarrativeReadinessState.Ready
                : Step15NarrativeReadinessState.Failed;
            return new Step15NarrativeReadinessSnapshot(state, runtimes, validation, manifest, sceneBindings);
        }

        public Step15NarrativeIntegrationValidationReport ValidateFinalization(
            Step15NarrativePersistenceSnapshot snapshot,
            Step15NarrativeSceneBindingSummary sceneBindings = null,
            string expectedWorldId = PersistenceService.LocalWorldId)
        {
            snapshot = snapshot?.Clone() ?? new Step15NarrativePersistenceSnapshot();
            List<Step15NarrativeValidationIssue> issues = new List<Step15NarrativeValidationIssue>();

            Step15NarrativeValidationReport historicalValidation = historicalService.Validate(snapshot, expectedWorldId);
            foreach (string error in historicalValidation.Errors)
            {
                issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Persistence, nameof(Step15NarrativeHistoricalService), string.Empty, error));
            }

            foreach (NarrativeRecoveryIssue recovery in historicalValidation.RecoveryIssues)
            {
                issues.Add(Issue(recovery.Recoverable ? Step15NarrativeValidationSeverity.Warning : Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Persistence, recovery.SourceRuntime, recovery.SourceId, recovery.Message));
            }

            ValidateRequiredParticipants(snapshot, issues);
            ValidateOwnership(issues);
            ValidateParticipationObjectiveOutcomeGraph(snapshot, issues);
            ValidateSourceGraph(snapshot, issues);
            ValidateDialogueGraph(snapshot, issues);
            ValidateNarrativeEventGraph(snapshot, issues);
            ValidateNarrativeStateGraph(snapshot, issues);
            ValidateArcGraph(snapshot, issues);
            ValidateSceneBindings(sceneBindings, issues);

            return new Step15NarrativeIntegrationValidationReport(issues);
        }

        public Step15NarrativeContextSnapshot BuildNarrativeContext(Step15NarrativePersistenceSnapshot snapshot, Step15NarrativeContextQuery query)
        {
            snapshot = snapshot?.Clone() ?? new Step15NarrativePersistenceSnapshot();
            query ??= new Step15NarrativeContextQuery();
            int limit = ClampLimit(query.Limit);
            string personId = N(string.IsNullOrWhiteSpace(query.PersonId) ? query.RequesterPersonId : query.PersonId);
            string locationId = N(query.LocationId);
            string organizationId = N(query.OrganizationId);
            double time = query.WorldTime == double.MaxValue ? snapshot.SaveWorldTime : query.WorldTime;
            NarrativeHistoricalAccessMode access = query.AccessMode;

            List<Step15NarrativeContextEntry> offers = (snapshot.Participation?.offers ?? new List<QuestOfferRecordData>())
                .Where(value => value.createdWorldTime <= time)
                .Where(value => string.IsNullOrWhiteSpace(personId) || string.Equals(value.recipient?.recipientId, personId, StringComparison.Ordinal))
                .Where(value => OfferActive(value, time))
                .Where(value => QuestVisible(value.visibility, access))
                .OrderBy(value => value.createdWorldTime).ThenBy(value => value.offerId, StringComparer.Ordinal)
                .Take(limit)
                .Select(value => Entry("QuestOffer", value.offerId, value.questId, nameof(QuestParticipationRuntime), value.lifecycleState.ToString(), false, "Visible quest offer."))
                .ToList();

            List<QuestAssignmentRecordData> assignmentRecords = (snapshot.Participation?.assignments ?? new List<QuestAssignmentRecordData>())
                .Where(value => string.IsNullOrWhiteSpace(personId) || string.Equals(value.assigneePersonId, personId, StringComparison.Ordinal))
                .Where(value => AssignmentActive(value, time))
                .Where(value => QuestVisible(value.visibility, access))
                .OrderBy(value => value.assignedWorldTime).ThenBy(value => value.assignmentId, StringComparer.Ordinal)
                .Take(limit)
                .ToList();

            HashSet<string> activeAssignmentIds = assignmentRecords.Select(value => value.assignmentId).ToHashSet(StringComparer.Ordinal);
            List<Step15NarrativeContextEntry> assignments = assignmentRecords
                .Select(value => Entry("QuestAssignment", value.assignmentId, value.questId, nameof(QuestParticipationRuntime), value.lifecycleState.ToString(), false, "Active quest assignment."))
                .ToList();

            List<QuestObjectiveRecordData> objectiveRecords = (snapshot.Objectives?.objectives ?? new List<QuestObjectiveRecordData>())
                .Where(value => activeAssignmentIds.Contains(value.assignmentId))
                .Where(value => ObjectiveVisible(value.visibility, access))
                .OrderBy(value => value.activatedWorldTime).ThenBy(value => value.objectiveId, StringComparer.Ordinal)
                .Take(limit)
                .ToList();

            List<Step15NarrativeContextEntry> objectives = objectiveRecords
                .Select(value => Entry("QuestObjective", value.objectiveId, value.assignmentId, nameof(QuestObjectiveProgressRuntime), value.lifecycleState.ToString(), false, $"{value.currentValue}/{value.targetValue}"))
                .ToList();

            HashSet<string> assignmentsWithOutcomes = (snapshot.Outcomes?.terminalOutcomes ?? new List<QuestTerminalOutcomeRecordData>())
                .Where(value => value.worldTime <= time)
                .Select(value => value.assignmentId)
                .ToHashSet(StringComparer.Ordinal);
            List<Step15NarrativeContextEntry> turnIns = objectiveRecords
                .GroupBy(value => value.assignmentId, StringComparer.Ordinal)
                .Where(group => !assignmentsWithOutcomes.Contains(group.Key) && group.All(value => value.satisfied || value.lifecycleState == QuestObjectiveLifecycleState.Satisfied))
                .Select(group => assignmentRecords.FirstOrDefault(value => string.Equals(value.assignmentId, group.Key, StringComparison.Ordinal)))
                .Where(value => value != null)
                .OrderBy(value => value.assignedWorldTime).ThenBy(value => value.assignmentId, StringComparer.Ordinal)
                .Take(limit)
                .Select(value => Entry("TurnInReadyQuest", value.assignmentId, value.questId, nameof(QuestOutcomeRuntime), "Ready", false, "Required objectives are satisfied and no terminal outcome exists."))
                .ToList();

            List<Step15NarrativeContextEntry> rewards = (snapshot.Outcomes?.rewardEntitlements ?? new List<QuestRewardEntitlementRecordData>())
                .Where(value => value.createdWorldTime <= time)
                .Where(value => string.IsNullOrWhiteSpace(personId) || string.Equals(value.recipientPersonId, personId, StringComparison.Ordinal))
                .Where(value => value.state == QuestRewardEntitlementState.Claimable)
                .Where(value => !value.hidden || access == NarrativeHistoricalAccessMode.Development)
                .OrderBy(value => value.createdWorldTime).ThenBy(value => value.entitlementId, StringComparer.Ordinal)
                .Take(limit)
                .Select(value => Entry("ClaimableReward", value.entitlementId, value.questId, nameof(QuestOutcomeRuntime), value.state.ToString(), false, $"{value.category}:{value.quantity}"))
                .ToList();

            List<Step15NarrativeContextEntry> sources = (snapshot.Sources?.sources ?? new List<QuestSourceRecordData>())
                .Where(value => value.lifecycleState == QuestSourceLifecycleState.Active)
                .Where(value => string.IsNullOrWhiteSpace(locationId) || string.Equals(value.hostLocationId, locationId, StringComparison.Ordinal))
                .Where(value => string.IsNullOrWhiteSpace(organizationId) || string.Equals(value.operatingOrganizationId, organizationId, StringComparison.Ordinal))
                .Where(value => SourceVisible(value.visibility, access))
                .OrderBy(value => value.questSourceId, StringComparer.Ordinal)
                .Take(limit)
                .Select(value => Entry("QuestSource", value.questSourceId, value.interactionPointId, nameof(QuestSourceRuntime), value.lifecycleState.ToString(), false, "Available quest source."))
                .ToList();

            List<Step15NarrativeContextEntry> conversations = (snapshot.Conversations?.conversations ?? new List<ConversationRecordData>())
                .Where(value => value.startedWorldTime <= time)
                .Where(value => value.lifecycleState == ConversationLifecycleState.Active)
                .Where(value => ConversationIncludes(value, personId))
                .Where(value => string.IsNullOrWhiteSpace(locationId) || string.Equals(value.hostLocationId, locationId, StringComparison.Ordinal))
                .Where(value => ConversationVisible(value.visibility, access))
                .OrderBy(value => value.startedWorldTime).ThenBy(value => value.conversationId, StringComparer.Ordinal)
                .Take(limit)
                .Select(value => Entry("Conversation", value.conversationId, value.questId, nameof(ConversationRuntime), value.lifecycleState.ToString(), false, "Active conversation."))
                .ToList();

            HashSet<string> conversationIds = conversations.Select(value => value.PrimaryId).ToHashSet(StringComparer.Ordinal);
            List<Step15NarrativeContextEntry> dialogue = (snapshot.DialogueFlows?.flows ?? new List<DialogueFlowRecordData>())
                .Where(value => conversationIds.Contains(value.conversationId))
                .OrderBy(value => value.flowId, StringComparer.Ordinal)
                .Take(limit)
                .Select(value => Entry("DialogueNode", value.currentNodeId, value.flowId, nameof(DialogueFlowRuntime), value.state.ToString(), false, "Current dialogue node."))
                .ToList();

            List<Step15NarrativeContextEntry> states = (snapshot.NarrativeStates?.states ?? Array.Empty<NarrativeStateRecordData>())
                .Where(value => value.lifecycle == NarrativeStateLifecycle.Active)
                .Where(value => string.IsNullOrWhiteSpace(personId) || value.scope != NarrativeStateScope.Person || string.Equals(value.scopeKey, personId, StringComparison.Ordinal))
                .OrderBy(value => value.stateDefinitionId, StringComparer.Ordinal).ThenBy(value => value.narrativeStateId, StringComparer.Ordinal)
                .Take(limit)
                .Select(value => Entry("NarrativeState", value.narrativeStateId, value.stateDefinitionId, nameof(NarrativeStateRuntime), value.lifecycle.ToString(), false, $"{value.variables?.Length ?? 0} variable(s)."))
                .ToList();

            List<Step15NarrativeContextEntry> arcStages = (snapshot.NarrativeArcs?.arcs ?? new List<NarrativeArcRecordData>())
                .Where(value => value.lifecycle == NarrativeArcLifecycle.Active || value.lifecycle == NarrativeArcLifecycle.Completed)
                .Where(value => string.IsNullOrWhiteSpace(personId) || string.Equals(value.actorPersonId, personId, StringComparison.Ordinal) || string.Equals(value.subjectId, personId, StringComparison.Ordinal))
                .SelectMany(arc => (arc.stages ?? Array.Empty<NarrativeArcStageRecordData>())
                    .Where(stage => stage.lifecycle == NarrativeArcStageLifecycle.Active)
                    .Select(stage => Entry("NarrativeArcStage", stage.stageRuntimeId, arc.narrativeArcId, nameof(NarrativeArcRuntime), stage.lifecycle.ToString(), false, stage.stageDefinitionId)))
                .OrderBy(value => value.RelatedId, StringComparer.Ordinal).ThenBy(value => value.PrimaryId, StringComparer.Ordinal)
                .Take(limit)
                .ToList();

            NarrativeTimelinePage recentPage = historicalService.QueryTimeline(snapshot, new NarrativeTimelineQuery
            {
                AccessMode = access,
                RequesterPersonId = query.RequesterPersonId,
                PersonId = personId,
                EndWorldTime = time,
                Limit = MaxQueryLimit
            });
            List<Step15NarrativeContextEntry> recentEvents = recentPage.Entries
                .Where(value => TimelineMatchesContext(snapshot, value, locationId, organizationId))
                .OrderByDescending(value => value.WorldTime)
                .ThenByDescending(value => value.Sequence)
                .Take(limit)
                .Select(value => Entry("RecentNarrativeTimeline", value.SourceId, value.QuestId, value.SourceRuntime, value.Category.ToString(), false, value.Cursor))
                .ToList();

            List<Step15NarrativeContextEntry> locationContext = sources
                .Select(value => Entry("LocationInstitutionContext", value.PrimaryId, value.RelatedId, value.OwnerRuntime, value.State, false, $"Location={locationId}; Organization={organizationId}"))
                .ToList();

            return new Step15NarrativeContextSnapshot(personId, locationId, organizationId, offers, assignments, objectives, turnIns, rewards, sources, conversations, dialogue, states, arcStages, recentEvents, locationContext);
        }

        public Step15NarrativeAuthoringContract BuildStep16AuthoringContract()
        {
            return new Step15NarrativeAuthoringContract(new[]
            {
                Contract("quests", nameof(QuestRuntime), new[] { "Quest definitions", "issuers", "recipients", "origins", "subject links", "visibility", "repeatability" }, new[] { "stable QuestDefinitionId", "no display-name identity", "single QuestRuntime ownership" }),
                Contract("quest-sources", nameof(QuestSourceRuntime), new[] { "Quest boards", "guild counters", "mayor desks", "hidden sources", "listing publication rules" }, new[] { "scene binding is presentation-only", "listings never own assignments" }),
                Contract("objectives", nameof(QuestObjectiveProgressRuntime), new[] { "required objectives", "optional objectives", "hidden objectives", "event-driven progress", "current-state reconciliation" }, new[] { "no per-frame whole-quest scan", "source-event idempotence" }),
                Contract("outcomes-rewards", nameof(QuestOutcomeRuntime), new[] { "completion policies", "failure policies", "deadlines", "reward packages", "claim rules" }, new[] { "terminal outcome before reward entitlement", "owner-runtime reward delegation", "restore never replays grants" }),
                Contract("conversation-dialogue", $"{nameof(ConversationRuntime)} + {nameof(DialogueFlowRuntime)}", new[] { "conversation providers", "participants", "dialogue nodes", "choices", "conditions", "typed effects" }, new[] { "conversation identity separate from flow", "effects route through owner runtimes" }),
                Contract("narrative-events", nameof(NarrativeEventRuntime), new[] { "triggers", "conditions", "typed actions", "signals", "cascade budgets" }, new[] { "no reflection/delegate actions", "bounded cascades", "required action failures are explicit" }),
                Contract("narrative-state", nameof(NarrativeStateRuntime), new[] { "typed state variables", "branches", "transitions", "consequences" }, new[] { "state does not duplicate domain owners", "truth/knowledge/belief source is explicit" }),
                Contract("narrative-arcs", nameof(NarrativeArcRuntime), new[] { "arc stages", "quest bindings", "dependencies", "branch convergence", "recovery paths" }, new[] { "arcs coordinate; they do not own quest/state records", "systemic bypasses can satisfy dependencies" }),
                Contract("history-persistence", nameof(Step15NarrativeHistoricalService), new[] { "timeline queries", "historical snapshots", "restore manifests", "recovery diagnostics" }, new[] { "owner payloads restore before derived indexes", "hidden entries redact in player-safe queries" })
            });
        }

        private static Step15NarrativeRuntimeReadiness Runtime(string componentId, string ownerRuntime, string participantKey, Step15NarrativeRuntimeRequirement requirement, bool present, int schema, int count, string diagnostics = null)
        {
            return new Step15NarrativeRuntimeReadiness(
                componentId,
                ownerRuntime,
                participantKey,
                requirement,
                present ? Step15NarrativeReadinessState.Ready : Step15NarrativeReadinessState.Uninitialized,
                schema,
                count,
                diagnostics ?? (present ? "Ready." : "Required runtime save payload is missing."));
        }

        private void ValidateRequiredParticipants(Step15NarrativePersistenceSnapshot snapshot, List<Step15NarrativeValidationIssue> issues)
        {
            if (snapshot.Quests == null) RequiredMissing(nameof(QuestRuntime), QuestRuntimePersistenceParticipant.Key, issues);
            if (snapshot.Participation == null) RequiredMissing(nameof(QuestParticipationRuntime), QuestParticipationRuntimePersistenceParticipant.Key, issues);
            if (snapshot.Objectives == null) RequiredMissing(nameof(QuestObjectiveProgressRuntime), QuestObjectiveProgressPersistenceParticipant.Key, issues);
            if (snapshot.Outcomes == null) RequiredMissing(nameof(QuestOutcomeRuntime), QuestOutcomePersistenceParticipant.Key, issues);
            if (snapshot.Sources == null) RequiredMissing(nameof(QuestSourceRuntime), QuestSourcePersistenceParticipant.Key, issues);
            if (snapshot.Conversations == null) RequiredMissing(nameof(ConversationRuntime), ConversationPersistenceParticipant.Key, issues);
            if (snapshot.DialogueFlows == null) RequiredMissing(nameof(DialogueFlowRuntime), DialogueFlowPersistenceParticipant.Key, issues);
            if (snapshot.NarrativeEvents == null) RequiredMissing(nameof(NarrativeEventRuntime), NarrativeEventPersistenceParticipant.Key, issues);
            if (snapshot.NarrativeStates == null) RequiredMissing(nameof(NarrativeStateRuntime), NarrativeStatePersistenceParticipant.Key, issues);
            if (snapshot.NarrativeArcs == null) RequiredMissing(nameof(NarrativeArcRuntime), NarrativeArcPersistenceParticipant.Key, issues);
        }

        private void ValidateOwnership(List<Step15NarrativeValidationIssue> issues)
        {
            Step15NarrativeOwnershipEntry[] owners = historicalService.OwnershipMap.Where(value => !value.Derived).ToArray();
            foreach (IGrouping<string, Step15NarrativeOwnershipEntry> duplicate in owners.GroupBy(value => value.Category, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Ownership, nameof(Step15NarrativeHistoricalService), duplicate.Key, "Step 15 ownership category has more than one authoritative owner."));
            }

            foreach (Step15NarrativeOwnershipEntry entry in owners.Where(value => string.IsNullOrWhiteSpace(value.AuthoritativeOwner) || string.IsNullOrWhiteSpace(value.ParticipantKey)))
            {
                issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Ownership, nameof(Step15NarrativeHistoricalService), entry.Category, "Authoritative Step 15 ownership entries must declare owner runtime and persistence participant."));
            }
        }

        private static void ValidateParticipationObjectiveOutcomeGraph(Step15NarrativePersistenceSnapshot snapshot, List<Step15NarrativeValidationIssue> issues)
        {
            Dictionary<string, QuestAssignmentRecordData> assignments = (snapshot.Participation?.assignments ?? new List<QuestAssignmentRecordData>()).Where(value => value != null).GroupBy(value => value.assignmentId, StringComparer.Ordinal).ToDictionary(value => value.Key, value => value.First(), StringComparer.Ordinal);
            Dictionary<string, QuestOfferRecordData> offers = (snapshot.Participation?.offers ?? new List<QuestOfferRecordData>()).Where(value => value != null).GroupBy(value => value.offerId, StringComparer.Ordinal).ToDictionary(value => value.Key, value => value.First(), StringComparer.Ordinal);
            ILookup<string, QuestObjectiveRecordData> objectivesByAssignment = (snapshot.Objectives?.objectives ?? new List<QuestObjectiveRecordData>()).Where(value => value != null).ToLookup(value => value.assignmentId, StringComparer.Ordinal);
            ILookup<string, QuestObjectiveRuntimeEventData> objectiveEventsByObjective = (snapshot.Objectives?.events ?? new List<QuestObjectiveRuntimeEventData>()).Where(value => value != null).ToLookup(value => value.objectiveId, StringComparer.Ordinal);
            ILookup<string, QuestTerminalOutcomeRecordData> outcomesByAssignment = (snapshot.Outcomes?.terminalOutcomes ?? new List<QuestTerminalOutcomeRecordData>()).Where(value => value != null).ToLookup(value => value.assignmentId, StringComparer.Ordinal);

            foreach (QuestOfferRecordData accepted in offers.Values.Where(value => value.lifecycleState == QuestOfferLifecycleState.Accepted))
            {
                bool hasAssignment = assignments.Values.Any(value => string.Equals(value.offerId, accepted.offerId, StringComparison.Ordinal) && string.Equals(value.questId, accepted.questId, StringComparison.Ordinal));
                if (!hasAssignment)
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.QuestParticipation, nameof(QuestParticipationRuntime), accepted.offerId, "Accepted quest offer has no matching assignment."));
                }
            }

            foreach (QuestAssignmentRecordData assignment in assignments.Values.Where(AssignmentCurrent))
            {
                if (!objectivesByAssignment[assignment.assignmentId].Any())
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Objectives, nameof(QuestObjectiveProgressRuntime), assignment.assignmentId, "Current quest assignment has no objective records."));
                }
            }

            foreach (QuestObjectiveRecordData objective in objectivesByAssignment.SelectMany(value => value))
            {
                if (assignments.TryGetValue(objective.assignmentId, out QuestAssignmentRecordData assignment) && !string.Equals(assignment.questId, objective.questId, StringComparison.Ordinal))
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Objectives, nameof(QuestObjectiveProgressRuntime), objective.objectiveId, "Quest objective assignment and quest references disagree."));
                }
            }

            foreach (IGrouping<string, QuestTerminalOutcomeRecordData> group in outcomesByAssignment)
            {
                if (!string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Outcomes, nameof(QuestOutcomeRuntime), group.Key, "Quest assignment has more than one terminal outcome."));
                }
            }

            foreach (QuestTerminalOutcomeRecordData outcome in outcomesByAssignment.SelectMany(value => value))
            {
                QuestObjectiveRecordData[] objectives = objectivesByAssignment[outcome.assignmentId].ToArray();
                if (outcome.outcomeKind == QuestTerminalOutcomeKind.Completed && (objectives.Length == 0 || objectives.Any(value => !ObjectiveSatisfied(value, objectiveEventsByObjective[value.objectiveId]))))
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Outcomes, nameof(QuestOutcomeRuntime), outcome.terminalOutcomeId, "Completed quest outcome was recorded from incomplete objective state."));
                }

                if (assignments.TryGetValue(outcome.assignmentId, out QuestAssignmentRecordData assignment) && AssignmentCurrent(assignment))
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.QuestParticipation, nameof(QuestParticipationRuntime), assignment.assignmentId, "Quest assignment remains current after terminal outcome; finalization requires historical/terminal participation state."));
                }
            }

            foreach (IGrouping<string, QuestRewardEntitlementRecordData> duplicate in (snapshot.Outcomes?.rewardEntitlements ?? new List<QuestRewardEntitlementRecordData>()).Where(value => value != null).GroupBy(value => value.entitlementId, StringComparer.Ordinal).Where(value => !string.IsNullOrWhiteSpace(value.Key) && value.Count() > 1))
            {
                issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Rewards, nameof(QuestOutcomeRuntime), duplicate.Key, "Duplicate reward entitlement ID would duplicate reward delivery."));
            }

            foreach (QuestRewardGrantRecordData grant in snapshot.Outcomes?.rewardGrants ?? new List<QuestRewardGrantRecordData>())
            {
                if ((snapshot.Outcomes?.rewardGrants ?? new List<QuestRewardGrantRecordData>()).Count(value => string.Equals(value.entitlementId, grant.entitlementId, StringComparison.Ordinal) && value.state == QuestRewardGrantState.Granted) > 1)
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Rewards, nameof(QuestOutcomeRuntime), grant.entitlementId, "Reward entitlement has multiple granted records."));
                }
            }
        }

        private static void ValidateSourceGraph(Step15NarrativePersistenceSnapshot snapshot, List<Step15NarrativeValidationIssue> issues)
        {
            HashSet<string> assignments = (snapshot.Participation?.assignments ?? new List<QuestAssignmentRecordData>()).Select(value => value.assignmentId).ToHashSet(StringComparer.Ordinal);
            foreach (QuestListingRecordData listing in snapshot.Sources?.listings ?? new List<QuestListingRecordData>())
            {
                if (listing.lifecycleState == QuestListingLifecycleState.Claimed && string.IsNullOrWhiteSpace(listing.claimedAssignmentId))
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Warning, Step15NarrativeValidationCategory.Sources, nameof(QuestSourceRuntime), listing.questListingId, "Claimed quest listing should record the assignment that claimed it."));
                }

                if (!string.IsNullOrWhiteSpace(listing.claimedAssignmentId) && !assignments.Contains(listing.claimedAssignmentId))
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Sources, nameof(QuestSourceRuntime), listing.questListingId, "Quest listing references missing claimed assignment."));
                }
            }
        }

        private static void ValidateDialogueGraph(Step15NarrativePersistenceSnapshot snapshot, List<Step15NarrativeValidationIssue> issues)
        {
            HashSet<string> conversations = (snapshot.Conversations?.conversations ?? new List<ConversationRecordData>()).Select(value => value.conversationId).ToHashSet(StringComparer.Ordinal);
            foreach (DialogueFlowRecordData flow in snapshot.DialogueFlows?.flows ?? new List<DialogueFlowRecordData>())
            {
                if (!conversations.Contains(flow.conversationId))
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Dialogue, nameof(DialogueFlowRuntime), flow.flowId, "Dialogue flow has no owning conversation."));
                }

                if (!string.IsNullOrWhiteSpace(flow.currentNodeId) && (flow.visits == null || flow.visits.All(value => !string.Equals(value.nodeId, flow.currentNodeId, StringComparison.Ordinal))))
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Warning, Step15NarrativeValidationCategory.Dialogue, nameof(DialogueFlowRuntime), flow.flowId, "Dialogue flow current node lacks a matching visit history record."));
                }
            }
        }

        private static void ValidateNarrativeEventGraph(Step15NarrativePersistenceSnapshot snapshot, List<Step15NarrativeValidationIssue> issues)
        {
            foreach (NarrativeEventRecordData evt in snapshot.NarrativeEvents?.events ?? new List<NarrativeEventRecordData>())
            {
                if (evt.cascadeDepth > DefaultCascadeBudget)
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Performance, nameof(NarrativeEventRuntime), evt.narrativeEventId, $"Narrative event cascade depth {evt.cascadeDepth} exceeds budget {DefaultCascadeBudget}."));
                }

                foreach (NarrativeActionExecutionRecordData action in evt.actionExecutions ?? Array.Empty<NarrativeActionExecutionRecordData>())
                {
                    if (action.category == NarrativeActionCategory.Unknown)
                    {
                        issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.NarrativeEvents, nameof(NarrativeEventRuntime), action.actionExecutionId, "Narrative action execution uses an unknown/untyped action category."));
                    }

                    if (LooksLikeArbitraryExecution(action.targetOwnerRuntime) || LooksLikeArbitraryExecution(action.externalResultId) || LooksLikeArbitraryExecution(action.resultValue))
                    {
                        issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.NarrativeEvents, nameof(NarrativeEventRuntime), action.actionExecutionId, "Narrative action execution contains reflection/delegate-like arbitrary execution text."));
                    }
                }
            }
        }

        private static void ValidateNarrativeStateGraph(Step15NarrativePersistenceSnapshot snapshot, List<Step15NarrativeValidationIssue> issues)
        {
            string[] domainStatePrefixes =
            {
                "quest.", "assignment.", "membership.", "rank.", "government.", "war.", "location.", "reputation.", "permit.", "inventory.", "item."
            };

            foreach (NarrativeStateRecordData state in snapshot.NarrativeStates?.states ?? Array.Empty<NarrativeStateRecordData>())
            {
                foreach (NarrativeStateVariableRecordData variable in state.variables ?? Array.Empty<NarrativeStateVariableRecordData>())
                {
                    string id = N(variable.variableDefinitionId).ToLowerInvariant();
                    if (domainStatePrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal)))
                    {
                        issues.Add(Issue(Step15NarrativeValidationSeverity.Warning, Step15NarrativeValidationCategory.NarrativeState, nameof(NarrativeStateRuntime), state.narrativeStateId, $"Narrative variable '{variable.variableDefinitionId}' looks like duplicated domain state; prefer authoritative owner conditions."));
                    }
                }
            }
        }

        private static void ValidateArcGraph(Step15NarrativePersistenceSnapshot snapshot, List<Step15NarrativeValidationIssue> issues)
        {
            HashSet<string> questIds = (snapshot.Quests?.quests ?? new List<QuestRecordData>()).Select(value => value.questId).ToHashSet(StringComparer.Ordinal);
            foreach (NarrativeArcRecordData arc in snapshot.NarrativeArcs?.arcs ?? new List<NarrativeArcRecordData>())
            {
                if (arc.processedSignalKeys != null && arc.processedSignalKeys.Length != arc.processedSignalKeys.Distinct(StringComparer.Ordinal).Count())
                {
                    issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.NarrativeArcs, nameof(NarrativeArcRuntime), arc.narrativeArcId, "Narrative arc processed signal keys are not unique."));
                }

                foreach (NarrativeArcStageRecordData stage in arc.stages ?? Array.Empty<NarrativeArcStageRecordData>())
                {
                    if ((stage.actionExecutions ?? Array.Empty<NarrativeActionExecutionRecordData>()).Count(action => action.lifecycle == NarrativeActionLifecycle.Committed && action.requirement == NarrativeActionRequirement.Required) > DefaultCascadeBudget)
                    {
                        issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Performance, nameof(NarrativeArcRuntime), stage.stageRuntimeId, "Narrative arc stage committed too many required actions in one cascade."));
                    }

                    foreach (NarrativeArcBoundQuestRecordData binding in stage.boundQuests ?? Array.Empty<NarrativeArcBoundQuestRecordData>())
                    {
                        if (!string.IsNullOrWhiteSpace(binding.questId) && !questIds.Contains(binding.questId))
                        {
                            issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.NarrativeArcs, nameof(NarrativeArcRuntime), stage.stageRuntimeId, "Narrative arc stage references a missing bound quest."));
                        }
                    }
                }
            }
        }

        private static void ValidateSceneBindings(Step15NarrativeSceneBindingSummary sceneBindings, List<Step15NarrativeValidationIssue> issues)
        {
            if (sceneBindings == null)
            {
                return;
            }

            if (sceneBindings.LoadedSceneRequired)
            {
                issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.SceneBinding, sceneBindings.SceneId, string.Empty, "Step 15 core readiness cannot require a loaded Unity scene."));
            }

            if (sceneBindings.AuthoritativeMutationBindingCount > 0)
            {
                issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.SceneBinding, sceneBindings.SceneId, string.Empty, "Scene bindings must remain presentation-only and route requests to owner runtimes."));
            }
        }

        private static Step15NarrativeContextEntry Entry(string category, string primaryId, string relatedId, string ownerRuntime, string state, bool redacted, string summary)
        {
            return new Step15NarrativeContextEntry(category, primaryId, relatedId, ownerRuntime, state, redacted, summary);
        }

        private static Step15NarrativeAuthoringContractSection Contract(string sectionId, string ownerRuntime, IEnumerable<string> concepts, IEnumerable<string> validations)
        {
            return new Step15NarrativeAuthoringContractSection(sectionId, ownerRuntime, concepts, validations);
        }

        private static void RequiredMissing(string runtime, string participantKey, List<Step15NarrativeValidationIssue> issues)
        {
            issues.Add(Issue(Step15NarrativeValidationSeverity.Error, Step15NarrativeValidationCategory.Dependency, runtime, participantKey, "Required Step 15 runtime save payload is missing."));
        }

        private static Step15NarrativeValidationIssue Issue(Step15NarrativeValidationSeverity severity, Step15NarrativeValidationCategory category, string runtime, string sourceId, string message)
        {
            return new Step15NarrativeValidationIssue(severity, category, runtime, sourceId, message);
        }

        private static int ClampLimit(int limit) => Math.Max(1, Math.Min(MaxQueryLimit, limit <= 0 ? DefaultQueryLimit : limit));

        private static bool OfferActive(QuestOfferRecordData offer, double time)
        {
            return offer != null && (offer.lifecycleState == QuestOfferLifecycleState.Active || offer.lifecycleState == QuestOfferLifecycleState.Proposed) && (offer.expirationWorldTime < 0d || offer.expirationWorldTime > time);
        }

        private static bool AssignmentActive(QuestAssignmentRecordData assignment, double time)
        {
            return assignment != null && AssignmentCurrent(assignment) && assignment.assignedWorldTime <= time && (assignment.endedWorldTime < 0d || assignment.endedWorldTime > time);
        }

        private static bool AssignmentCurrent(QuestAssignmentRecordData assignment)
        {
            return assignment != null && (assignment.lifecycleState == QuestAssignmentLifecycleState.Assigned || assignment.lifecycleState == QuestAssignmentLifecycleState.Active || assignment.lifecycleState == QuestAssignmentLifecycleState.Resumed || assignment.lifecycleState == QuestAssignmentLifecycleState.Suspended);
        }

        private static bool ObjectiveSatisfied(QuestObjectiveRecordData objective, IEnumerable<QuestObjectiveRuntimeEventData> events)
        {
            return objective != null
                && (objective.satisfied
                    || objective.lifecycleState == QuestObjectiveLifecycleState.Satisfied
                    || (events ?? Array.Empty<QuestObjectiveRuntimeEventData>()).Any(value => value.eventKind == QuestObjectiveEventKind.ObjectiveSatisfied || value.afterState == QuestObjectiveLifecycleState.Satisfied));
        }

        private static bool QuestVisible(QuestVisibility visibility, NarrativeHistoricalAccessMode access)
        {
            return access == NarrativeHistoricalAccessMode.Development || visibility != QuestVisibility.Unknown;
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
            return access == NarrativeHistoricalAccessMode.Development || (visibility != ConversationVisibility.Private && visibility != ConversationVisibility.Secret && visibility != ConversationVisibility.Hidden && visibility != ConversationVisibility.Diagnostic);
        }

        private static bool ConversationIncludes(ConversationRecordData conversation, string personId)
        {
            return conversation != null
                && (string.IsNullOrWhiteSpace(personId)
                    || (conversation.participants ?? Array.Empty<ConversationParticipantRecordData>()).Any(value => string.Equals(value.personId, personId, StringComparison.Ordinal)));
        }

        private static bool TimelineMatchesContext(Step15NarrativePersistenceSnapshot snapshot, NarrativeTimelineEntry entry, string locationId, string organizationId)
        {
            if (entry == null) return false;
            bool requiresLocation = !string.IsNullOrWhiteSpace(locationId);
            bool requiresOrganization = !string.IsNullOrWhiteSpace(organizationId);
            if (!requiresLocation && !requiresOrganization) return true;

            if (MatchesContext(entry.LocationId, locationId, requiresLocation) && MatchesContext(entry.OrganizationId, organizationId, requiresOrganization))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(entry.ConversationId) && ConversationMatchesContext(snapshot, entry.ConversationId, locationId, organizationId, requiresLocation, requiresOrganization))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(entry.QuestId) && QuestMatchesContext(snapshot, entry.QuestId, locationId, organizationId, requiresLocation, requiresOrganization))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(entry.NarrativeEventId))
            {
                NarrativeEventRecordData narrativeEvent = (snapshot.NarrativeEvents?.events ?? new List<NarrativeEventRecordData>())
                    .FirstOrDefault(value => string.Equals(value.narrativeEventId, entry.NarrativeEventId, StringComparison.Ordinal));
                if (narrativeEvent != null)
                {
                    if (MatchesContext(narrativeEvent.locationId, locationId, requiresLocation) && MatchesContext(narrativeEvent.organizationId, organizationId, requiresOrganization))
                    {
                        return true;
                    }

                    if (!string.IsNullOrWhiteSpace(narrativeEvent.conversationId) && ConversationMatchesContext(snapshot, narrativeEvent.conversationId, locationId, organizationId, requiresLocation, requiresOrganization))
                    {
                        return true;
                    }

                    if (!string.IsNullOrWhiteSpace(narrativeEvent.questId) && QuestMatchesContext(snapshot, narrativeEvent.questId, locationId, organizationId, requiresLocation, requiresOrganization))
                    {
                        return true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.NarrativeArcId))
            {
                NarrativeArcRecordData arc = (snapshot.NarrativeArcs?.arcs ?? new List<NarrativeArcRecordData>())
                    .FirstOrDefault(value => string.Equals(value.narrativeArcId, entry.NarrativeArcId, StringComparison.Ordinal));
                if (arc != null)
                {
                    return (arc.stages ?? Array.Empty<NarrativeArcStageRecordData>())
                        .SelectMany(value => value.boundQuests ?? Array.Empty<NarrativeArcBoundQuestRecordData>())
                        .Any(value => QuestMatchesContext(snapshot, value.questId, locationId, organizationId, requiresLocation, requiresOrganization));
                }
            }

            return false;
        }

        private static bool ConversationMatchesContext(Step15NarrativePersistenceSnapshot snapshot, string conversationId, string locationId, string organizationId, bool requiresLocation, bool requiresOrganization)
        {
            ConversationRecordData conversation = (snapshot.Conversations?.conversations ?? new List<ConversationRecordData>())
                .FirstOrDefault(value => string.Equals(value.conversationId, conversationId, StringComparison.Ordinal));
            return conversation != null
                && MatchesContext(conversation.hostLocationId, locationId, requiresLocation)
                && MatchesContext(conversation.operatingOrganizationId, organizationId, requiresOrganization);
        }

        private static bool QuestMatchesContext(Step15NarrativePersistenceSnapshot snapshot, string questId, string locationId, string organizationId, bool requiresLocation, bool requiresOrganization)
        {
            if (string.IsNullOrWhiteSpace(questId)) return false;

            QuestRecordData quest = (snapshot.Quests?.quests ?? new List<QuestRecordData>())
                .FirstOrDefault(value => string.Equals(value.questId, questId, StringComparison.Ordinal));
            if (quest != null && MatchesContext(quest.origin?.locationId, locationId, requiresLocation) && QuestOrganizationMatches(snapshot, questId, organizationId, requiresOrganization))
            {
                return true;
            }

            foreach (QuestListingRecordData listing in (snapshot.Sources?.listings ?? new List<QuestListingRecordData>()).Where(value => string.Equals(value.questId, questId, StringComparison.Ordinal)))
            {
                QuestSourceRecordData source = (snapshot.Sources?.sources ?? new List<QuestSourceRecordData>())
                    .FirstOrDefault(value => string.Equals(value.questSourceId, listing.questSourceId, StringComparison.Ordinal));
                if (source != null && MatchesContext(source.hostLocationId, locationId, requiresLocation) && MatchesContext(source.operatingOrganizationId, organizationId, requiresOrganization))
                {
                    return true;
                }
            }

            return (snapshot.Conversations?.conversations ?? new List<ConversationRecordData>())
                .Where(value => string.Equals(value.questId, questId, StringComparison.Ordinal))
                .Any(value => MatchesContext(value.hostLocationId, locationId, requiresLocation) && MatchesContext(value.operatingOrganizationId, organizationId, requiresOrganization));
        }

        private static bool QuestOrganizationMatches(Step15NarrativePersistenceSnapshot snapshot, string questId, string organizationId, bool requiresOrganization)
        {
            if (!requiresOrganization) return true;

            QuestRecordData quest = (snapshot.Quests?.quests ?? new List<QuestRecordData>())
                .FirstOrDefault(value => string.Equals(value.questId, questId, StringComparison.Ordinal));
            if (quest?.issuer != null && string.Equals(quest.issuer.issuerId, organizationId, StringComparison.Ordinal))
            {
                return true;
            }

            return (snapshot.Sources?.listings ?? new List<QuestListingRecordData>())
                .Where(value => string.Equals(value.questId, questId, StringComparison.Ordinal))
                .Select(value => (snapshot.Sources?.sources ?? new List<QuestSourceRecordData>()).FirstOrDefault(source => string.Equals(source.questSourceId, value.questSourceId, StringComparison.Ordinal)))
                .Where(value => value != null)
                .Any(value => string.Equals(value.operatingOrganizationId, organizationId, StringComparison.Ordinal));
        }

        private static bool MatchesContext(string actual, string expected, bool required)
        {
            return !required || string.Equals(N(actual), N(expected), StringComparison.Ordinal);
        }

        private static bool LooksLikeArbitraryExecution(string value)
        {
            value = N(value).ToLowerInvariant();
            return value.Contains("reflection", StringComparison.Ordinal)
                || value.Contains("delegate", StringComparison.Ordinal)
                || value.Contains("method:", StringComparison.Ordinal)
                || value.Contains("invoke:", StringComparison.Ordinal)
                || value.Contains("system.reflection", StringComparison.Ordinal);
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
