using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Narrative
{
    public sealed class NarrativeArcRuntime : IDisposable
    {
        private readonly Dictionary<string, NarrativeArcRecordData> arcsById = new Dictionary<string, NarrativeArcRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> arcByDefinitionScope = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, NarrativeArcRuntimeTransactionData> transactionsById = new Dictionary<string, NarrativeArcRuntimeTransactionData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private NarrativeArcRuntimeIntegrations integrations;
        private string worldId;
        private long revision;
        private bool disposed;

        public NarrativeArcRuntime(DefinitionRegistry definitionRegistry = null, NarrativeArcRuntimeIntegrations runtimeIntegrations = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            Configure(definitionRegistry, runtimeIntegrations, runtimeWorldId);
        }

        public long Revision => revision;
        public string WorldId => worldId ?? string.Empty;
        public int Count => arcsById.Count;
        public int TransactionCount => transactionsById.Count;

        public void Configure(DefinitionRegistry definitionRegistry, NarrativeArcRuntimeIntegrations runtimeIntegrations = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            registry = definitionRegistry;
            integrations = runtimeIntegrations ?? new NarrativeArcRuntimeIntegrations();
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId.Trim();
            RebuildIndexes();
        }

        public NarrativeArcOperationResult StartArc(NarrativeArcStartRequest request)
        {
            if (disposed) return Fail(NarrativeArcOperationStatus.Disposed, "Narrative arc runtime is disposed.");
            request ??= new NarrativeArcStartRequest();
            if (!ValidateRevision(request.expectedRevision, out NarrativeArcOperationResult revisionFailure)) return revisionFailure;
            if (TryDuplicate(request.transactionId, out NarrativeArcOperationResult duplicate)) return duplicate;
            if (!TryResolveDefinition(request.arcDefinitionId, out NarrativeArcDefinitionData definition, out NarrativeArcOperationResult definitionFailure)) return definitionFailure;

            string scopeKey = ResolveScopeKey(definition, request.scopeKey, request.actorPersonId, request.subjectId, request.conditionContext);
            if (string.IsNullOrWhiteSpace(scopeKey)) return Fail(NarrativeArcOperationStatus.InvalidRequest, "Narrative arc scope key could not be resolved.");
            string uniquenessKey = BuildDefinitionScopeKey(definition.arcDefinitionId, scopeKey);
            if (!definition.repeatable && arcByDefinitionScope.TryGetValue(uniquenessKey, out string existingId) && arcsById.TryGetValue(existingId, out NarrativeArcRecordData existing))
            {
                return NarrativeArcOperationResult.Success("Existing scoped NarrativeArc returned.", revision, revision, Snapshot(existing, definition), duplicate: true);
            }

            NarrativeArcRecordData record = CreateRecord(definition, request, scopeKey);
            NarrativeArcOperationResult evaluation = Reevaluate(record, definition, new NarrativeArcSignalRequest
            {
                transactionId = request.transactionId,
                category = NarrativeArcSignalCategory.Explicit,
                signalId = request.transactionId,
                actorPersonId = request.actorPersonId,
                subjectId = request.subjectId,
                scopeKey = scopeKey,
                conditionContext = request.conditionContext?.Clone(),
                worldTime = request.worldTime,
                preview = request.preview
            }, request.preview);
            if (!evaluation.Succeeded) return evaluation;
            record = evaluation.Snapshot.ToSaveData();
            if (request.preview) return NarrativeArcOperationResult.Success("Narrative arc start previewed.", revision, revision, Snapshot(record, definition), preview: true);

            long before = revision;
            CommitRecord(record);
            revision++;
            RecordTransaction(request.transactionId, "StartArc", record.narrativeArcId, string.Empty, NarrativeArcOperationStatus.Succeeded);
            return NarrativeArcOperationResult.Success("Narrative arc started.", before, revision, Snapshot(record, definition));
        }

        public NarrativeArcOperationResult ApplySignal(NarrativeArcSignalRequest request)
        {
            if (disposed) return Fail(NarrativeArcOperationStatus.Disposed, "Narrative arc runtime is disposed.");
            request ??= new NarrativeArcSignalRequest();
            if (!ValidateRevision(request.expectedRevision, out NarrativeArcOperationResult revisionFailure)) return revisionFailure;
            if (request.cascadeDepth > 16) return Fail(NarrativeArcOperationStatus.CascadeLimitReached, "Narrative arc cascade depth limit reached.");
            if (TryDuplicate(request.transactionId, out NarrativeArcOperationResult duplicate)) return duplicate;

            List<NarrativeArcRecordData> candidates = ResolveCandidates(request).ToList();
            if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(request.arcDefinitionId))
            {
                NarrativeArcStartRequest start = new NarrativeArcStartRequest
                {
                    transactionId = $"{N(request.transactionId)}.start",
                    arcDefinitionId = request.arcDefinitionId,
                    scopeKey = request.scopeKey,
                    actorPersonId = request.actorPersonId,
                    subjectId = request.subjectId,
                    conditionContext = request.conditionContext?.Clone(),
                    worldTime = request.worldTime,
                    preview = request.preview
                };
                NarrativeArcOperationResult started = StartArc(start);
                if (!started.Succeeded && !started.Duplicate) return started;
                candidates = ResolveCandidates(request).ToList();
            }

            bool changedAny = false;
            NarrativeArcRecordData last = null;
            long before = revision;
            foreach (NarrativeArcRecordData candidate in candidates.OrderBy(value => value.arcDefinitionId, StringComparer.Ordinal).ThenBy(value => value.narrativeArcId, StringComparer.Ordinal))
            {
                if (!TryResolveDefinition(candidate.arcDefinitionId, out NarrativeArcDefinitionData definition, out NarrativeArcOperationResult definitionFailure)) return definitionFailure;
                NarrativeArcRecordData working = candidate.Clone();
                if (!MarkSignalProcessed(working, request)) continue;
                NarrativeArcOperationResult result = Reevaluate(working, definition, request, request.preview);
                if (!result.Succeeded) return result;
                working = result.Snapshot.ToSaveData();
                last = working.Clone();
                if (!request.preview)
                {
                    CommitRecord(working);
                    changedAny = true;
                }
            }

            if (request.preview) return NarrativeArcOperationResult.Success("Narrative arc signal previewed.", revision, revision, last == null ? null : Snapshot(last), preview: true);
            if (changedAny) revision++;
            RecordTransaction(request.transactionId, "ApplySignal", last?.narrativeArcId ?? string.Empty, request.stageDefinitionId, NarrativeArcOperationStatus.Succeeded);
            return NarrativeArcOperationResult.Success(changedAny ? "Narrative arc signal applied." : "Narrative arc signal had no matching state change.", before, revision, last == null ? null : Snapshot(last));
        }

        public bool TryGetSnapshot(string narrativeArcId, out NarrativeArcSnapshot snapshot, bool developmentView = true)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(narrativeArcId) || !arcsById.TryGetValue(N(narrativeArcId), out NarrativeArcRecordData record)) return false;
            snapshot = Snapshot(record, developmentView: developmentView);
            return true;
        }

        public IReadOnlyList<NarrativeArcSnapshot> Query(NarrativeArcQuery query = null)
        {
            NarrativeArcQuery actual = query ?? new NarrativeArcQuery();
            IEnumerable<NarrativeArcRecordData> records = arcsById.Values;
            if (!string.IsNullOrWhiteSpace(actual.narrativeArcId)) records = records.Where(value => string.Equals(value.narrativeArcId, N(actual.narrativeArcId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.arcDefinitionId)) records = records.Where(value => string.Equals(value.arcDefinitionId, N(actual.arcDefinitionId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.scopeKey)) records = records.Where(value => string.Equals(value.scopeKey, N(actual.scopeKey), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(actual.actorPersonId)) records = records.Where(value => string.Equals(value.actorPersonId, N(actual.actorPersonId), StringComparison.Ordinal));
            if (actual.lifecycle.HasValue) records = records.Where(value => value.lifecycle == actual.lifecycle.Value);
            if (!string.IsNullOrWhiteSpace(actual.stageDefinitionId)) records = records.Where(value => value.stages.Any(stage => string.Equals(stage.stageDefinitionId, N(actual.stageDefinitionId), StringComparison.Ordinal)));
            return records.OrderBy(value => value.startedWorldTime).ThenBy(value => value.narrativeArcId, StringComparer.Ordinal).Select(value => Snapshot(value, developmentView: actual.developmentView)).ToArray();
        }

        public bool EvaluateCondition(NarrativeConditionDefinitionData condition, NarrativeConditionContextData context)
        {
            condition = condition?.Clone() ?? new NarrativeConditionDefinitionData();
            NarrativeConditionContextData data = context?.Clone() ?? new NarrativeConditionContextData();
            string secondary = condition.secondaryId ?? string.Empty;
            bool secondaryIsStage = secondary.StartsWith("narrative-arc-stage-definition.", StringComparison.Ordinal);
            return Query(new NarrativeArcQuery
            {
                arcDefinitionId = condition.requiredId,
                scopeKey = secondaryIsStage || string.IsNullOrWhiteSpace(secondary) ? ResolveConditionScopeKey(condition, data) : secondary,
                developmentView = true
            }).Any(snapshot =>
            {
                if (!secondaryIsStage) return snapshot.Lifecycle == NarrativeArcLifecycle.Completed || snapshot.Lifecycle == NarrativeArcLifecycle.Active;
                return snapshot.Stages.Any(stage => string.Equals(stage.StageDefinitionId, secondary, StringComparison.Ordinal) && (stage.Lifecycle == NarrativeArcStageLifecycle.Completed || stage.Lifecycle == NarrativeArcStageLifecycle.Skipped));
            });
        }

        public NarrativeArcRuntimeSaveData CreateSaveData()
        {
            return new NarrativeArcRuntimeSaveData
            {
                schemaVersion = NarrativeArcRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = revision,
                arcs = arcsById.Values.Select(value => value.Clone()).OrderBy(value => value.narrativeArcId, StringComparer.Ordinal).ToList(),
                transactions = transactionsById.Values.Select(value => value.Clone()).OrderBy(value => value.transactionId, StringComparer.Ordinal).ToList()
            };
        }

        public NarrativeArcOperationResult RestoreFromSaveData(NarrativeArcRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, NarrativeArcRuntimeIntegrations runtimeIntegrations = null, string expectedWorldId = PersistenceService.LocalWorldId)
        {
            if (disposed) return Fail(NarrativeArcOperationStatus.Disposed, "Narrative arc runtime is disposed.");
            if (!ValidateSaveData(saveData, definitionRegistry, expectedWorldId, out string failure)) return Fail(NarrativeArcOperationStatus.RestoreFailed, failure);

            arcsById.Clear();
            arcByDefinitionScope.Clear();
            transactionsById.Clear();
            registry = definitionRegistry;
            integrations = runtimeIntegrations ?? new NarrativeArcRuntimeIntegrations();
            worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            revision = saveData.revision;
            foreach (NarrativeArcRecordData record in saveData.arcs ?? new List<NarrativeArcRecordData>()) CommitRecord(record);
            foreach (NarrativeArcRuntimeTransactionData transaction in saveData.transactions ?? new List<NarrativeArcRuntimeTransactionData>()) transactionsById[transaction.transactionId] = transaction.Clone();
            return NarrativeArcOperationResult.Success("Narrative arcs restored.", revision, revision);
        }

        public static bool ValidateSaveData(NarrativeArcRuntimeSaveData saveData, DefinitionRegistry registry, string expectedWorldId, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Narrative arc save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != NarrativeArcRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported narrative arc save schema version {saveData.schemaVersion}.";
                return false;
            }

            string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            if (!string.Equals(saveData.worldId ?? string.Empty, world, StringComparison.Ordinal))
            {
                failure = $"Narrative arc save world '{saveData.worldId}' does not match expected world '{world}'.";
                return false;
            }

            HashSet<string> arcIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> scoped = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeArcRecordData record in saveData.arcs ?? new List<NarrativeArcRecordData>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.narrativeArcId))
                {
                    failure = "Narrative arc save contains a record without an ID.";
                    return false;
                }

                if (!arcIds.Add(record.narrativeArcId))
                {
                    failure = $"Duplicate NarrativeArcId '{record.narrativeArcId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(record.arcDefinitionId, out NarrativeArcDefinition definition))
                {
                    failure = $"NarrativeArc '{record.narrativeArcId}' references missing definition '{record.arcDefinitionId}'.";
                    return false;
                }

                NarrativeArcDefinitionData data = definition.ToRecordData();
                if (!data.repeatable && !scoped.Add(BuildDefinitionScopeKey(record.arcDefinitionId, record.scopeKey)))
                {
                    failure = $"Duplicate scoped NarrativeArc for definition '{record.arcDefinitionId}' and scope '{record.scopeKey}'.";
                    return false;
                }

                HashSet<string> expectedStages = new HashSet<string>(data.stages.Select(stage => stage.stageDefinitionId), StringComparer.Ordinal);
                HashSet<string> savedStages = new HashSet<string>(StringComparer.Ordinal);
                foreach (NarrativeArcStageRecordData stage in record.stages ?? Array.Empty<NarrativeArcStageRecordData>())
                {
                    if (stage == null || string.IsNullOrWhiteSpace(stage.stageDefinitionId))
                    {
                        failure = $"NarrativeArc '{record.narrativeArcId}' contains a stage without an ID.";
                        return false;
                    }

                    if (!savedStages.Add(stage.stageDefinitionId))
                    {
                        failure = $"NarrativeArc '{record.narrativeArcId}' contains duplicate stage '{stage.stageDefinitionId}'.";
                        return false;
                    }

                    if (!expectedStages.Contains(stage.stageDefinitionId))
                    {
                        failure = $"NarrativeArc '{record.narrativeArcId}' references non-definition stage '{stage.stageDefinitionId}'.";
                        return false;
                    }

                    if (!string.Equals(stage.stageRuntimeId, BuildStageRuntimeId(record.narrativeArcId, stage.stageDefinitionId), StringComparison.Ordinal))
                    {
                        failure = $"NarrativeArc '{record.narrativeArcId}' stage '{stage.stageDefinitionId}' has an invalid runtime ID.";
                        return false;
                    }
                }

                foreach (string expected in expectedStages)
                {
                    if (!savedStages.Contains(expected))
                    {
                        failure = $"NarrativeArc '{record.narrativeArcId}' is missing stage '{expected}'.";
                        return false;
                    }
                }
            }

            return true;
        }

        public void Dispose()
        {
            disposed = true;
            arcsById.Clear();
            arcByDefinitionScope.Clear();
            transactionsById.Clear();
        }

        private NarrativeArcOperationResult Reevaluate(NarrativeArcRecordData record, NarrativeArcDefinitionData definition, NarrativeArcSignalRequest signal, bool preview)
        {
            if (signal.cascadeDepth > definition.cascadeDepthLimit) return Fail(NarrativeArcOperationStatus.CascadeLimitReached, "Narrative arc cascade depth limit reached.", Snapshot(record, definition));
            NarrativeArcRecordData working = record.Clone();
            bool changed;
            int budget = Math.Max(4, definition.cascadeDepthLimit);
            do
            {
                changed = false;
                foreach (NarrativeArcStageDefinitionData stageDefinition in definition.stages.OrderBy(value => value.order).ThenBy(value => value.stageDefinitionId, StringComparer.Ordinal))
                {
                    NarrativeArcStageRecordData stage = FindStage(working, stageDefinition.stageDefinitionId);
                    if (stage.lifecycle == NarrativeArcStageLifecycle.Locked && DependenciesSatisfied(working, stageDefinition.entryDependencies, signal, emptySatisfied: true))
                    {
                        NarrativeArcOperationResult activated = ActivateStage(working, definition, stageDefinition, signal, preview);
                        if (!activated.Succeeded) return activated;
                        working = activated.Snapshot.ToSaveData();
                        changed = true;
                        stage = FindStage(working, stageDefinition.stageDefinitionId);
                    }

                    if (stage.lifecycle == NarrativeArcStageLifecycle.Active)
                    {
                        if (DependenciesSatisfied(working, stageDefinition.failureDependencies, signal, emptySatisfied: false))
                        {
                            ResolveStage(working, stage, NarrativeArcStageLifecycle.Failed, signal);
                            working.lifecycle = NarrativeArcLifecycle.Failed;
                            working.resolvedWorldTime = signal.worldTime;
                            changed = true;
                        }
                        else if (DependenciesSatisfied(working, stageDefinition.skipDependencies, signal, emptySatisfied: false))
                        {
                            ResolveStage(working, stage, NarrativeArcStageLifecycle.Skipped, signal);
                            changed = true;
                        }
                        else if (DependenciesSatisfied(working, stageDefinition.completionDependencies, signal, emptySatisfied: false))
                        {
                            NarrativeArcOperationResult completed = CompleteStage(working, definition, stageDefinition, stage, signal, preview);
                            if (!completed.Succeeded) return completed;
                            working = completed.Snapshot.ToSaveData();
                            changed = true;
                        }
                    }
                }
            }
            while (changed && --budget > 0);

            if (budget <= 0) return Fail(NarrativeArcOperationStatus.CascadeLimitReached, "Narrative arc re-evaluation budget exhausted.", Snapshot(working, definition));
            if (working.lifecycle == NarrativeArcLifecycle.Active && working.stages.Any() && working.stages.All(stage => IsResolved(stage.lifecycle)))
            {
                working.lifecycle = working.stages.Any(stage => stage.lifecycle == NarrativeArcStageLifecycle.Failed) ? NarrativeArcLifecycle.Failed : NarrativeArcLifecycle.Completed;
                working.resolvedWorldTime = signal.worldTime;
                working.revision++;
            }

            return NarrativeArcOperationResult.Success("Narrative arc evaluated.", revision, revision, Snapshot(working, definition), preview: preview);
        }

        private NarrativeArcOperationResult ActivateStage(NarrativeArcRecordData record, NarrativeArcDefinitionData definition, NarrativeArcStageDefinitionData stageDefinition, NarrativeArcSignalRequest signal, bool preview)
        {
            NarrativeArcRecordData working = record.Clone();
            NarrativeArcStageRecordData stage = FindStage(working, stageDefinition.stageDefinitionId);
            List<NarrativeArcBoundQuestRecordData> quests = new List<NarrativeArcBoundQuestRecordData>();
            foreach (NarrativeArcQuestBindingDefinitionData binding in stageDefinition.questBindings)
            {
                NarrativeArcQuestBindingResult result = BindQuest(definition, stageDefinition, working, binding, signal, preview);
                if (!result.Succeeded && binding.required) return Fail(NarrativeArcOperationStatus.QuestBindingFailed, result.Message, Snapshot(working, definition));
                if (result.Succeeded && !string.IsNullOrWhiteSpace(result.QuestId))
                {
                    quests.Add(new NarrativeArcBoundQuestRecordData
                    {
                        bindingDefinitionId = binding.bindingDefinitionId,
                        questId = result.QuestId,
                        questDefinitionId = binding.questDefinitionId,
                        mode = binding.mode,
                        worldTime = signal.worldTime
                    });
                }
            }

            List<NarrativeActionExecutionRecordData> actions = ExecuteActions(definition, stageDefinition, working, stageDefinition.entryActions, signal, preview);
            if (actions.Any(value => value.lifecycle == NarrativeActionLifecycle.Failed)) return Fail(NarrativeArcOperationStatus.ActionFailed, "Required stage entry action failed.", Snapshot(working, definition));
            stage.lifecycle = NarrativeArcStageLifecycle.Active;
            stage.activatedWorldTime = signal.worldTime;
            stage.boundQuests = quests.ToArray();
            stage.actionExecutions = stage.actionExecutions.Concat(actions).ToArray();
            stage.revision++;
            working.lifecycle = NarrativeArcLifecycle.Active;
            working.revision++;
            return NarrativeArcOperationResult.Success("Narrative arc stage activated.", revision, revision, Snapshot(working, definition), preview: preview);
        }

        private NarrativeArcOperationResult CompleteStage(NarrativeArcRecordData record, NarrativeArcDefinitionData definition, NarrativeArcStageDefinitionData stageDefinition, NarrativeArcStageRecordData stage, NarrativeArcSignalRequest signal, bool preview)
        {
            NarrativeArcRecordData working = record.Clone();
            NarrativeArcStageRecordData live = FindStage(working, stage.stageDefinitionId);
            List<NarrativeActionExecutionRecordData> actions = ExecuteActions(definition, stageDefinition, working, stageDefinition.completionActions, signal, preview);
            if (actions.Any(value => value.lifecycle == NarrativeActionLifecycle.Failed)) return Fail(NarrativeArcOperationStatus.ActionFailed, "Required stage completion action failed.", Snapshot(working, definition));
            live.actionExecutions = live.actionExecutions.Concat(actions).ToArray();
            ResolveStage(working, live, NarrativeArcStageLifecycle.Completed, signal);
            if (stageDefinition.terminalOnCompletion)
            {
                working.lifecycle = NarrativeArcLifecycle.Completed;
                working.resolvedWorldTime = signal.worldTime;
                working.revision++;
            }

            return NarrativeArcOperationResult.Success("Narrative arc stage completed.", revision, revision, Snapshot(working, definition), preview: preview);
        }

        private NarrativeArcQuestBindingResult BindQuest(NarrativeArcDefinitionData arc, NarrativeArcStageDefinitionData stage, NarrativeArcRecordData record, NarrativeArcQuestBindingDefinitionData binding, NarrativeArcSignalRequest signal, bool preview)
        {
            if (integrations?.QuestBindingExecutor != null)
            {
                return integrations.QuestBindingExecutor(new NarrativeArcQuestBindingRequest
                {
                    ArcDefinition = arc.Clone(),
                    StageDefinition = stage.Clone(),
                    ArcRecord = record.Clone(),
                    BindingDefinition = binding.Clone(),
                    TransactionId = $"{N(signal.transactionId)}.{binding.bindingDefinitionId}",
                    Preview = preview,
                    WorldTime = signal.worldTime
                });
            }

            if (integrations?.QuestRuntime == null) return new NarrativeArcQuestBindingResult(!binding.required, string.Empty, "QuestRuntime integration is missing.");
            string questId = !string.IsNullOrWhiteSpace(binding.questId) ? binding.questId : BuildQuestId(record.narrativeArcId, stage.stageDefinitionId, binding.bindingDefinitionId);
            if (binding.mode == NarrativeArcQuestBindingMode.ReferenceExistingQuest)
            {
                if (integrations.QuestRuntime.TryGetSnapshot(questId, out _)) return new NarrativeArcQuestBindingResult(true, questId, "Existing quest referenced.");
                QuestSnapshot existing = integrations.QuestRuntime.Query(new QuestQuery { definitionId = binding.questDefinitionId, includeRetired = false, access = QuestVisibilityAccess.PrivilegedDiagnostic }).FirstOrDefault();
                return existing != null
                    ? new NarrativeArcQuestBindingResult(true, existing.QuestId, "Existing quest definition instance referenced.")
                    : new NarrativeArcQuestBindingResult(!binding.required, string.Empty, "Existing quest was not found.");
            }

            if (binding.mode == NarrativeArcQuestBindingMode.ObserveAnyQuestFromDefinitionPlaceholder)
            {
                QuestSnapshot existing = integrations.QuestRuntime.Query(new QuestQuery { definitionId = binding.questDefinitionId, includeRetired = false, access = QuestVisibilityAccess.PrivilegedDiagnostic }).FirstOrDefault();
                return existing != null
                    ? new NarrativeArcQuestBindingResult(true, existing.QuestId, "Observed quest definition instance.")
                    : new NarrativeArcQuestBindingResult(!binding.required, string.Empty, "No quest currently matches observed definition.");
            }

            if (registry == null || !registry.TryGet(binding.questDefinitionId, out QuestDefinition questDefinition))
            {
                return new NarrativeArcQuestBindingResult(!binding.required, string.Empty, $"Quest definition '{binding.questDefinitionId}' is missing.");
            }

            QuestIssuerReferenceData issuer = BuildQuestIssuer(questDefinition, record, binding, integrations);
            QuestRuntimeOperationResult create = integrations.QuestRuntime.CreateQuest(new QuestCreateRequest
            {
                transactionId = $"{N(signal.transactionId)}.{binding.bindingDefinitionId}.quest",
                questId = questId,
                questDefinitionId = binding.questDefinitionId,
                initialLifecycleState = QuestRuntimeLifecycleState.Available,
                issuer = issuer,
                intendedRecipient = BuildQuestRecipient(questDefinition, record),
                origin = new QuestOriginReferenceData { sourceChannel = QuestSourceChannel.WorldEvent, provenanceId = record.narrativeArcId },
                tagIds = new[] { "narrative-arc", arc.arcDefinitionId },
                createdWorldTime = signal.worldTime,
                sourceEventId = record.narrativeArcId,
                provenanceId = record.narrativeArcId,
                preview = preview
            });
            return new NarrativeArcQuestBindingResult(create.Succeeded, create.Snapshot?.QuestId ?? questId, create.Message);
        }

        private static QuestIssuerReferenceData BuildQuestIssuer(QuestDefinition definition, NarrativeArcRecordData record, NarrativeArcQuestBindingDefinitionData binding, NarrativeArcRuntimeIntegrations integrations)
        {
            QuestIssuerType issuerType = PreferredIssuerTypeForBoundSource(definition, binding, integrations);
            if (issuerType == QuestIssuerType.Unknown) issuerType = definition?.SupportedIssuerTypes?.FirstOrDefault(value => value != QuestIssuerType.Unknown) ?? QuestIssuerType.System;
            if (issuerType == QuestIssuerType.Unknown) issuerType = QuestIssuerType.System;
            return new QuestIssuerReferenceData
            {
                issuerType = issuerType,
                issuerId = IssuerIdFor(issuerType, record, binding, integrations),
                provenanceId = record?.narrativeArcId ?? string.Empty
            };
        }

        private static QuestIssuerType PreferredIssuerTypeForBoundSource(QuestDefinition definition, NarrativeArcQuestBindingDefinitionData binding, NarrativeArcRuntimeIntegrations integrations)
        {
            if (definition == null || string.IsNullOrWhiteSpace(binding?.questSourceId) || integrations?.QuestSourceRuntime == null)
            {
                return QuestIssuerType.Unknown;
            }

            if (!integrations.QuestSourceRuntime.TryGetSource(binding.questSourceId, out QuestSourceSnapshot source))
            {
                return QuestIssuerType.Unknown;
            }

            if (!string.IsNullOrWhiteSpace(source.OperatingGovernmentId) && definition.SupportedIssuerTypes.Contains(QuestIssuerType.Government)) return QuestIssuerType.Government;
            if (!string.IsNullOrWhiteSpace(source.OperatingOrganizationId) && definition.SupportedIssuerTypes.Contains(QuestIssuerType.Organization)) return QuestIssuerType.Organization;
            if (!string.IsNullOrWhiteSpace(source.OperatingOfficeId) && definition.SupportedIssuerTypes.Contains(QuestIssuerType.Office)) return QuestIssuerType.Office;
            if (!string.IsNullOrWhiteSpace(source.OperatingBusinessId) && definition.SupportedIssuerTypes.Contains(QuestIssuerType.Business)) return QuestIssuerType.Business;
            if (!string.IsNullOrWhiteSpace(source.OperatingFactionId) && definition.SupportedIssuerTypes.Contains(QuestIssuerType.Faction)) return QuestIssuerType.Faction;
            return QuestIssuerType.Unknown;
        }

        private static QuestRecipientReferenceData BuildQuestRecipient(QuestDefinition definition, NarrativeArcRecordData record)
        {
            QuestRecipientScope preferred = !string.IsNullOrWhiteSpace(record?.actorPersonId) ? QuestRecipientScope.Person : QuestRecipientScope.Open;
            QuestRecipientScope scope = definition?.SupportedRecipientScopes?.Contains(preferred) == true
                ? preferred
                : definition?.SupportedRecipientScopes?.FirstOrDefault(value => value != QuestRecipientScope.Unknown) ?? preferred;
            if (scope == QuestRecipientScope.Unknown) scope = preferred;
            return new QuestRecipientReferenceData
            {
                recipientScope = scope,
                recipientId = RecipientIdFor(scope, record)
            };
        }

        private static string RecipientIdFor(QuestRecipientScope scope, NarrativeArcRecordData record)
        {
            return scope switch
            {
                QuestRecipientScope.Person => string.IsNullOrWhiteSpace(record?.actorPersonId) ? "person.prototype.narrative-arc-recipient" : record.actorPersonId,
                QuestRecipientScope.OrganizationMembers => "organization.prototype.narrative-arc",
                QuestRecipientScope.OrganizationRank => "rank.prototype.narrative-arc",
                QuestRecipientScope.Officeholder => "office.prototype.narrative-arc",
                QuestRecipientScope.Profession => "profession.prototype.narrative-arc",
                QuestRecipientScope.FactionMembers => "faction.prototype.narrative-arc",
                QuestRecipientScope.Citizens => "government.prototype.narrative-arc",
                _ => string.Empty
            };
        }

        private static string IssuerIdFor(QuestIssuerType issuerType, NarrativeArcRecordData record, NarrativeArcQuestBindingDefinitionData binding, NarrativeArcRuntimeIntegrations integrations)
        {
            if (issuerType == QuestIssuerType.System || issuerType == QuestIssuerType.Anonymous) return string.Empty;
            string sourceIssuer = IssuerFromBoundSource(issuerType, binding, integrations);
            if (!string.IsNullOrWhiteSpace(sourceIssuer)) return sourceIssuer;
            return issuerType switch
            {
                QuestIssuerType.Person => string.IsNullOrWhiteSpace(record?.actorPersonId) ? "person.prototype.narrative-arc-issuer" : record.actorPersonId,
                QuestIssuerType.Organization => "organization.prototype.narrative-arc",
                QuestIssuerType.Office => "office.prototype.narrative-arc",
                QuestIssuerType.Government => "government.prototype.narrative-arc",
                QuestIssuerType.Faction => "faction.prototype.narrative-arc",
                QuestIssuerType.Business => "business.prototype.narrative-arc",
                _ => "issuer.prototype.narrative-arc"
            };
        }

        private static string IssuerFromBoundSource(QuestIssuerType issuerType, NarrativeArcQuestBindingDefinitionData binding, NarrativeArcRuntimeIntegrations integrations)
        {
            if (string.IsNullOrWhiteSpace(binding?.questSourceId) || integrations?.QuestSourceRuntime == null)
            {
                return string.Empty;
            }

            if (!integrations.QuestSourceRuntime.TryGetSource(binding.questSourceId, out QuestSourceSnapshot source))
            {
                return string.Empty;
            }

            return issuerType switch
            {
                QuestIssuerType.Organization => source.OperatingOrganizationId,
                QuestIssuerType.Government => source.OperatingGovernmentId,
                QuestIssuerType.Office => source.OperatingOfficeId,
                QuestIssuerType.Faction => source.OperatingFactionId,
                QuestIssuerType.Business => source.OperatingBusinessId,
                _ => string.Empty
            };
        }

        private List<NarrativeActionExecutionRecordData> ExecuteActions(NarrativeArcDefinitionData arc, NarrativeArcStageDefinitionData stage, NarrativeArcRecordData record, IEnumerable<NarrativeActionDefinitionData> actions, NarrativeArcSignalRequest signal, bool preview)
        {
            List<NarrativeActionExecutionRecordData> records = new List<NarrativeActionExecutionRecordData>();
            int index = 0;
            foreach (NarrativeActionDefinitionData action in (actions ?? Array.Empty<NarrativeActionDefinitionData>()).OrderBy(value => value.order).ThenBy(value => value.actionDefinitionId, StringComparer.Ordinal))
            {
                index++;
                bool succeeded = action.category == NarrativeActionCategory.None || preview || ExecuteAction(arc, stage, record, action, signal, preview);
                records.Add(new NarrativeActionExecutionRecordData
                {
                    actionExecutionId = $"{BuildStageRuntimeId(record.narrativeArcId, stage.stageDefinitionId)}.{NarrativeModelUtility.SanitizeForId(action.actionDefinitionId)}.{index:000}",
                    narrativeEventId = record.narrativeArcId,
                    actionDefinitionId = action.actionDefinitionId,
                    category = action.category,
                    lifecycle = preview ? NarrativeActionLifecycle.Prepared : succeeded ? NarrativeActionLifecycle.Committed : action.requirement == NarrativeActionRequirement.Required ? NarrativeActionLifecycle.Failed : NarrativeActionLifecycle.SkippedOptional,
                    requirement = action.requirement,
                    order = action.order,
                    targetOwnerRuntime = OwnerRuntime(action.category),
                    externalResultId = action.targetId,
                    resultValue = action.targetId,
                    message = preview ? "Narrative arc action previewed." : succeeded ? "Narrative arc action committed." : "Narrative arc action rejected.",
                    worldTime = signal.worldTime,
                    runtimeRevision = revision
                });
                if (!succeeded && action.requirement == NarrativeActionRequirement.Required) break;
            }

            return records;
        }

        private bool ExecuteAction(NarrativeArcDefinitionData arc, NarrativeArcStageDefinitionData stage, NarrativeArcRecordData record, NarrativeActionDefinitionData action, NarrativeArcSignalRequest signal, bool preview)
        {
            if (action.category == NarrativeActionCategory.None) return true;
            if (integrations?.ActionExecutor != null)
            {
                return integrations.ActionExecutor(action.Clone(), new NarrativeArcActionContext
                {
                    ArcDefinition = arc.Clone(),
                    StageDefinition = stage.Clone(),
                    ArcRecord = record.Clone(),
                    ConditionContext = signal.conditionContext?.Clone(),
                    TransactionId = signal.transactionId,
                    Preview = preview,
                    WorldTime = signal.worldTime
                });
            }

            if (action.category == NarrativeActionCategory.EmitNarrativeSignal)
            {
                if (integrations?.NarrativeEventRuntime == null) return action.requirement != NarrativeActionRequirement.Required;
                NarrativeEventOperationResult emitted = integrations.NarrativeEventRuntime.EmitSignal(new NarrativeSignalRequest
                {
                    transactionId = $"{N(signal.transactionId)}.{action.actionDefinitionId}.signal",
                    signalDefinitionId = action.targetId,
                    sourceKind = NarrativeSignalSourceKind.NarrativeArcProgression,
                    sourceId = record.narrativeArcId,
                    actorPersonId = record.actorPersonId,
                    subjectIds = new[] { record.subjectId },
                    conditionContext = signal.conditionContext?.Clone(),
                    worldTime = signal.worldTime,
                    cascadeDepth = signal.cascadeDepth + 1
                });
                return emitted.Succeeded;
            }

            if (action.category == NarrativeActionCategory.RequestNarrativeStateTransition)
            {
                if (integrations?.NarrativeStateRuntime == null) return action.requirement != NarrativeActionRequirement.Required;
                NarrativeStateTransitionResult transition = integrations.NarrativeStateRuntime.RequestTransition(new NarrativeStateTransitionRequest
                {
                    transactionId = $"{N(signal.transactionId)}.{action.actionDefinitionId}.state",
                    transitionDefinitionId = action.targetId,
                    scopeKey = action.secondaryTargetId,
                    actorPersonId = record.actorPersonId,
                    sourceKind = NarrativeTransitionSourceKind.NarrativeEvent,
                    sourceId = record.narrativeArcId,
                    worldTime = signal.worldTime,
                    conditionContext = signal.conditionContext?.Clone()
                });
                return transition.Succeeded;
            }

            return action.requirement != NarrativeActionRequirement.Required;
        }

        private bool DependenciesSatisfied(NarrativeArcRecordData record, IEnumerable<NarrativeArcDependencyDefinitionData> dependencies, NarrativeArcSignalRequest signal, bool emptySatisfied)
        {
            NarrativeArcDependencyDefinitionData[] deps = (dependencies ?? Array.Empty<NarrativeArcDependencyDefinitionData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
            if (deps.Length == 0) return emptySatisfied;
            return deps.All(dep => dep.optional || DependencySatisfied(record, dep, signal));
        }

        private bool DependencySatisfied(NarrativeArcRecordData record, NarrativeArcDependencyDefinitionData dependency, NarrativeArcSignalRequest signal)
        {
            switch (dependency.kind)
            {
                case NarrativeArcDependencyKind.StageCompleted:
                    return StageLifecycle(record, dependency.requiredId) == NarrativeArcStageLifecycle.Completed;
                case NarrativeArcDependencyKind.StageSkipped:
                    return StageLifecycle(record, dependency.requiredId) == NarrativeArcStageLifecycle.Skipped;
                case NarrativeArcDependencyKind.StageResolved:
                    return IsResolved(StageLifecycle(record, dependency.requiredId));
                case NarrativeArcDependencyKind.AllStagesResolved:
                    return dependency.stageDefinitionIds.All(id => IsResolved(StageLifecycle(record, id)));
                case NarrativeArcDependencyKind.AnyStageResolved:
                    return dependency.stageDefinitionIds.Any(id => IsResolved(StageLifecycle(record, id)));
                case NarrativeArcDependencyKind.AtLeastNStagesResolved:
                    return dependency.stageDefinitionIds.Count(id => IsResolved(StageLifecycle(record, id))) >= Math.Max(1, dependency.minimumCount);
                case NarrativeArcDependencyKind.QuestOutcome:
                    return QuestOutcomeMatches(dependency, signal) || QueryQuestOutcomeMatches(dependency, record);
                case NarrativeArcDependencyKind.NarrativeState:
                    return NarrativeStateMatches(dependency, signal);
                case NarrativeArcDependencyKind.DialogueChoice:
                    return signal.category == NarrativeArcSignalCategory.DialogueChoice && (string.Equals(signal.sourceId, dependency.requiredId, StringComparison.Ordinal) || string.Equals(signal.value, dependency.requiredId, StringComparison.Ordinal) || Contains(signal.conditionContext?.dialogueStateIds, dependency.requiredId));
                case NarrativeArcDependencyKind.NarrativeEvent:
                    return signal.category == NarrativeArcSignalCategory.NarrativeEvent && (string.Equals(signal.sourceId, dependency.requiredId, StringComparison.Ordinal) || string.Equals(signal.signalId, dependency.requiredId, StringComparison.Ordinal));
                case NarrativeArcDependencyKind.CurrentWorldCondition:
                    return signal.category == NarrativeArcSignalCategory.CurrentWorldCondition && (string.Equals(signal.sourceId, dependency.requiredId, StringComparison.Ordinal) || Contains(signal.conditionContext?.customStateIds, dependency.requiredId));
                case NarrativeArcDependencyKind.ArcCompleted:
                    return Query(new NarrativeArcQuery { arcDefinitionId = dependency.requiredId, lifecycle = NarrativeArcLifecycle.Completed, scopeKey = dependency.secondaryId }).Any();
                case NarrativeArcDependencyKind.ArcResolved:
                    return Query(new NarrativeArcQuery { arcDefinitionId = dependency.requiredId, scopeKey = dependency.secondaryId }).Any(arc => arc.Lifecycle == NarrativeArcLifecycle.Completed || arc.Lifecycle == NarrativeArcLifecycle.Failed || arc.Lifecycle == NarrativeArcLifecycle.Cancelled);
                case NarrativeArcDependencyKind.Custom:
                    return Contains(signal.conditionContext?.customStateIds, dependency.requiredId);
                default:
                    return false;
            }
        }

        private bool QuestOutcomeMatches(NarrativeArcDependencyDefinitionData dependency, NarrativeArcSignalRequest signal)
        {
            if (signal.category != NarrativeArcSignalCategory.QuestOutcome) return false;
            bool idMatches = string.Equals(signal.questDefinitionId, dependency.requiredId, StringComparison.Ordinal)
                || string.Equals(signal.questId, dependency.requiredId, StringComparison.Ordinal)
                || string.Equals(signal.sourceId, dependency.requiredId, StringComparison.Ordinal);
            if (!idMatches) return false;
            if (string.IsNullOrWhiteSpace(dependency.requiredValue)) return signal.questOutcomeKind == QuestTerminalOutcomeKind.Completed;
            return Enum.TryParse(dependency.requiredValue, ignoreCase: true, out QuestTerminalOutcomeKind expected) && signal.questOutcomeKind == expected;
        }

        private bool QueryQuestOutcomeMatches(NarrativeArcDependencyDefinitionData dependency, NarrativeArcRecordData record)
        {
            if (integrations?.QuestOutcomeRuntime == null) return false;
            QuestTerminalOutcomeKind expected = QuestTerminalOutcomeKind.Completed;
            if (!string.IsNullOrWhiteSpace(dependency.requiredValue)) Enum.TryParse(dependency.requiredValue, ignoreCase: true, out expected);
            string[] boundQuestIds = record.stages.SelectMany(stage => stage.boundQuests ?? Array.Empty<NarrativeArcBoundQuestRecordData>()).Select(value => value.questId).ToArray();
            return integrations.QuestOutcomeRuntime.QueryOutcomes(new QuestOutcomeQuery { access = QuestVisibilityAccess.PrivilegedDiagnostic })
                .Any(outcome => outcome.OutcomeKind == expected && (string.Equals(outcome.QuestDefinitionId, dependency.requiredId, StringComparison.Ordinal) || string.Equals(outcome.QuestId, dependency.requiredId, StringComparison.Ordinal) || boundQuestIds.Contains(outcome.QuestId, StringComparer.Ordinal)));
        }

        private bool NarrativeStateMatches(NarrativeArcDependencyDefinitionData dependency, NarrativeArcSignalRequest signal)
        {
            if (signal.category == NarrativeArcSignalCategory.NarrativeState && (string.Equals(signal.sourceId, dependency.requiredId, StringComparison.Ordinal) || Contains(signal.conditionContext?.narrativeStateIds, dependency.requiredId))) return true;
            if (integrations?.NarrativeStateRuntime == null) return false;
            string[] parts = dependency.requiredId.Split('|');
            string stateId = parts.Length > 0 ? parts[0] : dependency.requiredId;
            string variableId = parts.Length > 1 ? parts[1] : dependency.secondaryId;
            string expected = parts.Length > 2 ? parts[2] : dependency.requiredValue;
            if (string.IsNullOrWhiteSpace(stateId) || string.IsNullOrWhiteSpace(variableId) || string.IsNullOrWhiteSpace(expected)) return false;
            return integrations.NarrativeStateRuntime.EvaluateCondition(new NarrativeStateConditionQuery
            {
                stateDefinitionId = stateId,
                variableDefinitionId = variableId,
                scope = string.IsNullOrWhiteSpace(signal.actorPersonId) ? NarrativeStateScope.World : NarrativeStateScope.Person,
                scopeKey = string.IsNullOrWhiteSpace(signal.scopeKey) ? signal.actorPersonId : signal.scopeKey,
                expectedValue = NarrativeVariableValueData.Token(expected)
            });
        }

        private IEnumerable<NarrativeArcRecordData> ResolveCandidates(NarrativeArcSignalRequest request)
        {
            IEnumerable<NarrativeArcRecordData> records = arcsById.Values;
            if (!string.IsNullOrWhiteSpace(request.narrativeArcId)) records = records.Where(value => string.Equals(value.narrativeArcId, N(request.narrativeArcId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(request.arcDefinitionId)) records = records.Where(value => string.Equals(value.arcDefinitionId, N(request.arcDefinitionId), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(request.scopeKey)) records = records.Where(value => string.Equals(value.scopeKey, N(request.scopeKey), StringComparison.Ordinal));
            else if (!string.IsNullOrWhiteSpace(request.actorPersonId)) records = records.Where(value => value.scope == NarrativeArcScope.World || string.Equals(value.scopeKey, N(request.actorPersonId), StringComparison.Ordinal));
            return records.Where(value => value.lifecycle == NarrativeArcLifecycle.Active || value.lifecycle == NarrativeArcLifecycle.Eligible).Select(value => value.Clone()).ToArray();
        }

        private static void ResolveStage(NarrativeArcRecordData record, NarrativeArcStageRecordData stage, NarrativeArcStageLifecycle lifecycle, NarrativeArcSignalRequest signal)
        {
            stage.lifecycle = lifecycle;
            stage.resolvedWorldTime = signal.worldTime;
            stage.resolvedBySignalId = signal.signalId ?? string.Empty;
            stage.revision++;
            record.revision++;
        }

        private bool MarkSignalProcessed(NarrativeArcRecordData record, NarrativeArcSignalRequest signal)
        {
            string key = StableSignalKey(signal);
            HashSet<string> processed = new HashSet<string>(record.processedSignalKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (!processed.Add(key)) return false;
            record.processedSignalKeys = processed.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return true;
        }

        private NarrativeArcStageLifecycle StageLifecycle(NarrativeArcRecordData record, string stageDefinitionId)
        {
            return FindStage(record, stageDefinitionId)?.lifecycle ?? NarrativeArcStageLifecycle.Unknown;
        }

        private static NarrativeArcStageRecordData FindStage(NarrativeArcRecordData record, string stageDefinitionId)
        {
            return (record.stages ?? Array.Empty<NarrativeArcStageRecordData>()).FirstOrDefault(value => string.Equals(value.stageDefinitionId, stageDefinitionId, StringComparison.Ordinal));
        }

        private NarrativeArcRecordData CreateRecord(NarrativeArcDefinitionData definition, NarrativeArcStartRequest request, string scopeKey)
        {
            string arcId = string.IsNullOrWhiteSpace(request.narrativeArcId) ? BuildArcId(definition.arcDefinitionId, scopeKey) : N(request.narrativeArcId);
            return new NarrativeArcRecordData
            {
                narrativeArcId = arcId,
                arcDefinitionId = definition.arcDefinitionId,
                worldId = worldId,
                scope = definition.scope,
                lifecycle = NarrativeArcLifecycle.Active,
                scopeKey = scopeKey,
                actorPersonId = N(request.actorPersonId),
                subjectId = N(request.subjectId),
                startedWorldTime = request.worldTime,
                provenanceId = N(request.provenanceId),
                stages = definition.stages.Select(stage => new NarrativeArcStageRecordData
                {
                    stageDefinitionId = stage.stageDefinitionId,
                    stageRuntimeId = BuildStageRuntimeId(arcId, stage.stageDefinitionId),
                    lifecycle = NarrativeArcStageLifecycle.Locked,
                    revision = 1L
                }).ToArray(),
                revision = 1L
            };
        }

        private void CommitRecord(NarrativeArcRecordData record)
        {
            NarrativeArcRecordData clone = record.Clone();
            arcsById[clone.narrativeArcId] = clone;
            arcByDefinitionScope[BuildDefinitionScopeKey(clone.arcDefinitionId, clone.scopeKey)] = clone.narrativeArcId;
        }

        private NarrativeArcSnapshot Snapshot(NarrativeArcRecordData record, NarrativeArcDefinitionData definition = null, bool developmentView = true)
        {
            NarrativeArcDefinitionData data = definition;
            if (data == null && registry != null && registry.TryGet(record.arcDefinitionId, out NarrativeArcDefinition asset)) data = asset.ToRecordData();
            return new NarrativeArcSnapshot(record, data, developmentView);
        }

        private bool TryResolveDefinition(string definitionId, out NarrativeArcDefinitionData definition, out NarrativeArcOperationResult failure)
        {
            definition = null;
            failure = null;
            if (registry == null)
            {
                failure = Fail(NarrativeArcOperationStatus.MissingDefinitionRegistry, "Narrative arc runtime has no definition registry.");
                return false;
            }

            if (!registry.TryGet(N(definitionId), out NarrativeArcDefinition asset))
            {
                failure = Fail(NarrativeArcOperationStatus.MissingDefinition, $"NarrativeArcDefinition '{N(definitionId)}' is missing.");
                return false;
            }

            definition = asset.ToRecordData();
            NarrativeArcValidationReport report = NarrativeArcDefinitionValidator.Validate(definition, registry.DefinitionsById);
            if (!report.Succeeded)
            {
                failure = Fail(NarrativeArcOperationStatus.DefinitionInvalid, string.Join(" | ", report.Errors));
                return false;
            }

            return true;
        }

        private bool ValidateRevision(long expected, out NarrativeArcOperationResult failure)
        {
            failure = null;
            if (expected < 0L || expected == revision) return true;
            failure = Fail(NarrativeArcOperationStatus.RevisionConflict, $"Expected revision {expected}, actual {revision}.");
            return false;
        }

        private bool TryDuplicate(string transactionId, out NarrativeArcOperationResult result)
        {
            result = null;
            string tx = N(transactionId);
            if (string.IsNullOrWhiteSpace(tx)) return false;
            if (!transactionsById.TryGetValue(tx, out NarrativeArcRuntimeTransactionData transaction)) return false;
            NarrativeArcSnapshot snapshot = !string.IsNullOrWhiteSpace(transaction.narrativeArcId) && arcsById.TryGetValue(transaction.narrativeArcId, out NarrativeArcRecordData record) ? Snapshot(record) : null;
            result = NarrativeArcOperationResult.Success("Duplicate narrative arc transaction ignored.", revision, revision, snapshot, duplicate: true);
            return true;
        }

        private void RecordTransaction(string transactionId, string operation, string arcId, string stageId, NarrativeArcOperationStatus status)
        {
            string tx = N(transactionId);
            if (string.IsNullOrWhiteSpace(tx)) return;
            transactionsById[tx] = new NarrativeArcRuntimeTransactionData
            {
                transactionId = tx,
                operation = operation ?? string.Empty,
                narrativeArcId = arcId ?? string.Empty,
                stageDefinitionId = stageId ?? string.Empty,
                status = status,
                runtimeRevision = revision
            };
        }

        private void RebuildIndexes()
        {
            arcByDefinitionScope.Clear();
            foreach (NarrativeArcRecordData record in arcsById.Values) arcByDefinitionScope[BuildDefinitionScopeKey(record.arcDefinitionId, record.scopeKey)] = record.narrativeArcId;
        }

        private string ResolveScopeKey(NarrativeArcDefinitionData definition, string explicitScopeKey, string actorPersonId, string subjectId, NarrativeConditionContextData context)
        {
            if (!string.IsNullOrWhiteSpace(explicitScopeKey)) return N(explicitScopeKey);
            NarrativeConditionContextData data = context?.Clone() ?? new NarrativeConditionContextData();
            return definition.scope == NarrativeArcScope.World ? worldId : First(actorPersonId, data.actorPersonId, subjectId, data.subjectId);
        }

        private string ResolveConditionScopeKey(NarrativeConditionDefinitionData condition, NarrativeConditionContextData context)
        {
            return First(condition.secondaryId, context?.actorPersonId, context?.subjectId, worldId);
        }

        private NarrativeArcOperationResult Fail(NarrativeArcOperationStatus status, string message, NarrativeArcSnapshot snapshot = null) => NarrativeArcOperationResult.Failure(status, message, revision, snapshot);
        private static string First(params string[] values) => values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        private static string N(string value) => NarrativeModelUtility.N(value);
        private static bool Contains(IEnumerable<string> values, string id) => !string.IsNullOrWhiteSpace(id) && (values ?? Array.Empty<string>()).Contains(id, StringComparer.Ordinal);
        private static bool IsResolved(NarrativeArcStageLifecycle lifecycle) => lifecycle == NarrativeArcStageLifecycle.Completed || lifecycle == NarrativeArcStageLifecycle.Skipped || lifecycle == NarrativeArcStageLifecycle.Failed || lifecycle == NarrativeArcStageLifecycle.Historical;
        private static string StableSignalKey(NarrativeArcSignalRequest signal) => $"{signal.category}:{N(signal.signalId)}:{N(signal.sourceId)}:{N(signal.secondaryId)}:{N(signal.value)}:{N(signal.questId)}:{signal.questOutcomeKind}:{signal.worldTime:0.###}";
        private static string BuildArcId(string definitionId, string scopeKey) => $"narrative-arc.{NarrativeModelUtility.SanitizeForId(definitionId)}.{NarrativeModelUtility.SanitizeForId(scopeKey)}";
        public static string BuildStageRuntimeId(string narrativeArcId, string stageDefinitionId) => $"narrative-arc-stage.{NarrativeModelUtility.SanitizeForId(narrativeArcId)}.{NarrativeModelUtility.SanitizeForId(stageDefinitionId)}";
        public static string BuildDefinitionScopeKey(string arcDefinitionId, string scopeKey) => $"{N(arcDefinitionId)}::{N(scopeKey)}";
        private static string BuildQuestId(string arcId, string stageId, string bindingId) => $"quest.arc.{NarrativeModelUtility.SanitizeForId(arcId)}.{NarrativeModelUtility.SanitizeForId(stageId)}.{NarrativeModelUtility.SanitizeForId(bindingId)}";
        private static string OwnerRuntime(NarrativeActionCategory category) => category switch
        {
            NarrativeActionCategory.EmitNarrativeSignal => "NarrativeEventRuntime",
            NarrativeActionCategory.RequestNarrativeStateTransition => "NarrativeStateRuntime",
            NarrativeActionCategory.RequestNarrativeArcProgression => "NarrativeArcRuntime",
            NarrativeActionCategory.InstantiateQuest or NarrativeActionCategory.PublishQuestListing or NarrativeActionCategory.CreateQuestOffer or NarrativeActionCategory.DirectAssignQuest => "QuestRuntime",
            _ => "NarrativeArcRuntime"
        };
    }
}
