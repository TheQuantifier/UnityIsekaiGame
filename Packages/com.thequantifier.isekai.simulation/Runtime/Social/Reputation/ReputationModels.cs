using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Social.Reputation
{
    [Serializable]
    public sealed class ReputationContributionData
    {
        public string sourceId;
        public ReputationContributionSourceCategory sourceCategory;
        public ReputationAuthenticity authenticity;
        public string dimensionId;
        public int amount;
        public double worldTime;
        public string historicalEventId;
        public string supportingReferenceId;

        public ReputationContributionData Clone()
        {
            return new ReputationContributionData
            {
                sourceId = sourceId ?? string.Empty,
                sourceCategory = sourceCategory,
                authenticity = authenticity,
                dimensionId = dimensionId ?? string.Empty,
                amount = amount,
                worldTime = worldTime,
                historicalEventId = historicalEventId ?? string.Empty,
                supportingReferenceId = supportingReferenceId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ReputationDimensionValueData
    {
        public string dimensionId;
        public bool hasBaseline;
        public int baselineValue;
        public List<ReputationContributionData> contributions = new List<ReputationContributionData>();

        public ReputationDimensionValueData Clone()
        {
            return new ReputationDimensionValueData
            {
                dimensionId = dimensionId ?? string.Empty,
                hasBaseline = hasBaseline,
                baselineValue = baselineValue,
                contributions = contributions == null ? new List<ReputationContributionData>() : contributions.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    [Serializable]
    public sealed class ReputationRecordData
    {
        public string recordId;
        public string subjectPersonId;
        public string audienceId;
        public ReputationLifecycleState lifecycleState = ReputationLifecycleState.Active;
        public List<ReputationDimensionValueData> dimensions = new List<ReputationDimensionValueData>();
        public double createdWorldTime;
        public double lastModifiedWorldTime;
        public long revision = 1L;

        public ReputationRecordData Clone()
        {
            return new ReputationRecordData
            {
                recordId = recordId ?? string.Empty,
                subjectPersonId = subjectPersonId ?? string.Empty,
                audienceId = audienceId ?? string.Empty,
                lifecycleState = lifecycleState,
                dimensions = dimensions == null ? new List<ReputationDimensionValueData>() : dimensions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                createdWorldTime = createdWorldTime,
                lastModifiedWorldTime = lastModifiedWorldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ReputationRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<ReputationRecordData> records = new List<ReputationRecordData>();
        public List<string> processedTransactionIds = new List<string>();

        public ReputationRuntimeSaveData Clone()
        {
            return new ReputationRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                records = records == null ? new List<ReputationRecordData>() : records.Select(item => item?.Clone()).Where(item => item != null).ToList(),
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

    public sealed class ReputationMutationRequest
    {
        public string transactionId;
        public string recordId;
        public string subjectPersonId;
        public string audienceId;
        public string dimensionId;
        public ReputationMutationKind mutationKind;
        public int value;
        public int delta;
        public string sourceId;
        public ReputationContributionSourceCategory sourceCategory = ReputationContributionSourceCategory.Unknown;
        public ReputationAuthenticity authenticity = ReputationAuthenticity.Unknown;
        public string historicalEventId;
        public string supportingReferenceId;
        public double worldTime;
        public bool preview;
    }

    public sealed class ReputationContributionSnapshot
    {
        public ReputationContributionSnapshot(ReputationContributionData data)
        {
            Data = data?.Clone() ?? new ReputationContributionData();
        }

        public ReputationContributionData Data { get; }
        public string SourceId => Data.sourceId ?? string.Empty;
        public ReputationContributionSourceCategory SourceCategory => Data.sourceCategory;
        public ReputationAuthenticity Authenticity => Data.authenticity;
        public string DimensionId => Data.dimensionId ?? string.Empty;
        public int Amount => Data.amount;
        public double WorldTime => Data.worldTime;
        public string HistoricalEventId => Data.historicalEventId ?? string.Empty;
        public string SupportingReferenceId => Data.supportingReferenceId ?? string.Empty;
    }

    public sealed class ReputationEffectiveValueSnapshot
    {
        public ReputationEffectiveValueSnapshot(string dimensionId, int baselineValue, bool hasBaseline, IReadOnlyList<ReputationContributionSnapshot> contributions, int rawValue, int effectiveValue, bool clamped, bool recordExists, bool inherited, string sourceAudienceId)
        {
            DimensionId = dimensionId ?? string.Empty;
            BaselineValue = baselineValue;
            HasBaseline = hasBaseline;
            Contributions = (contributions ?? Array.Empty<ReputationContributionSnapshot>()).ToArray();
            RawValue = rawValue;
            EffectiveValue = effectiveValue;
            Clamped = clamped;
            RecordExists = recordExists;
            Inherited = inherited;
            SourceAudienceId = sourceAudienceId ?? string.Empty;
        }

        public string DimensionId { get; }
        public int BaselineValue { get; }
        public bool HasBaseline { get; }
        public IReadOnlyList<ReputationContributionSnapshot> Contributions { get; }
        public int RawValue { get; }
        public int EffectiveValue { get; }
        public bool Clamped { get; }
        public bool RecordExists { get; }
        public bool Inherited { get; }
        public string SourceAudienceId { get; }
        public bool IsNeutralDefault => !RecordExists || (!HasBaseline && Contributions.Count == 0);
    }

    public sealed class ReputationSnapshot
    {
        public ReputationSnapshot(ReputationRecordData data)
        {
            Data = data?.Clone() ?? new ReputationRecordData();
        }

        public ReputationRecordData Data { get; }
        public string RecordId => Data.recordId ?? string.Empty;
        public string SubjectPersonId => Data.subjectPersonId ?? string.Empty;
        public string AudienceId => Data.audienceId ?? string.Empty;
        public ReputationLifecycleState LifecycleState => Data.lifecycleState;
        public IReadOnlyList<ReputationDimensionValueData> Dimensions => Data.dimensions ?? new List<ReputationDimensionValueData>();
        public double CreatedWorldTime => Data.createdWorldTime;
        public double LastModifiedWorldTime => Data.lastModifiedWorldTime;
        public long Revision => Data.revision;
    }

    public sealed class ReputationMutationResult
    {
        private ReputationMutationResult(
            bool succeeded,
            ReputationOperationStatus status,
            string message,
            ReputationSnapshot snapshot,
            string subjectPersonId,
            string audienceId,
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
            SubjectPersonId = subjectPersonId ?? string.Empty;
            AudienceId = audienceId ?? string.Empty;
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
        public ReputationOperationStatus Status { get; }
        public string Message { get; }
        public ReputationSnapshot Snapshot { get; }
        public string SubjectPersonId { get; }
        public string AudienceId { get; }
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

        public static ReputationMutationResult Success(
            ReputationOperationStatus status,
            string message,
            ReputationSnapshot snapshot,
            string subjectPersonId,
            string audienceId,
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
            return new ReputationMutationResult(true, status, message, snapshot, subjectPersonId, audienceId, recordId, dimensionId, previousBaseline, newBaseline, previousEffective, newEffective, appliedDelta, clamped, sourceAffected, recordCreated, duplicate, preview, beforeRevision, afterRevision);
        }

        public static ReputationMutationResult Failure(ReputationOperationStatus status, string message, long revision)
        {
            return new ReputationMutationResult(false, status, message, null, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, 0, 0, 0, false, false, false, false, false, revision, revision);
        }
    }

    public sealed class ReputationThresholdRequest
    {
        public string subjectPersonId;
        public string audienceId;
        public string dimensionId;
        public ReputationThresholdComparison comparison;
        public int value;
        public int minimum;
        public int maximum;
        public bool allowInherited;
        public int minimumRenown;
    }

    public sealed class ReputationThresholdResult
    {
        public ReputationThresholdResult(bool passed, ReputationOperationStatus status, string message, string subjectPersonId, string audienceId, string dimensionId, int actualValue, int requiredValue, int minimum, int maximum, ReputationThresholdComparison comparison, bool recordExists, bool inherited, int renownValue)
        {
            Passed = passed;
            Status = status;
            Message = message ?? string.Empty;
            SubjectPersonId = subjectPersonId ?? string.Empty;
            AudienceId = audienceId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ActualValue = actualValue;
            RequiredValue = requiredValue;
            Minimum = minimum;
            Maximum = maximum;
            Comparison = comparison;
            RecordExists = recordExists;
            Inherited = inherited;
            RenownValue = renownValue;
        }

        public bool Passed { get; }
        public ReputationOperationStatus Status { get; }
        public string Message { get; }
        public string SubjectPersonId { get; }
        public string AudienceId { get; }
        public string DimensionId { get; }
        public int ActualValue { get; }
        public int RequiredValue { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public ReputationThresholdComparison Comparison { get; }
        public bool RecordExists { get; }
        public bool Inherited { get; }
        public int RenownValue { get; }
    }
}
