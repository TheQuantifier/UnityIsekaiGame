using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Sources;

namespace UnityIsekaiGame.Knowledge.Sharing
{
    [Serializable]
    public sealed class TransferContentItemData
    {
        public string contentItemId;
        public InformationTransferContentType contentType;
        public KnowledgeDomain domain;
        public KnowledgePropositionData proposition;
        public string senderEvidenceId;
        public string senderBeliefId;
        public string senderMemoryId;
        public string historicalEventId;
        public string lifeEventId;
        public string immediateSourceId;
        public string originalSourceId;
        public int senderConfidence;
        public KnowledgeBeliefState senderBeliefState;
        public string[] includedDetailIds;
        public string[] omittedDetailIds;
        public int claimedCertainty;
        public string claimedSourceId;
        public string actualKnownSourceId;
        public KnowledgeVisibility privacyClassification;
        public string requiredRecipientAccessId;
        public InformationTransferAssertionType assertionType;
        public string typedPayloadId;
        public string debugDescription;
        public bool deliberateFalsehood;
        public bool deliberateOmission;
        public bool deliberateDistortion;
        public TransferUnderstandingState intendedUnderstanding = TransferUnderstandingState.Complete;
        public int rawEvidenceStrength;

        public TransferContentItemData Clone()
        {
            return new TransferContentItemData
            {
                contentItemId = contentItemId,
                contentType = contentType,
                domain = domain,
                proposition = proposition?.Clone(),
                senderEvidenceId = senderEvidenceId,
                senderBeliefId = senderBeliefId,
                senderMemoryId = senderMemoryId,
                historicalEventId = historicalEventId,
                lifeEventId = lifeEventId,
                immediateSourceId = immediateSourceId,
                originalSourceId = originalSourceId,
                senderConfidence = KnowledgeConfidence.Clamp(senderConfidence),
                senderBeliefState = senderBeliefState,
                includedDetailIds = includedDetailIds == null ? Array.Empty<string>() : includedDetailIds.ToArray(),
                omittedDetailIds = omittedDetailIds == null ? Array.Empty<string>() : omittedDetailIds.ToArray(),
                claimedCertainty = KnowledgeConfidence.Clamp(claimedCertainty),
                claimedSourceId = claimedSourceId,
                actualKnownSourceId = actualKnownSourceId,
                privacyClassification = privacyClassification,
                requiredRecipientAccessId = requiredRecipientAccessId,
                assertionType = assertionType,
                typedPayloadId = typedPayloadId,
                debugDescription = debugDescription,
                deliberateFalsehood = deliberateFalsehood,
                deliberateOmission = deliberateOmission,
                deliberateDistortion = deliberateDistortion,
                intendedUnderstanding = intendedUnderstanding,
                rawEvidenceStrength = KnowledgeConfidence.Clamp(rawEvidenceStrength)
            };
        }
    }

    [Serializable]
    public sealed class TransferRecipientResultData
    {
        public string recipientPersonId;
        public InformationTransferStatus status;
        public TransferUnderstandingState understanding;
        public int inheritedConfidence;
        public int rawEvidenceStrength;
        public int effectiveEvidenceStrength;
        public string reliabilityPolicyId;
        public string reliabilityEvaluationId;
        public string transferSourceId;
        public string immediateSourceId;
        public string originalSourceId;
        public string[] deliveredContentItemIds;
        public string[] omittedContentItemIds;
        public string[] misunderstoodContentItemIds;
        public string[] rejectedContentItemIds;
        public string[] createdEvidenceIds;
        public string[] resultingBeliefIds;
        public string[] formedMemoryIds;
        public bool persistenceStateChanged;
        public string message;

        public TransferRecipientResultData Clone()
        {
            return new TransferRecipientResultData
            {
                recipientPersonId = recipientPersonId,
                status = status,
                understanding = understanding,
                inheritedConfidence = KnowledgeConfidence.Clamp(inheritedConfidence),
                rawEvidenceStrength = KnowledgeConfidence.Clamp(rawEvidenceStrength),
                effectiveEvidenceStrength = KnowledgeConfidence.Clamp(effectiveEvidenceStrength),
                reliabilityPolicyId = reliabilityPolicyId,
                reliabilityEvaluationId = reliabilityEvaluationId,
                transferSourceId = transferSourceId,
                immediateSourceId = immediateSourceId,
                originalSourceId = originalSourceId,
                deliveredContentItemIds = deliveredContentItemIds == null ? Array.Empty<string>() : deliveredContentItemIds.ToArray(),
                omittedContentItemIds = omittedContentItemIds == null ? Array.Empty<string>() : omittedContentItemIds.ToArray(),
                misunderstoodContentItemIds = misunderstoodContentItemIds == null ? Array.Empty<string>() : misunderstoodContentItemIds.ToArray(),
                rejectedContentItemIds = rejectedContentItemIds == null ? Array.Empty<string>() : rejectedContentItemIds.ToArray(),
                createdEvidenceIds = createdEvidenceIds == null ? Array.Empty<string>() : createdEvidenceIds.ToArray(),
                resultingBeliefIds = resultingBeliefIds == null ? Array.Empty<string>() : resultingBeliefIds.ToArray(),
                formedMemoryIds = formedMemoryIds == null ? Array.Empty<string>() : formedMemoryIds.ToArray(),
                persistenceStateChanged = persistenceStateChanged,
                message = message
            };
        }
    }

    [Serializable]
    public sealed class InformationTransferRecordData
    {
        public string transferId;
        public string transactionId;
        public string senderPersonId;
        public string[] recipientPersonIds;
        public string transferDefinitionId;
        public InformationTransferMode mode;
        public double worldTimeSeconds;
        public string locationContextId;
        public TransferPrivacyScope privacyScope;
        public bool recallRequired;
        public bool summarizationRequested;
        public bool translationRequested;
        public bool omissionRequested;
        public bool distortionRequested;
        public bool teachingRequested;
        public bool recipientAcknowledgmentRequired;
        public string deterministicPolicyId;
        public string parentTransferId;
        public string correctionOfTransferId;
        public string retractionOfTransferId;
        public string immediateSourceId;
        public string originalSourceId;
        public string createdSourceId;
        public TransferContentItemData[] contentItems;
        public TransferRecipientResultData[] recipientResults;
        public string senderRecallOutcome;
        public string[] validationFailures;
        public long revision;

        public InformationTransferRecordData Clone()
        {
            return new InformationTransferRecordData
            {
                transferId = transferId,
                transactionId = transactionId,
                senderPersonId = senderPersonId,
                recipientPersonIds = recipientPersonIds == null ? Array.Empty<string>() : recipientPersonIds.ToArray(),
                transferDefinitionId = transferDefinitionId,
                mode = mode,
                worldTimeSeconds = worldTimeSeconds,
                locationContextId = locationContextId,
                privacyScope = privacyScope,
                recallRequired = recallRequired,
                summarizationRequested = summarizationRequested,
                translationRequested = translationRequested,
                omissionRequested = omissionRequested,
                distortionRequested = distortionRequested,
                teachingRequested = teachingRequested,
                recipientAcknowledgmentRequired = recipientAcknowledgmentRequired,
                deterministicPolicyId = deterministicPolicyId,
                parentTransferId = parentTransferId,
                correctionOfTransferId = correctionOfTransferId,
                retractionOfTransferId = retractionOfTransferId,
                immediateSourceId = immediateSourceId,
                originalSourceId = originalSourceId,
                createdSourceId = createdSourceId,
                contentItems = contentItems == null ? Array.Empty<TransferContentItemData>() : contentItems.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                recipientResults = recipientResults == null ? Array.Empty<TransferRecipientResultData>() : recipientResults.Select(result => result?.Clone()).Where(result => result != null).ToArray(),
                senderRecallOutcome = senderRecallOutcome,
                validationFailures = validationFailures == null ? Array.Empty<string>() : validationFailures.ToArray(),
                revision = Math.Max(0L, revision)
            };
        }
    }

    [Serializable]
    public sealed class InformationTransferProcessedTransactionData
    {
        public string transactionId;
        public InformationTransferStatus status;
        public string transferId;
        public long revision;
    }

    [Serializable]
    public sealed class InformationTransferSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string ownerId;
        public long transferRevision;
        public InformationTransferRecordData[] transfers;
        public InformationTransferProcessedTransactionData[] processedTransactions;
    }

    public sealed class InformationTransferRequest
    {
        public string TransactionId { get; set; }
        public string TransferId { get; set; }
        public string SenderPersonId { get; set; }
        public string[] RecipientPersonIds { get; set; }
        public string TransferDefinitionId { get; set; }
        public InformationTransferMode Mode { get; set; } = InformationTransferMode.DirectTestimony;
        public TransferContentItemData[] ContentItems { get; set; }
        public string ImmediateSourceId { get; set; }
        public string OriginalSourceId { get; set; }
        public string CreatedSourceId { get; set; }
        public double WorldTimeSeconds { get; set; }
        public string LocationContextId { get; set; }
        public TransferPrivacyScope PrivacyScope { get; set; } = TransferPrivacyScope.RecipientOnly;
        public bool SenderRecallRequired { get; set; }
        public bool SummarizationRequested { get; set; }
        public bool TranslationRequested { get; set; }
        public bool OmissionRequested { get; set; }
        public bool DistortionRequested { get; set; }
        public bool TeachingRequested { get; set; }
        public bool RecipientAcknowledgmentRequired { get; set; }
        public string DeterministicPolicyId { get; set; } = "information-transfer.policy.prototype.default";
        public string ParentTransferId { get; set; }
        public string CorrectionOfTransferId { get; set; }
        public string RetractionOfTransferId { get; set; }
        public bool DeliberateFalsehoodAuthorized { get; set; }
        public bool PrivilegedAccess { get; set; }
        public InformationAccessRuntime AccessRuntime { get; set; }
        public string AccessPolicyId { get; set; }
        public InformationSubjectReferenceData AccessSubject { get; set; }
        public string[] SenderAuthorizationIds { get; set; } = Array.Empty<string>();
        public string[] SenderOrganizationIds { get; set; } = Array.Empty<string>();
        public string[] SenderRoleIds { get; set; } = Array.Empty<string>();
        public string[] SenderNeedToKnowTags { get; set; } = Array.Empty<string>();
        public string[] SenderCapabilityIds { get; set; } = Array.Empty<string>();
        public string[] AvailableMethodIds { get; set; } = Array.Empty<string>();
        public string[] SharedLanguageIds { get; set; } = Array.Empty<string>();
        public PersonKnowledgeRuntime SenderKnowledge { get; set; }
        public PersonMemoryRuntime SenderMemory { get; set; }
        public InformationSourceRuntime SourceRuntime { get; set; }
        public IReadOnlyDictionary<string, PersonKnowledgeRuntime> RecipientKnowledgeRuntimes { get; set; }
        public IReadOnlyDictionary<string, PersonMemoryRuntime> RecipientMemoryRuntimes { get; set; }
    }

    public sealed class TransferRecipientResult
    {
        public TransferRecipientResult(TransferRecipientResultData data)
        {
            Data = data == null ? new TransferRecipientResultData() : data.Clone();
        }

        public TransferRecipientResultData Data { get; }
        public string RecipientPersonId => Data.recipientPersonId ?? string.Empty;
        public InformationTransferStatus Status => Data.status;
        public TransferUnderstandingState Understanding => Data.understanding;
        public int InheritedConfidence => Data.inheritedConfidence;
        public IReadOnlyList<string> CreatedEvidenceIds => Data.createdEvidenceIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ResultingBeliefIds => Data.resultingBeliefIds ?? Array.Empty<string>();
        public IReadOnlyList<string> FormedMemoryIds => Data.formedMemoryIds ?? Array.Empty<string>();
    }

    public sealed class InformationTransferRecord
    {
        public InformationTransferRecord(InformationTransferRecordData data)
        {
            Data = data == null ? new InformationTransferRecordData() : data.Clone();
        }

        public InformationTransferRecordData Data { get; }
        public string TransferId => Data.transferId ?? string.Empty;
        public string SenderPersonId => Data.senderPersonId ?? string.Empty;
        public IReadOnlyList<string> RecipientPersonIds => Data.recipientPersonIds ?? Array.Empty<string>();
        public IReadOnlyList<TransferContentItemData> ContentItems => Data.contentItems ?? Array.Empty<TransferContentItemData>();
        public IReadOnlyList<TransferRecipientResult> RecipientResults => (Data.recipientResults ?? Array.Empty<TransferRecipientResultData>()).Select(result => new TransferRecipientResult(result)).ToArray();
    }

    public sealed class InformationTransferResult
    {
        private InformationTransferResult(bool succeeded, InformationTransferStatus status, string message, string transactionId, bool preview, bool duplicate, InformationTransferRecord record, IReadOnlyList<TransferRecipientResult> recipientResults, long priorRevision, long resultingRevision)
        {
            Succeeded = succeeded;
            Status = status;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            Record = record;
            RecipientResults = (recipientResults ?? Array.Empty<TransferRecipientResult>()).ToArray();
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
        }

        public bool Succeeded { get; }
        public InformationTransferStatus Status { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public InformationTransferRecord Record { get; }
        public IReadOnlyList<TransferRecipientResult> RecipientResults { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }

        public static InformationTransferResult Success(string message, string transactionId, InformationTransferRecord record, IReadOnlyList<TransferRecipientResult> recipients, long priorRevision, long resultingRevision, bool preview = false, bool duplicate = false)
        {
            return new InformationTransferResult(true, duplicate ? InformationTransferStatus.Duplicate : preview ? InformationTransferStatus.Preview : InformationTransferStatus.Succeeded, message, transactionId, preview, duplicate, record, recipients, priorRevision, resultingRevision);
        }

        public static InformationTransferResult Failure(InformationTransferStatus status, string message, string transactionId = "", bool preview = false, long revision = 0L)
        {
            return new InformationTransferResult(false, status, message, transactionId, preview, false, null, Array.Empty<TransferRecipientResult>(), revision, revision);
        }
    }

    public sealed class InformationTransferSnapshot
    {
        public InformationTransferSnapshot(string ownerId, long revision, IReadOnlyList<InformationTransferRecord> transfers)
        {
            OwnerId = ownerId ?? string.Empty;
            Revision = revision;
            Transfers = (transfers ?? Array.Empty<InformationTransferRecord>()).OrderBy(record => record.TransferId, StringComparer.Ordinal).ToArray();
        }

        public string OwnerId { get; }
        public long Revision { get; }
        public IReadOnlyList<InformationTransferRecord> Transfers { get; }
    }
}
