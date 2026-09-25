using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Tests
{
    public sealed class ProfessionalEligibilityEntryTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string PersonId = "person.profession-entry.tests";

        [Test]
        public void PrototypeEntryPathDefinitionsValidateAgainstProfessionDefinitions()
        {
            DefinitionRegistry registry = CreateRegistry();
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
            Assert.That(registry.TryGet(ProfessionContentIds.FieldMedicRecognitionEntryPathId, out ProfessionEntryPathDefinition medic), Is.True);
            Assert.That(medic.RequiresRecognizingAuthority, Is.True);
            Assert.That(medic.RecognizingAuthorityIds, Does.Contain("authority.medical.prototype"));
        }

        [Test]
        public void EligibilityPreviewIsImmutableAndDoesNotMutateProfessionState()
        {
            RuntimeFixture fixture = CreateFixture();
            ProfessionEligibilityResult result = fixture.Entry.Evaluate(BlacksmithContext(preview: true));

            fixture.Professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.mutation.after-preview",
                personId = PersonId,
                professionId = ProfessionContentIds.FieldMedicProfessionId,
                informalPractice = true,
                startWorldTime = "1"
            });

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(fixture.Entry.Revision, Is.Zero);
            Assert.That(result.RuntimeToken.professionRevision, Is.Zero);
            Assert.That(result.RuntimeToken.SemanticallyEquals(fixture.Entry.Evaluate(BlacksmithContext(preview: true)).RuntimeToken), Is.False);
        }

        [Test]
        public void InformalSelfDeclarationCreatesPracticeWithoutRecognitionOrCompetencyGrants()
        {
            RuntimeFixture fixture = CreateFixture();
            ProfessionEntryOperationResult entry = fixture.Entry.EnterInformal(BlacksmithContext(preview: false), "tx.entry.blacksmith");

            Assert.That(entry.Succeeded, Is.True, entry.Message);
            Assert.That(entry.Relationship, Is.Not.Null);
            Assert.That(entry.Relationship.SelfDeclared, Is.True);
            Assert.That(entry.Relationship.InformalPractice, Is.True);
            Assert.That(entry.Relationship.FormalPractice, Is.False);
            Assert.That(entry.Relationship.Recognized, Is.False);
            Assert.That(fixture.KnowledgeRevision, Is.EqualTo(0L));
        }

        [Test]
        public void FormalRequestApprovalRevalidatesAndCreatesRecognizedRelationship()
        {
            RuntimeFixture fixture = CreateFixture();
            ProfessionEntryOperationResult submit = fixture.Entry.SubmitFormalRequest(MedicContext(preview: false), "tx.entry.medic.submit", "profession-entry-request.medic");
            Assert.That(submit.Succeeded, Is.True, submit.Message);
            Assert.That(fixture.Professions.QueryByProfession(ProfessionContentIds.FieldMedicProfessionId), Is.Empty);
            ProfessionOperationResult unrelatedMutation = fixture.Professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.unrelated.before-approval",
                personId = PersonId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                informalPractice = true,
                selfDeclared = true,
                startWorldTime = "2.5",
                transactionId = "tx.entry.unrelated.before-approval"
            });
            Assert.That(unrelatedMutation.Succeeded, Is.True, unrelatedMutation.Message);

            ProfessionEntryOperationResult approve = fixture.Entry.ApproveFormalRequest("profession-entry-request.medic", "authority.medical.prototype", "tx.entry.medic.approve");

            Assert.That(approve.Succeeded, Is.True, approve.Message);
            Assert.That(approve.Relationship.Recognized, Is.True);
            Assert.That(approve.Relationship.FormalPractice, Is.True);
            Assert.That(approve.Relationship.RecognizingAuthorityId, Is.EqualTo("authority.medical.prototype"));
            Assert.That(approve.Request.State, Is.EqualTo(ProfessionEntryRequestState.Approved));
        }

        [Test]
        public void InvalidAuthorityAndStaleEvaluationRejectBeforeMutation()
        {
            RuntimeFixture fixture = CreateFixture();
            ProfessionEligibilityResult evaluated = fixture.Entry.Evaluate(BlacksmithContext(preview: true));
            ProfessionEligibilityResult invalidAuthority = fixture.Entry.Evaluate(new ProfessionEligibilityContext(
                PersonId,
                ProfessionContentIds.FieldMedicProfessionId,
                ProfessionContentIds.FieldMedicRecognitionEntryPathId,
                formal: true,
                authorityId: "authority.guild.prototype",
                skills: new[] { Skill("skill.healing-magic", 1) },
                knowledgeSubjects: new[] { "knowledge.subject.first-aid" }));

            fixture.Professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.stale.other",
                personId = PersonId,
                professionId = ProfessionContentIds.FieldMedicProfessionId,
                informalPractice = true,
                startWorldTime = "2"
            });

            ProfessionEntryOperationResult stale = fixture.Entry.EnterInformal(new ProfessionEligibilityContext(
                PersonId,
                ProfessionContentIds.BlacksmithProfessionId,
                ProfessionContentIds.BlacksmithSelfDeclaredEntryPathId,
                selfDeclared: true,
                expectedRuntimeToken: evaluated.RuntimeToken,
                preview: false), "tx.entry.stale");

            Assert.That(invalidAuthority.Succeeded, Is.False);
            Assert.That(invalidAuthority.Status, Is.EqualTo(ProfessionEligibilityStatus.InvalidAuthority));
            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.Status, Is.EqualTo(ProfessionEntryOperationStatus.EligibilityFailed));
            Assert.That(fixture.Professions.QueryByProfession(ProfessionContentIds.BlacksmithProfessionId), Is.Empty);
        }

        [Test]
        public void SpecializationAndReentryUseParentProfessionState()
        {
            RuntimeFixture fixture = CreateFixture();
            ProfessionEntryOperationResult parent = fixture.Entry.EnterInformal(BlacksmithContext(preview: false), "tx.entry.parent", "profession-relationship.parent");
            ProfessionEntryOperationResult specialization = fixture.Entry.EnterSpecialization(new ProfessionEligibilityContext(
                PersonId,
                ProfessionContentIds.BlacksmithProfessionId,
                ProfessionContentIds.WeaponsmithSpecializationEntryPathId,
                ProfessionContentIds.WeaponsmithSpecializationId,
                skills: new[] { Skill("skill.smithing", 1) },
                preview: false), "profession-relationship.parent", "tx.entry.spec");
            ProfessionOperationResult inactive = fixture.Professions.Activate("profession-relationship.parent", false);
            ProfessionEntryOperationResult resume = fixture.Entry.ResumeInactive(new ProfessionEligibilityContext(
                PersonId,
                ProfessionContentIds.BlacksmithProfessionId,
                ProfessionContentIds.BlacksmithReentryPathId,
                preview: false), "profession-relationship.parent", "tx.entry.resume");

            Assert.That(parent.Succeeded, Is.True, parent.Message);
            Assert.That(specialization.Succeeded, Is.True, specialization.Message);
            Assert.That(inactive.Succeeded, Is.True, inactive.Message);
            Assert.That(resume.Succeeded, Is.True, resume.Message);
            Assert.That(fixture.Professions.TryGetSnapshot("profession-relationship.parent", out PersonProfessionSnapshot final), Is.True);
            Assert.That(final.Active, Is.True);
            Assert.That(final.SpecializationIds, Does.Contain(ProfessionContentIds.WeaponsmithSpecializationId));
        }

        [Test]
        public void EntryRequestProjectionAndPersistenceRoundTripAreAtomic()
        {
            RuntimeFixture fixture = CreateFixture();
            ProfessionEntryOperationResult submit = fixture.Entry.SubmitFormalRequest(MedicContext(preview: false), "tx.entry.persist", "profession-entry-request.persist");
            ProfessionEntryProjection<ProfessionEntryRequestSnapshot> projection = fixture.Entry.ProjectRequest("profession-entry-request.persist", ProfessionEntryProjectionAudience.PublicInspection, RedactedDecision());
            ProfessionEntryRuntimeSaveData save = fixture.Entry.CreateSaveData();

            ProfessionEntryRuntime restored = new ProfessionEntryRuntime();
            ProfessionEntryOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry, fixture.Professions, new[] { PersonId }, restoring: true);
            ProfessionEntryRuntimeSaveData corrupt = save.Clone();
            corrupt.requests[0].professionId = ProfessionContentIds.BlacksmithProfessionId;
            ProfessionEntryOperationResult rejected = restored.RestoreFromSaveData(corrupt, fixture.Registry, fixture.Professions, new[] { PersonId }, restoring: true);

            Assert.That(submit.Succeeded, Is.True, submit.Message);
            Assert.That(projection.Redacted, Is.True);
            Assert.That(projection.Record.ApplicantPersonId, Is.Empty);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(restored.HistoryHooks, Is.Empty);
        }

        [Test]
        public void PersistenceParticipantCapturesAndCommitsEntryRequests()
        {
            RuntimeFixture fixture = CreateFixture();
            fixture.Entry.SubmitFormalRequest(MedicContext(preview: false), "tx.entry.participant", "profession-entry-request.participant");
            ProfessionEntryPersistenceParticipant participant = new ProfessionEntryPersistenceParticipant(fixture.Entry, () => fixture.Registry, () => fixture.Professions, () => new[] { PersonId });

            PersistenceParticipantSaveResult capture = participant.CapturePayload();
            ProfessionEntryRuntime restoredRuntime = new ProfessionEntryRuntime();
            ProfessionEntryPersistenceParticipant restoreParticipant = new ProfessionEntryPersistenceParticipant(restoredRuntime, () => fixture.Registry, () => fixture.Professions, () => new[] { PersonId });
            PersistenceParticipantPrepareResult prepare = restoreParticipant.PreparePayload(capture.PayloadJson, ProfessionEntryPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoreParticipant.CommitPreparedPayload(prepare.PreparedPayload);

            Assert.That(capture.Succeeded, Is.True, capture.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(restoredRuntime.TryGetRequest("profession-entry-request.participant", out ProfessionEntryRequestSnapshot request), Is.True);
            Assert.That(request.State, Is.EqualTo(ProfessionEntryRequestState.Submitted));
        }

        private static RuntimeFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            PersonProfessionRuntime professions = new PersonProfessionRuntime();
            professions.Configure(registry, new[] { PersonId });
            ProfessionEntryRuntime entry = new ProfessionEntryRuntime();
            entry.Configure(registry, professions, new[] { PersonId });
            return new RuntimeFixture(registry, professions, entry);
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog.CreateRegistry();
        }

        private static ProfessionEligibilityContext BlacksmithContext(bool preview)
        {
            return new ProfessionEligibilityContext(
                PersonId,
                ProfessionContentIds.BlacksmithProfessionId,
                ProfessionContentIds.BlacksmithSelfDeclaredEntryPathId,
                selfDeclared: true,
                worldTime: 1d,
                correlationId: "edit.blacksmith",
                preview: preview);
        }

        private static ProfessionEligibilityContext MedicContext(bool preview)
        {
            return new ProfessionEligibilityContext(
                PersonId,
                ProfessionContentIds.FieldMedicProfessionId,
                ProfessionContentIds.FieldMedicRecognitionEntryPathId,
                formal: true,
                authorityId: "authority.medical.prototype",
                worldTime: 2d,
                correlationId: "edit.medic",
                preview: preview,
                skills: new[] { Skill("skill.healing-magic", 1) },
                knowledgeSubjects: new[] { "knowledge.subject.first-aid" });
        }

        private static ProfessionEntrySkillStateData Skill(string skillId, int grade)
        {
            return new ProfessionEntrySkillStateData { skillId = skillId, grade = grade };
        }

        private static InformationAccessDecision RedactedDecision()
        {
            return new InformationAccessDecision(
                "person.observer",
                ProfessionEntryInformationSubject.Request("profession-entry-request.persist", PersonId, ProfessionContentIds.FieldMedicProfessionId),
                InformationAccessMode.Inspect,
                InformationAccessDecisionKind.RedactedAccess,
                InformationAccessDenialCode.DetailRestriction,
                false,
                InformationResharingPolicy.NoResharing,
                new[] { "profession-id", "entry-path-id", "state" },
                ProfessionEntryInformationSubject.ProtectedFields,
                Array.Empty<string>(),
                new[] { ProfessionContentIds.AccessPublicId },
                2d,
                "Redacted profession entry request.",
                "Entry request hides applicant and authority details.",
                true);
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(DefinitionRegistry registry, PersonProfessionRuntime professions, ProfessionEntryRuntime entry)
            {
                Registry = registry;
                Professions = professions;
                Entry = entry;
            }

            public DefinitionRegistry Registry { get; }
            public PersonProfessionRuntime Professions { get; }
            public ProfessionEntryRuntime Entry { get; }
            public long KnowledgeRevision => 0L;
        }
    }
}
