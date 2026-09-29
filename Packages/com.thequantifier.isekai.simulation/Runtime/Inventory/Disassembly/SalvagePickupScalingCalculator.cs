using UnityEngine;

namespace UnityIsekaiGame.Inventory.Disassembly
{
    public sealed class SalvagePickupCalculation
    {
        public int BaseQuantity { get; set; }
        public int TotalQuantity { get; set; }
        public int BonusQuantity => Mathf.Max(0, TotalQuantity - BaseQuantity);
        public string SkillId { get; set; } = "skill.salvaging";
        public int SkillGrade { get; set; }
        public bool SkillUsed { get; set; }
        public float BonusChance { get; set; }
        public float BonusRoll { get; set; }
        public int QuantityMultiplier { get; set; } = 1;
        public bool BonusApplied => TotalQuantity > BaseQuantity;
    }

    public static class SalvagePickupScalingCalculator
    {
        public const float DefaultMasterDoubleChance = 0.5f;
        public const int DefaultBonusMultiplier = 2;

        public static SalvagePickupCalculation Calculate(int baseQuantity, int skillGrade, bool skillUsed, string seed, float masterBonusChance = DefaultMasterDoubleChance, int bonusMultiplier = DefaultBonusMultiplier)
        {
            int quantity = Mathf.Max(1, baseQuantity);
            int grade = Mathf.Clamp(skillGrade, 0, 7);
            float chance = skillUsed ? Mathf.Lerp(0.05f, Mathf.Clamp01(masterBonusChance), grade / 7f) : 0f;
            float roll = DisassemblyScalingCalculator.DeterministicUnitInterval((seed ?? string.Empty) + ":salvage-pickup");
            int multiplier = roll <= chance ? Mathf.Max(2, bonusMultiplier) : 1;
            return new SalvagePickupCalculation
            {
                BaseQuantity = quantity,
                TotalQuantity = quantity * multiplier,
                SkillGrade = grade,
                SkillUsed = skillUsed,
                BonusChance = chance,
                BonusRoll = roll,
                QuantityMultiplier = multiplier
            };
        }
    }
}
