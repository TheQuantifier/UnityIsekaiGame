using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Reputation
{
    public sealed class ReputationRuntime
    {
        private readonly Dictionary<string, ReputationRecordData> recordsById = new Dictionary<string, ReputationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> recordIdBySubjectAudience = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> processedTransactionIds = new HashSet<string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private bool dirty;
        private bool restoring;
        private bool disposed;

        public event Action<ReputationMutationResult> ReputationChanged;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public bool IsReady => registry != null && !disposed;
        public int Count => recordsById.Count;
        public IReadOnlyList<ReputationSnapshot> Snapshots => Ordered(recordsById.Values).Select(record => new ReputationSnapshot(record)).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, IEnumerable<string> persons)
        {
            registry = definitionRegistry;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
            disposed = false;
            RebuildIndexes();
        }

        public ReputationMutationResult Mutate(ReputationMutationRequest request)
        {
            request ??= new ReputationMutationRequest();
            long before = revision;
            if (!IsReady || restoring)
            {
                return ReputationMutationResult.Failure(ReputationOperationStatus.RuntimeNotReady, "Reputation runtime is not ready for mutation.", before);
            }

            if (string.IsNullOrWhiteSpace(request.transactionId))
            {
                return ReputationMutationResult.Failure(ReputationOperationStatus.MissingTransactionId, "Reputation mutation requires a transaction ID.", before);
            }

            string transactionId = request.transactionId.Trim();
            if (processedTransactionIds.Contains(transactionId))
            {
                ReputationSnapshot duplicateSnapshot = TryGetSnapshotBySubjectAudience(request.subjectPersonId, request.audienceId, out ReputationSnapshot existing) ? existing : null;
                return ReputationMutationResult.Success(ReputationOperationStatus.Duplicate, "Reputation mutation transaction was already processed.", duplicateSnapshot, request.subjectPersonId, request.audienceId, duplicateSnapshot?.RecordId ?? string.Empty, request.dimensionId, 0, 0, 0, 0, 0, false, false, false, true, false, before, before);
            }

            ReputationRuntimeSaveData rollback = CreateSaveData();
            if (!TryApply(request, out ReputationMutationResult result))
            {
                RestoreInternal(rollback);
                return result;
            }

            if (request.preview)
            {
                RestoreInternal(rollback);
                return result;
            }

            processedTransactionIds.Add(transactionId);
            revision++;
            dirty = true;
            ReputationRecordData committed = recordsById.TryGetValue(result.RecordId, out ReputationRecordData record) ? record : null;
            if (committed != null)
            {
                committed.revision++;
            }

            ReputationMutationResult committedResult = ReputationMutationResult.Success(
                ReputationOperationStatus.Succeeded,
                result.Message,
                committed == null ? result.Snapshot : new ReputationSnapshot(committed),
                result.SubjectPersonId,
                result.AudienceId,
                result.RecordId,
                result.DimensionId,
                result.PreviousBaselineValue,
                result.NewBaselineValue,
                result.PreviousEffectiveValue,
                result.NewEffectiveValue,
                result.AppliedDelta,
                result.Clamped,
                result.SourceContributionAffected,
                result.RecordCreated,
                duplicate: false,
                preview: false,
                beforeRevision: before,
                afterRevision: revision);
            ReputationChanged?.Invoke(committedResult);
            return committedResult;
        }

        public bool TryGetSnapshot(string recordId, out ReputationSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(recordId) && recordsById.TryGetValue(recordId.Trim(), out ReputationRecordData record))
            {
                snapshot = new ReputationSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool TryGetSnapshotBySubjectAudience(string subjectPersonId, string audienceId, out ReputationSnapshot snapshot)
        {
            if (recordIdBySubjectAudience.TryGetValue(SubjectAudienceKey(subjectPersonId, audienceId), out string recordId))
            {
                return TryGetSnapshot(recordId, out snapshot);
            }

            snapshot = null;
            return false;
        }

        public ReputationEffectiveValueSnapshot ResolveValue(string subjectPersonId, string audienceId, string dimensionId, bool allowInherited = false)
        {
            if (!TryGetDimensionDefinition(dimensionId, out ReputationDimensionDefinition definition))
            {
                return new ReputationEffectiveValueSnapshot(dimensionId, 0, false, Array.Empty<ReputationContributionSnapshot>(), 0, 0, false, false, false, string.Empty);
            }

            bool inherited = false;
            string resolvedAudienceId = audienceId;
            ReputationRecordData record = null;
            if (!TryGetRecordBySubjectAudience(subjectPersonId, audienceId, out record) && allowInherited)
            {
                foreach (string parentId in ResolveAudienceLineage(audienceId).Skip(1))
                {
                    if (TryGetRecordBySubjectAudience(subjectPersonId, parentId, out record))
                    {
                        inherited = true;
                        resolvedAudienceId = parentId;
                        break;
                    }
                }
            }

            ReputationDimensionValueData value = record != null ? FindDimension(record, definition.Id) : null;
            int baseline = value != null && value.hasBaseline ? value.baselineValue : definition.NeutralValue;
            IReadOnlyList<ReputationContributionSnapshot> contributions = OrderedContributions(value?.contributions).Select(item => new ReputationContributionSnapshot(item)).ToArray();
            int raw = baseline + contributions.Sum(item => item.Amount);
            int effective = definition.Clamp(raw, out bool clamped);
            return new ReputationEffectiveValueSnapshot(definition.Id, baseline, value?.hasBaseline == true, contributions, raw, effective, clamped, record != null, inherited, resolvedAudienceId);
        }

        public IReadOnlyList<ReputationSnapshot> QueryBySubject(string subjectPersonId, bool activeOnly = true)
        {
            return Query(record => string.Equals(record.subjectPersonId, subjectPersonId, StringComparison.Ordinal) && (!activeOnly || record.lifecycleState == ReputationLifecycleState.Active));
        }

        public IReadOnlyList<ReputationSnapshot> QueryByAudience(string audienceId, bool activeOnly = true)
        {
            return Query(record => string.Equals(record.audienceId, audienceId, StringComparison.Ordinal) && (!activeOnly || record.lifecycleState == ReputationLifecycleState.Active));
        }

        public IReadOnlyList<ReputationSnapshot> QueryByHistoricalEvent(string historicalEventId)
        {
            return Query(record => !string.IsNullOrWhiteSpace(historicalEventId)
                && (record.dimensions ?? new List<ReputationDimensionValueData>()).Any(dimension => (dimension.contributions ?? new List<ReputationContributionData>()).Any(contribution => string.Equals(contribution.historicalEventId, historicalEventId, StringComparison.Ordinal))));
        }

        public IReadOnlyList<ReputationSnapshot> QueryRanked(string audienceId, string dimensionId, int limit = 10, bool descending = true)
        {
            IEnumerable<ReputationRecordData> records = recordsById.Values
                .Where(record => record.lifecycleState == ReputationLifecycleState.Active && string.Equals(record.audienceId, audienceId, StringComparison.Ordinal));
            IOrderedEnumerable<ReputationRecordData> ordered = descending
                ? records.OrderByDescending(record => ResolveValue(record.subjectPersonId, record.audienceId, dimensionId).EffectiveValue).ThenBy(record => record.subjectPersonId, StringComparer.Ordinal)
                : records.OrderBy(record => ResolveValue(record.subjectPersonId, record.audienceId, dimensionId).EffectiveValue).ThenBy(record => record.subjectPersonId, StringComparer.Ordinal);
            return ordered.Take(Math.Max(0, limit)).Select(record => new ReputationSnapshot(record)).ToArray();
        }

        public ReputationThresholdResult EvaluateThreshold(ReputationThresholdRequest request)
        {
            request ??= new ReputationThresholdRequest();
            if (!ValidatePerson(request.subjectPersonId, out ReputationOperationStatus status, out string failure))
            {
                return new ReputationThresholdResult(false, status, failure, request.subjectPersonId, request.audienceId, request.dimensionId, 0, request.value, request.minimum, request.maximum, request.comparison, false, false, 0);
            }

            if (!TryGetAudienceDefinition(request.audienceId, out _))
            {
                return new ReputationThresholdResult(false, ReputationOperationStatus.MissingAudienceDefinition, $"Reputation Audience '{request.audienceId}' is missing.", request.subjectPersonId, request.audienceId, request.dimensionId, 0, request.value, request.minimum, request.maximum, request.comparison, false, false, 0);
            }

            if (!TryGetDimensionDefinition(request.dimensionId, out ReputationDimensionDefinition definition))
            {
                return new ReputationThresholdResult(false, ReputationOperationStatus.MissingDimensionDefinition, $"Reputation Dimension '{request.dimensionId}' is missing.", request.subjectPersonId, request.audienceId, request.dimensionId, 0, request.value, request.minimum, request.maximum, request.comparison, false, false, 0);
            }

            ReputationEffectiveValueSnapshot value = ResolveValue(request.subjectPersonId, request.audienceId, definition.Id, request.allowInherited);
            ReputationEffectiveValueSnapshot renown = ResolveValue(request.subjectPersonId, request.audienceId, PrototypeReputationDefinitionFactory.RenownId, request.allowInherited);
            bool passed = request.comparison switch
            {
                ReputationThresholdComparison.Equal => value.EffectiveValue == request.value,
                ReputationThresholdComparison.NotEqual => value.EffectiveValue != request.value,
                ReputationThresholdComparison.LessThan => value.EffectiveValue < request.value,
                ReputationThresholdComparison.LessThanOrEqual => value.EffectiveValue <= request.value,
                ReputationThresholdComparison.GreaterThan => value.EffectiveValue > request.value,
                ReputationThresholdComparison.GreaterThanOrEqual => value.EffectiveValue >= request.value,
                ReputationThresholdComparison.WithinInclusiveRange => value.EffectiveValue >= request.minimum && value.EffectiveValue <= request.maximum,
                ReputationThresholdComparison.OutsideInclusiveRange => value.EffectiveValue < request.minimum || value.EffectiveValue > request.maximum,
                _ => false
            };
            if (request.minimumRenown > 0 && renown.EffectiveValue < request.minimumRenown)
            {
                passed = false;
            }

            return new ReputationThresholdResult(passed, ReputationOperationStatus.Succeeded, passed ? "Reputation threshold passed." : "Reputation threshold failed.", request.subjectPersonId, request.audienceId, definition.Id, value.EffectiveValue, request.value, request.minimum, request.maximum, request.comparison, value.RecordExists, value.Inherited, renown.EffectiveValue);
        }

        public ReputationRuntimeSaveData CreateSaveData()
        {
            return new ReputationRuntimeSaveData
            {
                schemaVersion = ReputationRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                records = Ordered(recordsById.Values).Select(record => record.Clone()).ToList(),
                processedTransactionIds = processedTransactionIds.OrderBy(value => value, StringComparer.Ordinal).ToList()
            };
        }

        public ReputationMutationResult RestoreFromSaveData(ReputationRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, bool restoringState = true)
        {
            long before = revision;
            if (!ValidateSaveData(saveData, definitionRegistry, persons, out string failureReason))
            {
                return ReputationMutationResult.Failure(ReputationOperationStatus.RestoreFailed, failureReason, before);
            }

            Configure(definitionRegistry, persons);
            restoring = true;
            RestoreInternal(saveData ?? new ReputationRuntimeSaveData());
            restoring = false;
            dirty = !restoringState;
            return ReputationMutationResult.Success(ReputationOperationStatus.Succeeded, "Reputation restored.", null, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, 0, 0, 0, false, false, false, false, false, before, revision);
        }

        public static bool ValidateSaveData(ReputationRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, out string failureReason)
        {
            failureReason = string.Empty;
            ReputationRuntimeSaveData effective = saveData ?? new ReputationRuntimeSaveData();
            if (effective.schemaVersion != ReputationRuntimeSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Reputation save schema version {effective.schemaVersion}.";
                return false;
            }

            if (definitionRegistry == null)
            {
                failureReason = "Reputation runtime requires a definition registry.";
                return false;
            }

            if (!ValidateAudienceGraph(definitionRegistry, out failureReason))
            {
                return false;
            }

            HashSet<string> known = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> activeSubjectAudiences = new HashSet<string>(StringComparer.Ordinal);
            foreach (ReputationRecordData raw in effective.records ?? new List<ReputationRecordData>())
            {
                ReputationRecordData record = raw?.Clone();
                if (record == null)
                {
                    failureReason = "Reputation save contains a null record.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(record.recordId) || !ids.Add(record.recordId))
                {
                    failureReason = $"Reputation save contains duplicate or empty record ID '{record.recordId}'.";
                    return false;
                }

                if (!ValidateStaticPerson(record.subjectPersonId, known, out failureReason))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(record.audienceId) || !definitionRegistry.TryGet(record.audienceId, out ReputationAudienceDefinition _))
                {
                    failureReason = $"Reputation record '{record.recordId}' references missing Reputation Audience '{record.audienceId}'.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(ReputationLifecycleState), record.lifecycleState))
                {
                    failureReason = $"Reputation record '{record.recordId}' has invalid lifecycle state '{record.lifecycleState}'.";
                    return false;
                }

                if (record.lifecycleState == ReputationLifecycleState.Active && !activeSubjectAudiences.Add(SubjectAudienceKey(record.subjectPersonId, record.audienceId)))
                {
                    failureReason = $"Reputation save contains duplicate active subject-audience record for '{record.subjectPersonId}' and '{record.audienceId}'.";
                    return false;
                }

                HashSet<string> dimensions = new HashSet<string>(StringComparer.Ordinal);
                foreach (ReputationDimensionValueData dimension in record.dimensions ?? new List<ReputationDimensionValueData>())
                {
                    if (dimension == null || string.IsNullOrWhiteSpace(dimension.dimensionId) || !definitionRegistry.TryGet(dimension.dimensionId, out ReputationDimensionDefinition definition))
                    {
                        failureReason = $"Reputation record '{record.recordId}' references missing Reputation Dimension '{dimension?.dimensionId}'.";
                        return false;
                    }

                    if (!dimensions.Add(definition.Id))
                    {
                        failureReason = $"Reputation record '{record.recordId}' contains duplicate dimension '{definition.Id}'.";
                        return false;
                    }

                    if (dimension.hasBaseline && (dimension.baselineValue < definition.MinimumValue || dimension.baselineValue > definition.MaximumValue))
                    {
                        failureReason = $"Reputation record '{record.recordId}' dimension '{definition.Id}' has baseline {dimension.baselineValue} outside the authored range {definition.MinimumValue}..{definition.MaximumValue}.";
                        return false;
                    }

                    HashSet<string> sources = new HashSet<string>(StringComparer.Ordinal);
                    foreach (ReputationContributionData contribution in dimension.contributions ?? new List<ReputationContributionData>())
                    {
                        if (contribution == null || string.IsNullOrWhiteSpace(contribution.sourceId))
                        {
                            failureReason = $"Reputation record '{record.recordId}' contains a contribution with no source ID.";
                            return false;
                        }

                        if (!string.Equals(contribution.dimensionId, definition.Id, StringComparison.Ordinal))
                        {
                            failureReason = $"Reputation contribution '{contribution.sourceId}' is stored under dimension '{definition.Id}' but references '{contribution.dimensionId}'.";
                            return false;
                        }

                        if (!sources.Add(contribution.sourceId))
                        {
                            failureReason = $"Reputation record '{record.recordId}' contains duplicate contribution source '{contribution.sourceId}' for dimension '{definition.Id}'.";
                            return false;
                        }

                        if (!Enum.IsDefined(typeof(ReputationContributionSourceCategory), contribution.sourceCategory)
                            || !Enum.IsDefined(typeof(ReputationAuthenticity), contribution.authenticity))
                        {
                            failureReason = $"Reputation contribution '{contribution.sourceId}' has invalid source metadata.";
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public static bool ValidateAudienceGraph(DefinitionRegistry definitionRegistry, out string failureReason)
        {
            failureReason = string.Empty;
            if (definitionRegistry == null)
            {
                failureReason = "Reputation audience graph requires a definition registry.";
                return false;
            }

            ReputationAudienceDefinition[] audiences = definitionRegistry.DefinitionsById.Values.OfType<ReputationAudienceDefinition>().OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
            foreach (ReputationAudienceDefinition audience in audiences)
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal) { audience.Id };
                string parentId = audience.ParentAudienceId;
                while (!string.IsNullOrWhiteSpace(parentId))
                {
                    if (!definitionRegistry.TryGet(parentId, out ReputationAudienceDefinition parent))
                    {
                        failureReason = $"Reputation audience '{audience.Id}' references missing parent '{parentId}'.";
                        return false;
                    }

                    if (!seen.Add(parent.Id))
                    {
                        failureReason = $"Reputation audience hierarchy contains a cycle at '{parent.Id}'.";
                        return false;
                    }

                    parentId = parent.ParentAudienceId;
                }
            }

            return true;
        }

        public void Clear()
        {
            recordsById.Clear();
            recordIdBySubjectAudience.Clear();
            processedTransactionIds.Clear();
            revision++;
            dirty = true;
        }

        public void Dispose()
        {
            disposed = true;
            recordsById.Clear();
            recordIdBySubjectAudience.Clear();
            processedTransactionIds.Clear();
        }

        private bool TryApply(ReputationMutationRequest request, out ReputationMutationResult result)
        {
            long before = revision;
            result = null;
            if (!ValidatePerson(request.subjectPersonId, out ReputationOperationStatus personStatus, out string personFailure))
            {
                result = ReputationMutationResult.Failure(personStatus, personFailure, before);
                return false;
            }

            string subject = request.subjectPersonId.Trim();
            if (!TryGetAudienceDefinition(request.audienceId, out ReputationAudienceDefinition audience))
            {
                result = ReputationMutationResult.Failure(ReputationOperationStatus.MissingAudienceDefinition, $"Reputation Audience '{request.audienceId}' is missing.", before);
                return false;
            }

            if (!TryGetDimensionDefinition(request.dimensionId, out ReputationDimensionDefinition definition)
                && request.mutationKind != ReputationMutationKind.EnsureRecord
                && request.mutationKind != ReputationMutationKind.ArchiveRecord)
            {
                result = ReputationMutationResult.Failure(ReputationOperationStatus.MissingDimensionDefinition, $"Reputation Dimension '{request.dimensionId}' is missing.", before);
                return false;
            }

            string requestedRecordId = string.IsNullOrWhiteSpace(request.recordId) ? DefaultRecordId(subject, audience.Id) : request.recordId.Trim();
            bool created = false;
            if (!TryGetRecordBySubjectAudience(subject, audience.Id, out ReputationRecordData record))
            {
                if (recordsById.ContainsKey(requestedRecordId))
                {
                    result = ReputationMutationResult.Failure(ReputationOperationStatus.DuplicateRecordId, $"Reputation record ID '{requestedRecordId}' already exists.", before);
                    return false;
                }

                record = new ReputationRecordData
                {
                    recordId = requestedRecordId,
                    subjectPersonId = subject,
                    audienceId = audience.Id,
                    lifecycleState = ReputationLifecycleState.Active,
                    createdWorldTime = request.worldTime,
                    lastModifiedWorldTime = request.worldTime,
                    revision = 1L
                };
                recordsById[record.recordId] = record;
                recordIdBySubjectAudience[SubjectAudienceKey(subject, audience.Id)] = record.recordId;
                created = true;
            }
            else if (!string.IsNullOrWhiteSpace(request.recordId) && !string.Equals(record.recordId, requestedRecordId, StringComparison.Ordinal))
            {
                result = ReputationMutationResult.Failure(ReputationOperationStatus.DuplicateSubjectAudience, $"Subject '{subject}' audience '{audience.Id}' already has record '{record.recordId}'.", before);
                return false;
            }

            if (request.mutationKind == ReputationMutationKind.EnsureRecord)
            {
                result = ReputationMutationResult.Success(request.preview ? ReputationOperationStatus.Preview : ReputationOperationStatus.Succeeded, created ? "Reputation record created." : "Reputation record already exists.", new ReputationSnapshot(record), subject, audience.Id, record.recordId, string.Empty, 0, 0, 0, 0, 0, false, false, created, duplicate: !created, preview: request.preview, beforeRevision: before, afterRevision: before);
                return true;
            }

            if (request.mutationKind == ReputationMutationKind.ArchiveRecord)
            {
                record.lifecycleState = ReputationLifecycleState.Archived;
                record.lastModifiedWorldTime = request.worldTime;
                result = ReputationMutationResult.Success(request.preview ? ReputationOperationStatus.Preview : ReputationOperationStatus.Succeeded, "Reputation record archived.", new ReputationSnapshot(record), subject, audience.Id, record.recordId, string.Empty, 0, 0, 0, 0, 0, false, false, created, false, request.preview, before, before);
                return true;
            }

            ReputationDimensionValueData dimension = EnsureDimension(record, definition.Id);
            int previousBaseline = dimension.hasBaseline ? dimension.baselineValue : definition.NeutralValue;
            ReputationEffectiveValueSnapshot previousEffective = ResolveValue(subject, audience.Id, definition.Id);
            bool clamped = false;
            bool sourceAffected = false;
            int appliedDelta = 0;

            switch (request.mutationKind)
            {
                case ReputationMutationKind.SetBaseline:
                    dimension.baselineValue = definition.Clamp(request.value, out clamped);
                    dimension.hasBaseline = true;
                    appliedDelta = dimension.baselineValue - previousBaseline;
                    break;
                case ReputationMutationKind.AdjustBaseline:
                    dimension.baselineValue = definition.Clamp(previousBaseline + request.delta, out clamped);
                    dimension.hasBaseline = true;
                    appliedDelta = request.delta;
                    break;
                case ReputationMutationKind.ClearBaseline:
                    dimension.baselineValue = definition.NeutralValue;
                    dimension.hasBaseline = false;
                    appliedDelta = definition.NeutralValue - previousBaseline;
                    break;
                case ReputationMutationKind.AddOrReplaceContribution:
                    if (string.IsNullOrWhiteSpace(request.sourceId))
                    {
                        result = ReputationMutationResult.Failure(ReputationOperationStatus.InvalidSource, "Source contribution requires a source ID.", before);
                        return false;
                    }

                    string sourceId = request.sourceId.Trim();
                    ReputationContributionData contribution = (dimension.contributions ??= new List<ReputationContributionData>()).FirstOrDefault(item => string.Equals(item.sourceId, sourceId, StringComparison.Ordinal));
                    if (contribution == null)
                    {
                        contribution = new ReputationContributionData { sourceId = sourceId };
                        dimension.contributions.Add(contribution);
                    }

                    contribution.dimensionId = definition.Id;
                    contribution.amount = request.value;
                    contribution.sourceCategory = request.sourceCategory;
                    contribution.authenticity = request.authenticity;
                    contribution.worldTime = request.worldTime;
                    contribution.historicalEventId = request.historicalEventId ?? string.Empty;
                    contribution.supportingReferenceId = request.supportingReferenceId ?? string.Empty;
                    sourceAffected = true;
                    appliedDelta = request.value;
                    break;
                case ReputationMutationKind.RemoveContribution:
                    if (string.IsNullOrWhiteSpace(request.sourceId))
                    {
                        result = ReputationMutationResult.Failure(ReputationOperationStatus.InvalidSource, "Removing a source contribution requires a source ID.", before);
                        return false;
                    }

                    int removed = (dimension.contributions ?? new List<ReputationContributionData>()).RemoveAll(item => string.Equals(item.sourceId, request.sourceId.Trim(), StringComparison.Ordinal));
                    if (removed == 0)
                    {
                        result = ReputationMutationResult.Failure(ReputationOperationStatus.UnknownSource, $"Source contribution '{request.sourceId}' does not exist.", before);
                        return false;
                    }

                    sourceAffected = true;
                    break;
                default:
                    result = ReputationMutationResult.Failure(ReputationOperationStatus.InvalidRequest, $"Unsupported reputation mutation kind '{request.mutationKind}'.", before);
                    return false;
            }

            record.lastModifiedWorldTime = request.worldTime;
            RemoveEmptyNeutralDimensions(record);
            ReputationEffectiveValueSnapshot newEffective = ResolveValue(subject, audience.Id, definition.Id);
            clamped |= newEffective.Clamped;
            result = ReputationMutationResult.Success(request.preview ? ReputationOperationStatus.Preview : ReputationOperationStatus.Succeeded, "Reputation mutation applied.", new ReputationSnapshot(record), subject, audience.Id, record.recordId, definition.Id, previousBaseline, dimension.hasBaseline ? dimension.baselineValue : definition.NeutralValue, previousEffective.EffectiveValue, newEffective.EffectiveValue, appliedDelta, clamped, sourceAffected, created, duplicate: false, preview: request.preview, beforeRevision: before, afterRevision: before);
            return true;
        }

        private bool TryGetAudienceDefinition(string audienceId, out ReputationAudienceDefinition definition)
        {
            definition = null;
            return registry != null
                && !string.IsNullOrWhiteSpace(audienceId)
                && registry.TryGet(audienceId.Trim(), out definition);
        }

        private bool TryGetDimensionDefinition(string dimensionId, out ReputationDimensionDefinition definition)
        {
            definition = null;
            return registry != null
                && !string.IsNullOrWhiteSpace(dimensionId)
                && registry.TryGet(dimensionId.Trim(), out definition);
        }

        private bool ValidatePerson(string personId, out ReputationOperationStatus status, out string failure)
        {
            if (string.IsNullOrWhiteSpace(personId))
            {
                status = ReputationOperationStatus.MissingSubject;
                failure = "Reputation subject person ID is required.";
                return false;
            }

            if (knownPersonIds != null && knownPersonIds.Count > 0 && !knownPersonIds.Contains(personId.Trim()))
            {
                status = ReputationOperationStatus.UnknownSubject;
                failure = $"Reputation subject Person '{personId}' is unknown.";
                return false;
            }

            status = ReputationOperationStatus.Succeeded;
            failure = string.Empty;
            return true;
        }

        private static bool ValidateStaticPerson(string personId, HashSet<string> known, out string failure)
        {
            if (string.IsNullOrWhiteSpace(personId))
            {
                failure = "Reputation subject person ID is required.";
                return false;
            }

            if (known != null && known.Count > 0 && !known.Contains(personId.Trim()))
            {
                failure = $"Reputation record references unknown Person '{personId}'.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private IEnumerable<string> ResolveAudienceLineage(string audienceId)
        {
            string current = audienceId?.Trim() ?? string.Empty;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(current) && seen.Add(current))
            {
                yield return current;
                if (!TryGetAudienceDefinition(current, out ReputationAudienceDefinition audience) || !audience.SupportsHierarchy)
                {
                    yield break;
                }

                current = audience.ParentAudienceId;
            }
        }

        private bool TryGetRecordBySubjectAudience(string subjectPersonId, string audienceId, out ReputationRecordData record)
        {
            if (recordIdBySubjectAudience.TryGetValue(SubjectAudienceKey(subjectPersonId, audienceId), out string recordId)
                && recordsById.TryGetValue(recordId, out record)
                && record.lifecycleState == ReputationLifecycleState.Active)
            {
                return true;
            }

            record = null;
            return false;
        }

        private IReadOnlyList<ReputationSnapshot> Query(Func<ReputationRecordData, bool> predicate)
        {
            return Ordered(recordsById.Values.Where(predicate)).Select(record => new ReputationSnapshot(record)).ToArray();
        }

        private static ReputationDimensionValueData EnsureDimension(ReputationRecordData record, string dimensionId)
        {
            ReputationDimensionValueData value = FindDimension(record, dimensionId);
            if (value != null)
            {
                return value;
            }

            value = new ReputationDimensionValueData { dimensionId = dimensionId };
            record.dimensions ??= new List<ReputationDimensionValueData>();
            record.dimensions.Add(value);
            return value;
        }

        private static ReputationDimensionValueData FindDimension(ReputationRecordData record, string dimensionId)
        {
            return (record?.dimensions ?? new List<ReputationDimensionValueData>())
                .FirstOrDefault(item => string.Equals(item.dimensionId, dimensionId, StringComparison.Ordinal));
        }

        private static void RemoveEmptyNeutralDimensions(ReputationRecordData record)
        {
            if (record?.dimensions == null)
            {
                return;
            }

            record.dimensions.RemoveAll(item => item != null && !item.hasBaseline && (item.contributions == null || item.contributions.Count == 0));
            record.dimensions = record.dimensions.OrderBy(item => item.dimensionId, StringComparer.Ordinal).ToList();
        }

        private static IEnumerable<ReputationRecordData> Ordered(IEnumerable<ReputationRecordData> records)
        {
            return (records ?? Array.Empty<ReputationRecordData>())
                .OrderBy(record => record.subjectPersonId, StringComparer.Ordinal)
                .ThenBy(record => record.audienceId, StringComparer.Ordinal)
                .ThenBy(record => record.recordId, StringComparer.Ordinal);
        }

        private static IEnumerable<ReputationContributionData> OrderedContributions(IEnumerable<ReputationContributionData> contributions)
        {
            return (contributions ?? Array.Empty<ReputationContributionData>())
                .Where(item => item != null)
                .OrderBy(item => item.dimensionId, StringComparer.Ordinal)
                .ThenBy(item => item.sourceCategory)
                .ThenBy(item => item.authenticity)
                .ThenBy(item => item.sourceId, StringComparer.Ordinal);
        }

        private void RestoreInternal(ReputationRuntimeSaveData saveData)
        {
            recordsById.Clear();
            recordIdBySubjectAudience.Clear();
            processedTransactionIds.Clear();
            foreach (ReputationRecordData record in saveData?.records ?? new List<ReputationRecordData>())
            {
                ReputationRecordData clone = record.Clone();
                recordsById[clone.recordId] = clone;
                if (clone.lifecycleState == ReputationLifecycleState.Active)
                {
                    recordIdBySubjectAudience[SubjectAudienceKey(clone.subjectPersonId, clone.audienceId)] = clone.recordId;
                }
            }

            foreach (string transactionId in saveData?.processedTransactionIds ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(transactionId))
                {
                    processedTransactionIds.Add(transactionId.Trim());
                }
            }

            revision = saveData?.revision ?? 0L;
        }

        private void RebuildIndexes()
        {
            recordIdBySubjectAudience.Clear();
            foreach (ReputationRecordData record in recordsById.Values.Where(item => item.lifecycleState == ReputationLifecycleState.Active))
            {
                recordIdBySubjectAudience[SubjectAudienceKey(record.subjectPersonId, record.audienceId)] = record.recordId;
            }
        }

        public static string DefaultRecordId(string subjectPersonId, string audienceId)
        {
            return $"reputation-record.{subjectPersonId}.{audienceId}";
        }

        private static string SubjectAudienceKey(string subjectPersonId, string audienceId)
        {
            return $"{subjectPersonId?.Trim() ?? string.Empty}|{audienceId?.Trim() ?? string.Empty}";
        }
    }
}
