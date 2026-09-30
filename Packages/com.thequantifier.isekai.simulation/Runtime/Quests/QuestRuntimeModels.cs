using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Quests
{
    [Serializable]
    public sealed class QuestIssuerReferenceData
    {
        public QuestIssuerType issuerType;
        public string issuerId;
        public string actingPersonId;
        public string displayName;
        public string provenanceId;

        public QuestIssuerReferenceData Clone()
        {
            return new QuestIssuerReferenceData
            {
                issuerType = issuerType,
                issuerId = issuerId ?? string.Empty,
                actingPersonId = actingPersonId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty
            };
        }

        public string StableKey => $"{issuerType}:{issuerId ?? string.Empty}:{actingPersonId ?? string.Empty}";
    }

    [Serializable]
    public sealed class QuestRecipientReferenceData
    {
        public QuestRecipientScope recipientScope = QuestRecipientScope.Open;
        public string recipientId;
        public string displayName;
        public string provenanceId;

        public QuestRecipientReferenceData Clone()
        {
            return new QuestRecipientReferenceData
            {
                recipientScope = recipientScope,
                recipientId = recipientId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty
            };
        }

        public string StableKey => $"{recipientScope}:{recipientId ?? string.Empty}";
    }

    [Serializable]
    public sealed class QuestOriginReferenceData
    {
        public string locationId;
        public string interactionPointId;
        public QuestSourceChannel sourceChannel = QuestSourceChannel.Manual;
        public string sceneBindingKey;
        public string provenanceId;

        public QuestOriginReferenceData Clone()
        {
            return new QuestOriginReferenceData
            {
                locationId = locationId ?? string.Empty,
                interactionPointId = interactionPointId ?? string.Empty,
                sourceChannel = sourceChannel,
                sceneBindingKey = sceneBindingKey ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class QuestSubjectLinkData
    {
        public string linkId;
        public QuestSubjectRole role;
        public InformationSubjectReferenceData subject = new InformationSubjectReferenceData();
        public string provenanceId;

        public QuestSubjectLinkData Clone()
        {
            return new QuestSubjectLinkData
            {
                linkId = linkId ?? string.Empty,
                role = role,
                subject = subject?.Clone() ?? new InformationSubjectReferenceData(),
                provenanceId = provenanceId ?? string.Empty
            };
        }

        public string StableKey => $"{role}:{subject?.subjectType}:{subject?.subjectId}";
    }

    [Serializable]
    public sealed class QuestRecordData
    {
        public string questId;
        public string questDefinitionId;
        public string worldId;
        public string saveSlotId;
        public QuestRuntimeLifecycleState lifecycleState = QuestRuntimeLifecycleState.Available;
        public QuestIssuerReferenceData issuer = new QuestIssuerReferenceData();
        public QuestRecipientReferenceData intendedRecipient = new QuestRecipientReferenceData();
        public QuestOriginReferenceData origin = new QuestOriginReferenceData();
        public QuestSubjectLinkData[] subjectLinks = Array.Empty<QuestSubjectLinkData>();
        public string[] tagIds = Array.Empty<string>();
        public QuestVisibility visibility = QuestVisibility.Public;
        public double createdWorldTime;
        public double retiredWorldTime = -1d;
        public string repeatInstanceKey;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public QuestRecordData Clone()
        {
            return new QuestRecordData
            {
                questId = questId ?? string.Empty,
                questDefinitionId = questDefinitionId ?? string.Empty,
                worldId = worldId ?? string.Empty,
                saveSlotId = saveSlotId ?? string.Empty,
                lifecycleState = lifecycleState,
                issuer = issuer?.Clone() ?? new QuestIssuerReferenceData(),
                intendedRecipient = intendedRecipient?.Clone() ?? new QuestRecipientReferenceData(),
                origin = origin?.Clone() ?? new QuestOriginReferenceData(),
                subjectLinks = (subjectLinks ?? Array.Empty<QuestSubjectLinkData>()).Where(value => value != null).Select(value => value.Clone()).ToArray(),
                tagIds = QuestRuntimeModelUtility.Clean(tagIds),
                visibility = visibility,
                createdWorldTime = createdWorldTime,
                retiredWorldTime = retiredWorldTime,
                repeatInstanceKey = repeatInstanceKey ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class QuestRuntimeEventData
    {
        public string eventId;
        public string transactionId;
        public string questId;
        public QuestRuntimeEventKind eventKind;
        public QuestRuntimeLifecycleState beforeState;
        public QuestRuntimeLifecycleState afterState;
        public double worldTime;
        public string sourceEventId;
        public string provenanceId;
        public long runtimeRevision;

        public QuestRuntimeEventData Clone()
        {
            return new QuestRuntimeEventData
            {
                eventId = eventId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                questId = questId ?? string.Empty,
                eventKind = eventKind,
                beforeState = beforeState,
                afterState = afterState,
                worldTime = worldTime,
                sourceEventId = sourceEventId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class QuestRuntimeTransactionData
    {
        public string transactionId;
        public string operation;
        public string questId;
        public long runtimeRevision;

        public QuestRuntimeTransactionData Clone()
        {
            return new QuestRuntimeTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                questId = questId ?? string.Empty,
                runtimeRevision = runtimeRevision
            };
        }
    }

    [Serializable]
    public sealed class QuestRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<QuestRecordData> quests = new List<QuestRecordData>();
        public List<QuestRuntimeEventData> events = new List<QuestRuntimeEventData>();
        public List<QuestRuntimeTransactionData> transactions = new List<QuestRuntimeTransactionData>();

        public QuestRuntimeSaveData Clone()
        {
            return new QuestRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                quests = (quests ?? new List<QuestRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                events = (events ?? new List<QuestRuntimeEventData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                transactions = (transactions ?? new List<QuestRuntimeTransactionData>()).Where(value => value != null).Select(value => value.Clone()).ToList()
            };
        }
    }

    public sealed class QuestCreateRequest
    {
        public string transactionId;
        public string questId;
        public string questDefinitionId;
        public string saveSlotId;
        public QuestRuntimeLifecycleState initialLifecycleState = QuestRuntimeLifecycleState.Available;
        public QuestIssuerReferenceData issuer;
        public QuestRecipientReferenceData intendedRecipient;
        public QuestOriginReferenceData origin;
        public IEnumerable<QuestSubjectLinkData> subjectLinks;
        public IEnumerable<string> tagIds;
        public QuestVisibility? visibility;
        public double createdWorldTime;
        public string repeatInstanceKey;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestLifecycleTransitionRequest
    {
        public string transactionId;
        public string questId;
        public QuestRuntimeLifecycleState targetState;
        public double worldTime;
        public string sourceEventId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class QuestQuery
    {
        public QuestVisibilityAccess access = QuestVisibilityAccess.PublicOnly;
        public string requesterPersonId;
        public string definitionId;
        public QuestCategory? category;
        public string tagId;
        public string issuerId;
        public string recipientId;
        public string originLocationId;
        public string originInteractionPointId;
        public string subjectId;
        public bool includeRetired;
        public string worldId;
    }

    public sealed class QuestSnapshot
    {
        private readonly QuestRecordData data;

        public QuestSnapshot(QuestRecordData record)
        {
            data = record?.Clone() ?? new QuestRecordData();
        }

        public string QuestId => data.questId ?? string.Empty;
        public string QuestDefinitionId => data.questDefinitionId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public string SaveSlotId => data.saveSlotId ?? string.Empty;
        public QuestRuntimeLifecycleState LifecycleState => data.lifecycleState;
        public QuestVisibility Visibility => data.visibility;
        public QuestIssuerReferenceData Issuer => data.issuer?.Clone() ?? new QuestIssuerReferenceData();
        public QuestRecipientReferenceData IntendedRecipient => data.intendedRecipient?.Clone() ?? new QuestRecipientReferenceData();
        public QuestOriginReferenceData Origin => data.origin?.Clone() ?? new QuestOriginReferenceData();
        public IReadOnlyList<QuestSubjectLinkData> SubjectLinks => (data.subjectLinks ?? Array.Empty<QuestSubjectLinkData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<string> TagIds => QuestRuntimeModelUtility.Clean(data.tagIds);
        public double CreatedWorldTime => data.createdWorldTime;
        public double RetiredWorldTime => data.retiredWorldTime;
        public string RepeatInstanceKey => data.repeatInstanceKey ?? string.Empty;
        public string SourceEventId => data.sourceEventId ?? string.Empty;
        public string SourceRecordId => data.sourceRecordId ?? string.Empty;
        public string ProvenanceId => data.provenanceId ?? string.Empty;
        public long Revision => data.revision;
        public QuestRecordData ToSaveData() => data.Clone();

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return QuestInformationSubject.Quest(QuestId, QuestDefinitionId, IntendedRecipient.recipientScope == QuestRecipientScope.Person ? IntendedRecipient.recipientId : string.Empty, Issuer.issuerId, TagIds);
        }
    }

    public sealed class QuestProjection
    {
        public QuestProjection(QuestSnapshot snapshot, bool redacted, bool concealed)
        {
            Snapshot = snapshot;
            Redacted = redacted;
            Concealed = concealed;
        }

        public QuestSnapshot Snapshot { get; }
        public bool Redacted { get; }
        public bool Concealed { get; }
    }

    public sealed class QuestRuntimeOperationResult
    {
        private QuestRuntimeOperationResult(QuestRuntimeOperationStatus status, string message, QuestSnapshot snapshot, bool preview, bool duplicate, long before, long after)
        {
            Status = status;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
            Preview = preview;
            Duplicate = duplicate;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public QuestRuntimeOperationStatus Status { get; }
        public string Message { get; }
        public QuestSnapshot Snapshot { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Succeeded => Status == QuestRuntimeOperationStatus.Succeeded || Status == QuestRuntimeOperationStatus.Preview || Status == QuestRuntimeOperationStatus.Duplicate;

        public static QuestRuntimeOperationResult Success(QuestSnapshot snapshot, string message, long before, long after, bool preview = false, bool duplicate = false)
        {
            return new QuestRuntimeOperationResult(preview ? QuestRuntimeOperationStatus.Preview : duplicate ? QuestRuntimeOperationStatus.Duplicate : QuestRuntimeOperationStatus.Succeeded, message, snapshot, preview, duplicate, before, after);
        }

        public static QuestRuntimeOperationResult Failure(QuestRuntimeOperationStatus status, string message, long revision)
        {
            return new QuestRuntimeOperationResult(status, message, null, false, false, revision, revision);
        }
    }

    public sealed class QuestRuntimeValidationReport
    {
        public QuestRuntimeValidationReport(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Succeeded => Errors.Count == 0;
        public string Summary => $"Quest validation finished with {Errors.Count} error(s), {Warnings.Count} warning(s).";
    }

    public static class QuestRuntimeModelUtility
    {
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
}
