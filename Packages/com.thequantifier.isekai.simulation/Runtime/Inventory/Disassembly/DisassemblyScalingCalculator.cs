using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace UnityIsekaiGame.Inventory.Disassembly
{
    public static class DisassemblyScalingCalculator
    {
        public static float ExpectedEfficiency(int skillGrade, bool skillUsed, int itemLevel, int itemRarityRank, float itemQuality, float itemCondition)
        {
            int skillTier = skillUsed ? Mathf.Clamp(skillGrade + 1, 1, 8) : 0;
            float levelBonus = Mathf.Clamp(itemLevel, 1, 100) * 0.0025f;
            return Mathf.Clamp(0.28f + skillTier * 0.065f + levelBonus + itemRarityRank * 0.035f + Mathf.Clamp01(itemQuality) * 0.16f + Mathf.Clamp01(itemCondition) * 0.11f, 0.12f, 0.97f);
        }

        public static float ComponentRecoveryChance(float expectedEfficiency, int componentRarityRank, float componentCondition)
        {
            float conditionMultiplier = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(componentCondition));
            return Mathf.Clamp((expectedEfficiency + 0.12f - Mathf.Max(0, componentRarityRank) * 0.075f) * conditionMultiplier, 0.05f, 0.98f);
        }

        public static float QuantityEfficiency(float expectedEfficiency, int componentRarityRank, float componentCondition, string seed)
        {
            float noise = (DeterministicUnitInterval(seed + ":efficiency") - 0.5f) * 0.18f;
            float conditionCap = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(componentCondition));
            return Mathf.Clamp(Mathf.Min(conditionCap, expectedEfficiency - Mathf.Max(0, componentRarityRank) * 0.035f + noise), 0.08f, conditionCap);
        }

        public static int RecoveredQuantity(float sourceQuantity, float chance, float efficiency, string seed)
        {
            if (sourceQuantity <= 0f || DeterministicUnitInterval(seed + ":chance") > chance) return 0;
            float recovered = sourceQuantity * efficiency;
            int whole = Mathf.FloorToInt(recovered);
            if (DeterministicUnitInterval(seed + ":quantity") < recovered - whole) whole++;
            return Mathf.Clamp(Mathf.Max(1, whole), 0, Mathf.CeilToInt(sourceQuantity));
        }

        public static DisassemblyEfficiencyTier Tier(float efficiency)
        {
            if (efficiency >= 0.9f) return DisassemblyEfficiencyTier.Masterful;
            if (efficiency >= 0.75f) return DisassemblyEfficiencyTier.Excellent;
            if (efficiency >= 0.6f) return DisassemblyEfficiencyTier.Skilled;
            if (efficiency >= 0.42f) return DisassemblyEfficiencyTier.Standard;
            if (efficiency >= 0.24f) return DisassemblyEfficiencyTier.Poor;
            return DisassemblyEfficiencyTier.Ruined;
        }

        public static float DeterministicUnitInterval(string seed)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(seed ?? string.Empty));
            return BitConverter.ToUInt32(bytes, 0) / (float)uint.MaxValue;
        }
    }
}
