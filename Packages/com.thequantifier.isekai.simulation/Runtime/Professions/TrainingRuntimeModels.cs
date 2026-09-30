using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.Sharing;

namespace UnityIsekaiGame.Professions
{
    [Serializable]
    public sealed class TrainingInstructorAssignmentData
    {
        public string assignmentId;
        public string enrollmentId;
        public TrainingInstructorRoleKind role;
        public string personId;
        public string assignedWorldTime;
        public string professionId;
        public string specializationId;
        public string authorityId;
        public int maximumLearnerCapacity = 1;
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public TrainingInstructorAssignmentData Clone()
        {
            return new TrainingInstructorAssignmentData
            {
                assignmentId = assignmentId ?? string.Empty,
                enrollmentId = enrollmentId ?? string.Empty,
                role = role,
                personId = personId ?? string.Empty,
                assignedWorldTime = assignedWorldTime ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                maximumLearnerCapacity = Math.Max(0, maximumLearnerCapacity),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = Math.Max(0L, revision)
            };
        }
    }

    [Serializable]
    public sealed class TrainingLearningSessionData
    {
        public string sessionId;
        public string enrollmentId;
        public string programId;
        public string moduleId;
        public string lessonId;
        public string[] instructorIds;
        public string[] learnerIds;
        public TrainingTeachingMethod teachingMethod;
        public string[] sourceOrRecordIds;
        public string startWorldTime;
        public string completionWorldTime;
        public bool attended;
        public TrainingSessionCompletionState state;
        public string transferId;
        public string[] observations;
        public string[] practicePerformedIds;
        public string[] evidenceIds;
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public TrainingLearningSessionData Clone()
        {
            return new TrainingLearningSessionData
            {
                sessionId = sessionId ?? string.Empty,
                enrollmentId = enrollmentId ?? string.Empty,
                programId = programId ?? string.Empty,
                moduleId = moduleId ?? string.Empty,
                lessonId = lessonId ?? string.Empty,
                instructorIds = Clean(instructorIds),
                learnerIds = Clean(learnerIds),
                teachingMethod = teachingMethod,
                sourceOrRecordIds = Clean(sourceOrRecordIds),
                startWorldTime = startWorldTime ?? string.Empty,
                completionWorldTime = completionWorldTime ?? string.Empty,
                attended = attended,
                state = state,
                transferId = transferId ?? string.Empty,
                observations = Clean(observations),
                practicePerformedIds = Clean(practicePerformedIds),
                evidenceIds = Clean(evidenceIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = Math.Max(0L, revision)
            };
        }

        internal static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class TrainingPracticalWorkRecordData
    {
        public string recordId;
        public string enrollmentId;
        public string moduleId;
        public string assignmentId;
        public TrainingAssignmentActivityCategory activityCategory;
        public string activityReferenceId;
        public string professionId;
        public int quantity;
        public int quality;
        public bool successful;
        public string supervisorPersonId;
        public bool accepted;
        public string worldTime;
        public string[] evidenceIds;
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public TrainingPracticalWorkRecordData Clone()
        {
            return new TrainingPracticalWorkRecordData
            {
                recordId = recordId ?? string.Empty,
                enrollmentId = enrollmentId ?? string.Empty,
                moduleId = moduleId ?? string.Empty,
                assignmentId = assignmentId ?? string.Empty,
                activityCategory = activityCategory,
                activityReferenceId = activityReferenceId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                quantity = Math.Max(0, quantity),
                quality = Math.Max(0, quality),
                successful = successful,
                supervisorPersonId = supervisorPersonId ?? string.Empty,
                accepted = accepted,
                worldTime = worldTime ?? string.Empty,
                evidenceIds = TrainingLearningSessionData.Clean(evidenceIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = Math.Max(0L, revision)
            };
        }
    }

    [Serializable]
    public sealed class TrainingSupervisedWorkRecordData
    {
        public string recordId;
        public string enrollmentId;
        public string learnerPersonId;
        public string supervisorPersonId;
        public string professionId;
        public string activityReferenceId;
        public string startWorldTime;
        public string completionWorldTime;
        public TrainingSupervisionLevel supervisionLevel;
        public string learnerResponsibility;
        public string supervisorParticipation;
        public TrainingWorkOutcome outcome;
        public int quality;
        public string[] mistakesOrIncidentIds;
        public string[] evidenceIds;
        public string evaluationSummary;
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public TrainingSupervisedWorkRecordData Clone()
        {
            return new TrainingSupervisedWorkRecordData
            {
                recordId = recordId ?? string.Empty,
                enrollmentId = enrollmentId ?? string.Empty,
                learnerPersonId = learnerPersonId ?? string.Empty,
                supervisorPersonId = supervisorPersonId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                activityReferenceId = activityReferenceId ?? string.Empty,
                startWorldTime = startWorldTime ?? string.Empty,
                completionWorldTime = completionWorldTime ?? string.Empty,
                supervisionLevel = supervisionLevel,
                learnerResponsibility = learnerResponsibility ?? string.Empty,
                supervisorParticipation = supervisorParticipation ?? string.Empty,
                outcome = outcome,
                quality = Math.Max(0, quality),
                mistakesOrIncidentIds = TrainingLearningSessionData.Clean(mistakesOrIncidentIds),
                evidenceIds = TrainingLearningSessionData.Clean(evidenceIds),
                evaluationSummary = evaluationSummary ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = Math.Max(0L, revision)
            };
        }
    }

    [Serializable]
    public sealed class TrainingEnrollmentData
    {
        public string enrollmentId;
        public string personId;
        public string programId;
        public string relatedProfessionId;
        public string relatedSpecializationId;
        public string institutionOrOrganizationId;
        public string[] instructorAssignmentIds;
        public string masterPersonId;
        public string workplaceOrStationId;
        public string[] requiredWorkCategoryIds;
        public string startWorldTime;
        public string expectedCompletionTime;
        public string completionWorldTime;
        public TrainingEnrollmentState state = TrainingEnrollmentState.Applied;
        public string[] completedModuleIds;
        public string[] activeModuleIds;
        public string[] failedModuleIds;
        public string[] lessonSessionIds;
        public string[] practicalWorkRecordIds;
        public string[] supervisedWorkRecordIds;
        public string progressSummary;
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public TrainingEnrollmentData Clone()
        {
            return new TrainingEnrollmentData
            {
                enrollmentId = enrollmentId ?? string.Empty,
                personId = personId ?? string.Empty,
                programId = programId ?? string.Empty,
                relatedProfessionId = relatedProfessionId ?? string.Empty,
                relatedSpecializationId = relatedSpecializationId ?? string.Empty,
                institutionOrOrganizationId = institutionOrOrganizationId ?? string.Empty,
                instructorAssignmentIds = TrainingLearningSessionData.Clean(instructorAssignmentIds),
                masterPersonId = masterPersonId ?? string.Empty,
                workplaceOrStationId = workplaceOrStationId ?? string.Empty,
                requiredWorkCategoryIds = TrainingLearningSessionData.Clean(requiredWorkCategoryIds),
                startWorldTime = startWorldTime ?? string.Empty,
                expectedCompletionTime = expectedCompletionTime ?? string.Empty,
                completionWorldTime = completionWorldTime ?? string.Empty,
                state = state,
                completedModuleIds = TrainingLearningSessionData.Clean(completedModuleIds),
                activeModuleIds = TrainingLearningSessionData.Clean(activeModuleIds),
                failedModuleIds = TrainingLearningSessionData.Clean(failedModuleIds),
                lessonSessionIds = TrainingLearningSessionData.Clean(lessonSessionIds),
                practicalWorkRecordIds = TrainingLearningSessionData.Clean(practicalWorkRecordIds),
                supervisedWorkRecordIds = TrainingLearningSessionData.Clean(supervisedWorkRecordIds),
                progressSummary = progressSummary ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = Math.Max(0L, revision)
            };
        }
    }

    [Serializable]
    public sealed class TrainingProgressTokenData
    {
        public long trainingRevision;
        public long professionRevision;
        public long transferRevision;
        public string contextHash;

        public TrainingProgressTokenData Clone()
        {
            return new TrainingProgressTokenData
            {
                trainingRevision = trainingRevision,
                professionRevision = professionRevision,
                transferRevision = transferRevision,
                contextHash = contextHash ?? string.Empty
            };
        }

        public bool SemanticallyEquals(TrainingProgressTokenData other)
        {
            return other != null
                && trainingRevision == other.trainingRevision
                && professionRevision == other.professionRevision
                && transferRevision == other.transferRevision
                && string.Equals(contextHash ?? string.Empty, other.contextHash ?? string.Empty, StringComparison.Ordinal);
        }
    }

    public sealed class TrainingProgressResult
    {
        public TrainingProgressResult(string enrollmentId, int percentage, IEnumerable<string> completed, IEnumerable<string> remaining, IEnumerable<string> failed, IEnumerable<string> blockers, bool eligible, bool perceived, TrainingProgressTokenData token, long revision, string diagnostics)
        {
            EnrollmentId = enrollmentId ?? string.Empty;
            Percentage = Math.Max(0, Math.Min(100, percentage));
            CompletedRequirements = TrainingLearningSessionData.Clean(completed);
            RemainingRequirements = TrainingLearningSessionData.Clean(remaining);
            FailedRequirements = TrainingLearningSessionData.Clean(failed);
            BlockingIssues = TrainingLearningSessionData.Clean(blockers);
            EligibleForCompletion = eligible;
            Perceived = perceived;
            RuntimeToken = token?.Clone();
            Revision = revision;
            Diagnostics = diagnostics ?? string.Empty;
        }

        public string EnrollmentId { get; }
        public int Percentage { get; }
        public IReadOnlyList<string> CompletedRequirements { get; }
        public IReadOnlyList<string> RemainingRequirements { get; }
        public IReadOnlyList<string> FailedRequirements { get; }
        public IReadOnlyList<string> BlockingIssues { get; }
        public bool EligibleForCompletion { get; }
        public bool Perceived { get; }
        public TrainingProgressTokenData RuntimeToken { get; }
        public long Revision { get; }
        public string Diagnostics { get; }
    }

    [Serializable]
    public sealed class TrainingRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<TrainingEnrollmentData> enrollments = new List<TrainingEnrollmentData>();
        public List<TrainingInstructorAssignmentData> instructorAssignments = new List<TrainingInstructorAssignmentData>();
        public List<TrainingLearningSessionData> learningSessions = new List<TrainingLearningSessionData>();
        public List<TrainingPracticalWorkRecordData> practicalWorkRecords = new List<TrainingPracticalWorkRecordData>();
        public List<TrainingSupervisedWorkRecordData> supervisedWorkRecords = new List<TrainingSupervisedWorkRecordData>();

        public TrainingRuntimeSaveData Clone()
        {
            return new TrainingRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                enrollments = enrollments == null ? new List<TrainingEnrollmentData>() : enrollments.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                instructorAssignments = instructorAssignments == null ? new List<TrainingInstructorAssignmentData>() : instructorAssignments.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                learningSessions = learningSessions == null ? new List<TrainingLearningSessionData>() : learningSessions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                practicalWorkRecords = practicalWorkRecords == null ? new List<TrainingPracticalWorkRecordData>() : practicalWorkRecords.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                supervisedWorkRecords = supervisedWorkRecords == null ? new List<TrainingSupervisedWorkRecordData>() : supervisedWorkRecords.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class TrainingEnrollmentSnapshot
    {
        public TrainingEnrollmentSnapshot(TrainingEnrollmentData data)
        {
            Data = data?.Clone() ?? new TrainingEnrollmentData();
        }

        public TrainingEnrollmentData Data { get; }
        public string EnrollmentId => Data.enrollmentId ?? string.Empty;
        public string PersonId => Data.personId ?? string.Empty;
        public string ProgramId => Data.programId ?? string.Empty;
        public string RelatedProfessionId => Data.relatedProfessionId ?? string.Empty;
        public TrainingEnrollmentState State => Data.state;
        public long Revision => Data.revision;
    }

    public sealed class TrainingProjection<TRecord>
    {
        public TrainingProjection(TRecord record, TrainingProjectionAudience audience, InformationAccessDecision decision, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields)
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
        public TrainingProjectionAudience Audience { get; }
        public InformationAccessDecision Decision { get; }
        public bool Redacted { get; }
        public bool Denied { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    public sealed class TrainingOperationResult
    {
        private TrainingOperationResult(bool succeeded, bool preview, bool duplicate, TrainingOperationStatus status, string message, long priorRevision, long resultingRevision, TrainingEnrollmentSnapshot enrollment = null, TrainingProgressResult progress = null, InformationTransferResult transfer = null)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Status = status;
            Message = message ?? string.Empty;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
            Enrollment = enrollment;
            Progress = progress;
            Transfer = transfer;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public TrainingOperationStatus Status { get; }
        public string Message { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public TrainingEnrollmentSnapshot Enrollment { get; }
        public TrainingProgressResult Progress { get; }
        public InformationTransferResult Transfer { get; }

        public static TrainingOperationResult Success(string message, long priorRevision, long resultingRevision, TrainingEnrollmentSnapshot enrollment = null, TrainingProgressResult progress = null, InformationTransferResult transfer = null, bool preview = false, bool duplicate = false)
        {
            return new TrainingOperationResult(true, preview, duplicate, preview ? TrainingOperationStatus.Preview : duplicate ? TrainingOperationStatus.Duplicate : TrainingOperationStatus.Succeeded, message, priorRevision, resultingRevision, enrollment, progress, transfer);
        }

        public static TrainingOperationResult Failure(TrainingOperationStatus status, string message, long revision = 0L, TrainingProgressResult progress = null, InformationTransferResult transfer = null)
        {
            return new TrainingOperationResult(false, false, false, status, message, revision, revision, null, progress, transfer);
        }
    }

    public sealed class TrainingHistoryHookData
    {
        public TrainingHistoryHookKind kind;
        public string enrollmentId;
        public string personId;
        public string programId;
        public string relatedId;
        public string worldTime;
        public string transactionId;

        public TrainingHistoryHookData Clone()
        {
            return new TrainingHistoryHookData
            {
                kind = kind,
                enrollmentId = enrollmentId ?? string.Empty,
                personId = personId ?? string.Empty,
                programId = programId ?? string.Empty,
                relatedId = relatedId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            };
        }
    }

    public static class TrainingInformationSubject
    {
        public const string ProgramTag = "subject-type:training-program";
        public const string CurriculumTag = "subject-type:training-curriculum";
        public const string ModuleTag = "subject-type:training-module";
        public const string LessonTag = "subject-type:training-lesson";
        public const string EnrollmentTag = "subject-type:training-enrollment";
        public const string ApprenticeshipTag = "subject-type:training-apprenticeship";
        public const string InstructorAssignmentTag = "subject-type:training-instructor-assignment";
        public const string SessionTag = "subject-type:training-learning-session";
        public const string PracticalAssignmentTag = "subject-type:training-practical-assignment";
        public const string SupervisedWorkTag = "subject-type:training-supervised-work";
        public const string ProgressTag = "subject-type:training-progress";
        public const string DecisionTag = "subject-type:training-decision";

        public static readonly string[] ProtectedFields =
        {
            "person-id",
            "instructor-ids",
            "hidden-requirements",
            "progress-token",
            "provenance"
        };

        public static InformationSubjectReferenceData Create(string tag, string subjectId, string ownerPersonId, string parentSubjectId = "")
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                tags = new[] { "domain.profession", "domain.training", tag ?? string.Empty }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            };
        }
    }
}
