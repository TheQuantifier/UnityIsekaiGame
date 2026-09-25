using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Development.Automation;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Tests
{
    public sealed class LifePathsAspirationsProfessionalIdentityTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string PersonId = "person.life-path.prototype";
        private const string OtherPersonId = "person.life-path.other";
        private const string GuildAuthority = "authority.guild.prototype";
        private const string GuildOrganization = "organization.prototype.guild";

        [Test]
        public void PrototypeLifePathDefinitionsValidateAndInvalidReferencesAreRejected()
        {
            DefinitionRegistry registry = Registry();
            DefinitionValidationReport report = ValidateRegistry(registry);
            AspirationDefinition badAspiration = ScriptableObject.CreateInstance<AspirationDefinition>();
            badAspiration.DevelopmentConfigure("aspiration.test.bad", "Bad Aspiration", AspirationCategory.EnterProfession, LifePathTargetSubjectType.Profession, professions: new[] { "profession.missing" });
            LifeGoalDefinition badGoal = ScriptableObject.CreateInstance<LifeGoalDefinition>();
            badGoal.DevelopmentConfigure("goal.test.bad", "Bad Goal", LifeGoalCategory.EnterProfession, LifePathTargetSubjectType.Profession, professions: new[] { "profession.missing" }, dependencies: new[] { "goal.missing" });
            DefinitionValidationReport badReport = new DefinitionValidationReport();
            badAspiration.ValidateCatalogDefinition(registry.DefinitionsById, badReport);
            badGoal.ValidateCatalogDefinition(registry.DefinitionsById, badReport);

            Assert.That(report.ErrorCount, Is.Zero, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());
            Assert.That(registry.TryGet(ProfessionContentIds.AspirationReachMasterWeaponsmithId, out AspirationDefinition aspiration), Is.True);
            Assert.That(aspiration.SuggestedGoalDefinitionIds, Does.Contain(ProfessionContentIds.GoalReachMasterRankId));
            Assert.That(registry.TryGet(ProfessionContentIds.GoalCompleteBlacksmithApprenticeshipId, out LifeGoalDefinition trainingGoal), Is.True);
            Assert.That(trainingGoal.RequiredTrainingProgramIds, Does.Contain(ProfessionContentIds.BlacksmithApprenticeshipProgramId));
            Assert.That(badReport.ErrorCount, Is.GreaterThanOrEqualTo(2), badReport.GetSummary());
        }

        [Test]
        public void AspirationsGoalsLifecycleDependenciesAlternativesAndConflictsAreDeterministic()
        {
            using TestLabRuntimeBundle bundle = Bundle();
            AddLifePath(bundle);
            LifePathOperationResult aspiration = bundle.LifePaths.AddAspiration(Aspiration("aspiration.life.blacksmith", ProfessionContentIds.AspirationEnterBlacksmithProfessionId, targetProfession: ProfessionContentIds.BlacksmithProfessionId), "tx.life.aspiration");
            LifePathOperationResult duplicateConflict = bundle.LifePaths.AddAspiration(Aspiration("aspiration.life.blacksmith.second", ProfessionContentIds.AspirationEnterBlacksmithProfessionId, targetProfession: ProfessionContentIds.BlacksmithProfessionId), "tx.life.aspiration.conflict");
            LifePathOperationResult enter = bundle.LifePaths.AddGoal(Goal("goal.life.enter", ProfessionContentIds.GoalEnterBlacksmithProfessionId, aspiration.Aspiration.aspirationId, targetProfession: ProfessionContentIds.BlacksmithProfessionId), "tx.life.goal.enter");
            LifePathOperationResult training = bundle.LifePaths.AddGoal(Goal("goal.life.training", ProfessionContentIds.GoalCompleteBlacksmithApprenticeshipId, aspiration.Aspiration.aspirationId, targetTraining: ProfessionContentIds.BlacksmithApprenticeshipProgramId, dependencies: new[] { enter.Goal.goalId }), "tx.life.goal.training");
            LifePathOperationResult impossible = bundle.LifePaths.AddGoal(Goal("goal.life.impossible", ProfessionContentIds.GoalReachMasterRankId, aspiration.Aspiration.aspirationId, targetRank: ProfessionContentIds.BlacksmithRankMasterId, dependencies: new[] { "goal.life.missing" }), "tx.life.goal.bad");
            LifePathSnapshot before = bundle.LifePaths.BuildSnapshot(PersonId).Snapshot;
            LifePathOperationResult pause = bundle.LifePaths.SetAspirationState(aspiration.Aspiration.aspirationId, PersonAspirationState.Paused, "2", "Waiting on apprenticeship.", "tx.life.pause");
            LifePathOperationResult resume = bundle.LifePaths.SetAspirationState(aspiration.Aspiration.aspirationId, PersonAspirationState.Active, "3", "Resumed.", "tx.life.resume");
            LifePathOperationResult abandon = bundle.LifePaths.SetGoalState(training.Goal.goalId, PersonGoalState.Abandoned, "4", "Picked another route.", "tx.life.abandon");
            LifePathOperationResult revive = bundle.LifePaths.SetGoalState(training.Goal.goalId, PersonGoalState.Active, "5", "Invalid return.", "tx.life.revive");

            Assert.That(aspiration.Succeeded, Is.True, aspiration.Message);
            Assert.That(duplicateConflict.Succeeded, Is.False);
            Assert.That(duplicateConflict.Status, Is.EqualTo(LifePathOperationStatus.Conflict));
            Assert.That(enter.Succeeded, Is.True, enter.Message);
            Assert.That(training.Succeeded, Is.True, training.Message);
            Assert.That(impossible.Succeeded, Is.False);
            Assert.That(impossible.Status, Is.EqualTo(LifePathOperationStatus.MissingGoal));
            Assert.That(pause.Succeeded, Is.True, pause.Message);
            Assert.That(resume.Succeeded, Is.True, resume.Message);
            Assert.That(abandon.Succeeded, Is.True, abandon.Message);
            Assert.That(revive.Succeeded, Is.False);
            Assert.That(before.Aspirations.Single().state, Is.EqualTo(PersonAspirationState.Active));
            Assert.That(bundle.LifePaths.QueryGoalsByPerson(PersonId).Select(item => item.goalId).ToArray(), Is.Ordered);
        }

        [Test]
        public void GoalProgressUsesAuthoritativeSourcesAndRejectsStaleCompletion()
        {
            using TestLabRuntimeBundle bundle = Bundle();
            AddLifePath(bundle);
            AddProfession(bundle);
            SeedTraining(bundle, "training.life.complete", ProfessionContentIds.BlacksmithApprenticeshipProgramId);
            SeedCredential(bundle, "credential.life.guild", ProfessionContentIds.BlacksmithGuildLicenseCredentialId);
            SeedRank(bundle, "rank.life.journeyman", ProfessionContentIds.BlacksmithRankJourneymanId);
            SeedEmployment(bundle, "position.life.guild-clerk", "employment.life.guild-clerk", ProfessionContentIds.GuildClerkPositionId);
            SeedActivity(bundle, "activity.life.crafting", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, ProfessionalActivitySourceType.CraftingOperation);
            SeedActivity(bundle, "activity.life.discovery", ProfessionContentIds.BlacksmithExperimentationActivityDefinitionId, ProfessionalActivitySourceType.DiscoveryClaim);

            PersonGoalData professionGoal = AddGoal(bundle, "goal.life.progress.profession", ProfessionContentIds.GoalEnterBlacksmithProfessionId, targetProfession: ProfessionContentIds.BlacksmithProfessionId);
            PersonGoalData trainingGoal = AddGoal(bundle, "goal.life.progress.training", ProfessionContentIds.GoalCompleteBlacksmithApprenticeshipId, targetTraining: ProfessionContentIds.BlacksmithApprenticeshipProgramId);
            PersonGoalData credentialGoal = AddGoal(bundle, "goal.life.progress.credential", ProfessionContentIds.GoalEarnBlacksmithGuildLicenseId, targetCredential: ProfessionContentIds.BlacksmithGuildLicenseCredentialId);
            PersonGoalData rankGoal = AddGoal(bundle, "goal.life.progress.rank", ProfessionContentIds.GoalReachJourneymanRankId, targetRank: ProfessionContentIds.BlacksmithRankJourneymanId);
            PersonGoalData positionGoal = AddGoal(bundle, "goal.life.progress.position", ProfessionContentIds.GoalObtainGuildClerkPositionId, targetPosition: ProfessionContentIds.GuildClerkPositionId);
            PersonGoalData craftingGoal = AddGoal(bundle, "goal.life.progress.crafting", ProfessionContentIds.GoalProduceMasterworkId, targetActivity: ProfessionContentIds.BlacksmithCraftingActivityDefinitionId);
            PersonGoalData discoveryGoal = AddGoal(bundle, "goal.life.progress.discovery", ProfessionContentIds.GoalConfirmDiscoveryId, targetActivity: ProfessionContentIds.BlacksmithExperimentationActivityDefinitionId);
            LifePathOperationResult perceivedCreate = bundle.LifePaths.AddGoal(Goal("goal.life.perceived", ProfessionContentIds.GoalReachMasterRankId, targetRank: ProfessionContentIds.BlacksmithRankMasterId, progress: LifeGoalProgressState.Satisfied), "tx.life.perceived");
            Assert.That(perceivedCreate.Succeeded, Is.True, perceivedCreate.Message);
            PersonGoalData perceivedGoal = perceivedCreate.Goal;
            LifePathOperationResult perceivedOnly = bundle.LifePaths.EvaluateGoalProgress(perceivedGoal.goalId);

            LifePathOperationResult staleToken = bundle.LifePaths.EvaluateGoalProgress(professionGoal.goalId);
            AddProfession(bundle, "second", ProfessionContentIds.FieldMedicProfessionId);
            LifePathOperationResult staleComplete = bundle.LifePaths.CompleteGoal(professionGoal.goalId, staleToken.Progress, "9", "tx.life.stale");
            LifePathOperationResult currentToken = bundle.LifePaths.EvaluateGoalProgress(professionGoal.goalId);
            LifePathOperationResult complete = bundle.LifePaths.CompleteGoal(professionGoal.goalId, currentToken.Progress, "10", "tx.life.complete");

            AssertComplete(bundle, professionGoal.goalId);
            AssertComplete(bundle, trainingGoal.goalId);
            AssertComplete(bundle, credentialGoal.goalId);
            AssertComplete(bundle, rankGoal.goalId);
            AssertComplete(bundle, positionGoal.goalId);
            AssertComplete(bundle, craftingGoal.goalId);
            AssertComplete(bundle, discoveryGoal.goalId);
            Assert.That(perceivedOnly.Succeeded, Is.True, perceivedOnly.Message);
            Assert.That(perceivedOnly.Progress.AuthoritativeComplete, Is.False);
            Assert.That(perceivedOnly.Progress.PerceivedComplete, Is.True);
            Assert.That(staleComplete.Succeeded, Is.False);
            Assert.That(staleComplete.Status, Is.EqualTo(LifePathOperationStatus.StaleProgress));
            Assert.That(complete.Succeeded, Is.True, complete.Message);
        }

        [Test]
        public void ProfessionalIdentityConflictsAndSecretProjectionDoNotGrantProfessionState()
        {
            using TestLabRuntimeBundle bundle = Bundle();
            AddLifePath(bundle);
            long professionRevision = bundle.Professions.Revision;
            LifePathOperationResult identity = bundle.LifePaths.SetProfessionalIdentity(new ProfessionalIdentityData
            {
                identityId = "identity.life.secret-spy",
                personId = PersonId,
                kind = ProfessionalIdentityKind.Primary,
                alignment = ProfessionalIdentityAlignmentState.Conflicted,
                professionId = ProfessionContentIds.SpyProfessionId,
                selfPerceived = true,
                publicDeclared = false,
                active = true,
                secret = true,
                motivationTags = new[] { "secret.identity" },
                accessPolicyId = ProfessionContentIds.AccessSecretId
            }, "tx.life.identity");
            LifePathOperationResult conflict = bundle.LifePaths.RecordIdentityConflict(new IdentityConflictData
            {
                conflictId = "conflict.life.identity",
                personId = PersonId,
                state = ProfessionalIdentityAlignmentState.Conflicted,
                identityIds = new[] { identity.Identity.identityId },
                aspirationIds = Array.Empty<string>(),
                goalIds = Array.Empty<string>(),
                conflictTags = new[] { "secret.identity" },
                accessPolicyId = ProfessionContentIds.AccessSecretId
            }, "tx.life.conflict");
            LifePathProjection<LifePathSnapshot> publicProjection = bundle.LifePaths.ProjectSnapshot(PersonId, LifePathProjectionAudience.Public);
            LifePathProjection<LifePathSnapshot> privateProjection = bundle.LifePaths.ProjectSnapshot(PersonId, LifePathProjectionAudience.SubjectPerson);

            Assert.That(identity.Succeeded, Is.True, identity.Message);
            Assert.That(conflict.Succeeded, Is.True, conflict.Message);
            Assert.That(bundle.Professions.Revision, Is.EqualTo(professionRevision));
            Assert.That(bundle.Professions.QueryByPerson(PersonId).Count, Is.Zero);
            Assert.That(publicProjection.Redacted, Is.True);
            Assert.That(publicProjection.Record.Identities, Is.Empty);
            Assert.That(privateProjection.Record.Identities.Single().professionId, Is.EqualTo(ProfessionContentIds.SpyProfessionId));
        }

        [Test]
        public void PersistenceRoundTripAndCorruptRestoreAreAtomic()
        {
            using TestLabRuntimeBundle bundle = Bundle();
            AddLifePath(bundle);
            AddGoal(bundle, "goal.life.persist", ProfessionContentIds.GoalEnterBlacksmithProfessionId, targetProfession: ProfessionContentIds.BlacksmithProfessionId);
            LifePathOperationResult achievement = bundle.LifePaths.RecordAchievementOrSetback(new LifePathAchievementSetbackReferenceData
            {
                recordId = "achievement.life.persist",
                personId = PersonId,
                lifePathId = "life-path.prototype",
                kind = LifePathAchievementSetbackKind.Achievement,
                sourceRecordType = CareerTransitionSourceRecordType.Custom,
                sourceRecordId = "source.life.persist",
                worldTime = "11",
                exclusive = true,
                accessPolicyId = ProfessionContentIds.AccessPublicId
            }, "tx.life.achievement");
            LifePathRuntimeSaveData save = bundle.LifePaths.CreateSaveData();
            LifePathRuntime restored = NewLifePathRuntime(bundle);
            LifePathOperationResult restore = restored.RestoreFromSaveData(save, bundle.DefinitionRegistry, bundle.Professions, bundle.Training, bundle.ProfessionalActivities, bundle.Credentials, bundle.ProfessionalRanks, bundle.PositionEmployment, bundle.CareerHistory, bundle.KnownPersonIds, Organizations(), restoring: true);
            LifePathRuntimeSaveData corrupt = save.Clone();
            corrupt.goals[0].personId = "person.missing";
            int beforeGoals = restored.GoalCount;
            long beforeRevision = restored.Revision;
            LifePathOperationResult rejected = restored.RestoreFromSaveData(corrupt, bundle.DefinitionRegistry, bundle.Professions, bundle.Training, bundle.ProfessionalActivities, bundle.Credentials, bundle.ProfessionalRanks, bundle.PositionEmployment, bundle.CareerHistory, bundle.KnownPersonIds, Organizations(), restoring: true);

            Assert.That(achievement.Succeeded, Is.True, achievement.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.GoalCount, Is.EqualTo(bundle.LifePaths.GoalCount));
            Assert.That(restored.HistoryHooks.Count, Is.Zero);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Status, Is.EqualTo(LifePathOperationStatus.CorruptSave));
            Assert.That(restored.GoalCount, Is.EqualTo(beforeGoals));
            Assert.That(restored.Revision, Is.EqualTo(beforeRevision));
        }

        private static TestLabRuntimeBundle Bundle()
        {
            return TestLabRuntimeBundle.CreateFresh(Registry(), PersonId, "world.life-path.test", new[] { PersonId, OtherPersonId }, Array.Empty<string>(), "Life Path Tests");
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

        private static void AddLifePath(TestLabRuntimeBundle bundle)
        {
            LifePathOperationResult result = bundle.LifePaths.CreateOrUpdateLifePath(new LifePathRecordData
            {
                lifePathId = "life-path.prototype",
                personId = PersonId,
                state = LifePathState.Active,
                startWorldTime = "1",
                formativeReferences = new[]
                {
                    new FormativeReferenceData { referenceId = "origin.prototype", kind = FormativeReferenceKind.Origin, subjectId = "origin.village", weight = 10 }
                },
                accessPolicyId = ProfessionContentIds.AccessPublicId
            }, "tx.life.path");
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static LifePathOperationResult AddProfession(TestLabRuntimeBundle bundle, string slug = "blacksmith", string professionId = ProfessionContentIds.BlacksmithProfessionId)
        {
            LifePathOperationResult noOp = LifePathOperationResult.Success("No-op", bundle.LifePaths.Revision, bundle.LifePaths.Revision);
            ProfessionOperationResult result = bundle.Professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = $"profession-relationship.life.{slug}",
                personId = PersonId,
                professionId = professionId,
                informalPractice = true,
                formalPractice = true,
                selfDeclared = true,
                recognized = true,
                recognizingAuthorityId = professionId == ProfessionContentIds.FieldMedicProfessionId ? "authority.medical.prototype" : GuildAuthority,
                primary = professionId == ProfessionContentIds.BlacksmithProfessionId,
                active = true,
                startWorldTime = "2",
                accessPolicyId = ProfessionContentIds.AccessPublicId,
                transactionId = $"tx.profession.life.{slug}"
            });
            Assert.That(result.Succeeded, Is.True, result.Message);
            return noOp;
        }

        private static PersonGoalData AddGoal(TestLabRuntimeBundle bundle, string goalId, string definitionId, string parentAspiration = "", string targetProfession = "", string targetTraining = "", string targetCredential = "", string targetRank = "", string targetPosition = "", string targetActivity = "")
        {
            LifePathOperationResult result = bundle.LifePaths.AddGoal(Goal(goalId, definitionId, parentAspiration, targetProfession, targetTraining, targetCredential, targetRank, targetPosition, targetActivity), $"tx.{goalId}");
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Goal;
        }

        private static PersonAspirationData Aspiration(string id, string definitionId, string targetProfession = "")
        {
            return new PersonAspirationData
            {
                aspirationId = id,
                personId = PersonId,
                aspirationDefinitionId = definitionId,
                state = PersonAspirationState.Active,
                targetProfessionId = targetProfession,
                priority = 10,
                startWorldTime = "1",
                accessPolicyId = ProfessionContentIds.AccessPublicId
            };
        }

        private static PersonGoalData Goal(string id, string definitionId, string parentAspiration = "", string targetProfession = "", string targetTraining = "", string targetCredential = "", string targetRank = "", string targetPosition = "", string targetActivity = "", string[] dependencies = null, LifeGoalProgressState progress = LifeGoalProgressState.NotStarted)
        {
            return new PersonGoalData
            {
                goalId = id,
                personId = PersonId,
                goalDefinitionId = definitionId,
                parentAspirationId = parentAspiration,
                state = PersonGoalState.Active,
                progressState = progress,
                targetProfessionId = targetProfession,
                targetTrainingProgramId = targetTraining,
                targetCredentialDefinitionId = targetCredential,
                targetRankDefinitionId = targetRank,
                targetPositionDefinitionId = targetPosition,
                targetActivityDefinitionId = targetActivity,
                dependencyGoalIds = dependencies ?? Array.Empty<string>(),
                priority = 10,
                startWorldTime = "1",
                accessPolicyId = ProfessionContentIds.AccessPublicId
            };
        }

        private static void AssertComplete(TestLabRuntimeBundle bundle, string goalId)
        {
            LifePathOperationResult result = bundle.LifePaths.EvaluateGoalProgress(goalId);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Progress.AuthoritativeComplete, Is.True, goalId);
        }

        private static void SeedTraining(TestLabRuntimeBundle bundle, string enrollmentId, string programId)
        {
            TrainingRuntimeSaveData save = bundle.Training.CreateSaveData();
            save.enrollments.Add(new TrainingEnrollmentData { enrollmentId = enrollmentId, personId = PersonId, programId = programId, relatedProfessionId = ProfessionContentIds.BlacksmithProfessionId, state = TrainingEnrollmentState.Completed, revision = 1L });
            TrainingOperationResult restore = bundle.Training.RestoreFromSaveData(save, bundle.DefinitionRegistry, bundle.Professions, bundle.Transfers, bundle.KnownPersonIds, restoring: false);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
        }

        private static void SeedCredential(TestLabRuntimeBundle bundle, string credentialId, string credentialDefinitionId)
        {
            CredentialRuntimeSaveData save = bundle.Credentials.CreateSaveData();
            Assert.That(bundle.DefinitionRegistry.TryGet(credentialDefinitionId, out CredentialDefinition definition), Is.True);
            string applicationId = $"{credentialId}.application";
            string[] examinationIds = definition.RequiredExaminationDefinitionIds.ToArray();
            save.applications.Add(new CredentialApplicationData
            {
                applicationId = applicationId,
                applicantPersonId = PersonId,
                credentialDefinitionId = credentialDefinitionId,
                requestedIssuer = new CredentialIssuerReferenceData { issuerId = GuildAuthority, issuerKind = CredentialIssuerAuthorityKind.Guild },
                relatedProfessionId = ProfessionContentIds.BlacksmithProfessionId,
                relatedSpecializationId = ProfessionContentIds.WeaponsmithSpecializationId,
                submissionWorldTime = "3",
                qualificationSnapshot = bundle.Credentials.EvaluateQualification(PersonId, credentialDefinitionId, perceived: true, privilegedDiagnostics: true).Snapshot,
                supportingTrainingRecordIds = definition.RequiredTrainingProgramIds.ToArray(),
                examinationAttemptIds = examinationIds.Select(id => $"{credentialId}.{id}.attempt").ToArray(),
                state = CredentialApplicationState.Approved,
                decisionWorldTime = "3",
                decisionMakerId = GuildAuthority,
                decisionReason = "Approved fixture prerequisite.",
                accessPolicyId = ProfessionContentIds.AccessPublicId
            });
            foreach (string examinationId in examinationIds)
            {
                save.examinationAttempts.Add(new CredentialExaminationAttemptData
                {
                    attemptId = $"{credentialId}.{examinationId}.attempt",
                    examinationDefinitionId = examinationId,
                    applicantPersonId = PersonId,
                    evaluatorAuthorityId = GuildAuthority,
                    startWorldTime = "3",
                    completionWorldTime = "3",
                    score = 900,
                    state = CredentialExaminationAttemptState.Passed,
                    accessPolicyId = ProfessionContentIds.AccessPublicId
                });
            }

            save.credentials.Add(new CredentialRecordData
            {
                credentialId = credentialId,
                credentialDefinitionId = credentialDefinitionId,
                recipientPersonId = PersonId,
                issuer = new CredentialIssuerReferenceData { issuerId = GuildAuthority, issuerKind = CredentialIssuerAuthorityKind.Guild },
                state = CredentialState.Active,
                relatedProfessionId = ProfessionContentIds.BlacksmithProfessionId,
                relatedSpecializationId = ProfessionContentIds.WeaponsmithSpecializationId,
                supportingApplicationId = applicationId,
                supportingExaminationAttemptId = examinationIds.FirstOrDefault() is string firstExam && !string.IsNullOrWhiteSpace(firstExam) ? $"{credentialId}.{firstExam}.attempt" : string.Empty,
                supportingTrainingRecordIds = definition.RequiredTrainingProgramIds.ToArray(),
                authenticityState = CredentialAuthenticityState.Authoritative,
                registrationNumber = $"registration.{credentialId}",
                issueWorldTime = "3",
                effectiveWorldTime = "3",
                accessPolicyId = ProfessionContentIds.AccessPublicId
            });
            CredentialOperationResult restore = bundle.Credentials.RestoreFromSaveData(save, bundle.DefinitionRegistry, bundle.Professions, bundle.Training, bundle.ProfessionalActivities, bundle.KnownPersonIds, Authorities(), restoring: false);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
        }

        private static void SeedRank(TestLabRuntimeBundle bundle, string rankRecordId, string rankDefinitionId)
        {
            ProfessionalRankRuntimeSaveData save = bundle.ProfessionalRanks.CreateSaveData();
            save.ranks.Add(new ProfessionalRankRecordData { rankRecordId = rankRecordId, personId = PersonId, professionId = ProfessionContentIds.BlacksmithProfessionId, ladderDefinitionId = ProfessionContentIds.BlacksmithRankLadderId, rankDefinitionId = rankDefinitionId, state = ProfessionalRankState.Active, recognizingAuthorityId = GuildAuthority, issueWorldTime = "4", effectiveWorldTime = "4", accessPolicyId = ProfessionContentIds.AccessPublicId });
            ProfessionalRankOperationResult restore = bundle.ProfessionalRanks.RestoreFromSaveData(save, bundle.DefinitionRegistry, bundle.Professions, bundle.Training, bundle.ProfessionalActivities, bundle.Credentials, bundle.KnownPersonIds, Authorities(), restoring: false);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
        }

        private static void SeedEmployment(TestLabRuntimeBundle bundle, string positionId, string employmentId, string positionDefinitionId)
        {
            PositionEmploymentRuntimeSaveData save = bundle.PositionEmployment.CreateSaveData();
            save.positions.Add(new PositionInstanceData { positionInstanceId = positionId, positionDefinitionId = positionDefinitionId, organizationId = GuildOrganization, organizationTypeId = ProfessionContentIds.GuildOrganizationTypeId, state = PositionInstanceState.Filled, holderPersonIds = new[] { PersonId }, maximumHolders = 2, vacancyAllowed = true, accessPolicyId = ProfessionContentIds.AccessPublicId });
            save.employments.Add(new EmploymentRecordData { employmentId = employmentId, personId = PersonId, employerOrganizationId = GuildOrganization, positionInstanceId = positionId, positionDefinitionId = positionDefinitionId, classification = EmploymentClassification.PartTime, state = EmploymentState.Active, startWorldTime = "5", appointmentAuthorityId = ProfessionContentIds.PositionAppointAuthorityId, accessPolicyId = ProfessionContentIds.AccessPublicId });
            PositionEmploymentOperationResult restore = bundle.PositionEmployment.RestoreFromSaveData(save, bundle.DefinitionRegistry, bundle.Professions, bundle.Training, bundle.ProfessionalActivities, bundle.Credentials, bundle.ProfessionalRanks, bundle.KnownPersonIds, Organizations(), Authorities(), restoring: false);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
        }

        private static void SeedActivity(TestLabRuntimeBundle bundle, string activityId, string activityDefinitionId, ProfessionalActivitySourceType sourceType)
        {
            ProfessionalActivityRuntimeSaveData save = bundle.ProfessionalActivities.CreateSaveData();
            save.activities.Add(new ProfessionalActivityRecordData
            {
                activityId = activityId,
                personId = PersonId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                activityDefinitionId = activityDefinitionId,
                source = new ProfessionalActivitySourceReferenceData { sourceType = sourceType, sourceId = $"{activityId}.source", sourceRevision = 1L },
                state = ProfessionalActivityState.Validated,
                outcome = ProfessionalActivityOutcomeState.Successful,
                responsibility = ProfessionalResponsibilityLevel.IndependentPractitioner,
                quality = 800,
                startWorldTime = "6",
                completionWorldTime = "7",
                accessPolicyId = ProfessionContentIds.AccessPublicId
            });
            ProfessionalActivityOperationResult restore = bundle.ProfessionalActivities.RestoreFromSaveData(save, bundle.DefinitionRegistry, bundle.Professions, bundle.KnownPersonIds, restoring: false);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
        }

        private static LifePathRuntime NewLifePathRuntime(TestLabRuntimeBundle bundle)
        {
            LifePathRuntime runtime = new LifePathRuntime();
            runtime.Configure(bundle.DefinitionRegistry, bundle.Professions, bundle.Training, bundle.ProfessionalActivities, bundle.Credentials, bundle.ProfessionalRanks, bundle.PositionEmployment, bundle.CareerHistory, bundle.KnownPersonIds, Organizations());
            return runtime;
        }

        private static string[] Organizations() => new[] { GuildOrganization, "organization.prototype.royal-forge", "organization.prototype.independent" };

        private static string[] Authorities() => new[] { GuildAuthority, ProfessionContentIds.PositionAppointAuthorityId, ProfessionContentIds.PositionDutyAssignAuthorityId, GuildOrganization };
    }
}
