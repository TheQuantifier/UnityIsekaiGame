using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
namespace UnityIsekaiGame.Professions
{
    public static class ProfessionContentIds
    {
        public const string CrafterProfessionId = "profession.crafter";
        public const string BlacksmithProfessionId = "profession.blacksmith";
        public const string DisassemblerProfessionId = "profession.disassembler";
        public const string SalvagerProfessionId = "profession.salvager";
        public const string FieldMedicProfessionId = "profession.field-medic";
        public const string ScoutProfessionId = "profession.scout";
        public const string SpyProfessionId = "profession.spy";
        public const string WeaponsmithSpecializationId = "profession.specialization.blacksmith.weaponsmith";
        public const string TraumaSpecializationId = "profession.specialization.field-medic.trauma-care";
        public const string CrafterSelfDeclaredEntryPathId = "profession.entry.crafter.self-declared";
        public const string BlacksmithSelfDeclaredEntryPathId = "profession.entry.blacksmith.self-declared";
        public const string DisassemblerSelfDeclaredEntryPathId = "profession.entry.disassembler.self-declared";
        public const string SalvagerSelfDeclaredEntryPathId = "profession.entry.salvager.self-declared";
        public const string FieldMedicRecognitionEntryPathId = "profession.entry.field-medic.recognition";
        public const string SpySecretEntryPathId = "profession.entry.spy.secret-self-declared";
        public const string WeaponsmithSpecializationEntryPathId = "profession.entry.blacksmith.weaponsmith";
        public const string BlacksmithReentryPathId = "profession.entry.blacksmith.reentry";
        public const string BlacksmithApprenticeshipProgramId = "training.program.blacksmith.apprenticeship";
        public const string BlacksmithApprenticeshipCurriculumId = "training.curriculum.blacksmith.apprenticeship";
        public const string BlacksmithSafetyProgramId = "training.program.blacksmith.safety";
        public const string BlacksmithSafetyCurriculumId = "training.curriculum.blacksmith.safety";
        public const string TrainingLessonTransferDefinitionId = "information-transfer.training.prototype-lecture";
        public const string TrainingDemonstrationTransferDefinitionId = "information-transfer.training.prototype-demonstration";
        public const string TrainingGuidedPracticeTransferDefinitionId = "information-transfer.training.prototype-guided-practice";
        public const string BlacksmithBasicsModuleId = "training.module.blacksmith.basics";
        public const string BlacksmithPracticeModuleId = "training.module.blacksmith.practical";
        public const string BlacksmithHiddenAssessmentModuleId = "training.module.blacksmith.hidden-assessment";
        public const string BlacksmithSafetyLessonId = "training.lesson.blacksmith.safety";
        public const string BlacksmithDemonstrationLessonId = "training.lesson.blacksmith.demonstration";
        public const string BlacksmithPracticalAssignmentId = "training.assignment.blacksmith.practice-forging";
        public const string CrafterActivityDefinitionId = "profession.activity.crafter.crafting";
        public const string BlacksmithCraftingActivityDefinitionId = "profession.activity.blacksmith.crafting";
        public const string BlacksmithProductionActivityDefinitionId = "profession.activity.blacksmith.production";
        public const string BlacksmithRepairActivityDefinitionId = "profession.activity.blacksmith.repair";
        public const string ItemRecoveryActivityDefinitionId = "profession.activity.item-recovery";
        public const string SalvagePickupActivityDefinitionId = "profession.activity.salvage-pickup";
        public const string BlacksmithSupervisedPracticeActivityDefinitionId = "profession.activity.blacksmith.supervised-practice";
        public const string BlacksmithTeachingActivityDefinitionId = "profession.activity.blacksmith.teaching";
        public const string BlacksmithExperimentationActivityDefinitionId = "profession.activity.blacksmith.experimentation";
        public const string BlacksmithApprenticeshipCertificateCredentialId = "credential.blacksmith.apprenticeship-certificate";
        public const string BlacksmithGuildLicenseCredentialId = "credential.blacksmith.guild-license";
        public const string BlacksmithSafetyCertificateCredentialId = "credential.blacksmith.safety-certificate";
        public const string BlacksmithPracticalExaminationId = "examination.blacksmith.practical";
        public const string BlacksmithWrittenExaminationId = "examination.blacksmith.written";
        public const string BlacksmithPracticePermissionId = "permission.profession.blacksmith.practice";
        public const string BlacksmithTeachPermissionId = "permission.profession.blacksmith.teach";
        public const string ForgeRestrictedStationPermissionId = "permission.station.forge.restricted";
        public const string BlacksmithRankApprenticeId = "profession.rank.blacksmith.apprentice";
        public const string BlacksmithRankJourneymanId = "profession.rank.blacksmith.journeyman";
        public const string BlacksmithRankMasterId = "profession.rank.blacksmith.master";
        public const string WeaponsmithRankApprenticeId = "profession.rank.blacksmith.weaponsmith.apprentice";
        public const string WeaponsmithRankMasterId = "profession.rank.blacksmith.weaponsmith.master";
        public const string BlacksmithRankLadderId = "profession.rank-ladder.blacksmith.guild";
        public const string WeaponsmithRankLadderId = "profession.rank-ladder.blacksmith.weaponsmith";
        public const string CrafterRankNoviceId = "profession.rank.crafter.novice";
        public const string CrafterRankSkilledId = "profession.rank.crafter.skilled";
        public const string CrafterRankMasterId = "profession.rank.crafter.master";
        public const string CrafterRankLadderId = "profession.rank-ladder.crafter.informal";
        public const string DisassemblerRankNoviceId = "profession.rank.disassembler.novice";
        public const string DisassemblerRankSkilledId = "profession.rank.disassembler.skilled";
        public const string DisassemblerRankMasterId = "profession.rank.disassembler.master";
        public const string DisassemblerRankLadderId = "profession.rank-ladder.disassembler.informal";
        public const string SalvagerRankNoviceId = "profession.rank.salvager.novice";
        public const string SalvagerRankSkilledId = "profession.rank.salvager.skilled";
        public const string SalvagerRankMasterId = "profession.rank.salvager.master";
        public const string SalvagerRankLadderId = "profession.rank-ladder.salvager.informal";
        public const string WeaponsmithMasteryId = "profession.mastery.blacksmith.weaponsmith";
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
        public const string CareerProfessionEnteredTransitionId = "career.transition.profession-entered";
        public const string CareerEmploymentStartedTransitionId = "career.transition.employment-started";
        public const string CareerEmploymentEndedTransitionId = "career.transition.employment-ended";
        public const string CareerPromotionTransitionId = "career.transition.promotion";
        public const string CareerDemotionTransitionId = "career.transition.demotion";
        public const string CareerTransferTransitionId = "career.transition.transfer";
        public const string CareerResignationTransitionId = "career.transition.resignation";
        public const string CareerDismissalTransitionId = "career.transition.dismissal";
        public const string CareerRetirementTransitionId = "career.transition.retirement";
        public const string CareerReturnFromRetirementTransitionId = "career.transition.return-from-retirement";
        public const string CareerGapStartedTransitionId = "career.transition.career-gap-started";
        public const string CareerGapEndedTransitionId = "career.transition.career-gap-ended";
        public const string CareerChangeTransitionId = "career.transition.career-change";
        public const string CareerAchievementTransitionId = "career.transition.achievement";
        public const string CareerSetbackTransitionId = "career.transition.setback";
        public const string AspirationEnterBlacksmithProfessionId = "aspiration.blacksmith.enter";
        public const string AspirationReachMasterWeaponsmithId = "aspiration.blacksmith.weaponsmith-mastery";
        public const string AspirationEarnGuildLicenseId = "aspiration.blacksmith.guild-license";
        public const string AspirationObtainGuildPositionId = "aspiration.guild.position";
        public const string AspirationCreateMasterworkId = "aspiration.crafting.masterwork";
        public const string AspirationMakeDiscoveryId = "aspiration.profession.discovery";
        public const string AspirationRetireSuccessfullyId = "aspiration.profession.retire-successfully";
        public const string AspirationSecretSpyIdentityId = "aspiration.spy.secret-identity";
        public const string GoalEnterBlacksmithProfessionId = "goal.blacksmith.enter";
        public const string GoalCompleteBlacksmithApprenticeshipId = "goal.blacksmith.complete-apprenticeship";
        public const string GoalEarnBlacksmithGuildLicenseId = "goal.blacksmith.earn-guild-license";
        public const string GoalReachJourneymanRankId = "goal.blacksmith.reach-journeyman";
        public const string GoalReachMasterRankId = "goal.blacksmith.reach-master";
        public const string GoalObtainGuildClerkPositionId = "goal.guild.obtain-clerk-position";
        public const string GoalRecordCraftingActivityId = "goal.crafting.record-activity";
        public const string GoalProduceMasterworkId = "goal.crafting.produce-masterwork";
        public const string GoalConfirmDiscoveryId = "goal.profession.confirm-discovery";
        public const string GoalRetireFromProfessionId = "goal.profession.retire";

        public static IReadOnlyList<IGameDefinition> GetDefinitions(DefinitionRegistry registry)
        {
            if (registry == null)
            {
                return new IGameDefinition[0];
            }

            return registry.DefinitionsById.Values
                .Where(IsProfessionDefinition)
                .OrderBy(definition => definition.Id, System.StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsProfessionDefinition(IGameDefinition definition)
        {
            return definition is ProfessionDefinition
                || definition is ProfessionSpecializationDefinition
                || definition is ProfessionEntryPathDefinition
                || definition is TrainingProgramDefinition
                || definition is TrainingCurriculumDefinition
                || definition is ProfessionalActivityDefinition
                || definition is CredentialDefinition
                || definition is CredentialExaminationDefinition
                || definition is ProfessionalRankDefinition
                || definition is ProfessionalRankLadderDefinition
                || definition is ProfessionalMasteryDefinition
                || definition is PositionDefinition
                || definition is DutyDefinition
                || definition is CareerTransitionDefinition
                || definition is AspirationDefinition
                || definition is LifeGoalDefinition;
        }
    }
}
