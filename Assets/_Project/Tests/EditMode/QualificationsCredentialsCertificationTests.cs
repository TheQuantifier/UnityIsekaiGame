using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Tests
{
    public sealed class QualificationsCredentialsCertificationTests
    {
        private const string PersonId = "person.credential.test";
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeCredentialDefinitionsValidate()
        {
            DefinitionRegistry registry = Registry();
            DefinitionValidationReport report = new DefinitionValidationReport();

            foreach (IGameDefinition definition in ProfessionContentIds.GetDefinitions(registry))
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(registry.DefinitionsById, report);
                }
            }

            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
            Assert.That(registry.TryGet(ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, out CredentialDefinition credential), Is.True);
            Assert.That(credential.RequiredTrainingProgramIds, Does.Contain(ProfessionContentIds.BlacksmithApprenticeshipProgramId));
            Assert.That(registry.TryGet(ProfessionContentIds.BlacksmithPracticalExaminationId, out CredentialExaminationDefinition exam), Is.True);
            Assert.That(exam.RelatedCredentialDefinitionIds, Does.Contain(ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId));
        }

        [Test]
        public void QualificationEvaluationUsesSharedEvidenceWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            long professionRevision = fixture.Professions.Revision;
            long trainingRevision = fixture.Training.Revision;
            long activityRevision = fixture.Activities.Revision;
            long credentialRevision = fixture.Credentials.Revision;

            CredentialQualificationResult initial = fixture.Credentials.EvaluateQualification(PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, privilegedDiagnostics: true);
            CompleteTrainingAndExperience(fixture, "qualification");
            CredentialQualificationResult withoutExam = fixture.Credentials.EvaluateQualification(PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, privilegedDiagnostics: true);
            RecordExam(fixture, "qualification", ProfessionContentIds.BlacksmithPracticalExaminationId, 850);
            CredentialQualificationResult qualified = fixture.Credentials.EvaluateQualification(PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, privilegedDiagnostics: true);

            Assert.That(initial.AuthoritativeQualified, Is.False);
            Assert.That(fixture.Professions.Revision, Is.GreaterThanOrEqualTo(professionRevision));
            Assert.That(fixture.Training.Revision, Is.GreaterThan(trainingRevision));
            Assert.That(fixture.Activities.Revision, Is.GreaterThan(activityRevision));
            Assert.That(fixture.Credentials.Revision, Is.GreaterThan(credentialRevision));
            Assert.That(withoutExam.AuthoritativeQualified, Is.False);
            Assert.That(withoutExam.BlockingFailures, Does.Contain($"examination:{ProfessionContentIds.BlacksmithPracticalExaminationId}"));
            Assert.That(qualified.AuthoritativeQualified, Is.True);
            Assert.That(fixture.Credentials.ApplicationCount, Is.Zero);
            Assert.That(fixture.Credentials.CredentialCount, Is.Zero);
        }

        [Test]
        public void ApplicationExaminationAndIssuanceCreateCredentialWithoutGrantingProfession()
        {
            Fixture fixture = CreateFixture();
            CredentialOperationResult issued = IssueApprenticeshipCredential(fixture, "issue", out _);

            Assert.That(issued.Succeeded, Is.True, issued.Message);
            Assert.That(issued.Credential.authenticityState, Is.EqualTo(CredentialAuthenticityState.Authoritative));
            Assert.That(fixture.Credentials.HasActivePermission(PersonId, ProfessionContentIds.BlacksmithPracticePermissionId), Is.True);
            Assert.That(fixture.Professions.QueryByProfession(ProfessionContentIds.BlacksmithProfessionId).Count, Is.EqualTo(1));
            Assert.That(fixture.Professions.QueryByProfession(ProfessionContentIds.FieldMedicProfessionId).Count, Is.Zero);
        }

        [Test]
        public void UnauthorizedStaleAndForgedCredentialsRejectWithoutAuthorityMutation()
        {
            Fixture fixture = CreateFixture();
            CompleteTrainingAndExperience(fixture, "boundary");
            RecordExam(fixture, "boundary", ProfessionContentIds.BlacksmithPracticalExaminationId, 850);
            CredentialQualificationResult qualification = fixture.Credentials.EvaluateQualification(PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId);

            CredentialOperationResult badIssuer = fixture.Credentials.SubmitApplication("credential-application.bad-issuer", PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, new CredentialIssuerReferenceData { issuerId = "authority.medical.prototype", issuerKind = CredentialIssuerAuthorityKind.ProfessionalOrganization }, qualification.Snapshot, "30", "tx.bad-issuer");
            CredentialOperationResult apply = fixture.Credentials.SubmitApplication("credential-application.stale", PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, GuildIssuer(), qualification.Snapshot, "31", "tx.stale.apply");
            fixture.Activities.RegisterAndValidateActivity(Request("activity.stale.extra", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, Source(ProfessionalActivitySourceType.CraftingOperation, "source.stale.extra", "source.crafting")), "evidence.stale.extra", "authority.guild.prototype", "tx.stale.extra");
            CredentialQualificationResult current = fixture.Credentials.EvaluateQualification(PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId);
            CredentialOperationResult stale = fixture.Credentials.ApproveApplication(apply.Application.applicationId, "authority.guild.prototype", current.Snapshot, "32", "tx.stale.approve");
            CredentialOperationResult forged = fixture.Credentials.RecordForgedClaim("credential.forged.claim", ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, PersonId, "authority.guild.prototype", "33", "tx.forged");

            Assert.That(badIssuer.Succeeded, Is.False);
            Assert.That(badIssuer.Status, Is.EqualTo(CredentialOperationStatus.UnauthorizedIssuer));
            Assert.That(apply.Succeeded, Is.True, apply.Message);
            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.Status, Is.EqualTo(CredentialOperationStatus.StaleQualification));
            Assert.That(forged.Succeeded, Is.True, forged.Message);
            Assert.That(forged.Credential.state, Is.EqualTo(CredentialState.ForgedClaimFoundation));
            Assert.That(fixture.Credentials.HasActivePermission(PersonId, ProfessionContentIds.BlacksmithPracticePermissionId, CredentialPermissionStatePolicy.AnyNonRevoked), Is.False);
        }

        [Test]
        public void LifecycleAndProjectionRespectCredentialState()
        {
            Fixture fixture = CreateFixture();
            CredentialOperationResult issued = IssueApprenticeshipCredential(fixture, "lifecycle", out _);
            string credentialId = issued.Credential.credentialId;

            CredentialOperationResult suspend = fixture.Credentials.SuspendCredential(credentialId, "40", "tx.suspend");
            bool suspendedPermission = fixture.Credentials.HasActivePermission(PersonId, ProfessionContentIds.BlacksmithPracticePermissionId);
            CredentialOperationResult reinstate = fixture.Credentials.ReinstateCredential(credentialId, "41", "tx.reinstate");
            bool reinstatedPermission = fixture.Credentials.HasActivePermission(PersonId, ProfessionContentIds.BlacksmithPracticePermissionId);
            CredentialOperationResult renew = fixture.Credentials.RenewCredential(credentialId, null, "42", "tx.renew");
            CredentialOperationResult expire = fixture.Credentials.ExpireCredential(credentialId, "43", "tx.expire");
            bool expiredPermission = fixture.Credentials.HasActivePermission(PersonId, ProfessionContentIds.BlacksmithPracticePermissionId);
            InformationAccessDecision decision = new InformationAccessDecision("person.observer", CredentialInformationSubject.Create(CredentialInformationSubject.CredentialTag, credentialId, PersonId), InformationAccessMode.Inspect, InformationAccessDecisionKind.RedactedAccess, InformationAccessDenialCode.DetailRestriction, false, InformationResharingPolicy.NoResharing, new[] { "credential-definition-id", "state" }, CredentialInformationSubject.ProtectedFields, Array.Empty<string>(), new[] { ProfessionContentIds.AccessPublicId }, 44d, "Redacted.", "Hidden.", true);
            CredentialProjection<CredentialRecordData> projection = fixture.Credentials.ProjectCredential(credentialId, CredentialProjectionAudience.PublicInspection, decision);

            Assert.That(issued.Succeeded, Is.True, issued.Message);
            Assert.That(suspend.Succeeded, Is.True, suspend.Message);
            Assert.That(suspendedPermission, Is.False);
            Assert.That(reinstate.Succeeded, Is.True, reinstate.Message);
            Assert.That(reinstatedPermission, Is.True);
            Assert.That(renew.Succeeded, Is.False);
            Assert.That(renew.Status, Is.EqualTo(CredentialOperationStatus.InvalidTransition));
            Assert.That(expire.Succeeded, Is.True, expire.Message);
            Assert.That(expiredPermission, Is.False);
            Assert.That(projection.Redacted, Is.True);
            Assert.That(projection.Record.registrationNumber, Is.Empty);
        }

        [Test]
        public void PersistenceRestoreIsAtomicAndDoesNotReplayCredentialHistory()
        {
            Fixture fixture = CreateFixture();
            CredentialOperationResult issued = IssueApprenticeshipCredential(fixture, "persist", out _);
            CredentialRuntimeSaveData save = fixture.Credentials.CreateSaveData();
            CredentialRuntime restored = new CredentialRuntime();
            CredentialOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry, fixture.Professions, fixture.Training, fixture.Activities, fixture.KnownPersons, fixture.KnownAuthorities, restoring: true);
            CredentialRuntimeSaveData corrupt = save.Clone();
            corrupt.credentials[0].supportingApplicationId = string.Empty;
            CredentialOperationResult rejected = restored.RestoreFromSaveData(corrupt, fixture.Registry, fixture.Professions, fixture.Training, fixture.Activities, fixture.KnownPersons, fixture.KnownAuthorities, restoring: true);

            Assert.That(issued.Succeeded, Is.True, issued.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.CredentialCount, Is.EqualTo(1));
            Assert.That(restored.HistoryHooks.Count, Is.Zero);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Status, Is.EqualTo(CredentialOperationStatus.RestoreFailed));
            Assert.That(restored.CredentialCount, Is.EqualTo(1));
        }

        private static Fixture CreateFixture()
        {
            DefinitionRegistry registry = Registry();
            string[] knownPersons = { PersonId, "person.observer" };
            string[] knownAuthorities = { "authority.guild.prototype", "authority.medical.prototype", "organization.prototype.adventurers-guild" };
            PersonProfessionRuntime professions = new PersonProfessionRuntime();
            professions.Configure(registry, knownPersons);
            ProfessionOperationResult relationship = professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.credential.blacksmith",
                personId = PersonId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                specializationIds = new[] { ProfessionContentIds.WeaponsmithSpecializationId },
                informalPractice = true,
                selfDeclared = true,
                active = true,
                startWorldTime = "1",
                transactionId = "tx.credential.profession"
            });
            Assert.That(relationship.Succeeded, Is.True, relationship.Message);
            TrainingRuntime training = new TrainingRuntime();
            training.Configure(registry, professions, null, knownPersons);
            ProfessionalActivityRuntime activities = new ProfessionalActivityRuntime();
            activities.Configure(registry, professions, knownPersons);
            CredentialRuntime credentials = new CredentialRuntime();
            credentials.Configure(registry, professions, training, activities, knownPersons, knownAuthorities);
            return new Fixture(registry, professions, training, activities, credentials, knownPersons, knownAuthorities);
        }

        private static DefinitionRegistry Registry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog.CreateRegistry();
        }

        private static void CompleteTrainingAndExperience(Fixture fixture, string slug)
        {
            string enrollmentId = $"training-enrollment.credential.{slug}";
            fixture.Training.ApplyToProgram(enrollmentId, PersonId, ProfessionContentIds.BlacksmithApprenticeshipProgramId, $"tx.training.{slug}.apply", worldTime: 1d);
            fixture.Training.AcceptEnrollment(enrollmentId, $"tx.training.{slug}.accept");
            fixture.Training.AssignInstructor(enrollmentId, $"training-instructor.{slug}.master", TrainingInstructorRoleKind.Master, PersonId, $"tx.training.{slug}.master", professionId: ProfessionContentIds.BlacksmithProfessionId, authorityId: "authority.guild.prototype");
            fixture.Training.BeginProgram(enrollmentId, $"tx.training.{slug}.begin");
            fixture.Training.RunLearningSession($"training-session.{slug}.safety", enrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, ProfessionContentIds.BlacksmithSafetyLessonId, $"tx.training.{slug}.safety");
            fixture.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, $"tx.training.{slug}.module.basics");
            fixture.Training.RunLearningSession($"training-session.{slug}.practice", enrollmentId, ProfessionContentIds.BlacksmithPracticeModuleId, ProfessionContentIds.BlacksmithDemonstrationLessonId, $"tx.training.{slug}.practice");
            fixture.Training.RecordPracticalAssignment($"training-practical.{slug}.complete", enrollmentId, ProfessionContentIds.BlacksmithPracticalAssignmentId, $"crafting-operation.{slug}.training", TrainingAssignmentActivityCategory.Crafting, $"tx.training.{slug}.practical", quality: 700, supervisorPersonId: PersonId);
            fixture.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithPracticeModuleId, $"tx.training.{slug}.module.practice");
            fixture.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithHiddenAssessmentModuleId, $"tx.training.{slug}.module.hidden");
            TrainingProgressResult progress = fixture.Training.EvaluateProgress(enrollmentId, perceived: false);
            fixture.Training.CompleteProgram(enrollmentId, $"tx.training.{slug}.complete", progress.RuntimeToken, worldTime: 24d);
            fixture.Activities.RegisterAndValidateActivity(Request($"activity.credential.{slug}.practice", ProfessionContentIds.BlacksmithSupervisedPracticeActivityDefinitionId, Source(ProfessionalActivitySourceType.TrainingPracticalAssignment, $"source.credential.{slug}.practice", "training.activity.practical"), ProfessionalResponsibilityLevel.SupervisedWorker, TrainingSupervisionLevel.CloselySupervised), $"evidence.credential.{slug}.practice", "authority.guild.prototype", $"tx.activity.{slug}.practice");
        }

        private static CredentialOperationResult RecordExam(Fixture fixture, string slug, string examinationDefinitionId, int score)
        {
            return fixture.Credentials.RecordExaminationAttempt(new CredentialExaminationAttemptData
            {
                attemptId = $"credential-exam.{slug}",
                examinationDefinitionId = examinationDefinitionId,
                applicantPersonId = PersonId,
                evaluatorPersonId = PersonId,
                evaluatorAuthorityId = "authority.guild.prototype",
                startWorldTime = "25",
                completionWorldTime = "26",
                score = score,
                sectionResults = new[]
                {
                    new CredentialExaminationSectionResultData
                    {
                        sectionId = $"credential-section.{slug}",
                        displayName = "Prototype assessment",
                        score = score,
                        passed = score >= 700
                    }
                },
                provenance = $"credential-exam-provenance.{slug}"
            }, $"tx.exam.{slug}");
        }

        private static CredentialOperationResult IssueApprenticeshipCredential(Fixture fixture, string slug, out string applicationId)
        {
            CompleteTrainingAndExperience(fixture, slug);
            CredentialOperationResult exam = RecordExam(fixture, slug, ProfessionContentIds.BlacksmithPracticalExaminationId, 850);
            CredentialQualificationResult qualification = fixture.Credentials.EvaluateQualification(PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId);
            CredentialOperationResult apply = fixture.Credentials.SubmitApplication($"credential-application.{slug}", PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, GuildIssuer(), qualification.Snapshot, "30", $"tx.application.{slug}");
            CredentialOperationResult approve = fixture.Credentials.ApproveApplication(apply.Application.applicationId, "authority.guild.prototype", qualification.Snapshot, "31", $"tx.approve.{slug}");
            applicationId = apply.Application.applicationId;
            Assert.That(exam.Succeeded, Is.True, exam.Message);
            Assert.That(qualification.AuthoritativeQualified, Is.True);
            Assert.That(apply.Succeeded, Is.True, apply.Message);
            Assert.That(approve.Succeeded, Is.True, approve.Message);
            return fixture.Credentials.IssueCredential($"credential-record.{slug}", ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, PersonId, GuildIssuer(), applicationId, exam.ExaminationAttempt.attemptId, $"registration.{slug}", qualification.Snapshot, "32", $"tx.issue.{slug}");
        }

        private static CredentialIssuerReferenceData GuildIssuer()
        {
            return new CredentialIssuerReferenceData
            {
                issuerId = "authority.guild.prototype",
                issuerKind = CredentialIssuerAuthorityKind.Guild
            };
        }

        private static ProfessionalActivityRegistrationRequest Request(
            string activityId,
            string definitionId,
            ProfessionalActivitySourceSnapshot source,
            ProfessionalResponsibilityLevel responsibility = ProfessionalResponsibilityLevel.IndependentPractitioner,
            TrainingSupervisionLevel supervision = TrainingSupervisionLevel.IndependentWithReview)
        {
            return new ProfessionalActivityRegistrationRequest
            {
                ActivityId = activityId,
                PersonId = PersonId,
                ProfessionId = ProfessionContentIds.BlacksmithProfessionId,
                SpecializationId = ProfessionContentIds.WeaponsmithSpecializationId,
                ActivityDefinitionId = definitionId,
                Source = source,
                Responsibility = responsibility,
                SupervisionLevel = supervision,
                CompletionWorldTime = source?.WorldTime,
                QuantityOrDuration = source?.QuantityOrDuration ?? 1f,
                Quality = source?.Quality ?? 700,
                Difficulty = source?.Difficulty ?? ProfessionalActivityDifficulty.Routine,
                Outcome = source?.Outcome ?? ProfessionalActivityOutcomeState.Successful,
                AccessPolicyId = ProfessionContentIds.AccessPublicId,
                Provenance = "test"
            };
        }

        private static ProfessionalActivitySourceSnapshot Source(ProfessionalActivitySourceType type, string sourceId, string tag)
        {
            return ProfessionalActivitySourceAdapters.FromCustom(
                type,
                sourceId,
                PersonId,
                ProfessionalActivityOutcomeState.Successful,
                quality: 750,
                difficulty: ProfessionalActivityDifficulty.Routine,
                worldTime: sourceId,
                tags: tag);
        }

        private sealed class Fixture
        {
            public Fixture(DefinitionRegistry registry, PersonProfessionRuntime professions, TrainingRuntime training, ProfessionalActivityRuntime activities, CredentialRuntime credentials, string[] knownPersons, string[] knownAuthorities)
            {
                Registry = registry;
                Professions = professions;
                Training = training;
                Activities = activities;
                Credentials = credentials;
                KnownPersons = knownPersons;
                KnownAuthorities = knownAuthorities;
            }

            public DefinitionRegistry Registry { get; }
            public PersonProfessionRuntime Professions { get; }
            public TrainingRuntime Training { get; }
            public ProfessionalActivityRuntime Activities { get; }
            public CredentialRuntime Credentials { get; }
            public string[] KnownPersons { get; }
            public string[] KnownAuthorities { get; }
        }
    }
}
