using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat
{
    public sealed class HealingApplicationResult
    {
        private HealingApplicationResult(
            bool succeeded,
            bool preview,
            string code,
            string message,
            HealingApplicationRequest request,
            string resolvedTargetActorId,
            float requestedAmount,
            float finalHealingAmount,
            float overhealAmount,
            float oldHealth,
            float newHealth,
            float healthMinimum,
            float healthMaximum,
            bool duplicate,
            bool healthChanged,
            bool becameFull,
            ResourceChangeResult resourceResult)
        {
            Succeeded = succeeded;
            Preview = preview;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? ImmediateCombatResultCode.Healed : ImmediateCombatResultCode.InvalidRequest : code;
            Message = message ?? string.Empty;
            Request = request;
            ResolvedTargetActorId = resolvedTargetActorId ?? string.Empty;
            RequestedAmount = requestedAmount;
            FinalHealingAmount = finalHealingAmount;
            OverhealAmount = overhealAmount;
            OldHealth = oldHealth;
            NewHealth = newHealth;
            HealthMinimum = healthMinimum;
            HealthMaximum = healthMaximum;
            Duplicate = duplicate;
            HealthChanged = healthChanged;
            BecameFull = becameFull;
            ResourceResult = resourceResult;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public string Code { get; }
        public string Message { get; }
        public HealingApplicationRequest Request { get; }
        public string ResolvedTargetActorId { get; }
        public float RequestedAmount { get; }
        public float FinalHealingAmount { get; }
        public float OverhealAmount { get; }
        public float OldHealth { get; }
        public float NewHealth { get; }
        public float HealthMinimum { get; }
        public float HealthMaximum { get; }
        public bool Duplicate { get; }
        public bool HealthChanged { get; }
        public bool BecameFull { get; }
        public ResourceChangeResult ResourceResult { get; }

        public static HealingApplicationResult Failure(HealingApplicationRequest request, string code, string message, string resolvedTargetActorId = "")
        {
            return new HealingApplicationResult(false, false, code, message, request, resolvedTargetActorId, request.RequestedAmount, 0f, 0f, 0f, 0f, 0f, 0f, false, false, false, null);
        }

        public static HealingApplicationResult Create(
            bool preview,
            string code,
            string message,
            HealingApplicationRequest request,
            string resolvedTargetActorId,
            float requestedAmount,
            float finalHealingAmount,
            float overhealAmount,
            float oldHealth,
            float newHealth,
            float healthMinimum,
            float healthMaximum,
            bool duplicate,
            bool healthChanged,
            bool becameFull,
            ResourceChangeResult resourceResult)
        {
            return new HealingApplicationResult(true, preview, code, message, request, resolvedTargetActorId, requestedAmount, finalHealingAmount, overhealAmount, oldHealth, newHealth, healthMinimum, healthMaximum, duplicate, healthChanged, becameFull, resourceResult);
        }
    }
}
