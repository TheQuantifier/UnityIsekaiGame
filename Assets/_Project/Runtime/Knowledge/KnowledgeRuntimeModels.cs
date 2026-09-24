using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Knowledge
{
    [Serializable]
    public sealed class KnowledgeEvidenceRecordData
    {
        public string evidenceId;
        public string observerPersonId;
        public string sourceId;
        public KnowledgeAcquisitionSource acquisitionSource;
        public KnowledgeProvenance provenance;
        public KnowledgeEvidenceDirection direction;
        public KnowledgePropositionData proposition;
        public string informationSourceId;
        public int rawStrength;
        public int effectiveStrength;
        public string reliabilityPolicyId;
        public string reliabilityEvaluationId;
        public int strength;
        public int credibility;
        public double gameTimeSeconds;
        public string locationContextId;
        public string bodyContextId;
        public KnowledgeVisibility visibility;
        public string relatedEventId;
        public string[] tags;

        public KnowledgeEvidenceRecordData Clone()
        {
            return new KnowledgeEvidenceRecordData
            {
                evidenceId = evidenceId,
                observerPersonId = observerPersonId,
                sourceId = sourceId,
                acquisitionSource = acquisitionSource,
                provenance = provenance,
                direction = direction,
                proposition = proposition?.Clone(),
                informationSourceId = informationSourceId,
                rawStrength = rawStrength,
                effectiveStrength = effectiveStrength,
                reliabilityPolicyId = reliabilityPolicyId,
                reliabilityEvaluationId = reliabilityEvaluationId,
                strength = strength,
                credibility = credibility,
                gameTimeSeconds = gameTimeSeconds,
                locationContextId = locationContextId,
                bodyContextId = bodyContextId,
                visibility = visibility,
                relatedEventId = relatedEventId,
                tags = tags == null ? Array.Empty<string>() : tags.ToArray()
            };
        }
    }

    [Serializable]
    public sealed class KnowledgeBeliefRecordData
    {
        public string beliefId;
        public string personId;
        public KnowledgePropositionData proposition;
        public int confidence;
        public double firstLearnedGameTimeSeconds;
        public double lastUpdatedGameTimeSeconds;
        public double lastVerifiedGameTimeSeconds;
        public string[] supportingEvidenceIds;
        public string[] opposingEvidenceIds;
        public string[] sourceIds;
        public KnowledgeVisibility visibility;
        public KnowledgeFreshnessState freshness;
        public KnowledgeTruthState truthState;
        public bool disputed;
        public bool forgotten;
        public string retainedSummary;
        public long beliefRevision;

        public KnowledgeBeliefRecordData Clone()
        {
            return new KnowledgeBeliefRecordData
            {
                beliefId = beliefId,
                personId = personId,
                proposition = proposition?.Clone(),
                confidence = confidence,
                firstLearnedGameTimeSeconds = firstLearnedGameTimeSeconds,
                lastUpdatedGameTimeSeconds = lastUpdatedGameTimeSeconds,
                lastVerifiedGameTimeSeconds = lastVerifiedGameTimeSeconds,
                supportingEvidenceIds = supportingEvidenceIds == null ? Array.Empty<string>() : supportingEvidenceIds.ToArray(),
                opposingEvidenceIds = opposingEvidenceIds == null ? Array.Empty<string>() : opposingEvidenceIds.ToArray(),
                sourceIds = sourceIds == null ? Array.Empty<string>() : sourceIds.ToArray(),
                visibility = visibility,
                freshness = freshness,
                truthState = truthState,
                disputed = disputed,
                forgotten = forgotten,
                retainedSummary = retainedSummary,
                beliefRevision = beliefRevision
            };
        }
    }

    [Serializable]
    public sealed class KnowledgeProcessedTransactionData
    {
        public string transactionId;
        public KnowledgeResultCode code;
        public string beliefId;
        public string evidenceId;
        public long revision;
    }

    [Serializable]
    public sealed class PersonKnowledgeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string personId;
        public string currentActorId;
        public string currentBodyId;
        public long knowledgeRevision;
        public KnowledgeBeliefRecordData[] beliefs;
        public KnowledgeEvidenceRecordData[] evidence;
        public KnowledgeProcessedTransactionData[] processedTransactions;
    }

    public sealed class KnowledgeEvidenceRecord
    {
        public KnowledgeEvidenceRecord(KnowledgeEvidenceRecordData data)
        {
            Data = data == null ? new KnowledgeEvidenceRecordData() : data.Clone();
        }

        public KnowledgeEvidenceRecordData Data { get; }
        public string EvidenceId => Data.evidenceId ?? string.Empty;
        public string ObserverPersonId => Data.observerPersonId ?? string.Empty;
        public KnowledgeProposition Proposition => new KnowledgeProposition(Data.proposition);
        public KnowledgeEvidenceDirection Direction => Data.direction;
        public KnowledgeProvenance Provenance => Data.provenance;
        public int Strength => KnowledgeConfidence.Clamp(Data.strength);
        public int RawStrength => Data.rawStrength <= 0 ? Strength : KnowledgeConfidence.Clamp(Data.rawStrength);
        public int EffectiveStrength => Data.effectiveStrength <= 0 ? Strength : KnowledgeConfidence.Clamp(Data.effectiveStrength);
        public int Credibility => KnowledgeConfidence.Clamp(Data.credibility);
        public string InformationSourceId => Data.informationSourceId ?? string.Empty;
        public KnowledgeVisibility Visibility => Data.visibility;
    }

    public sealed class KnowledgeBeliefRecord
    {
        public KnowledgeBeliefRecord(KnowledgeBeliefRecordData data, KnowledgeFactDefinition definition, KnowledgePolicyDefinition policy = null)
        {
            Data = data == null ? new KnowledgeBeliefRecordData() : data.Clone();
            Definition = definition;
            State = DeriveState(Data, definition, policy);
        }

        public KnowledgeBeliefRecordData Data { get; }
        public KnowledgeFactDefinition Definition { get; }
        public string BeliefId => Data.beliefId ?? string.Empty;
        public string PersonId => Data.personId ?? string.Empty;
        public KnowledgeProposition Proposition => new KnowledgeProposition(Data.proposition);
        public int Confidence => KnowledgeConfidence.Clamp(Data.confidence);
        public KnowledgeBeliefState State { get; }
        public KnowledgeFreshnessState Freshness => Data.freshness;
        public KnowledgeTruthState TruthState => Data.truthState;
        public IReadOnlyList<string> SupportingEvidenceIds => Data.supportingEvidenceIds ?? Array.Empty<string>();
        public IReadOnlyList<string> OpposingEvidenceIds => Data.opposingEvidenceIds ?? Array.Empty<string>();

        public static KnowledgeBeliefState DeriveState(KnowledgeBeliefRecordData data, KnowledgeFactDefinition definition)
        {
            return DeriveState(data, definition, null);
        }

        public static KnowledgeBeliefState DeriveState(KnowledgeBeliefRecordData data, KnowledgeFactDefinition definition, KnowledgePolicyDefinition policy)
        {
            if (data == null)
            {
                return KnowledgeBeliefState.Invalid;
            }

            if (data.forgotten || data.freshness == KnowledgeFreshnessState.Forgotten)
            {
                return KnowledgeBeliefState.Forgotten;
            }

            if (data.truthState == KnowledgeTruthState.Misconception)
            {
                return KnowledgeBeliefState.Misconception;
            }

            if (data.disputed)
            {
                return KnowledgeBeliefState.Disputed;
            }

            if (data.freshness == KnowledgeFreshnessState.Stale || data.freshness == KnowledgeFreshnessState.RequiresReverification)
            {
                return KnowledgeBeliefState.Stale;
            }

            int confidence = KnowledgeConfidence.Clamp(data.confidence);
            int threshold = definition == null ? policy == null ? 700 : policy.DefaultKnownThreshold : definition.CertaintyThreshold;
            if (confidence >= threshold)
            {
                return KnowledgeBeliefState.Known;
            }

            if (confidence >= (policy == null ? 600 : policy.StronglyBelievedThreshold))
            {
                return KnowledgeBeliefState.StronglyBelieved;
            }

            if (confidence >= (policy == null ? 400 : policy.BelievedThreshold))
            {
                return KnowledgeBeliefState.Believed;
            }

            if (confidence >= (policy == null ? 1 : policy.SuspectedThreshold))
            {
                return KnowledgeBeliefState.Suspected;
            }

            return KnowledgeBeliefState.Unknown;
        }
    }

    public sealed class KnowledgeDiscovery
    {
        public KnowledgeDiscovery(string category, KnowledgeBeliefRecord priorBelief, KnowledgeBeliefRecord resultingBelief, KnowledgeEvidenceRecord evidence, int confidenceDelta, string message)
        {
            Category = category ?? string.Empty;
            PriorBelief = priorBelief;
            ResultingBelief = resultingBelief;
            Evidence = evidence;
            ConfidenceDelta = confidenceDelta;
            Message = message ?? string.Empty;
        }

        public string Category { get; }
        public KnowledgeBeliefRecord PriorBelief { get; }
        public KnowledgeBeliefRecord ResultingBelief { get; }
        public KnowledgeEvidenceRecord Evidence { get; }
        public int ConfidenceDelta { get; }
        public string Message { get; }
    }

    public sealed class KnowledgeOperationResult
    {
        private KnowledgeOperationResult(
            bool succeeded,
            KnowledgeResultCode code,
            string message,
            string transactionId,
            bool preview,
            bool duplicate,
            KnowledgeBeliefRecord priorBelief,
            KnowledgeBeliefRecord resultingBelief,
            KnowledgeEvidenceRecord evidence,
            KnowledgeDiscovery discovery,
            long priorRevision,
            long resultingRevision)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            PriorBelief = priorBelief;
            ResultingBelief = resultingBelief;
            Evidence = evidence;
            Discovery = discovery;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
        }

        public bool Succeeded { get; }
        public KnowledgeResultCode Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public KnowledgeBeliefRecord PriorBelief { get; }
        public KnowledgeBeliefRecord ResultingBelief { get; }
        public KnowledgeEvidenceRecord Evidence { get; }
        public KnowledgeDiscovery Discovery { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }

        public static KnowledgeOperationResult Success(string message, string transactionId, KnowledgeBeliefRecord prior, KnowledgeBeliefRecord result, KnowledgeEvidenceRecord evidence, KnowledgeDiscovery discovery, long priorRevision, long resultingRevision, bool preview = false, bool duplicate = false)
        {
            return new KnowledgeOperationResult(true, duplicate ? KnowledgeResultCode.Duplicate : preview ? KnowledgeResultCode.Preview : KnowledgeResultCode.Success, message, transactionId, preview, duplicate, prior, result, evidence, discovery, priorRevision, resultingRevision);
        }

        public static KnowledgeOperationResult Failure(KnowledgeResultCode code, string message, string transactionId = "", bool preview = false, long revision = 0L)
        {
            return new KnowledgeOperationResult(false, code, message, transactionId, preview, false, null, null, null, null, revision, revision);
        }
    }

    public sealed class KnowledgeSnapshot
    {
        public KnowledgeSnapshot(
            string personId,
            string currentActorId,
            string currentBodyId,
            long revision,
            KnowledgeReadinessState readiness,
            IReadOnlyList<KnowledgeBeliefRecord> beliefs,
            IReadOnlyList<KnowledgeEvidenceRecord> evidence,
            IReadOnlyList<string> diagnostics)
        {
            PersonId = personId ?? string.Empty;
            CurrentActorId = currentActorId ?? string.Empty;
            CurrentBodyId = currentBodyId ?? string.Empty;
            Revision = revision;
            Readiness = readiness;
            Beliefs = (beliefs ?? Array.Empty<KnowledgeBeliefRecord>()).OrderBy(record => record.BeliefId, StringComparer.Ordinal).ToArray();
            Evidence = (evidence ?? Array.Empty<KnowledgeEvidenceRecord>()).OrderBy(record => record.EvidenceId, StringComparer.Ordinal).ToArray();
            Diagnostics = (diagnostics ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public string PersonId { get; }
        public string CurrentActorId { get; }
        public string CurrentBodyId { get; }
        public long Revision { get; }
        public KnowledgeReadinessState Readiness { get; }
        public IReadOnlyList<KnowledgeBeliefRecord> Beliefs { get; }
        public IReadOnlyList<KnowledgeEvidenceRecord> Evidence { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<KnowledgeBeliefRecord> KnownFacts => Beliefs.Where(record => record.State == KnowledgeBeliefState.Known).ToArray();
        public IReadOnlyList<KnowledgeBeliefRecord> Suspicions => Beliefs.Where(record => record.State == KnowledgeBeliefState.Suspected).ToArray();
        public IReadOnlyList<KnowledgeBeliefRecord> Misconceptions => Beliefs.Where(record => record.State == KnowledgeBeliefState.Misconception).ToArray();
        public IReadOnlyList<KnowledgeBeliefRecord> DisputedBeliefs => Beliefs.Where(record => record.State == KnowledgeBeliefState.Disputed).ToArray();
        public IReadOnlyList<KnowledgeBeliefRecord> StaleBeliefs => Beliefs.Where(record => record.State == KnowledgeBeliefState.Stale).ToArray();
    }

    public sealed class KnowledgeObservationRequest
    {
        public string PersonId { get; set; }
        public string TransactionId { get; set; }
        public KnowledgePropositionData Proposition { get; set; }
        public KnowledgeAcquisitionSource AcquisitionSource { get; set; } = KnowledgeAcquisitionSource.DirectObservation;
        public KnowledgeProvenance Provenance { get; set; } = KnowledgeProvenance.DirectObservation;
        public KnowledgeEvidenceDirection Direction { get; set; } = KnowledgeEvidenceDirection.Supports;
        public int Strength { get; set; } = KnowledgeConfidence.DefaultObservation;
        public int? EffectiveStrengthOverride { get; set; }
        public string InformationSourceId { get; set; }
        public string ReliabilityPolicyId { get; set; }
        public string ReliabilityEvaluationId { get; set; }
        public int Credibility { get; set; } = KnowledgeConfidence.DefaultObservation;
        public double GameTimeSeconds { get; set; }
        public string SourceId { get; set; }
        public string EvidenceId { get; set; }
        public KnowledgeVisibility Visibility { get; set; } = KnowledgeVisibility.Public;
        public KnowledgeTruthAuthorization TruthAuthorization { get; set; }
        public bool HasTruthAuthorization => TruthAuthorization != null && TruthAuthorization.AllowsTruthComparison;
        public bool MarkAsMisconception { get; set; }
        public bool PrivateAccessAuthorized { get; set; }
        public string RelatedEventId { get; set; }
        public string[] Tags { get; set; }
    }

    public sealed class KnowledgeTruthAuthorization
    {
        private KnowledgeTruthAuthorization(string authorityContext)
        {
            AuthorityContext = authorityContext ?? string.Empty;
        }

        public string AuthorityContext { get; }
        public bool AllowsTruthComparison => !string.IsNullOrWhiteSpace(AuthorityContext);

        internal static KnowledgeTruthAuthorization CreateTrusted(string authorityContext)
        {
            return new KnowledgeTruthAuthorization(authorityContext);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static KnowledgeTruthAuthorization CreateDevelopmentFixture(string authorityContext)
        {
            return new KnowledgeTruthAuthorization(authorityContext);
        }
#endif
    }

    public sealed class KnowledgeShareRequest
    {
        public string TransactionId { get; set; }
        public string SpeakerPersonId { get; set; }
        public string ListenerPersonId { get; set; }
        public KnowledgeBeliefRecord SpeakerBelief { get; set; }
        public int ListenerCredibility { get; set; } = 500;
        public double GameTimeSeconds { get; set; }
        public bool PrivateAccessAuthorized { get; set; }
    }

    public sealed class KnowledgeValidationResult
    {
        public KnowledgeValidationResult(bool succeeded, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            Succeeded = succeeded;
            Errors = errors ?? Array.Empty<string>();
            Warnings = warnings ?? Array.Empty<string>();
        }

        public bool Succeeded { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public string Message => Succeeded ? "Knowledge validation succeeded." : string.Join("; ", Errors);
    }
}
