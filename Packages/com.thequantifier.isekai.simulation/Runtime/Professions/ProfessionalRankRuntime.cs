using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    public sealed class ProfessionalRankRuntime
    {
        private readonly Dictionary<string, ProfessionalRankApplicationData> applicationsById = new Dictionary<string, ProfessionalRankApplicationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProfessionalRankRecordData> ranksById = new Dictionary<string, ProfessionalRankRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProfessionalMasteryRecordData> masteriesById = new Dictionary<string, ProfessionalMasteryRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProfessionalQualifyingAchievementData> achievementsById = new Dictionary<string, ProfessionalQualifyingAchievementData>(StringComparer.Ordinal);
        private readonly List<ProfessionalRankHistoryHookData> historyHooks = new List<ProfessionalRankHistoryHookData>();
        private DefinitionRegistry registry;
        private PersonProfessionRuntime professions;
        private TrainingRuntime training;
        private ProfessionalActivityRuntime professionalActivities;
        private CredentialRuntime credentials;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownAuthorityIds = new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private bool dirty;

        public long Revision => revision;
        public bool IsDirty => dirty;
        public int ApplicationCount => applicationsById.Count;
        public int RankCount => ranksById.Count;
        public int MasteryCount => masteriesById.Count;
        public int AchievementCount => achievementsById.Count;
        public IReadOnlyList<ProfessionalRankHistoryHookData> HistoryHooks => historyHooks.Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ProfessionalRankApplicationData> Applications => applicationsById.Values.OrderBy(item => item.applicationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ProfessionalRankRecordData> Ranks => ranksById.Values.OrderBy(item => item.rankRecordId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ProfessionalMasteryRecordData> Masteries => masteriesById.Values.OrderBy(item => item.masteryRecordId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<ProfessionalQualifyingAchievementData> Achievements => achievementsById.Values.OrderBy(item => item.achievementId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, IEnumerable<string> persons = null, IEnumerable<string> authorities = null)
        {
            registry = definitionRegistry;
            professions = professionRuntime;
            training = trainingRuntime;
            professionalActivities = activityRuntime;
            credentials = credentialRuntime;
            knownPersonIds = new HashSet<string>((persons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            knownAuthorityIds = new HashSet<string>((authorities ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        }

        public bool TryGetRank(string rankRecordId, out ProfessionalRankRecordData rank)
        {
            if (!string.IsNullOrWhiteSpace(rankRecordId) && ranksById.TryGetValue(rankRecordId, out ProfessionalRankRecordData found))
            {
                rank = found.Clone();
                return true;
            }

            rank = null;
            return false;
        }

        public bool TryGetApplication(string applicationId, out ProfessionalRankApplicationData application)
        {
            if (!string.IsNullOrWhiteSpace(applicationId) && applicationsById.TryGetValue(applicationId, out ProfessionalRankApplicationData found))
            {
                application = found.Clone();
                return true;
            }

            application = null;
            return false;
        }

        public IReadOnlyList<ProfessionalRankRecordData> QueryByPerson(string personId, bool currentOnly = false)
        {
            return ranksById.Values
                .Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal))
                .Where(item => !currentOnly || IsActiveLike(item.state))
                .OrderBy(item => item.professionId, StringComparer.Ordinal)
                .ThenBy(item => item.specializationId, StringComparer.Ordinal)
                .ThenBy(item => item.rankDefinitionId, StringComparer.Ordinal)
                .ThenBy(item => item.rankRecordId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
        }

        public ProfessionalRankRecordData GetCurrentRank(string personId, string ladderDefinitionId)
        {
            return ranksById.Values
                .Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal)
                    && string.Equals(item.ladderDefinitionId, ladderDefinitionId ?? string.Empty, StringComparison.Ordinal)
                    && IsActiveLike(item.state))
                .OrderByDescending(item => ResolveRankOrder(item.rankDefinitionId))
                .ThenBy(item => item.rankRecordId, StringComparer.Ordinal)
                .FirstOrDefault()
                ?.Clone();
        }

        public bool HasActiveRank(string personId, string rankDefinitionId, bool allowInformal = true, string requiredAuthorityId = "")
        {
            return ranksById.Values.Any(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(item.rankDefinitionId, rankDefinitionId ?? string.Empty, StringComparison.Ordinal)
                && IsActiveLike(item.state)
                && (allowInformal || item.trackKind != ProfessionalRankTrackKind.Informal)
                && (string.IsNullOrWhiteSpace(requiredAuthorityId) || string.Equals(item.recognizingAuthorityId, requiredAuthorityId, StringComparison.Ordinal)));
        }

        public bool HasPermissionFoundation(string personId, string permissionId)
        {
            foreach (ProfessionalRankRecordData rank in ranksById.Values.Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal) && IsActiveLike(item.state)))
            {
                if (TryRankDefinition(rank.rankDefinitionId, out ProfessionalRankDefinition definition) && definition.GrantedPermissionFoundationIds.Contains(permissionId ?? string.Empty))
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanTeach(string personId, string professionId, string specializationId = "")
        {
            return ActiveDefinitions(personId).Any(definition => definition.TeachingEligibility
                && string.Equals(definition.ProfessionId, professionId ?? string.Empty, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(specializationId) || string.Equals(definition.SpecializationId, specializationId, StringComparison.Ordinal)));
        }

        public bool CanSupervise(string personId, string professionId, string specializationId = "")
        {
            return ActiveDefinitions(personId).Any(definition => definition.SupervisionEligibility
                && string.Equals(definition.ProfessionId, professionId ?? string.Empty, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(specializationId) || string.Equals(definition.SpecializationId, specializationId, StringComparison.Ordinal)));
        }

        public ProfessionalRankAdvancementResult EvaluateAdvancement(string personId, string requestedRankDefinitionId, string recognizingAuthorityId = "", bool perceived = false, bool privilegedDiagnostics = false)
        {
            List<string> satisfied = new List<string>();
            List<string> blockers = new List<string>();
            List<string> recommendations = new List<string>();
            List<string> alternatives = new List<string>();

            if (!KnownPerson(personId))
            {
                blockers.Add("person.missing");
            }

            if (!TryRankDefinition(requestedRankDefinitionId, out ProfessionalRankDefinition requestedRank))
            {
                blockers.Add("rank-definition.missing");
                return BuildAdvancementResult(personId, string.Empty, string.Empty, string.Empty, requestedRankDefinitionId, recognizingAuthorityId, false, perceived, satisfied, blockers, recommendations, alternatives, privilegedDiagnostics);
            }

            ProfessionalRankLadderDefinition ladder = FindLadder(requestedRank.ProfessionId, requestedRank.SpecializationId, requestedRank.Id);
            if (ladder == null)
            {
                blockers.Add("rank-ladder.missing");
            }

            ProfessionalRankRecordData current = ladder == null ? null : GetCurrentRank(personId, ladder.Id);
            string currentRankId = current?.rankDefinitionId ?? string.Empty;
            bool professionActive = professions != null && professions.QueryByProfession(requestedRank.ProfessionId, activeOnly: true).Any(item => string.Equals(item.PersonId, personId ?? string.Empty, StringComparison.Ordinal));
            if (professionActive)
            {
                satisfied.Add($"profession:{requestedRank.ProfessionId}");
            }
            else
            {
                blockers.Add($"profession:{requestedRank.ProfessionId}");
            }

            if (!string.IsNullOrWhiteSpace(requestedRank.SpecializationId))
            {
                bool hasSpecialization = professions != null && professions.QueryByProfession(requestedRank.ProfessionId, activeOnly: true).Any(item => string.Equals(item.PersonId, personId ?? string.Empty, StringComparison.Ordinal) && item.Data.specializationIds.Contains(requestedRank.SpecializationId));
                if (hasSpecialization)
                {
                    satisfied.Add($"specialization:{requestedRank.SpecializationId}");
                }
                else
                {
                    blockers.Add($"specialization:{requestedRank.SpecializationId}");
                }
            }

            if (requestedRank.PriorRankDefinitionIds.Count > 0)
            {
                bool priorSatisfied = requestedRank.PriorRankDefinitionIds.Any(prior => ranksById.Values.Any(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal) && string.Equals(item.rankDefinitionId, prior, StringComparison.Ordinal) && item.state != ProfessionalRankState.Invalid));
                if (priorSatisfied)
                {
                    satisfied.Add($"prior-rank:{string.Join(",", requestedRank.PriorRankDefinitionIds)}");
                }
                else
                {
                    blockers.Add($"prior-rank:{string.Join(",", requestedRank.PriorRankDefinitionIds)}");
                }
            }
            else
            {
                satisfied.Add("prior-rank:root");
            }

            if (current != null && IsProhibitedRankSkip(ladder, current.rankDefinitionId, requestedRank.Id, requestedRank))
            {
                blockers.Add("rank-skip.prohibited");
            }

            if (!AuthorityAllowed(requestedRank.RequiredAuthorityIds, recognizingAuthorityId, requestedRank.TrackKind, requestedRank.SelfClaimAllowed))
            {
                blockers.Add($"authority:{recognizingAuthorityId ?? string.Empty}");
            }
            else if (!string.IsNullOrWhiteSpace(recognizingAuthorityId))
            {
                satisfied.Add($"authority:{recognizingAuthorityId}");
            }

            foreach (string credentialDefinitionId in requestedRank.RequiredCredentialDefinitionIds)
            {
                bool hasCredential = credentials != null && credentials.QueryByRecipient(personId, activeOnly: true).Any(item => string.Equals(item.credentialDefinitionId, credentialDefinitionId, StringComparison.Ordinal));
                if (hasCredential)
                {
                    satisfied.Add($"credential:{credentialDefinitionId}");
                }
                else
                {
                    blockers.Add($"credential:{credentialDefinitionId}");
                }
            }

            foreach (string trainingProgramId in requestedRank.RequiredTrainingProgramIds)
            {
                bool completed = training != null && training.QueryByProgram(trainingProgramId).Any(item => string.Equals(item.PersonId, personId ?? string.Empty, StringComparison.Ordinal) && item.State == TrainingEnrollmentState.Completed);
                if (completed)
                {
                    satisfied.Add($"training:{trainingProgramId}");
                }
                else
                {
                    blockers.Add($"training:{trainingProgramId}");
                }
            }

            ProfessionalExperienceRequirementData experience = requestedRank.ExperienceRequirement;
            if (experience.minimumValidatedActivities > 0)
            {
                bool experienceSatisfied = professionalActivities != null && professionalActivities.EvaluateExperienceRequirement(personId, experience, out _);
                if (experienceSatisfied)
                {
                    satisfied.Add($"experience:{experience.professionId}");
                }
                else
                {
                    blockers.Add($"experience:{experience.professionId}");
                }
            }

            foreach (string examinationId in requestedRank.RequiredExaminationDefinitionIds)
            {
                bool passed = credentials != null && credentials.ExaminationAttempts.Any(item => string.Equals(item.applicantPersonId, personId ?? string.Empty, StringComparison.Ordinal)
                    && string.Equals(item.examinationDefinitionId, examinationId, StringComparison.Ordinal)
                    && item.state == CredentialExaminationAttemptState.Passed);
                if (passed)
                {
                    satisfied.Add($"examination:{examinationId}");
                }
                else
                {
                    blockers.Add($"examination:{examinationId}");
                }
            }

            if (ladder != null)
            {
                alternatives.AddRange(ladder.OrderedRankDefinitionIds.Where(rankId => !string.Equals(rankId, requestedRankDefinitionId, StringComparison.Ordinal)).Take(3));
            }

            bool eligible = blockers.Count == 0;
            bool perceivedEligible = perceived ? satisfied.Count > 0 && !blockers.Contains("rank-definition.missing") : eligible;
            return BuildAdvancementResult(personId, requestedRank.ProfessionId, requestedRank.SpecializationId, currentRankId, requestedRankDefinitionId, recognizingAuthorityId, eligible, perceivedEligible, satisfied, blockers, recommendations, alternatives, privilegedDiagnostics);
        }

        public ProfessionalRankOperationResult SubmitApplication(string applicationId, string personId, string requestedRankDefinitionId, string recognizingAuthorityId, ProfessionalRankAdvancementSnapshotData evaluation, string worldTime, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!TryRankDefinition(requestedRankDefinitionId, out ProfessionalRankDefinition rank))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingDefinition, "Rank definition is missing.", revision);
            }

            if (!KnownPerson(personId))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingPerson, "Applicant is unknown.", revision);
            }

            if (!AuthorityAllowed(rank.RequiredAuthorityIds, recognizingAuthorityId, rank.TrackKind, rank.SelfClaimAllowed))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.UnauthorizedAuthority, "Recognizing authority is not authorized for this rank.", revision);
            }

            string id = string.IsNullOrWhiteSpace(applicationId) ? $"rank-application.{personId}.{requestedRankDefinitionId}" : applicationId.Trim();
            if (applicationsById.ContainsKey(id))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.Duplicate, $"Rank application '{id}' already exists.", revision);
            }

            if (applicationsById.Values.Any(item => string.Equals(item.applicantPersonId, personId, StringComparison.Ordinal) && string.Equals(item.requestedRankDefinitionId, requestedRankDefinitionId, StringComparison.Ordinal) && IsActiveApplication(item.state)))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.DuplicateActiveApplication, "An active rank application already exists.", revision);
            }

            ProfessionalRankAdvancementResult current = evaluation == null ? EvaluateAdvancement(personId, requestedRankDefinitionId, recognizingAuthorityId, privilegedDiagnostics: true) : new ProfessionalRankAdvancementResult(evaluation);
            ProfessionalRankApplicationData application = new ProfessionalRankApplicationData
            {
                applicationId = id,
                applicantPersonId = personId,
                professionId = rank.ProfessionId,
                specializationId = rank.SpecializationId,
                currentRankDefinitionId = current.Snapshot.currentRankDefinitionId,
                requestedRankDefinitionId = requestedRankDefinitionId,
                recognizingAuthorityId = recognizingAuthorityId ?? string.Empty,
                submissionWorldTime = worldTime ?? string.Empty,
                evaluationSnapshot = current.Snapshot,
                supportingCredentialIds = credentials?.QueryByRecipient(personId, activeOnly: true).Where(item => rank.RequiredCredentialDefinitionIds.Contains(item.credentialDefinitionId)).Select(item => item.credentialId).ToArray() ?? Array.Empty<string>(),
                supportingExperienceEvidenceIds = professionalActivities?.BuildExperienceSummary(personId, rank.ExperienceRequirement.professionId).Evidence.Select(item => item.evidenceId).ToArray() ?? Array.Empty<string>(),
                supportingExaminationAttemptIds = credentials?.ExaminationAttempts.Where(item => string.Equals(item.applicantPersonId, personId, StringComparison.Ordinal) && rank.RequiredExaminationDefinitionIds.Contains(item.examinationDefinitionId)).Select(item => item.attemptId).ToArray() ?? Array.Empty<string>(),
                state = ProfessionalRankApplicationState.Submitted,
                accessPolicyId = rank.AccessPolicyId,
                provenance = transactionId ?? string.Empty,
                revision = 1L
            };

            if (preview)
            {
                return ProfessionalRankOperationResult.Success("Rank application previewed.", before, before, current, application, preview: true);
            }

            applicationsById[id] = application.Clone();
            revision++;
            dirty = true;
            AddHook(ProfessionalRankHistoryHookKind.ApplicationSubmitted, applicationId: id, personId: personId, authorityId: recognizingAuthorityId, worldTime: worldTime, transactionId: transactionId);
            return ProfessionalRankOperationResult.Success("Rank application submitted.", before, revision, current, application);
        }

        public ProfessionalRankOperationResult RequestAdditionalEvidence(string applicationId, string reviewerId, string reason, string worldTime, string transactionId, bool preview = false)
        {
            return SetApplicationState(applicationId, ProfessionalRankApplicationState.AwaitingEvidence, reviewerId, reason, worldTime, transactionId, "Rank application requires additional evidence.", preview);
        }

        public ProfessionalRankOperationResult RejectApplication(string applicationId, string reviewerId, string reason, string worldTime, string transactionId, bool preview = false)
        {
            return SetApplicationState(applicationId, ProfessionalRankApplicationState.Rejected, reviewerId, reason, worldTime, transactionId, "Rank application rejected.", preview);
        }

        public ProfessionalRankOperationResult WithdrawApplication(string applicationId, string transactionId, bool preview = false)
        {
            return SetApplicationState(applicationId, ProfessionalRankApplicationState.Withdrawn, string.Empty, "Withdrawn", string.Empty, transactionId, "Rank application withdrawn.", preview);
        }

        public ProfessionalRankOperationResult ApprovePromotion(string applicationId, string reviewerId, ProfessionalRankAdvancementSnapshotData currentEvaluation, string worldTime, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!applicationsById.TryGetValue(applicationId ?? string.Empty, out ProfessionalRankApplicationData application))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingApplication, "Rank application is missing.", revision);
            }

            if (application.state is ProfessionalRankApplicationState.Rejected or ProfessionalRankApplicationState.Withdrawn or ProfessionalRankApplicationState.Cancelled or ProfessionalRankApplicationState.Expired or ProfessionalRankApplicationState.Invalid)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.InvalidState, "Rank application cannot be approved from its current state.", revision);
            }

            ProfessionalRankAdvancementResult current = currentEvaluation == null
                ? EvaluateAdvancement(application.applicantPersonId, application.requestedRankDefinitionId, application.recognizingAuthorityId, privilegedDiagnostics: true)
                : new ProfessionalRankAdvancementResult(currentEvaluation);

            if (!current.AuthoritativeEligible)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingQualification, "Rank advancement requirements are not satisfied.", revision, current);
            }

            if (!application.evaluationSnapshot.SemanticallyEquals(current.Snapshot))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.StaleEvaluation, "Rank advancement evaluation snapshot is stale.", revision, current);
            }

            ProfessionalRankApplicationData updated = application.Clone();
            updated.state = ProfessionalRankApplicationState.Approved;
            updated.reviewerPersonId = reviewerId ?? string.Empty;
            updated.decisionWorldTime = worldTime ?? string.Empty;
            updated.decisionReason = "Approved";
            updated.revision++;

            if (preview)
            {
                return ProfessionalRankOperationResult.Success("Rank approval previewed.", before, before, current, updated, preview: true);
            }

            applicationsById[updated.applicationId] = updated.Clone();
            revision++;
            dirty = true;
            AddHook(ProfessionalRankHistoryHookKind.PromotionApproved, applicationId: updated.applicationId, personId: updated.applicantPersonId, authorityId: updated.recognizingAuthorityId, worldTime: worldTime, transactionId: transactionId);
            return ProfessionalRankOperationResult.Success("Rank application approved.", before, revision, current, updated);
        }

        public ProfessionalRankOperationResult PromotePerson(string rankRecordId, string applicationId, ProfessionalRankAdvancementSnapshotData expectedEvaluation, string worldTime, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!applicationsById.TryGetValue(applicationId ?? string.Empty, out ProfessionalRankApplicationData application))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingApplication, "Approved rank application is missing.", revision);
            }

            if (application.state != ProfessionalRankApplicationState.Approved)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.InvalidState, "Rank application is not approved.", revision);
            }

            if (!TryRankDefinition(application.requestedRankDefinitionId, out ProfessionalRankDefinition definition))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingDefinition, "Rank definition is missing.", revision);
            }

            ProfessionalRankAdvancementResult current = expectedEvaluation == null ? EvaluateAdvancement(application.applicantPersonId, application.requestedRankDefinitionId, application.recognizingAuthorityId, privilegedDiagnostics: true) : new ProfessionalRankAdvancementResult(expectedEvaluation);
            if (!current.AuthoritativeEligible)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingQualification, "Rank promotion requirements are not currently satisfied.", revision, current);
            }

            if (!application.evaluationSnapshot.SemanticallyEquals(current.Snapshot))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.StaleEvaluation, "Rank promotion evaluation snapshot is stale.", revision, current);
            }

            ProfessionalRankLadderDefinition ladder = FindLadder(definition.ProfessionId, definition.SpecializationId, definition.Id);
            if (ladder == null)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingLadder, "Rank ladder is missing.", revision, current);
            }

            string id = string.IsNullOrWhiteSpace(rankRecordId) ? $"rank-record.{application.applicantPersonId}.{definition.Id}" : rankRecordId.Trim();
            if (ranksById.ContainsKey(id))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.Duplicate, "Rank record already exists.", revision, current);
            }

            if (ranksById.Values.Any(item => string.Equals(item.personId, application.applicantPersonId, StringComparison.Ordinal) && string.Equals(item.ladderDefinitionId, ladder.Id, StringComparison.Ordinal) && IsActiveLike(item.state) && string.Equals(item.rankDefinitionId, definition.Id, StringComparison.Ordinal)))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.DuplicateActiveRank, "Person already has this active rank.", revision, current);
            }

            List<ProfessionalRankRecordData> changedPrior = new List<ProfessionalRankRecordData>();
            foreach (ProfessionalRankRecordData prior in ranksById.Values.Where(item => string.Equals(item.personId, application.applicantPersonId, StringComparison.Ordinal) && string.Equals(item.ladderDefinitionId, ladder.Id, StringComparison.Ordinal) && IsActiveLike(item.state)).OrderBy(item => item.rankRecordId, StringComparer.Ordinal))
            {
                ProfessionalRankRecordData updatedPrior = prior.Clone();
                updatedPrior.state = ProfessionalRankState.Former;
                updatedPrior.endWorldTime = worldTime ?? string.Empty;
                updatedPrior.revision++;
                updatedPrior.revisionHistory = AddSorted(updatedPrior.revisionHistory, $"superseded:{id}:{worldTime ?? string.Empty}");
                changedPrior.Add(updatedPrior);
            }

            ProfessionalRankRecordData rank = new ProfessionalRankRecordData
            {
                rankRecordId = id,
                personId = application.applicantPersonId,
                professionId = definition.ProfessionId,
                specializationId = definition.SpecializationId,
                ladderDefinitionId = ladder.Id,
                rankDefinitionId = definition.Id,
                state = ProfessionalRankState.Active,
                trackKind = definition.TrackKind == ProfessionalRankTrackKind.Either ? ProfessionalRankTrackKind.Formal : definition.TrackKind,
                recognizingAuthorityId = application.recognizingAuthorityId,
                issueWorldTime = worldTime ?? string.Empty,
                effectiveWorldTime = worldTime ?? string.Empty,
                supportingApplicationId = application.applicationId,
                supportingCredentialIds = application.supportingCredentialIds,
                supportingExperienceEvidenceIds = application.supportingExperienceEvidenceIds,
                supportingExaminationAttemptIds = application.supportingExaminationAttemptIds,
                accessPolicyId = definition.AccessPolicyId,
                provenance = transactionId ?? string.Empty,
                revisionHistory = new[] { $"promoted:{worldTime ?? string.Empty}" },
                revision = 1L
            };

            if (preview)
            {
                return ProfessionalRankOperationResult.Success("Rank promotion previewed.", before, before, current, rank: rank, preview: true);
            }

            foreach (ProfessionalRankRecordData prior in changedPrior)
            {
                ranksById[prior.rankRecordId] = prior.Clone();
            }

            ranksById[id] = rank.Clone();
            revision++;
            dirty = true;
            AddHook(ProfessionalRankHistoryHookKind.PersonPromoted, rankRecordId: id, applicationId: application.applicationId, personId: rank.personId, authorityId: rank.recognizingAuthorityId, worldTime: worldTime, transactionId: transactionId);
            return ProfessionalRankOperationResult.Success("Person promoted.", before, revision, current, rank: rank);
        }

        public ProfessionalRankOperationResult GrantInformalRankRecognition(string rankRecordId, string personId, string rankDefinitionId, string recognizerId, string worldTime, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!TryRankDefinition(rankDefinitionId, out ProfessionalRankDefinition definition))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingDefinition, "Rank definition is missing.", revision);
            }

            if (!definition.SelfClaimAllowed && definition.TrackKind == ProfessionalRankTrackKind.Formal)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.UnauthorizedAuthority, "Formal rank cannot be granted as informal recognition.", revision);
            }

            string id = string.IsNullOrWhiteSpace(rankRecordId) ? $"rank-informal.{personId}.{rankDefinitionId}" : rankRecordId.Trim();
            if (ranksById.ContainsKey(id))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.Duplicate, "Rank record already exists.", revision);
            }

            ProfessionalRankLadderDefinition ladder = FindLadder(definition.ProfessionId, definition.SpecializationId, definition.Id);
            if (ladder == null)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingLadder, "Informal rank ladder is missing.", revision);
            }

            if (ranksById.Values.Any(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(item.ladderDefinitionId, ladder.Id, StringComparison.Ordinal)
                && IsActiveLike(item.state)
                && string.Equals(item.rankDefinitionId, definition.Id, StringComparison.Ordinal)))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.DuplicateActiveRank, "Person already has this active informal rank.", revision);
            }

            List<ProfessionalRankRecordData> changedPrior = new List<ProfessionalRankRecordData>();
            foreach (ProfessionalRankRecordData prior in ranksById.Values
                .Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal)
                    && string.Equals(item.ladderDefinitionId, ladder.Id, StringComparison.Ordinal)
                    && IsActiveLike(item.state))
                .OrderBy(item => item.rankRecordId, StringComparer.Ordinal))
            {
                if (!TryRankDefinition(prior.rankDefinitionId, out ProfessionalRankDefinition priorDefinition)
                    || priorDefinition.RankOrder >= definition.RankOrder)
                {
                    continue;
                }

                ProfessionalRankRecordData updatedPrior = prior.Clone();
                updatedPrior.state = ProfessionalRankState.Former;
                updatedPrior.endWorldTime = worldTime ?? string.Empty;
                updatedPrior.replacedByRankRecordId = id;
                updatedPrior.revision++;
                updatedPrior.revisionHistory = AddSorted(updatedPrior.revisionHistory, $"superseded:{id}:{worldTime ?? string.Empty}");
                changedPrior.Add(updatedPrior);
            }

            ProfessionalRankRecordData rank = new ProfessionalRankRecordData
            {
                rankRecordId = id,
                personId = personId ?? string.Empty,
                professionId = definition.ProfessionId,
                specializationId = definition.SpecializationId,
                ladderDefinitionId = ladder?.Id ?? string.Empty,
                rankDefinitionId = definition.Id,
                state = ProfessionalRankState.Active,
                trackKind = ProfessionalRankTrackKind.Informal,
                recognizingAuthorityId = recognizerId ?? string.Empty,
                issueWorldTime = worldTime ?? string.Empty,
                effectiveWorldTime = worldTime ?? string.Empty,
                accessPolicyId = definition.AccessPolicyId,
                provenance = transactionId ?? string.Empty,
                replacesRankRecordId = changedPrior.OrderByDescending(item => ResolveRankOrder(item.rankDefinitionId)).Select(item => item.rankRecordId).FirstOrDefault() ?? string.Empty,
                revisionHistory = new[] { $"informal:{worldTime ?? string.Empty}" },
                revision = 1L
            };

            if (preview)
            {
                return ProfessionalRankOperationResult.Success("Informal rank recognition previewed.", before, before, rank: rank, preview: true);
            }

            foreach (ProfessionalRankRecordData prior in changedPrior)
            {
                ranksById[prior.rankRecordId] = prior.Clone();
            }

            ranksById[id] = rank.Clone();
            revision++;
            dirty = true;
            AddHook(ProfessionalRankHistoryHookKind.InformalRankRecognized, rankRecordId: id, personId: personId, authorityId: recognizerId, worldTime: worldTime, transactionId: transactionId);
            return ProfessionalRankOperationResult.Success("Informal rank recognized.", before, revision, rank: rank);
        }

        public ProfessionalRankOperationResult ApplyLateralRankChange(string sourceRankRecordId, string newRankRecordId, string targetRankDefinitionId, string worldTime, string transactionId, bool preview = false)
        {
            if (!ranksById.TryGetValue(sourceRankRecordId ?? string.Empty, out ProfessionalRankRecordData source))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingRank, "Source rank is missing.", revision);
            }

            ProfessionalRankAdvancementResult evaluation = EvaluateAdvancement(source.personId, targetRankDefinitionId, source.recognizingAuthorityId, privilegedDiagnostics: true);
            if (!evaluation.AuthoritativeEligible)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingQualification, "Lateral rank change requirements are not satisfied.", revision, evaluation);
            }

            return CreateReplacementRank(source, newRankRecordId, targetRankDefinitionId, ProfessionalRankState.Replaced, ProfessionalRankHistoryHookKind.LateralRankChanged, worldTime, transactionId, preview);
        }

        public ProfessionalRankOperationResult ApplyPrivilegedCorrection(string rankRecordId, string correction, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!ranksById.TryGetValue(rankRecordId ?? string.Empty, out ProfessionalRankRecordData rank))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingRank, "Rank is missing.", revision);
            }

            ProfessionalRankRecordData updated = rank.Clone();
            updated.revision++;
            updated.revisionHistory = AddSorted(updated.revisionHistory, $"correction:{correction ?? string.Empty}");
            if (preview)
            {
                return ProfessionalRankOperationResult.Success("Rank correction previewed.", before, before, rank: updated, preview: true);
            }

            ranksById[updated.rankRecordId] = updated.Clone();
            revision++;
            dirty = true;
            AddHook(ProfessionalRankHistoryHookKind.RankCorrected, rankRecordId: updated.rankRecordId, personId: updated.personId, transactionId: transactionId);
            return ProfessionalRankOperationResult.Success("Rank corrected.", before, revision, rank: updated);
        }

        public ProfessionalRankOperationResult RecordQualifyingAchievement(ProfessionalQualifyingAchievementData achievement, string transactionId, bool preview = false)
        {
            long before = revision;
            ProfessionalQualifyingAchievementData data = achievement?.Clone();
            if (data == null || string.IsNullOrWhiteSpace(data.achievementId) || string.IsNullOrWhiteSpace(data.personId) || string.IsNullOrWhiteSpace(data.professionId))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.InvalidRequest, "Qualifying achievement requires IDs.", revision);
            }

            if (achievementsById.ContainsKey(data.achievementId))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.Duplicate, "Qualifying achievement already exists.", revision);
            }

            if (!KnownPerson(data.personId))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingPerson, "Achievement person is unknown.", revision);
            }

            if (string.IsNullOrWhiteSpace(data.sourceActivityId))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingAchievement, "Qualifying achievement must reference authoritative activity.", revision);
            }

            if (preview)
            {
                return ProfessionalRankOperationResult.Success("Qualifying achievement previewed.", before, before, achievement: data, preview: true);
            }

            achievementsById[data.achievementId] = data.Clone();
            revision++;
            dirty = true;
            return ProfessionalRankOperationResult.Success("Qualifying achievement recorded.", before, revision, achievement: data);
        }

        public ProfessionalRankAdvancementResult EvaluateMastery(string personId, string masteryDefinitionId, string recognizingAuthorityId = "", bool perceived = false, bool privilegedDiagnostics = false)
        {
            List<string> satisfied = new List<string>();
            List<string> blockers = new List<string>();
            List<string> recommendations = new List<string>();
            if (!KnownPerson(personId))
            {
                blockers.Add("person.missing");
            }

            if (!TryMasteryDefinition(masteryDefinitionId, out ProfessionalMasteryDefinition definition))
            {
                blockers.Add("mastery-definition.missing");
                return BuildAdvancementResult(personId, string.Empty, string.Empty, string.Empty, masteryDefinitionId, recognizingAuthorityId, false, perceived, satisfied, blockers, recommendations, Array.Empty<string>(), privilegedDiagnostics);
            }

            if (HasActiveRank(personId, definition.RequiredRankDefinitionId, allowInformal: definition.RecognitionTrack != ProfessionalRankTrackKind.Formal))
            {
                satisfied.Add($"rank:{definition.RequiredRankDefinitionId}");
            }
            else
            {
                blockers.Add($"rank:{definition.RequiredRankDefinitionId}");
            }

            foreach (string credentialDefinitionId in definition.RequiredCredentialDefinitionIds)
            {
                bool hasCredential = credentials != null && credentials.QueryByRecipient(personId, activeOnly: true).Any(item => string.Equals(item.credentialDefinitionId, credentialDefinitionId, StringComparison.Ordinal));
                if (hasCredential)
                {
                    satisfied.Add($"credential:{credentialDefinitionId}");
                }
                else
                {
                    blockers.Add($"credential:{credentialDefinitionId}");
                }
            }

            ProfessionalExperienceRequirementData experience = definition.ExperienceRequirement;
            if (experience.minimumValidatedActivities > 0 && professionalActivities != null && professionalActivities.EvaluateExperienceRequirement(personId, experience, out ProfessionalExperienceSummary summary))
            {
                satisfied.Add($"experience:{experience.professionId}");
                if (definition.RequiredBreadthCount > 0 && summary.Evidence.Select(item => item.category).Distinct().Count() < definition.RequiredBreadthCount)
                {
                    blockers.Add("experience.breadth");
                }

                if (definition.RequiredDepthQuality > 0 && summary.Evidence.All(item => item.quality < definition.RequiredDepthQuality))
                {
                    blockers.Add("experience.depth");
                }

                if (definition.RequiredIndependentWorkCount > 0 && summary.Evidence.Count(item => item.supervisionLevel == TrainingSupervisionLevel.IndependentWithReview) < definition.RequiredIndependentWorkCount)
                {
                    blockers.Add("experience.independent");
                }

                if (definition.RequiredTeachingOrLeadershipCount > 0 && summary.Evidence.Count(item => item.category == ProfessionalExperienceCategory.Teaching || item.responsibility >= ProfessionalResponsibilityLevel.Leader) < definition.RequiredTeachingOrLeadershipCount)
                {
                    blockers.Add("experience.teaching-or-leadership");
                }
            }
            else if (experience.minimumValidatedActivities > 0)
            {
                blockers.Add($"experience:{experience.professionId}");
            }

            foreach (string achievementId in definition.RequiredAchievementIds)
            {
                if (achievementsById.TryGetValue(achievementId, out ProfessionalQualifyingAchievementData achievement) && string.Equals(achievement.personId, personId ?? string.Empty, StringComparison.Ordinal))
                {
                    satisfied.Add($"achievement:{achievementId}");
                }
                else
                {
                    blockers.Add($"achievement:{achievementId}");
                }
            }

            if (!AuthorityAllowed(definition.RequiredAuthorityIds, recognizingAuthorityId, definition.RecognitionTrack, allowSelfClaim: false))
            {
                blockers.Add($"authority:{recognizingAuthorityId ?? string.Empty}");
            }
            else if (!string.IsNullOrWhiteSpace(recognizingAuthorityId))
            {
                satisfied.Add($"authority:{recognizingAuthorityId}");
            }

            bool eligible = blockers.Count == 0;
            bool perceivedEligible = perceived ? satisfied.Count > 0 && !blockers.Contains("mastery-definition.missing") : eligible;
            return BuildAdvancementResult(personId, definition.ProfessionId, definition.SpecializationId, definition.RequiredRankDefinitionId, masteryDefinitionId, recognizingAuthorityId, eligible, perceivedEligible, satisfied, blockers, recommendations, Array.Empty<string>(), privilegedDiagnostics);
        }

        public ProfessionalRankOperationResult GrantMastery(string masteryRecordId, string personId, string masteryDefinitionId, string recognizingAuthorityId, ProfessionalRankAdvancementSnapshotData expectedEvaluation, string worldTime, string transactionId, bool preview = false)
        {
            long before = revision;
            if (!TryMasteryDefinition(masteryDefinitionId, out ProfessionalMasteryDefinition definition))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingDefinition, "Mastery definition is missing.", revision);
            }

            ProfessionalRankAdvancementResult current = expectedEvaluation == null ? EvaluateMastery(personId, masteryDefinitionId, recognizingAuthorityId, privilegedDiagnostics: true) : new ProfessionalRankAdvancementResult(expectedEvaluation);
            if (!current.AuthoritativeEligible)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingQualification, "Mastery requirements are not satisfied.", revision, current);
            }

            if (masteriesById.Values.Any(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal) && string.Equals(item.masteryDefinitionId, masteryDefinitionId, StringComparison.Ordinal) && IsActiveLike(item.state)))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.DuplicateActiveMastery, "Active mastery already exists.", revision, current);
            }

            string id = string.IsNullOrWhiteSpace(masteryRecordId) ? $"mastery-record.{personId}.{masteryDefinitionId}" : masteryRecordId.Trim();
            ProfessionalMasteryRecordData mastery = new ProfessionalMasteryRecordData
            {
                masteryRecordId = id,
                personId = personId ?? string.Empty,
                professionId = definition.ProfessionId,
                specializationId = definition.SpecializationId,
                masteryDefinitionId = masteryDefinitionId,
                state = ProfessionalRankState.Active,
                trackKind = definition.RecognitionTrack == ProfessionalRankTrackKind.Either ? ProfessionalRankTrackKind.Formal : definition.RecognitionTrack,
                recognizingAuthorityId = recognizingAuthorityId ?? string.Empty,
                issueWorldTime = worldTime ?? string.Empty,
                supportingRankRecordIds = ranksById.Values.Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal) && string.Equals(item.rankDefinitionId, definition.RequiredRankDefinitionId, StringComparison.Ordinal)).Select(item => item.rankRecordId).ToArray(),
                supportingCredentialIds = credentials?.QueryByRecipient(personId, activeOnly: true).Where(item => definition.RequiredCredentialDefinitionIds.Contains(item.credentialDefinitionId)).Select(item => item.credentialId).ToArray() ?? Array.Empty<string>(),
                supportingExperienceEvidenceIds = professionalActivities?.BuildExperienceSummary(personId, definition.ExperienceRequirement.professionId).Evidence.Select(item => item.evidenceId).ToArray() ?? Array.Empty<string>(),
                supportingAchievementIds = definition.RequiredAchievementIds.ToArray(),
                accessPolicyId = definition.AccessPolicyId,
                provenance = transactionId ?? string.Empty,
                revisionHistory = new[] { $"mastery:{worldTime ?? string.Empty}" },
                revision = 1L
            };

            if (preview)
            {
                return ProfessionalRankOperationResult.Success("Mastery recognition previewed.", before, before, current, mastery: mastery, preview: true);
            }

            masteriesById[id] = mastery.Clone();
            revision++;
            dirty = true;
            AddHook(ProfessionalRankHistoryHookKind.MasteryRecognized, masteryRecordId: id, personId: personId, authorityId: recognizingAuthorityId, worldTime: worldTime, transactionId: transactionId);
            return ProfessionalRankOperationResult.Success("Mastery recognized.", before, revision, current, mastery: mastery);
        }

        public ProfessionalRankOperationResult SuspendRank(string rankRecordId, string worldTime, string transactionId, bool preview = false) => SetRankState(rankRecordId, ProfessionalRankState.Suspended, ProfessionalRankHistoryHookKind.RankSuspended, "Rank suspended.", worldTime, transactionId, preview);
        public ProfessionalRankOperationResult ReinstateRank(string rankRecordId, string worldTime, string transactionId, bool preview = false) => SetRankState(rankRecordId, ProfessionalRankState.Active, ProfessionalRankHistoryHookKind.RankReinstated, "Rank reinstated.", worldTime, transactionId, preview);
        public ProfessionalRankOperationResult RevokeRank(string rankRecordId, string worldTime, string transactionId, bool preview = false) => SetRankState(rankRecordId, ProfessionalRankState.Revoked, ProfessionalRankHistoryHookKind.RankRevoked, "Rank revoked.", worldTime, transactionId, preview);
        public ProfessionalRankOperationResult RetireRank(string rankRecordId, string worldTime, string transactionId, bool preview = false) => SetRankState(rankRecordId, ProfessionalRankState.Retired, ProfessionalRankHistoryHookKind.PersonRetired, "Rank retired.", worldTime, transactionId, preview);
        public ProfessionalRankOperationResult MarkRankDisputed(string rankRecordId, string worldTime, string transactionId, bool preview = false) => SetRankState(rankRecordId, ProfessionalRankState.Disputed, ProfessionalRankHistoryHookKind.RankDisputeResolved, "Rank disputed.", worldTime, transactionId, preview);

        public ProfessionalRankOperationResult DemotePerson(string sourceRankRecordId, string demotedRankRecordId, string targetRankDefinitionId, string worldTime, string transactionId, bool preview = false)
        {
            if (!ranksById.TryGetValue(sourceRankRecordId ?? string.Empty, out ProfessionalRankRecordData source))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingRank, "Source rank is missing.", revision);
            }

            return CreateReplacementRank(source, demotedRankRecordId, targetRankDefinitionId, ProfessionalRankState.Demoted, ProfessionalRankHistoryHookKind.PersonDemoted, worldTime, transactionId, preview);
        }

        public ProfessionalRankOperationResult ReplaceRank(string sourceRankRecordId, string replacementRankRecordId, string targetRankDefinitionId, string worldTime, string transactionId, bool preview = false)
        {
            if (!ranksById.TryGetValue(sourceRankRecordId ?? string.Empty, out ProfessionalRankRecordData source))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingRank, "Source rank is missing.", revision);
            }

            return CreateReplacementRank(source, replacementRankRecordId, targetRankDefinitionId, ProfessionalRankState.Replaced, ProfessionalRankHistoryHookKind.RankReplaced, worldTime, transactionId, preview);
        }

        public ProfessionalRankProjection<ProfessionalRankRecordData> ProjectRank(string rankRecordId, ProfessionalRankProjectionAudience audience, InformationAccessDecision decision)
        {
            if (!ranksById.TryGetValue(rankRecordId ?? string.Empty, out ProfessionalRankRecordData rank))
            {
                return new ProfessionalRankProjection<ProfessionalRankRecordData>(null, audience, decision, false, true, Array.Empty<string>(), ProfessionalRankInformationSubject.ProtectedFields);
            }

            bool privileged = audience == ProfessionalRankProjectionAudience.PrivilegedDebug || audience == ProfessionalRankProjectionAudience.Holder || audience == ProfessionalRankProjectionAudience.RecognizingAuthority;
            bool denied = decision != null && decision.Denied;
            bool redacted = denied || (decision != null && (decision.Decision == InformationAccessDecisionKind.RedactedAccess || decision.Decision == InformationAccessDecisionKind.PartialAccess)) || (!privileged && IsSecretRank(rank.rankDefinitionId));
            ProfessionalRankRecordData projected = denied ? null : rank.Clone();
            if (projected != null && redacted)
            {
                projected.supportingExperienceEvidenceIds = Array.Empty<string>();
                projected.supportingExaminationAttemptIds = Array.Empty<string>();
                projected.provenance = string.Empty;
            }

            return new ProfessionalRankProjection<ProfessionalRankRecordData>(projected, audience, decision, redacted, denied, redacted ? new[] { "rank", "state" } : new[] { "all" }, redacted ? ProfessionalRankInformationSubject.ProtectedFields : Array.Empty<string>());
        }

        public ProfessionalRankRuntimeSaveData CreateSaveData()
        {
            return new ProfessionalRankRuntimeSaveData
            {
                schemaVersion = ProfessionalRankRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                applications = applicationsById.Values.OrderBy(item => item.applicationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                ranks = ranksById.Values.OrderBy(item => item.rankRecordId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                masteries = masteriesById.Values.OrderBy(item => item.masteryRecordId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                achievements = achievementsById.Values.OrderBy(item => item.achievementId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public ProfessionalRankOperationResult RestoreFromSaveData(ProfessionalRankRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, PersonProfessionRuntime professionRuntime, TrainingRuntime trainingRuntime, ProfessionalActivityRuntime activityRuntime, CredentialRuntime credentialRuntime, IEnumerable<string> persons, IEnumerable<string> authorities, bool restoring = true)
        {
            long before = revision;
            ProfessionalRankRuntimeSaveData candidate = (saveData ?? new ProfessionalRankRuntimeSaveData()).Clone();
            if (!ValidateSaveData(candidate, definitionRegistry, professionRuntime, trainingRuntime, activityRuntime, credentialRuntime, persons, authorities, out string failure))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.CorruptSave, failure, before);
            }

            Configure(definitionRegistry, professionRuntime, trainingRuntime, activityRuntime, credentialRuntime, persons, authorities);
            RestoreInternal(candidate);
            dirty = false;
            return ProfessionalRankOperationResult.Success(restoring ? "Professional rank state restored." : "Professional rank state loaded.", before, revision);
        }

        public static bool ValidateSaveData(ProfessionalRankRuntimeSaveData saveData, DefinitionRegistry registry, PersonProfessionRuntime professions, TrainingRuntime training, ProfessionalActivityRuntime activities, CredentialRuntime credentials, IEnumerable<string> knownPersons, IEnumerable<string> knownAuthorities, out string failure)
        {
            failure = string.Empty;
            ProfessionalRankRuntimeSaveData data = (saveData ?? new ProfessionalRankRuntimeSaveData()).Clone();
            if (data.schemaVersion != ProfessionalRankRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported professional rank schema version {data.schemaVersion}.";
                return false;
            }

            HashSet<string> persons = new HashSet<string>((knownPersons ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            HashSet<string> authorities = new HashSet<string>((knownAuthorities ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            if (HasDuplicate(data.applications.Select(item => item.applicationId), out string duplicateApplication))
            {
                failure = $"Duplicate rank application ID '{duplicateApplication}'.";
                return false;
            }

            if (HasDuplicate(data.ranks.Select(item => item.rankRecordId), out string duplicateRank))
            {
                failure = $"Duplicate rank record ID '{duplicateRank}'.";
                return false;
            }

            if (HasDuplicate(data.masteries.Select(item => item.masteryRecordId), out string duplicateMastery))
            {
                failure = $"Duplicate mastery record ID '{duplicateMastery}'.";
                return false;
            }

            if (HasDuplicate(data.achievements.Select(item => item.achievementId), out string duplicateAchievement))
            {
                failure = $"Duplicate qualifying achievement ID '{duplicateAchievement}'.";
                return false;
            }

            foreach (ProfessionalRankApplicationData application in data.applications)
            {
                if (string.IsNullOrWhiteSpace(application.applicationId) || string.IsNullOrWhiteSpace(application.applicantPersonId) || string.IsNullOrWhiteSpace(application.requestedRankDefinitionId))
                {
                    failure = "Rank application has missing required IDs.";
                    return false;
                }

                if (persons.Count > 0 && !persons.Contains(application.applicantPersonId))
                {
                    failure = $"Rank application '{application.applicationId}' references unknown Person '{application.applicantPersonId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(application.requestedRankDefinitionId, out ProfessionalRankDefinition _))
                {
                    failure = $"Rank application '{application.applicationId}' references missing Rank '{application.requestedRankDefinitionId}'.";
                    return false;
                }
            }

            foreach (ProfessionalRankRecordData rank in data.ranks)
            {
                if (string.IsNullOrWhiteSpace(rank.rankRecordId) || string.IsNullOrWhiteSpace(rank.personId) || string.IsNullOrWhiteSpace(rank.rankDefinitionId))
                {
                    failure = "Rank record has missing required IDs.";
                    return false;
                }

                if (persons.Count > 0 && !persons.Contains(rank.personId))
                {
                    failure = $"Rank record '{rank.rankRecordId}' references unknown Person '{rank.personId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(rank.rankDefinitionId, out ProfessionalRankDefinition definition))
                {
                    failure = $"Rank record '{rank.rankRecordId}' references missing Rank '{rank.rankDefinitionId}'.";
                    return false;
                }

                if (!string.Equals(rank.professionId, definition.ProfessionId, StringComparison.Ordinal) || !string.Equals(rank.specializationId, definition.SpecializationId, StringComparison.Ordinal))
                {
                    failure = $"Rank record '{rank.rankRecordId}' does not match its Rank definition.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(rank.supportingApplicationId) && data.applications.All(item => !string.Equals(item.applicationId, rank.supportingApplicationId, StringComparison.Ordinal)))
                {
                    failure = $"Rank record '{rank.rankRecordId}' references missing application '{rank.supportingApplicationId}'.";
                    return false;
                }

                if (rank.state == ProfessionalRankState.Active && string.IsNullOrWhiteSpace(rank.ladderDefinitionId))
                {
                    failure = $"Active Rank record '{rank.rankRecordId}' has no ladder.";
                    return false;
                }
            }

            foreach (var group in data.ranks.Where(rank => IsActiveLike(rank.state)).GroupBy(rank => $"{rank.personId}|{rank.ladderDefinitionId}", StringComparer.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                {
                    failure = $"Multiple active ranks exist for exclusive ladder '{group.Key}'.";
                    return false;
                }
            }

            foreach (ProfessionalMasteryRecordData mastery in data.masteries)
            {
                if (string.IsNullOrWhiteSpace(mastery.masteryRecordId) || string.IsNullOrWhiteSpace(mastery.personId) || string.IsNullOrWhiteSpace(mastery.masteryDefinitionId))
                {
                    failure = "Mastery record has missing required IDs.";
                    return false;
                }

                if (persons.Count > 0 && !persons.Contains(mastery.personId))
                {
                    failure = $"Mastery record '{mastery.masteryRecordId}' references unknown Person '{mastery.personId}'.";
                    return false;
                }

                if (registry == null || !registry.TryGet(mastery.masteryDefinitionId, out ProfessionalMasteryDefinition _))
                {
                    failure = $"Mastery record '{mastery.masteryRecordId}' references missing Mastery '{mastery.masteryDefinitionId}'.";
                    return false;
                }
            }

            foreach (ProfessionalQualifyingAchievementData achievement in data.achievements)
            {
                if (string.IsNullOrWhiteSpace(achievement.achievementId) || string.IsNullOrWhiteSpace(achievement.personId) || string.IsNullOrWhiteSpace(achievement.sourceActivityId))
                {
                    failure = "Qualifying achievement has missing required IDs.";
                    return false;
                }

                if (persons.Count > 0 && !persons.Contains(achievement.personId))
                {
                    failure = $"Qualifying achievement '{achievement.achievementId}' references unknown Person '{achievement.personId}'.";
                    return false;
                }
            }

            _ = professions;
            _ = training;
            _ = activities;
            _ = credentials;
            _ = authorities;
            return true;
        }

        private ProfessionalRankOperationResult SetApplicationState(string applicationId, ProfessionalRankApplicationState state, string reviewerId, string reason, string worldTime, string transactionId, string message, bool preview)
        {
            long before = revision;
            if (!applicationsById.TryGetValue(applicationId ?? string.Empty, out ProfessionalRankApplicationData application))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingApplication, "Rank application is missing.", revision);
            }

            ProfessionalRankApplicationData updated = application.Clone();
            updated.state = state;
            updated.reviewerPersonId = reviewerId ?? string.Empty;
            updated.decisionReason = reason ?? string.Empty;
            updated.decisionWorldTime = worldTime ?? string.Empty;
            updated.revision++;
            if (preview)
            {
                return ProfessionalRankOperationResult.Success($"{message} Preview.", before, before, application: updated, preview: true);
            }

            applicationsById[updated.applicationId] = updated.Clone();
            revision++;
            dirty = true;
            return ProfessionalRankOperationResult.Success(message, before, revision, application: updated);
        }

        private ProfessionalRankOperationResult SetRankState(string rankRecordId, ProfessionalRankState state, ProfessionalRankHistoryHookKind hook, string message, string worldTime, string transactionId, bool preview)
        {
            long before = revision;
            if (!ranksById.TryGetValue(rankRecordId ?? string.Empty, out ProfessionalRankRecordData rank))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingRank, "Rank is missing.", revision);
            }

            if (rank.state == ProfessionalRankState.Revoked && state == ProfessionalRankState.Active)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.InvalidTransition, "Revoked rank cannot be reinstated.", revision);
            }

            ProfessionalRankRecordData updated = rank.Clone();
            updated.state = state;
            if (state is ProfessionalRankState.Revoked or ProfessionalRankState.Retired or ProfessionalRankState.Demoted or ProfessionalRankState.Replaced)
            {
                updated.endWorldTime = worldTime ?? string.Empty;
            }

            updated.revision++;
            updated.revisionHistory = AddSorted(updated.revisionHistory, $"{state}:{worldTime ?? string.Empty}");
            if (preview)
            {
                return ProfessionalRankOperationResult.Success($"{message} Preview.", before, before, rank: updated, preview: true);
            }

            ranksById[updated.rankRecordId] = updated.Clone();
            revision++;
            dirty = true;
            AddHook(hook, rankRecordId: updated.rankRecordId, personId: updated.personId, authorityId: updated.recognizingAuthorityId, worldTime: worldTime, transactionId: transactionId);
            return ProfessionalRankOperationResult.Success(message, before, revision, rank: updated);
        }

        private ProfessionalRankOperationResult CreateReplacementRank(ProfessionalRankRecordData source, string newRankRecordId, string targetRankDefinitionId, ProfessionalRankState sourceTerminalState, ProfessionalRankHistoryHookKind hook, string worldTime, string transactionId, bool preview)
        {
            long before = revision;
            if (!TryRankDefinition(targetRankDefinitionId, out ProfessionalRankDefinition target))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingDefinition, "Target rank definition is missing.", revision);
            }

            if (!string.Equals(source.professionId, target.ProfessionId, StringComparison.Ordinal))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.InvalidTransition, "Replacement rank must remain in the same profession.", revision);
            }

            string id = string.IsNullOrWhiteSpace(newRankRecordId) ? $"rank-record.{source.personId}.{targetRankDefinitionId}.{revision + 1}" : newRankRecordId.Trim();
            if (ranksById.ContainsKey(id))
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.Duplicate, "Replacement rank already exists.", revision);
            }

            ProfessionalRankLadderDefinition ladder = FindLadder(target.ProfessionId, target.SpecializationId, target.Id);
            ProfessionalRankRecordData old = source.Clone();
            old.state = sourceTerminalState;
            old.replacedByRankRecordId = id;
            old.endWorldTime = worldTime ?? string.Empty;
            old.revision++;
            old.revisionHistory = AddSorted(old.revisionHistory, $"replaced:{id}:{worldTime ?? string.Empty}");
            ProfessionalRankRecordData replacement = source.Clone();
            replacement.rankRecordId = id;
            replacement.rankDefinitionId = target.Id;
            replacement.specializationId = target.SpecializationId;
            replacement.ladderDefinitionId = ladder?.Id ?? source.ladderDefinitionId;
            replacement.state = ProfessionalRankState.Active;
            replacement.issueWorldTime = worldTime ?? string.Empty;
            replacement.effectiveWorldTime = worldTime ?? string.Empty;
            replacement.endWorldTime = string.Empty;
            replacement.replacesRankRecordId = source.rankRecordId;
            replacement.replacedByRankRecordId = string.Empty;
            replacement.revision = 1L;
            replacement.revisionHistory = new[] { $"replacement:{source.rankRecordId}:{worldTime ?? string.Empty}" };
            if (preview)
            {
                return ProfessionalRankOperationResult.Success("Rank replacement previewed.", before, before, rank: replacement, preview: true);
            }

            ranksById[old.rankRecordId] = old.Clone();
            ranksById[replacement.rankRecordId] = replacement.Clone();
            revision++;
            dirty = true;
            AddHook(hook, rankRecordId: replacement.rankRecordId, personId: replacement.personId, authorityId: replacement.recognizingAuthorityId, worldTime: worldTime, transactionId: transactionId);
            return ProfessionalRankOperationResult.Success("Rank replacement applied.", before, revision, rank: replacement);
        }

        private void RestoreInternal(ProfessionalRankRuntimeSaveData data)
        {
            applicationsById.Clear();
            ranksById.Clear();
            masteriesById.Clear();
            achievementsById.Clear();
            historyHooks.Clear();
            foreach (ProfessionalRankApplicationData application in data.applications.OrderBy(item => item.applicationId, StringComparer.Ordinal))
            {
                applicationsById[application.applicationId] = application.Clone();
            }

            foreach (ProfessionalRankRecordData rank in data.ranks.OrderBy(item => item.rankRecordId, StringComparer.Ordinal))
            {
                ranksById[rank.rankRecordId] = rank.Clone();
            }

            foreach (ProfessionalMasteryRecordData mastery in data.masteries.OrderBy(item => item.masteryRecordId, StringComparer.Ordinal))
            {
                masteriesById[mastery.masteryRecordId] = mastery.Clone();
            }

            foreach (ProfessionalQualifyingAchievementData achievement in data.achievements.OrderBy(item => item.achievementId, StringComparer.Ordinal))
            {
                achievementsById[achievement.achievementId] = achievement.Clone();
            }

            revision = Math.Max(0L, data.revision);
        }

        private ProfessionalRankAdvancementResult BuildAdvancementResult(string personId, string professionId, string specializationId, string currentRankId, string requestedRankId, string recognizingAuthorityId, bool authoritative, bool perceived, List<string> satisfied, List<string> blockers, List<string> recommendations, IEnumerable<string> alternatives, bool privilegedDiagnostics)
        {
            string[] satisfiedClean = ProfessionalRankAdvancementSnapshotData.Clean(satisfied);
            string[] blockerClean = ProfessionalRankAdvancementSnapshotData.Clean(blockers);
            string hash = string.Join("|", new[] { personId ?? string.Empty, professionId ?? string.Empty, specializationId ?? string.Empty, currentRankId ?? string.Empty, requestedRankId ?? string.Empty, recognizingAuthorityId ?? string.Empty, authoritative.ToString(), perceived.ToString() }.Concat(satisfiedClean).Concat(blockerClean));
            ProfessionalRankAdvancementSnapshotData snapshot = new ProfessionalRankAdvancementSnapshotData
            {
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                currentRankDefinitionId = currentRankId ?? string.Empty,
                requestedRankDefinitionId = requestedRankId ?? string.Empty,
                recognizingAuthorityId = recognizingAuthorityId ?? string.Empty,
                authoritativeEligible = authoritative,
                perceivedEligible = perceived,
                satisfiedRequirementIds = satisfiedClean,
                blockingRequirementIds = blockerClean,
                recommendationIds = ProfessionalRankAdvancementSnapshotData.Clean(recommendations),
                alternativeRankDefinitionIds = ProfessionalRankAdvancementSnapshotData.Clean(alternatives),
                professionRevision = professions?.Revision ?? 0L,
                trainingRevision = training?.Revision ?? 0L,
                activityRevision = professionalActivities?.Revision ?? 0L,
                credentialRevision = CredentialQualificationRevision(),
                rankRevision = revision,
                evaluationHash = hash,
                privilegedDiagnostics = privilegedDiagnostics ? string.Join(";", blockerClean) : string.Empty,
                redactedDiagnostics = blockerClean.Length == 0 ? "Eligible" : "Requirements not satisfied"
            };
            return new ProfessionalRankAdvancementResult(snapshot);
        }

        private long CredentialQualificationRevision()
        {
            return credentials == null ? 0L : credentials.Revision;
        }

        private IEnumerable<ProfessionalRankDefinition> ActiveDefinitions(string personId)
        {
            foreach (ProfessionalRankRecordData rank in ranksById.Values.Where(item => string.Equals(item.personId, personId ?? string.Empty, StringComparison.Ordinal) && IsActiveLike(item.state)))
            {
                if (TryRankDefinition(rank.rankDefinitionId, out ProfessionalRankDefinition definition))
                {
                    yield return definition;
                }
            }
        }

        private bool AuthorityAllowed(IReadOnlyList<string> allowedAuthorities, string authorityId, ProfessionalRankTrackKind trackKind, bool allowSelfClaim)
        {
            if (trackKind == ProfessionalRankTrackKind.Informal && allowSelfClaim)
            {
                return true;
            }

            if (allowedAuthorities == null || allowedAuthorities.Count == 0)
            {
                return allowSelfClaim || trackKind == ProfessionalRankTrackKind.Informal;
            }

            if (string.IsNullOrWhiteSpace(authorityId))
            {
                return false;
            }

            return allowedAuthorities.Contains(authorityId) && (knownAuthorityIds.Count == 0 || knownAuthorityIds.Contains(authorityId));
        }

        private ProfessionalRankLadderDefinition FindLadder(string professionId, string specializationId, string rankId)
        {
            if (registry == null)
            {
                return null;
            }

            return registry.DefinitionsById.Values
                .OfType<ProfessionalRankLadderDefinition>()
                .Where(ladder => string.Equals(ladder.ProfessionId, professionId ?? string.Empty, StringComparison.Ordinal)
                    && string.Equals(ladder.SpecializationId, specializationId ?? string.Empty, StringComparison.Ordinal)
                    && ladder.OrderedRankDefinitionIds.Contains(rankId ?? string.Empty))
                .OrderBy(ladder => ladder.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private int ResolveRankOrder(string rankDefinitionId)
        {
            return TryRankDefinition(rankDefinitionId, out ProfessionalRankDefinition definition) ? definition.RankOrder : -1;
        }

        private static bool IsProhibitedRankSkip(ProfessionalRankLadderDefinition ladder, string currentRankDefinitionId, string requestedRankDefinitionId, ProfessionalRankDefinition requestedRank)
        {
            if (ladder == null || requestedRank == null || ladder.AllowRankSkipping || requestedRank.AllowRankSkipping)
            {
                return false;
            }

            IReadOnlyList<string> ordered = ladder.OrderedRankDefinitionIds;
            int currentIndex = IndexOf(ordered, currentRankDefinitionId);
            int requestedIndex = IndexOf(ordered, requestedRankDefinitionId);
            return currentIndex >= 0 && requestedIndex >= 0 && requestedIndex > currentIndex + 1;
        }

        private static int IndexOf(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < (values?.Count ?? 0); i++)
            {
                if (string.Equals(values[i], value ?? string.Empty, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool TryRankDefinition(string rankDefinitionId, out ProfessionalRankDefinition definition)
        {
            if (registry != null && !string.IsNullOrWhiteSpace(rankDefinitionId) && registry.TryGet(rankDefinitionId, out definition))
            {
                return true;
            }

            definition = null;
            return false;
        }

        private bool TryMasteryDefinition(string masteryDefinitionId, out ProfessionalMasteryDefinition definition)
        {
            if (registry != null && !string.IsNullOrWhiteSpace(masteryDefinitionId) && registry.TryGet(masteryDefinitionId, out definition))
            {
                return true;
            }

            definition = null;
            return false;
        }

        private bool IsSecretRank(string rankDefinitionId)
        {
            return TryRankDefinition(rankDefinitionId, out ProfessionalRankDefinition definition) && definition.Secret;
        }

        private bool KnownPerson(string personId)
        {
            return !string.IsNullOrWhiteSpace(personId) && (knownPersonIds.Count == 0 || knownPersonIds.Contains(personId));
        }

        private static bool IsActiveApplication(ProfessionalRankApplicationState state)
        {
            return state is ProfessionalRankApplicationState.Submitted or ProfessionalRankApplicationState.UnderReview or ProfessionalRankApplicationState.AwaitingEvidence or ProfessionalRankApplicationState.AwaitingExamination;
        }

        private static bool IsActiveLike(ProfessionalRankState state)
        {
            return state is ProfessionalRankState.Active or ProfessionalRankState.Provisional;
        }

        private static bool HasDuplicate(IEnumerable<string> ids, out string duplicate)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                {
                    duplicate = id ?? string.Empty;
                    return true;
                }
            }

            duplicate = string.Empty;
            return false;
        }

        private static string[] AddSorted(IEnumerable<string> existing, string value)
        {
            return ProfessionalRankAdvancementSnapshotData.Clean((existing ?? Array.Empty<string>()).Concat(new[] { value }));
        }

        private void AddHook(ProfessionalRankHistoryHookKind kind, string rankRecordId = "", string applicationId = "", string masteryRecordId = "", string achievementId = "", string personId = "", string authorityId = "", string worldTime = "", string transactionId = "")
        {
            historyHooks.Add(new ProfessionalRankHistoryHookData
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
            });
        }
    }
}
