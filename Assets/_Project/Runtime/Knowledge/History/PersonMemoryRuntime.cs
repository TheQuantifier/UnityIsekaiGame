using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Knowledge.History
{
    public sealed class PersonMemoryRuntime
    {
        private readonly Dictionary<string, HistoryMemoryRecordData> memoriesById = new Dictionary<string, HistoryMemoryRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> memoryIdsByEvent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> processedTransactions = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private AuthoritativeHistoryRuntime historyRuntime;
        private DefinitionRegistry registry;
        private bool suppressEvents;

        public event Action<PersonMemoryRuntime, HistoryOperationResult> MemoryChanged;

        public string PersonId { get; private set; } = string.Empty;
        public long MemoryRevision { get; private set; }

        public void Configure(string personId, DefinitionRegistry definitionRegistry, AuthoritativeHistoryRuntime authoritativeHistory, IEnumerable<string> knownPersons = null)
        {
            string normalizedPersonId = personId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(PersonId) && !string.Equals(PersonId, normalizedPersonId, StringComparison.Ordinal))
            {
                memoriesById.Clear();
                memoryIdsByEvent.Clear();
                processedTransactions.Clear();
                MemoryRevision = 0L;
            }

            PersonId = normalizedPersonId;
            registry = definitionRegistry ?? registry;
            historyRuntime = authoritativeHistory ?? historyRuntime;
            knownPersonIds = new HashSet<string>((knownPersons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
        }

        public HistoryOperationResult PreviewFormMemory(FormMemoryRequest request, PersonKnowledgeRuntime knowledge = null)
        {
            return FormMemory(request, knowledge, preview: true, restoring: false);
        }

        public HistoryOperationResult FormMemory(FormMemoryRequest request, PersonKnowledgeRuntime knowledge = null, bool preview = false, bool restoring = false)
        {
            long priorRevision = MemoryRevision;
            if (!ValidateFormMemory(request, out string failure, out HistoryResultCode code))
            {
                return HistoryOperationResult.Failure(code, failure, request?.TransactionId, preview, MemoryRevision);
            }

            if (!preview && processedTransactions.Contains(request.TransactionId ?? string.Empty))
            {
                return HistoryOperationResult.Success("Memory transaction already processed.", request.TransactionId, null, TryWrap(request.MemoryId), null, priorRevision, MemoryRevision, duplicate: true);
            }

            HistoricalEventRecord linkedEvent = null;
            if (!string.IsNullOrWhiteSpace(request.HistoricalEventId))
            {
                historyRuntime?.TryGetEvent(request.HistoricalEventId, out linkedEvent);
            }

            HistoryMemoryRecordData data = new HistoryMemoryRecordData
            {
                memoryId = request.MemoryId,
                ownerPersonId = request.OwnerPersonId,
                historicalEventId = request.HistoricalEventId,
                beliefId = request.BeliefId,
                evidenceIds = request.EvidenceIds == null ? Array.Empty<string>() : request.EvidenceIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray(),
                source = request.Source,
                formedAtWorldTime = request.FormedAtWorldTime,
                rememberedOccurredAtWorldTime = request.RememberedOccurredAtWorldTime,
                lastRecalledWorldTime = -1d,
                lastRecallAttemptWorldTime = -1d,
                lastReinforcedWorldTime = -1d,
                lastDegradationEvaluatedWorldTime = request.FormedAtWorldTime,
                confidence = HistoryMath.ClampMetric(request.Confidence),
                clarity = HistoryMath.ClampMetric(request.Clarity),
                salience = HistoryMath.ClampMetric(request.Salience),
                firstHand = request.FirstHand,
                state = MemoryState.Accessible,
                visibility = request.Visibility,
                stateBeforeSuppression = MemoryState.Accessible,
                identityAtTimeId = request.IdentityAtTimeId,
                bodyAtTimeId = request.BodyAtTimeId,
                currentRevisionId = $"{request.MemoryId}.revision.0",
                rememberedDetails = BuildInitialDetails(request, linkedEvent),
                suppressions = Array.Empty<MemorySuppressionData>(),
                tags = request.Tags == null ? Array.Empty<string>() : request.Tags.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray(),
                debugDescription = request.DebugDescription
            };
            data.revisions = new[]
            {
                CreateRevision(data, request.TransactionId, MemoryAlterationType.None, request.FormedAtWorldTime, request.Source.ToString(), "Memory formed.", previousRevisionId: string.Empty)
            };

            KnowledgeOperationResult knowledgeResult = null;
            if (request.CreateKnowledgeEvidence && knowledge != null && historyRuntime != null && historyRuntime.TryGetEvent(request.HistoricalEventId, out HistoricalEventRecord historicalEvent))
            {
                knowledgeResult = CreateKnowledgeForMemory(request, knowledge, historicalEvent, preview);
                if (!knowledgeResult.Succeeded)
                {
                    return HistoryOperationResult.Failure(HistoryResultCode.KnowledgeRejected, knowledgeResult.Message, request.TransactionId, preview, MemoryRevision);
                }
            }

            HistoryMemoryRecord snapshot = new HistoryMemoryRecord(data);
            if (preview)
            {
                return HistoryOperationResult.Success("Memory preview succeeded.", request.TransactionId, null, snapshot, knowledgeResult, priorRevision, MemoryRevision, preview: true);
            }

            memoriesById[data.memoryId] = data;
            AddIndex(data.historicalEventId, data.memoryId);
            processedTransactions.Add(request.TransactionId ?? string.Empty);
            MemoryRevision++;
            HistoryOperationResult result = HistoryOperationResult.Success("Memory formed.", request.TransactionId, null, new HistoryMemoryRecord(data), knowledgeResult, priorRevision, MemoryRevision);
            RaiseChanged(result, restoring);
            return result;
        }

        public HistoryOperationResult RecallMemory(string memoryId, string transactionId, double recalledAtWorldTime, int clarityReinforcement = 50, bool preview = false, bool restoring = false)
        {
            MemoryRecallResult recalled = Recall(new MemoryRecallRequest
            {
                TransactionId = transactionId,
                RequestingPersonId = PersonId,
                MemoryId = memoryId,
                WorldTime = recalledAtWorldTime,
                AttemptDifficult = true,
                AllowCueRecovery = true,
                ReinforceOnSuccess = clarityReinforcement > 0,
                MutateMetadata = true,
                AccessContext = MemoryAccessContext.OrdinaryRecall
            }, preview, restoring);
            HistoryMemoryRecord memory = recalled.Entries.FirstOrDefault()?.Memory ?? TryWrap(memoryId);
            return recalled.Succeeded
                ? HistoryOperationResult.Success(recalled.Message, transactionId, null, memory, null, recalled.PriorRevision, recalled.ResultingRevision, preview: recalled.Preview)
                : HistoryOperationResult.Failure(recalled.Code, recalled.Message, transactionId, preview, MemoryRevision);
        }

        public HistoryOperationResult ForgetMemory(string memoryId, string transactionId, int clarityReduction = 1000, bool preview = false, bool restoring = false)
        {
            MemoryState resultingState = clarityReduction >= 1000 ? MemoryState.Forgotten : MemoryState.Inaccessible;
            double worldTime = memoriesById.TryGetValue(memoryId ?? string.Empty, out HistoryMemoryRecordData existing)
                ? Math.Max(existing.formedAtWorldTime, existing.lastRecallAttemptWorldTime)
                : 0d;
            return AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = transactionId,
                OwnerPersonId = PersonId,
                MemoryId = memoryId,
                WorldTime = worldTime,
                AlterationType = MemoryAlterationType.DetailLoss,
                ResultingState = resultingState,
                ClarityDelta = -Math.Max(0, clarityReduction),
                Description = "Memory access reduced through compatibility forgetting API."
            }, preview, restoring);
        }

        public HistoryOperationResult CorrectMemory(string existingMemoryId, FormMemoryRequest correctedMemoryRequest, bool preview = false, bool restoring = false)
        {
            if (!memoriesById.TryGetValue(existingMemoryId ?? string.Empty, out HistoryMemoryRecordData existing))
            {
                return HistoryOperationResult.Failure(HistoryResultCode.MissingMemory, $"Memory '{existingMemoryId}' is missing.", correctedMemoryRequest?.TransactionId, preview, MemoryRevision);
            }

            HistoryOperationResult formed = FormMemory(correctedMemoryRequest, null, preview, restoring);
            if (!formed.Succeeded || preview)
            {
                return formed;
            }

            existing.state = MemoryState.Corrected;
            existing.correctedByMemoryId = correctedMemoryRequest.MemoryId;
            memoriesById[correctedMemoryRequest.MemoryId].correctionOfMemoryId = existingMemoryId;
            MemoryRevision++;
            return formed;
        }

        public MemoryRecallResult Recall(MemoryRecallRequest request, bool preview = false, bool restoring = false)
        {
            long priorRevision = MemoryRevision;
            if (!ValidateRecallRequest(request, out string failure, out HistoryResultCode code))
            {
                return MemoryRecallResult.Failure(code, code == HistoryResultCode.AccessDenied ? MemoryRecallOutcome.AccessDenied : MemoryRecallOutcome.NoMatch, request?.TransactionId, failure, MemoryRevision);
            }

            IReadOnlyList<HistoryMemoryRecordData> candidates = QueryMemoryData(request);
            if (candidates.Count == 0)
            {
                return MemoryRecallResult.Failure(HistoryResultCode.NoMatch, MemoryRecallOutcome.NoMatch, request.TransactionId, "No matching memory exists.", MemoryRevision);
            }

            List<MemoryRecallEntry> entries = new List<MemoryRecallEntry>();
            bool mutated = false;
            bool anySuccess = false;
            foreach (HistoryMemoryRecordData memory in candidates)
            {
                MemoryRecallEntry entry = EvaluateRecall(memory, request, preview, out bool entrySuccess, out bool entryMutated);
                entries.Add(entry);
                anySuccess |= entrySuccess;
                mutated |= entryMutated;
            }

            if (!preview && mutated)
            {
                processedTransactions.Add(request.TransactionId ?? string.Empty);
                MemoryRevision++;
                HistoryOperationResult changed = HistoryOperationResult.Success("Memory recall metadata updated.", request.TransactionId, null, entries.FirstOrDefault()?.Memory, null, priorRevision, MemoryRevision);
                RaiseChanged(changed, restoring);
            }

            MemoryRecallOutcome aggregate = ResolveAggregateOutcome(entries);
            HistoryResultCode resultCode = anySuccess ? HistoryResultCode.Success : FailureCodeFor(aggregate);
            return anySuccess
                ? MemoryRecallResult.Success(aggregate, request.TransactionId, preview ? "Memory recall preview succeeded." : "Memory recall evaluated.", entries, priorRevision, MemoryRevision, preview)
                : MemoryRecallResult.Failure(resultCode, aggregate, request.TransactionId, entries.FirstOrDefault()?.Reason ?? "Recall failed.", MemoryRevision);
        }

        public HistoryOperationResult ReinforceMemory(MemoryReinforcementRequest request, bool preview = false, bool restoring = false)
        {
            long priorRevision = MemoryRevision;
            if (!ValidateMemoryMutation(request?.OwnerPersonId, request?.MemoryId, request?.TransactionId, request?.WorldTime ?? 0d, out HistoryMemoryRecordData memory, out string failure, out HistoryResultCode code))
            {
                return HistoryOperationResult.Failure(code, failure, request?.TransactionId, preview, MemoryRevision);
            }

            if (!preview && processedTransactions.Contains(request.TransactionId ?? string.Empty))
            {
                return HistoryOperationResult.Success("Memory reinforcement transaction already processed.", request.TransactionId, null, new HistoryMemoryRecord(memory), null, priorRevision, MemoryRevision, duplicate: true);
            }

            HistoryMemoryRecordData working = memory.Clone();
            ApplyReinforcement(working, request);
            AddRevision(working, request.TransactionId, MemoryAlterationType.Reinforcement, request.WorldTime, request.SourceId, $"Reinforced by {request.Source}.");
            if (preview)
            {
                return HistoryOperationResult.Success("Memory reinforcement preview succeeded.", request.TransactionId, null, new HistoryMemoryRecord(working), null, priorRevision, MemoryRevision, preview: true);
            }

            memoriesById[memory.memoryId] = working;
            processedTransactions.Add(request.TransactionId ?? string.Empty);
            MemoryRevision++;
            HistoryOperationResult result = HistoryOperationResult.Success("Memory reinforced.", request.TransactionId, null, new HistoryMemoryRecord(working), null, priorRevision, MemoryRevision);
            RaiseChanged(result, restoring);
            return result;
        }

        public HistoryOperationResult ApplyDegradation(MemoryDegradationRequest request, bool preview = false, bool restoring = false)
        {
            long priorRevision = MemoryRevision;
            if (!ValidateMemoryMutation(request?.OwnerPersonId, request?.MemoryId, request?.TransactionId, request?.ToWorldTime ?? 0d, out HistoryMemoryRecordData memory, out string failure, out HistoryResultCode code))
            {
                return HistoryOperationResult.Failure(code, failure, request?.TransactionId, preview, MemoryRevision);
            }

            if (request.ToWorldTime < request.FromWorldTime)
            {
                return HistoryOperationResult.Failure(HistoryResultCode.InvalidTimeRange, "Memory degradation end time cannot be earlier than start time.", request.TransactionId, preview, MemoryRevision);
            }

            if (!preview && processedTransactions.Contains(request.TransactionId ?? string.Empty))
            {
                return HistoryOperationResult.Success("Memory degradation transaction already processed.", request.TransactionId, null, new HistoryMemoryRecord(memory), null, priorRevision, MemoryRevision, duplicate: true);
            }

            double effectiveFrom = Math.Max(request.FromWorldTime, memory.lastDegradationEvaluatedWorldTime);
            if (request.ToWorldTime <= effectiveFrom)
            {
                return HistoryOperationResult.Success("Memory degradation already evaluated for this world-time boundary.", request.TransactionId, null, new HistoryMemoryRecord(memory), null, priorRevision, MemoryRevision);
            }

            HistoryMemoryRecordData working = memory.Clone();
            double days = Math.Max(0d, request.ToWorldTime - effectiveFrom) / 86400d;
            working.confidence = HistoryMath.ClampMetric(working.confidence - (int)Math.Floor(days * Math.Max(0, request.ConfidenceLossPerDay)));
            working.clarity = HistoryMath.ClampMetric(working.clarity - (int)Math.Floor(days * Math.Max(0, request.ClarityLossPerDay)));
            working.salience = HistoryMath.ClampMetric(working.salience - (int)Math.Floor(days * Math.Max(0, request.SalienceLossPerDay)));
            working.lastDegradationEvaluatedWorldTime = request.ToWorldTime;
            if (working.clarity <= request.ForgottenClarityThreshold)
            {
                working.state = MemoryState.Forgotten;
            }
            else if (working.clarity <= request.InaccessibleClarityThreshold)
            {
                working.state = MemoryState.Inaccessible;
            }
            else if (working.clarity <= request.DifficultClarityThreshold && working.state == MemoryState.Accessible)
            {
                working.state = MemoryState.Difficult;
            }

            if (request.CreateRevision)
            {
                AddRevision(working, request.TransactionId, MemoryAlterationType.NaturalDegradation, request.ToWorldTime, "memory.degradation.policy", "World-time memory degradation evaluated.");
            }

            if (preview)
            {
                return HistoryOperationResult.Success("Memory degradation preview succeeded.", request.TransactionId, null, new HistoryMemoryRecord(working), null, priorRevision, MemoryRevision, preview: true);
            }

            memoriesById[memory.memoryId] = working;
            processedTransactions.Add(request.TransactionId ?? string.Empty);
            MemoryRevision++;
            HistoryOperationResult result = HistoryOperationResult.Success("Memory degradation applied.", request.TransactionId, null, new HistoryMemoryRecord(working), null, priorRevision, MemoryRevision);
            RaiseChanged(result, restoring);
            return result;
        }

        public HistoryOperationResult AlterMemory(MemoryAlterationRequest request, bool preview = false, bool restoring = false)
        {
            long priorRevision = MemoryRevision;
            if (!ValidateMemoryMutation(request?.OwnerPersonId, request?.MemoryId, request?.TransactionId, request?.WorldTime ?? 0d, out HistoryMemoryRecordData memory, out string failure, out HistoryResultCode code))
            {
                return HistoryOperationResult.Failure(code, failure, request?.TransactionId, preview, MemoryRevision);
            }

            if (!preview && processedTransactions.Contains(request.TransactionId ?? string.Empty))
            {
                return HistoryOperationResult.Success("Memory alteration transaction already processed.", request.TransactionId, null, new HistoryMemoryRecord(memory), null, priorRevision, MemoryRevision, duplicate: true);
            }

            HistoryMemoryRecordData working = memory.Clone();
            working.confidence = HistoryMath.ClampMetric(working.confidence + request.ConfidenceDelta);
            working.clarity = HistoryMath.ClampMetric(working.clarity + request.ClarityDelta);
            working.salience = HistoryMath.ClampMetric(working.salience + request.SalienceDelta);
            if (request.ResultingState.HasValue)
            {
                working.state = request.ResultingState.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.BodyAtTimeId))
            {
                working.bodyAtTimeId = request.BodyAtTimeId;
            }

            ApplyDetailAlterations(working, request);
            AddRevision(working, request.TransactionId, request.AlterationType == MemoryAlterationType.None ? MemoryAlterationType.Reconstruction : request.AlterationType, request.WorldTime, request.SourceId, request.Description);
            if (preview)
            {
                return HistoryOperationResult.Success("Memory alteration preview succeeded.", request.TransactionId, null, new HistoryMemoryRecord(working), null, priorRevision, MemoryRevision, preview: true);
            }

            memoriesById[memory.memoryId] = working;
            processedTransactions.Add(request.TransactionId ?? string.Empty);
            MemoryRevision++;
            HistoryOperationResult result = HistoryOperationResult.Success("Memory altered.", request.TransactionId, null, new HistoryMemoryRecord(working), null, priorRevision, MemoryRevision);
            RaiseChanged(result, restoring);
            return result;
        }

        public HistoryOperationResult AddSuppression(MemorySuppressionRequest request, bool preview = false, bool restoring = false)
        {
            long priorRevision = MemoryRevision;
            if (!ValidateMemoryMutation(request?.OwnerPersonId, request?.MemoryId, request?.TransactionId, request?.StartedAtWorldTime ?? 0d, out HistoryMemoryRecordData memory, out string failure, out HistoryResultCode code))
            {
                return HistoryOperationResult.Failure(code, failure, request?.TransactionId, preview, MemoryRevision);
            }

            if (string.IsNullOrWhiteSpace(request.SuppressionId) || string.IsNullOrWhiteSpace(request.SourceId))
            {
                return HistoryOperationResult.Failure(HistoryResultCode.InvalidSuppression, "Memory suppression requires suppression and source IDs.", request.TransactionId, preview, MemoryRevision);
            }

            if (request.EndedAtWorldTime >= 0d && request.EndedAtWorldTime <= request.StartedAtWorldTime)
            {
                return HistoryOperationResult.Failure(HistoryResultCode.InvalidTimeRange, "Memory suppression end time must be after start time.", request.TransactionId, preview, MemoryRevision);
            }

            if ((memory.suppressions ?? Array.Empty<MemorySuppressionData>()).Any(suppression => string.Equals(suppression.suppressionId, request.SuppressionId, StringComparison.Ordinal)))
            {
                return HistoryOperationResult.Failure(HistoryResultCode.InvalidSuppression, $"Memory suppression '{request.SuppressionId}' already exists.", request.TransactionId, preview, MemoryRevision);
            }

            HistoryMemoryRecordData working = memory.Clone();
            List<MemorySuppressionData> suppressions = (working.suppressions ?? Array.Empty<MemorySuppressionData>()).Select(suppression => suppression.Clone()).ToList();
            if (!HasActiveBlockingSuppression(working, request.StartedAtWorldTime, bypassWithCue: false))
            {
                working.stateBeforeSuppression = working.state == MemoryState.Suppressed ? MemoryState.Accessible : working.state;
            }

            suppressions.Add(new MemorySuppressionData
            {
                suppressionId = request.SuppressionId,
                memoryId = request.MemoryId,
                sourceId = request.SourceId,
                reasonId = request.ReasonId,
                startedAtWorldTime = request.StartedAtWorldTime,
                endedAtWorldTime = request.EndedAtWorldTime,
                allowsCueBypass = request.AllowsCueBypass,
                provenance = request.Provenance
            });
            working.suppressions = suppressions.OrderBy(suppression => suppression.suppressionId, StringComparer.Ordinal).ToArray();
            working.state = MemoryState.Suppressed;
            AddRevision(working, request.TransactionId, MemoryAlterationType.Suppression, request.StartedAtWorldTime, request.SourceId, "Memory suppression added.");
            if (preview)
            {
                return HistoryOperationResult.Success("Memory suppression preview succeeded.", request.TransactionId, null, new HistoryMemoryRecord(working), null, priorRevision, MemoryRevision, preview: true);
            }

            memoriesById[memory.memoryId] = working;
            processedTransactions.Add(request.TransactionId ?? string.Empty);
            MemoryRevision++;
            HistoryOperationResult result = HistoryOperationResult.Success("Memory suppression added.", request.TransactionId, null, new HistoryMemoryRecord(working), null, priorRevision, MemoryRevision);
            RaiseChanged(result, restoring);
            return result;
        }

        public HistoryOperationResult RemoveSuppression(string memoryId, string suppressionId, string transactionId, double worldTime, bool expireOnly = false, bool preview = false, bool restoring = false)
        {
            long priorRevision = MemoryRevision;
            if (!ValidateMemoryMutation(PersonId, memoryId, transactionId, worldTime, out HistoryMemoryRecordData memory, out string failure, out HistoryResultCode code))
            {
                return HistoryOperationResult.Failure(code, failure, transactionId, preview, MemoryRevision);
            }

            HistoryMemoryRecordData working = memory.Clone();
            MemorySuppressionData suppression = (working.suppressions ?? Array.Empty<MemorySuppressionData>()).FirstOrDefault(item => string.Equals(item.suppressionId, suppressionId, StringComparison.Ordinal));
            if (suppression == null)
            {
                return HistoryOperationResult.Failure(HistoryResultCode.InvalidSuppression, $"Memory suppression '{suppressionId}' is missing.", transactionId, preview, MemoryRevision);
            }

            if (expireOnly && suppression.endedAtWorldTime < 0d)
            {
                return HistoryOperationResult.Failure(HistoryResultCode.InvalidSuppression, $"Permanent suppression '{suppressionId}' cannot expire without explicit removal.", transactionId, preview, MemoryRevision);
            }

            suppression.removed = !expireOnly;
            suppression.removedAtWorldTime = worldTime;

            if (!HasActiveBlockingSuppression(working, worldTime, bypassWithCue: false))
            {
                working.state = working.stateBeforeSuppression == MemoryState.Suppressed ? MemoryState.Accessible : working.stateBeforeSuppression;
            }

            AddRevision(working, transactionId, MemoryAlterationType.SuppressionRemoval, worldTime, suppression.sourceId, "Memory suppression removed or expired.");
            if (preview)
            {
                return HistoryOperationResult.Success("Memory suppression removal preview succeeded.", transactionId, null, new HistoryMemoryRecord(working), null, priorRevision, MemoryRevision, preview: true);
            }

            memoriesById[memory.memoryId] = working;
            processedTransactions.Add(transactionId ?? string.Empty);
            MemoryRevision++;
            HistoryOperationResult result = HistoryOperationResult.Success("Memory suppression removed.", transactionId, null, new HistoryMemoryRecord(working), null, priorRevision, MemoryRevision);
            RaiseChanged(result, restoring);
            return result;
        }

        public HistoryOperationResult RecoverMemory(string memoryId, string transactionId, double worldTime, MemoryState resultingState = MemoryState.Accessible, string sourceId = "memory.recovery", bool preview = false, bool restoring = false)
        {
            return AlterMemory(new MemoryAlterationRequest
            {
                TransactionId = transactionId,
                OwnerPersonId = PersonId,
                MemoryId = memoryId,
                WorldTime = worldTime,
                AlterationType = MemoryAlterationType.Recovery,
                ResultingState = resultingState,
                SourceId = sourceId,
                Description = "Memory explicitly recovered."
            }, preview, restoring);
        }

        public bool TryGetMemory(string memoryId, out HistoryMemoryRecord memory)
        {
            if (memoriesById.TryGetValue(memoryId ?? string.Empty, out HistoryMemoryRecordData data))
            {
                memory = new HistoryMemoryRecord(data);
                return true;
            }

            memory = null;
            return false;
        }

        public IReadOnlyList<HistoryMemoryRecord> QueryByEvent(string eventId)
        {
            if (!memoryIdsByEvent.TryGetValue(eventId ?? string.Empty, out List<string> ids))
            {
                return Array.Empty<HistoryMemoryRecord>();
            }

            return ids.Where(id => memoriesById.ContainsKey(id)).Select(id => new HistoryMemoryRecord(memoriesById[id])).OrderBy(record => record.MemoryId, StringComparer.Ordinal).ToArray();
        }

        public InformationAccessProjection<HistoryMemoryRecord> GetMemoryProjection(string memoryId, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, string policyId = "", bool recordAudit = false)
        {
            if (!TryGetMemory(memoryId, out HistoryMemoryRecord memory))
            {
                return new InformationAccessProjection<HistoryMemoryRecord>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, $"Memory '{memoryId}' was not found.");
            }

            string[] detailIds = MemoryDetailIds(memory);
            InformationAccessContext context = InformationAccessProjectionUtility.BuildContext(
                accessContext,
                BuildMemorySubject(memory),
                InformationAccessMode.Inspect,
                InformationAccessPurpose.Journal,
                detailIds,
                policyId);
            RedactedInformationProjection projection = accessRuntime?.Project(context, detailIds);
            if (projection == null)
            {
                return new InformationAccessProjection<HistoryMemoryRecord>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Information access runtime is missing.");
            }

            if (recordAudit)
            {
                accessRuntime.RecordAudit(projection.Decision, context, gameplayAudit: false);
            }

            HistoryMemoryRecord projected = projection.Decision.Denied ? null : MaterializeMemory(memory, projection.Details);
            string visibleSubjectId = projected == null || !InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.memory") ? string.Empty : projected.MemoryId;
            return new InformationAccessProjection<HistoryMemoryRecord>(projected, projection.Decision, projection.Details, visibleSubjectId, projection.Decision.VisibleReason);
        }

        public InformationAccessProjection<MemoryRecallResult> GetRecallProjection(MemoryRecallRequest request, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, string policyId = "", bool recordAudit = false)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MemoryId))
            {
                return new InformationAccessProjection<MemoryRecallResult>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Recall projection requires a memory ID.");
            }

            InformationAccessProjection<HistoryMemoryRecord> memoryProjection = GetMemoryProjection(request.MemoryId, accessRuntime, accessContext, policyId, recordAudit);
            if (memoryProjection.Denied)
            {
                return new InformationAccessProjection<MemoryRecallResult>(null, memoryProjection.Decision, memoryProjection.DetailStates, memoryProjection.VisibleSubjectId, memoryProjection.Message);
            }

            MemoryRecallRequest previewRequest = new MemoryRecallRequest
            {
                TransactionId = request.TransactionId,
                RequestingPersonId = request.RequestingPersonId,
                MemoryId = request.MemoryId,
                HistoricalEventId = request.HistoricalEventId,
                SubjectId = request.SubjectId,
                BodyId = request.BodyId,
                LocationId = request.LocationId,
                OrganizationId = request.OrganizationId,
                Tags = request.Tags == null ? Array.Empty<string>() : request.Tags.ToArray(),
                WorldTime = request.WorldTime,
                AttemptDifficult = request.AttemptDifficult,
                AllowCueRecovery = request.AllowCueRecovery,
                ReinforceOnSuccess = false,
                MutateMetadata = false,
                Cues = request.Cues == null ? Array.Empty<MemoryRecallCue>() : request.Cues.ToArray(),
                AccessContext = request.AccessContext
            };
            MemoryRecallResult result = Recall(previewRequest, preview: true, restoring: true);
            return new InformationAccessProjection<MemoryRecallResult>(result, memoryProjection.Decision, memoryProjection.DetailStates, memoryProjection.VisibleSubjectId, memoryProjection.Message);
        }

        public PersonMemorySnapshot CreateSnapshot()
        {
            return new PersonMemorySnapshot(PersonId, MemoryRevision, memoriesById.Values.Select(data => new HistoryMemoryRecord(data)).ToArray());
        }

        public PersonMemorySaveData CreateSaveData()
        {
            return new PersonMemorySaveData
            {
                schemaVersion = PersonMemorySaveData.CurrentSchemaVersion,
                personId = PersonId,
                memoryRevision = MemoryRevision,
                memories = memoriesById.Values.OrderBy(data => data.memoryId, StringComparer.Ordinal).Select(data => data.Clone()).ToArray(),
                processedTransactions = processedTransactions.OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
        }

        public HistoryOperationResult RestoreFromSaveData(PersonMemorySaveData saveData, DefinitionRegistry definitionRegistry, AuthoritativeHistoryRuntime authoritativeHistory, IEnumerable<string> knownPersons, bool restoring = true)
        {
            if (!ValidateSaveData(saveData, authoritativeHistory, knownPersons, out string failureReason))
            {
                return HistoryOperationResult.Failure(HistoryResultCode.RestoreFailed, failureReason, revision: MemoryRevision);
            }

            PersonMemorySaveData rollback = CreateSaveData();
            try
            {
                suppressEvents = restoring;
                Configure(saveData.personId, definitionRegistry, authoritativeHistory, knownPersons);
                memoriesById.Clear();
                memoryIdsByEvent.Clear();
                processedTransactions.Clear();

                foreach (HistoryMemoryRecordData memory in saveData.memories ?? Array.Empty<HistoryMemoryRecordData>())
                {
                    HistoryMemoryRecordData clone = memory.Clone();
                    memoriesById[clone.memoryId] = clone;
                    AddIndex(clone.historicalEventId, clone.memoryId);
                }

                foreach (string transaction in saveData.processedTransactions ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(transaction))
                    {
                        processedTransactions.Add(transaction);
                    }
                }

                MemoryRevision = Math.Max(0L, saveData.memoryRevision);
                return HistoryOperationResult.Success("Person memories restored.", string.Empty, null, null, null, MemoryRevision, MemoryRevision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, registry, historyRuntime, knownPersonIds, restoring: true);
                return HistoryOperationResult.Failure(HistoryResultCode.RestoreFailed, exception.Message, revision: MemoryRevision);
            }
            finally
            {
                suppressEvents = false;
            }
        }

        public static bool ValidateSaveData(PersonMemorySaveData saveData, AuthoritativeHistoryRuntime authoritativeHistory, IEnumerable<string> knownPersons, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Person memory save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != PersonMemorySaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Person Memory schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> known = new HashSet<string>((knownPersons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(saveData.personId) || known.Count > 0 && !known.Contains(saveData.personId))
            {
                failureReason = $"Person memory save references unknown Person '{saveData.personId}'.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (HistoryMemoryRecordData memory in saveData.memories ?? Array.Empty<HistoryMemoryRecordData>())
            {
                if (!ValidateMemoryData(memory, saveData.personId, authoritativeHistory, out failureReason) || !ids.Add(memory.memoryId ?? string.Empty))
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason) ? $"Missing or duplicate memory ID '{memory?.memoryId}'." : failureReason;
                    return false;
                }
            }

            return true;
        }

        private bool ValidateRecallRequest(MemoryRecallRequest request, out string failure, out HistoryResultCode code)
        {
            failure = string.Empty;
            code = HistoryResultCode.InvalidRequest;
            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId))
            {
                failure = "Memory recall requires a transaction ID.";
                return false;
            }

            if (!string.Equals(request.RequestingPersonId, PersonId, StringComparison.Ordinal) && request.AccessContext == MemoryAccessContext.OrdinaryRecall)
            {
                code = HistoryResultCode.AccessDenied;
                failure = $"Person '{request.RequestingPersonId}' cannot recall memories owned by '{PersonId}'.";
                return false;
            }

            if (processedTransactions.Contains(request.TransactionId ?? string.Empty) && request.MutateMetadata)
            {
                code = HistoryResultCode.Duplicate;
                failure = "Memory recall transaction already processed.";
                return false;
            }

            if (request.MaxResults < 0)
            {
                failure = "Memory recall max results cannot be negative.";
                return false;
            }

            return true;
        }

        private bool ValidateMemoryMutation(string ownerPersonId, string memoryId, string transactionId, double worldTime, out HistoryMemoryRecordData memory, out string failure, out HistoryResultCode code)
        {
            memory = null;
            failure = string.Empty;
            code = HistoryResultCode.InvalidRequest;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                failure = "Memory mutation requires a transaction ID.";
                return false;
            }

            if (!string.Equals(ownerPersonId, PersonId, StringComparison.Ordinal))
            {
                code = HistoryResultCode.AccessDenied;
                failure = $"Memory mutation targets Person '{ownerPersonId}', but runtime owns '{PersonId}'.";
                return false;
            }

            if (!memoriesById.TryGetValue(memoryId ?? string.Empty, out memory))
            {
                code = HistoryResultCode.MissingMemory;
                failure = $"Memory '{memoryId}' is missing.";
                return false;
            }

            if (worldTime < memory.formedAtWorldTime)
            {
                code = HistoryResultCode.InvalidTimeRange;
                failure = "Memory mutation cannot occur before memory formation.";
                return false;
            }

            return true;
        }

        private IReadOnlyList<HistoryMemoryRecordData> QueryMemoryData(MemoryRecallRequest request)
        {
            IEnumerable<HistoryMemoryRecordData> query = memoriesById.Values;
            if (!string.IsNullOrWhiteSpace(request.MemoryId))
            {
                query = query.Where(memory => string.Equals(memory.memoryId, request.MemoryId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(request.HistoricalEventId))
            {
                query = query.Where(memory => string.Equals(memory.historicalEventId, request.HistoricalEventId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(request.SubjectId))
            {
                query = query.Where(memory => MemoryMatchesReference(memory, request.SubjectId));
            }

            if (!string.IsNullOrWhiteSpace(request.BodyId))
            {
                query = query.Where(memory => string.Equals(memory.bodyAtTimeId, request.BodyId, StringComparison.Ordinal) || DetailMatches(memory, MemoryDetailKind.Body, request.BodyId));
            }

            if (!string.IsNullOrWhiteSpace(request.LocationId))
            {
                query = query.Where(memory => DetailMatches(memory, MemoryDetailKind.Location, request.LocationId));
            }

            if (!string.IsNullOrWhiteSpace(request.OrganizationId))
            {
                query = query.Where(memory => DetailMatches(memory, MemoryDetailKind.Organization, request.OrganizationId));
            }

            string[] tags = request.Tags ?? Array.Empty<string>();
            if (tags.Length > 0)
            {
                query = query.Where(memory => tags.All(tag => (memory.tags ?? Array.Empty<string>()).Contains(tag, StringComparer.Ordinal)));
            }

            int limit = request.MaxResults <= 0 ? int.MaxValue : request.MaxResults;
            return query.OrderBy(memory => memory.rememberedOccurredAtWorldTime).ThenBy(memory => memory.memoryId, StringComparer.Ordinal).Take(limit).ToArray();
        }

        private MemoryRecallEntry EvaluateRecall(HistoryMemoryRecordData memory, MemoryRecallRequest request, bool preview, out bool succeeded, out bool mutated)
        {
            succeeded = false;
            mutated = false;
            bool cueMatched = CueMatches(memory, request.Cues);
            bool privileged = request.AccessContext != MemoryAccessContext.OrdinaryRecall;
            bool suppressed = HasActiveBlockingSuppression(memory, request.WorldTime, cueMatched);
            if (suppressed && !privileged)
            {
                return Entry(memory, MemoryRecallOutcome.BlockedBySuppression, false, false, cueMatched, "Recall blocked by active suppression.");
            }

            if (memory.state == MemoryState.Forgotten && !privileged && !(request.AllowCueRecovery && cueMatched))
            {
                return Entry(memory, MemoryRecallOutcome.Forgotten, false, false, cueMatched, "Memory is forgotten.");
            }

            if (memory.state == MemoryState.Inaccessible && !privileged && !(request.AllowCueRecovery && cueMatched))
            {
                return Entry(memory, MemoryRecallOutcome.Inaccessible, false, false, cueMatched, "Memory is inaccessible.");
            }

            if (memory.state == MemoryState.Difficult && !request.AttemptDifficult && !cueMatched && !privileged)
            {
                return Entry(memory, MemoryRecallOutcome.Inaccessible, false, false, cueMatched, "Memory is difficult to recall without an attempt or cue.");
            }

            if (memory.state == MemoryState.Dormant && !privileged && !cueMatched)
            {
                return Entry(memory, MemoryRecallOutcome.Inaccessible, false, false, cueMatched, "Memory is dormant.");
            }

            HistoryMemoryRecordData working = memory;
            MemoryRecallOutcome outcome = OutcomeFor(memory, cueMatched, request.AllowCueRecovery);
            if (!preview && request.MutateMetadata)
            {
                working.lastRecallAttemptWorldTime = Math.Max(working.lastRecallAttemptWorldTime, request.WorldTime);
                if (outcome != MemoryRecallOutcome.Inaccessible && outcome != MemoryRecallOutcome.Forgotten && outcome != MemoryRecallOutcome.BlockedBySuppression)
                {
                    working.lastRecalledWorldTime = Math.Max(working.lastRecalledWorldTime, request.WorldTime);
                    working.recallCount = Math.Max(0, working.recallCount) + 1;
                    if (outcome == MemoryRecallOutcome.Recovered || outcome == MemoryRecallOutcome.CueAssisted)
                    {
                        working.state = MemoryState.Recovered;
                        AddRevision(working, request.TransactionId, MemoryAlterationType.Recovery, request.WorldTime, "memory.recall.cue", "Memory recovered during cue-assisted recall.");
                    }

                    if (request.ReinforceOnSuccess)
                    {
                        ApplyReinforcement(working, new MemoryReinforcementRequest
                        {
                            TransactionId = request.TransactionId,
                            OwnerPersonId = PersonId,
                            MemoryId = memory.memoryId,
                            WorldTime = request.WorldTime,
                            Source = MemoryReinforcementSource.SuccessfulRecall,
                            ClarityDelta = 25,
                            SalienceDelta = 10,
                            SourceId = "memory.recall"
                        });
                    }
                }

                mutated = true;
            }

            succeeded = true;
            return Entry(working, outcome, true, mutated, cueMatched, cueMatched ? "Memory recalled with contextual cue." : "Memory recalled.");
        }

        private MemoryRecallEntry Entry(HistoryMemoryRecordData memory, MemoryRecallOutcome outcome, bool recalled, bool metadataUpdated, bool cueMatched, string reason)
        {
            MemoryDetailData[] details = memory.rememberedDetails ?? Array.Empty<MemoryDetailData>();
            IReadOnlyList<MemoryDetailData> remembered = recalled ? details.Where(detail => detail.state == MemoryDetailState.Remembered || detail.state == MemoryDetailState.Recovered || detail.state == MemoryDetailState.Altered || detail.state == MemoryDetailState.Uncertain).Select(detail => detail.Clone()).ToArray() : Array.Empty<MemoryDetailData>();
            IReadOnlyList<MemoryDetailData> unavailable = details.Where(detail => detail.state == MemoryDetailState.Unavailable || detail.state == MemoryDetailState.Suppressed).Select(detail => detail.Clone()).ToArray();
            return new MemoryRecallEntry(new HistoryMemoryRecord(memory), outcome, remembered, unavailable, metadataUpdated, cueMatched, reason);
        }

        private static MemoryRecallOutcome ResolveAggregateOutcome(IReadOnlyList<MemoryRecallEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return MemoryRecallOutcome.NoMatch;
            }

            if (entries.Count > 1 && entries.Any(entry => entry.Outcome == MemoryRecallOutcome.FullyRecalled || entry.Outcome == MemoryRecallOutcome.PartiallyRecalled || entry.Outcome == MemoryRecallOutcome.Altered))
            {
                return MemoryRecallOutcome.Conflicting;
            }

            return entries[0].Outcome;
        }

        private static HistoryResultCode FailureCodeFor(MemoryRecallOutcome outcome)
        {
            return outcome switch
            {
                MemoryRecallOutcome.AccessDenied => HistoryResultCode.AccessDenied,
                MemoryRecallOutcome.BlockedBySuppression => HistoryResultCode.Suppressed,
                MemoryRecallOutcome.Forgotten => HistoryResultCode.Forgotten,
                MemoryRecallOutcome.Inaccessible => HistoryResultCode.InvalidTransition,
                _ => HistoryResultCode.NoMatch
            };
        }

        private static MemoryRecallOutcome OutcomeFor(HistoryMemoryRecordData memory, bool cueMatched, bool allowCueRecovery)
        {
            if ((memory.state == MemoryState.Inaccessible || memory.state == MemoryState.Forgotten || memory.state == MemoryState.Dormant) && cueMatched && allowCueRecovery)
            {
                return MemoryRecallOutcome.Recovered;
            }

            if (cueMatched && memory.state == MemoryState.Difficult)
            {
                return MemoryRecallOutcome.CueAssisted;
            }

            MemoryDetailData[] details = memory.rememberedDetails ?? Array.Empty<MemoryDetailData>();
            if (details.Any(detail => detail.state == MemoryDetailState.Unavailable || detail.state == MemoryDetailState.Suppressed))
            {
                return MemoryRecallOutcome.PartiallyRecalled;
            }

            if (memory.state == MemoryState.Altered || memory.state == MemoryState.Corrected)
            {
                return MemoryRecallOutcome.Altered;
            }

            if (memory.clarity < 500 || memory.confidence < 500 || memory.state == MemoryState.Uncertain || memory.state == MemoryState.Difficult)
            {
                return MemoryRecallOutcome.Uncertain;
            }

            return MemoryRecallOutcome.FullyRecalled;
        }

        private static bool HasActiveBlockingSuppression(HistoryMemoryRecordData memory, double worldTime, bool bypassWithCue)
        {
            return (memory.suppressions ?? Array.Empty<MemorySuppressionData>()).Any(suppression => suppression.IsActiveAt(worldTime) && !(bypassWithCue && suppression.allowsCueBypass));
        }

        private static bool CueMatches(HistoryMemoryRecordData memory, IEnumerable<MemoryRecallCue> cues)
        {
            foreach (MemoryRecallCue cue in cues ?? Array.Empty<MemoryRecallCue>())
            {
                if (cue == null || string.IsNullOrWhiteSpace(cue.ReferenceId))
                {
                    continue;
                }

                if (cue.Kind == MemoryCueKind.HistoricalEvent && string.Equals(memory.historicalEventId, cue.ReferenceId, StringComparison.Ordinal)
                    || cue.Kind == MemoryCueKind.Body && string.Equals(memory.bodyAtTimeId, cue.ReferenceId, StringComparison.Ordinal)
                    || cue.Kind == MemoryCueKind.Memory && string.Equals(memory.memoryId, cue.ReferenceId, StringComparison.Ordinal)
                    || cue.Kind == MemoryCueKind.Tag && (memory.tags ?? Array.Empty<string>()).Contains(cue.ReferenceId, StringComparer.Ordinal)
                    || DetailMatches(memory, DetailKindForCue(cue.Kind), cue.ReferenceId))
                {
                    return true;
                }
            }

            return false;
        }

        private static MemoryDetailKind DetailKindForCue(MemoryCueKind kind)
        {
            return kind switch
            {
                MemoryCueKind.Person => MemoryDetailKind.Participant,
                MemoryCueKind.Location => MemoryDetailKind.Location,
                MemoryCueKind.Body => MemoryDetailKind.Body,
                MemoryCueKind.Item => MemoryDetailKind.Item,
                MemoryCueKind.Organization => MemoryDetailKind.Organization,
                _ => MemoryDetailKind.Unknown
            };
        }

        private static bool MemoryMatchesReference(HistoryMemoryRecordData memory, string referenceId)
        {
            return string.Equals(memory.historicalEventId, referenceId, StringComparison.Ordinal)
                || string.Equals(memory.bodyAtTimeId, referenceId, StringComparison.Ordinal)
                || DetailMatches(memory, MemoryDetailKind.Unknown, referenceId)
                || (memory.tags ?? Array.Empty<string>()).Contains(referenceId, StringComparer.Ordinal);
        }

        private static bool DetailMatches(HistoryMemoryRecordData memory, MemoryDetailKind kind, string value)
        {
            return (memory.rememberedDetails ?? Array.Empty<MemoryDetailData>()).Any(detail =>
                (kind == MemoryDetailKind.Unknown || detail.kind == kind)
                && string.Equals(detail.value, value, StringComparison.Ordinal)
                && detail.state != MemoryDetailState.Unavailable
                && detail.state != MemoryDetailState.Suppressed);
        }

        private static void ApplyReinforcement(HistoryMemoryRecordData memory, MemoryReinforcementRequest request)
        {
            memory.confidence = HistoryMath.ClampMetric(memory.confidence + Math.Max(0, request.ConfidenceDelta));
            memory.clarity = HistoryMath.ClampMetric(memory.clarity + Math.Max(0, request.ClarityDelta));
            memory.salience = HistoryMath.ClampMetric(memory.salience + Math.Max(0, request.SalienceDelta));
            memory.lastReinforcedWorldTime = Math.Max(memory.lastReinforcedWorldTime, request.WorldTime);
            memory.reinforcementCount = Math.Max(0, memory.reinforcementCount) + 1;
            if (request.ImproveAccessibility && (memory.state == MemoryState.Difficult || memory.state == MemoryState.Inaccessible || memory.state == MemoryState.Uncertain))
            {
                memory.state = memory.clarity >= 500 ? MemoryState.Accessible : MemoryState.Difficult;
            }
        }

        private static void ApplyDetailAlterations(HistoryMemoryRecordData memory, MemoryAlterationRequest request)
        {
            List<MemoryDetailData> details = (memory.rememberedDetails ?? Array.Empty<MemoryDetailData>()).Select(detail => detail.Clone()).ToList();
            foreach (string detailId in request.DetailIdsToForget ?? Array.Empty<string>())
            {
                MemoryDetailData detail = details.FirstOrDefault(item => string.Equals(item.detailId, detailId, StringComparison.Ordinal));
                if (detail != null)
                {
                    detail.state = request.AlterationType == MemoryAlterationType.Suppression ? MemoryDetailState.Suppressed : MemoryDetailState.Unavailable;
                    detail.alteredByRevisionId = request.TransactionId;
                }
            }

            foreach (MemoryDetailData incoming in request.DetailsToAddOrReplace ?? Array.Empty<MemoryDetailData>())
            {
                if (incoming == null || string.IsNullOrWhiteSpace(incoming.detailId))
                {
                    continue;
                }

                int index = details.FindIndex(item => string.Equals(item.detailId, incoming.detailId, StringComparison.Ordinal));
                MemoryDetailData clone = incoming.Clone();
                clone.alteredByRevisionId = request.TransactionId;
                if (index >= 0)
                {
                    details[index] = clone;
                }
                else
                {
                    details.Add(clone);
                }
            }

            memory.rememberedDetails = details.OrderBy(detail => detail.detailId, StringComparer.Ordinal).ToArray();
            if (request.AlterationType != MemoryAlterationType.Recovery && request.AlterationType != MemoryAlterationType.SuppressionRemoval && request.AlterationType != MemoryAlterationType.Reinforcement)
            {
                memory.state = request.ResultingState ?? MemoryState.Altered;
            }
        }

        private static MemoryDetailData[] BuildInitialDetails(FormMemoryRequest request, HistoricalEventRecord historicalEvent)
        {
            List<MemoryDetailData> details = new List<MemoryDetailData>
            {
                Detail("detail.event", MemoryDetailKind.Event, request.HistoricalEventId),
                Detail("detail.time", MemoryDetailKind.Time, request.RememberedOccurredAtWorldTime.ToString("R"))
            };

            if (!string.IsNullOrWhiteSpace(request.BodyAtTimeId))
            {
                details.Add(Detail("detail.body", MemoryDetailKind.Body, request.BodyAtTimeId));
            }

            if (historicalEvent != null)
            {
                if (!string.IsNullOrWhiteSpace(historicalEvent.PrimaryPersonId))
                {
                    details.Add(Detail("detail.primary-person", MemoryDetailKind.Participant, historicalEvent.PrimaryPersonId));
                }

                foreach (string person in historicalEvent.ParticipantPersonIds.Where(value => !string.IsNullOrWhiteSpace(value)).OrderBy(value => value, StringComparer.Ordinal))
                {
                    details.Add(Detail($"detail.participant.{person}", MemoryDetailKind.Participant, person));
                }

                if (!string.IsNullOrWhiteSpace(historicalEvent.LocationId))
                {
                    details.Add(Detail("detail.location", MemoryDetailKind.Location, historicalEvent.LocationId));
                }

                if (!string.IsNullOrWhiteSpace(historicalEvent.OrganizationId))
                {
                    details.Add(Detail("detail.organization", MemoryDetailKind.Organization, historicalEvent.OrganizationId));
                }
            }

            return details.GroupBy(detail => detail.detailId, StringComparer.Ordinal).Select(group => group.First()).OrderBy(detail => detail.detailId, StringComparer.Ordinal).ToArray();
        }

        private static MemoryDetailData Detail(string id, MemoryDetailKind kind, string value)
        {
            return new MemoryDetailData
            {
                detailId = id,
                kind = kind,
                state = MemoryDetailState.Remembered,
                value = value ?? string.Empty,
                confidence = 700
            };
        }

        private static void AddRevision(HistoryMemoryRecordData memory, string transactionId, MemoryAlterationType type, double worldTime, string sourceId, string description)
        {
            MemoryRevisionData previous = (memory.revisions ?? Array.Empty<MemoryRevisionData>()).LastOrDefault();
            string previousId = previous?.revisionId ?? string.Empty;
            string revisionId = $"{memory.memoryId}.revision.{(memory.revisions ?? Array.Empty<MemoryRevisionData>()).Length}";
            MemoryRevisionData revision = CreateRevision(memory, transactionId, type, worldTime, sourceId, description, previousId);
            revision.revisionId = revisionId;
            List<MemoryRevisionData> revisions = (memory.revisions ?? Array.Empty<MemoryRevisionData>()).Select(item => item.Clone()).ToList();
            revisions.Add(revision);
            memory.revisions = revisions.ToArray();
            memory.currentRevisionId = revisionId;
        }

        private static MemoryRevisionData CreateRevision(HistoryMemoryRecordData memory, string transactionId, MemoryAlterationType type, double worldTime, string sourceId, string description, string previousRevisionId)
        {
            return new MemoryRevisionData
            {
                revisionId = string.IsNullOrWhiteSpace(memory.currentRevisionId) ? $"{memory.memoryId}.revision.0" : memory.currentRevisionId,
                previousRevisionId = previousRevisionId,
                transactionId = transactionId,
                alterationType = type,
                worldTime = worldTime,
                sourceId = sourceId,
                description = description,
                state = memory.state,
                confidence = memory.confidence,
                clarity = memory.clarity,
                salience = memory.salience,
                bodyAtTimeId = memory.bodyAtTimeId,
                details = memory.rememberedDetails == null ? Array.Empty<MemoryDetailData>() : memory.rememberedDetails.Select(detail => detail.Clone()).ToArray()
            };
        }

        private bool ValidateFormMemory(FormMemoryRequest request, out string failure, out HistoryResultCode code)
        {
            failure = string.Empty;
            code = HistoryResultCode.InvalidRequest;
            if (request == null || string.IsNullOrWhiteSpace(request.TransactionId) || string.IsNullOrWhiteSpace(request.MemoryId))
            {
                failure = "Memory formation requires transaction and memory IDs.";
                return false;
            }

            if (!string.Equals(request.OwnerPersonId, PersonId, StringComparison.Ordinal))
            {
                code = HistoryResultCode.MissingPerson;
                failure = $"Memory request targets Person '{request.OwnerPersonId}', but runtime owns '{PersonId}'.";
                return false;
            }

            if (knownPersonIds.Count > 0 && !knownPersonIds.Contains(request.OwnerPersonId))
            {
                code = HistoryResultCode.MissingPerson;
                failure = $"Memory request references unknown Person '{request.OwnerPersonId}'.";
                return false;
            }

            if (memoriesById.ContainsKey(request.MemoryId))
            {
                failure = $"Memory '{request.MemoryId}' already exists.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.HistoricalEventId) && historyRuntime != null && !historyRuntime.TryGetEvent(request.HistoricalEventId, out _))
            {
                code = HistoryResultCode.MissingEvent;
                failure = $"Memory references missing historical event '{request.HistoricalEventId}'.";
                return false;
            }

            if (request.FormedAtWorldTime < request.RememberedOccurredAtWorldTime)
            {
                code = HistoryResultCode.InvalidTimeRange;
                failure = "Memory cannot form before the remembered event occurs.";
                return false;
            }

            return true;
        }

        private static bool ValidateMemoryData(HistoryMemoryRecordData memory, string expectedPersonId, AuthoritativeHistoryRuntime authoritativeHistory, out string failure)
        {
            failure = string.Empty;
            if (memory == null || string.IsNullOrWhiteSpace(memory.memoryId))
            {
                failure = "Memory data is missing an ID.";
                return false;
            }

            if (!string.Equals(memory.ownerPersonId, expectedPersonId, StringComparison.Ordinal))
            {
                failure = $"Memory '{memory.memoryId}' is owned by '{memory.ownerPersonId}' instead of '{expectedPersonId}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(memory.historicalEventId) && authoritativeHistory != null && !authoritativeHistory.TryGetEvent(memory.historicalEventId, out _))
            {
                failure = $"Memory '{memory.memoryId}' references missing historical event '{memory.historicalEventId}'.";
                return false;
            }

            if (memory.formedAtWorldTime < memory.rememberedOccurredAtWorldTime)
            {
                failure = $"Memory '{memory.memoryId}' has invalid time ordering.";
                return false;
            }

            if (memory.lastRecalledWorldTime > 0d && memory.lastRecalledWorldTime < memory.formedAtWorldTime || memory.lastRecallAttemptWorldTime > 0d && memory.lastRecallAttemptWorldTime < memory.formedAtWorldTime)
            {
                failure = $"Memory '{memory.memoryId}' has recall metadata earlier than formation.";
                return false;
            }

            if (memory.lastDegradationEvaluatedWorldTime > 0d && memory.lastDegradationEvaluatedWorldTime < memory.formedAtWorldTime)
            {
                failure = $"Memory '{memory.memoryId}' has degradation metadata earlier than formation.";
                return false;
            }

            HashSet<string> detailIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MemoryDetailData detail in memory.rememberedDetails ?? Array.Empty<MemoryDetailData>())
            {
                if (detail == null || string.IsNullOrWhiteSpace(detail.detailId) || !detailIds.Add(detail.detailId))
                {
                    failure = $"Memory '{memory.memoryId}' has missing or duplicate detail IDs.";
                    return false;
                }
            }

            HashSet<string> suppressionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MemorySuppressionData suppression in memory.suppressions ?? Array.Empty<MemorySuppressionData>())
            {
                if (suppression == null || string.IsNullOrWhiteSpace(suppression.suppressionId) || string.IsNullOrWhiteSpace(suppression.sourceId) || !suppressionIds.Add(suppression.suppressionId))
                {
                    failure = $"Memory '{memory.memoryId}' has missing or duplicate suppression IDs.";
                    return false;
                }

                if (!string.Equals(suppression.memoryId, memory.memoryId, StringComparison.Ordinal))
                {
                    failure = $"Suppression '{suppression.suppressionId}' targets '{suppression.memoryId}' instead of memory '{memory.memoryId}'.";
                    return false;
                }

                if (suppression.endedAtWorldTime >= 0d && suppression.endedAtWorldTime <= suppression.startedAtWorldTime)
                {
                    failure = $"Suppression '{suppression.suppressionId}' has an invalid time range.";
                    return false;
                }
            }

            if (!ValidateRevisionChain(memory, out failure))
            {
                return false;
            }

            return true;
        }

        private static bool ValidateRevisionChain(HistoryMemoryRecordData memory, out string failure)
        {
            failure = string.Empty;
            MemoryRevisionData[] revisions = memory.revisions ?? Array.Empty<MemoryRevisionData>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, MemoryRevisionData> byId = new Dictionary<string, MemoryRevisionData>(StringComparer.Ordinal);
            foreach (MemoryRevisionData revision in revisions)
            {
                if (revision == null || string.IsNullOrWhiteSpace(revision.revisionId) || !ids.Add(revision.revisionId))
                {
                    failure = $"Memory '{memory.memoryId}' has missing or duplicate revision IDs.";
                    return false;
                }

                if (string.Equals(revision.revisionId, revision.previousRevisionId, StringComparison.Ordinal))
                {
                    failure = $"Memory revision '{revision.revisionId}' cannot reference itself.";
                    return false;
                }

                byId[revision.revisionId] = revision;
            }

            foreach (MemoryRevisionData revision in revisions)
            {
                if (!string.IsNullOrWhiteSpace(revision.previousRevisionId) && !byId.ContainsKey(revision.previousRevisionId))
                {
                    failure = $"Memory revision '{revision.revisionId}' references missing previous revision '{revision.previousRevisionId}'.";
                    return false;
                }
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            string current = memory.currentRevisionId;
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (!seen.Add(current))
                {
                    failure = $"Memory '{memory.memoryId}' has a circular revision chain.";
                    return false;
                }

                if (!byId.TryGetValue(current, out MemoryRevisionData revision))
                {
                    failure = $"Memory '{memory.memoryId}' current revision '{current}' is missing.";
                    return false;
                }

                current = revision.previousRevisionId;
            }

            return true;
        }

        private KnowledgeOperationResult CreateKnowledgeForMemory(FormMemoryRequest request, PersonKnowledgeRuntime knowledge, HistoricalEventRecord historicalEvent, bool preview)
        {
            KnowledgeObservationRequest observation = new KnowledgeObservationRequest
            {
                PersonId = request.OwnerPersonId,
                TransactionId = $"{request.TransactionId}.knowledge",
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.EventOccurred,
                    subjectType = KnowledgeSubjectType.Event,
                    subjectId = request.HistoricalEventId,
                    valueType = KnowledgeValueType.Boolean,
                    booleanValue = true,
                    bodyContextId = request.BodyAtTimeId,
                    sourceContextId = request.MemoryId,
                    sourceRevision = historicalEvent.Sequence
                },
                AcquisitionSource = AcquisitionSourceFor(request.Source),
                Provenance = ProvenanceFor(request.Source),
                Direction = KnowledgeEvidenceDirection.Supports,
                Strength = request.Confidence,
                Credibility = request.Clarity,
                GameTimeSeconds = request.FormedAtWorldTime,
                SourceId = request.HistoricalEventId,
                Visibility = request.Visibility,
                RelatedEventId = request.HistoricalEventId,
                EvidenceId = $"evidence.memory.{request.MemoryId}",
                PrivateAccessAuthorized = request.Visibility >= KnowledgeVisibility.Private,
                Tags = (request.Tags ?? Array.Empty<string>()).Concat(new[] { "history-memory", request.Source.ToString() }).ToArray()
            };
            return preview ? knowledge.PreviewObservation(observation) : knowledge.RecordObservation(observation);
        }

        private static KnowledgeAcquisitionSource AcquisitionSourceFor(HistoryMemorySource source)
        {
            return source switch
            {
                HistoryMemorySource.WitnessTestimony => KnowledgeAcquisitionSource.Testimony,
                HistoryMemorySource.WrittenRecord => KnowledgeAcquisitionSource.WrittenSource,
                HistoryMemorySource.Investigation => KnowledgeAcquisitionSource.SkillOrEducation,
                HistoryMemorySource.Examination => KnowledgeAcquisitionSource.Examination,
                HistoryMemorySource.Diagnosis => KnowledgeAcquisitionSource.Examination,
                HistoryMemorySource.Inference => KnowledgeAcquisitionSource.SkillOrEducation,
                HistoryMemorySource.KnowledgeSharing => KnowledgeAcquisitionSource.Testimony,
                HistoryMemorySource.PreviousBody => KnowledgeAcquisitionSource.PersonalExperience,
                HistoryMemorySource.ScriptedSetup => KnowledgeAcquisitionSource.ScriptedRevelation,
                HistoryMemorySource.DevelopmentFixture => KnowledgeAcquisitionSource.DevelopmentFixture,
                _ => KnowledgeAcquisitionSource.DirectObservation
            };
        }

        private static KnowledgeProvenance ProvenanceFor(HistoryMemorySource source)
        {
            return source switch
            {
                HistoryMemorySource.WitnessTestimony => KnowledgeProvenance.Testimony,
                HistoryMemorySource.WrittenRecord => KnowledgeProvenance.Document,
                HistoryMemorySource.Investigation => KnowledgeProvenance.Inference,
                HistoryMemorySource.Examination => KnowledgeProvenance.Examination,
                HistoryMemorySource.Diagnosis => KnowledgeProvenance.Examination,
                HistoryMemorySource.Inference => KnowledgeProvenance.Inference,
                HistoryMemorySource.KnowledgeSharing => KnowledgeProvenance.Testimony,
                HistoryMemorySource.PreviousBody => KnowledgeProvenance.Memory,
                HistoryMemorySource.ScriptedSetup => KnowledgeProvenance.ScriptedDiscovery,
                HistoryMemorySource.DevelopmentFixture => KnowledgeProvenance.DevelopmentFixture,
                _ => KnowledgeProvenance.DirectObservation
            };
        }

        private void AddIndex(string eventId, string memoryId)
        {
            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(memoryId))
            {
                return;
            }

            if (!memoryIdsByEvent.TryGetValue(eventId, out List<string> ids))
            {
                ids = new List<string>();
                memoryIdsByEvent[eventId] = ids;
            }

            if (!ids.Contains(memoryId, StringComparer.Ordinal))
            {
                ids.Add(memoryId);
            }
        }

        private HistoryMemoryRecord TryWrap(string memoryId)
        {
            return memoriesById.TryGetValue(memoryId ?? string.Empty, out HistoryMemoryRecordData data) ? new HistoryMemoryRecord(data) : null;
        }

        private static InformationSubjectReferenceData BuildMemorySubject(HistoryMemoryRecord memory)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Memory,
                subjectId = memory?.MemoryId ?? string.Empty,
                parentSubjectId = memory?.HistoricalEventId ?? string.Empty,
                ownerPersonId = memory?.OwnerPersonId ?? string.Empty,
                tags = memory?.Data.tags == null ? Array.Empty<string>() : memory.Data.tags.ToArray()
            };
        }

        private static string[] MemoryDetailIds(HistoryMemoryRecord memory)
        {
            List<string> details = new List<string>
            {
                "detail.summary",
                "detail.memory",
                "detail.event",
                "detail.belief",
                "detail.evidence",
                "detail.source",
                "detail.time",
                "detail.body",
                "detail.details",
                "detail.suppressions",
                "detail.revisions"
            };
            details.AddRange(memory?.RememberedDetails.Select(detail => detail.detailId) ?? Array.Empty<string>());
            return details.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static HistoryMemoryRecord MaterializeMemory(HistoryMemoryRecord memory, IReadOnlyDictionary<string, InformationRedactionState> details)
        {
            if (memory == null)
            {
                return null;
            }

            if (details.Values.All(state => state == InformationRedactionState.Visible))
            {
                return memory;
            }

            HistoryMemoryRecordData data = memory.Data.Clone();
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.memory")) data.memoryId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.event")) data.historicalEventId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.belief")) data.beliefId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.evidence")) data.evidenceIds = Array.Empty<string>();
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.source")) data.source = HistoryMemorySource.Unknown;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.time")) { data.formedAtWorldTime = 0d; data.rememberedOccurredAtWorldTime = 0d; data.lastRecalledWorldTime = -1d; data.lastRecallAttemptWorldTime = -1d; }
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.body")) data.bodyAtTimeId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.suppressions")) data.suppressions = Array.Empty<MemorySuppressionData>();
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.revisions")) { data.revisions = Array.Empty<MemoryRevisionData>(); data.currentRevisionId = string.Empty; }
            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.details"))
            {
                data.rememberedDetails = (data.rememberedDetails ?? Array.Empty<MemoryDetailData>())
                    .Where(detail => detail != null && InformationAccessProjectionUtility.IsVisible(details, detail.detailId))
                    .Select(detail => detail.Clone())
                    .ToArray();
            }

            return new HistoryMemoryRecord(data);
        }

        private void RaiseChanged(HistoryOperationResult result, bool restoring)
        {
            if (!restoring && !suppressEvents)
            {
                MemoryChanged?.Invoke(this, result);
            }
        }
    }
}
