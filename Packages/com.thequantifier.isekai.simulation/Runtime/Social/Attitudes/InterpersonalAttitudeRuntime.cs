using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Attitudes
{
    public sealed class InterpersonalAttitudeRuntime
    {
        private readonly Dictionary<string, InterpersonalAttitudeRecordData> recordsById = new Dictionary<string, InterpersonalAttitudeRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> recordIdByPair = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> processedTransactionIds = new HashSet<string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private bool dirty;
        private bool restoring;
        private bool disposed;

        public event Action<AttitudeMutationResult> AttitudeChanged;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public bool IsReady => registry != null && !disposed;
        public int Count => recordsById.Count;
        public IReadOnlyList<InterpersonalAttitudeSnapshot> Snapshots => Ordered(recordsById.Values).Select(record => new InterpersonalAttitudeSnapshot(record)).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, IEnumerable<string> persons)
        {
            registry = definitionRegistry;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
            disposed = false;
            RebuildIndexes();
        }

        public AttitudeMutationResult Mutate(AttitudeMutationRequest request)
        {
            request ??= new AttitudeMutationRequest();
            long before = revision;
            if (!IsReady || restoring)
            {
                return AttitudeMutationResult.Failure(AttitudeOperationStatus.RuntimeNotReady, "Interpersonal attitude runtime is not ready for mutation.", before);
            }

            if (string.IsNullOrWhiteSpace(request.transactionId))
            {
                return AttitudeMutationResult.Failure(AttitudeOperationStatus.MissingTransactionId, "Attitude mutation requires a transaction ID.", before);
            }

            string transactionId = request.transactionId.Trim();
            if (processedTransactionIds.Contains(transactionId))
            {
                InterpersonalAttitudeSnapshot duplicateSnapshot = TryGetSnapshotByPair(request.observerPersonId, request.subjectPersonId, out InterpersonalAttitudeSnapshot existing) ? existing : null;
                return AttitudeMutationResult.Success(AttitudeOperationStatus.Duplicate, "Attitude mutation transaction was already processed.", duplicateSnapshot, request.observerPersonId, request.subjectPersonId, duplicateSnapshot?.RecordId ?? string.Empty, request.dimensionId, 0, 0, 0, 0, 0, false, false, false, true, false, before, before);
            }

            InterpersonalAttitudeRuntimeSaveData rollback = CreateSaveData();
            if (!TryApply(request, out AttitudeMutationResult result))
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
            InterpersonalAttitudeRecordData committed = recordsById.TryGetValue(result.RecordId, out InterpersonalAttitudeRecordData record) ? record : null;
            if (committed != null)
            {
                committed.revision++;
            }

            AttitudeMutationResult committedResult = AttitudeMutationResult.Success(
                AttitudeOperationStatus.Succeeded,
                result.Message,
                committed == null ? result.Snapshot : new InterpersonalAttitudeSnapshot(committed),
                result.ObserverPersonId,
                result.SubjectPersonId,
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
            AttitudeChanged?.Invoke(committedResult);
            return committedResult;
        }

        public bool TryGetSnapshot(string recordId, out InterpersonalAttitudeSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(recordId) && recordsById.TryGetValue(recordId.Trim(), out InterpersonalAttitudeRecordData record))
            {
                snapshot = new InterpersonalAttitudeSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool TryGetSnapshotByPair(string observerPersonId, string subjectPersonId, out InterpersonalAttitudeSnapshot snapshot)
        {
            if (recordIdByPair.TryGetValue(PairKey(observerPersonId, subjectPersonId), out string recordId))
            {
                return TryGetSnapshot(recordId, out snapshot);
            }

            snapshot = null;
            return false;
        }

        public AttitudeEffectiveValueSnapshot ResolveValue(string observerPersonId, string subjectPersonId, string dimensionId)
        {
            if (!TryGetDefinition(dimensionId, out AttitudeDimensionDefinition definition))
            {
                return new AttitudeEffectiveValueSnapshot(dimensionId, 0, false, Array.Empty<AttitudeContributionSnapshot>(), 0, 0, false, false);
            }

            bool recordExists = TryGetRecordByPair(observerPersonId, subjectPersonId, out InterpersonalAttitudeRecordData record);
            AttitudeDimensionValueData value = recordExists ? FindDimension(record, definition.Id) : null;
            int baseline = value != null && value.hasBaseline ? value.baselineValue : definition.NeutralValue;
            IReadOnlyList<AttitudeContributionSnapshot> contributions = OrderedContributions(value?.contributions).Select(item => new AttitudeContributionSnapshot(item)).ToArray();
            int raw = baseline + contributions.Sum(item => item.Amount);
            int effective = definition.Clamp(raw, out bool clamped);
            return new AttitudeEffectiveValueSnapshot(definition.Id, baseline, value?.hasBaseline == true, contributions, raw, effective, clamped, recordExists);
        }

        public IReadOnlyList<AttitudeEffectiveValueSnapshot> ResolveAllDimensions(string observerPersonId, string subjectPersonId)
        {
            return AttitudeDefinitions()
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .Select(definition => ResolveValue(observerPersonId, subjectPersonId, definition.Id))
                .ToArray();
        }

        public IReadOnlyList<InterpersonalAttitudeSnapshot> QueryByObserver(string observerPersonId)
        {
            return Query(record => string.Equals(record.observerPersonId, observerPersonId, StringComparison.Ordinal));
        }

        public IReadOnlyList<InterpersonalAttitudeSnapshot> QueryBySubject(string subjectPersonId)
        {
            return Query(record => string.Equals(record.subjectPersonId, subjectPersonId, StringComparison.Ordinal));
        }

        public IReadOnlyList<InterpersonalAttitudeSnapshot> QueryNonNeutral(string dimensionId)
        {
            return Query(record => ResolveValue(record.observerPersonId, record.subjectPersonId, dimensionId).EffectiveValue != NeutralValue(dimensionId));
        }

        public IReadOnlyList<InterpersonalAttitudeSnapshot> QueryByThreshold(string dimensionId, AttitudeThresholdComparison comparison, int value, int minimum = 0, int maximum = 0)
        {
            return Query(record => EvaluateThreshold(new AttitudeThresholdRequest
            {
                observerPersonId = record.observerPersonId,
                subjectPersonId = record.subjectPersonId,
                dimensionId = dimensionId,
                comparison = comparison,
                value = value,
                minimum = minimum,
                maximum = maximum
            }).Passed);
        }

        public IReadOnlyList<InterpersonalAttitudeSnapshot> QueryByHistoricalEvent(string historicalEventId)
        {
            return Query(record => !string.IsNullOrWhiteSpace(historicalEventId)
                && (string.Equals(record.sourceHistoricalEventId, historicalEventId, StringComparison.Ordinal)
                    || (record.dimensions ?? new List<AttitudeDimensionValueData>()).Any(dimension => (dimension.contributions ?? new List<AttitudeContributionData>()).Any(contribution => string.Equals(contribution.historicalEventId, historicalEventId, StringComparison.Ordinal)))));
        }

        public IReadOnlyList<InterpersonalAttitudeSnapshot> QueryModifiedBetween(double minimumWorldTime, double maximumWorldTime)
        {
            return Query(record => record.lastModifiedWorldTime >= minimumWorldTime && record.lastModifiedWorldTime <= maximumWorldTime);
        }

        public AttitudeThresholdResult EvaluateThreshold(AttitudeThresholdRequest request)
        {
            request ??= new AttitudeThresholdRequest();
            if (!ValidatePerson(request.observerPersonId, true, out AttitudeOperationStatus personStatus, out string personFailure))
            {
                return new AttitudeThresholdResult(false, personStatus, personFailure, request.observerPersonId, request.subjectPersonId, request.dimensionId, 0, request.value, request.minimum, request.maximum, request.comparison, false);
            }

            if (!ValidatePerson(request.subjectPersonId, false, out personStatus, out personFailure))
            {
                return new AttitudeThresholdResult(false, personStatus, personFailure, request.observerPersonId, request.subjectPersonId, request.dimensionId, 0, request.value, request.minimum, request.maximum, request.comparison, false);
            }

            if (!TryGetDefinition(request.dimensionId, out AttitudeDimensionDefinition definition))
            {
                return new AttitudeThresholdResult(false, AttitudeOperationStatus.MissingDimensionDefinition, $"Attitude Dimension '{request.dimensionId}' is missing.", request.observerPersonId, request.subjectPersonId, request.dimensionId, 0, request.value, request.minimum, request.maximum, request.comparison, false);
            }

            AttitudeEffectiveValueSnapshot value = ResolveValue(request.observerPersonId, request.subjectPersonId, definition.Id);
            bool passed = request.comparison switch
            {
                AttitudeThresholdComparison.Equal => value.EffectiveValue == request.value,
                AttitudeThresholdComparison.NotEqual => value.EffectiveValue != request.value,
                AttitudeThresholdComparison.LessThan => value.EffectiveValue < request.value,
                AttitudeThresholdComparison.LessThanOrEqual => value.EffectiveValue <= request.value,
                AttitudeThresholdComparison.GreaterThan => value.EffectiveValue > request.value,
                AttitudeThresholdComparison.GreaterThanOrEqual => value.EffectiveValue >= request.value,
                AttitudeThresholdComparison.WithinInclusiveRange => value.EffectiveValue >= request.minimum && value.EffectiveValue <= request.maximum,
                AttitudeThresholdComparison.OutsideInclusiveRange => value.EffectiveValue < request.minimum || value.EffectiveValue > request.maximum,
                _ => false
            };
            return new AttitudeThresholdResult(passed, AttitudeOperationStatus.Succeeded, passed ? "Threshold passed." : "Threshold failed.", request.observerPersonId, request.subjectPersonId, definition.Id, value.EffectiveValue, request.value, request.minimum, request.maximum, request.comparison, value.RecordExists);
        }

        public bool IsTrustAtLeast(string observerPersonId, string subjectPersonId, int threshold)
        {
            return EvaluateThreshold(new AttitudeThresholdRequest { observerPersonId = observerPersonId, subjectPersonId = subjectPersonId, dimensionId = PrototypeAttitudeDefinitionFactory.TrustId, comparison = AttitudeThresholdComparison.GreaterThanOrEqual, value = threshold }).Passed;
        }

        public bool IsHostilityAbove(string observerPersonId, string subjectPersonId, int threshold)
        {
            return EvaluateThreshold(new AttitudeThresholdRequest { observerPersonId = observerPersonId, subjectPersonId = subjectPersonId, dimensionId = PrototypeAttitudeDefinitionFactory.HostilityId, comparison = AttitudeThresholdComparison.GreaterThan, value = threshold }).Passed;
        }

        public bool IsFearWithin(string observerPersonId, string subjectPersonId, int minimum, int maximum)
        {
            return EvaluateThreshold(new AttitudeThresholdRequest { observerPersonId = observerPersonId, subjectPersonId = subjectPersonId, dimensionId = PrototypeAttitudeDefinitionFactory.FearId, comparison = AttitudeThresholdComparison.WithinInclusiveRange, minimum = minimum, maximum = maximum }).Passed;
        }

        public InterpersonalAttitudeRuntimeSaveData CreateSaveData()
        {
            return new InterpersonalAttitudeRuntimeSaveData
            {
                schemaVersion = InterpersonalAttitudeRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                records = Ordered(recordsById.Values).Select(record => record.Clone()).ToList(),
                processedTransactionIds = processedTransactionIds.OrderBy(value => value, StringComparer.Ordinal).ToList()
            };
        }

        public AttitudeMutationResult RestoreFromSaveData(InterpersonalAttitudeRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, bool restoringState = true)
        {
            long before = revision;
            if (!ValidateSaveData(saveData, definitionRegistry, persons, out string failureReason))
            {
                return AttitudeMutationResult.Failure(AttitudeOperationStatus.RestoreFailed, failureReason, before);
            }

            Configure(definitionRegistry, persons);
            restoring = true;
            RestoreInternal(saveData ?? new InterpersonalAttitudeRuntimeSaveData());
            restoring = false;
            dirty = !restoringState;
            return AttitudeMutationResult.Success(AttitudeOperationStatus.Succeeded, "Interpersonal attitudes restored.", null, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, 0, 0, 0, false, false, false, false, false, before, revision);
        }

        public static bool ValidateSaveData(InterpersonalAttitudeRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, IEnumerable<string> persons, out string failureReason)
        {
            failureReason = string.Empty;
            InterpersonalAttitudeRuntimeSaveData effective = saveData ?? new InterpersonalAttitudeRuntimeSaveData();
            if (effective.schemaVersion != InterpersonalAttitudeRuntimeSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Interpersonal Attitude save schema version {effective.schemaVersion}.";
                return false;
            }

            if (definitionRegistry == null)
            {
                failureReason = "Interpersonal Attitude runtime requires a definition registry.";
                return false;
            }

            HashSet<string> known = new HashSet<string>((persons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (InterpersonalAttitudeRecordData raw in effective.records ?? new List<InterpersonalAttitudeRecordData>())
            {
                InterpersonalAttitudeRecordData record = raw?.Clone();
                if (record == null)
                {
                    failureReason = "Interpersonal Attitude save contains a null record.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(record.recordId) || !ids.Add(record.recordId))
                {
                    failureReason = $"Interpersonal Attitude save contains duplicate or empty record ID '{record.recordId}'.";
                    return false;
                }

                if (!ValidateStaticPerson(record.observerPersonId, true, known, out failureReason)
                    || !ValidateStaticPerson(record.subjectPersonId, false, known, out failureReason))
                {
                    return false;
                }

                if (string.Equals(record.observerPersonId, record.subjectPersonId, StringComparison.Ordinal))
                {
                    failureReason = $"Interpersonal Attitude record '{record.recordId}' cannot target the observer as subject.";
                    return false;
                }

                if (!pairs.Add(PairKey(record.observerPersonId, record.subjectPersonId)))
                {
                    failureReason = $"Interpersonal Attitude save contains duplicate ordered pair '{record.observerPersonId}->{record.subjectPersonId}'.";
                    return false;
                }

                HashSet<string> dimensions = new HashSet<string>(StringComparer.Ordinal);
                foreach (AttitudeDimensionValueData dimension in record.dimensions ?? new List<AttitudeDimensionValueData>())
                {
                    if (dimension == null || string.IsNullOrWhiteSpace(dimension.dimensionId) || !definitionRegistry.TryGet(dimension.dimensionId, out AttitudeDimensionDefinition definition))
                    {
                        failureReason = $"Interpersonal Attitude record '{record.recordId}' references missing Attitude Dimension '{dimension?.dimensionId}'.";
                        return false;
                    }

                    if (!dimensions.Add(definition.Id))
                    {
                        failureReason = $"Interpersonal Attitude record '{record.recordId}' contains duplicate dimension '{definition.Id}'.";
                        return false;
                    }

                    if (dimension.hasBaseline && (dimension.baselineValue < definition.MinimumValue || dimension.baselineValue > definition.MaximumValue))
                    {
                        failureReason = $"Interpersonal Attitude record '{record.recordId}' dimension '{definition.Id}' has baseline {dimension.baselineValue} outside the authored range {definition.MinimumValue}..{definition.MaximumValue}.";
                        return false;
                    }

                    HashSet<string> sources = new HashSet<string>(StringComparer.Ordinal);
                    foreach (AttitudeContributionData contribution in dimension.contributions ?? new List<AttitudeContributionData>())
                    {
                        if (contribution == null || string.IsNullOrWhiteSpace(contribution.sourceId))
                        {
                            failureReason = $"Interpersonal Attitude record '{record.recordId}' contains a contribution with no source ID.";
                            return false;
                        }

                        if (!string.Equals(contribution.dimensionId, definition.Id, StringComparison.Ordinal))
                        {
                            failureReason = $"Interpersonal Attitude contribution '{contribution.sourceId}' is stored under dimension '{definition.Id}' but references '{contribution.dimensionId}'.";
                            return false;
                        }

                        if (!sources.Add(contribution.sourceId))
                        {
                            failureReason = $"Interpersonal Attitude record '{record.recordId}' contains duplicate contribution source '{contribution.sourceId}' for dimension '{definition.Id}'.";
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public void Clear()
        {
            recordsById.Clear();
            recordIdByPair.Clear();
            processedTransactionIds.Clear();
            revision++;
            dirty = true;
        }

        public void Dispose()
        {
            disposed = true;
            recordsById.Clear();
            recordIdByPair.Clear();
            processedTransactionIds.Clear();
        }

        private bool TryApply(AttitudeMutationRequest request, out AttitudeMutationResult result)
        {
            long before = revision;
            result = null;
            if (!ValidatePerson(request.observerPersonId, true, out AttitudeOperationStatus personStatus, out string personFailure)
                || !ValidatePerson(request.subjectPersonId, false, out personStatus, out personFailure))
            {
                result = AttitudeMutationResult.Failure(personStatus, personFailure, before);
                return false;
            }

            string observer = request.observerPersonId.Trim();
            string subject = request.subjectPersonId.Trim();
            if (string.Equals(observer, subject, StringComparison.Ordinal))
            {
                result = AttitudeMutationResult.Failure(AttitudeOperationStatus.SelfAttitudeNotAllowed, "Interpersonal attitudes cannot target the observer as subject.", before);
                return false;
            }

            if (!TryGetDefinition(request.dimensionId, out AttitudeDimensionDefinition definition)
                && request.mutationKind != AttitudeMutationKind.EnsureRecord)
            {
                result = AttitudeMutationResult.Failure(AttitudeOperationStatus.MissingDimensionDefinition, $"Attitude Dimension '{request.dimensionId}' is missing.", before);
                return false;
            }

            string pairKey = PairKey(observer, subject);
            string requestedRecordId = string.IsNullOrWhiteSpace(request.recordId) ? DefaultRecordId(observer, subject) : request.recordId.Trim();
            bool created = false;
            if (!TryGetRecordByPair(observer, subject, out InterpersonalAttitudeRecordData record))
            {
                if (recordsById.ContainsKey(requestedRecordId))
                {
                    result = AttitudeMutationResult.Failure(AttitudeOperationStatus.DuplicateRecordId, $"Interpersonal Attitude record ID '{requestedRecordId}' already exists.", before);
                    return false;
                }

                record = new InterpersonalAttitudeRecordData
                {
                    recordId = requestedRecordId,
                    observerPersonId = observer,
                    subjectPersonId = subject,
                    createdWorldTime = request.worldTime,
                    lastModifiedWorldTime = request.worldTime,
                    sourceHistoricalEventId = request.historicalEventId ?? string.Empty,
                    sourceRelationshipRecordId = request.relationshipRecordId ?? string.Empty,
                    revision = 1L
                };
                recordsById[record.recordId] = record;
                recordIdByPair[pairKey] = record.recordId;
                created = true;
            }
            else if (!string.IsNullOrWhiteSpace(request.recordId) && !string.Equals(record.recordId, requestedRecordId, StringComparison.Ordinal))
            {
                result = AttitudeMutationResult.Failure(AttitudeOperationStatus.DuplicateOrderedPair, $"Ordered attitude pair '{observer}->{subject}' already has record '{record.recordId}'.", before);
                return false;
            }

            if (request.mutationKind == AttitudeMutationKind.EnsureRecord)
            {
                result = AttitudeMutationResult.Success(request.preview ? AttitudeOperationStatus.Preview : AttitudeOperationStatus.Succeeded, created ? "Attitude record created." : "Attitude record already exists.", new InterpersonalAttitudeSnapshot(record), observer, subject, record.recordId, string.Empty, 0, 0, 0, 0, 0, false, false, created, duplicate: !created, preview: request.preview, beforeRevision: before, afterRevision: before);
                return true;
            }

            AttitudeDimensionValueData dimension = EnsureDimension(record, definition.Id);
            int previousBaseline = dimension.hasBaseline ? dimension.baselineValue : definition.NeutralValue;
            AttitudeEffectiveValueSnapshot previousEffective = ResolveValue(observer, subject, definition.Id);
            bool clamped = false;
            bool sourceAffected = false;
            int appliedDelta = 0;

            switch (request.mutationKind)
            {
                case AttitudeMutationKind.SetBaseline:
                    dimension.baselineValue = definition.Clamp(request.value, out clamped);
                    dimension.hasBaseline = true;
                    appliedDelta = dimension.baselineValue - previousBaseline;
                    break;
                case AttitudeMutationKind.AdjustBaseline:
                    dimension.baselineValue = definition.Clamp(previousBaseline + request.delta, out clamped);
                    dimension.hasBaseline = true;
                    appliedDelta = request.delta;
                    break;
                case AttitudeMutationKind.ClearBaseline:
                    dimension.baselineValue = definition.NeutralValue;
                    dimension.hasBaseline = false;
                    appliedDelta = definition.NeutralValue - previousBaseline;
                    break;
                case AttitudeMutationKind.AddOrReplaceContribution:
                    if (string.IsNullOrWhiteSpace(request.sourceId))
                    {
                        result = AttitudeMutationResult.Failure(AttitudeOperationStatus.InvalidSource, "Source contribution requires a source ID.", before);
                        return false;
                    }

                    string sourceId = request.sourceId.Trim();
                    AttitudeContributionData contribution = (dimension.contributions ??= new List<AttitudeContributionData>()).FirstOrDefault(item => string.Equals(item.sourceId, sourceId, StringComparison.Ordinal));
                    if (contribution == null)
                    {
                        contribution = new AttitudeContributionData { sourceId = sourceId };
                        dimension.contributions.Add(contribution);
                    }

                    contribution.dimensionId = definition.Id;
                    contribution.amount = request.value;
                    contribution.sourceCategory = request.sourceCategory;
                    contribution.worldTime = request.worldTime;
                    contribution.historicalEventId = request.historicalEventId ?? string.Empty;
                    contribution.relationshipRecordId = request.relationshipRecordId ?? string.Empty;
                    sourceAffected = true;
                    appliedDelta = request.value;
                    break;
                case AttitudeMutationKind.RemoveContribution:
                    if (string.IsNullOrWhiteSpace(request.sourceId))
                    {
                        result = AttitudeMutationResult.Failure(AttitudeOperationStatus.InvalidSource, "Removing a source contribution requires a source ID.", before);
                        return false;
                    }

                    int removed = (dimension.contributions ?? new List<AttitudeContributionData>()).RemoveAll(item => string.Equals(item.sourceId, request.sourceId.Trim(), StringComparison.Ordinal));
                    if (removed == 0)
                    {
                        result = AttitudeMutationResult.Failure(AttitudeOperationStatus.UnknownSource, $"Source contribution '{request.sourceId}' does not exist.", before);
                        return false;
                    }

                    sourceAffected = true;
                    break;
                default:
                    result = AttitudeMutationResult.Failure(AttitudeOperationStatus.InvalidRequest, $"Unsupported attitude mutation kind '{request.mutationKind}'.", before);
                    return false;
            }

            record.lastModifiedWorldTime = request.worldTime;
            RemoveEmptyNeutralDimensions(record);
            AttitudeEffectiveValueSnapshot newEffective = ResolveValue(observer, subject, definition.Id);
            clamped |= newEffective.Clamped;
            result = AttitudeMutationResult.Success(request.preview ? AttitudeOperationStatus.Preview : AttitudeOperationStatus.Succeeded, "Attitude mutation applied.", new InterpersonalAttitudeSnapshot(record), observer, subject, record.recordId, definition.Id, previousBaseline, dimension.hasBaseline ? dimension.baselineValue : definition.NeutralValue, previousEffective.EffectiveValue, newEffective.EffectiveValue, appliedDelta, clamped, sourceAffected, created, duplicate: false, preview: request.preview, beforeRevision: before, afterRevision: before);
            return true;
        }

        private bool TryGetDefinition(string dimensionId, out AttitudeDimensionDefinition definition)
        {
            definition = null;
            return registry != null
                && !string.IsNullOrWhiteSpace(dimensionId)
                && registry.TryGet(dimensionId.Trim(), out definition);
        }

        private IEnumerable<AttitudeDimensionDefinition> AttitudeDefinitions()
        {
            return registry?.DefinitionsById.Values.OfType<AttitudeDimensionDefinition>() ?? Array.Empty<AttitudeDimensionDefinition>();
        }

        private bool ValidatePerson(string personId, bool observer, out AttitudeOperationStatus status, out string failure)
        {
            return ValidateStaticPerson(personId, observer, knownPersonIds, out status, out failure);
        }

        private static bool ValidateStaticPerson(string personId, bool observer, HashSet<string> known, out string failure)
        {
            bool valid = ValidateStaticPerson(personId, observer, known, out _, out failure);
            return valid;
        }

        private static bool ValidateStaticPerson(string personId, bool observer, HashSet<string> known, out AttitudeOperationStatus status, out string failure)
        {
            if (string.IsNullOrWhiteSpace(personId))
            {
                status = observer ? AttitudeOperationStatus.MissingObserver : AttitudeOperationStatus.MissingSubject;
                failure = observer ? "Observer person ID is required." : "Subject person ID is required.";
                return false;
            }

            if (known != null && known.Count > 0 && !known.Contains(personId.Trim()))
            {
                status = observer ? AttitudeOperationStatus.UnknownObserver : AttitudeOperationStatus.UnknownSubject;
                failure = observer ? $"Observer Person '{personId}' is unknown." : $"Subject Person '{personId}' is unknown.";
                return false;
            }

            status = AttitudeOperationStatus.Succeeded;
            failure = string.Empty;
            return true;
        }

        private bool TryGetRecordByPair(string observerPersonId, string subjectPersonId, out InterpersonalAttitudeRecordData record)
        {
            if (recordIdByPair.TryGetValue(PairKey(observerPersonId, subjectPersonId), out string recordId)
                && recordsById.TryGetValue(recordId, out record))
            {
                return true;
            }

            record = null;
            return false;
        }

        private IReadOnlyList<InterpersonalAttitudeSnapshot> Query(Func<InterpersonalAttitudeRecordData, bool> predicate)
        {
            return Ordered(recordsById.Values.Where(predicate)).Select(record => new InterpersonalAttitudeSnapshot(record)).ToArray();
        }

        private int NeutralValue(string dimensionId)
        {
            return TryGetDefinition(dimensionId, out AttitudeDimensionDefinition definition) ? definition.NeutralValue : 0;
        }

        private static AttitudeDimensionValueData EnsureDimension(InterpersonalAttitudeRecordData record, string dimensionId)
        {
            AttitudeDimensionValueData value = FindDimension(record, dimensionId);
            if (value != null)
            {
                return value;
            }

            value = new AttitudeDimensionValueData { dimensionId = dimensionId };
            record.dimensions ??= new List<AttitudeDimensionValueData>();
            record.dimensions.Add(value);
            return value;
        }

        private static AttitudeDimensionValueData FindDimension(InterpersonalAttitudeRecordData record, string dimensionId)
        {
            return (record?.dimensions ?? new List<AttitudeDimensionValueData>())
                .FirstOrDefault(item => string.Equals(item.dimensionId, dimensionId, StringComparison.Ordinal));
        }

        private static void RemoveEmptyNeutralDimensions(InterpersonalAttitudeRecordData record)
        {
            if (record?.dimensions == null)
            {
                return;
            }

            record.dimensions.RemoveAll(item => item != null && !item.hasBaseline && (item.contributions == null || item.contributions.Count == 0));
            record.dimensions = record.dimensions.OrderBy(item => item.dimensionId, StringComparer.Ordinal).ToList();
        }

        private static IEnumerable<InterpersonalAttitudeRecordData> Ordered(IEnumerable<InterpersonalAttitudeRecordData> records)
        {
            return (records ?? Array.Empty<InterpersonalAttitudeRecordData>())
                .OrderBy(record => record.observerPersonId, StringComparer.Ordinal)
                .ThenBy(record => record.subjectPersonId, StringComparer.Ordinal)
                .ThenBy(record => record.recordId, StringComparer.Ordinal);
        }

        private static IEnumerable<AttitudeContributionData> OrderedContributions(IEnumerable<AttitudeContributionData> contributions)
        {
            return (contributions ?? Array.Empty<AttitudeContributionData>())
                .Where(item => item != null)
                .OrderBy(item => item.dimensionId, StringComparer.Ordinal)
                .ThenBy(item => item.sourceCategory)
                .ThenBy(item => item.sourceId, StringComparer.Ordinal);
        }

        private void RestoreInternal(InterpersonalAttitudeRuntimeSaveData saveData)
        {
            recordsById.Clear();
            recordIdByPair.Clear();
            processedTransactionIds.Clear();
            foreach (InterpersonalAttitudeRecordData record in saveData?.records ?? new List<InterpersonalAttitudeRecordData>())
            {
                InterpersonalAttitudeRecordData clone = record.Clone();
                recordsById[clone.recordId] = clone;
                recordIdByPair[PairKey(clone.observerPersonId, clone.subjectPersonId)] = clone.recordId;
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
            recordIdByPair.Clear();
            foreach (InterpersonalAttitudeRecordData record in recordsById.Values)
            {
                recordIdByPair[PairKey(record.observerPersonId, record.subjectPersonId)] = record.recordId;
            }
        }

        public static string DefaultRecordId(string observerPersonId, string subjectPersonId)
        {
            return $"attitude-record.{observerPersonId}.{subjectPersonId}";
        }

        private static string PairKey(string observerPersonId, string subjectPersonId)
        {
            return $"{observerPersonId?.Trim() ?? string.Empty}->{subjectPersonId?.Trim() ?? string.Empty}";
        }
    }
}
