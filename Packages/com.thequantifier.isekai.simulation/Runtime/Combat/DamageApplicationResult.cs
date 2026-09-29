using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat
{
    public sealed class DamageApplicationResult
    {
        private DamageApplicationResult(
            bool succeeded,
            bool preview,
            string code,
            string message,
            DamageApplicationRequest request,
            string resolvedTargetActorId,
            float requestedAmount,
            float defenseApplied,
            float defenseMitigatedAmount,
            float resistanceFraction,
            float resistanceMitigatedAmount,
            float finalDamageAmount,
            float oldHealth,
            float newHealth,
            float healthMinimum,
            float healthMaximum,
            bool immune,
            bool trueDamage,
            bool duplicate,
            bool healthChanged,
            bool becameZero,
            float overkillAmount,
            ResourceChangeResult resourceResult)
        {
            Succeeded = succeeded;
            Preview = preview;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? ImmediateCombatResultCode.Applied : ImmediateCombatResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            Request = request;
            ResolvedTargetActorId = resolvedTargetActorId ?? string.Empty;
            RequestedAmount = requestedAmount;
            DefenseApplied = defenseApplied;
            DefenseMitigatedAmount = defenseMitigatedAmount;
            ResistanceFraction = resistanceFraction;
            ResistanceMitigatedAmount = resistanceMitigatedAmount;
            FinalDamageAmount = finalDamageAmount;
            OldHealth = oldHealth;
            NewHealth = newHealth;
            HealthMinimum = healthMinimum;
            HealthMaximum = healthMaximum;
            Immune = immune;
            TrueDamage = trueDamage;
            Duplicate = duplicate;
            HealthChanged = healthChanged;
            BecameZero = becameZero;
            OverkillAmount = overkillAmount;
            ResourceResult = resourceResult;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public string Code { get; }
        public string Message { get; }
        public DamageApplicationRequest Request { get; }
        public string ResolvedTargetActorId { get; }
        public float RequestedAmount { get; }
        public float DefenseApplied { get; }
        public float DefenseMitigatedAmount { get; }
        public float ResistanceFraction { get; }
        public float ResistanceMitigatedAmount { get; }
        public float FinalDamageAmount { get; }
        public float OldHealth { get; }
        public float NewHealth { get; }
        public float HealthMinimum { get; }
        public float HealthMaximum { get; }
        public bool Immune { get; }
        public bool TrueDamage { get; }
        public bool Duplicate { get; }
        public bool HealthChanged { get; }
        public bool BecameZero { get; }
        public float OverkillAmount { get; }
        public ResourceChangeResult ResourceResult { get; }

        public static DamageApplicationResult Failure(DamageApplicationRequest request, string code, string message, string resolvedTargetActorId = "")
        {
            return new DamageApplicationResult(false, false, code, message, request, resolvedTargetActorId, request.RequestedAmount, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false, false, false, false, false, 0f, null);
        }

        public static DamageApplicationResult Create(
            bool preview,
            string code,
            string message,
            DamageApplicationRequest request,
            string resolvedTargetActorId,
            float requestedAmount,
            float defenseApplied,
            float defenseMitigatedAmount,
            float resistanceFraction,
            float resistanceMitigatedAmount,
            float finalDamageAmount,
            float oldHealth,
            float newHealth,
            float healthMinimum,
            float healthMaximum,
            bool immune,
            bool trueDamage,
            bool duplicate,
            bool healthChanged,
            bool becameZero,
            float overkillAmount,
            ResourceChangeResult resourceResult)
        {
            return new DamageApplicationResult(true, preview, code, message, request, resolvedTargetActorId, requestedAmount, defenseApplied, defenseMitigatedAmount, resistanceFraction, resistanceMitigatedAmount, finalDamageAmount, oldHealth, newHealth, healthMinimum, healthMaximum, immune, trueDamage, duplicate, healthChanged, becameZero, overkillAmount, resourceResult);
        }
    }
}
