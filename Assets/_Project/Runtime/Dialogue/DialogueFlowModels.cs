using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Dialogue
{
    [Serializable]
    public sealed class DialogueSpeakerSelectorData
    {
        public DialogueSpeakerSelectorKind kind = DialogueSpeakerSelectorKind.ActiveSpeaker;
        public ConversationParticipantRole role = ConversationParticipantRole.Speaker;
        public string personId;
        public string officeId;
        public string organizationId;
        public string customSelectorId;

        public DialogueSpeakerSelectorData Clone()
        {
            return new DialogueSpeakerSelectorData
            {
                kind = kind,
                role = role,
                personId = N(personId),
                officeId = N(officeId),
                organizationId = N(organizationId),
                customSelectorId = N(customSelectorId)
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
    }

    [Serializable]
    public sealed class DialogueListenerSelectorData
    {
        public DialogueListenerSelectorKind kind = DialogueListenerSelectorKind.AllParticipants;
        public ConversationParticipantRole role = ConversationParticipantRole.Listener;
        public string personId;
        public string customSelectorId;

        public DialogueListenerSelectorData Clone()
        {
            return new DialogueListenerSelectorData
            {
                kind = kind,
                role = role,
                personId = N(personId),
                customSelectorId = N(customSelectorId)
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
    }

    [Serializable]
    public sealed class DialogueConditionData
    {
        public string conditionId;
        public DialogueConditionKind kind = DialogueConditionKind.Always;
        public DialogueConditionEvaluationMode evaluationMode = DialogueConditionEvaluationMode.AuthoritativeTruth;
        public string requiredId;
        public string secondaryId;
        public int minimumValue = 1;
        public int maximumValue = 1;
        public DialogueValueComparison comparison = DialogueValueComparison.Exists;
        public bool hidden;
        public bool revealFailure = true;
        public bool negate;

        public DialogueConditionData Clone()
        {
            return new DialogueConditionData
            {
                conditionId = N(conditionId),
                kind = kind,
                evaluationMode = evaluationMode == DialogueConditionEvaluationMode.Unknown ? DialogueConditionEvaluationMode.AuthoritativeTruth : evaluationMode,
                requiredId = N(requiredId),
                secondaryId = N(secondaryId),
                minimumValue = minimumValue,
                maximumValue = maximumValue,
                comparison = comparison,
                hidden = hidden,
                revealFailure = revealFailure,
                negate = negate
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
    }

    [Serializable]
    public sealed class DialogueEffectData
    {
        public string effectId;
        public DialogueEffectKind kind = DialogueEffectKind.None;
        public DialogueEffectRequirement requirement = DialogueEffectRequirement.Optional;
        public string targetId;
        public string secondaryTargetId;
        public string value;
        public int quantity = 1;
        public string successNodeId;
        public string failureNodeId;
        public bool runOnNodeEntry;
        public bool oneShot = true;
        public bool hidden;

        public DialogueEffectData Clone()
        {
            return new DialogueEffectData
            {
                effectId = N(effectId),
                kind = kind,
                requirement = requirement,
                targetId = N(targetId),
                secondaryTargetId = N(secondaryTargetId),
                value = value ?? string.Empty,
                quantity = quantity,
                successNodeId = N(successNodeId),
                failureNodeId = N(failureNodeId),
                runOnNodeEntry = runOnNodeEntry,
                oneShot = oneShot,
                hidden = hidden
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
    }

    [Serializable]
    public sealed class DialogueTransitionDefinitionData
    {
        public string transitionId;
        public string targetNodeId;
        public DialogueTransitionCategory category = DialogueTransitionCategory.Automatic;
        public int priority;
        public DialogueConditionData[] conditions = Array.Empty<DialogueConditionData>();
        public DialogueEffectData[] effects = Array.Empty<DialogueEffectData>();
        public bool fallback;

        public DialogueTransitionDefinitionData Clone()
        {
            return new DialogueTransitionDefinitionData
            {
                transitionId = N(transitionId),
                targetNodeId = N(targetNodeId),
                category = category == DialogueTransitionCategory.Unknown ? DialogueTransitionCategory.Automatic : category,
                priority = priority,
                conditions = (conditions ?? Array.Empty<DialogueConditionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                effects = (effects ?? Array.Empty<DialogueEffectData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                fallback = fallback
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
    }

    [Serializable]
    public sealed class DialogueChoiceDefinitionData
    {
        public string choiceId;
        public string displayText;
        public DialogueChoiceCategory category = DialogueChoiceCategory.Response;
        public ConversationVisibility visibility = ConversationVisibility.Public;
        public ConversationParticipantRole actorRole = ConversationParticipantRole.Initiator;
        public DialogueChoiceRepeatPolicy repeatPolicy = DialogueChoiceRepeatPolicy.Repeatable;
        public string targetNodeId;
        public string unavailableReason;
        public bool hideUnavailableReason;
        public DialogueConditionData[] conditions = Array.Empty<DialogueConditionData>();
        public DialogueEffectData[] effects = Array.Empty<DialogueEffectData>();
        public string effectFailureNodeId;
        public string[] tagIds = Array.Empty<string>();

        public DialogueChoiceDefinitionData Clone()
        {
            return new DialogueChoiceDefinitionData
            {
                choiceId = N(choiceId),
                displayText = displayText ?? string.Empty,
                category = category == DialogueChoiceCategory.Unknown ? DialogueChoiceCategory.Response : category,
                visibility = visibility == ConversationVisibility.Unknown ? ConversationVisibility.Public : visibility,
                actorRole = actorRole == ConversationParticipantRole.Unknown ? ConversationParticipantRole.Initiator : actorRole,
                repeatPolicy = repeatPolicy == DialogueChoiceRepeatPolicy.Unknown ? DialogueChoiceRepeatPolicy.Repeatable : repeatPolicy,
                targetNodeId = N(targetNodeId),
                unavailableReason = unavailableReason ?? string.Empty,
                hideUnavailableReason = hideUnavailableReason,
                conditions = (conditions ?? Array.Empty<DialogueConditionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                effects = (effects ?? Array.Empty<DialogueEffectData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                effectFailureNodeId = N(effectFailureNodeId),
                tagIds = Clean(tagIds)
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
        private static string[] Clean(IEnumerable<string> values) => DialogueFlowModelUtility.Clean(values);
    }

    [Serializable]
    public sealed class DialogueNodeDefinitionData
    {
        public string nodeId;
        public DialogueNodeCategory category = DialogueNodeCategory.Line;
        public DialogueSpeakerSelectorData speaker = new DialogueSpeakerSelectorData();
        public DialogueListenerSelectorData listener = new DialogueListenerSelectorData();
        public string authoredText;
        public string localizationKey;
        public ConversationVisibility visibility = ConversationVisibility.Public;
        public DialogueConditionData[] entryConditions = Array.Empty<DialogueConditionData>();
        public DialogueChoiceDefinitionData[] choices = Array.Empty<DialogueChoiceDefinitionData>();
        public DialogueTransitionDefinitionData[] transitions = Array.Empty<DialogueTransitionDefinitionData>();
        public DialogueEffectData[] entryEffects = Array.Empty<DialogueEffectData>();
        public bool canCancel = true;
        public bool canSuspend = true;
        public string[] tagIds = Array.Empty<string>();

        public DialogueNodeDefinitionData Clone()
        {
            return new DialogueNodeDefinitionData
            {
                nodeId = N(nodeId),
                category = category == DialogueNodeCategory.Unknown ? DialogueNodeCategory.Line : category,
                speaker = speaker?.Clone() ?? new DialogueSpeakerSelectorData(),
                listener = listener?.Clone() ?? new DialogueListenerSelectorData(),
                authoredText = authoredText ?? string.Empty,
                localizationKey = N(localizationKey),
                visibility = visibility == ConversationVisibility.Unknown ? ConversationVisibility.Public : visibility,
                entryConditions = (entryConditions ?? Array.Empty<DialogueConditionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                choices = (choices ?? Array.Empty<DialogueChoiceDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.choiceId, StringComparer.Ordinal).ToArray(),
                transitions = (transitions ?? Array.Empty<DialogueTransitionDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.priority).ThenBy(value => value.transitionId, StringComparer.Ordinal).ToArray(),
                entryEffects = (entryEffects ?? Array.Empty<DialogueEffectData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                canCancel = canCancel,
                canSuspend = canSuspend,
                tagIds = Clean(tagIds)
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
        private static string[] Clean(IEnumerable<string> values) => DialogueFlowModelUtility.Clean(values);
    }

    [Serializable]
    public sealed class DialogueGraphDefinitionData
    {
        public string graphId;
        public string displayName;
        public string conversationDefinitionId;
        public string canonicalEntryNodeId;
        public string fallbackNodeId;
        public int automaticTransitionLimit = 8;
        public DialogueNodeDefinitionData[] nodes = Array.Empty<DialogueNodeDefinitionData>();
        public DialogueConditionData[] entryConditions = Array.Empty<DialogueConditionData>();
        public string[] tagIds = Array.Empty<string>();

        public DialogueGraphDefinitionData Clone()
        {
            return new DialogueGraphDefinitionData
            {
                graphId = N(graphId),
                displayName = displayName ?? string.Empty,
                conversationDefinitionId = N(conversationDefinitionId),
                canonicalEntryNodeId = N(canonicalEntryNodeId),
                fallbackNodeId = N(fallbackNodeId),
                automaticTransitionLimit = Math.Max(1, automaticTransitionLimit),
                nodes = (nodes ?? Array.Empty<DialogueNodeDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.nodeId, StringComparer.Ordinal).ToArray(),
                entryConditions = (entryConditions ?? Array.Empty<DialogueConditionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                tagIds = Clean(tagIds)
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
        private static string[] Clean(IEnumerable<string> values) => DialogueFlowModelUtility.Clean(values);
    }

    [Serializable]
    public sealed class DialogueLocalVariableData
    {
        public string variableId;
        public bool boolValue;
        public int intValue;
        public string tokenValue;

        public DialogueLocalVariableData Clone()
        {
            return new DialogueLocalVariableData
            {
                variableId = N(variableId),
                boolValue = boolValue,
                intValue = intValue,
                tokenValue = tokenValue ?? string.Empty
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
    }

    [Serializable]
    public sealed class DialogueNodeVisitRecordData
    {
        public string visitId;
        public string conversationId;
        public string graphId;
        public string nodeId;
        public string speakerPersonId;
        public string[] listenerPersonIds = Array.Empty<string>();
        public double enteredWorldTime;
        public double exitedWorldTime = -1d;
        public string selectedChoiceId;
        public string transitionId;
        public ConversationVisibility visibility = ConversationVisibility.Public;
        public long sequence;

        public DialogueNodeVisitRecordData Clone()
        {
            return new DialogueNodeVisitRecordData
            {
                visitId = N(visitId),
                conversationId = N(conversationId),
                graphId = N(graphId),
                nodeId = N(nodeId),
                speakerPersonId = N(speakerPersonId),
                listenerPersonIds = Clean(listenerPersonIds),
                enteredWorldTime = enteredWorldTime,
                exitedWorldTime = exitedWorldTime,
                selectedChoiceId = N(selectedChoiceId),
                transitionId = N(transitionId),
                visibility = visibility == ConversationVisibility.Unknown ? ConversationVisibility.Public : visibility,
                sequence = sequence
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
        private static string[] Clean(IEnumerable<string> values) => DialogueFlowModelUtility.Clean(values);
    }

    [Serializable]
    public sealed class DialogueChoiceSelectionRecordData
    {
        public string selectionId;
        public string transactionId;
        public string conversationId;
        public string graphId;
        public string nodeId;
        public string choiceId;
        public string actorPersonId;
        public string targetNodeId;
        public double worldTime;
        public bool preview;
        public bool duplicate;
        public string[] effectResultIds = Array.Empty<string>();
        public long runtimeRevision;

        public DialogueChoiceSelectionRecordData Clone()
        {
            return new DialogueChoiceSelectionRecordData
            {
                selectionId = N(selectionId),
                transactionId = N(transactionId),
                conversationId = N(conversationId),
                graphId = N(graphId),
                nodeId = N(nodeId),
                choiceId = N(choiceId),
                actorPersonId = N(actorPersonId),
                targetNodeId = N(targetNodeId),
                worldTime = worldTime,
                preview = preview,
                duplicate = duplicate,
                effectResultIds = Clean(effectResultIds),
                runtimeRevision = runtimeRevision
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
        private static string[] Clean(IEnumerable<string> values) => DialogueFlowModelUtility.Clean(values);
    }

    [Serializable]
    public sealed class DialogueFlowRecordData
    {
        public string flowId;
        public string conversationId;
        public string graphId;
        public string worldId;
        public DialogueFlowState state = DialogueFlowState.NotStarted;
        public string currentNodeId;
        public string currentVisitId;
        public double nodeEnteredWorldTime;
        public long nodeSequence;
        public DialogueLocalVariableData[] localVariables = Array.Empty<DialogueLocalVariableData>();
        public string[] usedChoiceKeys = Array.Empty<string>();
        public DialogueNodeVisitRecordData[] visits = Array.Empty<DialogueNodeVisitRecordData>();
        public DialogueChoiceSelectionRecordData[] selections = Array.Empty<DialogueChoiceSelectionRecordData>();
        public long revision = 1L;

        public DialogueFlowRecordData Clone()
        {
            return new DialogueFlowRecordData
            {
                flowId = N(flowId),
                conversationId = N(conversationId),
                graphId = N(graphId),
                worldId = N(worldId),
                state = state,
                currentNodeId = N(currentNodeId),
                currentVisitId = N(currentVisitId),
                nodeEnteredWorldTime = nodeEnteredWorldTime,
                nodeSequence = nodeSequence,
                localVariables = (localVariables ?? Array.Empty<DialogueLocalVariableData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.variableId, StringComparer.Ordinal).ToArray(),
                usedChoiceKeys = Clean(usedChoiceKeys),
                visits = (visits ?? Array.Empty<DialogueNodeVisitRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.sequence).ThenBy(value => value.visitId, StringComparer.Ordinal).ToArray(),
                selections = (selections ?? Array.Empty<DialogueChoiceSelectionRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.selectionId, StringComparer.Ordinal).ToArray(),
                revision = revision
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
        private static string[] Clean(IEnumerable<string> values) => DialogueFlowModelUtility.Clean(values);
    }

    [Serializable]
    public sealed class DialogueFlowEventData
    {
        public string eventId;
        public string transactionId;
        public DialogueFlowEventKind eventKind = DialogueFlowEventKind.NodeEntered;
        public string flowId;
        public string conversationId;
        public string nodeId;
        public string choiceId;
        public string actorPersonId;
        public double worldTime;
        public long runtimeRevision;

        public DialogueFlowEventData Clone()
        {
            return new DialogueFlowEventData
            {
                eventId = N(eventId),
                transactionId = N(transactionId),
                eventKind = eventKind,
                flowId = N(flowId),
                conversationId = N(conversationId),
                nodeId = N(nodeId),
                choiceId = N(choiceId),
                actorPersonId = N(actorPersonId),
                worldTime = worldTime,
                runtimeRevision = runtimeRevision
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
    }

    [Serializable]
    public sealed class DialogueFlowTransactionData
    {
        public string transactionId;
        public string operation;
        public string flowId;
        public string nodeId;
        public string choiceId;
        public DialogueFlowOperationStatus status = DialogueFlowOperationStatus.Succeeded;
        public long runtimeRevision;

        public DialogueFlowTransactionData Clone()
        {
            return new DialogueFlowTransactionData
            {
                transactionId = N(transactionId),
                operation = N(operation),
                flowId = N(flowId),
                nodeId = N(nodeId),
                choiceId = N(choiceId),
                status = status,
                runtimeRevision = runtimeRevision
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
    }

    [Serializable]
    public sealed class DialogueFlowRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<DialogueFlowRecordData> flows = new List<DialogueFlowRecordData>();
        public List<DialogueFlowEventData> events = new List<DialogueFlowEventData>();
        public List<DialogueFlowTransactionData> transactions = new List<DialogueFlowTransactionData>();

        public DialogueFlowRuntimeSaveData Clone()
        {
            return new DialogueFlowRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = N(worldId),
                revision = revision,
                flows = (flows ?? new List<DialogueFlowRecordData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.flowId, StringComparer.Ordinal).ToList(),
                events = (events ?? new List<DialogueFlowEventData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.eventId, StringComparer.Ordinal).ToList(),
                transactions = (transactions ?? new List<DialogueFlowTransactionData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.transactionId, StringComparer.Ordinal).ToList()
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
    }

    public sealed class DialogueConditionContext
    {
        public string actorPersonId;
        public string speakerPersonId;
        public string listenerPersonId;
        public string locationId;
        public string interactionPointId;
        public double worldTime;
        public QuestEligibilityFactSet facts = QuestEligibilityFactSet.Empty;
        public IEnumerable<string> activeQuestIds;
        public IEnumerable<string> activeOfferIds;
        public IEnumerable<string> activeAssignmentQuestIds;
        public IEnumerable<string> completedQuestIds;
        public IEnumerable<string> claimableRewardIds;
        public string partyId;
        public string partyLeaderPersonId;
        public IEnumerable<string> partyMemberPersonIds;
        public IEnumerable<string> readyPartyMemberPersonIds;
        public ConversationAccessLevel access = ConversationAccessLevel.Public;
        public bool privilegedDiagnostics;

        public DialogueConditionContext Clone()
        {
            return new DialogueConditionContext
            {
                actorPersonId = N(actorPersonId),
                speakerPersonId = N(speakerPersonId),
                listenerPersonId = N(listenerPersonId),
                locationId = N(locationId),
                interactionPointId = N(interactionPointId),
                worldTime = worldTime,
                facts = facts ?? QuestEligibilityFactSet.Empty,
                activeQuestIds = Clean(activeQuestIds),
                activeOfferIds = Clean(activeOfferIds),
                activeAssignmentQuestIds = Clean(activeAssignmentQuestIds),
                completedQuestIds = Clean(completedQuestIds),
                claimableRewardIds = Clean(claimableRewardIds),
                partyId = N(partyId),
                partyLeaderPersonId = N(partyLeaderPersonId),
                partyMemberPersonIds = Clean(partyMemberPersonIds),
                readyPartyMemberPersonIds = Clean(readyPartyMemberPersonIds),
                access = access,
                privilegedDiagnostics = privilegedDiagnostics
            };
        }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
        private static string[] Clean(IEnumerable<string> values) => DialogueFlowModelUtility.Clean(values);
    }

    public sealed class DialogueFlowStartRequest
    {
        public string transactionId;
        public string flowId;
        public string conversationId;
        public string graphId;
        public DialogueConditionContext conditionContext;
        public double worldTime;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public sealed class DialogueChoiceSelectionRequest
    {
        public string transactionId;
        public string flowId;
        public string choiceId;
        public string actorPersonId;
        public DialogueConditionContext conditionContext;
        public double worldTime;
        public bool preview;
        public long expectedRevision = -1L;
    }

    public sealed class DialogueFlowLifecycleRequest
    {
        public string transactionId;
        public string flowId;
        public DialogueFlowState targetState;
        public double worldTime;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class DialogueChoiceEvaluationResult
    {
        public DialogueChoiceEvaluationResult(string choiceId, DialogueChoiceAvailabilityState state, bool visible, bool selectable, IEnumerable<string> visibleReasons, int hiddenReasonCount)
        {
            ChoiceId = N(choiceId);
            State = state;
            Visible = visible;
            Selectable = selectable;
            VisibleFailureReasons = Clean(visibleReasons);
            HiddenFailureCount = hiddenReasonCount;
        }

        public string ChoiceId { get; }
        public DialogueChoiceAvailabilityState State { get; }
        public bool Visible { get; }
        public bool Selectable { get; }
        public IReadOnlyList<string> VisibleFailureReasons { get; }
        public int HiddenFailureCount { get; }

        private static string N(string value) => DialogueFlowModelUtility.N(value);
        private static string[] Clean(IEnumerable<string> values) => DialogueFlowModelUtility.Clean(values);
    }

    public sealed class DialogueChoiceSnapshot
    {
        public DialogueChoiceSnapshot(DialogueChoiceDefinitionData data, DialogueChoiceEvaluationResult evaluation)
        {
            ChoiceId = data?.choiceId ?? string.Empty;
            DisplayText = data?.displayText ?? string.Empty;
            Category = data?.category ?? DialogueChoiceCategory.Unknown;
            TargetNodeId = data?.targetNodeId ?? string.Empty;
            Evaluation = evaluation ?? new DialogueChoiceEvaluationResult(ChoiceId, DialogueChoiceAvailabilityState.Invalid, false, false, new[] { "choice.invalid" }, 0);
        }

        public string ChoiceId { get; }
        public string DisplayText { get; }
        public DialogueChoiceCategory Category { get; }
        public string TargetNodeId { get; }
        public DialogueChoiceEvaluationResult Evaluation { get; }
    }

    public sealed class DialogueFlowSnapshot
    {
        private readonly DialogueFlowRecordData data;
        private readonly DialogueNodeDefinitionData node;
        private readonly DialogueChoiceSnapshot[] choices;

        public DialogueFlowSnapshot(DialogueFlowRecordData record, DialogueNodeDefinitionData currentNode, IEnumerable<DialogueChoiceSnapshot> visibleChoices)
        {
            data = record?.Clone() ?? new DialogueFlowRecordData();
            node = currentNode?.Clone();
            choices = (visibleChoices ?? Array.Empty<DialogueChoiceSnapshot>()).Where(value => value != null).ToArray();
        }

        public string FlowId => data.flowId ?? string.Empty;
        public string ConversationId => data.conversationId ?? string.Empty;
        public string GraphId => data.graphId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public DialogueFlowState State => data.state;
        public string CurrentNodeId => data.currentNodeId ?? string.Empty;
        public string CurrentVisitId => data.currentVisitId ?? string.Empty;
        public long NodeSequence => data.nodeSequence;
        public string AuthoredText => node?.authoredText ?? string.Empty;
        public DialogueNodeCategory NodeCategory => node?.category ?? DialogueNodeCategory.Unknown;
        public IReadOnlyList<DialogueChoiceSnapshot> VisibleChoices => choices.ToArray();
        public IReadOnlyList<DialogueLocalVariableData> LocalVariables => (data.localVariables ?? Array.Empty<DialogueLocalVariableData>()).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<DialogueNodeVisitRecordData> Visits => (data.visits ?? Array.Empty<DialogueNodeVisitRecordData>()).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<DialogueChoiceSelectionRecordData> Selections => (data.selections ?? Array.Empty<DialogueChoiceSelectionRecordData>()).Select(value => value.Clone()).ToArray();
        public long Revision => data.revision;
        public DialogueFlowRecordData ToSaveData() => data.Clone();
    }

    public sealed class DialogueFlowOperationResult
    {
        private DialogueFlowOperationResult(DialogueFlowOperationStatus status, string message, DialogueFlowSnapshot snapshot, DialogueChoiceSelectionRecordData selection, bool preview, bool duplicate, long before, long after)
        {
            Status = status;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
            Selection = selection?.Clone();
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public DialogueFlowOperationStatus Status { get; }
        public string Message { get; }
        public DialogueFlowSnapshot Snapshot { get; }
        public DialogueChoiceSelectionRecordData Selection { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Succeeded => Status == DialogueFlowOperationStatus.Succeeded || Status == DialogueFlowOperationStatus.Preview || Status == DialogueFlowOperationStatus.Duplicate;

        public static DialogueFlowOperationResult Success(string message, long before, long after, DialogueFlowSnapshot snapshot = null, DialogueChoiceSelectionRecordData selection = null, bool preview = false, bool duplicate = false)
        {
            return new DialogueFlowOperationResult(preview ? DialogueFlowOperationStatus.Preview : duplicate ? DialogueFlowOperationStatus.Duplicate : DialogueFlowOperationStatus.Succeeded, message, snapshot, selection, preview, duplicate, before, after);
        }

        public static DialogueFlowOperationResult Failure(DialogueFlowOperationStatus status, string message, long revision, DialogueFlowSnapshot snapshot = null)
        {
            return new DialogueFlowOperationResult(status, message, snapshot, null, false, false, revision, revision);
        }
    }

    public sealed class DialogueFlowValidationReport
    {
        public DialogueFlowValidationReport(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = Clean(errors);
            Warnings = Clean(warnings);
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Errors.Count == 0;
        public string Summary => $"Dialogue flow validation finished with {Errors.Count} error(s), {Warnings.Count} warning(s).";

        private static string[] Clean(IEnumerable<string> values) => DialogueFlowModelUtility.Clean(values);
    }

    public interface IDialogueEffectExecutor
    {
        DialogueEffectExecutionResult Execute(DialogueEffectExecutionRequest request);
    }

    public sealed class DialogueEffectExecutionRequest
    {
        public string flowId;
        public string conversationId;
        public string nodeId;
        public string choiceId;
        public string actorPersonId;
        public DialogueEffectData effect;
        public DialogueConditionContext conditionContext;
        public double worldTime;
        public bool preview;
    }

    public sealed class DialogueEffectExecutionResult
    {
        private DialogueEffectExecutionResult(bool succeeded, bool duplicate, string ownerRuntimeId, string ownerRecordId, string message)
        {
            Succeeded = succeeded;
            Duplicate = duplicate;
            OwnerRuntimeId = ownerRuntimeId ?? string.Empty;
            OwnerRecordId = ownerRecordId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool Duplicate { get; }
        public string OwnerRuntimeId { get; }
        public string OwnerRecordId { get; }
        public string Message { get; }

        public static DialogueEffectExecutionResult Success(string ownerRuntimeId, string ownerRecordId = "", bool duplicate = false) => new DialogueEffectExecutionResult(true, duplicate, ownerRuntimeId, ownerRecordId, duplicate ? "Dialogue effect was already applied." : "Dialogue effect applied.");
        public static DialogueEffectExecutionResult Failure(string message) => new DialogueEffectExecutionResult(false, false, string.Empty, string.Empty, message);
    }

    public static class DialogueFlowModelUtility
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

        public static bool WorldMatches(string actual, string expected)
        {
            return string.Equals(N(actual), N(string.IsNullOrWhiteSpace(expected) ? PersistenceService.LocalWorldId : expected), StringComparison.Ordinal);
        }

        public static string Sanitize(string value)
        {
            value = N(value).ToLowerInvariant();
            return new string(value.Select(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' ? ch : '-').ToArray()).Trim('-');
        }
    }
}
