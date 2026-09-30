using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Narrative
{
    [Serializable]
    public sealed class NarrativeArcDependencyDefinitionData
    {
        public string dependencyDefinitionId;
        public NarrativeArcDependencyKind kind = NarrativeArcDependencyKind.StageResolved;
        public string requiredId;
        public string secondaryId;
        public string requiredValue;
        public string[] stageDefinitionIds = Array.Empty<string>();
        public int minimumCount = 1;
        public bool hidden;
        public bool optional;

        public NarrativeArcDependencyDefinitionData Clone()
        {
            return new NarrativeArcDependencyDefinitionData
            {
                dependencyDefinitionId = N(dependencyDefinitionId),
                kind = kind == NarrativeArcDependencyKind.Unknown ? NarrativeArcDependencyKind.StageResolved : kind,
                requiredId = N(requiredId),
                secondaryId = N(secondaryId),
                requiredValue = N(requiredValue),
                stageDefinitionIds = NarrativeModelUtility.Clean(stageDefinitionIds),
                minimumCount = Math.Max(0, minimumCount),
                hidden = hidden,
                optional = optional
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeArcQuestBindingDefinitionData
    {
        public string bindingDefinitionId;
        public NarrativeArcQuestBindingMode mode = NarrativeArcQuestBindingMode.ReferenceExistingQuest;
        public string questDefinitionId;
        public string questId;
        public string questSourceId;
        public bool required = true;
        public bool hidden;

        public NarrativeArcQuestBindingDefinitionData Clone()
        {
            return new NarrativeArcQuestBindingDefinitionData
            {
                bindingDefinitionId = N(bindingDefinitionId),
                mode = mode == NarrativeArcQuestBindingMode.Unknown ? NarrativeArcQuestBindingMode.ReferenceExistingQuest : mode,
                questDefinitionId = N(questDefinitionId),
                questId = N(questId),
                questSourceId = N(questSourceId),
                required = required,
                hidden = hidden
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeArcStageDefinitionData
    {
        public string stageDefinitionId;
        public string displayName;
        public int order;
        public bool initial;
        public bool terminalOnCompletion;
        public bool hidden;
        public NarrativeArcDependencyDefinitionData[] entryDependencies = Array.Empty<NarrativeArcDependencyDefinitionData>();
        public NarrativeArcDependencyDefinitionData[] completionDependencies = Array.Empty<NarrativeArcDependencyDefinitionData>();
        public NarrativeArcDependencyDefinitionData[] skipDependencies = Array.Empty<NarrativeArcDependencyDefinitionData>();
        public NarrativeArcDependencyDefinitionData[] failureDependencies = Array.Empty<NarrativeArcDependencyDefinitionData>();
        public NarrativeActionDefinitionData[] entryActions = Array.Empty<NarrativeActionDefinitionData>();
        public NarrativeActionDefinitionData[] completionActions = Array.Empty<NarrativeActionDefinitionData>();
        public NarrativeArcQuestBindingDefinitionData[] questBindings = Array.Empty<NarrativeArcQuestBindingDefinitionData>();
        public string[] tagIds = Array.Empty<string>();

        public NarrativeArcStageDefinitionData Clone()
        {
            return new NarrativeArcStageDefinitionData
            {
                stageDefinitionId = N(stageDefinitionId),
                displayName = displayName ?? string.Empty,
                order = order,
                initial = initial,
                terminalOnCompletion = terminalOnCompletion,
                hidden = hidden,
                entryDependencies = (entryDependencies ?? Array.Empty<NarrativeArcDependencyDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.dependencyDefinitionId, StringComparer.Ordinal).ToArray(),
                completionDependencies = (completionDependencies ?? Array.Empty<NarrativeArcDependencyDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.dependencyDefinitionId, StringComparer.Ordinal).ToArray(),
                skipDependencies = (skipDependencies ?? Array.Empty<NarrativeArcDependencyDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.dependencyDefinitionId, StringComparer.Ordinal).ToArray(),
                failureDependencies = (failureDependencies ?? Array.Empty<NarrativeArcDependencyDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.dependencyDefinitionId, StringComparer.Ordinal).ToArray(),
                entryActions = (entryActions ?? Array.Empty<NarrativeActionDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.order).ThenBy(value => value.actionDefinitionId, StringComparer.Ordinal).ToArray(),
                completionActions = (completionActions ?? Array.Empty<NarrativeActionDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.order).ThenBy(value => value.actionDefinitionId, StringComparer.Ordinal).ToArray(),
                questBindings = (questBindings ?? Array.Empty<NarrativeArcQuestBindingDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.bindingDefinitionId, StringComparer.Ordinal).ToArray(),
                tagIds = NarrativeModelUtility.Clean(tagIds)
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeArcDefinitionData
    {
        public string arcDefinitionId;
        public string displayName;
        public NarrativeArcScope scope = NarrativeArcScope.Person;
        public NarrativeEventVisibility visibility = NarrativeEventVisibility.ParticipantKnown;
        public bool repeatable;
        public int cascadeDepthLimit = 16;
        public string[] tagIds = Array.Empty<string>();
        public NarrativeArcStageDefinitionData[] stages = Array.Empty<NarrativeArcStageDefinitionData>();

        public NarrativeArcDefinitionData Clone()
        {
            return new NarrativeArcDefinitionData
            {
                arcDefinitionId = N(arcDefinitionId),
                displayName = displayName ?? string.Empty,
                scope = scope == NarrativeArcScope.Unknown ? NarrativeArcScope.Person : scope,
                visibility = visibility == NarrativeEventVisibility.Unknown ? NarrativeEventVisibility.ParticipantKnown : visibility,
                repeatable = repeatable,
                cascadeDepthLimit = Math.Max(1, cascadeDepthLimit),
                tagIds = NarrativeModelUtility.Clean(tagIds),
                stages = (stages ?? Array.Empty<NarrativeArcStageDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.order).ThenBy(value => value.stageDefinitionId, StringComparer.Ordinal).ToArray()
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeArcBoundQuestRecordData
    {
        public string bindingDefinitionId;
        public string questId;
        public string questDefinitionId;
        public NarrativeArcQuestBindingMode mode;
        public double worldTime;

        public NarrativeArcBoundQuestRecordData Clone()
        {
            return new NarrativeArcBoundQuestRecordData
            {
                bindingDefinitionId = bindingDefinitionId ?? string.Empty,
                questId = questId ?? string.Empty,
                questDefinitionId = questDefinitionId ?? string.Empty,
                mode = mode,
                worldTime = worldTime
            };
        }
    }

    [Serializable]
    public sealed class NarrativeArcStageRecordData
    {
        public string stageRuntimeId;
        public string stageDefinitionId;
        public NarrativeArcStageLifecycle lifecycle = NarrativeArcStageLifecycle.Locked;
        public double activatedWorldTime = -1d;
        public double resolvedWorldTime = -1d;
        public string resolvedBySignalId;
        public NarrativeArcBoundQuestRecordData[] boundQuests = Array.Empty<NarrativeArcBoundQuestRecordData>();
        public NarrativeActionExecutionRecordData[] actionExecutions = Array.Empty<NarrativeActionExecutionRecordData>();
        public long revision = 1L;

        public NarrativeArcStageRecordData Clone()
        {
            return new NarrativeArcStageRecordData
            {
                stageRuntimeId = stageRuntimeId ?? string.Empty,
                stageDefinitionId = stageDefinitionId ?? string.Empty,
                lifecycle = lifecycle,
                activatedWorldTime = activatedWorldTime,
                resolvedWorldTime = resolvedWorldTime,
                resolvedBySignalId = resolvedBySignalId ?? string.Empty,
                boundQuests = (boundQuests ?? Array.Empty<NarrativeArcBoundQuestRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.bindingDefinitionId, StringComparer.Ordinal).ToArray(),
                actionExecutions = (actionExecutions ?? Array.Empty<NarrativeActionExecutionRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.order).ThenBy(value => value.actionDefinitionId, StringComparer.Ordinal).ToArray(),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class NarrativeArcRecordData
    {
        public string narrativeArcId;
        public string arcDefinitionId;
        public string worldId;
        public NarrativeArcScope scope = NarrativeArcScope.Person;
        public NarrativeArcLifecycle lifecycle = NarrativeArcLifecycle.Active;
        public string scopeKey;
        public string actorPersonId;
        public string subjectId;
        public double startedWorldTime;
        public double resolvedWorldTime = -1d;
        public string provenanceId;
        public NarrativeArcStageRecordData[] stages = Array.Empty<NarrativeArcStageRecordData>();
        public string[] processedSignalKeys = Array.Empty<string>();
        public long revision = 1L;

        public NarrativeArcRecordData Clone()
        {
            return new NarrativeArcRecordData
            {
                narrativeArcId = narrativeArcId ?? string.Empty,
                arcDefinitionId = arcDefinitionId ?? string.Empty,
                worldId = worldId ?? string.Empty,
                scope = scope,
                lifecycle = lifecycle,
                scopeKey = scopeKey ?? string.Empty,
                actorPersonId = actorPersonId ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                startedWorldTime = startedWorldTime,
                resolvedWorldTime = resolvedWorldTime,
                provenanceId = provenanceId ?? string.Empty,
                stages = (stages ?? Array.Empty<NarrativeArcStageRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.stageRuntimeId, StringComparer.Ordinal).ToArray(),
                processedSignalKeys = NarrativeModelUtility.Clean(processedSignalKeys),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class NarrativeArcRuntimeTransactionData
    {
        public string transactionId;
        public string operation;
        public string narrativeArcId;
        public string stageDefinitionId;
        public NarrativeArcOperationStatus status;
        public long runtimeRevision;

        public NarrativeArcRuntimeTransactionData Clone()
        {
            return new NarrativeArcRuntimeTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                narrativeArcId = narrativeArcId ?? string.Empty,
                stageDefinitionId = stageDefinitionId ?? string.Empty,
                status = status,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class NarrativeArcRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<NarrativeArcRecordData> arcs = new List<NarrativeArcRecordData>();
        public List<NarrativeArcRuntimeTransactionData> transactions = new List<NarrativeArcRuntimeTransactionData>();

        public NarrativeArcRuntimeSaveData Clone()
        {
            return new NarrativeArcRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                arcs = (arcs ?? new List<NarrativeArcRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                transactions = (transactions ?? new List<NarrativeArcRuntimeTransactionData>()).Where(value => value != null).Select(value => value.Clone()).ToList()
            };
        }
    }

    public sealed class NarrativeArcStartRequest
    {
        public string transactionId;
        public string narrativeArcId;
        public string arcDefinitionId;
        public string scopeKey;
        public string actorPersonId;
        public string subjectId;
        public string provenanceId;
        public NarrativeConditionContextData conditionContext;
        public double worldTime;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class NarrativeArcSignalRequest
    {
        public string transactionId;
        public string narrativeArcId;
        public string arcDefinitionId;
        public string stageDefinitionId;
        public NarrativeArcSignalCategory category = NarrativeArcSignalCategory.Explicit;
        public string signalId;
        public string sourceId;
        public string secondaryId;
        public string value;
        public string questId;
        public string questDefinitionId;
        public QuestTerminalOutcomeKind questOutcomeKind = QuestTerminalOutcomeKind.Unknown;
        public string actorPersonId;
        public string subjectId;
        public string scopeKey;
        public NarrativeConditionContextData conditionContext;
        public double worldTime;
        public int cascadeDepth;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class NarrativeArcQuery
    {
        public string narrativeArcId;
        public string arcDefinitionId;
        public string scopeKey;
        public string actorPersonId;
        public string stageDefinitionId;
        public NarrativeArcLifecycle? lifecycle;
        public bool developmentView = true;
        public bool hideConcealedCounts;
    }

    public sealed class NarrativeArcStageSnapshot
    {
        private readonly NarrativeArcStageRecordData data;
        private readonly NarrativeArcStageDefinitionData definition;
        private readonly bool redacted;

        public NarrativeArcStageSnapshot(NarrativeArcStageRecordData record, NarrativeArcStageDefinitionData stageDefinition = null, bool redact = false)
        {
            data = record?.Clone() ?? new NarrativeArcStageRecordData();
            definition = stageDefinition?.Clone();
            redacted = redact;
        }

        public string StageRuntimeId => redacted ? string.Empty : data.stageRuntimeId;
        public string StageDefinitionId => redacted ? string.Empty : data.stageDefinitionId;
        public string DisplayName => redacted ? string.Empty : definition?.displayName ?? data.stageDefinitionId;
        public NarrativeArcStageLifecycle Lifecycle => redacted ? NarrativeArcStageLifecycle.Locked : data.lifecycle;
        public double ActivatedWorldTime => redacted ? -1d : data.activatedWorldTime;
        public double ResolvedWorldTime => redacted ? -1d : data.resolvedWorldTime;
        public IReadOnlyList<NarrativeArcBoundQuestRecordData> BoundQuests => redacted ? Array.Empty<NarrativeArcBoundQuestRecordData>() : data.boundQuests.Select(value => value.Clone()).ToArray();
        public IReadOnlyList<NarrativeActionExecutionRecordData> ActionExecutions => redacted ? Array.Empty<NarrativeActionExecutionRecordData>() : data.actionExecutions.Select(value => value.Clone()).ToArray();
        public NarrativeArcStageRecordData ToSaveData() => data.Clone();
    }

    public sealed class NarrativeArcSnapshot
    {
        private readonly NarrativeArcRecordData data;
        private readonly NarrativeArcDefinitionData definition;
        private readonly bool development;

        public NarrativeArcSnapshot(NarrativeArcRecordData record, NarrativeArcDefinitionData arcDefinition = null, bool developmentView = true)
        {
            data = record?.Clone() ?? new NarrativeArcRecordData();
            definition = arcDefinition?.Clone();
            development = developmentView;
        }

        public string NarrativeArcId => data.narrativeArcId;
        public string ArcDefinitionId => data.arcDefinitionId;
        public NarrativeArcLifecycle Lifecycle => data.lifecycle;
        public NarrativeArcScope Scope => data.scope;
        public string ScopeKey => IsHidden && !development ? string.Empty : data.scopeKey;
        public string ActorPersonId => IsHidden && !development ? string.Empty : data.actorPersonId;
        public string SubjectId => IsHidden && !development ? string.Empty : data.subjectId;
        public double StartedWorldTime => IsHidden && !development ? -1d : data.startedWorldTime;
        public double ResolvedWorldTime => IsHidden && !development ? -1d : data.resolvedWorldTime;
        public long Revision => data.revision;
        public bool DevelopmentView => development;
        public bool IsHidden => definition?.visibility == NarrativeEventVisibility.Hidden || definition?.visibility == NarrativeEventVisibility.Secret || definition?.visibility == NarrativeEventVisibility.Diagnostic;
        public IReadOnlyList<NarrativeArcStageSnapshot> Stages => (definition?.stages ?? Array.Empty<NarrativeArcStageDefinitionData>())
            .OrderBy(value => value.order)
            .ThenBy(value => value.stageDefinitionId, StringComparer.Ordinal)
            .Select(stage =>
            {
                NarrativeArcStageRecordData record = (data.stages ?? Array.Empty<NarrativeArcStageRecordData>()).FirstOrDefault(value => string.Equals(value.stageDefinitionId, stage.stageDefinitionId, StringComparison.Ordinal));
                bool redact = !development && (IsHidden || stage.hidden);
                return new NarrativeArcStageSnapshot(record, stage, redact);
            })
            .Where(value => development || !string.IsNullOrWhiteSpace(value.StageDefinitionId))
            .ToArray();
        public NarrativeArcRecordData ToSaveData() => data.Clone();
    }

    public sealed class NarrativeArcQuestBindingRequest
    {
        public NarrativeArcDefinitionData ArcDefinition { get; set; }
        public NarrativeArcStageDefinitionData StageDefinition { get; set; }
        public NarrativeArcRecordData ArcRecord { get; set; }
        public NarrativeArcQuestBindingDefinitionData BindingDefinition { get; set; }
        public string TransactionId { get; set; }
        public bool Preview { get; set; }
        public double WorldTime { get; set; }
    }

    public sealed class NarrativeArcQuestBindingResult
    {
        public NarrativeArcQuestBindingResult(bool succeeded, string questId, string message)
        {
            Succeeded = succeeded;
            QuestId = questId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string QuestId { get; }
        public string Message { get; }
    }

    public sealed class NarrativeArcActionContext
    {
        public NarrativeArcDefinitionData ArcDefinition { get; set; }
        public NarrativeArcStageDefinitionData StageDefinition { get; set; }
        public NarrativeArcRecordData ArcRecord { get; set; }
        public NarrativeConditionContextData ConditionContext { get; set; }
        public string TransactionId { get; set; }
        public bool Preview { get; set; }
        public double WorldTime { get; set; }
    }

    public sealed class NarrativeArcRuntimeIntegrations
    {
        public QuestRuntime QuestRuntime { get; set; }
        public QuestSourceRuntime QuestSourceRuntime { get; set; }
        public QuestOutcomeRuntime QuestOutcomeRuntime { get; set; }
        public NarrativeEventRuntime NarrativeEventRuntime { get; set; }
        public NarrativeStateRuntime NarrativeStateRuntime { get; set; }
        public Func<NarrativeArcQuestBindingRequest, NarrativeArcQuestBindingResult> QuestBindingExecutor { get; set; }
        public Func<NarrativeActionDefinitionData, NarrativeArcActionContext, bool> ActionExecutor { get; set; }
    }

    public sealed class NarrativeArcOperationResult
    {
        public NarrativeArcOperationResult(NarrativeArcOperationStatus status, string message, long before, long after, NarrativeArcSnapshot snapshot = null, bool preview = false, bool duplicate = false)
        {
            Status = status;
            Message = message ?? string.Empty;
            RevisionBefore = before;
            RevisionAfter = after;
            Snapshot = snapshot;
            Preview = preview;
            Duplicate = duplicate;
        }

        public NarrativeArcOperationStatus Status { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public NarrativeArcSnapshot Snapshot { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public bool Succeeded => Status == NarrativeArcOperationStatus.Succeeded || Status == NarrativeArcOperationStatus.Preview || Status == NarrativeArcOperationStatus.Duplicate;

        public static NarrativeArcOperationResult Success(string message, long before, long after, NarrativeArcSnapshot snapshot = null, bool preview = false, bool duplicate = false)
        {
            return new NarrativeArcOperationResult(preview ? NarrativeArcOperationStatus.Preview : duplicate ? NarrativeArcOperationStatus.Duplicate : NarrativeArcOperationStatus.Succeeded, message, before, after, snapshot, preview, duplicate);
        }

        public static NarrativeArcOperationResult Failure(NarrativeArcOperationStatus status, string message, long revision, NarrativeArcSnapshot snapshot = null)
        {
            return new NarrativeArcOperationResult(status, message, revision, revision, snapshot);
        }
    }
}
