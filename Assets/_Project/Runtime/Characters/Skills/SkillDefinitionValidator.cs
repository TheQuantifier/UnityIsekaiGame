using System;
using System.Collections.Generic;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Skills
{
    public static class SkillDefinitionValidator
    {
        public static void Validate(SkillDefinition skill, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (skill == null || report == null)
            {
                return;
            }

            if (!skill.Id.StartsWith("skill.", StringComparison.Ordinal))
            {
                report.AddWarning($"Skill '{skill.DisplayName}' should use the 'skill.' namespace prefix.");
            }

            ValidateNaturalLearning(skill, report);
            ValidateThresholds(skill, report);
            ValidateGradePackages(skill, definitionsById, report);
            ValidateAbilityUnlocks(skill, skill.AbilityUnlocks, definitionsById, "skill-level", report);
        }

        private static void ValidateNaturalLearning(SkillDefinition skill, DefinitionValidationReport report)
        {
            SkillNaturalLearningDefinition learning = skill.NaturalLearning;
            if (learning == null || !learning.Enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(learning.QualifyingEventId))
            {
                report.AddError($"Skill '{skill.DisplayName}' has natural learning enabled without a qualifying event ID.");
            }

            if (!Enum.IsDefined(typeof(SkillActionEventCategory), learning.ActionCategory) || learning.ActionCategory == SkillActionEventCategory.Unknown)
            {
                report.AddError($"Skill '{skill.DisplayName}' has an invalid natural-learning action category.");
            }

            if (learning.RequiredCount <= 0)
            {
                report.AddError($"Skill '{skill.DisplayName}' has a natural-learning required count below 1.");
            }

            if (!SkillGradeUtility.IsValid(learning.GrantedStartingGrade))
            {
                report.AddError($"Skill '{skill.DisplayName}' has an invalid natural-learning starting grade.");
            }
        }

        private static void ValidateThresholds(SkillDefinition skill, DefinitionValidationReport report)
        {
            HashSet<SkillGrade> seen = new HashSet<SkillGrade>();
            for (int i = 0; i < skill.XpThresholds.Count; i++)
            {
                SkillXpThresholdDefinition threshold = skill.XpThresholds[i];
                if (threshold == null)
                {
                    report.AddError($"Skill '{skill.DisplayName}' has a missing XP threshold entry.");
                    continue;
                }

                if (threshold.FromGrade == SkillGrade.AAA)
                {
                    report.AddError($"Skill '{skill.DisplayName}' defines an XP threshold from AAA, which is mastery.");
                }

                if (!seen.Add(threshold.FromGrade))
                {
                    report.AddError($"Skill '{skill.DisplayName}' has duplicate XP threshold for grade {threshold.FromGrade}.");
                }

                if (threshold.XpRequired <= 0)
                {
                    report.AddError($"Skill '{skill.DisplayName}' has a non-positive XP threshold from {threshold.FromGrade}.");
                }
            }

            for (int i = SkillGradeUtility.MinimumIndex; i < SkillGradeUtility.MaximumIndex; i++)
            {
                SkillGrade grade = (SkillGrade)i;
                if (!seen.Contains(grade))
                {
                    report.AddError($"Skill '{skill.DisplayName}' is missing XP threshold from {grade}.");
                }
            }
        }

        private static void ValidateGradePackages(SkillDefinition skill, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            HashSet<SkillGrade> packageGrades = new HashSet<SkillGrade>();
            foreach (SkillGradeEffectPackageDefinition package in skill.GradePackages)
            {
                if (package == null)
                {
                    report.AddError($"Skill '{skill.DisplayName}' has a missing grade package.");
                    continue;
                }

                if (!packageGrades.Add(package.Grade))
                {
                    report.AddError($"Skill '{skill.DisplayName}' has duplicate grade package {package.Grade}.");
                }

                foreach (SkillCalculatedStatContributionDefinition contribution in package.CalculatedStatContributions)
                {
                    ValidateContribution(skill, package.Grade, contribution, definitionsById, report);
                }

                ValidateAbilityUnlocks(skill, package.AbilityUnlocks, definitionsById, $"grade {package.Grade}", report);

                foreach (string capabilityId in package.CapabilityUnlockIds)
                {
                    if (string.IsNullOrWhiteSpace(capabilityId))
                    {
                        report.AddError($"Skill '{skill.DisplayName}' grade {package.Grade} has an empty capability unlock ID.");
                    }
                }
            }
        }

        private static void ValidateContribution(
            SkillDefinition skill,
            SkillGrade grade,
            SkillCalculatedStatContributionDefinition contribution,
            IReadOnlyDictionary<string, IGameDefinition> definitionsById,
            DefinitionValidationReport report)
        {
            if (contribution == null)
            {
                report.AddError($"Skill '{skill.DisplayName}' grade {grade} has a missing calculated-stat contribution.");
                return;
            }

            if (contribution.CalculatedStat == null)
            {
                report.AddError($"Skill '{skill.DisplayName}' grade {grade} has a calculated-stat contribution with no target.");
            }
            else if (definitionsById == null
                || !definitionsById.TryGetValue(contribution.CalculatedStat.Id, out IGameDefinition found)
                || found is not CalculatedStatDefinition)
            {
                report.AddError($"Skill '{skill.DisplayName}' grade {grade} references calculated stat '{contribution.CalculatedStat.Id}', which is not in the configured catalog.");
            }

            if (contribution.Magnitude <= 0f || float.IsNaN(contribution.Magnitude) || float.IsInfinity(contribution.Magnitude))
            {
                report.AddError($"Skill '{skill.DisplayName}' grade {grade} has an invalid calculated-stat contribution magnitude.");
            }

            if (!Enum.IsDefined(typeof(CalculatedStatContributionKind), contribution.Kind)
                || !Enum.IsDefined(typeof(CalculatedStatContributionDirection), contribution.Direction))
            {
                report.AddError($"Skill '{skill.DisplayName}' grade {grade} has an invalid calculated-stat contribution enum.");
            }
        }

        private static void ValidateAbilityUnlocks(
            SkillDefinition skill,
            IReadOnlyList<SkillAbilityUnlockDefinition> unlocks,
            IReadOnlyDictionary<string, IGameDefinition> definitionsById,
            string label,
            DefinitionValidationReport report)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (SkillAbilityUnlockDefinition unlock in unlocks ?? Array.Empty<SkillAbilityUnlockDefinition>())
            {
                if (unlock == null)
                {
                    report.AddError($"Skill '{skill.DisplayName}' has a missing {label} ability unlock.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(unlock.AbilityId))
                {
                    report.AddError($"Skill '{skill.DisplayName}' has a {label} ability unlock with no ability/action ID.");
                    continue;
                }

                string key = $"{unlock.AbilityId}|{unlock.RequiredGrade}";
                if (!seen.Add(key))
                {
                    report.AddError($"Skill '{skill.DisplayName}' has duplicate {label} ability unlock '{key}'.");
                }

                if (unlock.Ability != null
                    && (definitionsById == null || !definitionsById.TryGetValue(unlock.Ability.Id, out IGameDefinition found) || found is not AbilityDefinition))
                {
                    report.AddError($"Skill '{skill.DisplayName}' references ability '{unlock.Ability.Id}', which is not in the configured catalog.");
                }

                if (!SkillGradeUtility.IsValid(unlock.RequiredGrade))
                {
                    report.AddError($"Skill '{skill.DisplayName}' has an ability unlock with invalid required grade.");
                }
            }
        }
    }
}
