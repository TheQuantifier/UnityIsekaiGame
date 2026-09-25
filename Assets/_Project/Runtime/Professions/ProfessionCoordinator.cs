using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Disassembly;

namespace UnityIsekaiGame.Professions
{
    public interface IProfessionAuthorityResolver
    {
        bool HasAuthority(string actingPersonId, string authorityId, string organizationId = "");
    }

    public sealed class ProfessionCoordinator
    {
        private readonly DefinitionRegistry registry;
        private readonly PersonProfessionRuntime professions;
        private readonly ProfessionEntryRuntime entries;
        private readonly ProfessionalActivityRuntime activities;
        private readonly ProfessionalRankRuntime ranks;
        private readonly CareerHistoryRuntime careerHistory;
        private readonly LifePathRuntime lifePaths;
        private readonly IProfessionAuthorityResolver authorityResolver;
        private readonly TrainingRuntime training;
        private readonly CredentialRuntime credentials;
        private readonly PositionEmploymentRuntime positions;

        public ProfessionCoordinator(
            DefinitionRegistry registry,
            PersonProfessionRuntime professions,
            ProfessionEntryRuntime entries,
            ProfessionalActivityRuntime activities,
            ProfessionalRankRuntime ranks,
            CareerHistoryRuntime careerHistory,
            LifePathRuntime lifePaths,
            IProfessionAuthorityResolver authorityResolver = null,
            TrainingRuntime training = null,
            CredentialRuntime credentials = null,
            PositionEmploymentRuntime positions = null)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.professions = professions ?? throw new ArgumentNullException(nameof(professions));
            this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
            this.activities = activities ?? throw new ArgumentNullException(nameof(activities));
            this.ranks = ranks ?? throw new ArgumentNullException(nameof(ranks));
            this.careerHistory = careerHistory;
            this.lifePaths = lifePaths;
            this.authorityResolver = authorityResolver;
            this.training = training;
            this.credentials = credentials;
            this.positions = positions;
        }

        public ProfessionEntryOperationResult DeclareInformalProfession(
            string personId,
            string professionId,
            string entryPathId,
            string worldTime,
            string transactionId)
        {
            PersonProfessionSnapshot existing = professions.QueryByPerson(personId, true)
                .FirstOrDefault(item => string.Equals(item.ProfessionId, professionId, StringComparison.Ordinal));
            if (existing != null)
            {
                return ProfessionEntryOperationResult.Success("Profession is already active.", professions.Revision, professions.Revision, relationship: existing, duplicate: true);
            }

            ProfessionEligibilityContext context = new ProfessionEligibilityContext(
                personId,
                professionId,
                entryPathId,
                formal: false,
                selfDeclared: true,
                worldTime: ParseWorldTime(worldTime),
                correlationId: transactionId,
                preview: false);
            ProfessionEntryOperationResult result = entries.EnterInformal(context, transactionId, StableId("profession-relationship", personId, professionId));
            if (result.Succeeded && result.Relationship != null)
            {
                RecordProfessionEntered(result.Relationship, worldTime, transactionId);
                EnsureInformalRank(result.Relationship, worldTime, transactionId);
                SynchronizeLifePath(personId, worldTime, transactionId);
            }

            return result;
        }

        public bool CanExerciseAuthority(string actingPersonId, string authorityId, string organizationId = "")
        {
            return !string.IsNullOrWhiteSpace(authorityId)
                && authorityResolver != null
                && authorityResolver.HasAuthority(actingPersonId, authorityId, organizationId);
        }

        public ProfessionEntryOperationResult SubmitFormalProfessionRequest(
            ProfessionEligibilityContext context,
            string transactionId,
            string requestId = "")
        {
            if (context == null || !context.Formal)
            {
                return ProfessionEntryOperationResult.Failure(
                    ProfessionEntryOperationStatus.InvalidRequest,
                    "A formal profession entry context is required.",
                    entries.Revision);
            }

            return entries.SubmitFormalRequest(context, transactionId, requestId);
        }

        public ProfessionEntryOperationResult ApproveFormalProfessionRequest(
            string requestId,
            string actingPersonId,
            string organizationId,
            string worldTime,
            string transactionId,
            bool preview = false)
        {
            if (!entries.TryGetRequest(requestId, out ProfessionEntryRequestSnapshot request))
            {
                return ProfessionEntryOperationResult.Failure(
                    ProfessionEntryOperationStatus.MissingRequest,
                    $"Formal entry request '{requestId}' is missing.",
                    entries.Revision);
            }

            if (!CanExerciseAuthority(actingPersonId, request.AuthorityId, organizationId))
            {
                return ProfessionEntryOperationResult.Failure(
                    ProfessionEntryOperationStatus.InvalidAuthority,
                    $"Person '{actingPersonId}' does not hold authority '{request.AuthorityId}' for organization '{organizationId}'.",
                    entries.Revision);
            }

            ProfessionEntryOperationResult result = entries.ApproveFormalRequest(requestId, request.AuthorityId, transactionId, preview);
            if (result.Succeeded && !preview && result.Relationship != null)
            {
                RecordProfessionEntered(result.Relationship, worldTime, transactionId);
                SynchronizeLifePath(result.Relationship.PersonId, worldTime, transactionId);
            }

            return result;
        }

        public IReadOnlyList<ProfessionalActivityOperationResult> RecordCrafting(CraftingOperationRecordData operation, int actualQuality, ProfessionalActivityDifficulty difficulty)
        {
            ProfessionalActivitySourceSnapshot source = ProfessionalActivitySourceAdapters.FromCraftingOperation(operation, actualQuality, difficulty);
            return RecordForActiveProfessions(source, new[]
            {
                (ProfessionContentIds.CrafterProfessionId, ProfessionContentIds.CrafterActivityDefinitionId),
                (ProfessionContentIds.BlacksmithProfessionId, ProfessionContentIds.BlacksmithCraftingActivityDefinitionId)
            });
        }

        public IReadOnlyList<ProfessionalActivityOperationResult> RecordItemRecovery(DisassemblyOperationRecordData operation)
        {
            ProfessionalActivitySourceSnapshot source = ProfessionalActivitySourceAdapters.FromItemRecoveryOperation(operation);
            bool salvage = operation != null && operation.operationKind == ItemRecoveryOperationKind.Salvage;
            return RecordForActiveProfessions(source, salvage
                ? new[] { (ProfessionContentIds.SalvagerProfessionId, ProfessionContentIds.SalvagePickupActivityDefinitionId) }
                : new[] { (ProfessionContentIds.DisassemblerProfessionId, ProfessionContentIds.ItemRecoveryActivityDefinitionId) });
        }

        public bool SynchronizeCareerLifecycle(string personId, string worldTime, string transactionId)
        {
            long careerRevision = careerHistory?.Revision ?? 0L;
            long lifePathRevision = lifePaths?.Revision ?? 0L;

            if (careerHistory != null)
            {
                SynchronizeEmploymentEpisodes(personId, worldTime, transactionId);
                SynchronizeCareerMilestones(personId, worldTime, transactionId);
            }

            SynchronizeLifePath(personId, worldTime, transactionId);
            return (careerHistory?.Revision ?? 0L) != careerRevision || (lifePaths?.Revision ?? 0L) != lifePathRevision;
        }

        public bool EnsureLifePathFoundation(string personId, string originId, string birthGiftId, string worldTime, string transactionId)
        {
            if (lifePaths == null || string.IsNullOrWhiteSpace(personId))
            {
                return false;
            }

            string lifePathId = StableId("life-path", personId);
            bool exists = lifePaths.TryGetLifePath(lifePathId, out LifePathRecordData current);
            current ??= new LifePathRecordData
            {
                lifePathId = lifePathId,
                personId = personId,
                state = LifePathState.Forming,
                startWorldTime = worldTime,
                accessPolicyId = ProfessionContentIds.AccessPublicId,
                provenance = "profession-coordinator.character-foundation"
            };

            List<FormativeReferenceData> formative = (current.formativeReferences ?? Array.Empty<FormativeReferenceData>())
                .Where(item => item != null)
                .Select(item => item.Clone())
                .ToList();
            bool changed = false;
            if (!string.IsNullOrWhiteSpace(originId))
            {
                string referenceId = StableId("formative.origin", personId, originId);
                if (!formative.Any(item => string.Equals(item.referenceId, referenceId, StringComparison.Ordinal)))
                {
                    formative.Add(new FormativeReferenceData
                    {
                        referenceId = referenceId,
                        kind = FormativeReferenceKind.Origin,
                        subjectId = originId,
                        description = "Generated character origin; may inform player-chosen aspirations but grants no profession.",
                        worldTime = worldTime,
                        weight = 100,
                        accessPolicyId = ProfessionContentIds.AccessPublicId,
                        provenance = "identity.origin"
                    });
                    changed = true;
                }
            }
            if (!string.IsNullOrWhiteSpace(birthGiftId))
            {
                string referenceId = StableId("formative.birth-gift", personId, birthGiftId);
                if (!formative.Any(item => string.Equals(item.referenceId, referenceId, StringComparison.Ordinal)))
                {
                    formative.Add(new FormativeReferenceData
                    {
                        referenceId = referenceId,
                        kind = FormativeReferenceKind.CustomBackgroundSubject,
                        subjectId = birthGiftId,
                        description = "Generated birth gift; may inform player-chosen aspirations but grants no profession.",
                        worldTime = worldTime,
                        weight = 100,
                        accessPolicyId = ProfessionContentIds.AccessPublicId,
                        provenance = "identity.birth-gift"
                    });
                    changed = true;
                }
            }

            if (exists && !changed)
            {
                return false;
            }

            current.formativeReferences = formative.ToArray();
            current.lastRevisionWorldTime = worldTime;
            LifePathOperationResult result = lifePaths.CreateOrUpdateLifePath(current, transactionId);
            return result.Succeeded;
        }

        private IReadOnlyList<ProfessionalActivityOperationResult> RecordForActiveProfessions(
            ProfessionalActivitySourceSnapshot source,
            IEnumerable<(string professionId, string activityDefinitionId)> candidates)
        {
            List<ProfessionalActivityOperationResult> results = new List<ProfessionalActivityOperationResult>();
            if (source == null || source.Reference == null || string.IsNullOrWhiteSpace(source.ActingPersonId) || (!source.Completed || source.Revoked || !source.AccessAllowed))
            {
                return results;
            }

            activities.RegisterSourceSnapshot(source);
            foreach ((string professionId, string activityDefinitionId) in candidates)
            {
                PersonProfessionSnapshot relationship = professions.QueryByPerson(source.ActingPersonId, true)
                    .FirstOrDefault(item => string.Equals(item.ProfessionId, professionId, StringComparison.Ordinal));
                if (relationship == null || !registry.TryGet(activityDefinitionId, out ProfessionalActivityDefinition definition))
                {
                    continue;
                }

                string activityId = StableId("professional-activity", source.ActingPersonId, professionId, source.Reference.sourceId);
                string transactionId = StableId("tx.professional-activity", source.Reference.sourceId, professionId);
                ProfessionalActivityOperationResult result = activities.RegisterAndValidateActivity(
                    new ProfessionalActivityRegistrationRequest
                    {
                        ActivityId = activityId,
                        PersonId = source.ActingPersonId,
                        ProfessionId = professionId,
                        ActivityDefinitionId = activityDefinitionId,
                        Source = source,
                        Category = definition.Category,
                        StartWorldTime = source.WorldTime,
                        CompletionWorldTime = source.WorldTime,
                        Outcome = source.Outcome,
                        SupervisionLevel = TrainingSupervisionLevel.IndependentWithReview,
                        Responsibility = ProfessionalResponsibilityLevel.IndependentPractitioner,
                        QuantityOrDuration = source.QuantityOrDuration,
                        Difficulty = source.Difficulty,
                        Quality = source.Quality,
                        OutcomeSummary = source.Diagnostics,
                        RelatedItemIds = source.RelatedSubjectIds,
                        RepetitionSignature = source.Reference.Signature,
                        AccessPolicyId = ProfessionContentIds.AccessPublicId,
                        Provenance = source.Reference.Signature
                    },
                    StableId("professional-evidence", source.ActingPersonId, professionId, source.Reference.sourceId),
                    "policy.profession.self-validated-work",
                    transactionId);
                results.Add(result);
                if (result.Succeeded)
                {
                    EnsureInformalRank(relationship, source.WorldTime, transactionId);
                    RecordFirstWorkMilestone(relationship, result, source.WorldTime, transactionId);
                    SynchronizeLifePath(source.ActingPersonId, source.WorldTime, transactionId);
                }
            }

            return results;
        }

        private void RecordProfessionEntered(PersonProfessionSnapshot relationship, string worldTime, string transactionId)
        {
            if (careerHistory == null)
            {
                return;
            }

            bool primary = !careerHistory.QueryEpisodesByPerson(relationship.PersonId).Any(item => item.state == CareerEpisodeState.Active && item.primaryCareer);
            string episodeId = StableId("career-episode", relationship.PersonId, relationship.ProfessionId, relationship.RelationshipId);
            CareerHistoryOperationResult episode = careerHistory.StartCareerEpisode(new CareerEpisodeData
            {
                episodeId = episodeId,
                personId = relationship.PersonId,
                category = relationship.FormalPractice ? CareerEpisodeCategory.FormalProfessionalPractice : CareerEpisodeCategory.InformalPractice,
                professionId = relationship.ProfessionId,
                startWorldTime = worldTime,
                state = CareerEpisodeState.Active,
                careerClassification = primary ? CareerClassification.Primary : CareerClassification.Secondary,
                workClassification = EmploymentClassification.IndependentServiceFoundation,
                reasonStarted = "Profession entered through its configured entry path.",
                primaryCareer = primary,
                accessPolicyId = relationship.AccessPolicyId,
                provenance = relationship.RelationshipId,
                sourceRecords = new[] { new CareerSourceRecordReferenceData { recordType = CareerTransitionSourceRecordType.ProfessionRelationship, recordId = relationship.RelationshipId, sourceRevision = relationship.Revision } }
            }, transactionId);
            if (!episode.Succeeded)
            {
                return;
            }

            careerHistory.RecordTransition(new CareerTransitionRecordData
            {
                transitionId = StableId("career-transition.profession-entered", relationship.RelationshipId),
                personId = relationship.PersonId,
                transitionDefinitionId = ProfessionContentIds.CareerProfessionEnteredTransitionId,
                category = CareerTransitionCategory.ProfessionEntered,
                destinationEpisodeIds = new[] { episodeId },
                professionId = relationship.ProfessionId,
                transitionWorldTime = worldTime,
                voluntary = true,
                reason = "Profession entered.",
                supportingRecords = new[] { new CareerSourceRecordReferenceData { recordType = CareerTransitionSourceRecordType.ProfessionRelationship, recordId = relationship.RelationshipId, sourceRevision = relationship.Revision } },
                accessPolicyId = relationship.AccessPolicyId,
                provenance = transactionId
            }, transactionId);
        }

        private void EnsureInformalRank(PersonProfessionSnapshot relationship, string worldTime, string transactionId)
        {
            if (!TryGetInformalRankTrack(relationship.ProfessionId, out string noviceId, out string skilledId, out string masterId))
            {
                return;
            }

            ProfessionalExperienceEvidenceData[] evidence = activities.Evidence
                .Where(item => string.Equals(item.personId, relationship.PersonId, StringComparison.Ordinal)
                    && string.Equals(item.professionId, relationship.ProfessionId, StringComparison.Ordinal)
                    && item.outcome != ProfessionalActivityOutcomeState.Revoked)
                .ToArray();
            string target = evidence.Count(item => item.quality >= 700 && item.difficulty >= ProfessionalActivityDifficulty.Skilled) >= 25
                ? masterId
                : evidence.Count(item => item.quality >= 450) >= 5
                    ? skilledId
                    : noviceId;
            if (ranks.QueryByPerson(relationship.PersonId, currentOnly: true).Any(item => string.Equals(item.rankDefinitionId, target, StringComparison.Ordinal)))
            {
                return;
            }

            ranks.GrantInformalRankRecognition(
                StableId("rank-record", relationship.PersonId, target),
                relationship.PersonId,
                target,
                relationship.PersonId,
                worldTime,
                transactionId);
        }

        private static bool TryGetInformalRankTrack(string professionId, out string noviceId, out string skilledId, out string masterId)
        {
            noviceId = skilledId = masterId = string.Empty;
            if (string.Equals(professionId, ProfessionContentIds.CrafterProfessionId, StringComparison.Ordinal))
            {
                noviceId = ProfessionContentIds.CrafterRankNoviceId; skilledId = ProfessionContentIds.CrafterRankSkilledId; masterId = ProfessionContentIds.CrafterRankMasterId; return true;
            }
            if (string.Equals(professionId, ProfessionContentIds.DisassemblerProfessionId, StringComparison.Ordinal))
            {
                noviceId = ProfessionContentIds.DisassemblerRankNoviceId; skilledId = ProfessionContentIds.DisassemblerRankSkilledId; masterId = ProfessionContentIds.DisassemblerRankMasterId; return true;
            }
            if (string.Equals(professionId, ProfessionContentIds.SalvagerProfessionId, StringComparison.Ordinal))
            {
                noviceId = ProfessionContentIds.SalvagerRankNoviceId; skilledId = ProfessionContentIds.SalvagerRankSkilledId; masterId = ProfessionContentIds.SalvagerRankMasterId; return true;
            }
            return false;
        }

        private void RecordFirstWorkMilestone(PersonProfessionSnapshot relationship, ProfessionalActivityOperationResult activity, string worldTime, string transactionId)
        {
            if (careerHistory == null || activity?.Activity == null
                || careerHistory.Milestones.Any(item => string.Equals(item.personId, relationship.PersonId, StringComparison.Ordinal)
                    && string.Equals(item.professionId, relationship.ProfessionId, StringComparison.Ordinal)
                    && item.kind == CareerMilestoneKind.FirstProfessionalWork))
            {
                return;
            }

            careerHistory.RecordMilestone(new CareerMilestoneRecordData
            {
                milestoneId = StableId("career-milestone.first-work", relationship.PersonId, relationship.ProfessionId),
                personId = relationship.PersonId,
                kind = CareerMilestoneKind.FirstProfessionalWork,
                professionId = relationship.ProfessionId,
                worldTime = worldTime,
                description = "Created automatically from validated gameplay activity.",
                sourceRecordId = activity.Activity.activityId,
                sourceRecordType = CareerTransitionSourceRecordType.ProfessionalActivity,
                accessPolicyId = ProfessionContentIds.AccessPublicId,
                provenance = transactionId
            }, transactionId);
        }

        private void SynchronizeEmploymentEpisodes(string personId, string worldTime, string transactionId)
        {
            if (positions == null || careerHistory == null)
            {
                return;
            }

            foreach (EmploymentRecordData employment in positions.QueryEmploymentByPerson(personId))
            {
                string episodeId = StableId("career-episode.employment", employment.employmentId);
                CareerEpisodeData existing = careerHistory.QueryByEmployment(employment.employmentId)
                    .FirstOrDefault(item => string.Equals(item.episodeId, episodeId, StringComparison.Ordinal));
                bool active = employment.state is EmploymentState.Active or EmploymentState.Probationary or EmploymentState.OnLeaveFoundation or EmploymentState.Suspended;
                if (active && existing == null)
                {
                    bool primary = !careerHistory.QueryEpisodesByPerson(personId).Any(item => item.state == CareerEpisodeState.Active && item.primaryCareer);
                    string professionId = professions.QueryByPerson(personId, activeOnly: true).Select(item => item.ProfessionId).FirstOrDefault() ?? string.Empty;
                    CareerHistoryOperationResult episodeResult = careerHistory.StartCareerEpisode(new CareerEpisodeData
                    {
                        episodeId = episodeId,
                        personId = personId,
                        category = CareerEpisodeCategory.Employment,
                        professionId = professionId,
                        employmentId = employment.employmentId,
                        positionInstanceId = employment.positionInstanceId,
                        organizationId = employment.employerOrganizationId,
                        startWorldTime = string.IsNullOrWhiteSpace(employment.startWorldTime) ? worldTime : employment.startWorldTime,
                        state = CareerEpisodeState.Active,
                        careerClassification = primary ? CareerClassification.Primary : CareerClassification.Secondary,
                        workClassification = employment.classification,
                        reasonStarted = "Created automatically from authoritative employment.",
                        primaryCareer = primary,
                        accessPolicyId = employment.accessPolicyId,
                        provenance = employment.employmentId,
                        sourceRecords = new[]
                        {
                            new CareerSourceRecordReferenceData
                            {
                                recordType = CareerTransitionSourceRecordType.Employment,
                                recordId = employment.employmentId,
                                sourceRevision = employment.revision
                            }
                        }
                    }, StableId(transactionId, "employment-start", employment.employmentId));
                    if (episodeResult.Succeeded)
                    {
                        careerHistory.RecordTransition(new CareerTransitionRecordData
                        {
                            transitionId = StableId("career-transition.employment-started", employment.employmentId),
                            personId = personId,
                            transitionDefinitionId = ProfessionContentIds.CareerEmploymentStartedTransitionId,
                            category = CareerTransitionCategory.EmploymentStarted,
                            destinationEpisodeIds = new[] { episodeId },
                            professionId = professionId,
                            newEmploymentId = employment.employmentId,
                            newPositionInstanceId = employment.positionInstanceId,
                            organizationId = employment.employerOrganizationId,
                            transitionWorldTime = string.IsNullOrWhiteSpace(employment.startWorldTime) ? worldTime : employment.startWorldTime,
                            decidingAuthorityId = employment.appointmentAuthorityId,
                            voluntary = true,
                            reason = "Employment began.",
                            supportingRecords = new[]
                            {
                                new CareerSourceRecordReferenceData { recordType = CareerTransitionSourceRecordType.Employment, recordId = employment.employmentId, sourceRevision = employment.revision },
                                new CareerSourceRecordReferenceData { recordType = CareerTransitionSourceRecordType.Position, recordId = employment.positionInstanceId, sourceRevision = 0L }
                            },
                            accessPolicyId = employment.accessPolicyId,
                            provenance = transactionId
                        }, StableId(transactionId, "employment-start-transition", employment.employmentId));
                    }
                }
                else if (!active && existing != null && existing.state == CareerEpisodeState.Active)
                {
                    bool involuntary = employment.state is EmploymentState.Dismissed or EmploymentState.LaidOffFoundation;
                    careerHistory.RecordTransition(new CareerTransitionRecordData
                    {
                        transitionId = StableId("career-transition.employment-ended", employment.employmentId),
                        personId = personId,
                        transitionDefinitionId = ProfessionContentIds.CareerEmploymentEndedTransitionId,
                        category = CareerTransitionCategory.EmploymentEnded,
                        sourceEpisodeIds = new[] { existing.episodeId },
                        professionId = existing.professionId,
                        previousEmploymentId = employment.employmentId,
                        previousPositionInstanceId = employment.positionInstanceId,
                        organizationId = employment.employerOrganizationId,
                        transitionWorldTime = string.IsNullOrWhiteSpace(employment.endWorldTime) ? worldTime : employment.endWorldTime,
                        decidingAuthorityId = employment.appointmentAuthorityId,
                        voluntary = !involuntary,
                        involuntary = involuntary,
                        reason = $"Employment became {employment.state}.",
                        supportingRecords = new[]
                        {
                            new CareerSourceRecordReferenceData { recordType = CareerTransitionSourceRecordType.Employment, recordId = employment.employmentId, sourceRevision = employment.revision }
                        },
                        accessPolicyId = employment.accessPolicyId,
                        provenance = transactionId
                    }, StableId(transactionId, "employment-end-transition", employment.employmentId));
                    careerHistory.EndCareerEpisode(
                        existing.episodeId,
                        string.IsNullOrWhiteSpace(employment.endWorldTime) ? worldTime : employment.endWorldTime,
                        $"Employment became {employment.state}.",
                        StableId(transactionId, "employment-end", employment.employmentId));
                }
            }
        }

        private void SynchronizeCareerMilestones(string personId, string worldTime, string transactionId)
        {
            if (careerHistory == null)
            {
                return;
            }

            foreach (TrainingEnrollmentSnapshot enrollment in training?.QueryByPerson(personId).Where(item => item.State == TrainingEnrollmentState.Completed) ?? Enumerable.Empty<TrainingEnrollmentSnapshot>())
            {
                RecordLifecycleMilestone(personId, enrollment.RelatedProfessionId, CareerMilestoneKind.Achievement, enrollment.EnrollmentId, CareerTransitionSourceRecordType.TrainingEnrollment, worldTime, "Training program completed.", transactionId);
            }

            foreach (CredentialRecordData credential in credentials?.Credentials.Where(item => string.Equals(item.recipientPersonId, personId, StringComparison.Ordinal) && item.state == CredentialState.Active) ?? Enumerable.Empty<CredentialRecordData>())
            {
                RecordLifecycleMilestone(personId, credential.relatedProfessionId, CareerMilestoneKind.CredentialEarned, credential.credentialId, CareerTransitionSourceRecordType.Credential, credential.issueWorldTime, "Professional credential earned.", transactionId);
            }

            foreach (ProfessionalRankRecordData rank in ranks.QueryByPerson(personId, currentOnly: true))
            {
                if (registry.TryGet(rank.rankDefinitionId, out ProfessionalRankDefinition definition) && definition.RankOrder > 0)
                {
                    RecordLifecycleMilestone(personId, rank.professionId, CareerMilestoneKind.MajorPromotion, rank.rankRecordId, CareerTransitionSourceRecordType.Rank, rank.effectiveWorldTime, $"Professional rank recognized: {definition.DisplayName}.", transactionId);
                }
            }

            foreach (ProfessionalMasteryRecordData mastery in ranks.Masteries.Where(item => string.Equals(item.personId, personId, StringComparison.Ordinal)))
            {
                RecordLifecycleMilestone(personId, mastery.professionId, CareerMilestoneKind.MasteryRecognition, mastery.masteryRecordId, CareerTransitionSourceRecordType.Custom, mastery.issueWorldTime, "Professional mastery recognized.", transactionId);
            }
        }

        private void RecordLifecycleMilestone(string personId, string professionId, CareerMilestoneKind kind, string sourceId, CareerTransitionSourceRecordType sourceType, string sourceWorldTime, string description, string transactionId)
        {
            string milestoneId = StableId("career-milestone", kind.ToString().ToLowerInvariant(), sourceId);
            if (careerHistory.Milestones.Any(item => string.Equals(item.milestoneId, milestoneId, StringComparison.Ordinal)))
            {
                return;
            }

            careerHistory.RecordMilestone(new CareerMilestoneRecordData
            {
                milestoneId = milestoneId,
                personId = personId,
                kind = kind,
                professionId = professionId,
                worldTime = sourceWorldTime ?? string.Empty,
                description = description,
                sourceRecordId = sourceId,
                sourceRecordType = sourceType,
                accessPolicyId = ProfessionContentIds.AccessPublicId,
                provenance = transactionId
            }, StableId(transactionId, "milestone", sourceId));
        }

        private void SynchronizeLifePath(string personId, string worldTime, string transactionId)
        {
            if (lifePaths == null)
            {
                return;
            }

            string id = StableId("life-path", personId);
            lifePaths.TryGetLifePath(id, out LifePathRecordData current);
            current ??= new LifePathRecordData { lifePathId = id, personId = personId, startWorldTime = worldTime };
            PersonProfessionSnapshot[] relationships = professions.QueryByPerson(personId).ToArray();
            current.professionRelationshipIds = relationships.Select(item => item.RelationshipId).ToArray();
            current.professionIds = relationships.Select(item => item.ProfessionId).ToArray();
            current.specializationIds = relationships.SelectMany(item => item.SpecializationIds).ToArray();
            current.careerEpisodeIds = careerHistory?.QueryEpisodesByPerson(personId).Select(item => item.episodeId).ToArray() ?? Array.Empty<string>();
            current.educationAndTrainingReferences = training?.QueryByPerson(personId)
                .Select(item => new LifePathSourceReferenceData { recordType = CareerTransitionSourceRecordType.TrainingEnrollment, recordId = item.EnrollmentId, sourceRevision = item.Revision })
                .ToArray() ?? Array.Empty<LifePathSourceReferenceData>();
            current.credentialIds = credentials?.Credentials.Where(item => string.Equals(item.recipientPersonId, personId, StringComparison.Ordinal)).Select(item => item.credentialId).ToArray() ?? Array.Empty<string>();
            current.rankRecordIds = ranks.QueryByPerson(personId).Select(item => item.rankRecordId).ToArray();
            current.positionInstanceIds = positions?.QueryEmploymentByPerson(personId).Select(item => item.positionInstanceId).ToArray() ?? Array.Empty<string>();
            current.employmentIds = positions?.QueryEmploymentByPerson(personId).Select(item => item.employmentId).ToArray() ?? Array.Empty<string>();
            current.state = relationships.Any(item => item.Active) ? LifePathState.Active : LifePathState.Forming;
            current.lastRevisionWorldTime = worldTime;
            current.accessPolicyId = ProfessionContentIds.AccessPublicId;
            current.provenance = "profession-coordinator.synchronized";
            lifePaths.CreateOrUpdateLifePath(current, transactionId);
        }

        private static string StableId(string prefix, params string[] parts)
        {
            return string.Join(".", new[] { prefix }.Concat(parts ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().Replace(' ', '-').Replace(':', '-').Replace('/', '-')));
        }

        private static double ParseWorldTime(string worldTime)
        {
            return double.TryParse(worldTime, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed) ? parsed : 0d;
        }
    }
}
