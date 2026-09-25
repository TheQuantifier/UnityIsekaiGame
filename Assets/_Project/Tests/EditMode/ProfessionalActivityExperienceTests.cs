using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Disassembly;
using UnityIsekaiGame.Inventory.Experimentation;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Tests
{
    public sealed class ProfessionalActivityExperienceTests
    {
        private const string PersonId = "person.professional-activity.test";
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeProfessionalActivityDefinitionsValidate()
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
            Assert.That(registry.TryGet(ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, out ProfessionalActivityDefinition crafting), Is.True);
            Assert.That(crafting.AcceptedSourceTypes, Does.Contain(ProfessionalActivitySourceType.CraftingOperation));
        }

        [Test]
        public void SourceAdaptersProduceExactProfessionalActivitySnapshots()
        {
            CraftingOperationRecordData crafting = new CraftingOperationRecordData
            {
                operationId = "crafting.professional.test",
                recipeId = "recipe.prototype-iron-ingot",
                actorPersonId = PersonId,
                worldTime = "10",
                state = CraftingOperationState.Completed,
                status = CraftingExecutionStatus.Succeeded,
                outputs = { new CraftingOutputItemData { itemInstanceId = "item.instance.ingot", quantity = 2f } }
            };
            ProductionJobData production = new ProductionJobData
            {
                jobId = "production.professional.test",
                workOrderId = "work-order.professional.test",
                recipeDefinitionId = "recipe.prototype-sword",
                completionWorldTime = "12",
                state = ProductionJobState.Completed,
                completedStageIds = new[] { "stage.forge" },
                outputItemIds = new[] { "item.instance.sword" }
            };
            ItemRepairRecordData repair = new ItemRepairRecordData
            {
                repairId = "repair.professional.test",
                itemInstanceId = "item.instance.sword",
                actorPersonId = PersonId,
                recoveredDurability = 25f,
                repairQuality = ItemRepairQuality.Good,
                worldTime = "13"
            };
            DisassemblyOperationRecordData recovery = new DisassemblyOperationRecordData
            {
                operationId = "item-recovery.professional.test",
                itemInstanceId = "item.instance.scrap-source",
                itemDefinitionId = "item.prototype-sword",
                actorPersonId = PersonId,
                worldTime = "14",
                operationKind = ItemRecoveryOperationKind.Salvage,
                state = DisassemblyOperationState.Completed,
                actualEfficiency = 0.65f,
                efficiencyTier = DisassemblyEfficiencyTier.Skilled,
                outcomes = { new DisassemblyComponentOutcomeData { outcomeId = "recovery.output.iron", returnedQuantity = 1, outputItemInstanceIds = new[] { "item.instance.iron" } } }
            };
            ExperimentTrialData trial = new ExperimentTrialData
            {
                trialId = "experiment.professional.test",
                experimentRunId = "experiment.run.professional.test",
                workerIds = new[] { PersonId },
                completionWorldTime = "15",
                recipeDefinitionId = "recipe.prototype-alloy",
                outcome = ExperimentTrialOutcome.UnexpectedSuccess
            };

            ProfessionalActivitySourceSnapshot craftingSnapshot = ProfessionalActivitySourceAdapters.FromCraftingOperation(crafting);
            ProfessionalActivitySourceSnapshot productionSnapshot = ProfessionalActivitySourceAdapters.FromProductionJob(production, new ProductionWorkerAssignmentData { personId = PersonId, role = ProductionWorkerRole.PrimaryCrafter });
            ProfessionalActivitySourceSnapshot repairSnapshot = ProfessionalActivitySourceAdapters.FromRepairRecord(repair);
            ProfessionalActivitySourceSnapshot salvageSnapshot = ProfessionalActivitySourceAdapters.FromItemRecoveryOperation(recovery);
            ProfessionalActivitySourceSnapshot trialSnapshot = ProfessionalActivitySourceAdapters.FromExperimentTrial(trial);

            Assert.That(craftingSnapshot.Reference.sourceType, Is.EqualTo(ProfessionalActivitySourceType.CraftingOperation));
            Assert.That(craftingSnapshot.ActingPersonId, Is.EqualTo(PersonId));
            Assert.That(craftingSnapshot.Completed, Is.True);
            Assert.That(productionSnapshot.RelatedSubjectIds, Does.Contain("item.instance.sword"));
            Assert.That(repairSnapshot.Quality, Is.EqualTo(700));
            Assert.That(salvageSnapshot.Completed, Is.True);
            Assert.That(salvageSnapshot.ActingPersonId, Is.EqualTo(PersonId));
            Assert.That(trialSnapshot.Difficulty, Is.EqualTo(ProfessionalActivityDifficulty.Advanced));
        }

        [Test]
        public void ActivityValidationCreatesExperienceWithoutMutatingProfessionOrTraining()
        {
            Fixture fixture = CreateFixture();
            long professionRevision = fixture.Professions.Revision;
            TrainingRuntime training = new TrainingRuntime();
            training.Configure(fixture.Registry, fixture.Professions, null, fixture.KnownPersons);
            long trainingRevision = training.Revision;

            ProfessionalActivityOperationResult result = fixture.Activities.RegisterAndValidateActivity(
                Request("craft.activity.validation", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, CraftingSource("craft.source.validation", PersonId, "20")),
                "evidence.craft.activity.validation",
                "authority.guild.prototype",
                "tx.craft.activity.validation");

            ProfessionalExperienceSummary summary = fixture.Activities.BuildExperienceSummary(PersonId, ProfessionContentIds.BlacksmithProfessionId);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Evidence, Is.Not.Null);
            Assert.That(summary.TotalValidatedActivities, Is.EqualTo(1));
            Assert.That(summary.SuccessfulCount, Is.EqualTo(1));
            Assert.That(fixture.Professions.Revision, Is.EqualTo(professionRevision));
            Assert.That(training.Revision, Is.EqualTo(trainingRevision));
        }

        [Test]
        public void ActivityValidationRequiresExistingProfessionRelationship()
        {
            DefinitionRegistry registry = Registry();
            string[] knownPersons = { PersonId };
            PersonProfessionRuntime professions = new PersonProfessionRuntime();
            professions.Configure(registry, knownPersons);
            ProfessionalActivityRuntime activities = new ProfessionalActivityRuntime();
            activities.Configure(registry, professions, knownPersons);

            ProfessionalActivityValidationResult result = activities.EvaluateActivity(
                Request("activity.no-profession", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, CraftingSource("craft.source.no-profession", PersonId, "25")));

            Assert.That(result.Valid, Is.False);
            Assert.That(result.Status, Is.EqualTo(ProfessionalActivityOperationStatus.MissingProfessionRelationship));
            Assert.That(activities.ActivityCount, Is.Zero);
            Assert.That(activities.EvidenceCount, Is.Zero);
        }

        [Test]
        public void DuplicateExclusiveSourcesAreRejectedButSharedCreditAllowsRoles()
        {
            Fixture fixture = CreateFixture();
            ProfessionalActivitySourceSnapshot exclusiveSource = CraftingSource("craft.source.exclusive", PersonId, "30");
            ProfessionalActivityOperationResult first = fixture.Activities.RegisterAndValidateActivity(
                Request("activity.exclusive.first", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, exclusiveSource),
                "evidence.exclusive.first",
                "authority.guild.prototype",
                "tx.exclusive.first");
            ProfessionalActivityOperationResult duplicate = fixture.Activities.RegisterAndValidateActivity(
                Request("activity.exclusive.second", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, exclusiveSource),
                "evidence.exclusive.second",
                "authority.guild.prototype",
                "tx.exclusive.second");

            ProfessionalActivitySourceSnapshot sharedSource = ProfessionalActivitySourceAdapters.FromCustom(
                ProfessionalActivitySourceType.TeachingSession,
                "teaching.source.shared",
                PersonId,
                ProfessionalActivityOutcomeState.Successful,
                quality: 800,
                difficulty: ProfessionalActivityDifficulty.Skilled,
                worldTime: "31",
                tags: "training.activity.teaching");
            ProfessionalActivityOperationResult instructor = fixture.Activities.RegisterAndValidateActivity(
                Request("activity.shared.instructor", ProfessionContentIds.BlacksmithTeachingActivityDefinitionId, sharedSource, ProfessionalResponsibilityLevel.Instructor, TrainingSupervisionLevel.IndependentWithReview),
                "evidence.shared.instructor",
                "authority.guild.prototype",
                "tx.shared.instructor");
            ProfessionalActivityOperationResult assistant = fixture.Activities.RegisterAndValidateActivity(
                Request("activity.shared.assistant", ProfessionContentIds.BlacksmithTeachingActivityDefinitionId, sharedSource, ProfessionalResponsibilityLevel.Assistant, TrainingSupervisionLevel.ObservationOnly),
                "evidence.shared.assistant",
                "authority.guild.prototype",
                "tx.shared.assistant");

            ProfessionalExperienceSummary summary = fixture.Activities.BuildExperienceSummary(PersonId, ProfessionContentIds.BlacksmithProfessionId);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Status, Is.EqualTo(ProfessionalActivityOperationStatus.DuplicateExclusiveSource));
            Assert.That(instructor.Succeeded, Is.True, instructor.Message);
            Assert.That(assistant.Succeeded, Is.True, assistant.Message);
            Assert.That(summary.TotalValidatedActivities, Is.EqualTo(3));
            Assert.That(summary.TeachingCount, Is.EqualTo(1));
        }

        [Test]
        public void SummariesRequirementsAccessAndSnapshotsAreDeterministic()
        {
            Fixture fixture = CreateFixture();
            fixture.Activities.RegisterAndValidateActivity(
                Request("activity.summary.independent", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, CraftingSource("craft.source.summary", PersonId, "40"), ProfessionalResponsibilityLevel.IndependentPractitioner),
                "evidence.summary.independent",
                "authority.guild.prototype",
                "tx.summary.independent");
            fixture.Activities.RegisterAndValidateActivity(
                Request("activity.summary.practice", ProfessionContentIds.BlacksmithSupervisedPracticeActivityDefinitionId, PracticalSource("training.source.practice", PersonId, "41"), ProfessionalResponsibilityLevel.SupervisedWorker, TrainingSupervisionLevel.CloselySupervised),
                "evidence.summary.practice",
                "authority.guild.prototype",
                "tx.summary.practice");

            ProfessionalExperienceSummary before = fixture.Activities.BuildExperienceSummary(PersonId, ProfessionContentIds.BlacksmithProfessionId);
            bool requirement = fixture.Activities.EvaluateExperienceRequirement(PersonId, new ProfessionalExperienceRequirementData
            {
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                minimumValidatedActivities = 2,
                minimumIndependentActivities = 1,
                minimumSupervisedActivities = 1,
                minimumQuality = 600,
                minimumDifficulty = ProfessionalActivityDifficulty.Routine,
                requireRecentActivity = true
            }, out ProfessionalExperienceSummary evaluated);
            ProfessionalActivityRecordData immutable = fixture.Activities.Activities.First();
            immutable.personId = "mutated";
            fixture.Activities.TryGetActivity("activity.summary.independent", out ProfessionalActivityRecordData afterMutationAttempt);
            InformationAccessDecision redactedDecision = new InformationAccessDecision(
                "person.observer",
                ProfessionalActivityInformationSubject.Create(ProfessionalActivityInformationSubject.ActivityTag, "activity.summary.independent", PersonId),
                InformationAccessMode.Inspect,
                InformationAccessDecisionKind.RedactedAccess,
                InformationAccessDenialCode.DetailRestriction,
                false,
                InformationResharingPolicy.NoResharing,
                new[] { "profession-id", "state" },
                ProfessionalActivityInformationSubject.ProtectedFields,
                Array.Empty<string>(),
                new[] { ProfessionContentIds.AccessPublicId },
                42d,
                "Redacted professional activity.",
                "Professional source details hidden.",
                true);
            ProfessionalActivityProjection<ProfessionalActivityRecordData> projection = fixture.Activities.ProjectActivity("activity.summary.independent", ProfessionalActivityProjectionAudience.PublicInspection, redactedDecision);

            Assert.That(requirement, Is.True);
            Assert.That(before.TotalValidatedActivities, Is.EqualTo(evaluated.TotalValidatedActivities));
            Assert.That(before.BreadthScore, Is.EqualTo(evaluated.BreadthScore));
            Assert.That(afterMutationAttempt.personId, Is.EqualTo(PersonId));
            Assert.That(projection.Redacted, Is.True);
            Assert.That(projection.Record.personId, Is.Empty);
            Assert.That(fixture.Activities.HistoryHooks.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void PersistenceRestoreIsAtomicAndDoesNotReplayHistoryHooks()
        {
            Fixture fixture = CreateFixture();
            ProfessionalActivityOperationResult recorded = fixture.Activities.RegisterAndValidateActivity(
                Request("activity.persist", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, CraftingSource("craft.source.persist", PersonId, "50")),
                "evidence.persist",
                "authority.guild.prototype",
                "tx.persist");
            ProfessionalActivityRuntimeSaveData save = fixture.Activities.CreateSaveData();
            ProfessionalActivityRuntime restored = new ProfessionalActivityRuntime();
            ProfessionalActivityOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry, fixture.Professions, fixture.KnownPersons, restoring: true);
            ProfessionalActivityRuntimeSaveData corrupt = save.Clone();
            corrupt.activities[0].professionId = "profession.missing";
            ProfessionalActivityOperationResult rejected = restored.RestoreFromSaveData(corrupt, fixture.Registry, fixture.Professions, fixture.KnownPersons, restoring: true);
            PersonProfessionRuntime emptyProfessions = new PersonProfessionRuntime();
            emptyProfessions.Configure(fixture.Registry, fixture.KnownPersons);
            bool missingRelationshipValid = ProfessionalActivityRuntime.ValidateSaveData(save, fixture.Registry, emptyProfessions, fixture.KnownPersons, out string missingRelationshipFailure);

            Assert.That(recorded.Succeeded, Is.True, recorded.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.EvidenceCount, Is.EqualTo(1));
            Assert.That(restored.HistoryHooks.Count, Is.Zero);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(missingRelationshipValid, Is.False);
            Assert.That(missingRelationshipFailure, Does.Contain("relationship"));
            Assert.That(restored.EvidenceCount, Is.EqualTo(1));
            Assert.That(restored.BuildExperienceSummary(PersonId, ProfessionContentIds.BlacksmithProfessionId).TotalValidatedActivities, Is.EqualTo(1));
        }

        private static Fixture CreateFixture()
        {
            DefinitionRegistry registry = Registry();
            PersonProfessionRuntime professions = new PersonProfessionRuntime();
            string[] knownPersons = { PersonId, "person.observer" };
            professions.Configure(registry, knownPersons);
            ProfessionOperationResult relationship = professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.professional-activity.blacksmith",
                personId = PersonId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                specializationIds = new[] { ProfessionContentIds.WeaponsmithSpecializationId },
                informalPractice = true,
                selfDeclared = true,
                active = true,
                startWorldTime = "1",
                transactionId = "tx.professional-activity.profession"
            });
            Assert.That(relationship.Succeeded, Is.True, relationship.Message);
            ProfessionalActivityRuntime activities = new ProfessionalActivityRuntime();
            activities.Configure(registry, professions, knownPersons);
            return new Fixture(registry, professions, activities, knownPersons);
        }

        private static DefinitionRegistry Registry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog.CreateRegistry();
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

        private static ProfessionalActivitySourceSnapshot CraftingSource(string sourceId, string personId, string worldTime)
        {
            return ProfessionalActivitySourceAdapters.FromCustom(
                ProfessionalActivitySourceType.CraftingOperation,
                sourceId,
                personId,
                ProfessionalActivityOutcomeState.Successful,
                quality: 750,
                difficulty: ProfessionalActivityDifficulty.Routine,
                worldTime: worldTime,
                tags: "source.crafting");
        }

        private static ProfessionalActivitySourceSnapshot PracticalSource(string sourceId, string personId, string worldTime)
        {
            return ProfessionalActivitySourceAdapters.FromCustom(
                ProfessionalActivitySourceType.TrainingPracticalAssignment,
                sourceId,
                personId,
                ProfessionalActivityOutcomeState.Successful,
                quality: 700,
                difficulty: ProfessionalActivityDifficulty.Routine,
                worldTime: worldTime,
                tags: "training.activity.practical");
        }

        private sealed class Fixture
        {
            public Fixture(DefinitionRegistry registry, PersonProfessionRuntime professions, ProfessionalActivityRuntime activities, string[] knownPersons)
            {
                Registry = registry;
                Professions = professions;
                Activities = activities;
                KnownPersons = knownPersons;
            }

            public DefinitionRegistry Registry { get; }
            public PersonProfessionRuntime Professions { get; }
            public ProfessionalActivityRuntime Activities { get; }
            public string[] KnownPersons { get; }
        }
    }
}
