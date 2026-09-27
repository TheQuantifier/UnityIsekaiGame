using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Dialogue
{
    public sealed class DialogueFlowRuntime : IDisposable
    {
        private readonly Dictionary<string, DialogueFlowRecordData> flowsById = new Dictionary<string, DialogueFlowRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> flowByConversation = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, DialogueFlowTransactionData> transactionsById = new Dictionary<string, DialogueFlowTransactionData>(StringComparer.Ordinal);
        private readonly List<DialogueFlowEventData> events = new List<DialogueFlowEventData>();
        private readonly Dictionary<string, List<string>> flowsByGraph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> flowsByNode = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private ConversationRuntime conversationRuntime;
        private IDialogueEffectExecutor effectExecutor;
        private Func<DialogueConditionData, DialogueConditionContext, bool> narrativeStateConditionEvaluator;
        private string worldId;
        private long revision;
        private bool disposed;

        public event Action<DialogueFlowEventData> EventCommitted;

        public DialogueFlowRuntime(DefinitionRegistry definitionRegistry = null, ConversationRuntime conversations = null, IDialogueEffectExecutor executor = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            Configure(definitionRegistry, conversations, executor, runtimeWorldId);
        }

        public long Revision => revision;
        public string WorldId => worldId ?? string.Empty;
        public int Count => flowsById.Count;
        public IReadOnlyList<DialogueFlowEventData> Events => events.Select(value => value.Clone()).ToArray();
        public Func<DialogueConditionData, DialogueConditionContext, bool> NarrativeStateConditionEvaluator
        {
            get => narrativeStateConditionEvaluator;
            set => narrativeStateConditionEvaluator = value;
        }

        public void Configure(DefinitionRegistry definitionRegistry, ConversationRuntime conversations, IDialogueEffectExecutor executor = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            registry = definitionRegistry;
            conversationRuntime = conversations;
            effectExecutor = executor;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId.Trim();
        }

        public DialogueFlowOperationResult StartFlow(DialogueFlowStartRequest request)
        {
            if (disposed) return Fail(DialogueFlowOperationStatus.Disposed, "Dialogue flow runtime is disposed.");
            request ??= new DialogueFlowStartRequest();
            if (!ValidateRevision(request.expectedRevision, out DialogueFlowOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out DialogueFlowOperationResult duplicate)) return duplicate;
            if (registry == null) return Fail(DialogueFlowOperationStatus.MissingDefinitionRegistry, "Dialogue flow runtime has no definition registry.");
            if (conversationRuntime == null) return Fail(DialogueFlowOperationStatus.MissingConversationRuntime, "Dialogue flow runtime has no ConversationRuntime.");
            if (!conversationRuntime.TryGetSnapshot(request.conversationId, out ConversationSnapshot conversation)) return Fail(DialogueFlowOperationStatus.MissingConversation, $"Conversation '{N(request.conversationId)}' is missing.");
            if (!string.Equals(conversation.WorldId, worldId, StringComparison.Ordinal)) return Fail(DialogueFlowOperationStatus.WrongWorld, $"Conversation world '{conversation.WorldId}' does not match dialogue flow world '{worldId}'.");

            string graphId = N(request.graphId);
            if (string.IsNullOrWhiteSpace(graphId)) graphId = GraphForConversation(conversation.ConversationDefinitionId);
            if (string.IsNullOrWhiteSpace(graphId) || !registry.TryGet(graphId, out DialogueGraphDefinition graphDefinition)) return Fail(DialogueFlowOperationStatus.MissingGraph, $"Dialogue graph '{graphId}' is missing.");
            DialogueGraphDefinitionData graph = graphDefinition.ToRecordData();
            DialogueFlowValidationReport graphReport = DialogueGraphValidator.Validate(graph);
            if (!graphReport.Succeeded) return Fail(DialogueFlowOperationStatus.GraphInvalid, string.Join(" | ", graphReport.Errors));
            if (!string.Equals(graph.conversationDefinitionId, conversation.ConversationDefinitionId, StringComparison.Ordinal)) return Fail(DialogueFlowOperationStatus.GraphInvalid, $"Dialogue graph '{graph.graphId}' is not authored for Conversation definition '{conversation.ConversationDefinitionId}'.");

            string flowId = string.IsNullOrWhiteSpace(request.flowId) ? $"dialogue-flow.{conversation.ConversationId}" : N(request.flowId);
            if (flowsById.ContainsKey(flowId) || flowByConversation.ContainsKey(conversation.ConversationId)) return Fail(DialogueFlowOperationStatus.InvalidRequest, $"Conversation '{conversation.ConversationId}' already has a Dialogue flow.");
            if (!EntryConditionsSatisfied(graph.entryConditions, request.conditionContext, null, out DialogueFlowOperationResult entryFailure)) return entryFailure;

            DialogueFlowRecordData flow = new DialogueFlowRecordData
            {
                flowId = flowId,
                conversationId = conversation.ConversationId,
                graphId = graph.graphId,
                worldId = worldId,
                state = DialogueFlowState.NotStarted,
                revision = 1L
            };

            DialogueFlowOperationResult entered = EnterNode(flow, graph, graph.canonicalEntryNodeId, request.conditionContext, request.worldTime, transactionId, preview: request.preview);
            if (!entered.Succeeded) return entered;
            if (request.preview) return DialogueFlowOperationResult.Success("Dialogue flow previewed.", revision, revision, entered.Snapshot, preview: true);

            long before = revision;
            DialogueFlowRecordData committed = entered.Snapshot.ToSaveData();
            flowsById[committed.flowId] = committed.Clone();
            flowByConversation[committed.conversationId] = committed.flowId;
            revision++;
            RecordTransaction(transactionId, "StartFlow", committed.flowId, committed.currentNodeId, string.Empty, DialogueFlowOperationStatus.Succeeded);
            Emit(transactionId, DialogueFlowEventKind.FlowStarted, committed.flowId, committed.conversationId, committed.currentNodeId, string.Empty, string.Empty, request.worldTime);
            RebuildIndexes();
            return DialogueFlowOperationResult.Success("Dialogue flow started.", before, revision, Snapshot(committed, graph, request.conditionContext));
        }

        public DialogueFlowOperationResult SelectChoice(DialogueChoiceSelectionRequest request)
        {
            if (disposed) return Fail(DialogueFlowOperationStatus.Disposed, "Dialogue flow runtime is disposed.");
            request ??= new DialogueChoiceSelectionRequest();
            if (!ValidateRevision(request.expectedRevision, out DialogueFlowOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out DialogueFlowOperationResult duplicate)) return duplicate;
            if (!TryResolveFlow(request.flowId, out DialogueFlowRecordData flow, out DialogueGraphDefinitionData graph, out DialogueNodeDefinitionData node, out DialogueFlowOperationResult failure)) return failure;
            if (flow.state != DialogueFlowState.AwaitingChoice) return Fail(DialogueFlowOperationStatus.InvalidRequest, "Dialogue flow is not awaiting a choice.", Snapshot(flow, graph, request.conditionContext));

            DialogueChoiceDefinitionData choice = (node.choices ?? Array.Empty<DialogueChoiceDefinitionData>()).FirstOrDefault(value => string.Equals(value.choiceId, N(request.choiceId), StringComparison.Ordinal));
            if (choice == null) return Fail(DialogueFlowOperationStatus.MissingChoice, $"Dialogue choice '{N(request.choiceId)}' is missing.", Snapshot(flow, graph, request.conditionContext));

            DialogueChoiceEvaluationResult evaluation = EvaluateChoice(flow, choice, request.conditionContext, request.actorPersonId);
            if (evaluation.State == DialogueChoiceAvailabilityState.Hidden) return Fail(DialogueFlowOperationStatus.ChoiceHidden, "Dialogue choice is hidden.", Snapshot(flow, graph, request.conditionContext));
            if (evaluation.State == DialogueChoiceAvailabilityState.AlreadyUsed) return Fail(DialogueFlowOperationStatus.ChoiceAlreadyUsed, "Dialogue choice was already used.", Snapshot(flow, graph, request.conditionContext));
            if (!evaluation.Selectable) return Fail(DialogueFlowOperationStatus.ChoiceUnavailable, "Dialogue choice is unavailable.", Snapshot(flow, graph, request.conditionContext));

            DialogueFlowRecordData changed = flow.Clone();
            string selectionId = BuildSelectionId(changed.flowId, choice.choiceId, changed.selections.Length + 1);
            List<string> effectResults = new List<string>();
            string targetNodeId = choice.targetNodeId;
            foreach (DialogueEffectData effect in choice.effects ?? Array.Empty<DialogueEffectData>())
            {
                DialogueEffectExecutionResult effectResult = ApplyEffect(changed, node.nodeId, choice.choiceId, effect, request.conditionContext, request.actorPersonId, request.worldTime, request.preview);
                if (effectResult.Succeeded)
                {
                    if (!string.IsNullOrWhiteSpace(effectResult.OwnerRecordId)) effectResults.Add(effectResult.OwnerRecordId);
                    if (!string.IsNullOrWhiteSpace(effect.successNodeId)) targetNodeId = effect.successNodeId;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(effect.failureNodeId)) targetNodeId = effect.failureNodeId;
                else if (!string.IsNullOrWhiteSpace(choice.effectFailureNodeId)) targetNodeId = choice.effectFailureNodeId;
                if (effect.requirement == DialogueEffectRequirement.Required && string.IsNullOrWhiteSpace(targetNodeId))
                {
                    return Fail(DialogueFlowOperationStatus.EffectFailed, effectResult.Message, Snapshot(flow, graph, request.conditionContext));
                }
            }

            DialogueChoiceSelectionRecordData selection = new DialogueChoiceSelectionRecordData
            {
                selectionId = selectionId,
                transactionId = transactionId,
                conversationId = changed.conversationId,
                graphId = changed.graphId,
                nodeId = node.nodeId,
                choiceId = choice.choiceId,
                actorPersonId = N(request.actorPersonId),
                targetNodeId = N(targetNodeId),
                worldTime = request.worldTime,
                preview = request.preview,
                effectResultIds = effectResults.ToArray(),
                runtimeRevision = revision
            };

            MarkChoiceUsed(changed, choice, request.actorPersonId);
            ExitCurrentVisit(changed, request.worldTime, choice.choiceId, string.Empty);
            changed.selections = changed.selections.Concat(new[] { selection.Clone() }).ToArray();

            DialogueFlowOperationResult entered;
            if (choice.category == DialogueChoiceCategory.EndConversation || string.IsNullOrWhiteSpace(targetNodeId))
            {
                changed.state = DialogueFlowState.Ended;
                changed.currentNodeId = string.Empty;
                changed.currentVisitId = string.Empty;
                entered = DialogueFlowOperationResult.Success("Dialogue flow ended.", revision, revision, Snapshot(changed, graph, request.conditionContext), selection, preview: request.preview);
            }
            else
            {
                entered = EnterNode(changed, graph, targetNodeId, request.conditionContext, request.worldTime, transactionId, request.preview);
            }

            if (!entered.Succeeded) return entered;
            if (request.preview) return DialogueFlowOperationResult.Success("Dialogue choice previewed.", revision, revision, entered.Snapshot, selection, preview: true);

            long before = revision;
            DialogueFlowRecordData committed = entered.Snapshot.ToSaveData();
            flowsById[committed.flowId] = committed.Clone();
            revision++;
            RecordTransaction(transactionId, "SelectChoice", committed.flowId, node.nodeId, choice.choiceId, DialogueFlowOperationStatus.Succeeded);
            Emit(transactionId, DialogueFlowEventKind.ChoiceSelected, committed.flowId, committed.conversationId, node.nodeId, choice.choiceId, request.actorPersonId, request.worldTime);
            if (committed.state == DialogueFlowState.Ended) Emit(transactionId, DialogueFlowEventKind.FlowEnded, committed.flowId, committed.conversationId, node.nodeId, choice.choiceId, request.actorPersonId, request.worldTime);
            RebuildIndexes();
            return DialogueFlowOperationResult.Success("Dialogue choice selected.", before, revision, Snapshot(committed, graph, request.conditionContext), selection);
        }

        public DialogueFlowOperationResult TransitionLifecycle(DialogueFlowLifecycleRequest request)
        {
            if (disposed) return Fail(DialogueFlowOperationStatus.Disposed, "Dialogue flow runtime is disposed.");
            request ??= new DialogueFlowLifecycleRequest();
            if (!ValidateRevision(request.expectedRevision, out DialogueFlowOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out DialogueFlowOperationResult duplicate)) return duplicate;
            if (!flowsById.TryGetValue(N(request.flowId), out DialogueFlowRecordData flow)) return Fail(DialogueFlowOperationStatus.InvalidRequest, $"Dialogue flow '{N(request.flowId)}' is missing.");
            if (request.targetState != DialogueFlowState.Suspended && request.targetState != DialogueFlowState.Ended && request.targetState != DialogueFlowState.Invalid) return Fail(DialogueFlowOperationStatus.InvalidRequest, "Dialogue flow lifecycle supports Suspended, Ended, or Invalid transitions.");

            DialogueFlowRecordData changed = flow.Clone();
            changed.state = request.targetState;
            changed.revision++;
            if (request.preview) return DialogueFlowOperationResult.Success("Dialogue flow lifecycle previewed.", revision, revision, Snapshot(changed, null, null), preview: true);

            long before = revision;
            flowsById[changed.flowId] = changed;
            revision++;
            RecordTransaction(transactionId, "TransitionLifecycle", changed.flowId, changed.currentNodeId, string.Empty, DialogueFlowOperationStatus.Succeeded);
            Emit(transactionId, request.targetState == DialogueFlowState.Suspended ? DialogueFlowEventKind.FlowSuspended : DialogueFlowEventKind.FlowEnded, changed.flowId, changed.conversationId, changed.currentNodeId, string.Empty, string.Empty, request.worldTime);
            RebuildIndexes();
            return DialogueFlowOperationResult.Success("Dialogue flow lifecycle changed.", before, revision, Snapshot(changed, null, null));
        }

        public bool TryGetSnapshot(string flowId, DialogueConditionContext context, out DialogueFlowSnapshot snapshot)
        {
            snapshot = null;
            if (!flowsById.TryGetValue(N(flowId), out DialogueFlowRecordData flow)) return false;
            TryResolveGraph(flow.graphId, out DialogueGraphDefinitionData graph);
            snapshot = Snapshot(flow, graph, context);
            return true;
        }

        public IReadOnlyList<DialogueFlowSnapshot> Query(string conversationId = null, string graphId = null, DialogueFlowState? state = null, DialogueConditionContext context = null)
        {
            IEnumerable<DialogueFlowRecordData> flows = flowsById.Values;
            if (!string.IsNullOrWhiteSpace(conversationId)) flows = flows.Where(value => string.Equals(value.conversationId, N(conversationId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(graphId)) flows = flows.Where(value => string.Equals(value.graphId, N(graphId), StringComparison.Ordinal));
            if (state.HasValue) flows = flows.Where(value => value.state == state.Value);
            return flows.OrderBy(value => value.flowId, StringComparer.Ordinal).Select(value =>
            {
                TryResolveGraph(value.graphId, out DialogueGraphDefinitionData graph);
                return Snapshot(value, graph, context);
            }).ToArray();
        }

        public DialogueFlowRuntimeSaveData CreateSaveData()
        {
            return new DialogueFlowRuntimeSaveData
            {
                schemaVersion = DialogueFlowRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = revision,
                flows = flowsById.Values.Select(value => value.Clone()).OrderBy(value => value.flowId, StringComparer.Ordinal).ToList(),
                events = events.Select(value => value.Clone()).OrderBy(value => value.eventId, StringComparer.Ordinal).ToList(),
                transactions = transactionsById.Values.Select(value => value.Clone()).OrderBy(value => value.transactionId, StringComparer.Ordinal).ToList()
            };
        }

        public DialogueFlowOperationResult RestoreFromSaveData(DialogueFlowRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, ConversationRuntime conversations, IDialogueEffectExecutor executor = null, string expectedWorldId = PersistenceService.LocalWorldId)
        {
            if (disposed) return Fail(DialogueFlowOperationStatus.Disposed, "Dialogue flow runtime is disposed.");
            if (!ValidateSaveData(saveData, definitionRegistry, conversations, expectedWorldId, out string failure)) return Fail(DialogueFlowOperationStatus.RestoreFailed, failure);

            flowsById.Clear();
            flowByConversation.Clear();
            transactionsById.Clear();
            events.Clear();
            registry = definitionRegistry;
            conversationRuntime = conversations;
            effectExecutor = executor;
            worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            revision = saveData.revision;
            foreach (DialogueFlowRecordData flow in saveData.flows ?? new List<DialogueFlowRecordData>()) flowsById[flow.flowId] = flow.Clone();
            foreach (DialogueFlowRecordData flow in flowsById.Values) flowByConversation[flow.conversationId] = flow.flowId;
            foreach (DialogueFlowTransactionData transaction in saveData.transactions ?? new List<DialogueFlowTransactionData>()) transactionsById[transaction.transactionId] = transaction.Clone();
            events.AddRange((saveData.events ?? new List<DialogueFlowEventData>()).Where(value => value != null).Select(value => value.Clone()));
            RebuildIndexes();
            return DialogueFlowOperationResult.Success("Dialogue flows restored.", revision, revision);
        }

        public DialogueFlowValidationReport ValidateRuntime()
        {
            ValidateSaveData(CreateSaveData(), registry, conversationRuntime, worldId, out _, out DialogueFlowValidationReport report);
            return report;
        }

        public static bool ValidateSaveData(DialogueFlowRuntimeSaveData saveData, DefinitionRegistry registry, ConversationRuntime conversations, string expectedWorldId, out string failure)
        {
            return ValidateSaveData(saveData, registry, conversations, expectedWorldId, out failure, out _);
        }

        public static bool ValidateSaveData(DialogueFlowRuntimeSaveData saveData, DefinitionRegistry registry, ConversationRuntime conversations, string expectedWorldId, out string failure, out DialogueFlowValidationReport report)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            if (saveData == null)
            {
                errors.Add("Dialogue flow save data is missing.");
            }
            else
            {
                if (saveData.schemaVersion != DialogueFlowRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported dialogue flow save schema version {saveData.schemaVersion}.");
                if (!DialogueFlowModelUtility.WorldMatches(saveData.worldId, world)) errors.Add($"Dialogue flow save world '{saveData.worldId}' does not match expected world '{world}'.");
                if (registry == null) errors.Add("Dialogue flow save validation requires a DefinitionRegistry.");
                if (conversations == null) errors.Add("Dialogue flow save validation requires a ConversationRuntime.");

                HashSet<string> flowIds = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> conversationIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (DialogueFlowRecordData flow in saveData.flows ?? new List<DialogueFlowRecordData>())
                {
                    if (flow == null) continue;
                    if (string.IsNullOrWhiteSpace(flow.flowId)) errors.Add("Dialogue flow record has no ID.");
                    else if (!flowIds.Add(flow.flowId)) errors.Add($"Duplicate Dialogue flow '{flow.flowId}'.");
                    if (string.IsNullOrWhiteSpace(flow.conversationId)) errors.Add($"Dialogue flow '{flow.flowId}' has no Conversation ID.");
                    else if (!conversationIds.Add(flow.conversationId)) errors.Add($"Conversation '{flow.conversationId}' has multiple Dialogue flows.");
                    else if (conversations != null && !conversations.TryGetSnapshot(flow.conversationId, out _)) errors.Add($"Dialogue flow '{flow.flowId}' references missing Conversation '{flow.conversationId}'.");
                    DialogueGraphDefinition graph = null;
                    if (string.IsNullOrWhiteSpace(flow.graphId)) errors.Add($"Dialogue flow '{flow.flowId}' has no graph ID.");
                    else if (registry != null && !registry.TryGet(flow.graphId, out graph)) errors.Add($"Dialogue flow '{flow.flowId}' references missing graph '{flow.graphId}'.");
                    else if (graph != null)
                    {
                        DialogueGraphDefinitionData graphData = graph.ToRecordData();
                        if (!string.IsNullOrWhiteSpace(flow.currentNodeId) && !graphData.nodes.Any(node => node.nodeId == flow.currentNodeId)) errors.Add($"Dialogue flow '{flow.flowId}' current node '{flow.currentNodeId}' is not in graph '{flow.graphId}'.");
                    }
                    if (!DialogueFlowModelUtility.WorldMatches(flow.worldId, world)) errors.Add($"Dialogue flow '{flow.flowId}' belongs to wrong world '{flow.worldId}'.");
                    if (flow.state == DialogueFlowState.Unknown) errors.Add($"Dialogue flow '{flow.flowId}' has unknown state.");
                }

                foreach (DialogueFlowEventData evt in saveData.events ?? new List<DialogueFlowEventData>())
                {
                    if (evt == null) continue;
                    if (string.IsNullOrWhiteSpace(evt.eventId)) errors.Add("Dialogue flow event has no ID.");
                    if (!string.IsNullOrWhiteSpace(evt.flowId) && !flowIds.Contains(evt.flowId)) warnings.Add($"Dialogue flow event '{evt.eventId}' references missing flow '{evt.flowId}'.");
                }
            }

            report = new DialogueFlowValidationReport(errors, warnings);
            failure = string.Join(" | ", report.Errors);
            return report.Succeeded;
        }

        public void Dispose()
        {
            disposed = true;
            EventCommitted = null;
        }

        private DialogueFlowOperationResult EnterNode(DialogueFlowRecordData flow, DialogueGraphDefinitionData graph, string nodeId, DialogueConditionContext context, double worldTime, string transactionId, bool preview, int automaticDepth = 0)
        {
            DialogueNodeDefinitionData node = Node(graph, nodeId);
            if (node == null) return Fail(DialogueFlowOperationStatus.MissingNode, $"Dialogue node '{N(nodeId)}' is missing.");
            if (!EntryConditionsSatisfied(node.entryConditions, context, flow, out DialogueFlowOperationResult conditionFailure)) return conditionFailure;
            if (!ResolveSpeaker(node.speaker, flow.conversationId, out string speaker, out DialogueFlowOperationResult speakerFailure)) return speakerFailure;
            if (!ResolveListeners(node.listener, flow.conversationId, out string[] listeners, out DialogueFlowOperationResult listenerFailure)) return listenerFailure;

            DialogueFlowRecordData changed = flow.Clone();
            foreach (DialogueEffectData effect in node.entryEffects ?? Array.Empty<DialogueEffectData>())
            {
                string effectKey = $"entry:{node.nodeId}:{effect.effectId}";
                if (effect.oneShot && changed.usedChoiceKeys.Contains(effectKey, StringComparer.Ordinal)) continue;
                DialogueEffectExecutionResult effectResult = ApplyEffect(changed, node.nodeId, string.Empty, effect, context, speaker, worldTime, preview);
                if (!effectResult.Succeeded && effect.requirement == DialogueEffectRequirement.Required) return Fail(DialogueFlowOperationStatus.EffectFailed, effectResult.Message);
                if (effect.oneShot) changed.usedChoiceKeys = changed.usedChoiceKeys.Concat(new[] { effectKey }).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            }

            changed.state = node.category == DialogueNodeCategory.End ? DialogueFlowState.Ended : node.choices.Length > 0 ? DialogueFlowState.AwaitingChoice : DialogueFlowState.AwaitingAdvance;
            changed.currentNodeId = node.nodeId;
            changed.nodeEnteredWorldTime = worldTime;
            changed.nodeSequence++;
            string visitId = $"dialogue-node-visit.{changed.flowId}.{changed.nodeSequence:000000}";
            changed.currentVisitId = visitId;
            changed.visits = changed.visits.Concat(new[]
            {
                new DialogueNodeVisitRecordData
                {
                    visitId = visitId,
                    conversationId = changed.conversationId,
                    graphId = graph.graphId,
                    nodeId = node.nodeId,
                    speakerPersonId = speaker,
                    listenerPersonIds = listeners,
                    enteredWorldTime = worldTime,
                    visibility = node.visibility,
                    sequence = changed.nodeSequence
                }
            }).ToArray();
            changed.revision++;

            DialogueFlowOperationResult automatic = ResolveAutomaticTransitions(changed, graph, context, worldTime, transactionId, preview, automaticDepth);
            return automatic ?? DialogueFlowOperationResult.Success("Dialogue node entered.", revision, revision, Snapshot(changed, graph, context), preview: preview);
        }

        private DialogueFlowOperationResult ResolveAutomaticTransitions(DialogueFlowRecordData flow, DialogueGraphDefinitionData graph, DialogueConditionContext context, double worldTime, string transactionId, bool preview, int depth)
        {
            if (depth >= Math.Max(1, graph.automaticTransitionLimit)) return Fail(DialogueFlowOperationStatus.AutomaticLoopRejected, "Dialogue automatic transition limit was reached.", Snapshot(flow, graph, context));
            DialogueNodeDefinitionData node = Node(graph, flow.currentNodeId);
            if (node == null || node.category == DialogueNodeCategory.End || node.choices.Length > 0) return null;
            DialogueTransitionDefinitionData transition = (node.transitions ?? Array.Empty<DialogueTransitionDefinitionData>())
                .Where(value => value.category == DialogueTransitionCategory.Automatic || value.category == DialogueTransitionCategory.Redirect || value.fallback)
                .OrderBy(value => value.fallback ? 1 : 0)
                .ThenBy(value => value.priority)
                .ThenBy(value => value.transitionId, StringComparer.Ordinal)
                .FirstOrDefault(value => ConditionsSatisfied(value.conditions, context, flow, out _, out _));
            if (transition == null) return null;
            if (string.IsNullOrWhiteSpace(transition.targetNodeId)) return Fail(DialogueFlowOperationStatus.NoValidTransition, "Automatic transition has no target node.", Snapshot(flow, graph, context));
            DialogueFlowRecordData changed = flow.Clone();
            ExitCurrentVisit(changed, worldTime, string.Empty, transition.transitionId);
            return EnterNode(changed, graph, transition.targetNodeId, context, worldTime, transactionId, preview, depth + 1);
        }

        private DialogueFlowSnapshot Snapshot(DialogueFlowRecordData flow, DialogueGraphDefinitionData graph, DialogueConditionContext context)
        {
            graph ??= TryResolveGraph(flow.graphId, out DialogueGraphDefinitionData resolved) ? resolved : null;
            DialogueNodeDefinitionData node = graph == null ? null : Node(graph, flow.currentNodeId);
            DialogueChoiceSnapshot[] choices = node == null
                ? Array.Empty<DialogueChoiceSnapshot>()
                : (node.choices ?? Array.Empty<DialogueChoiceDefinitionData>())
                    .Select(choice => new DialogueChoiceSnapshot(choice, EvaluateChoice(flow, choice, context, context?.actorPersonId)))
                    .Where(choice => choice.Evaluation.Visible)
                    .OrderBy(choice => choice.ChoiceId, StringComparer.Ordinal)
                    .ToArray();
            return new DialogueFlowSnapshot(flow, node, choices);
        }

        private DialogueChoiceEvaluationResult EvaluateChoice(DialogueFlowRecordData flow, DialogueChoiceDefinitionData choice, DialogueConditionContext context, string actorPersonId)
        {
            if (choice == null) return new DialogueChoiceEvaluationResult(string.Empty, DialogueChoiceAvailabilityState.Invalid, false, false, new[] { "choice.invalid" }, 0);
            if (choice.visibility == ConversationVisibility.Hidden || choice.visibility == ConversationVisibility.Secret)
            {
                bool privileged = context != null && context.privilegedDiagnostics;
                bool allowed = privileged || ConditionsSatisfied(choice.conditions, context, flow, out _, out _);
                if (!allowed) return new DialogueChoiceEvaluationResult(choice.choiceId, DialogueChoiceAvailabilityState.Hidden, false, false, Array.Empty<string>(), 1);
            }

            string usedKey = UsedChoiceKey(choice, actorPersonId);
            if (choice.repeatPolicy != DialogueChoiceRepeatPolicy.Repeatable && flow.usedChoiceKeys.Contains(usedKey, StringComparer.Ordinal))
            {
                return new DialogueChoiceEvaluationResult(choice.choiceId, DialogueChoiceAvailabilityState.AlreadyUsed, true, false, new[] { "choice.already-used" }, 0);
            }

            bool conditions = ConditionsSatisfied(choice.conditions, context, flow, out List<string> visible, out int hidden);
            if (!conditions)
            {
                bool visibleReason = !choice.hideUnavailableReason;
                string[] reasons = visibleReason ? visible.Concat(string.IsNullOrWhiteSpace(choice.unavailableReason) ? Array.Empty<string>() : new[] { choice.unavailableReason }).ToArray() : Array.Empty<string>();
                return new DialogueChoiceEvaluationResult(choice.choiceId, choice.visibility == ConversationVisibility.Hidden ? DialogueChoiceAvailabilityState.Hidden : DialogueChoiceAvailabilityState.UnavailableVisible, choice.visibility != ConversationVisibility.Hidden, false, reasons, hidden + (visibleReason ? 0 : 1));
            }

            return new DialogueChoiceEvaluationResult(choice.choiceId, DialogueChoiceAvailabilityState.Available, true, true, Array.Empty<string>(), 0);
        }

        private bool EntryConditionsSatisfied(IEnumerable<DialogueConditionData> conditions, DialogueConditionContext context, DialogueFlowRecordData flow, out DialogueFlowOperationResult failure)
        {
            bool ok = ConditionsSatisfied(conditions, context, flow, out List<string> visible, out int hidden);
            failure = ok ? null : Fail(DialogueFlowOperationStatus.ConditionFailed, visible.Count > 0 ? string.Join(",", visible) : $"Hidden dialogue condition failed ({hidden}).");
            return ok;
        }

        private bool ConditionsSatisfied(IEnumerable<DialogueConditionData> conditions, DialogueConditionContext context, DialogueFlowRecordData flow, out List<string> visibleFailures, out int hiddenFailures)
        {
            visibleFailures = new List<string>();
            hiddenFailures = 0;
            foreach (DialogueConditionData condition in conditions ?? Array.Empty<DialogueConditionData>())
            {
                bool passed = EvaluateCondition(condition, context, flow);
                if (passed) continue;
                if (condition.hidden || !condition.revealFailure) hiddenFailures++;
                else visibleFailures.Add(string.IsNullOrWhiteSpace(condition.conditionId) ? $"condition.{condition.kind}.failed" : condition.conditionId);
            }

            return visibleFailures.Count == 0 && hiddenFailures == 0;
        }

        private bool EvaluateCondition(DialogueConditionData condition, DialogueConditionContext context, DialogueFlowRecordData flow)
        {
            condition ??= new DialogueConditionData();
            context = (context ?? new DialogueConditionContext()).Clone();
            bool result = condition.kind switch
            {
                DialogueConditionKind.Always => true,
                DialogueConditionKind.QuestExists => Contains(context.activeQuestIds, condition.requiredId),
                DialogueConditionKind.QuestOfferActive => Contains(context.activeOfferIds, condition.requiredId),
                DialogueConditionKind.QuestAssignmentActive => Contains(context.activeAssignmentQuestIds, condition.requiredId),
                DialogueConditionKind.QuestObjectiveReady => Contains(context.activeAssignmentQuestIds, condition.requiredId),
                DialogueConditionKind.QuestOutcomeCompleted => Contains(context.completedQuestIds, condition.requiredId),
                DialogueConditionKind.RewardClaimable => Contains(context.claimableRewardIds, condition.requiredId),
                DialogueConditionKind.Knowledge => context.facts.Contains(QuestEligibilityRequirementKind.Knowledge, condition.requiredId),
                DialogueConditionKind.Belief => context.facts.Contains(QuestEligibilityRequirementKind.Knowledge, $"belief:{condition.requiredId}"),
                DialogueConditionKind.OrganizationMembership => context.facts.Contains(QuestEligibilityRequirementKind.OrganizationMembership, condition.requiredId),
                DialogueConditionKind.OrganizationRank => context.facts.Contains(QuestEligibilityRequirementKind.OrganizationRank, condition.requiredId),
                DialogueConditionKind.Office => context.facts.Contains(QuestEligibilityRequirementKind.Office, condition.requiredId),
                DialogueConditionKind.Authority => context.facts.Contains(QuestEligibilityRequirementKind.InstitutionalAuthority, condition.requiredId),
                DialogueConditionKind.Reputation => Compare(context.facts.Value(QuestEligibilityRequirementKind.Reputation, condition.requiredId), condition),
                DialogueConditionKind.Relationship => Compare(context.facts.Value(QuestEligibilityRequirementKind.Relationship, condition.requiredId), condition),
                DialogueConditionKind.ItemPossessed => context.facts.Contains(QuestEligibilityRequirementKind.ItemPossessed, condition.requiredId),
                DialogueConditionKind.ItemEquipped => context.facts.Contains(QuestEligibilityRequirementKind.ItemEquipped, condition.requiredId),
                DialogueConditionKind.Location => string.Equals(N(context.locationId), N(condition.requiredId), StringComparison.Ordinal),
                DialogueConditionKind.InteractionPoint => string.Equals(N(context.interactionPointId), N(condition.requiredId), StringComparison.Ordinal),
                DialogueConditionKind.Permit => context.facts.Contains(QuestEligibilityRequirementKind.Permit, condition.requiredId),
                DialogueConditionKind.LegalStatus => context.facts.Contains(QuestEligibilityRequirementKind.LegalStatus, condition.requiredId),
                DialogueConditionKind.NarrativeState => narrativeStateConditionEvaluator?.Invoke(condition.Clone(), context.Clone()) ?? context.facts.Contains(QuestEligibilityRequirementKind.NarrativeState, condition.requiredId),
                DialogueConditionKind.LocalFlag => LocalFlag(flow, condition.requiredId),
                DialogueConditionKind.LocalCounter => Compare(LocalCounter(flow, condition.requiredId), condition),
                DialogueConditionKind.Custom => context.facts.Contains(QuestEligibilityRequirementKind.Custom, condition.requiredId),
                DialogueConditionKind.PartySize => Compare((context.partyMemberPersonIds ?? Array.Empty<string>()).Count(), condition),
                DialogueConditionKind.PartyHasMember => Contains(context.partyMemberPersonIds, condition.requiredId),
                DialogueConditionKind.PartyLeader => string.Equals(N(context.partyLeaderPersonId), string.IsNullOrWhiteSpace(condition.requiredId) ? N(context.actorPersonId) : N(condition.requiredId), StringComparison.Ordinal),
                DialogueConditionKind.PartyReady => Contains(context.readyPartyMemberPersonIds, condition.requiredId),
                _ => false
            };
            return condition.negate ? !result : result;
        }

        private DialogueEffectExecutionResult ApplyEffect(DialogueFlowRecordData flow, string nodeId, string choiceId, DialogueEffectData effect, DialogueConditionContext context, string actorPersonId, double worldTime, bool preview)
        {
            effect ??= new DialogueEffectData();
            if (effect.kind == DialogueEffectKind.None) return DialogueEffectExecutionResult.Success("dialogue-flow", string.Empty);
            if (effect.kind == DialogueEffectKind.SetLocalFlag)
            {
                if (!preview) SetLocal(flow, effect.targetId, true, 0, effect.value);
                return DialogueEffectExecutionResult.Success("dialogue-flow", effect.targetId);
            }
            if (effect.kind == DialogueEffectKind.IncrementLocalCounter)
            {
                if (!preview) IncrementLocal(flow, effect.targetId, Math.Max(1, effect.quantity));
                return DialogueEffectExecutionResult.Success("dialogue-flow", effect.targetId);
            }

            if (effectExecutor == null) return effect.requirement == DialogueEffectRequirement.Required ? DialogueEffectExecutionResult.Failure($"No Dialogue effect executor is configured for {effect.kind}.") : DialogueEffectExecutionResult.Success("dialogue-flow.unsupported-optional", effect.effectId);
            return effectExecutor.Execute(new DialogueEffectExecutionRequest
            {
                flowId = flow.flowId,
                conversationId = flow.conversationId,
                nodeId = nodeId,
                choiceId = choiceId,
                actorPersonId = N(actorPersonId),
                effect = effect.Clone(),
                conditionContext = context?.Clone(),
                worldTime = worldTime,
                preview = preview
            });
        }

        private bool ResolveSpeaker(DialogueSpeakerSelectorData selector, string conversationId, out string speakerPersonId, out DialogueFlowOperationResult failure)
        {
            speakerPersonId = string.Empty;
            failure = null;
            if (conversationRuntime == null || !conversationRuntime.TryGetSnapshot(conversationId, out ConversationSnapshot conversation))
            {
                failure = Fail(DialogueFlowOperationStatus.MissingConversation, $"Conversation '{conversationId}' is missing.");
                return false;
            }

            selector ??= new DialogueSpeakerSelectorData();
            ConversationParticipantRecordData[] participants = conversation.Participants.ToArray();
            speakerPersonId = selector.kind switch
            {
                DialogueSpeakerSelectorKind.None => string.Empty,
                DialogueSpeakerSelectorKind.ConversationInitiator => participants.FirstOrDefault(value => value.role == ConversationParticipantRole.Initiator)?.personId,
                DialogueSpeakerSelectorKind.ActiveSpeaker => conversation.ActiveSpeakerPersonId,
                DialogueSpeakerSelectorKind.ParticipantRole => participants.FirstOrDefault(value => value.role == selector.role)?.personId,
                DialogueSpeakerSelectorKind.SpecificPerson => participants.FirstOrDefault(value => value.personId == selector.personId)?.personId,
                DialogueSpeakerSelectorKind.Provider => participants.FirstOrDefault(value => value.role == ConversationParticipantRole.Provider || value.role == ConversationParticipantRole.OfficeHolder)?.personId,
                DialogueSpeakerSelectorKind.OfficeRepresentative => participants.FirstOrDefault(value => value.representedOfficeId == selector.officeId || value.role == ConversationParticipantRole.OfficeHolder)?.personId,
                DialogueSpeakerSelectorKind.OrganizationRepresentative => participants.FirstOrDefault(value => value.representedOrganizationId == selector.organizationId || value.role == ConversationParticipantRole.OrganizationRepresentative)?.personId,
                _ => participants.FirstOrDefault()?.personId
            } ?? string.Empty;
            if (selector.kind != DialogueSpeakerSelectorKind.None && string.IsNullOrWhiteSpace(speakerPersonId))
            {
                failure = Fail(DialogueFlowOperationStatus.SpeakerResolutionFailed, "Dialogue node speaker could not be resolved.");
                return false;
            }
            return true;
        }

        private bool ResolveListeners(DialogueListenerSelectorData selector, string conversationId, out string[] listenerPersonIds, out DialogueFlowOperationResult failure)
        {
            listenerPersonIds = Array.Empty<string>();
            failure = null;
            if (conversationRuntime == null || !conversationRuntime.TryGetSnapshot(conversationId, out ConversationSnapshot conversation))
            {
                failure = Fail(DialogueFlowOperationStatus.MissingConversation, $"Conversation '{conversationId}' is missing.");
                return false;
            }

            selector ??= new DialogueListenerSelectorData();
            ConversationParticipantRecordData[] participants = conversation.Participants.ToArray();
            IEnumerable<string> listeners = selector.kind switch
            {
                DialogueListenerSelectorKind.None => Array.Empty<string>(),
                DialogueListenerSelectorKind.AllParticipants => participants.Select(value => value.personId),
                DialogueListenerSelectorKind.ParticipantRole => participants.Where(value => value.role == selector.role).Select(value => value.personId),
                DialogueListenerSelectorKind.SpecificPerson => participants.Where(value => value.personId == selector.personId).Select(value => value.personId),
                DialogueListenerSelectorKind.Initiator => participants.Where(value => value.role == ConversationParticipantRole.Initiator).Select(value => value.personId),
                DialogueListenerSelectorKind.Provider => participants.Where(value => value.role == ConversationParticipantRole.Provider || value.role == ConversationParticipantRole.OfficeHolder).Select(value => value.personId),
                _ => participants.Select(value => value.personId)
            };
            listenerPersonIds = DialogueFlowModelUtility.Clean(listeners);
            if (selector.kind != DialogueListenerSelectorKind.None && listenerPersonIds.Length == 0)
            {
                failure = Fail(DialogueFlowOperationStatus.ListenerResolutionFailed, "Dialogue node listeners could not be resolved.");
                return false;
            }
            return true;
        }

        private bool TryResolveFlow(string flowId, out DialogueFlowRecordData flow, out DialogueGraphDefinitionData graph, out DialogueNodeDefinitionData node, out DialogueFlowOperationResult failure)
        {
            flow = null;
            graph = null;
            node = null;
            failure = null;
            if (!flowsById.TryGetValue(N(flowId), out flow))
            {
                failure = Fail(DialogueFlowOperationStatus.InvalidRequest, $"Dialogue flow '{N(flowId)}' is missing.");
                return false;
            }
            flow = flow.Clone();
            if (!TryResolveGraph(flow.graphId, out graph))
            {
                failure = Fail(DialogueFlowOperationStatus.MissingGraph, $"Dialogue graph '{flow.graphId}' is missing.");
                return false;
            }
            node = Node(graph, flow.currentNodeId);
            if (node == null)
            {
                failure = Fail(DialogueFlowOperationStatus.MissingNode, $"Dialogue node '{flow.currentNodeId}' is missing.");
                return false;
            }
            return true;
        }

        private bool TryResolveGraph(string graphId, out DialogueGraphDefinitionData graph)
        {
            graph = null;
            if (registry == null || !registry.TryGet(N(graphId), out DialogueGraphDefinition definition)) return false;
            graph = definition.ToRecordData();
            return true;
        }

        private string GraphForConversation(string conversationDefinitionId)
        {
            return registry?.DefinitionsById.Values.OfType<DialogueGraphDefinition>()
                .Where(value => value.ConversationDefinitionId == N(conversationDefinitionId))
                .OrderBy(value => value.Id, StringComparer.Ordinal)
                .Select(value => value.Id)
                .FirstOrDefault() ?? string.Empty;
        }

        private static DialogueNodeDefinitionData Node(DialogueGraphDefinitionData graph, string nodeId)
        {
            return (graph?.nodes ?? Array.Empty<DialogueNodeDefinitionData>()).FirstOrDefault(value => value.nodeId == N(nodeId))?.Clone();
        }

        private static void ExitCurrentVisit(DialogueFlowRecordData flow, double worldTime, string choiceId, string transitionId)
        {
            DialogueNodeVisitRecordData[] visits = flow.visits ?? Array.Empty<DialogueNodeVisitRecordData>();
            for (int i = 0; i < visits.Length; i++)
            {
                if (!string.Equals(visits[i].visitId, flow.currentVisitId, StringComparison.Ordinal)) continue;
                visits[i] = visits[i].Clone();
                visits[i].exitedWorldTime = worldTime;
                visits[i].selectedChoiceId = N(choiceId);
                visits[i].transitionId = N(transitionId);
                break;
            }
            flow.visits = visits;
        }

        private static void MarkChoiceUsed(DialogueFlowRecordData flow, DialogueChoiceDefinitionData choice, string actorPersonId)
        {
            if (choice.repeatPolicy == DialogueChoiceRepeatPolicy.Repeatable) return;
            string key = UsedChoiceKey(choice, actorPersonId);
            flow.usedChoiceKeys = (flow.usedChoiceKeys ?? Array.Empty<string>()).Concat(new[] { key }).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static string UsedChoiceKey(DialogueChoiceDefinitionData choice, string actorPersonId)
        {
            return choice.repeatPolicy == DialogueChoiceRepeatPolicy.OneShotPerActor ? $"{choice.choiceId}:{N(actorPersonId)}" : choice.choiceId;
        }

        private static bool LocalFlag(DialogueFlowRecordData flow, string variableId)
        {
            return (flow?.localVariables ?? Array.Empty<DialogueLocalVariableData>()).FirstOrDefault(value => value.variableId == N(variableId))?.boolValue == true;
        }

        private static int LocalCounter(DialogueFlowRecordData flow, string variableId)
        {
            return (flow?.localVariables ?? Array.Empty<DialogueLocalVariableData>()).FirstOrDefault(value => value.variableId == N(variableId))?.intValue ?? int.MinValue;
        }

        private static void SetLocal(DialogueFlowRecordData flow, string variableId, bool boolValue, int intValue, string token)
        {
            variableId = N(variableId);
            List<DialogueLocalVariableData> values = (flow.localVariables ?? Array.Empty<DialogueLocalVariableData>()).Select(value => value.Clone()).ToList();
            DialogueLocalVariableData existing = values.FirstOrDefault(value => value.variableId == variableId);
            if (existing == null)
            {
                existing = new DialogueLocalVariableData { variableId = variableId };
                values.Add(existing);
            }
            existing.boolValue = boolValue;
            existing.intValue = intValue;
            existing.tokenValue = token ?? string.Empty;
            flow.localVariables = values.OrderBy(value => value.variableId, StringComparer.Ordinal).ToArray();
        }

        private static void IncrementLocal(DialogueFlowRecordData flow, string variableId, int amount)
        {
            variableId = N(variableId);
            List<DialogueLocalVariableData> values = (flow.localVariables ?? Array.Empty<DialogueLocalVariableData>()).Select(value => value.Clone()).ToList();
            DialogueLocalVariableData existing = values.FirstOrDefault(value => value.variableId == variableId);
            if (existing == null)
            {
                existing = new DialogueLocalVariableData { variableId = variableId };
                values.Add(existing);
            }
            existing.intValue += amount;
            flow.localVariables = values.OrderBy(value => value.variableId, StringComparer.Ordinal).ToArray();
        }

        private static bool Contains(IEnumerable<string> values, string id)
        {
            return (values ?? Array.Empty<string>()).Contains(N(id), StringComparer.Ordinal);
        }

        private static bool Compare(int value, DialogueConditionData condition)
        {
            if (value == int.MinValue) return condition.comparison == DialogueValueComparison.NotExists;
            return condition.comparison switch
            {
                DialogueValueComparison.NotExists => false,
                DialogueValueComparison.GreaterThanOrEqual => value >= condition.minimumValue,
                DialogueValueComparison.LessThanOrEqual => value <= condition.maximumValue,
                DialogueValueComparison.Equal => value == condition.minimumValue,
                _ => true
            };
        }

        private void RecordTransaction(string transactionId, string operation, string flowId, string nodeId, string choiceId, DialogueFlowOperationStatus status)
        {
            if (string.IsNullOrWhiteSpace(transactionId)) return;
            transactionsById[transactionId] = new DialogueFlowTransactionData { transactionId = transactionId, operation = operation, flowId = flowId, nodeId = nodeId, choiceId = choiceId, status = status, runtimeRevision = revision };
        }

        private void Emit(string transactionId, DialogueFlowEventKind kind, string flowId, string conversationId, string nodeId, string choiceId, string actorPersonId, double worldTime)
        {
            DialogueFlowEventData evt = new DialogueFlowEventData
            {
                eventId = $"dialogue-flow-event.{events.Count + 1:000000}.{kind.ToString().ToLowerInvariant()}",
                transactionId = transactionId ?? string.Empty,
                eventKind = kind,
                flowId = flowId ?? string.Empty,
                conversationId = conversationId ?? string.Empty,
                nodeId = nodeId ?? string.Empty,
                choiceId = choiceId ?? string.Empty,
                actorPersonId = actorPersonId ?? string.Empty,
                worldTime = worldTime,
                runtimeRevision = revision
            };
            events.Add(evt.Clone());
            EventCommitted?.Invoke(evt.Clone());
        }

        private bool TryDuplicate(string transactionId, out DialogueFlowOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId) || !transactionsById.TryGetValue(transactionId, out DialogueFlowTransactionData transaction)) return false;
            flowsById.TryGetValue(transaction.flowId, out DialogueFlowRecordData flow);
            TryResolveGraph(flow?.graphId, out DialogueGraphDefinitionData graph);
            result = DialogueFlowOperationResult.Success("Duplicate Dialogue flow transaction ignored.", revision, revision, flow == null ? null : Snapshot(flow, graph, null), duplicate: true);
            return true;
        }

        private bool ValidateRevision(long expectedRevision, out DialogueFlowOperationResult failure)
        {
            if (expectedRevision >= 0L && expectedRevision != revision)
            {
                failure = Fail(DialogueFlowOperationStatus.RevisionConflict, $"Expected revision {expectedRevision}, actual {revision}.");
                return false;
            }
            failure = null;
            return true;
        }

        private void RebuildIndexes()
        {
            flowsByGraph.Clear();
            flowsByNode.Clear();
            foreach (DialogueFlowRecordData flow in flowsById.Values)
            {
                AddIndex(flowsByGraph, flow.graphId, flow.flowId);
                AddIndex(flowsByNode, flow.currentNodeId, flow.flowId);
            }
        }

        private static void AddIndex(IDictionary<string, List<string>> index, string key, string value)
        {
            key = N(key);
            value = N(value);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) return;
            if (!index.TryGetValue(key, out List<string> values))
            {
                values = new List<string>();
                index[key] = values;
            }
            if (!values.Contains(value)) values.Add(value);
            values.Sort(StringComparer.Ordinal);
        }

        private DialogueFlowOperationResult Fail(DialogueFlowOperationStatus status, string message, DialogueFlowSnapshot snapshot = null)
        {
            return DialogueFlowOperationResult.Failure(status, message, revision, snapshot);
        }

        private static string BuildSelectionId(string flowId, string choiceId, int index) => $"dialogue-choice-selection.{DialogueFlowModelUtility.Sanitize(flowId)}.{DialogueFlowModelUtility.Sanitize(choiceId)}.{index:000000}";
        private static string N(string value) => DialogueFlowModelUtility.N(value);
    }
}
