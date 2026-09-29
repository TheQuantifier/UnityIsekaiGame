using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Narrative
{
    public sealed class NarrativeStateRuntime : IDisposable
    {
        public const int CurrentSaveSchemaVersion = 1;

        private readonly Dictionary<string, NarrativeStateRecordData> statesById = new Dictionary<string, NarrativeStateRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> stateIdByScope = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, NarrativeStateTransitionRecordData> transitionByTransaction = new Dictionary<string, NarrativeStateTransitionRecordData>(StringComparer.Ordinal);
        private readonly List<NarrativeStateTransitionRecordData> transitions = new List<NarrativeStateTransitionRecordData>();

        private DefinitionRegistry registry;
        private NarrativeStateRuntimeIntegrations integrations;
        private string worldId;
        private long revision;
        private bool disposed;

        public NarrativeStateRuntime(DefinitionRegistry definitionRegistry = null, NarrativeStateRuntimeIntegrations runtimeIntegrations = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            Configure(definitionRegistry, runtimeIntegrations, runtimeWorldId);
        }

        public long Revision => revision;
        public string WorldId => worldId ?? string.Empty;
        public int MaterializedStateCount => statesById.Count;
        public int TransitionCount => transitions.Count;

        public void Configure(DefinitionRegistry definitionRegistry, NarrativeStateRuntimeIntegrations runtimeIntegrations = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            registry = definitionRegistry;
            integrations = runtimeIntegrations ?? new NarrativeStateRuntimeIntegrations();
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId.Trim();
            RebuildIndexes();
        }

        public NarrativeStateTransitionResult RequestTransition(NarrativeStateTransitionRequest request)
        {
            if (disposed) return Fail(NarrativeStateTransitionStatus.Disposed, "Narrative state runtime is disposed.");
            request = request?.Clone() ?? new NarrativeStateTransitionRequest();
            if (!ValidateRevision(request.expectedRevision, out NarrativeStateTransitionResult revisionFailure)) return revisionFailure;
            if (!string.IsNullOrWhiteSpace(request.transactionId) && transitionByTransaction.TryGetValue(request.transactionId, out NarrativeStateTransitionRecordData duplicate))
            {
                return NarrativeStateTransitionResult.Success("Narrative state transition was already applied.", revision, revision, Snapshot(statesById[duplicate.narrativeStateId], true), new NarrativeStateTransitionSnapshot(duplicate, true), duplicate.consequences, duplicate: true);
            }

            if (!TryResolveTransition(request, out NarrativeStateDefinitionData stateDefinition, out NarrativeStateTransitionDefinitionData transitionDefinition, out NarrativeVariableDefinitionData variableDefinition, out NarrativeStateTransitionResult failure)) return failure;
            string scopeKey = ResolveScopeKey(stateDefinition, request.scopeKey, request);
            if (string.IsNullOrWhiteSpace(scopeKey)) return Fail(NarrativeStateTransitionStatus.InvalidScope, "Narrative state transition scope key could not be resolved.");

            NarrativeStateRecordData current = ResolveRecord(stateDefinition, scopeKey, request.worldTime);
            NarrativeVariableValueData oldValue = ResolveValue(current, variableDefinition);
            if (!SourceValueAllowed(transitionDefinition, oldValue)) return Fail(NarrativeStateTransitionStatus.SourceValueMismatch, $"Current value '{oldValue.StableText}' is not an authored source value for transition '{transitionDefinition.transitionDefinitionId}'.", Snapshot(current, true));
            if (IsTerminal(variableDefinition, oldValue) && transitionDefinition.reentryPolicy == NarrativeTransitionReentryPolicy.RejectAfterTerminal) return Fail(NarrativeStateTransitionStatus.TerminalState, $"Current value '{oldValue.StableText}' is terminal.", Snapshot(current, true));
            if (transitionDefinition.reentryPolicy == NarrativeTransitionReentryPolicy.RejectSameValue && oldValue.SameValue(transitionDefinition.targetValue)) return Fail(NarrativeStateTransitionStatus.SourceValueMismatch, "Transition target matches current value and re-entry is rejected.", Snapshot(current, true));
            if (transitionDefinition.repeatPolicy == NarrativeTransitionRepeatPolicy.OncePerScope && transitions.Any(item => item.transitionDefinitionId == transitionDefinition.transitionDefinitionId && item.scopeKey == scopeKey && item.stateDefinitionId == stateDefinition.stateDefinitionId)) return Fail(NarrativeStateTransitionStatus.Duplicate, "Narrative transition has already occurred for this scope.", Snapshot(current, true));

            NarrativeConditionResultData[] conditions = EvaluateConditions(transitionDefinition.conditions, request.conditionContext).ToArray();
            if (conditions.Any(condition => !condition.matched)) return Fail(NarrativeStateTransitionStatus.ConditionFailed, "Narrative transition conditions did not match.", Snapshot(current, true));

            List<NarrativeActionExecutionRecordData> consequenceRecords = PrepareConsequences(transitionDefinition, request, current, conditions, out NarrativeStateTransitionResult consequenceFailure);
            if (consequenceFailure != null) return consequenceFailure;

            NarrativeStateRecordData changed = current.Clone();
            SetVariable(changed, variableDefinition.variableDefinitionId, transitionDefinition.targetValue, BuildTransitionId(request, transitionDefinition, current, transitions.Count + 1), request.worldTime);
            changed.lifecycle = NarrativeStateLifecycle.Active;
            changed.updatedWorldTime = request.worldTime;
            changed.revision++;

            NarrativeStateTransitionRecordData transition = BuildTransitionRecord(request, stateDefinition, transitionDefinition, variableDefinition, current, oldValue, transitionDefinition.targetValue, conditions, consequenceRecords, transitions.Count + 1);
            transition.revisionBefore = revision;
            transition.revisionAfter = request.preview ? revision : revision + 1;

            if (request.preview)
            {
                return NarrativeStateTransitionResult.Success("Narrative state transition previewed.", revision, revision, new NarrativeStateSnapshot(changed, stateDefinition, true), new NarrativeStateTransitionSnapshot(transition, true), consequenceRecords, preview: true);
            }

            NarrativeStateTransitionResult executionFailure;
            foreach (NarrativeActionExecutionRecordData consequence in ExecuteConsequences(transitionDefinition, request, consequenceRecords, out executionFailure))
            {
                ReplaceConsequence(consequenceRecords, consequence);
            }

            if (executionFailure != null) return executionFailure;

            long before = revision;
            transition.consequences = consequenceRecords.Select(value => value.Clone()).ToArray();
            CommitRecord(changed);
            transitions.Add(transition.Clone());
            if (!string.IsNullOrWhiteSpace(request.transactionId)) transitionByTransaction[request.transactionId] = transition.Clone();
            revision++;
            EmitStateChangedSignal(transition, request);
            return NarrativeStateTransitionResult.Success("Narrative state transition committed.", before, revision, new NarrativeStateSnapshot(changed, stateDefinition, true), new NarrativeStateTransitionSnapshot(transition, true), consequenceRecords);
        }

        public bool EvaluateCondition(NarrativeStateConditionQuery query)
        {
            query ??= new NarrativeStateConditionQuery();
            if (!TryResolveState(query.stateDefinitionId, out NarrativeStateDefinitionData stateDefinition, out _)) return false;
            NarrativeVariableDefinitionData variable = stateDefinition.variables.FirstOrDefault(item => string.Equals(item.variableDefinitionId, query.variableDefinitionId, StringComparison.Ordinal));
            if (variable == null) return false;
            string scopeKey = ResolveScopeKey(stateDefinition, query.scopeKey, new NarrativeStateTransitionRequest { scope = query.scope, scopeKey = query.scopeKey });
            NarrativeStateRecordData record = ResolveRecord(stateDefinition, scopeKey, 0d);
            NarrativeVariableValueData value = ResolveValue(record, variable);
            bool matched = query.expectedValue != null
                ? value.SameValue(query.expectedValue)
                : (value.kind == NarrativeVariableKind.Integer || value.kind == NarrativeVariableKind.SmallCounter) && value.intValue >= query.minimumValue && value.intValue <= query.maximumValue;
            return query.negate ? !matched : matched;
        }

        public bool TryGetSnapshot(string stateDefinitionId, NarrativeStateScope scope, string scopeKey, out NarrativeStateSnapshot snapshot, bool developmentView = true)
        {
            snapshot = null;
            if (!TryResolveState(stateDefinitionId, out NarrativeStateDefinitionData definition, out _)) return false;
            string resolvedScope = ResolveScopeKey(definition, scopeKey, new NarrativeStateTransitionRequest { scope = scope, scopeKey = scopeKey });
            snapshot = new NarrativeStateSnapshot(ResolveRecord(definition, resolvedScope, 0d), definition, developmentView);
            return true;
        }

        public IReadOnlyList<NarrativeStateSnapshot> Query(NarrativeStateQuery query)
        {
            query ??= new NarrativeStateQuery();
            IEnumerable<NarrativeStateDefinitionData> definitions = CandidateStateDefinitions()
                .Where(definition => string.IsNullOrWhiteSpace(query.stateDefinitionId) || string.Equals(definition.stateDefinitionId, query.stateDefinitionId, StringComparison.Ordinal))
                .Where(definition => !query.scope.HasValue || definition.scope == query.scope.Value)
                .OrderBy(definition => definition.stateDefinitionId, StringComparer.Ordinal);

            List<NarrativeStateSnapshot> snapshots = new List<NarrativeStateSnapshot>();
            foreach (NarrativeStateDefinitionData definition in definitions)
            {
                if (!string.IsNullOrWhiteSpace(query.scopeKey))
                {
                    snapshots.Add(new NarrativeStateSnapshot(ResolveRecord(definition, query.scopeKey, 0d), definition, query.developmentView));
                    continue;
                }

                string prefix = $"{definition.stateDefinitionId}|";
                foreach (NarrativeStateRecordData record in statesById.Values.Where(record => record.stateDefinitionId == definition.stateDefinitionId).OrderBy(record => record.scopeKey, StringComparer.Ordinal))
                {
                    snapshots.Add(new NarrativeStateSnapshot(record, definition, query.developmentView));
                }

                if (!statesById.Values.Any(record => record.stateDefinitionId == definition.stateDefinitionId) && !query.hideConcealedCounts)
                {
                    snapshots.Add(new NarrativeStateSnapshot(ResolveRecord(definition, DefaultScopeKey(definition.scope), 0d), definition, query.developmentView));
                }
            }

            return snapshots.ToArray();
        }

        public IReadOnlyList<NarrativeStateTransitionSnapshot> QueryTransitions(string stateDefinitionId = "", string scopeKey = "", bool developmentView = true)
        {
            return transitions
                .Where(item => string.IsNullOrWhiteSpace(stateDefinitionId) || item.stateDefinitionId == stateDefinitionId)
                .Where(item => string.IsNullOrWhiteSpace(scopeKey) || item.scopeKey == scopeKey)
                .OrderBy(item => item.worldTime)
                .ThenBy(item => item.sequence)
                .ThenBy(item => item.transitionId, StringComparer.Ordinal)
                .Select(item => new NarrativeStateTransitionSnapshot(item, developmentView))
                .ToArray();
        }

        public NarrativeVariableValueData ValueAt(string stateDefinitionId, string variableDefinitionId, NarrativeStateScope scope, string scopeKey, double worldTime)
        {
            if (!TryResolveState(stateDefinitionId, out NarrativeStateDefinitionData definition, out _)) return null;
            NarrativeVariableDefinitionData variable = definition.variables.FirstOrDefault(item => item.variableDefinitionId == variableDefinitionId);
            if (variable == null) return null;
            string resolvedScope = ResolveScopeKey(definition, scopeKey, new NarrativeStateTransitionRequest { scope = scope, scopeKey = scopeKey });
            NarrativeStateTransitionRecordData historical = transitions
                .Where(item => item.stateDefinitionId == stateDefinitionId && item.variableDefinitionId == variableDefinitionId && item.scopeKey == resolvedScope && item.worldTime <= worldTime)
                .OrderBy(item => item.worldTime)
                .ThenBy(item => item.sequence)
                .LastOrDefault();
            return historical?.newValue?.Clone() ?? variable.defaultValue.Clone();
        }

        public NarrativeStateRuntimeSaveData CreateSaveData()
        {
            return new NarrativeStateRuntimeSaveData
            {
                schemaVersion = CurrentSaveSchemaVersion,
                worldId = worldId,
                revision = revision,
                states = statesById.Values.Select(value => value.Clone()).OrderBy(value => value.narrativeStateId, StringComparer.Ordinal).ToArray(),
                transitions = transitions.Select(value => value.Clone()).OrderBy(value => value.sequence).ThenBy(value => value.transitionId, StringComparer.Ordinal).ToArray()
            };
        }

        public NarrativeStateTransitionResult RestoreFromSaveData(NarrativeStateRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, NarrativeStateRuntimeIntegrations runtimeIntegrations = null, string expectedWorldId = PersistenceService.LocalWorldId)
        {
            if (!ValidateSaveData(saveData, definitionRegistry ?? registry, expectedWorldId, out string failure)) return Fail(NarrativeStateTransitionStatus.PersistenceInvalid, failure);
            NarrativeStateRuntimeSaveData rollback = CreateSaveData();
            try
            {
                registry = definitionRegistry ?? registry;
                integrations = runtimeIntegrations ?? integrations ?? new NarrativeStateRuntimeIntegrations();
                worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
                statesById.Clear();
                transitions.Clear();
                transitionByTransaction.Clear();
                foreach (NarrativeStateRecordData state in saveData.states ?? Array.Empty<NarrativeStateRecordData>()) statesById[state.narrativeStateId] = state.Clone();
                foreach (NarrativeStateTransitionRecordData transition in (saveData.transitions ?? Array.Empty<NarrativeStateTransitionRecordData>()).OrderBy(value => value.sequence).ThenBy(value => value.transitionId, StringComparer.Ordinal))
                {
                    transitions.Add(transition.Clone());
                    if (!string.IsNullOrWhiteSpace(transition.sourceTransactionId)) transitionByTransaction[transition.sourceTransactionId] = transition.Clone();
                }

                revision = saveData.revision;
                RebuildIndexes();
                return new NarrativeStateTransitionResult(NarrativeStateTransitionStatus.Succeeded, "Narrative state restored.", revision, revision);
            }
            catch (Exception ex)
            {
                RestoreFromSaveData(rollback, registry, integrations, worldId);
                return Fail(NarrativeStateTransitionStatus.RestoreFailed, $"Narrative state restore failed: {ex.Message}");
            }
        }

        public static bool ValidateSaveData(NarrativeStateRuntimeSaveData saveData, DefinitionRegistry registry, string expectedWorldId, out string failure)
        {
            failure = string.Empty;
            saveData = saveData?.Clone();
            if (saveData == null)
            {
                failure = "Narrative state save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != CurrentSaveSchemaVersion)
            {
                failure = $"Unsupported narrative state schema version {saveData.schemaVersion}.";
                return false;
            }

            string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            if (!string.Equals(N(saveData.worldId), world, StringComparison.Ordinal))
            {
                failure = $"Narrative state world '{saveData.worldId}' does not match expected world '{world}'.";
                return false;
            }

            HashSet<string> states = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeStateRecordData state in saveData.states ?? Array.Empty<NarrativeStateRecordData>())
            {
                if (state == null || string.IsNullOrWhiteSpace(state.narrativeStateId))
                {
                    failure = "Narrative state record has no stable ID.";
                    return false;
                }

                if (!states.Add(state.narrativeStateId))
                {
                    failure = $"Duplicate NarrativeStateId '{state.narrativeStateId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(state.stateDefinitionId, out NarrativeStateDefinition definition))
                {
                    failure = $"NarrativeStateDefinition '{state.stateDefinitionId}' is missing.";
                    return false;
                }

                NarrativeStateDefinitionData data = definition.ToRecordData();
                foreach (NarrativeStateVariableRecordData variable in state.variables ?? Array.Empty<NarrativeStateVariableRecordData>())
                {
                    NarrativeVariableDefinitionData variableDefinition = data.variables.FirstOrDefault(item => item.variableDefinitionId == variable.variableDefinitionId);
                    if (variableDefinition == null)
                    {
                        failure = $"Narrative state '{state.narrativeStateId}' references missing variable '{variable.variableDefinitionId}'.";
                        return false;
                    }

                    if (!NarrativeStateDefinitionValidator.ValueMatchesKind(variable.value, variableDefinition.kind, variableDefinition, out string variableFailure))
                    {
                        failure = $"Narrative state '{state.narrativeStateId}' variable '{variable.variableDefinitionId}' has invalid value: {variableFailure}";
                        return false;
                    }
                }
            }

            int previousSequence = 0;
            foreach (NarrativeStateTransitionRecordData transition in saveData.transitions ?? Array.Empty<NarrativeStateTransitionRecordData>())
            {
                if (transition == null || string.IsNullOrWhiteSpace(transition.transitionId))
                {
                    failure = "Narrative transition record has no stable ID.";
                    return false;
                }

                if (!states.Contains(transition.narrativeStateId))
                {
                    failure = $"Narrative transition '{transition.transitionId}' references missing state '{transition.narrativeStateId}'.";
                    return false;
                }

                if (transition.sequence <= previousSequence)
                {
                    failure = $"Narrative transition '{transition.transitionId}' sequence ordering is invalid.";
                    return false;
                }

                previousSequence = transition.sequence;
            }

            return true;
        }

        public void Dispose()
        {
            disposed = true;
            statesById.Clear();
            stateIdByScope.Clear();
            transitionByTransaction.Clear();
            transitions.Clear();
        }

        private bool TryResolveTransition(NarrativeStateTransitionRequest request, out NarrativeStateDefinitionData stateDefinition, out NarrativeStateTransitionDefinitionData transitionDefinition, out NarrativeVariableDefinitionData variableDefinition, out NarrativeStateTransitionResult failure)
        {
            stateDefinition = null;
            transitionDefinition = null;
            variableDefinition = null;
            failure = null;
            if (registry == null)
            {
                failure = Fail(NarrativeStateTransitionStatus.MissingDefinitionRegistry, "Narrative state runtime has no definition registry.");
                return false;
            }

            foreach (NarrativeStateDefinition definition in registry.DefinitionsById.Values.OfType<NarrativeStateDefinition>().OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                NarrativeStateDefinitionData data = definition.ToRecordData();
                if (!string.IsNullOrWhiteSpace(request.stateDefinitionId) && data.stateDefinitionId != request.stateDefinitionId) continue;
                NarrativeStateTransitionDefinitionData transition = data.transitions.FirstOrDefault(item => item.transitionDefinitionId == request.transitionDefinitionId);
                if (transition == null) continue;
                NarrativeStateValidationReport report = NarrativeStateDefinitionValidator.Validate(data, registry.DefinitionsById);
                if (!report.Succeeded)
                {
                    failure = Fail(NarrativeStateTransitionStatus.DefinitionInvalid, string.Join(" | ", report.Errors));
                    return false;
                }

                stateDefinition = data;
                transitionDefinition = transition;
                variableDefinition = data.variables.First(item => item.variableDefinitionId == transition.variableDefinitionId);
                return true;
            }

            failure = Fail(NarrativeStateTransitionStatus.MissingDefinition, $"Narrative transition definition '{request.transitionDefinitionId}' is missing.");
            return false;
        }

        private bool TryResolveState(string stateDefinitionId, out NarrativeStateDefinitionData stateDefinition, out NarrativeStateTransitionResult failure)
        {
            stateDefinition = null;
            failure = null;
            if (registry == null)
            {
                failure = Fail(NarrativeStateTransitionStatus.MissingDefinitionRegistry, "Narrative state runtime has no definition registry.");
                return false;
            }

            if (!registry.TryGet(N(stateDefinitionId), out NarrativeStateDefinition definition))
            {
                failure = Fail(NarrativeStateTransitionStatus.MissingDefinition, $"NarrativeStateDefinition '{N(stateDefinitionId)}' is missing.");
                return false;
            }

            stateDefinition = definition.ToRecordData();
            return true;
        }

        private IEnumerable<NarrativeStateDefinitionData> CandidateStateDefinitions()
        {
            if (registry == null) return Array.Empty<NarrativeStateDefinitionData>();
            return registry.DefinitionsById.Values.OfType<NarrativeStateDefinition>().Select(value => value.ToRecordData());
        }

        private NarrativeStateRecordData ResolveRecord(NarrativeStateDefinitionData definition, string scopeKey, double worldTime)
        {
            scopeKey = N(scopeKey);
            string id = BuildStateId(definition.stateDefinitionId, scopeKey);
            if (statesById.TryGetValue(id, out NarrativeStateRecordData existing)) return existing.Clone();
            return new NarrativeStateRecordData
            {
                narrativeStateId = id,
                stateDefinitionId = definition.stateDefinitionId,
                worldId = worldId,
                scope = definition.scope,
                scopeKey = scopeKey,
                lifecycle = NarrativeStateLifecycle.DefaultProjected,
                variables = definition.variables.Select(variable => new NarrativeStateVariableRecordData
                {
                    variableDefinitionId = variable.variableDefinitionId,
                    value = variable.defaultValue.Clone(),
                    changedWorldTime = worldTime,
                    sourceTransitionId = string.Empty,
                    revision = 0L
                }).ToArray(),
                createdWorldTime = worldTime,
                updatedWorldTime = worldTime,
                revision = 0L
            };
        }

        private static NarrativeVariableValueData ResolveValue(NarrativeStateRecordData record, NarrativeVariableDefinitionData variable)
        {
            return record.variables.FirstOrDefault(item => item.variableDefinitionId == variable.variableDefinitionId)?.value?.Clone() ?? variable.defaultValue.Clone();
        }

        private static bool SourceValueAllowed(NarrativeStateTransitionDefinitionData transition, NarrativeVariableValueData current)
        {
            NarrativeVariableValueData[] sources = transition.allowedSourceValues ?? Array.Empty<NarrativeVariableValueData>();
            return sources.Length == 0 || sources.Any(source => current.SameValue(source));
        }

        private static bool IsTerminal(NarrativeVariableDefinitionData variable, NarrativeVariableValueData value)
        {
            if (variable.kind != NarrativeVariableKind.StateToken) return false;
            return (variable.allowedValues ?? Array.Empty<NarrativeStateValueDefinitionData>()).Any(item => item.terminal && string.Equals(item.valueDefinitionId, value.tokenValue, StringComparison.Ordinal));
        }

        private IEnumerable<NarrativeConditionResultData> EvaluateConditions(IEnumerable<NarrativeConditionDefinitionData> conditions, NarrativeConditionContextData context)
        {
            NarrativeConditionContextData data = context?.Clone() ?? new NarrativeConditionContextData();
            foreach (NarrativeConditionDefinitionData condition in conditions ?? Array.Empty<NarrativeConditionDefinitionData>())
            {
                bool matched = condition.category switch
                {
                    NarrativeConditionCategory.Always => true,
                    NarrativeConditionCategory.NarrativeState => EvaluateCondition(ParseConditionQuery(condition, data)),
                    NarrativeConditionCategory.AuthoritativeTruth => Contains(data.authoritativeTruthIds, condition.requiredId),
                    NarrativeConditionCategory.ActorKnowledge => Contains(data.knownSubjectIds, condition.requiredId),
                    NarrativeConditionCategory.ParticipantKnowledge => Contains(data.knownSubjectIds, condition.requiredId),
                    NarrativeConditionCategory.InstitutionalKnowledge => Contains(data.knownSubjectIds, condition.requiredId),
                    NarrativeConditionCategory.Belief => Contains(data.beliefIds, condition.requiredId),
                    NarrativeConditionCategory.QuestState => Contains(data.questStateIds, condition.requiredId),
                    NarrativeConditionCategory.DialogueState => Contains(data.dialogueStateIds, condition.requiredId),
                    NarrativeConditionCategory.LocationState => Contains(data.locationStateIds, condition.requiredId) || string.Equals(data.locationId, condition.requiredId, StringComparison.Ordinal),
                    NarrativeConditionCategory.ItemState => Contains(data.itemStateIds, condition.requiredId) || string.Equals(data.itemId, condition.requiredId, StringComparison.Ordinal),
                    NarrativeConditionCategory.CharacterState => Contains(data.characterStateIds, condition.requiredId),
                    NarrativeConditionCategory.OrganizationState => Contains(data.organizationStateIds, condition.requiredId) || string.Equals(data.organizationId, condition.requiredId, StringComparison.Ordinal),
                    NarrativeConditionCategory.SocialState => Contains(data.socialStateIds, condition.requiredId),
                    NarrativeConditionCategory.EconomicState => Contains(data.economicStateIds, condition.requiredId),
                    NarrativeConditionCategory.LegalState => Contains(data.legalStateIds, condition.requiredId),
                    NarrativeConditionCategory.HistoricalState => Contains(data.historicalStateIds, condition.requiredId),
                    NarrativeConditionCategory.TimeState => data.worldTime >= condition.minimumValue,
                    NarrativeConditionCategory.Custom => Contains(data.customStateIds, condition.requiredId),
                    _ => false
                };

                if (condition.negate) matched = !matched;
                yield return new NarrativeConditionResultData
                {
                    conditionDefinitionId = condition.conditionDefinitionId,
                    category = condition.category,
                    subjectId = condition.requiredId,
                    sourceRuntime = condition.category == NarrativeConditionCategory.NarrativeState ? "NarrativeStateRuntime" : "NarrativeConditionContext",
                    matched = matched,
                    hidden = condition.hidden,
                    reason = matched ? "Matched" : condition.revealFailure ? "Condition did not match" : "Hidden"
                };
            }
        }

        private NarrativeStateConditionQuery ParseConditionQuery(NarrativeConditionDefinitionData condition, NarrativeConditionContextData context)
        {
            string[] parts = N(condition.requiredId).Split('|');
            NarrativeStateConditionQuery query = new NarrativeStateConditionQuery
            {
                stateDefinitionId = parts.Length > 0 ? parts[0] : string.Empty,
                variableDefinitionId = parts.Length > 1 ? parts[1] : string.Empty,
                scope = ScopeFromText(parts.Length > 3 ? parts[3] : string.Empty),
                scopeKey = parts.Length > 4 ? parts[4] : string.Empty,
                minimumValue = condition.minimumValue
            };

            if (string.IsNullOrWhiteSpace(query.scopeKey)) query.scopeKey = query.scope == NarrativeStateScope.Person ? context.actorPersonId : DefaultScopeKey(query.scope);
            if (parts.Length > 2) query.expectedValue = NarrativeVariableValueData.Token(parts[2]);
            return query;
        }

        private List<NarrativeActionExecutionRecordData> PrepareConsequences(NarrativeStateTransitionDefinitionData transition, NarrativeStateTransitionRequest request, NarrativeStateRecordData current, IReadOnlyList<NarrativeConditionResultData> conditions, out NarrativeStateTransitionResult failure)
        {
            failure = null;
            List<NarrativeActionExecutionRecordData> records = new List<NarrativeActionExecutionRecordData>();
            int index = 0;
            foreach (NarrativeActionDefinitionData action in transition.consequences ?? Array.Empty<NarrativeActionDefinitionData>())
            {
                index++;
                NarrativeActionExecutionRecordData record = NewConsequenceRecord(current, action, index, request.worldTime, NarrativeActionLifecycle.Prepared, "Narrative transition consequence prepared.");
                if (action.category != NarrativeActionCategory.None && integrations?.ConsequenceValidator != null && !integrations.ConsequenceValidator(action.Clone(), request.Clone()))
                {
                    record.lifecycle = action.requirement == NarrativeActionRequirement.OptionalBestEffort ? NarrativeActionLifecycle.SkippedOptional : NarrativeActionLifecycle.Failed;
                    record.message = "Narrative transition consequence was rejected during prepare.";
                }

                records.Add(record);
                if (record.lifecycle == NarrativeActionLifecycle.Failed && action.requirement == NarrativeActionRequirement.Required)
                {
                    failure = new NarrativeStateTransitionResult(NarrativeStateTransitionStatus.ConsequenceFailed, record.message, revision, revision, Snapshot(current, true), consequences: records);
                    return records;
                }
            }

            return records;
        }

        private IEnumerable<NarrativeActionExecutionRecordData> ExecuteConsequences(NarrativeStateTransitionDefinitionData transition, NarrativeStateTransitionRequest request, IReadOnlyList<NarrativeActionExecutionRecordData> prepared, out NarrativeStateTransitionResult failure)
        {
            failure = null;
            List<NarrativeActionExecutionRecordData> executed = new List<NarrativeActionExecutionRecordData>();
            foreach (NarrativeActionDefinitionData action in transition.consequences ?? Array.Empty<NarrativeActionDefinitionData>())
            {
                NarrativeActionExecutionRecordData record = prepared.First(item => item.actionDefinitionId == action.actionDefinitionId).Clone();
                if (record.lifecycle == NarrativeActionLifecycle.SkippedOptional)
                {
                    executed.Add(record);
                    continue;
                }

                if (action.category == NarrativeActionCategory.None)
                {
                    record.lifecycle = NarrativeActionLifecycle.Committed;
                    record.message = "No-op narrative state consequence.";
                }
                else if (action.category == NarrativeActionCategory.EmitNarrativeSignal)
                {
                    NarrativeEventOperationResult signal = integrations?.NarrativeEventRuntime?.EmitSignal(new NarrativeSignalRequest
                    {
                        transactionId = $"{request.transactionId}.{action.actionDefinitionId}.signal",
                        signalDefinitionId = action.targetId,
                        sourceKind = NarrativeSignalSourceKind.NarrativeStateTransition,
                        sourceId = request.transitionDefinitionId,
                        actorPersonId = request.actorPersonId,
                        subjectIds = new[] { request.scopeKey },
                        conditionContext = request.conditionContext?.Clone(),
                        worldTime = request.worldTime,
                        cascadeDepth = request.cascadeDepth + 1
                    });
                    record.lifecycle = signal != null && signal.Succeeded ? NarrativeActionLifecycle.Committed : action.requirement == NarrativeActionRequirement.OptionalBestEffort ? NarrativeActionLifecycle.SkippedOptional : NarrativeActionLifecycle.Failed;
                    record.externalResultId = action.targetId;
                    record.resultValue = action.targetId;
                    record.message = signal?.Message ?? "NarrativeEventRuntime integration is missing.";
                }
                else
                {
                    string result = integrations?.ConsequenceExecutor?.Invoke(action.Clone(), request.Clone());
                    bool succeeded = !string.IsNullOrWhiteSpace(result);
                    record.lifecycle = succeeded ? NarrativeActionLifecycle.Committed : action.requirement == NarrativeActionRequirement.OptionalBestEffort ? NarrativeActionLifecycle.SkippedOptional : NarrativeActionLifecycle.Failed;
                    record.externalResultId = result ?? string.Empty;
                    record.resultValue = result ?? string.Empty;
                    record.message = succeeded ? "Narrative state consequence executed by owner runtime." : "Narrative state consequence owner integration is missing or rejected the action.";
                }

                executed.Add(record);
                if (record.lifecycle == NarrativeActionLifecycle.Failed && action.requirement == NarrativeActionRequirement.Required)
                {
                    failure = new NarrativeStateTransitionResult(NarrativeStateTransitionStatus.ConsequenceFailed, record.message, revision, revision, consequences: executed);
                    break;
                }
            }

            return executed;
        }

        private void EmitStateChangedSignal(NarrativeStateTransitionRecordData transition, NarrativeStateTransitionRequest request)
        {
            if (integrations?.NarrativeEventRuntime == null) return;
            integrations.NarrativeEventRuntime.RouteTrigger(new NarrativeTriggerRequest
            {
                transactionId = $"{transition.sourceTransactionId}.state-changed",
                source = new NarrativeTriggerSourceData
                {
                    category = NarrativeTriggerCategory.StateChanged,
                    sourceId = transition.transitionDefinitionId,
                    sourceTransactionId = transition.transitionId,
                    actorPersonId = transition.actorPersonId,
                    targetId = transition.stateDefinitionId,
                    subjectId = transition.scopeKey,
                    ownerRuntime = "NarrativeStateRuntime",
                    worldTime = transition.worldTime,
                    committed = true
                },
                conditionContext = request.conditionContext?.Clone(),
                cascadeDepth = request.cascadeDepth + 1
            });
        }

        private NarrativeActionExecutionRecordData NewConsequenceRecord(NarrativeStateRecordData state, NarrativeActionDefinitionData action, int index, double worldTime, NarrativeActionLifecycle lifecycle, string message)
        {
            return new NarrativeActionExecutionRecordData
            {
                actionExecutionId = $"narrative-state-action.{NarrativeModelUtility.SanitizeForId(state.narrativeStateId)}.{NarrativeModelUtility.SanitizeForId(action.actionDefinitionId)}.{index:000}",
                narrativeEventId = string.Empty,
                actionDefinitionId = action.actionDefinitionId,
                category = action.category,
                lifecycle = lifecycle,
                requirement = action.requirement,
                order = action.order,
                targetOwnerRuntime = OwnerRuntime(action.category),
                externalResultId = action.targetId,
                outputSlotId = action.outputSlotId,
                resultValue = action.targetId,
                message = message,
                worldTime = worldTime,
                runtimeRevision = revision
            };
        }

        private static string OwnerRuntime(NarrativeActionCategory category)
        {
            return category switch
            {
                NarrativeActionCategory.EmitNarrativeSignal => "NarrativeEventRuntime",
                NarrativeActionCategory.RequestNarrativeStateTransition => "NarrativeStateRuntime",
                NarrativeActionCategory.InstantiateQuest => "QuestRuntime",
                NarrativeActionCategory.PublishQuestListing => "QuestSourceRuntime",
                NarrativeActionCategory.StartConversation => "ConversationRuntime",
                _ => "OwnerRuntime"
            };
        }

        private NarrativeStateTransitionRecordData BuildTransitionRecord(NarrativeStateTransitionRequest request, NarrativeStateDefinitionData stateDefinition, NarrativeStateTransitionDefinitionData transitionDefinition, NarrativeVariableDefinitionData variableDefinition, NarrativeStateRecordData current, NarrativeVariableValueData oldValue, NarrativeVariableValueData newValue, IReadOnlyList<NarrativeConditionResultData> conditions, IReadOnlyList<NarrativeActionExecutionRecordData> consequences, int sequence)
        {
            string transitionId = BuildTransitionId(request, transitionDefinition, current, sequence);
            return new NarrativeStateTransitionRecordData
            {
                transitionId = transitionId,
                transitionDefinitionId = transitionDefinition.transitionDefinitionId,
                narrativeStateId = current.narrativeStateId,
                stateDefinitionId = stateDefinition.stateDefinitionId,
                variableDefinitionId = variableDefinition.variableDefinitionId,
                worldId = worldId,
                scope = stateDefinition.scope,
                scopeKey = current.scopeKey,
                sourceKind = request.sourceKind,
                sourceId = request.sourceId,
                sourceTransactionId = request.transactionId,
                actorPersonId = request.actorPersonId,
                questId = request.questId,
                conversationId = request.conversationId,
                narrativeEventId = request.narrativeEventId,
                oldValue = oldValue.Clone(),
                newValue = newValue.Clone(),
                conditions = conditions.Select(value => value.Clone()).ToArray(),
                consequences = consequences.Select(value => value.Clone()).ToArray(),
                worldTime = request.worldTime,
                sequence = sequence,
                visibility = transitionDefinition.visibility,
                provenanceId = request.sourceId
            };
        }

        private void SetVariable(NarrativeStateRecordData state, string variableDefinitionId, NarrativeVariableValueData value, string transitionId, double worldTime)
        {
            List<NarrativeStateVariableRecordData> variables = state.variables?.Select(item => item.Clone()).ToList() ?? new List<NarrativeStateVariableRecordData>();
            NarrativeStateVariableRecordData existing = variables.FirstOrDefault(item => item.variableDefinitionId == variableDefinitionId);
            if (existing == null)
            {
                existing = new NarrativeStateVariableRecordData { variableDefinitionId = variableDefinitionId };
                variables.Add(existing);
            }

            existing.value = value.Clone();
            existing.changedWorldTime = worldTime;
            existing.sourceTransitionId = transitionId;
            existing.revision = state.revision + 1;
            state.variables = variables.OrderBy(item => item.variableDefinitionId, StringComparer.Ordinal).ToArray();
        }

        private void CommitRecord(NarrativeStateRecordData record)
        {
            record.lifecycle = NarrativeStateLifecycle.Active;
            statesById[record.narrativeStateId] = record.Clone();
            stateIdByScope[BuildScopeIndexKey(record.stateDefinitionId, record.scopeKey)] = record.narrativeStateId;
        }

        private void RebuildIndexes()
        {
            stateIdByScope.Clear();
            foreach (NarrativeStateRecordData state in statesById.Values)
            {
                stateIdByScope[BuildScopeIndexKey(state.stateDefinitionId, state.scopeKey)] = state.narrativeStateId;
            }
        }

        private bool ValidateRevision(long expectedRevision, out NarrativeStateTransitionResult failure)
        {
            failure = null;
            if (expectedRevision < 0L || expectedRevision == revision) return true;
            failure = Fail(NarrativeStateTransitionStatus.RevisionConflict, $"Expected narrative state revision {expectedRevision}, actual {revision}.");
            return false;
        }

        private NarrativeStateSnapshot Snapshot(NarrativeStateRecordData record, bool developmentView)
        {
            if (record == null) return null;
            if (!TryResolveState(record.stateDefinitionId, out NarrativeStateDefinitionData definition, out _)) definition = new NarrativeStateDefinitionData { stateDefinitionId = record.stateDefinitionId };
            return new NarrativeStateSnapshot(record, definition, developmentView);
        }

        private NarrativeStateTransitionResult Fail(NarrativeStateTransitionStatus status, string message, NarrativeStateSnapshot snapshot = null)
        {
            return NarrativeStateTransitionResult.Failure(status, message, revision, snapshot);
        }

        private string ResolveScopeKey(NarrativeStateDefinitionData definition, string requestedScopeKey, NarrativeStateTransitionRequest request)
        {
            if (!string.IsNullOrWhiteSpace(requestedScopeKey)) return N(requestedScopeKey);
            return definition.scope switch
            {
                NarrativeStateScope.World => worldId,
                NarrativeStateScope.Person => N(request?.actorPersonId),
                NarrativeStateScope.Quest => N(request?.questId),
                NarrativeStateScope.Organization => N(request?.conditionContext?.organizationId),
                NarrativeStateScope.Location => N(request?.conditionContext?.locationId),
                _ => DefaultScopeKey(definition.scope)
            };
        }

        public static string BuildConditionKey(string stateDefinitionId, string variableDefinitionId, string expectedToken, NarrativeStateScope scope = NarrativeStateScope.World, string scopeKey = "")
        {
            return $"{N(stateDefinitionId)}|{N(variableDefinitionId)}|{N(expectedToken)}|{scope}|{N(scopeKey)}";
        }

        public static string BuildStateId(string stateDefinitionId, string scopeKey)
        {
            return $"narrative-state.{NarrativeModelUtility.SanitizeForId(stateDefinitionId)}.{NarrativeModelUtility.SanitizeForId(scopeKey)}";
        }

        private static string BuildScopeIndexKey(string stateDefinitionId, string scopeKey) => $"{N(stateDefinitionId)}|{N(scopeKey)}";

        private string BuildTransitionId(NarrativeStateTransitionRequest request, NarrativeStateTransitionDefinitionData transition, NarrativeStateRecordData state, int sequence)
        {
            string source = string.IsNullOrWhiteSpace(request.transactionId) ? $"{transition.transitionDefinitionId}.{state.scopeKey}.{sequence:000000}" : request.transactionId;
            return $"narrative-transition.{NarrativeModelUtility.SanitizeForId(transition.transitionDefinitionId)}.{NarrativeModelUtility.SanitizeForId(state.scopeKey)}.{NarrativeModelUtility.SanitizeForId(source)}";
        }

        private static NarrativeStateScope ScopeFromText(string value)
        {
            return Enum.TryParse(value, out NarrativeStateScope scope) && scope != NarrativeStateScope.Unknown ? scope : NarrativeStateScope.World;
        }

        private static string DefaultScopeKey(NarrativeStateScope scope) => scope == NarrativeStateScope.World ? PersistenceService.LocalWorldId : "default";
        private static bool Contains(IEnumerable<string> values, string id) => (values ?? Array.Empty<string>()).Any(value => string.Equals(N(value), N(id), StringComparison.Ordinal));
        private static string N(string value) => NarrativeModelUtility.N(value);

        private static void ReplaceConsequence(IList<NarrativeActionExecutionRecordData> records, NarrativeActionExecutionRecordData replacement)
        {
            for (int index = 0; index < records.Count; index++)
            {
                if (records[index].actionDefinitionId == replacement.actionDefinitionId)
                {
                    records[index] = replacement.Clone();
                    return;
                }
            }

            records.Add(replacement.Clone());
        }
    }
}
