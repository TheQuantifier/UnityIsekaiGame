using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityIsekaiGame.Development.Automation;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Tests
{
    public sealed class CareerHistoryTransitionsTests
    {
        private const string PersonId = "person.career-history.test";
        private const string OtherPersonId = "person.career-history.other";
        private const string GuildAuthority = "authority.guild.prototype";
        private const string GuildOrganization = "organization.prototype.guild";
        private const string IndependentOrganization = "organization.prototype.independent";
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeCareerTransitionDefinitionsValidate()
        {
            DefinitionRegistry registry = Registry();
            DefinitionValidationReport report = ValidateRegistry(registry);

            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
            Assert.That(registry.TryGet(ProfessionContentIds.CareerPromotionTransitionId, out CareerTransitionDefinition promotion), Is.True);
            Assert.That(registry.TryGet(ProfessionContentIds.CareerDismissalTransitionId, out CareerTransitionDefinition dismissal), Is.True);
            Assert.That(registry.TryGet(ProfessionContentIds.CareerReturnFromRetirementTransitionId, out CareerTransitionDefinition returnDefinition), Is.True);
            Assert.That(promotion.RequiredSourceRecordTypes, Does.Contain(CareerTransitionSourceRecordType.Rank));
            Assert.That(dismissal.SecretAllowed, Is.True);
            Assert.That(returnDefinition.RequiredSourceRecordTypes, Does.Contain(CareerTransitionSourceRecordType.CareerEpisode));
        }

        [Test]
        public void EpisodesTimelinesConcurrentPrimaryAndGapsAreDeterministic()
        {
            using TestLabRuntimeBundle bundle = Bundle();
            ProfessionOperationResult profession = AddProfession(bundle, "primary", ProfessionContentIds.BlacksmithProfessionId, primary: true);
            PositionEmploymentOperationResult employment = Appoint(bundle, "primary", IndependentOrganization, ProfessionContentIds.IndependentOrganizationTypeId);
            CareerHistoryOperationResult primary = bundle.CareerHistory.StartCareerEpisode(Episode("career.primary", profession.Snapshot.RelationshipId, employment.Employment, primary: true), "tx.career.primary");
            CareerHistoryOperationResult secondary = bundle.CareerHistory.StartCareerEpisode(new CareerEpisodeData
            {
                episodeId = "career.secondary",
                personId = PersonId,
                category = CareerEpisodeCategory.Employment,
                professionId = ProfessionContentIds.FieldMedicProfessionId,
                startWorldTime = "3",
                state = CareerEpisodeState.Active,
                careerClassification = CareerClassification.Secondary,
                accessPolicyId = ProfessionContentIds.AccessPublicId
            }, "tx.career.secondary");
            CareerHistoryOperationResult gap = bundle.CareerHistory.BeginCareerGap("career.gap", PersonId, "4", "Between commissions", "tx.career.gap");

            CareerTimelineSnapshot beforeMutation = bundle.CareerHistory.BuildTimeline(PersonId).Timeline;
            CareerEpisodeData immutableEpisode = beforeMutation.Episodes.First(item => item.episodeId == "career.primary");
            immutableEpisode.reasonStarted = "mutated locally";
            CareerTimelineSnapshot afterMutation = bundle.CareerHistory.BuildTimeline(PersonId).Timeline;

            Assert.That(primary.Succeeded, Is.True, primary.Message);
            Assert.That(secondary.Succeeded, Is.True, secondary.Message);
            Assert.That(gap.Succeeded, Is.True, gap.Message);
            Assert.That(afterMutation.PrimaryCareers.Count, Is.EqualTo(1));
            Assert.That(afterMutation.ConcurrentCareers.Count, Is.EqualTo(2));
            Assert.That(afterMutation.CareerGaps.Count, Is.EqualTo(1));
            Assert.That(afterMutation.Episodes.Select(item => item.episodeId).ToArray(), Is.EqualTo(new[] { "career.primary", "career.secondary", "career.gap" }));
            Assert.That(afterMutation.Episodes.First(item => item.episodeId == "career.primary").reasonStarted, Is.Not.EqualTo("mutated locally"));
        }

        [Test]
        public void PromotionDemotionTransferResignationDismissalReferenceAuthoritativeSources()
        {
            using TestLabRuntimeBundle bundle = Bundle();
            ProfessionOperationResult profession = AddProfession(bundle, "authoritative", ProfessionContentIds.BlacksmithProfessionId, primary: true);
            ProfessionalRankRecordData apprentice = SeedRank(bundle, "rank.apprentice", ProfessionContentIds.BlacksmithRankApprenticeId, ProfessionalRankState.Replaced);
            ProfessionalRankRecordData journeyman = SeedRank(bundle, "rank.journeyman", ProfessionContentIds.BlacksmithRankJourneymanId, ProfessionalRankState.Active);
            PositionEmploymentOperationResult firstEmployment = Appoint(bundle, "source", IndependentOrganization, ProfessionContentIds.IndependentOrganizationTypeId);
            PositionEmploymentOperationResult targetPosition = CreatePosition(bundle, "target", ProfessionContentIds.GuildClerkPositionId, GuildOrganization, ProfessionContentIds.GuildOrganizationTypeId);
            PositionEligibilityResult transferEligibility = bundle.PositionEmployment.EvaluateEligibility(PersonId, targetPosition.Position.positionInstanceId, privilegedDiagnostics: true);
            PositionEmploymentOperationResult transferEmployment = bundle.PositionEmployment.TransferPerson(firstEmployment.Employment.employmentId, "employment.career.transfer", targetPosition.Position.positionInstanceId, ProfessionContentIds.PositionRestrictedRecordsAuthorityId, transferEligibility.Snapshot, "20", "tx.position.transfer");
            PositionEmploymentOperationResult resignation = bundle.PositionEmployment.Resign(transferEmployment.Employment.employmentId, "21", "tx.position.resign");
            PositionEmploymentOperationResult dismissedEmployment = Appoint(bundle, "dismissal", IndependentOrganization, ProfessionContentIds.IndependentOrganizationTypeId);
            PositionEmploymentOperationResult dismissal = bundle.PositionEmployment.Dismiss(dismissedEmployment.Employment.employmentId, "22", "tx.position.dismiss");
            CareerHistoryOperationResult episode = bundle.CareerHistory.StartCareerEpisode(Episode("career.authoritative", profession.Snapshot.RelationshipId, firstEmployment.Employment, primary: true), "tx.career.authoritative");
            CareerHistoryOperationResult promotion = bundle.CareerHistory.RecordTransition(Transition("transition.promotion", ProfessionContentIds.CareerPromotionTransitionId, CareerTransitionCategory.Promotion, new[] { "career.authoritative" }, Array.Empty<string>(), firstEmployment.Employment, new[] { Source(CareerTransitionSourceRecordType.Rank, apprentice.rankRecordId), Source(CareerTransitionSourceRecordType.Rank, journeyman.rankRecordId) }, previousRank: apprentice.rankRecordId, newRank: journeyman.rankRecordId), "tx.career.promotion");
            CareerHistoryOperationResult demotion = bundle.CareerHistory.RecordTransition(Transition("transition.demotion", ProfessionContentIds.CareerDemotionTransitionId, CareerTransitionCategory.Demotion, new[] { "career.authoritative" }, Array.Empty<string>(), firstEmployment.Employment, new[] { Source(CareerTransitionSourceRecordType.Rank, journeyman.rankRecordId), Source(CareerTransitionSourceRecordType.Rank, apprentice.rankRecordId) }, previousRank: journeyman.rankRecordId, newRank: apprentice.rankRecordId), "tx.career.demotion");
            CareerHistoryOperationResult transfer = bundle.CareerHistory.RecordTransition(Transition("transition.transfer", ProfessionContentIds.CareerTransferTransitionId, CareerTransitionCategory.Transfer, new[] { "career.authoritative" }, Array.Empty<string>(), transferEmployment.Employment, new[] { Source(CareerTransitionSourceRecordType.Employment, firstEmployment.Employment.employmentId), Source(CareerTransitionSourceRecordType.Employment, transferEmployment.Employment.employmentId), Source(CareerTransitionSourceRecordType.Position, targetPosition.Position.positionInstanceId) }, previousEmployment: firstEmployment.Employment.employmentId, previousPosition: firstEmployment.Employment.positionInstanceId), "tx.career.transfer");
            CareerHistoryOperationResult resign = bundle.CareerHistory.RecordTransition(Transition("transition.resignation", ProfessionContentIds.CareerResignationTransitionId, CareerTransitionCategory.Resignation, new[] { "career.authoritative" }, Array.Empty<string>(), transferEmployment.Employment, new[] { Source(CareerTransitionSourceRecordType.Employment, resignation.Employment.employmentId) }), "tx.career.resign");
            CareerHistoryOperationResult dismiss = bundle.CareerHistory.RecordTransition(Transition("transition.dismissal", ProfessionContentIds.CareerDismissalTransitionId, CareerTransitionCategory.Dismissal, new[] { "career.authoritative" }, Array.Empty<string>(), dismissal.Employment, new[] { Source(CareerTransitionSourceRecordType.Employment, dismissal.Employment.employmentId) }, secret: true), "tx.career.dismiss");
            CareerHistoryProjection<CareerTimelineSnapshot> publicProjection = bundle.CareerHistory.ProjectTimeline(PersonId, CareerHistoryProjectionAudience.Public, null);

            Assert.That(episode.Succeeded, Is.True, episode.Message);
            Assert.That(transferEmployment.Succeeded, Is.True, transferEmployment.Message);
            Assert.That(resignation.Succeeded, Is.True, resignation.Message);
            Assert.That(dismissal.Succeeded, Is.True, dismissal.Message);
            Assert.That(new[] { promotion, demotion, transfer, resign, dismiss }.All(result => result.Succeeded), Is.True, string.Join(" | ", new[] { promotion, demotion, transfer, resign, dismiss }.Select(result => result.Message)));
            Assert.That(publicProjection.Redacted, Is.True);
            Assert.That(publicProjection.Record.Transitions.First(item => item.transitionId == "transition.dismissal").supportingRecords.Count(), Is.Zero);
        }

        [Test]
        public void RetirementReturnCareerChangeAndMilestonesPreservePriorHistory()
        {
            using TestLabRuntimeBundle bundle = Bundle();
            ProfessionOperationResult blacksmith = AddProfession(bundle, "smith", ProfessionContentIds.BlacksmithProfessionId, primary: true);
            ProfessionOperationResult medic = AddProfession(bundle, "medic", ProfessionContentIds.FieldMedicProfessionId, primary: false);
            PositionEmploymentOperationResult employment = Appoint(bundle, "retirement", IndependentOrganization, ProfessionContentIds.IndependentOrganizationTypeId);
            CareerHistoryOperationResult smithEpisode = bundle.CareerHistory.StartCareerEpisode(Episode("career.smith", blacksmith.Snapshot.RelationshipId, employment.Employment, primary: true), "tx.career.smith");
            PositionEmploymentOperationResult retirementEmployment = bundle.PositionEmployment.Retire(employment.Employment.employmentId, "30", "tx.position.retire");
            CareerHistoryOperationResult retiredEpisode = bundle.CareerHistory.StartCareerEpisode(new CareerEpisodeData
            {
                episodeId = "career.retired",
                personId = PersonId,
                category = CareerEpisodeCategory.Retirement,
                state = CareerEpisodeState.Active,
                careerClassification = CareerClassification.Retirement,
                startWorldTime = "30",
                sourceRecords = new[] { Source(CareerTransitionSourceRecordType.CareerEpisode, "career.smith") },
                accessPolicyId = ProfessionContentIds.AccessPublicId
            }, "tx.career.retired");
            CareerHistoryOperationResult retirement = bundle.CareerHistory.RecordTransition(Transition("transition.retirement", ProfessionContentIds.CareerRetirementTransitionId, CareerTransitionCategory.Retirement, new[] { "career.smith" }, new[] { "career.retired" }, retirementEmployment.Employment, new[] { Source(CareerTransitionSourceRecordType.Employment, retirementEmployment.Employment.employmentId) }), "tx.career.retirement");
            PositionEmploymentOperationResult returnEmployment = Appoint(bundle, "return", IndependentOrganization, ProfessionContentIds.IndependentOrganizationTypeId);
            CareerHistoryOperationResult medicEpisode = bundle.CareerHistory.StartCareerEpisode(Episode("career.medic", medic.Snapshot.RelationshipId, returnEmployment.Employment, primary: false, professionId: ProfessionContentIds.FieldMedicProfessionId), "tx.career.medic");
            CareerHistoryOperationResult returned = bundle.CareerHistory.RecordTransition(Transition("transition.return", ProfessionContentIds.CareerReturnFromRetirementTransitionId, CareerTransitionCategory.ReturnFromRetirement, new[] { "career.retired" }, new[] { "career.medic" }, returnEmployment.Employment, new[] { Source(CareerTransitionSourceRecordType.CareerEpisode, "career.retired"), Source(CareerTransitionSourceRecordType.Employment, returnEmployment.Employment.employmentId) }), "tx.career.return");
            CareerHistoryOperationResult retirementEnded = bundle.CareerHistory.EndCareerEpisode("career.retired", "31", "Returned to active work", "tx.career.retirement-ended");
            CareerHistoryOperationResult careerChange = bundle.CareerHistory.RecordTransition(Transition("transition.change", ProfessionContentIds.CareerChangeTransitionId, CareerTransitionCategory.CareerChange, new[] { "career.smith" }, new[] { "career.medic" }, returnEmployment.Employment, new[] { Source(CareerTransitionSourceRecordType.ProfessionRelationship, medic.Snapshot.RelationshipId), Source(CareerTransitionSourceRecordType.Employment, returnEmployment.Employment.employmentId) }), "tx.career.change");
            CareerHistoryOperationResult achievement = bundle.CareerHistory.RecordMilestone(Milestone("milestone.achievement", CareerMilestoneKind.Achievement, "career.smith"), "tx.career.achievement");
            CareerHistoryOperationResult setback = bundle.CareerHistory.RecordMilestone(Milestone("milestone.setback", CareerMilestoneKind.Setback, "career.medic", secret: true), "tx.career.setback");
            CareerTimelineSnapshot timeline = bundle.CareerHistory.BuildTimeline(PersonId).Timeline;

            Assert.That(smithEpisode.Succeeded, Is.True, smithEpisode.Message);
            Assert.That(retiredEpisode.Succeeded, Is.True, retiredEpisode.Message);
            Assert.That(medicEpisode.Succeeded, Is.True, medicEpisode.Message);
            Assert.That(new[] { retirement, returned, retirementEnded, careerChange, achievement, setback }.All(result => result.Succeeded), Is.True, string.Join(" | ", new[] { retirement, returned, retirementEnded, careerChange, achievement, setback }.Select(result => result.Message)));
            Assert.That(timeline.Episodes.Select(item => item.episodeId), Does.Contain("career.smith"));
            Assert.That(timeline.Episodes.Select(item => item.episodeId), Does.Contain("career.retired"));
            Assert.That(timeline.Episodes.Select(item => item.episodeId), Does.Contain("career.medic"));
            Assert.That(CareerHistoryRequirementAdapters.IsRetired(bundle.CareerHistory, PersonId), Is.False);
            Assert.That(CareerHistoryRequirementAdapters.HasPreviousProfession(bundle.CareerHistory, PersonId, ProfessionContentIds.BlacksmithProfessionId), Is.True);
            Assert.That(CareerHistoryRequirementAdapters.HasCareerTransition(bundle.CareerHistory, PersonId, CareerTransitionCategory.CareerChange), Is.True);
        }

        [Test]
        public void PersistenceRejectsCorruptRestoreWithoutMutationOrHookReplay()
        {
            using TestLabRuntimeBundle bundle = Bundle();
            ProfessionOperationResult profession = AddProfession(bundle, "persist", ProfessionContentIds.BlacksmithProfessionId, primary: true);
            PositionEmploymentOperationResult employment = Appoint(bundle, "persist", IndependentOrganization, ProfessionContentIds.IndependentOrganizationTypeId);
            CareerHistoryOperationResult episode = bundle.CareerHistory.StartCareerEpisode(Episode("career.persist", profession.Snapshot.RelationshipId, employment.Employment, primary: true), "tx.career.persist");
            CareerHistoryRuntimeSaveData save = bundle.CareerHistory.CreateSaveData();
            CareerHistoryRuntime restored = NewCareerRuntime(bundle);
            CareerHistoryOperationResult restore = restored.RestoreFromSaveData(save, bundle.DefinitionRegistry, bundle.Professions, bundle.Training, bundle.ProfessionalActivities, bundle.Credentials, bundle.ProfessionalRanks, bundle.PositionEmployment, bundle.KnownPersonIds, Organizations(), Authorities(), restoring: true);
            CareerHistoryRuntimeSaveData corrupt = save.Clone();
            corrupt.episodes[0].personId = "person.missing";
            int beforeCount = restored.EpisodeCount;
            long beforeRevision = restored.Revision;
            CareerHistoryOperationResult rejected = restored.RestoreFromSaveData(corrupt, bundle.DefinitionRegistry, bundle.Professions, bundle.Training, bundle.ProfessionalActivities, bundle.Credentials, bundle.ProfessionalRanks, bundle.PositionEmployment, bundle.KnownPersonIds, Organizations(), Authorities(), restoring: true);

            Assert.That(episode.Succeeded, Is.True, episode.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.HistoryHooks.Count, Is.Zero);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Status, Is.EqualTo(CareerHistoryOperationStatus.CorruptSave));
            Assert.That(restored.EpisodeCount, Is.EqualTo(beforeCount));
            Assert.That(restored.Revision, Is.EqualTo(beforeRevision));
        }

        private static TestLabRuntimeBundle Bundle()
        {
            return TestLabRuntimeBundle.CreateFresh(Registry(), PersonId, "world.career-history.test", new[] { PersonId, OtherPersonId }, Array.Empty<string>(), "Career History Tests");
        }

        private static DefinitionRegistry Registry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            DefinitionRegistry registry = catalog.CreateRegistry();
            DefinitionValidationReport report = new DefinitionValidationReport();
            registry = new DefinitionRegistry(registry.DefinitionsById.Values, report);
            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            return registry;
        }

        private static DefinitionValidationReport ValidateRegistry(DefinitionRegistry registry)
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IDefinitionCatalogValidationParticipant participant in registry.DefinitionsById.Values.OfType<IDefinitionCatalogValidationParticipant>())
            {
                participant.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            return report;
        }

        private static ProfessionOperationResult AddProfession(TestLabRuntimeBundle bundle, string slug, string professionId, bool primary)
        {
            ProfessionOperationResult result = bundle.Professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = $"profession-relationship.career.{slug}",
                personId = PersonId,
                professionId = professionId,
                informalPractice = true,
                formalPractice = true,
                selfDeclared = true,
                recognized = true,
                recognizingAuthorityId = professionId == ProfessionContentIds.FieldMedicProfessionId ? "authority.medical.prototype" : GuildAuthority,
                primary = primary,
                active = true,
                startWorldTime = "1",
                accessPolicyId = ProfessionContentIds.AccessPublicId,
                transactionId = $"tx.profession.{slug}"
            });
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result;
        }

        private static PositionEmploymentOperationResult CreatePosition(TestLabRuntimeBundle bundle, string slug, string organizationId, string organizationTypeId)
        {
            return CreatePosition(bundle, slug, ProfessionContentIds.IndependentContractorPositionId, organizationId, organizationTypeId);
        }

        private static PositionEmploymentOperationResult CreatePosition(TestLabRuntimeBundle bundle, string slug, string definitionId, string organizationId, string organizationTypeId)
        {
            PositionEmploymentOperationResult result = bundle.PositionEmployment.CreatePosition(new PositionInstanceData
            {
                positionInstanceId = $"position-instance.career.{slug}",
                positionDefinitionId = definitionId,
                organizationId = organizationId,
                organizationTypeId = organizationTypeId,
                state = PositionInstanceState.Vacant,
                maximumHolders = definitionId == ProfessionContentIds.GuildClerkPositionId ? 2 : 4,
                vacancyAllowed = true,
                createdWorldTime = "1",
                accessPolicyId = ProfessionContentIds.AccessPublicId
            }, $"tx.position.create.{slug}");
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result;
        }

        private static PositionEmploymentOperationResult Appoint(TestLabRuntimeBundle bundle, string slug, string organizationId, string organizationTypeId)
        {
            PositionEmploymentOperationResult position = CreatePosition(bundle, slug, organizationId, organizationTypeId);
            PositionEligibilityResult eligibility = bundle.PositionEmployment.EvaluateEligibility(PersonId, position.Position.positionInstanceId, privilegedDiagnostics: true);
            Assert.That(eligibility.AuthoritativeEligible, Is.True, string.Join(",", eligibility.BlockingFailures));
            PositionEmploymentOperationResult appointment = bundle.PositionEmployment.AppointPerson($"employment.career.{slug}", string.Empty, PersonId, position.Position.positionInstanceId, ProfessionContentIds.PositionAppointAuthorityId, eligibility.Snapshot, "2", $"tx.position.appoint.{slug}", EmploymentClassification.IndependentServiceFoundation);
            Assert.That(appointment.Succeeded, Is.True, appointment.Message);
            return appointment;
        }

        private static CareerEpisodeData Episode(string episodeId, string professionRelationshipId, EmploymentRecordData employment, bool primary, string professionId = "")
        {
            return new CareerEpisodeData
            {
                episodeId = episodeId,
                personId = PersonId,
                category = CareerEpisodeCategory.Employment,
                professionId = string.IsNullOrWhiteSpace(professionId) ? ProfessionContentIds.BlacksmithProfessionId : professionId,
                employmentId = employment.employmentId,
                positionInstanceId = employment.positionInstanceId,
                organizationId = employment.employerOrganizationId,
                startWorldTime = employment.startWorldTime,
                state = CareerEpisodeState.Active,
                careerClassification = primary ? CareerClassification.Primary : CareerClassification.Secondary,
                workClassification = employment.classification,
                primaryCareer = primary,
                accessPolicyId = ProfessionContentIds.AccessPublicId,
                sourceRecords = new[]
                {
                    Source(CareerTransitionSourceRecordType.ProfessionRelationship, professionRelationshipId),
                    Source(CareerTransitionSourceRecordType.Employment, employment.employmentId)
                }
            };
        }

        private static CareerTransitionRecordData Transition(string transitionId, string definitionId, CareerTransitionCategory category, string[] sourceEpisodes, string[] destinationEpisodes, EmploymentRecordData employment, CareerSourceRecordReferenceData[] sources, string previousRank = "", string newRank = "", string previousEmployment = "", string previousPosition = "", bool secret = false)
        {
            return new CareerTransitionRecordData
            {
                transitionId = transitionId,
                personId = PersonId,
                transitionDefinitionId = definitionId,
                category = category,
                sourceEpisodeIds = sourceEpisodes ?? Array.Empty<string>(),
                destinationEpisodeIds = destinationEpisodes ?? Array.Empty<string>(),
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                previousRankRecordId = previousRank,
                newRankRecordId = newRank,
                previousEmploymentId = string.IsNullOrWhiteSpace(previousEmployment) ? employment.employmentId : previousEmployment,
                newEmploymentId = employment.employmentId,
                previousPositionInstanceId = string.IsNullOrWhiteSpace(previousPosition) ? employment.positionInstanceId : previousPosition,
                newPositionInstanceId = employment.positionInstanceId,
                organizationId = employment.employerOrganizationId,
                transitionWorldTime = employment.startWorldTime,
                decidingAuthorityId = ProfessionContentIds.PositionAppointAuthorityId,
                secret = secret,
                accessPolicyId = secret ? ProfessionContentIds.AccessSecretId : ProfessionContentIds.AccessPublicId,
                supportingRecords = sources ?? Array.Empty<CareerSourceRecordReferenceData>()
            };
        }

        private static CareerMilestoneRecordData Milestone(string id, CareerMilestoneKind kind, string episodeId, bool secret = false)
        {
            return new CareerMilestoneRecordData
            {
                milestoneId = id,
                personId = PersonId,
                kind = kind,
                episodeId = episodeId,
                sourceRecordId = episodeId,
                sourceRecordType = CareerTransitionSourceRecordType.CareerEpisode,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                worldTime = "40",
                description = kind.ToString(),
                exclusive = true,
                secret = secret,
                accessPolicyId = secret ? ProfessionContentIds.AccessSecretId : ProfessionContentIds.AccessPublicId
            };
        }

        private static CareerSourceRecordReferenceData Source(CareerTransitionSourceRecordType type, string id, long revision = 1L)
        {
            return new CareerSourceRecordReferenceData
            {
                recordType = type,
                recordId = id,
                sourceRevision = revision
            };
        }

        private static ProfessionalRankRecordData SeedRank(TestLabRuntimeBundle bundle, string recordId, string rankDefinitionId, ProfessionalRankState state)
        {
            ProfessionalRankRuntimeSaveData save = bundle.ProfessionalRanks.CreateSaveData();
            ProfessionalRankRecordData rank = new ProfessionalRankRecordData
            {
                rankRecordId = recordId,
                personId = PersonId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                specializationId = string.Empty,
                ladderDefinitionId = ProfessionContentIds.BlacksmithRankLadderId,
                rankDefinitionId = rankDefinitionId,
                state = state,
                trackKind = ProfessionalRankTrackKind.Formal,
                recognizingAuthorityId = GuildAuthority,
                issueWorldTime = "5",
                effectiveWorldTime = "5",
                accessPolicyId = ProfessionContentIds.AccessPublicId
            };
            save.ranks.Add(rank);
            ProfessionalRankOperationResult restore = bundle.ProfessionalRanks.RestoreFromSaveData(save, bundle.DefinitionRegistry, bundle.Professions, bundle.Training, bundle.ProfessionalActivities, bundle.Credentials, bundle.KnownPersonIds, Authorities(), restoring: false);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(bundle.ProfessionalRanks.TryGetRank(recordId, out ProfessionalRankRecordData restored), Is.True);
            return restored;
        }

        private static CareerHistoryRuntime NewCareerRuntime(TestLabRuntimeBundle bundle)
        {
            CareerHistoryRuntime runtime = new CareerHistoryRuntime();
            runtime.Configure(bundle.DefinitionRegistry, bundle.Professions, bundle.Training, bundle.ProfessionalActivities, bundle.Credentials, bundle.ProfessionalRanks, bundle.PositionEmployment, bundle.KnownPersonIds, Organizations(), Authorities());
            return runtime;
        }

        private static string[] Organizations()
        {
            return new[]
            {
                GuildOrganization,
                "organization.prototype.royal-forge",
                "organization.prototype.temple",
                "organization.prototype.university",
                "organization.prototype.government",
                IndependentOrganization
            };
        }

        private static string[] Authorities()
        {
            return new[]
            {
                GuildAuthority,
                "authority.medical.prototype",
                "authority.government.prototype",
                "authority.school.prototype",
                ProfessionContentIds.PositionAppointAuthorityId,
                ProfessionContentIds.PositionDutyAssignAuthorityId,
                ProfessionContentIds.PositionSuperviseAuthorityId,
                ProfessionContentIds.PositionRestrictedRecordsAuthorityId,
                ProfessionContentIds.BlacksmithTeachPermissionId,
                ProfessionContentIds.ForgeRestrictedStationPermissionId,
                GuildOrganization,
                IndependentOrganization
            };
        }
    }
}
