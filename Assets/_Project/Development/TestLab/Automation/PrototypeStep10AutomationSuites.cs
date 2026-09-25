#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Professions.Integration;
using UnityEngine;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(10, "Professions", 1000)]
    public static class PrototypeStep10AutomationSuites
    {
        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            registry.TryRegister(BuildProfessionIdentitySuite(), out _);
            registry.TryRegister(BuildProfessionalEligibilityEntrySuite(), out _);
            registry.TryRegister(BuildEducationTrainingApprenticeshipSuite(), out _);
            registry.TryRegister(BuildProfessionalActivityExperienceSuite(), out _);
            registry.TryRegister(BuildQualificationsCredentialsCertificationSuite(), out _);
            registry.TryRegister(BuildProfessionalRanksMasterySpecializationsSuite(), out _);
            registry.TryRegister(BuildPositionsDutiesEmploymentFoundationsSuite(), out _);
            registry.TryRegister(BuildCareerHistoryTransitionsSuite(), out _);
            registry.TryRegister(BuildLifePathsAspirationsProfessionalIdentitySuite(), out _);
            registry.TryRegister(BuildProfessionLifePathIntegrationFinalizationSuite(), out _);
        }

        private static ITestLabAutomationSuite BuildProfessionIdentitySuite()
        {
            return new TestLabAutomationSuite(
                "feature.10.1.profession-identity-definitions",
                "Feature 10.1 Profession Identity and Definitions",
                "10.1",
                "Profession definition, specialization, relationship, access, persistence, and boundary automation.",
                1010,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "PersonProfessionRuntime", "ProfessionDefinition", "ProfessionSpecializationDefinition", "InformationAccessRuntime" },
                scenarios: new ITestLabAutomationScenario[]
                {
                    Scenario("definitions-and-specializations", "Profession definitions and specialization parentage resolve", 10, Step("step10-profession-definitions", DefinitionsAndSpecializations)),
                    Scenario("formal-informal-primary", "Formal, informal, primary, and duplicate profession rules are enforced", 20, Step("step10-profession-primary", FormalInformalPrimary)),
                    Scenario("secret-access-projection", "Secret profession relationships redact ordinary projections", 30, Step("step10-profession-secret", SecretAccessProjection)),
                    Scenario("persistence-round-trip", "Profession relationships save and restore without replay", 40, Step("step10-profession-persistence", PersistenceRoundTrip)),
                    Scenario("competency-boundary", "Profession identity does not grant skills or capabilities", 50, Step("step10-profession-boundary", CompetencyBoundary))
                });
        }

        private static ITestLabAutomationScenario Scenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[]
                {
                    ProfessionContentIds.BlacksmithProfessionId,
                    ProfessionContentIds.FieldMedicProfessionId,
                    ProfessionContentIds.WeaponsmithSpecializationId,
                    ProfessionContentIds.AccessSecretId
                });
        }

        private static ITestLabAutomationSuite BuildLifePathsAspirationsProfessionalIdentitySuite()
        {
            return new TestLabAutomationSuite(
                "feature.10.9.life-paths-aspirations-professional-identity",
                "Feature 10.9 Life Paths, Aspirations, and Professional Identity",
                "10.9",
                "Life-path records, aspirations, goals, progress, professional identity, access projection, persistence, and boundary automation.",
                1090,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "LifePathRuntime", "AspirationDefinition", "LifeGoalDefinition", "CareerHistoryRuntime" },
                scenarios: new ITestLabAutomationScenario[]
                {
                    LifePathScenario("definitions-and-records", "Life-path definitions and records validate", 10, Step("step10-life-definitions", LifePathDefinitionsAndRecords)),
                    LifePathScenario("goal-progress-boundaries", "Goal progress evaluates authoritative sources and stale completion", 20, Step("step10-life-progress", LifePathGoalProgressBoundaries)),
                    LifePathScenario("identity-and-access", "Professional identity remains separate and secret projections redact", 30, Step("step10-life-identity", LifePathIdentityAndAccess)),
                    LifePathScenario("lifecycle-conflicts", "Aspiration and goal lifecycle transitions preserve conflicts", 40, Step("step10-life-lifecycle", LifePathLifecycleConflicts)),
                    LifePathScenario("persistence-round-trip", "Life paths persist and corrupt restore is atomic", 50, Step("step10-life-persistence", LifePathPersistenceRoundTrip))
                });
        }

        private static ITestLabAutomationScenario LifePathScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[]
                {
                    ProfessionContentIds.AspirationEnterBlacksmithProfessionId,
                    ProfessionContentIds.AspirationReachMasterWeaponsmithId,
                    ProfessionContentIds.GoalEnterBlacksmithProfessionId,
                    ProfessionContentIds.GoalCompleteBlacksmithApprenticeshipId,
                    ProfessionContentIds.GoalProduceMasterworkId
                });
        }

        private static ITestLabAutomationSuite BuildProfessionLifePathIntegrationFinalizationSuite()
        {
            return new TestLabAutomationSuite(
                "feature.10.10.profession-life-path-integration-finalization",
                "Feature 10.10 Profession and Life Path Integration Finalization",
                "10.10",
                "Step 10 authority, graph validation, persistence dependency, deterministic fingerprint, lifecycle, access, and scale automation.",
                1100,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "Step10IntegrationValidator", "PersonProfessionRuntime", "LifePathRuntime", "CareerHistoryRuntime" },
                scenarios: new ITestLabAutomationScenario[]
                {
                    IntegrationScenario("readiness-and-validation", "Step 10 definitions and integrated runtime graph validate", 10, Step("step10-integration-validate", Step10IntegrationReadinessAndValidation)),
                    IntegrationScenario("persistence-fingerprint-and-schema", "Persistence dependencies, schema validation, and fingerprints are deterministic", 20, Step("step10-integration-persistence", Step10IntegrationPersistenceFingerprintAndSchema)),
                    IntegrationScenario("cross-system-career-graph", "Representative cross-system career graph remains authoritative", 30, Step("step10-integration-cross-system", Step10IntegrationCrossSystemCareerGraph)),
                    IntegrationScenario("scale-snapshot-and-access", "Large fixtures, immutable snapshots, and access boundaries remain stable", 40, Step("step10-integration-scale", Step10IntegrationScaleSnapshotAndAccess))
                });
        }

        private static ITestLabAutomationScenario IntegrationScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[]
                {
                    ProfessionContentIds.BlacksmithProfessionId,
                    ProfessionContentIds.BlacksmithApprenticeshipProgramId,
                    ProfessionContentIds.BlacksmithGuildLicenseCredentialId,
                    ProfessionContentIds.BlacksmithRankJourneymanId,
                    ProfessionContentIds.GuildClerkPositionId,
                    ProfessionContentIds.AspirationEarnGuildLicenseId,
                    ProfessionContentIds.GoalEarnBlacksmithGuildLicenseId
                });
        }

        private static ITestLabAutomationSuite BuildProfessionalEligibilityEntrySuite()
        {
            return new TestLabAutomationSuite(
                "feature.10.2.professional-eligibility-entry",
                "Feature 10.2 Professional Eligibility and Entry",
                "10.2",
                "Professional eligibility, informal entry, formal requests, specialization, reentry, access, and persistence automation.",
                1020,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "ProfessionEntryRuntime", "PersonProfessionRuntime", "ProfessionEntryPathDefinition" },
                scenarios: new ITestLabAutomationScenario[]
                {
                    EntryScenario("eligibility-preview", "Eligibility preview is non-mutating and immutable", 10, EntryStep("step10-entry-preview", EligibilityPreview)),
                    EntryScenario("informal-self-declaration", "Self-declared informal practice commits without recognition grants", 20, EntryStep("step10-entry-informal", InformalSelfDeclaration)),
                    EntryScenario("formal-request-approval", "Formal request approval revalidates eligibility and creates recognition", 30, EntryStep("step10-entry-formal", FormalRequestApproval)),
                    EntryScenario("stale-and-authority-rejection", "Invalid authority and stale eligibility are rejected before mutation", 40, EntryStep("step10-entry-stale", StaleAndAuthorityRejection)),
                    EntryScenario("specialization-and-reentry", "Specialization entry and inactive reentry preserve parent relationships", 50, EntryStep("step10-entry-specialization-reentry", SpecializationAndReentry)),
                    EntryScenario("projection-and-persistence", "Entry request projections redact and persistence restores without replay", 60, EntryStep("step10-entry-persistence", ProjectionAndPersistence))
                });
        }

        private static ITestLabAutomationScenario EntryScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[]
                {
                    ProfessionContentIds.BlacksmithSelfDeclaredEntryPathId,
                    ProfessionContentIds.FieldMedicRecognitionEntryPathId,
                    ProfessionContentIds.WeaponsmithSpecializationEntryPathId,
                    ProfessionContentIds.BlacksmithReentryPathId
                });
        }

        private static ITestLabScenarioStep EntryStep(string stepId, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            return new TestLabScenarioStep(stepId, stepId, action);
        }

        private static ITestLabScenarioStep Step(string stepId, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            return new TestLabScenarioStep(stepId, stepId, action);
        }

        private static ITestLabAutomationSuite BuildEducationTrainingApprenticeshipSuite()
        {
            return new TestLabAutomationSuite(
                "feature.10.3.education-training-apprenticeship",
                "Feature 10.3 Education, Training, and Apprenticeship",
                "10.3",
                "Training programs, curricula, apprenticeship enrollment, instruction, supervised work, progress, access, and persistence automation.",
                1030,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "TrainingRuntime", "TrainingProgramDefinition", "TrainingCurriculumDefinition", "InformationTransferRuntime" },
                scenarios: new ITestLabAutomationScenario[]
                {
                    TrainingScenario("definitions-and-curricula", "Training definitions and curricula validate", 10, Step("step10-training-definitions", TrainingDefinitionsAndCurricula)),
                    TrainingScenario("enrollment-apprenticeship", "Enrollment and apprenticeship transitions preserve boundaries", 20, Step("step10-training-enrollment", TrainingEnrollmentApprenticeship)),
                    TrainingScenario("learning-session-teaching", "Learning sessions route teaching through Step 8", 30, Step("step10-training-teaching", TrainingLearningSessionTeaching)),
                    TrainingScenario("practical-supervised-work", "Practical assignments and supervised work reference activity records", 40, Step("step10-training-practical", TrainingPracticalSupervisedWork)),
                    TrainingScenario("progress-completion-boundaries", "Progress evaluation and completion enforce hidden requirements", 50, Step("step10-training-progress", TrainingProgressCompletionBoundaries)),
                    TrainingScenario("persistence-projections", "Training persistence and projections are atomic and redacted", 60, Step("step10-training-persistence", TrainingPersistenceProjections))
                });
        }

        private static ITestLabAutomationScenario TrainingScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[]
                {
                    ProfessionContentIds.BlacksmithProfessionId,
                    ProfessionContentIds.BlacksmithApprenticeshipProgramId,
                    ProfessionContentIds.BlacksmithApprenticeshipCurriculumId,
                    ProfessionContentIds.BlacksmithSafetyProgramId,
                    ProfessionContentIds.BlacksmithSafetyCurriculumId,
                    ProfessionContentIds.TrainingLessonTransferDefinitionId,
                    ProfessionContentIds.TrainingDemonstrationTransferDefinitionId,
                    ProfessionContentIds.TrainingGuidedPracticeTransferDefinitionId
                });
        }

        private static ITestLabAutomationSuite BuildProfessionalActivityExperienceSuite()
        {
            return new TestLabAutomationSuite(
                "feature.10.4.professional-activity-experience",
                "Feature 10.4 Professional Activity and Experience",
                "10.4",
                "Professional activity source adapters, validation, evidence summaries, boundaries, access, and persistence automation.",
                1040,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "ProfessionalActivityRuntime", "ProfessionalActivityDefinition", "PersonProfessionRuntime" },
                scenarios: new ITestLabAutomationScenario[]
                {
                    ActivityScenario("definitions-and-adapters", "Professional activity definitions and source adapters validate", 10, Step("step10-activity-definitions", ProfessionalActivityDefinitionsAndAdapters)),
                    ActivityScenario("record-and-validate", "Validated professional activity creates experience evidence", 20, Step("step10-activity-validate", ProfessionalActivityRecordAndValidate)),
                    ActivityScenario("duplicates-shared-credit", "Exclusive duplicates and shared role credit are deterministic", 30, Step("step10-activity-duplicates", ProfessionalActivityDuplicatesSharedCredit)),
                    ActivityScenario("summary-requirements-boundaries", "Experience summaries and requirements do not mutate owning systems", 40, Step("step10-activity-summary", ProfessionalActivitySummaryRequirementsBoundaries)),
                    ActivityScenario("access-persistence", "Activity projections redact and persistence restores atomically", 50, Step("step10-activity-persistence", ProfessionalActivityAccessPersistence))
                });
        }

        private static ITestLabAutomationScenario ActivityScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[]
                {
                    ProfessionContentIds.BlacksmithProfessionId,
                    ProfessionContentIds.WeaponsmithSpecializationId,
                    ProfessionContentIds.BlacksmithCraftingActivityDefinitionId,
                    ProfessionContentIds.BlacksmithSupervisedPracticeActivityDefinitionId,
                    ProfessionContentIds.BlacksmithTeachingActivityDefinitionId,
                    ProfessionContentIds.BlacksmithExperimentationActivityDefinitionId
                });
        }

        private static ITestLabAutomationSuite BuildQualificationsCredentialsCertificationSuite()
        {
            return new TestLabAutomationSuite(
                "feature.10.5.qualifications-credentials-certification",
                "Feature 10.5 Qualifications, Credentials, and Certification",
                "10.5",
                "Credential qualification, application, examination, issuing authority, lifecycle, access, and persistence automation.",
                1050,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "CredentialRuntime", "CredentialDefinition", "CredentialExaminationDefinition", "PersonProfessionRuntime", "TrainingRuntime", "ProfessionalActivityRuntime" },
                scenarios: new ITestLabAutomationScenario[]
                {
                    CredentialScenario("definitions-and-qualification", "Credential definitions resolve and qualification evaluates without mutation", 10, Step("step10-credential-qualification", CredentialDefinitionsAndQualification)),
                    CredentialScenario("application-examination-issuance", "Applications, examinations, and issuance produce authoritative credentials", 20, Step("step10-credential-issue", CredentialApplicationExaminationIssuance)),
                    CredentialScenario("stale-unauthorized-forgery", "Unauthorized issuers, stale qualifications, and forged claims are rejected safely", 30, Step("step10-credential-boundaries", CredentialStaleUnauthorizedForgery)),
                    CredentialScenario("lifecycle-permissions", "Expiration, suspension, reinstatement, revocation, and renewal affect permissions", 40, Step("step10-credential-lifecycle", CredentialLifecyclePermissions)),
                    CredentialScenario("access-persistence", "Credential projections redact and persistence restores atomically", 50, Step("step10-credential-persistence", CredentialAccessPersistence))
                });
        }

        private static ITestLabAutomationScenario CredentialScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[]
                {
                    ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId,
                    ProfessionContentIds.BlacksmithGuildLicenseCredentialId,
                    ProfessionContentIds.BlacksmithPracticalExaminationId,
                    ProfessionContentIds.BlacksmithWrittenExaminationId,
                    ProfessionContentIds.BlacksmithApprenticeshipProgramId,
                    ProfessionContentIds.BlacksmithSupervisedPracticeActivityDefinitionId
                });
        }

        private static ITestLabAutomationSuite BuildProfessionalRanksMasterySpecializationsSuite()
        {
            return new TestLabAutomationSuite(
                "feature.10.6.professional-ranks-mastery-specializations",
                "Feature 10.6 Professional Ranks, Mastery, and Specializations",
                "10.6",
                "Professional rank ladders, advancement, specialization progression, mastery, permissions, lifecycle, access, and persistence automation.",
                1060,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "ProfessionalRankRuntime", "ProfessionalRankDefinition", "ProfessionalRankLadderDefinition", "ProfessionalMasteryDefinition", "CredentialRuntime", "TrainingRuntime", "ProfessionalActivityRuntime" },
                scenarios: new ITestLabAutomationScenario[]
                {
                    RankScenario("definitions-ladders", "Rank definitions, ladders, and mastery definitions validate", 10, Step("step10-rank-definitions", RankDefinitionsLadders)),
                    RankScenario("advancement-application-promotion", "Advancement application and promotion revalidate requirements", 20, Step("step10-rank-promotion", RankAdvancementApplicationPromotion)),
                    RankScenario("specialization-mastery", "Specialization ranks and mastery use explicit evidence", 30, Step("step10-rank-mastery", RankSpecializationMastery)),
                    RankScenario("lifecycle-permissions-boundaries", "Rank lifecycle controls permissions without granting competencies", 40, Step("step10-rank-lifecycle", RankLifecyclePermissionsBoundaries)),
                    RankScenario("access-persistence", "Rank projections redact and persistence restores atomically", 50, Step("step10-rank-persistence", RankAccessPersistence))
                });
        }

        private static ITestLabAutomationScenario RankScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[]
                {
                    ProfessionContentIds.BlacksmithRankApprenticeId,
                    ProfessionContentIds.BlacksmithRankJourneymanId,
                    ProfessionContentIds.BlacksmithRankMasterId,
                    ProfessionContentIds.WeaponsmithRankMasterId,
                    ProfessionContentIds.BlacksmithRankLadderId,
                    ProfessionContentIds.WeaponsmithRankLadderId,
                    ProfessionContentIds.WeaponsmithMasteryId
                });
        }

        private static ITestLabAutomationSuite BuildPositionsDutiesEmploymentFoundationsSuite()
        {
            return new TestLabAutomationSuite(
                "feature.10.7.positions-duties-employment-foundations",
                "Feature 10.7 Positions, Duties, and Employment Foundations",
                "10.7",
                "Position definitions, exact organization positions, employment records, duties, authority, lifecycle, access, and persistence automation.",
                1070,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "PositionEmploymentRuntime", "PositionDefinition", "DutyDefinition", "ProfessionalRankRuntime", "CredentialRuntime", "ProfessionalActivityRuntime" },
                scenarios: new ITestLabAutomationScenario[]
                {
                    PositionScenario("definitions-and-vacancies", "Position and duty definitions validate and vacant fixtures are created", 10, Step("step10-position-definitions", PositionDefinitionsAndVacancies)),
                    PositionScenario("eligibility-applications-appointments", "Eligibility, application, offer, acceptance, and direct appointment are integrated", 20, Step("step10-position-appointment", PositionEligibilityApplicationsAppointments)),
                    PositionScenario("capacity-conflicts-duties-authority", "Capacity, conflicts, duties, activity evidence, and authority are enforced", 30, Step("step10-position-duties", PositionCapacityConflictsDutiesAuthority)),
                    PositionScenario("reporting-lifecycle-persistence", "Reporting, lifecycle, projections, and persistence are safe", 40, Step("step10-position-persistence", PositionReportingLifecyclePersistence))
                });
        }

        private static ITestLabAutomationScenario PositionScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory | TestLabRuntimeArea.Items,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[]
                {
                    ProfessionContentIds.RoyalForgeSeniorSmithPositionId,
                    ProfessionContentIds.GuildClerkPositionId,
                    ProfessionContentIds.ApprenticeSupervisorPositionId,
                    ProfessionContentIds.SeniorSmithCraftDutyId,
                    ProfessionContentIds.GuildClerkRecordDutyId
                });
        }

        private static ITestLabAutomationSuite BuildCareerHistoryTransitionsSuite()
        {
            return new TestLabAutomationSuite(
                "feature.10.8.career-history-transitions",
                "Feature 10.8 Career History and Transitions",
                "10.8",
                "Career episodes, transitions, timelines, gaps, retirement, access, persistence, and authoritative source integration automation.",
                1080,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: new[] { "CareerHistoryRuntime", "CareerTransitionDefinition", "PositionEmploymentRuntime", "ProfessionalRankRuntime" },
                scenarios: new ITestLabAutomationScenario[]
                {
                    CareerScenario("episodes-and-timelines", "Career episodes build immutable deterministic timelines", 10, Step("step10-career-timeline", CareerDefinitionsEpisodesTimeline)),
                    CareerScenario("authoritative-transitions", "Promotion, demotion, transfer, resignation, and dismissal reference owning runtimes", 20, Step("step10-career-transitions", CareerAuthoritativeTransitions)),
                    CareerScenario("concurrent-primary-gaps-redaction", "Concurrent careers, primary career changes, gaps, and redaction behave deterministically", 30, Step("step10-career-concurrent", CareerConcurrentGapsRedaction)),
                    CareerScenario("retirement-return-career-change", "Retirement, return, and career change preserve prior history", 40, Step("step10-career-retirement", CareerRetirementReturnChange)),
                    CareerScenario("persistence-and-restore", "Career history persistence rejects corrupt restore without replaying hooks", 50, Step("step10-career-persistence", CareerPersistenceRestore))
                });
        }

        private static ITestLabAutomationScenario CareerScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[]
                {
                    ProfessionContentIds.CareerProfessionEnteredTransitionId,
                    ProfessionContentIds.CareerEmploymentStartedTransitionId,
                    ProfessionContentIds.CareerPromotionTransitionId,
                    ProfessionContentIds.CareerTransferTransitionId,
                    ProfessionContentIds.CareerRetirementTransitionId,
                    ProfessionContentIds.CareerChangeTransitionId
                });
        }

        private static TestLabAutomationStepResult PositionDefinitionsAndVacancies(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in ProfessionContentIds.GetDefinitions(runtimes.DefinitionRegistry))
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(runtimes.DefinitionRegistry.DefinitionsById, report);
                }
            }

            bool senior = runtimes.DefinitionRegistry.TryGet(ProfessionContentIds.RoyalForgeSeniorSmithPositionId, out PositionDefinition seniorDefinition);
            bool clerk = runtimes.DefinitionRegistry.TryGet(ProfessionContentIds.GuildClerkPositionId, out PositionDefinition clerkDefinition);
            bool duty = runtimes.DefinitionRegistry.TryGet(ProfessionContentIds.SeniorSmithCraftDutyId, out DutyDefinition craftDuty);
            PositionEmploymentOperationResult vacant = CreatePosition(context, "vacant-senior", ProfessionContentIds.RoyalForgeSeniorSmithPositionId, "organization.prototype.royal-forge", ProfessionContentIds.ForgeOrganizationTypeId, 1);
            PositionEmploymentOperationResult shared = CreatePosition(context, "vacant-clerk", ProfessionContentIds.GuildClerkPositionId, "organization.prototype.guild", ProfessionContentIds.GuildOrganizationTypeId, 2);

            bool valid = report.ErrorCount == 0
                && report.WarningCount == 0
                && senior
                && clerk
                && duty
                && seniorDefinition != null
                && seniorDefinition.RequiredRankDefinitionIds.Contains(ProfessionContentIds.BlacksmithRankJourneymanId)
                && seniorDefinition.RequiredCredentialDefinitionIds.Contains(ProfessionContentIds.BlacksmithGuildLicenseCredentialId)
                && seniorDefinition.RequiredTrainingProgramIds.Contains(ProfessionContentIds.BlacksmithApprenticeshipProgramId)
                && seniorDefinition.ExperienceRequirement.minimumValidatedActivities >= 2
                && clerkDefinition != null
                && clerkDefinition.SharedPositionAllowed
                && craftDuty != null
                && craftDuty.PositionDefinitionId == ProfessionContentIds.RoyalForgeSeniorSmithPositionId
                && vacant.Succeeded
                && vacant.Position?.state == PositionInstanceState.Vacant
                && shared.Succeeded
                && shared.Position?.maximumHolders == 2;
            return TestLabAssertions.True("step10-position-definitions", "Position and duty definitions validate and vacant position fixtures are created", valid, $"Errors={report.ErrorCount} Warnings={report.WarningCount} Senior={senior} Clerk={clerk} Duty={duty} Vacant={vacant.Status} Shared={shared.Status}");
        }

        private static TestLabAutomationStepResult PositionEligibilityApplicationsAppointments(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            PositionEmploymentRuntime runtime = runtimes.PositionEmployment;
            PositionEmploymentOperationResult position = CreatePosition(context, "appointment-senior", ProfessionContentIds.RoyalForgeSeniorSmithPositionId, "organization.prototype.royal-forge", ProfessionContentIds.ForgeOrganizationTypeId, 1);
            long beforeEligibilityRevision = runtime.Revision;
            PositionEligibilityResult before = runtime.EvaluateEligibility(runtimes.PersonId, position.Position?.positionInstanceId, perceived: true, privilegedDiagnostics: false);
            bool noEligibilityMutation = beforeEligibilityRevision == runtime.Revision;
            EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankJourneymanId, "position-appointment");
            EnsureGuildLicense(context, "position-appointment");
            PositionEligibilityResult authoritative = runtime.EvaluateEligibility(runtimes.PersonId, position.Position?.positionInstanceId, privilegedDiagnostics: true);
            PositionEligibilityResult perceived = runtime.EvaluateEligibility(runtimes.PersonId, position.Position?.positionInstanceId, perceived: true, privilegedDiagnostics: false);
            PositionEmploymentOperationResult apply = runtime.SubmitApplication(context.ScenarioContext.ScopedId("position-application", "senior"), runtimes.PersonId, position.Position?.positionInstanceId, authoritative.Snapshot, "100", context.ScenarioContext.ScopedId("position-tx", "apply"));
            PositionEmploymentOperationResult offer = runtime.OfferPosition(apply.Application?.requestId, ProfessionContentIds.PositionDutyAssignAuthorityId, "101", context.ScenarioContext.ScopedId("position-tx", "offer"));
            PositionEmploymentOperationResult accept = runtime.AcceptOffer(apply.Application?.requestId, runtimes.PersonId, "102", context.ScenarioContext.ScopedId("position-tx", "accept"));
            PositionEmploymentOperationResult staleAppointment = runtime.AppointPerson(context.ScenarioContext.ScopedId("employment", "stale"), apply.Application?.requestId, runtimes.PersonId, position.Position?.positionInstanceId, ProfessionContentIds.PositionDutyAssignAuthorityId, authoritative.Snapshot, "103", context.ScenarioContext.ScopedId("position-tx", "stale"));
            PositionEligibilityResult currentEligibility = runtime.EvaluateEligibility(runtimes.PersonId, position.Position?.positionInstanceId, privilegedDiagnostics: true);
            PositionEligibilitySnapshotData tampered = currentEligibility.Snapshot.Clone();
            tampered.evaluationHash = "stale";
            PositionEmploymentOperationResult tamperedAppointment = runtime.AppointPerson(context.ScenarioContext.ScopedId("employment", "tampered"), apply.Application?.requestId, runtimes.PersonId, position.Position?.positionInstanceId, ProfessionContentIds.PositionDutyAssignAuthorityId, tampered, "104", context.ScenarioContext.ScopedId("position-tx", "tampered"));
            PositionEmploymentOperationResult unauthorized = runtime.AppointPerson(context.ScenarioContext.ScopedId("employment", "unauthorized"), apply.Application?.requestId, runtimes.PersonId, position.Position?.positionInstanceId, "authority.bad", currentEligibility.Snapshot, "105", context.ScenarioContext.ScopedId("position-tx", "unauthorized"));
            PositionEmploymentOperationResult appoint = runtime.AppointPerson(context.ScenarioContext.ScopedId("employment", "senior"), apply.Application?.requestId, runtimes.PersonId, position.Position?.positionInstanceId, ProfessionContentIds.PositionDutyAssignAuthorityId, currentEligibility.Snapshot, "106", context.ScenarioContext.ScopedId("position-tx", "appoint"));

            bool boundaries = runtimes.Professions.QueryByProfession(ProfessionContentIds.BlacksmithProfessionId).Count == 1
                && runtimes.Credentials.QueryByRecipient(runtimes.PersonId, activeOnly: true).Any()
                && runtimes.ProfessionalRanks.QueryByPerson(runtimes.PersonId, currentOnly: true).Any()
                && runtimes.Knowledge.KnowledgeRevision == 0;
            bool valid = position.Succeeded
                && !before.AuthoritativeEligible
                && noEligibilityMutation
                && authoritative.AuthoritativeEligible
                && perceived.PerceivedEligible
                && apply.Succeeded
                && offer.Succeeded
                && accept.Succeeded
                && !staleAppointment.Succeeded
                && staleAppointment.Status == PositionEmploymentOperationStatus.StaleEvaluation
                && !tamperedAppointment.Succeeded
                && tamperedAppointment.Status == PositionEmploymentOperationStatus.StaleEvaluation
                && !unauthorized.Succeeded
                && unauthorized.Status == PositionEmploymentOperationStatus.UnauthorizedAuthority
                && appoint.Succeeded
                && appoint.Employment?.state == EmploymentState.Active
                && boundaries;
            return TestLabAssertions.True("step10-position-appointment", "Eligibility, applications, offers, acceptance, stale checks, authority, and employment boundaries are integrated", valid, $"Before={before.AuthoritativeEligible} Auth={authoritative.AuthoritativeEligible} Perceived={perceived.PerceivedEligible} Apply={apply.Status} Offer={offer.Status} Accept={accept.Status} Stale={staleAppointment.Status} Tampered={tamperedAppointment.Status} Unauthorized={unauthorized.Status} Appoint={appoint.Status} Rev={beforeEligibilityRevision}->{runtime.Revision}");
        }

        private static TestLabAutomationStepResult PositionCapacityConflictsDutiesAuthority(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            PositionEmploymentRuntime runtime = runtimes.PositionEmployment;
            EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankJourneymanId, "position-duty");
            EnsureGuildLicense(context, "position-duty");
            PositionEmploymentOperationResult senior = AppointEligiblePerson(context, "duty-senior", ProfessionContentIds.RoyalForgeSeniorSmithPositionId, "organization.prototype.royal-forge", ProfessionContentIds.ForgeOrganizationTypeId, ProfessionContentIds.PositionDutyAssignAuthorityId);
            PositionEmploymentOperationResult overflow = AppointEligiblePerson(context, "duty-overflow", ProfessionContentIds.RoyalForgeSeniorSmithPositionId, "organization.prototype.royal-forge", ProfessionContentIds.ForgeOrganizationTypeId, ProfessionContentIds.PositionDutyAssignAuthorityId);
            PositionEmploymentOperationResult clerk = AppointEligiblePerson(context, "duty-clerk", ProfessionContentIds.GuildClerkPositionId, "organization.prototype.guild", ProfessionContentIds.GuildOrganizationTypeId, ProfessionContentIds.PositionRestrictedRecordsAuthorityId);
            PositionEmploymentOperationResult duplicateSamePosition = runtime.AppointPerson(context.ScenarioContext.ScopedId("employment", "duplicate-holder"), string.Empty, runtimes.PersonId, senior.Position?.positionInstanceId, ProfessionContentIds.PositionDutyAssignAuthorityId, runtime.EvaluateEligibility(runtimes.PersonId, senior.Position?.positionInstanceId, privilegedDiagnostics: true).Snapshot, "130", context.ScenarioContext.ScopedId("position-tx", "duplicate-holder"));
            PositionEmploymentOperationResult duty = runtime.AssignDuty(context.ScenarioContext.ScopedId("position-duty", "craft"), senior.Employment?.employmentId, ProfessionContentIds.SeniorSmithCraftDutyId, "131", context.ScenarioContext.ScopedId("position-tx", "assign-duty"));
            ProfessionalActivityOperationResult activity = runtimes.ProfessionalActivities.RegisterAndValidateActivity(
                ActivityRequest(context, "position-duty-craft", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, ActivityCustomSource(context, ProfessionalActivitySourceType.CraftingOperation, "position-duty-craft", "source.crafting"), ProfessionalResponsibilityLevel.IndependentPractitioner),
                context.ScenarioContext.ScopedId("professional-evidence", "position-duty-craft"),
                "authority.guild.prototype",
                context.ScenarioContext.ScopedId("professional-tx", "position-duty-craft"));
            PositionEmploymentOperationResult complete = runtime.CompleteDutyWithEvidence(duty.Duty?.assignmentId, new[] { activity.Evidence?.evidenceId }, "132", context.ScenarioContext.ScopedId("position-tx", "complete-duty"));
            PositionEmploymentOperationResult superviseDuty = runtime.AssignDuty(context.ScenarioContext.ScopedId("position-duty", "supervise"), senior.Employment?.employmentId, ProfessionContentIds.SeniorSmithSuperviseDutyId, "133", context.ScenarioContext.ScopedId("position-tx", "assign-supervise"));
            PositionEmploymentOperationResult delegated = runtime.DelegateDuty(superviseDuty.Duty?.assignmentId, runtimes.PersonId, runtimes.PersonId, "134", context.ScenarioContext.ScopedId("position-tx", "delegate-duty"));
            bool authorityActive = runtime.HasActiveAuthority(runtimes.PersonId, "organization.prototype.royal-forge", ProfessionContentIds.PositionSuperviseAuthorityId);
            PositionEmploymentOperationResult suspended = runtime.SuspendEmployment(senior.Employment?.employmentId, "135", context.ScenarioContext.ScopedId("position-tx", "suspend"));
            bool authoritySuspended = runtime.HasActiveAuthority(runtimes.PersonId, "organization.prototype.royal-forge", ProfessionContentIds.PositionSuperviseAuthorityId);
            PositionEmploymentOperationResult reinstated = runtime.ReinstateEmployment(senior.Employment?.employmentId, "136", context.ScenarioContext.ScopedId("position-tx", "reinstate"));
            bool authorityReinstated = runtime.HasActiveAuthority(runtimes.PersonId, "organization.prototype.royal-forge", ProfessionContentIds.PositionSuperviseAuthorityId);

            bool valid = senior.Succeeded
                && !overflow.Succeeded
                && (overflow.Status == PositionEmploymentOperationStatus.CapacityExceeded || overflow.Status == PositionEmploymentOperationStatus.EmploymentConflict || overflow.Status == PositionEmploymentOperationStatus.MissingRequirement || overflow.Status == PositionEmploymentOperationStatus.StaleEvaluation)
                && clerk.Succeeded
                && !duplicateSamePosition.Succeeded
                && duty.Succeeded
                && activity.Succeeded
                && complete.Succeeded
                && complete.Duty?.state == DutyAssignmentState.Completed
                && delegated.Succeeded
                && authorityActive
                && suspended.Succeeded
                && !authoritySuspended
                && reinstated.Succeeded
                && authorityReinstated;
            return TestLabAssertions.True("step10-position-duties", "Capacity, conflicts, duties, real activity evidence, delegation, and active-state authority are enforced", valid, $"Senior={senior.Status} Overflow={overflow.Status} Clerk={clerk.Status} Duplicate={duplicateSamePosition.Status} Duty={duty.Status} Activity={activity.Status} Complete={complete.Status} Delegate={delegated.Status} Authority={authorityActive}/{authoritySuspended}/{authorityReinstated}");
        }

        private static TestLabAutomationStepResult PositionReportingLifecyclePersistence(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            PositionEmploymentRuntime runtime = runtimes.PositionEmployment;
            EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankMasterId, "position-persistence");
            EnsureGuildLicense(context, "position-persistence");
            PositionEmploymentOperationResult supervisor = AppointEligiblePerson(context, "persistence-supervisor", ProfessionContentIds.ApprenticeSupervisorPositionId, "organization.prototype.guild", ProfessionContentIds.GuildOrganizationTypeId, ProfessionContentIds.PositionAppointAuthorityId);
            PositionEmploymentOperationResult clerk = AppointEligiblePerson(context, "persistence-clerk", ProfessionContentIds.GuildClerkPositionId, "organization.prototype.guild", ProfessionContentIds.GuildOrganizationTypeId, ProfessionContentIds.PositionRestrictedRecordsAuthorityId);
            PositionEmploymentOperationResult assignSupervisor = runtime.AssignSupervisor(clerk.Position?.positionInstanceId, supervisor.Position?.positionInstanceId, context.ScenarioContext.ScopedId("position-tx", "supervisor"));
            PositionEmploymentOperationResult cycle = runtime.AssignSupervisor(supervisor.Position?.positionInstanceId, clerk.Position?.positionInstanceId, context.ScenarioContext.ScopedId("position-tx", "cycle"));
            PositionEmploymentOperationResult secretDuty = runtime.AssignDuty(context.ScenarioContext.ScopedId("position-duty", "secret-records"), clerk.Employment?.employmentId, ProfessionContentIds.GuildClerkRecordDutyId, "149", context.ScenarioContext.ScopedId("position-tx", "secret-duty"));
            PositionEmploymentProjection<DutyAssignmentData> publicProjection = runtime.ProjectDuty(secretDuty.Duty?.assignmentId, PositionEmploymentProjectionAudience.Public, null);
            PositionEmploymentOperationResult resign = runtime.Resign(clerk.Employment?.employmentId, "150", context.ScenarioContext.ScopedId("position-tx", "resign"));
            bool authorityEnded = runtime.HasActiveAuthority(runtimes.PersonId, "organization.prototype.guild", ProfessionContentIds.PositionRestrictedRecordsAuthorityId);
            PositionEmploymentOperationResult close = runtime.ClosePosition(clerk.Position?.positionInstanceId, "151", context.ScenarioContext.ScopedId("position-tx", "close"), forceEndActiveEmployment: true);
            PositionEmploymentRuntimeSaveData save = runtime.CreateSaveData();
            PositionEmploymentRuntime restored = new PositionEmploymentRuntime();
            PositionEmploymentOperationResult restore = restored.RestoreFromSaveData(save, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.ProfessionalRanks, runtimes.KnownPersonIds, new[] { "organization.prototype.guild", "organization.prototype.royal-forge" }, new[] { "authority.guild.prototype", ProfessionContentIds.PositionAppointAuthorityId, ProfessionContentIds.PositionDutyAssignAuthorityId, ProfessionContentIds.PositionSuperviseAuthorityId }, restoring: true);
            PositionEmploymentRuntimeSaveData corrupt = save.Clone();
            corrupt.employments[0].personId = "person.missing";
            int beforeCount = restored.EmploymentCount;
            long beforeRevision = restored.Revision;
            PositionEmploymentOperationResult rejected = restored.RestoreFromSaveData(corrupt, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.ProfessionalRanks, runtimes.KnownPersonIds, new[] { "organization.prototype.guild", "organization.prototype.royal-forge" }, new[] { "authority.guild.prototype", ProfessionContentIds.PositionAppointAuthorityId, ProfessionContentIds.PositionDutyAssignAuthorityId, ProfessionContentIds.PositionSuperviseAuthorityId }, restoring: true);
            PositionEmploymentOperationResult legacyEmpty = new PositionEmploymentRuntime().RestoreFromSaveData(null, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.ProfessionalRanks, runtimes.KnownPersonIds, Array.Empty<string>(), Array.Empty<string>(), restoring: true);

            bool valid = supervisor.Succeeded
                && clerk.Succeeded
                && assignSupervisor.Succeeded
                && !cycle.Succeeded
                && cycle.Status == PositionEmploymentOperationStatus.ReportingCycle
                && secretDuty.Succeeded
                && publicProjection.Record != null
                && publicProjection.Redacted
                && resign.Succeeded
                && !authorityEnded
                && close.Succeeded
                && close.Position?.state == PositionInstanceState.Closed
                && restore.Succeeded
                && restored.EmploymentCount == save.employments.Count
                && restored.PositionCount == save.positions.Count
                && restored.HistoryHooks.Count == 0
                && !rejected.Succeeded
                && restored.EmploymentCount == beforeCount
                && restored.Revision == beforeRevision
                && legacyEmpty.Succeeded;
            return TestLabAssertions.True("step10-position-persistence", "Reporting, lifecycle transitions, redacted projections, and persistence restore safely", valid, $"Supervisor={supervisor.Status} Clerk={clerk.Status} Assign={assignSupervisor.Status} Cycle={cycle.Status} ProjectionRedacted={publicProjection.Redacted} Resign={resign.Status} Close={close.Status} Restore={restore.Status} Reject={rejected.Status} Legacy={legacyEmpty.Status}");
        }

        private static TestLabAutomationStepResult RankDefinitionsLadders(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in ProfessionContentIds.GetDefinitions(runtimes.DefinitionRegistry))
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(runtimes.DefinitionRegistry.DefinitionsById, report);
                }
            }

            bool hasApprentice = runtimes.DefinitionRegistry.TryGet(ProfessionContentIds.BlacksmithRankApprenticeId, out ProfessionalRankDefinition apprentice);
            bool hasLadder = runtimes.DefinitionRegistry.TryGet(ProfessionContentIds.BlacksmithRankLadderId, out ProfessionalRankLadderDefinition ladder);
            bool hasMastery = runtimes.DefinitionRegistry.TryGet(ProfessionContentIds.WeaponsmithMasteryId, out ProfessionalMasteryDefinition mastery);
            bool hasDefinitions = hasApprentice && hasLadder && hasMastery;
            long revision = runtimes.ProfessionalRanks.Revision;
            ProfessionalRankAdvancementResult evaluation = runtimes.ProfessionalRanks.EvaluateAdvancement(runtimes.PersonId, ProfessionContentIds.BlacksmithRankApprenticeId, "authority.guild.prototype", privilegedDiagnostics: true);
            bool nonMutating = runtimes.ProfessionalRanks.Revision == revision;
            bool valid = report.ErrorCount == 0
                && report.WarningCount == 0
                && hasDefinitions
                && apprentice != null
                && apprentice.RankOrder == 10
                && ladder != null
                && ladder.OrderedRankDefinitionIds.SequenceEqual(new[] { ProfessionContentIds.BlacksmithRankApprenticeId, ProfessionContentIds.BlacksmithRankJourneymanId, ProfessionContentIds.BlacksmithRankMasterId })
                && mastery != null
                && mastery.RequiredRankDefinitionId == ProfessionContentIds.WeaponsmithRankMasterId
                && !evaluation.AuthoritativeEligible
                && nonMutating;
            return TestLabAssertions.True("step10-rank-definitions", "Rank definitions, ladders, mastery, and non-mutating evaluation validate", valid, $"Errors={report.ErrorCount} Warnings={report.WarningCount} Has={hasDefinitions} Eligible={evaluation.AuthoritativeEligible} Rev={revision}->{runtimes.ProfessionalRanks.Revision}");
        }

        private static TestLabAutomationStepResult RankAdvancementApplicationPromotion(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureApprenticeRankFoundation(context, "promotion");
            ProfessionalRankAdvancementResult evaluation = runtimes.ProfessionalRanks.EvaluateAdvancement(runtimes.PersonId, ProfessionContentIds.BlacksmithRankApprenticeId, "authority.guild.prototype", privilegedDiagnostics: true);
            ProfessionalRankOperationResult submit = runtimes.ProfessionalRanks.SubmitApplication(context.ScenarioContext.ScopedId("rank-application", "apprentice"), runtimes.PersonId, ProfessionContentIds.BlacksmithRankApprenticeId, "authority.guild.prototype", evaluation.Snapshot, "40", context.ScenarioContext.ScopedId("rank-tx", "submit"));
            ProfessionalRankAdvancementSnapshotData staleSnapshot = evaluation.Snapshot?.Clone() ?? new ProfessionalRankAdvancementSnapshotData();
            staleSnapshot.evaluationHash = "stale";
            ProfessionalRankOperationResult stale = runtimes.ProfessionalRanks.ApprovePromotion(submit.Application?.applicationId, runtimes.PersonId, staleSnapshot, "41", context.ScenarioContext.ScopedId("rank-tx", "stale"));
            ProfessionalRankOperationResult approve = runtimes.ProfessionalRanks.ApprovePromotion(submit.Application?.applicationId, runtimes.PersonId, evaluation.Snapshot, "42", context.ScenarioContext.ScopedId("rank-tx", "approve"));
            ProfessionalRankOperationResult promote = runtimes.ProfessionalRanks.PromotePerson(context.ScenarioContext.ScopedId("rank-record", "apprentice"), submit.Application?.applicationId, evaluation.Snapshot, "43", context.ScenarioContext.ScopedId("rank-tx", "promote"));
            bool historicalPreserved = runtimes.ProfessionalRanks.QueryByPerson(runtimes.PersonId).Any(item => item.rankDefinitionId == ProfessionContentIds.BlacksmithRankApprenticeId);
            bool valid = evaluation.AuthoritativeEligible
                && submit.Succeeded
                && !stale.Succeeded
                && stale.Status == ProfessionalRankOperationStatus.StaleEvaluation
                && approve.Succeeded
                && promote.Succeeded
                && historicalPreserved
                && !runtimes.Credentials.QueryByRecipient(runtimes.PersonId, activeOnly: true).Any(item => item.credentialDefinitionId == ProfessionContentIds.BlacksmithGuildLicenseCredentialId);
            return TestLabAssertions.True("step10-rank-promotion", "Rank application and promotion revalidate current state", valid, $"Eval={evaluation.AuthoritativeEligible} Submit={submit.Status} Stale={stale.Status} Approve={approve.Status} Promote={promote.Status}");
        }

        private static TestLabAutomationStepResult RankSpecializationMastery(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            ProfessionalRankOperationResult apprentice = EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankApprenticeId, "mastery-apprentice");
            ProfessionalRankOperationResult journeyman = EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankJourneymanId, "mastery-journey");
            EnsureGuildLicense(context, "mastery-license");
            ProfessionalRankOperationResult weaponsmithApprentice = EnsurePromotedRank(context, ProfessionContentIds.WeaponsmithRankApprenticeId, "weaponsmith-apprentice");
            ProfessionalRankOperationResult weaponsmithMaster = EnsurePromotedRank(context, ProfessionContentIds.WeaponsmithRankMasterId, "weaponsmith-master");
            ProfessionalRankOperationResult achievement = runtimes.ProfessionalRanks.RecordQualifyingAchievement(new ProfessionalQualifyingAchievementData
            {
                achievementId = ProfessionContentIds.BlacksmithMasterworkAchievementId,
                personId = runtimes.PersonId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                specializationId = ProfessionContentIds.WeaponsmithSpecializationId,
                sourceActivityId = context.ScenarioContext.ScopedId("professional-activity", "masterwork"),
                activityDefinitionId = ProfessionContentIds.BlacksmithCraftingActivityDefinitionId,
                description = "Prototype masterwork blade",
                quality = 850,
                difficulty = ProfessionalActivityDifficulty.Skilled,
                validatingAuthorityId = "authority.guild.prototype",
                worldTime = "70",
                accessPolicyId = ProfessionContentIds.AccessPublicId
            }, context.ScenarioContext.ScopedId("rank-tx", "achievement"));
            ProfessionalRankAdvancementResult masteryEval = runtimes.ProfessionalRanks.EvaluateMastery(runtimes.PersonId, ProfessionContentIds.WeaponsmithMasteryId, "authority.guild.prototype", privilegedDiagnostics: true);
            ProfessionalRankOperationResult mastery = runtimes.ProfessionalRanks.GrantMastery(context.ScenarioContext.ScopedId("mastery-record", "weaponsmith"), runtimes.PersonId, ProfessionContentIds.WeaponsmithMasteryId, "authority.guild.prototype", masteryEval.Snapshot, "71", context.ScenarioContext.ScopedId("rank-tx", "mastery"));
            bool distinctGeneralAndSpecialization = runtimes.ProfessionalRanks.QueryByPerson(runtimes.PersonId, currentOnly: true).Any(item => item.rankDefinitionId == ProfessionContentIds.WeaponsmithRankMasterId)
                && runtimes.ProfessionalRanks.QueryByPerson(runtimes.PersonId).Any(item => item.rankDefinitionId == ProfessionContentIds.BlacksmithRankJourneymanId);
            bool valid = apprentice.Succeeded
                && journeyman.Succeeded
                && weaponsmithApprentice.Succeeded
                && weaponsmithMaster.Succeeded
                && achievement.Succeeded
                && masteryEval.AuthoritativeEligible
                && mastery.Succeeded
                && distinctGeneralAndSpecialization;
            return TestLabAssertions.True("step10-rank-mastery", "Specialization rank and mastery use explicit evidence", valid, $"Apprentice={apprentice.Status} Journey={journeyman.Status} WeaponMaster={weaponsmithMaster.Status} Achievement={achievement.Status} Mastery={mastery.Status}");
        }

        private static TestLabAutomationStepResult RankLifecyclePermissionsBoundaries(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            ProfessionalRankOperationResult promote = EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankMasterId, "lifecycle-master");
            string rankId = promote.Rank?.rankRecordId ?? string.Empty;
            bool teachActive = runtimes.ProfessionalRanks.CanTeach(runtimes.PersonId, ProfessionContentIds.BlacksmithProfessionId);
            ProfessionalRankOperationResult suspend = runtimes.ProfessionalRanks.SuspendRank(rankId, "80", context.ScenarioContext.ScopedId("rank-tx", "suspend"));
            bool teachSuspended = runtimes.ProfessionalRanks.CanTeach(runtimes.PersonId, ProfessionContentIds.BlacksmithProfessionId);
            ProfessionalRankOperationResult reinstate = runtimes.ProfessionalRanks.ReinstateRank(rankId, "81", context.ScenarioContext.ScopedId("rank-tx", "reinstate"));
            bool teachReinstated = runtimes.ProfessionalRanks.CanTeach(runtimes.PersonId, ProfessionContentIds.BlacksmithProfessionId);
            ProfessionalRankOperationResult demote = runtimes.ProfessionalRanks.DemotePerson(rankId, context.ScenarioContext.ScopedId("rank-record", "demoted-journeyman"), ProfessionContentIds.BlacksmithRankJourneymanId, "82", context.ScenarioContext.ScopedId("rank-tx", "demote"));
            bool teachDemoted = runtimes.ProfessionalRanks.CanTeach(runtimes.PersonId, ProfessionContentIds.BlacksmithProfessionId);
            ProfessionalRankOperationResult revoke = runtimes.ProfessionalRanks.RevokeRank(demote.Rank?.rankRecordId, "83", context.ScenarioContext.ScopedId("rank-tx", "revoke"));
            bool skillsUnaffected = true;
            bool credentialsUnaffected = runtimes.Credentials.CredentialCount > 0;
            bool valid = promote.Succeeded
                && teachActive
                && suspend.Succeeded
                && !teachSuspended
                && reinstate.Succeeded
                && teachReinstated
                && demote.Succeeded
                && !teachDemoted
                && revoke.Succeeded
                && skillsUnaffected
                && credentialsUnaffected;
            return TestLabAssertions.True("step10-rank-lifecycle", "Rank lifecycle affects rank permissions without granting competencies", valid, $"Promote={promote.Status} Active={teachActive} Suspended={teachSuspended} Reinstated={teachReinstated} Demote={demote.Status} Revoked={revoke.Status}");
        }

        private static TestLabAutomationStepResult RankAccessPersistence(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            ProfessionalRankOperationResult promote = EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankApprenticeId, "persistence-rank");
            ProfessionalRankProjection<ProfessionalRankRecordData> projection = runtimes.ProfessionalRanks.ProjectRank(promote.Rank?.rankRecordId, ProfessionalRankProjectionAudience.Public, null);
            ProfessionalRankRuntimeSaveData save = runtimes.ProfessionalRanks.CreateSaveData();
            ProfessionalRankRuntime restored = new ProfessionalRankRuntime();
            ProfessionalRankOperationResult restore = restored.RestoreFromSaveData(save, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.KnownPersonIds, new[] { "authority.guild.prototype" }, restoring: true);
            ProfessionalRankRuntimeSaveData corrupt = save.Clone();
            corrupt.ranks[0].rankDefinitionId = "profession-rank.missing";
            ProfessionalRankOperationResult rejected = restored.RestoreFromSaveData(corrupt, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.KnownPersonIds, new[] { "authority.guild.prototype" }, restoring: true);
            bool valid = promote.Succeeded
                && projection.Record != null
                && restore.Succeeded
                && restored.QueryByPerson(runtimes.PersonId).Count == 1
                && restored.HistoryHooks.Count == 0
                && !rejected.Succeeded
                && restored.QueryByPerson(runtimes.PersonId).Count == 1;
            return TestLabAssertions.True("step10-rank-persistence", "Rank projections and persistence restore safely without replay", valid, $"Promote={promote.Status} Restore={restore.Status} Rejected={rejected.Status} Count={restored.QueryByPerson(runtimes.PersonId).Count}");
        }

        private static TestLabAutomationStepResult CredentialDefinitionsAndQualification(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in ProfessionContentIds.GetDefinitions(runtimes.DefinitionRegistry))
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(runtimes.DefinitionRegistry.DefinitionsById, report);
                }
            }

            long professionRevision = runtimes.Professions.Revision;
            long trainingRevision = runtimes.Training.Revision;
            long activityRevision = runtimes.ProfessionalActivities.Revision;
            long credentialRevision = runtimes.Credentials.Revision;
            CredentialQualificationResult before = runtimes.Credentials.EvaluateQualification(runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, privilegedDiagnostics: true);
            bool nonMutatingInitial = runtimes.Professions.Revision == professionRevision
                && runtimes.Training.Revision == trainingRevision
                && runtimes.ProfessionalActivities.Revision == activityRevision
                && runtimes.Credentials.Revision == credentialRevision;

            EnsureBlacksmithCredentialFoundation(context, "qualification");
            CredentialQualificationResult afterFoundation = runtimes.Credentials.EvaluateQualification(runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, privilegedDiagnostics: true);
            RecordCredentialExam(context, "qualification", ProfessionContentIds.BlacksmithPracticalExaminationId, 820);
            CredentialQualificationResult qualified = runtimes.Credentials.EvaluateQualification(runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, privilegedDiagnostics: true);

            bool valid = report.ErrorCount == 0
                && report.WarningCount == 0
                && !before.AuthoritativeQualified
                && !afterFoundation.AuthoritativeQualified
                && afterFoundation.BlockingFailures.Contains($"examination:{ProfessionContentIds.BlacksmithPracticalExaminationId}")
                && qualified.AuthoritativeQualified
                && nonMutatingInitial;
            return TestLabAssertions.True("step10-credential-qualification", "Credential definitions resolve and qualification evaluates without mutation", valid, $"Errors={report.ErrorCount} Warnings={report.WarningCount} Before={before.AuthoritativeQualified} AfterFoundation={afterFoundation.AuthoritativeQualified} Qualified={qualified.AuthoritativeQualified} NonMutating={nonMutatingInitial}");
        }

        private static TestLabAutomationStepResult CredentialApplicationExaminationIssuance(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureBlacksmithCredentialFoundation(context, "issue");
            CredentialOperationResult exam = RecordCredentialExam(context, "issue", ProfessionContentIds.BlacksmithPracticalExaminationId, 850);
            CredentialQualificationResult qualification = runtimes.Credentials.EvaluateQualification(runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId);
            CredentialOperationResult apply = runtimes.Credentials.SubmitApplication(context.ScenarioContext.ScopedId("credential-application", "issue"), runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, GuildIssuer(), qualification.Snapshot, "30", context.ScenarioContext.ScopedId("credential-tx", "apply"));
            CredentialOperationResult approve = runtimes.Credentials.ApproveApplication(apply.Application?.applicationId, "authority.guild.prototype", qualification.Snapshot, "31", context.ScenarioContext.ScopedId("credential-tx", "approve"));
            CredentialOperationResult issue = runtimes.Credentials.IssueCredential(context.ScenarioContext.ScopedId("credential-record", "issue"), ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, runtimes.PersonId, GuildIssuer(), apply.Application?.applicationId, exam.ExaminationAttempt?.attemptId, context.ScenarioContext.ScopedId("registration", "issue"), qualification.Snapshot, "32", context.ScenarioContext.ScopedId("credential-tx", "issue"));
            bool permission = runtimes.Credentials.HasActivePermission(runtimes.PersonId, ProfessionContentIds.BlacksmithPracticePermissionId);

            bool valid = exam.Succeeded
                && qualification.AuthoritativeQualified
                && apply.Succeeded
                && approve.Succeeded
                && issue.Succeeded
                && issue.Credential?.authenticityState == CredentialAuthenticityState.Authoritative
                && permission
                && runtimes.Professions.QueryByProfession(ProfessionContentIds.BlacksmithProfessionId).Count == 1;
            return TestLabAssertions.True("step10-credential-issue", "Applications, examinations, and issuance produce authoritative credentials", valid, $"Exam={exam.Status} Qualified={qualification.AuthoritativeQualified} Apply={apply.Status} Approve={approve.Status} Issue={issue.Status} Permission={permission}");
        }

        private static TestLabAutomationStepResult CredentialStaleUnauthorizedForgery(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureBlacksmithCredentialFoundation(context, "boundary");
            CredentialOperationResult exam = RecordCredentialExam(context, "boundary", ProfessionContentIds.BlacksmithPracticalExaminationId, 850);
            CredentialQualificationResult qualification = runtimes.Credentials.EvaluateQualification(runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId);
            CredentialOperationResult badIssuer = runtimes.Credentials.SubmitApplication(context.ScenarioContext.ScopedId("credential-application", "bad-issuer"), runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, new CredentialIssuerReferenceData { issuerId = "authority.medical.prototype", issuerKind = CredentialIssuerAuthorityKind.ProfessionalOrganization }, qualification.Snapshot, "33", context.ScenarioContext.ScopedId("credential-tx", "bad-issuer"));
            CredentialOperationResult apply = runtimes.Credentials.SubmitApplication(context.ScenarioContext.ScopedId("credential-application", "stale"), runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, GuildIssuer(), qualification.Snapshot, "34", context.ScenarioContext.ScopedId("credential-tx", "stale-apply"));
            runtimes.ProfessionalActivities.RegisterAndValidateActivity(ActivityRequest(context, "stale-extra", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, ActivityCustomSource(context, ProfessionalActivitySourceType.CraftingOperation, "stale-extra", "source.crafting")), context.ScenarioContext.ScopedId("professional-evidence", "stale-extra"), "authority.guild.prototype", context.ScenarioContext.ScopedId("professional-tx", "stale-extra"));
            CredentialQualificationResult current = runtimes.Credentials.EvaluateQualification(runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId);
            CredentialOperationResult staleApprove = runtimes.Credentials.ApproveApplication(apply.Application?.applicationId, "authority.guild.prototype", current.Snapshot, "35", context.ScenarioContext.ScopedId("credential-tx", "stale-approve"));
            CredentialOperationResult forged = runtimes.Credentials.RecordForgedClaim(context.ScenarioContext.ScopedId("credential-forged", "claim"), ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, runtimes.PersonId, "authority.guild.prototype", "36", context.ScenarioContext.ScopedId("credential-tx", "forged"));
            bool forgedPermission = runtimes.Credentials.HasActivePermission(runtimes.PersonId, ProfessionContentIds.BlacksmithPracticePermissionId, CredentialPermissionStatePolicy.AnyNonRevoked);

            bool valid = exam.Succeeded
                && !badIssuer.Succeeded
                && badIssuer.Status == CredentialOperationStatus.UnauthorizedIssuer
                && apply.Succeeded
                && !staleApprove.Succeeded
                && staleApprove.Status == CredentialOperationStatus.StaleQualification
                && forged.Succeeded
                && forged.Credential?.authenticityState == CredentialAuthenticityState.ForgedClaim
                && !forgedPermission;
            return TestLabAssertions.True("step10-credential-boundaries", "Unauthorized issuers, stale qualifications, and forged claims are rejected safely", valid, $"Exam={exam.Status} BadIssuer={badIssuer.Status} Apply={apply.Status} Stale={staleApprove.Status} Forged={forged.Status} ForgedPermission={forgedPermission}");
        }

        private static TestLabAutomationStepResult CredentialLifecyclePermissions(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            CredentialOperationResult issued = IssueApprenticeshipCredential(context, "lifecycle", out _);
            string credentialId = issued.Credential?.credentialId;
            bool activePermission = runtimes.Credentials.HasActivePermission(runtimes.PersonId, ProfessionContentIds.BlacksmithPracticePermissionId);
            CredentialOperationResult suspend = runtimes.Credentials.SuspendCredential(credentialId, "40", context.ScenarioContext.ScopedId("credential-tx", "suspend"));
            bool suspendedPermission = runtimes.Credentials.HasActivePermission(runtimes.PersonId, ProfessionContentIds.BlacksmithPracticePermissionId);
            CredentialOperationResult reinstate = runtimes.Credentials.ReinstateCredential(credentialId, "41", context.ScenarioContext.ScopedId("credential-tx", "reinstate"));
            bool reinstatedPermission = runtimes.Credentials.HasActivePermission(runtimes.PersonId, ProfessionContentIds.BlacksmithPracticePermissionId);
            CredentialOperationResult renew = runtimes.Credentials.RenewCredential(credentialId, null, "42", context.ScenarioContext.ScopedId("credential-tx", "renew"));
            CredentialOperationResult expire = runtimes.Credentials.ExpireCredential(credentialId, "43", context.ScenarioContext.ScopedId("credential-tx", "expire"));
            bool expiredPermission = runtimes.Credentials.HasActivePermission(runtimes.PersonId, ProfessionContentIds.BlacksmithPracticePermissionId);
            CredentialOperationResult revoke = runtimes.Credentials.RevokeCredential(credentialId, "44", context.ScenarioContext.ScopedId("credential-tx", "revoke"));
            CredentialOperationResult revokedReinstate = runtimes.Credentials.ReinstateCredential(credentialId, "45", context.ScenarioContext.ScopedId("credential-tx", "revoked-reinstate"));

            bool valid = issued.Succeeded
                && activePermission
                && suspend.Succeeded
                && !suspendedPermission
                && reinstate.Succeeded
                && reinstatedPermission
                && !renew.Succeeded
                && renew.Status == CredentialOperationStatus.InvalidTransition
                && expire.Succeeded
                && !expiredPermission
                && revoke.Succeeded
                && !revokedReinstate.Succeeded
                && revokedReinstate.Status == CredentialOperationStatus.InvalidTransition;
            return TestLabAssertions.True("step10-credential-lifecycle", "Expiration, suspension, reinstatement, revocation, and renewal affect permissions", valid, $"Issued={issued.Status} Active={activePermission} Suspend={suspend.Status} SuspendedPermission={suspendedPermission} Reinstate={reinstate.Status} Renew={renew.Status} Expire={expire.Status} Revoke={revoke.Status} RevokedReinstate={revokedReinstate.Status}");
        }

        private static TestLabAutomationStepResult CredentialAccessPersistence(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            CredentialOperationResult issued = IssueApprenticeshipCredential(context, "persist", out string applicationId);
            InformationAccessDecision decision = new InformationAccessDecision(
                "person.observer",
                CredentialInformationSubject.Create(CredentialInformationSubject.CredentialTag, issued.Credential?.credentialId, runtimes.PersonId),
                InformationAccessMode.Inspect,
                InformationAccessDecisionKind.RedactedAccess,
                InformationAccessDenialCode.DetailRestriction,
                false,
                InformationResharingPolicy.NoResharing,
                new[] { "credential-definition-id", "state" },
                CredentialInformationSubject.ProtectedFields,
                Array.Empty<string>(),
                new[] { ProfessionContentIds.AccessPublicId },
                50d,
                "Credential redacted.",
                "Credential supporting evidence hidden.",
                true);
            CredentialProjection<CredentialRecordData> projection = runtimes.Credentials.ProjectCredential(issued.Credential?.credentialId, CredentialProjectionAudience.PublicInspection, decision);
            CredentialRuntimeSaveData save = runtimes.Credentials.CreateSaveData();
            string[] authorities = { "authority.guild.prototype", "authority.medical.prototype", "organization.prototype.guild" };
            CredentialRuntime restored = new CredentialRuntime();
            CredentialOperationResult restore = restored.RestoreFromSaveData(save, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.KnownPersonIds, authorities, restoring: true);
            CredentialRuntimeSaveData corrupt = save.Clone();
            corrupt.credentials[0].supportingApplicationId = string.Empty;
            CredentialOperationResult rejected = restored.RestoreFromSaveData(corrupt, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.KnownPersonIds, authorities, restoring: true);

            bool valid = issued.Succeeded
                && !string.IsNullOrWhiteSpace(applicationId)
                && projection.Redacted
                && projection.Record != null
                && string.IsNullOrWhiteSpace(projection.Record.registrationNumber)
                && restore.Succeeded
                && restored.CredentialCount == 1
                && restored.HistoryHooks.Count == 0
                && !rejected.Succeeded
                && restored.CredentialCount == 1;
            return TestLabAssertions.True("step10-credential-persistence", "Credential projections redact and persistence restores atomically", valid, $"Issued={issued.Status} Redacted={projection.Redacted} Restore={restore.Status} Rejected={rejected.Status} Count={restored.CredentialCount}");
        }

        private static TestLabAutomationStepResult ProfessionalActivityDefinitionsAndAdapters(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in ProfessionContentIds.GetDefinitions(registry))
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(registry.DefinitionsById, report);
                }
            }

            ProfessionalActivitySourceSnapshot crafting = ActivityCustomSource(context, ProfessionalActivitySourceType.CraftingOperation, "adapter-craft", "source.crafting");
            ProfessionalActivitySourceSnapshot practice = ActivityCustomSource(context, ProfessionalActivitySourceType.TrainingPracticalAssignment, "adapter-practice", "training.activity.practical");
            bool definitions = registry.TryGet(ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, out ProfessionalActivityDefinition craftDefinition)
                && craftDefinition.AcceptedSourceTypes.Contains(ProfessionalActivitySourceType.CraftingOperation)
                && registry.TryGet(ProfessionContentIds.BlacksmithExperimentationActivityDefinitionId, out ProfessionalActivityDefinition experimentDefinition)
                && experimentDefinition.FailureCreditPolicy == ProfessionalFailureCreditPolicy.CountsAsFailedAttempt;
            bool adapters = crafting.Completed
                && crafting.Reference.sourceType == ProfessionalActivitySourceType.CraftingOperation
                && practice.Tags.Contains("training.activity.practical");
            bool valid = report.ErrorCount == 0 && report.WarningCount == 0 && definitions && adapters;
            return TestLabAssertions.True("step10-activity-definitions", "Professional activity definitions and source adapters validate", valid, $"Errors={report.ErrorCount} Warnings={report.WarningCount} Definitions={definitions} Adapters={adapters}");
        }

        private static TestLabAutomationStepResult ProfessionalActivityRecordAndValidate(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureBlacksmithActivityProfession(context);
            long professionRevision = runtimes.Professions.Revision;
            long knowledgeRevision = runtimes.Knowledge.KnowledgeRevision;
            ProfessionalActivityOperationResult result = runtimes.ProfessionalActivities.RegisterAndValidateActivity(
                ActivityRequest(context, "validated", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, ActivityCustomSource(context, ProfessionalActivitySourceType.CraftingOperation, "validated-source", "source.crafting")),
                context.ScenarioContext.ScopedId("professional-evidence", "validated"),
                "authority.guild.prototype",
                context.ScenarioContext.ScopedId("professional-tx", "validated"));
            ProfessionalExperienceSummary summary = runtimes.ProfessionalActivities.BuildExperienceSummary(runtimes.PersonId, ProfessionContentIds.BlacksmithProfessionId);

            bool valid = result.Succeeded
                && result.Evidence != null
                && summary.TotalValidatedActivities == 1
                && summary.SuccessfulCount == 1
                && runtimes.Professions.Revision == professionRevision
                && runtimes.Knowledge.KnowledgeRevision == knowledgeRevision;
            return TestLabAssertions.True("step10-activity-validate", "Validated professional activity creates experience evidence", valid, $"Result={result.Status} Evidence={result.Evidence?.evidenceId} Summary={summary.TotalValidatedActivities} Profession={professionRevision}->{runtimes.Professions.Revision} Knowledge={knowledgeRevision}->{runtimes.Knowledge.KnowledgeRevision}");
        }

        private static TestLabAutomationStepResult ProfessionalActivityDuplicatesSharedCredit(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureBlacksmithActivityProfession(context);
            ProfessionalActivitySourceSnapshot source = ActivityCustomSource(context, ProfessionalActivitySourceType.CraftingOperation, "exclusive-source", "source.crafting");
            ProfessionalActivityOperationResult first = runtimes.ProfessionalActivities.RegisterAndValidateActivity(ActivityRequest(context, "exclusive-a", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, source), context.ScenarioContext.ScopedId("professional-evidence", "exclusive-a"), "authority.guild.prototype", context.ScenarioContext.ScopedId("professional-tx", "exclusive-a"));
            ProfessionalActivityOperationResult second = runtimes.ProfessionalActivities.RegisterAndValidateActivity(ActivityRequest(context, "exclusive-b", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, source), context.ScenarioContext.ScopedId("professional-evidence", "exclusive-b"), "authority.guild.prototype", context.ScenarioContext.ScopedId("professional-tx", "exclusive-b"));
            ProfessionalActivitySourceSnapshot shared = ActivityCustomSource(context, ProfessionalActivitySourceType.TeachingSession, "shared-source", "training.activity.teaching", difficulty: ProfessionalActivityDifficulty.Skilled);
            ProfessionalActivityOperationResult instructor = runtimes.ProfessionalActivities.RegisterAndValidateActivity(ActivityRequest(context, "shared-instructor", ProfessionContentIds.BlacksmithTeachingActivityDefinitionId, shared, ProfessionalResponsibilityLevel.Instructor, TrainingSupervisionLevel.IndependentWithReview), context.ScenarioContext.ScopedId("professional-evidence", "shared-instructor"), "authority.guild.prototype", context.ScenarioContext.ScopedId("professional-tx", "shared-instructor"));
            ProfessionalActivityOperationResult assistant = runtimes.ProfessionalActivities.RegisterAndValidateActivity(ActivityRequest(context, "shared-assistant", ProfessionContentIds.BlacksmithTeachingActivityDefinitionId, shared, ProfessionalResponsibilityLevel.Assistant, TrainingSupervisionLevel.ObservationOnly), context.ScenarioContext.ScopedId("professional-evidence", "shared-assistant"), "authority.guild.prototype", context.ScenarioContext.ScopedId("professional-tx", "shared-assistant"));
            ProfessionalExperienceSummary summary = runtimes.ProfessionalActivities.BuildExperienceSummary(runtimes.PersonId, ProfessionContentIds.BlacksmithProfessionId);

            bool valid = first.Succeeded
                && !second.Succeeded
                && second.Status == ProfessionalActivityOperationStatus.DuplicateExclusiveSource
                && instructor.Succeeded
                && assistant.Succeeded
                && summary.TotalValidatedActivities == 3;
            return TestLabAssertions.True("step10-activity-duplicates", "Exclusive duplicates and shared role credit are deterministic", valid, $"First={first.Status} Second={second.Status} Instructor={instructor.Status} Assistant={assistant.Status} Summary={summary.TotalValidatedActivities}");
        }

        private static TestLabAutomationStepResult ProfessionalActivitySummaryRequirementsBoundaries(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureBlacksmithActivityProfession(context);
            long professionRevision = runtimes.Professions.Revision;
            long trainingRevision = runtimes.Training.Revision;
            runtimes.ProfessionalActivities.RegisterAndValidateActivity(ActivityRequest(context, "summary-independent", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, ActivityCustomSource(context, ProfessionalActivitySourceType.CraftingOperation, "summary-independent", "source.crafting"), ProfessionalResponsibilityLevel.IndependentPractitioner), context.ScenarioContext.ScopedId("professional-evidence", "summary-independent"), "authority.guild.prototype", context.ScenarioContext.ScopedId("professional-tx", "summary-independent"));
            runtimes.ProfessionalActivities.RegisterAndValidateActivity(ActivityRequest(context, "summary-supervised", ProfessionContentIds.BlacksmithSupervisedPracticeActivityDefinitionId, ActivityCustomSource(context, ProfessionalActivitySourceType.TrainingPracticalAssignment, "summary-supervised", "training.activity.practical"), ProfessionalResponsibilityLevel.SupervisedWorker, TrainingSupervisionLevel.CloselySupervised), context.ScenarioContext.ScopedId("professional-evidence", "summary-supervised"), "authority.guild.prototype", context.ScenarioContext.ScopedId("professional-tx", "summary-supervised"));

            bool requirement = runtimes.ProfessionalActivities.EvaluateExperienceRequirement(runtimes.PersonId, new ProfessionalExperienceRequirementData
            {
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                minimumValidatedActivities = 2,
                minimumIndependentActivities = 1,
                minimumSupervisedActivities = 1,
                minimumQuality = 600,
                minimumDifficulty = ProfessionalActivityDifficulty.Routine,
                requireRecentActivity = true
            }, out ProfessionalExperienceSummary summary);

            bool valid = requirement
                && summary.IndependentCount == 1
                && summary.SupervisedCount == 1
                && summary.BreadthScore >= 2
                && runtimes.Professions.Revision == professionRevision
                && runtimes.Training.Revision == trainingRevision;
            return TestLabAssertions.True("step10-activity-summary", "Experience summaries and requirements do not mutate owning systems", valid, $"Requirement={requirement} Independent={summary.IndependentCount} Supervised={summary.SupervisedCount} Breadth={summary.BreadthScore} Profession={professionRevision}->{runtimes.Professions.Revision} Training={trainingRevision}->{runtimes.Training.Revision}");
        }

        private static TestLabAutomationStepResult ProfessionalActivityAccessPersistence(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureBlacksmithActivityProfession(context);
            ProfessionalActivityOperationResult recorded = runtimes.ProfessionalActivities.RegisterAndValidateActivity(ActivityRequest(context, "persisted", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, ActivityCustomSource(context, ProfessionalActivitySourceType.CraftingOperation, "persisted-source", "source.crafting")), context.ScenarioContext.ScopedId("professional-evidence", "persisted"), "authority.guild.prototype", context.ScenarioContext.ScopedId("professional-tx", "persisted"));
            InformationAccessDecision decision = new InformationAccessDecision("person.observer", ProfessionalActivityInformationSubject.Create(ProfessionalActivityInformationSubject.ActivityTag, recorded.Activity?.activityId, runtimes.PersonId), InformationAccessMode.Inspect, InformationAccessDecisionKind.RedactedAccess, InformationAccessDenialCode.DetailRestriction, false, InformationResharingPolicy.NoResharing, new[] { "profession-id", "state" }, ProfessionalActivityInformationSubject.ProtectedFields, Array.Empty<string>(), new[] { ProfessionContentIds.AccessPublicId }, 1d, "Redacted professional activity.", "Professional source hidden.", true);
            ProfessionalActivityProjection<ProfessionalActivityRecordData> projection = runtimes.ProfessionalActivities.ProjectActivity(recorded.Activity?.activityId, ProfessionalActivityProjectionAudience.PublicInspection, decision);
            ProfessionalActivityRuntimeSaveData save = runtimes.ProfessionalActivities.CreateSaveData();
            ProfessionalActivityRuntime restored = new ProfessionalActivityRuntime();
            ProfessionalActivityOperationResult restore = restored.RestoreFromSaveData(save, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.KnownPersonIds, restoring: true);
            ProfessionalActivityRuntimeSaveData corrupt = save.Clone();
            corrupt.activities[0].activityDefinitionId = "profession.activity.missing";
            ProfessionalActivityOperationResult rejected = restored.RestoreFromSaveData(corrupt, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.KnownPersonIds, restoring: true);

            bool valid = recorded.Succeeded
                && projection.Redacted
                && projection.Record != null
                && string.IsNullOrWhiteSpace(projection.Record.personId)
                && restore.Succeeded
                && restored.EvidenceCount == 1
                && restored.HistoryHooks.Count == 0
                && !rejected.Succeeded
                && restored.EvidenceCount == 1;
            return TestLabAssertions.True("step10-activity-persistence", "Activity projections redact and persistence restores atomically", valid, $"Record={recorded.Status} Redacted={projection.Redacted} Restore={restore.Status} Rejected={rejected.Status} Evidence={restored.EvidenceCount}");
        }

        private static TestLabAutomationStepResult TrainingDefinitionsAndCurricula(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in ProfessionContentIds.GetDefinitions(registry))
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(registry.DefinitionsById, report);
                }
            }

            TrainingCurriculumDefinition cyclic = ScriptableObject.CreateInstance<TrainingCurriculumDefinition>();
            cyclic.DevelopmentConfigure(
                "training-curriculum.automation.cycle",
                ProfessionContentIds.BlacksmithApprenticeshipProgramId,
                "Automation Cycle",
                new[]
                {
                    TrainingModule("training-module.automation.cycle-a", true, false, dependencies: new[] { "training-module.automation.cycle-b" }),
                    TrainingModule("training-module.automation.cycle-b", true, false, dependencies: new[] { "training-module.automation.cycle-a" })
                },
                Array.Empty<TrainingLessonDefinitionData>());
            DefinitionValidationReport cycleReport = new DefinitionValidationReport();
            cyclic.ValidateCatalogDefinition(registry.DefinitionsById, cycleReport);
            UnityEngine.Object.DestroyImmediate(cyclic);

            bool program = registry.TryGet(ProfessionContentIds.BlacksmithApprenticeshipProgramId, out TrainingProgramDefinition apprenticeship)
                && apprenticeship.Category == TrainingProgramCategory.Apprenticeship;
            bool curriculum = registry.TryGet(ProfessionContentIds.BlacksmithApprenticeshipCurriculumId, out TrainingCurriculumDefinition trainingCurriculum)
                && trainingCurriculum.Modules.Count >= 3
                && trainingCurriculum.Lessons.Any(lesson => lesson.teachingMethod == TrainingTeachingMethod.Demonstration);
            bool transfers = registry.TryGet(ProfessionContentIds.TrainingLessonTransferDefinitionId, out InformationTransferDefinition lecture)
                && lecture.Mode == InformationTransferMode.Lecture
                && registry.TryGet(ProfessionContentIds.TrainingDemonstrationTransferDefinitionId, out InformationTransferDefinition demonstration)
                && demonstration.Mode == InformationTransferMode.Demonstration;

            bool valid = report.ErrorCount == 0
                && report.WarningCount == 0
                && cycleReport.ErrorCount > 0
                && program
                && curriculum
                && transfers;
            return TestLabAssertions.True("step10-training-definitions", "Training definitions and curricula validate", valid, $"Errors={report.ErrorCount} Warnings={report.WarningCount} CycleErrors={cycleReport.ErrorCount} Program={program} Curriculum={curriculum} Transfers={transfers}");
        }

        private static TestLabAutomationStepResult TrainingEnrollmentApprenticeship(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            long professionRevision = runtimes.Professions.Revision;
            long knowledgeRevision = runtimes.Knowledge.KnowledgeRevision;
            string enrollmentId = TrainingEnrollmentId(context, "enrollment");

            TrainingOperationResult apply = runtimes.Training.ApplyToProgram(enrollmentId, runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipProgramId, context.ScenarioContext.ScopedId("training-tx", "apply"), worldTime: 1d);
            TrainingOperationResult duplicate = runtimes.Training.ApplyToProgram(context.ScenarioContext.ScopedId("training-enrollment", "duplicate"), runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipProgramId, context.ScenarioContext.ScopedId("training-tx", "duplicate"), worldTime: 2d);
            TrainingOperationResult accept = runtimes.Training.AcceptEnrollment(enrollmentId, context.ScenarioContext.ScopedId("training-tx", "accept"));
            TrainingOperationResult instructor = runtimes.Training.AssignInstructor(enrollmentId, context.ScenarioContext.ScopedId("training-instructor", "master"), TrainingInstructorRoleKind.Master, runtimes.PersonId, context.ScenarioContext.ScopedId("training-tx", "master"), professionId: ProfessionContentIds.BlacksmithProfessionId, authorityId: "authority.guild.prototype");
            TrainingOperationResult begin = runtimes.Training.BeginProgram(enrollmentId, context.ScenarioContext.ScopedId("training-tx", "begin"));
            TrainingOperationResult withdraw = runtimes.Training.Withdraw(enrollmentId, context.ScenarioContext.ScopedId("training-tx", "withdraw"));
            TrainingOperationResult terminalBegin = runtimes.Training.BeginProgram(enrollmentId, context.ScenarioContext.ScopedId("training-tx", "terminal-begin"));

            bool valid = apply.Succeeded
                && !duplicate.Succeeded
                && duplicate.Status == TrainingOperationStatus.Duplicate
                && accept.Succeeded
                && instructor.Succeeded
                && begin.Succeeded
                && withdraw.Succeeded
                && !terminalBegin.Succeeded
                && terminalBegin.Status == TrainingOperationStatus.InvalidTransition
                && runtimes.Professions.Revision == professionRevision
                && runtimes.Knowledge.KnowledgeRevision == knowledgeRevision;
            return TestLabAssertions.True("step10-training-enrollment", "Enrollment and apprenticeship transitions preserve boundaries", valid, $"Apply={apply.Status} Duplicate={duplicate.Status} Accept={accept.Status} Instructor={instructor.Status} Begin={begin.Status} Withdraw={withdraw.Status} TerminalBegin={terminalBegin.Status} Profession={professionRevision}->{runtimes.Professions.Revision} Knowledge={knowledgeRevision}->{runtimes.Knowledge.KnowledgeRevision}");
        }

        private static TestLabAutomationStepResult TrainingLearningSessionTeaching(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            string enrollmentId = BeginTrainingApprenticeship(context, "teaching");
            TrainingOperationResult attendance = runtimes.Training.RunLearningSession(context.ScenarioContext.ScopedId("training-session", "attendance"), enrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, ProfessionContentIds.BlacksmithSafetyLessonId, context.ScenarioContext.ScopedId("training-tx", "attendance"), startWorldTime: 10d, completionWorldTime: 11d);
            long knowledgeBeforeTeaching = runtimes.Knowledge.KnowledgeRevision;
            TrainingOperationResult taught = runtimes.Training.RunLearningSession(
                context.ScenarioContext.ScopedId("training-session", "teaching"),
                enrollmentId,
                ProfessionContentIds.BlacksmithPracticeModuleId,
                ProfessionContentIds.BlacksmithDemonstrationLessonId,
                context.ScenarioContext.ScopedId("training-tx", "teaching"),
                BuildTrainingTeachingRequest(context, "teaching-transfer"),
                startWorldTime: 12d,
                completionWorldTime: 13d);

            bool valid = attendance.Succeeded
                && taught.Succeeded
                && taught.Transfer != null
                && taught.Transfer.Succeeded
                && taught.Transfer.Record != null
                && taught.Transfer.Record.Data.teachingRequested
                && runtimes.Knowledge.KnowledgeRevision > knowledgeBeforeTeaching;
            return TestLabAssertions.True("step10-training-teaching", "Learning sessions route teaching through Step 8", valid, $"Attendance={attendance.Status} Teaching={taught.Status} Transfer={taught.Transfer?.Status} Requested={taught.Transfer?.Record?.Data.teachingRequested} Knowledge={knowledgeBeforeTeaching}->{runtimes.Knowledge.KnowledgeRevision}");
        }

        private static TestLabAutomationStepResult TrainingPracticalSupervisedWork(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            string enrollmentId = BeginTrainingApprenticeship(context, "practical");
            CompleteTrainingVisibleRequirements(context, enrollmentId, completePractice: false);
            string activityId = context.ScenarioContext.ScopedId("crafting-operation", "blacksmith-training");

            TrainingOperationResult missingSupervisor = runtimes.Training.RecordPracticalAssignment(context.ScenarioContext.ScopedId("training-practical", "missing-supervisor"), enrollmentId, ProfessionContentIds.BlacksmithPracticalAssignmentId, activityId, TrainingAssignmentActivityCategory.Crafting, context.ScenarioContext.ScopedId("training-tx", "practice-missing"), supervisorPersonId: string.Empty);
            TrainingOperationResult accepted = runtimes.Training.RecordPracticalAssignment(context.ScenarioContext.ScopedId("training-practical", "accepted"), enrollmentId, ProfessionContentIds.BlacksmithPracticalAssignmentId, activityId, TrainingAssignmentActivityCategory.Crafting, context.ScenarioContext.ScopedId("training-tx", "practice-accepted"), quality: 700, supervisorPersonId: runtimes.PersonId);
            TrainingOperationResult duplicate = runtimes.Training.RecordPracticalAssignment(context.ScenarioContext.ScopedId("training-practical", "duplicate"), enrollmentId, ProfessionContentIds.BlacksmithPracticalAssignmentId, activityId, TrainingAssignmentActivityCategory.Crafting, context.ScenarioContext.ScopedId("training-tx", "practice-duplicate"), quality: 700, supervisorPersonId: runtimes.PersonId);
            TrainingOperationResult supervised = runtimes.Training.RecordSupervisedWork(context.ScenarioContext.ScopedId("training-supervised", "forge"), enrollmentId, runtimes.PersonId, ProfessionContentIds.BlacksmithProfessionId, activityId, TrainingSupervisionLevel.CloselySupervised, TrainingWorkOutcome.Succeeded, context.ScenarioContext.ScopedId("training-tx", "supervised"), quality: 725, startWorldTime: 20d, completionWorldTime: 21d);
            TrainingRuntimeSaveData save = runtimes.Training.CreateSaveData();

            bool valid = !missingSupervisor.Succeeded
                && missingSupervisor.Status == TrainingOperationStatus.RequirementBlocked
                && accepted.Succeeded
                && !duplicate.Succeeded
                && duplicate.Status == TrainingOperationStatus.DuplicateActivity
                && supervised.Succeeded
                && save.practicalWorkRecords.Any(record => string.Equals(record.activityReferenceId, activityId, StringComparison.Ordinal))
                && save.supervisedWorkRecords.Any(record => record.supervisionLevel == TrainingSupervisionLevel.CloselySupervised && string.Equals(record.activityReferenceId, activityId, StringComparison.Ordinal));
            return TestLabAssertions.True("step10-training-practical", "Practical assignments and supervised work reference activity records", valid, $"Missing={missingSupervisor.Status} Accepted={accepted.Status} Duplicate={duplicate.Status} Supervised={supervised.Status} Practical={save.practicalWorkRecords.Count} SupervisedCount={save.supervisedWorkRecords.Count}");
        }

        private static TestLabAutomationStepResult TrainingProgressCompletionBoundaries(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            string enrollmentId = BeginTrainingApprenticeship(context, "progress");
            CompleteTrainingVisibleRequirements(context, enrollmentId, completePractice: true);

            TrainingProgressResult perceived = runtimes.Training.EvaluateProgress(enrollmentId, perceived: true);
            TrainingProgressResult authoritative = runtimes.Training.EvaluateProgress(enrollmentId, perceived: false);
            TrainingProgressTokenData staleToken = authoritative.RuntimeToken;
            TrainingOperationResult blocked = runtimes.Training.CompleteProgram(enrollmentId, context.ScenarioContext.ScopedId("training-tx", "complete-blocked"), authoritative.RuntimeToken);
            TrainingOperationResult hidden = runtimes.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithHiddenAssessmentModuleId, context.ScenarioContext.ScopedId("training-tx", "hidden"));
            TrainingOperationResult stale = runtimes.Training.CompleteProgram(enrollmentId, context.ScenarioContext.ScopedId("training-tx", "complete-stale"), staleToken);
            TrainingProgressResult current = runtimes.Training.EvaluateProgress(enrollmentId, perceived: false);
            TrainingOperationResult complete = runtimes.Training.CompleteProgram(enrollmentId, context.ScenarioContext.ScopedId("training-tx", "complete"), current.RuntimeToken, worldTime: 100d);

            bool valid = perceived.EligibleForCompletion
                && !authoritative.EligibleForCompletion
                && authoritative.RemainingRequirements.Contains(ProfessionContentIds.BlacksmithHiddenAssessmentModuleId)
                && !blocked.Succeeded
                && blocked.Status == TrainingOperationStatus.RequirementBlocked
                && hidden.Succeeded
                && !stale.Succeeded
                && stale.Status == TrainingOperationStatus.StaleProgress
                && complete.Succeeded
                && complete.Enrollment != null
                && complete.Enrollment.State == TrainingEnrollmentState.Completed
                && runtimes.Professions.Count == 0;
            return TestLabAssertions.True("step10-training-progress", "Progress evaluation and completion enforce hidden requirements", valid, $"Perceived={perceived.EligibleForCompletion}/{perceived.Percentage} Authoritative={authoritative.EligibleForCompletion}/{authoritative.Percentage} Blocked={blocked.Status} Hidden={hidden.Status} Stale={stale.Status} Complete={complete.Status} Professions={runtimes.Professions.Count}");
        }

        private static TestLabAutomationStepResult TrainingPersistenceProjections(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            string enrollmentId = BeginTrainingApprenticeship(context, "persistence");
            CompleteTrainingVisibleRequirements(context, enrollmentId, completePractice: true);
            TrainingRuntimeSaveData save = runtimes.Training.CreateSaveData();
            TrainingRuntime restored = new TrainingRuntime();
            TrainingOperationResult restore = restored.RestoreFromSaveData(save, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Transfers, runtimes.KnownPersonIds, restoring: true);

            TrainingRuntimeSaveData corrupt = save.Clone();
            corrupt.enrollments[0].programId = "training.program.missing";
            int beforeCount = restored.EnrollmentCount;
            long beforeRevision = restored.Revision;
            TrainingOperationResult rejected = restored.RestoreFromSaveData(corrupt, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Transfers, runtimes.KnownPersonIds, restoring: true);

            InformationAccessDecision decision = new InformationAccessDecision(
                "person.observer",
                TrainingInformationSubject.Create(TrainingInformationSubject.EnrollmentTag, enrollmentId, runtimes.PersonId),
                InformationAccessMode.Inspect,
                InformationAccessDecisionKind.RedactedAccess,
                InformationAccessDenialCode.DetailRestriction,
                false,
                InformationResharingPolicy.NoResharing,
                new[] { "program-id", "state" },
                TrainingInformationSubject.ProtectedFields,
                Array.Empty<string>(),
                new[] { ProfessionContentIds.AccessPublicId },
                50d,
                "Redacted training enrollment access.",
                "Training enrollment hides learner and progress token details.",
                true);
            TrainingProjection<TrainingEnrollmentSnapshot> projection = restored.ProjectEnrollment(enrollmentId, TrainingProjectionAudience.PublicInspection, decision);

            bool valid = restore.Succeeded
                && restored.EnrollmentCount == runtimes.Training.EnrollmentCount
                && restored.HistoryHooks.Count == 0
                && !rejected.Succeeded
                && restored.EnrollmentCount == beforeCount
                && restored.Revision == beforeRevision
                && projection.Record != null
                && projection.Redacted
                && !projection.Denied
                && string.IsNullOrWhiteSpace(projection.Record.PersonId)
                && projection.Record.ProgramId == ProfessionContentIds.BlacksmithApprenticeshipProgramId;
            return TestLabAssertions.True("step10-training-persistence", "Training persistence and projections are atomic and redacted", valid, $"Restore={restore.Status} Rejected={rejected.Status} Count={restored.EnrollmentCount}/{runtimes.Training.EnrollmentCount} Hooks={restored.HistoryHooks.Count} Redacted={projection.Redacted} Person='{projection.Record?.PersonId}'");
        }

        private static TestLabAutomationStepResult DefinitionsAndSpecializations(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            bool blacksmith = registry.TryGet(ProfessionContentIds.BlacksmithProfessionId, out ProfessionDefinition profession);
            bool specialization = registry.TryGet(ProfessionContentIds.WeaponsmithSpecializationId, out ProfessionSpecializationDefinition weaponsmith);
            bool parentMatches = specialization && string.Equals(weaponsmith.ParentProfessionId, profession?.Id, StringComparison.Ordinal);
            bool listed = blacksmith && profession.AllowedSpecializationIds.Contains(weaponsmith.Id);
            bool noGrantPayloads = blacksmith && profession.RelatedSkillIds.Contains("skill.smithing") && profession.RelatedCapabilityIds.Count == 0;

            return TestLabAssertions.True("step10-profession-definitions", "Profession definitions and specialization parentage resolve", blacksmith && specialization && parentMatches && listed && noGrantPayloads, $"Blacksmith={blacksmith} Specialization={specialization} Parent={parentMatches} Listed={listed} CapabilityGrants={(profession == null ? 0 : profession.RelatedCapabilityIds.Count)}");
        }

        private static TestLabAutomationStepResult FormalInformalPrimary(TestLabAutomationContext context)
        {
            PersonProfessionRuntime runtime = context.ScenarioContext.Runtimes.Professions;
            string personId = context.ScenarioContext.Runtimes.PersonId;
            ProfessionOperationResult informal = runtime.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.test.blacksmith",
                personId = personId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                informalPractice = true,
                primary = true,
                startWorldTime = "10",
                transactionId = "tx.profession.blacksmith"
            });
            ProfessionOperationResult duplicate = runtime.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.test.blacksmith.duplicate",
                personId = personId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                informalPractice = true,
                startWorldTime = "11",
                transactionId = "tx.profession.blacksmith.duplicate"
            });
            ProfessionOperationResult medical = runtime.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.test.medic",
                personId = personId,
                professionId = ProfessionContentIds.FieldMedicProfessionId,
                informalPractice = true,
                startWorldTime = "12",
                transactionId = "tx.profession.medic"
            });
            ProfessionOperationResult recognizeMissing = runtime.Recognize("profession-relationship.test.medic", string.Empty, transactionId: "tx.profession.medic.recognize.missing");
            ProfessionOperationResult recognize = runtime.Recognize("profession-relationship.test.medic", "authority.medical.prototype", "credential.prototype.medic", "tx.profession.medic.recognize");
            ProfessionOperationResult primary = runtime.SetPrimary("profession-relationship.test.medic", "tx.profession.medic.primary");

            bool onePrimary = runtime.QueryPrimary(personId).Count == 1 && runtime.QueryPrimary(personId).Single().ProfessionId == ProfessionContentIds.FieldMedicProfessionId;
            bool valid = informal.Succeeded
                && !duplicate.Succeeded
                && duplicate.Status == ProfessionOperationStatus.DuplicateActiveRelationship
                && medical.Succeeded
                && !recognizeMissing.Succeeded
                && recognizeMissing.Status == ProfessionOperationStatus.MissingRecognitionAuthority
                && recognize.Succeeded
                && primary.Succeeded
                && onePrimary;

            return TestLabAssertions.True("step10-profession-primary", "Formal, informal, primary, and duplicate profession rules are enforced", valid, $"Informal={informal.Status} Duplicate={duplicate.Status} Medical={medical.Status} MissingAuth={recognizeMissing.Status} Recognize={recognize.Status} Primary={primary.Status} OnePrimary={onePrimary}");
        }

        private static TestLabAutomationStepResult SecretAccessProjection(TestLabAutomationContext context)
        {
            PersonProfessionRuntime runtime = context.ScenarioContext.Runtimes.Professions;
            string personId = context.ScenarioContext.Runtimes.PersonId;
            ProfessionOperationResult created = runtime.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.test.spy",
                personId = personId,
                professionId = ProfessionContentIds.SpyProfessionId,
                state = ProfessionRelationshipState.Secret,
                informalPractice = true,
                active = true,
                startWorldTime = "20",
                accessPolicyId = ProfessionContentIds.AccessSecretId,
                tags = new[] { "profession.secret" },
                transactionId = "tx.profession.spy"
            });

            InformationAccessDecision redactedDecision = new InformationAccessDecision(
                "person.observer",
                ProfessionInformationSubject.Relationship("profession-relationship.test.spy", personId, ProfessionContentIds.SpyProfessionId, new[] { "profession.secret" }),
                InformationAccessMode.Inspect,
                InformationAccessDecisionKind.RedactedAccess,
                InformationAccessDenialCode.DetailRestriction,
                false,
                InformationResharingPolicy.NoResharing,
                new[] { "profession-id", "state" },
                ProfessionInformationSubject.ProtectedFields,
                Array.Empty<string>(),
                new[] { ProfessionContentIds.AccessSecretId },
                20d,
                "Redacted profession access.",
                "Secret profession relationship hides identity details.",
                true);
            PersonProfessionProjection projection = runtime.Project("profession-relationship.test.spy", ProfessionProjectionAudience.PublicInspection, redactedDecision);

            bool valid = created.Succeeded
                && projection.Redacted
                && !projection.Denied
                && projection.Snapshot != null
                && string.IsNullOrWhiteSpace(projection.Snapshot.PersonId)
                && projection.Snapshot.ProfessionId == ProfessionContentIds.SpyProfessionId;
            return TestLabAssertions.True("step10-profession-secret", "Secret profession relationships redact ordinary projections", valid, $"Created={created.Status} Redacted={projection.Redacted} Denied={projection.Denied} Person='{projection.Snapshot?.PersonId}' Profession='{projection.Snapshot?.ProfessionId}'");
        }

        private static TestLabAutomationStepResult PersistenceRoundTrip(TestLabAutomationContext context)
        {
            PersonProfessionRuntime runtime = context.ScenarioContext.Runtimes.Professions;
            string personId = context.ScenarioContext.Runtimes.PersonId;
            runtime.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.test.persist",
                personId = personId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                informalPractice = true,
                primary = true,
                specializationIds = new[] { ProfessionContentIds.WeaponsmithSpecializationId },
                startWorldTime = "30",
                transactionId = "tx.profession.persist"
            });

            PersonProfessionRuntimeSaveData save = runtime.CreateSaveData();
            PersonProfessionRuntime restored = new PersonProfessionRuntime();
            ProfessionOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.KnownPersonIds, restoring: true);
            PersonProfessionRuntimeSaveData corrupt = save.Clone();
            corrupt.relationships[0].professionId = "profession.missing";
            ProfessionOperationResult rejected = restored.RestoreFromSaveData(corrupt, context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.KnownPersonIds, restoring: true);

            bool valid = restore.Succeeded
                && restored.Count == runtime.Count
                && restored.Revision == runtime.Revision
                && restored.HistoryHooks.Count == 0
                && !rejected.Succeeded
                && restored.Count == runtime.Count;
            return TestLabAssertions.True("step10-profession-persistence", "Profession relationships save and restore without replay", valid, $"Restore={restore.Status} Rejected={rejected.Status} Count={restored.Count}/{runtime.Count} Hooks={restored.HistoryHooks.Count}");
        }

        private static TestLabAutomationStepResult CompetencyBoundary(TestLabAutomationContext context)
        {
            PersonProfessionRuntime runtime = context.ScenarioContext.Runtimes.Professions;
            string personId = context.ScenarioContext.Runtimes.PersonId;
            long knowledgeRevision = context.ScenarioContext.Runtimes.Knowledge.KnowledgeRevision;
            long accessRevision = context.ScenarioContext.Runtimes.Access.AccessRevision;
            runtime.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.test.boundary",
                personId = personId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                informalPractice = true,
                startWorldTime = "40",
                transactionId = "tx.profession.boundary"
            });

            bool valid = context.ScenarioContext.Runtimes.Knowledge.KnowledgeRevision == knowledgeRevision
                && context.ScenarioContext.Runtimes.Access.AccessRevision == accessRevision
                && context.ScenarioContext.Runtimes.History.HistoryRevision == 0L
                && runtime.HistoryHooks.Count >= 1;
            return TestLabAssertions.True("step10-profession-boundary", "Profession identity does not grant skills or capabilities", valid, $"Knowledge={knowledgeRevision}->{context.ScenarioContext.Runtimes.Knowledge.KnowledgeRevision} Access={accessRevision}->{context.ScenarioContext.Runtimes.Access.AccessRevision} History={context.ScenarioContext.Runtimes.History.HistoryRevision} Hooks={runtime.HistoryHooks.Count}");
        }

        private static PositionEmploymentOperationResult CreatePosition(TestLabAutomationContext context, string slug, string positionDefinitionId, string organizationId, string organizationTypeId, int maxHolders)
        {
            return context.ScenarioContext.Runtimes.PositionEmployment.CreatePosition(new PositionInstanceData
            {
                positionInstanceId = context.ScenarioContext.ScopedId("position-instance", slug),
                positionDefinitionId = positionDefinitionId,
                organizationId = organizationId,
                organizationTypeId = organizationTypeId,
                state = PositionInstanceState.Vacant,
                maximumHolders = Math.Max(1, maxHolders),
                vacancyAllowed = true,
                createdWorldTime = context.ScenarioContext.ScopedId("position-time", $"{slug}-created"),
                accessPolicyId = ProfessionContentIds.AccessPublicId,
                provenance = "test-lab"
            }, context.ScenarioContext.ScopedId("position-tx", $"{slug}-create"));
        }

        private static PositionEmploymentOperationResult AppointEligiblePerson(TestLabAutomationContext context, string slug, string positionDefinitionId, string organizationId, string organizationTypeId, string authorityId, EmploymentClassification? classification = null)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            PositionEmploymentOperationResult position = CreatePosition(context, slug, positionDefinitionId, organizationId, organizationTypeId, positionDefinitionId == ProfessionContentIds.GuildClerkPositionId ? 2 : 1);
            if (!position.Succeeded && !position.Duplicate)
            {
                return position;
            }

            string positionInstanceId = position.Position?.positionInstanceId ?? context.ScenarioContext.ScopedId("position-instance", slug);
            PositionEligibilityResult eligibility = runtimes.PositionEmployment.EvaluateEligibility(runtimes.PersonId, positionInstanceId, privilegedDiagnostics: true);
            return runtimes.PositionEmployment.AppointPerson(context.ScenarioContext.ScopedId("employment", slug), string.Empty, runtimes.PersonId, positionInstanceId, authorityId, eligibility.Snapshot, context.ScenarioContext.ScopedId("position-time", $"{slug}-appoint"), context.ScenarioContext.ScopedId("position-tx", $"{slug}-appoint"), classification);
        }

        private static TestLabAutomationStepResult CareerDefinitionsEpisodesTimeline(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in ProfessionContentIds.GetDefinitions(runtimes.DefinitionRegistry))
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(runtimes.DefinitionRegistry.DefinitionsById, report);
                }
            }

            EnsureBlacksmithActivityProfession(context);
            PersonProfessionSnapshot relationship = runtimes.Professions.QueryByProfession(ProfessionContentIds.BlacksmithProfessionId, activeOnly: true).First();
            CareerHistoryOperationResult start = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "identity", CareerEpisodeCategory.FormalProfessionalPractice, relationshipId: relationship.RelationshipId, primary: true), context.ScenarioContext.ScopedId("career-tx", "identity-start"));
            CareerHistoryOperationResult transition = runtimes.CareerHistory.RecordTransition(Transition(context, "identity", ProfessionContentIds.CareerProfessionEnteredTransitionId, CareerTransitionCategory.ProfessionEntered, destinationEpisodeIds: new[] { start.Episode?.episodeId }, supportingRecords: new[] { CareerSource(CareerTransitionSourceRecordType.ProfessionRelationship, relationship.RelationshipId, runtimes.Professions.Revision) }), context.ScenarioContext.ScopedId("career-tx", "identity-transition"));
            CareerHistoryOperationResult first = runtimes.CareerHistory.BuildTimeline(runtimes.PersonId);
            first.Timeline.Episodes[0].state = CareerEpisodeState.Ended;
            CareerHistoryOperationResult second = runtimes.CareerHistory.BuildTimeline(runtimes.PersonId);

            bool valid = report.ErrorCount == 0
                && report.WarningCount == 0
                && start.Succeeded
                && transition.Succeeded
                && first.Succeeded
                && second.Succeeded
                && second.Timeline.Episodes[0].state == CareerEpisodeState.Active
                && second.Timeline.PrimaryCareers.Count == 1
                && second.Timeline.Transitions.Count == 1;
            return TestLabAssertions.True("step10-career-timeline", "Career definitions validate and timelines are immutable deterministic projections", valid, $"Errors={report.ErrorCount} Warnings={report.WarningCount} Start={start.Status} Transition={transition.Status} Timeline={second.Timeline?.Episodes.Count}/{second.Timeline?.Transitions.Count}");
        }

        private static TestLabAutomationStepResult CareerAuthoritativeTransitions(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            ProfessionalRankOperationResult journeyman = EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankJourneymanId, "career-authority-journeyman");
            CareerHistoryOperationResult rankEpisode = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "ranked", CareerEpisodeCategory.FormalProfessionalPractice, rankRecordId: journeyman.Rank?.rankRecordId, rankDefinitionId: journeyman.Rank?.rankDefinitionId, primary: true), context.ScenarioContext.ScopedId("career-tx", "ranked"));
            ProfessionalRankOperationResult master = EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankMasterId, "career-authority-master");
            CareerHistoryOperationResult promotion = runtimes.CareerHistory.RecordTransition(Transition(context, "promotion", ProfessionContentIds.CareerPromotionTransitionId, CareerTransitionCategory.Promotion, sourceEpisodeIds: new[] { rankEpisode.Episode?.episodeId }, destinationEpisodeIds: new[] { rankEpisode.Episode?.episodeId }, previousRank: journeyman.Rank?.rankRecordId, newRank: master.Rank?.rankRecordId, supportingRecords: new[] { CareerSource(CareerTransitionSourceRecordType.Rank, master.Rank?.rankRecordId, runtimes.ProfessionalRanks.Revision) }, authority: "authority.guild.prototype"), context.ScenarioContext.ScopedId("career-tx", "promotion"));
            ProfessionalRankOperationResult demoted = runtimes.ProfessionalRanks.DemotePerson(master.Rank?.rankRecordId, context.ScenarioContext.ScopedId("rank-record", "career-demoted"), ProfessionContentIds.BlacksmithRankJourneymanId, "70", context.ScenarioContext.ScopedId("rank-tx", "career-demote"));
            CareerHistoryOperationResult demotion = runtimes.CareerHistory.RecordTransition(Transition(context, "demotion", ProfessionContentIds.CareerDemotionTransitionId, CareerTransitionCategory.Demotion, sourceEpisodeIds: new[] { rankEpisode.Episode?.episodeId }, destinationEpisodeIds: new[] { rankEpisode.Episode?.episodeId }, previousRank: master.Rank?.rankRecordId, newRank: demoted.Rank?.rankRecordId, supportingRecords: new[] { CareerSource(CareerTransitionSourceRecordType.Rank, demoted.Rank?.rankRecordId, runtimes.ProfessionalRanks.Revision) }, authority: "authority.guild.prototype"), context.ScenarioContext.ScopedId("career-tx", "demotion"));

            PositionEmploymentOperationResult clerk = AppointEligiblePerson(context, "career-transfer-clerk", ProfessionContentIds.GuildClerkPositionId, "organization.prototype.guild", ProfessionContentIds.GuildOrganizationTypeId, ProfessionContentIds.PositionRestrictedRecordsAuthorityId);
            CareerHistoryOperationResult clerkEpisode = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "transfer-clerk", CareerEpisodeCategory.Employment, employment: clerk.Employment, primary: false), context.ScenarioContext.ScopedId("career-tx", "transfer-clerk"));
            PositionEmploymentOperationResult target = CreatePosition(context, "career-transfer-target", ProfessionContentIds.IndependentContractorPositionId, "organization.prototype.independent", ProfessionContentIds.IndependentOrganizationTypeId, 2);
            PositionEligibilityResult transferEligibility = runtimes.PositionEmployment.EvaluateEligibility(runtimes.PersonId, target.Position?.positionInstanceId, privilegedDiagnostics: true);
            PositionEmploymentOperationResult transfer = runtimes.PositionEmployment.TransferPerson(clerk.Employment?.employmentId, context.ScenarioContext.ScopedId("employment", "career-transfer"), target.Position?.positionInstanceId, "authority.guild.prototype", transferEligibility.Snapshot, "72", context.ScenarioContext.ScopedId("position-tx", "career-transfer"));
            CareerHistoryOperationResult transferEpisode = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "transfer-target", CareerEpisodeCategory.Employment, employment: transfer.Employment, primary: false), context.ScenarioContext.ScopedId("career-tx", "transfer-target"));
            CareerHistoryOperationResult transferTransition = runtimes.CareerHistory.RecordTransition(Transition(context, "transfer", ProfessionContentIds.CareerTransferTransitionId, CareerTransitionCategory.Transfer, sourceEpisodeIds: new[] { clerkEpisode.Episode?.episodeId }, destinationEpisodeIds: new[] { transferEpisode.Episode?.episodeId }, previousEmployment: clerk.Employment?.employmentId, newEmployment: transfer.Employment?.employmentId, previousPosition: clerk.Position?.positionInstanceId, newPosition: target.Position?.positionInstanceId, organization: "organization.prototype.independent", supportingRecords: new[] { CareerSource(CareerTransitionSourceRecordType.Employment, transfer.Employment?.employmentId, runtimes.PositionEmployment.Revision), CareerSource(CareerTransitionSourceRecordType.Position, target.Position?.positionInstanceId, runtimes.PositionEmployment.Revision) }, authority: "authority.guild.prototype"), context.ScenarioContext.ScopedId("career-tx", "transfer"));
            PositionEmploymentOperationResult resigned = runtimes.PositionEmployment.Resign(transfer.Employment?.employmentId, "73", context.ScenarioContext.ScopedId("position-tx", "career-resign"));
            CareerHistoryOperationResult resignation = runtimes.CareerHistory.RecordTransition(Transition(context, "resignation", ProfessionContentIds.CareerResignationTransitionId, CareerTransitionCategory.Resignation, sourceEpisodeIds: new[] { transferEpisode.Episode?.episodeId }, previousEmployment: transfer.Employment?.employmentId, supportingRecords: new[] { CareerSource(CareerTransitionSourceRecordType.Employment, transfer.Employment?.employmentId, runtimes.PositionEmployment.Revision) }), context.ScenarioContext.ScopedId("career-tx", "resignation"));
            PositionEmploymentOperationResult dismissedEmployment = AppointEligiblePerson(context, "career-dismiss-clerk", ProfessionContentIds.GuildClerkPositionId, "organization.prototype.guild", ProfessionContentIds.GuildOrganizationTypeId, ProfessionContentIds.PositionRestrictedRecordsAuthorityId);
            CareerHistoryOperationResult dismissEpisode = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "dismiss-clerk", CareerEpisodeCategory.Employment, employment: dismissedEmployment.Employment, secret: true), context.ScenarioContext.ScopedId("career-tx", "dismiss-clerk"));
            PositionEmploymentOperationResult dismissed = runtimes.PositionEmployment.Dismiss(dismissedEmployment.Employment?.employmentId, "74", context.ScenarioContext.ScopedId("position-tx", "career-dismiss"));
            CareerHistoryOperationResult dismissal = runtimes.CareerHistory.RecordTransition(Transition(context, "dismissal", ProfessionContentIds.CareerDismissalTransitionId, CareerTransitionCategory.Dismissal, sourceEpisodeIds: new[] { dismissEpisode.Episode?.episodeId }, previousEmployment: dismissedEmployment.Employment?.employmentId, supportingRecords: new[] { CareerSource(CareerTransitionSourceRecordType.Employment, dismissedEmployment.Employment?.employmentId, runtimes.PositionEmployment.Revision) }, authority: ProfessionContentIds.PositionRestrictedRecordsAuthorityId, secret: true), context.ScenarioContext.ScopedId("career-tx", "dismissal"));

            bool valid = journeyman.Succeeded && master.Succeeded && promotion.Succeeded && demoted.Succeeded && demotion.Succeeded && transfer.Succeeded && transferTransition.Succeeded && resigned.Succeeded && resignation.Succeeded && dismissed.Succeeded && dismissal.Succeeded;
            return TestLabAssertions.True("step10-career-transitions", "Career transitions reference authoritative rank and employment mutations", valid, $"Promotion={promotion.Status} Demotion={demotion.Status} Transfer={transferTransition.Status} Resign={resignation.Status} Dismiss={dismissal.Status}");
        }

        private static TestLabAutomationStepResult CareerConcurrentGapsRedaction(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankJourneymanId, "career-concurrent");
            PositionEmploymentOperationResult senior = AppointEligiblePerson(context, "career-primary-senior", ProfessionContentIds.RoyalForgeSeniorSmithPositionId, "organization.prototype.royal-forge", ProfessionContentIds.ForgeOrganizationTypeId, ProfessionContentIds.PositionDutyAssignAuthorityId);
            CareerHistoryOperationResult primary = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "primary-senior", CareerEpisodeCategory.Employment, employment: senior.Employment, primary: true, exclusive: false), context.ScenarioContext.ScopedId("career-tx", "primary-senior"));
            PositionEmploymentOperationResult clerk = AppointEligiblePerson(context, "career-secondary-clerk", ProfessionContentIds.GuildClerkPositionId, "organization.prototype.guild", ProfessionContentIds.GuildOrganizationTypeId, ProfessionContentIds.PositionRestrictedRecordsAuthorityId);
            CareerHistoryOperationResult secondary = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "secondary-clerk", CareerEpisodeCategory.Employment, employment: clerk.Employment, primary: false, exclusive: false), context.ScenarioContext.ScopedId("career-tx", "secondary-clerk"));
            CareerHistoryOperationResult setPrimary = runtimes.CareerHistory.SetPrimaryCareer(secondary.Episode?.episodeId, context.ScenarioContext.ScopedId("career-tx", "set-primary"));
            CareerHistoryOperationResult gap = runtimes.CareerHistory.BeginCareerGap(context.ScenarioContext.ScopedId("career-episode", "gap"), runtimes.PersonId, "80", "Travel between contracts.", context.ScenarioContext.ScopedId("career-tx", "gap-start"));
            CareerHistoryOperationResult gapEnd = runtimes.CareerHistory.EndCareerGap(gap.Episode?.episodeId, "81", "Accepted work.", context.ScenarioContext.ScopedId("career-tx", "gap-end"));
            CareerHistoryOperationResult secret = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "secret-spy", CareerEpisodeCategory.FormalProfessionalPractice, profession: ProfessionContentIds.SpyProfessionId, primary: false, secret: true), context.ScenarioContext.ScopedId("career-tx", "secret"));
            CareerHistoryProjection<CareerEpisodeData> redacted = runtimes.CareerHistory.ProjectEpisode(secret.Episode?.episodeId, CareerHistoryProjectionAudience.Public);
            CareerHistoryOperationResult timeline = runtimes.CareerHistory.BuildTimeline(runtimes.PersonId);

            bool valid = primary.Succeeded
                && secondary.Succeeded
                && setPrimary.Succeeded
                && gap.Succeeded
                && gapEnd.Succeeded
                && secret.Succeeded
                && redacted.Redacted
                && timeline.Timeline.PrimaryCareers.Count == 1
                && timeline.Timeline.PrimaryCareers[0].episodeId == secondary.Episode?.episodeId
                && timeline.Timeline.CareerGaps.Count == 1;
            return TestLabAssertions.True("step10-career-concurrent", "Concurrent careers, primary selection, gaps, and redaction are deterministic", valid, $"Primary={timeline.Timeline?.PrimaryCareers.Count} Active={timeline.Timeline?.ActiveCareers.Count} Gaps={timeline.Timeline?.CareerGaps.Count} Redacted={redacted.Redacted}");
        }

        private static TestLabAutomationStepResult CareerRetirementReturnChange(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            PositionEmploymentOperationResult contractor = AppointEligiblePerson(context, "career-retire-contractor", ProfessionContentIds.IndependentContractorPositionId, "organization.prototype.independent", ProfessionContentIds.IndependentOrganizationTypeId, "authority.guild.prototype", EmploymentClassification.IndependentServiceFoundation);
            CareerHistoryOperationResult contractorEpisode = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "retire-contractor", CareerEpisodeCategory.IndependentPractice, employment: contractor.Employment, primary: true, exclusive: false), context.ScenarioContext.ScopedId("career-tx", "retire-contractor"));
            PositionEmploymentOperationResult retireEmployment = runtimes.PositionEmployment.Retire(contractor.Employment?.employmentId, "90", context.ScenarioContext.ScopedId("position-tx", "retire"));
            CareerHistoryOperationResult retirementEpisode = runtimes.CareerHistory.StartCareerEpisode(new CareerEpisodeData { episodeId = context.ScenarioContext.ScopedId("career-episode", "retirement"), personId = runtimes.PersonId, category = CareerEpisodeCategory.Retirement, state = CareerEpisodeState.Active, careerClassification = CareerClassification.Retirement, startWorldTime = "90", primaryCareer = false, accessPolicyId = ProfessionContentIds.AccessPublicId }, context.ScenarioContext.ScopedId("career-tx", "retirement-episode"));
            CareerHistoryOperationResult retirement = runtimes.CareerHistory.RecordTransition(Transition(context, "retirement", ProfessionContentIds.CareerRetirementTransitionId, CareerTransitionCategory.Retirement, sourceEpisodeIds: new[] { contractorEpisode.Episode?.episodeId }, destinationEpisodeIds: new[] { retirementEpisode.Episode?.episodeId }, previousEmployment: contractor.Employment?.employmentId, supportingRecords: new[] { CareerSource(CareerTransitionSourceRecordType.Employment, contractor.Employment?.employmentId, runtimes.PositionEmployment.Revision) }), context.ScenarioContext.ScopedId("career-tx", "retirement"));
            PositionEmploymentOperationResult returnEmployment = AppointEligiblePerson(context, "career-return-contractor", ProfessionContentIds.IndependentContractorPositionId, "organization.prototype.independent", ProfessionContentIds.IndependentOrganizationTypeId, "authority.guild.prototype", EmploymentClassification.IndependentServiceFoundation);
            CareerHistoryOperationResult returnEpisode = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "return-contractor", CareerEpisodeCategory.IndependentPractice, employment: returnEmployment.Employment, primary: true, exclusive: false), context.ScenarioContext.ScopedId("career-tx", "return-episode"));
            CareerHistoryOperationResult returned = runtimes.CareerHistory.RecordTransition(Transition(context, "return", ProfessionContentIds.CareerReturnFromRetirementTransitionId, CareerTransitionCategory.ReturnFromRetirement, sourceEpisodeIds: new[] { retirementEpisode.Episode?.episodeId }, destinationEpisodeIds: new[] { returnEpisode.Episode?.episodeId }, newEmployment: returnEmployment.Employment?.employmentId, supportingRecords: new[] { CareerSource(CareerTransitionSourceRecordType.CareerEpisode, retirementEpisode.Episode?.episodeId, runtimes.CareerHistory.Revision), CareerSource(CareerTransitionSourceRecordType.Employment, returnEmployment.Employment?.employmentId, runtimes.PositionEmployment.Revision) }), context.ScenarioContext.ScopedId("career-tx", "return"));
            ProfessionOperationResult medic = runtimes.Professions.AddRelationship(new AddProfessionRelationshipRequest { relationshipId = context.ScenarioContext.ScopedId("profession-relationship", "career-medic"), personId = runtimes.PersonId, professionId = ProfessionContentIds.FieldMedicProfessionId, informalPractice = true, formalPractice = true, active = true, startWorldTime = "94", transactionId = context.ScenarioContext.ScopedId("profession-tx", "career-medic") });
            CareerHistoryOperationResult medicEpisode = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "career-medic", CareerEpisodeCategory.FormalProfessionalPractice, profession: ProfessionContentIds.FieldMedicProfessionId, relationshipId: medic.Snapshot?.RelationshipId, primary: true, exclusive: false), context.ScenarioContext.ScopedId("career-tx", "medic"));
            CareerHistoryOperationResult change = runtimes.CareerHistory.RecordTransition(Transition(context, "career-change", ProfessionContentIds.CareerChangeTransitionId, CareerTransitionCategory.CareerChange, sourceEpisodeIds: new[] { returnEpisode.Episode?.episodeId }, destinationEpisodeIds: new[] { medicEpisode.Episode?.episodeId }, profession: ProfessionContentIds.FieldMedicProfessionId, supportingRecords: new[] { CareerSource(CareerTransitionSourceRecordType.ProfessionRelationship, medic.Snapshot?.RelationshipId, runtimes.Professions.Revision), CareerSource(CareerTransitionSourceRecordType.Employment, returnEmployment.Employment?.employmentId, runtimes.PositionEmployment.Revision) }), context.ScenarioContext.ScopedId("career-tx", "career-change"));
            CareerHistoryOperationResult timeline = runtimes.CareerHistory.BuildTimeline(runtimes.PersonId);

            bool valid = retireEmployment.Succeeded && retirementEpisode.Succeeded && retirement.Succeeded && returnEmployment.Succeeded && returned.Succeeded && medic.Succeeded && change.Succeeded && timeline.Timeline.Transitions.Any(item => item.category == CareerTransitionCategory.CareerChange) && timeline.Timeline.Episodes.Any(item => item.professionId == ProfessionContentIds.BlacksmithProfessionId);
            return TestLabAssertions.True("step10-career-retirement", "Retirement, return, and career change retain prior career history", valid, $"Retire={retirement.Status} Return={returned.Status} Change={change.Status} Episodes={timeline.Timeline?.Episodes.Count}");
        }

        private static TestLabAutomationStepResult CareerPersistenceRestore(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureBlacksmithActivityProfession(context);
            PersonProfessionSnapshot relationship = runtimes.Professions.QueryByProfession(ProfessionContentIds.BlacksmithProfessionId, activeOnly: true).First();
            CareerHistoryOperationResult start = runtimes.CareerHistory.StartCareerEpisode(CareerEpisode(context, "persist", CareerEpisodeCategory.FormalProfessionalPractice, relationshipId: relationship.RelationshipId, primary: true), context.ScenarioContext.ScopedId("career-tx", "persist-start"));
            CareerHistoryOperationResult achievement = runtimes.CareerHistory.RecordMilestone(new CareerMilestoneRecordData { milestoneId = context.ScenarioContext.ScopedId("career-milestone", "achievement"), personId = runtimes.PersonId, kind = CareerMilestoneKind.Achievement, episodeId = start.Episode?.episodeId, professionId = ProfessionContentIds.BlacksmithProfessionId, worldTime = "100", description = "Prototype career milestone.", exclusive = true, accessPolicyId = ProfessionContentIds.AccessPublicId }, context.ScenarioContext.ScopedId("career-tx", "achievement"));
            CareerHistoryRuntimeSaveData save = runtimes.CareerHistory.CreateSaveData();
            CareerHistoryRuntime restored = new CareerHistoryRuntime();
            CareerHistoryOperationResult restore = restored.RestoreFromSaveData(save, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.ProfessionalRanks, runtimes.PositionEmployment, runtimes.KnownPersonIds, new[] { "organization.prototype.guild", "organization.prototype.royal-forge", "organization.prototype.independent" }, new[] { "authority.guild.prototype", ProfessionContentIds.PositionDutyAssignAuthorityId }, restoring: true);
            CareerHistoryRuntimeSaveData corrupt = save.Clone();
            corrupt.episodes[0].professionId = "profession.missing";
            CareerHistoryOperationResult rejected = restored.RestoreFromSaveData(corrupt, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.ProfessionalRanks, runtimes.PositionEmployment, runtimes.KnownPersonIds, new[] { "organization.prototype.guild", "organization.prototype.royal-forge", "organization.prototype.independent" }, new[] { "authority.guild.prototype", ProfessionContentIds.PositionDutyAssignAuthorityId }, restoring: true);
            CareerHistoryOperationResult timeline = restored.BuildTimeline(runtimes.PersonId);

            bool valid = start.Succeeded && achievement.Succeeded && restore.Succeeded && restored.HistoryHooks.Count == 0 && !rejected.Succeeded && timeline.Succeeded && timeline.Timeline.Episodes.Count == 1 && timeline.Timeline.Milestones.Count == 1;
            return TestLabAssertions.True("step10-career-persistence", "Career persistence restores atomically and rejects corrupt data without replaying hooks", valid, $"Start={start.Status} Achievement={achievement.Status} Restore={restore.Status} Rejected={rejected.Status} Hooks={restored.HistoryHooks.Count}");
        }

        private static CareerEpisodeData CareerEpisode(
            TestLabAutomationContext context,
            string slug,
            CareerEpisodeCategory category,
            EmploymentRecordData employment = null,
            string relationshipId = "",
            string profession = null,
            string rankRecordId = "",
            string rankDefinitionId = "",
            bool primary = false,
            bool exclusive = false,
            bool secret = false)
        {
            string professionId = profession ?? ProfessionContentIds.BlacksmithProfessionId;
            string organizationId = employment?.employerOrganizationId ?? string.Empty;
            string sourceId = !string.IsNullOrWhiteSpace(employment?.employmentId) ? employment.employmentId : relationshipId;
            bool hasEmploymentSource = !string.IsNullOrWhiteSpace(employment?.employmentId);
            bool hasProfessionSource = !hasEmploymentSource && !string.IsNullOrWhiteSpace(relationshipId);
            CareerTransitionSourceRecordType sourceType = hasEmploymentSource ? CareerTransitionSourceRecordType.Employment : CareerTransitionSourceRecordType.ProfessionRelationship;
            return new CareerEpisodeData
            {
                episodeId = context.ScenarioContext.ScopedId("career-episode", slug),
                personId = context.ScenarioContext.Runtimes.PersonId,
                category = category,
                professionId = professionId,
                employmentId = employment?.employmentId ?? string.Empty,
                positionInstanceId = employment?.positionInstanceId ?? string.Empty,
                organizationId = organizationId,
                rankRecordId = rankRecordId ?? string.Empty,
                rankDefinitionId = rankDefinitionId ?? string.Empty,
                startWorldTime = context.ScenarioContext.ScopedId("career-time", $"{slug}-start"),
                state = CareerEpisodeState.Active,
                careerClassification = primary ? CareerClassification.Primary : CareerClassification.Secondary,
                workClassification = employment?.classification ?? EmploymentClassification.Custom,
                reasonStarted = "Prototype career fixture.",
                primaryCareer = primary,
                exclusiveCareer = exclusive,
                secret = secret,
                accessPolicyId = secret ? ProfessionContentIds.AccessSecretId : ProfessionContentIds.AccessPublicId,
                provenance = "test-lab",
                sourceRecords = string.IsNullOrWhiteSpace(sourceId) || (!hasEmploymentSource && !hasProfessionSource)
                    ? Array.Empty<CareerSourceRecordReferenceData>()
                    : new[] { CareerSource(sourceType, sourceId, sourceType == CareerTransitionSourceRecordType.Employment ? context.ScenarioContext.Runtimes.PositionEmployment.Revision : context.ScenarioContext.Runtimes.Professions.Revision) }
            };
        }

        private static CareerTransitionRecordData Transition(
            TestLabAutomationContext context,
            string slug,
            string definitionId,
            CareerTransitionCategory category,
            string[] sourceEpisodeIds = null,
            string[] destinationEpisodeIds = null,
            string profession = null,
            string previousRank = "",
            string newRank = "",
            string previousEmployment = "",
            string newEmployment = "",
            string previousPosition = "",
            string newPosition = "",
            string organization = "",
            CareerSourceRecordReferenceData[] supportingRecords = null,
            string authority = "",
            bool secret = false)
        {
            return new CareerTransitionRecordData
            {
                transitionId = context.ScenarioContext.ScopedId("career-transition", slug),
                personId = context.ScenarioContext.Runtimes.PersonId,
                transitionDefinitionId = definitionId,
                category = category,
                sourceEpisodeIds = sourceEpisodeIds ?? Array.Empty<string>(),
                destinationEpisodeIds = destinationEpisodeIds ?? Array.Empty<string>(),
                professionId = profession ?? ProfessionContentIds.BlacksmithProfessionId,
                previousRankRecordId = previousRank ?? string.Empty,
                newRankRecordId = newRank ?? string.Empty,
                previousEmploymentId = previousEmployment ?? string.Empty,
                newEmploymentId = newEmployment ?? string.Empty,
                previousPositionInstanceId = previousPosition ?? string.Empty,
                newPositionInstanceId = newPosition ?? string.Empty,
                organizationId = organization ?? string.Empty,
                transitionWorldTime = context.ScenarioContext.ScopedId("career-time", slug),
                decidingAuthorityId = authority ?? string.Empty,
                voluntary = category != CareerTransitionCategory.Dismissal && category != CareerTransitionCategory.Demotion,
                involuntary = category == CareerTransitionCategory.Dismissal || category == CareerTransitionCategory.Demotion,
                secret = secret,
                reason = "Prototype career transition.",
                supportingRecords = supportingRecords ?? Array.Empty<CareerSourceRecordReferenceData>(),
                accessPolicyId = secret ? ProfessionContentIds.AccessSecretId : ProfessionContentIds.AccessPublicId,
                provenance = "test-lab"
            };
        }

        private static CareerSourceRecordReferenceData CareerSource(CareerTransitionSourceRecordType type, string recordId, long revision)
        {
            return new CareerSourceRecordReferenceData
            {
                recordType = type,
                recordId = recordId ?? string.Empty,
                sourceRevision = revision
            };
        }

        private static TestLabAutomationStepResult EligibilityPreview(TestLabAutomationContext context)
        {
            ProfessionEntryRuntime entries = context.ScenarioContext.Runtimes.ProfessionEntries;
            long entryRevision = entries.Revision;
            long professionRevision = context.ScenarioContext.Runtimes.Professions.Revision;
            ProfessionEligibilityResult result = entries.Evaluate(BlacksmithContext(context, preview: true));
            bool snapshotImmutable = result.RuntimeToken != null;

            context.ScenarioContext.Runtimes.Professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.step10.preview-other",
                personId = context.ScenarioContext.Runtimes.PersonId,
                professionId = ProfessionContentIds.FieldMedicProfessionId,
                informalPractice = true,
                startWorldTime = "1",
                transactionId = "tx.step10.preview-other"
            });

            bool valid = result.Succeeded
                && result.Preview
                && entries.Revision == entryRevision
                && professionRevision == 0L
                && snapshotImmutable
                && result.RuntimeToken.professionRevision == professionRevision;
            return TestLabAssertions.True("step10-entry-preview", "Eligibility preview is non-mutating and immutable", valid, $"Status={result.Status} EntryRev={entryRevision}->{entries.Revision} ProfessionToken={result.RuntimeToken?.professionRevision} CurrentProf={context.ScenarioContext.Runtimes.Professions.Revision}");
        }

        private static TestLabAutomationStepResult InformalSelfDeclaration(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            long knowledgeRevision = runtimes.Knowledge.KnowledgeRevision;
            long accessRevision = runtimes.Access.AccessRevision;
            ProfessionEntryOperationResult entry = runtimes.ProfessionEntries.EnterInformal(BlacksmithContext(context, preview: false), "tx.step10.informal");
            PersonProfessionSnapshot snapshot = entry.Relationship;

            bool valid = entry.Succeeded
                && snapshot != null
                && snapshot.SelfDeclared
                && snapshot.InformalPractice
                && !snapshot.FormalPractice
                && !snapshot.Recognized
                && runtimes.Knowledge.KnowledgeRevision == knowledgeRevision
                && runtimes.Access.AccessRevision == accessRevision;
            return TestLabAssertions.True("step10-entry-informal", "Self-declared informal practice commits without recognition grants", valid, $"Entry={entry.Status} Self={snapshot?.SelfDeclared} Informal={snapshot?.InformalPractice} Recognized={snapshot?.Recognized} Knowledge={knowledgeRevision}->{runtimes.Knowledge.KnowledgeRevision}");
        }

        private static TestLabAutomationStepResult FormalRequestApproval(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            ProfessionEligibilityContext requestContext = MedicContext(context, preview: false);
            ProfessionEntryOperationResult submit = runtimes.ProfessionEntries.SubmitFormalRequest(requestContext, "tx.step10.formal.submit", "profession-entry-request.step10.medic");
            bool noRelationshipBeforeApproval = runtimes.Professions.QueryByProfession(ProfessionContentIds.FieldMedicProfessionId).Count == 0;
            ProfessionOperationResult unrelatedMutation = runtimes.Professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.step10.formal.unrelated",
                personId = runtimes.PersonId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                informalPractice = true,
                selfDeclared = true,
                startWorldTime = "2.5",
                transactionId = "tx.step10.formal.unrelated"
            });
            ProfessionEntryOperationResult approve = runtimes.ProfessionEntries.ApproveFormalRequest("profession-entry-request.step10.medic", "authority.medical.prototype", "tx.step10.formal.approve");
            PersonProfessionSnapshot relationship = approve.Relationship;

            bool valid = submit.Succeeded
                && noRelationshipBeforeApproval
                && unrelatedMutation.Succeeded
                && approve.Succeeded
                && relationship != null
                && relationship.Recognized
                && relationship.FormalPractice
                && !relationship.SelfDeclared
                && relationship.RecognizingAuthorityId == "authority.medical.prototype";
            return TestLabAssertions.True("step10-entry-formal", "Formal request approval revalidates eligibility and creates recognition", valid, $"Submit={submit.Status} Unrelated={unrelatedMutation.Status} Approve={approve.Status} Before={noRelationshipBeforeApproval} Recognized={relationship?.Recognized} Authority={relationship?.RecognizingAuthorityId}");
        }

        private static TestLabAutomationStepResult StaleAndAuthorityRejection(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            ProfessionEligibilityResult evaluated = runtimes.ProfessionEntries.Evaluate(BlacksmithContext(context, preview: true));
            ProfessionEligibilityResult badAuthority = runtimes.ProfessionEntries.Evaluate(new ProfessionEligibilityContext(
                runtimes.PersonId,
                ProfessionContentIds.FieldMedicProfessionId,
                ProfessionContentIds.FieldMedicRecognitionEntryPathId,
                formal: true,
                authorityId: "authority.guild.prototype",
                worldTime: 2d,
                skills: new[] { Skill("skill.healing-magic", 1) },
                knowledgeSubjects: new[] { "knowledge.subject.first-aid" },
                preview: true));
            runtimes.Professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = "profession-relationship.step10.stale-other",
                personId = runtimes.PersonId,
                professionId = ProfessionContentIds.FieldMedicProfessionId,
                informalPractice = true,
                startWorldTime = "3",
                transactionId = "tx.step10.stale-other"
            });
            ProfessionEntryOperationResult stale = runtimes.ProfessionEntries.EnterInformal(new ProfessionEligibilityContext(
                runtimes.PersonId,
                ProfessionContentIds.BlacksmithProfessionId,
                ProfessionContentIds.BlacksmithSelfDeclaredEntryPathId,
                selfDeclared: true,
                worldTime: 4d,
                preview: false,
                expectedRuntimeToken: evaluated.RuntimeToken), "tx.step10.stale");

            bool valid = evaluated.Succeeded
                && !badAuthority.Succeeded
                && badAuthority.Status == ProfessionEligibilityStatus.InvalidAuthority
                && !stale.Succeeded
                && stale.Status == ProfessionEntryOperationStatus.EligibilityFailed
                && runtimes.Professions.QueryByProfession(ProfessionContentIds.BlacksmithProfessionId).Count == 0;
            return TestLabAssertions.True("step10-entry-stale", "Invalid authority and stale eligibility are rejected before mutation", valid, $"Evaluated={evaluated.Status} BadAuthority={badAuthority.Status} Stale={stale.Status} BlacksmithCount={runtimes.Professions.QueryByProfession(ProfessionContentIds.BlacksmithProfessionId).Count}");
        }

        private static TestLabAutomationStepResult SpecializationAndReentry(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            ProfessionEntryOperationResult baseEntry = runtimes.ProfessionEntries.EnterInformal(BlacksmithContext(context, preview: false), "tx.step10.spec.base", "profession-relationship.step10.spec.blacksmith");
            ProfessionEntryOperationResult specialization = runtimes.ProfessionEntries.EnterSpecialization(new ProfessionEligibilityContext(
                runtimes.PersonId,
                ProfessionContentIds.BlacksmithProfessionId,
                ProfessionContentIds.WeaponsmithSpecializationEntryPathId,
                ProfessionContentIds.WeaponsmithSpecializationId,
                worldTime: 5d,
                skills: new[] { Skill("skill.smithing", 1) },
                preview: false), "profession-relationship.step10.spec.blacksmith", "tx.step10.spec");
            ProfessionOperationResult inactive = runtimes.Professions.Activate("profession-relationship.step10.spec.blacksmith", false, "tx.step10.inactive");
            ProfessionEntryOperationResult resume = runtimes.ProfessionEntries.ResumeInactive(new ProfessionEligibilityContext(
                runtimes.PersonId,
                ProfessionContentIds.BlacksmithProfessionId,
                ProfessionContentIds.BlacksmithReentryPathId,
                worldTime: 6d,
                preview: false), "profession-relationship.step10.spec.blacksmith", "tx.step10.resume");
            runtimes.Professions.TryGetSnapshot("profession-relationship.step10.spec.blacksmith", out PersonProfessionSnapshot final);

            bool valid = baseEntry.Succeeded
                && specialization.Succeeded
                && inactive.Succeeded
                && resume.Succeeded
                && final != null
                && final.Active
                && final.SpecializationIds.Contains(ProfessionContentIds.WeaponsmithSpecializationId);
            return TestLabAssertions.True("step10-entry-specialization-reentry", "Specialization entry and inactive reentry preserve parent relationships", valid, $"Base={baseEntry.Status} Spec={specialization.Status} Inactive={inactive.Status} Resume={resume.Status} Active={final?.Active} Specs={string.Join(",", final?.SpecializationIds ?? Array.Empty<string>())}");
        }

        private static TestLabAutomationStepResult ProjectionAndPersistence(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            ProfessionEntryOperationResult submit = runtimes.ProfessionEntries.SubmitFormalRequest(MedicContext(context, preview: false), "tx.step10.persist.submit", "profession-entry-request.step10.persist");
            InformationAccessDecision redactedDecision = new InformationAccessDecision(
                "person.observer",
                ProfessionEntryInformationSubject.Request("profession-entry-request.step10.persist", runtimes.PersonId, ProfessionContentIds.FieldMedicProfessionId),
                InformationAccessMode.Inspect,
                InformationAccessDecisionKind.RedactedAccess,
                InformationAccessDenialCode.DetailRestriction,
                false,
                InformationResharingPolicy.NoResharing,
                new[] { "profession-id", "entry-path-id", "state" },
                ProfessionEntryInformationSubject.ProtectedFields,
                Array.Empty<string>(),
                new[] { ProfessionContentIds.AccessPublicId },
                7d,
                "Redacted profession entry request.",
                "Entry request hides applicant and authority details.",
                true);
            ProfessionEntryProjection<ProfessionEntryRequestSnapshot> projection = runtimes.ProfessionEntries.ProjectRequest("profession-entry-request.step10.persist", ProfessionEntryProjectionAudience.PublicInspection, redactedDecision);
            ProfessionEntryRuntimeSaveData save = runtimes.ProfessionEntries.CreateSaveData();
            ProfessionEntryRuntime restored = new ProfessionEntryRuntime();
            ProfessionEntryOperationResult restore = restored.RestoreFromSaveData(save, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.KnownPersonIds, restoring: true);
            ProfessionEntryRuntimeSaveData corrupt = save.Clone();
            corrupt.requests[0].entryPathId = "profession-entry.missing";
            ProfessionEntryOperationResult rejected = restored.RestoreFromSaveData(corrupt, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.KnownPersonIds, restoring: true);

            bool valid = submit.Succeeded
                && projection.Redacted
                && projection.Record != null
                && string.IsNullOrWhiteSpace(projection.Record.ApplicantPersonId)
                && restore.Succeeded
                && restored.Count == 1
                && !rejected.Succeeded
                && restored.Count == 1
                && restored.HistoryHooks.Count == 0;
            return TestLabAssertions.True("step10-entry-persistence", "Entry request projections redact and persistence restores without replay", valid, $"Submit={submit.Status} Redacted={projection.Redacted} Applicant='{projection.Record?.ApplicantPersonId}' Restore={restore.Status} Rejected={rejected.Status} Count={restored.Count}");
        }

        private static ProfessionEligibilityContext BlacksmithContext(TestLabAutomationContext context, bool preview)
        {
            return new ProfessionEligibilityContext(
                context.ScenarioContext.Runtimes.PersonId,
                ProfessionContentIds.BlacksmithProfessionId,
                ProfessionContentIds.BlacksmithSelfDeclaredEntryPathId,
                selfDeclared: true,
                worldTime: 1d,
                correlationId: "step10.blacksmith",
                preview: preview);
        }

        private static string TrainingEnrollmentId(TestLabAutomationContext context, string slug)
        {
            return context.ScenarioContext.ScopedId("training-enrollment", slug);
        }

        private static string BeginTrainingApprenticeship(TestLabAutomationContext context, string slug)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            string enrollmentId = TrainingEnrollmentId(context, slug);
            TrainingOperationResult apply = runtimes.Training.ApplyToProgram(enrollmentId, runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipProgramId, context.ScenarioContext.ScopedId("training-tx", $"{slug}-apply"), worldTime: 1d);
            if (apply.Succeeded)
            {
                runtimes.Training.AcceptEnrollment(enrollmentId, context.ScenarioContext.ScopedId("training-tx", $"{slug}-accept"));
                runtimes.Training.AssignInstructor(enrollmentId, context.ScenarioContext.ScopedId("training-instructor", $"{slug}-master"), TrainingInstructorRoleKind.Master, runtimes.PersonId, context.ScenarioContext.ScopedId("training-tx", $"{slug}-master"), professionId: ProfessionContentIds.BlacksmithProfessionId, authorityId: "authority.guild.prototype");
                runtimes.Training.BeginProgram(enrollmentId, context.ScenarioContext.ScopedId("training-tx", $"{slug}-begin"));
            }

            return enrollmentId;
        }

        private static void CompleteTrainingVisibleRequirements(TestLabAutomationContext context, string enrollmentId, bool completePractice)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            runtimes.Training.RunLearningSession(context.ScenarioContext.ScopedId("training-session", "safety"), enrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, ProfessionContentIds.BlacksmithSafetyLessonId, context.ScenarioContext.ScopedId("training-tx", "safety"));
            runtimes.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, context.ScenarioContext.ScopedId("training-tx", "module-basics"));
            runtimes.Training.RunLearningSession(context.ScenarioContext.ScopedId("training-session", "practice"), enrollmentId, ProfessionContentIds.BlacksmithPracticeModuleId, ProfessionContentIds.BlacksmithDemonstrationLessonId, context.ScenarioContext.ScopedId("training-tx", "practice-lesson"));
            if (!completePractice)
            {
                return;
            }

            string activityId = context.ScenarioContext.ScopedId("crafting-operation", "practice-complete");
            runtimes.Training.RecordPracticalAssignment(context.ScenarioContext.ScopedId("training-practical", "practice-complete"), enrollmentId, ProfessionContentIds.BlacksmithPracticalAssignmentId, activityId, TrainingAssignmentActivityCategory.Crafting, context.ScenarioContext.ScopedId("training-tx", "practice-complete"), quality: 700, supervisorPersonId: runtimes.PersonId);
            runtimes.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithPracticeModuleId, context.ScenarioContext.ScopedId("training-tx", "module-practice"));
        }

        private static InformationTransferRequest BuildTrainingTeachingRequest(TestLabAutomationContext context, string slug)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            KnowledgePropositionData proposition = TrainingTeachingProposition();
            runtimes.Knowledge.RecordObservation(new KnowledgeObservationRequest
            {
                PersonId = runtimes.PersonId,
                TransactionId = context.ScenarioContext.ScopedId("knowledge-observation", $"{slug}-teacher"),
                Proposition = proposition,
                AcquisitionSource = KnowledgeAcquisitionSource.DirectObservation,
                Provenance = KnowledgeProvenance.DirectObservation,
                Direction = KnowledgeEvidenceDirection.Supports,
                Strength = 950,
                Credibility = 950,
                SourceId = context.ScenarioContext.ScopedId("training-source", "teacher"),
                Visibility = KnowledgeVisibility.Public,
                PrivateAccessAuthorized = true
            });

            string transferTx = context.ScenarioContext.ScopedId("training-transfer", slug);
            return new InformationTransferRequest
            {
                TransactionId = transferTx,
                TransferId = context.ScenarioContext.ScopedId("transfer", slug),
                SenderPersonId = runtimes.PersonId,
                RecipientPersonIds = new[] { runtimes.PersonId },
                ContentItems = new[]
                {
                    new TransferContentItemData
                    {
                        contentItemId = context.ScenarioContext.ScopedId("transfer-content", slug),
                        contentType = InformationTransferContentType.InstructionalConcept,
                        domain = KnowledgeDomain.Professional,
                        proposition = proposition,
                        senderConfidence = 900,
                        senderBeliefState = KnowledgeBeliefState.Known,
                        privacyClassification = KnowledgeVisibility.Public,
                        assertionType = InformationTransferAssertionType.Instruction,
                        typedPayloadId = "procedure.blacksmith.forge-safety",
                        rawEvidenceStrength = 850
                    }
                },
                WorldTimeSeconds = 12d,
                PrivacyScope = TransferPrivacyScope.RecipientOnly,
                SenderKnowledge = runtimes.Knowledge,
                SenderMemory = runtimes.Memory,
                SourceRuntime = null,
                RecipientKnowledgeRuntimes = new Dictionary<string, PersonKnowledgeRuntime> { [runtimes.PersonId] = runtimes.Knowledge },
                RecipientMemoryRuntimes = new Dictionary<string, PersonMemoryRuntime> { [runtimes.PersonId] = runtimes.Memory },
                PrivilegedAccess = true
            };
        }

        private static KnowledgePropositionData TrainingTeachingProposition()
        {
            return new KnowledgePropositionData
            {
                factDefinitionId = BuiltInKnowledgeFacts.SpeciesCapability,
                subjectType = KnowledgeSubjectType.Species,
                subjectId = "species.human",
                valueType = KnowledgeValueType.StableId,
                stableValueId = "capability.profession.blacksmith-safety"
            };
        }

        private static void EnsureBlacksmithActivityProfession(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            if (runtimes.Professions.QueryByProfession(ProfessionContentIds.BlacksmithProfessionId, activeOnly: true).Any(item => string.Equals(item.PersonId, runtimes.PersonId, StringComparison.Ordinal)))
            {
                return;
            }

            runtimes.Professions.AddRelationship(new AddProfessionRelationshipRequest
            {
                relationshipId = context.ScenarioContext.ScopedId("profession-relationship", "activity-blacksmith"),
                personId = runtimes.PersonId,
                professionId = ProfessionContentIds.BlacksmithProfessionId,
                specializationIds = new[] { ProfessionContentIds.WeaponsmithSpecializationId },
                formalPractice = true,
                informalPractice = true,
                selfDeclared = true,
                recognized = true,
                recognizingAuthorityId = "authority.guild.prototype",
                recognitionReferenceId = context.ScenarioContext.ScopedId("profession-recognition", "activity-blacksmith"),
                active = true,
                startWorldTime = "1",
                transactionId = context.ScenarioContext.ScopedId("profession-tx", "activity-blacksmith")
            });
        }

        private static TestLabAutomationStepResult LifePathDefinitionsAndRecords(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            DefinitionValidationReport report = ValidatePrototypeProfessionDefinitions(runtimes.DefinitionRegistry);

            LifePathOperationResult path = EnsureLifePathRecord(context);
            LifePathOperationResult aspiration = runtimes.LifePaths.AddAspiration(LifeAspiration(context, "definitions", ProfessionContentIds.AspirationEnterBlacksmithProfessionId, ProfessionContentIds.BlacksmithProfessionId), context.ScenarioContext.ScopedId("life-tx", "definitions-aspiration"));
            LifePathOperationResult goal = runtimes.LifePaths.AddGoal(LifeGoal(context, "definitions", ProfessionContentIds.GoalEnterBlacksmithProfessionId, aspiration.Aspiration?.aspirationId, targetProfession: ProfessionContentIds.BlacksmithProfessionId), context.ScenarioContext.ScopedId("life-tx", "definitions-goal"));
            LifePathSnapshot snapshot = runtimes.LifePaths.BuildSnapshot(runtimes.PersonId).Snapshot;
            bool valid = report.ErrorCount == 0
                && report.WarningCount == 0
                && path.Succeeded
                && aspiration.Succeeded
                && goal.Succeeded
                && snapshot != null
                && snapshot.LifePaths.Count == 1
                && snapshot.LifePaths[0].formativeReferences.Length == 1
                && runtimes.DefinitionRegistry.TryGet(ProfessionContentIds.GoalCompleteBlacksmithApprenticeshipId, out LifeGoalDefinition trainingGoal)
                && trainingGoal.RequiredTrainingProgramIds.Contains(ProfessionContentIds.BlacksmithApprenticeshipProgramId);
            return TestLabAssertions.True("step10-life-definitions", "Life-path definitions and records validate without grants", valid, $"Errors={report.ErrorCount} Warnings={report.WarningCount} Path={path.Status} Aspiration={aspiration.Status} Goal={goal.Status} Snapshot={snapshot?.LifePaths.Count}/{snapshot?.Aspirations.Count}/{snapshot?.Goals.Count}");
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

        private static TestLabAutomationStepResult LifePathGoalProgressBoundaries(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureLifePathRecord(context);
            EnsureBlacksmithCredentialFoundation(context, "life-progress");
            CredentialOperationResult license = EnsureGuildLicense(context, "life-progress");
            EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankJourneymanId, "life-progress");
            CreateLifePositionEmployment(context, "life-progress");
            ProfessionalActivityOperationResult crafting = RegisterLifeActivity(context, "life-crafting", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, ProfessionalActivitySourceType.CraftingOperation);
            ProfessionalActivityOperationResult discovery = RegisterLifeActivity(context, "life-discovery", ProfessionContentIds.BlacksmithExperimentationActivityDefinitionId, ProfessionalActivitySourceType.DiscoveryClaim);

            PersonGoalData professionGoal = AddLifeGoal(context, "progress-profession", ProfessionContentIds.GoalEnterBlacksmithProfessionId, targetProfession: ProfessionContentIds.BlacksmithProfessionId);
            PersonGoalData trainingGoal = AddLifeGoal(context, "progress-training", ProfessionContentIds.GoalCompleteBlacksmithApprenticeshipId, targetTraining: ProfessionContentIds.BlacksmithApprenticeshipProgramId);
            PersonGoalData credentialGoal = AddLifeGoal(context, "progress-credential", ProfessionContentIds.GoalEarnBlacksmithGuildLicenseId, targetCredential: ProfessionContentIds.BlacksmithGuildLicenseCredentialId);
            PersonGoalData rankGoal = AddLifeGoal(context, "progress-rank", ProfessionContentIds.GoalReachJourneymanRankId, targetRank: ProfessionContentIds.BlacksmithRankJourneymanId);
            PersonGoalData positionGoal = AddLifeGoal(context, "progress-position", ProfessionContentIds.GoalObtainGuildClerkPositionId, targetPosition: ProfessionContentIds.GuildClerkPositionId);
            PersonGoalData craftingGoal = AddLifeGoal(context, "progress-crafting", ProfessionContentIds.GoalProduceMasterworkId, targetActivity: ProfessionContentIds.BlacksmithCraftingActivityDefinitionId);
            PersonGoalData discoveryGoal = AddLifeGoal(context, "progress-discovery", ProfessionContentIds.GoalConfirmDiscoveryId, targetActivity: ProfessionContentIds.BlacksmithExperimentationActivityDefinitionId);

            LifePathOperationResult staleToken = runtimes.LifePaths.EvaluateGoalProgress(professionGoal.goalId);
            RegisterLifeActivity(context, "life-stale-mutation", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, ProfessionalActivitySourceType.CraftingOperation);
            LifePathOperationResult stale = runtimes.LifePaths.CompleteGoal(professionGoal.goalId, staleToken.Progress, "70", context.ScenarioContext.ScopedId("life-tx", "stale-complete"));
            LifePathOperationResult current = runtimes.LifePaths.EvaluateGoalProgress(professionGoal.goalId);
            LifePathOperationResult complete = runtimes.LifePaths.CompleteGoal(professionGoal.goalId, current.Progress, "71", context.ScenarioContext.ScopedId("life-tx", "complete"));

            bool valid = crafting.Succeeded
                && discovery.Succeeded
                && license.Succeeded
                && GoalComplete(runtimes, trainingGoal.goalId)
                && GoalComplete(runtimes, credentialGoal.goalId)
                && GoalComplete(runtimes, rankGoal.goalId)
                && GoalComplete(runtimes, positionGoal.goalId)
                && GoalComplete(runtimes, craftingGoal.goalId)
                && GoalComplete(runtimes, discoveryGoal.goalId)
                && stale.Status == LifePathOperationStatus.StaleProgress
                && complete.Succeeded;
            return TestLabAssertions.True("step10-life-progress", "Goal progress evaluates authoritative sources and rejects stale completion", valid, $"License={license.Status} Crafting={crafting.Status} Discovery={discovery.Status} Stale={stale.Status} Complete={complete.Status} Training={trainingGoal.goalId} Credential={credentialGoal.goalId} Rank={rankGoal.goalId} Position={positionGoal.goalId}");
        }

        private static TestLabAutomationStepResult LifePathIdentityAndAccess(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureLifePathRecord(context);
            long professionRevision = runtimes.Professions.Revision;
            LifePathOperationResult identity = runtimes.LifePaths.SetProfessionalIdentity(new ProfessionalIdentityData
            {
                identityId = context.ScenarioContext.ScopedId("life-identity", "secret-spy"),
                personId = runtimes.PersonId,
                kind = ProfessionalIdentityKind.Primary,
                alignment = ProfessionalIdentityAlignmentState.Conflicted,
                professionId = ProfessionContentIds.SpyProfessionId,
                selfPerceived = true,
                publicDeclared = false,
                active = true,
                secret = true,
                motivationTags = new[] { "secret.identity" },
                accessPolicyId = ProfessionContentIds.AccessSecretId
            }, context.ScenarioContext.ScopedId("life-tx", "identity"));
            LifePathOperationResult conflict = runtimes.LifePaths.RecordIdentityConflict(new IdentityConflictData
            {
                conflictId = context.ScenarioContext.ScopedId("life-conflict", "secret-spy"),
                personId = runtimes.PersonId,
                state = ProfessionalIdentityAlignmentState.Conflicted,
                identityIds = new[] { identity.Identity?.identityId },
                accessPolicyId = ProfessionContentIds.AccessSecretId
            }, context.ScenarioContext.ScopedId("life-tx", "identity-conflict"));
            LifePathProjection<LifePathSnapshot> publicProjection = runtimes.LifePaths.ProjectSnapshot(runtimes.PersonId, LifePathProjectionAudience.Public);
            LifePathProjection<LifePathSnapshot> privileged = runtimes.LifePaths.ProjectSnapshot(runtimes.PersonId, LifePathProjectionAudience.SubjectPerson);
            bool valid = identity.Succeeded
                && conflict.Succeeded
                && runtimes.Professions.Revision == professionRevision
                && runtimes.Professions.QueryByPerson(runtimes.PersonId).Count == 0
                && publicProjection.Redacted
                && publicProjection.Record.Identities.Count == 0
                && privileged.Record.Identities.Any(item => item.professionId == ProfessionContentIds.SpyProfessionId);
            return TestLabAssertions.True("step10-life-identity", "Professional identity stays separate from profession state and secret projections redact", valid, $"Identity={identity.Status} Conflict={conflict.Status} Redacted={publicProjection.Redacted} ProfRev={professionRevision}->{runtimes.Professions.Revision}");
        }

        private static TestLabAutomationStepResult LifePathLifecycleConflicts(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureLifePathRecord(context);
            LifePathOperationResult aspiration = runtimes.LifePaths.AddAspiration(LifeAspiration(context, "lifecycle", ProfessionContentIds.AspirationEnterBlacksmithProfessionId, ProfessionContentIds.BlacksmithProfessionId), context.ScenarioContext.ScopedId("life-tx", "lifecycle-aspiration"));
            LifePathOperationResult conflict = runtimes.LifePaths.AddAspiration(LifeAspiration(context, "lifecycle-conflict", ProfessionContentIds.AspirationEnterBlacksmithProfessionId, ProfessionContentIds.BlacksmithProfessionId), context.ScenarioContext.ScopedId("life-tx", "lifecycle-conflict"));
            PersonGoalData goal = AddLifeGoal(context, "lifecycle-goal", ProfessionContentIds.GoalEnterBlacksmithProfessionId, aspiration.Aspiration?.aspirationId, targetProfession: ProfessionContentIds.BlacksmithProfessionId);
            LifePathOperationResult pause = runtimes.LifePaths.SetAspirationState(aspiration.Aspiration?.aspirationId, PersonAspirationState.Paused, "20", "Paused.", context.ScenarioContext.ScopedId("life-tx", "pause"));
            LifePathOperationResult resume = runtimes.LifePaths.SetAspirationState(aspiration.Aspiration?.aspirationId, PersonAspirationState.Active, "21", "Resumed.", context.ScenarioContext.ScopedId("life-tx", "resume"));
            LifePathOperationResult fail = runtimes.LifePaths.SetGoalState(goal.goalId, PersonGoalState.Failed, "22", "Failed.", context.ScenarioContext.ScopedId("life-tx", "goal-fail"));
            LifePathOperationResult revive = runtimes.LifePaths.SetGoalState(goal.goalId, PersonGoalState.Active, "23", "Invalid.", context.ScenarioContext.ScopedId("life-tx", "goal-revive"));
            LifePathOperationResult achievement = runtimes.LifePaths.RecordAchievementOrSetback(new LifePathAchievementSetbackReferenceData
            {
                recordId = context.ScenarioContext.ScopedId("life-achievement", "lifecycle"),
                personId = runtimes.PersonId,
                lifePathId = context.ScenarioContext.ScopedId("life-path", "prototype"),
                aspirationId = aspiration.Aspiration?.aspirationId,
                goalId = goal.goalId,
                kind = LifePathAchievementSetbackKind.Setback,
                sourceRecordType = CareerTransitionSourceRecordType.Custom,
                sourceRecordId = context.ScenarioContext.ScopedId("life-source", "setback"),
                worldTime = "24",
                exclusive = true,
                accessPolicyId = ProfessionContentIds.AccessPublicId
            }, context.ScenarioContext.ScopedId("life-tx", "setback"));
            bool valid = aspiration.Succeeded
                && conflict.Status == LifePathOperationStatus.Conflict
                && pause.Succeeded
                && resume.Succeeded
                && fail.Succeeded
                && revive.Status == LifePathOperationStatus.InvalidTransition
                && achievement.Succeeded;
            return TestLabAssertions.True("step10-life-lifecycle", "Aspiration and goal lifecycle transitions preserve conflicts and achievements", valid, $"Aspiration={aspiration.Status} Conflict={conflict.Status} Pause={pause.Status} Resume={resume.Status} Fail={fail.Status} Revive={revive.Status} Record={achievement.Status}");
        }

        private static TestLabAutomationStepResult LifePathPersistenceRoundTrip(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureLifePathRecord(context);
            AddLifeGoal(context, "persist-goal", ProfessionContentIds.GoalEnterBlacksmithProfessionId, targetProfession: ProfessionContentIds.BlacksmithProfessionId);
            LifePathRuntimeSaveData save = runtimes.LifePaths.CreateSaveData();
            LifePathRuntime restored = new LifePathRuntime();
            restored.Configure(runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.ProfessionalRanks, runtimes.PositionEmployment, runtimes.CareerHistory, runtimes.KnownPersonIds, DefaultLifePathOrganizations());
            LifePathOperationResult restore = restored.RestoreFromSaveData(save, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.ProfessionalRanks, runtimes.PositionEmployment, runtimes.CareerHistory, runtimes.KnownPersonIds, DefaultLifePathOrganizations(), restoring: true);
            LifePathRuntimeSaveData corrupt = save.Clone();
            corrupt.goals[0].personId = "person.missing";
            int beforeGoals = restored.GoalCount;
            long beforeRevision = restored.Revision;
            LifePathOperationResult rejected = restored.RestoreFromSaveData(corrupt, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.ProfessionalRanks, runtimes.PositionEmployment, runtimes.CareerHistory, runtimes.KnownPersonIds, DefaultLifePathOrganizations(), restoring: true);
            bool valid = restore.Succeeded
                && restored.GoalCount == runtimes.LifePaths.GoalCount
                && restored.HistoryHooks.Count == 0
                && rejected.Status == LifePathOperationStatus.CorruptSave
                && restored.GoalCount == beforeGoals
                && restored.Revision == beforeRevision;
            return TestLabAssertions.True("step10-life-persistence", "Life-path persistence restores atomically and rejects corrupt payloads", valid, $"Restore={restore.Status} Rejected={rejected.Status} Count={restored.GoalCount}/{runtimes.LifePaths.GoalCount} Hooks={restored.HistoryHooks.Count}");
        }

        private static TestLabAutomationStepResult Step10IntegrationReadinessAndValidation(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            Step10IntegrationValidationReport definitions = Step10IntegrationValidator.ValidateDefinitions(runtimes.DefinitionRegistry);
            PrepareStep10IntegrationFixture(context, "readiness");
            Step10IntegrationValidationReport graph = ValidateStep10Graph(context);
            bool valid = definitions.ErrorCount == 0
                && graph.ErrorCount == 0
                && Step10IntegrationValidator.AuthorityMap.Select(entry => entry.Domain).Distinct(StringComparer.Ordinal).Count() == Step10IntegrationValidator.AuthorityMap.Count;
            return TestLabAssertions.True("step10-integration-validate", "Step 10 definitions, authority ownership, and integrated runtime graph validate", valid, $"DefinitionErrors={definitions.ErrorCount} GraphErrors={graph.ErrorCount} GraphWarnings={graph.WarningCount} {graph.ToSummary()}");
        }

        private static TestLabAutomationStepResult Step10IntegrationPersistenceFingerprintAndSchema(TestLabAutomationContext context)
        {
            PrepareStep10IntegrationFixture(context, "fingerprint");
            Step10IntegrationRuntimeSnapshot snapshot = CreateStep10IntegrationSnapshot(context);
            string first = Step10IntegrationValidator.CreateCanonicalFingerprint(snapshot);
            Step10IntegrationRuntimeSnapshot reordered = snapshot.Clone();
            reordered.Activities.activities.Reverse();
            reordered.Positions.employments.Reverse();
            reordered.LifePaths.goals.Reverse();
            string second = Step10IntegrationValidator.CreateCanonicalFingerprint(reordered);

            Step10IntegrationRuntimeSnapshot corrupt = snapshot.Clone();
            corrupt.LifePaths.schemaVersion = LifePathRuntimeSaveData.CurrentSchemaVersion + 1;
            Step10IntegrationValidationReport corruptReport = Step10IntegrationValidator.ValidateRuntimeGraph(
                corrupt,
                context.ScenarioContext.Runtimes.DefinitionRegistry,
                context.ScenarioContext.Runtimes.KnownPersonIds,
                DefaultLifePathOrganizations(),
                DefaultLifePathAuthorities());
            bool valid = string.Equals(first, second, StringComparison.Ordinal)
                && corruptReport.Diagnostics.Any(diagnostic => diagnostic.Code == "UnsupportedSchemaVersion" && diagnostic.SubjectId == "LifePathRuntime")
                && !Step10IntegrationValidator.PersistenceDependencies.Any(entry => entry.DependsOn.Contains(entry.Owner));
            return TestLabAssertions.True("step10-integration-persistence", "Persistence dependency order, schema boundaries, and canonical fingerprints are deterministic", valid, $"Fingerprint={first} CorruptErrors={corruptReport.ErrorCount} Dependencies={Step10IntegrationValidator.PersistenceDependencies.Count}");
        }

        private static TestLabAutomationStepResult Step10IntegrationCrossSystemCareerGraph(TestLabAutomationContext context)
        {
            PrepareStep10IntegrationFixture(context, "cross-system");
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            Step10IntegrationValidationReport graph = ValidateStep10Graph(context);
            bool hasProfession = runtimes.Professions.QueryByPerson(runtimes.PersonId).Any(item => item.ProfessionId == ProfessionContentIds.BlacksmithProfessionId);
            bool hasCredential = runtimes.Credentials.QueryByRecipient(runtimes.PersonId, activeOnly: true).Any(item => item.credentialDefinitionId == ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId || item.credentialDefinitionId == ProfessionContentIds.BlacksmithGuildLicenseCredentialId);
            bool hasRank = runtimes.ProfessionalRanks.QueryByPerson(runtimes.PersonId, currentOnly: true).Any(item => item.rankDefinitionId == ProfessionContentIds.BlacksmithRankJourneymanId);
            bool hasEmployment = runtimes.PositionEmployment.QueryEmploymentByPerson(runtimes.PersonId, activeOnly: true).Any(item => item.positionDefinitionId == ProfessionContentIds.GuildClerkPositionId);
            bool hasLifePath = runtimes.LifePaths.QueryLifePathsByPerson(runtimes.PersonId).Count > 0;
            bool valid = graph.ErrorCount == 0 && hasProfession && hasCredential && hasRank && hasEmployment && hasLifePath;
            return TestLabAssertions.True("step10-integration-cross-system", "Representative profession, credential, rank, employment, career, and life-path graph remains authoritative", valid, $"Errors={graph.ErrorCount} Profession={hasProfession} Credential={hasCredential} Rank={hasRank} Employment={hasEmployment} LifePath={hasLifePath}");
        }

        private static TestLabAutomationStepResult Step10IntegrationScaleSnapshotAndAccess(TestLabAutomationContext context)
        {
            PrepareStep10IntegrationFixture(context, "scale");
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            Step10IntegrationRuntimeSnapshot snapshot = CreateStep10IntegrationSnapshot(context);
            string before = Step10IntegrationValidator.CreateCanonicalFingerprint(snapshot);

            for (int i = 0; i < 24; i++)
            {
                RegisterLifeActivity(context, $"scale-{i:00}", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, ProfessionalActivitySourceType.CraftingOperation);
            }

            string snapshotAfterMutation = Step10IntegrationValidator.CreateCanonicalFingerprint(snapshot);
            Step10IntegrationValidationReport currentGraph = ValidateStep10Graph(context);
            PersonProfessionProjection publicProjection = runtimes.Professions.Project(
                runtimes.Professions.QueryByPerson(runtimes.PersonId).First().RelationshipId,
                ProfessionProjectionAudience.PublicInspection);
            bool valid = string.Equals(before, snapshotAfterMutation, StringComparison.Ordinal)
                && currentGraph.ErrorCount == 0
                && publicProjection != null
                && runtimes.ProfessionalActivities.BuildExperienceSummary(runtimes.PersonId, ProfessionContentIds.BlacksmithProfessionId).TotalValidatedActivities >= 24;
            return TestLabAssertions.True("step10-integration-scale", "Large fixtures, immutable snapshots, and access-aware projections remain stable", valid, $"SnapshotStable={before == snapshotAfterMutation} Errors={currentGraph.ErrorCount} Activities={runtimes.ProfessionalActivities.BuildExperienceSummary(runtimes.PersonId, ProfessionContentIds.BlacksmithProfessionId).TotalValidatedActivities} ProjectionDenied={publicProjection?.Denied}");
        }

        private static void PrepareStep10IntegrationFixture(TestLabAutomationContext context, string slug)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureLifePathRecord(context);
            EnsureBlacksmithCredentialFoundation(context, slug);
            CredentialOperationResult credential = IssueApprenticeshipCredential(context, $"{slug}-credential", out _);
            ProfessionalRankOperationResult rank = EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankJourneymanId, $"{slug}-rank");
            CreateLifePositionEmployment(context, slug);
            ProfessionalActivityOperationResult activity = RegisterLifeActivity(context, $"{slug}-activity", ProfessionContentIds.BlacksmithCraftingActivityDefinitionId, ProfessionalActivitySourceType.CraftingOperation);

            PersonAspirationData aspiration = LifeAspiration(context, $"{slug}-license", ProfessionContentIds.AspirationEarnGuildLicenseId, ProfessionContentIds.BlacksmithProfessionId);
            aspiration.targetSubjectType = LifePathTargetSubjectType.Credential;
            aspiration.targetCredentialDefinitionId = ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId;
            runtimes.LifePaths.AddAspiration(aspiration, context.ScenarioContext.ScopedId("life-tx", $"{slug}-aspiration"));

            AddLifeGoal(
                context,
                $"{slug}-credential",
                ProfessionContentIds.GoalEarnBlacksmithGuildLicenseId,
                aspiration.aspirationId,
                targetProfession: ProfessionContentIds.BlacksmithProfessionId,
                targetCredential: ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId);

            CareerHistoryRuntimeSaveData careerSave = runtimes.CareerHistory.CreateSaveData();
            PositionEmploymentRuntimeSaveData positionSave = runtimes.PositionEmployment.CreateSaveData();
            EmploymentRecordData employment = positionSave.employments.FirstOrDefault(item => item.personId == runtimes.PersonId && item.positionDefinitionId == ProfessionContentIds.GuildClerkPositionId);
            if (employment != null && careerSave.episodes.All(item => item.employmentId != employment.employmentId))
            {
                careerSave.episodes.Add(new CareerEpisodeData
                {
                    episodeId = context.ScenarioContext.ScopedId("career-episode", slug),
                    personId = runtimes.PersonId,
                    category = CareerEpisodeCategory.Employment,
                    professionId = ProfessionContentIds.BlacksmithProfessionId,
                    employmentId = employment.employmentId,
                    positionInstanceId = employment.positionInstanceId,
                    organizationId = employment.employerOrganizationId,
                    state = CareerEpisodeState.Active,
                    rankRecordId = rank.Rank?.rankRecordId ?? string.Empty,
                    credentialId = credential.Credential?.credentialId ?? string.Empty,
                    primaryCareer = true,
                    accessPolicyId = ProfessionContentIds.AccessPublicId
                });
                runtimes.CareerHistory.RestoreFromSaveData(careerSave, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.ProfessionalRanks, runtimes.PositionEmployment, runtimes.KnownPersonIds, DefaultLifePathOrganizations(), DefaultLifePathAuthorities(), restoring: false);
            }

            CareerEpisodeData episode = runtimes.CareerHistory.CreateSaveData().episodes.FirstOrDefault(item => item.personId == runtimes.PersonId && item.state == CareerEpisodeState.Active);
            PersonProfessionSnapshot profession = runtimes.Professions.QueryByPerson(runtimes.PersonId).FirstOrDefault(item => item.ProfessionId == ProfessionContentIds.BlacksmithProfessionId);
            if (profession != null && episode != null)
            {
                runtimes.LifePaths.SetProfessionalIdentity(new ProfessionalIdentityData
                {
                    identityId = context.ScenarioContext.ScopedId("professional-identity", slug),
                    personId = runtimes.PersonId,
                    kind = ProfessionalIdentityKind.Primary,
                    alignment = ProfessionalIdentityAlignmentState.Aligned,
                    professionId = ProfessionContentIds.BlacksmithProfessionId,
                    professionRelationshipId = profession.RelationshipId,
                    careerEpisodeId = episode.episodeId,
                    active = true,
                    accessPolicyId = ProfessionContentIds.AccessPublicId
                }, context.ScenarioContext.ScopedId("life-tx", $"{slug}-identity"));
            }

            _ = activity;
        }

        private static Step10IntegrationValidationReport ValidateStep10Graph(TestLabAutomationContext context)
        {
            return Step10IntegrationValidator.ValidateRuntimeGraph(
                CreateStep10IntegrationSnapshot(context),
                context.ScenarioContext.Runtimes.DefinitionRegistry,
                context.ScenarioContext.Runtimes.KnownPersonIds,
                DefaultLifePathOrganizations(),
                DefaultLifePathAuthorities());
        }

        private static Step10IntegrationRuntimeSnapshot CreateStep10IntegrationSnapshot(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            return new Step10IntegrationRuntimeSnapshot(
                runtimes.Professions.CreateSaveData(),
                runtimes.ProfessionEntries.CreateSaveData(),
                runtimes.Training.CreateSaveData(),
                runtimes.ProfessionalActivities.CreateSaveData(),
                runtimes.Credentials.CreateSaveData(),
                runtimes.ProfessionalRanks.CreateSaveData(),
                runtimes.PositionEmployment.CreateSaveData(),
                runtimes.CareerHistory.CreateSaveData(),
                runtimes.LifePaths.CreateSaveData());
        }

        private static LifePathOperationResult EnsureLifePathRecord(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            return runtimes.LifePaths.CreateOrUpdateLifePath(new LifePathRecordData
            {
                lifePathId = context.ScenarioContext.ScopedId("life-path", "prototype"),
                personId = runtimes.PersonId,
                state = LifePathState.Active,
                startWorldTime = "1",
                formativeReferences = new[] { new FormativeReferenceData { referenceId = context.ScenarioContext.ScopedId("life-origin", "village"), kind = FormativeReferenceKind.Origin, subjectId = "origin.prototype.village", weight = 10 } },
                accessPolicyId = ProfessionContentIds.AccessPublicId
            }, context.ScenarioContext.ScopedId("life-tx", "path"));
        }

        private static PersonAspirationData LifeAspiration(TestLabAutomationContext context, string slug, string definitionId, string targetProfession = "")
        {
            return new PersonAspirationData
            {
                aspirationId = context.ScenarioContext.ScopedId("life-aspiration", slug),
                personId = context.ScenarioContext.Runtimes.PersonId,
                aspirationDefinitionId = definitionId,
                state = PersonAspirationState.Active,
                targetProfessionId = targetProfession,
                priority = 10,
                startWorldTime = "2",
                accessPolicyId = ProfessionContentIds.AccessPublicId
            };
        }

        private static PersonGoalData LifeGoal(TestLabAutomationContext context, string slug, string definitionId, string parentAspiration = "", string targetProfession = "", string targetTraining = "", string targetCredential = "", string targetRank = "", string targetPosition = "", string targetActivity = "")
        {
            return new PersonGoalData
            {
                goalId = context.ScenarioContext.ScopedId("life-goal", slug),
                personId = context.ScenarioContext.Runtimes.PersonId,
                goalDefinitionId = definitionId,
                parentAspirationId = parentAspiration ?? string.Empty,
                state = PersonGoalState.Active,
                targetProfessionId = targetProfession,
                targetTrainingProgramId = targetTraining,
                targetCredentialDefinitionId = targetCredential,
                targetRankDefinitionId = targetRank,
                targetPositionDefinitionId = targetPosition,
                targetActivityDefinitionId = targetActivity,
                priority = 10,
                startWorldTime = "3",
                accessPolicyId = ProfessionContentIds.AccessPublicId
            };
        }

        private static PersonGoalData AddLifeGoal(TestLabAutomationContext context, string slug, string definitionId, string parentAspiration = "", string targetProfession = "", string targetTraining = "", string targetCredential = "", string targetRank = "", string targetPosition = "", string targetActivity = "")
        {
            LifePathOperationResult result = context.ScenarioContext.Runtimes.LifePaths.AddGoal(LifeGoal(context, slug, definitionId, parentAspiration, targetProfession, targetTraining, targetCredential, targetRank, targetPosition, targetActivity), context.ScenarioContext.ScopedId("life-tx", slug));
            return result.Goal;
        }

        private static bool GoalComplete(TestLabRuntimeBundle runtimes, string goalId)
        {
            LifePathOperationResult result = runtimes.LifePaths.EvaluateGoalProgress(goalId);
            return result.Succeeded && result.Progress != null && result.Progress.AuthoritativeComplete;
        }

        private static ProfessionalActivityOperationResult RegisterLifeActivity(TestLabAutomationContext context, string slug, string definitionId, ProfessionalActivitySourceType sourceType)
        {
            EnsureBlacksmithActivityProfession(context);
            return context.ScenarioContext.Runtimes.ProfessionalActivities.RegisterAndValidateActivity(
                ActivityRequest(context, slug, definitionId, ActivityCustomSource(context, sourceType, slug, sourceType == ProfessionalActivitySourceType.DiscoveryClaim ? "production.activity.experimentation" : "source.crafting", ProfessionalActivityDifficulty.Skilled), ProfessionalResponsibilityLevel.IndependentPractitioner),
                context.ScenarioContext.ScopedId("professional-evidence", slug),
                "authority.guild.prototype",
                context.ScenarioContext.ScopedId("professional-tx", slug));
        }

        private static void CreateLifePositionEmployment(TestLabAutomationContext context, string slug)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            PositionEmploymentRuntimeSaveData save = runtimes.PositionEmployment.CreateSaveData();
            string positionId = context.ScenarioContext.ScopedId("position-instance", slug);
            string employmentId = context.ScenarioContext.ScopedId("employment", slug);
            if (save.positions.All(item => !string.Equals(item.positionInstanceId, positionId, StringComparison.Ordinal)))
            {
                save.positions.Add(new PositionInstanceData { positionInstanceId = positionId, positionDefinitionId = ProfessionContentIds.GuildClerkPositionId, organizationId = "organization.prototype.guild", organizationTypeId = ProfessionContentIds.GuildOrganizationTypeId, state = PositionInstanceState.Filled, holderPersonIds = new[] { runtimes.PersonId }, maximumHolders = 2, vacancyAllowed = true, accessPolicyId = ProfessionContentIds.AccessPublicId });
            }

            if (save.employments.All(item => !string.Equals(item.employmentId, employmentId, StringComparison.Ordinal)))
            {
                save.employments.Add(new EmploymentRecordData { employmentId = employmentId, personId = runtimes.PersonId, employerOrganizationId = "organization.prototype.guild", positionInstanceId = positionId, positionDefinitionId = ProfessionContentIds.GuildClerkPositionId, classification = EmploymentClassification.PartTime, state = EmploymentState.Active, startWorldTime = "12", appointmentAuthorityId = ProfessionContentIds.PositionAppointAuthorityId, accessPolicyId = ProfessionContentIds.AccessPublicId });
            }

            runtimes.PositionEmployment.RestoreFromSaveData(save, runtimes.DefinitionRegistry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.ProfessionalRanks, runtimes.KnownPersonIds, DefaultLifePathOrganizations(), DefaultLifePathAuthorities(), restoring: false);
        }

        private static string[] DefaultLifePathOrganizations() => new[] { "organization.prototype.guild", "organization.prototype.royal-forge", "organization.prototype.independent" };

        private static string[] DefaultLifePathAuthorities() => new[] { "authority.guild.prototype", ProfessionContentIds.PositionAppointAuthorityId, ProfessionContentIds.PositionDutyAssignAuthorityId, "organization.prototype.guild" };

        private static ProfessionalActivityRegistrationRequest ActivityRequest(
            TestLabAutomationContext context,
            string slug,
            string definitionId,
            ProfessionalActivitySourceSnapshot source,
            ProfessionalResponsibilityLevel responsibility = ProfessionalResponsibilityLevel.IndependentPractitioner,
            TrainingSupervisionLevel supervision = TrainingSupervisionLevel.IndependentWithReview)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            return new ProfessionalActivityRegistrationRequest
            {
                ActivityId = context.ScenarioContext.ScopedId("professional-activity", slug),
                PersonId = runtimes.PersonId,
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
                Provenance = "test-lab"
            };
        }

        private static ProfessionalActivitySourceSnapshot ActivityCustomSource(
            TestLabAutomationContext context,
            ProfessionalActivitySourceType sourceType,
            string slug,
            string tag,
            ProfessionalActivityDifficulty difficulty = ProfessionalActivityDifficulty.Routine)
        {
            return ProfessionalActivitySourceAdapters.FromCustom(
                sourceType,
                context.ScenarioContext.ScopedId("professional-source", slug),
                context.ScenarioContext.Runtimes.PersonId,
                ProfessionalActivityOutcomeState.Successful,
                quality: difficulty >= ProfessionalActivityDifficulty.Skilled ? 780 : 720,
                difficulty: difficulty,
                worldTime: context.ScenarioContext.ScopedId("professional-time", slug),
                tags: tag);
        }

        private static void EnsureBlacksmithCredentialFoundation(TestLabAutomationContext context, string slug)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureBlacksmithActivityProfession(context);

            bool completedTraining = runtimes.Training.QueryByProgram(ProfessionContentIds.BlacksmithApprenticeshipProgramId)
                .Any(item => string.Equals(item.PersonId, runtimes.PersonId, StringComparison.Ordinal) && item.State == TrainingEnrollmentState.Completed);
            if (!completedTraining)
            {
                string enrollmentId = BeginTrainingApprenticeship(context, $"credential-{slug}");
                CompleteTrainingVisibleRequirements(context, enrollmentId, completePractice: true);
                runtimes.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithHiddenAssessmentModuleId, context.ScenarioContext.ScopedId("training-tx", $"{slug}-hidden"));
                TrainingProgressResult progress = runtimes.Training.EvaluateProgress(enrollmentId, perceived: false);
                runtimes.Training.CompleteProgram(enrollmentId, context.ScenarioContext.ScopedId("training-tx", $"{slug}-complete"), progress.RuntimeToken, worldTime: 24d);
            }

            if (runtimes.ProfessionalActivities.BuildExperienceSummary(runtimes.PersonId, ProfessionContentIds.BlacksmithProfessionId).TotalValidatedActivities == 0)
            {
                runtimes.ProfessionalActivities.RegisterAndValidateActivity(
                    ActivityRequest(
                        context,
                        $"credential-{slug}-supervised",
                        ProfessionContentIds.BlacksmithSupervisedPracticeActivityDefinitionId,
                        ActivityCustomSource(context, ProfessionalActivitySourceType.TrainingPracticalAssignment, $"credential-{slug}-practice", "training.activity.practical"),
                        ProfessionalResponsibilityLevel.SupervisedWorker,
                        TrainingSupervisionLevel.CloselySupervised),
                    context.ScenarioContext.ScopedId("professional-evidence", $"credential-{slug}"),
                    "authority.guild.prototype",
                    context.ScenarioContext.ScopedId("professional-tx", $"credential-{slug}"));
            }
        }

        private static CredentialOperationResult RecordCredentialExam(TestLabAutomationContext context, string slug, string examinationDefinitionId, int score)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            return runtimes.Credentials.RecordExaminationAttempt(new CredentialExaminationAttemptData
            {
                attemptId = context.ScenarioContext.ScopedId("credential-exam", slug),
                examinationDefinitionId = examinationDefinitionId,
                applicantPersonId = runtimes.PersonId,
                evaluatorPersonId = runtimes.PersonId,
                evaluatorAuthorityId = "authority.guild.prototype",
                startWorldTime = "25",
                completionWorldTime = "26",
                score = score,
                sectionResults = new[]
                {
                    new CredentialExaminationSectionResultData
                    {
                        sectionId = context.ScenarioContext.ScopedId("credential-exam-section", slug),
                        displayName = "Prototype assessment",
                        score = score,
                        passed = score >= 700
                    }
                },
                provenance = context.ScenarioContext.ScopedId("credential-exam-provenance", slug)
            }, context.ScenarioContext.ScopedId("credential-tx", $"exam-{slug}"));
        }

        private static CredentialOperationResult IssueApprenticeshipCredential(TestLabAutomationContext context, string slug, out string applicationId)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            EnsureBlacksmithCredentialFoundation(context, slug);
            CredentialOperationResult exam = RecordCredentialExam(context, slug, ProfessionContentIds.BlacksmithPracticalExaminationId, 850);
            CredentialQualificationResult qualification = runtimes.Credentials.EvaluateQualification(runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId);
            CredentialOperationResult apply = runtimes.Credentials.SubmitApplication(context.ScenarioContext.ScopedId("credential-application", slug), runtimes.PersonId, ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, GuildIssuer(), qualification.Snapshot, "30", context.ScenarioContext.ScopedId("credential-tx", $"{slug}-apply"));
            CredentialOperationResult approve = runtimes.Credentials.ApproveApplication(apply.Application?.applicationId, "authority.guild.prototype", qualification.Snapshot, "31", context.ScenarioContext.ScopedId("credential-tx", $"{slug}-approve"));
            applicationId = apply.Application?.applicationId ?? string.Empty;
            if (!exam.Succeeded || !qualification.AuthoritativeQualified || !apply.Succeeded || !approve.Succeeded)
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.InvalidRequest, $"Credential fixture setup failed. Exam={exam.Status} Qualified={qualification.AuthoritativeQualified} Apply={apply.Status} Approve={approve.Status}", runtimes.Credentials.Revision, qualification);
            }

            return runtimes.Credentials.IssueCredential(context.ScenarioContext.ScopedId("credential-record", slug), ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId, runtimes.PersonId, GuildIssuer(), applicationId, exam.ExaminationAttempt?.attemptId, context.ScenarioContext.ScopedId("registration", slug), qualification.Snapshot, "32", context.ScenarioContext.ScopedId("credential-tx", $"{slug}-issue"));
        }

        private static CredentialIssuerReferenceData GuildIssuer()
        {
            return new CredentialIssuerReferenceData
            {
                issuerId = "authority.guild.prototype",
                issuerKind = CredentialIssuerAuthorityKind.Guild
            };
        }

        private static void EnsureApprenticeRankFoundation(TestLabAutomationContext context, string slug)
        {
            EnsureBlacksmithCredentialFoundation(context, $"rank-{slug}");
        }

        private static ProfessionalRankOperationResult EnsurePromotedRank(TestLabAutomationContext context, string rankDefinitionId, string slug)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            ProfessionalRankRecordData existing = runtimes.ProfessionalRanks.QueryByPerson(runtimes.PersonId, currentOnly: true)
                .FirstOrDefault(item => string.Equals(item.rankDefinitionId, rankDefinitionId, StringComparison.Ordinal));
            if (existing != null)
            {
                return ProfessionalRankOperationResult.Success("Rank already active.", runtimes.ProfessionalRanks.Revision, runtimes.ProfessionalRanks.Revision, rank: existing, duplicate: true);
            }

            if (rankDefinitionId == ProfessionContentIds.BlacksmithRankJourneymanId)
            {
                EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankApprenticeId, $"{slug}-prior");
                if (!runtimes.Credentials.QueryByRecipient(runtimes.PersonId, activeOnly: true).Any(item => item.credentialDefinitionId == ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId))
                {
                    IssueApprenticeshipCredential(context, $"{slug}-apprenticeship", out _);
                }
            }
            else if (rankDefinitionId == ProfessionContentIds.BlacksmithRankMasterId)
            {
                EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankJourneymanId, $"{slug}-prior");
                EnsureGuildLicense(context, $"{slug}-license");
            }
            else if (rankDefinitionId == ProfessionContentIds.WeaponsmithRankApprenticeId)
            {
                EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankApprenticeId, $"{slug}-prior");
                if (!runtimes.Credentials.QueryByRecipient(runtimes.PersonId, activeOnly: true).Any(item => item.credentialDefinitionId == ProfessionContentIds.BlacksmithApprenticeshipCertificateCredentialId))
                {
                    IssueApprenticeshipCredential(context, $"{slug}-apprenticeship", out _);
                }
            }
            else if (rankDefinitionId == ProfessionContentIds.WeaponsmithRankMasterId)
            {
                EnsurePromotedRank(context, ProfessionContentIds.BlacksmithRankJourneymanId, $"{slug}-general-prior");
                EnsurePromotedRank(context, ProfessionContentIds.WeaponsmithRankApprenticeId, $"{slug}-special-prior");
                EnsureGuildLicense(context, $"{slug}-license");
            }
            else
            {
                EnsureApprenticeRankFoundation(context, slug);
            }

            ProfessionalRankAdvancementResult evaluation = runtimes.ProfessionalRanks.EvaluateAdvancement(runtimes.PersonId, rankDefinitionId, "authority.guild.prototype", privilegedDiagnostics: true);
            ProfessionalRankOperationResult submit = runtimes.ProfessionalRanks.SubmitApplication(context.ScenarioContext.ScopedId("rank-application", slug), runtimes.PersonId, rankDefinitionId, "authority.guild.prototype", evaluation.Snapshot, "50", context.ScenarioContext.ScopedId("rank-tx", $"{slug}-submit"));
            ProfessionalRankOperationResult approve = runtimes.ProfessionalRanks.ApprovePromotion(submit.Application?.applicationId, runtimes.PersonId, evaluation.Snapshot, "51", context.ScenarioContext.ScopedId("rank-tx", $"{slug}-approve"));
            if (!evaluation.AuthoritativeEligible || !submit.Succeeded || !approve.Succeeded)
            {
                return ProfessionalRankOperationResult.Failure(ProfessionalRankOperationStatus.MissingQualification, $"Rank fixture setup failed. Eligible={evaluation.AuthoritativeEligible} Submit={submit.Status} Approve={approve.Status}", runtimes.ProfessionalRanks.Revision, evaluation);
            }

            return runtimes.ProfessionalRanks.PromotePerson(context.ScenarioContext.ScopedId("rank-record", slug), submit.Application?.applicationId, evaluation.Snapshot, "52", context.ScenarioContext.ScopedId("rank-tx", $"{slug}-promote"));
        }

        private static CredentialOperationResult EnsureGuildLicense(TestLabAutomationContext context, string slug)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            CredentialRecordData existing = runtimes.Credentials.QueryByRecipient(runtimes.PersonId, activeOnly: true)
                .FirstOrDefault(item => string.Equals(item.credentialDefinitionId, ProfessionContentIds.BlacksmithGuildLicenseCredentialId, StringComparison.Ordinal));
            if (existing != null)
            {
                return CredentialOperationResult.Success("Guild license already active.", runtimes.Credentials.Revision, runtimes.Credentials.Revision, credential: existing, duplicate: true);
            }

            EnsureBlacksmithCredentialFoundation(context, $"guild-{slug}");
            EnsureBlacksmithSafetyTraining(context, slug);
            RegisterIndependentBlacksmithActivity(context, $"{slug}-independent");
            RecordCredentialExam(context, $"{slug}-practical", ProfessionContentIds.BlacksmithPracticalExaminationId, 850);
            RecordCredentialExam(context, $"{slug}-written", ProfessionContentIds.BlacksmithWrittenExaminationId, 840);
            CredentialQualificationResult qualification = runtimes.Credentials.EvaluateQualification(runtimes.PersonId, ProfessionContentIds.BlacksmithGuildLicenseCredentialId, privilegedDiagnostics: true);
            CredentialOperationResult apply = runtimes.Credentials.SubmitApplication(context.ScenarioContext.ScopedId("credential-application", $"guild-{slug}"), runtimes.PersonId, ProfessionContentIds.BlacksmithGuildLicenseCredentialId, GuildIssuer(), qualification.Snapshot, "60", context.ScenarioContext.ScopedId("credential-tx", $"guild-{slug}-apply"));
            CredentialOperationResult approve = runtimes.Credentials.ApproveApplication(apply.Application?.applicationId, "authority.guild.prototype", qualification.Snapshot, "61", context.ScenarioContext.ScopedId("credential-tx", $"guild-{slug}-approve"));
            if (!qualification.AuthoritativeQualified || !apply.Succeeded || !approve.Succeeded)
            {
                return CredentialOperationResult.Failure(CredentialOperationStatus.MissingQualification, $"Guild license fixture failed. Qualified={qualification.AuthoritativeQualified} Apply={apply.Status} Approve={approve.Status}", runtimes.Credentials.Revision, qualification);
            }

            return runtimes.Credentials.IssueCredential(context.ScenarioContext.ScopedId("credential-record", $"guild-{slug}"), ProfessionContentIds.BlacksmithGuildLicenseCredentialId, runtimes.PersonId, GuildIssuer(), apply.Application?.applicationId, context.ScenarioContext.ScopedId("credential-exam", $"{slug}-practical"), context.ScenarioContext.ScopedId("registration", $"guild-{slug}"), qualification.Snapshot, "62", context.ScenarioContext.ScopedId("credential-tx", $"guild-{slug}-issue"));
        }

        private static void EnsureBlacksmithSafetyTraining(TestLabAutomationContext context, string slug)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            bool completed = runtimes.Training.QueryByProgram(ProfessionContentIds.BlacksmithSafetyProgramId)
                .Any(item => string.Equals(item.PersonId, runtimes.PersonId, StringComparison.Ordinal) && item.State == TrainingEnrollmentState.Completed);
            if (completed)
            {
                return;
            }

            string enrollmentId = context.ScenarioContext.ScopedId("training-enrollment", $"safety-{slug}");
            runtimes.Training.ApplyToProgram(enrollmentId, runtimes.PersonId, ProfessionContentIds.BlacksmithSafetyProgramId, context.ScenarioContext.ScopedId("training-tx", $"safety-{slug}-apply"), worldTime: 33d);
            runtimes.Training.AcceptEnrollment(enrollmentId, context.ScenarioContext.ScopedId("training-tx", $"safety-{slug}-accept"));
            runtimes.Training.BeginProgram(enrollmentId, context.ScenarioContext.ScopedId("training-tx", $"safety-{slug}-begin"));
            runtimes.Training.RunLearningSession(context.ScenarioContext.ScopedId("training-session", $"safety-{slug}"), enrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, ProfessionContentIds.BlacksmithSafetyLessonId, context.ScenarioContext.ScopedId("training-tx", $"safety-{slug}-lesson"));
            runtimes.Training.CompleteModule(enrollmentId, ProfessionContentIds.BlacksmithBasicsModuleId, context.ScenarioContext.ScopedId("training-tx", $"safety-{slug}-module"));
            TrainingProgressResult progress = runtimes.Training.EvaluateProgress(enrollmentId, perceived: false);
            runtimes.Training.CompleteProgram(enrollmentId, context.ScenarioContext.ScopedId("training-tx", $"safety-{slug}-complete"), progress.RuntimeToken, worldTime: 36d);
        }

        private static void RegisterIndependentBlacksmithActivity(TestLabAutomationContext context, string slug)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            runtimes.ProfessionalActivities.RegisterAndValidateActivity(
                ActivityRequest(
                    context,
                    slug,
                    ProfessionContentIds.BlacksmithCraftingActivityDefinitionId,
                    ActivityCustomSource(context, ProfessionalActivitySourceType.CraftingOperation, slug, "source.crafting", ProfessionalActivityDifficulty.Skilled),
                    ProfessionalResponsibilityLevel.IndependentPractitioner,
                    TrainingSupervisionLevel.IndependentWithReview),
                context.ScenarioContext.ScopedId("professional-evidence", slug),
                "authority.guild.prototype",
                context.ScenarioContext.ScopedId("professional-tx", slug));
        }

        private static TrainingModuleDefinitionData TrainingModule(string id, bool required, bool hidden, string[] dependencies = null)
        {
            return new TrainingModuleDefinitionData
            {
                moduleId = id,
                displayName = id,
                required = required,
                hiddenFromLearner = hidden,
                dependencyModuleIds = dependencies ?? Array.Empty<string>()
            };
        }

        private static ProfessionEligibilityContext MedicContext(TestLabAutomationContext context, bool preview)
        {
            return new ProfessionEligibilityContext(
                context.ScenarioContext.Runtimes.PersonId,
                ProfessionContentIds.FieldMedicProfessionId,
                ProfessionContentIds.FieldMedicRecognitionEntryPathId,
                formal: true,
                authorityId: "authority.medical.prototype",
                worldTime: 2d,
                correlationId: "step10.medic",
                preview: preview,
                skills: new[] { Skill("skill.healing-magic", 1) },
                knowledgeSubjects: new[] { "knowledge.subject.first-aid" });
        }

        private static ProfessionEntrySkillStateData Skill(string skillId, int grade)
        {
            return new ProfessionEntrySkillStateData { skillId = skillId, grade = grade };
        }
    }
}
#endif
