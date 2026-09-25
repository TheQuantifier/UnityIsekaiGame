using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Tests
{
    public sealed class ProfessionalRanksMasterySpecializationsTests
    {
        private const string PersonId = "person.prototype.rank-test";
        private const string AuthorityId = "authority.guild.prototype";
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeRankDefinitionsLaddersAndMasteryValidate()
        {
            DefinitionRegistry registry = Registry();
            DefinitionValidationReport report = ValidateRegistry(registry);

            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
            Assert.That(registry.TryGet(ProfessionContentIds.BlacksmithRankApprenticeId, out ProfessionalRankDefinition rank), Is.True);
            Assert.That(registry.TryGet(ProfessionContentIds.BlacksmithRankLadderId, out ProfessionalRankLadderDefinition ladder), Is.True);
            Assert.That(registry.TryGet(ProfessionContentIds.WeaponsmithMasteryId, out ProfessionalMasteryDefinition mastery), Is.True);
            Assert.That(rank.RankOrder, Is.EqualTo(10));
            Assert.That(ladder.OrderedRankDefinitionIds, Is.EqualTo(new[] { ProfessionContentIds.BlacksmithRankApprenticeId, ProfessionContentIds.BlacksmithRankJourneymanId, ProfessionContentIds.BlacksmithRankMasterId }));
            Assert.That(mastery.RequiredRankDefinitionId, Is.EqualTo(ProfessionContentIds.WeaponsmithRankMasterId));
        }

        [Test]
        public void RankLadderCycleValidationRejectsCircularAdvancement()
        {
            DefinitionRegistry registry = Registry();
            ProfessionalRankDefinition a = Rank("profession-rank.test.a", "A", 1, prior: new[] { "profession-rank.test.b" });
            ProfessionalRankDefinition b = Rank("profession-rank.test.b", "B", 2, prior: new[] { "profession-rank.test.a" });
            ProfessionalRankLadderDefinition ladder = UnityEngine.ScriptableObject.CreateInstance<ProfessionalRankLadderDefinition>();
            ladder.DevelopmentConfigure("profession-rank-ladder.test.cycle", "Cycle", ProfessionContentIds.BlacksmithProfessionId, new[] { a.Id, b.Id }, multipleRoots: true);
            DefinitionRegistry cycleRegistry = new DefinitionRegistry(registry.DefinitionsById.Values.Concat(new IGameDefinition[] { a, b, ladder }));
            DefinitionValidationReport report = ValidateRegistry(cycleRegistry);

            Assert.That(report.Messages.Any(error => error.Message.Contains("circular", StringComparison.OrdinalIgnoreCase)), Is.True, report.GetSummary());
        }

        [Test]
        public void AdvancementEvaluationUsesTrainingCredentialAndExperienceWithoutMutation()
        {
            RuntimeFixture fixture = CreateFixture();
            long rankRevision = fixture.Ranks.Revision;
            ProfessionalRankAdvancementResult blocked = fixture.Ranks.EvaluateAdvancement(PersonId, ProfessionContentIds.BlacksmithRankJourneymanId, AuthorityId, perceived: true, privilegedDiagnostics: true);
            Assert.That(blocked.AuthoritativeEligible, Is.False);
            Assert.That(blocked.PerceivedEligible, Is.True);
            Assert.That(fixture.Ranks.Revision, Is.EqualTo(rankRevision));

            ProfessionalRankOperationResult apprentice = Promote(fixture, ProfessionContentIds.BlacksmithRankApprenticeId, "apprentice");
            IssueApprenticeshipCredential(fixture, "journeyman");
            ProfessionalRankAdvancementResult eligible = fixture.Ranks.EvaluateAdvancement(PersonId, ProfessionContentIds.BlacksmithRankJourneymanId, AuthorityId, privilegedDiagnostics: true);

            Assert.That(apprentice.Succeeded, Is.True, apprentice.Message);
            Assert.That(eligible.AuthoritativeEligible, Is.True, string.Join(",", eligible.BlockingFailures));
            Assert.That(eligible.SatisfiedRequirements, Does.Contain($"credential:{ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId}"));
            Assert.That(eligible.SatisfiedRequirements.Any(item => item.StartsWith("training:", StringComparison.Ordinal)), Is.True);
            Assert.That(eligible.SatisfiedRequirements.Any(item => item.StartsWith("experience:", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void PromotionRejectsUnauthorizedStaleAndInvalidSkippingWithoutGrantingCompetencies()
        {
            RuntimeFixture fixture = CreateFixture();
            EnsureApprenticeFoundation(fixture, "unauthorized");
            ProfessionalRankAdvancementResult unauthorized = fixture.Ranks.EvaluateAdvancement(PersonId, ProfessionContentIds.BlacksmithRankApprenticeId, "authority.bad", privilegedDiagnostics: true);
            Assert.That(unauthorized.AuthoritativeEligible, Is.False);

            ProfessionalRankAdvancementResult skip = fixture.Ranks.EvaluateAdvancement(PersonId, ProfessionContentIds.BlacksmithRankMasterId, AuthorityId, privilegedDiagnostics: true);
            Assert.That(skip.AuthoritativeEligible, Is.False);

            ProfessionalRankAdvancementResult evaluation = fixture.Ranks.EvaluateAdvancement(PersonId, ProfessionContentIds.BlacksmithRankApprenticeId, AuthorityId, privilegedDiagnostics: true);
            ProfessionalRankOperationResult submit = fixture.Ranks.SubmitApplication("rank-application.test.stale", PersonId, ProfessionContentIds.BlacksmithRankApprenticeId, AuthorityId, evaluation.Snapshot, "1", "tx.submit");
            ProfessionalRankAdvancementSnapshotData staleSnapshot = evaluation.Snapshot.Clone();
            staleSnapshot.evaluationHash = "stale";
            ProfessionalRankOperationResult stale = fixture.Ranks.ApprovePromotion(submit.Application.applicationId, PersonId, staleSnapshot, "2", "tx.stale");

            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.Status, Is.EqualTo(ProfessionalRankOperationStatus.StaleEvaluation));
            Assert.That(fixture.Credentials.QueryByRecipient(PersonId).Count(item => item.credentialDefinitionId == ProfessionContentIds.BlacksmithGuildLicenseCredentialId), Is.Zero);
        }

        [Test]
        public void SpecializationRankAndMasteryUseExplicitAuthoritativeAchievement()
        {
            RuntimeFixture fixture = CreateFixture();
            Promote(fixture, ProfessionContentIds.BlacksmithRankApprenticeId, "general-apprentice");
            IssueApprenticeshipCredential(fixture, "specialization");
            Promote(fixture, ProfessionContentIds.BlacksmithRankJourneymanId, "general-journeyman");
            IssueGuildLicense(fixture, "mastery");
            Promote(fixture, ProfessionContentIds.WeaponsmithRankApprenticeId, "weapon-apprentice");
            ProfessionalRankOperationResult weaponMaster = Promote(fixture, ProfessionContentIds.WeaponsmithRankMasterId, "weapon-master");
            ProfessionalRankAdvancementResult blockedMastery = fixture.Ranks.EvaluateMastery(PersonId, ProfessionContentIds.WeaponsmithMasteryId, AuthorityId, privilegedDiagnostics: true);
            Assert.That(blockedMastery.AuthoritativeEligible, Is.False);

            ProfessionalRankOperationResult achievement = fixture.Ranks.RecordQualifyingAchievement(new ProfessionalQualifyingAchievementData
            {
                achievementId = ProfessionContentIds.BlacksmithMasterworkAchievementId,
                personId = PersonId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                specializationId = ProfessionContentIds.WeaponsmithSpecializationId,
                sourceActivityId = "professional-activity.masterwork",
                activityDefinitionId = ProfessionContentIds.BlacksmithCraftingActivityDefinitionId,
                quality = 850,
                difficulty = ProfessionalActivityDifficulty.Skilled,
                validatingAuthorityId = AuthorityId,
                worldTime = "10"
            }, "tx.achievement");
            ProfessionalRankAdvancementResult masteryEval = fixture.Ranks.EvaluateMastery(PersonId, ProfessionContentIds.WeaponsmithMasteryId, AuthorityId, privilegedDiagnostics: true);
            ProfessionalRankOperationResult mastery = fixture.Ranks.GrantMastery("mastery-record.test", PersonId, ProfessionContentIds.WeaponsmithMasteryId, AuthorityId, masteryEval.Snapshot, "11", "tx.mastery");

            Assert.That(weaponMaster.Succeeded, Is.True, weaponMaster.Message);
            Assert.That(achievement.Succeeded, Is.True, achievement.Message);
            Assert.That(masteryEval.AuthoritativeEligible, Is.True, string.Join(",", masteryEval.BlockingFailures));
            Assert.That(mastery.Succeeded, Is.True, mastery.Message);
            Assert.That(fixture.Ranks.QueryByPerson(PersonId).Any(rank => rank.rankDefinitionId == ProfessionContentIds.BlacksmithRankJourneymanId), Is.True);
            Assert.That(fixture.Ranks.QueryByPerson(PersonId).Any(rank => rank.rankDefinitionId == ProfessionContentIds.WeaponsmithRankMasterId), Is.True);
        }

        [Test]
        public void LifecyclePermissionsFollowActiveSuspendedRevokedAndRetiredStates()
        {
            RuntimeFixture fixture = CreateFixture();
            ProfessionalRankOperationResult master = Promote(fixture, ProfessionContentIds.BlacksmithRankMasterId, "master");
            string rankId = master.Rank.rankRecordId;

            Assert.That(fixture.Ranks.CanTeach(PersonId, ProfessionContentIds.BlacksmithProfessionId), Is.True);
            Assert.That(fixture.Ranks.SuspendRank(rankId, "12", "tx.suspend").Succeeded, Is.True);
            Assert.That(fixture.Ranks.CanTeach(PersonId, ProfessionContentIds.BlacksmithProfessionId), Is.False);
            Assert.That(fixture.Ranks.ReinstateRank(rankId, "13", "tx.reinstate").Succeeded, Is.True);
            Assert.That(fixture.Ranks.CanTeach(PersonId, ProfessionContentIds.BlacksmithProfessionId), Is.True);
            Assert.That(fixture.Ranks.RevokeRank(rankId, "14", "tx.revoke").Succeeded, Is.True);
            Assert.That(fixture.Ranks.CanTeach(PersonId, ProfessionContentIds.BlacksmithProfessionId), Is.False);
            Assert.That(fixture.Ranks.QueryByPerson(PersonId).Any(rank => rank.state == ProfessionalRankState.Revoked), Is.True);
            Assert.That(fixture.Credentials.CredentialCount, Is.GreaterThan(0));
        }

        [Test]
        public void RankProjectionRedactsSecretFieldsAndPersistenceRestoresWithoutReplay()
        {
            RuntimeFixture fixture = CreateFixture();
            ProfessionalRankOperationResult apprentice = Promote(fixture, ProfessionContentIds.BlacksmithRankApprenticeId, "persist");
            ProfessionalRankProjection<ProfessionalRankRecordData> projection = fixture.Ranks.ProjectRank(apprentice.Rank.rankRecordId, ProfessionalRankProjectionAudience.Public, null);
            ProfessionalRankRuntimeSaveData save = fixture.Ranks.CreateSaveData();
            ProfessionalRankRuntime restored = new ProfessionalRankRuntime();
            ProfessionalRankOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry, fixture.Professions, fixture.Training, fixture.Activities, fixture.Credentials, new[] { PersonId }, new[] { AuthorityId }, restoring: true);

            Assert.That(projection.Record, Is.Not.Null);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.QueryByPerson(PersonId).Count, Is.EqualTo(fixture.Ranks.QueryByPerson(PersonId).Count));
            Assert.That(restored.HistoryHooks.Count, Is.Zero);
            Assert.That(restored.Revision, Is.EqualTo(save.revision));
        }

        [Test]
        public void CorruptRestoreLeavesLiveRankStateUnchanged()
        {
            RuntimeFixture fixture = CreateFixture();
            Promote(fixture, ProfessionContentIds.BlacksmithRankApprenticeId, "restore");
            ProfessionalRankRuntimeSaveData before = fixture.Ranks.CreateSaveData();
            ProfessionalRankRuntimeSaveData corrupt = before.Clone();
            corrupt.ranks[0].rankDefinitionId = "profession-rank.missing";

            ProfessionalRankOperationResult rejected = fixture.Ranks.RestoreFromSaveData(corrupt, fixture.Registry, fixture.Professions, fixture.Training, fixture.Activities, fixture.Credentials, new[] { PersonId }, new[] { AuthorityId }, restoring: true);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Ranks.QueryByPerson(PersonId).Count, Is.EqualTo(before.ranks.Count));
            Assert.That(fixture.Ranks.CreateSaveData().ranks[0].rankDefinitionId, Is.EqualTo(before.ranks[0].rankDefinitionId));
        }

        private static RuntimeFixture CreateFixture()
        {
            DefinitionRegistry registry = Registry();
            PersonProfessionRuntime professions = new PersonProfessionRuntime();
            TrainingRuntime training = new TrainingRuntime();
            ProfessionalActivityRuntime activities = new ProfessionalActivityRuntime();
            CredentialRuntime credentials = new CredentialRuntime();
            ProfessionalRankRuntime ranks = new ProfessionalRankRuntime();
            InformationTransferRuntime transfers = new InformationTransferRuntime();
            string[] persons = { PersonId };
            string[] authorities = { AuthorityId, "organization.prototype.guild" };

            professions.Configure(registry, persons);
            transfers.Configure(registry, PersonId);
            training.Configure(registry, professions, transfers, persons);
            activities.Configure(registry, professions, persons);
            credentials.Configure(registry, professions, training, activities, persons, authorities);
            ranks.Configure(registry, professions, training, activities, credentials, persons, authorities);
            EnsureProfession(professions);
            return new RuntimeFixture(registry, professions, training, activities, credentials, ranks);
        }

        private static DefinitionRegistry Registry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog.CreateRegistry();
        }

        private static DefinitionValidationReport ValidateRegistry(DefinitionRegistry registry)
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in registry.DefinitionsById.Values)
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(registry.DefinitionsById, report);
                }
            }

            return report;
        }

        private static ProfessionalRankDefinition Rank(string id, string name, int order, string[] prior)
        {
            ProfessionalRankDefinition rank = UnityEngine.ScriptableObject.CreateInstance<ProfessionalRankDefinition>();
            rank.DevelopmentConfigure(id, name, ProfessionContentIds.BlacksmithProfessionId, order, ProfessionalRankCategory.Custom, priorRanks: prior, requiredAuthorities: new[] { AuthorityId });
            return rank;
        }

        private static void EnsureProfession(PersonProfessionRuntime professions)
        {
            professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.rank-test.blacksmith",
                personId = PersonId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                specializationIds = new[] { ProfessionContentIds.WeaponsmithSpecializationId },
                formalPractice = true,
                informalPractice = true,
                selfDeclared = true,
                recognized = true,
                recognizingAuthorityId = AuthorityId,
                recognitionReferenceId = "recognition.rank-test.guild",
                active = true,
                startWorldTime = "1",
                transactionId = "tx.profession"
            });
        }

        private static void EnsureApprenticeFoundation(RuntimeFixture fixture, string slug)
        {
            string enrollmentId = $"training-enrollment.{slug}.apprenticeship";
            fixture.Training.ApplyToProgram(enrollmentId, PersonId, ProfessionContentIds.BlacksmithApprenticeshipProgramId, $"tx.{slug}.apply", worldTime: 1d);
            fixture.Training.AcceptEnrollment(enrollmentId, $"tx.{slug}.accept");
            fixture.Training.AssignInstructor(enrollmentId, $"training-instructor.{slug}", TrainingInstructorRoleKind.Master, PersonId, $"tx.{slug}.instructor", professionId: ProfessionContentIds.BlacksmithProfessionId, authorityId: AuthorityId);
            fixture.Training.BeginProgram(enrollmentId, $"tx.{slug}.begin");
            fixture.Training.RunLearningSession($"training-session.{slug}.basics", enrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, ProfessionContentIds.BlacksmithSafetyLessonId, $"tx.{slug}.lesson");
            fixture.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, $"tx.{slug}.module.basics");
            fixture.Training.RunLearningSession($"training-session.{slug}.practice", enrollmentId, ProfessionContentIds.BlacksmithPracticeModuleId, ProfessionContentIds.BlacksmithDemonstrationLessonId, $"tx.{slug}.practice");
            fixture.Training.RecordPracticalAssignment($"training-practical.{slug}", enrollmentId, ProfessionContentIds.BlacksmithPracticalAssignmentId, $"activity.{slug}.practice", TrainingAssignmentActivityCategory.Crafting, $"tx.{slug}.practical", quality: 750, supervisorPersonId: PersonId);
            fixture.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithPracticeModuleId, $"tx.{slug}.module.practice");
            fixture.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithHiddenAssessmentModuleId, $"tx.{slug}.module.hidden");
            TrainingProgressResult progress = fixture.Training.EvaluateProgress(enrollmentId);
            fixture.Training.CompleteProgram(enrollmentId, $"tx.{slug}.complete", progress.RuntimeToken, worldTime: 20d);
            RegisterActivity(fixture, $"activity.{slug}.supervised", ProfessionContentIds.BlacksmithSupervisedPracticeActivityDefinitionId, ProfessionalResponsibilityLevel.SupervisedWorker, TrainingSupervisionLevel.CloselySupervised, quality: 750);
        }

        private static void EnsureSafetyTraining(RuntimeFixture fixture, string slug)
        {
            string enrollmentId = $"training-enrollment.{slug}.safety";
            fixture.Training.ApplyToProgram(enrollmentId, PersonId, ProfessionContentIds.BlacksmithSafetyProgramId, $"tx.{slug}.safety.apply", worldTime: 21d);
            fixture.Training.AcceptEnrollment(enrollmentId, $"tx.{slug}.safety.accept");
            fixture.Training.BeginProgram(enrollmentId, $"tx.{slug}.safety.begin");
            fixture.Training.RunLearningSession($"training-session.{slug}.safety", enrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, ProfessionContentIds.BlacksmithSafetyLessonId, $"tx.{slug}.safety.lesson");
            fixture.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, $"tx.{slug}.safety.module");
            TrainingProgressResult progress = fixture.Training.EvaluateProgress(enrollmentId);
            fixture.Training.CompleteProgram(enrollmentId, $"tx.{slug}.safety.complete", progress.RuntimeToken, worldTime: 22d);
        }

        private static void RegisterActivity(RuntimeFixture fixture, string activityId, string definitionId, ProfessionalResponsibilityLevel responsibility, TrainingSupervisionLevel supervision, int quality = 750)
        {
            bool supervisedPractice = string.Equals(definitionId, ProfessionContentIds.BlacksmithSupervisedPracticeActivityDefinitionId, StringComparison.Ordinal);
            ProfessionalActivitySourceType sourceType = supervisedPractice ? ProfessionalActivitySourceType.TrainingPracticalAssignment : ProfessionalActivitySourceType.CraftingOperation;
            string sourceTag = supervisedPractice ? "training.activity.practical" : "source.crafting";
            fixture.Activities.RegisterAndValidateActivity(new ProfessionalActivityRegistrationRequest
            {
                ActivityId = activityId,
                PersonId = PersonId,
                ProfessionId = ProfessionContentIds.BlacksmithProfessionId,
                SpecializationId = ProfessionContentIds.WeaponsmithSpecializationId,
                ActivityDefinitionId = definitionId,
                Source = ProfessionalActivitySourceAdapters.FromCustom(sourceType, $"source.{activityId}", PersonId, ProfessionalActivityOutcomeState.Successful, completed: true, quality: quality, difficulty: ProfessionalActivityDifficulty.Skilled, worldTime: "30", sourceTag),
                Responsibility = responsibility,
                SupervisionLevel = supervision,
                CompletionWorldTime = "30",
                QuantityOrDuration = 1f,
                Quality = quality,
                Difficulty = ProfessionalActivityDifficulty.Skilled,
                Outcome = ProfessionalActivityOutcomeState.Successful,
                AccessPolicyId = ProfessionContentIds.AccessPublicId,
                Provenance = "test"
            }, $"evidence.{activityId}", AuthorityId, $"tx.{activityId}");
        }

        private static CredentialOperationResult RecordExam(RuntimeFixture fixture, string slug, string examinationId, int score)
        {
            return fixture.Credentials.RecordExaminationAttempt(new CredentialExaminationAttemptData
            {
                attemptId = $"credential-exam.{slug}",
                examinationDefinitionId = examinationId,
                applicantPersonId = PersonId,
                evaluatorPersonId = PersonId,
                evaluatorAuthorityId = AuthorityId,
                startWorldTime = "31",
                completionWorldTime = "32",
                score = score,
                sectionResults = new[]
                {
                    new CredentialExaminationSectionResultData { sectionId = $"section.{slug}", displayName = "Section", score = score, passed = score >= 700 }
                }
            }, $"tx.exam.{slug}");
        }

        private static CredentialOperationResult IssueApprenticeshipCredential(RuntimeFixture fixture, string slug)
        {
            EnsureApprenticeFoundation(fixture, slug);
            RecordExam(fixture, $"{slug}.practical", ProfessionContentIds.BlacksmithPracticalExaminationId, 850);
            CredentialQualificationResult qualification = fixture.Credentials.EvaluateQualification(PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId);
            CredentialOperationResult apply = fixture.Credentials.SubmitApplication($"credential-application.{slug}.apprentice", PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, Issuer(), qualification.Snapshot, "33", $"tx.{slug}.credential.apply");
            fixture.Credentials.ApproveApplication(apply.Application?.applicationId, AuthorityId, qualification.Snapshot, "34", $"tx.{slug}.credential.approve");
            return fixture.Credentials.IssueCredential($"credential-record.{slug}.apprentice", ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, PersonId, Issuer(), apply.Application?.applicationId, $"credential-exam.{slug}.practical", $"registration.{slug}.apprentice", qualification.Snapshot, "35", $"tx.{slug}.credential.issue");
        }

        private static CredentialOperationResult IssueGuildLicense(RuntimeFixture fixture, string slug)
        {
            EnsureSafetyTraining(fixture, slug);
            RegisterActivity(fixture, $"activity.{slug}.independent", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, ProfessionalResponsibilityLevel.IndependentPractitioner, TrainingSupervisionLevel.IndependentWithReview, quality: 800);
            RecordExam(fixture, $"{slug}.practical", ProfessionContentIds.BlacksmithPracticalExaminationId, 850);
            RecordExam(fixture, $"{slug}.written", ProfessionContentIds.BlacksmithWrittenExaminationId, 840);
            CredentialQualificationResult qualification = fixture.Credentials.EvaluateQualification(PersonId, ProfessionContentIds.BlacksmithGuildLicenseCredentialId);
            CredentialOperationResult apply = fixture.Credentials.SubmitApplication($"credential-application.{slug}.guild", PersonId, ProfessionContentIds.BlacksmithGuildLicenseCredentialId, Issuer(), qualification.Snapshot, "36", $"tx.{slug}.guild.apply");
            fixture.Credentials.ApproveApplication(apply.Application?.applicationId, AuthorityId, qualification.Snapshot, "37", $"tx.{slug}.guild.approve");
            return fixture.Credentials.IssueCredential($"credential-record.{slug}.guild", ProfessionContentIds.BlacksmithGuildLicenseCredentialId, PersonId, Issuer(), apply.Application?.applicationId, $"credential-exam.{slug}.practical", $"registration.{slug}.guild", qualification.Snapshot, "38", $"tx.{slug}.guild.issue");
        }

        private static ProfessionalRankOperationResult Promote(RuntimeFixture fixture, string rankDefinitionId, string slug)
        {
            if (rankDefinitionId == ProfessionContentIds.BlacksmithRankJourneymanId)
            {
                Promote(fixture, ProfessionContentIds.BlacksmithRankApprenticeId, $"{slug}.prior");
                if (!fixture.Credentials.QueryByRecipient(PersonId, activeOnly: true).Any(item => item.credentialDefinitionId == ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId))
                {
                    IssueApprenticeshipCredential(fixture, $"{slug}.credential");
                }
            }
            else if (rankDefinitionId == ProfessionContentIds.BlacksmithRankMasterId)
            {
                Promote(fixture, ProfessionContentIds.BlacksmithRankJourneymanId, $"{slug}.prior");
                if (!fixture.Credentials.QueryByRecipient(PersonId, activeOnly: true).Any(item => item.credentialDefinitionId == ProfessionContentIds.BlacksmithGuildLicenseCredentialId))
                {
                    IssueGuildLicense(fixture, $"{slug}.license");
                }
            }
            else if (rankDefinitionId == ProfessionContentIds.WeaponsmithRankApprenticeId)
            {
                Promote(fixture, ProfessionContentIds.BlacksmithRankApprenticeId, $"{slug}.prior");
                if (!fixture.Credentials.QueryByRecipient(PersonId, activeOnly: true).Any(item => item.credentialDefinitionId == ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId))
                {
                    IssueApprenticeshipCredential(fixture, $"{slug}.credential");
                }
            }
            else if (rankDefinitionId == ProfessionContentIds.WeaponsmithRankMasterId)
            {
                Promote(fixture, ProfessionContentIds.BlacksmithRankJourneymanId, $"{slug}.general");
                Promote(fixture, ProfessionContentIds.WeaponsmithRankApprenticeId, $"{slug}.special");
                if (!fixture.Credentials.QueryByRecipient(PersonId, activeOnly: true).Any(item => item.credentialDefinitionId == ProfessionContentIds.BlacksmithGuildLicenseCredentialId))
                {
                    IssueGuildLicense(fixture, $"{slug}.license");
                }
            }
            else
            {
                EnsureApprenticeFoundation(fixture, slug);
            }

            ProfessionalRankRecordData existing = fixture.Ranks.QueryByPerson(PersonId, currentOnly: true).FirstOrDefault(item => item.rankDefinitionId == rankDefinitionId);
            if (existing != null)
            {
                return ProfessionalRankOperationResult.Success("Rank already active.", fixture.Ranks.Revision, fixture.Ranks.Revision, rank: existing, duplicate: true);
            }

            ProfessionalRankAdvancementResult evaluation = fixture.Ranks.EvaluateAdvancement(PersonId, rankDefinitionId, AuthorityId, privilegedDiagnostics: true);
            if (!evaluation.AuthoritativeEligible)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingQualification, string.Join(",", evaluation.BlockingFailures), fixture.Ranks.Revision, evaluation);
            }

            ProfessionalRankOperationResult submit = fixture.Ranks.SubmitApplication($"rank-application.{slug}", PersonId, rankDefinitionId, AuthorityId, evaluation.Snapshot, "40", $"tx.{slug}.rank.submit");
            fixture.Ranks.ApprovePromotion(submit.Application?.applicationId, PersonId, evaluation.Snapshot, "41", $"tx.{slug}.rank.approve");
            return fixture.Ranks.PromotePerson($"rank-record.{slug}", submit.Application?.applicationId, evaluation.Snapshot, "42", $"tx.{slug}.rank.promote");
        }

        private static CredentialIssuerReferenceData Issuer()
        {
            return new CredentialIssuerReferenceData { issuerId = AuthorityId, issuerKind = CredentialIssuerAuthorityKind.Guild };
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(DefinitionRegistry registry, PersonProfessionRuntime professions, TrainingRuntime training, ProfessionalActivityRuntime activities, CredentialRuntime credentials, ProfessionalRankRuntime ranks)
            {
                Registry = registry;
                Professions = professions;
                Training = training;
                Activities = activities;
                Credentials = credentials;
                Ranks = ranks;
            }

            public DefinitionRegistry Registry { get; }
            public PersonProfessionRuntime Professions { get; }
            public TrainingRuntime Training { get; }
            public ProfessionalActivityRuntime Activities { get; }
            public CredentialRuntime Credentials { get; }
            public ProfessionalRankRuntime Ranks { get; }
        }
    }
}
