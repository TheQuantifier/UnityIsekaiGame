using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [Serializable]
    public sealed class LifePathSourceReferenceData
    {
        public CareerTransitionSourceRecordType recordType = CareerTransitionSourceRecordType.Custom;
        public string recordId;
        public long sourceRevision;

        public LifePathSourceReferenceData Clone()
        {
            return new LifePathSourceReferenceData
            {
                recordType = recordType,
                recordId = recordId ?? string.Empty,
                sourceRevision = Math.Max(0L, sourceRevision)
            };
        }

        public string Signature => $"{recordType}:{recordId ?? string.Empty}:{Math.Max(0L, sourceRevision)}";
    }

    [Serializable]
    public sealed class FormativeReferenceData
    {
        public string referenceId;
        public FormativeReferenceKind kind = FormativeReferenceKind.Custom;
        public string subjectId;
        public string description;
        public string worldTime;
        public int weight;
        public string accessPolicyId;
        public string provenance;

        public FormativeReferenceData Clone()
        {
            return new FormativeReferenceData
            {
                referenceId = referenceId ?? string.Empty,
                kind = kind,
                subjectId = subjectId ?? string.Empty,
                description = description ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                weight = weight,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class LifePathRecordData
    {
        public string lifePathId;
        public string personId;
        public LifePathState state = LifePathState.Forming;
        public FormativeReferenceData[] formativeReferences = Array.Empty<FormativeReferenceData>();
        public LifePathSourceReferenceData[] educationAndTrainingReferences = Array.Empty<LifePathSourceReferenceData>();
        public string[] professionRelationshipIds = Array.Empty<string>();
        public string[] professionIds = Array.Empty<string>();
        public string[] specializationIds = Array.Empty<string>();
        public string[] careerEpisodeIds = Array.Empty<string>();
        public string[] credentialIds = Array.Empty<string>();
        public string[] rankRecordIds = Array.Empty<string>();
        public string[] positionInstanceIds = Array.Empty<string>();
        public string[] employmentIds = Array.Empty<string>();
        public string[] achievementAndSetbackIds = Array.Empty<string>();
        public string[] activeAspirationIds = Array.Empty<string>();
        public string[] activeGoalIds = Array.Empty<string>();
        public string primaryProfessionalIdentityId;
        public string[] secondaryProfessionalIdentityIds = Array.Empty<string>();
        public string startWorldTime;
        public string lastRevisionWorldTime;
        public string accessPolicyId;
        public string provenance;
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public LifePathRecordData Clone()
        {
            return new LifePathRecordData
            {
                lifePathId = lifePathId ?? string.Empty,
                personId = personId ?? string.Empty,
                state = state,
                formativeReferences = CleanFormative(formativeReferences),
                educationAndTrainingReferences = CleanSources(educationAndTrainingReferences),
                professionRelationshipIds = Clean(professionRelationshipIds),
                professionIds = Clean(professionIds),
                specializationIds = Clean(specializationIds),
                careerEpisodeIds = Clean(careerEpisodeIds),
                credentialIds = Clean(credentialIds),
                rankRecordIds = Clean(rankRecordIds),
                positionInstanceIds = Clean(positionInstanceIds),
                employmentIds = Clean(employmentIds),
                achievementAndSetbackIds = Clean(achievementAndSetbackIds),
                activeAspirationIds = Clean(activeAspirationIds),
                activeGoalIds = Clean(activeGoalIds),
                primaryProfessionalIdentityId = primaryProfessionalIdentityId ?? string.Empty,
                secondaryProfessionalIdentityIds = Clean(secondaryProfessionalIdentityIds),
                startWorldTime = startWorldTime ?? string.Empty,
                lastRevisionWorldTime = lastRevisionWorldTime ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revisionHistory = Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return LifePathInformationSubject.LifePath(lifePathId, personId);
        }

        public static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public static LifePathSourceReferenceData[] CleanSources(IEnumerable<LifePathSourceReferenceData> values)
        {
            return (values ?? Array.Empty<LifePathSourceReferenceData>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.recordId))
                .Select(value => value.Clone())
                .OrderBy(value => value.Signature, StringComparer.Ordinal)
                .ToArray();
        }

        public static FormativeReferenceData[] CleanFormative(IEnumerable<FormativeReferenceData> values)
        {
            return (values ?? Array.Empty<FormativeReferenceData>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.referenceId))
                .Select(value => value.Clone())
                .OrderBy(value => value.worldTime ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(value => value.referenceId ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
        }
    }

    [Serializable]
    public sealed class PersonAspirationData
    {
        public string aspirationId;
        public string personId;
        public string aspirationDefinitionId;
        public LifePathTargetSubjectType targetSubjectType = LifePathTargetSubjectType.Custom;
        public string targetProfessionId;
        public string targetSpecializationId;
        public string targetRankDefinitionId;
        public string targetCredentialDefinitionId;
        public string targetPositionDefinitionId;
        public string targetOrganizationId;
        public string targetItemId;
        public string targetAchievementId;
        public string customTargetSubjectId;
        public int priority;
        public int importance;
        public string timeHorizon;
        public string startWorldTime;
        public string targetTimeFoundationId;
        public PersonAspirationState state = PersonAspirationState.Desired;
        public string[] motivationTags = Array.Empty<string>();
        public string[] relatedGoalIds = Array.Empty<string>();
        public string progressSummary;
        public string replacedByAspirationId;
        public string replacementReason;
        public string accessPolicyId;
        public string provenance;
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public PersonAspirationData Clone()
        {
            return new PersonAspirationData
            {
                aspirationId = aspirationId ?? string.Empty,
                personId = personId ?? string.Empty,
                aspirationDefinitionId = aspirationDefinitionId ?? string.Empty,
                targetSubjectType = targetSubjectType,
                targetProfessionId = targetProfessionId ?? string.Empty,
                targetSpecializationId = targetSpecializationId ?? string.Empty,
                targetRankDefinitionId = targetRankDefinitionId ?? string.Empty,
                targetCredentialDefinitionId = targetCredentialDefinitionId ?? string.Empty,
                targetPositionDefinitionId = targetPositionDefinitionId ?? string.Empty,
                targetOrganizationId = targetOrganizationId ?? string.Empty,
                targetItemId = targetItemId ?? string.Empty,
                targetAchievementId = targetAchievementId ?? string.Empty,
                customTargetSubjectId = customTargetSubjectId ?? string.Empty,
                priority = priority,
                importance = importance,
                timeHorizon = timeHorizon ?? string.Empty,
                startWorldTime = startWorldTime ?? string.Empty,
                targetTimeFoundationId = targetTimeFoundationId ?? string.Empty,
                state = state,
                motivationTags = LifePathRecordData.Clean(motivationTags),
                relatedGoalIds = LifePathRecordData.Clean(relatedGoalIds),
                progressSummary = progressSummary ?? string.Empty,
                replacedByAspirationId = replacedByAspirationId ?? string.Empty,
                replacementReason = replacementReason ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revisionHistory = LifePathRecordData.Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }

        public bool ActiveLike => state == PersonAspirationState.Desired || state == PersonAspirationState.Considering || state == PersonAspirationState.Active || state == PersonAspirationState.Paused || state == PersonAspirationState.Blocked || state == PersonAspirationState.Conflicted || state == PersonAspirationState.Secret || state == PersonAspirationState.Disputed;
        public bool Secret => state == PersonAspirationState.Secret || MotivationTagsContainSecret || string.Equals(accessPolicyId, ProfessionContentIds.AccessSecretId, StringComparison.Ordinal);
        private bool MotivationTagsContainSecret => motivationTags != null && motivationTags.Any(tag => tag.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 || tag.IndexOf("private", StringComparison.OrdinalIgnoreCase) >= 0);

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return LifePathInformationSubject.Aspiration(aspirationId, personId, aspirationDefinitionId, targetProfessionId);
        }
    }

    [Serializable]
    public sealed class PersonGoalData
    {
        public string goalId;
        public string personId;
        public string goalDefinitionId;
        public string parentAspirationId;
        public LifePathTargetSubjectType targetSubjectType = LifePathTargetSubjectType.Custom;
        public string targetProfessionId;
        public string targetTrainingProgramId;
        public string targetCredentialDefinitionId;
        public string targetRankDefinitionId;
        public string targetPositionDefinitionId;
        public string targetActivityDefinitionId;
        public string targetCareerEpisodeId;
        public string targetCareerTransitionId;
        public string targetItemId;
        public string targetDiscoveryId;
        public string customTargetSubjectId;
        public string[] dependencyGoalIds = Array.Empty<string>();
        public string[] alternativeGoalIds = Array.Empty<string>();
        public string[] conflictTags = Array.Empty<string>();
        public int priority;
        public string startWorldTime;
        public string deadlineFoundationId;
        public PersonGoalState state = PersonGoalState.Planned;
        public LifeGoalProgressState progressState = LifeGoalProgressState.NotStarted;
        public string[] completedRequirementIds = Array.Empty<string>();
        public string[] remainingRequirementIds = Array.Empty<string>();
        public string[] blockingReasons = Array.Empty<string>();
        public LifePathSourceReferenceData[] authoritativeReferences = Array.Empty<LifePathSourceReferenceData>();
        public string completionWorldTime;
        public string failureOrAbandonmentReason;
        public string accessPolicyId;
        public string provenance;
        public long progressRevision;
        public string progressHash;
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public PersonGoalData Clone()
        {
            return new PersonGoalData
            {
                goalId = goalId ?? string.Empty,
                personId = personId ?? string.Empty,
                goalDefinitionId = goalDefinitionId ?? string.Empty,
                parentAspirationId = parentAspirationId ?? string.Empty,
                targetSubjectType = targetSubjectType,
                targetProfessionId = targetProfessionId ?? string.Empty,
                targetTrainingProgramId = targetTrainingProgramId ?? string.Empty,
                targetCredentialDefinitionId = targetCredentialDefinitionId ?? string.Empty,
                targetRankDefinitionId = targetRankDefinitionId ?? string.Empty,
                targetPositionDefinitionId = targetPositionDefinitionId ?? string.Empty,
                targetActivityDefinitionId = targetActivityDefinitionId ?? string.Empty,
                targetCareerEpisodeId = targetCareerEpisodeId ?? string.Empty,
                targetCareerTransitionId = targetCareerTransitionId ?? string.Empty,
                targetItemId = targetItemId ?? string.Empty,
                targetDiscoveryId = targetDiscoveryId ?? string.Empty,
                customTargetSubjectId = customTargetSubjectId ?? string.Empty,
                dependencyGoalIds = LifePathRecordData.Clean(dependencyGoalIds),
                alternativeGoalIds = LifePathRecordData.Clean(alternativeGoalIds),
                conflictTags = LifePathRecordData.Clean(conflictTags),
                priority = priority,
                startWorldTime = startWorldTime ?? string.Empty,
                deadlineFoundationId = deadlineFoundationId ?? string.Empty,
                state = state,
                progressState = progressState,
                completedRequirementIds = LifePathRecordData.Clean(completedRequirementIds),
                remainingRequirementIds = LifePathRecordData.Clean(remainingRequirementIds),
                blockingReasons = LifePathRecordData.Clean(blockingReasons),
                authoritativeReferences = LifePathRecordData.CleanSources(authoritativeReferences),
                completionWorldTime = completionWorldTime ?? string.Empty,
                failureOrAbandonmentReason = failureOrAbandonmentReason ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                progressRevision = Math.Max(0L, progressRevision),
                progressHash = progressHash ?? string.Empty,
                revisionHistory = LifePathRecordData.Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }

        public bool Terminal => state == PersonGoalState.Completed || state == PersonGoalState.Failed || state == PersonGoalState.Abandoned || state == PersonGoalState.Replaced || state == PersonGoalState.Cancelled || state == PersonGoalState.Expired;
        public bool Secret => state == PersonGoalState.Secret || string.Equals(accessPolicyId, ProfessionContentIds.AccessSecretId, StringComparison.Ordinal);

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return LifePathInformationSubject.Goal(goalId, personId, goalDefinitionId, targetProfessionId);
        }
    }

    [Serializable]
    public sealed class ProfessionalIdentityData
    {
        public string identityId;
        public string personId;
        public ProfessionalIdentityKind kind = ProfessionalIdentityKind.Secondary;
        public ProfessionalIdentityAlignmentState alignment = ProfessionalIdentityAlignmentState.Unknown;
        public string professionId;
        public string specializationId;
        public string professionRelationshipId;
        public string careerEpisodeId;
        public string beliefId;
        public int importance;
        public bool selfPerceived;
        public bool selfDeclared;
        public bool publicDeclared;
        public bool authorityRecognized;
        public string publicIdentityFoundationId;
        public bool secret;
        public string[] motivationTags = Array.Empty<string>();
        public bool active = true;
        public string startWorldTime;
        public string endWorldTime;
        public string accessPolicyId;
        public string provenance;
        public string[] revisionHistory = Array.Empty<string>();
        public long revision = 1L;

        public ProfessionalIdentityData Clone()
        {
            return new ProfessionalIdentityData
            {
                identityId = identityId ?? string.Empty,
                personId = personId ?? string.Empty,
                kind = kind,
                alignment = alignment,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                professionRelationshipId = professionRelationshipId ?? string.Empty,
                careerEpisodeId = careerEpisodeId ?? string.Empty,
                beliefId = beliefId ?? string.Empty,
                importance = importance,
                selfPerceived = selfPerceived,
                selfDeclared = selfDeclared,
                publicDeclared = publicDeclared,
                authorityRecognized = authorityRecognized,
                publicIdentityFoundationId = publicIdentityFoundationId ?? string.Empty,
                secret = secret,
                motivationTags = LifePathRecordData.Clean(motivationTags),
                active = active,
                startWorldTime = startWorldTime ?? string.Empty,
                endWorldTime = endWorldTime ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revisionHistory = LifePathRecordData.Clean(revisionHistory),
                revision = Math.Max(1L, revision)
            };
        }

        public bool Secret => secret || kind == ProfessionalIdentityKind.Secret || alignment == ProfessionalIdentityAlignmentState.Secret || string.Equals(accessPolicyId, ProfessionContentIds.AccessSecretId, StringComparison.Ordinal);
    }

    [Serializable]
    public sealed class IdentityConflictData
    {
        public string conflictId;
        public string personId;
        public string[] identityIds = Array.Empty<string>();
        public string[] goalIds = Array.Empty<string>();
        public string[] aspirationIds = Array.Empty<string>();
        public string[] conflictTags = Array.Empty<string>();
        public ProfessionalIdentityAlignmentState state = ProfessionalIdentityAlignmentState.Conflicted;
        public string reason;
        public bool resolved;
        public string resolutionWorldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public IdentityConflictData Clone()
        {
            return new IdentityConflictData
            {
                conflictId = conflictId ?? string.Empty,
                personId = personId ?? string.Empty,
                identityIds = LifePathRecordData.Clean(identityIds),
                goalIds = LifePathRecordData.Clean(goalIds),
                aspirationIds = LifePathRecordData.Clean(aspirationIds),
                conflictTags = LifePathRecordData.Clean(conflictTags),
                state = state,
                reason = reason ?? string.Empty,
                resolved = resolved,
                resolutionWorldTime = resolutionWorldTime ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class LifePathAchievementSetbackReferenceData
    {
        public string recordId;
        public string personId;
        public LifePathAchievementSetbackKind kind = LifePathAchievementSetbackKind.Custom;
        public string sourceRecordId;
        public CareerTransitionSourceRecordType sourceRecordType = CareerTransitionSourceRecordType.Custom;
        public string lifePathId;
        public string aspirationId;
        public string goalId;
        public string worldTime;
        public bool exclusive;
        public bool secret;
        public string significance;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public LifePathAchievementSetbackReferenceData Clone()
        {
            return new LifePathAchievementSetbackReferenceData
            {
                recordId = recordId ?? string.Empty,
                personId = personId ?? string.Empty,
                kind = kind,
                sourceRecordId = sourceRecordId ?? string.Empty,
                sourceRecordType = sourceRecordType,
                lifePathId = lifePathId ?? string.Empty,
                aspirationId = aspirationId ?? string.Empty,
                goalId = goalId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                exclusive = exclusive,
                secret = secret,
                significance = significance ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class LifePathRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<LifePathRecordData> lifePaths = new List<LifePathRecordData>();
        public List<PersonAspirationData> aspirations = new List<PersonAspirationData>();
        public List<PersonGoalData> goals = new List<PersonGoalData>();
        public List<ProfessionalIdentityData> identities = new List<ProfessionalIdentityData>();
        public List<IdentityConflictData> conflicts = new List<IdentityConflictData>();
        public List<LifePathAchievementSetbackReferenceData> achievementSetbacks = new List<LifePathAchievementSetbackReferenceData>();

        public LifePathRuntimeSaveData Clone()
        {
            return new LifePathRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = Math.Max(0L, revision),
                lifePaths = lifePaths == null ? new List<LifePathRecordData>() : lifePaths.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                aspirations = aspirations == null ? new List<PersonAspirationData>() : aspirations.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                goals = goals == null ? new List<PersonGoalData>() : goals.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                identities = identities == null ? new List<ProfessionalIdentityData>() : identities.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                conflicts = conflicts == null ? new List<IdentityConflictData>() : conflicts.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                achievementSetbacks = achievementSetbacks == null ? new List<LifePathAchievementSetbackReferenceData>() : achievementSetbacks.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class LifePathSnapshot
    {
        public LifePathSnapshot(string personId, IEnumerable<LifePathRecordData> lifePaths, IEnumerable<PersonAspirationData> aspirations, IEnumerable<PersonGoalData> goals, IEnumerable<ProfessionalIdentityData> identities, IEnumerable<IdentityConflictData> conflicts, IEnumerable<LifePathAchievementSetbackReferenceData> achievements, long revision)
        {
            PersonId = personId ?? string.Empty;
            LifePaths = Sort(lifePaths, item => item.startWorldTime, item => item.lifePathId).Select(item => item.Clone()).ToArray();
            Aspirations = (aspirations ?? Array.Empty<PersonAspirationData>()).Where(item => item != null).OrderByDescending(item => item.priority).ThenBy(item => item.startWorldTime ?? string.Empty, StringComparer.Ordinal).ThenBy(item => item.aspirationId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
            Goals = (goals ?? Array.Empty<PersonGoalData>()).Where(item => item != null).OrderByDescending(item => item.priority).ThenBy(item => item.startWorldTime ?? string.Empty, StringComparer.Ordinal).ThenBy(item => item.goalId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
            Identities = (identities ?? Array.Empty<ProfessionalIdentityData>()).Where(item => item != null).OrderBy(item => item.kind).ThenByDescending(item => item.importance).ThenBy(item => item.identityId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
            Conflicts = (conflicts ?? Array.Empty<IdentityConflictData>()).Where(item => item != null).OrderBy(item => item.conflictId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
            AchievementSetbacks = (achievements ?? Array.Empty<LifePathAchievementSetbackReferenceData>()).Where(item => item != null).OrderBy(item => item.worldTime ?? string.Empty, StringComparer.Ordinal).ThenBy(item => item.recordId ?? string.Empty, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
            Revision = Math.Max(0L, revision);
        }

        public string PersonId { get; }
        public IReadOnlyList<LifePathRecordData> LifePaths { get; }
        public IReadOnlyList<PersonAspirationData> Aspirations { get; }
        public IReadOnlyList<PersonGoalData> Goals { get; }
        public IReadOnlyList<ProfessionalIdentityData> Identities { get; }
        public IReadOnlyList<IdentityConflictData> Conflicts { get; }
        public IReadOnlyList<LifePathAchievementSetbackReferenceData> AchievementSetbacks { get; }
        public IReadOnlyList<PersonAspirationData> ActiveAspirations => Aspirations.Where(item => item.ActiveLike).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PersonGoalData> ActiveGoals => Goals.Where(item => item.state == PersonGoalState.Active || item.state == PersonGoalState.Planned || item.state == PersonGoalState.Blocked || item.state == PersonGoalState.Paused || item.state == PersonGoalState.Secret).Select(item => item.Clone()).ToArray();
        public ProfessionalIdentityData PrimaryIdentity => Identities.FirstOrDefault(item => item.kind == ProfessionalIdentityKind.Primary && item.active)?.Clone();
        public long Revision { get; }

        private static IEnumerable<T> Sort<T>(IEnumerable<T> values, Func<T, string> time, Func<T, string> id)
        {
            return (values ?? Array.Empty<T>()).Where(item => item != null).OrderBy(item => time(item) ?? string.Empty, StringComparer.Ordinal).ThenBy(item => id(item) ?? string.Empty, StringComparer.Ordinal);
        }
    }

    public sealed class LifeGoalProgressEvaluation
    {
        public LifeGoalProgressEvaluation(string goalId, string personId, LifeGoalProgressState state, int percentage, IEnumerable<string> satisfied, IEnumerable<string> remaining, IEnumerable<string> blocking, IEnumerable<string> failed, IEnumerable<LifePathSourceReferenceData> references, bool authoritativeComplete, bool perceivedComplete, long revision, string hash, bool redacted = false)
        {
            GoalId = goalId ?? string.Empty;
            PersonId = personId ?? string.Empty;
            State = state;
            CompletionPercentage = Math.Max(0, Math.Min(1000, percentage));
            SatisfiedRequirements = LifePathRecordData.Clean(satisfied);
            RemainingRequirements = LifePathRecordData.Clean(remaining);
            BlockingIssues = LifePathRecordData.Clean(blocking);
            FailedConditions = LifePathRecordData.Clean(failed);
            AuthoritativeReferences = LifePathRecordData.CleanSources(references);
            AuthoritativeComplete = authoritativeComplete;
            PerceivedComplete = perceivedComplete;
            RuntimeRevision = Math.Max(0L, revision);
            EvaluationHash = hash ?? string.Empty;
            Redacted = redacted;
        }

        public string GoalId { get; }
        public string PersonId { get; }
        public LifeGoalProgressState State { get; }
        public int CompletionPercentage { get; }
        public IReadOnlyList<string> SatisfiedRequirements { get; }
        public IReadOnlyList<string> RemainingRequirements { get; }
        public IReadOnlyList<string> BlockingIssues { get; }
        public IReadOnlyList<string> FailedConditions { get; }
        public IReadOnlyList<LifePathSourceReferenceData> AuthoritativeReferences { get; }
        public bool AuthoritativeComplete { get; }
        public bool PerceivedComplete { get; }
        public long RuntimeRevision { get; }
        public string EvaluationHash { get; }
        public bool Redacted { get; }
        public bool SemanticallyEqualsCurrent(long revision, string hash) => RuntimeRevision == revision && string.Equals(EvaluationHash, hash ?? string.Empty, StringComparison.Ordinal);
    }

    public sealed class LifePathOperationResult
    {
        private LifePathOperationResult(bool succeeded, bool preview, bool duplicate, LifePathOperationStatus status, string message, long priorRevision, long resultingRevision, LifePathRecordData lifePath = null, PersonAspirationData aspiration = null, PersonGoalData goal = null, ProfessionalIdentityData identity = null, IdentityConflictData conflict = null, LifePathAchievementSetbackReferenceData achievementSetback = null, LifePathSnapshot snapshot = null, LifeGoalProgressEvaluation progress = null)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Status = status;
            Message = message ?? string.Empty;
            PriorRevision = Math.Max(0L, priorRevision);
            ResultingRevision = Math.Max(0L, resultingRevision);
            LifePath = lifePath?.Clone();
            Aspiration = aspiration?.Clone();
            Goal = goal?.Clone();
            Identity = identity?.Clone();
            Conflict = conflict?.Clone();
            AchievementSetback = achievementSetback?.Clone();
            Snapshot = snapshot;
            Progress = progress;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public LifePathOperationStatus Status { get; }
        public string Message { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public LifePathRecordData LifePath { get; }
        public PersonAspirationData Aspiration { get; }
        public PersonGoalData Goal { get; }
        public ProfessionalIdentityData Identity { get; }
        public IdentityConflictData Conflict { get; }
        public LifePathAchievementSetbackReferenceData AchievementSetback { get; }
        public LifePathSnapshot Snapshot { get; }
        public LifeGoalProgressEvaluation Progress { get; }

        public static LifePathOperationResult Success(string message, long priorRevision, long resultingRevision, LifePathRecordData lifePath = null, PersonAspirationData aspiration = null, PersonGoalData goal = null, ProfessionalIdentityData identity = null, IdentityConflictData conflict = null, LifePathAchievementSetbackReferenceData achievementSetback = null, LifePathSnapshot snapshot = null, LifeGoalProgressEvaluation progress = null, bool preview = false, bool duplicate = false)
        {
            return new LifePathOperationResult(true, preview, duplicate, preview ? LifePathOperationStatus.Preview : duplicate ? LifePathOperationStatus.Duplicate : LifePathOperationStatus.Succeeded, message, priorRevision, resultingRevision, lifePath, aspiration, goal, identity, conflict, achievementSetback, snapshot, progress);
        }

        public static LifePathOperationResult Failure(LifePathOperationStatus status, string message, long revision = 0L)
        {
            return new LifePathOperationResult(false, false, false, status, message, revision, revision);
        }
    }

    public sealed class LifePathProjection<TRecord>
    {
        public LifePathProjection(TRecord record, LifePathProjectionAudience audience, InformationAccessDecision decision, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields)
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
        public LifePathProjectionAudience Audience { get; }
        public InformationAccessDecision Decision { get; }
        public bool Redacted { get; }
        public bool Denied { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    public sealed class LifePathHookData
    {
        public LifePathHookKind kind;
        public string personId;
        public string lifePathId;
        public string aspirationId;
        public string goalId;
        public string identityId;
        public string conflictId;
        public string recordId;
        public string worldTime;
        public string transactionId;

        public LifePathHookData Clone()
        {
            return new LifePathHookData
            {
                kind = kind,
                personId = personId ?? string.Empty,
                lifePathId = lifePathId ?? string.Empty,
                aspirationId = aspirationId ?? string.Empty,
                goalId = goalId ?? string.Empty,
                identityId = identityId ?? string.Empty,
                conflictId = conflictId ?? string.Empty,
                recordId = recordId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            };
        }
    }

    public static class LifePathInformationSubject
    {
        public const string LifePathTag = "subject.profession.life-path";
        public const string AspirationDefinitionTag = "subject.profession.aspiration-definition";
        public const string AspirationTag = "subject.profession.person-aspiration";
        public const string GoalDefinitionTag = "subject.profession.goal-definition";
        public const string GoalTag = "subject.profession.person-goal";
        public const string GoalProgressTag = "subject.profession.goal-progress";
        public const string ProfessionalIdentityTag = "subject.profession.identity";
        public const string IdentityConflictTag = "subject.profession.identity-conflict";
        public const string FulfillmentTag = "subject.profession.fulfillment";
        public const string FailureTag = "subject.profession.failure";
        public const string AbandonmentTag = "subject.profession.abandonment";
        public const string AchievementTag = "subject.profession.life-achievement";
        public const string SetbackTag = "subject.profession.life-setback";
        public static readonly string[] ProtectedFields = { "motivation", "target", "secret-identity", "private-failure", "confidential-career-history", "protected-setback" };

        public static InformationSubjectReferenceData LifePath(string lifePathId, string personId) => Subject(lifePathId, personId, string.Empty, LifePathTag);
        public static InformationSubjectReferenceData Aspiration(string aspirationId, string personId, string definitionId, string professionId) => Subject(aspirationId, personId, definitionId, AspirationTag, professionId);
        public static InformationSubjectReferenceData Goal(string goalId, string personId, string definitionId, string professionId) => Subject(goalId, personId, definitionId, GoalTag, professionId);
        public static InformationSubjectReferenceData Identity(string identityId, string personId, string professionId) => Subject(identityId, personId, professionId, ProfessionalIdentityTag, professionId);
        public static InformationSubjectReferenceData Conflict(string conflictId, string personId) => Subject(conflictId, personId, string.Empty, IdentityConflictTag);

        private static InformationSubjectReferenceData Subject(string id, string personId, string parent, string tag, string professionId = "")
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Affiliation,
                subjectId = id ?? string.Empty,
                parentSubjectId = parent ?? string.Empty,
                ownerPersonId = personId ?? string.Empty,
                tags = LifePathRecordData.Clean(new[] { tag, string.IsNullOrWhiteSpace(professionId) ? string.Empty : $"profession:{professionId}" })
            };
        }
    }

    public static class LifePathRequirementAdapters
    {
        public static bool HasActiveAspiration(LifePathRuntime runtime, string personId, string aspirationDefinitionId)
        {
            return runtime != null && runtime.QueryAspirationsByPerson(personId).Any(item => item.ActiveLike && string.Equals(item.aspirationDefinitionId, aspirationDefinitionId ?? string.Empty, StringComparison.Ordinal));
        }

        public static bool HasCompletedGoal(LifePathRuntime runtime, string personId, string goalDefinitionId)
        {
            return runtime != null && runtime.QueryGoalsByPerson(personId).Any(item => item.state == PersonGoalState.Completed && string.Equals(item.goalDefinitionId, goalDefinitionId ?? string.Empty, StringComparison.Ordinal));
        }

        public static bool HasFailedOrAbandonedGoal(LifePathRuntime runtime, string personId, string goalDefinitionId)
        {
            return runtime != null && runtime.QueryGoalsByPerson(personId).Any(item => (item.state == PersonGoalState.Failed || item.state == PersonGoalState.Abandoned) && string.Equals(item.goalDefinitionId, goalDefinitionId ?? string.Empty, StringComparison.Ordinal));
        }

        public static bool WantsProfession(LifePathRuntime runtime, string personId, string professionId)
        {
            return runtime != null && runtime.QueryAspirationsByPerson(personId).Any(item => item.ActiveLike && string.Equals(item.targetProfessionId, professionId ?? string.Empty, StringComparison.Ordinal));
        }

        public static bool HasProfessionalIdentity(LifePathRuntime runtime, string personId, string professionId)
        {
            return runtime != null && runtime.QueryIdentitiesByPerson(personId).Any(item => item.active && string.Equals(item.professionId, professionId ?? string.Empty, StringComparison.Ordinal));
        }

        public static bool HasCareerChangeIntent(LifePathRuntime runtime, string personId)
        {
            return runtime != null && runtime.QueryAspirationsByPerson(personId).Any(item => item.ActiveLike && item.targetSubjectType == LifePathTargetSubjectType.Profession && item.motivationTags.Contains("intent.career-change"));
        }

        public static bool HasRetirementIntent(LifePathRuntime runtime, string personId)
        {
            return runtime != null && runtime.QueryAspirationsByPerson(personId).Any(item => item.ActiveLike && item.motivationTags.Contains("intent.retirement"));
        }

        public static LifePathState GetLifePathState(LifePathRuntime runtime, string personId)
        {
            return runtime?.QueryLifePathsByPerson(personId).FirstOrDefault()?.state ?? LifePathState.Unknown;
        }

        public static bool HasIdentityConflict(LifePathRuntime runtime, string personId)
        {
            return runtime != null && runtime.QueryConflictsByPerson(personId).Any(item => !item.resolved);
        }
    }
}
