using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Quests
{
    public sealed class QuestOutcomeRuntime : IDisposable
    {
        private readonly Dictionary<string, QuestTerminalOutcomeRecordData> outcomesById = new Dictionary<string, QuestTerminalOutcomeRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> terminalOutcomeByAssignment = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestDeadlineRecordData> deadlinesById = new Dictionary<string, QuestDeadlineRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> deadlinesByAssignment = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestRewardEntitlementRecordData> entitlementsById = new Dictionary<string, QuestRewardEntitlementRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> entitlementsByOutcome = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestRewardGrantRecordData> grantsById = new Dictionary<string, QuestRewardGrantRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestOutcomeTransactionData> transactionsById = new Dictionary<string, QuestOutcomeTransactionData>(StringComparer.Ordinal);
        private readonly List<QuestOutcomeEventData> events = new List<QuestOutcomeEventData>();

        private QuestRuntime questRuntime;
        private QuestParticipationRuntime participationRuntime;
        private QuestObjectiveProgressRuntime objectiveRuntime;
        private DefinitionRegistry registry;
        private IQuestRewardEffectExecutor rewardExecutor;
        private string worldId;
        private long revision;
        private bool disposed;

        public QuestOutcomeRuntime(
            QuestRuntime quests = null,
            QuestParticipationRuntime participation = null,
            QuestObjectiveProgressRuntime objectives = null,
            DefinitionRegistry definitionRegistry = null,
            IQuestRewardEffectExecutor executor = null,
            string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            Configure(quests, participation, objectives, definitionRegistry, executor, runtimeWorldId);
        }

        public long Revision => revision;
        public string WorldId => worldId ?? string.Empty;
        public int TerminalOutcomeCount => outcomesById.Count;
        public int DeadlineCount => deadlinesById.Count;
        public int RewardEntitlementCount => entitlementsById.Count;
        public int RewardGrantCount => grantsById.Count;
        public IReadOnlyList<QuestOutcomeEventData> Events => events.Select(value => value.Clone()).ToArray();

        public void Configure(QuestRuntime quests, QuestParticipationRuntime participation, QuestObjectiveProgressRuntime objectives, DefinitionRegistry definitionRegistry, IQuestRewardEffectExecutor executor = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            questRuntime = quests;
            participationRuntime = participation;
            objectiveRuntime = objectives;
            registry = definitionRegistry;
            rewardExecutor = executor;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId.Trim();
        }

        public QuestOutcomeOperationResult TrackAssignment(QuestAssignmentSnapshot assignment, string transactionId = null, bool preview = false)
        {
            if (disposed) return Fail(QuestOutcomeOperationStatus.Disposed, "Quest outcome runtime is disposed.");
            if (assignment == null || string.IsNullOrWhiteSpace(assignment.AssignmentId)) return Fail(QuestOutcomeOperationStatus.MissingAssignment, "Quest assignment is missing.");
            if (!string.Equals(assignment.WorldId, worldId, StringComparison.Ordinal)) return Fail(QuestOutcomeOperationStatus.WrongWorld, $"Assignment world '{assignment.WorldId}' does not match outcome runtime world '{worldId}'.");
            if (!TryResolveQuestAndDefinition(assignment.QuestId, out QuestSnapshot quest, out QuestDefinition definition, out string failure)) return Fail(QuestOutcomeOperationStatus.MissingQuest, failure);

            QuestDeadlineDefinitionData[] deadlineDefinitions = definition.DeadlineDefinitions.ToArray();
            if (deadlineDefinitions.Length == 0)
            {
                return QuestOutcomeOperationResult.Success("Quest assignment has no deadlines to track.", revision, revision, duplicate: true);
            }

            List<QuestDeadlineRecordData> created = new List<QuestDeadlineRecordData>();
            foreach (QuestDeadlineDefinitionData deadline in deadlineDefinitions)
            {
                string deadlineId = BuildDeadlineId(assignment.AssignmentId, deadline.deadlineDefinitionId);
                if (deadlinesById.ContainsKey(deadlineId)) continue;
                double start = deadline.startKind == QuestDeadlineStartKind.QuestCreated ? quest.CreatedWorldTime : assignment.AssignedWorldTime;
                double due = deadline.startKind == QuestDeadlineStartKind.AbsoluteWorldTime ? deadline.absoluteWorldTime : deadline.durationFromStart >= 0d ? start + deadline.durationFromStart : -1d;
                if (due < 0d) continue;
                created.Add(new QuestDeadlineRecordData
                {
                    deadlineId = deadlineId,
                    deadlineDefinitionId = deadline.deadlineDefinitionId,
                    questId = assignment.QuestId,
                    questDefinitionId = quest.QuestDefinitionId,
                    assignmentId = assignment.AssignmentId,
                    worldId = worldId,
                    startKind = deadline.startKind,
                    expirationPolicy = deadline.expirationPolicy,
                    startWorldTime = start,
                    deadlineWorldTime = due,
                    hidden = deadline.hidden,
                    revision = 1L
                });
            }

            if (created.Count == 0) return QuestOutcomeOperationResult.Success("Quest assignment deadlines already tracked.", revision, revision, duplicate: true);
            if (preview) return QuestOutcomeOperationResult.Success("Quest assignment deadline tracking previewed.", revision, revision, preview: true);

            long before = revision;
            foreach (QuestDeadlineRecordData record in created)
            {
                deadlinesById[record.deadlineId] = record.Clone();
                AddToIndex(deadlinesByAssignment, record.assignmentId, record.deadlineId);
                RecordEvent(transactionId, QuestOutcomeEventKind.DeadlineCreated, record.questId, record.assignmentId, string.Empty, string.Empty, string.Empty, record.startWorldTime);
            }

            revision++;
            RecordTransaction(transactionId, "TrackAssignment", assignment.AssignmentId, assignment.QuestId, string.Empty, string.Empty);
            return QuestOutcomeOperationResult.Success("Quest assignment deadlines tracked.", before, revision);
        }

        public QuestCompletionEvaluationResult EvaluateCompletion(QuestCompletionEvaluationRequest request)
        {
            request ??= new QuestCompletionEvaluationRequest();
            if (disposed) return Evaluation(QuestOutcomeOperationStatus.Disposed, "Quest outcome runtime is disposed.", null, null, null, null, false, request.preview);
            if (participationRuntime == null) return Evaluation(QuestOutcomeOperationStatus.MissingParticipationRuntime, "Quest participation runtime is missing.", null, null, null, null, false, request.preview);
            if (objectiveRuntime == null) return Evaluation(QuestOutcomeOperationStatus.MissingObjectiveRuntime, "Quest objective progress runtime is missing.", null, null, null, null, false, request.preview);
            if (!participationRuntime.TryGetAssignment(request.assignmentId, out QuestAssignmentSnapshot assignment)) return Evaluation(QuestOutcomeOperationStatus.MissingAssignment, $"Quest assignment '{N(request.assignmentId)}' is missing.", null, null, null, null, false, request.preview);
            if (!TryResolveQuestAndDefinition(assignment.QuestId, out _, out QuestDefinition definition, out string failure)) return Evaluation(QuestOutcomeOperationStatus.MissingQuest, failure, assignment, null, null, null, false, request.preview);
            if (HasTerminalOutcome(assignment.AssignmentId, out _)) return Evaluation(QuestOutcomeOperationStatus.AlreadyTerminal, "Quest assignment already has a terminal outcome.", assignment, null, definition.CompletionPolicy, null, false, request.preview);

            QuestDeadlineSnapshot expired = FirstExpiredBlockingDeadline(assignment.AssignmentId, request.worldTime);
            if (expired != null) return Evaluation(QuestOutcomeOperationStatus.DeadlineExpired, "Quest assignment deadline has expired.", assignment, null, definition.CompletionPolicy, expired, false, request.preview);

            QuestAssignmentObjectiveSummary summary = objectiveRuntime.SummarizeAssignment(assignment.AssignmentId, QuestVisibilityAccess.PrivilegedDiagnostic, request.requesterPersonId);
            if (summary.RequiredRemaining > 0 || !summary.CompletionCandidate)
            {
                return Evaluation(QuestOutcomeOperationStatus.ObjectivesIncomplete, "Quest objectives are not complete.", assignment, summary, definition.CompletionPolicy, null, false, request.preview);
            }

            QuestCompletionPolicyData policy = definition.CompletionPolicy;
            QuestCompletionPolicy actualPolicy = policy.policy == QuestCompletionPolicy.Unknown ? QuestCompletionPolicy.AutoCompleteWhenRequiredObjectivesSatisfied : policy.policy;
            if (actualPolicy == QuestCompletionPolicy.RequireTurnIn && !string.IsNullOrWhiteSpace(policy.requiredInteractionPointId) && !string.Equals(policy.requiredInteractionPointId, N(request.interactionPointId), StringComparison.Ordinal))
            {
                return Evaluation(QuestOutcomeOperationStatus.TurnInRequired, "Quest completion requires turn-in at the configured interaction point.", assignment, summary, policy, null, false, request.preview);
            }

            if (actualPolicy == QuestCompletionPolicy.RequireIssuerVerification && !string.IsNullOrWhiteSpace(policy.requiredIssuerId) && !string.Equals(policy.requiredIssuerId, N(request.issuerId), StringComparison.Ordinal))
            {
                return Evaluation(QuestOutcomeOperationStatus.IssuerVerificationRequired, "Quest completion requires issuer verification.", assignment, summary, policy, null, false, request.preview);
            }

            if (actualPolicy == QuestCompletionPolicy.ExplicitSystemCompletion)
            {
                QuestCompletionRequest completionRequest = request as QuestCompletionRequest;
                if (completionRequest == null || !completionRequest.forceSystemCompletion)
                {
                    return Evaluation(QuestOutcomeOperationStatus.ExplicitCompletionRequired, "Quest completion requires an explicit system completion request.", assignment, summary, policy, null, false, request.preview);
                }
            }

            QuestOutcomeOperationStatus readyStatus = request.preview ? QuestOutcomeOperationStatus.Preview : QuestOutcomeOperationStatus.Succeeded;
            return Evaluation(readyStatus, "Quest completion is ready.", assignment, summary, policy, null, true, request.preview);
        }

        public QuestOutcomeOperationResult Complete(QuestCompletionRequest request)
        {
            request ??= new QuestCompletionRequest();
            if (!ValidateRevision(-1L, out QuestOutcomeOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestOutcomeOperationResult duplicate)) return duplicate;
            if (participationRuntime != null && participationRuntime.TryGetAssignment(request.assignmentId, out QuestAssignmentSnapshot trackedAssignment))
            {
                TrackAssignment(trackedAssignment, $"{transactionId}.track", request.preview);
            }

            QuestCompletionEvaluationResult evaluation = EvaluateCompletion(request);
            if (!evaluation.Ready) return QuestOutcomeOperationResult.Failure(evaluation.Status, evaluation.Message, revision, evaluation);
            QuestAssignmentSnapshot assignment = evaluation.Assignment;
            if (request.preview) return QuestOutcomeOperationResult.Success("Quest completion previewed.", revision, revision, evaluation: evaluation, preview: true);

            QuestTerminalOutcomeRecordData outcome = CreateOutcome(assignment, QuestTerminalOutcomeKind.Completed, QuestFailureReasonCode.Unknown, QuestFailureTriggerKind.ExplicitRequest, request.actorPersonIdOrAssignee(), request.issuerId, request.interactionPointId, request.locationId, request.worldTime, request.sourceEventId, request.provenanceId, false);
            long before = revision;
            StoreOutcome(outcome);
            List<QuestRewardEntitlementRecordData> rewards = CreateRewardEntitlements(assignment, outcome, evaluation.Policy, request.worldTime).ToList();
            foreach (QuestRewardEntitlementRecordData reward in rewards)
            {
                StoreEntitlement(reward);
            }

            revision++;
            RecordTransaction(transactionId, "Complete", assignment.AssignmentId, assignment.QuestId, outcome.terminalOutcomeId, string.Empty);
            RecordEvent(transactionId, QuestOutcomeEventKind.TerminalOutcomeRecorded, assignment.QuestId, assignment.AssignmentId, outcome.terminalOutcomeId, string.Empty, request.sourceEventId, request.worldTime);
            foreach (QuestRewardEntitlementRecordData reward in rewards)
            {
                RecordEvent(transactionId, QuestOutcomeEventKind.RewardEntitlementCreated, reward.questId, reward.assignmentId, reward.terminalOutcomeId, reward.entitlementId, string.Empty, request.worldTime);
            }

            if (rewards.Any(value => value.deliveryPolicy == QuestRewardDeliveryPolicy.GrantOnCompletion))
            {
                foreach (QuestRewardEntitlementRecordData reward in rewards.Where(value => value.deliveryPolicy == QuestRewardDeliveryPolicy.GrantOnCompletion).OrderBy(value => value.entitlementId, StringComparer.Ordinal).ToArray())
                {
                    GrantReward(reward.entitlementId, request.claimantOrAssignee(), request.worldTime, $"{transactionId}.grant.{reward.rewardDefinitionId}");
                }
            }

            return QuestOutcomeOperationResult.Success("Quest assignment completed.", before, revision, outcome, RewardsForOutcome(outcome.terminalOutcomeId), evaluation: evaluation);
        }

        public QuestOutcomeOperationResult Fail(QuestFailureRequest request)
        {
            request ??= new QuestFailureRequest();
            if (disposed) return Fail(QuestOutcomeOperationStatus.Disposed, "Quest outcome runtime is disposed.");
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestOutcomeOperationResult duplicate)) return duplicate;
            if (!TryResolveAssignment(request.assignmentId, request.questId, out QuestAssignmentSnapshot assignment, out string failure)) return Fail(QuestOutcomeOperationStatus.MissingAssignment, failure);
            if (HasTerminalOutcome(assignment.AssignmentId, out QuestTerminalOutcomeRecordData existing)) return QuestOutcomeOperationResult.Success("Quest assignment already has a terminal outcome.", revision, revision, existing, duplicate: true);
            if (!TryResolveQuestAndDefinition(assignment.QuestId, out _, out _, out failure)) return Fail(QuestOutcomeOperationStatus.MissingQuest, failure);
            if (request.preview) return QuestOutcomeOperationResult.Success("Quest failure previewed.", revision, revision, preview: true);

            QuestTerminalOutcomeKind kind = request.reasonCode == QuestFailureReasonCode.DeadlineExpired ? QuestTerminalOutcomeKind.Expired : QuestTerminalOutcomeKind.Failed;
            QuestTerminalOutcomeRecordData outcome = CreateOutcome(assignment, kind, request.reasonCode, request.triggerKind, request.actorPersonId, string.Empty, string.Empty, string.Empty, request.worldTime, request.sourceEventId, request.provenanceId, false);
            long before = revision;
            StoreOutcome(outcome);
            revision++;
            RecordTransaction(transactionId, "Fail", assignment.AssignmentId, assignment.QuestId, outcome.terminalOutcomeId, string.Empty);
            RecordEvent(transactionId, QuestOutcomeEventKind.TerminalOutcomeRecorded, assignment.QuestId, assignment.AssignmentId, outcome.terminalOutcomeId, string.Empty, request.sourceEventId, request.worldTime);
            return QuestOutcomeOperationResult.Success("Quest assignment failed.", before, revision, outcome);
        }

        public QuestOutcomeOperationResult EvaluateDeadlines(double worldTime, string transactionPrefix = null)
        {
            if (disposed) return Fail(QuestOutcomeOperationStatus.Disposed, "Quest outcome runtime is disposed.");
            QuestDeadlineRecordData[] due = deadlinesById.Values
                .Where(value => value.deadlineWorldTime >= 0d && !value.handled && worldTime >= value.deadlineWorldTime)
                .OrderBy(value => value.deadlineWorldTime)
                .ThenBy(value => value.deadlineId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToArray();
            if (due.Length == 0) return QuestOutcomeOperationResult.Success("No quest deadlines expired.", revision, revision, duplicate: true);

            long before = revision;
            QuestTerminalOutcomeRecordData lastOutcome = null;
            foreach (QuestDeadlineRecordData deadline in due)
            {
                if (!deadlinesById.TryGetValue(deadline.deadlineId, out QuestDeadlineRecordData live) || live.handled) continue;
                live.expired = true;
                live.handled = true;
                live.revision++;
                if (deadline.expirationPolicy == QuestDeadlineExpirationPolicy.FailAssignment || deadline.expirationPolicy == QuestDeadlineExpirationPolicy.FailQuest || deadline.expirationPolicy == QuestDeadlineExpirationPolicy.LockCompletion)
                {
                    QuestOutcomeOperationResult failed = Fail(new QuestFailureRequest
                    {
                        transactionId = $"{N(transactionPrefix)}.deadline.{deadline.deadlineId}",
                        assignmentId = deadline.assignmentId,
                        reasonCode = QuestFailureReasonCode.DeadlineExpired,
                        triggerKind = QuestFailureTriggerKind.Deadline,
                        sourceEventId = deadline.deadlineId,
                        worldTime = worldTime
                    });
                    if (failed.Outcome != null)
                    {
                        live.terminalOutcomeId = failed.Outcome.TerminalOutcomeId;
                        lastOutcome = failed.Outcome.ToSaveData();
                    }
                }

                RecordEvent(transactionPrefix, QuestOutcomeEventKind.DeadlineExpired, deadline.questId, deadline.assignmentId, live.terminalOutcomeId, string.Empty, deadline.deadlineId, worldTime);
            }

            revision++;
            return QuestOutcomeOperationResult.Success("Quest deadlines evaluated.", before, revision, lastOutcome);
        }

        public QuestOutcomeOperationResult ClaimReward(QuestRewardClaimRequest request)
        {
            request ??= new QuestRewardClaimRequest();
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out QuestOutcomeOperationResult duplicate)) return duplicate;
            return GrantReward(N(request.entitlementId), N(request.claimantPersonId), request.worldTime, transactionId, request.preview);
        }

        public bool TryGetOutcome(string terminalOutcomeId, out QuestTerminalOutcomeSnapshot snapshot)
        {
            snapshot = null;
            if (!outcomesById.TryGetValue(N(terminalOutcomeId), out QuestTerminalOutcomeRecordData record)) return false;
            snapshot = new QuestTerminalOutcomeSnapshot(record);
            return true;
        }

        public IReadOnlyList<QuestTerminalOutcomeSnapshot> QueryOutcomes(QuestOutcomeQuery query = null)
        {
            QuestOutcomeQuery actual = query ?? new QuestOutcomeQuery();
            IEnumerable<QuestTerminalOutcomeRecordData> records = outcomesById.Values;
            if (!string.IsNullOrWhiteSpace(actual.worldId)) records = records.Where(value => string.Equals(value.worldId, actual.worldId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.questId)) records = records.Where(value => string.Equals(value.questId, actual.questId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.assignmentId)) records = records.Where(value => string.Equals(value.assignmentId, actual.assignmentId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.terminalOutcomeId)) records = records.Where(value => string.Equals(value.terminalOutcomeId, actual.terminalOutcomeId, StringComparison.Ordinal));
            if (actual.outcomeKind.HasValue) records = records.Where(value => value.outcomeKind == actual.outcomeKind.Value);
            records = records.Where(value => actual.includeHidden || !value.hidden || IsPrivileged(actual.access));
            return records.OrderBy(value => value.worldTime).ThenBy(value => value.terminalOutcomeId, StringComparer.Ordinal).Select(value => new QuestTerminalOutcomeSnapshot(value, value.hidden && !IsPrivileged(actual.access))).ToArray();
        }

        public IReadOnlyList<QuestDeadlineSnapshot> QueryDeadlines(QuestOutcomeQuery query = null)
        {
            QuestOutcomeQuery actual = query ?? new QuestOutcomeQuery();
            IEnumerable<QuestDeadlineRecordData> records = deadlinesById.Values;
            if (!string.IsNullOrWhiteSpace(actual.worldId)) records = records.Where(value => string.Equals(value.worldId, actual.worldId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.questId)) records = records.Where(value => string.Equals(value.questId, actual.questId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.assignmentId)) records = records.Where(value => string.Equals(value.assignmentId, actual.assignmentId, StringComparison.Ordinal));
            records = records.Where(value => actual.includeHidden || !value.hidden || IsPrivileged(actual.access));
            return records.OrderBy(value => value.deadlineWorldTime).ThenBy(value => value.deadlineId, StringComparer.Ordinal).Select(value => new QuestDeadlineSnapshot(value, value.hidden && !IsPrivileged(actual.access))).ToArray();
        }

        public IReadOnlyList<QuestRewardEntitlementSnapshot> QueryRewards(QuestRewardQuery query = null)
        {
            QuestRewardQuery actual = query ?? new QuestRewardQuery();
            IEnumerable<QuestRewardEntitlementRecordData> records = entitlementsById.Values;
            if (!string.IsNullOrWhiteSpace(actual.worldId)) records = records.Where(value => string.Equals(value.worldId, actual.worldId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.entitlementId)) records = records.Where(value => string.Equals(value.entitlementId, actual.entitlementId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.questId)) records = records.Where(value => string.Equals(value.questId, actual.questId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.assignmentId)) records = records.Where(value => string.Equals(value.assignmentId, actual.assignmentId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.recipientPersonId)) records = records.Where(value => string.Equals(value.recipientPersonId, actual.recipientPersonId, StringComparison.Ordinal));
            if (!actual.includeTerminal) records = records.Where(value => value.state != QuestRewardEntitlementState.Granted && value.state != QuestRewardEntitlementState.Cancelled);
            records = records.Where(value => actual.includeHidden || !value.hidden || IsPrivileged(actual.access) || string.Equals(value.recipientPersonId, actual.requesterPersonId, StringComparison.Ordinal));
            return records.OrderBy(value => value.createdWorldTime).ThenBy(value => value.entitlementId, StringComparer.Ordinal).Select(value => new QuestRewardEntitlementSnapshot(value, value.hidden && !IsPrivileged(actual.access) && !string.Equals(value.recipientPersonId, actual.requesterPersonId, StringComparison.Ordinal))).ToArray();
        }

        public QuestOutcomeRuntimeSaveData CreateSaveData()
        {
            return new QuestOutcomeRuntimeSaveData
            {
                worldId = worldId,
                revision = revision,
                terminalOutcomes = outcomesById.Values.OrderBy(value => value.terminalOutcomeId, StringComparer.Ordinal).Select(value => value.Clone()).ToList(),
                deadlines = deadlinesById.Values.OrderBy(value => value.deadlineId, StringComparer.Ordinal).Select(value => value.Clone()).ToList(),
                rewardEntitlements = entitlementsById.Values.OrderBy(value => value.entitlementId, StringComparer.Ordinal).Select(value => value.Clone()).ToList(),
                rewardGrants = grantsById.Values.OrderBy(value => value.grantId, StringComparer.Ordinal).Select(value => value.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(value => value.transactionId, StringComparer.Ordinal).Select(value => value.Clone()).ToList(),
                events = events.OrderBy(value => value.runtimeRevision).ThenBy(value => value.eventId, StringComparer.Ordinal).Select(value => value.Clone()).ToList()
            };
        }

        public QuestOutcomeOperationResult RestoreFromSaveData(QuestOutcomeRuntimeSaveData saveData, QuestRuntime quests, QuestParticipationRuntime participation, QuestObjectiveProgressRuntime objectives, DefinitionRegistry definitionRegistry, IQuestRewardEffectExecutor executor, string expectedWorldId = PersistenceService.LocalWorldId)
        {
            if (!ValidateSaveData(saveData, quests ?? questRuntime, participation ?? participationRuntime, objectives ?? objectiveRuntime, definitionRegistry ?? registry, expectedWorldId, out string failure))
            {
                return Fail(QuestOutcomeOperationStatus.PersistenceInvalid, failure);
            }

            QuestOutcomeRuntimeSaveData rollback = CreateSaveData();
            try
            {
                Configure(quests ?? questRuntime, participation ?? participationRuntime, objectives ?? objectiveRuntime, definitionRegistry ?? registry, executor ?? rewardExecutor, string.IsNullOrWhiteSpace(saveData.worldId) ? expectedWorldId : saveData.worldId);
                Clear();
                worldId = string.IsNullOrWhiteSpace(saveData.worldId) ? expectedWorldId : saveData.worldId;
                foreach (QuestTerminalOutcomeRecordData record in saveData.terminalOutcomes ?? new List<QuestTerminalOutcomeRecordData>()) StoreOutcome(record);
                foreach (QuestDeadlineRecordData record in saveData.deadlines ?? new List<QuestDeadlineRecordData>())
                {
                    deadlinesById[record.deadlineId] = record.Clone();
                    AddToIndex(deadlinesByAssignment, record.assignmentId, record.deadlineId);
                }
                foreach (QuestRewardEntitlementRecordData record in saveData.rewardEntitlements ?? new List<QuestRewardEntitlementRecordData>()) StoreEntitlement(record);
                foreach (QuestRewardGrantRecordData record in saveData.rewardGrants ?? new List<QuestRewardGrantRecordData>()) grantsById[record.grantId] = record.Clone();
                foreach (QuestOutcomeTransactionData record in saveData.transactions ?? new List<QuestOutcomeTransactionData>()) transactionsById[record.transactionId] = record.Clone();
                events.AddRange((saveData.events ?? new List<QuestOutcomeEventData>()).Select(value => value.Clone()));
                revision = saveData.revision;
                return QuestOutcomeOperationResult.Success("Quest outcomes restored.", revision, revision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, questRuntime, participationRuntime, objectiveRuntime, registry, rewardExecutor, worldId);
                return Fail(QuestOutcomeOperationStatus.RestoreFailed, $"Quest outcome restore failed: {exception.Message}");
            }
        }

        public QuestOutcomeValidationReport ValidateRuntime()
        {
            ValidateSaveData(CreateSaveData(), questRuntime, participationRuntime, objectiveRuntime, registry, worldId, out _, out QuestOutcomeValidationReport report);
            return report;
        }

        public static bool ValidateSaveData(QuestOutcomeRuntimeSaveData saveData, QuestRuntime quests, QuestParticipationRuntime participation, QuestObjectiveProgressRuntime objectives, DefinitionRegistry registry, string expectedWorldId, out string failure)
        {
            return ValidateSaveData(saveData, quests, participation, objectives, registry, expectedWorldId, out failure, out _);
        }

        public static bool ValidateSaveData(QuestOutcomeRuntimeSaveData saveData, QuestRuntime quests, QuestParticipationRuntime participation, QuestObjectiveProgressRuntime objectives, DefinitionRegistry registry, string expectedWorldId, out string failure, out QuestOutcomeValidationReport report)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            if (saveData == null)
            {
                errors.Add("Quest outcome save data is missing.");
            }
            else
            {
                if (saveData.schemaVersion != QuestOutcomeRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported quest outcome save schema version {saveData.schemaVersion}.");
                string expected = string.IsNullOrWhiteSpace(expectedWorldId) ? saveData.worldId : expectedWorldId;
                if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(saveData.worldId, expected, StringComparison.Ordinal)) errors.Add($"Quest outcome save world '{saveData.worldId}' does not match expected world '{expected}'.");
                if (quests == null) errors.Add("Quest outcome validation requires QuestRuntime.");
                if (participation == null) errors.Add("Quest outcome validation requires QuestParticipationRuntime.");
                if (objectives == null) errors.Add("Quest outcome validation requires QuestObjectiveProgressRuntime.");
                if (registry == null) errors.Add("Quest outcome validation requires DefinitionRegistry.");

                HashSet<string> outcomeIds = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> assignmentTerminals = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestTerminalOutcomeRecordData outcome in saveData.terminalOutcomes ?? new List<QuestTerminalOutcomeRecordData>())
                {
                    ValidateOutcome(outcome, quests, participation, registry, outcomeIds, assignmentTerminals, errors);
                }

                HashSet<string> deadlineIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestDeadlineRecordData deadline in saveData.deadlines ?? new List<QuestDeadlineRecordData>())
                {
                    if (deadline == null || string.IsNullOrWhiteSpace(deadline.deadlineId)) { errors.Add("Quest deadline record is missing a stable ID."); continue; }
                    if (!deadlineIds.Add(deadline.deadlineId)) errors.Add($"Duplicate quest deadline ID '{deadline.deadlineId}'.");
                    if (participation != null && !participation.TryGetAssignment(deadline.assignmentId, out _)) errors.Add($"Quest deadline '{deadline.deadlineId}' references missing assignment '{deadline.assignmentId}'.");
                    if (deadline.handled && !string.IsNullOrWhiteSpace(deadline.terminalOutcomeId) && !outcomeIds.Contains(deadline.terminalOutcomeId)) errors.Add($"Quest deadline '{deadline.deadlineId}' references missing terminal outcome '{deadline.terminalOutcomeId}'.");
                }

                HashSet<string> entitlementIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestRewardEntitlementRecordData entitlement in saveData.rewardEntitlements ?? new List<QuestRewardEntitlementRecordData>())
                {
                    if (entitlement == null || string.IsNullOrWhiteSpace(entitlement.entitlementId)) { errors.Add("Quest reward entitlement is missing a stable ID."); continue; }
                    if (!entitlementIds.Add(entitlement.entitlementId)) errors.Add($"Duplicate quest reward entitlement ID '{entitlement.entitlementId}'.");
                    if (!outcomeIds.Contains(entitlement.terminalOutcomeId)) errors.Add($"Quest reward entitlement '{entitlement.entitlementId}' references missing terminal outcome '{entitlement.terminalOutcomeId}'.");
                    if (entitlement.quantity <= 0) errors.Add($"Quest reward entitlement '{entitlement.entitlementId}' has invalid quantity.");
                }

                HashSet<string> grantIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestRewardGrantRecordData grant in saveData.rewardGrants ?? new List<QuestRewardGrantRecordData>())
                {
                    if (grant == null || string.IsNullOrWhiteSpace(grant.grantId)) { errors.Add("Quest reward grant is missing a stable ID."); continue; }
                    if (!grantIds.Add(grant.grantId)) errors.Add($"Duplicate quest reward grant ID '{grant.grantId}'.");
                    if (!entitlementIds.Contains(grant.entitlementId)) errors.Add($"Quest reward grant '{grant.grantId}' references missing entitlement '{grant.entitlementId}'.");
                }

                HashSet<string> transactionIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestOutcomeTransactionData transaction in saveData.transactions ?? new List<QuestOutcomeTransactionData>())
                {
                    if (transaction == null || string.IsNullOrWhiteSpace(transaction.transactionId)) continue;
                    if (!transactionIds.Add(transaction.transactionId)) errors.Add($"Duplicate quest outcome transaction ID '{transaction.transactionId}'.");
                }
            }

            report = new QuestOutcomeValidationReport(errors, warnings);
            failure = report.Succeeded ? string.Empty : string.Join(" | ", report.Errors);
            return report.Succeeded;
        }

        public void Clear()
        {
            outcomesById.Clear();
            terminalOutcomeByAssignment.Clear();
            deadlinesById.Clear();
            deadlinesByAssignment.Clear();
            entitlementsById.Clear();
            entitlementsByOutcome.Clear();
            grantsById.Clear();
            transactionsById.Clear();
            events.Clear();
            revision = 0L;
        }

        public void Dispose()
        {
            disposed = true;
            Clear();
        }

        private QuestOutcomeOperationResult GrantReward(string entitlementId, string claimantPersonId, double worldTime, string transactionId, bool preview = false)
        {
            if (disposed) return Fail(QuestOutcomeOperationStatus.Disposed, "Quest outcome runtime is disposed.");
            if (!entitlementsById.TryGetValue(N(entitlementId), out QuestRewardEntitlementRecordData entitlement)) return Fail(QuestOutcomeOperationStatus.MissingReward, $"Quest reward entitlement '{N(entitlementId)}' is missing.");
            if (entitlement.state == QuestRewardEntitlementState.Granted)
            {
                return QuestOutcomeOperationResult.Success("Quest reward already granted.", revision, revision, reward: entitlement, duplicate: true);
            }

            if (entitlement.state != QuestRewardEntitlementState.Claimable && entitlement.state != QuestRewardEntitlementState.Pending && entitlement.deliveryPolicy != QuestRewardDeliveryPolicy.GrantOnCompletion)
            {
                return Fail(QuestOutcomeOperationStatus.RewardNotClaimable, $"Quest reward entitlement '{entitlement.entitlementId}' is not claimable.");
            }

            if (!string.IsNullOrWhiteSpace(claimantPersonId) && !string.Equals(entitlement.recipientPersonId, claimantPersonId, StringComparison.Ordinal))
            {
                return Fail(QuestOutcomeOperationStatus.InvalidRequest, "Quest reward claimant does not match the entitlement recipient.");
            }

            string grantId = BuildGrantId(entitlement.entitlementId);
            if (grantsById.TryGetValue(grantId, out QuestRewardGrantRecordData existingGrant) && existingGrant.state == QuestRewardGrantState.Granted)
            {
                QuestRewardEntitlementRecordData duplicateEntitlement = entitlement.Clone();
                duplicateEntitlement.state = QuestRewardEntitlementState.Granted;
                entitlementsById[duplicateEntitlement.entitlementId] = duplicateEntitlement;
                return QuestOutcomeOperationResult.Success("Quest reward grant already exists.", revision, revision, reward: duplicateEntitlement, duplicate: true);
            }

            if (preview) return QuestOutcomeOperationResult.Success("Quest reward claim previewed.", revision, revision, reward: entitlement, preview: true);

            QuestRewardEffectResult effect = rewardExecutor == null
                ? QuestRewardEffectResult.Unsupported("No reward owner executor is configured.")
                : rewardExecutor.Execute(new QuestRewardEffectRequest
                {
                    grantId = grantId,
                    entitlementId = entitlement.entitlementId,
                    terminalOutcomeId = entitlement.terminalOutcomeId,
                    questId = entitlement.questId,
                    assignmentId = entitlement.assignmentId,
                    recipientPersonId = entitlement.recipientPersonId,
                    category = entitlement.category,
                    targetDefinitionId = entitlement.targetDefinitionId,
                    secondaryTargetId = entitlement.secondaryTargetId,
                    quantity = entitlement.quantity,
                    worldTime = worldTime
                });

            long before = revision;
            QuestRewardGrantRecordData grant = new QuestRewardGrantRecordData
            {
                grantId = grantId,
                entitlementId = entitlement.entitlementId,
                terminalOutcomeId = entitlement.terminalOutcomeId,
                recipientPersonId = entitlement.recipientPersonId,
                worldId = worldId,
                category = entitlement.category,
                ownerRuntimeId = effect.OwnerRuntimeId,
                ownerRecordId = effect.OwnerRecordId,
                state = effect.Succeeded ? effect.Duplicate ? QuestRewardGrantState.Duplicate : QuestRewardGrantState.Granted : QuestRewardGrantState.Failed,
                failureReason = effect.Message,
                worldTime = worldTime,
                revision = 1L
            };
            grantsById[grant.grantId] = grant.Clone();

            QuestRewardEntitlementRecordData changed = entitlement.Clone();
            changed.lastGrantId = grant.grantId;
            changed.failureReason = effect.Succeeded ? string.Empty : effect.Message;
            changed.grantedWorldTime = effect.Succeeded ? worldTime : changed.grantedWorldTime;
            changed.state = effect.Succeeded ? QuestRewardEntitlementState.Granted : effect.IsUnsupported ? QuestRewardEntitlementState.Failed : QuestRewardEntitlementState.Claimable;
            changed.revision++;
            entitlementsById[changed.entitlementId] = changed.Clone();
            revision++;
            RecordTransaction(transactionId, "GrantReward", entitlement.assignmentId, entitlement.questId, entitlement.terminalOutcomeId, entitlement.entitlementId);
            RecordEvent(transactionId, effect.Succeeded ? QuestOutcomeEventKind.RewardGranted : QuestOutcomeEventKind.RewardGrantFailed, entitlement.questId, entitlement.assignmentId, entitlement.terminalOutcomeId, entitlement.entitlementId, string.Empty, worldTime);
            if (!effect.Succeeded)
            {
                return QuestOutcomeOperationResult.Failure(effect.IsUnsupported ? QuestOutcomeOperationStatus.RewardUnsupported : QuestOutcomeOperationStatus.RewardOwnerRejected, effect.Message, revision);
            }

            return QuestOutcomeOperationResult.Success("Quest reward granted.", before, revision, reward: changed);
        }

        private IEnumerable<QuestRewardEntitlementRecordData> CreateRewardEntitlements(QuestAssignmentSnapshot assignment, QuestTerminalOutcomeRecordData outcome, QuestCompletionPolicyData completionPolicy, double worldTime)
        {
            if (!registry.TryGet(outcome.questDefinitionId, out QuestDefinition definition)) yield break;
            foreach (QuestRewardPackageDefinitionData package in definition.RewardPackages.OrderBy(value => value.rewardPackageId, StringComparer.Ordinal))
            {
                QuestRewardDeliveryPolicy delivery = package.deliveryPolicy == QuestRewardDeliveryPolicy.Unknown ? QuestRewardDeliveryPolicy.ClaimAfterCompletion : package.deliveryPolicy;
                foreach (QuestRewardDefinitionData reward in package.rewards.Where(value => value != null).OrderBy(value => value.rewardDefinitionId, StringComparer.Ordinal))
                {
                    if (reward.optional && (completionPolicy == null || !completionPolicy.allowOptionalBonusRewards)) continue;
                    string[] recipients = definition.PartyRewardPolicy == QuestPartyRewardPolicy.LeaderOnly
                        ? new[] { assignment.AssigneePersonId }
                        : assignment.ParticipantPersonIds.DefaultIfEmpty(assignment.AssigneePersonId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                    for (int recipientIndex = 0; recipientIndex < recipients.Length; recipientIndex++)
                    {
                        string recipient = recipients[recipientIndex];
                        string entitlementId = BuildEntitlementId(outcome.terminalOutcomeId, reward.rewardDefinitionId) + (recipients.Length > 1 ? $".{recipientIndex + 1:00}" : string.Empty);
                        int quantity = Math.Max(1, reward.quantity);
                        if (definition.PartyRewardPolicy == QuestPartyRewardPolicy.SplitCurrency && reward.category == QuestRewardCategory.Currency)
                        {
                            int baseShare = reward.quantity / recipients.Length;
                            int remainder = reward.quantity % recipients.Length;
                            quantity = Math.Max(0, baseShare + (recipientIndex < remainder ? 1 : 0));
                            if (quantity <= 0) continue;
                        }
                        yield return new QuestRewardEntitlementRecordData
                        {
                        entitlementId = entitlementId,
                        rewardPackageId = package.rewardPackageId,
                        rewardDefinitionId = reward.rewardDefinitionId,
                        terminalOutcomeId = outcome.terminalOutcomeId,
                        questId = assignment.QuestId,
                        assignmentId = assignment.AssignmentId,
                        recipientPersonId = recipient,
                        worldId = worldId,
                        category = reward.category,
                        deliveryPolicy = delivery,
                        targetDefinitionId = reward.targetDefinitionId,
                        secondaryTargetId = reward.secondaryTargetId,
                        quantity = quantity,
                        optional = reward.optional,
                        hidden = reward.hidden,
                        state = delivery == QuestRewardDeliveryPolicy.GrantOnCompletion ? QuestRewardEntitlementState.Pending : QuestRewardEntitlementState.Claimable,
                        createdWorldTime = worldTime,
                        grantedWorldTime = -1d,
                        revision = 1L
                        };
                    }
                }
            }
        }

        private bool TryResolveAssignment(string assignmentId, string questId, out QuestAssignmentSnapshot assignment, out string failure)
        {
            assignment = null;
            string id = N(assignmentId);
            if (!string.IsNullOrWhiteSpace(id) && participationRuntime != null && participationRuntime.TryGetAssignment(id, out assignment))
            {
                failure = string.Empty;
                return true;
            }

            string requestedQuest = N(questId);
            if (!string.IsNullOrWhiteSpace(requestedQuest) && participationRuntime != null)
            {
                assignment = participationRuntime.QueryAssignments(new QuestAssignmentQuery { questId = requestedQuest, access = QuestVisibilityAccess.PrivilegedDiagnostic, includeHistorical = true }).FirstOrDefault();
                if (assignment != null)
                {
                    failure = string.Empty;
                    return true;
                }
            }

            failure = $"Quest assignment '{id}' is missing.";
            return false;
        }

        private bool TryResolveQuestAndDefinition(string questId, out QuestSnapshot quest, out QuestDefinition definition, out string failure)
        {
            quest = null;
            definition = null;
            if (questRuntime == null) { failure = "Quest runtime is missing."; return false; }
            if (registry == null) { failure = "Definition registry is missing."; return false; }
            if (!questRuntime.TryGetSnapshot(N(questId), out quest)) { failure = $"Quest '{N(questId)}' is missing."; return false; }
            if (!registry.TryGet(quest.QuestDefinitionId, out definition)) { failure = $"Quest definition '{quest.QuestDefinitionId}' is missing."; return false; }
            failure = string.Empty;
            return true;
        }

        private QuestTerminalOutcomeRecordData CreateOutcome(QuestAssignmentSnapshot assignment, QuestTerminalOutcomeKind kind, QuestFailureReasonCode reason, QuestFailureTriggerKind trigger, string actorPersonId, string issuerId, string interactionPointId, string locationId, double worldTime, string sourceEventId, string provenanceId, bool hidden)
        {
            string terminalOutcomeId = BuildTerminalOutcomeId(assignment.AssignmentId, kind);
            return new QuestTerminalOutcomeRecordData
            {
                outcomeId = $"outcome.{terminalOutcomeId}",
                terminalOutcomeId = terminalOutcomeId,
                questId = assignment.QuestId,
                assignmentId = assignment.AssignmentId,
                worldId = worldId,
                questDefinitionId = TryResolveQuestAndDefinition(assignment.QuestId, out QuestSnapshot quest, out _, out _) ? quest.QuestDefinitionId : string.Empty,
                scope = QuestOutcomeScope.Assignment,
                outcomeKind = kind,
                failureReason = reason,
                triggerKind = trigger,
                sourceEventId = N(sourceEventId),
                actorPersonId = N(actorPersonId),
                issuerId = N(issuerId),
                interactionPointId = N(interactionPointId),
                locationId = N(locationId),
                worldTime = worldTime,
                provenanceId = N(provenanceId),
                hidden = hidden,
                revision = 1L
            };
        }

        private QuestDeadlineSnapshot FirstExpiredBlockingDeadline(string assignmentId, double worldTime)
        {
            if (!deadlinesByAssignment.TryGetValue(N(assignmentId), out HashSet<string> ids)) return null;
            QuestDeadlineRecordData record = ids.Select(id => deadlinesById[id])
                .Where(value => value.deadlineWorldTime >= 0d && !value.handled && worldTime >= value.deadlineWorldTime && value.expirationPolicy != QuestDeadlineExpirationPolicy.AdvisoryOnly)
                .OrderBy(value => value.deadlineWorldTime)
                .ThenBy(value => value.deadlineId, StringComparer.Ordinal)
                .FirstOrDefault();
            return record == null ? null : new QuestDeadlineSnapshot(record);
        }

        private bool HasTerminalOutcome(string assignmentId, out QuestTerminalOutcomeRecordData outcome)
        {
            outcome = null;
            if (!terminalOutcomeByAssignment.TryGetValue(N(assignmentId), out string id)) return false;
            return outcomesById.TryGetValue(id, out outcome);
        }

        private IEnumerable<QuestRewardEntitlementRecordData> RewardsForOutcome(string terminalOutcomeId)
        {
            if (!entitlementsByOutcome.TryGetValue(N(terminalOutcomeId), out HashSet<string> ids)) return Array.Empty<QuestRewardEntitlementRecordData>();
            return ids.Select(id => entitlementsById[id].Clone()).OrderBy(value => value.entitlementId, StringComparer.Ordinal).ToArray();
        }

        private void StoreOutcome(QuestTerminalOutcomeRecordData outcome)
        {
            QuestTerminalOutcomeRecordData clone = outcome.Clone();
            outcomesById[clone.terminalOutcomeId] = clone;
            if (!string.IsNullOrWhiteSpace(clone.assignmentId)) terminalOutcomeByAssignment[clone.assignmentId] = clone.terminalOutcomeId;
        }

        private void StoreEntitlement(QuestRewardEntitlementRecordData entitlement)
        {
            QuestRewardEntitlementRecordData clone = entitlement.Clone();
            entitlementsById[clone.entitlementId] = clone;
            AddToIndex(entitlementsByOutcome, clone.terminalOutcomeId, clone.entitlementId);
        }

        private static void ValidateOutcome(QuestTerminalOutcomeRecordData outcome, QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry registry, ISet<string> outcomeIds, ISet<string> assignmentTerminals, ICollection<string> errors)
        {
            if (outcome == null || string.IsNullOrWhiteSpace(outcome.terminalOutcomeId)) { errors.Add("Quest terminal outcome is missing a stable ID."); return; }
            if (!outcomeIds.Add(outcome.terminalOutcomeId)) errors.Add($"Duplicate quest terminal outcome ID '{outcome.terminalOutcomeId}'.");
            if (!string.IsNullOrWhiteSpace(outcome.assignmentId) && !assignmentTerminals.Add(outcome.assignmentId)) errors.Add($"Quest assignment '{outcome.assignmentId}' has more than one terminal outcome.");
            if (quests != null && !quests.TryGetSnapshot(outcome.questId, out _)) errors.Add($"Quest terminal outcome '{outcome.terminalOutcomeId}' references missing quest '{outcome.questId}'.");
            if (participation != null && !participation.TryGetAssignment(outcome.assignmentId, out _)) errors.Add($"Quest terminal outcome '{outcome.terminalOutcomeId}' references missing assignment '{outcome.assignmentId}'.");
            if (registry != null && !registry.TryGet(outcome.questDefinitionId, out QuestDefinition _)) errors.Add($"Quest terminal outcome '{outcome.terminalOutcomeId}' references missing quest definition '{outcome.questDefinitionId}'.");
            if (outcome.outcomeKind == QuestTerminalOutcomeKind.Unknown) errors.Add($"Quest terminal outcome '{outcome.terminalOutcomeId}' has unknown outcome kind.");
        }

        private QuestOutcomeOperationResult Fail(QuestOutcomeOperationStatus status, string message) => QuestOutcomeOperationResult.Failure(status, message, revision);
        private bool ValidateRevision(long expectedRevision, out QuestOutcomeOperationResult failure)
        {
            failure = null;
            if (expectedRevision >= 0L && expectedRevision != revision)
            {
                failure = Fail(QuestOutcomeOperationStatus.RevisionConflict, $"Expected revision {expectedRevision}, actual {revision}.");
                return false;
            }
            return true;
        }

        private bool TryDuplicate(string transactionId, out QuestOutcomeOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId)) return false;
            if (!transactionsById.TryGetValue(transactionId, out QuestOutcomeTransactionData transaction)) return false;
            QuestTerminalOutcomeRecordData outcome = !string.IsNullOrWhiteSpace(transaction.terminalOutcomeId) && outcomesById.TryGetValue(transaction.terminalOutcomeId, out QuestTerminalOutcomeRecordData foundOutcome) ? foundOutcome : null;
            QuestRewardEntitlementRecordData reward = !string.IsNullOrWhiteSpace(transaction.entitlementId) && entitlementsById.TryGetValue(transaction.entitlementId, out QuestRewardEntitlementRecordData foundReward) ? foundReward : null;
            result = QuestOutcomeOperationResult.Success("Duplicate quest outcome transaction ignored.", revision, revision, outcome, outcome == null ? null : RewardsForOutcome(outcome.terminalOutcomeId), reward, duplicate: true);
            return true;
        }

        private QuestCompletionEvaluationResult Evaluation(QuestOutcomeOperationStatus status, string message, QuestAssignmentSnapshot assignment, QuestAssignmentObjectiveSummary summary, QuestCompletionPolicyData policy, QuestDeadlineSnapshot deadline, bool ready, bool preview)
        {
            return new QuestCompletionEvaluationResult(status, message, assignment, summary, policy, deadline, ready, preview);
        }

        private void RecordTransaction(string transactionId, string operation, string assignmentId, string questId, string terminalOutcomeId, string entitlementId)
        {
            transactionId = N(transactionId);
            if (string.IsNullOrWhiteSpace(transactionId)) return;
            transactionsById[transactionId] = new QuestOutcomeTransactionData
            {
                transactionId = transactionId,
                operation = operation ?? string.Empty,
                assignmentId = N(assignmentId),
                questId = N(questId),
                terminalOutcomeId = N(terminalOutcomeId),
                entitlementId = N(entitlementId),
                runtimeRevision = revision
            };
        }

        private void RecordEvent(string transactionId, QuestOutcomeEventKind kind, string questId, string assignmentId, string terminalOutcomeId, string rewardEntitlementId, string sourceEventId, double worldTime)
        {
            events.Add(new QuestOutcomeEventData
            {
                eventId = $"quest-outcome-event.{events.Count + 1:0000}",
                transactionId = N(transactionId),
                eventKind = kind,
                questId = N(questId),
                assignmentId = N(assignmentId),
                terminalOutcomeId = N(terminalOutcomeId),
                rewardEntitlementId = N(rewardEntitlementId),
                sourceEventId = N(sourceEventId),
                worldTime = worldTime,
                runtimeRevision = revision
            });
        }

        private static void AddToIndex(IDictionary<string, HashSet<string>> index, string key, string value)
        {
            key = N(key);
            value = N(value);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) return;
            if (!index.TryGetValue(key, out HashSet<string> ids))
            {
                ids = new HashSet<string>(StringComparer.Ordinal);
                index[key] = ids;
            }
            ids.Add(value);
        }

        private static string BuildTerminalOutcomeId(string assignmentId, QuestTerminalOutcomeKind kind) => $"quest-terminal-outcome.{N(assignmentId)}.{kind}".ToLowerInvariant();
        private static string BuildDeadlineId(string assignmentId, string deadlineDefinitionId) => $"quest-deadline.{N(assignmentId)}.{N(deadlineDefinitionId)}";
        private static string BuildEntitlementId(string terminalOutcomeId, string rewardDefinitionId) => $"quest-reward-entitlement.{N(terminalOutcomeId)}.{N(rewardDefinitionId)}";
        private static string BuildGrantId(string entitlementId) => $"quest-reward-grant.{N(entitlementId)}";
        private static bool IsPrivileged(QuestVisibilityAccess access) => access == QuestVisibilityAccess.PrivilegedDiagnostic || access == QuestVisibilityAccess.Government || access == QuestVisibilityAccess.OrganizationMember;
        private static string N(string value) => QuestOutcomeModelUtility.N(value);
    }

    internal static class QuestOutcomeRequestExtensions
    {
        public static string actorPersonIdOrAssignee(this QuestCompletionRequest request) => string.IsNullOrWhiteSpace(request.requesterPersonId) ? string.Empty : request.requesterPersonId.Trim();
        public static string claimantOrAssignee(this QuestCompletionRequest request) => string.IsNullOrWhiteSpace(request.requesterPersonId) ? string.Empty : request.requesterPersonId.Trim();
    }
}
