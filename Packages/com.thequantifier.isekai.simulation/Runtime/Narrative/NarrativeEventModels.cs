using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Narrative
{
    [Serializable]
    public sealed class NarrativeTriggerDefinitionData
    {
        public string triggerDefinitionId;
        public NarrativeTriggerCategory category = NarrativeTriggerCategory.ExplicitSignal;
        public string requiredSourceId;
        public string requiredSubjectId;
        public bool committedOnly = true;
        public bool ignoreRestoreReplay = true;
        public bool hidden;

        public NarrativeTriggerDefinitionData Clone()
        {
            return new NarrativeTriggerDefinitionData
            {
                triggerDefinitionId = N(triggerDefinitionId),
                category = category == NarrativeTriggerCategory.Unknown ? NarrativeTriggerCategory.ExplicitSignal : category,
                requiredSourceId = N(requiredSourceId),
                requiredSubjectId = N(requiredSubjectId),
                committedOnly = committedOnly,
                ignoreRestoreReplay = ignoreRestoreReplay,
                hidden = hidden
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeConditionDefinitionData
    {
        public string conditionDefinitionId;
        public NarrativeConditionCategory category = NarrativeConditionCategory.Always;
        public string requiredId;
        public string secondaryId;
        public int minimumValue = 1;
        public bool negate;
        public bool hidden;
        public bool revealFailure = true;

        public NarrativeConditionDefinitionData Clone()
        {
            return new NarrativeConditionDefinitionData
            {
                conditionDefinitionId = N(conditionDefinitionId),
                category = category == NarrativeConditionCategory.Unknown ? NarrativeConditionCategory.Always : category,
                requiredId = N(requiredId),
                secondaryId = N(secondaryId),
                minimumValue = minimumValue,
                negate = negate,
                hidden = hidden,
                revealFailure = revealFailure
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeActionDefinitionData
    {
        public string actionDefinitionId;
        public NarrativeActionCategory category = NarrativeActionCategory.None;
        public NarrativeActionRequirement requirement = NarrativeActionRequirement.Required;
        public string targetId;
        public string secondaryTargetId;
        public string outputSlotId;
        public string inputSlotId;
        public int order;
        public int priority;
        public bool hidden;

        public NarrativeActionDefinitionData Clone()
        {
            return new NarrativeActionDefinitionData
            {
                actionDefinitionId = N(actionDefinitionId),
                category = category == NarrativeActionCategory.Unknown ? NarrativeActionCategory.None : category,
                requirement = requirement,
                targetId = N(targetId),
                secondaryTargetId = N(secondaryTargetId),
                outputSlotId = N(outputSlotId),
                inputSlotId = N(inputSlotId),
                order = order,
                priority = priority,
                hidden = hidden
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeEventDefinitionData
    {
        public string eventDefinitionId;
        public string displayName;
        public NarrativeEventCategory category = NarrativeEventCategory.World;
        public NarrativeEventScope scope = NarrativeEventScope.OncePerWorld;
        public NarrativeRepeatPolicy repeatPolicy = NarrativeRepeatPolicy.OncePerScope;
        public NarrativeArmingPolicy armingPolicy = NarrativeArmingPolicy.OnWorldInitialization;
        public NarrativeTriggerMode triggerMode = NarrativeTriggerMode.TriggerImmediatelyWhenMatched;
        public NarrativeDelayedRevalidationPolicy delayedRevalidationPolicy = NarrativeDelayedRevalidationPolicy.Revalidate;
        public NarrativeActionAtomicityPolicy atomicityPolicy = NarrativeActionAtomicityPolicy.AtomicAllActions;
        public NarrativeRetryPolicy retryPolicy = NarrativeRetryPolicy.NeverRetryAutomatically;
        public NarrativeEventVisibility visibility = NarrativeEventVisibility.Public;
        public NarrativeConditionGroupPolicy conditionGroupPolicy = NarrativeConditionGroupPolicy.All;
        public int atLeastConditionCount = 1;
        public int priority;
        public double activationStartTime = -1d;
        public double activationEndTime = -1d;
        public double delayDuration = -1d;
        public int cascadeDepthLimit = 8;
        public string scopeSelectorId;
        public string historyPolicyId;
        public string failurePolicyId;
        public string[] tagIds = Array.Empty<string>();
        public NarrativeTriggerDefinitionData[] triggers = Array.Empty<NarrativeTriggerDefinitionData>();
        public NarrativeConditionDefinitionData[] conditions = Array.Empty<NarrativeConditionDefinitionData>();
        public NarrativeActionDefinitionData[] actions = Array.Empty<NarrativeActionDefinitionData>();

        public NarrativeEventDefinitionData Clone()
        {
            return new NarrativeEventDefinitionData
            {
                eventDefinitionId = N(eventDefinitionId),
                displayName = displayName ?? string.Empty,
                category = category == NarrativeEventCategory.Unknown ? NarrativeEventCategory.World : category,
                scope = scope == NarrativeEventScope.Unknown ? NarrativeEventScope.OncePerWorld : scope,
                repeatPolicy = repeatPolicy == NarrativeRepeatPolicy.Unknown ? NarrativeRepeatPolicy.OncePerScope : repeatPolicy,
                armingPolicy = armingPolicy == NarrativeArmingPolicy.Unknown ? NarrativeArmingPolicy.OnWorldInitialization : armingPolicy,
                triggerMode = triggerMode == NarrativeTriggerMode.Unknown ? NarrativeTriggerMode.TriggerImmediatelyWhenMatched : triggerMode,
                delayedRevalidationPolicy = delayedRevalidationPolicy,
                atomicityPolicy = atomicityPolicy,
                retryPolicy = retryPolicy,
                visibility = visibility == NarrativeEventVisibility.Unknown ? NarrativeEventVisibility.Public : visibility,
                conditionGroupPolicy = conditionGroupPolicy,
                atLeastConditionCount = Math.Max(0, atLeastConditionCount),
                priority = priority,
                activationStartTime = activationStartTime,
                activationEndTime = activationEndTime,
                delayDuration = delayDuration,
                cascadeDepthLimit = Math.Max(1, cascadeDepthLimit),
                scopeSelectorId = N(scopeSelectorId),
                historyPolicyId = N(historyPolicyId),
                failurePolicyId = N(failurePolicyId),
                tagIds = NarrativeModelUtility.Clean(tagIds),
                triggers = (triggers ?? Array.Empty<NarrativeTriggerDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                conditions = (conditions ?? Array.Empty<NarrativeConditionDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                actions = (actions ?? Array.Empty<NarrativeActionDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.order).ThenBy(value => value.actionDefinitionId, StringComparer.Ordinal).ToArray()
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeTriggerSourceData
    {
        public NarrativeTriggerCategory category = NarrativeTriggerCategory.ExplicitSignal;
        public string sourceId;
        public string sourceTransactionId;
        public string actorPersonId;
        public string targetId;
        public string subjectId;
        public string ownerRuntime;
        public double worldTime;
        public bool preview;
        public bool committed = true;
        public bool restoreReplay;

        public NarrativeTriggerSourceData Clone()
        {
            return new NarrativeTriggerSourceData
            {
                category = category == NarrativeTriggerCategory.Unknown ? NarrativeTriggerCategory.ExplicitSignal : category,
                sourceId = N(sourceId),
                sourceTransactionId = N(sourceTransactionId),
                actorPersonId = N(actorPersonId),
                targetId = N(targetId),
                subjectId = N(subjectId),
                ownerRuntime = N(ownerRuntime),
                worldTime = worldTime,
                preview = preview,
                committed = committed,
                restoreReplay = restoreReplay
            };
        }

        public string StableOccurrenceKey => $"{category}:{sourceId}:{sourceTransactionId}:{subjectId}:{targetId}:{worldTime:0.###}";
        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeConditionContextData
    {
        public string actorPersonId;
        public string questId;
        public string conversationId;
        public string locationId;
        public string organizationId;
        public string governmentId;
        public string itemId;
        public string subjectId;
        public double worldTime;
        public string[] authoritativeTruthIds = Array.Empty<string>();
        public string[] knownSubjectIds = Array.Empty<string>();
        public string[] beliefIds = Array.Empty<string>();
        public string[] questStateIds = Array.Empty<string>();
        public string[] dialogueStateIds = Array.Empty<string>();
        public string[] locationStateIds = Array.Empty<string>();
        public string[] itemStateIds = Array.Empty<string>();
        public string[] characterStateIds = Array.Empty<string>();
        public string[] organizationStateIds = Array.Empty<string>();
        public string[] socialStateIds = Array.Empty<string>();
        public string[] economicStateIds = Array.Empty<string>();
        public string[] legalStateIds = Array.Empty<string>();
        public string[] historicalStateIds = Array.Empty<string>();
        public string[] narrativeStateIds = Array.Empty<string>();
        public string[] customStateIds = Array.Empty<string>();

        public NarrativeConditionContextData Clone()
        {
            return new NarrativeConditionContextData
            {
                actorPersonId = N(actorPersonId),
                questId = N(questId),
                conversationId = N(conversationId),
                locationId = N(locationId),
                organizationId = N(organizationId),
                governmentId = N(governmentId),
                itemId = N(itemId),
                subjectId = N(subjectId),
                worldTime = worldTime,
                authoritativeTruthIds = NarrativeModelUtility.Clean(authoritativeTruthIds),
                knownSubjectIds = NarrativeModelUtility.Clean(knownSubjectIds),
                beliefIds = NarrativeModelUtility.Clean(beliefIds),
                questStateIds = NarrativeModelUtility.Clean(questStateIds),
                dialogueStateIds = NarrativeModelUtility.Clean(dialogueStateIds),
                locationStateIds = NarrativeModelUtility.Clean(locationStateIds),
                itemStateIds = NarrativeModelUtility.Clean(itemStateIds),
                characterStateIds = NarrativeModelUtility.Clean(characterStateIds),
                organizationStateIds = NarrativeModelUtility.Clean(organizationStateIds),
                socialStateIds = NarrativeModelUtility.Clean(socialStateIds),
                economicStateIds = NarrativeModelUtility.Clean(economicStateIds),
                legalStateIds = NarrativeModelUtility.Clean(legalStateIds),
                historicalStateIds = NarrativeModelUtility.Clean(historicalStateIds),
                narrativeStateIds = NarrativeModelUtility.Clean(narrativeStateIds),
                customStateIds = NarrativeModelUtility.Clean(customStateIds)
            };
        }

        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    [Serializable]
    public sealed class NarrativeConditionResultData
    {
        public string conditionDefinitionId;
        public NarrativeConditionCategory category;
        public string subjectId;
        public string sourceRuntime;
        public bool matched;
        public bool hidden;
        public string reason;

        public NarrativeConditionResultData Clone()
        {
            return new NarrativeConditionResultData
            {
                conditionDefinitionId = conditionDefinitionId ?? string.Empty,
                category = category,
                subjectId = subjectId ?? string.Empty,
                sourceRuntime = sourceRuntime ?? string.Empty,
                matched = matched,
                hidden = hidden,
                reason = reason ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class NarrativeActionExecutionRecordData
    {
        public string actionExecutionId;
        public string narrativeEventId;
        public string actionDefinitionId;
        public NarrativeActionCategory category;
        public NarrativeActionLifecycle lifecycle = NarrativeActionLifecycle.Pending;
        public NarrativeActionRequirement requirement = NarrativeActionRequirement.Required;
        public int order;
        public string targetOwnerRuntime;
        public string externalResultId;
        public string outputSlotId;
        public string resultValue;
        public string message;
        public double worldTime;
        public long runtimeRevision;

        public NarrativeActionExecutionRecordData Clone()
        {
            return new NarrativeActionExecutionRecordData
            {
                actionExecutionId = actionExecutionId ?? string.Empty,
                narrativeEventId = narrativeEventId ?? string.Empty,
                actionDefinitionId = actionDefinitionId ?? string.Empty,
                category = category,
                lifecycle = lifecycle,
                requirement = requirement,
                order = order,
                targetOwnerRuntime = targetOwnerRuntime ?? string.Empty,
                externalResultId = externalResultId ?? string.Empty,
                outputSlotId = outputSlotId ?? string.Empty,
                resultValue = resultValue ?? string.Empty,
                message = message ?? string.Empty,
                worldTime = worldTime,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class NarrativeEventRecordData
    {
        public string narrativeEventId;
        public string eventDefinitionId;
        public string worldId;
        public NarrativeEventLifecycle lifecycle = NarrativeEventLifecycle.Created;
        public NarrativeEventScope scope = NarrativeEventScope.OncePerWorld;
        public string scopeKey;
        public string actorPersonId;
        public string questId;
        public string conversationId;
        public string locationId;
        public string organizationId;
        public string subjectId;
        public double armTime = -1d;
        public double triggerTime = -1d;
        public double executionStartTime = -1d;
        public double executionEndTime = -1d;
        public NarrativeTriggerSourceData triggerSource = new NarrativeTriggerSourceData();
        public NarrativeConditionResultData[] matchedConditions = Array.Empty<NarrativeConditionResultData>();
        public NarrativeActionExecutionRecordData[] actionExecutions = Array.Empty<NarrativeActionExecutionRecordData>();
        public NarrativeEventVisibility visibility = NarrativeEventVisibility.Public;
        public string provenanceId;
        public string sourceLineageId;
        public int cascadeDepth;
        public long revision = 1L;

        public NarrativeEventRecordData Clone()
        {
            return new NarrativeEventRecordData
            {
                narrativeEventId = narrativeEventId ?? string.Empty,
                eventDefinitionId = eventDefinitionId ?? string.Empty,
                worldId = worldId ?? string.Empty,
                lifecycle = lifecycle,
                scope = scope,
                scopeKey = scopeKey ?? string.Empty,
                actorPersonId = actorPersonId ?? string.Empty,
                questId = questId ?? string.Empty,
                conversationId = conversationId ?? string.Empty,
                locationId = locationId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                armTime = armTime,
                triggerTime = triggerTime,
                executionStartTime = executionStartTime,
                executionEndTime = executionEndTime,
                triggerSource = triggerSource?.Clone() ?? new NarrativeTriggerSourceData(),
                matchedConditions = (matchedConditions ?? Array.Empty<NarrativeConditionResultData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                actionExecutions = (actionExecutions ?? Array.Empty<NarrativeActionExecutionRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                visibility = visibility,
                provenanceId = provenanceId ?? string.Empty,
                sourceLineageId = sourceLineageId ?? string.Empty,
                cascadeDepth = cascadeDepth,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class NarrativeSignalRecordData
    {
        public string narrativeSignalId;
        public string signalDefinitionId;
        public NarrativeSignalSourceKind sourceKind = NarrativeSignalSourceKind.NarrativeSystem;
        public string sourceId;
        public string sourceTransactionId;
        public string actorPersonId;
        public string[] subjectIds = Array.Empty<string>();
        public string provenanceId;
        public double worldTime;
        public long runtimeRevision;

        public NarrativeSignalRecordData Clone()
        {
            return new NarrativeSignalRecordData
            {
                narrativeSignalId = narrativeSignalId ?? string.Empty,
                signalDefinitionId = signalDefinitionId ?? string.Empty,
                sourceKind = sourceKind,
                sourceId = sourceId ?? string.Empty,
                sourceTransactionId = sourceTransactionId ?? string.Empty,
                actorPersonId = actorPersonId ?? string.Empty,
                subjectIds = NarrativeModelUtility.Clean(subjectIds),
                provenanceId = provenanceId ?? string.Empty,
                worldTime = worldTime,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class NarrativeRuntimeTransactionData
    {
        public string transactionId;
        public string operation;
        public string narrativeEventId;
        public NarrativeOperationStatus status;
        public long runtimeRevision;

        public NarrativeRuntimeTransactionData Clone()
        {
            return new NarrativeRuntimeTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                narrativeEventId = narrativeEventId ?? string.Empty,
                status = status,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class NarrativeEventRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<NarrativeEventRecordData> events = new List<NarrativeEventRecordData>();
        public List<NarrativeSignalRecordData> signals = new List<NarrativeSignalRecordData>();
        public List<NarrativeRuntimeTransactionData> transactions = new List<NarrativeRuntimeTransactionData>();
        public string[] processedTriggerKeys = Array.Empty<string>();

        public NarrativeEventRuntimeSaveData Clone()
        {
            return new NarrativeEventRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                events = (events ?? new List<NarrativeEventRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                signals = (signals ?? new List<NarrativeSignalRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                transactions = (transactions ?? new List<NarrativeRuntimeTransactionData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                processedTriggerKeys = NarrativeModelUtility.Clean(processedTriggerKeys)
            };
        }
    }

    public sealed class NarrativeEventSnapshot
    {
        private readonly NarrativeEventRecordData data;

        public NarrativeEventSnapshot(NarrativeEventRecordData record, NarrativeEventDefinitionData definition = null, bool development = true)
        {
            data = record?.Clone() ?? new NarrativeEventRecordData();
            Definition = definition?.Clone();
            DevelopmentView = development;
        }

        public string NarrativeEventId => data.narrativeEventId;
        public string EventDefinitionId => data.eventDefinitionId;
        public string WorldId => data.worldId;
        public NarrativeEventLifecycle Lifecycle => data.lifecycle;
        public NarrativeEventScope Scope => data.scope;
        public string ScopeKey => data.scopeKey;
        public string ActorPersonId => data.actorPersonId;
        public string QuestId => data.questId;
        public string ConversationId => data.conversationId;
        public string LocationId => data.locationId;
        public string OrganizationId => data.organizationId;
        public string SubjectId => data.subjectId;
        public double ArmTime => data.armTime;
        public double TriggerTime => data.triggerTime;
        public double ExecutionStartTime => data.executionStartTime;
        public double ExecutionEndTime => data.executionEndTime;
        public NarrativeEventVisibility Visibility => data.visibility;
        public long Revision => data.revision;
        public bool DevelopmentView { get; }
        public NarrativeEventDefinitionData Definition { get; }
        public NarrativeTriggerSourceData TriggerSource => DevelopmentView || !IsHidden ? data.triggerSource.Clone() : new NarrativeTriggerSourceData { category = data.triggerSource.category };
        public IReadOnlyList<NarrativeConditionResultData> MatchedConditions => (DevelopmentView || !IsHidden) ? data.matchedConditions.Select(value => value.Clone()).ToArray() : Array.Empty<NarrativeConditionResultData>();
        public IReadOnlyList<NarrativeActionExecutionRecordData> ActionExecutions => (DevelopmentView || !IsHidden) ? data.actionExecutions.Select(value => value.Clone()).ToArray() : Array.Empty<NarrativeActionExecutionRecordData>();
        public bool IsHidden => data.visibility == NarrativeEventVisibility.Hidden || data.visibility == NarrativeEventVisibility.Secret || data.visibility == NarrativeEventVisibility.Diagnostic;

        public NarrativeEventRecordData ToSaveData() => data.Clone();
    }

    public sealed class NarrativeEventOperationResult
    {
        public NarrativeEventOperationResult(
            NarrativeOperationStatus status,
            string message,
            long revisionBefore,
            long revisionAfter,
            NarrativeEventSnapshot snapshot = null,
            IReadOnlyList<NarrativeEventSnapshot> snapshots = null,
            IReadOnlyList<NarrativeActionExecutionRecordData> actionResults = null,
            bool preview = false,
            bool duplicate = false)
        {
            Status = status;
            Message = message ?? string.Empty;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
            Snapshot = snapshot;
            Snapshots = snapshots ?? Array.Empty<NarrativeEventSnapshot>();
            ActionResults = actionResults ?? Array.Empty<NarrativeActionExecutionRecordData>();
            Preview = preview;
            Duplicate = duplicate;
        }

        public NarrativeOperationStatus Status { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public NarrativeEventSnapshot Snapshot { get; }
        public IReadOnlyList<NarrativeEventSnapshot> Snapshots { get; }
        public IReadOnlyList<NarrativeActionExecutionRecordData> ActionResults { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public bool Succeeded => Status == NarrativeOperationStatus.Succeeded || Status == NarrativeOperationStatus.Preview || Status == NarrativeOperationStatus.Duplicate;

        public static NarrativeEventOperationResult Success(string message, long before, long after, NarrativeEventSnapshot snapshot = null, IReadOnlyList<NarrativeActionExecutionRecordData> actionResults = null, bool preview = false, bool duplicate = false)
        {
            return new NarrativeEventOperationResult(preview ? NarrativeOperationStatus.Preview : duplicate ? NarrativeOperationStatus.Duplicate : NarrativeOperationStatus.Succeeded, message, before, after, snapshot, actionResults: actionResults, preview: preview, duplicate: duplicate);
        }

        public static NarrativeEventOperationResult SuccessMany(string message, long before, long after, IReadOnlyList<NarrativeEventSnapshot> snapshots, bool preview = false)
        {
            return new NarrativeEventOperationResult(preview ? NarrativeOperationStatus.Preview : NarrativeOperationStatus.Succeeded, message, before, after, snapshots: snapshots, preview: preview);
        }

        public static NarrativeEventOperationResult Failure(NarrativeOperationStatus status, string message, long revision, NarrativeEventSnapshot snapshot = null)
        {
            return new NarrativeEventOperationResult(status, message, revision, revision, snapshot);
        }
    }

    public sealed class NarrativeEventQuery
    {
        public string eventId;
        public string definitionId;
        public string scopeKey;
        public string personId;
        public string questId;
        public string conversationId;
        public string locationId;
        public string organizationId;
        public NarrativeEventLifecycle? lifecycle;
        public double minWorldTime = -1d;
        public double maxWorldTime = -1d;
        public bool developmentView = true;
        public bool hideConcealedCounts;
    }

    public sealed class NarrativeEventRuntimeIntegrations
    {
        public QuestRuntime QuestRuntime { get; set; }
        public QuestSourceRuntime QuestSourceRuntime { get; set; }
        public Dialogue.ConversationRuntime ConversationRuntime { get; set; }
        public Func<string, bool> InformationGrantExecutor { get; set; }
        public Func<string, bool> TravelConditionExecutor { get; set; }
        public Func<string, bool> ConnectionChangeExecutor { get; set; }
        public Func<string, bool> SocialActionExecutor { get; set; }
        public Func<NarrativeSocialActionRequest, bool> ContextualSocialActionExecutor { get; set; }
        public Func<string, bool> OrganizationActionExecutor { get; set; }
        public Func<string, bool> LegalActionExecutor { get; set; }
        public Func<NarrativeStateTransitionRequest, NarrativeStateTransitionResult> NarrativeStateTransitionExecutor { get; set; }
        public Func<NarrativeConditionDefinitionData, NarrativeConditionContextData, bool> NarrativeStateConditionEvaluator { get; set; }
        public Func<NarrativeArcSignalRequest, NarrativeArcOperationResult> NarrativeArcSignalExecutor { get; set; }
        public Func<NarrativeConditionDefinitionData, NarrativeConditionContextData, bool> NarrativeArcConditionEvaluator { get; set; }
    }

    public sealed class NarrativeSocialActionRequest
    {
        public string InteractionDefinitionId { get; set; }
        public string ActorPersonId { get; set; }
        public string TargetPersonId { get; set; }
        public string NarrativeEventId { get; set; }
        public string ActionDefinitionId { get; set; }
        public double WorldTime { get; set; }
    }

    public static class NarrativeModelUtility
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

        public static string SanitizeForId(string value)
        {
            string cleaned = N(value);
            if (string.IsNullOrWhiteSpace(cleaned)) return "none";
            char[] chars = cleaned.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray();
            return new string(chars).Trim('-');
        }

        public static InformationSubjectReferenceData Subject(string type, string id)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = string.IsNullOrWhiteSpace(type) ? InformationSubjectType.Custom : Enum.TryParse(type, out InformationSubjectType parsed) ? parsed : InformationSubjectType.Custom,
                subjectId = N(id)
            };
        }
    }
}
