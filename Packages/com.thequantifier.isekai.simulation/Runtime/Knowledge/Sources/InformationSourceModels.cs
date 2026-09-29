using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Knowledge.Sources
{
    [Serializable]
    public sealed class ReliabilityProfileData
    {
        public int generalDependability = 500;
        public int domainExpertise = 500;
        public int firsthandProximity = 500;
        public int methodQuality = 500;
        public int authenticity = 500;
        public int identityCertainty = 500;
        public int observationQuality = 500;
        public int recordIntegrity = 500;
        public int recency = 500;
        public int transmissionIntegrity = 500;
        public int independence = 500;
        public int corroboration = 500;
        public int internalConsistency = 500;
        public int errorRisk = 250;
        public int deceptionRisk = 150;
        public int biasRisk = 150;
        public int completeness = 500;
        public int precision = 500;
        public int contextFit = 500;

        public static ReliabilityProfileData Default()
        {
            return new ReliabilityProfileData();
        }

        public ReliabilityProfileData Clone()
        {
            return (ReliabilityProfileData)MemberwiseClone();
        }

        public int Get(ReliabilityDimension dimension)
        {
            return dimension switch
            {
                ReliabilityDimension.GeneralDependability => generalDependability,
                ReliabilityDimension.DomainExpertise => domainExpertise,
                ReliabilityDimension.FirsthandProximity => firsthandProximity,
                ReliabilityDimension.MethodQuality => methodQuality,
                ReliabilityDimension.Authenticity => authenticity,
                ReliabilityDimension.IdentityCertainty => identityCertainty,
                ReliabilityDimension.ObservationQuality => observationQuality,
                ReliabilityDimension.RecordIntegrity => recordIntegrity,
                ReliabilityDimension.Recency => recency,
                ReliabilityDimension.TransmissionIntegrity => transmissionIntegrity,
                ReliabilityDimension.Independence => independence,
                ReliabilityDimension.Corroboration => corroboration,
                ReliabilityDimension.InternalConsistency => internalConsistency,
                ReliabilityDimension.ErrorRisk => errorRisk,
                ReliabilityDimension.DeceptionRisk => deceptionRisk,
                ReliabilityDimension.BiasRisk => biasRisk,
                ReliabilityDimension.Completeness => completeness,
                ReliabilityDimension.Precision => precision,
                ReliabilityDimension.ContextFit => contextFit,
                _ => 0
            };
        }

        public void Set(ReliabilityDimension dimension, int value)
        {
            int clamped = KnowledgeConfidence.Clamp(value);
            switch (dimension)
            {
                case ReliabilityDimension.GeneralDependability: generalDependability = clamped; break;
                case ReliabilityDimension.DomainExpertise: domainExpertise = clamped; break;
                case ReliabilityDimension.FirsthandProximity: firsthandProximity = clamped; break;
                case ReliabilityDimension.MethodQuality: methodQuality = clamped; break;
                case ReliabilityDimension.Authenticity: authenticity = clamped; break;
                case ReliabilityDimension.IdentityCertainty: identityCertainty = clamped; break;
                case ReliabilityDimension.ObservationQuality: observationQuality = clamped; break;
                case ReliabilityDimension.RecordIntegrity: recordIntegrity = clamped; break;
                case ReliabilityDimension.Recency: recency = clamped; break;
                case ReliabilityDimension.TransmissionIntegrity: transmissionIntegrity = clamped; break;
                case ReliabilityDimension.Independence: independence = clamped; break;
                case ReliabilityDimension.Corroboration: corroboration = clamped; break;
                case ReliabilityDimension.InternalConsistency: internalConsistency = clamped; break;
                case ReliabilityDimension.ErrorRisk: errorRisk = clamped; break;
                case ReliabilityDimension.DeceptionRisk: deceptionRisk = clamped; break;
                case ReliabilityDimension.BiasRisk: biasRisk = clamped; break;
                case ReliabilityDimension.Completeness: completeness = clamped; break;
                case ReliabilityDimension.Precision: precision = clamped; break;
                case ReliabilityDimension.ContextFit: contextFit = clamped; break;
            }
        }

        public ReliabilityProfileData Overlay(ReliabilityProfileData overrideValues, bool zeroMeansUnspecified = true)
        {
            ReliabilityProfileData result = Clone();
            if (overrideValues == null)
            {
                return result;
            }

            foreach (ReliabilityDimension dimension in Enum.GetValues(typeof(ReliabilityDimension)))
            {
                int value = overrideValues.Get(dimension);
                if (!zeroMeansUnspecified || value > 0)
                {
                    result.Set(dimension, value);
                }
            }

            return result;
        }

        public bool IsValid(out string failureReason)
        {
            failureReason = string.Empty;
            foreach (ReliabilityDimension dimension in Enum.GetValues(typeof(ReliabilityDimension)))
            {
                int value = Get(dimension);
                if (value < KnowledgeConfidence.Minimum || value > KnowledgeConfidence.Maximum)
                {
                    failureReason = $"{dimension} is outside 0..1000.";
                    return false;
                }
            }

            return true;
        }

        public int DeriveOverall()
        {
            int[] values =
            {
                generalDependability,
                domainExpertise,
                firsthandProximity,
                methodQuality,
                authenticity,
                identityCertainty,
                observationQuality,
                recordIntegrity,
                recency,
                transmissionIntegrity,
                independence,
                corroboration,
                internalConsistency,
                KnowledgeConfidence.Maximum - errorRisk,
                KnowledgeConfidence.Maximum - deceptionRisk,
                KnowledgeConfidence.Maximum - biasRisk,
                completeness,
                precision,
                contextFit
            };
            return KnowledgeConfidence.Clamp((int)Math.Round(values.Average()));
        }
    }

    [Serializable]
    public sealed class SourceTransformationData
    {
        public string transformationId;
        public InformationSourceTransformationType transformationType;
        public string fromSourceId;
        public string toSourceId;
        public string actorPersonId;
        public double worldTimeSeconds;
        public int quality = 800;
        public string note;

        public SourceTransformationData Clone()
        {
            return new SourceTransformationData
            {
                transformationId = transformationId,
                transformationType = transformationType,
                fromSourceId = fromSourceId,
                toSourceId = toSourceId,
                actorPersonId = actorPersonId,
                worldTimeSeconds = worldTimeSeconds,
                quality = KnowledgeConfidence.Clamp(quality),
                note = note
            };
        }
    }

    [Serializable]
    public sealed class InformationSourceInstanceData
    {
        public string sourceInstanceId;
        public string sourceDefinitionId;
        public InformationSourceCategory category;
        public InformationSourceReferenceType referenceType;
        public string referencedId;
        public string originalCreatorPersonId;
        public string observerPersonId;
        public string holderPersonId;
        public string transmitterPersonId;
        public double creationWorldTimeSeconds;
        public double observationWorldTimeSeconds;
        public double transmissionWorldTimeSeconds;
        public int generation;
        public string parentSourceId;
        public string originalSourceId;
        public SourceVerificationState verificationState;
        public SourceVerificationState authenticityState;
        public KnowledgeDomain domain;
        public string subjectId;
        public string methodId;
        public string authorityClassification;
        public string biasProfileId;
        public int errorRisk;
        public int deceptionRisk;
        public int biasRisk;
        public SourcePrivacyLevel privacy;
        public bool hidesOriginal;
        public string supersedesSourceId;
        public string correctedBySourceId;
        public string[] tags;
        public long revision;

        public InformationSourceInstanceData Clone()
        {
            return new InformationSourceInstanceData
            {
                sourceInstanceId = sourceInstanceId,
                sourceDefinitionId = sourceDefinitionId,
                category = category,
                referenceType = referenceType,
                referencedId = referencedId,
                originalCreatorPersonId = originalCreatorPersonId,
                observerPersonId = observerPersonId,
                holderPersonId = holderPersonId,
                transmitterPersonId = transmitterPersonId,
                creationWorldTimeSeconds = creationWorldTimeSeconds,
                observationWorldTimeSeconds = observationWorldTimeSeconds,
                transmissionWorldTimeSeconds = transmissionWorldTimeSeconds,
                generation = generation,
                parentSourceId = parentSourceId,
                originalSourceId = originalSourceId,
                verificationState = verificationState,
                authenticityState = authenticityState,
                domain = domain,
                subjectId = subjectId,
                methodId = methodId,
                authorityClassification = authorityClassification,
                biasProfileId = biasProfileId,
                errorRisk = KnowledgeConfidence.Clamp(errorRisk),
                deceptionRisk = KnowledgeConfidence.Clamp(deceptionRisk),
                biasRisk = KnowledgeConfidence.Clamp(biasRisk),
                privacy = privacy,
                hidesOriginal = hidesOriginal,
                supersedesSourceId = supersedesSourceId,
                correctedBySourceId = correctedBySourceId,
                tags = tags == null ? Array.Empty<string>() : tags.ToArray(),
                revision = Math.Max(0L, revision)
            };
        }
    }

    [Serializable]
    public sealed class PersonSourceAssessmentData
    {
        public string assessmentId;
        public string assessingPersonId;
        public string sourceInstanceId;
        public KnowledgeDomain domain;
        public string subjectId;
        public string methodId;
        public double assessmentWorldTimeSeconds;
        public ReliabilityProfileData reliability = ReliabilityProfileData.Default();
        public int authority = 500;
        public int errorRisk = 250;
        public int deceptionRisk = 150;
        public int biasRisk = 150;
        public int familiarity = 0;
        public int confidenceInAssessment = 500;
        public string[] supportingEvidenceIds;
        public string[] priorExperienceIds;
        public string supersedesAssessmentId;
        public SourcePrivacyLevel privacy;
        public long revision;

        public PersonSourceAssessmentData Clone()
        {
            return new PersonSourceAssessmentData
            {
                assessmentId = assessmentId,
                assessingPersonId = assessingPersonId,
                sourceInstanceId = sourceInstanceId,
                domain = domain,
                subjectId = subjectId,
                methodId = methodId,
                assessmentWorldTimeSeconds = assessmentWorldTimeSeconds,
                reliability = reliability?.Clone() ?? ReliabilityProfileData.Default(),
                authority = KnowledgeConfidence.Clamp(authority),
                errorRisk = KnowledgeConfidence.Clamp(errorRisk),
                deceptionRisk = KnowledgeConfidence.Clamp(deceptionRisk),
                biasRisk = KnowledgeConfidence.Clamp(biasRisk),
                familiarity = KnowledgeConfidence.Clamp(familiarity),
                confidenceInAssessment = KnowledgeConfidence.Clamp(confidenceInAssessment),
                supportingEvidenceIds = supportingEvidenceIds == null ? Array.Empty<string>() : supportingEvidenceIds.ToArray(),
                priorExperienceIds = priorExperienceIds == null ? Array.Empty<string>() : priorExperienceIds.ToArray(),
                supersedesAssessmentId = supersedesAssessmentId,
                privacy = privacy,
                revision = Math.Max(0L, revision)
            };
        }
    }

    [Serializable]
    public sealed class InformationSourceProcessedTransactionData
    {
        public string transactionId;
        public InformationSourceResultCode code;
        public string sourceInstanceId;
        public string assessmentId;
        public long revision;
    }

    [Serializable]
    public sealed class InformationSourceSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string ownerId;
        public long sourceRevision;
        public InformationSourceInstanceData[] sources;
        public PersonSourceAssessmentData[] assessments;
        public SourceTransformationData[] transformations;
        public InformationSourceProcessedTransactionData[] processedTransactions;
    }

    public sealed class InformationSourceRegistrationRequest
    {
        public string TransactionId { get; set; }
        public string SourceInstanceId { get; set; }
        public string SourceDefinitionId { get; set; }
        public InformationSourceCategory Category { get; set; }
        public InformationSourceReferenceType ReferenceType { get; set; }
        public string ReferencedId { get; set; }
        public string OriginalCreatorPersonId { get; set; }
        public string ObserverPersonId { get; set; }
        public string HolderPersonId { get; set; }
        public string TransmitterPersonId { get; set; }
        public double CreationWorldTimeSeconds { get; set; }
        public double ObservationWorldTimeSeconds { get; set; }
        public double TransmissionWorldTimeSeconds { get; set; }
        public KnowledgeDomain Domain { get; set; }
        public string SubjectId { get; set; }
        public string MethodId { get; set; }
        public string AuthorityClassification { get; set; }
        public string BiasProfileId { get; set; }
        public int ErrorRisk { get; set; } = 250;
        public int DeceptionRisk { get; set; } = 150;
        public int BiasRisk { get; set; } = 150;
        public SourcePrivacyLevel Privacy { get; set; } = SourcePrivacyLevel.Public;
        public string[] Tags { get; set; }
    }

    public sealed class SourceTransformationRequest
    {
        public string TransactionId { get; set; }
        public string SourceInstanceId { get; set; }
        public string ParentSourceId { get; set; }
        public InformationSourceTransformationType TransformationType { get; set; }
        public string ActorPersonId { get; set; }
        public double WorldTimeSeconds { get; set; }
        public int Quality { get; set; } = 800;
        public bool HidesOriginal { get; set; }
        public string Note { get; set; }
    }

    public sealed class SourceAssessmentRequest
    {
        public string TransactionId { get; set; }
        public string AssessmentId { get; set; }
        public string AssessingPersonId { get; set; }
        public string SourceInstanceId { get; set; }
        public KnowledgeDomain Domain { get; set; }
        public string SubjectId { get; set; }
        public string MethodId { get; set; }
        public double WorldTimeSeconds { get; set; }
        public ReliabilityProfileData Reliability { get; set; } = ReliabilityProfileData.Default();
        public int Authority { get; set; } = 500;
        public int ErrorRisk { get; set; } = 250;
        public int DeceptionRisk { get; set; } = 150;
        public int BiasRisk { get; set; } = 150;
        public int Familiarity { get; set; }
        public int ConfidenceInAssessment { get; set; } = 500;
        public string[] SupportingEvidenceIds { get; set; }
        public string[] PriorExperienceIds { get; set; }
        public SourcePrivacyLevel Privacy { get; set; } = SourcePrivacyLevel.Personal;
    }

    public sealed class SourceReliabilityRequest
    {
        public string EvaluatingPersonId { get; set; }
        public string SourceInstanceId { get; set; }
        public KnowledgeDomain Domain { get; set; }
        public string SubjectId { get; set; }
        public string MethodId { get; set; }
        public double WorldTimeSeconds { get; set; }
        public bool PrivilegedAccess { get; set; }
        public string PolicyId { get; set; } = "source-reliability.prototype.default";
    }

    public sealed class InformationSourceRecord
    {
        public InformationSourceRecord(InformationSourceInstanceData data)
        {
            Data = data == null ? new InformationSourceInstanceData() : data.Clone();
        }

        public InformationSourceInstanceData Data { get; }
        public string SourceInstanceId => Data.sourceInstanceId ?? string.Empty;
        public string OriginalSourceId => string.IsNullOrWhiteSpace(Data.originalSourceId) ? SourceInstanceId : Data.originalSourceId;
        public InformationSourceCategory Category => Data.category;
        public SourcePrivacyLevel Privacy => Data.privacy;
    }

    public sealed class PersonSourceAssessmentRecord
    {
        public PersonSourceAssessmentRecord(PersonSourceAssessmentData data)
        {
            Data = data == null ? new PersonSourceAssessmentData() : data.Clone();
        }

        public PersonSourceAssessmentData Data { get; }
        public string AssessmentId => Data.assessmentId ?? string.Empty;
        public string AssessingPersonId => Data.assessingPersonId ?? string.Empty;
        public string SourceInstanceId => Data.sourceInstanceId ?? string.Empty;
    }

    public sealed class SourceChainSnapshot
    {
        public SourceChainSnapshot(IReadOnlyList<InformationSourceRecord> chain, IReadOnlyList<SourceTransformationData> transformations, bool originalHidden)
        {
            Chain = (chain ?? Array.Empty<InformationSourceRecord>()).ToArray();
            Transformations = (transformations ?? Array.Empty<SourceTransformationData>()).Select(item => item.Clone()).ToArray();
            OriginalHidden = originalHidden;
        }

        public IReadOnlyList<InformationSourceRecord> Chain { get; }
        public IReadOnlyList<SourceTransformationData> Transformations { get; }
        public bool OriginalHidden { get; }
        public int TransmissionDepth => Math.Max(0, Chain.Count - 1);
        public string ImmediateSourceId => Chain.Count == 0 ? string.Empty : Chain[0].SourceInstanceId;
        public string OriginalSourceId => Chain.Count == 0 ? string.Empty : Chain[Chain.Count - 1].SourceInstanceId;
    }

    public sealed class SourceReliabilityResult
    {
        public SourceReliabilityResult(
            bool succeeded,
            InformationSourceResultCode code,
            string message,
            SourceReliabilityRequest request,
            ReliabilityProfileData finalDimensions,
            int derivedOverall,
            int confidence,
            SourceChainSnapshot chain,
            PersonSourceAssessmentRecord personAssessment,
            IReadOnlyList<string> diagnostics)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Request = request;
            FinalDimensions = finalDimensions?.Clone() ?? ReliabilityProfileData.Default();
            DerivedOverall = KnowledgeConfidence.Clamp(derivedOverall);
            Confidence = KnowledgeConfidence.Clamp(confidence);
            Chain = chain ?? new SourceChainSnapshot(Array.Empty<InformationSourceRecord>(), Array.Empty<SourceTransformationData>(), false);
            PersonAssessment = personAssessment;
            Diagnostics = (diagnostics ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        public bool Succeeded { get; }
        public InformationSourceResultCode Code { get; }
        public string Message { get; }
        public SourceReliabilityRequest Request { get; }
        public ReliabilityProfileData FinalDimensions { get; }
        public int DerivedOverall { get; }
        public int Confidence { get; }
        public SourceChainSnapshot Chain { get; }
        public PersonSourceAssessmentRecord PersonAssessment { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }

    public sealed class InformationSourceOperationResult
    {
        private InformationSourceOperationResult(bool succeeded, InformationSourceResultCode code, string message, string transactionId, bool preview, bool duplicate, InformationSourceRecord source, PersonSourceAssessmentRecord assessment, long priorRevision, long resultingRevision)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            Source = source;
            Assessment = assessment;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
        }

        public bool Succeeded { get; }
        public InformationSourceResultCode Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public InformationSourceRecord Source { get; }
        public PersonSourceAssessmentRecord Assessment { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }

        public static InformationSourceOperationResult Success(string message, string transactionId, InformationSourceRecord source, PersonSourceAssessmentRecord assessment, long priorRevision, long resultingRevision, bool preview = false, bool duplicate = false)
        {
            return new InformationSourceOperationResult(true, duplicate ? InformationSourceResultCode.Duplicate : preview ? InformationSourceResultCode.Preview : InformationSourceResultCode.Success, message, transactionId, preview, duplicate, source, assessment, priorRevision, resultingRevision);
        }

        public static InformationSourceOperationResult Failure(InformationSourceResultCode code, string message, string transactionId = "", bool preview = false, long revision = 0L)
        {
            return new InformationSourceOperationResult(false, code, message, transactionId, preview, false, null, null, revision, revision);
        }
    }

    public sealed class InformationSourceSnapshot
    {
        public InformationSourceSnapshot(string ownerId, long revision, IReadOnlyList<InformationSourceRecord> sources, IReadOnlyList<PersonSourceAssessmentRecord> assessments, IReadOnlyList<SourceTransformationData> transformations)
        {
            OwnerId = ownerId ?? string.Empty;
            Revision = revision;
            Sources = (sources ?? Array.Empty<InformationSourceRecord>()).OrderBy(record => record.SourceInstanceId, StringComparer.Ordinal).ToArray();
            Assessments = (assessments ?? Array.Empty<PersonSourceAssessmentRecord>()).OrderBy(record => record.AssessmentId, StringComparer.Ordinal).ToArray();
            Transformations = (transformations ?? Array.Empty<SourceTransformationData>()).Select(item => item.Clone()).OrderBy(item => item.transformationId, StringComparer.Ordinal).ToArray();
        }

        public string OwnerId { get; }
        public long Revision { get; }
        public IReadOnlyList<InformationSourceRecord> Sources { get; }
        public IReadOnlyList<PersonSourceAssessmentRecord> Assessments { get; }
        public IReadOnlyList<SourceTransformationData> Transformations { get; }
    }
}
