using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Narrative
{
    [Serializable]
    public sealed class NarrativeVariableValueData
    {
        public NarrativeVariableKind kind = NarrativeVariableKind.Boolean;
        public bool boolValue;
        public int intValue;
        public string tokenValue;
        public InformationSubjectReferenceData subjectReference = new InformationSubjectReferenceData();

        public NarrativeVariableValueData Clone()
        {
            return new NarrativeVariableValueData
            {
                kind = kind == NarrativeVariableKind.Unknown ? NarrativeVariableKind.Boolean : kind,
                boolValue = boolValue,
                intValue = intValue,
                tokenValue = N(tokenValue),
                subjectReference = subjectReference?.Clone() ?? new InformationSubjectReferenceData()
            };
        }

        public static NarrativeVariableValueData Bool(bool value) => new NarrativeVariableValueData { kind = NarrativeVariableKind.Boolean, boolValue = value };
        public static NarrativeVariableValueData Integer(int value) => new NarrativeVariableValueData { kind = NarrativeVariableKind.Integer, intValue = value };
        public static NarrativeVariableValueData Counter(int value) => new NarrativeVariableValueData { kind = NarrativeVariableKind.SmallCounter, intValue = value };
        public static NarrativeVariableValueData Token(string value) => new NarrativeVariableValueData { kind = NarrativeVariableKind.StateToken, tokenValue = N(value) };
        public static NarrativeVariableValueData Subject(InformationSubjectType type, string id) => new NarrativeVariableValueData { kind = NarrativeVariableKind.StableSubjectReference, subjectReference = new InformationSubjectReferenceData { subjectType = type, subjectId = N(id) } };
        public static NarrativeVariableValueData OptionalSubject(InformationSubjectType type, string id) => new NarrativeVariableValueData { kind = NarrativeVariableKind.OptionalStableSubjectReference, subjectReference = new InformationSubjectReferenceData { subjectType = type, subjectId = N(id) } };

        public string StableText => kind switch
        {
            NarrativeVariableKind.Boolean => boolValue ? "true" : "false",
            NarrativeVariableKind.Integer => intValue.ToString(),
            NarrativeVariableKind.SmallCounter => intValue.ToString(),
            NarrativeVariableKind.StateToken => N(tokenValue),
            NarrativeVariableKind.StableSubjectReference => $"{subjectReference?.subjectType ?? InformationSubjectType.Custom}:{N(subjectReference?.subjectId)}",
            NarrativeVariableKind.OptionalStableSubjectReference => string.IsNullOrWhiteSpace(subjectReference?.subjectId) ? string.Empty : $"{subjectReference.subjectType}:{N(subjectReference.subjectId)}",
            _ => string.Empty
        };

        public bool SameValue(NarrativeVariableValueData other)
        {
            other = other?.Clone();
            if (other == null || kind != other.kind) return false;
            return string.Equals(StableText, other.StableText, StringComparison.Ordinal);
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeStateValueDefinitionData
    {
        public string valueDefinitionId;
        public string displayName;
        public bool terminal;
        public string branchDefinitionId;
        public string mergeGroupId;
        public string[] tagIds = Array.Empty<string>();

        public NarrativeStateValueDefinitionData Clone()
        {
            return new NarrativeStateValueDefinitionData
            {
                valueDefinitionId = N(valueDefinitionId),
                displayName = displayName ?? string.Empty,
                terminal = terminal,
                branchDefinitionId = N(branchDefinitionId),
                mergeGroupId = N(mergeGroupId),
                tagIds = NarrativeModelUtility.Clean(tagIds)
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeVariableDefinitionData
    {
        public string variableDefinitionId;
        public string displayName;
        public NarrativeVariableKind kind = NarrativeVariableKind.Boolean;
        public NarrativeStateScope scope = NarrativeStateScope.World;
        public NarrativeStateVisibility visibility = NarrativeStateVisibility.Public;
        public NarrativeVariableMutabilityPolicy mutabilityPolicy = NarrativeVariableMutabilityPolicy.TransitionOnly;
        public NarrativeVariableValueData defaultValue = NarrativeVariableValueData.Bool(false);
        public NarrativeStateValueDefinitionData[] allowedValues = Array.Empty<NarrativeStateValueDefinitionData>();
        public int minimumValue = int.MinValue;
        public int maximumValue = int.MaxValue;
        public string exclusionGroupId;
        public bool recordInitialDefaultHistory;
        public string[] tagIds = Array.Empty<string>();

        public NarrativeVariableDefinitionData Clone()
        {
            return new NarrativeVariableDefinitionData
            {
                variableDefinitionId = N(variableDefinitionId),
                displayName = displayName ?? string.Empty,
                kind = kind == NarrativeVariableKind.Unknown ? NarrativeVariableKind.Boolean : kind,
                scope = scope == NarrativeStateScope.Unknown ? NarrativeStateScope.World : scope,
                visibility = visibility == NarrativeStateVisibility.Unknown ? NarrativeStateVisibility.Public : visibility,
                mutabilityPolicy = mutabilityPolicy == NarrativeVariableMutabilityPolicy.Unknown ? NarrativeVariableMutabilityPolicy.TransitionOnly : mutabilityPolicy,
                defaultValue = defaultValue?.Clone() ?? NarrativeVariableValueData.Bool(false),
                allowedValues = (allowedValues ?? Array.Empty<NarrativeStateValueDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                minimumValue = minimumValue,
                maximumValue = maximumValue,
                exclusionGroupId = N(exclusionGroupId),
                recordInitialDefaultHistory = recordInitialDefaultHistory,
                tagIds = NarrativeModelUtility.Clean(tagIds)
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeStateTransitionDefinitionData
    {
        public string transitionDefinitionId;
        public string displayName;
        public string variableDefinitionId;
        public NarrativeVariableValueData[] allowedSourceValues = Array.Empty<NarrativeVariableValueData>();
        public NarrativeVariableValueData targetValue = NarrativeVariableValueData.Bool(true);
        public NarrativeConditionDefinitionData[] conditions = Array.Empty<NarrativeConditionDefinitionData>();
        public NarrativeActionDefinitionData[] consequences = Array.Empty<NarrativeActionDefinitionData>();
        public NarrativeTransitionRepeatPolicy repeatPolicy = NarrativeTransitionRepeatPolicy.IdempotentSameTarget;
        public NarrativeTransitionReentryPolicy reentryPolicy = NarrativeTransitionReentryPolicy.RejectAfterTerminal;
        public NarrativeStateVisibility visibility = NarrativeStateVisibility.Public;
        public int order;
        public bool automatic;
        public bool hidden;

        public NarrativeStateTransitionDefinitionData Clone()
        {
            return new NarrativeStateTransitionDefinitionData
            {
                transitionDefinitionId = N(transitionDefinitionId),
                displayName = displayName ?? string.Empty,
                variableDefinitionId = N(variableDefinitionId),
                allowedSourceValues = (allowedSourceValues ?? Array.Empty<NarrativeVariableValueData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                targetValue = targetValue?.Clone() ?? NarrativeVariableValueData.Bool(true),
                conditions = (conditions ?? Array.Empty<NarrativeConditionDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                consequences = (consequences ?? Array.Empty<NarrativeActionDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.order).ThenBy(value => value.actionDefinitionId, StringComparer.Ordinal).ToArray(),
                repeatPolicy = repeatPolicy == NarrativeTransitionRepeatPolicy.Unknown ? NarrativeTransitionRepeatPolicy.IdempotentSameTarget : repeatPolicy,
                reentryPolicy = reentryPolicy == NarrativeTransitionReentryPolicy.Unknown ? NarrativeTransitionReentryPolicy.RejectAfterTerminal : reentryPolicy,
                visibility = visibility == NarrativeStateVisibility.Unknown ? NarrativeStateVisibility.Public : visibility,
                order = order,
                automatic = automatic,
                hidden = hidden
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeStateDefinitionData
    {
        public string stateDefinitionId;
        public string displayName;
        public NarrativeStateScope scope = NarrativeStateScope.World;
        public NarrativeStateVisibility visibility = NarrativeStateVisibility.Public;
        public string domainId;
        public string[] tagIds = Array.Empty<string>();
        public NarrativeVariableDefinitionData[] variables = Array.Empty<NarrativeVariableDefinitionData>();
        public NarrativeStateTransitionDefinitionData[] transitions = Array.Empty<NarrativeStateTransitionDefinitionData>();

        public NarrativeStateDefinitionData Clone()
        {
            return new NarrativeStateDefinitionData
            {
                stateDefinitionId = N(stateDefinitionId),
                displayName = displayName ?? string.Empty,
                scope = scope == NarrativeStateScope.Unknown ? NarrativeStateScope.World : scope,
                visibility = visibility == NarrativeStateVisibility.Unknown ? NarrativeStateVisibility.Public : visibility,
                domainId = N(domainId),
                tagIds = NarrativeModelUtility.Clean(tagIds),
                variables = (variables ?? Array.Empty<NarrativeVariableDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                transitions = (transitions ?? Array.Empty<NarrativeStateTransitionDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.order).ThenBy(value => value.transitionDefinitionId, StringComparer.Ordinal).ToArray()
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeStateVariableRecordData
    {
        public string variableDefinitionId;
        public NarrativeVariableValueData value = NarrativeVariableValueData.Bool(false);
        public double changedWorldTime;
        public string sourceTransitionId;
        public long revision;

        public NarrativeStateVariableRecordData Clone()
        {
            return new NarrativeStateVariableRecordData
            {
                variableDefinitionId = N(variableDefinitionId),
                value = value?.Clone() ?? NarrativeVariableValueData.Bool(false),
                changedWorldTime = changedWorldTime,
                sourceTransitionId = N(sourceTransitionId),
                revision = revision
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeStateRecordData
    {
        public string narrativeStateId;
        public string stateDefinitionId;
        public string worldId;
        public NarrativeStateScope scope = NarrativeStateScope.World;
        public string scopeKey;
        public NarrativeStateLifecycle lifecycle = NarrativeStateLifecycle.Active;
        public NarrativeStateVariableRecordData[] variables = Array.Empty<NarrativeStateVariableRecordData>();
        public double createdWorldTime;
        public double updatedWorldTime;
        public long revision;

        public NarrativeStateRecordData Clone()
        {
            return new NarrativeStateRecordData
            {
                narrativeStateId = N(narrativeStateId),
                stateDefinitionId = N(stateDefinitionId),
                worldId = N(worldId),
                scope = scope == NarrativeStateScope.Unknown ? NarrativeStateScope.World : scope,
                scopeKey = N(scopeKey),
                lifecycle = lifecycle == NarrativeStateLifecycle.Unknown ? NarrativeStateLifecycle.Active : lifecycle,
                variables = (variables ?? Array.Empty<NarrativeStateVariableRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.variableDefinitionId, StringComparer.Ordinal).ToArray(),
                createdWorldTime = createdWorldTime,
                updatedWorldTime = updatedWorldTime,
                revision = revision
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeStateTransitionRecordData
    {
        public string transitionId;
        public string transitionDefinitionId;
        public string narrativeStateId;
        public string stateDefinitionId;
        public string variableDefinitionId;
        public string worldId;
        public NarrativeStateScope scope = NarrativeStateScope.World;
        public string scopeKey;
        public NarrativeTransitionSourceKind sourceKind = NarrativeTransitionSourceKind.Development;
        public string sourceId;
        public string sourceTransactionId;
        public string actorPersonId;
        public string questId;
        public string conversationId;
        public string narrativeEventId;
        public NarrativeVariableValueData oldValue = NarrativeVariableValueData.Bool(false);
        public NarrativeVariableValueData newValue = NarrativeVariableValueData.Bool(true);
        public NarrativeConditionResultData[] conditions = Array.Empty<NarrativeConditionResultData>();
        public NarrativeActionExecutionRecordData[] consequences = Array.Empty<NarrativeActionExecutionRecordData>();
        public double worldTime;
        public long revisionBefore;
        public long revisionAfter;
        public int sequence;
        public NarrativeStateVisibility visibility = NarrativeStateVisibility.Public;
        public string provenanceId;

        public NarrativeStateTransitionRecordData Clone()
        {
            return new NarrativeStateTransitionRecordData
            {
                transitionId = N(transitionId),
                transitionDefinitionId = N(transitionDefinitionId),
                narrativeStateId = N(narrativeStateId),
                stateDefinitionId = N(stateDefinitionId),
                variableDefinitionId = N(variableDefinitionId),
                worldId = N(worldId),
                scope = scope == NarrativeStateScope.Unknown ? NarrativeStateScope.World : scope,
                scopeKey = N(scopeKey),
                sourceKind = sourceKind == NarrativeTransitionSourceKind.Unknown ? NarrativeTransitionSourceKind.Development : sourceKind,
                sourceId = N(sourceId),
                sourceTransactionId = N(sourceTransactionId),
                actorPersonId = N(actorPersonId),
                questId = N(questId),
                conversationId = N(conversationId),
                narrativeEventId = N(narrativeEventId),
                oldValue = oldValue?.Clone() ?? NarrativeVariableValueData.Bool(false),
                newValue = newValue?.Clone() ?? NarrativeVariableValueData.Bool(true),
                conditions = (conditions ?? Array.Empty<NarrativeConditionResultData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                consequences = (consequences ?? Array.Empty<NarrativeActionExecutionRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.order).ThenBy(value => value.actionDefinitionId, StringComparer.Ordinal).ToArray(),
                worldTime = worldTime,
                revisionBefore = revisionBefore,
                revisionAfter = revisionAfter,
                sequence = sequence,
                visibility = visibility == NarrativeStateVisibility.Unknown ? NarrativeStateVisibility.Public : visibility,
                provenanceId = N(provenanceId)
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    public sealed class NarrativeStateTransitionRequest
    {
        public string transactionId;
        public string transitionDefinitionId;
        public string stateDefinitionId;
        public NarrativeStateScope scope = NarrativeStateScope.World;
        public string scopeKey;
        public NarrativeTransitionSourceKind sourceKind = NarrativeTransitionSourceKind.Development;
        public string sourceId;
        public string actorPersonId;
        public string questId;
        public string conversationId;
        public string narrativeEventId;
        public NarrativeConditionContextData conditionContext;
        public double worldTime;
        public bool preview;
        public long expectedRevision = -1L;
        public int cascadeDepth;

        public NarrativeStateTransitionRequest Clone()
        {
            return new NarrativeStateTransitionRequest
            {
                transactionId = N(transactionId),
                transitionDefinitionId = N(transitionDefinitionId),
                stateDefinitionId = N(stateDefinitionId),
                scope = scope == NarrativeStateScope.Unknown ? NarrativeStateScope.World : scope,
                scopeKey = N(scopeKey),
                sourceKind = sourceKind == NarrativeTransitionSourceKind.Unknown ? NarrativeTransitionSourceKind.Development : sourceKind,
                sourceId = N(sourceId),
                actorPersonId = N(actorPersonId),
                questId = N(questId),
                conversationId = N(conversationId),
                narrativeEventId = N(narrativeEventId),
                conditionContext = conditionContext?.Clone(),
                worldTime = worldTime,
                preview = preview,
                expectedRevision = expectedRevision,
                cascadeDepth = cascadeDepth
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    public sealed class NarrativeStateQuery
    {
        public string stateDefinitionId;
        public NarrativeStateScope? scope;
        public string scopeKey;
        public bool developmentView = true;
        public bool hideConcealedCounts;
    }

    public sealed class NarrativeStateConditionQuery
    {
        public string stateDefinitionId;
        public string variableDefinitionId;
        public NarrativeStateScope scope = NarrativeStateScope.World;
        public string scopeKey;
        public NarrativeVariableValueData expectedValue;
        public int minimumValue = int.MinValue;
        public int maximumValue = int.MaxValue;
        public bool negate;
    }

    [Serializable]
    public sealed class NarrativeStateRuntimeSaveData
    {
        public int schemaVersion = 1;
        public string worldId;
        public long revision;
        public NarrativeStateRecordData[] states = Array.Empty<NarrativeStateRecordData>();
        public NarrativeStateTransitionRecordData[] transitions = Array.Empty<NarrativeStateTransitionRecordData>();

        public NarrativeStateRuntimeSaveData Clone()
        {
            return new NarrativeStateRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = N(worldId),
                revision = revision,
                states = (states ?? Array.Empty<NarrativeStateRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.narrativeStateId, StringComparer.Ordinal).ToArray(),
                transitions = (transitions ?? Array.Empty<NarrativeStateTransitionRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.sequence).ThenBy(value => value.transitionId, StringComparer.Ordinal).ToArray()
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    public sealed class NarrativeStateSnapshot
    {
        private readonly NarrativeStateRecordData data;
        private readonly NarrativeStateDefinitionData definition;
        private readonly bool developmentView;

        public NarrativeStateSnapshot(NarrativeStateRecordData record, NarrativeStateDefinitionData stateDefinition, bool development)
        {
            data = record?.Clone() ?? new NarrativeStateRecordData();
            definition = stateDefinition?.Clone() ?? new NarrativeStateDefinitionData();
            developmentView = development;
        }

        public string NarrativeStateId => data.narrativeStateId;
        public string StateDefinitionId => data.stateDefinitionId;
        public string WorldId => data.worldId;
        public NarrativeStateScope Scope => data.scope;
        public string ScopeKey => data.scopeKey;
        public NarrativeStateLifecycle Lifecycle => data.lifecycle;
        public long Revision => data.revision;
        public bool IsHidden => definition.visibility == NarrativeStateVisibility.Hidden || definition.visibility == NarrativeStateVisibility.Secret || definition.visibility == NarrativeStateVisibility.Diagnostic;
        public IReadOnlyList<NarrativeStateVariableRecordData> Variables => developmentView || !IsHidden ? data.variables.Select(value => value.Clone()).ToArray() : Array.Empty<NarrativeStateVariableRecordData>();

        public bool TryGetValue(string variableDefinitionId, out NarrativeVariableValueData value)
        {
            value = data.variables.FirstOrDefault(variable => string.Equals(variable.variableDefinitionId, variableDefinitionId, StringComparison.Ordinal))?.value?.Clone();
            return value != null;
        }

        public NarrativeStateRecordData ToSaveData() => data.Clone();
    }

    public sealed class NarrativeStateTransitionSnapshot
    {
        private readonly NarrativeStateTransitionRecordData data;
        private readonly bool redacted;

        public NarrativeStateTransitionSnapshot(NarrativeStateTransitionRecordData record, bool developmentView)
        {
            data = record?.Clone() ?? new NarrativeStateTransitionRecordData();
            redacted = !developmentView && (data.visibility == NarrativeStateVisibility.Hidden || data.visibility == NarrativeStateVisibility.Secret || data.visibility == NarrativeStateVisibility.Diagnostic);
        }

        public string TransitionId => data.transitionId;
        public string TransitionDefinitionId => data.transitionDefinitionId;
        public string StateDefinitionId => data.stateDefinitionId;
        public string VariableDefinitionId => redacted ? string.Empty : data.variableDefinitionId;
        public string NarrativeStateId => data.narrativeStateId;
        public NarrativeTransitionSourceKind SourceKind => data.sourceKind;
        public string SourceId => redacted ? string.Empty : data.sourceId;
        public double WorldTime => data.worldTime;
        public int Sequence => data.sequence;
        public NarrativeVariableValueData OldValue => redacted ? null : data.oldValue.Clone();
        public NarrativeVariableValueData NewValue => redacted ? null : data.newValue.Clone();
        public IReadOnlyList<NarrativeActionExecutionRecordData> Consequences => redacted ? Array.Empty<NarrativeActionExecutionRecordData>() : data.consequences.Select(value => value.Clone()).ToArray();
        public NarrativeStateTransitionRecordData ToSaveData() => data.Clone();
    }

    public sealed class NarrativeStateTransitionResult
    {
        public NarrativeStateTransitionResult(
            NarrativeStateTransitionStatus status,
            string message,
            long revisionBefore,
            long revisionAfter,
            NarrativeStateSnapshot snapshot = null,
            NarrativeStateTransitionSnapshot transition = null,
            IReadOnlyList<NarrativeActionExecutionRecordData> consequences = null,
            bool preview = false,
            bool duplicate = false)
        {
            Status = status;
            Message = message ?? string.Empty;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
            Snapshot = snapshot;
            Transition = transition;
            Consequences = consequences ?? Array.Empty<NarrativeActionExecutionRecordData>();
            Preview = preview;
            Duplicate = duplicate;
        }

        public NarrativeStateTransitionStatus Status { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public NarrativeStateSnapshot Snapshot { get; }
        public NarrativeStateTransitionSnapshot Transition { get; }
        public IReadOnlyList<NarrativeActionExecutionRecordData> Consequences { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public bool Succeeded => Status == NarrativeStateTransitionStatus.Succeeded || Status == NarrativeStateTransitionStatus.Preview || Status == NarrativeStateTransitionStatus.Duplicate;

        public static NarrativeStateTransitionResult Success(string message, long before, long after, NarrativeStateSnapshot snapshot, NarrativeStateTransitionSnapshot transition, IReadOnlyList<NarrativeActionExecutionRecordData> consequences = null, bool preview = false, bool duplicate = false)
        {
            return new NarrativeStateTransitionResult(preview ? NarrativeStateTransitionStatus.Preview : duplicate ? NarrativeStateTransitionStatus.Duplicate : NarrativeStateTransitionStatus.Succeeded, message, before, after, snapshot, transition, consequences, preview, duplicate);
        }

        public static NarrativeStateTransitionResult Failure(NarrativeStateTransitionStatus status, string message, long revision, NarrativeStateSnapshot snapshot = null)
        {
            return new NarrativeStateTransitionResult(status, message, revision, revision, snapshot);
        }
    }

    public sealed class NarrativeStateRuntimeIntegrations
    {
        public NarrativeEventRuntime NarrativeEventRuntime { get; set; }
        public Func<NarrativeActionDefinitionData, NarrativeStateTransitionRequest, bool> ConsequenceValidator { get; set; }
        public Func<NarrativeActionDefinitionData, NarrativeStateTransitionRequest, string> ConsequenceExecutor { get; set; }
    }

    public sealed class NarrativeStateValidationReport
    {
        public NarrativeStateValidationReport(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Errors.Count == 0;
    }
}
