using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Quests
{
    [Serializable]
    public sealed class QuestObjectiveDefinitionData
    {
        public string objectiveDefinitionId;
        public string groupDefinitionId;
        public string label;
        public string description;
        public QuestObjectiveCategory category;
        public QuestObjectiveProgressModel progressModel;
        public QuestObjectiveProgressSource progressSource;
        public QuestObjectiveRequirementClassification classification = QuestObjectiveRequirementClassification.Required;
        public QuestObjectiveVisibility visibility = QuestObjectiveVisibility.Public;
        public QuestObjectiveSatisfactionPolicy satisfactionPolicy = QuestObjectiveSatisfactionPolicy.StickyOnceSatisfied;
        public QuestObjectiveRepetitionPolicy repetitionPolicy = QuestObjectiveRepetitionPolicy.CountSourceEventOnce;
        public QuestObjectiveProgressBeforeActivationPolicy beforeActivationPolicy = QuestObjectiveProgressBeforeActivationPolicy.Ignore;
        public QuestObjectiveOwnershipScope ownershipScope = QuestObjectiveOwnershipScope.PerAssignment;
        public InformationSubjectReferenceData target = new InformationSubjectReferenceData();
        public InformationSubjectReferenceData secondaryTarget = new InformationSubjectReferenceData();
        public string[] alternativeTargetIds = Array.Empty<string>();
        public string[] prerequisiteObjectiveDefinitionIds = Array.Empty<string>();
        public string[] tagIds = Array.Empty<string>();
        public int targetAmount = 1;
        public int thresholdValue;
        public int sequenceOrder;
        public bool requiredForCompletion = true;
        public bool allowProgressWhileLocked;
        public string failureConditionId;
        public string validationNotes;

        public QuestObjectiveDefinitionData Clone()
        {
            return new QuestObjectiveDefinitionData
            {
                objectiveDefinitionId = N(objectiveDefinitionId),
                groupDefinitionId = N(groupDefinitionId),
                label = N(label),
                description = description ?? string.Empty,
                category = category,
                progressModel = progressModel,
                progressSource = progressSource,
                classification = classification,
                visibility = visibility,
                satisfactionPolicy = satisfactionPolicy,
                repetitionPolicy = repetitionPolicy,
                beforeActivationPolicy = beforeActivationPolicy,
                ownershipScope = ownershipScope,
                target = target?.Clone() ?? new InformationSubjectReferenceData(),
                secondaryTarget = secondaryTarget?.Clone() ?? new InformationSubjectReferenceData(),
                alternativeTargetIds = Clean(alternativeTargetIds),
                prerequisiteObjectiveDefinitionIds = Clean(prerequisiteObjectiveDefinitionIds),
                tagIds = Clean(tagIds),
                targetAmount = Math.Max(1, targetAmount),
                thresholdValue = thresholdValue,
                sequenceOrder = sequenceOrder,
                requiredForCompletion = requiredForCompletion,
                allowProgressWhileLocked = allowProgressWhileLocked,
                failureConditionId = N(failureConditionId),
                validationNotes = validationNotes ?? string.Empty
            };
        }

        public bool IsHidden => visibility == QuestObjectiveVisibility.Hidden || visibility == QuestObjectiveVisibility.Secret || classification == QuestObjectiveRequirementClassification.HiddenRequired;
        public bool IsRequired => classification == QuestObjectiveRequirementClassification.Required || classification == QuestObjectiveRequirementClassification.HiddenRequired || requiredForCompletion;
        public int EffectiveTarget => progressModel == QuestObjectiveProgressModel.Threshold ? thresholdValue : Math.Max(1, targetAmount);
        public bool UsesCurrentState => progressSource == QuestObjectiveProgressSource.CurrentStateQuery || progressModel == QuestObjectiveProgressModel.BooleanState || progressModel == QuestObjectiveProgressModel.QuantityCurrent || progressModel == QuestObjectiveProgressModel.Threshold;

        private static string N(string value) => QuestObjectiveProgressModelUtility.N(value);
        private static string[] Clean(IEnumerable<string> values) => QuestObjectiveProgressModelUtility.Clean(values);
    }

    [Serializable]
    public sealed class QuestObjectiveGroupDefinitionData
    {
        public string groupDefinitionId;
        public string label;
        public QuestObjectiveGroupPolicy policy = QuestObjectiveGroupPolicy.All;
        public int thresholdCount;
        public string[] objectiveDefinitionIds = Array.Empty<string>();
        public bool requiredForCompletion = true;
        public int sequenceOrder;

        public QuestObjectiveGroupDefinitionData Clone()
        {
            return new QuestObjectiveGroupDefinitionData
            {
                groupDefinitionId = N(groupDefinitionId),
                label = N(label),
                policy = policy,
                thresholdCount = thresholdCount,
                objectiveDefinitionIds = Clean(objectiveDefinitionIds),
                requiredForCompletion = requiredForCompletion,
                sequenceOrder = sequenceOrder
            };
        }

        private static string N(string value) => QuestObjectiveProgressModelUtility.N(value);
        private static string[] Clean(IEnumerable<string> values) => QuestObjectiveProgressModelUtility.Clean(values);
    }

    [Serializable]
    public sealed class QuestObjectiveProgressEvidenceData
    {
        public string evidenceId;
        public string sourceEventId;
        public string sourceRuntimeId;
        public QuestObjectiveCategory category;
        public InformationSubjectReferenceData target = new InformationSubjectReferenceData();
        public string actorPersonId;
        public int amount;
        public double worldTime;
        public string diagnostics;

        public QuestObjectiveProgressEvidenceData Clone()
        {
            return new QuestObjectiveProgressEvidenceData
            {
                evidenceId = N(evidenceId),
                sourceEventId = N(sourceEventId),
                sourceRuntimeId = N(sourceRuntimeId),
                category = category,
                target = target?.Clone() ?? new InformationSubjectReferenceData(),
                actorPersonId = N(actorPersonId),
                amount = amount,
                worldTime = worldTime,
                diagnostics = diagnostics ?? string.Empty
            };
        }

        private static string N(string value) => QuestObjectiveProgressModelUtility.N(value);
    }

    [Serializable]
    public sealed class QuestObjectiveRecordData
    {
        public string objectiveId;
        public string objectiveDefinitionId;
        public string groupDefinitionId;
        public string questId;
        public string questDefinitionId;
        public string assignmentId;
        public string assigneePersonId;
        public string worldId;
        public QuestObjectiveOwnershipScope ownershipScope = QuestObjectiveOwnershipScope.PerAssignment;
        public QuestObjectiveLifecycleState lifecycleState = QuestObjectiveLifecycleState.Locked;
        public QuestObjectiveRequirementClassification classification = QuestObjectiveRequirementClassification.Required;
        public QuestObjectiveVisibility visibility = QuestObjectiveVisibility.Public;
        public QuestObjectiveCategory category;
        public QuestObjectiveProgressModel progressModel;
        public QuestObjectiveProgressSource progressSource;
        public QuestObjectiveSatisfactionPolicy satisfactionPolicy = QuestObjectiveSatisfactionPolicy.StickyOnceSatisfied;
        public QuestObjectiveRepetitionPolicy repetitionPolicy = QuestObjectiveRepetitionPolicy.CountSourceEventOnce;
        public int currentValue;
        public int targetValue = 1;
        public bool satisfied;
        public double activatedWorldTime = -1d;
        public double satisfiedWorldTime = -1d;
        public string[] prerequisiteObjectiveDefinitionIds = Array.Empty<string>();
        public string[] countedSourceEventIds = Array.Empty<string>();
        public string[] countedTargetIds = Array.Empty<string>();
        public QuestObjectiveProgressEvidenceData[] evidence = Array.Empty<QuestObjectiveProgressEvidenceData>();
        public long revision = 1L;

        public QuestObjectiveRecordData Clone()
        {
            return new QuestObjectiveRecordData
            {
                objectiveId = N(objectiveId),
                objectiveDefinitionId = N(objectiveDefinitionId),
                groupDefinitionId = N(groupDefinitionId),
                questId = N(questId),
                questDefinitionId = N(questDefinitionId),
                assignmentId = N(assignmentId),
                assigneePersonId = N(assigneePersonId),
                worldId = N(worldId),
                ownershipScope = ownershipScope,
                lifecycleState = lifecycleState,
                classification = classification,
                visibility = visibility,
                category = category,
                progressModel = progressModel,
                progressSource = progressSource,
                satisfactionPolicy = satisfactionPolicy,
                repetitionPolicy = repetitionPolicy,
                currentValue = currentValue,
                targetValue = Math.Max(1, targetValue),
                satisfied = satisfied,
                activatedWorldTime = activatedWorldTime,
                satisfiedWorldTime = satisfiedWorldTime,
                prerequisiteObjectiveDefinitionIds = Clean(prerequisiteObjectiveDefinitionIds),
                countedSourceEventIds = Clean(countedSourceEventIds),
                countedTargetIds = Clean(countedTargetIds),
                evidence = (evidence ?? Array.Empty<QuestObjectiveProgressEvidenceData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.worldTime).ThenBy(value => value.evidenceId, StringComparer.Ordinal).ToArray(),
                revision = revision
            };
        }

        public bool IsTerminal => lifecycleState == QuestObjectiveLifecycleState.Abandoned || lifecycleState == QuestObjectiveLifecycleState.Withdrawn || lifecycleState == QuestObjectiveLifecycleState.Historical || lifecycleState == QuestObjectiveLifecycleState.Invalid;
        public bool IsVisibleToPublic => visibility != QuestObjectiveVisibility.Hidden && visibility != QuestObjectiveVisibility.Secret && visibility != QuestObjectiveVisibility.Diagnostic;

        private static string N(string value) => QuestObjectiveProgressModelUtility.N(value);
        private static string[] Clean(IEnumerable<string> values) => QuestObjectiveProgressModelUtility.Clean(values);
    }

    [Serializable]
    public sealed class QuestObjectiveRuntimeEventData
    {
        public string eventId;
        public string transactionId;
        public string objectiveId;
        public string objectiveDefinitionId;
        public string questId;
        public string assignmentId;
        public QuestObjectiveEventKind eventKind;
        public int beforeValue;
        public int afterValue;
        public QuestObjectiveLifecycleState beforeState;
        public QuestObjectiveLifecycleState afterState;
        public string sourceEventId;
        public double worldTime;
        public long runtimeRevision;

        public QuestObjectiveRuntimeEventData Clone()
        {
            return new QuestObjectiveRuntimeEventData
            {
                eventId = eventId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                objectiveId = objectiveId ?? string.Empty,
                objectiveDefinitionId = objectiveDefinitionId ?? string.Empty,
                questId = questId ?? string.Empty,
                assignmentId = assignmentId ?? string.Empty,
                eventKind = eventKind,
                beforeValue = beforeValue,
                afterValue = afterValue,
                beforeState = beforeState,
                afterState = afterState,
                sourceEventId = sourceEventId ?? string.Empty,
                worldTime = worldTime,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class QuestObjectiveTransactionData
    {
        public string transactionId;
        public string operation;
        public string objectiveId;
        public string assignmentId;
        public long runtimeRevision;

        public QuestObjectiveTransactionData Clone()
        {
            return new QuestObjectiveTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                objectiveId = objectiveId ?? string.Empty,
                assignmentId = assignmentId ?? string.Empty,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class QuestObjectiveProgressRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<QuestObjectiveRecordData> objectives = new List<QuestObjectiveRecordData>();
        public List<QuestObjectiveRuntimeEventData> events = new List<QuestObjectiveRuntimeEventData>();
        public List<QuestObjectiveTransactionData> transactions = new List<QuestObjectiveTransactionData>();

        public QuestObjectiveProgressRuntimeSaveData Clone()
        {
            return new QuestObjectiveProgressRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                objectives = (objectives ?? new List<QuestObjectiveRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                events = (events ?? new List<QuestObjectiveRuntimeEventData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                transactions = (transactions ?? new List<QuestObjectiveTransactionData>()).Where(value => value != null).Select(value => value.Clone()).ToList()
            };
        }
    }

    [Serializable]
    public sealed class QuestObjectiveStateFactData
    {
        public QuestObjectiveCategory category;
        public InformationSubjectReferenceData target = new InformationSubjectReferenceData();
        public InformationSubjectReferenceData secondaryTarget = new InformationSubjectReferenceData();
        public int value = 1;
        public string sourceRuntimeId;
        public long sourceRevision;

        public QuestObjectiveStateFactData Clone()
        {
            return new QuestObjectiveStateFactData
            {
                category = category,
                target = target?.Clone() ?? new InformationSubjectReferenceData(),
                secondaryTarget = secondaryTarget?.Clone() ?? new InformationSubjectReferenceData(),
                value = value,
                sourceRuntimeId = N(sourceRuntimeId),
                sourceRevision = sourceRevision
            };
        }

        private static string N(string value) => QuestObjectiveProgressModelUtility.N(value);
    }

    public sealed class QuestObjectiveStateFactSet
    {
        private readonly Dictionary<string, int> values;

        public QuestObjectiveStateFactSet(IEnumerable<QuestObjectiveStateFactData> facts = null)
        {
            values = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (QuestObjectiveStateFactData fact in facts ?? Array.Empty<QuestObjectiveStateFactData>())
            {
                if (fact == null)
                {
                    continue;
                }

                string key = BuildKey(fact.category, fact.target?.subjectId, fact.secondaryTarget?.subjectId);
                values[key] = Math.Max(values.TryGetValue(key, out int existing) ? existing : 0, fact.value);
            }
        }

        public static QuestObjectiveStateFactSet Empty { get; } = new QuestObjectiveStateFactSet();

        public bool Contains(QuestObjectiveCategory category, string targetId, string secondaryId = "")
        {
            return values.ContainsKey(BuildKey(category, targetId, secondaryId));
        }

        public int Value(QuestObjectiveCategory category, string targetId, string secondaryId = "")
        {
            return values.TryGetValue(BuildKey(category, targetId, secondaryId), out int value) ? value : 0;
        }

        public static string BuildKey(QuestObjectiveCategory category, string targetId, string secondaryId = "")
        {
            return $"{category}:{QuestObjectiveProgressModelUtility.N(targetId)}:{QuestObjectiveProgressModelUtility.N(secondaryId)}";
        }
    }

    public sealed class QuestObjectiveStateContext
    {
        public string assignmentId;
        public string personId;
        public double worldTime;
        public QuestObjectiveStateFactSet facts = QuestObjectiveStateFactSet.Empty;
        public long sourceRevision;

        public QuestObjectiveStateContext Clone()
        {
            return new QuestObjectiveStateContext
            {
                assignmentId = N(assignmentId),
                personId = N(personId),
                worldTime = worldTime,
                facts = facts ?? QuestObjectiveStateFactSet.Empty,
                sourceRevision = sourceRevision
            };
        }

        private static string N(string value) => QuestObjectiveProgressModelUtility.N(value);
    }

    public sealed class QuestObjectiveSignal
    {
        public string transactionId;
        public string sourceEventId;
        public string sourceRuntimeId;
        public string questId;
        public string assignmentId;
        public string actorPersonId;
        public string participantPersonId;
        public QuestObjectiveCategory category;
        public InformationSubjectReferenceData target = new InformationSubjectReferenceData();
        public InformationSubjectReferenceData secondaryTarget = new InformationSubjectReferenceData();
        public int amount = 1;
        public double worldTime;
        public bool committed = true;
        public bool preview;

        public QuestObjectiveSignal Clone()
        {
            return new QuestObjectiveSignal
            {
                transactionId = N(transactionId),
                sourceEventId = N(sourceEventId),
                sourceRuntimeId = N(sourceRuntimeId),
                questId = N(questId),
                assignmentId = N(assignmentId),
                actorPersonId = N(actorPersonId),
                participantPersonId = N(participantPersonId),
                category = category,
                target = target?.Clone() ?? new InformationSubjectReferenceData(),
                secondaryTarget = secondaryTarget?.Clone() ?? new InformationSubjectReferenceData(),
                amount = Math.Max(1, amount),
                worldTime = worldTime,
                committed = committed,
                preview = preview
            };
        }

        private static string N(string value) => QuestObjectiveProgressModelUtility.N(value);
    }

    public sealed class QuestObjectiveSnapshot
    {
        private readonly QuestObjectiveRecordData data;

        public QuestObjectiveSnapshot(QuestObjectiveRecordData record)
        {
            data = record?.Clone() ?? new QuestObjectiveRecordData();
        }

        public string ObjectiveId => data.objectiveId ?? string.Empty;
        public string ObjectiveDefinitionId => data.objectiveDefinitionId ?? string.Empty;
        public string GroupDefinitionId => data.groupDefinitionId ?? string.Empty;
        public string QuestId => data.questId ?? string.Empty;
        public string QuestDefinitionId => data.questDefinitionId ?? string.Empty;
        public string AssignmentId => data.assignmentId ?? string.Empty;
        public string AssigneePersonId => data.assigneePersonId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public QuestObjectiveOwnershipScope OwnershipScope => data.ownershipScope;
        public QuestObjectiveLifecycleState LifecycleState => data.lifecycleState;
        public QuestObjectiveRequirementClassification Classification => data.classification;
        public QuestObjectiveVisibility Visibility => data.visibility;
        public QuestObjectiveCategory Category => data.category;
        public QuestObjectiveProgressModel ProgressModel => data.progressModel;
        public QuestObjectiveProgressSource ProgressSource => data.progressSource;
        public int CurrentValue => data.currentValue;
        public int TargetValue => data.targetValue;
        public bool Satisfied => data.satisfied;
        public double ActivatedWorldTime => data.activatedWorldTime;
        public double SatisfiedWorldTime => data.satisfiedWorldTime;
        public IReadOnlyList<string> CountedSourceEventIds => QuestObjectiveProgressModelUtility.Clean(data.countedSourceEventIds);
        public IReadOnlyList<string> CountedTargetIds => QuestObjectiveProgressModelUtility.Clean(data.countedTargetIds);
        public IReadOnlyList<QuestObjectiveProgressEvidenceData> Evidence => (data.evidence ?? Array.Empty<QuestObjectiveProgressEvidenceData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        public long Revision => data.revision;
        public QuestObjectiveRecordData ToSaveData() => data.Clone();
    }

    public sealed class QuestObjectiveQuery
    {
        public QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly;
        public string requesterPersonId;
        public string questId;
        public string assignmentId;
        public string objectiveId;
        public string objectiveDefinitionId;
        public string assigneePersonId;
        public QuestObjectiveCategory? category;
        public QuestObjectiveLifecycleState? lifecycleState;
        public bool includeTerminal;
        public string worldId;
    }

    public sealed class QuestAssignmentObjectiveSummary
    {
        public QuestAssignmentObjectiveSummary(string assignmentId, int visibleObjectives, int requiredSatisfied, int requiredRemaining, int optionalSatisfied, bool hiddenCountsRedacted, bool completionCandidate)
        {
            AssignmentId = assignmentId ?? string.Empty;
            VisibleObjectives = visibleObjectives;
            RequiredSatisfied = requiredSatisfied;
            RequiredRemaining = requiredRemaining;
            OptionalSatisfied = optionalSatisfied;
            HiddenCountsRedacted = hiddenCountsRedacted;
            CompletionCandidate = completionCandidate;
        }

        public string AssignmentId { get; }
        public int VisibleObjectives { get; }
        public int RequiredSatisfied { get; }
        public int RequiredRemaining { get; }
        public int OptionalSatisfied { get; }
        public bool HiddenCountsRedacted { get; }
        public bool CompletionCandidate { get; }
    }

    public sealed class QuestObjectiveOperationResult
    {
        private QuestObjectiveOperationResult(QuestObjectiveOperationStatus status, string message, IReadOnlyList<QuestObjectiveSnapshot> objectives, bool preview, bool duplicate, long before, long after)
        {
            Status = status;
            Message = message ?? string.Empty;
            Objectives = objectives ?? Array.Empty<QuestObjectiveSnapshot>();
            Objective = Objectives.FirstOrDefault();
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public QuestObjectiveOperationStatus Status { get; }
        public string Message { get; }
        public QuestObjectiveSnapshot Objective { get; }
        public IReadOnlyList<QuestObjectiveSnapshot> Objectives { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Succeeded => Status == QuestObjectiveOperationStatus.Succeeded || Status == QuestObjectiveOperationStatus.Preview || Status == QuestObjectiveOperationStatus.Duplicate;

        public static QuestObjectiveOperationResult Success(string message, long before, long after, IEnumerable<QuestObjectiveRecordData> objectives = null, bool preview = false, bool duplicate = false)
        {
            return new QuestObjectiveOperationResult(preview ? QuestObjectiveOperationStatus.Preview : duplicate ? QuestObjectiveOperationStatus.Duplicate : QuestObjectiveOperationStatus.Succeeded, message, (objectives ?? Array.Empty<QuestObjectiveRecordData>()).Select(value => new QuestObjectiveSnapshot(value)).ToArray(), preview, duplicate, before, after);
        }

        public static QuestObjectiveOperationResult Failure(QuestObjectiveOperationStatus status, string message, long revision)
        {
            return new QuestObjectiveOperationResult(status, message, Array.Empty<QuestObjectiveSnapshot>(), false, false, revision, revision);
        }
    }

    public sealed class QuestObjectiveValidationReport
    {
        public QuestObjectiveValidationReport(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Errors.Count == 0;
        public string Summary => $"Quest objective validation finished with {Errors.Count} error(s), {Warnings.Count} warning(s).";
    }

    public static class QuestObjectiveProgressModelUtility
    {
        public static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        public static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public static string TargetId(InformationSubjectReferenceData target)
        {
            return N(target?.subjectId);
        }
    }
}
