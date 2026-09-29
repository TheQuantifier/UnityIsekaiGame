using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Social.Attitudes
{
    [Serializable]
    public sealed class AttitudeContributionData
    {
        public string sourceId;
        public AttitudeContributionSourceCategory sourceCategory;
        public string dimensionId;
        public int amount;
        public double worldTime;
        public string historicalEventId;
        public string relationshipRecordId;

        public AttitudeContributionData Clone()
        {
            return new AttitudeContributionData
            {
                sourceId = sourceId ?? string.Empty,
                sourceCategory = sourceCategory,
                dimensionId = dimensionId ?? string.Empty,
                amount = amount,
                worldTime = worldTime,
                historicalEventId = historicalEventId ?? string.Empty,
                relationshipRecordId = relationshipRecordId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class AttitudeDimensionValueData
    {
        public string dimensionId;
        public bool hasBaseline;
        public int baselineValue;
        public List<AttitudeContributionData> contributions = new List<AttitudeContributionData>();

        public AttitudeDimensionValueData Clone()
        {
            return new AttitudeDimensionValueData
            {
                dimensionId = dimensionId ?? string.Empty,
                hasBaseline = hasBaseline,
                baselineValue = baselineValue,
                contributions = contributions == null ? new List<AttitudeContributionData>() : contributions.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    [Serializable]
    public sealed class InterpersonalAttitudeRecordData
    {
        public string recordId;
        public string observerPersonId;
        public string subjectPersonId;
        public List<AttitudeDimensionValueData> dimensions = new List<AttitudeDimensionValueData>();
        public double createdWorldTime;
        public double lastModifiedWorldTime;
        public string sourceRelationshipRecordId;
        public string sourceHistoricalEventId;
        public long revision = 1L;

        public InterpersonalAttitudeRecordData Clone()
        {
            return new InterpersonalAttitudeRecordData
            {
                recordId = recordId ?? string.Empty,
                observerPersonId = observerPersonId ?? string.Empty,
                subjectPersonId = subjectPersonId ?? string.Empty,
                dimensions = dimensions == null ? new List<AttitudeDimensionValueData>() : dimensions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                createdWorldTime = createdWorldTime,
                lastModifiedWorldTime = lastModifiedWorldTime,
                sourceRelationshipRecordId = sourceRelationshipRecordId ?? string.Empty,
                sourceHistoricalEventId = sourceHistoricalEventId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class InterpersonalAttitudeRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<InterpersonalAttitudeRecordData> records = new List<InterpersonalAttitudeRecordData>();
        public List<string> processedTransactionIds = new List<string>();

        public InterpersonalAttitudeRuntimeSaveData Clone()
        {
            return new InterpersonalAttitudeRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                records = records == null ? new List<InterpersonalAttitudeRecordData>() : records.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                processedTransactionIds = Clean(processedTransactionIds).ToList()
            };
        }

        private static IEnumerable<string> Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal);
        }
    }

    public sealed class AttitudeMutationRequest
    {
        public string transactionId;
        public string recordId;
        public string observerPersonId;
        public string subjectPersonId;
        public string dimensionId;
        public AttitudeMutationKind mutationKind;
        public int value;
        public int delta;
        public string sourceId;
        public AttitudeContributionSourceCategory sourceCategory = AttitudeContributionSourceCategory.Unknown;
        public string historicalEventId;
        public string relationshipRecordId;
        public double worldTime;
        public bool preview;
    }

    public sealed class AttitudeContributionSnapshot
    {
        public AttitudeContributionSnapshot(AttitudeContributionData data)
        {
            Data = data?.Clone() ?? new AttitudeContributionData();
        }

        public AttitudeContributionData Data { get; }
        public string SourceId => Data.sourceId ?? string.Empty;
        public AttitudeContributionSourceCategory SourceCategory => Data.sourceCategory;
        public string DimensionId => Data.dimensionId ?? string.Empty;
        public int Amount => Data.amount;
        public double WorldTime => Data.worldTime;
        public string HistoricalEventId => Data.historicalEventId ?? string.Empty;
        public string RelationshipRecordId => Data.relationshipRecordId ?? string.Empty;
    }

    public sealed class AttitudeEffectiveValueSnapshot
    {
        public AttitudeEffectiveValueSnapshot(string dimensionId, int baselineValue, bool hasBaseline, IReadOnlyList<AttitudeContributionSnapshot> contributions, int rawValue, int effectiveValue, bool clamped, bool recordExists)
        {
            DimensionId = dimensionId ?? string.Empty;
            BaselineValue = baselineValue;
            HasBaseline = hasBaseline;
            Contributions = (contributions ?? Array.Empty<AttitudeContributionSnapshot>()).ToArray();
            RawValue = rawValue;
            EffectiveValue = effectiveValue;
            Clamped = clamped;
            RecordExists = recordExists;
        }

        public string DimensionId { get; }
        public int BaselineValue { get; }
        public bool HasBaseline { get; }
        public IReadOnlyList<AttitudeContributionSnapshot> Contributions { get; }
        public int RawValue { get; }
        public int EffectiveValue { get; }
        public bool Clamped { get; }
        public bool RecordExists { get; }
        public bool IsNeutralDefault => !RecordExists || (!HasBaseline && Contributions.Count == 0);
    }

    public sealed class InterpersonalAttitudeSnapshot
    {
        public InterpersonalAttitudeSnapshot(InterpersonalAttitudeRecordData data)
        {
            Data = data?.Clone() ?? new InterpersonalAttitudeRecordData();
        }

        public InterpersonalAttitudeRecordData Data { get; }
        public string RecordId => Data.recordId ?? string.Empty;
        public string ObserverPersonId => Data.observerPersonId ?? string.Empty;
        public string SubjectPersonId => Data.subjectPersonId ?? string.Empty;
        public IReadOnlyList<AttitudeDimensionValueData> Dimensions => Data.dimensions ?? new List<AttitudeDimensionValueData>();
        public double CreatedWorldTime => Data.createdWorldTime;
        public double LastModifiedWorldTime => Data.lastModifiedWorldTime;
        public string SourceRelationshipRecordId => Data.sourceRelationshipRecordId ?? string.Empty;
        public string SourceHistoricalEventId => Data.sourceHistoricalEventId ?? string.Empty;
        public long Revision => Data.revision;
    }

    public sealed class AttitudeMutationResult
    {
        private AttitudeMutationResult(
            bool succeeded,
            AttitudeOperationStatus status,
            string message,
            InterpersonalAttitudeSnapshot snapshot,
            string observerPersonId,
            string subjectPersonId,
            string recordId,
            string dimensionId,
            int previousBaseline,
            int newBaseline,
            int previousEffective,
            int newEffective,
            int appliedDelta,
            bool clamped,
            bool sourceAffected,
            bool recordCreated,
            bool duplicate,
            bool preview,
            long beforeRevision,
            long afterRevision)
        {
            Succeeded = succeeded;
            Status = status;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
            ObserverPersonId = observerPersonId ?? string.Empty;
            SubjectPersonId = subjectPersonId ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            PreviousBaselineValue = previousBaseline;
            NewBaselineValue = newBaseline;
            PreviousEffectiveValue = previousEffective;
            NewEffectiveValue = newEffective;
            AppliedDelta = appliedDelta;
            Clamped = clamped;
            SourceContributionAffected = sourceAffected;
            RecordCreated = recordCreated;
            Duplicate = duplicate;
            Preview = preview;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
        }

        public bool Succeeded { get; }
        public AttitudeOperationStatus Status { get; }
        public string Message { get; }
        public InterpersonalAttitudeSnapshot Snapshot { get; }
        public string ObserverPersonId { get; }
        public string SubjectPersonId { get; }
        public string RecordId { get; }
        public string DimensionId { get; }
        public int PreviousBaselineValue { get; }
        public int NewBaselineValue { get; }
        public int PreviousEffectiveValue { get; }
        public int NewEffectiveValue { get; }
        public int AppliedDelta { get; }
        public bool Clamped { get; }
        public bool SourceContributionAffected { get; }
        public bool RecordCreated { get; }
        public bool Duplicate { get; }
        public bool Preview { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }

        public static AttitudeMutationResult Success(
            AttitudeOperationStatus status,
            string message,
            InterpersonalAttitudeSnapshot snapshot,
            string observerPersonId,
            string subjectPersonId,
            string recordId,
            string dimensionId,
            int previousBaseline,
            int newBaseline,
            int previousEffective,
            int newEffective,
            int appliedDelta,
            bool clamped,
            bool sourceAffected,
            bool recordCreated,
            bool duplicate,
            bool preview,
            long beforeRevision,
            long afterRevision)
        {
            return new AttitudeMutationResult(true, status, message, snapshot, observerPersonId, subjectPersonId, recordId, dimensionId, previousBaseline, newBaseline, previousEffective, newEffective, appliedDelta, clamped, sourceAffected, recordCreated, duplicate, preview, beforeRevision, afterRevision);
        }

        public static AttitudeMutationResult Failure(AttitudeOperationStatus status, string message, long revision)
        {
            return new AttitudeMutationResult(false, status, message, null, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, 0, 0, 0, false, false, false, false, false, revision, revision);
        }
    }

    public sealed class AttitudeThresholdRequest
    {
        public string observerPersonId;
        public string subjectPersonId;
        public string dimensionId;
        public AttitudeThresholdComparison comparison;
        public int value;
        public int minimum;
        public int maximum;
    }

    public sealed class AttitudeThresholdResult
    {
        public AttitudeThresholdResult(bool passed, AttitudeOperationStatus status, string message, string observerPersonId, string subjectPersonId, string dimensionId, int actualValue, int requiredValue, int minimum, int maximum, AttitudeThresholdComparison comparison, bool recordExists)
        {
            Passed = passed;
            Status = status;
            Message = message ?? string.Empty;
            ObserverPersonId = observerPersonId ?? string.Empty;
            SubjectPersonId = subjectPersonId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ActualValue = actualValue;
            RequiredValue = requiredValue;
            Minimum = minimum;
            Maximum = maximum;
            Comparison = comparison;
            RecordExists = recordExists;
        }

        public bool Passed { get; }
        public AttitudeOperationStatus Status { get; }
        public string Message { get; }
        public string ObserverPersonId { get; }
        public string SubjectPersonId { get; }
        public string DimensionId { get; }
        public int ActualValue { get; }
        public int RequiredValue { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public AttitudeThresholdComparison Comparison { get; }
        public bool RecordExists { get; }
    }
}
