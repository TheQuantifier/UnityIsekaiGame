using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Social.Relationships
{
    [Serializable]
    public sealed class RelationshipEndpointData
    {
        public string personId;
        public string roleId;

        public RelationshipEndpointData Clone()
        {
            return new RelationshipEndpointData
            {
                personId = personId ?? string.Empty,
                roleId = roleId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class RelationshipRecordData
    {
        public string recordId;
        public string relationshipDefinitionId;
        public RelationshipLifecycleStatus status = RelationshipLifecycleStatus.Active;
        public RelationshipEndpointData[] participants = Array.Empty<RelationshipEndpointData>();
        public double startWorldTime;
        public double endWorldTime = -1d;
        public string sourceEventId;
        public string sourceRecordId;
        public string accessPolicyId;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public RelationshipRecordData Clone()
        {
            return new RelationshipRecordData
            {
                recordId = recordId ?? string.Empty,
                relationshipDefinitionId = relationshipDefinitionId ?? string.Empty,
                status = status,
                participants = participants == null ? Array.Empty<RelationshipEndpointData>() : participants.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                tags = Clean(tags),
                revision = revision
            };
        }

        public static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }

    [Serializable]
    public sealed class RelationshipRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<RelationshipRecordData> records = new List<RelationshipRecordData>();

        public RelationshipRuntimeSaveData Clone()
        {
            return new RelationshipRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                records = records == null ? new List<RelationshipRecordData>() : records.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class RelationshipCreateRequest
    {
        public string recordId;
        public string relationshipDefinitionId;
        public string firstPersonId;
        public string firstRoleId;
        public string secondPersonId;
        public string secondRoleId;
        public double startWorldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string accessPolicyId;
        public string[] tags = Array.Empty<string>();
        public string transactionId;
        public bool preview;
    }

    public sealed class RelationshipEndRequest
    {
        public string recordId;
        public double endWorldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string transactionId;
        public bool preview;
    }

    public sealed class RelationshipSnapshot
    {
        public RelationshipSnapshot(RelationshipRecordData data)
        {
            Data = data?.Clone() ?? new RelationshipRecordData();
        }

        public RelationshipRecordData Data { get; }
        public string RecordId => Data.recordId ?? string.Empty;
        public string RelationshipDefinitionId => Data.relationshipDefinitionId ?? string.Empty;
        public RelationshipLifecycleStatus Status => Data.status;
        public IReadOnlyList<RelationshipEndpointData> Participants => Data.participants ?? Array.Empty<RelationshipEndpointData>();
        public double StartWorldTime => Data.startWorldTime;
        public double EndWorldTime => Data.endWorldTime;
        public string SourceEventId => Data.sourceEventId ?? string.Empty;
        public string SourceRecordId => Data.sourceRecordId ?? string.Empty;
        public string AccessPolicyId => Data.accessPolicyId ?? string.Empty;
        public IReadOnlyList<string> Tags => Data.tags ?? Array.Empty<string>();
        public long Revision => Data.revision;

        public bool IncludesPerson(string personId)
        {
            return !string.IsNullOrWhiteSpace(personId)
                && Participants.Any(item => string.Equals(item.personId, personId, StringComparison.Ordinal));
        }
    }

    public sealed class RelationshipOperationResult
    {
        private RelationshipOperationResult(bool succeeded, RelationshipOperationStatus status, RelationshipSnapshot snapshot, string message, long beforeRevision, long afterRevision, bool preview, bool duplicate)
        {
            Succeeded = succeeded;
            Status = status;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Preview = preview;
            Duplicate = duplicate;
        }

        public bool Succeeded { get; }
        public RelationshipOperationStatus Status { get; }
        public RelationshipSnapshot Snapshot { get; }
        public string Message { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }

        public static RelationshipOperationResult Success(RelationshipSnapshot snapshot, string message, long beforeRevision, long afterRevision, bool preview = false, bool duplicate = false)
        {
            return new RelationshipOperationResult(true, preview ? RelationshipOperationStatus.Preview : duplicate ? RelationshipOperationStatus.Duplicate : RelationshipOperationStatus.Succeeded, snapshot, message, beforeRevision, afterRevision, preview, duplicate);
        }

        public static RelationshipOperationResult Failure(RelationshipOperationStatus status, string message, long beforeRevision)
        {
            return new RelationshipOperationResult(false, status, null, message, beforeRevision, beforeRevision, preview: false, duplicate: false);
        }
    }
}
