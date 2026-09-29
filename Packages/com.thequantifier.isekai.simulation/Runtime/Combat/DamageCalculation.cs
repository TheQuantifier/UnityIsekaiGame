namespace UnityIsekaiGame.Combat
{
    public readonly struct DamageCalculation
    {
        private readonly System.Collections.Generic.IReadOnlyList<DamageComponentResult> componentResults;

        public DamageCalculation(float preMitigationAmount, float defense, float mitigatedAmount, float finalAmount)
            : this(preMitigationAmount, defense, mitigatedAmount, 0f, 0f, finalAmount, System.Array.Empty<DamageComponentResult>())
        {
        }

        public DamageCalculation(
            float preMitigationAmount,
            float defense,
            float mitigatedAmount,
            float resistanceMitigation,
            float weaknessAmplification,
            float finalAmount,
            System.Collections.Generic.IReadOnlyList<DamageComponentResult> componentResults)
        {
            PreMitigationAmount = preMitigationAmount;
            Defense = defense;
            MitigatedAmount = mitigatedAmount;
            ResistanceMitigation = resistanceMitigation;
            WeaknessAmplification = weaknessAmplification;
            FinalAmount = finalAmount;
            this.componentResults = componentResults ?? System.Array.Empty<DamageComponentResult>();
        }

        public float PreMitigationAmount { get; }
        public float Defense { get; }
        public float MitigatedAmount { get; }
        public float ResistanceMitigation { get; }
        public float WeaknessAmplification { get; }
        public float FinalAmount { get; }
        public System.Collections.Generic.IReadOnlyList<DamageComponentResult> ComponentResults => componentResults ?? System.Array.Empty<DamageComponentResult>();
    }
}
