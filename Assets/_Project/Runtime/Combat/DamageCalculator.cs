using System;
using UnityEngine;

namespace UnityIsekaiGame.Combat
{
    public static class DamageCalculator
    {
        public const float DefaultMinimumDamage = 1f;

        public static float CalculateAppliedDamage(float rawDamage, float defense, float minimumDamage = 1f)
        {
            if (rawDamage <= 0f)
            {
                return 0f;
            }

            // Prototype formula: subtract flat defense, but any valid hit still deals at least 1 damage.
            return Mathf.Max(minimumDamage, rawDamage - Mathf.Max(0f, defense));
        }

        public static DamageCalculation Calculate(float rawDamage, float defense, float minimumDamage = DefaultMinimumDamage)
        {
            float preMitigationAmount = Mathf.Max(0f, rawDamage);
            float clampedDefense = Mathf.Max(0f, defense);
            float finalAmount = CalculateAppliedDamage(preMitigationAmount, clampedDefense, minimumDamage);
            float mitigatedAmount = Mathf.Max(0f, preMitigationAmount - finalAmount);
            return new DamageCalculation(preMitigationAmount, clampedDefense, mitigatedAmount, finalAmount);
        }

        public static DamageCalculation CalculatePacket(in DamagePacket packet, float defense, IDamageResistanceReceiver resistanceReceiver)
        {
            return CalculatePacket(
                in packet,
                _ => Mathf.Max(0f, defense),
                damageType => resistanceReceiver == null ? 0f : resistanceReceiver.GetEffectiveResistance(damageType));
        }

        public static DamageCalculation CalculatePacket(
            in DamagePacket packet,
            Func<DamageTypeDefinition, float> defenseResolver,
            Func<DamageTypeDefinition, float> resistanceResolver)
        {
            if (!packet.HasComponents)
            {
                return new DamageCalculation(0f, 0f, 0f, 0f);
            }

            float totalOriginal = 0f;
            float totalDefenseApplied = 0f;
            float totalDefenseMitigation = 0f;
            float totalResistanceMitigation = 0f;
            float totalWeaknessAmplification = 0f;
            float totalFinal = 0f;
            System.Collections.Generic.List<DamageComponentResult> results = new System.Collections.Generic.List<DamageComponentResult>();

            for (int i = 0; i < packet.Components.Count; i++)
            {
                DamageComponent component = packet.Components[i];
                if (!component.IsValid)
                {
                    continue;
                }

                float componentDefense = component.DamageType != null && component.DamageType.IsTrueDamage
                    ? 0f
                    : Mathf.Max(0f, defenseResolver?.Invoke(component.DamageType) ?? 0f);
                float componentResistance = component.DamageType != null && component.DamageType.IsTrueDamage
                    ? 0f
                    : Mathf.Clamp(resistanceResolver?.Invoke(component.DamageType) ?? 0f, RuntimeResistanceCollection.MinimumResistance, RuntimeResistanceCollection.MaximumResistance);
                DamageComponentResult result = CalculateComponent(component, componentDefense, componentResistance);
                results.Add(result);
                totalOriginal += result.OriginalAmount;
                totalDefenseApplied += componentDefense;
                totalDefenseMitigation += result.DefenseMitigation;
                totalResistanceMitigation += result.ResistanceMitigation;
                totalWeaknessAmplification += result.WeaknessAmplification;
                totalFinal += result.FinalAmount;
            }

            return new DamageCalculation(
                totalOriginal,
                totalDefenseApplied,
                Mathf.Max(0f, totalDefenseMitigation + totalResistanceMitigation),
                totalResistanceMitigation,
                totalWeaknessAmplification,
                Mathf.Max(0f, totalFinal),
                results);
        }

        private static DamageComponentResult CalculateComponent(
            DamageComponent component,
            float defense,
            float resistance)
        {
            float originalAmount = Mathf.Max(0f, component.Amount);
            bool defenseApplies = component.DamageType != null && component.DamageType.GeneralDefenseApplies;
            float defenseMitigation = defenseApplies ? Mathf.Min(originalAmount, defense) : 0f;
            float afterDefense = Mathf.Max(0f, originalAmount - defenseMitigation);
            float afterResistance = afterDefense * (1f - resistance);
            float resistanceDelta = afterDefense - afterResistance;
            bool immune = component.DamageType != null && resistance >= RuntimeResistanceCollection.MaximumResistance;
            float minimumDamage = ResolveMinimumDamage(component);
            float finalAmount = immune ? 0f : Mathf.Max(minimumDamage, afterResistance);

            if (originalAmount <= 0f)
            {
                finalAmount = 0f;
            }

            return new DamageComponentResult(
                component.DamageType,
                originalAmount,
                defenseMitigation,
                resistance,
                resistanceDelta,
                Mathf.Max(0f, finalAmount),
                immune);
        }

        private static float ResolveMinimumDamage(DamageComponent component)
        {
            return component.DamageType.EnforceMinimumDamage ? component.DamageType.MinimumDamage : 0f;
        }
    }
}
