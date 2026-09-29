using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [Serializable]
    public sealed class CareerSourceRecordReferenceData
    {
        public CareerTransitionSourceRecordType recordType;
        public string recordId;
        public long sourceRevision;

        public CareerSourceRecordReferenceData Clone()
        {
            return new CareerSourceRecordReferenceData
            {
                recordType = recordType,
                recordId = recordId ?? string.Empty,
                sourceRevision = Math.Max(0L, sourceRevision)
            };
        }

        public string Signature => $"{recordType}:{recordId ?? string.Empty}:{Math.Max(0L, sourceRevision)}";
    }

    [Serializable]
    public sealed partial class CareerEpisodeData
    {
        public string episodeId;
        public string personId;
        public CareerEpisodeCategory category = CareerEpisodeCategory.Custom;
        public string professionId;
        public string specializationId;
        public string employmentId;
        public string positionInstanceId;
        public string organizationId;
        public string trainingEnrollmentId;
        public string rankRecordId;
        public string rankDefinitionId;
        public string credentialId;
        public string credentialDefinitionId;
        public string startWorldTime;
        public string endWorldTime;
        public CareerEpisodeState state = CareerEpisodeState.Planned;
        public CareerClassification careerClassification = CareerClassification.Secondary;
        public EmploymentClassification workClassification = EmploymentClassification.Custom;
        public string locationFoundationId;
        public string reasonStarted;
        public string reasonEnded;
        public bool primaryCareer;
        public bool exclusiveCareer;
        public bool secret;
        public bool disputed;
        public string accessPolicyId;
        public string provenance;
        public CareerSourceRecordReferenceData[] sourceRecords = Array.Empty<CareerSourceRecordReferenceData>();
        public string[] transitionIds = Array.Empty<string>();
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public CareerEpisodeData Clone()
        {
            return new CareerEpisodeData
            {
                episodeId = episodeId ?? string.Empty,
                personId = personId ?? string.Empty,
                category = category,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                employmentId = employmentId ?? string.Empty,
                positionInstanceId = positionInstanceId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                trainingEnrollmentId = trainingEnrollmentId ?? string.Empty,
                rankRecordId = rankRecordId ?? string.Empty,
                rankDefinitionId = rankDefinitionId ?? string.Empty,
                credentialId = credentialId ?? string.Empty,
                credentialDefinitionId = credentialDefinitionId ?? string.Empty,
                startWorldTime = startWorldTime ?? string.Empty,
                endWorldTime = endWorldTime ?? string.Empty,
                state = state,
                careerClassification = careerClassification,
                workClassification = workClassification,
                locationFoundationId = locationFoundationId ?? string.Empty,
                reasonStarted = reasonStarted ?? string.Empty,
                reasonEnded = reasonEnded ?? string.Empty,
                primaryCareer = primaryCareer,
                exclusiveCareer = exclusiveCareer,
                secret = secret,
                disputed = disputed,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                sourceRecords = CleanSources(sourceRecords),
                transitionIds = Clean(transitionIds),
                revisionHistory = Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return CareerHistoryInformationSubject.Episode(episodeId, personId, professionId, organizationId);
        }
    }

    [Serializable]
    public sealed class CareerTransitionRecordData
    {
        public string transitionId;
        public string personId;
        public string transitionDefinitionId;
        public CareerTransitionCategory category = CareerTransitionCategory.Custom;
        public string[] sourceEpisodeIds = Array.Empty<string>();
        public string[] destinationEpisodeIds = Array.Empty<string>();
        public string professionId;
        public string specializationId;
        public string previousRankRecordId;
        public string newRankRecordId;
        public string previousCredentialId;
        public string newCredentialId;
        public string previousEmploymentId;
        public string newEmploymentId;
        public string previousPositionInstanceId;
        public string newPositionInstanceId;
        public string organizationId;
        public string transitionWorldTime;
        public string decidingAuthorityId;
        public bool voluntary = true;
        public bool involuntary;
        public bool secret;
        public bool disputed;
        public string reason;
        public CareerSourceRecordReferenceData[] supportingRecords = Array.Empty<CareerSourceRecordReferenceData>();
        public string accessPolicyId;
        public string provenance;
        public long professionRevision;
        public long trainingRevision;
        public long activityRevision;
        public long credentialRevision;
        public long rankRevision;
        public long employmentRevision;
        public long careerRevisionAtEvaluation;
        public string evaluationHash;
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public CareerTransitionRecordData Clone()
        {
            return new CareerTransitionRecordData
            {
                transitionId = transitionId ?? string.Empty,
                personId = personId ?? string.Empty,
                transitionDefinitionId = transitionDefinitionId ?? string.Empty,
                category = category,
                sourceEpisodeIds = CareerEpisodeData.Clean(sourceEpisodeIds),
                destinationEpisodeIds = CareerEpisodeData.Clean(destinationEpisodeIds),
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                previousRankRecordId = previousRankRecordId ?? string.Empty,
                newRankRecordId = newRankRecordId ?? string.Empty,
                previousCredentialId = previousCredentialId ?? string.Empty,
                newCredentialId = newCredentialId ?? string.Empty,
                previousEmploymentId = previousEmploymentId ?? string.Empty,
                newEmploymentId = newEmploymentId ?? string.Empty,
                previousPositionInstanceId = previousPositionInstanceId ?? string.Empty,
                newPositionInstanceId = newPositionInstanceId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                transitionWorldTime = transitionWorldTime ?? string.Empty,
                decidingAuthorityId = decidingAuthorityId ?? string.Empty,
                voluntary = voluntary,
                involuntary = involuntary,
                secret = secret,
                disputed = disputed,
                reason = reason ?? string.Empty,
                supportingRecords = CareerEpisodeData.CleanSources(supportingRecords),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                professionRevision = Math.Max(0L, professionRevision),
                trainingRevision = Math.Max(0L, trainingRevision),
                activityRevision = Math.Max(0L, activityRevision),
                credentialRevision = Math.Max(0L, credentialRevision),
                rankRevision = Math.Max(0L, rankRevision),
                employmentRevision = Math.Max(0L, employmentRevision),
                careerRevisionAtEvaluation = Math.Max(0L, careerRevisionAtEvaluation),
                evaluationHash = evaluationHash ?? string.Empty,
                revisionHistory = CareerEpisodeData.Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }

        public bool SemanticallyEqualsCurrent(long careerRevision, long professionRev, long trainingRev, long activityRev, long credentialRev, long rankRev, long employmentRev)
        {
            return careerRevisionAtEvaluation == careerRevision
                && professionRevision == professionRev
                && trainingRevision == trainingRev
                && activityRevision == activityRev
                && credentialRevision == credentialRev
                && rankRevision == rankRev
                && employmentRevision == employmentRev
                && string.Equals(evaluationHash ?? string.Empty, BuildEvaluationHash(personId, transitionDefinitionId, category, sourceEpisodeIds, destinationEpisodeIds, professionId, previousRankRecordId, newRankRecordId, previousEmploymentId, newEmploymentId, previousPositionInstanceId, newPositionInstanceId, organizationId, careerRevision, professionRev, trainingRev, activityRev, credentialRev, rankRev, employmentRev), StringComparison.Ordinal);
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return CareerHistoryInformationSubject.Transition(transitionId, personId, category);
        }

        public static string BuildEvaluationHash(string personId, string definitionId, CareerTransitionCategory category, IEnumerable<string> sourceEpisodes, IEnumerable<string> destinationEpisodes, string professionId, string previousRankId, string newRankId, string previousEmploymentId, string newEmploymentId, string previousPositionId, string newPositionId, string organizationId, long careerRevision, long professionRevision, long trainingRevision, long activityRevision, long credentialRevision, long rankRevision, long employmentRevision)
        {
            string sources = string.Join(",", CareerEpisodeData.Clean(sourceEpisodes));
            string destinations = string.Join(",", CareerEpisodeData.Clean(destinationEpisodes));
            return $"{personId ?? string.Empty}|{definitionId ?? string.Empty}|{category}|{sources}|{destinations}|{professionId ?? string.Empty}|{previousRankId ?? string.Empty}|{newRankId ?? string.Empty}|{previousEmploymentId ?? string.Empty}|{newEmploymentId ?? string.Empty}|{previousPositionId ?? string.Empty}|{newPositionId ?? string.Empty}|{organizationId ?? string.Empty}|{careerRevision}|{professionRevision}|{trainingRevision}|{activityRevision}|{credentialRevision}|{rankRevision}|{employmentRevision}";
        }
    }

    [Serializable]
    public sealed class CareerMilestoneRecordData
    {
        public string milestoneId;
        public string personId;
        public CareerMilestoneKind kind = CareerMilestoneKind.Custom;
        public string episodeId;
        public string transitionId;
        public string sourceRecordId;
        public CareerTransitionSourceRecordType sourceRecordType;
        public string professionId;
        public string organizationId;
        public string worldTime;
        public string description;
        public bool exclusive;
        public bool secret;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public CareerMilestoneRecordData Clone()
        {
            return new CareerMilestoneRecordData
            {
                milestoneId = milestoneId ?? string.Empty,
                personId = personId ?? string.Empty,
                kind = kind,
                episodeId = episodeId ?? string.Empty,
                transitionId = transitionId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                sourceRecordType = sourceRecordType,
                professionId = professionId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                description = description ?? string.Empty,
                exclusive = exclusive,
                secret = secret,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class CareerHistoryRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<CareerEpisodeData> episodes = new List<CareerEpisodeData>();
        public List<CareerTransitionRecordData> transitions = new List<CareerTransitionRecordData>();
        public List<CareerMilestoneRecordData> milestones = new List<CareerMilestoneRecordData>();

        public CareerHistoryRuntimeSaveData Clone()
        {
            return new CareerHistoryRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = Math.Max(0L, revision),
                episodes = episodes == null ? new List<CareerEpisodeData>() : episodes.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                transitions = transitions == null ? new List<CareerTransitionRecordData>() : transitions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                milestones = milestones == null ? new List<CareerMilestoneRecordData>() : milestones.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class CareerTimelineSnapshot
    {
        public CareerTimelineSnapshot(string personId, IEnumerable<CareerEpisodeData> episodes, IEnumerable<CareerTransitionRecordData> transitions, IEnumerable<CareerMilestoneRecordData> milestones, long revision)
        {
            PersonId = personId ?? string.Empty;
            Episodes = SortEpisodes(episodes).Select(item => item.Clone()).ToArray();
            Transitions = SortTransitions(transitions).Select(item => item.Clone()).ToArray();
            Milestones = SortMilestones(milestones).Select(item => item.Clone()).ToArray();
            Revision = Math.Max(0L, revision);
        }

        public string PersonId { get; }
        public IReadOnlyList<CareerEpisodeData> Episodes { get; }
        public IReadOnlyList<CareerTransitionRecordData> Transitions { get; }
        public IReadOnlyList<CareerMilestoneRecordData> Milestones { get; }
        public IReadOnlyList<CareerEpisodeData> ActiveCareers => Episodes.Where(item => item.state == CareerEpisodeState.Active).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CareerEpisodeData> PrimaryCareers => Episodes.Where(item => item.primaryCareer).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CareerEpisodeData> ConcurrentCareers => Episodes.Where(item => item.state == CareerEpisodeState.Active && !item.primaryCareer).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<CareerEpisodeData> CareerGaps => Episodes.Where(item => item.category == CareerEpisodeCategory.CareerGap).Select(item => item.Clone()).ToArray();
        public bool Retired => Episodes.Any(item => item.category == CareerEpisodeCategory.Retirement && item.state == CareerEpisodeState.Active);
        public long Revision { get; }

        private static IEnumerable<CareerEpisodeData> SortEpisodes(IEnumerable<CareerEpisodeData> values)
        {
            return (values ?? Array.Empty<CareerEpisodeData>())
                .Where(item => item != null)
                .OrderBy(item => item.startWorldTime ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.episodeId ?? string.Empty, StringComparer.Ordinal);
        }

        private static IEnumerable<CareerTransitionRecordData> SortTransitions(IEnumerable<CareerTransitionRecordData> values)
        {
            return (values ?? Array.Empty<CareerTransitionRecordData>())
                .Where(item => item != null)
                .OrderBy(item => item.transitionWorldTime ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.transitionId ?? string.Empty, StringComparer.Ordinal);
        }

        private static IEnumerable<CareerMilestoneRecordData> SortMilestones(IEnumerable<CareerMilestoneRecordData> values)
        {
            return (values ?? Array.Empty<CareerMilestoneRecordData>())
                .Where(item => item != null)
                .OrderBy(item => item.worldTime ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.milestoneId ?? string.Empty, StringComparer.Ordinal);
        }
    }

    public sealed class CareerHistoryOperationResult
    {
        private CareerHistoryOperationResult(bool succeeded, bool preview, bool duplicate, CareerHistoryOperationStatus status, string message, long priorRevision, long resultingRevision, CareerEpisodeData episode = null, CareerTransitionRecordData transition = null, CareerMilestoneRecordData milestone = null, CareerTimelineSnapshot timeline = null)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Status = status;
            Message = message ?? string.Empty;
            PriorRevision = Math.Max(0L, priorRevision);
            ResultingRevision = Math.Max(0L, resultingRevision);
            Episode = episode?.Clone();
            Transition = transition?.Clone();
            Milestone = milestone?.Clone();
            Timeline = timeline;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public CareerHistoryOperationStatus Status { get; }
        public string Message { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public CareerEpisodeData Episode { get; }
        public CareerTransitionRecordData Transition { get; }
        public CareerMilestoneRecordData Milestone { get; }
        public CareerTimelineSnapshot Timeline { get; }

        public static CareerHistoryOperationResult Success(string message, long priorRevision, long resultingRevision, CareerEpisodeData episode = null, CareerTransitionRecordData transition = null, CareerMilestoneRecordData milestone = null, CareerTimelineSnapshot timeline = null, bool preview = false, bool duplicate = false)
        {
            return new CareerHistoryOperationResult(true, preview, duplicate, preview ? CareerHistoryOperationStatus.Preview : duplicate ? CareerHistoryOperationStatus.Duplicate : CareerHistoryOperationStatus.Succeeded, message, priorRevision, resultingRevision, episode, transition, milestone, timeline);
        }

        public static CareerHistoryOperationResult Failure(CareerHistoryOperationStatus status, string message, long revision = 0L)
        {
            return new CareerHistoryOperationResult(false, false, false, status, message, revision, revision);
        }
    }

    public sealed class CareerHistoryProjection<TRecord>
    {
        public CareerHistoryProjection(TRecord record, CareerHistoryProjectionAudience audience, InformationAccessDecision decision, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields)
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
        public CareerHistoryProjectionAudience Audience { get; }
        public InformationAccessDecision Decision { get; }
        public bool Redacted { get; }
        public bool Denied { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    public sealed class CareerHistoryHookData
    {
        public CareerHistoryHookKind kind;
        public string episodeId;
        public string transitionId;
        public string milestoneId;
        public string personId;
        public string professionId;
        public string employmentId;
        public string organizationId;
        public string worldTime;
        public string transactionId;

        public CareerHistoryHookData Clone()
        {
            return new CareerHistoryHookData
            {
                kind = kind,
                episodeId = episodeId ?? string.Empty,
                transitionId = transitionId ?? string.Empty,
                milestoneId = milestoneId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                employmentId = employmentId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            };
        }
    }

    public static class CareerHistoryInformationSubject
    {
        public const string EpisodeTag = "subject.profession.career-episode";
        public const string TransitionTag = "subject.profession.career-transition";
        public const string TimelineTag = "subject.profession.career-timeline";
        public const string GapTag = "subject.profession.career-gap";
        public const string RetirementTag = "subject.profession.retirement";
        public const string CareerChangeTag = "subject.profession.career-change";
        public const string PromotionTag = "subject.profession.promotion";
        public const string TransferTag = "subject.profession.transfer";
        public const string ResignationTag = "subject.profession.resignation";
        public const string DismissalTag = "subject.profession.dismissal";
        public const string AchievementTag = "subject.profession.career-achievement";
        public const string SetbackTag = "subject.profession.career-setback";
        public static readonly string[] ProtectedFields = { "reason", "source-records", "organization-id", "employment-id", "position-id", "medical-or-disciplinary-context" };

        public static InformationSubjectReferenceData Episode(string episodeId, string personId, string professionId, string organizationId)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Affiliation,
                subjectId = episodeId ?? string.Empty,
                parentSubjectId = professionId ?? string.Empty,
                ownerPersonId = personId ?? string.Empty,
                controllingEntityId = organizationId ?? string.Empty,
                tags = CareerEpisodeData.Clean(new[] { EpisodeTag, string.IsNullOrWhiteSpace(organizationId) ? string.Empty : $"organization:{organizationId}" })
            };
        }

        public static InformationSubjectReferenceData Transition(string transitionId, string personId, CareerTransitionCategory category)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.HistoricalEvent,
                subjectId = transitionId ?? string.Empty,
                parentSubjectId = category.ToString(),
                ownerPersonId = personId ?? string.Empty,
                tags = TagsFor(category)
            };
        }

        public static InformationSubjectReferenceData Timeline(string personId)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.KnowledgeRecord,
                subjectId = personId ?? string.Empty,
                ownerPersonId = personId ?? string.Empty,
                tags = new[] { TimelineTag }
            };
        }

        private static string[] TagsFor(CareerTransitionCategory category)
        {
            string specific = category switch
            {
                CareerTransitionCategory.CareerGapStarted or CareerTransitionCategory.CareerGapEnded => GapTag,
                CareerTransitionCategory.Retirement or CareerTransitionCategory.ReturnFromRetirement => RetirementTag,
                CareerTransitionCategory.CareerChange => CareerChangeTag,
                CareerTransitionCategory.Promotion => PromotionTag,
                CareerTransitionCategory.Transfer => TransferTag,
                CareerTransitionCategory.Resignation => ResignationTag,
                CareerTransitionCategory.Dismissal => DismissalTag,
                CareerTransitionCategory.Achievement => AchievementTag,
                CareerTransitionCategory.Setback => SetbackTag,
                _ => TransitionTag
            };
            return new[] { TransitionTag, specific };
        }
    }

    public static class CareerHistoryRequirementAdapters
    {
        public static bool HasPreviousProfession(CareerHistoryRuntime runtime, string personId, string professionId)
        {
            return runtime != null && runtime.QueryEpisodesByPerson(personId).Any(item => string.Equals(item.professionId, professionId ?? string.Empty, StringComparison.Ordinal));
        }

        public static bool HasPreviousEmployment(CareerHistoryRuntime runtime, string personId, string employmentId)
        {
            return runtime != null && runtime.QueryEpisodesByPerson(personId).Any(item => string.Equals(item.employmentId, employmentId ?? string.Empty, StringComparison.Ordinal));
        }

        public static bool HasPriorSupervisoryExperience(CareerHistoryRuntime runtime, string personId)
        {
            return runtime != null && runtime.QueryTransitionsByPerson(personId).Any(item => item.category == CareerTransitionCategory.Promotion || item.category == CareerTransitionCategory.Transfer);
        }

        public static bool HasNoProhibitedDismissal(CareerHistoryRuntime runtime, string personId)
        {
            return runtime == null || !runtime.QueryTransitionsByPerson(personId).Any(item => item.category == CareerTransitionCategory.Dismissal && !item.disputed);
        }

        public static bool IsRetired(CareerHistoryRuntime runtime, string personId)
        {
            CareerHistoryOperationResult result = runtime?.BuildTimeline(personId);
            return result != null && result.Succeeded && result.Timeline != null && result.Timeline.Retired;
        }

        public static bool HasCareerTransition(CareerHistoryRuntime runtime, string personId, CareerTransitionCategory category)
        {
            return runtime != null && runtime.QueryTransitionsByPerson(personId).Any(item => item.category == category);
        }
    }

    internal static class CareerHistoryModelUtilities
    {
        public static string[] Clean(IEnumerable<string> values)
        {
            return CareerEpisodeData.Clean(values);
        }
    }

    public sealed partial class CareerEpisodeData
    {
        internal static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        internal static CareerSourceRecordReferenceData[] CleanSources(IEnumerable<CareerSourceRecordReferenceData> values)
        {
            return (values ?? Array.Empty<CareerSourceRecordReferenceData>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.recordId))
                .Select(value => value.Clone())
                .GroupBy(value => value.Signature, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(value => value.recordType)
                .ThenBy(value => value.recordId ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
