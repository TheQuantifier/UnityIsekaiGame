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
    public sealed class ProfessionIdentityDefinitionTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string PersonId = "person.profession.tests";

        [Test]
        public void PrototypeProfessionDefinitionsValidateAndRemainNonGranting()
        {
            DefinitionRegistry registry = CreateRegistry();
            DefinitionValidationReport report = ValidatePrototypeProfessionDefinitions(registry);

            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
            Assert.That(registry.TryGet(ProfessionContentIds.BlacksmithProfessionId, out ProfessionDefinition blacksmith), Is.True);
            Assert.That(registry.TryGet(ProfessionContentIds.WeaponsmithSpecializationId, out ProfessionSpecializationDefinition weaponsmith), Is.True);
            Assert.That(blacksmith.Category, Is.EqualTo(ProfessionCategory.Craft));
            Assert.That(weaponsmith.ParentProfessionId, Is.EqualTo(blacksmith.Id));
            Assert.That(blacksmith.RelatedSkillIds, Does.Contain("skill.smithing"));
            Assert.That(blacksmith.RelatedCapabilityIds, Is.Empty);
        }

        [Test]
        public void RelationshipRulesDefaultPrimaryRejectDuplicatesAndValidateRecognition()
        {
            PersonProfessionRuntime runtime = CreateRuntime();

            ProfessionOperationResult blacksmith = runtime.AddRelationship(Request("profession-relationship.blacksmith", ProfessionContentIds.BlacksmithProfessionId));
            ProfessionOperationResult duplicateActive = runtime.AddRelationship(Request("profession-relationship.blacksmith.duplicate", ProfessionContentIds.BlacksmithProfessionId));
            ProfessionOperationResult idempotent = runtime.AddRelationship(Request("profession-relationship.blacksmith", ProfessionContentIds.BlacksmithProfessionId));
            ProfessionOperationResult conflictingSameId = runtime.AddRelationship(Request("profession-relationship.blacksmith", ProfessionContentIds.FieldMedicProfessionId));
            ProfessionOperationResult medic = runtime.AddRelationship(Request("profession-relationship.medic", ProfessionContentIds.FieldMedicProfessionId));
            ProfessionOperationResult missingAuthority = runtime.Recognize("profession-relationship.medic", string.Empty);
            ProfessionOperationResult invalidAuthority = runtime.Recognize("profession-relationship.medic", "authority.guild.prototype");
            ProfessionOperationResult recognized = runtime.Recognize("profession-relationship.medic", "authority.medical.prototype", "credential.profession.medic");
            ProfessionOperationResult primary = runtime.SetPrimary("profession-relationship.medic");

            Assert.That(blacksmith.Succeeded, Is.True, blacksmith.Message);
            Assert.That(blacksmith.Snapshot.Primary, Is.True);
            Assert.That(duplicateActive.Succeeded, Is.False);
            Assert.That(duplicateActive.Status, Is.EqualTo(ProfessionOperationStatus.DuplicateActiveRelationship));
            Assert.That(idempotent.Succeeded, Is.True);
            Assert.That(idempotent.Duplicate, Is.True);
            Assert.That(conflictingSameId.Succeeded, Is.False);
            Assert.That(conflictingSameId.Status, Is.EqualTo(ProfessionOperationStatus.DuplicateRelationshipId));
            Assert.That(medic.Succeeded, Is.True, medic.Message);
            Assert.That(missingAuthority.Status, Is.EqualTo(ProfessionOperationStatus.MissingRecognitionAuthority));
            Assert.That(invalidAuthority.Status, Is.EqualTo(ProfessionOperationStatus.ValidationFailed));
            Assert.That(recognized.Succeeded, Is.True, recognized.Message);
            Assert.That(primary.Succeeded, Is.True, primary.Message);
            Assert.That(runtime.QueryPrimary(PersonId).Select(snapshot => snapshot.ProfessionId), Is.EqualTo(new[] { ProfessionContentIds.FieldMedicProfessionId }));
        }

        [Test]
        public void SecretRelationshipProjectionRedactsOrdinaryInspectionWithoutMutatingAuthoritativeState()
        {
            PersonProfessionRuntime runtime = CreateRuntime();
            ProfessionOperationResult created = runtime.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.spy",
                personId = PersonId,
                professionId = ProfessionContentIds.SpyProfessionId,
                state = ProfessionRelationshipState.Secret,
                informalPractice = true,
                active = true,
                accessPolicyId = ProfessionContentIds.AccessSecretId,
                tags = new[] { "profession.secret" }
            });
            long revision = runtime.Revision;

            PersonProfessionProjection projection = runtime.Project("profession-relationship.spy", ProfessionProjectionAudience.PublicInspection, RedactedDecision());
            PersonProfessionProjection internalProjection = runtime.Project("profession-relationship.spy", ProfessionProjectionAudience.AuthoritativeInternal);

            Assert.That(created.Succeeded, Is.True, created.Message);
            Assert.That(projection.Redacted, Is.True);
            Assert.That(projection.Denied, Is.False);
            Assert.That(projection.Snapshot.RelationshipId, Is.Empty);
            Assert.That(projection.Snapshot.PersonId, Is.Empty);
            Assert.That(projection.Snapshot.ProfessionId, Is.EqualTo(ProfessionContentIds.SpyProfessionId));
            Assert.That(internalProjection.Snapshot.RelationshipId, Is.EqualTo("profession-relationship.spy"));
            Assert.That(runtime.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void PersistenceRoundTripClearsTransientHooksAndRejectsCorruptRestoreWithoutMutation()
        {
            DefinitionRegistry registry = CreateRegistry();
            PersonProfessionRuntime runtime = CreateRuntime(registry);
            runtime.AddRelationship(Request("profession-relationship.persist", ProfessionContentIds.BlacksmithProfessionId, ProfessionContentIds.WeaponsmithSpecializationId));
            PersonProfessionRuntimeSaveData save = runtime.CreateSaveData();

            PersonProfessionRuntime restored = CreateRuntime(registry);
            restored.AddRelationship(Request("profession-relationship.temporary", ProfessionContentIds.FieldMedicProfessionId));
            Assert.That(restored.HistoryHooks.Count, Is.GreaterThan(0));
            ProfessionOperationResult restore = restored.RestoreFromSaveData(save, registry, new[] { PersonId }, restoring: true);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.HistoryHooks.Count, Is.Zero);
            Assert.That(restored.Count, Is.EqualTo(1));

            PersonProfessionRuntimeSaveData corrupt = save.Clone();
            corrupt.relationships[0].professionId = "profession.missing";
            ProfessionOperationResult rejected = restored.RestoreFromSaveData(corrupt, registry, new[] { PersonId }, restoring: true);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(restored.QueryByPerson(PersonId).Single().ProfessionId, Is.EqualTo(ProfessionContentIds.BlacksmithProfessionId));
        }

        [Test]
        public void PersistenceParticipantCapturesAndCommitsProfessionIdentityProjection()
        {
            DefinitionRegistry registry = CreateRegistry();
            PersonProfessionRuntime runtime = CreateRuntime(registry);
            runtime.AddRelationship(Request("profession-relationship.participant", ProfessionContentIds.BlacksmithProfessionId));
            PersonProfessionPersistenceParticipant participant = new PersonProfessionPersistenceParticipant(runtime, () => registry, () => new[] { PersonId });

            PersistenceParticipantSaveResult capture = participant.CapturePayload();
            Assert.That(capture.Succeeded, Is.True, capture.Message);

            PersonProfessionRuntime target = CreateRuntime(registry);
            PersonProfessionPersistenceParticipant restoreParticipant = new PersonProfessionPersistenceParticipant(target, () => registry, () => new[] { PersonId });
            PersistenceParticipantPrepareResult prepare = restoreParticipant.PreparePayload(capture.PayloadJson, PersonProfessionPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = restoreParticipant.CommitPreparedPayload(prepare.PreparedPayload);

            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(target.QueryByPerson(PersonId).Single().RelationshipId, Is.EqualTo("profession-relationship.participant"));
        }

        private static AddProfessionRelationshipRequest Request(string relationshipId, string professionId, string specializationId = "")
        {
            return new AddProfessionRelationshipRequest
            {
                relationshipId = relationshipId,
                personId = PersonId,
                professionId = professionId,
                informalPractice = true,
                specializationIds = string.IsNullOrWhiteSpace(specializationId) ? Array.Empty<string>() : new[] { specializationId },
                startWorldTime = "100"
            };
        }

        private static PersonProfessionRuntime CreateRuntime(DefinitionRegistry registry = null)
        {
            PersonProfessionRuntime runtime = new PersonProfessionRuntime();
            runtime.Configure(registry ?? CreateRegistry(), new[] { PersonId });
            return runtime;
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            DefinitionValidationReport report = new DefinitionValidationReport();
            DefinitionRegistry registry = new DefinitionRegistry(catalog.CreateRegistry().DefinitionsById.Values, report);
            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            return registry;
        }

        private static DefinitionValidationReport ValidatePrototypeProfessionDefinitions(DefinitionRegistry registry)
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in ProfessionContentIds.GetDefinitions(registry))
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(registry.DefinitionsById, report);
                }
            }

            return report;
        }

        private static InformationAccessDecision RedactedDecision()
        {
            return new InformationAccessDecision(
                "person.observer",
                ProfessionInformationSubject.Relationship("profession-relationship.spy", PersonId, ProfessionContentIds.SpyProfessionId, new[] { "profession.secret" }),
                InformationAccessMode.Inspect,
                InformationAccessDecisionKind.RedactedAccess,
                InformationAccessDenialCode.DetailRestriction,
                false,
                InformationResharingPolicy.NoResharing,
                new[] { "profession-id", "state" },
                ProfessionInformationSubject.ProtectedFields,
                Array.Empty<string>(),
                new[] { ProfessionContentIds.AccessSecretId },
                10d,
                "Redacted profession access.",
                "Secret profession relationship hides identity details.",
                true);
        }
    }
}
