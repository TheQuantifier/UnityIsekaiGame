using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [Serializable]
    public sealed class CredentialIssuerReferenceData
    {
        public string issuerId;
        public CredentialIssuerAuthorityKind issuerKind = CredentialIssuerAuthorityKind.Custom;
        public string issuingPersonId;

        public CredentialIssuerReferenceData Clone()
        {
            return new CredentialIssuerReferenceData
            {
                issuerId = issuerId ?? string.Empty,
                issuerKind = issuerKind,
                issuingPersonId = issuingPersonId ?? string.Empty
            };
        }

        public string Signature => $"{issuerKind}:{issuerId ?? string.Empty}:{issuingPersonId ?? string.Empty}";
    }

    [Serializable]
    public sealed class CredentialQualificationSnapshotData
    {
        public string credentialDefinitionId;
        public string personId;
        public string professionId;
        public string specializationId;
        public bool authoritativeQualified;
        public bool perceivedQualified;
        public string[] satisfiedRequirementIds = Array.Empty<string>();
        public string[] blockingRequirementIds = Array.Empty<string>();
        public string[] optionalUnmetRequirementIds = Array.Empty<string>();
        public string[] expiringRequirementIds = Array.Empty<string>();
        public long professionRevision;
        public long trainingRevision;
        public long activityRevision;
        public long credentialRevision;
        public string qualificationHash;
        public string privilegedDiagnostics;
        public string redactedDiagnostics;

        public CredentialQualificationSnapshotData Clone()
        {
            return new CredentialQualificationSnapshotData
            {
                credentialDefinitionId = credentialDefinitionId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                authoritativeQualified = authoritativeQualified,
                perceivedQualified = perceivedQualified,
                satisfiedRequirementIds = Clean(satisfiedRequirementIds),
                blockingRequirementIds = Clean(blockingRequirementIds),
                optionalUnmetRequirementIds = Clean(optionalUnmetRequirementIds),
                expiringRequirementIds = Clean(expiringRequirementIds),
                professionRevision = Math.Max(0L, professionRevision),
                trainingRevision = Math.Max(0L, trainingRevision),
                activityRevision = Math.Max(0L, activityRevision),
                credentialRevision = Math.Max(0L, credentialRevision),
                qualificationHash = qualificationHash ?? string.Empty,
                privilegedDiagnostics = privilegedDiagnostics ?? string.Empty,
                redactedDiagnostics = redactedDiagnostics ?? string.Empty
            };
        }

        public bool SemanticallyEquals(CredentialQualificationSnapshotData other)
        {
            return other != null
                && string.Equals(qualificationHash ?? string.Empty, other.qualificationHash ?? string.Empty, StringComparison.Ordinal)
                && professionRevision == other.professionRevision
                && trainingRevision == other.trainingRevision
                && activityRevision == other.activityRevision
                && credentialRevision == other.credentialRevision;
        }

        internal static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public sealed class CredentialQualificationResult
    {
        public CredentialQualificationResult(CredentialQualificationSnapshotData snapshot)
        {
            Snapshot = snapshot?.Clone() ?? new CredentialQualificationSnapshotData();
        }

        public CredentialQualificationSnapshotData Snapshot { get; }
        public bool AuthoritativeQualified => Snapshot.authoritativeQualified;
        public bool PerceivedQualified => Snapshot.perceivedQualified;
        public IReadOnlyList<string> SatisfiedRequirements => Snapshot.satisfiedRequirementIds;
        public IReadOnlyList<string> BlockingFailures => Snapshot.blockingRequirementIds;
        public IReadOnlyList<string> OptionalUnmetRequirements => Snapshot.optionalUnmetRequirementIds;
        public IReadOnlyList<string> ExpiringRequirements => Snapshot.expiringRequirementIds;
    }

    [Serializable]
    public sealed class CredentialApplicationData
    {
        public string applicationId;
        public string applicantPersonId;
        public string credentialDefinitionId;
        public CredentialIssuerReferenceData requestedIssuer = new CredentialIssuerReferenceData();
        public string relatedProfessionId;
        public string relatedSpecializationId;
        public string submissionWorldTime;
        public CredentialQualificationSnapshotData qualificationSnapshot = new CredentialQualificationSnapshotData();
        public string[] supportingTrainingRecordIds = Array.Empty<string>();
        public string[] supportingExperienceEvidenceIds = Array.Empty<string>();
        public string[] examinationAttemptIds = Array.Empty<string>();
        public string[] recommendationReferenceIds = Array.Empty<string>();
        public CredentialApplicationState state = CredentialApplicationState.Draft;
        public string decisionWorldTime;
        public string decisionMakerId;
        public string decisionReason;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public CredentialApplicationData Clone()
        {
            return new CredentialApplicationData
            {
                applicationId = applicationId ?? string.Empty,
                applicantPersonId = applicantPersonId ?? string.Empty,
                credentialDefinitionId = credentialDefinitionId ?? string.Empty,
                requestedIssuer = requestedIssuer?.Clone() ?? new CredentialIssuerReferenceData(),
                relatedProfessionId = relatedProfessionId ?? string.Empty,
                relatedSpecializationId = relatedSpecializationId ?? string.Empty,
                submissionWorldTime = submissionWorldTime ?? string.Empty,
                qualificationSnapshot = qualificationSnapshot?.Clone() ?? new CredentialQualificationSnapshotData(),
                supportingTrainingRecordIds = CredentialQualificationSnapshotData.Clean(supportingTrainingRecordIds),
                supportingExperienceEvidenceIds = CredentialQualificationSnapshotData.Clean(supportingExperienceEvidenceIds),
                examinationAttemptIds = CredentialQualificationSnapshotData.Clean(examinationAttemptIds),
                recommendationReferenceIds = CredentialQualificationSnapshotData.Clean(recommendationReferenceIds),
                state = state,
                decisionWorldTime = decisionWorldTime ?? string.Empty,
                decisionMakerId = decisionMakerId ?? string.Empty,
                decisionReason = decisionReason ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class CredentialExaminationSectionResultData
    {
        public string sectionId;
        public string displayName;
        public int score;
        public bool passed;

        public CredentialExaminationSectionResultData Clone()
        {
            return new CredentialExaminationSectionResultData
            {
                sectionId = sectionId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                score = Math.Max(0, Math.Min(1000, score)),
                passed = passed
            };
        }
    }

    [Serializable]
    public sealed class CredentialExaminationAttemptData
    {
        public string attemptId;
        public string examinationDefinitionId;
        public string applicantPersonId;
        public string evaluatorPersonId;
        public string evaluatorAuthorityId;
        public string startWorldTime;
        public string completionWorldTime;
        public CredentialAssessmentCategory assessmentCategory = CredentialAssessmentCategory.Custom;
        public ProfessionalActivitySourceReferenceData[] sourceActivityReferences = Array.Empty<ProfessionalActivitySourceReferenceData>();
        public CredentialExaminationSectionResultData[] sectionResults = Array.Empty<CredentialExaminationSectionResultData>();
        public int score;
        public CredentialExaminationAttemptState state = CredentialExaminationAttemptState.Draft;
        public string[] evidenceReferenceIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public CredentialExaminationAttemptData Clone()
        {
            return new CredentialExaminationAttemptData
            {
                attemptId = attemptId ?? string.Empty,
                examinationDefinitionId = examinationDefinitionId ?? string.Empty,
                applicantPersonId = applicantPersonId ?? string.Empty,
                evaluatorPersonId = evaluatorPersonId ?? string.Empty,
                evaluatorAuthorityId = evaluatorAuthorityId ?? string.Empty,
                startWorldTime = startWorldTime ?? string.Empty,
                completionWorldTime = completionWorldTime ?? string.Empty,
                assessmentCategory = assessmentCategory,
                sourceActivityReferences = (sourceActivityReferences ?? Array.Empty<ProfessionalActivitySourceReferenceData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.Signature, StringComparer.Ordinal).ToArray(),
                sectionResults = (sectionResults ?? Array.Empty<CredentialExaminationSectionResultData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.sectionId, StringComparer.Ordinal).ToArray(),
                score = Math.Max(0, Math.Min(1000, score)),
                state = state,
                evidenceReferenceIds = CredentialQualificationSnapshotData.Clean(evidenceReferenceIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class CredentialRecordData
    {
        public string credentialId;
        public string credentialDefinitionId;
        public string recipientPersonId;
        public CredentialIssuerReferenceData issuer = new CredentialIssuerReferenceData();
        public string issueWorldTime;
        public string effectiveWorldTime;
        public string expirationWorldTime;
        public CredentialState state = CredentialState.Pending;
        public string relatedProfessionId;
        public string relatedSpecializationId;
        public string supportingApplicationId;
        public string supportingExaminationAttemptId;
        public string[] supportingTrainingRecordIds = Array.Empty<string>();
        public string[] supportingExperienceEvidenceIds = Array.Empty<string>();
        public string[] grantedPermissionIds = Array.Empty<string>();
        public CredentialAuthenticityState authenticityState = CredentialAuthenticityState.Authoritative;
        public string registrationNumber;
        public string replacedByCredentialId;
        public string replacesCredentialId;
        public string accessPolicyId;
        public string provenance;
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public CredentialRecordData Clone()
        {
            return new CredentialRecordData
            {
                credentialId = credentialId ?? string.Empty,
                credentialDefinitionId = credentialDefinitionId ?? string.Empty,
                recipientPersonId = recipientPersonId ?? string.Empty,
                issuer = issuer?.Clone() ?? new CredentialIssuerReferenceData(),
                issueWorldTime = issueWorldTime ?? string.Empty,
                effectiveWorldTime = effectiveWorldTime ?? string.Empty,
                expirationWorldTime = expirationWorldTime ?? string.Empty,
                state = state,
                relatedProfessionId = relatedProfessionId ?? string.Empty,
                relatedSpecializationId = relatedSpecializationId ?? string.Empty,
                supportingApplicationId = supportingApplicationId ?? string.Empty,
                supportingExaminationAttemptId = supportingExaminationAttemptId ?? string.Empty,
                supportingTrainingRecordIds = CredentialQualificationSnapshotData.Clean(supportingTrainingRecordIds),
                supportingExperienceEvidenceIds = CredentialQualificationSnapshotData.Clean(supportingExperienceEvidenceIds),
                grantedPermissionIds = CredentialQualificationSnapshotData.Clean(grantedPermissionIds),
                authenticityState = authenticityState,
                registrationNumber = registrationNumber ?? string.Empty,
                replacedByCredentialId = replacedByCredentialId ?? string.Empty,
                replacesCredentialId = replacesCredentialId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revisionHistory = CredentialQualificationSnapshotData.Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class CredentialRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<CredentialApplicationData> applications = new List<CredentialApplicationData>();
        public List<CredentialExaminationAttemptData> examinationAttempts = new List<CredentialExaminationAttemptData>();
        public List<CredentialRecordData> credentials = new List<CredentialRecordData>();

        public CredentialRuntimeSaveData Clone()
        {
            return new CredentialRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = Math.Max(0L, revision),
                applications = applications == null ? new List<CredentialApplicationData>() : applications.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                examinationAttempts = examinationAttempts == null ? new List<CredentialExaminationAttemptData>() : examinationAttempts.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                credentials = credentials == null ? new List<CredentialRecordData>() : credentials.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class CredentialOperationResult
    {
        private CredentialOperationResult(bool succeeded, bool preview, bool duplicate, CredentialOperationStatus status, string message, long priorRevision, long resultingRevision, CredentialQualificationResult qualification = null, CredentialApplicationData application = null, CredentialExaminationAttemptData attempt = null, CredentialRecordData credential = null)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Status = status;
            Message = message ?? string.Empty;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
            Qualification = qualification;
            Application = application?.Clone();
            ExaminationAttempt = attempt?.Clone();
            Credential = credential?.Clone();
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public CredentialOperationStatus Status { get; }
        public string Message { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public CredentialQualificationResult Qualification { get; }
        public CredentialApplicationData Application { get; }
        public CredentialExaminationAttemptData ExaminationAttempt { get; }
        public CredentialRecordData Credential { get; }

        public static CredentialOperationResult Success(string message, long priorRevision, long resultingRevision, CredentialQualificationResult qualification = null, CredentialApplicationData application = null, CredentialExaminationAttemptData attempt = null, CredentialRecordData credential = null, bool preview = false, bool duplicate = false)
        {
            return new CredentialOperationResult(true, preview, duplicate, preview ? CredentialOperationStatus.Preview : duplicate ? CredentialOperationStatus.Duplicate : CredentialOperationStatus.Succeeded, message, priorRevision, resultingRevision, qualification, application, attempt, credential);
        }

        public static CredentialOperationResult Failure(CredentialOperationStatus status, string message, long revision = 0L, CredentialQualificationResult qualification = null)
        {
            return new CredentialOperationResult(false, false, false, status, message, revision, revision, qualification);
        }
    }

    public sealed class CredentialProjection<TRecord>
    {
        public CredentialProjection(TRecord record, CredentialProjectionAudience audience, InformationAccessDecision decision, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields)
        {
            Record = record;
            Audience = audience;
            Decision = decision;
            Redacted = redacted;
            Denied = denied;
            VisibleFields = (visibleFields ?? Array.Empty<string>()).ToArray();
            RedactedFields = (redactedFields ?? Array.Empty<string>()).ToArray();
        }

        public TRecord Record { get; }
        public CredentialProjectionAudience Audience { get; }
        public InformationAccessDecision Decision { get; }
        public bool Redacted { get; }
        public bool Denied { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    [Serializable]
    public sealed class CredentialHistoryHookData
    {
        public CredentialHistoryHookKind kind;
        public string credentialId;
        public string applicationId;
        public string examinationAttemptId;
        public string personId;
        public string issuerId;
        public string worldTime;
        public string transactionId;

        public CredentialHistoryHookData Clone()
        {
            return new CredentialHistoryHookData
            {
                kind = kind,
                credentialId = credentialId ?? string.Empty,
                applicationId = applicationId ?? string.Empty,
                examinationAttemptId = examinationAttemptId ?? string.Empty,
                personId = personId ?? string.Empty,
                issuerId = issuerId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            };
        }
    }

    public static class CredentialInformationSubject
    {
        public const string CredentialDefinitionTag = "subject.profession.credential-definition";
        public const string QualificationTag = "subject.profession.qualification";
        public const string ApplicationTag = "subject.profession.credential-application";
        public const string ExaminationDefinitionTag = "subject.profession.examination-definition";
        public const string ExaminationAttemptTag = "subject.profession.examination-attempt";
        public const string CredentialTag = "subject.profession.credential";
        public const string CredentialDecisionTag = "subject.profession.credential-decision";
        public const string CredentialVerificationTag = "subject.profession.credential-verification";

        public static readonly string[] ProtectedFields =
        {
            "applicant",
            "holder",
            "source",
            "examination",
            "score",
            "decision-reason",
            "registration",
            "provenance",
            "authenticity"
        };

        public static InformationSubjectReferenceData Create(string tag, string subjectId, string ownerId)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                ownerPersonId = ownerId ?? string.Empty,
                tags = CredentialQualificationSnapshotData.Clean(new[] { tag })
            };
        }
    }
}
