using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;

namespace UnityIsekaiGame.Social.Rumors
{
    [Serializable]
    public sealed class RumorRecordData
    {
        public string rumorId;
        public string rootRumorId;
        public string parentRumorId;
        public string definitionId;
        public KnowledgePropositionData claim;
        public string claimIdentity;
        public string[] subjectIds;
        public string originatorPersonId;
        public string originatingEventId;
        public string originatingEvidenceId;
        public string sourceAttributionPersonId;
        public bool sourceNamed;
        public int confidence;
        public int salience;
        public int memorability;
        public RumorDisclosure disclosure;
        public RumorAuthenticity authenticity;
        public RumorOriginCategory originCategory;
        public RumorDistortionOperation[] distortionOperations;
        public string derivationReason;
        public double creationWorldTime;
        public long revision;
        public RumorLifecycleState lifecycleState;
        public string[] tags;

        public RumorRecordData Clone()
        {
            return new RumorRecordData
            {
                rumorId = rumorId,
                rootRumorId = rootRumorId,
                parentRumorId = parentRumorId,
                definitionId = definitionId,
                claim = claim?.Clone(),
                claimIdentity = claimIdentity,
                subjectIds = subjectIds == null ? Array.Empty<string>() : subjectIds.ToArray(),
                originatorPersonId = originatorPersonId,
                originatingEventId = originatingEventId,
                originatingEvidenceId = originatingEvidenceId,
                sourceAttributionPersonId = sourceAttributionPersonId,
                sourceNamed = sourceNamed,
                confidence = confidence,
                salience = salience,
                memorability = memorability,
                disclosure = disclosure,
                authenticity = authenticity,
                originCategory = originCategory,
                distortionOperations = distortionOperations == null ? Array.Empty<RumorDistortionOperation>() : distortionOperations.ToArray(),
                derivationReason = derivationReason,
                creationWorldTime = creationWorldTime,
                revision = revision,
                lifecycleState = lifecycleState,
                tags = tags == null ? Array.Empty<string>() : tags.ToArray()
            };
        }
    }

    [Serializable]
    public sealed class RumorTransmissionRecordData
    {
        public string transmissionId;
        public string transactionId;
        public string rumorVersionId;
        public string rootRumorId;
        public string speakerPersonId;
        public string listenerPersonId;
        public double transmissionWorldTime;
        public string channelId;
        public string placeId;
        public string interactionContextId;
        public bool sourceNamed;
        public int speakerConfidence;
        public RumorTransmissionOutcome outcome;
        public string resultingRumorVersionId;
        public string evidenceId;
        public string beliefId;
        public string memoryId;
        public string failureReason;
        public long revision;

        public RumorTransmissionRecordData Clone()
        {
            return new RumorTransmissionRecordData
            {
                transmissionId = transmissionId,
                transactionId = transactionId,
                rumorVersionId = rumorVersionId,
                rootRumorId = rootRumorId,
                speakerPersonId = speakerPersonId,
                listenerPersonId = listenerPersonId,
                transmissionWorldTime = transmissionWorldTime,
                channelId = channelId,
                placeId = placeId,
                interactionContextId = interactionContextId,
                sourceNamed = sourceNamed,
                speakerConfidence = speakerConfidence,
                outcome = outcome,
                resultingRumorVersionId = resultingRumorVersionId,
                evidenceId = evidenceId,
                beliefId = beliefId,
                memoryId = memoryId,
                failureReason = failureReason,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RumorProcessedTransactionData
    {
        public string transactionId;
        public RumorOperationStatus status;
        public string rumorId;
        public string transmissionId;
        public long revision;

        public RumorProcessedTransactionData Clone()
        {
            return new RumorProcessedTransactionData
            {
                transactionId = transactionId,
                status = status,
                rumorId = rumorId,
                transmissionId = transmissionId,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RumorRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public RumorRecordData[] rumors;
        public RumorTransmissionRecordData[] transmissions;
        public RumorProcessedTransactionData[] processedTransactions;

        public RumorRuntimeSaveData Clone()
        {
            return new RumorRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                rumors = rumors == null ? Array.Empty<RumorRecordData>() : rumors.Select(item => item?.Clone()).ToArray(),
                transmissions = transmissions == null ? Array.Empty<RumorTransmissionRecordData>() : transmissions.Select(item => item?.Clone()).ToArray(),
                processedTransactions = processedTransactions == null ? Array.Empty<RumorProcessedTransactionData>() : processedTransactions.Select(item => item?.Clone()).ToArray()
            };
        }
    }

    public sealed class RumorCreateRequest
    {
        public string TransactionId { get; set; }
        public string RumorId { get; set; }
        public string DefinitionId { get; set; }
        public KnowledgePropositionData Claim { get; set; }
        public string OriginatorPersonId { get; set; }
        public RumorOriginCategory OriginCategory { get; set; } = RumorOriginCategory.Unknown;
        public string OriginatingEventId { get; set; }
        public string OriginatingEvidenceId { get; set; }
        public string SourceAttributionPersonId { get; set; }
        public bool SourceNamed { get; set; } = true;
        public int Confidence { get; set; } = KnowledgeConfidence.DefaultObservation;
        public int Salience { get; set; } = 500;
        public int Memorability { get; set; } = 500;
        public RumorDisclosure? DisclosureOverride { get; set; }
        public RumorAuthenticity Authenticity { get; set; } = RumorAuthenticity.Unverified;
        public double WorldTime { get; set; }
        public string[] Tags { get; set; }
        public bool Preview { get; set; }
    }

    public sealed class RumorTransmissionRequest
    {
        public string TransactionId { get; set; }
        public string TransmissionId { get; set; }
        public string RumorVersionId { get; set; }
        public string SpeakerPersonId { get; set; }
        public string ListenerPersonId { get; set; }
        public double WorldTime { get; set; }
        public string ChannelId { get; set; }
        public string PlaceId { get; set; }
        public string InteractionContextId { get; set; }
        public bool NameSource { get; set; } = true;
        public bool SpeakerClaimsFirsthand { get; set; }
        public bool IntentionalSharing { get; set; } = true;
        public int SpeakerConfidence { get; set; } = KnowledgeConfidence.DefaultObservation;
        public RumorTransmissionOutcome RequestedOutcome { get; set; } = RumorTransmissionOutcome.Heard;
        public RumorDistortionPolicy RequestedDistortionPolicy { get; set; } = RumorDistortionPolicy.None;
        public string DerivedRumorId { get; set; }
        public string DeterministicSeed { get; set; }
        public bool CreateKnowledgeEvidence { get; set; } = true;
        public bool CreateMemory { get; set; } = true;
        public bool BypassDisclosure { get; set; }
        public bool Preview { get; set; }
    }

    public sealed class RumorPropagationRequest
    {
        public string TransactionId { get; set; }
        public string RumorVersionId { get; set; }
        public string SpeakerPersonId { get; set; }
        public string[] ListenerPersonIds { get; set; }
        public string ChannelId { get; set; }
        public double WorldTime { get; set; }
        public int MaximumTransmissions { get; set; } = 8;
        public int MaximumDepth { get; set; } = 3;
        public string DeterministicSeed { get; set; }
        public bool Preview { get; set; }
    }

    public sealed class RumorSnapshot
    {
        private readonly RumorRecordData data;

        public RumorSnapshot(RumorRecordData data)
        {
            this.data = data == null ? new RumorRecordData() : data.Clone();
        }

        public RumorRecordData Data => data.Clone();
        public string RumorId => data.rumorId ?? string.Empty;
        public string RootRumorId => data.rootRumorId ?? string.Empty;
        public string ParentRumorId => data.parentRumorId ?? string.Empty;
        public string DefinitionId => data.definitionId ?? string.Empty;
        public KnowledgeProposition Proposition => new KnowledgeProposition(data.claim);
        public string ClaimIdentity => data.claimIdentity ?? string.Empty;
        public string OriginatorPersonId => data.originatorPersonId ?? string.Empty;
        public int Confidence => KnowledgeConfidence.Clamp(data.confidence);
        public RumorDisclosure Disclosure => data.disclosure;
        public RumorAuthenticity Authenticity => data.authenticity;
        public IReadOnlyList<RumorDistortionOperation> DistortionOperations => data.distortionOperations == null ? Array.Empty<RumorDistortionOperation>() : data.distortionOperations.ToArray();
    }

    public sealed class RumorTransmissionSnapshot
    {
        private readonly RumorTransmissionRecordData data;

        public RumorTransmissionSnapshot(RumorTransmissionRecordData data)
        {
            this.data = data == null ? new RumorTransmissionRecordData() : data.Clone();
        }

        public RumorTransmissionRecordData Data => data.Clone();
        public string TransmissionId => data.transmissionId ?? string.Empty;
        public string RumorVersionId => data.rumorVersionId ?? string.Empty;
        public string RootRumorId => data.rootRumorId ?? string.Empty;
        public string SpeakerPersonId => data.speakerPersonId ?? string.Empty;
        public string ListenerPersonId => data.listenerPersonId ?? string.Empty;
        public RumorTransmissionOutcome Outcome => data.outcome;
        public string ResultingRumorVersionId => data.resultingRumorVersionId ?? string.Empty;
        public string EvidenceId => data.evidenceId ?? string.Empty;
        public string BeliefId => data.beliefId ?? string.Empty;
        public string MemoryId => data.memoryId ?? string.Empty;
    }

    public sealed class RumorOperationResult
    {
        private RumorOperationResult(bool succeeded, RumorOperationStatus status, string message, string transactionId, bool preview, bool duplicate, RumorSnapshot rumor, RumorTransmissionSnapshot transmission, KnowledgeOperationResult knowledgeResult, HistoryOperationResult memoryResult, long priorRevision, long resultingRevision)
        {
            Succeeded = succeeded;
            Status = status;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            Rumor = rumor;
            Transmission = transmission;
            KnowledgeResult = knowledgeResult;
            MemoryResult = memoryResult;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
        }

        public bool Succeeded { get; }
        public RumorOperationStatus Status { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public RumorSnapshot Rumor { get; }
        public RumorTransmissionSnapshot Transmission { get; }
        public KnowledgeOperationResult KnowledgeResult { get; }
        public HistoryOperationResult MemoryResult { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }

        public static RumorOperationResult Success(string message, string transactionId, RumorSnapshot rumor, RumorTransmissionSnapshot transmission, KnowledgeOperationResult knowledgeResult, HistoryOperationResult memoryResult, long priorRevision, long resultingRevision, bool preview = false, bool duplicate = false)
        {
            return new RumorOperationResult(true, duplicate ? RumorOperationStatus.Duplicate : preview ? RumorOperationStatus.Preview : RumorOperationStatus.Succeeded, message, transactionId, preview, duplicate, rumor, transmission, knowledgeResult, memoryResult, priorRevision, resultingRevision);
        }

        public static RumorOperationResult Failure(RumorOperationStatus status, string message, string transactionId = "", bool preview = false, long revision = 0L)
        {
            return new RumorOperationResult(false, status, message, transactionId, preview, false, null, null, null, null, revision, revision);
        }
    }

    public sealed class RumorPropagationResult
    {
        public RumorPropagationResult(string transactionId, bool succeeded, bool preview, IReadOnlyList<RumorOperationResult> transmissions, string message)
        {
            TransactionId = transactionId ?? string.Empty;
            Succeeded = succeeded;
            Preview = preview;
            Transmissions = (transmissions ?? Array.Empty<RumorOperationResult>()).ToArray();
            Message = message ?? string.Empty;
        }

        public string TransactionId { get; }
        public bool Succeeded { get; }
        public bool Preview { get; }
        public IReadOnlyList<RumorOperationResult> Transmissions { get; }
        public string Message { get; }
    }

    public sealed class RumorPropagationMetrics
    {
        public RumorPropagationMetrics(string rootRumorId, int versions, int transmissions, int awarePeople, int believers, int uncertain, int rejected)
        {
            RootRumorId = rootRumorId ?? string.Empty;
            Versions = versions;
            Transmissions = transmissions;
            AwarePeople = awarePeople;
            Believers = believers;
            Uncertain = uncertain;
            Rejected = rejected;
        }

        public string RootRumorId { get; }
        public int Versions { get; }
        public int Transmissions { get; }
        public int AwarePeople { get; }
        public int Believers { get; }
        public int Uncertain { get; }
        public int Rejected { get; }
    }
}
