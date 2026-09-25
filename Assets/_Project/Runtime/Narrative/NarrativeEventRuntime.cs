using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Narrative
{
    public sealed class NarrativeEventRuntime : IDisposable
    {
        private readonly Dictionary<string, NarrativeEventRecordData> eventsById = new Dictionary<string, NarrativeEventRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> eventByDefinitionScope = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, NarrativeRuntimeTransactionData> transactionsById = new Dictionary<string, NarrativeRuntimeTransactionData>(StringComparer.Ordinal);
        private readonly Dictionary<NarrativeTriggerCategory, List<string>> definitionIdsByTrigger = new Dictionary<NarrativeTriggerCategory, List<string>>();
        private readonly List<NarrativeSignalRecordData> signals = new List<NarrativeSignalRecordData>();
        private readonly HashSet<string> processedTriggerKeys = new HashSet<string>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private NarrativeEventRuntimeIntegrations integrations;
        private string worldId;
        private long revision;
        private bool disposed;

        public NarrativeEventRuntime(DefinitionRegistry definitionRegistry = null, NarrativeEventRuntimeIntegrations runtimeIntegrations = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            Configure(definitionRegistry, runtimeIntegrations, runtimeWorldId);
        }

        public long Revision => revision;
        public string WorldId => worldId ?? string.Empty;
        public int Count => eventsById.Count;
        public IReadOnlyList<NarrativeSignalRecordData> Signals => signals.Select(value => value.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, NarrativeEventRuntimeIntegrations runtimeIntegrations = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            registry = definitionRegistry;
            integrations = runtimeIntegrations ?? new NarrativeEventRuntimeIntegrations();
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId.Trim();
            RebuildIndexes();
        }

        public NarrativeEventOperationResult Instantiate(NarrativeEventMutationRequest request)
        {
            if (disposed) return Fail(NarrativeOperationStatus.Disposed, "Narrative event runtime is disposed.");
            request ??= new NarrativeEventMutationRequest();
            if (!ValidateRevision(request.expectedRevision, out NarrativeEventOperationResult revisionFailure)) return revisionFailure;
            if (TryDuplicate(request.transactionId, out NarrativeEventOperationResult duplicate)) return duplicate;
            if (!TryResolveDefinition(request.eventDefinitionId, out NarrativeEventDefinitionData definition, out NarrativeEventOperationResult definitionFailure)) return definitionFailure;

            string scopeKey = ResolveScopeKey(definition, request.context, request.scopeKey);
            if (string.IsNullOrWhiteSpace(scopeKey)) return Fail(NarrativeOperationStatus.InvalidRequest, "Narrative event scope key could not be resolved.");
            string uniquenessKey = BuildDefinitionScopeKey(definition.eventDefinitionId, scopeKey);
            if (eventByDefinitionScope.TryGetValue(uniquenessKey, out string existingId) && eventsById.TryGetValue(existingId, out NarrativeEventRecordData existing))
            {
                return NarrativeEventOperationResult.Success("Existing scoped NarrativeEvent returned.", revision, revision, Snapshot(existing, definition), duplicate: true);
            }

            NarrativeEventRecordData record = CreateRecord(definition, scopeKey, request.context, request.worldTime);
            if (request.preview) return NarrativeEventOperationResult.Success("Narrative event instantiation previewed.", revision, revision, Snapshot(record, definition), preview: true);

            long before = revision;
            eventsById[record.narrativeEventId] = record.Clone();
            eventByDefinitionScope[uniquenessKey] = record.narrativeEventId;
            revision++;
            RecordTransaction(request.transactionId, "Instantiate", record.narrativeEventId, NarrativeOperationStatus.Succeeded);
            return NarrativeEventOperationResult.Success("Narrative event instantiated.", before, revision, Snapshot(record, definition));
        }

        public NarrativeEventOperationResult Arm(NarrativeEventMutationRequest request)
        {
            return ChangeLifecycle(request, NarrativeEventLifecycle.Armed, "Arm", "Narrative event armed.");
        }

        public NarrativeEventOperationResult Disarm(NarrativeEventMutationRequest request)
        {
            return ChangeLifecycle(request, NarrativeEventLifecycle.Disarmed, "Disarm", "Narrative event disarmed.");
        }

        public NarrativeEventOperationResult Cancel(NarrativeEventMutationRequest request)
        {
            return ChangeLifecycle(request, NarrativeEventLifecycle.Cancelled, "Cancel", "Narrative event cancelled.");
        }

        public NarrativeEventOperationResult EmitSignal(NarrativeSignalRequest request)
        {
            if (disposed) return Fail(NarrativeOperationStatus.Disposed, "Narrative event runtime is disposed.");
            request ??= new NarrativeSignalRequest();
            if (!ValidateRevision(request.expectedRevision, out NarrativeEventOperationResult revisionFailure)) return revisionFailure;
            string transactionId = N(request.transactionId);
            if (TryDuplicate(transactionId, out NarrativeEventOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(request.signalDefinitionId)) return Fail(NarrativeOperationStatus.InvalidRequest, "Narrative signal requires a stable definition ID.");

            NarrativeSignalRecordData signal = new NarrativeSignalRecordData
            {
                narrativeSignalId = string.IsNullOrWhiteSpace(request.signalId) ? BuildSignalId(request.signalDefinitionId, transactionId, signals.Count + 1) : N(request.signalId),
                signalDefinitionId = N(request.signalDefinitionId),
                sourceKind = request.sourceKind == NarrativeSignalSourceKind.Unknown ? NarrativeSignalSourceKind.NarrativeSystem : request.sourceKind,
                sourceId = N(request.sourceId),
                sourceTransactionId = transactionId,
                actorPersonId = N(request.actorPersonId),
                subjectIds = NarrativeModelUtility.Clean(request.subjectIds),
                provenanceId = N(request.provenanceId),
                worldTime = request.worldTime,
                runtimeRevision = revision
            };

            if (request.preview)
            {
                NarrativeTriggerSourceData previewSource = SourceFromSignal(signal);
                return RouteTrigger(new NarrativeTriggerRequest { transactionId = transactionId, source = previewSource, conditionContext = request.conditionContext?.Clone(), preview = true, cascadeDepth = request.cascadeDepth });
            }

            long before = revision;
            signals.Add(signal.Clone());
            revision++;
            RecordTransaction(transactionId, "EmitSignal", string.Empty, NarrativeOperationStatus.Succeeded);
            NarrativeEventOperationResult routed = RouteTrigger(new NarrativeTriggerRequest { transactionId = transactionId, source = SourceFromSignal(signal), conditionContext = request.conditionContext?.Clone(), cascadeDepth = request.cascadeDepth });
            if (!routed.Succeeded && routed.Status != NarrativeOperationStatus.TriggerIgnored) return routed;
            return NarrativeEventOperationResult.SuccessMany("Narrative signal emitted.", before, revision, routed.Snapshots);
        }

        public NarrativeEventOperationResult RouteTrigger(NarrativeTriggerRequest request)
        {
            if (disposed) return Fail(NarrativeOperationStatus.Disposed, "Narrative event runtime is disposed.");
            request ??= new NarrativeTriggerRequest();
            NarrativeTriggerSourceData source = request.source?.Clone() ?? new NarrativeTriggerSourceData();
            bool preview = request.preview || source.preview;
            if (preview) source.preview = true;
            if (!source.committed && !preview) return Fail(NarrativeOperationStatus.TriggerIgnored, "Preview or failed source events do not trigger narrative events.");
            if (source.restoreReplay) return Fail(NarrativeOperationStatus.TriggerIgnored, "Restore replay sources do not trigger narrative events.");
            if (request.cascadeDepth > MaxCascadeDepthFor(source)) return Fail(NarrativeOperationStatus.CascadeLimitReached, "Narrative cascade depth limit reached.");

            string triggerKey = source.StableOccurrenceKey;
            if (!preview && processedTriggerKeys.Contains(triggerKey)) return NarrativeEventOperationResult.SuccessMany("Duplicate trigger occurrence ignored.", revision, revision, Array.Empty<NarrativeEventSnapshot>());

            IReadOnlyList<NarrativeEventDefinitionData> definitions = CandidateDefinitions(source.category)
                .Where(definition => TriggerMatches(definition, source))
                .Where(definition => InActivationWindow(definition, source.worldTime))
                .OrderBy(definition => source.worldTime)
                .ThenBy(definition => definition.priority)
                .ThenBy(definition => definition.eventDefinitionId, StringComparer.Ordinal)
                .ToArray();

            List<NarrativeEventSnapshot> changed = new List<NarrativeEventSnapshot>();
            long before = revision;
            foreach (NarrativeEventDefinitionData definition in definitions)
            {
                NarrativeEventOperationResult result = TriggerDefinition(definition, source, request.conditionContext, request.transactionId, preview, request.cascadeDepth);
                if (result.Succeeded && result.Snapshot != null) changed.Add(result.Snapshot);
                else if (IsHardTriggerFailure(result.Status)) return result;
            }

            if (!preview)
            {
                processedTriggerKeys.Add(triggerKey);
            }

            return changed.Count == 0
                ? NarrativeEventOperationResult.SuccessMany("No narrative event candidates matched.", before, revision, changed, preview)
                : NarrativeEventOperationResult.SuccessMany("Narrative trigger routed.", before, revision, changed, preview);
        }

        public NarrativeEventOperationResult Trigger(NarrativeTriggerRequest request)
        {
            if (disposed) return Fail(NarrativeOperationStatus.Disposed, "Narrative event runtime is disposed.");
            request ??= new NarrativeTriggerRequest();
            if (!TryResolveEvent(request.narrativeEventId, request.eventDefinitionId, request.conditionContext, out NarrativeEventRecordData record, out NarrativeEventDefinitionData definition, out NarrativeEventOperationResult failure)) return failure;
            return TriggerExisting(record, definition, request.source?.Clone() ?? new NarrativeTriggerSourceData(), request.conditionContext, request.transactionId, request.preview, request.cascadeDepth);
        }

        public NarrativeEventOperationResult Execute(NarrativeExecutionRequest request)
        {
            if (disposed) return Fail(NarrativeOperationStatus.Disposed, "Narrative event runtime is disposed.");
            request ??= new NarrativeExecutionRequest();
            if (!ValidateRevision(request.expectedRevision, out NarrativeEventOperationResult revisionFailure)) return revisionFailure;
            if (TryDuplicate(request.transactionId, out NarrativeEventOperationResult duplicate)) return duplicate;
            if (!eventsById.TryGetValue(N(request.narrativeEventId), out NarrativeEventRecordData record)) return Fail(NarrativeOperationStatus.InvalidRequest, $"Narrative event '{N(request.narrativeEventId)}' is missing.");
            if (!TryResolveDefinition(record.eventDefinitionId, out NarrativeEventDefinitionData definition, out NarrativeEventOperationResult definitionFailure)) return definitionFailure;
            if (record.lifecycle != NarrativeEventLifecycle.Triggered && record.lifecycle != NarrativeEventLifecycle.Waiting) return Fail(NarrativeOperationStatus.InvalidRequest, $"Narrative event '{record.narrativeEventId}' is not pending execution.", Snapshot(record, definition));

            return ExecuteActions(record, definition, request.conditionContext, request.transactionId, request.preview, request.cascadeDepth);
        }

        public IReadOnlyList<NarrativeEventSnapshot> Query(NarrativeEventQuery query = null)
        {
            query ??= new NarrativeEventQuery();
            IEnumerable<NarrativeEventRecordData> records = eventsById.Values;
            if (!string.IsNullOrWhiteSpace(query.eventId)) records = records.Where(value => string.Equals(value.narrativeEventId, N(query.eventId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.definitionId)) records = records.Where(value => string.Equals(value.eventDefinitionId, N(query.definitionId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.scopeKey)) records = records.Where(value => string.Equals(value.scopeKey, N(query.scopeKey), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.personId)) records = records.Where(value => string.Equals(value.actorPersonId, N(query.personId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.questId)) records = records.Where(value => string.Equals(value.questId, N(query.questId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.conversationId)) records = records.Where(value => string.Equals(value.conversationId, N(query.conversationId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.locationId)) records = records.Where(value => string.Equals(value.locationId, N(query.locationId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.organizationId)) records = records.Where(value => string.Equals(value.organizationId, N(query.organizationId), StringComparison.Ordinal));
            if (query.lifecycle.HasValue) records = records.Where(value => value.lifecycle == query.lifecycle.Value);
            if (query.minWorldTime >= 0d) records = records.Where(value => value.triggerTime >= query.minWorldTime || value.armTime >= query.minWorldTime);
            if (query.maxWorldTime >= 0d) records = records.Where(value => (value.triggerTime >= 0d ? value.triggerTime : value.armTime) <= query.maxWorldTime);

            return records
                .OrderBy(value => value.triggerTime < 0d ? value.armTime : value.triggerTime)
                .ThenBy(value => value.eventDefinitionId, StringComparer.Ordinal)
                .ThenBy(value => value.narrativeEventId, StringComparer.Ordinal)
                .Select(value =>
                {
                    TryResolveDefinition(value.eventDefinitionId, out NarrativeEventDefinitionData definition, out _);
                    return Snapshot(value, definition, query.developmentView);
                })
                .ToArray();
        }

        public NarrativeEventRuntimeSaveData CreateSaveData()
        {
            return new NarrativeEventRuntimeSaveData
            {
                schemaVersion = NarrativeEventRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = revision,
                events = eventsById.Values.Select(value => value.Clone()).OrderBy(value => value.narrativeEventId, StringComparer.Ordinal).ToList(),
                signals = signals.Select(value => value.Clone()).OrderBy(value => value.narrativeSignalId, StringComparer.Ordinal).ToList(),
                transactions = transactionsById.Values.Select(value => value.Clone()).OrderBy(value => value.transactionId, StringComparer.Ordinal).ToList(),
                processedTriggerKeys = processedTriggerKeys.OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
        }

        public NarrativeEventOperationResult RestoreFromSaveData(NarrativeEventRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, NarrativeEventRuntimeIntegrations runtimeIntegrations = null, string expectedWorldId = PersistenceService.LocalWorldId)
        {
            if (disposed) return Fail(NarrativeOperationStatus.Disposed, "Narrative event runtime is disposed.");
            if (!ValidateSaveData(saveData, definitionRegistry, expectedWorldId, out string failure)) return Fail(NarrativeOperationStatus.RestoreFailed, failure);

            eventsById.Clear();
            eventByDefinitionScope.Clear();
            transactionsById.Clear();
            signals.Clear();
            processedTriggerKeys.Clear();
            registry = definitionRegistry;
            integrations = runtimeIntegrations ?? new NarrativeEventRuntimeIntegrations();
            worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            revision = saveData.revision;
            foreach (NarrativeEventRecordData record in saveData.events ?? new List<NarrativeEventRecordData>())
            {
                NarrativeEventRecordData clone = record.Clone();
                eventsById[clone.narrativeEventId] = clone;
                eventByDefinitionScope[BuildDefinitionScopeKey(clone.eventDefinitionId, clone.scopeKey)] = clone.narrativeEventId;
            }

            foreach (NarrativeSignalRecordData signal in saveData.signals ?? new List<NarrativeSignalRecordData>()) signals.Add(signal.Clone());
            foreach (NarrativeRuntimeTransactionData transaction in saveData.transactions ?? new List<NarrativeRuntimeTransactionData>()) transactionsById[transaction.transactionId] = transaction.Clone();
            foreach (string key in saveData.processedTriggerKeys ?? Array.Empty<string>()) processedTriggerKeys.Add(key);
            RebuildIndexes();
            return NarrativeEventOperationResult.Success("Narrative events restored.", revision, revision);
        }

        public static bool ValidateSaveData(NarrativeEventRuntimeSaveData saveData, DefinitionRegistry registry, string expectedWorldId, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Narrative event save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != NarrativeEventRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported narrative event save schema version {saveData.schemaVersion}.";
                return false;
            }

            string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            if (!string.Equals(saveData.worldId ?? string.Empty, world, StringComparison.Ordinal))
            {
                failure = $"Narrative event save world '{saveData.worldId}' does not match expected world '{world}'.";
                return false;
            }

            HashSet<string> eventIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> scopeKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeEventRecordData record in saveData.events ?? new List<NarrativeEventRecordData>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.narrativeEventId))
                {
                    failure = "Narrative event save contains a record without an ID.";
                    return false;
                }

                if (!eventIds.Add(record.narrativeEventId))
                {
                    failure = $"Duplicate NarrativeEventId '{record.narrativeEventId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(record.eventDefinitionId, out NarrativeEventDefinition definition))
                {
                    failure = $"NarrativeEvent '{record.narrativeEventId}' references missing definition '{record.eventDefinitionId}'.";
                    return false;
                }

                NarrativeEventDefinitionData definitionData = definition.ToRecordData();
                if (!scopeKeys.Add(BuildDefinitionScopeKey(record.eventDefinitionId, record.scopeKey)) && definitionData.repeatPolicy != NarrativeRepeatPolicy.Repeatable)
                {
                    failure = $"Duplicate scoped NarrativeEvent for definition '{record.eventDefinitionId}' and scope '{record.scopeKey}'.";
                    return false;
                }

                if (record.lifecycle == NarrativeEventLifecycle.Resolved && (definitionData.actions ?? Array.Empty<NarrativeActionDefinitionData>()).Any(action => action.requirement == NarrativeActionRequirement.Required) && (record.actionExecutions == null || record.actionExecutions.Length == 0))
                {
                    failure = $"Resolved NarrativeEvent '{record.narrativeEventId}' has no required action execution records.";
                    return false;
                }

                foreach (NarrativeActionExecutionRecordData action in record.actionExecutions ?? Array.Empty<NarrativeActionExecutionRecordData>())
                {
                    if (action == null || string.IsNullOrWhiteSpace(action.actionExecutionId))
                    {
                        failure = $"NarrativeEvent '{record.narrativeEventId}' has an action execution without an ID.";
                        return false;
                    }
                }
            }

            return true;
        }

        public void Dispose()
        {
            disposed = true;
            eventsById.Clear();
            eventByDefinitionScope.Clear();
            transactionsById.Clear();
            signals.Clear();
            processedTriggerKeys.Clear();
        }

        private NarrativeEventOperationResult TriggerDefinition(NarrativeEventDefinitionData definition, NarrativeTriggerSourceData source, NarrativeConditionContextData context, string transactionId, bool preview, int cascadeDepth)
        {
            string scopeKey = ResolveScopeKey(definition, context, string.Empty, source);
            if (string.IsNullOrWhiteSpace(scopeKey)) return Fail(NarrativeOperationStatus.InvalidRequest, "Scoped NarrativeEvent cannot resolve scope key.");
            string uniquenessKey = BuildDefinitionScopeKey(definition.eventDefinitionId, scopeKey);
            NarrativeEventRecordData record;
            if (eventByDefinitionScope.TryGetValue(uniquenessKey, out string eventId) && eventsById.TryGetValue(eventId, out NarrativeEventRecordData existing))
            {
                record = existing.Clone();
            }
            else
            {
                record = CreateRecord(definition, scopeKey, context, source.worldTime, source);
                if (!preview && definition.armingPolicy == NarrativeArmingPolicy.OnWorldInitialization)
                {
                    record.lifecycle = NarrativeEventLifecycle.Armed;
                    record.armTime = source.worldTime;
                }
            }

            return TriggerExisting(record, definition, source, context, transactionId, preview, cascadeDepth);
        }

        private NarrativeEventOperationResult TriggerExisting(NarrativeEventRecordData record, NarrativeEventDefinitionData definition, NarrativeTriggerSourceData source, NarrativeConditionContextData context, string transactionId, bool preview, int cascadeDepth)
        {
            if (record.lifecycle == NarrativeEventLifecycle.Resolved && definition.repeatPolicy != NarrativeRepeatPolicy.Repeatable && definition.repeatPolicy != NarrativeRepeatPolicy.RearmExplicitly)
            {
                return NarrativeEventOperationResult.Success("NarrativeEvent already resolved.", revision, revision, Snapshot(record, definition), duplicate: true);
            }

            if (record.lifecycle == NarrativeEventLifecycle.Disarmed || record.lifecycle == NarrativeEventLifecycle.Cancelled || record.lifecycle == NarrativeEventLifecycle.Failed)
            {
                return Fail(NarrativeOperationStatus.NotArmed, $"NarrativeEvent '{record.narrativeEventId}' is not armed.", Snapshot(record, definition));
            }

            if (record.lifecycle == NarrativeEventLifecycle.Created && definition.armingPolicy != NarrativeArmingPolicy.OnWorldInitialization && definition.armingPolicy != NarrativeArmingPolicy.Development)
            {
                return Fail(NarrativeOperationStatus.NotArmed, $"NarrativeEvent '{record.narrativeEventId}' is not armed.", Snapshot(record, definition));
            }

            NarrativeConditionResultData[] conditions = EvaluateConditions(definition, context, source).ToArray();
            bool matched = ConditionsMatched(definition, conditions);
            if (!matched) return Fail(NarrativeOperationStatus.ConditionFailed, "Narrative event conditions did not match.", Snapshot(record, definition));

            NarrativeEventRecordData triggered = record.Clone();
            triggered.lifecycle = definition.triggerMode == NarrativeTriggerMode.TriggerAfterDelay ? NarrativeEventLifecycle.Waiting : NarrativeEventLifecycle.Triggered;
            triggered.triggerTime = source.worldTime;
            triggered.triggerSource = source.Clone();
            triggered.matchedConditions = conditions;
            triggered.cascadeDepth = cascadeDepth;
            triggered.revision++;

            if (preview) return NarrativeEventOperationResult.Success("NarrativeEvent trigger previewed.", revision, revision, Snapshot(triggered, definition), preview: true);
            if (definition.triggerMode == NarrativeTriggerMode.TriggerAfterDelay || definition.triggerMode == NarrativeTriggerMode.QueueForExecution)
            {
                long before = revision;
                CommitRecord(triggered);
                revision++;
                RecordTransaction(transactionId, "Trigger", triggered.narrativeEventId, NarrativeOperationStatus.Succeeded);
                return NarrativeEventOperationResult.Success("NarrativeEvent triggered and queued.", before, revision, Snapshot(triggered, definition));
            }

            return ExecuteActions(triggered, definition, context, transactionId, false, cascadeDepth);
        }

        private NarrativeEventOperationResult ExecuteActions(NarrativeEventRecordData triggered, NarrativeEventDefinitionData definition, NarrativeConditionContextData context, string transactionId, bool preview, int cascadeDepth)
        {
            NarrativeEventRecordData executing = triggered.Clone();
            executing.lifecycle = NarrativeEventLifecycle.Executing;
            executing.executionStartTime = executing.triggerTime >= 0d ? executing.triggerTime : context?.worldTime ?? 0d;

            List<NarrativeActionExecutionRecordData> actionRecords = new List<NarrativeActionExecutionRecordData>();
            Dictionary<string, string> outputSlots = new Dictionary<string, string>(StringComparer.Ordinal);
            bool requiredFailed = false;
            int actionIndex = 0;
            foreach (NarrativeActionDefinitionData action in definition.actions.OrderBy(value => value.order).ThenBy(value => value.actionDefinitionId, StringComparer.Ordinal))
            {
                actionIndex++;
                NarrativeActionExecutionRecordData actionRecord = ExecuteAction(executing, definition, action, actionIndex, context, outputSlots, transactionId, preview, cascadeDepth);
                actionRecords.Add(actionRecord);
                if (!string.IsNullOrWhiteSpace(action.outputSlotId) && !string.IsNullOrWhiteSpace(actionRecord.resultValue)) outputSlots[action.outputSlotId] = actionRecord.resultValue;
                if (actionRecord.lifecycle == NarrativeActionLifecycle.Failed && action.requirement == NarrativeActionRequirement.Required)
                {
                    requiredFailed = true;
                    if (definition.atomicityPolicy == NarrativeActionAtomicityPolicy.AtomicAllActions || definition.atomicityPolicy == NarrativeActionAtomicityPolicy.RequiredAtomicOptionalIndependent) break;
                }
            }

            executing.actionExecutions = actionRecords.Select(value => value.Clone()).ToArray();
            executing.executionEndTime = executing.executionStartTime;
            executing.lifecycle = requiredFailed ? NarrativeEventLifecycle.Failed : NarrativeEventLifecycle.Resolved;
            executing.revision++;

            if (preview)
            {
                return NarrativeEventOperationResult.Success("NarrativeEvent execution previewed.", revision, revision, Snapshot(executing, definition), actionRecords, preview: true);
            }

            long before = revision;
            CommitRecord(executing);
            revision++;
            RecordTransaction(transactionId, "Execute", executing.narrativeEventId, requiredFailed ? NarrativeOperationStatus.ActionFailed : NarrativeOperationStatus.Succeeded);
            string failureMessage = requiredFailed
                ? "Required narrative action failed: " + string.Join(" | ", actionRecords.Where(value => value.lifecycle == NarrativeActionLifecycle.Failed).Select(value => $"{value.actionDefinitionId}={value.message}").Where(value => !string.IsNullOrWhiteSpace(value)))
                : string.Empty;
            return requiredFailed
                ? new NarrativeEventOperationResult(NarrativeOperationStatus.ActionFailed, string.IsNullOrWhiteSpace(failureMessage) ? "Required narrative action failed." : failureMessage, before, revision, Snapshot(executing, definition), actionResults: actionRecords)
                : NarrativeEventOperationResult.Success("NarrativeEvent resolved.", before, revision, Snapshot(executing, definition), actionRecords);
        }

        private NarrativeActionExecutionRecordData ExecuteAction(NarrativeEventRecordData record, NarrativeEventDefinitionData definition, NarrativeActionDefinitionData action, int actionIndex, NarrativeConditionContextData context, IDictionary<string, string> outputSlots, string transactionId, bool preview, int cascadeDepth)
        {
            string target = ResolveActionTarget(action, outputSlots);
            NarrativeActionExecutionRecordData result = new NarrativeActionExecutionRecordData
            {
                actionExecutionId = BuildActionExecutionId(record.narrativeEventId, action.actionDefinitionId, actionIndex),
                narrativeEventId = record.narrativeEventId,
                actionDefinitionId = action.actionDefinitionId,
                category = action.category,
                requirement = action.requirement,
                order = action.order,
                targetOwnerRuntime = OwnerRuntime(action.category),
                outputSlotId = action.outputSlotId,
                worldTime = record.triggerTime >= 0d ? record.triggerTime : context?.worldTime ?? 0d,
                runtimeRevision = revision
            };

            if (action.category == NarrativeActionCategory.None)
            {
                result.lifecycle = NarrativeActionLifecycle.SkippedOptional;
                result.message = "No-op narrative action.";
                return result;
            }

            if (preview)
            {
                result.lifecycle = NarrativeActionLifecycle.Prepared;
                result.resultValue = PreviewResultValue(record, action, target);
                result.externalResultId = result.resultValue;
                result.message = "Narrative action previewed.";
                return result;
            }

            bool succeeded = TryExecuteOwnerAction(record, action, target, context, outputSlots, transactionId, cascadeDepth, out string externalId, out string message);
            result.lifecycle = succeeded ? NarrativeActionLifecycle.Committed : action.requirement == NarrativeActionRequirement.OptionalBestEffort ? NarrativeActionLifecycle.SkippedOptional : NarrativeActionLifecycle.Failed;
            result.externalResultId = externalId ?? string.Empty;
            result.resultValue = externalId ?? string.Empty;
            result.message = message ?? string.Empty;
            return result;
        }

        private bool TryExecuteOwnerAction(NarrativeEventRecordData record, NarrativeActionDefinitionData action, string target, NarrativeConditionContextData context, IDictionary<string, string> outputSlots, string transactionId, int cascadeDepth, out string externalId, out string message)
        {
            externalId = string.Empty;
            message = string.Empty;
            switch (action.category)
            {
                case NarrativeActionCategory.InstantiateQuest:
                    if (integrations?.QuestRuntime == null)
                    {
                        message = "QuestRuntime integration is missing.";
                        return false;
                    }

                    if (registry == null || !registry.TryGet(target, out QuestDefinition questDefinition))
                    {
                        message = $"Quest definition '{target}' is missing.";
                        return false;
                    }

                    string questId = $"quest.narrative.{NarrativeModelUtility.SanitizeForId(record.narrativeEventId)}.{NarrativeModelUtility.SanitizeForId(action.actionDefinitionId)}";
                    QuestRuntimeOperationResult create = integrations.QuestRuntime.CreateQuest(new QuestCreateRequest
                    {
                        transactionId = $"{transactionId}.{action.actionDefinitionId}.quest",
                        questId = questId,
                        questDefinitionId = target,
                        issuer = BuildQuestIssuer(questDefinition, action, record, context),
                        intendedRecipient = BuildQuestRecipient(questDefinition, record),
                        origin = BuildQuestOrigin(questDefinition, record),
                        subjectLinks = BuildQuestSubjectLinks(record),
                        createdWorldTime = record.triggerTime,
                        sourceEventId = record.narrativeEventId,
                        provenanceId = record.narrativeEventId
                    });
                    externalId = create.Snapshot?.QuestId ?? questId;
                    message = create.Message;
                    return create.Succeeded;
                case NarrativeActionCategory.PublishQuestListing:
                    if (integrations?.QuestSourceRuntime == null)
                    {
                        message = "QuestSourceRuntime integration is missing.";
                        return false;
                    }

                    string questToPublish = string.IsNullOrWhiteSpace(action.inputSlotId) ? record.questId : ResolveActionTarget(action, outputSlots);
                    QuestSourceOperationResult publish = integrations.QuestSourceRuntime.PublishListing(new QuestListingPublishRequest
                    {
                        transactionId = $"{transactionId}.{action.actionDefinitionId}.listing",
                        questSourceId = target,
                        questId = questToPublish,
                        publisherAuthorityId = action.secondaryTargetId,
                        publisherPersonId = string.IsNullOrWhiteSpace(record.actorPersonId) ? "person.narrative.publisher" : record.actorPersonId,
                        sourceEventId = record.narrativeEventId,
                        provenanceId = record.narrativeEventId,
                        worldTime = record.triggerTime
                    });
                    externalId = publish.Listing?.QuestListingId ?? string.Empty;
                    message = publish.Message;
                    return publish.Succeeded;
                case NarrativeActionCategory.StartConversation:
                    if (integrations?.ConversationRuntime == null)
                    {
                        message = "ConversationRuntime integration is missing.";
                        return false;
                    }

                    if (registry == null || !registry.TryGet(target, out ConversationDefinition conversationDefinition))
                    {
                        message = $"Conversation definition '{target}' is missing.";
                        return false;
                    }

                    string conversationId = $"conversation.narrative.{NarrativeModelUtility.SanitizeForId(record.narrativeEventId)}.{NarrativeModelUtility.SanitizeForId(action.actionDefinitionId)}";
                    ConversationOperationResult conversation = integrations.ConversationRuntime.StartConversation(BuildConversationStartRequest(conversationDefinition, action, record, context, transactionId, conversationId));
                    externalId = conversation.Snapshot?.ConversationId ?? conversationId;
                    message = conversation.Message;
                    return conversation.Succeeded;
                case NarrativeActionCategory.EmitNarrativeSignal:
                    NarrativeSignalRequest signal = new NarrativeSignalRequest
                    {
                        transactionId = $"{transactionId}.{action.actionDefinitionId}.signal",
                        signalDefinitionId = target,
                        sourceKind = NarrativeSignalSourceKind.NarrativeSystem,
                        sourceId = record.narrativeEventId,
                        actorPersonId = record.actorPersonId,
                        subjectIds = new[] { record.subjectId },
                        conditionContext = context?.Clone(),
                        worldTime = record.triggerTime,
                        cascadeDepth = cascadeDepth + 1
                    };
                    NarrativeEventOperationResult signalResult = EmitSignal(signal);
                    externalId = signal.signalDefinitionId;
                    message = signalResult.Message;
                    return signalResult.Succeeded;
                case NarrativeActionCategory.GrantInformation:
                    return ExecuteDelegate(integrations?.InformationGrantExecutor, target, "Information grant recorded.", out externalId, out message);
                case NarrativeActionCategory.ActivateTravelCondition:
                case NarrativeActionCategory.ResolveTravelCondition:
                case NarrativeActionCategory.TriggerTravelEncounter:
                    return ExecuteDelegate(integrations?.TravelConditionExecutor, target, "Travel condition action recorded.", out externalId, out message);
                case NarrativeActionCategory.RequestConnectionStateChange:
                    return ExecuteDelegate(integrations?.ConnectionChangeExecutor, target, "Connection change action recorded.", out externalId, out message);
                case NarrativeActionCategory.TriggerSocialInteraction:
                    if (integrations?.ContextualSocialActionExecutor != null)
                    {
                        externalId = target;
                        bool socialSucceeded = integrations.ContextualSocialActionExecutor(new NarrativeSocialActionRequest
                        {
                            InteractionDefinitionId = target,
                            ActorPersonId = record.actorPersonId,
                            TargetPersonId = record.subjectId,
                            NarrativeEventId = record.narrativeEventId,
                            ActionDefinitionId = action.actionDefinitionId,
                            WorldTime = record.triggerTime
                        });
                        message = socialSucceeded ? "Social action recorded." : "Social action was rejected by the social runtime.";
                        return socialSucceeded;
                    }
                    return ExecuteDelegate(integrations?.SocialActionExecutor, target, "Social action recorded.", out externalId, out message);
                case NarrativeActionCategory.RequestOrganizationMembership:
                case NarrativeActionCategory.RequestRankChange:
                    return ExecuteDelegate(integrations?.OrganizationActionExecutor, target, "Organization action recorded.", out externalId, out message);
                case NarrativeActionCategory.RequestPermit:
                case NarrativeActionCategory.CreateIncidentReport:
                    return ExecuteDelegate(integrations?.LegalActionExecutor, target, "Legal action recorded.", out externalId, out message);
                case NarrativeActionCategory.HistoricalEventRequest:
                    externalId = $"historical-request.{NarrativeModelUtility.SanitizeForId(record.narrativeEventId)}.{NarrativeModelUtility.SanitizeForId(action.actionDefinitionId)}";
                    message = "Explicit Step 8 historical event request recorded for owner processing.";
                    return true;
                case NarrativeActionCategory.RequestNarrativeStateTransition:
                    if (integrations?.NarrativeStateTransitionExecutor == null)
                    {
                        message = "NarrativeStateRuntime integration is missing.";
                        return false;
                    }

                    NarrativeStateTransitionResult transition = integrations.NarrativeStateTransitionExecutor(new NarrativeStateTransitionRequest
                    {
                        transactionId = $"{transactionId}.{action.actionDefinitionId}.narrative-state",
                        transitionDefinitionId = target,
                        scopeKey = action.secondaryTargetId,
                        sourceKind = NarrativeTransitionSourceKind.NarrativeEvent,
                        sourceId = record.narrativeEventId,
                        actorPersonId = record.actorPersonId,
                        questId = record.questId,
                        conversationId = record.conversationId,
                        narrativeEventId = record.narrativeEventId,
                        conditionContext = context?.Clone(),
                        worldTime = record.triggerTime,
                        cascadeDepth = cascadeDepth + 1
                    });
                    externalId = transition.Transition?.TransitionId ?? target;
                    message = transition.Message;
                    return transition.Succeeded;
                case NarrativeActionCategory.RequestNarrativeArcProgression:
                    if (integrations?.NarrativeArcSignalExecutor == null)
                    {
                        message = "NarrativeArcRuntime integration is missing.";
                        return false;
                    }

                    NarrativeArcOperationResult arc = integrations.NarrativeArcSignalExecutor(new NarrativeArcSignalRequest
                    {
                        transactionId = $"{transactionId}.{action.actionDefinitionId}.narrative-arc",
                        arcDefinitionId = action.secondaryTargetId,
                        stageDefinitionId = target,
                        category = NarrativeArcSignalCategory.NarrativeEvent,
                        signalId = record.narrativeEventId,
                        sourceId = record.eventDefinitionId,
                        actorPersonId = record.actorPersonId,
                        subjectId = record.subjectId,
                        scopeKey = record.scopeKey,
                        conditionContext = context?.Clone(),
                        worldTime = record.triggerTime,
                        cascadeDepth = cascadeDepth + 1
                    });
                    externalId = arc.Snapshot?.NarrativeArcId ?? target;
                    message = arc.Message;
                    return arc.Succeeded;
                case NarrativeActionCategory.ArmNarrativeEvent:
                case NarrativeActionCategory.DisarmNarrativeEvent:
                    externalId = target;
                    message = "Narrative lifecycle action recorded.";
                    return true;
                default:
                    externalId = target;
                    message = "Typed action category is present but owner integration is deferred.";
                    return action.requirement != NarrativeActionRequirement.Required;
            }
        }

        private static bool ExecuteDelegate(Func<string, bool> executor, string target, string successMessage, out string externalId, out string message)
        {
            externalId = target ?? string.Empty;
            if (executor == null)
            {
                message = "Owner integration is missing.";
                return false;
            }

            bool succeeded = executor(target ?? string.Empty);
            message = succeeded ? successMessage : "Owner integration rejected action.";
            return succeeded;
        }

        private IReadOnlyList<NarrativeConditionResultData> EvaluateConditions(NarrativeEventDefinitionData definition, NarrativeConditionContextData context, NarrativeTriggerSourceData source)
        {
            NarrativeConditionContextData data = context?.Clone() ?? new NarrativeConditionContextData();
            data.worldTime = data.worldTime > 0d ? data.worldTime : source.worldTime;
            if (definition.conditions == null || definition.conditions.Length == 0) return Array.Empty<NarrativeConditionResultData>();
            return definition.conditions.Select(condition => EvaluateCondition(condition, data)).ToArray();
        }

        private NarrativeConditionResultData EvaluateCondition(NarrativeConditionDefinitionData condition, NarrativeConditionContextData context)
        {
            bool matched = condition.category switch
            {
                NarrativeConditionCategory.Always => true,
                NarrativeConditionCategory.AuthoritativeTruth => Contains(context.authoritativeTruthIds, condition.requiredId),
                NarrativeConditionCategory.ActorKnowledge => Contains(context.knownSubjectIds, condition.requiredId),
                NarrativeConditionCategory.ParticipantKnowledge => Contains(context.knownSubjectIds, condition.requiredId),
                NarrativeConditionCategory.InstitutionalKnowledge => Contains(context.knownSubjectIds, condition.requiredId),
                NarrativeConditionCategory.Belief => Contains(context.beliefIds, condition.requiredId),
                NarrativeConditionCategory.QuestState => Contains(context.questStateIds, condition.requiredId),
                NarrativeConditionCategory.DialogueState => Contains(context.dialogueStateIds, condition.requiredId),
                NarrativeConditionCategory.LocationState => Contains(context.locationStateIds, condition.requiredId) || string.Equals(context.locationId, condition.requiredId, StringComparison.Ordinal),
                NarrativeConditionCategory.ItemState => Contains(context.itemStateIds, condition.requiredId) || string.Equals(context.itemId, condition.requiredId, StringComparison.Ordinal),
                NarrativeConditionCategory.CharacterState => Contains(context.characterStateIds, condition.requiredId),
                NarrativeConditionCategory.OrganizationState => Contains(context.organizationStateIds, condition.requiredId) || string.Equals(context.organizationId, condition.requiredId, StringComparison.Ordinal),
                NarrativeConditionCategory.SocialState => Contains(context.socialStateIds, condition.requiredId),
                NarrativeConditionCategory.EconomicState => Contains(context.economicStateIds, condition.requiredId),
                NarrativeConditionCategory.LegalState => Contains(context.legalStateIds, condition.requiredId),
                NarrativeConditionCategory.HistoricalState => Contains(context.historicalStateIds, condition.requiredId),
                NarrativeConditionCategory.NarrativeState => Contains(context.narrativeStateIds, condition.requiredId) || (integrations?.NarrativeStateConditionEvaluator?.Invoke(condition.Clone(), context.Clone()) ?? false),
                NarrativeConditionCategory.NarrativeArc => (integrations?.NarrativeArcConditionEvaluator?.Invoke(condition.Clone(), context.Clone()) ?? false),
                NarrativeConditionCategory.TimeState => context.worldTime >= condition.minimumValue,
                NarrativeConditionCategory.Custom => Contains(context.customStateIds, condition.requiredId),
                _ => false
            };

            if (condition.negate) matched = !matched;
            return new NarrativeConditionResultData
            {
                conditionDefinitionId = condition.conditionDefinitionId,
                category = condition.category,
                subjectId = condition.requiredId,
                sourceRuntime = OwnerRuntime(condition.category),
                matched = matched,
                hidden = condition.hidden,
                reason = matched ? "Matched" : condition.revealFailure ? "Condition did not match" : "Hidden"
            };
        }

        private static bool ConditionsMatched(NarrativeEventDefinitionData definition, IReadOnlyList<NarrativeConditionResultData> results)
        {
            if (results == null || results.Count == 0) return true;
            return definition.conditionGroupPolicy switch
            {
                NarrativeConditionGroupPolicy.Any => results.Any(value => value.matched),
                NarrativeConditionGroupPolicy.None => results.All(value => !value.matched),
                NarrativeConditionGroupPolicy.AtLeastN => results.Count(value => value.matched) >= Math.Max(1, definition.atLeastConditionCount),
                _ => results.All(value => value.matched)
            };
        }

        private NarrativeEventOperationResult ChangeLifecycle(NarrativeEventMutationRequest request, NarrativeEventLifecycle target, string operation, string message)
        {
            if (disposed) return Fail(NarrativeOperationStatus.Disposed, "Narrative event runtime is disposed.");
            request ??= new NarrativeEventMutationRequest();
            if (!ValidateRevision(request.expectedRevision, out NarrativeEventOperationResult revisionFailure)) return revisionFailure;
            if (TryDuplicate(request.transactionId, out NarrativeEventOperationResult duplicate)) return duplicate;
            if (!TryResolveEvent(request.narrativeEventId, request.eventDefinitionId, request.context, out NarrativeEventRecordData record, out NarrativeEventDefinitionData definition, out NarrativeEventOperationResult failure)) return failure;

            NarrativeEventRecordData changed = record.Clone();
            changed.lifecycle = target;
            if (target == NarrativeEventLifecycle.Armed) changed.armTime = request.worldTime;
            changed.revision++;
            if (request.preview) return NarrativeEventOperationResult.Success($"{message} Preview.", revision, revision, Snapshot(changed, definition), preview: true);

            long before = revision;
            CommitRecord(changed);
            revision++;
            RecordTransaction(request.transactionId, operation, changed.narrativeEventId, NarrativeOperationStatus.Succeeded);
            return NarrativeEventOperationResult.Success(message, before, revision, Snapshot(changed, definition));
        }

        private bool TryResolveEvent(string eventId, string definitionId, NarrativeConditionContextData context, out NarrativeEventRecordData record, out NarrativeEventDefinitionData definition, out NarrativeEventOperationResult failure)
        {
            record = null;
            definition = null;
            failure = null;
            string id = N(eventId);
            if (!string.IsNullOrWhiteSpace(id) && eventsById.TryGetValue(id, out NarrativeEventRecordData existing))
            {
                record = existing.Clone();
                return TryResolveDefinition(record.eventDefinitionId, out definition, out failure);
            }

            if (!TryResolveDefinition(definitionId, out definition, out failure)) return false;
            string scopeKey = ResolveScopeKey(definition, context, string.Empty);
            string scoped = BuildDefinitionScopeKey(definition.eventDefinitionId, scopeKey);
            if (eventByDefinitionScope.TryGetValue(scoped, out string scopedId) && eventsById.TryGetValue(scopedId, out existing))
            {
                record = existing.Clone();
                return true;
            }

            failure = Fail(NarrativeOperationStatus.InvalidRequest, "Narrative event record is missing.");
            return false;
        }

        private bool TryResolveDefinition(string definitionId, out NarrativeEventDefinitionData definition, out NarrativeEventOperationResult failure)
        {
            definition = null;
            failure = null;
            if (registry == null)
            {
                failure = Fail(NarrativeOperationStatus.MissingDefinitionRegistry, "Narrative event runtime has no definition registry.");
                return false;
            }

            if (!registry.TryGet(N(definitionId), out NarrativeEventDefinition asset))
            {
                failure = Fail(NarrativeOperationStatus.MissingDefinition, $"NarrativeEventDefinition '{N(definitionId)}' is missing.");
                return false;
            }

            definition = asset.ToRecordData();
            NarrativeEventValidationReport report = NarrativeEventDefinitionValidator.Validate(definition, registry.DefinitionsById);
            if (!report.Succeeded)
            {
                failure = Fail(NarrativeOperationStatus.DefinitionInvalid, string.Join(" | ", report.Errors));
                return false;
            }

            return true;
        }

        private IReadOnlyList<NarrativeEventDefinitionData> CandidateDefinitions(NarrativeTriggerCategory category)
        {
            if (registry == null) return Array.Empty<NarrativeEventDefinitionData>();
            if (!definitionIdsByTrigger.TryGetValue(category, out List<string> ids)) return Array.Empty<NarrativeEventDefinitionData>();
            return ids.Select(id => registry.TryGet(id, out NarrativeEventDefinition definition) ? definition.ToRecordData() : null)
                .Where(value => value != null)
                .ToArray();
        }

        private void RebuildIndexes()
        {
            definitionIdsByTrigger.Clear();
            if (registry == null) return;
            foreach (NarrativeEventDefinition definition in registry.DefinitionsById.Values.OfType<NarrativeEventDefinition>())
            {
                NarrativeEventDefinitionData data = definition.ToRecordData();
                foreach (NarrativeTriggerDefinitionData trigger in data.triggers ?? Array.Empty<NarrativeTriggerDefinitionData>())
                {
                    if (!definitionIdsByTrigger.TryGetValue(trigger.category, out List<string> list))
                    {
                        list = new List<string>();
                        definitionIdsByTrigger[trigger.category] = list;
                    }

                    if (!list.Contains(data.eventDefinitionId)) list.Add(data.eventDefinitionId);
                }
            }

            foreach (List<string> list in definitionIdsByTrigger.Values) list.Sort(StringComparer.Ordinal);
        }

        private void CommitRecord(NarrativeEventRecordData record)
        {
            eventsById[record.narrativeEventId] = record.Clone();
            eventByDefinitionScope[BuildDefinitionScopeKey(record.eventDefinitionId, record.scopeKey)] = record.narrativeEventId;
        }

        private NarrativeEventRecordData CreateRecord(NarrativeEventDefinitionData definition, string scopeKey, NarrativeConditionContextData context, double worldTime, NarrativeTriggerSourceData source = null)
        {
            NarrativeConditionContextData data = context?.Clone() ?? new NarrativeConditionContextData();
            NarrativeTriggerSourceData trigger = source?.Clone() ?? new NarrativeTriggerSourceData { worldTime = worldTime };
            string eventId = BuildEventId(definition.eventDefinitionId, scopeKey);
            return new NarrativeEventRecordData
            {
                narrativeEventId = eventId,
                eventDefinitionId = definition.eventDefinitionId,
                worldId = worldId,
                lifecycle = NarrativeEventLifecycle.Created,
                scope = definition.scope,
                scopeKey = scopeKey,
                actorPersonId = First(data.actorPersonId, trigger.actorPersonId),
                questId = data.questId,
                conversationId = data.conversationId,
                locationId = First(data.locationId, trigger.targetId),
                organizationId = data.organizationId,
                subjectId = First(data.subjectId, trigger.subjectId),
                visibility = definition.visibility,
                armTime = definition.armingPolicy == NarrativeArmingPolicy.OnWorldInitialization ? worldTime : -1d,
                provenanceId = trigger.sourceId,
                sourceLineageId = trigger.sourceTransactionId,
                revision = 1L
            };
        }

        private string ResolveScopeKey(NarrativeEventDefinitionData definition, NarrativeConditionContextData context, string explicitScopeKey, NarrativeTriggerSourceData source = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitScopeKey)) return N(explicitScopeKey);
            NarrativeConditionContextData data = context?.Clone() ?? new NarrativeConditionContextData();
            return definition.scope switch
            {
                NarrativeEventScope.OncePerWorld => worldId,
                NarrativeEventScope.OncePerPerson => First(data.actorPersonId, source?.actorPersonId, data.subjectId),
                NarrativeEventScope.OncePerQuest => First(data.questId, source?.targetId),
                NarrativeEventScope.OncePerConversation => First(data.conversationId, source?.targetId),
                NarrativeEventScope.OncePerLocationPlaceholder => First(data.locationId, source?.targetId),
                NarrativeEventScope.PerSubject => First(data.subjectId, source?.subjectId, source?.targetId),
                NarrativeEventScope.Repeatable => $"{worldId}:{source?.StableOccurrenceKey ?? BuildRepeatableScopeKey(definition)}",
                _ => First(data.subjectId, source?.subjectId, worldId)
            };
        }

        private static bool TriggerMatches(NarrativeEventDefinitionData definition, NarrativeTriggerSourceData source)
        {
            return (definition.triggers ?? Array.Empty<NarrativeTriggerDefinitionData>()).Any(trigger =>
                trigger.category == source.category
                && (string.IsNullOrWhiteSpace(trigger.requiredSourceId) || string.Equals(trigger.requiredSourceId, source.sourceId, StringComparison.Ordinal))
                && (string.IsNullOrWhiteSpace(trigger.requiredSubjectId) || string.Equals(trigger.requiredSubjectId, source.subjectId, StringComparison.Ordinal) || string.Equals(trigger.requiredSubjectId, source.targetId, StringComparison.Ordinal)));
        }

        private static bool InActivationWindow(NarrativeEventDefinitionData definition, double worldTime)
        {
            if (definition.activationStartTime >= 0d && worldTime < definition.activationStartTime) return false;
            if (definition.activationEndTime >= 0d && worldTime > definition.activationEndTime) return false;
            return true;
        }

        private static bool IsHardTriggerFailure(NarrativeOperationStatus status)
        {
            return status == NarrativeOperationStatus.ActionFailed
                || status == NarrativeOperationStatus.AtomicityRejected
                || status == NarrativeOperationStatus.DefinitionInvalid
                || status == NarrativeOperationStatus.MissingRuntimeIntegration
                || status == NarrativeOperationStatus.PersistenceInvalid
                || status == NarrativeOperationStatus.RestoreFailed
                || status == NarrativeOperationStatus.RevisionConflict
                || status == NarrativeOperationStatus.WrongWorld
                || status == NarrativeOperationStatus.Disposed;
        }

        private int MaxCascadeDepthFor(NarrativeTriggerSourceData source)
        {
            int configured = registry?.DefinitionsById.Values.OfType<NarrativeEventDefinition>().Select(value => value.ToRecordData().cascadeDepthLimit).DefaultIfEmpty(8).Max() ?? 8;
            return Math.Max(1, configured);
        }

        private bool ValidateRevision(long expectedRevision, out NarrativeEventOperationResult failure)
        {
            failure = null;
            if (expectedRevision >= 0L && expectedRevision != revision)
            {
                failure = Fail(NarrativeOperationStatus.RevisionConflict, $"Expected revision {expectedRevision}, actual {revision}.");
                return false;
            }

            return true;
        }

        private bool TryDuplicate(string transactionId, out NarrativeEventOperationResult duplicate)
        {
            duplicate = null;
            string id = N(transactionId);
            if (string.IsNullOrWhiteSpace(id) || !transactionsById.TryGetValue(id, out NarrativeRuntimeTransactionData transaction)) return false;
            NarrativeEventSnapshot snapshot = eventsById.TryGetValue(transaction.narrativeEventId, out NarrativeEventRecordData record) ? Snapshot(record, null) : null;
            duplicate = NarrativeEventOperationResult.Success("Duplicate narrative transaction ignored.", revision, revision, snapshot, duplicate: true);
            return true;
        }

        private void RecordTransaction(string transactionId, string operation, string eventId, NarrativeOperationStatus status)
        {
            string id = N(transactionId);
            if (string.IsNullOrWhiteSpace(id)) return;
            transactionsById[id] = new NarrativeRuntimeTransactionData { transactionId = id, operation = operation ?? string.Empty, narrativeEventId = eventId ?? string.Empty, status = status, runtimeRevision = revision };
        }

        private NarrativeEventSnapshot Snapshot(NarrativeEventRecordData record, NarrativeEventDefinitionData definition, bool development = true)
        {
            if (definition == null && registry != null && registry.TryGet(record.eventDefinitionId, out NarrativeEventDefinition asset)) definition = asset.ToRecordData();
            return new NarrativeEventSnapshot(record, definition, development);
        }

        private NarrativeEventOperationResult Fail(NarrativeOperationStatus status, string message, NarrativeEventSnapshot snapshot = null)
        {
            return NarrativeEventOperationResult.Failure(status, message, revision, snapshot);
        }

        private static NarrativeTriggerSourceData SourceFromSignal(NarrativeSignalRecordData signal)
        {
            return new NarrativeTriggerSourceData
            {
                category = NarrativeTriggerCategory.ExplicitSignal,
                sourceId = signal.signalDefinitionId,
                sourceTransactionId = signal.sourceTransactionId,
                actorPersonId = signal.actorPersonId,
                subjectId = signal.subjectIds.FirstOrDefault() ?? string.Empty,
                ownerRuntime = "NarrativeEventRuntime",
                worldTime = signal.worldTime,
                committed = true
            };
        }

        private static ConversationStartRequest BuildConversationStartRequest(ConversationDefinition definition, NarrativeActionDefinitionData action, NarrativeEventRecordData record, NarrativeConditionContextData context, string transactionId, string conversationId)
        {
            string hostLocationId = First(record?.locationId, context?.locationId, "location.prototype.narrative");
            string hostInteractionPointId = definition != null && definition.CoLocationPolicy == ConversationCoLocationPolicy.SameInteractionPoint
                ? First(record?.subjectId, context?.subjectId, "interaction-point.prototype.narrative")
                : string.Empty;

            return new ConversationStartRequest
            {
                transactionId = $"{transactionId}.{action.actionDefinitionId}.conversation",
                conversationId = conversationId,
                conversationDefinitionId = N(definition?.Id),
                participants = BuildConversationParticipants(definition, record, hostLocationId, hostInteractionPointId),
                subjectLinks = BuildConversationSubjectLinks(record, hostLocationId, hostInteractionPointId),
                activeSpeakerPersonId = First(record?.actorPersonId, "person.narrative.actor"),
                hostLocationId = hostLocationId,
                hostInteractionPointId = hostInteractionPointId,
                questId = N(record?.questId),
                operatingOrganizationId = ResolveConversationProviderId(definition, ConversationProviderRequirementKind.Organization, context, action),
                operatingOfficeId = ResolveConversationProviderId(definition, ConversationProviderRequirementKind.Office, context, action),
                operatingGovernmentId = ResolveConversationProviderId(definition, ConversationProviderRequirementKind.Government, context, action),
                operatingFactionId = ResolveConversationProviderId(definition, ConversationProviderRequirementKind.Faction, context, action),
                operatingBusinessId = ResolveConversationProviderId(definition, ConversationProviderRequirementKind.Business, context, action),
                tagIds = BuildConversationAuthorityTags(definition),
                worldTime = record?.triggerTime ?? context?.worldTime ?? 0d,
                provenanceId = N(record?.narrativeEventId)
            };
        }

        private static ConversationParticipantRecordData[] BuildConversationParticipants(ConversationDefinition definition, NarrativeEventRecordData record, string hostLocationId, string hostInteractionPointId)
        {
            List<ConversationParticipantRecordData> participants = new List<ConversationParticipantRecordData>();
            IReadOnlyList<ConversationParticipantRole> requiredRoles = definition?.RequiredRoles ?? Array.Empty<ConversationParticipantRole>();
            if (requiredRoles.Count == 0)
            {
                requiredRoles = new[] { ConversationParticipantRole.Initiator, ConversationParticipantRole.Listener };
            }

            foreach (ConversationParticipantRole role in requiredRoles.Where(value => value != ConversationParticipantRole.Unknown).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal))
            {
                participants.Add(new ConversationParticipantRecordData
                {
                    personId = ConversationPersonIdForRole(role, record),
                    role = role,
                    currentLocationId = hostLocationId,
                    currentInteractionPointId = hostInteractionPointId,
                    provenanceId = ConversationParticipantProvenanceForRole(role, definition),
                    representedOrganizationId = role == ConversationParticipantRole.OrganizationRepresentative ? FirstOrganizationProvider(definition) : string.Empty,
                    representedOfficeId = role == ConversationParticipantRole.OfficeHolder ? FirstProviderId(definition, ConversationProviderRequirementKind.Office) : string.Empty,
                    representedGovernmentId = FirstProviderId(definition, ConversationProviderRequirementKind.Government),
                    representedFactionId = FirstProviderId(definition, ConversationProviderRequirementKind.Faction),
                    representedBusinessId = FirstProviderId(definition, ConversationProviderRequirementKind.Business)
                });
            }

            if ((definition?.ProviderRequirements ?? Array.Empty<ConversationProviderRequirementData>()).Any(value => value.kind == ConversationProviderRequirementKind.Person)
                && participants.All(value => value.role != ConversationParticipantRole.Provider))
            {
                participants.Add(new ConversationParticipantRecordData
                {
                    personId = FirstProviderId(definition, ConversationProviderRequirementKind.Person),
                    role = ConversationParticipantRole.Provider,
                    currentLocationId = hostLocationId,
                    currentInteractionPointId = hostInteractionPointId,
                    provenanceId = N(record?.narrativeEventId)
                });
            }

            return participants.ToArray();
        }

        private static ConversationSubjectLinkData[] BuildConversationSubjectLinks(NarrativeEventRecordData record, string hostLocationId, string hostInteractionPointId)
        {
            List<ConversationSubjectLinkData> links = new List<ConversationSubjectLinkData>();
            string provenanceId = N(record?.narrativeEventId);
            if (!string.IsNullOrWhiteSpace(hostLocationId))
            {
                links.Add(new ConversationSubjectLinkData
                {
                    role = ConversationSubjectRole.Location,
                    locationId = hostLocationId,
                    subject = new InformationSubjectReferenceData { subjectType = InformationSubjectType.Location, subjectId = hostLocationId },
                    provenanceId = provenanceId
                });
            }

            if (!string.IsNullOrWhiteSpace(hostInteractionPointId))
            {
                links.Add(new ConversationSubjectLinkData
                {
                    role = ConversationSubjectRole.InteractionPoint,
                    interactionPointId = hostInteractionPointId,
                    subject = new InformationSubjectReferenceData { subjectType = InformationSubjectType.Custom, subjectId = hostInteractionPointId },
                    provenanceId = provenanceId
                });
            }

            string subjectId = N(record?.subjectId);
            if (!string.IsNullOrWhiteSpace(subjectId) && !string.Equals(subjectId, hostLocationId, StringComparison.Ordinal) && !string.Equals(subjectId, hostInteractionPointId, StringComparison.Ordinal))
            {
                links.Add(new ConversationSubjectLinkData
                {
                    role = ConversationSubjectRole.Information,
                    subject = new InformationSubjectReferenceData { subjectType = InformationSubjectType.Custom, subjectId = subjectId },
                    provenanceId = provenanceId
                });
            }

            return links.ToArray();
        }

        private static string[] BuildConversationAuthorityTags(ConversationDefinition definition)
        {
            return (definition?.AuthorityRequirementIds ?? Array.Empty<string>())
                .Concat((definition?.ProviderRequirements ?? Array.Empty<ConversationProviderRequirementData>())
                    .Where(value => value != null && value.kind == ConversationProviderRequirementKind.Authority)
                    .Select(value => value.requirementId))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(N)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string ResolveConversationProviderId(ConversationDefinition definition, ConversationProviderRequirementKind kind, NarrativeConditionContextData context, NarrativeActionDefinitionData action)
        {
            string explicitTarget = N(action?.secondaryTargetId);
            if (!string.IsNullOrWhiteSpace(explicitTarget) && ConversationProviderKindForId(explicitTarget) == kind) return explicitTarget;
            string contextual = kind switch
            {
                ConversationProviderRequirementKind.Organization => N(context?.organizationId),
                ConversationProviderRequirementKind.Government => N(context?.governmentId),
                _ => string.Empty
            };
            return string.IsNullOrWhiteSpace(contextual) ? FirstProviderId(definition, kind) : contextual;
        }

        private static string ConversationPersonIdForRole(ConversationParticipantRole role, NarrativeEventRecordData record)
        {
            return role switch
            {
                ConversationParticipantRole.Initiator => First(record?.actorPersonId, "person.narrative.actor"),
                ConversationParticipantRole.Prisoner => "person.prototype.prisoner",
                ConversationParticipantRole.Guard => "person.prototype.guard",
                ConversationParticipantRole.Addressee => "person.prototype.addressee",
                ConversationParticipantRole.Provider => "person.prototype.provider",
                ConversationParticipantRole.OfficeHolder => "person.prototype.office-holder",
                ConversationParticipantRole.OrganizationRepresentative => "person.prototype.organization-representative",
                ConversationParticipantRole.Merchant => "person.prototype.merchant",
                ConversationParticipantRole.QuestGiver => "person.prototype.quest-giver",
                ConversationParticipantRole.QuestRecipient => First(record?.actorPersonId, "person.narrative.actor"),
                _ => $"person.prototype.{NarrativeModelUtility.SanitizeForId(role.ToString())}"
            };
        }

        private static string ConversationParticipantProvenanceForRole(ConversationParticipantRole role, ConversationDefinition definition)
        {
            return role == ConversationParticipantRole.Guard
                ? FirstProviderId(definition, ConversationProviderRequirementKind.Authority)
                : string.Empty;
        }

        private static string FirstOrganizationProvider(ConversationDefinition definition)
        {
            return First(FirstProviderId(definition, ConversationProviderRequirementKind.Organization), FirstProviderId(definition, ConversationProviderRequirementKind.OrganizationMembership));
        }

        private static string FirstProviderId(ConversationDefinition definition, ConversationProviderRequirementKind kind)
        {
            return (definition?.ProviderRequirements ?? Array.Empty<ConversationProviderRequirementData>())
                .Where(value => value != null && value.kind == kind)
                .Select(value => N(value.requirementId))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static ConversationProviderRequirementKind ConversationProviderKindForId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return ConversationProviderRequirementKind.Unknown;
            if (id.StartsWith("person.", StringComparison.Ordinal)) return ConversationProviderRequirementKind.Person;
            if (id.StartsWith("organization.", StringComparison.Ordinal)) return ConversationProviderRequirementKind.Organization;
            if (id.StartsWith("office.", StringComparison.Ordinal)) return ConversationProviderRequirementKind.Office;
            if (id.StartsWith("government.", StringComparison.Ordinal)) return ConversationProviderRequirementKind.Government;
            if (id.StartsWith("faction.", StringComparison.Ordinal)) return ConversationProviderRequirementKind.Faction;
            if (id.StartsWith("business.", StringComparison.Ordinal)) return ConversationProviderRequirementKind.Business;
            if (id.StartsWith("authority.", StringComparison.Ordinal)) return ConversationProviderRequirementKind.Authority;
            return ConversationProviderRequirementKind.Custom;
        }

        private static QuestIssuerReferenceData BuildQuestIssuer(QuestDefinition definition, NarrativeActionDefinitionData action, NarrativeEventRecordData record, NarrativeConditionContextData context)
        {
            IReadOnlyList<QuestIssuerType> supported = definition?.SupportedIssuerTypes ?? Array.Empty<QuestIssuerType>();
            string explicitIssuerId = N(action?.secondaryTargetId);
            QuestIssuerType explicitType = InferQuestIssuerType(explicitIssuerId);
            QuestIssuerType issuerType = explicitType != QuestIssuerType.Unknown && (supported.Count == 0 || supported.Contains(explicitType))
                ? explicitType
                : ChooseQuestIssuerType(supported, context);

            string issuerId = explicitIssuerId;
            if (string.IsNullOrWhiteSpace(issuerId) || InferQuestIssuerType(issuerId) != issuerType)
            {
                issuerId = DefaultQuestIssuerId(issuerType, context);
            }

            if (issuerType == QuestIssuerType.Unknown)
            {
                issuerType = QuestIssuerType.System;
            }

            return new QuestIssuerReferenceData
            {
                issuerType = issuerType,
                issuerId = RequiresQuestIssuerId(issuerType) ? issuerId : string.Empty,
                actingPersonId = N(record?.actorPersonId),
                provenanceId = N(record?.narrativeEventId)
            };
        }

        private static QuestRecipientReferenceData BuildQuestRecipient(QuestDefinition definition, NarrativeEventRecordData record)
        {
            IReadOnlyList<QuestRecipientScope> supported = definition?.SupportedRecipientScopes ?? Array.Empty<QuestRecipientScope>();
            string actorId = N(record?.actorPersonId);
            if (!string.IsNullOrWhiteSpace(actorId) && (supported.Count == 0 || supported.Contains(QuestRecipientScope.Person)))
            {
                return new QuestRecipientReferenceData
                {
                    recipientScope = QuestRecipientScope.Person,
                    recipientId = actorId,
                    provenanceId = N(record?.narrativeEventId)
                };
            }

            QuestRecipientScope scope = supported.Contains(QuestRecipientScope.Open) || supported.Count == 0
                ? QuestRecipientScope.Open
                : supported.FirstOrDefault(value => value != QuestRecipientScope.Unknown);
            return new QuestRecipientReferenceData
            {
                recipientScope = scope == QuestRecipientScope.Unknown ? QuestRecipientScope.Open : scope,
                provenanceId = N(record?.narrativeEventId)
            };
        }

        private static QuestOriginReferenceData BuildQuestOrigin(QuestDefinition definition, NarrativeEventRecordData record)
        {
            QuestSourceChannel channel = definition == null || definition.DefaultSourceChannel == QuestSourceChannel.Unknown
                ? QuestSourceChannel.WorldEvent
                : definition.DefaultSourceChannel;
            return new QuestOriginReferenceData
            {
                sourceChannel = channel,
                locationId = N(record?.locationId),
                provenanceId = N(record?.narrativeEventId)
            };
        }

        private static QuestSubjectLinkData[] BuildQuestSubjectLinks(NarrativeEventRecordData record)
        {
            List<QuestSubjectLinkData> links = new List<QuestSubjectLinkData>();
            string provenanceId = N(record?.narrativeEventId);
            string locationId = N(record?.locationId);
            if (!string.IsNullOrWhiteSpace(locationId))
            {
                links.Add(new QuestSubjectLinkData
                {
                    linkId = $"quest-subject.{NarrativeModelUtility.SanitizeForId(provenanceId)}.location",
                    role = QuestSubjectRole.Location,
                    subject = new InformationSubjectReferenceData { subjectType = InformationSubjectType.Location, subjectId = locationId },
                    provenanceId = provenanceId
                });
            }

            string subjectId = N(record?.subjectId);
            if (!string.IsNullOrWhiteSpace(subjectId) && !string.Equals(subjectId, locationId, StringComparison.Ordinal))
            {
                links.Add(new QuestSubjectLinkData
                {
                    linkId = $"quest-subject.{NarrativeModelUtility.SanitizeForId(provenanceId)}.context",
                    role = QuestSubjectRole.Context,
                    subject = new InformationSubjectReferenceData { subjectType = InformationSubjectType.Custom, subjectId = subjectId },
                    provenanceId = provenanceId
                });
            }

            return links.ToArray();
        }

        private static QuestIssuerType ChooseQuestIssuerType(IReadOnlyList<QuestIssuerType> supported, NarrativeConditionContextData context)
        {
            if (supported == null || supported.Count == 0)
            {
                return QuestIssuerType.System;
            }

            QuestIssuerType[] contextPriority =
            {
                !string.IsNullOrWhiteSpace(context?.organizationId) ? QuestIssuerType.Organization : QuestIssuerType.Unknown,
                !string.IsNullOrWhiteSpace(context?.governmentId) ? QuestIssuerType.Government : QuestIssuerType.Unknown,
                QuestIssuerType.System,
                QuestIssuerType.Anonymous,
                QuestIssuerType.Organization,
                QuestIssuerType.Government,
                QuestIssuerType.Office,
                QuestIssuerType.Faction,
                QuestIssuerType.Business,
                QuestIssuerType.Person,
                QuestIssuerType.Custom
            };

            foreach (QuestIssuerType candidate in contextPriority)
            {
                if (candidate != QuestIssuerType.Unknown && supported.Contains(candidate))
                {
                    return candidate;
                }
            }

            return supported.FirstOrDefault(value => value != QuestIssuerType.Unknown);
        }

        private static QuestIssuerType InferQuestIssuerType(string issuerId)
        {
            if (string.IsNullOrWhiteSpace(issuerId)) return QuestIssuerType.Unknown;
            if (issuerId.StartsWith("person.", StringComparison.Ordinal)) return QuestIssuerType.Person;
            if (issuerId.StartsWith("organization.", StringComparison.Ordinal)) return QuestIssuerType.Organization;
            if (issuerId.StartsWith("office.", StringComparison.Ordinal)) return QuestIssuerType.Office;
            if (issuerId.StartsWith("government.", StringComparison.Ordinal)) return QuestIssuerType.Government;
            if (issuerId.StartsWith("faction.", StringComparison.Ordinal)) return QuestIssuerType.Faction;
            if (issuerId.StartsWith("business.", StringComparison.Ordinal)) return QuestIssuerType.Business;
            if (issuerId.StartsWith("system.", StringComparison.Ordinal)) return QuestIssuerType.System;
            if (issuerId.StartsWith("anonymous.", StringComparison.Ordinal)) return QuestIssuerType.Anonymous;
            return QuestIssuerType.Unknown;
        }

        private static string DefaultQuestIssuerId(QuestIssuerType issuerType, NarrativeConditionContextData context)
        {
            return issuerType switch
            {
                QuestIssuerType.Person => N(context?.actorPersonId),
                QuestIssuerType.Organization => string.IsNullOrWhiteSpace(context?.organizationId) ? "organization.prototype.guild" : N(context.organizationId),
                QuestIssuerType.Office => "office.prototype.guild-clerk",
                QuestIssuerType.Government => string.IsNullOrWhiteSpace(context?.governmentId) ? "government.prototype.city" : N(context.governmentId),
                QuestIssuerType.Faction => "faction.prototype.hidden",
                QuestIssuerType.Business => "business.prototype.merchant",
                QuestIssuerType.Custom => "issuer.prototype.narrative",
                _ => string.Empty
            };
        }

        private static bool RequiresQuestIssuerId(QuestIssuerType issuerType)
        {
            return issuerType != QuestIssuerType.System && issuerType != QuestIssuerType.Anonymous;
        }

        private static string ResolveActionTarget(NarrativeActionDefinitionData action, IDictionary<string, string> outputSlots)
        {
            if (!string.IsNullOrWhiteSpace(action.inputSlotId) && outputSlots != null && outputSlots.TryGetValue(action.inputSlotId, out string value)) return value;
            return N(action.targetId);
        }

        private static string PreviewResultValue(NarrativeEventRecordData record, NarrativeActionDefinitionData action, string target)
        {
            return action.category == NarrativeActionCategory.InstantiateQuest
                ? $"quest.narrative.{NarrativeModelUtility.SanitizeForId(record.narrativeEventId)}.{NarrativeModelUtility.SanitizeForId(action.actionDefinitionId)}"
                : target;
        }

        private static string OwnerRuntime(NarrativeActionCategory category)
        {
            return category switch
            {
                NarrativeActionCategory.InstantiateQuest or NarrativeActionCategory.SuspendQuest or NarrativeActionCategory.RetireQuest => "QuestRuntime",
                NarrativeActionCategory.PublishQuestListing or NarrativeActionCategory.CreateQuestOffer or NarrativeActionCategory.DirectAssignQuest => "QuestSourceRuntime",
                NarrativeActionCategory.StartConversation or NarrativeActionCategory.EndConversation => "ConversationRuntime",
                NarrativeActionCategory.EmitNarrativeSignal or NarrativeActionCategory.ArmNarrativeEvent or NarrativeActionCategory.DisarmNarrativeEvent or NarrativeActionCategory.ScheduleNarrativeEvent => "NarrativeEventRuntime",
                NarrativeActionCategory.GrantInformation or NarrativeActionCategory.CreateObservation or NarrativeActionCategory.HistoricalEventRequest => "Step8",
                NarrativeActionCategory.ActivateTravelCondition or NarrativeActionCategory.ResolveTravelCondition or NarrativeActionCategory.TriggerTravelEncounter => "TravelConditionRuntime",
                NarrativeActionCategory.RequestConnectionStateChange => "LocationConnectionRuntime",
                NarrativeActionCategory.TriggerSocialInteraction => "SocialInteractionRuntime",
                NarrativeActionCategory.RequestOrganizationMembership or NarrativeActionCategory.RequestRankChange or NarrativeActionCategory.RequestOfficeActionPlaceholder => "OrganizationMembershipRuntime",
                NarrativeActionCategory.RequestPermit or NarrativeActionCategory.CreateIncidentReport => "LegalRuntime",
                _ => "NarrativeEventRuntime"
            };
        }

        private static string OwnerRuntime(NarrativeConditionCategory category)
        {
            return category switch
            {
                NarrativeConditionCategory.AuthoritativeTruth or NarrativeConditionCategory.ActorKnowledge or NarrativeConditionCategory.ParticipantKnowledge or NarrativeConditionCategory.InstitutionalKnowledge or NarrativeConditionCategory.Belief or NarrativeConditionCategory.HistoricalState => "Step8",
                NarrativeConditionCategory.QuestState => "QuestRuntime",
                NarrativeConditionCategory.DialogueState => "DialogueFlowRuntime",
                NarrativeConditionCategory.LocationState => "LocationRuntime",
                NarrativeConditionCategory.ItemState => "ItemRuntime",
                NarrativeConditionCategory.OrganizationState => "OrganizationRuntime",
                NarrativeConditionCategory.SocialState => "SocialRuntime",
                NarrativeConditionCategory.EconomicState => "EconomyRuntime",
                NarrativeConditionCategory.LegalState => "LegalRuntime",
                _ => "NarrativeEventRuntime"
            };
        }

        private static string BuildEventId(string definitionId, string scopeKey) => $"narrative-event.{NarrativeModelUtility.SanitizeForId(definitionId)}.{NarrativeModelUtility.SanitizeForId(scopeKey)}";
        private static string BuildSignalId(string signalDefinitionId, string transactionId, int index) => $"narrative-signal.{NarrativeModelUtility.SanitizeForId(signalDefinitionId)}.{NarrativeModelUtility.SanitizeForId(transactionId)}.{index:0000}";
        private static string BuildActionExecutionId(string eventId, string actionId, int index) => $"narrative-action.{NarrativeModelUtility.SanitizeForId(eventId)}.{NarrativeModelUtility.SanitizeForId(actionId)}.{index:0000}";
        private static string BuildDefinitionScopeKey(string definitionId, string scopeKey) => $"{N(definitionId)}::{N(scopeKey)}";
        private string BuildRepeatableScopeKey(NarrativeEventDefinitionData definition) => $"{NarrativeModelUtility.SanitizeForId(definition.eventDefinitionId)}.{eventsById.Count + 1:0000}";
        private static bool Contains(IEnumerable<string> values, string expected) => (values ?? Array.Empty<string>()).Contains(N(expected), StringComparer.Ordinal);
        private static string First(params string[] values) => (values ?? Array.Empty<string>()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        private static string N(string value) => NarrativeModelUtility.N(value);
    }

    public sealed class NarrativeEventMutationRequest
    {
        public string transactionId;
        public string narrativeEventId;
        public string eventDefinitionId;
        public string scopeKey;
        public NarrativeConditionContextData context;
        public double worldTime;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class NarrativeTriggerRequest
    {
        public string transactionId;
        public string narrativeEventId;
        public string eventDefinitionId;
        public NarrativeTriggerSourceData source;
        public NarrativeConditionContextData conditionContext;
        public bool preview;
        public int cascadeDepth;
    }

    public sealed class NarrativeExecutionRequest
    {
        public string transactionId;
        public string narrativeEventId;
        public NarrativeConditionContextData conditionContext;
        public long expectedRevision = -1L;
        public bool preview;
        public int cascadeDepth;
    }

    public sealed class NarrativeSignalRequest
    {
        public string transactionId;
        public string signalId;
        public string signalDefinitionId;
        public NarrativeSignalSourceKind sourceKind = NarrativeSignalSourceKind.NarrativeSystem;
        public string sourceId;
        public string actorPersonId;
        public string[] subjectIds = Array.Empty<string>();
        public string provenanceId;
        public NarrativeConditionContextData conditionContext;
        public double worldTime;
        public long expectedRevision = -1L;
        public bool preview;
        public int cascadeDepth;
    }
}
