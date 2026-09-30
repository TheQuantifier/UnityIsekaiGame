using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Quests
{
    public sealed class QuestObjectiveProgressRuntime : IDisposable
    {
        private readonly Dictionary<string, QuestObjectiveRecordData> objectivesById = new Dictionary<string, QuestObjectiveRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> objectivesByAssignment = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<QuestObjectiveCategory, HashSet<string>> eventObjectiveIndex = new Dictionary<QuestObjectiveCategory, HashSet<string>>();
        private readonly Dictionary<string, QuestObjectiveTransactionData> transactionsById = new Dictionary<string, QuestObjectiveTransactionData>(StringComparer.Ordinal);
        private readonly List<QuestObjectiveRuntimeEventData> events = new List<QuestObjectiveRuntimeEventData>();

        private QuestRuntime questRuntime;
        private QuestParticipationRuntime participationRuntime;
        private DefinitionRegistry registry;
        private string worldId;
        private long revision;
        private bool disposed;

        public QuestObjectiveProgressRuntime(
            QuestRuntime quests = null,
            QuestParticipationRuntime participation = null,
            DefinitionRegistry definitionRegistry = null,
            string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            Configure(quests, participation, definitionRegistry, runtimeWorldId);
        }

        public long Revision => revision;
        public string WorldId => worldId ?? string.Empty;
        public int ObjectiveCount => objectivesById.Count;
        public IReadOnlyList<QuestObjectiveRuntimeEventData> Events => events.Select(value => value.Clone()).ToArray();

        public void Configure(QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry definitionRegistry, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            questRuntime = quests;
            participationRuntime = participation;
            registry = definitionRegistry;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId.Trim();
        }

        public QuestObjectiveOperationResult InstantiateForAssignment(QuestAssignmentSnapshot assignment, QuestObjectiveStateContext stateContext = null, string transactionId = null, bool preview = false)
        {
            if (disposed) return Fail(QuestObjectiveOperationStatus.Disposed, "Quest objective runtime is disposed.");
            if (assignment == null || string.IsNullOrWhiteSpace(assignment.AssignmentId)) return Fail(QuestObjectiveOperationStatus.MissingAssignment, "Quest assignment is missing.");
            if (!string.Equals(assignment.WorldId, worldId, StringComparison.Ordinal)) return Fail(QuestObjectiveOperationStatus.WrongWorld, $"Assignment world '{assignment.WorldId}' does not match objective runtime world '{worldId}'.");
            if (objectivesByAssignment.ContainsKey(assignment.AssignmentId))
            {
                return QuestObjectiveOperationResult.Success("Quest objectives already exist for assignment.", revision, revision, RecordsForAssignment(assignment.AssignmentId), duplicate: true);
            }

            if (!TryResolveQuestAndDefinition(assignment.QuestId, out QuestSnapshot quest, out QuestDefinition definition, out string failure)) return Fail(QuestObjectiveOperationStatus.MissingQuest, failure);
            QuestObjectiveDefinitionData[] definitions = definition.ObjectiveDefinitions.ToArray();
            if (definitions.Length == 0) return Fail(QuestObjectiveOperationStatus.MissingObjectiveDefinition, $"Quest definition '{definition.QuestId}' declares no objective definitions.");

            List<QuestObjectiveRecordData> created = new List<QuestObjectiveRecordData>();
            foreach (QuestObjectiveDefinitionData objectiveDefinition in definitions.OrderBy(value => value.sequenceOrder).ThenBy(value => value.objectiveDefinitionId, StringComparer.Ordinal))
            {
                QuestObjectiveRecordData record = CreateRecord(assignment, quest, definition, objectiveDefinition);
                created.Add(record);
            }

            if (preview)
            {
                return QuestObjectiveOperationResult.Success("Quest objective instantiation previewed.", revision, revision, created, preview: true);
            }

            long before = revision;
            foreach (QuestObjectiveRecordData record in created)
            {
                objectivesById[record.objectiveId] = record.Clone();
                AddToIndexes(record);
            }

            revision++;
            RecordTransaction(transactionId, "InstantiateForAssignment", string.Empty, assignment.AssignmentId);
            RecordEvent(transactionId, QuestObjectiveEventKind.ObjectivesInstantiated, null, assignment.AssignmentId, assignment.QuestId, 0, created.Count, QuestObjectiveLifecycleState.Unknown, QuestObjectiveLifecycleState.Active, string.Empty, assignment.AssignedWorldTime);
            ActivateReadyObjectives(assignment.AssignmentId, stateContext?.Clone() ?? new QuestObjectiveStateContext { assignmentId = assignment.AssignmentId, personId = assignment.AssigneePersonId, worldTime = assignment.AssignedWorldTime });
            return QuestObjectiveOperationResult.Success("Quest objectives instantiated.", before, revision, RecordsForAssignment(assignment.AssignmentId));
        }

        public QuestObjectiveOperationResult ApplySignal(QuestObjectiveSignal signal)
        {
            if (disposed) return Fail(QuestObjectiveOperationStatus.Disposed, "Quest objective runtime is disposed.");
            QuestObjectiveSignal actual = signal?.Clone() ?? new QuestObjectiveSignal();
            if (!actual.committed) return Fail(QuestObjectiveOperationStatus.NotCommitted, "Quest objective progress requires a committed domain event.");
            if (actual.category == QuestObjectiveCategory.Unknown) return Fail(QuestObjectiveOperationStatus.InvalidRequest, "Quest objective signal has unknown category.");
            if (string.IsNullOrWhiteSpace(actual.sourceEventId)) return Fail(QuestObjectiveOperationStatus.InvalidRequest, "Quest objective signal requires a stable source event ID.");

            List<QuestObjectiveRecordData> candidates = CandidateRecords(actual).ToList();
            if (candidates.Count == 0) return Fail(QuestObjectiveOperationStatus.EventNotMatched, "No objective matched the progress signal.");
            if (actual.preview) return QuestObjectiveOperationResult.Success("Quest objective signal previewed.", revision, revision, candidates, preview: true);

            long before = revision;
            List<QuestObjectiveRecordData> changed = new List<QuestObjectiveRecordData>();
            foreach (QuestObjectiveRecordData record in candidates)
            {
                if (!TryApplySignalToRecord(record, actual, out QuestObjectiveRecordData updated, out QuestObjectiveOperationStatus skipped))
                {
                    if (skipped != QuestObjectiveOperationStatus.AlreadyCounted && skipped != QuestObjectiveOperationStatus.EventTooEarly)
                    {
                        continue;
                    }

                    continue;
                }

                objectivesById[updated.objectiveId] = updated.Clone();
                changed.Add(updated.Clone());
            }

            if (changed.Count == 0)
            {
                return Fail(QuestObjectiveOperationStatus.AlreadyCounted, "Quest objective signal did not change any matching objective.");
            }

            revision++;
            RecordTransaction(actual.transactionId, "ApplySignal", changed[0].objectiveId, changed[0].assignmentId);
            foreach (QuestObjectiveRecordData record in changed.OrderBy(value => value.objectiveId, StringComparer.Ordinal))
            {
                RecordEvent(actual.transactionId, record.satisfied ? QuestObjectiveEventKind.ObjectiveSatisfied : QuestObjectiveEventKind.ProgressUpdated, record, record.currentValue, record.currentValue, record.lifecycleState, record.lifecycleState, actual.sourceEventId, actual.worldTime);
                ActivateReadyObjectives(record.assignmentId, new QuestObjectiveStateContext { assignmentId = record.assignmentId, personId = record.assigneePersonId, worldTime = actual.worldTime });
            }

            return QuestObjectiveOperationResult.Success("Quest objective signal applied.", before, revision, changed);
        }

        public QuestObjectiveOperationResult ReconcileState(QuestObjectiveStateContext context)
        {
            if (disposed) return Fail(QuestObjectiveOperationStatus.Disposed, "Quest objective runtime is disposed.");
            QuestObjectiveStateContext actual = (context ?? new QuestObjectiveStateContext()).Clone();
            if (string.IsNullOrWhiteSpace(actual.assignmentId)) return Fail(QuestObjectiveOperationStatus.MissingAssignment, "State reconciliation requires an assignment ID.");
            if (!objectivesByAssignment.TryGetValue(actual.assignmentId, out HashSet<string> ids)) return Fail(QuestObjectiveOperationStatus.MissingObjective, $"Assignment '{actual.assignmentId}' has no objective records.");

            long before = revision;
            List<QuestObjectiveRecordData> changed = new List<QuestObjectiveRecordData>();
            foreach (string id in ids.OrderBy(value => value, StringComparer.Ordinal).ToArray())
            {
                QuestObjectiveRecordData record = objectivesById[id];
                if (record.lifecycleState != QuestObjectiveLifecycleState.Active && record.lifecycleState != QuestObjectiveLifecycleState.Satisfied) continue;
                if (!UsesCurrentState(record)) continue;

                QuestObjectiveRecordData updated = record.Clone();
                int beforeValue = updated.currentValue;
                bool beforeSatisfied = updated.satisfied;
                int value = actual.facts?.Value(updated.category, TargetIdForDefinition(updated), SecondaryTargetIdForDefinition(updated)) ?? 0;
                updated.currentValue = Math.Max(0, value);
                ApplySatisfaction(updated, actual.worldTime);
                if (updated.satisfactionPolicy == QuestObjectiveSatisfactionPolicy.StickyOnceSatisfied && beforeSatisfied)
                {
                    updated.satisfied = true;
                    updated.lifecycleState = QuestObjectiveLifecycleState.Satisfied;
                    updated.currentValue = Math.Max(updated.currentValue, updated.targetValue);
                }

                if (beforeValue != updated.currentValue || beforeSatisfied != updated.satisfied)
                {
                    updated.revision++;
                    objectivesById[id] = updated.Clone();
                    changed.Add(updated.Clone());
                    RecordEvent(null, updated.satisfied ? QuestObjectiveEventKind.ObjectiveSatisfied : QuestObjectiveEventKind.ObjectiveUnsatisfied, updated, beforeValue, updated.currentValue, record.lifecycleState, updated.lifecycleState, string.Empty, actual.worldTime);
                }
            }

            if (changed.Count == 0) return QuestObjectiveOperationResult.Success("Quest objective state reconciliation produced no changes.", revision, revision, RecordsForAssignment(actual.assignmentId), duplicate: true);
            revision++;
            ActivateReadyObjectives(actual.assignmentId, actual);
            return QuestObjectiveOperationResult.Success("Quest objective state reconciled.", before, revision, changed);
        }

        public QuestObjectiveOperationResult SuspendAssignment(string assignmentId, double worldTime, string transactionId = null)
        {
            return TransitionAssignmentObjectives(assignmentId, QuestObjectiveLifecycleState.Suspended, QuestObjectiveEventKind.ObjectiveSuspended, worldTime, transactionId);
        }

        public QuestObjectiveOperationResult ResumeAssignment(string assignmentId, double worldTime, string transactionId = null, QuestObjectiveStateContext stateContext = null)
        {
            QuestObjectiveOperationResult result = TransitionAssignmentObjectives(assignmentId, QuestObjectiveLifecycleState.Active, QuestObjectiveEventKind.ObjectiveResumed, worldTime, transactionId);
            if (result.Succeeded)
            {
                ActivateReadyObjectives(assignmentId, stateContext ?? new QuestObjectiveStateContext { assignmentId = assignmentId, worldTime = worldTime });
            }

            return result;
        }

        public QuestObjectiveOperationResult AbandonAssignment(string assignmentId, double worldTime, string transactionId = null)
        {
            return TransitionAssignmentObjectives(assignmentId, QuestObjectiveLifecycleState.Abandoned, QuestObjectiveEventKind.ObjectiveAbandoned, worldTime, transactionId);
        }

        public QuestObjectiveOperationResult WithdrawAssignment(string assignmentId, double worldTime, string transactionId = null)
        {
            return TransitionAssignmentObjectives(assignmentId, QuestObjectiveLifecycleState.Withdrawn, QuestObjectiveEventKind.ObjectiveWithdrawn, worldTime, transactionId);
        }

        public bool TryGetObjective(string objectiveId, out QuestObjectiveSnapshot snapshot)
        {
            snapshot = null;
            if (!objectivesById.TryGetValue(N(objectiveId), out QuestObjectiveRecordData record)) return false;
            snapshot = new QuestObjectiveSnapshot(record);
            return true;
        }

        public IReadOnlyList<QuestObjectiveSnapshot> QueryObjectives(QuestObjectiveQuery query = null)
        {
            QuestObjectiveQuery actual = query ?? new QuestObjectiveQuery();
            IEnumerable<QuestObjectiveRecordData> records = objectivesById.Values;
            if (!string.IsNullOrWhiteSpace(actual.worldId)) records = records.Where(record => string.Equals(record.worldId, actual.worldId, StringComparison.Ordinal));
            if (!actual.includeTerminal) records = records.Where(record => !record.IsTerminal);
            if (!string.IsNullOrWhiteSpace(actual.questId)) records = records.Where(record => string.Equals(record.questId, actual.questId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.assignmentId)) records = records.Where(record => string.Equals(record.assignmentId, actual.assignmentId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.objectiveId)) records = records.Where(record => string.Equals(record.objectiveId, actual.objectiveId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.objectiveDefinitionId)) records = records.Where(record => string.Equals(record.objectiveDefinitionId, actual.objectiveDefinitionId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.assigneePersonId)) records = records.Where(record => string.Equals(record.assigneePersonId, actual.assigneePersonId, StringComparison.Ordinal));
            if (actual.category.HasValue) records = records.Where(record => record.category == actual.category.Value);
            if (actual.lifecycleState.HasValue) records = records.Where(record => record.lifecycleState == actual.lifecycleState.Value);
            records = records.Where(record => CanSee(record, actual.access, actual.requesterPersonId));
            return records.OrderBy(record => record.assignmentId, StringComparer.Ordinal).ThenBy(record => record.objectiveDefinitionId, StringComparer.Ordinal).Select(record => new QuestObjectiveSnapshot(record)).ToArray();
        }

        public QuestAssignmentObjectiveSummary SummarizeAssignment(string assignmentId, QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly, string requesterPersonId = null)
        {
            QuestObjectiveRecordData[] records = RecordsForAssignment(assignmentId).ToArray();
            bool privileged = access == QuestVisibilityAccess.PrivilegedDiagnostic;
            QuestObjectiveRecordData[] visible = records.Where(record => CanSee(record, access, requesterPersonId)).ToArray();
            QuestObjectiveRecordData[] required = records.Where(IsRequired).ToArray();
            int requiredSatisfied = required.Count(record => record.satisfied);
            int requiredRemaining = required.Length - requiredSatisfied;
            int optionalSatisfied = records.Count(record => !IsRequired(record) && record.satisfied);
            return new QuestAssignmentObjectiveSummary(assignmentId, visible.Length, privileged ? requiredSatisfied : visible.Count(record => IsRequired(record) && record.satisfied), privileged ? requiredRemaining : -1, optionalSatisfied, !privileged, requiredRemaining == 0 && required.Length > 0);
        }

        public QuestObjectiveProgressRuntimeSaveData CreateSaveData()
        {
            return new QuestObjectiveProgressRuntimeSaveData
            {
                worldId = worldId,
                revision = revision,
                objectives = objectivesById.Values.OrderBy(record => record.objectiveId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                events = events.OrderBy(record => record.runtimeRevision).ThenBy(record => record.eventId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(record => record.transactionId, StringComparer.Ordinal).Select(record => record.Clone()).ToList()
            };
        }

        public QuestObjectiveOperationResult RestoreFromSaveData(QuestObjectiveProgressRuntimeSaveData saveData, QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry definitionRegistry, string expectedWorldId = PersistenceService.LocalWorldId)
        {
            if (!ValidateSaveData(saveData, quests ?? questRuntime, participation ?? participationRuntime, definitionRegistry ?? registry, expectedWorldId, out string failure))
            {
                return Fail(QuestObjectiveOperationStatus.PersistenceInvalid, failure);
            }

            QuestObjectiveProgressRuntimeSaveData rollback = CreateSaveData();
            try
            {
                Configure(quests ?? questRuntime, participation ?? participationRuntime, definitionRegistry ?? registry, string.IsNullOrWhiteSpace(saveData.worldId) ? expectedWorldId : saveData.worldId);
                Clear();
                worldId = string.IsNullOrWhiteSpace(saveData.worldId) ? expectedWorldId : saveData.worldId;
                foreach (QuestObjectiveRecordData record in saveData.objectives ?? new List<QuestObjectiveRecordData>())
                {
                    objectivesById[record.objectiveId] = record.Clone();
                    AddToIndexes(record);
                }

                foreach (QuestObjectiveTransactionData transaction in saveData.transactions ?? new List<QuestObjectiveTransactionData>())
                {
                    transactionsById[transaction.transactionId] = transaction.Clone();
                }

                events.AddRange((saveData.events ?? new List<QuestObjectiveRuntimeEventData>()).Select(value => value.Clone()));
                revision = saveData.revision;
                return QuestObjectiveOperationResult.Success("Quest objective progress restored.", revision, revision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, questRuntime, participationRuntime, registry, worldId);
                return Fail(QuestObjectiveOperationStatus.RestoreFailed, $"Quest objective progress restore failed: {exception.Message}");
            }
        }

        public QuestObjectiveValidationReport ValidateRuntime()
        {
            ValidateSaveData(CreateSaveData(), questRuntime, participationRuntime, registry, worldId, out _, out QuestObjectiveValidationReport report);
            return report;
        }

        public static bool ValidateSaveData(QuestObjectiveProgressRuntimeSaveData saveData, QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry registry, string expectedWorldId, out string failure)
        {
            return ValidateSaveData(saveData, quests, participation, registry, expectedWorldId, out failure, out _);
        }

        public static bool ValidateSaveData(QuestObjectiveProgressRuntimeSaveData saveData, QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry registry, string expectedWorldId, out string failure, out QuestObjectiveValidationReport report)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            if (saveData == null)
            {
                errors.Add("Quest objective progress save data is missing.");
            }
            else
            {
                if (saveData.schemaVersion != QuestObjectiveProgressRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported quest objective progress save schema version {saveData.schemaVersion}.");
                string world = string.IsNullOrWhiteSpace(expectedWorldId) ? saveData.worldId : expectedWorldId;
                if (!string.IsNullOrWhiteSpace(world) && !string.Equals(saveData.worldId, world, StringComparison.Ordinal)) errors.Add($"Quest objective progress save world '{saveData.worldId}' does not match expected world '{world}'.");
                if (quests == null) errors.Add("Quest objective progress validation requires QuestRuntime.");
                if (participation == null) errors.Add("Quest objective progress validation requires QuestParticipationRuntime.");
                if (registry == null) errors.Add("Quest objective progress validation requires DefinitionRegistry.");

                HashSet<string> objectiveIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestObjectiveRecordData record in saveData.objectives ?? new List<QuestObjectiveRecordData>())
                {
                    ValidateObjectiveRecord(record, quests, participation, registry, objectiveIds, errors);
                }

                HashSet<string> transactionIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (QuestObjectiveTransactionData transaction in saveData.transactions ?? new List<QuestObjectiveTransactionData>())
                {
                    if (transaction == null || string.IsNullOrWhiteSpace(transaction.transactionId)) continue;
                    if (!transactionIds.Add(transaction.transactionId)) errors.Add($"Duplicate quest objective transaction ID '{transaction.transactionId}'.");
                }
            }

            report = new QuestObjectiveValidationReport(errors, warnings);
            failure = report.Succeeded ? string.Empty : string.Join(" | ", report.Errors);
            return report.Succeeded;
        }

        public void Clear()
        {
            objectivesById.Clear();
            objectivesByAssignment.Clear();
            eventObjectiveIndex.Clear();
            transactionsById.Clear();
            events.Clear();
            revision = 0L;
        }

        public void Dispose()
        {
            disposed = true;
            Clear();
        }

        private QuestObjectiveRecordData CreateRecord(QuestAssignmentSnapshot assignment, QuestSnapshot quest, QuestDefinition definition, QuestObjectiveDefinitionData objectiveDefinition)
        {
            string objectiveId = BuildObjectiveId(assignment.AssignmentId, objectiveDefinition.objectiveDefinitionId);
            QuestObjectiveLifecycleState state = objectiveDefinition.prerequisiteObjectiveDefinitionIds.Length == 0 ? QuestObjectiveLifecycleState.Active : QuestObjectiveLifecycleState.Locked;
            return new QuestObjectiveRecordData
            {
                objectiveId = objectiveId,
                objectiveDefinitionId = objectiveDefinition.objectiveDefinitionId,
                groupDefinitionId = objectiveDefinition.groupDefinitionId,
                questId = assignment.QuestId,
                questDefinitionId = quest.QuestDefinitionId,
                assignmentId = assignment.AssignmentId,
                assigneePersonId = assignment.AssigneePersonId,
                worldId = worldId,
                ownershipScope = objectiveDefinition.ownershipScope == QuestObjectiveOwnershipScope.Unknown ? QuestObjectiveOwnershipScope.PerAssignment : objectiveDefinition.ownershipScope,
                lifecycleState = state,
                classification = objectiveDefinition.classification == QuestObjectiveRequirementClassification.Unknown ? QuestObjectiveRequirementClassification.Required : objectiveDefinition.classification,
                visibility = objectiveDefinition.visibility == QuestObjectiveVisibility.Unknown ? QuestObjectiveVisibility.Public : objectiveDefinition.visibility,
                category = objectiveDefinition.category,
                progressModel = objectiveDefinition.progressModel,
                progressSource = objectiveDefinition.progressSource,
                satisfactionPolicy = objectiveDefinition.satisfactionPolicy == QuestObjectiveSatisfactionPolicy.Unknown ? QuestObjectiveSatisfactionPolicy.StickyOnceSatisfied : objectiveDefinition.satisfactionPolicy,
                repetitionPolicy = objectiveDefinition.repetitionPolicy == QuestObjectiveRepetitionPolicy.Unknown ? QuestObjectiveRepetitionPolicy.CountSourceEventOnce : objectiveDefinition.repetitionPolicy,
                currentValue = 0,
                targetValue = objectiveDefinition.EffectiveTarget,
                satisfied = false,
                activatedWorldTime = state == QuestObjectiveLifecycleState.Active ? assignment.AssignedWorldTime : -1d,
                prerequisiteObjectiveDefinitionIds = objectiveDefinition.prerequisiteObjectiveDefinitionIds,
                revision = 1L
            };
        }

        private void ActivateReadyObjectives(string assignmentId, QuestObjectiveStateContext stateContext)
        {
            if (!objectivesByAssignment.TryGetValue(N(assignmentId), out HashSet<string> ids)) return;
            bool changed;
            do
            {
                changed = false;
                foreach (string id in ids.OrderBy(value => value, StringComparer.Ordinal).ToArray())
                {
                    QuestObjectiveRecordData record = objectivesById[id];
                    if (record.lifecycleState != QuestObjectiveLifecycleState.Locked) continue;
                    if (!PrerequisitesSatisfied(record, assignmentId)) continue;

                    record.lifecycleState = QuestObjectiveLifecycleState.Active;
                    record.activatedWorldTime = stateContext?.worldTime ?? 0d;
                    record.revision++;
                    objectivesById[id] = record.Clone();
                    RecordEvent(null, QuestObjectiveEventKind.ObjectiveActivated, record, record.currentValue, record.currentValue, QuestObjectiveLifecycleState.Locked, QuestObjectiveLifecycleState.Active, string.Empty, record.activatedWorldTime);
                    changed = true;
                }
            }
            while (changed);

            if (stateContext != null)
            {
                ReconcileActivationState(assignmentId, stateContext);
            }
        }

        private void ReconcileActivationState(string assignmentId, QuestObjectiveStateContext context)
        {
            if (!objectivesByAssignment.TryGetValue(N(assignmentId), out HashSet<string> ids)) return;
            foreach (string id in ids.OrderBy(value => value, StringComparer.Ordinal).ToArray())
            {
                QuestObjectiveRecordData record = objectivesById[id];
                if (record.lifecycleState != QuestObjectiveLifecycleState.Active || !UsesCurrentState(record)) continue;
                int value = context.facts?.Value(record.category, TargetIdForDefinition(record), SecondaryTargetIdForDefinition(record)) ?? 0;
                if (value <= 0) continue;
                record.currentValue = value;
                ApplySatisfaction(record, context.worldTime);
                record.revision++;
                objectivesById[id] = record.Clone();
            }
        }

        private IEnumerable<QuestObjectiveRecordData> CandidateRecords(QuestObjectiveSignal signal)
        {
            if (!eventObjectiveIndex.TryGetValue(signal.category, out HashSet<string> ids)) return Array.Empty<QuestObjectiveRecordData>();
            return ids
                .Select(id => objectivesById.TryGetValue(id, out QuestObjectiveRecordData record) ? record : null)
                .Where(record => record != null && !record.IsTerminal)
                .Where(record => string.IsNullOrWhiteSpace(signal.assignmentId) || string.Equals(record.assignmentId, signal.assignmentId, StringComparison.Ordinal))
                .Where(record => string.IsNullOrWhiteSpace(signal.questId) || string.Equals(record.questId, signal.questId, StringComparison.Ordinal))
                .Where(record => ParticipantMatches(record, signal.participantPersonId))
                .Where(record => ParticipantMatches(record, signal.actorPersonId))
                .Where(record => record.lifecycleState == QuestObjectiveLifecycleState.Active || record.lifecycleState == QuestObjectiveLifecycleState.Satisfied || AllowsLockedProgress(record))
                .Where(record => TargetMatches(record, signal));
        }

        private bool ParticipantMatches(QuestObjectiveRecordData record, string personId)
        {
            if (string.IsNullOrWhiteSpace(personId)) return true;
            if (string.Equals(record.assigneePersonId, personId, StringComparison.Ordinal)) return true;
            return participationRuntime != null
                && participationRuntime.TryGetAssignment(record.assignmentId, out QuestAssignmentSnapshot assignment)
                && assignment.ParticipantPersonIds.Contains(personId, StringComparer.Ordinal);
        }

        private bool TryApplySignalToRecord(QuestObjectiveRecordData record, QuestObjectiveSignal signal, out QuestObjectiveRecordData updated, out QuestObjectiveOperationStatus skippedStatus)
        {
            updated = record.Clone();
            skippedStatus = QuestObjectiveOperationStatus.Succeeded;
            HashSet<string> countedSources = new HashSet<string>(updated.countedSourceEventIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (updated.repetitionPolicy != QuestObjectiveRepetitionPolicy.CountEveryCommittedEvent && countedSources.Contains(signal.sourceEventId))
            {
                skippedStatus = QuestObjectiveOperationStatus.AlreadyCounted;
                return false;
            }

            if (updated.lifecycleState == QuestObjectiveLifecycleState.Satisfied && updated.satisfactionPolicy == QuestObjectiveSatisfactionPolicy.StickyOnceSatisfied)
            {
                skippedStatus = QuestObjectiveOperationStatus.AlreadyCounted;
                return false;
            }

            if (updated.lifecycleState != QuestObjectiveLifecycleState.Active && !AllowsLockedProgress(updated))
            {
                skippedStatus = QuestObjectiveOperationStatus.EventTooEarly;
                return false;
            }

            if (updated.activatedWorldTime >= 0d && signal.worldTime < updated.activatedWorldTime && !AllowsLockedProgress(updated))
            {
                skippedStatus = QuestObjectiveOperationStatus.EventTooEarly;
                return false;
            }

            int beforeValue = updated.currentValue;
            string targetId = QuestObjectiveProgressModelUtility.TargetId(signal.target);
            HashSet<string> targets = new HashSet<string>(updated.countedTargetIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            switch (updated.progressModel)
            {
                case QuestObjectiveProgressModel.BooleanEvent:
                    updated.currentValue = updated.targetValue;
                    break;
                case QuestObjectiveProgressModel.Counter:
                case QuestObjectiveProgressModel.QuantityCumulative:
                    updated.currentValue = Math.Min(updated.targetValue, updated.currentValue + Math.Max(1, signal.amount));
                    break;
                case QuestObjectiveProgressModel.UniqueTargetCount:
                case QuestObjectiveProgressModel.SetMembership:
                    if (targets.Contains(targetId))
                    {
                        skippedStatus = QuestObjectiveOperationStatus.AlreadyCounted;
                        return false;
                    }

                    targets.Add(targetId);
                    updated.currentValue = Math.Min(updated.targetValue, targets.Count);
                    updated.countedTargetIds = targets.OrderBy(value => value, StringComparer.Ordinal).ToArray();
                    break;
                default:
                    skippedStatus = QuestObjectiveOperationStatus.UnsupportedProgressModel;
                    return false;
            }

            countedSources.Add(signal.sourceEventId);
            updated.countedSourceEventIds = countedSources.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            AddEvidence(updated, signal);
            ApplySatisfaction(updated, signal.worldTime);
            if (updated.currentValue == beforeValue && !updated.satisfied)
            {
                skippedStatus = QuestObjectiveOperationStatus.EventNotMatched;
                return false;
            }

            updated.revision++;
            return true;
        }

        private QuestObjectiveOperationResult TransitionAssignmentObjectives(string assignmentId, QuestObjectiveLifecycleState target, QuestObjectiveEventKind kind, double worldTime, string transactionId)
        {
            if (disposed) return Fail(QuestObjectiveOperationStatus.Disposed, "Quest objective runtime is disposed.");
            assignmentId = N(assignmentId);
            if (!objectivesByAssignment.TryGetValue(assignmentId, out HashSet<string> ids)) return Fail(QuestObjectiveOperationStatus.MissingAssignment, $"Assignment '{assignmentId}' has no objective records.");

            long before = revision;
            List<QuestObjectiveRecordData> changed = new List<QuestObjectiveRecordData>();
            foreach (string id in ids.OrderBy(value => value, StringComparer.Ordinal).ToArray())
            {
                QuestObjectiveRecordData record = objectivesById[id];
                if (record.satisfied && target == QuestObjectiveLifecycleState.Active) continue;
                if (record.IsTerminal) continue;
                QuestObjectiveLifecycleState beforeState = record.lifecycleState;
                record.lifecycleState = record.satisfied && target == QuestObjectiveLifecycleState.Active ? QuestObjectiveLifecycleState.Satisfied : target;
                record.revision++;
                objectivesById[id] = record.Clone();
                changed.Add(record.Clone());
                RecordEvent(transactionId, kind, record, record.currentValue, record.currentValue, beforeState, record.lifecycleState, string.Empty, worldTime);
            }

            if (changed.Count == 0) return QuestObjectiveOperationResult.Success("Quest objective lifecycle transition produced no changes.", revision, revision, RecordsForAssignment(assignmentId), duplicate: true);
            revision++;
            RecordTransaction(transactionId, target.ToString(), string.Empty, assignmentId);
            return QuestObjectiveOperationResult.Success("Quest objective lifecycle transitioned.", before, revision, changed);
        }

        private bool PrerequisitesSatisfied(QuestObjectiveRecordData record, string assignmentId)
        {
            string[] prerequisites = record.prerequisiteObjectiveDefinitionIds ?? Array.Empty<string>();
            if (prerequisites.Length == 0) return true;
            QuestObjectiveRecordData[] records = RecordsForAssignment(assignmentId).ToArray();
            return prerequisites.All(required => records.Any(candidate => string.Equals(candidate.objectiveDefinitionId, required, StringComparison.Ordinal) && candidate.satisfied));
        }

        private void ApplySatisfaction(QuestObjectiveRecordData record, double worldTime)
        {
            bool nowSatisfied = record.currentValue >= Math.Max(1, record.targetValue);
            if (record.satisfactionPolicy == QuestObjectiveSatisfactionPolicy.StickyOnceSatisfied && record.satisfied)
            {
                nowSatisfied = true;
            }

            record.satisfied = nowSatisfied;
            if (nowSatisfied)
            {
                record.lifecycleState = QuestObjectiveLifecycleState.Satisfied;
                if (record.satisfiedWorldTime < 0d) record.satisfiedWorldTime = worldTime;
            }
            else if (record.lifecycleState == QuestObjectiveLifecycleState.Satisfied && record.satisfactionPolicy == QuestObjectiveSatisfactionPolicy.DynamicWhileTrue)
            {
                record.lifecycleState = QuestObjectiveLifecycleState.Active;
                record.satisfiedWorldTime = -1d;
            }
        }

        private void AddEvidence(QuestObjectiveRecordData record, QuestObjectiveSignal signal)
        {
            List<QuestObjectiveProgressEvidenceData> evidence = new List<QuestObjectiveProgressEvidenceData>(record.evidence ?? Array.Empty<QuestObjectiveProgressEvidenceData>())
            {
                new QuestObjectiveProgressEvidenceData
                {
                    evidenceId = $"quest-objective-evidence.{record.objectiveId}.{(record.evidence?.Length ?? 0):000}",
                    sourceEventId = signal.sourceEventId,
                    sourceRuntimeId = signal.sourceRuntimeId,
                    category = signal.category,
                    target = signal.target?.Clone() ?? new UnityIsekaiGame.Knowledge.Access.InformationSubjectReferenceData(),
                    actorPersonId = string.IsNullOrWhiteSpace(signal.actorPersonId) ? signal.participantPersonId : signal.actorPersonId,
                    amount = signal.amount,
                    worldTime = signal.worldTime,
                    diagnostics = "committed-domain-signal"
                }
            };
            record.evidence = evidence.OrderBy(value => value.worldTime).ThenBy(value => value.evidenceId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        }

        private void AddToIndexes(QuestObjectiveRecordData record)
        {
            if (!objectivesByAssignment.TryGetValue(record.assignmentId, out HashSet<string> assignmentIds))
            {
                assignmentIds = new HashSet<string>(StringComparer.Ordinal);
                objectivesByAssignment[record.assignmentId] = assignmentIds;
            }

            assignmentIds.Add(record.objectiveId);
            if (!UsesCurrentState(record))
            {
                if (!eventObjectiveIndex.TryGetValue(record.category, out HashSet<string> eventIds))
                {
                    eventIds = new HashSet<string>(StringComparer.Ordinal);
                    eventObjectiveIndex[record.category] = eventIds;
                }

                eventIds.Add(record.objectiveId);
            }
        }

        private IEnumerable<QuestObjectiveRecordData> RecordsForAssignment(string assignmentId)
        {
            if (!objectivesByAssignment.TryGetValue(N(assignmentId), out HashSet<string> ids)) return Array.Empty<QuestObjectiveRecordData>();
            return ids.Select(id => objectivesById.TryGetValue(id, out QuestObjectiveRecordData record) ? record.Clone() : null).Where(value => value != null).OrderBy(value => value.objectiveDefinitionId, StringComparer.Ordinal).ToArray();
        }

        private bool TryResolveQuestAndDefinition(string questId, out QuestSnapshot quest, out QuestDefinition definition, out string failure)
        {
            quest = null;
            definition = null;
            if (questRuntime == null) { failure = "QuestRuntime is missing."; return false; }
            if (registry == null) { failure = "DefinitionRegistry is missing."; return false; }
            if (!questRuntime.TryGetSnapshot(N(questId), out quest)) { failure = $"Quest '{N(questId)}' is missing."; return false; }
            if (!registry.TryGet(quest.QuestDefinitionId, out definition)) { failure = $"Quest definition '{quest.QuestDefinitionId}' is missing."; return false; }
            failure = string.Empty;
            return true;
        }

        private static void ValidateObjectiveRecord(QuestObjectiveRecordData record, QuestRuntime quests, QuestParticipationRuntime participation, DefinitionRegistry registry, ISet<string> ids, ICollection<string> errors)
        {
            if (record == null) { errors.Add("Quest objective record is null."); return; }
            if (string.IsNullOrWhiteSpace(record.objectiveId)) errors.Add("Quest objective record is missing an objective ID.");
            else if (!ids.Add(record.objectiveId)) errors.Add($"Duplicate quest objective ID '{record.objectiveId}'.");
            if (string.IsNullOrWhiteSpace(record.assignmentId) || participation == null || !participation.TryGetAssignment(record.assignmentId, out QuestAssignmentSnapshot assignment)) errors.Add($"Quest objective '{record.objectiveId}' references missing assignment '{record.assignmentId}'.");
            else if (!string.Equals(assignment.QuestId, record.questId, StringComparison.Ordinal)) errors.Add($"Quest objective '{record.objectiveId}' assignment quest mismatch.");
            if (string.IsNullOrWhiteSpace(record.questId) || quests == null || !quests.TryGetSnapshot(record.questId, out QuestSnapshot quest)) errors.Add($"Quest objective '{record.objectiveId}' references missing quest '{record.questId}'.");
            else if (registry == null || !registry.TryGet(quest.QuestDefinitionId, out QuestDefinition definition)) errors.Add($"Quest objective '{record.objectiveId}' references missing quest definition '{quest.QuestDefinitionId}'.");
            else if (!definition.ObjectiveDefinitions.Any(item => string.Equals(item.objectiveDefinitionId, record.objectiveDefinitionId, StringComparison.Ordinal))) errors.Add($"Quest objective '{record.objectiveId}' references missing objective definition '{record.objectiveDefinitionId}'.");
            if (record.lifecycleState == QuestObjectiveLifecycleState.Unknown) errors.Add($"Quest objective '{record.objectiveId}' has unknown lifecycle state.");
            if (record.category == QuestObjectiveCategory.Unknown) errors.Add($"Quest objective '{record.objectiveId}' has unknown category.");
            if (record.progressModel == QuestObjectiveProgressModel.Unknown) errors.Add($"Quest objective '{record.objectiveId}' has unknown progress model.");
            if (record.currentValue < 0) errors.Add($"Quest objective '{record.objectiveId}' has negative progress.");
            if (record.targetValue <= 0) errors.Add($"Quest objective '{record.objectiveId}' has non-positive target value.");
        }

        private static bool UsesCurrentState(QuestObjectiveRecordData record)
        {
            return record.progressSource == QuestObjectiveProgressSource.CurrentStateQuery
                || record.progressModel == QuestObjectiveProgressModel.BooleanState
                || record.progressModel == QuestObjectiveProgressModel.QuantityCurrent
                || record.progressModel == QuestObjectiveProgressModel.Threshold;
        }

        private static bool AllowsLockedProgress(QuestObjectiveRecordData record)
        {
            return false;
        }

        private bool TargetMatches(QuestObjectiveRecordData record, QuestObjectiveSignal signal)
        {
            if (!TryGetObjectiveDefinition(record, out QuestObjectiveDefinitionData definition)) return false;
            string signalTarget = QuestObjectiveProgressModelUtility.TargetId(signal.target);
            string configuredTarget = QuestObjectiveProgressModelUtility.TargetId(definition.target);
            if (string.IsNullOrWhiteSpace(configuredTarget)) return true;
            if (string.Equals(configuredTarget, signalTarget, StringComparison.Ordinal)) return true;
            return definition.alternativeTargetIds.Contains(signalTarget, StringComparer.Ordinal);
        }

        private bool TryGetObjectiveDefinition(QuestObjectiveRecordData record, out QuestObjectiveDefinitionData definition)
        {
            definition = null;
            if (registry == null || !registry.TryGet(record.questDefinitionId, out QuestDefinition questDefinition)) return false;
            definition = questDefinition.ObjectiveDefinitions.FirstOrDefault(item => string.Equals(item.objectiveDefinitionId, record.objectiveDefinitionId, StringComparison.Ordinal));
            return definition != null;
        }

        private string TargetIdForDefinition(QuestObjectiveRecordData record)
        {
            return TryGetObjectiveDefinition(record, out QuestObjectiveDefinitionData definition) ? QuestObjectiveProgressModelUtility.TargetId(definition.target) : string.Empty;
        }

        private string SecondaryTargetIdForDefinition(QuestObjectiveRecordData record)
        {
            return TryGetObjectiveDefinition(record, out QuestObjectiveDefinitionData definition) ? QuestObjectiveProgressModelUtility.TargetId(definition.secondaryTarget) : string.Empty;
        }

        private static bool IsRequired(QuestObjectiveRecordData record)
        {
            return record.classification == QuestObjectiveRequirementClassification.Required || record.classification == QuestObjectiveRequirementClassification.HiddenRequired;
        }

        private static bool CanSee(QuestObjectiveRecordData record, QuestVisibilityAccess access, string requesterPersonId)
        {
            if (access == QuestVisibilityAccess.PrivilegedDiagnostic) return true;
            if (record.visibility == QuestObjectiveVisibility.Hidden || record.visibility == QuestObjectiveVisibility.Secret || record.visibility == QuestObjectiveVisibility.Diagnostic) return false;
            if (record.visibility == QuestObjectiveVisibility.RecipientKnown) return access == QuestVisibilityAccess.Recipient && string.Equals(N(requesterPersonId), record.assigneePersonId, StringComparison.Ordinal);
            if (record.visibility == QuestObjectiveVisibility.Restricted) return access == QuestVisibilityAccess.OrganizationMember || access == QuestVisibilityAccess.Government;
            return true;
        }

        private void RecordTransaction(string transactionId, string operation, string objectiveId, string assignmentId)
        {
            if (string.IsNullOrWhiteSpace(transactionId)) return;
            transactionsById[transactionId] = new QuestObjectiveTransactionData { transactionId = transactionId, operation = operation, objectiveId = objectiveId, assignmentId = assignmentId, runtimeRevision = revision };
        }

        private void RecordEvent(string transactionId, QuestObjectiveEventKind kind, QuestObjectiveRecordData record, int beforeValue, int afterValue, QuestObjectiveLifecycleState beforeState, QuestObjectiveLifecycleState afterState, string sourceEventId, double worldTime)
        {
            RecordEvent(transactionId, kind, record?.objectiveId, record?.assignmentId, record?.questId, beforeValue, afterValue, beforeState, afterState, sourceEventId, worldTime, record?.objectiveDefinitionId);
        }

        private void RecordEvent(string transactionId, QuestObjectiveEventKind kind, string objectiveId, string assignmentId, string questId, int beforeValue, int afterValue, QuestObjectiveLifecycleState beforeState, QuestObjectiveLifecycleState afterState, string sourceEventId, double worldTime, string objectiveDefinitionId = "")
        {
            events.Add(new QuestObjectiveRuntimeEventData
            {
                eventId = $"quest-objective-event.{revision:000000}.{kind}.{events.Count:000}",
                transactionId = transactionId ?? string.Empty,
                objectiveId = objectiveId ?? string.Empty,
                objectiveDefinitionId = objectiveDefinitionId ?? string.Empty,
                questId = questId ?? string.Empty,
                assignmentId = assignmentId ?? string.Empty,
                eventKind = kind,
                beforeValue = beforeValue,
                afterValue = afterValue,
                beforeState = beforeState,
                afterState = afterState,
                sourceEventId = sourceEventId ?? string.Empty,
                worldTime = worldTime,
                runtimeRevision = revision
            });
        }

        private QuestObjectiveOperationResult Fail(QuestObjectiveOperationStatus status, string message)
        {
            return QuestObjectiveOperationResult.Failure(status, message, revision);
        }

        private static string BuildObjectiveId(string assignmentId, string objectiveDefinitionId)
        {
            return $"quest-objective.{N(assignmentId).Replace('.', '-')}.{N(objectiveDefinitionId).Replace('.', '-')}";
        }

        private static string N(string value) => QuestObjectiveProgressModelUtility.N(value);
    }
}
