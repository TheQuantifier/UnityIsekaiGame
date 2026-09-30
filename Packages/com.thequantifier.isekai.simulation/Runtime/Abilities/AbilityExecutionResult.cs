namespace UnityIsekaiGame.Abilities
{
    public readonly struct AbilityExecutionResult
    {
        private AbilityExecutionResult(AbilityExecutionStatus status, string message, int failedEffectIndex, EffectExecutionResult effectResult)
        {
            Status = status;
            Message = message;
            FailedEffectIndex = failedEffectIndex;
            EffectResult = effectResult;
        }

        public AbilityExecutionStatus Status { get; }
        public string Message { get; }
        public int FailedEffectIndex { get; }
        public EffectExecutionResult EffectResult { get; }
        public bool Succeeded => Status == AbilityExecutionStatus.Success;

        public static AbilityExecutionResult Success(string message)
        {
            return new AbilityExecutionResult(AbilityExecutionStatus.Success, message, -1, default);
        }

        public static AbilityExecutionResult Failure(AbilityExecutionStatus status, string message, int failedEffectIndex = -1, EffectExecutionResult effectResult = default)
        {
            return new AbilityExecutionResult(status, message, failedEffectIndex, effectResult);
        }
    }
}
