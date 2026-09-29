using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Sources;

namespace UnityIsekaiGame.Knowledge.Records
{
    [Serializable]
    public sealed class KnowledgeRecordDetailData
    {
        public string detailId;
        public string labelKey;
        public string value;
        public KnowledgeValueType valueType;
        public bool uncertain;
        public bool disputed;
        public string sourceId;
        public string evidenceId;

        public KnowledgeRecordDetailData Clone()
        {
            return new KnowledgeRecordDetailData
            {
                detailId = detailId ?? string.Empty,
                labelKey = labelKey ?? string.Empty,
                value = value ?? string.Empty,
                valueType = valueType,
                uncertain = uncertain,
                disputed = disputed,
                sourceId = sourceId ?? string.Empty,
                evidenceId = evidenceId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class KnowledgeRecordData
    {
        public string recordId;
        public string definitionId;
        public KnowledgeRecordCategory category;
        public KnowledgeRecordOwnerKind ownerKind;
        public string ownerId;
        public string controllingEntityId;
        public InformationSubjectReferenceData subject = new InformationSubjectReferenceData();
        public string authorPersonId;
        public string creatorSystemId;
        public double createdWorldTime;
        public double updatedWorldTime;
        public double occurredStartWorldTime;
        public double occurredEndWorldTime = -1d;
        public KnowledgeRecordProjectionContextKind preservedProjectionContext;
        public string knowledgeOwnerPersonId;
        public string[] sourceIds = Array.Empty<string>();
        public string[] evidenceIds = Array.Empty<string>();
        public string[] factDefinitionIds = Array.Empty<string>();
        public string[] propositionIds = Array.Empty<string>();
        public string[] beliefIds = Array.Empty<string>();
        public string[] memoryIds = Array.Empty<string>();
        public string[] historicalEventIds = Array.Empty<string>();
        public string[] lifeEventIds = Array.Empty<string>();
        public string[] transferIds = Array.Empty<string>();
        public string[] relatedRecordIds = Array.Empty<string>();
        public string parentRecordId;
        public string supersedesRecordId;
        public string correctedByRecordId;
        public KnowledgeRecordStatus status = KnowledgeRecordStatus.Active;
        public KnowledgeRecordCompleteness completeness = KnowledgeRecordCompleteness.Partial;
        public int confidence;
        public int reliability;
        public string accessPolicyId;
        public InformationVisibilityClassification classification = InformationVisibilityClassification.Public;
        public string[] tags = Array.Empty<string>();
        public KnowledgeRecordDetailData[] details = Array.Empty<KnowledgeRecordDetailData>();
        public string orderingToken;
        public long revision = 1L;

        public KnowledgeRecordData Clone()
        {
            return new KnowledgeRecordData
            {
                recordId = recordId ?? string.Empty,
                definitionId = definitionId ?? string.Empty,
                category = category,
                ownerKind = ownerKind,
                ownerId = ownerId ?? string.Empty,
                controllingEntityId = controllingEntityId ?? string.Empty,
                subject = subject?.Clone() ?? new InformationSubjectReferenceData(),
                authorPersonId = authorPersonId ?? string.Empty,
                creatorSystemId = creatorSystemId ?? string.Empty,
                createdWorldTime = Math.Max(0d, createdWorldTime),
                updatedWorldTime = Math.Max(0d, updatedWorldTime),
                occurredStartWorldTime = Math.Max(0d, occurredStartWorldTime),
                occurredEndWorldTime = occurredEndWorldTime,
                preservedProjectionContext = preservedProjectionContext,
                knowledgeOwnerPersonId = knowledgeOwnerPersonId ?? string.Empty,
                sourceIds = CloneArray(sourceIds),
                evidenceIds = CloneArray(evidenceIds),
                factDefinitionIds = CloneArray(factDefinitionIds),
                propositionIds = CloneArray(propositionIds),
                beliefIds = CloneArray(beliefIds),
                memoryIds = CloneArray(memoryIds),
                historicalEventIds = CloneArray(historicalEventIds),
                lifeEventIds = CloneArray(lifeEventIds),
                transferIds = CloneArray(transferIds),
                relatedRecordIds = CloneArray(relatedRecordIds),
                parentRecordId = parentRecordId ?? string.Empty,
                supersedesRecordId = supersedesRecordId ?? string.Empty,
                correctedByRecordId = correctedByRecordId ?? string.Empty,
                status = status,
                completeness = completeness,
                confidence = ClampScore(confidence),
                reliability = ClampScore(reliability),
                accessPolicyId = accessPolicyId ?? string.Empty,
                classification = classification,
                tags = CloneArray(tags),
                details = details == null ? Array.Empty<KnowledgeRecordDetailData>() : details.Select(detail => detail?.Clone()).Where(detail => detail != null).ToArray(),
                orderingToken = orderingToken ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }

        public static string[] CloneArray(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public static int ClampScore(int value)
        {
            return Math.Max(0, Math.Min(1000, value));
        }
    }

    [Serializable]
    public sealed class KnowledgeRecordCollectionData
    {
        public string collectionId;
        public string ownerId;
        public KnowledgeRecordCategory category = KnowledgeRecordCategory.Collection;
        public string displayName;
        public string[] recordIds = Array.Empty<string>();
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public KnowledgeRecordCollectionData Clone()
        {
            return new KnowledgeRecordCollectionData
            {
                collectionId = collectionId ?? string.Empty,
                ownerId = ownerId ?? string.Empty,
                category = category,
                displayName = displayName ?? string.Empty,
                recordIds = KnowledgeRecordData.CloneArray(recordIds),
                tags = KnowledgeRecordData.CloneArray(tags),
                revision = Math.Max(1L, revision)
            };
        }
    }

    public sealed class KnowledgeRecord
    {
        public KnowledgeRecord(KnowledgeRecordData data)
        {
            Data = data?.Clone() ?? new KnowledgeRecordData();
        }

        public KnowledgeRecordData Data { get; }
        public string RecordId => Data.recordId ?? string.Empty;
        public string DefinitionId => Data.definitionId ?? string.Empty;
        public KnowledgeRecordCategory Category => Data.category;
        public KnowledgeRecordOwnerKind OwnerKind => Data.ownerKind;
        public string OwnerId => Data.ownerId ?? string.Empty;
        public InformationSubjectReference Subject => new InformationSubjectReference(Data.subject);
        public string AuthorPersonId => Data.authorPersonId ?? string.Empty;
        public KnowledgeRecordStatus Status => Data.status;
        public InformationVisibilityClassification Classification => Data.classification;
        public IReadOnlyList<KnowledgeRecordDetailData> Details => Array.AsReadOnly((Data.details ?? Array.Empty<KnowledgeRecordDetailData>()).Select(detail => detail.Clone()).ToArray());
        public IReadOnlyList<string> Tags => Array.AsReadOnly(KnowledgeRecordData.CloneArray(Data.tags));
        public long Revision => Data.revision;
    }

    public sealed class KnowledgeRecordCollection
    {
        public KnowledgeRecordCollection(KnowledgeRecordCollectionData data)
        {
            Data = data?.Clone() ?? new KnowledgeRecordCollectionData();
        }

        public KnowledgeRecordCollectionData Data { get; }
        public string CollectionId => Data.collectionId ?? string.Empty;
        public IReadOnlyList<string> RecordIds => Array.AsReadOnly(KnowledgeRecordData.CloneArray(Data.recordIds));
    }

    public sealed class KnowledgeRecordProjectionContext
    {
        public string RequesterPersonId { get; set; }
        public KnowledgeRecordProjectionContextKind ContextKind { get; set; } = KnowledgeRecordProjectionContextKind.Public;
        public InformationAccessContext AccessContext { get; set; }
        public bool IncludeRedactedDetails { get; set; } = true;
        public bool Privileged => ContextKind == KnowledgeRecordProjectionContextKind.AuthoritativeDebug || ContextKind == KnowledgeRecordProjectionContextKind.Privileged || AccessContext?.IsPrivileged == true;
    }

    public sealed class KnowledgeRecordProjection
    {
        public KnowledgeRecordProjection(
            KnowledgeRecord record,
            InformationAccessDecision decision,
            IReadOnlyDictionary<string, InformationRedactionState> detailStates,
            IReadOnlyList<KnowledgeRecordDetailData> visibleDetails,
            string visibleRecordId,
            string message)
        {
            Record = record;
            Decision = decision;
            DetailStates = new ReadOnlyDictionary<string, InformationRedactionState>(new Dictionary<string, InformationRedactionState>(detailStates ?? new Dictionary<string, InformationRedactionState>(), StringComparer.Ordinal));
            VisibleDetails = Array.AsReadOnly((visibleDetails ?? Array.Empty<KnowledgeRecordDetailData>()).Select(detail => detail.Clone()).ToArray());
            VisibleRecordId = visibleRecordId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public KnowledgeRecord Record { get; }
        public InformationAccessDecision Decision { get; }
        public IReadOnlyDictionary<string, InformationRedactionState> DetailStates { get; }
        public IReadOnlyList<KnowledgeRecordDetailData> VisibleDetails { get; }
        public string VisibleRecordId { get; }
        public string Message { get; }
        public bool Succeeded => Record != null && (Decision == null || !Decision.Denied);
        public bool Denied => Record == null || Decision?.Denied == true;
        public bool Redacted => Decision != null && (Decision.RedactedAccess || Decision.PartialAccess || DetailStates.Any(pair => pair.Value != InformationRedactionState.Visible));
    }

    public sealed class KnowledgeRecordSnapshot
    {
        public KnowledgeRecordSnapshot(string ownerId, long revision, IReadOnlyList<KnowledgeRecord> records, IReadOnlyList<KnowledgeRecordCollection> collections)
        {
            OwnerId = ownerId ?? string.Empty;
            Revision = revision;
            Records = Array.AsReadOnly((records ?? Array.Empty<KnowledgeRecord>()).ToArray());
            Collections = Array.AsReadOnly((collections ?? Array.Empty<KnowledgeRecordCollection>()).ToArray());
        }

        public string OwnerId { get; }
        public long Revision { get; }
        public IReadOnlyList<KnowledgeRecord> Records { get; }
        public IReadOnlyList<KnowledgeRecordCollection> Collections { get; }
    }

    public sealed class KnowledgeRecordCreateRequest
    {
        public string TransactionId { get; set; }
        public string RecordId { get; set; }
        public string DefinitionId { get; set; }
        public KnowledgeRecordCategory Category { get; set; }
        public KnowledgeRecordOwnerKind OwnerKind { get; set; }
        public string OwnerId { get; set; }
        public InformationSubjectReferenceData Subject { get; set; }
        public string AuthorPersonId { get; set; }
        public double WorldTimeSeconds { get; set; }
        public double OccurredWorldTimeSeconds { get; set; }
        public KnowledgeRecordProjectionContextKind ProjectionContext { get; set; } = KnowledgeRecordProjectionContextKind.PersonRecorded;
        public string KnowledgeOwnerPersonId { get; set; }
        public string[] SourceIds { get; set; } = Array.Empty<string>();
        public string[] EvidenceIds { get; set; } = Array.Empty<string>();
        public string[] MemoryIds { get; set; } = Array.Empty<string>();
        public string[] HistoricalEventIds { get; set; } = Array.Empty<string>();
        public string[] LifeEventIds { get; set; } = Array.Empty<string>();
        public string[] TransferIds { get; set; } = Array.Empty<string>();
        public string[] RelatedRecordIds { get; set; } = Array.Empty<string>();
        public KnowledgeRecordStatus Status { get; set; } = KnowledgeRecordStatus.Active;
        public KnowledgeRecordCompleteness Completeness { get; set; } = KnowledgeRecordCompleteness.Partial;
        public int Confidence { get; set; } = 500;
        public int Reliability { get; set; } = 500;
        public string AccessPolicyId { get; set; }
        public InformationVisibilityClassification Classification { get; set; } = InformationVisibilityClassification.Public;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public KnowledgeRecordDetailData[] Details { get; set; } = Array.Empty<KnowledgeRecordDetailData>();
        public bool Preview { get; set; }
    }

    public sealed class KnowledgeRecordSearchQuery
    {
        public KnowledgeRecordCategory? Category { get; set; }
        public KnowledgeRecordStatus? Status { get; set; }
        public KnowledgeRecordOwnerKind? OwnerKind { get; set; }
        public string OwnerId { get; set; }
        public InformationSubjectType? SubjectType { get; set; }
        public string SubjectId { get; set; }
        public InformationVisibilityClassification? Classification { get; set; }
        public string Tag { get; set; }
        public bool IncludeCorrected { get; set; } = true;
        public bool IncludeArchived { get; set; } = true;
        public int Offset { get; set; }
        public int Limit { get; set; } = 100;
    }

    public sealed class KnowledgeRecordReadRequest
    {
        public string TransactionId { get; set; }
        public string RecordId { get; set; }
        public string ReaderPersonId { get; set; }
        public KnowledgeRecordProjectionContext ProjectionContext { get; set; }
        public double WorldTimeSeconds { get; set; }
        public bool Preview { get; set; }
        public bool PrivilegedInspection { get; set; }
        public bool CreateInformationSource { get; set; } = true;
        public bool CreateKnowledgeEvidence { get; set; } = true;
        public bool CreateMemory { get; set; } = true;
        public bool RequireEvidenceProposition { get; set; }
        public string InformationSourceDefinitionId { get; set; }
        public string SourceInstanceId { get; set; }
        public string MemoryId { get; set; }
        public KnowledgePropositionData Proposition { get; set; }
        public int EvidenceStrength { get; set; } = KnowledgeConfidence.DefaultObservation;
        public int EvidenceCredibility { get; set; } = KnowledgeConfidence.DefaultObservation;
        public KnowledgeVisibility EvidenceVisibility { get; set; } = KnowledgeVisibility.Public;
    }

    public sealed class KnowledgeRecordReadResult
    {
        private KnowledgeRecordReadResult(
            bool succeeded,
            KnowledgeRecordResultCode code,
            string message,
            string transactionId,
            bool preview,
            bool duplicate,
            KnowledgeRecordProjection projection,
            InformationSourceOperationResult sourceResult,
            KnowledgeOperationResult knowledgeResult,
            HistoryOperationResult memoryResult,
            string sourceInstanceId,
            string evidenceId,
            string beliefId,
            string memoryId)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            Projection = projection;
            SourceResult = sourceResult;
            KnowledgeResult = knowledgeResult;
            MemoryResult = memoryResult;
            SourceInstanceId = sourceInstanceId ?? string.Empty;
            EvidenceId = evidenceId ?? string.Empty;
            BeliefId = beliefId ?? string.Empty;
            MemoryId = memoryId ?? string.Empty;
        }

        public bool Succeeded { get; }
        public KnowledgeRecordResultCode Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public KnowledgeRecordProjection Projection { get; }
        public InformationSourceOperationResult SourceResult { get; }
        public KnowledgeOperationResult KnowledgeResult { get; }
        public HistoryOperationResult MemoryResult { get; }
        public string SourceInstanceId { get; }
        public string EvidenceId { get; }
        public string BeliefId { get; }
        public string MemoryId { get; }

        public static KnowledgeRecordReadResult Success(
            string message,
            string transactionId,
            KnowledgeRecordProjection projection,
            InformationSourceOperationResult sourceResult,
            KnowledgeOperationResult knowledgeResult,
            HistoryOperationResult memoryResult,
            bool preview = false,
            bool duplicate = false)
        {
            return new KnowledgeRecordReadResult(
                true,
                preview ? KnowledgeRecordResultCode.Preview : duplicate ? KnowledgeRecordResultCode.Duplicate : KnowledgeRecordResultCode.Success,
                message,
                transactionId,
                preview,
                duplicate,
                projection,
                sourceResult,
                knowledgeResult,
                memoryResult,
                sourceResult?.Source?.SourceInstanceId,
                knowledgeResult?.Evidence?.EvidenceId,
                knowledgeResult?.ResultingBelief?.BeliefId,
                memoryResult?.Memory?.MemoryId);
        }

        public static KnowledgeRecordReadResult Failure(KnowledgeRecordResultCode code, string message, string transactionId = "", KnowledgeRecordProjection projection = null)
        {
            return new KnowledgeRecordReadResult(false, code, message, transactionId, false, false, projection, null, null, null, string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }

    public sealed class KnowledgeRecordOperationResult
    {
        private KnowledgeRecordOperationResult(bool succeeded, KnowledgeRecordResultCode code, string message, string transactionId, bool preview, bool duplicate, long priorRevision, long resultingRevision, KnowledgeRecord record = null, KnowledgeRecordProjection projection = null)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
            Record = record;
            Projection = projection;
        }

        public bool Succeeded { get; }
        public KnowledgeRecordResultCode Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public KnowledgeRecord Record { get; }
        public KnowledgeRecordProjection Projection { get; }

        public static KnowledgeRecordOperationResult Success(string message, string transactionId, long priorRevision, long resultingRevision, KnowledgeRecord record = null, KnowledgeRecordProjection projection = null, bool preview = false, bool duplicate = false)
        {
            return new KnowledgeRecordOperationResult(true, preview ? KnowledgeRecordResultCode.Preview : duplicate ? KnowledgeRecordResultCode.Duplicate : KnowledgeRecordResultCode.Success, message, transactionId, preview, duplicate, priorRevision, resultingRevision, record, projection);
        }

        public static KnowledgeRecordOperationResult Failure(KnowledgeRecordResultCode code, string message, string transactionId = "", bool preview = false, long revision = 0)
        {
            return new KnowledgeRecordOperationResult(false, code, message, transactionId, preview, false, revision, revision);
        }
    }

    [Serializable]
    public sealed class KnowledgeRecordProcessedTransactionData
    {
        public string transactionId;
        public string operation;
        public string recordId;
        public long revision;
    }

    [Serializable]
    public sealed class KnowledgeRecordSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string ownerId;
        public long recordRevision;
        public KnowledgeRecordData[] records = Array.Empty<KnowledgeRecordData>();
        public KnowledgeRecordCollectionData[] collections = Array.Empty<KnowledgeRecordCollectionData>();
        public KnowledgeRecordProcessedTransactionData[] processedTransactions = Array.Empty<KnowledgeRecordProcessedTransactionData>();
    }
}
