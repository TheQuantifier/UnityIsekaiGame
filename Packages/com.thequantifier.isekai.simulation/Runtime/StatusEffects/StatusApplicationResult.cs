namespace UnityIsekaiGame.StatusEffects
{
    public readonly struct StatusApplicationResult
    {
        private StatusApplicationResult(StatusApplicationStatus status, string message, RuntimeStatusEffect statusEffect)
        {
            Status = status;
            Message = message;
            StatusEffect = statusEffect;
        }

        public StatusApplicationStatus Status { get; }
        public string Message { get; }
        public RuntimeStatusEffect StatusEffect { get; }
        public bool Succeeded => Status == StatusApplicationStatus.Success;

        public static StatusApplicationResult Success(RuntimeStatusEffect statusEffect, string message)
        {
            return new StatusApplicationResult(StatusApplicationStatus.Success, message, statusEffect);
        }

        public static StatusApplicationResult Failure(StatusApplicationStatus status, string message)
        {
            return new StatusApplicationResult(status, message, null);
        }
    }
}
