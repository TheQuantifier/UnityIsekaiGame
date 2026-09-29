using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [Serializable]
    public sealed class PositionEligibilitySnapshotData
    {
        public string personId;
        public string positionInstanceId;
        public string positionDefinitionId;
        public string employerOrganizationId;
        public bool authoritativeEligible;
        public bool perceivedEligible;
        public string[] satisfiedRequirementIds = Array.Empty<string>();
        public string[] blockingRequirementIds = Array.Empty<string>();
        public string[] redactedRequirementIds = Array.Empty<string>();
        public string[] alternativePositionInstanceIds = Array.Empty<string>();
        public long professionRevision;
        public long trainingRevision;
        public long activityRevision;
        public long credentialRevision;
        public long rankRevision;
        public long employmentRevision;
        public string evaluationHash;
        public string privilegedDiagnostics;
        public string redactedDiagnostics;

        public PositionEligibilitySnapshotData Clone()
        {
            return new PositionEligibilitySnapshotData
            {
                personId = personId ?? string.Empty,
                positionInstanceId = positionInstanceId ?? string.Empty,
                positionDefinitionId = positionDefinitionId ?? string.Empty,
                employerOrganizationId = employerOrganizationId ?? string.Empty,
                authoritativeEligible = authoritativeEligible,
                perceivedEligible = perceivedEligible,
                satisfiedRequirementIds = Clean(satisfiedRequirementIds),
                blockingRequirementIds = Clean(blockingRequirementIds),
                redactedRequirementIds = Clean(redactedRequirementIds),
                alternativePositionInstanceIds = Clean(alternativePositionInstanceIds),
                professionRevision = Math.Max(0L, professionRevision),
                trainingRevision = Math.Max(0L, trainingRevision),
                activityRevision = Math.Max(0L, activityRevision),
                credentialRevision = Math.Max(0L, credentialRevision),
                rankRevision = Math.Max(0L, rankRevision),
                employmentRevision = Math.Max(0L, employmentRevision),
                evaluationHash = evaluationHash ?? string.Empty,
                privilegedDiagnostics = privilegedDiagnostics ?? string.Empty,
                redactedDiagnostics = redactedDiagnostics ?? string.Empty
            };
        }

        public bool SemanticallyEquals(PositionEligibilitySnapshotData other)
        {
            return other != null
                && string.Equals(evaluationHash ?? string.Empty, other.evaluationHash ?? string.Empty, StringComparison.Ordinal)
                && professionRevision == other.professionRevision
                && trainingRevision == other.trainingRevision
                && activityRevision == other.activityRevision
                && credentialRevision == other.credentialRevision
                && rankRevision == other.rankRevision
                && employmentRevision == other.employmentRevision;
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

    public sealed class PositionEligibilityResult
    {
        public PositionEligibilityResult(PositionEligibilitySnapshotData snapshot)
        {
            Snapshot = snapshot?.Clone() ?? new PositionEligibilitySnapshotData();
        }

        public PositionEligibilitySnapshotData Snapshot { get; }
        public bool AuthoritativeEligible => Snapshot.authoritativeEligible;
        public bool PerceivedEligible => Snapshot.perceivedEligible;
        public IReadOnlyList<string> SatisfiedRequirements => Snapshot.satisfiedRequirementIds;
        public IReadOnlyList<string> BlockingFailures => Snapshot.blockingRequirementIds;
        public IReadOnlyList<string> RedactedRequirements => Snapshot.redactedRequirementIds;
        public IReadOnlyList<string> AlternativePositions => Snapshot.alternativePositionInstanceIds;
    }

    [Serializable]
    public sealed class PositionInstanceData
    {
        public string positionInstanceId;
        public string positionDefinitionId;
        public string organizationId;
        public string organizationTypeId;
        public string departmentFoundationId;
        public string locationFoundationId;
        public PositionInstanceState state = PositionInstanceState.Planned;
        public string[] holderPersonIds = Array.Empty<string>();
        public int maximumHolders = 1;
        public string supervisorPositionInstanceId;
        public string[] subordinatePositionInstanceIds = Array.Empty<string>();
        public bool vacancyAllowed = true;
        public string createdWorldTime;
        public string closedWorldTime;
        public string accessPolicyId;
        public string provenance;
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public PositionInstanceData Clone()
        {
            return new PositionInstanceData
            {
                positionInstanceId = positionInstanceId ?? string.Empty,
                positionDefinitionId = positionDefinitionId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                organizationTypeId = organizationTypeId ?? string.Empty,
                departmentFoundationId = departmentFoundationId ?? string.Empty,
                locationFoundationId = locationFoundationId ?? string.Empty,
                state = state,
                holderPersonIds = PositionEligibilitySnapshotData.Clean(holderPersonIds),
                maximumHolders = Math.Max(1, maximumHolders),
                supervisorPositionInstanceId = supervisorPositionInstanceId ?? string.Empty,
                subordinatePositionInstanceIds = PositionEligibilitySnapshotData.Clean(subordinatePositionInstanceIds),
                vacancyAllowed = vacancyAllowed,
                createdWorldTime = createdWorldTime ?? string.Empty,
                closedWorldTime = closedWorldTime ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revisionHistory = PositionEligibilitySnapshotData.Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return PositionEmploymentInformationSubject.PositionInstance(positionInstanceId, organizationId);
        }
    }

    [Serializable]
    public sealed class PositionApplicationData
    {
        public string requestId;
        public string applicantPersonId;
        public string positionInstanceId;
        public string positionDefinitionId;
        public string employerOrganizationId;
        public PositionRequestType requestType = PositionRequestType.Application;
        public string submissionWorldTime;
        public PositionEligibilitySnapshotData evaluationSnapshot = new PositionEligibilitySnapshotData();
        public string[] supportingProfessionRelationshipIds = Array.Empty<string>();
        public string[] supportingRankRecordIds = Array.Empty<string>();
        public string[] supportingCredentialIds = Array.Empty<string>();
        public string[] supportingTrainingEnrollmentIds = Array.Empty<string>();
        public string[] supportingExperienceEvidenceIds = Array.Empty<string>();
        public string sponsorOrRecommenderId;
        public PositionRequestState state = PositionRequestState.Draft;
        public string reviewerOrAppointingAuthorityId;
        public string decisionWorldTime;
        public string decisionReason;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public PositionApplicationData Clone()
        {
            return new PositionApplicationData
            {
                requestId = requestId ?? string.Empty,
                applicantPersonId = applicantPersonId ?? string.Empty,
                positionInstanceId = positionInstanceId ?? string.Empty,
                positionDefinitionId = positionDefinitionId ?? string.Empty,
                employerOrganizationId = employerOrganizationId ?? string.Empty,
                requestType = requestType,
                submissionWorldTime = submissionWorldTime ?? string.Empty,
                evaluationSnapshot = evaluationSnapshot?.Clone() ?? new PositionEligibilitySnapshotData(),
                supportingProfessionRelationshipIds = PositionEligibilitySnapshotData.Clean(supportingProfessionRelationshipIds),
                supportingRankRecordIds = PositionEligibilitySnapshotData.Clean(supportingRankRecordIds),
                supportingCredentialIds = PositionEligibilitySnapshotData.Clean(supportingCredentialIds),
                supportingTrainingEnrollmentIds = PositionEligibilitySnapshotData.Clean(supportingTrainingEnrollmentIds),
                supportingExperienceEvidenceIds = PositionEligibilitySnapshotData.Clean(supportingExperienceEvidenceIds),
                sponsorOrRecommenderId = sponsorOrRecommenderId ?? string.Empty,
                state = state,
                reviewerOrAppointingAuthorityId = reviewerOrAppointingAuthorityId ?? string.Empty,
                decisionWorldTime = decisionWorldTime ?? string.Empty,
                decisionReason = decisionReason ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class EmploymentRecordData
    {
        public string employmentId;
        public string personId;
        public string employerOrganizationId;
        public string positionInstanceId;
        public string positionDefinitionId;
        public EmploymentClassification classification = EmploymentClassification.Permanent;
        public EmploymentState state = EmploymentState.Proposed;
        public string startWorldTime;
        public string expectedEndWorldTimeFoundation;
        public string endWorldTime;
        public string appointmentAuthorityId;
        public string supervisorPersonId;
        public string supervisorPositionInstanceId;
        public string workLocationFoundationId;
        public string[] dutyAssignmentIds = Array.Empty<string>();
        public string compensationPolicyId;
        public string paymentScheduleFoundationId;
        public string wageOrSalaryFoundationId;
        public string benefitsFoundationId;
        public string employerCostCenterFoundationId;
        public string contractTermsFoundationId;
        public string commissionOrProfitShareFoundationId;
        public string accessPolicyId;
        public string provenance;
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public EmploymentRecordData Clone()
        {
            return new EmploymentRecordData
            {
                employmentId = employmentId ?? string.Empty,
                personId = personId ?? string.Empty,
                employerOrganizationId = employerOrganizationId ?? string.Empty,
                positionInstanceId = positionInstanceId ?? string.Empty,
                positionDefinitionId = positionDefinitionId ?? string.Empty,
                classification = classification,
                state = state,
                startWorldTime = startWorldTime ?? string.Empty,
                expectedEndWorldTimeFoundation = expectedEndWorldTimeFoundation ?? string.Empty,
                endWorldTime = endWorldTime ?? string.Empty,
                appointmentAuthorityId = appointmentAuthorityId ?? string.Empty,
                supervisorPersonId = supervisorPersonId ?? string.Empty,
                supervisorPositionInstanceId = supervisorPositionInstanceId ?? string.Empty,
                workLocationFoundationId = workLocationFoundationId ?? string.Empty,
                dutyAssignmentIds = PositionEligibilitySnapshotData.Clean(dutyAssignmentIds),
                compensationPolicyId = compensationPolicyId ?? string.Empty,
                paymentScheduleFoundationId = paymentScheduleFoundationId ?? string.Empty,
                wageOrSalaryFoundationId = wageOrSalaryFoundationId ?? string.Empty,
                benefitsFoundationId = benefitsFoundationId ?? string.Empty,
                employerCostCenterFoundationId = employerCostCenterFoundationId ?? string.Empty,
                contractTermsFoundationId = contractTermsFoundationId ?? string.Empty,
                commissionOrProfitShareFoundationId = commissionOrProfitShareFoundationId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revisionHistory = PositionEligibilitySnapshotData.Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return PositionEmploymentInformationSubject.Employment(employmentId, personId, employerOrganizationId);
        }
    }

    [Serializable]
    public sealed class DutyAssignmentData
    {
        public string assignmentId;
        public string employmentId;
        public string positionInstanceId;
        public string dutyDefinitionId;
        public string assignedPersonId;
        public string startWorldTime;
        public string endWorldTime;
        public bool required = true;
        public int priority = 100;
        public string targetReferenceFoundationId;
        public DutyAssignmentState state = DutyAssignmentState.Assigned;
        public string[] completionEvidenceReferenceIds = Array.Empty<string>();
        public string delegatedToPersonId;
        public string supervisorPersonId;
        public string accessPolicyId;
        public string provenance;
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public DutyAssignmentData Clone()
        {
            return new DutyAssignmentData
            {
                assignmentId = assignmentId ?? string.Empty,
                employmentId = employmentId ?? string.Empty,
                positionInstanceId = positionInstanceId ?? string.Empty,
                dutyDefinitionId = dutyDefinitionId ?? string.Empty,
                assignedPersonId = assignedPersonId ?? string.Empty,
                startWorldTime = startWorldTime ?? string.Empty,
                endWorldTime = endWorldTime ?? string.Empty,
                required = required,
                priority = Math.Max(0, priority),
                targetReferenceFoundationId = targetReferenceFoundationId ?? string.Empty,
                state = state,
                completionEvidenceReferenceIds = PositionEligibilitySnapshotData.Clean(completionEvidenceReferenceIds),
                delegatedToPersonId = delegatedToPersonId ?? string.Empty,
                supervisorPersonId = supervisorPersonId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revisionHistory = PositionEligibilitySnapshotData.Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class PositionEmploymentRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<PositionInstanceData> positions = new List<PositionInstanceData>();
        public List<PositionApplicationData> applications = new List<PositionApplicationData>();
        public List<EmploymentRecordData> employments = new List<EmploymentRecordData>();
        public List<DutyAssignmentData> duties = new List<DutyAssignmentData>();

        public PositionEmploymentRuntimeSaveData Clone()
        {
            return new PositionEmploymentRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = Math.Max(0L, revision),
                positions = positions == null ? new List<PositionInstanceData>() : positions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                applications = applications == null ? new List<PositionApplicationData>() : applications.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                employments = employments == null ? new List<EmploymentRecordData>() : employments.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                duties = duties == null ? new List<DutyAssignmentData>() : duties.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class PositionEmploymentOperationResult
    {
        private PositionEmploymentOperationResult(bool succeeded, bool preview, bool duplicate, PositionEmploymentOperationStatus status, string message, long priorRevision, long resultingRevision, PositionEligibilityResult eligibility = null, PositionInstanceData position = null, PositionApplicationData application = null, EmploymentRecordData employment = null, DutyAssignmentData duty = null)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Status = status;
            Message = message ?? string.Empty;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
            Eligibility = eligibility;
            Position = position?.Clone();
            Application = application?.Clone();
            Employment = employment?.Clone();
            Duty = duty?.Clone();
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public PositionEmploymentOperationStatus Status { get; }
        public string Message { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public PositionEligibilityResult Eligibility { get; }
        public PositionInstanceData Position { get; }
        public PositionApplicationData Application { get; }
        public EmploymentRecordData Employment { get; }
        public DutyAssignmentData Duty { get; }

        public static PositionEmploymentOperationResult Success(string message, long priorRevision, long resultingRevision, PositionEligibilityResult eligibility = null, PositionInstanceData position = null, PositionApplicationData application = null, EmploymentRecordData employment = null, DutyAssignmentData duty = null, bool preview = false, bool duplicate = false)
        {
            return new PositionEmploymentOperationResult(true, preview, duplicate, preview ? PositionEmploymentOperationStatus.Preview : duplicate ? PositionEmploymentOperationStatus.Duplicate : PositionEmploymentOperationStatus.Succeeded, message, priorRevision, resultingRevision, eligibility, position, application, employment, duty);
        }

        public static PositionEmploymentOperationResult Failure(PositionEmploymentOperationStatus status, string message, long revision = 0L, PositionEligibilityResult eligibility = null)
        {
            return new PositionEmploymentOperationResult(false, false, false, status, message, revision, revision, eligibility);
        }
    }

    public sealed class PositionEmploymentProjection<TRecord>
    {
        public PositionEmploymentProjection(TRecord record, PositionEmploymentProjectionAudience audience, InformationAccessDecision decision, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields)
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
        public PositionEmploymentProjectionAudience Audience { get; }
        public InformationAccessDecision Decision { get; }
        public bool Redacted { get; }
        public bool Denied { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    [Serializable]
    public sealed class PositionEmploymentHistoryHookData
    {
        public PositionEmploymentHistoryHookKind kind;
        public string positionInstanceId;
        public string applicationId;
        public string employmentId;
        public string dutyAssignmentId;
        public string personId;
        public string organizationId;
        public string authorityId;
        public string worldTime;
        public string transactionId;

        public PositionEmploymentHistoryHookData Clone()
        {
            return new PositionEmploymentHistoryHookData
            {
                kind = kind,
                positionInstanceId = positionInstanceId ?? string.Empty,
                applicationId = applicationId ?? string.Empty,
                employmentId = employmentId ?? string.Empty,
                dutyAssignmentId = dutyAssignmentId ?? string.Empty,
                personId = personId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            };
        }
    }

    public static class PositionEmploymentInformationSubject
    {
        public const string PositionDefinitionTag = "subject.profession.position-definition";
        public const string PositionInstanceTag = "subject.profession.position-instance";
        public const string VacancyTag = "subject.profession.position-vacancy";
        public const string EmploymentTag = "subject.profession.employment";
        public const string PositionApplicationTag = "subject.profession.position-application";
        public const string OfferTag = "subject.profession.position-offer";
        public const string AppointmentTag = "subject.profession.appointment";
        public const string DutyDefinitionTag = "subject.profession.duty-definition";
        public const string DutyAssignmentTag = "subject.profession.duty-assignment";
        public const string ReportingRelationshipTag = "subject.profession.reporting-relationship";
        public const string SuspensionTag = "subject.profession.employment-suspension";
        public const string ResignationTag = "subject.profession.employment-resignation";
        public const string DismissalTag = "subject.profession.employment-dismissal";
        public const string RetirementTag = "subject.profession.employment-retirement";
        public const string DisputeTag = "subject.profession.employment-dispute";

        public static readonly string[] ProtectedFields =
        {
            "applicant",
            "employee",
            "authority",
            "discipline",
            "secret-position",
            "confidential-duty",
            "rejection-reason",
            "supervisor",
            "reporting-structure",
            "provenance",
            "compensation-foundation"
        };

        public static InformationSubjectReferenceData PositionDefinition(string positionDefinitionId, string organizationId = "")
        {
            return Create(PositionDefinitionTag, positionDefinitionId, organizationId, string.Empty);
        }

        public static InformationSubjectReferenceData PositionInstance(string positionInstanceId, string organizationId = "")
        {
            return Create(PositionInstanceTag, positionInstanceId, organizationId, string.Empty);
        }

        public static InformationSubjectReferenceData Employment(string employmentId, string personId, string organizationId = "")
        {
            return Create(EmploymentTag, employmentId, organizationId, personId);
        }

        public static InformationSubjectReferenceData DutyAssignment(string assignmentId, string personId, string organizationId = "")
        {
            return Create(DutyAssignmentTag, assignmentId, organizationId, personId);
        }

        private static InformationSubjectReferenceData Create(string tag, string subjectId, string parentSubjectId, string ownerPersonId)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                tags = PositionEligibilitySnapshotData.Clean(new[] { tag })
            };
        }
    }
}
