using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [Serializable]
    public sealed class ProfessionalActivitySourceReferenceData
    {
        public ProfessionalActivitySourceType sourceType = ProfessionalActivitySourceType.Custom;
        public string sourceId;
        public string parentSourceId;
        public long sourceRevision;

        public ProfessionalActivitySourceReferenceData Clone()
        {
            return new ProfessionalActivitySourceReferenceData
            {
                sourceType = sourceType,
                sourceId = sourceId ?? string.Empty,
                parentSourceId = parentSourceId ?? string.Empty,
                sourceRevision = Math.Max(0L, sourceRevision)
            };
        }

        public string Signature => $"{sourceType}:{sourceId ?? string.Empty}:{parentSourceId ?? string.Empty}";
    }

    public sealed class ProfessionalActivitySourceSnapshot
    {
        public ProfessionalActivitySourceSnapshot(
            ProfessionalActivitySourceReferenceData reference,
            string actingPersonId,
            string worldTime,
            ProfessionalActivityOutcomeState outcome,
            float quantityOrDuration,
            ProfessionalActivityDifficulty difficulty,
            int quality,
            IEnumerable<string> tags,
            IEnumerable<string> relatedSubjectIds,
            bool completed,
            bool revoked,
            bool accessAllowed,
            string diagnostics)
        {
            Reference = reference?.Clone() ?? new ProfessionalActivitySourceReferenceData();
            ActingPersonId = actingPersonId ?? string.Empty;
            WorldTime = worldTime ?? string.Empty;
            Outcome = outcome;
            QuantityOrDuration = Math.Max(0f, quantityOrDuration);
            Difficulty = difficulty;
            Quality = Math.Max(0, Math.Min(1000, quality));
            Tags = Clean(tags);
            RelatedSubjectIds = Clean(relatedSubjectIds);
            Completed = completed;
            Revoked = revoked;
            AccessAllowed = accessAllowed;
            Diagnostics = diagnostics ?? string.Empty;
        }

        public ProfessionalActivitySourceReferenceData Reference { get; }
        public string ActingPersonId { get; }
        public string WorldTime { get; }
        public ProfessionalActivityOutcomeState Outcome { get; }
        public float QuantityOrDuration { get; }
        public ProfessionalActivityDifficulty Difficulty { get; }
        public int Quality { get; }
        public IReadOnlyList<string> Tags { get; }
        public IReadOnlyList<string> RelatedSubjectIds { get; }
        public bool Completed { get; }
        public bool Revoked { get; }
        public bool AccessAllowed { get; }
        public string Diagnostics { get; }

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

    [Serializable]
    public sealed class ProfessionalActivityRecordData
    {
        public string activityId;
        public string personId;
        public string professionId;
        public string specializationId;
        public string activityDefinitionId;
        public ProfessionalActivitySourceReferenceData source = new ProfessionalActivitySourceReferenceData();
        public ProfessionalActivityCategory category = ProfessionalActivityCategory.Custom;
        public string startWorldTime;
        public string completionWorldTime;
        public ProfessionalActivityState state = ProfessionalActivityState.Proposed;
        public ProfessionalActivityOutcomeState outcome = ProfessionalActivityOutcomeState.Unknown;
        public TrainingSupervisionLevel supervisionLevel = TrainingSupervisionLevel.Custom;
        public string[] supervisorOrInstructorIds = Array.Empty<string>();
        public ProfessionalResponsibilityLevel responsibility = ProfessionalResponsibilityLevel.Observer;
        public float quantityOrDuration;
        public ProfessionalActivityDifficulty difficulty = ProfessionalActivityDifficulty.Unknown;
        public int quality;
        public string outcomeSummary;
        public string[] relatedItemIds = Array.Empty<string>();
        public string[] relatedTargetIds = Array.Empty<string>();
        public string[] relatedOrganizationIds = Array.Empty<string>();
        public string locationId;
        public string jobId;
        public string batchId;
        public string experimentId;
        public string[] evidenceReferenceIds = Array.Empty<string>();
        public string repetitionSignature;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public ProfessionalActivityRecordData Clone()
        {
            return new ProfessionalActivityRecordData
            {
                activityId = activityId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                activityDefinitionId = activityDefinitionId ?? string.Empty,
                source = source?.Clone() ?? new ProfessionalActivitySourceReferenceData(),
                category = category,
                startWorldTime = startWorldTime ?? string.Empty,
                completionWorldTime = completionWorldTime ?? string.Empty,
                state = state,
                outcome = outcome,
                supervisionLevel = supervisionLevel,
                supervisorOrInstructorIds = ProfessionalActivitySourceSnapshot.Clean(supervisorOrInstructorIds),
                responsibility = responsibility,
                quantityOrDuration = Math.Max(0f, quantityOrDuration),
                difficulty = difficulty,
                quality = Math.Max(0, Math.Min(1000, quality)),
                outcomeSummary = outcomeSummary ?? string.Empty,
                relatedItemIds = ProfessionalActivitySourceSnapshot.Clean(relatedItemIds),
                relatedTargetIds = ProfessionalActivitySourceSnapshot.Clean(relatedTargetIds),
                relatedOrganizationIds = ProfessionalActivitySourceSnapshot.Clean(relatedOrganizationIds),
                locationId = locationId ?? string.Empty,
                jobId = jobId ?? string.Empty,
                batchId = batchId ?? string.Empty,
                experimentId = experimentId ?? string.Empty,
                evidenceReferenceIds = ProfessionalActivitySourceSnapshot.Clean(evidenceReferenceIds),
                repetitionSignature = repetitionSignature ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProfessionalExperienceEvidenceData
    {
        public string evidenceId;
        public string activityId;
        public string personId;
        public string professionId;
        public string specializationId;
        public ProfessionalActivitySourceReferenceData source = new ProfessionalActivitySourceReferenceData();
        public ProfessionalExperienceCategory category = ProfessionalExperienceCategory.RoutineWork;
        public float quantityOrDuration;
        public ProfessionalActivityDifficulty difficulty = ProfessionalActivityDifficulty.Unknown;
        public int quality;
        public TrainingSupervisionLevel supervisionLevel = TrainingSupervisionLevel.Custom;
        public ProfessionalResponsibilityLevel responsibility = ProfessionalResponsibilityLevel.Observer;
        public ProfessionalActivityOutcomeState outcome = ProfessionalActivityOutcomeState.Unknown;
        public string noveltyClassification;
        public string repetitionGroup;
        public string validationAuthorityOrPolicyId;
        public string validationWorldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public ProfessionalExperienceEvidenceData Clone()
        {
            return new ProfessionalExperienceEvidenceData
            {
                evidenceId = evidenceId ?? string.Empty,
                activityId = activityId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                source = source?.Clone() ?? new ProfessionalActivitySourceReferenceData(),
                category = category,
                quantityOrDuration = Math.Max(0f, quantityOrDuration),
                difficulty = difficulty,
                quality = Math.Max(0, Math.Min(1000, quality)),
                supervisionLevel = supervisionLevel,
                responsibility = responsibility,
                outcome = outcome,
                noveltyClassification = noveltyClassification ?? string.Empty,
                repetitionGroup = repetitionGroup ?? string.Empty,
                validationAuthorityOrPolicyId = validationAuthorityOrPolicyId ?? string.Empty,
                validationWorldTime = validationWorldTime ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    public sealed class ProfessionalExperienceSummary
    {
        public ProfessionalExperienceSummary(
            string personId,
            string professionId,
            IEnumerable<ProfessionalExperienceEvidenceData> evidence,
            long runtimeRevision,
            string diagnostics,
            bool redacted = false)
        {
            PersonId = personId ?? string.Empty;
            ProfessionId = professionId ?? string.Empty;
            Evidence = (evidence ?? Array.Empty<ProfessionalExperienceEvidenceData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.validationWorldTime, StringComparer.Ordinal).ThenBy(item => item.evidenceId, StringComparer.Ordinal).ToArray();
            RuntimeRevision = runtimeRevision;
            Diagnostics = diagnostics ?? string.Empty;
            Redacted = redacted;
            TotalValidatedActivities = Evidence.Count;
            FirstActivityWorldTime = Evidence.FirstOrDefault()?.validationWorldTime ?? string.Empty;
            MostRecentActivityWorldTime = Evidence.LastOrDefault()?.validationWorldTime ?? string.Empty;
            ActivitiesByCategory = CountBy(Evidence.Select(item => item.category.ToString()));
            ActivitiesBySpecialization = CountBy(Evidence.Select(item => item.specializationId).Where(value => !string.IsNullOrWhiteSpace(value)));
            SupervisedCount = Evidence.Count(item => item.supervisionLevel == TrainingSupervisionLevel.CloselySupervised || item.supervisionLevel == TrainingSupervisionLevel.PeriodicallySupervised || item.responsibility == ProfessionalResponsibilityLevel.SupervisedWorker);
            IndependentCount = Evidence.Count(item => item.responsibility == ProfessionalResponsibilityLevel.IndependentPractitioner || item.responsibility == ProfessionalResponsibilityLevel.IndependentWithReview);
            SuccessfulCount = Evidence.Count(item => item.outcome == ProfessionalActivityOutcomeState.Successful || item.outcome == ProfessionalActivityOutcomeState.PartialSuccess || item.outcome == ProfessionalActivityOutcomeState.Innovative);
            FailedCount = Evidence.Count(item => item.outcome == ProfessionalActivityOutcomeState.Failed || item.outcome == ProfessionalActivityOutcomeState.DangerousMistake || item.category == ProfessionalExperienceCategory.FailedAttempt);
            TeachingCount = Evidence.Count(item => item.category == ProfessionalExperienceCategory.Teaching || item.responsibility == ProfessionalResponsibilityLevel.Instructor);
            LeadershipCount = Evidence.Count(item => item.category == ProfessionalExperienceCategory.Leadership || item.responsibility == ProfessionalResponsibilityLevel.Leader || item.responsibility == ProfessionalResponsibilityLevel.Supervisor);
            ResearchCount = Evidence.Count(item => item.category == ProfessionalExperienceCategory.Research || item.category == ProfessionalExperienceCategory.Innovation);
            QualityDistribution = CountBy(Evidence.Select(item => QualityBucket(item.quality)));
            DifficultyDistribution = CountBy(Evidence.Select(item => item.difficulty.ToString()));
            RepresentativeSourceReferences = Evidence.Select(item => item.source?.Signature ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(8).ToArray();
            BreadthScore = ActivitiesByCategory.Count + ActivitiesBySpecialization.Count + Evidence.Select(item => item.source?.sourceType ?? ProfessionalActivitySourceType.Custom).Distinct().Count();
            DepthScore = Evidence.GroupBy(item => item.repetitionGroup ?? string.Empty, StringComparer.Ordinal).Select(group => group.Count()).DefaultIfEmpty(0).Max();
            RecencyScore = string.IsNullOrWhiteSpace(MostRecentActivityWorldTime) ? 0 : 1;
            ConsistencyScore = Evidence.GroupBy(item => item.repetitionGroup ?? item.category.ToString(), StringComparer.Ordinal).Count(group => group.Count() > 1);
        }

        public string PersonId { get; }
        public string ProfessionId { get; }
        public IReadOnlyList<ProfessionalExperienceEvidenceData> Evidence { get; }
        public int TotalValidatedActivities { get; }
        public string FirstActivityWorldTime { get; }
        public string MostRecentActivityWorldTime { get; }
        public IReadOnlyDictionary<string, int> ActivitiesByCategory { get; }
        public IReadOnlyDictionary<string, int> ActivitiesBySpecialization { get; }
        public int SupervisedCount { get; }
        public int IndependentCount { get; }
        public int SuccessfulCount { get; }
        public int FailedCount { get; }
        public int TeachingCount { get; }
        public int LeadershipCount { get; }
        public int ResearchCount { get; }
        public IReadOnlyDictionary<string, int> QualityDistribution { get; }
        public IReadOnlyDictionary<string, int> DifficultyDistribution { get; }
        public IReadOnlyList<string> RepresentativeSourceReferences { get; }
        public int BreadthScore { get; }
        public int DepthScore { get; }
        public int RecencyScore { get; }
        public int ConsistencyScore { get; }
        public long RuntimeRevision { get; }
        public string Diagnostics { get; }
        public bool Redacted { get; }

        private static IReadOnlyDictionary<string, int> CountBy(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        }

        private static string QualityBucket(int quality)
        {
            if (quality >= 850) return "High";
            if (quality >= 500) return "Adequate";
            if (quality > 0) return "Low";
            return "Unknown";
        }
    }

    [Serializable]
    public sealed class ProfessionalActivityRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<ProfessionalActivityRecordData> activities = new List<ProfessionalActivityRecordData>();
        public List<ProfessionalExperienceEvidenceData> evidence = new List<ProfessionalExperienceEvidenceData>();

        public ProfessionalActivityRuntimeSaveData Clone()
        {
            return new ProfessionalActivityRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = Math.Max(0L, revision),
                activities = activities == null ? new List<ProfessionalActivityRecordData>() : activities.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                evidence = evidence == null ? new List<ProfessionalExperienceEvidenceData>() : evidence.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class ProfessionalActivityOperationResult
    {
        private ProfessionalActivityOperationResult(bool succeeded, bool preview, bool duplicate, ProfessionalActivityOperationStatus status, string message, long priorRevision, long resultingRevision, ProfessionalActivityRecordData activity = null, ProfessionalExperienceEvidenceData evidence = null, ProfessionalExperienceSummary summary = null)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Status = status;
            Message = message ?? string.Empty;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
            Activity = activity?.Clone();
            Evidence = evidence?.Clone();
            Summary = summary;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public ProfessionalActivityOperationStatus Status { get; }
        public string Message { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public ProfessionalActivityRecordData Activity { get; }
        public ProfessionalExperienceEvidenceData Evidence { get; }
        public ProfessionalExperienceSummary Summary { get; }

        public static ProfessionalActivityOperationResult Success(string message, long priorRevision, long resultingRevision, ProfessionalActivityRecordData activity = null, ProfessionalExperienceEvidenceData evidence = null, ProfessionalExperienceSummary summary = null, bool preview = false, bool duplicate = false)
        {
            return new ProfessionalActivityOperationResult(true, preview, duplicate, preview ? ProfessionalActivityOperationStatus.Preview : duplicate ? ProfessionalActivityOperationStatus.Duplicate : ProfessionalActivityOperationStatus.Succeeded, message, priorRevision, resultingRevision, activity, evidence, summary);
        }

        public static ProfessionalActivityOperationResult Failure(ProfessionalActivityOperationStatus status, string message, long revision = 0L)
        {
            return new ProfessionalActivityOperationResult(false, false, false, status, message, revision, revision);
        }
    }

    public sealed class ProfessionalActivityValidationResult
    {
        public ProfessionalActivityValidationResult(bool valid, ProfessionalActivityOperationStatus status, IEnumerable<string> diagnostics, ProfessionalActivityRecordData proposedActivity, ProfessionalActivitySourceSnapshot source, long runtimeRevision)
        {
            Valid = valid;
            Status = status;
            Diagnostics = ProfessionalActivitySourceSnapshot.Clean(diagnostics);
            ProposedActivity = proposedActivity?.Clone();
            Source = source;
            RuntimeRevision = runtimeRevision;
        }

        public bool Valid { get; }
        public ProfessionalActivityOperationStatus Status { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public ProfessionalActivityRecordData ProposedActivity { get; }
        public ProfessionalActivitySourceSnapshot Source { get; }
        public long RuntimeRevision { get; }
    }

    public sealed class ProfessionalActivityProjection<TRecord>
    {
        public ProfessionalActivityProjection(TRecord record, ProfessionalActivityProjectionAudience audience, InformationAccessDecision decision, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields)
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
        public ProfessionalActivityProjectionAudience Audience { get; }
        public InformationAccessDecision Decision { get; }
        public bool Redacted { get; }
        public bool Denied { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    [Serializable]
    public sealed class ProfessionalExperienceRequirementData
    {
        public string professionId;
        public string specializationId;
        public ProfessionalExperienceCategory requiredCategory = ProfessionalExperienceCategory.Custom;
        public int minimumValidatedActivities;
        public int minimumIndependentActivities;
        public int minimumSupervisedActivities;
        public ProfessionalActivityDifficulty minimumDifficulty = ProfessionalActivityDifficulty.Unknown;
        public int minimumQuality;
        public bool requireRecentActivity;
    }

    public sealed class ProfessionalActivityHistoryHookData
    {
        public ProfessionalActivityHistoryHookKind kind;
        public string activityId;
        public string evidenceId;
        public string personId;
        public string professionId;
        public string worldTime;
        public string transactionId;

        public ProfessionalActivityHistoryHookData Clone()
        {
            return new ProfessionalActivityHistoryHookData
            {
                kind = kind,
                activityId = activityId ?? string.Empty,
                evidenceId = evidenceId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            };
        }
    }

    public static class ProfessionalActivityInformationSubject
    {
        public const string ActivityTag = "subject-type:professional-activity";
        public const string EvidenceTag = "subject-type:professional-experience-evidence";
        public const string SummaryTag = "subject-type:professional-experience-summary";
        public const string ValidationTag = "subject-type:professional-activity-validation";
        public const string DisputeTag = "subject-type:professional-activity-dispute";
        public const string PortfolioTag = "subject-type:professional-portfolio";

        public static readonly string[] ProtectedFields =
        {
            "person-id",
            "source-id",
            "client-id",
            "secret-profession",
            "confidential-detail",
            "private-failure",
            "progression-token",
            "provenance"
        };

        public static InformationSubjectReferenceData Create(string tag, string subjectId, string ownerPersonId, string parentSubjectId = "", IEnumerable<string> tags = null)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                tags = (tags ?? Array.Empty<string>())
                    .Concat(new[] { "domain.profession", "domain.professional-activity", tag ?? string.Empty })
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            };
        }
    }
}
