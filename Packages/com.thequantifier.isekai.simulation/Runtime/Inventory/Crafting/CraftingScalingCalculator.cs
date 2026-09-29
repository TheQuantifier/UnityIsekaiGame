using System;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Skills;

namespace UnityIsekaiGame.Inventory.Crafting
{
    public sealed class CraftingScalingCalculation
    {
        public string RecipeId { get; set; } = string.Empty;
        public float BaseDurationSeconds { get; set; }
        public float FinalDurationSeconds { get; set; }
        public string SkillId { get; set; } = string.Empty;
        public SkillGrade SkillGrade { get; set; } = SkillGrade.F;
        public bool SkillUsed { get; set; }
        public int SkillGradeTier { get; set; }
        public int HighestOutputRarityRank { get; set; }
        public float OutputStatComplexity { get; set; }
        public float DurationSkillMultiplier { get; set; } = 1f;
        public float DurationRarityMultiplier { get; set; } = 1f;
        public float DurationStatMultiplier { get; set; } = 1f;
        public float QualityAdjustment { get; set; }
        public float ExpectedQuality { get; set; } = 0.5f;
        public float AffixChanceBonus { get; set; }
        public float ExpectedEquipmentStatMultiplier => Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp01(ExpectedQuality));

        public string SkillLabel => SkillUsed ? $"{SkillId} {SkillGrade}" : "Unskilled";
    }

    public static class CraftingScalingCalculator
    {
        public static CraftingScalingCalculation Calculate(
            RecipeDefinition recipe,
            DefinitionRegistry registry,
            CharacterSkillCollection skills)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            RecipeCraftingScalingData scaling = recipe.CraftingScaling;
            (string skillId, SkillGrade grade, bool used) = ResolveBestSkill(scaling, skills);
            int gradeTier = used ? SkillGradeUtility.ToIndex(grade) + 1 : 0;
            int rarityRank = 0;
            float statComplexity = 0f;
            float baseQuality = 0.5f;

            foreach (RecipeOutputSpecificationData output in recipe.Outputs.Where(output => output != null && !string.IsNullOrWhiteSpace(output.itemDefinitionId)))
            {
                if (registry == null || !registry.TryGet(output.itemDefinitionId, out ItemDefinition item))
                {
                    continue;
                }

                rarityRank = Math.Max(rarityRank, item.Rarity?.Rank ?? 0);
                statComplexity += OutputStatComplexity(item) * Mathf.Max(1f, output.quantity);
                if (registry.TryGet(output.qualityPolicyId, out CraftingOutputPolicyDefinition qualityPolicy)
                    && qualityPolicy.Kind == CraftingOutputPolicyKind.QualityGeneration)
                {
                    baseQuality = Mathf.Max(baseQuality, qualityPolicy.BaseQualityNormalized);
                }
            }

            float skillMultiplier = used
                ? Mathf.Max(scaling.minimumSkillDurationMultiplier, 1f - scaling.durationReductionPerSkillGrade * gradeTier)
                : scaling.unskilledDurationMultiplier;
            float rarityMultiplier = 1f + rarityRank * scaling.rarityDurationMultiplierPerRank;
            float statMultiplier = 1f + statComplexity * scaling.statDurationMultiplierPerComplexityPoint;
            float duration = Mathf.Max(
                scaling.minimumDurationSeconds,
                scaling.baseDurationSeconds * skillMultiplier * rarityMultiplier * statMultiplier);
            float qualityAdjustment = used
                ? Mathf.Min(scaling.maximumSkillQualityBonus, scaling.qualityBonusPerSkillGrade * gradeTier)
                : scaling.unskilledQualityAdjustment;

            return new CraftingScalingCalculation
            {
                RecipeId = recipe.Id,
                BaseDurationSeconds = scaling.baseDurationSeconds,
                FinalDurationSeconds = duration,
                SkillId = skillId,
                SkillGrade = grade,
                SkillUsed = used,
                SkillGradeTier = gradeTier,
                HighestOutputRarityRank = rarityRank,
                OutputStatComplexity = statComplexity,
                DurationSkillMultiplier = skillMultiplier,
                DurationRarityMultiplier = rarityMultiplier,
                DurationStatMultiplier = statMultiplier,
                QualityAdjustment = qualityAdjustment,
                ExpectedQuality = Mathf.Clamp01(baseQuality + qualityAdjustment),
                AffixChanceBonus = used ? scaling.affixChanceBonusPerSkillGrade * gradeTier : 0f
            };
        }

        private static (string skillId, SkillGrade grade, bool used) ResolveBestSkill(RecipeCraftingScalingData scaling, CharacterSkillCollection skills)
        {
            string selectedId = string.Empty;
            SkillGrade selectedGrade = SkillGrade.F;
            int selectedIndex = -1;
            foreach (string skillId in scaling.eligibleSkillIds ?? Array.Empty<string>())
            {
                if (skills == null || !skills.TryGetSkill(skillId, out RuntimeSkillRecord record))
                {
                    continue;
                }

                SkillGrade grade = SkillGradeUtility.Clamp((SkillGrade)record.currentGrade);
                int index = SkillGradeUtility.ToIndex(grade);
                if (index > selectedIndex || index == selectedIndex && string.CompareOrdinal(skillId, selectedId) < 0)
                {
                    selectedId = skillId;
                    selectedGrade = grade;
                    selectedIndex = index;
                }
            }

            return (selectedId, selectedGrade, selectedIndex >= 0);
        }

        private static float OutputStatComplexity(ItemDefinition item)
        {
            if (item?.Equipment == null || !item.Equipment.Equippable)
            {
                return 0f;
            }

            EquipmentData equipment = item.Equipment;
            StatModifiers modifiers = equipment.StatModifiers;
            float score = (
                Mathf.Abs(modifiers.MaximumHealth)
                + Mathf.Abs(modifiers.MaximumStamina)
                + Mathf.Abs(modifiers.MaximumMana)
                + Mathf.Abs(modifiers.AttackPower)
                + Mathf.Abs(modifiers.Defense)) / 5f;
            score += equipment.ResistanceModifiers.Count * 0.5f;

            MeleeWeaponData melee = equipment.MeleeWeapon;
            if (melee != null && melee.IsWeapon)
            {
                score += melee.BaseDamage / 5f;
                score += melee.AttackRange / 2.5f;
            }

            RangedWeaponData ranged = equipment.RangedWeapon;
            if (ranged != null && ranged.IsWeapon)
            {
                score += ranged.BaseDamage / 5f;
                score += ranged.ProjectileSpeed * ranged.ProjectileLifetime / 25f;
            }

            return Mathf.Max(0f, score);
        }
    }
}
