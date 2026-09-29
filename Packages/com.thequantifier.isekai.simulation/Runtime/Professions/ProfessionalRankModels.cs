using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [Serializable]
    public sealed class ProfessionalRankAdvancementSnapshotData
    {
        public string personId;
        public string professionId;
        public string specializationId;
        public string currentRankDefinitionId;
        public string requestedRankDefinitionId;
        public string recognizingAuthorityId;
        public bool authoritativeEligible;
        public bool perceivedEligible;
        public string[] satisfiedRequirementIds = Array.Empty<string>();
        public string[] blockingRequirementIds = Array.Empty<string>();
        public string[] recommendationIds = Array.Empty<string>();
        public string[] alternativeRankDefinitionIds = Array.Empty<string>();
        public long professionRevision;
        public long trainingRevision;
        public long activityRevision;
        public long credentialRevision;
        public long rankRevision;
        public string evaluationHash;
        public string privilegedDiagnostics;
        public string redactedDiagnostics;

        public ProfessionalRankAdvancementSnapshotData Clone()
        {
            return new ProfessionalRankAdvancementSnapshotData
            {
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                currentRankDefinitionId = currentRankDefinitionId ?? string.Empty,
                requestedRankDefinitionId = requestedRankDefinitionId ?? string.Empty,
                recognizingAuthorityId = recognizingAuthorityId ?? string.Empty,
                authoritativeEligible = authoritativeEligible,
                perceivedEligible = perceivedEligible,
                satisfiedRequirementIds = Clean(satisfiedRequirementIds),
                blockingRequirementIds = Clean(blockingRequirementIds),
                recommendationIds = Clean(recommendationIds),
                alternativeRankDefinitionIds = Clean(alternativeRankDefinitionIds),
                professionRevision = Math.Max(0L, professionRevision),
                trainingRevision = Math.Max(0L, trainingRevision),
                activityRevision = Math.Max(0L, activityRevision),
                credentialRevision = Math.Max(0L, credentialRevision),
                rankRevision = Math.Max(0L, rankRevision),
                evaluationHash = evaluationHash ?? string.Empty,
                privilegedDiagnostics = privilegedDiagnostics ?? string.Empty,
                redactedDiagnostics = redactedDiagnostics ?? string.Empty
            };
        }

        public bool SemanticallyEquals(ProfessionalRankAdvancementSnapshotData other)
        {
            return other != null
                && string.Equals(evaluationHash ?? string.Empty, other.evaluationHash ?? string.Empty, StringComparison.Ordinal)
                && professionRevision == other.professionRevision
                && trainingRevision == other.trainingRevision
                && activityRevision == other.activityRevision
                && credentialRevision == other.credentialRevision
                && rankRevision == other.rankRevision;
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

    public sealed class ProfessionalRankAdvancementResult
    {
        public ProfessionalRankAdvancementResult(ProfessionalRankAdvancementSnapshotData snapshot)
        {
            Snapshot = snapshot?.Clone() ?? new ProfessionalRankAdvancementSnapshotData();
        }

        public ProfessionalRankAdvancementSnapshotData Snapshot { get; }
        public bool AuthoritativeEligible => Snapshot.authoritativeEligible;
        public bool PerceivedEligible => Snapshot.perceivedEligible;
        public IReadOnlyList<string> SatisfiedRequirements => Snapshot.satisfiedRequirementIds;
        public IReadOnlyList<string> BlockingFailures => Snapshot.blockingRequirementIds;
        public IReadOnlyList<string> Recommendations => Snapshot.recommendationIds;
        public IReadOnlyList<string> AlternativeRanks => Snapshot.alternativeRankDefinitionIds;
    }

    [Serializable]
    public sealed class ProfessionalRankApplicationData
    {
        public string applicationId;
        public string applicantPersonId;
        public string professionId;
        public string specializationId;
        public string currentRankDefinitionId;
        public string requestedRankDefinitionId;
        public string recognizingAuthorityId;
        public string submissionWorldTime;
        public ProfessionalRankAdvancementSnapshotData evaluationSnapshot = new ProfessionalRankAdvancementSnapshotData();
        public string[] supportingCredentialIds = Array.Empty<string>();
        public string[] supportingExperienceEvidenceIds = Array.Empty<string>();
        public string[] supportingExaminationAttemptIds = Array.Empty<string>();
        public string[] sponsorOrRecommenderIds = Array.Empty<string>();
        public ProfessionalRankApplicationState state = ProfessionalRankApplicationState.Draft;
        public string reviewerPersonId;
        public string decisionWorldTime;
        public string decisionReason;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public ProfessionalRankApplicationData Clone()
        {
            return new ProfessionalRankApplicationData
            {
                applicationId = applicationId ?? string.Empty,
                applicantPersonId = applicantPersonId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                currentRankDefinitionId = currentRankDefinitionId ?? string.Empty,
                requestedRankDefinitionId = requestedRankDefinitionId ?? string.Empty,
                recognizingAuthorityId = recognizingAuthorityId ?? string.Empty,
                submissionWorldTime = submissionWorldTime ?? string.Empty,
                evaluationSnapshot = evaluationSnapshot?.Clone() ?? new ProfessionalRankAdvancementSnapshotData(),
                supportingCredentialIds = ProfessionalRankAdvancementSnapshotData.Clean(supportingCredentialIds),
                supportingExperienceEvidenceIds = ProfessionalRankAdvancementSnapshotData.Clean(supportingExperienceEvidenceIds),
                supportingExaminationAttemptIds = ProfessionalRankAdvancementSnapshotData.Clean(supportingExaminationAttemptIds),
                sponsorOrRecommenderIds = ProfessionalRankAdvancementSnapshotData.Clean(sponsorOrRecommenderIds),
                state = state,
                reviewerPersonId = reviewerPersonId ?? string.Empty,
                decisionWorldTime = decisionWorldTime ?? string.Empty,
                decisionReason = decisionReason ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProfessionalRankRecordData
    {
        public string rankRecordId;
        public string personId;
        public string professionId;
        public string specializationId;
        public string ladderDefinitionId;
        public string rankDefinitionId;
        public ProfessionalRankState state = ProfessionalRankState.Proposed;
        public ProfessionalRankTrackKind trackKind = ProfessionalRankTrackKind.Formal;
        public string recognizingAuthorityId;
        public string issueWorldTime;
        public string effectiveWorldTime;
        public string endWorldTime;
        public string supportingApplicationId;
        public string[] supportingCredentialIds = Array.Empty<string>();
        public string[] supportingExperienceEvidenceIds = Array.Empty<string>();
        public string[] supportingExaminationAttemptIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenance;
        public string replacedByRankRecordId;
        public string replacesRankRecordId;
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public ProfessionalRankRecordData Clone()
        {
            return new ProfessionalRankRecordData
            {
                rankRecordId = rankRecordId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                ladderDefinitionId = ladderDefinitionId ?? string.Empty,
                rankDefinitionId = rankDefinitionId ?? string.Empty,
                state = state,
                trackKind = trackKind,
                recognizingAuthorityId = recognizingAuthorityId ?? string.Empty,
                issueWorldTime = issueWorldTime ?? string.Empty,
                effectiveWorldTime = effectiveWorldTime ?? string.Empty,
                endWorldTime = endWorldTime ?? string.Empty,
                supportingApplicationId = supportingApplicationId ?? string.Empty,
                supportingCredentialIds = ProfessionalRankAdvancementSnapshotData.Clean(supportingCredentialIds),
                supportingExperienceEvidenceIds = ProfessionalRankAdvancementSnapshotData.Clean(supportingExperienceEvidenceIds),
                supportingExaminationAttemptIds = ProfessionalRankAdvancementSnapshotData.Clean(supportingExaminationAttemptIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                replacedByRankRecordId = replacedByRankRecordId ?? string.Empty,
                replacesRankRecordId = replacesRankRecordId ?? string.Empty,
                revisionHistory = ProfessionalRankAdvancementSnapshotData.Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProfessionalQualifyingAchievementData
    {
        public string achievementId;
        public string personId;
        public string professionId;
        public string specializationId;
        public string sourceActivityId;
        public string activityDefinitionId;
        public string description;
        public int quality;
        public ProfessionalActivityDifficulty difficulty = ProfessionalActivityDifficulty.Unknown;
        public string[] evidenceReferenceIds = Array.Empty<string>();
        public string validatingAuthorityId;
        public string worldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public ProfessionalQualifyingAchievementData Clone()
        {
            return new ProfessionalQualifyingAchievementData
            {
                achievementId = achievementId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                sourceActivityId = sourceActivityId ?? string.Empty,
                activityDefinitionId = activityDefinitionId ?? string.Empty,
                description = description ?? string.Empty,
                quality = Math.Max(0, Math.Min(1000, quality)),
                difficulty = difficulty,
                evidenceReferenceIds = ProfessionalRankAdvancementSnapshotData.Clean(evidenceReferenceIds),
                validatingAuthorityId = validatingAuthorityId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProfessionalMasteryRecordData
    {
        public string masteryRecordId;
        public string personId;
        public string professionId;
        public string specializationId;
        public string masteryDefinitionId;
        public ProfessionalRankState state = ProfessionalRankState.Active;
        public ProfessionalRankTrackKind trackKind = ProfessionalRankTrackKind.Formal;
        public string recognizingAuthorityId;
        public string issueWorldTime;
        public string[] supportingRankRecordIds = Array.Empty<string>();
        public string[] supportingCredentialIds = Array.Empty<string>();
        public string[] supportingExperienceEvidenceIds = Array.Empty<string>();
        public string[] supportingAchievementIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenance;
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public ProfessionalMasteryRecordData Clone()
        {
            return new ProfessionalMasteryRecordData
            {
                masteryRecordId = masteryRecordId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                masteryDefinitionId = masteryDefinitionId ?? string.Empty,
                state = state,
                trackKind = trackKind,
                recognizingAuthorityId = recognizingAuthorityId ?? string.Empty,
                issueWorldTime = issueWorldTime ?? string.Empty,
                supportingRankRecordIds = ProfessionalRankAdvancementSnapshotData.Clean(supportingRankRecordIds),
                supportingCredentialIds = ProfessionalRankAdvancementSnapshotData.Clean(supportingCredentialIds),
                supportingExperienceEvidenceIds = ProfessionalRankAdvancementSnapshotData.Clean(supportingExperienceEvidenceIds),
                supportingAchievementIds = ProfessionalRankAdvancementSnapshotData.Clean(supportingAchievementIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revisionHistory = ProfessionalRankAdvancementSnapshotData.Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProfessionalRankRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<ProfessionalRankApplicationData> applications = new List<ProfessionalRankApplicationData>();
        public List<ProfessionalRankRecordData> ranks = new List<ProfessionalRankRecordData>();
        public List<ProfessionalMasteryRecordData> masteries = new List<ProfessionalMasteryRecordData>();
        public List<ProfessionalQualifyingAchievementData> achievements = new List<ProfessionalQualifyingAchievementData>();

        public ProfessionalRankRuntimeSaveData Clone()
        {
            return new ProfessionalRankRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = Math.Max(0L, revision),
                applications = applications == null ? new List<ProfessionalRankApplicationData>() : applications.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                ranks = ranks == null ? new List<ProfessionalRankRecordData>() : ranks.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                masteries = masteries == null ? new List<ProfessionalMasteryRecordData>() : masteries.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                achievements = achievements == null ? new List<ProfessionalQualifyingAchievementData>() : achievements.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class ProfessionalRankOperationResult
    {
        private ProfessionalRankOperationResult(bool succeeded, bool preview, bool duplicate, ProfessionalRankOperationStatus status, string message, long priorRevision, long resultingRevision, ProfessionalRankAdvancementResult evaluation = null, ProfessionalRankApplicationData application = null, ProfessionalRankRecordData rank = null, ProfessionalMasteryRecordData mastery = null, ProfessionalQualifyingAchievementData achievement = null)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Status = status;
            Message = message ?? string.Empty;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
            Evaluation = evaluation;
            Application = application?.Clone();
            Rank = rank?.Clone();
            Mastery = mastery?.Clone();
            Achievement = achievement?.Clone();
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public ProfessionalRankOperationStatus Status { get; }
        public string Message { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public ProfessionalRankAdvancementResult Evaluation { get; }
        public ProfessionalRankApplicationData Application { get; }
        public ProfessionalRankRecordData Rank { get; }
        public ProfessionalMasteryRecordData Mastery { get; }
        public ProfessionalQualifyingAchievementData Achievement { get; }

        public static ProfessionalRankOperationResult Success(string message, long priorRevision, long resultingRevision, ProfessionalRankAdvancementResult evaluation = null, ProfessionalRankApplicationData application = null, ProfessionalRankRecordData rank = null, ProfessionalMasteryRecordData mastery = null, ProfessionalQualifyingAchievementData achievement = null, bool preview = false, bool duplicate = false)
        {
            return new ProfessionalRankOperationResult(true, preview, duplicate, preview ? ProfessionalRankOperationStatus.Preview : duplicate ? ProfessionalRankOperationStatus.Duplicate : ProfessionalRankOperationStatus.Succeeded, message, priorRevision, resultingRevision, evaluation, application, rank, mastery, achievement);
        }

        public static ProfessionalRankOperationResult Failure(ProfessionalRankOperationStatus status, string message, long revision = 0L, ProfessionalRankAdvancementResult evaluation = null)
        {
            return new ProfessionalRankOperationResult(false, false, false, status, message, revision, revision, evaluation);
        }
    }

    public sealed class ProfessionalRankProjection<TRecord>
    {
        public ProfessionalRankProjection(TRecord record, ProfessionalRankProjectionAudience audience, InformationAccessDecision decision, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields)
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
        public ProfessionalRankProjectionAudience Audience { get; }
        public InformationAccessDecision Decision { get; }
        public bool Redacted { get; }
        public bool Denied { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    [Serializable]
    public sealed class ProfessionalRankHistoryHookData
    {
        public ProfessionalRankHistoryHookKind kind;
        public string rankRecordId;
        public string applicationId;
        public string masteryRecordId;
        public string achievementId;
        public string personId;
        public string authorityId;
        public string worldTime;
        public string transactionId;

        public ProfessionalRankHistoryHookData Clone()
        {
            return new ProfessionalRankHistoryHookData
            {
                kind = kind,
                rankRecordId = rankRecordId ?? string.Empty,
                applicationId = applicationId ?? string.Empty,
                masteryRecordId = masteryRecordId ?? string.Empty,
                achievementId = achievementId ?? string.Empty,
                personId = personId ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            };
        }
    }

    public static class ProfessionalRankInformationSubject
    {
        public const string RankDefinitionTag = "subject.profession.rank-definition";
        public const string RankLadderTag = "subject.profession.rank-ladder";
        public const string RankRecordTag = "subject.profession.rank-record";
        public const string AdvancementEligibilityTag = "subject.profession.advancement-eligibility";
        public const string AdvancementApplicationTag = "subject.profession.advancement-application";
        public const string PromotionDecisionTag = "subject.profession.promotion-decision";
        public const string DemotionTag = "subject.profession.rank-demotion";
        public const string SuspensionTag = "subject.profession.rank-suspension";
        public const string RevocationTag = "subject.profession.rank-revocation";
        public const string MasteryDefinitionTag = "subject.profession.mastery-definition";
        public const string MasteryRecordTag = "subject.profession.mastery-record";
        public const string QualifyingAchievementTag = "subject.profession.qualifying-achievement";

        public static readonly string[] ProtectedFields =
        {
            "applicant",
            "holder",
            "evaluation",
            "authority",
            "examination",
            "discipline",
            "secret-rank",
            "rejection-reason",
            "qualifying-work",
            "provenance"
        };

        public static InformationSubjectReferenceData Create(string tag, string subjectId, string ownerId)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                ownerPersonId = ownerId ?? string.Empty,
                tags = ProfessionalRankAdvancementSnapshotData.Clean(new[] { tag })
            };
        }
    }
}
