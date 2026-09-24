using UnityEngine;

namespace UnityIsekaiGame.Combat
{
    public readonly struct DamageResult
    {
        private readonly System.Collections.Generic.IReadOnlyList<DamageComponentResult> componentResults;

        public DamageResult(bool applied, float requestedAmount, float appliedAmount, bool defeated, string message)
            : this(applied, requestedAmount, requestedAmount, 0f, Mathf.Max(0f, requestedAmount - appliedAmount), appliedAmount, 0f, defeated, message)
        {
        }

        public DamageResult(
            bool applied,
            float requestedAmount,
            float preMitigationAmount,
            float defense,
            float mitigatedAmount,
            float appliedAmount,
            float remainingHealth,
            bool defeated,
            string message)
            : this(applied, requestedAmount, preMitigationAmount, defense, mitigatedAmount, 0f, 0f, appliedAmount, remainingHealth, defeated, message, System.Array.Empty<DamageComponentResult>())
        {
        }

        public DamageResult(
            bool applied,
            float requestedAmount,
            float preMitigationAmount,
            float defense,
            float mitigatedAmount,
            float resistanceMitigation,
            float weaknessAmplification,
            float appliedAmount,
            float remainingHealth,
            bool defeated,
            string message,
            System.Collections.Generic.IReadOnlyList<DamageComponentResult> componentResults)
        {
            Applied = applied;
            RequestedAmount = requestedAmount;
            PreMitigationAmount = preMitigationAmount;
            Defense = defense;
            MitigatedAmount = mitigatedAmount;
            ResistanceMitigation = resistanceMitigation;
            WeaknessAmplification = weaknessAmplification;
            AppliedAmount = appliedAmount;
            RemainingHealth = remainingHealth;
            Defeated = defeated;
            Message = message;
            this.componentResults = componentResults ?? System.Array.Empty<DamageComponentResult>();
        }

        public bool Applied { get; }
        public float RequestedAmount { get; }
        public float PreMitigationAmount { get; }
        public float Defense { get; }
        public float MitigatedAmount { get; }
        public float ResistanceMitigation { get; }
        public float WeaknessAmplification { get; }
        public float AppliedAmount { get; }
        public float RemainingHealth { get; }
        public bool Defeated { get; }
        public string Message { get; }
        public System.Collections.Generic.IReadOnlyList<DamageComponentResult> ComponentResults => componentResults ?? System.Array.Empty<DamageComponentResult>();

        public static DamageResult Success(float requestedAmount, float appliedAmount, bool defeated, string message)
        {
            return new DamageResult(true, requestedAmount, appliedAmount, defeated, message);
        }

        public static DamageResult Success(
            float requestedAmount,
            DamageCalculation calculation,
            float appliedAmount,
            float remainingHealth,
            bool defeated,
            string message)
        {
            return new DamageResult(
                true,
                requestedAmount,
                calculation.PreMitigationAmount,
                calculation.Defense,
                calculation.MitigatedAmount,
                calculation.ResistanceMitigation,
                calculation.WeaknessAmplification,
                appliedAmount,
                remainingHealth,
                defeated,
                message,
                calculation.ComponentResults);
        }

        public static DamageResult Failure(float requestedAmount, string message)
        {
            return new DamageResult(false, requestedAmount, 0f, false, message);
        }

        public static DamageResult FromApplication(DamageApplicationResult result, string message = "")
        {
            if (result == null || !result.Succeeded)
            {
                return Failure(result?.RequestedAmount ?? 0f, string.IsNullOrWhiteSpace(message) ? result?.Message ?? "Damage failed." : message);
            }

            float afterDefense = Mathf.Max(0f, result.RequestedAmount - result.DefenseMitigatedAmount);
            return new DamageResult(
                result.HealthChanged,
                result.RequestedAmount,
                result.RequestedAmount,
                result.DefenseApplied,
                result.DefenseMitigatedAmount,
                Mathf.Max(0f, result.ResistanceMitigatedAmount),
                Mathf.Max(0f, result.FinalDamageAmount - afterDefense),
                result.FinalDamageAmount,
                result.NewHealth,
                result.BecameZero,
                string.IsNullOrWhiteSpace(message) ? result.Message : message,
                System.Array.Empty<DamageComponentResult>());
        }
    }
}
