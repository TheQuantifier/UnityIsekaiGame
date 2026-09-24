using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;

namespace UnityIsekaiGame.Professions
{
    public static class PrototypeProfessionDefinitionFactory
    {
        public const string BlacksmithProfessionId = "profession.blacksmith";
        public const string DisassemblerProfessionId = "profession.disassembler";
        public const string SalvagerProfessionId = "profession.salvager";
        public const string FieldMedicProfessionId = "profession.field-medic";
        public const string ScoutProfessionId = "profession.scout";
        public const string SpyProfessionId = "profession.spy";
        public const string WeaponsmithSpecializationId = "profession-specialization.blacksmith.weaponsmith";
        public const string TraumaSpecializationId = "profession-specialization.field-medic.trauma-care";
        public const string BlacksmithSelfDeclaredEntryPathId = "profession-entry.blacksmith.self-declared";
        public const string FieldMedicRecognitionEntryPathId = "profession-entry.field-medic.recognition";
        public const string SpySecretEntryPathId = "profession-entry.spy.secret-self-declared";
        public const string WeaponsmithSpecializationEntryPathId = "profession-entry.blacksmith.weaponsmith";
        public const string BlacksmithReentryPathId = "profession-entry.blacksmith.reentry";
        public const string BlacksmithApprenticeshipProgramId = "training-program.blacksmith-apprenticeship";
        public const string BlacksmithApprenticeshipCurriculumId = "training-curriculum.blacksmith-apprenticeship";
        public const string BlacksmithSafetyProgramId = "training-program.blacksmith-safety";
        public const string BlacksmithSafetyCurriculumId = "training-curriculum.blacksmith-safety";
        public const string TrainingLessonTransferDefinitionId = "information-transfer.training.prototype-lecture";
        public const string TrainingDemonstrationTransferDefinitionId = "information-transfer.training.prototype-demonstration";
        public const string TrainingGuidedPracticeTransferDefinitionId = "information-transfer.training.prototype-guided-practice";
        public const string BlacksmithBasicsModuleId = "training-module.blacksmith.basics";
        public const string BlacksmithPracticeModuleId = "training-module.blacksmith.practical";
        public const string BlacksmithHiddenAssessmentModuleId = "training-module.blacksmith.hidden-assessment";
        public const string BlacksmithSafetyLessonId = "training-lesson.blacksmith.safety";
        public const string BlacksmithDemonstrationLessonId = "training-lesson.blacksmith.demonstration";
        public const string BlacksmithPracticalAssignmentId = "training-assignment.blacksmith.practice-forging";
        public const string BlacksmithCraftingActivityDefinitionId = "professional-activity.blacksmith.crafting";
        public const string BlacksmithProductionActivityDefinitionId = "professional-activity.blacksmith.production";
        public const string BlacksmithRepairActivityDefinitionId = "professional-activity.blacksmith.repair";
        public const string ItemRecoveryActivityDefinitionId = "professional-activity.item-recovery";
        public const string SalvagePickupActivityDefinitionId = "professional-activity.salvage-pickup";
        public const string BlacksmithSupervisedPracticeActivityDefinitionId = "professional-activity.blacksmith.supervised-practice";
        public const string BlacksmithTeachingActivityDefinitionId = "professional-activity.blacksmith.teaching";
        public const string BlacksmithExperimentationActivityDefinitionId = "professional-activity.blacksmith.experimentation";
        public const string BlacksmithApprenticeshipCertificateCredentialId = "credential.blacksmith.apprenticeship-certificate";
        public const string BlacksmithGuildLicenseCredentialId = "credential.blacksmith.guild-license";
        public const string BlacksmithSafetyCertificateCredentialId = "credential.blacksmith.safety-certificate";
        public const string BlacksmithPracticalExaminationId = "examination.blacksmith.practical";
        public const string BlacksmithWrittenExaminationId = "examination.blacksmith.written";
        public const string BlacksmithPracticePermissionId = "permission.profession.blacksmith.practice";
        public const string BlacksmithTeachPermissionId = "permission.profession.blacksmith.teach";
        public const string ForgeRestrictedStationPermissionId = "permission.station.forge.restricted";
        public const string BlacksmithRankApprenticeId = "profession-rank.blacksmith.apprentice";
        public const string BlacksmithRankJourneymanId = "profession-rank.blacksmith.journeyman";
        public const string BlacksmithRankMasterId = "profession-rank.blacksmith.master";
        public const string WeaponsmithRankApprenticeId = "profession-rank.blacksmith.weaponsmith.apprentice";
        public const string WeaponsmithRankMasterId = "profession-rank.blacksmith.weaponsmith.master";
        public const string BlacksmithRankLadderId = "profession-rank-ladder.blacksmith.guild";
        public const string WeaponsmithRankLadderId = "profession-rank-ladder.blacksmith.weaponsmith";
        public const string WeaponsmithMasteryId = "profession-mastery.blacksmith.weaponsmith.mastery";
        public const string BlacksmithMasterworkAchievementId = "achievement.blacksmith.masterwork.prototype";
        public const string BlacksmithExaminePermissionId = "permission.profession.blacksmith.examine";
        public const string BlacksmithSupervisePermissionId = "permission.profession.blacksmith.supervise";
        public const string AccessPublicId = "information-access.profession.public";
        public const string AccessSecretId = "information-access.profession.secret";
        public const string GuildOrganizationTypeId = "organization-type.guild";
        public const string ForgeOrganizationTypeId = "organization-type.forge";
        public const string TempleOrganizationTypeId = "organization-type.temple";
        public const string UniversityOrganizationTypeId = "organization-type.university";
        public const string GovernmentOrganizationTypeId = "organization-type.government";
        public const string IndependentOrganizationTypeId = "organization-type.independent";
        public const string PositionAppointAuthorityId = "authority.position.appoint";
        public const string PositionDutyAssignAuthorityId = "authority.position.assign-duty";
        public const string PositionSuperviseAuthorityId = "authority.position.supervise";
        public const string PositionRestrictedRecordsAuthorityId = "authority.position.restricted-records";
        public const string RoyalForgeSeniorSmithPositionId = "position.prototype.royal-forge.senior-smith";
        public const string GuildClerkPositionId = "position.prototype.guild.clerk";
        public const string ApprenticeSupervisorPositionId = "position.prototype.guild.apprentice-supervisor";
        public const string TempleHealerPositionId = "position.prototype.temple.healer";
        public const string UniversityLecturerPositionId = "position.prototype.university.lecturer";
        public const string IndependentContractorPositionId = "position.prototype.independent.contractor";
        public const string SeniorSmithCraftDutyId = "duty.prototype.royal-forge.senior-smith.craft";
        public const string SeniorSmithSuperviseDutyId = "duty.prototype.royal-forge.senior-smith.supervise";
        public const string GuildClerkRecordDutyId = "duty.prototype.guild.clerk.records";
        public const string GuildClerkCustomerDutyId = "duty.prototype.guild.clerk.customer-service";
        public const string ApprenticeSupervisorTeachingDutyId = "duty.prototype.guild.apprentice-supervisor.teaching";
        public const string TempleHealerMedicalDutyId = "duty.prototype.temple.healer.medical";
        public const string UniversityLecturerTeachingDutyId = "duty.prototype.university.lecturer.teaching";
        public const string IndependentContractorServiceDutyId = "duty.prototype.independent.contractor.service";
        public const string CareerProfessionEnteredTransitionId = "career-transition.prototype.profession-entered";
        public const string CareerEmploymentStartedTransitionId = "career-transition.prototype.employment-started";
        public const string CareerEmploymentEndedTransitionId = "career-transition.prototype.employment-ended";
        public const string CareerPromotionTransitionId = "career-transition.prototype.promotion";
        public const string CareerDemotionTransitionId = "career-transition.prototype.demotion";
        public const string CareerTransferTransitionId = "career-transition.prototype.transfer";
        public const string CareerResignationTransitionId = "career-transition.prototype.resignation";
        public const string CareerDismissalTransitionId = "career-transition.prototype.dismissal";
        public const string CareerRetirementTransitionId = "career-transition.prototype.retirement";
        public const string CareerReturnFromRetirementTransitionId = "career-transition.prototype.return-from-retirement";
        public const string CareerGapStartedTransitionId = "career-transition.prototype.career-gap-started";
        public const string CareerGapEndedTransitionId = "career-transition.prototype.career-gap-ended";
        public const string CareerChangeTransitionId = "career-transition.prototype.career-change";
        public const string CareerAchievementTransitionId = "career-transition.prototype.achievement";
        public const string CareerSetbackTransitionId = "career-transition.prototype.setback";
        public const string AspirationEnterBlacksmithProfessionId = "aspiration.prototype.enter-blacksmith";
        public const string AspirationReachMasterWeaponsmithId = "aspiration.prototype.reach-master-weaponsmith";
        public const string AspirationEarnGuildLicenseId = "aspiration.prototype.earn-guild-license";
        public const string AspirationObtainGuildPositionId = "aspiration.prototype.obtain-guild-position";
        public const string AspirationCreateMasterworkId = "aspiration.prototype.create-masterwork";
        public const string AspirationMakeDiscoveryId = "aspiration.prototype.make-discovery";
        public const string AspirationRetireSuccessfullyId = "aspiration.prototype.retire-successfully";
        public const string AspirationSecretSpyIdentityId = "aspiration.prototype.secret-spy-identity";
        public const string GoalEnterBlacksmithProfessionId = "goal.prototype.enter-blacksmith";
        public const string GoalCompleteBlacksmithApprenticeshipId = "goal.prototype.complete-blacksmith-apprenticeship";
        public const string GoalEarnBlacksmithGuildLicenseId = "goal.prototype.earn-blacksmith-guild-license";
        public const string GoalReachJourneymanRankId = "goal.prototype.reach-journeyman-rank";
        public const string GoalReachMasterRankId = "goal.prototype.reach-master-rank";
        public const string GoalObtainGuildClerkPositionId = "goal.prototype.obtain-guild-clerk-position";
        public const string GoalRecordCraftingActivityId = "goal.prototype.record-crafting-activity";
        public const string GoalProduceMasterworkId = "goal.prototype.produce-masterwork";
        public const string GoalConfirmDiscoveryId = "goal.prototype.confirm-discovery";
        public const string GoalRetireFromProfessionId = "goal.prototype.retire-from-profession";

        public static IReadOnlyList<ScriptableObject> CreateDefinitions()
        {
            List<ScriptableObject> definitions = new List<ScriptableObject>();
            definitions.Add(AccessPolicy(AccessPublicId, "Profession Public Access", InformationVisibilityClassification.Public, InformationDetailVisibilityPolicy.All));
            definitions.Add(AccessPolicy(AccessSecretId, "Profession Secret Access", InformationVisibilityClassification.Secret, InformationDetailVisibilityPolicy.Selected, new[] { "profession-id", "state" }, ProfessionInformationSubject.ProtectedFields));
            definitions.Add(TransferDefinition(TrainingLessonTransferDefinitionId, "Prototype Training Lecture Transfer", InformationTransferMode.Lecture));
            definitions.Add(TransferDefinition(TrainingDemonstrationTransferDefinitionId, "Prototype Training Demonstration Transfer", InformationTransferMode.Demonstration));
            definitions.Add(TransferDefinition(TrainingGuidedPracticeTransferDefinitionId, "Prototype Training Guided Practice Transfer", InformationTransferMode.GuidedPractice));

            ProfessionSpecializationDefinition weaponsmith = Specialization(WeaponsmithSpecializationId, BlacksmithProfessionId, "Weaponsmith", ProfessionRecognitionForm.Either, new[] { "skill.smithing" }, new[] { "knowledge.subject.weapons" }, activities: new[] { "production.activity.weapon-crafting" });
            ProfessionSpecializationDefinition trauma = Specialization(TraumaSpecializationId, FieldMedicProfessionId, "Trauma Care", ProfessionRecognitionForm.Formal, new[] { "skill.healing-magic" }, new[] { "knowledge.subject.injury-treatment" }, activities: new[] { "production.activity.medical-treatment" });
            definitions.Add(weaponsmith);
            definitions.Add(trauma);

            definitions.Add(Profession(BlacksmithProfessionId, "Blacksmith", ProfessionCategory.Craft, ProfessionRecognitionForm.Either, new[] { "skill.smithing" }, new[] { "knowledge.subject.metalwork" }, activities: new[] { "production.activity.forging" }, specializations: new[] { WeaponsmithSpecializationId }, authorities: new[] { "authority.guild.prototype" }));
            definitions.Add(Profession(DisassemblerProfessionId, "Disassembler", ProfessionCategory.Craft, ProfessionRecognitionForm.Informal, new[] { "skill.disassembly" }, new[] { "knowledge.subject.material-recovery" }, activities: new[] { "production.activity.item-recovery" }, allowFormal: false));
            definitions.Add(Profession(SalvagerProfessionId, "Salvager", ProfessionCategory.Craft, ProfessionRecognitionForm.Informal, new[] { "skill.salvaging" }, new[] { "knowledge.subject.salvage-gathering" }, activities: new[] { "production.activity.salvage-pickup" }, allowFormal: false));
            definitions.Add(Profession(FieldMedicProfessionId, "Field Medic", ProfessionCategory.Medical, ProfessionRecognitionForm.Formal, new[] { "skill.healing-magic" }, new[] { "knowledge.subject.first-aid" }, activities: new[] { "production.activity.medical-treatment" }, specializations: new[] { TraumaSpecializationId }, authorities: new[] { "authority.medical.prototype" }, allowSelf: true));
            definitions.Add(Profession(ScoutProfessionId, "Scout", ProfessionCategory.Exploration, ProfessionRecognitionForm.Informal, new[] { "skill.appraisal" }, new[] { "knowledge.subject.terrain" }, activities: new[] { "production.activity.reconnaissance" }, allowFormal: false));
            definitions.Add(Profession(SpyProfessionId, "Spy", ProfessionCategory.Criminal, ProfessionRecognitionForm.Informal, new[] { "skill.appraisal" }, new[] { "knowledge.subject.secrets" }, activities: new[] { "production.activity.infiltration" }, allowFormal: false, allowSecret: true, restricted: true, accessPolicy: AccessSecretId));
            definitions.Add(EntryPath(BlacksmithSelfDeclaredEntryPathId, BlacksmithProfessionId, "Blacksmith Self-Declared Practice", ProfessionEntryType.SelfDeclaredPractice, ProfessionEntryFormality.Informal, ProfessionSelfDeclarationPolicy.Required));
            definitions.Add(EntryPath(FieldMedicRecognitionEntryPathId, FieldMedicProfessionId, "Field Medic Recognition Application", ProfessionEntryType.RecognitionApplication, ProfessionEntryFormality.Formal, ProfessionSelfDeclarationPolicy.Disallowed, authorities: new[] { "authority.medical.prototype" }, skills: new[] { "skill.healing-magic" }, knowledge: new[] { "knowledge.subject.first-aid" }, requiresAuthority: true));
            definitions.Add(EntryPath(SpySecretEntryPathId, SpyProfessionId, "Secret Spy Self-Declared Practice", ProfessionEntryType.SelfDeclaredPractice, ProfessionEntryFormality.Informal, ProfessionSelfDeclarationPolicy.Required, accessKeys: new[] { "access.profession.secret-practice" }, secret: true, restricted: true, accessPolicy: AccessSecretId));
            definitions.Add(EntryPath(WeaponsmithSpecializationEntryPathId, BlacksmithProfessionId, "Weaponsmith Specialization Entry", ProfessionEntryType.Specialization, ProfessionEntryFormality.Informal, ProfessionSelfDeclarationPolicy.Disallowed, specialization: WeaponsmithSpecializationId, skills: new[] { "skill.smithing" }, requiredActiveProfessions: new[] { BlacksmithProfessionId }));
            definitions.Add(EntryPath(BlacksmithReentryPathId, BlacksmithProfessionId, "Blacksmith Reentry", ProfessionEntryType.Reentry, ProfessionEntryFormality.Informal, ProfessionSelfDeclarationPolicy.Disallowed, reentry: ProfessionReentryPolicy.AllowFormerInactiveAbandonedRetired));
            definitions.Add(ActivityDefinition(BlacksmithCraftingActivityDefinitionId, "Blacksmith Crafting Activity", ProfessionalActivityCategory.Crafting, new[] { BlacksmithProfessionId }, new[] { ProfessionalActivitySourceType.CraftingOperation }, specializationIds: new[] { WeaponsmithSpecializationId }, minQuality: 300, minDifficulty: ProfessionalActivityDifficulty.Routine, tags: new[] { "production.activity.forging" }));
            definitions.Add(ActivityDefinition(BlacksmithProductionActivityDefinitionId, "Blacksmith Production Activity", ProfessionalActivityCategory.Production, new[] { BlacksmithProfessionId }, new[] { ProfessionalActivitySourceType.ProductionJob, ProfessionalActivitySourceType.ProductionStage, ProfessionalActivitySourceType.WorkOrder }, specializationIds: new[] { WeaponsmithSpecializationId }, minQuality: 300, minDifficulty: ProfessionalActivityDifficulty.Routine, tags: new[] { "production.activity.forging" }));
            definitions.Add(ActivityDefinition(BlacksmithRepairActivityDefinitionId, "Blacksmith Repair Activity", ProfessionalActivityCategory.Repair, new[] { BlacksmithProfessionId }, new[] { ProfessionalActivitySourceType.RepairOperation }, minQuality: 250, minDifficulty: ProfessionalActivityDifficulty.Routine, tags: new[] { "production.activity.repair" }));
            definitions.Add(ActivityDefinition(ItemRecoveryActivityDefinitionId, "Disassembly Activity", ProfessionalActivityCategory.ItemRecovery, new[] { BlacksmithProfessionId, DisassemblerProfessionId }, new[] { ProfessionalActivitySourceType.ItemRecoveryOperation }, minQuality: 100, minDifficulty: ProfessionalActivityDifficulty.Trivial, tags: new[] { "source.item-recovery", "recovery.disassemble" }));
            definitions.Add(ActivityDefinition(SalvagePickupActivityDefinitionId, "Salvage Gathering Activity", ProfessionalActivityCategory.ItemRecovery, new[] { SalvagerProfessionId }, new[] { ProfessionalActivitySourceType.ItemRecoveryOperation }, minQuality: 100, minDifficulty: ProfessionalActivityDifficulty.Trivial, tags: new[] { "source.item-recovery", "recovery.salvage" }));
            definitions.Add(ActivityDefinition(BlacksmithSupervisedPracticeActivityDefinitionId, "Blacksmith Supervised Practice Activity", ProfessionalActivityCategory.SupervisedPractice, new[] { BlacksmithProfessionId }, new[] { ProfessionalActivitySourceType.TrainingPracticalAssignment, ProfessionalActivitySourceType.TrainingSupervisedWork }, supervision: ProfessionalSupervisionPolicy.RequiresSupervision, minQuality: 300, minDifficulty: ProfessionalActivityDifficulty.Routine, tags: new[] { "training.activity.practical" }, credit: ProfessionalCreditPolicy.Shared));
            definitions.Add(ActivityDefinition(BlacksmithTeachingActivityDefinitionId, "Blacksmith Teaching Activity", ProfessionalActivityCategory.Teaching, new[] { BlacksmithProfessionId }, new[] { ProfessionalActivitySourceType.TeachingSession }, minQuality: 300, minDifficulty: ProfessionalActivityDifficulty.Routine, tags: new[] { "training.activity.teaching" }, credit: ProfessionalCreditPolicy.Shared));
            definitions.Add(ActivityDefinition(BlacksmithExperimentationActivityDefinitionId, "Blacksmith Experimentation Activity", ProfessionalActivityCategory.Experimentation, new[] { BlacksmithProfessionId }, new[] { ProfessionalActivitySourceType.ExperimentTrial, ProfessionalActivitySourceType.DiscoveryClaim }, minQuality: 200, minDifficulty: ProfessionalActivityDifficulty.Skilled, tags: new[] { "production.activity.experimentation" }, failureCredit: ProfessionalFailureCreditPolicy.CountsAsFailedAttempt, credit: ProfessionalCreditPolicy.Shared));
            definitions.Add(BlacksmithApprenticeshipCurriculum());
            definitions.Add(TrainingProgram(
                BlacksmithApprenticeshipProgramId,
                "Blacksmith Apprenticeship",
                TrainingProgramCategory.Apprenticeship,
                TrainingProgramFormality.Formal,
                BlacksmithApprenticeshipCurriculumId,
                new[] { BlacksmithProfessionId },
                entryPaths: new[] { BlacksmithSelfDeclaredEntryPathId },
                instructors: new[]
                {
                    InstructorRequirement("training-instructor-requirement.blacksmith.master", TrainingInstructorRoleKind.Master, BlacksmithProfessionId, authority: "authority.guild.prototype", capacity: 1)
                },
                durationHours: 240d,
                organization: "organization.prototype.guild",
                stations: new[] { "production-station.prototype.forge" },
                completionRequirements: new[] { BlacksmithBasicsModuleId, BlacksmithPracticeModuleId, BlacksmithHiddenAssessmentModuleId },
                accessPolicy: AccessPublicId));
            definitions.Add(BlacksmithSafetyCurriculum());
            definitions.Add(TrainingProgram(
                BlacksmithSafetyProgramId,
                "Blacksmith Safety Training",
                TrainingProgramCategory.SafetyTraining,
                TrainingProgramFormality.Informal,
                BlacksmithSafetyCurriculumId,
                new[] { BlacksmithProfessionId },
                entryPaths: new[] { BlacksmithSelfDeclaredEntryPathId },
                instructors: new[]
                {
                    InstructorRequirement("training-instructor-requirement.blacksmith.safety", TrainingInstructorRoleKind.Instructor, BlacksmithProfessionId, capacity: 8)
                },
                durationHours: 4d,
                completionRequirements: new[] { BlacksmithBasicsModuleId },
                accessPolicy: AccessPublicId));
            definitions.Add(Examination(
                BlacksmithPracticalExaminationId,
                "Blacksmith Practical Certification Exam",
                new[] { BlacksmithApprenticeshipCertificateCredentialId, BlacksmithGuildLicenseCredentialId },
                CredentialAssessmentCategory.Practical,
                700,
                3,
                new[] { "authority.guild.prototype" },
                knowledgeSubjects: new[] { "knowledge.subject.metalwork", "knowledge.subject.forge-safety" },
                practicalActivityIds: new[] { BlacksmithCraftingActivityDefinitionId, BlacksmithSupervisedPracticeActivityDefinitionId },
                policyId: AccessPublicId));
            definitions.Add(Examination(
                BlacksmithWrittenExaminationId,
                "Blacksmith Written Safety Exam",
                new[] { BlacksmithGuildLicenseCredentialId, BlacksmithSafetyCertificateCredentialId },
                CredentialAssessmentCategory.Written,
                650,
                3,
                new[] { "authority.guild.prototype" },
                knowledgeSubjects: new[] { "knowledge.subject.metalwork", "knowledge.subject.forge-safety" },
                policyId: AccessPublicId));
            definitions.Add(Credential(
                BlacksmithSafetyCertificateCredentialId,
                "Blacksmith Safety Certificate",
                CredentialCategory.Certificate,
                new[] { BlacksmithProfessionId },
                new[] { "authority.guild.prototype" },
                new[] { CredentialIssuerAuthorityKind.Guild },
                trainingProgramIds: new[] { BlacksmithSafetyProgramId },
                examinationDefinitionIds: new[] { BlacksmithWrittenExaminationId },
                permissions: new[] { ForgeRestrictedStationPermissionId },
                renewal: CredentialRenewalPolicy.RenewWithNewExamination,
                policyId: AccessPublicId));
            definitions.Add(Credential(
                BlacksmithApprenticeshipCertificateCredentialId,
                "Blacksmith Apprenticeship Certificate",
                CredentialCategory.Certificate,
                new[] { BlacksmithProfessionId },
                new[] { "authority.guild.prototype" },
                new[] { CredentialIssuerAuthorityKind.Guild },
                trainingProgramIds: new[] { BlacksmithApprenticeshipProgramId },
                experience: new ProfessionalExperienceRequirementData
                {
                    professionId = BlacksmithProfessionId,
                    specializationId = WeaponsmithSpecializationId,
                    minimumValidatedActivities = 1,
                    minimumSupervisedActivities = 1,
                    minimumQuality = 500,
                    minimumDifficulty = ProfessionalActivityDifficulty.Routine
                },
                examinationDefinitionIds: new[] { BlacksmithPracticalExaminationId },
                permissions: new[] { BlacksmithPracticePermissionId },
                specializationIds: new[] { WeaponsmithSpecializationId },
                renewal: CredentialRenewalPolicy.NotRenewable,
                policyId: AccessPublicId));
            definitions.Add(Credential(
                BlacksmithGuildLicenseCredentialId,
                "Blacksmith Guild License",
                CredentialCategory.License,
                new[] { BlacksmithProfessionId },
                new[] { "authority.guild.prototype" },
                new[] { CredentialIssuerAuthorityKind.Guild },
                trainingProgramIds: new[] { BlacksmithSafetyProgramId },
                experience: new ProfessionalExperienceRequirementData
                {
                    professionId = BlacksmithProfessionId,
                    specializationId = WeaponsmithSpecializationId,
                    minimumValidatedActivities = 2,
                    minimumIndependentActivities = 1,
                    minimumSupervisedActivities = 1,
                    minimumQuality = 600,
                    minimumDifficulty = ProfessionalActivityDifficulty.Routine,
                    requireRecentActivity = true
                },
                examinationDefinitionIds: new[] { BlacksmithPracticalExaminationId, BlacksmithWrittenExaminationId },
                permissions: new[] { BlacksmithPracticePermissionId, BlacksmithTeachPermissionId, ForgeRestrictedStationPermissionId },
                specializationIds: new[] { WeaponsmithSpecializationId },
                formalRecognition: true,
                durationHours: 8760d,
                expiration: CredentialExpirationPolicy.FixedDuration,
                renewal: CredentialRenewalPolicy.RenewWithRecentExperience,
                policyId: AccessPublicId));
            definitions.Add(Rank(
                BlacksmithRankApprenticeId,
                "Apprentice Blacksmith",
                BlacksmithProfessionId,
                10,
                ProfessionalRankCategory.Apprentice,
                requiredTraining: new[] { BlacksmithApprenticeshipProgramId },
                requiredAuthorities: new[] { "authority.guild.prototype" },
                permissions: new[] { BlacksmithPracticePermissionId },
                canSupervise: false));
            definitions.Add(Rank(
                BlacksmithRankJourneymanId,
                "Journeyman Blacksmith",
                BlacksmithProfessionId,
                20,
                ProfessionalRankCategory.Journeyman,
                priorRanks: new[] { BlacksmithRankApprenticeId },
                requiredCredentials: new[] { BlacksmithApprenticeshipCertificateCredentialId },
                requiredTraining: new[] { BlacksmithApprenticeshipProgramId },
                experience: new ProfessionalExperienceRequirementData
                {
                    professionId = BlacksmithProfessionId,
                    minimumValidatedActivities = 1,
                    minimumSupervisedActivities = 1,
                    minimumQuality = 500,
                    minimumDifficulty = ProfessionalActivityDifficulty.Routine
                },
                requiredExaminations: new[] { BlacksmithPracticalExaminationId },
                requiredAuthorities: new[] { "authority.guild.prototype" },
                permissions: new[] { BlacksmithPracticePermissionId, ForgeRestrictedStationPermissionId },
                canSupervise: true,
                apprenticeCapacity: 1));
            definitions.Add(Rank(
                BlacksmithRankMasterId,
                "Master Blacksmith",
                BlacksmithProfessionId,
                30,
                ProfessionalRankCategory.Master,
                priorRanks: new[] { BlacksmithRankJourneymanId },
                requiredCredentials: new[] { BlacksmithGuildLicenseCredentialId },
                experience: new ProfessionalExperienceRequirementData
                {
                    professionId = BlacksmithProfessionId,
                    minimumValidatedActivities = 2,
                    minimumIndependentActivities = 1,
                    minimumQuality = 650,
                    minimumDifficulty = ProfessionalActivityDifficulty.Skilled
                },
                requiredExaminations: new[] { BlacksmithPracticalExaminationId, BlacksmithWrittenExaminationId },
                requiredAuthorities: new[] { "authority.guild.prototype" },
                permissions: new[] { BlacksmithPracticePermissionId, BlacksmithTeachPermissionId, ForgeRestrictedStationPermissionId, BlacksmithSupervisePermissionId, BlacksmithExaminePermissionId },
                canTeach: true,
                canSupervise: true,
                apprenticeCapacity: 3));
            definitions.Add(Rank(
                WeaponsmithRankApprenticeId,
                "Apprentice Weaponsmith",
                BlacksmithProfessionId,
                10,
                ProfessionalRankCategory.Apprentice,
                specialization: WeaponsmithSpecializationId,
                priorRanks: new[] { BlacksmithRankApprenticeId },
                requiredCredentials: new[] { BlacksmithApprenticeshipCertificateCredentialId },
                experience: new ProfessionalExperienceRequirementData
                {
                    professionId = BlacksmithProfessionId,
                    specializationId = WeaponsmithSpecializationId,
                    minimumValidatedActivities = 1,
                    minimumSupervisedActivities = 1,
                    minimumQuality = 500,
                    minimumDifficulty = ProfessionalActivityDifficulty.Routine
                },
                requiredAuthorities: new[] { "authority.guild.prototype" },
                permissions: new[] { BlacksmithPracticePermissionId }));
            definitions.Add(Rank(
                WeaponsmithRankMasterId,
                "Master Weaponsmith",
                BlacksmithProfessionId,
                20,
                ProfessionalRankCategory.Master,
                specialization: WeaponsmithSpecializationId,
                priorRanks: new[] { WeaponsmithRankApprenticeId, BlacksmithRankJourneymanId },
                requiredCredentials: new[] { BlacksmithGuildLicenseCredentialId },
                experience: new ProfessionalExperienceRequirementData
                {
                    professionId = BlacksmithProfessionId,
                    specializationId = WeaponsmithSpecializationId,
                    minimumValidatedActivities = 2,
                    minimumIndependentActivities = 1,
                    minimumQuality = 650,
                    minimumDifficulty = ProfessionalActivityDifficulty.Skilled
                },
                requiredExaminations: new[] { BlacksmithPracticalExaminationId },
                requiredAuthorities: new[] { "authority.guild.prototype" },
                permissions: new[] { BlacksmithPracticePermissionId, BlacksmithTeachPermissionId, BlacksmithSupervisePermissionId },
                canTeach: true,
                canSupervise: true,
                apprenticeCapacity: 2));
            definitions.Add(Ladder(BlacksmithRankLadderId, "Blacksmith Guild Rank Ladder", BlacksmithProfessionId, new[] { BlacksmithRankApprenticeId, BlacksmithRankJourneymanId, BlacksmithRankMasterId }, terminalRanks: new[] { BlacksmithRankMasterId }, demotionRanks: new[] { BlacksmithRankJourneymanId, BlacksmithRankApprenticeId }));
            definitions.Add(Ladder(WeaponsmithRankLadderId, "Weaponsmith Rank Ladder", BlacksmithProfessionId, new[] { WeaponsmithRankApprenticeId, WeaponsmithRankMasterId }, WeaponsmithSpecializationId, terminalRanks: new[] { WeaponsmithRankMasterId }, demotionRanks: new[] { WeaponsmithRankApprenticeId }));
            definitions.Add(Mastery(
                WeaponsmithMasteryId,
                "Weaponsmith Mastery",
                BlacksmithProfessionId,
                WeaponsmithRankMasterId,
                WeaponsmithSpecializationId,
                new ProfessionalExperienceRequirementData
                {
                    professionId = BlacksmithProfessionId,
                    specializationId = WeaponsmithSpecializationId,
                    minimumValidatedActivities = 2,
                    minimumIndependentActivities = 1,
                    minimumQuality = 700,
                    minimumDifficulty = ProfessionalActivityDifficulty.Skilled
                },
                breadth: 1,
                depthQuality: 700,
                independentWork: 1,
                teachingOrLeadership: 0,
                credentials: new[] { BlacksmithGuildLicenseCredentialId },
                examinations: new[] { BlacksmithPracticalExaminationId },
                achievements: new[] { BlacksmithMasterworkAchievementId },
                authorities: new[] { "authority.guild.prototype" }));
            definitions.Add(Position(
                RoyalForgeSeniorSmithPositionId,
                "Royal Forge Senior Smith",
                PositionCategory.Specialist,
                new[] { BlacksmithProfessionId },
                new[] { WeaponsmithSpecializationId },
                ForgeOrganizationTypeId,
                ranks: new[] { BlacksmithRankJourneymanId },
                credentials: new[] { BlacksmithGuildLicenseCredentialId },
                trainingPrograms: new[] { BlacksmithApprenticeshipProgramId, BlacksmithSafetyProgramId },
                experience: new ProfessionalExperienceRequirementData
                {
                    professionId = BlacksmithProfessionId,
                    specializationId = WeaponsmithSpecializationId,
                    minimumValidatedActivities = 2,
                    minimumIndependentActivities = 1,
                    minimumQuality = 600,
                    minimumDifficulty = ProfessionalActivityDifficulty.Routine
                },
                duties: new[] { SeniorSmithCraftDutyId, SeniorSmithSuperviseDutyId },
                authorities: new[] { PositionDutyAssignAuthorityId, PositionSuperviseAuthorityId, ForgeRestrictedStationPermissionId },
                classification: EmploymentClassification.FullTime,
                maxHolders: 1,
                exclusive: true,
                compensationPolicy: "compensation-policy.foundation.royal-forge.senior-smith",
                costCenter: "cost-center.foundation.royal-forge"));
            definitions.Add(Position(
                GuildClerkPositionId,
                "Adventurer Guild Clerk",
                PositionCategory.Administrator,
                professions: null,
                specializations: null,
                organizationTypeId: GuildOrganizationTypeId,
                duties: new[] { GuildClerkRecordDutyId, GuildClerkCustomerDutyId },
                authorities: new[] { PositionRestrictedRecordsAuthorityId },
                classification: EmploymentClassification.PartTime,
                maxHolders: 2,
                allowShared: true,
                exclusive: false,
                compensationPolicy: "compensation-policy.foundation.guild.clerk"));
            definitions.Add(Position(
                ApprenticeSupervisorPositionId,
                "Guild Apprentice Supervisor",
                PositionCategory.Supervisor,
                new[] { BlacksmithProfessionId },
                new[] { WeaponsmithSpecializationId },
                GuildOrganizationTypeId,
                ranks: new[] { BlacksmithRankMasterId },
                credentials: new[] { BlacksmithGuildLicenseCredentialId },
                trainingPrograms: new[] { BlacksmithApprenticeshipProgramId },
                duties: new[] { ApprenticeSupervisorTeachingDutyId },
                authorities: new[] { PositionAppointAuthorityId, PositionDutyAssignAuthorityId, PositionSuperviseAuthorityId, BlacksmithTeachPermissionId },
                classification: EmploymentClassification.Appointed,
                maxHolders: 1,
                exclusive: false,
                supervisorCapacity: 4));
            definitions.Add(Position(TempleHealerPositionId, "Temple Healer", PositionCategory.Religious, new[] { FieldMedicProfessionId }, new[] { TraumaSpecializationId }, TempleOrganizationTypeId, duties: new[] { TempleHealerMedicalDutyId }, authorities: new[] { "authority.medical.prototype" }, classification: EmploymentClassification.ReligiousService, exclusive: false));
            definitions.Add(Position(UniversityLecturerPositionId, "University Lecturer", PositionCategory.Instructor, new[] { BlacksmithProfessionId }, null, UniversityOrganizationTypeId, ranks: new[] { BlacksmithRankMasterId }, duties: new[] { UniversityLecturerTeachingDutyId }, authorities: new[] { BlacksmithTeachPermissionId }, classification: EmploymentClassification.ContractFoundation, exclusive: false));
            definitions.Add(Position(IndependentContractorPositionId, "Independent Contractor Foundation", PositionCategory.Contractor, null, null, IndependentOrganizationTypeId, duties: new[] { IndependentContractorServiceDutyId }, classification: EmploymentClassification.IndependentServiceFoundation, maxHolders: 99, allowShared: true, exclusive: false, contractTerms: "contract-terms.foundation.independent-service"));
            definitions.Add(Duty(SeniorSmithCraftDutyId, RoyalForgeSeniorSmithPositionId, "Craft Royal Forge Orders", DutyCategory.Crafting, PrototypeProfessionDefinitionFactory.BlacksmithProfessionId, PositionDutyAssignAuthorityId, allowDelegation: false));
            definitions.Add(Duty(SeniorSmithSuperviseDutyId, RoyalForgeSeniorSmithPositionId, "Supervise Forge Apprentices", DutyCategory.Supervision, PrototypeProfessionDefinitionFactory.BlacksmithProfessionId, PositionSuperviseAuthorityId, allowDelegation: true, requireSupervision: false));
            definitions.Add(Duty(GuildClerkRecordDutyId, GuildClerkPositionId, "Maintain Guild Records", DutyCategory.Recordkeeping, authorityId: PositionRestrictedRecordsAuthorityId, isSecret: true, policyId: AccessSecretId));
            definitions.Add(Duty(GuildClerkCustomerDutyId, GuildClerkPositionId, "Receive Adventurers", DutyCategory.CustomerInteraction, requireEvidence: false));
            definitions.Add(Duty(ApprenticeSupervisorTeachingDutyId, ApprenticeSupervisorPositionId, "Teach and Review Apprentices", DutyCategory.Teaching, BlacksmithProfessionId, BlacksmithTeachPermissionId, allowDelegation: true));
            definitions.Add(Duty(TempleHealerMedicalDutyId, TempleHealerPositionId, "Treat Temple Patients", DutyCategory.Medical, FieldMedicProfessionId, "authority.medical.prototype"));
            definitions.Add(Duty(UniversityLecturerTeachingDutyId, UniversityLecturerPositionId, "Deliver University Lectures", DutyCategory.Teaching, BlacksmithProfessionId, BlacksmithTeachPermissionId, allowDelegation: true));
            definitions.Add(Duty(IndependentContractorServiceDutyId, IndependentContractorPositionId, "Complete Contracted Service", DutyCategory.Service, requireEvidence: true));
            definitions.Add(CareerTransition(CareerProfessionEnteredTransitionId, "Profession Entered", CareerTransitionCategory.ProfessionEntered, destinations: new[] { CareerEpisodeState.Active }, required: new[] { CareerTransitionSourceRecordType.ProfessionRelationship }, professions: new[] { BlacksmithProfessionId }));
            definitions.Add(CareerTransition(CareerEmploymentStartedTransitionId, "Employment Started", CareerTransitionCategory.EmploymentStarted, destinations: new[] { CareerEpisodeState.Active }, required: new[] { CareerTransitionSourceRecordType.Employment, CareerTransitionSourceRecordType.Position }, requiresAuthority: true));
            definitions.Add(CareerTransition(CareerEmploymentEndedTransitionId, "Employment Ended", CareerTransitionCategory.EmploymentEnded, sources: new[] { CareerEpisodeState.Active, CareerEpisodeState.Suspended }, required: new[] { CareerTransitionSourceRecordType.Employment }));
            definitions.Add(CareerTransition(CareerPromotionTransitionId, "Career Promotion", CareerTransitionCategory.Promotion, sources: new[] { CareerEpisodeState.Active }, destinations: new[] { CareerEpisodeState.Active }, required: new[] { CareerTransitionSourceRecordType.Rank }, ranks: new[] { BlacksmithRankJourneymanId, BlacksmithRankMasterId }, requiresAuthority: true));
            definitions.Add(CareerTransition(CareerDemotionTransitionId, "Career Demotion", CareerTransitionCategory.Demotion, sources: new[] { CareerEpisodeState.Active }, destinations: new[] { CareerEpisodeState.Active }, required: new[] { CareerTransitionSourceRecordType.Rank }, ranks: new[] { BlacksmithRankApprenticeId, BlacksmithRankJourneymanId }, requiresAuthority: true, disputed: true));
            definitions.Add(CareerTransition(CareerTransferTransitionId, "Career Transfer", CareerTransitionCategory.Transfer, sources: new[] { CareerEpisodeState.Active }, destinations: new[] { CareerEpisodeState.Active }, required: new[] { CareerTransitionSourceRecordType.Employment, CareerTransitionSourceRecordType.Position }, requiresAuthority: true));
            definitions.Add(CareerTransition(CareerResignationTransitionId, "Resignation", CareerTransitionCategory.Resignation, sources: new[] { CareerEpisodeState.Active, CareerEpisodeState.Suspended }, required: new[] { CareerTransitionSourceRecordType.Employment }));
            definitions.Add(CareerTransition(CareerDismissalTransitionId, "Dismissal", CareerTransitionCategory.Dismissal, sources: new[] { CareerEpisodeState.Active, CareerEpisodeState.Suspended }, required: new[] { CareerTransitionSourceRecordType.Employment }, requiresAuthority: true, disputed: true, allowSecret: true, policyId: AccessSecretId));
            definitions.Add(CareerTransition(CareerRetirementTransitionId, "Retirement", CareerTransitionCategory.Retirement, sources: new[] { CareerEpisodeState.Active }, destinations: new[] { CareerEpisodeState.Active, CareerEpisodeState.Retired }, required: new[] { CareerTransitionSourceRecordType.Employment }));
            definitions.Add(CareerTransition(CareerReturnFromRetirementTransitionId, "Return From Retirement", CareerTransitionCategory.ReturnFromRetirement, sources: new[] { CareerEpisodeState.Active, CareerEpisodeState.Retired }, destinations: new[] { CareerEpisodeState.Active }, required: new[] { CareerTransitionSourceRecordType.CareerEpisode, CareerTransitionSourceRecordType.Employment }));
            definitions.Add(CareerTransition(CareerGapStartedTransitionId, "Career Gap Started", CareerTransitionCategory.CareerGapStarted, destinations: new[] { CareerEpisodeState.Active }));
            definitions.Add(CareerTransition(CareerGapEndedTransitionId, "Career Gap Ended", CareerTransitionCategory.CareerGapEnded, sources: new[] { CareerEpisodeState.Active }, required: new[] { CareerTransitionSourceRecordType.CareerEpisode }));
            definitions.Add(CareerTransition(CareerChangeTransitionId, "Career Change", CareerTransitionCategory.CareerChange, sources: new[] { CareerEpisodeState.Active, CareerEpisodeState.Ended, CareerEpisodeState.Retired }, destinations: new[] { CareerEpisodeState.Active }, required: new[] { CareerTransitionSourceRecordType.ProfessionRelationship, CareerTransitionSourceRecordType.Employment }));
            definitions.Add(CareerTransition(CareerAchievementTransitionId, "Career Achievement", CareerTransitionCategory.Achievement, sources: new[] { CareerEpisodeState.Active }, required: new[] { CareerTransitionSourceRecordType.ProfessionalActivity, CareerTransitionSourceRecordType.ExperienceEvidence }));
            definitions.Add(CareerTransition(CareerSetbackTransitionId, "Career Setback", CareerTransitionCategory.Setback, sources: new[] { CareerEpisodeState.Active }, required: new[] { CareerTransitionSourceRecordType.ProfessionalActivity }, disputed: true, allowSecret: true, policyId: AccessSecretId));
            definitions.Add(LifeGoal(GoalEnterBlacksmithProfessionId, "Enter Blacksmith Profession", LifeGoalCategory.EnterProfession, LifePathTargetSubjectType.Profession, professions: new[] { BlacksmithProfessionId }, compatibleAspirations: new[] { AspirationEnterBlacksmithProfessionId }));
            definitions.Add(LifeGoal(GoalCompleteBlacksmithApprenticeshipId, "Complete Blacksmith Apprenticeship", LifeGoalCategory.CompleteApprenticeship, LifePathTargetSubjectType.TrainingProgram, trainingPrograms: new[] { BlacksmithApprenticeshipProgramId }, compatibleAspirations: new[] { AspirationEnterBlacksmithProfessionId, AspirationReachMasterWeaponsmithId }, dependencies: new[] { GoalEnterBlacksmithProfessionId }));
            definitions.Add(LifeGoal(GoalEarnBlacksmithGuildLicenseId, "Earn Blacksmith Guild License", LifeGoalCategory.EarnCredential, LifePathTargetSubjectType.Credential, credentials: new[] { BlacksmithGuildLicenseCredentialId }, compatibleAspirations: new[] { AspirationEarnGuildLicenseId, AspirationReachMasterWeaponsmithId }, dependencies: new[] { GoalCompleteBlacksmithApprenticeshipId }));
            definitions.Add(LifeGoal(GoalReachJourneymanRankId, "Reach Journeyman Rank", LifeGoalCategory.ReachRank, LifePathTargetSubjectType.Rank, ranks: new[] { BlacksmithRankJourneymanId }, compatibleAspirations: new[] { AspirationReachMasterWeaponsmithId }, dependencies: new[] { GoalEarnBlacksmithGuildLicenseId }));
            definitions.Add(LifeGoal(GoalReachMasterRankId, "Reach Master Rank", LifeGoalCategory.ReachRank, LifePathTargetSubjectType.Rank, ranks: new[] { BlacksmithRankMasterId, WeaponsmithRankMasterId }, compatibleAspirations: new[] { AspirationReachMasterWeaponsmithId }, dependencies: new[] { GoalReachJourneymanRankId }));
            definitions.Add(LifeGoal(GoalObtainGuildClerkPositionId, "Obtain Guild Clerk Position", LifeGoalCategory.ObtainPosition, LifePathTargetSubjectType.Position, positions: new[] { GuildClerkPositionId }, compatibleAspirations: new[] { AspirationObtainGuildPositionId }));
            definitions.Add(LifeGoal(GoalRecordCraftingActivityId, "Record Crafting Activity", LifeGoalCategory.GainExperience, LifePathTargetSubjectType.ProfessionalActivity, activities: new[] { BlacksmithCraftingActivityDefinitionId }, compatibleAspirations: new[] { AspirationCreateMasterworkId }));
            definitions.Add(LifeGoal(GoalProduceMasterworkId, "Produce Masterwork", LifeGoalCategory.ProduceQualifyingItem, LifePathTargetSubjectType.ProfessionalActivity, activities: new[] { BlacksmithCraftingActivityDefinitionId }, compatibleAspirations: new[] { AspirationCreateMasterworkId }, dependencies: new[] { GoalRecordCraftingActivityId }, alternatives: new[] { GoalReachMasterRankId }));
            definitions.Add(LifeGoal(GoalConfirmDiscoveryId, "Confirm Discovery", LifeGoalCategory.MakeDiscovery, LifePathTargetSubjectType.Discovery, activities: new[] { BlacksmithExperimentationActivityDefinitionId }, compatibleAspirations: new[] { AspirationMakeDiscoveryId }, conflicts: new[] { "life-path.conflict.discovery-secrecy" }));
            definitions.Add(LifeGoal(GoalRetireFromProfessionId, "Retire From Profession", LifeGoalCategory.Retire, LifePathTargetSubjectType.CareerTransition, compatibleAspirations: new[] { AspirationRetireSuccessfullyId }));
            definitions.Add(Aspiration(AspirationEnterBlacksmithProfessionId, "Become a Blacksmith", AspirationCategory.EnterProfession, LifePathTargetSubjectType.Profession, professions: new[] { BlacksmithProfessionId }, suggestedGoals: new[] { GoalEnterBlacksmithProfessionId, GoalCompleteBlacksmithApprenticeshipId }, conflicts: new[] { "life-path.profession.blacksmith" }));
            definitions.Add(Aspiration(AspirationReachMasterWeaponsmithId, "Become a Master Weaponsmith", AspirationCategory.AchieveMastery, LifePathTargetSubjectType.Rank, professions: new[] { BlacksmithProfessionId }, specializations: new[] { WeaponsmithSpecializationId }, ranks: new[] { BlacksmithRankMasterId, WeaponsmithRankMasterId }, suggestedGoals: new[] { GoalReachJourneymanRankId, GoalReachMasterRankId }, conflicts: new[] { "life-path.mastery.weaponsmith" }));
            definitions.Add(Aspiration(AspirationEarnGuildLicenseId, "Earn a Guild License", AspirationCategory.EarnCredential, LifePathTargetSubjectType.Credential, professions: new[] { BlacksmithProfessionId }, credentials: new[] { BlacksmithGuildLicenseCredentialId }, suggestedGoals: new[] { GoalEarnBlacksmithGuildLicenseId }));
            definitions.Add(Aspiration(AspirationObtainGuildPositionId, "Obtain a Guild Position", AspirationCategory.ObtainPosition, LifePathTargetSubjectType.Position, positions: new[] { GuildClerkPositionId }, organizations: new[] { "organization.prototype.guild" }, suggestedGoals: new[] { GoalObtainGuildClerkPositionId }));
            definitions.Add(Aspiration(AspirationCreateMasterworkId, "Create a Masterwork", AspirationCategory.CreateMasterwork, LifePathTargetSubjectType.ProfessionalActivity, professions: new[] { BlacksmithProfessionId }, suggestedGoals: new[] { GoalRecordCraftingActivityId, GoalProduceMasterworkId }));
            definitions.Add(Aspiration(AspirationMakeDiscoveryId, "Make a Professional Discovery", AspirationCategory.MakeDiscovery, LifePathTargetSubjectType.Discovery, professions: new[] { BlacksmithProfessionId }, suggestedGoals: new[] { GoalConfirmDiscoveryId }, allowSecret: true, conflicts: new[] { "life-path.conflict.discovery-secrecy" }));
            definitions.Add(Aspiration(AspirationRetireSuccessfullyId, "Retire Successfully", AspirationCategory.RetireSuccessfully, LifePathTargetSubjectType.CareerTransition, professions: new[] { BlacksmithProfessionId }, suggestedGoals: new[] { GoalRetireFromProfessionId }));
            definitions.Add(Aspiration(AspirationSecretSpyIdentityId, "Maintain Secret Spy Identity", AspirationCategory.ProfessionalIdentity, LifePathTargetSubjectType.Profession, professions: new[] { SpyProfessionId }, suggestedGoals: Array.Empty<string>(), allowSecret: true, conflicts: new[] { "life-path.secret-identity" }, policyId: AccessSecretId));
            return definitions;
        }

        public static DefinitionRegistry AddMissingPrototypeProfessionDefinitions(DefinitionRegistry baseRegistry)
        {
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            HashSet<string> existing = new HashSet<string>(definitions.Select(definition => definition.Id), StringComparer.Ordinal);
            foreach (ScriptableObject definition in CreateDefinitions())
            {
                if (definition is IGameDefinition gameDefinition && existing.Add(gameDefinition.Id))
                {
                    definitions.Add(gameDefinition);
                }
            }

            return new DefinitionRegistry(definitions);
        }

        private static ProfessionDefinition Profession(string id, string name, ProfessionCategory category, ProfessionRecognitionForm form, string[] skills = null, string[] knowledge = null, string[] capabilities = null, string[] activities = null, string[] specializations = null, string[] authorities = null, bool allowSelf = true, bool allowFormal = true, bool allowSecret = false, bool illegal = false, bool restricted = false, string accessPolicy = AccessPublicId)
        {
            ProfessionDefinition definition = ScriptableObject.CreateInstance<ProfessionDefinition>();
            definition.name = name.Replace(" ", string.Empty) + "Profession";
            definition.DevelopmentConfigure(id, name, category, form, skills, knowledge, capabilities, activities, specializations, authorities, allowSelf, allowFormal, allowSecret, illegal, restricted, accessPolicy);
            return definition;
        }

        private static ProfessionSpecializationDefinition Specialization(string id, string parentId, string name, ProfessionRecognitionForm form, string[] skills = null, string[] knowledge = null, string[] capabilities = null, string[] activities = null, string accessPolicy = AccessPublicId)
        {
            ProfessionSpecializationDefinition definition = ScriptableObject.CreateInstance<ProfessionSpecializationDefinition>();
            definition.name = name.Replace(" ", string.Empty) + "Specialization";
            definition.DevelopmentConfigure(id, parentId, name, form, skills, knowledge, capabilities, activities, accessPolicy);
            return definition;
        }

        private static AspirationDefinition Aspiration(
            string id,
            string name,
            AspirationCategory category,
            LifePathTargetSubjectType targetType,
            string[] professions = null,
            string[] specializations = null,
            string[] ranks = null,
            string[] credentials = null,
            string[] positions = null,
            string[] organizations = null,
            string[] suggestedGoals = null,
            bool allowSecret = false,
            string[] conflicts = null,
            string policyId = AccessPublicId)
        {
            AspirationDefinition definition = ScriptableObject.CreateInstance<AspirationDefinition>();
            definition.name = name.Replace(" ", string.Empty) + "Aspiration";
            definition.DevelopmentConfigure(id, name, category, targetType, professions, specializations, ranks, credentials, positions, organizations, suggestedGoals, conflicts: conflicts, allowSecret: allowSecret, policyId: policyId);
            return definition;
        }

        private static LifeGoalDefinition LifeGoal(
            string id,
            string name,
            LifeGoalCategory category,
            LifePathTargetSubjectType targetType,
            string[] professions = null,
            string[] trainingPrograms = null,
            string[] credentials = null,
            string[] ranks = null,
            string[] positions = null,
            string[] activities = null,
            string[] compatibleAspirations = null,
            string[] dependencies = null,
            string[] alternatives = null,
            string[] conflicts = null,
            string policyId = AccessPublicId)
        {
            LifeGoalDefinition definition = ScriptableObject.CreateInstance<LifeGoalDefinition>();
            definition.name = name.Replace(" ", string.Empty) + "LifeGoal";
            definition.DevelopmentConfigure(id, name, category, targetType, professions, trainingPrograms, credentials, ranks, positions, activities, compatibleAspirations: compatibleAspirations, dependencies: dependencies, alternatives: alternatives, conflicts: conflicts, policyId: policyId);
            return definition;
        }

        private static ProfessionEntryPathDefinition EntryPath(
            string id,
            string professionId,
            string name,
            ProfessionEntryType type,
            ProfessionEntryFormality formality,
            ProfessionSelfDeclarationPolicy selfDeclaration,
            string specialization = "",
            string[] authorities = null,
            string[] skills = null,
            string[] knowledge = null,
            string[] capabilities = null,
            string[] traits = null,
            string[] statuses = null,
            string[] organizations = null,
            string[] accessKeys = null,
            string[] requiredActiveProfessions = null,
            string[] prohibitedActiveProfessions = null,
            string[] exclusiveProfessions = null,
            bool secret = false,
            bool disputed = false,
            bool restricted = false,
            bool illegal = false,
            bool requiresAuthority = false,
            bool immediateApproval = true,
            bool specializationNeedsParent = true,
            ProfessionReentryPolicy reentry = ProfessionReentryPolicy.NotApplicable,
            string accessPolicy = AccessPublicId)
        {
            ProfessionEntryPathDefinition definition = ScriptableObject.CreateInstance<ProfessionEntryPathDefinition>();
            definition.name = name.Replace(" ", string.Empty) + "EntryPath";
            definition.DevelopmentConfigure(
                id,
                professionId,
                name,
                type,
                formality,
                selfDeclaration,
                specialization,
                authorities,
                skills: skills,
                knowledge: knowledge,
                capabilities: capabilities,
                traits: traits,
                statuses: statuses,
                organizations: organizations,
                accessKeys: accessKeys,
                requiredActiveProfessions: requiredActiveProfessions,
                prohibitedActiveProfessions: prohibitedActiveProfessions,
                exclusiveProfessions: exclusiveProfessions,
                secret: secret,
                disputed: disputed,
                restricted: restricted,
                illegal: illegal,
                requiresAuthority: requiresAuthority,
                immediateApproval: immediateApproval,
                specializationNeedsParent: specializationNeedsParent,
                reentry: reentry,
                accessPolicy: accessPolicy);
            return definition;
        }

        private static InformationAccessPolicyDefinition AccessPolicy(string id, string name, InformationVisibilityClassification visibility, InformationDetailVisibilityPolicy detailVisibility, string[] visibleDetails = null, string[] hiddenDetails = null)
        {
            InformationAccessPolicyDefinition definition = ScriptableObject.CreateInstance<InformationAccessPolicyDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(
                id,
                name,
                InformationSubjectType.Custom,
                visibility,
                InformationDisclosurePolicy.SameAsAccess,
                visibility == InformationVisibilityClassification.Secret ? InformationResharingPolicy.NoResharing : InformationResharingPolicy.FreelyReshareable,
                visibility == InformationVisibilityClassification.Secret ? InformationSourceVisibilityPolicy.PrivilegedOnly : InformationSourceVisibilityPolicy.Reveal,
                detailVisibility,
                InformationAuditPolicy.AuditDenied,
                visibleDetails,
                null,
                hiddenDetails,
                requiresDiscovery: false,
                allowRedactedAccess: true);
            return definition;
        }

        private static InformationTransferDefinition TransferDefinition(string id, string name, InformationTransferMode mode)
        {
            InformationTransferDefinition definition = ScriptableObject.CreateInstance<InformationTransferDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(
                id,
                name,
                mode,
                new[] { KnowledgeDomain.Professional, KnowledgeDomain.Crafting },
                new[] { InformationSourceCategory.PersonalTestimony, InformationSourceCategory.ExpertTestimony, InformationSourceCategory.DirectObservation },
                false,
                true,
                false,
                true,
                850,
                850,
                TransferMemoryPolicy.FormCommunicationMemory,
                TransferEvidencePolicy.CreateRecipientEvidence);
            return definition;
        }

        private static TrainingProgramDefinition TrainingProgram(
            string id,
            string name,
            TrainingProgramCategory category,
            TrainingProgramFormality formality,
            string curriculum,
            string[] professions,
            string[] specializations = null,
            string[] entryPaths = null,
            TrainingInstructorRequirementData[] instructors = null,
            double durationHours = 0d,
            string organization = "",
            string[] stations = null,
            string[] completionRequirements = null,
            string accessPolicy = AccessPublicId)
        {
            TrainingProgramDefinition definition = ScriptableObject.CreateInstance<TrainingProgramDefinition>();
            definition.name = name.Replace(" ", string.Empty) + "TrainingProgram";
            definition.DevelopmentConfigure(
                id,
                name,
                category,
                formality,
                curriculum,
                professions,
                specializations,
                entryPaths,
                instructors,
                durationHours,
                minLearners: 1,
                maxLearners: category == TrainingProgramCategory.Apprenticeship ? 1 : 8,
                organization: organization,
                stations: stations,
                completionRequirements: completionRequirements,
                accessPolicy: accessPolicy);
            return definition;
        }

        private static ProfessionalActivityDefinition ActivityDefinition(
            string id,
            string name,
            ProfessionalActivityCategory category,
            string[] professionIds,
            ProfessionalActivitySourceType[] sourceTypes,
            string[] specializationIds = null,
            ProfessionalSupervisionPolicy supervision = ProfessionalSupervisionPolicy.Any,
            ProfessionalIndependentWorkPolicy independent = ProfessionalIndependentWorkPolicy.Any,
            ProfessionalCreditPolicy credit = ProfessionalCreditPolicy.Exclusive,
            ProfessionalFailureCreditPolicy failureCredit = ProfessionalFailureCreditPolicy.NoCredit,
            ProfessionalRepetitionPolicy repetition = ProfessionalRepetitionPolicy.PreserveAll,
            int minQuality = 0,
            ProfessionalActivityDifficulty minDifficulty = ProfessionalActivityDifficulty.Unknown,
            string[] tags = null,
            string accessPolicy = AccessPublicId)
        {
            ProfessionalActivityDefinition definition = ScriptableObject.CreateInstance<ProfessionalActivityDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, professionIds, sourceTypes, specializationIds, supervision, independent, credit, failureCredit, repetition, minQuality, minDifficulty, tags, accessPolicy);
            return definition;
        }

        private static CredentialDefinition Credential(
            string id,
            string name,
            CredentialCategory category,
            string[] professionIds,
            string[] issuerIds,
            CredentialIssuerAuthorityKind[] issuerKinds,
            string[] trainingProgramIds = null,
            ProfessionalExperienceRequirementData experience = null,
            string[] examinationDefinitionIds = null,
            string[] permissions = null,
            string[] specializationIds = null,
            bool formalRecognition = false,
            double durationHours = 0d,
            CredentialExpirationPolicy expiration = CredentialExpirationPolicy.NeverExpires,
            CredentialRenewalPolicy renewal = CredentialRenewalPolicy.NotRenewable,
            string policyId = AccessPublicId)
        {
            CredentialDefinition definition = ScriptableObject.CreateInstance<CredentialDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(
                id,
                name,
                category,
                professionIds,
                issuerIds,
                issuerKinds,
                trainingProgramIds,
                experience,
                examinationDefinitionIds,
                permissions,
                specializationIds,
                formalRecognition,
                durationHours,
                expiration,
                renewal,
                policyId: policyId);
            return definition;
        }

        private static CredentialExaminationDefinition Examination(
            string id,
            string name,
            string[] credentialDefinitionIds,
            CredentialAssessmentCategory category,
            int passingScore,
            int attemptLimit,
            string[] evaluatorAuthorityIds,
            string[] knowledgeSubjects = null,
            string[] skillOrCapabilityIds = null,
            string[] practicalActivityIds = null,
            string policyId = AccessPublicId)
        {
            CredentialExaminationDefinition definition = ScriptableObject.CreateInstance<CredentialExaminationDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(
                id,
                name,
                credentialDefinitionIds,
                category,
                passingScore,
                attemptLimit,
                evaluatorAuthorityIds,
                knowledgeSubjects,
                skillOrCapabilityIds,
                practicalActivityIds,
                policyId);
            return definition;
        }

        private static ProfessionalRankDefinition Rank(
            string id,
            string name,
            string professionId,
            int order,
            ProfessionalRankCategory category,
            string specialization = "",
            string[] priorRanks = null,
            string[] requiredCredentials = null,
            string[] requiredTraining = null,
            ProfessionalExperienceRequirementData experience = null,
            string[] requiredExaminations = null,
            string[] requiredAuthorities = null,
            string[] permissions = null,
            bool canTeach = false,
            bool canSupervise = false,
            int apprenticeCapacity = 0,
            ProfessionalRankTrackKind track = ProfessionalRankTrackKind.Formal,
            bool allowSelfClaim = false,
            string policyId = AccessPublicId)
        {
            ProfessionalRankDefinition definition = ScriptableObject.CreateInstance<ProfessionalRankDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, professionId, order, category, specialization, priorRanks, requiredCredentials, requiredTraining, experience, requiredExaminations, requiredAuthorities, allowSelfClaim, track, permissions, null, canTeach, canSupervise, apprenticeCapacity, policyId: policyId);
            return definition;
        }

        private static ProfessionalRankLadderDefinition Ladder(
            string id,
            string name,
            string professionId,
            string[] orderedRanks,
            string specialization = "",
            string[] terminalRanks = null,
            string[] lateralRanks = null,
            string[] demotionRanks = null,
            string policyId = AccessPublicId)
        {
            ProfessionalRankLadderDefinition definition = ScriptableObject.CreateInstance<ProfessionalRankLadderDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, professionId, orderedRanks, specialization, terminalRanks, lateralRanks, demotionRanks, policyId: policyId);
            return definition;
        }

        private static ProfessionalMasteryDefinition Mastery(
            string id,
            string name,
            string professionId,
            string requiredRank,
            string specialization,
            ProfessionalExperienceRequirementData experience,
            int breadth,
            int depthQuality,
            int independentWork,
            int teachingOrLeadership,
            string[] credentials,
            string[] examinations,
            string[] achievements,
            string[] authorities,
            string policyId = AccessPublicId)
        {
            ProfessionalMasteryDefinition definition = ScriptableObject.CreateInstance<ProfessionalMasteryDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, professionId, requiredRank, specialization, experience, breadth, depthQuality, independentWork, teachingOrLeadership, credentials, examinations, achievements, authorities, policyId: policyId);
            return definition;
        }

        private static PositionDefinition Position(
            string id,
            string name,
            PositionCategory category,
            string[] professions = null,
            string[] specializations = null,
            string organizationTypeId = "",
            string[] ranks = null,
            string[] credentials = null,
            string[] trainingPrograms = null,
            ProfessionalExperienceRequirementData experience = null,
            string[] duties = null,
            string[] authorities = null,
            EmploymentClassification classification = EmploymentClassification.Permanent,
            int maxHolders = 1,
            bool allowVacancy = true,
            bool allowShared = false,
            bool exclusive = true,
            bool isSecret = false,
            string policyId = AccessPublicId,
            int supervisorCapacity = 0,
            string compensationPolicy = "",
            string costCenter = "",
            string contractTerms = "")
        {
            PositionDefinition definition = ScriptableObject.CreateInstance<PositionDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(
                id,
                name,
                category,
                professions,
                specializations,
                organizationTypeId,
                ranks,
                credentials,
                trainingPrograms,
                experience,
                duties,
                authorities,
                classification,
                maxHolders,
                allowVacancy,
                allowShared,
                exclusive,
                isSecret,
                policyId,
                supervisorCapacity: supervisorCapacity,
                compensationPolicy: compensationPolicy,
                costCenter: costCenter,
                contractTerms: contractTerms);
            return definition;
        }

        private static DutyDefinition Duty(
            string id,
            string positionId,
            string name,
            DutyCategory category,
            string professionId = "",
            string authorityId = "",
            bool allowDelegation = false,
            bool requireSupervision = false,
            bool requireEvidence = true,
            bool isSecret = false,
            string policyId = AccessPublicId)
        {
            DutyDefinition definition = ScriptableObject.CreateInstance<DutyDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, positionId, name, category, true, 100, professionId, authorityId, allowDelegation, requireSupervision, requireEvidence, isSecret, policyId);
            return definition;
        }

        private static CareerTransitionDefinition CareerTransition(
            string id,
            string name,
            CareerTransitionCategory category,
            CareerEpisodeState[] sources = null,
            CareerEpisodeState[] destinations = null,
            CareerTransitionSourceRecordType[] required = null,
            string[] professions = null,
            string[] ranks = null,
            string[] credentials = null,
            bool voluntary = true,
            bool requiresAuthority = false,
            bool disputed = false,
            bool allowSecret = false,
            string policyId = AccessPublicId)
        {
            CareerTransitionDefinition definition = ScriptableObject.CreateInstance<CareerTransitionDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, sources, destinations, required, professions, ranks, credentials, voluntary, requiresAuthority, disputed, allowSecret, historyPolicy: "history-significance.profession.prototype", policyId: policyId);
            return definition;
        }

        private static TrainingInstructorRequirementData InstructorRequirement(string id, TrainingInstructorRoleKind role, string professionId, string specialization = "", string authority = "", int capacity = 1)
        {
            return new TrainingInstructorRequirementData
            {
                requirementId = id,
                role = role,
                requiredProfessionId = professionId,
                requiredSpecializationId = specialization,
                requiredAuthorityId = authority,
                accessPolicyId = AccessPublicId,
                maximumLearnerCapacity = capacity
            };
        }

        private static TrainingCurriculumDefinition BlacksmithApprenticeshipCurriculum()
        {
            TrainingCurriculumDefinition definition = ScriptableObject.CreateInstance<TrainingCurriculumDefinition>();
            definition.name = "BlacksmithApprenticeshipCurriculum";
            definition.DevelopmentConfigure(
                BlacksmithApprenticeshipCurriculumId,
                BlacksmithApprenticeshipProgramId,
                "Blacksmith Apprenticeship Curriculum",
                new[]
                {
                    Module(BlacksmithBasicsModuleId, "Forge Safety and Fundamentals", required: true, hidden: false, lessons: new[] { BlacksmithSafetyLessonId }),
                    Module(BlacksmithPracticeModuleId, "Supervised Practice", required: true, hidden: false, dependencies: new[] { BlacksmithBasicsModuleId }, lessons: new[] { BlacksmithDemonstrationLessonId }, assignments: new[] { BlacksmithPracticalAssignmentId }),
                    Module(BlacksmithHiddenAssessmentModuleId, "Master's Hidden Assessment", required: true, hidden: true, dependencies: new[] { BlacksmithPracticeModuleId })
                },
                new[]
                {
                    Lesson(BlacksmithSafetyLessonId, BlacksmithBasicsModuleId, "Forge Safety Lesson", TrainingTeachingMethod.Lecture, TrainingLessonTransferDefinitionId),
                    Lesson(BlacksmithDemonstrationLessonId, BlacksmithPracticeModuleId, "Hammering Demonstration", TrainingTeachingMethod.Demonstration, TrainingDemonstrationTransferDefinitionId)
                },
                new[]
                {
                    Assignment(BlacksmithPracticalAssignmentId, BlacksmithPracticeModuleId, TrainingAssignmentActivityCategory.Crafting, BlacksmithProfessionId, "recipe.prototype-iron-ingot", "production.activity.forging", requiredQuantity: 1, qualityThreshold: 500, supervisionRequired: true)
                },
                knowledgeSubjects: new[] { "knowledge.subject.metalwork", "knowledge.subject.forge-safety" },
                skillPractice: new[] { "skill.smithing.practice" });
            return definition;
        }

        private static TrainingCurriculumDefinition BlacksmithSafetyCurriculum()
        {
            TrainingCurriculumDefinition definition = ScriptableObject.CreateInstance<TrainingCurriculumDefinition>();
            definition.name = "BlacksmithSafetyCurriculum";
            definition.DevelopmentConfigure(
                BlacksmithSafetyCurriculumId,
                BlacksmithSafetyProgramId,
                "Blacksmith Safety Curriculum",
                new[] { Module(BlacksmithBasicsModuleId, "Forge Safety", required: true, hidden: false, lessons: new[] { BlacksmithSafetyLessonId }) },
                new[] { Lesson(BlacksmithSafetyLessonId, BlacksmithBasicsModuleId, "Forge Safety Lesson", TrainingTeachingMethod.Lecture, TrainingLessonTransferDefinitionId) },
                knowledgeSubjects: new[] { "knowledge.subject.forge-safety" });
            return definition;
        }

        private static TrainingModuleDefinitionData Module(string id, string name, bool required, bool hidden, string[] dependencies = null, string[] lessons = null, string[] assignments = null)
        {
            return new TrainingModuleDefinitionData
            {
                moduleId = id,
                displayName = name,
                required = required,
                hiddenFromLearner = hidden,
                dependencyModuleIds = dependencies ?? Array.Empty<string>(),
                lessonIds = lessons ?? Array.Empty<string>(),
                assignmentIds = assignments ?? Array.Empty<string>(),
                accessPolicyId = hidden ? AccessSecretId : AccessPublicId,
                estimatedDurationHours = 1d
            };
        }

        private static TrainingLessonDefinitionData Lesson(string id, string moduleId, string name, TrainingTeachingMethod method, string transferDefinitionId)
        {
            return new TrainingLessonDefinitionData
            {
                lessonId = id,
                moduleId = moduleId,
                displayName = name,
                teachingMethod = method,
                informationTransferDefinitionId = transferDefinitionId,
                knowledgeSubjectIds = new[] { "knowledge.subject.metalwork" },
                accessPolicyId = AccessPublicId,
                durationHours = 1d
            };
        }

        private static TrainingPracticalAssignmentDefinitionData Assignment(string id, string moduleId, TrainingAssignmentActivityCategory category, string professionId, string recipeId, string activityId, int requiredQuantity, int qualityThreshold, bool supervisionRequired)
        {
            return new TrainingPracticalAssignmentDefinitionData
            {
                assignmentId = id,
                moduleId = moduleId,
                activityCategory = category,
                requiredProfessionId = professionId,
                requiredRecipeId = recipeId,
                requiredProductionActivityId = activityId,
                requiredQuantity = requiredQuantity,
                qualityThreshold = qualityThreshold,
                supervisionRequired = supervisionRequired,
                exclusiveActivityReference = true,
                accessPolicyId = AccessPublicId
            };
        }
    }
}
