using System;

namespace UnityIsekaiGame.Abilities
{
    public readonly struct EffectExecutionResult
    {
        private EffectExecutionResult(EffectExecutionStatus status, string message, float appliedMagnitude, Action rollback)
        {
            Status = status;
            Message = message;
            AppliedMagnitude = appliedMagnitude;
            Rollback = rollback;
        }

        public EffectExecutionStatus Status { get; }
        public string Message { get; }
        public float AppliedMagnitude { get; }
        public bool Succeeded => Status == EffectExecutionStatus.Success;
        internal Action Rollback { get; }

        public static EffectExecutionResult Success(string message, float appliedMagnitude = 0f, Action rollback = null)
        {
            return new EffectExecutionResult(EffectExecutionStatus.Success, message, appliedMagnitude, rollback);
        }

        public static EffectExecutionResult Failure(EffectExecutionStatus status, string message)
        {
            return new EffectExecutionResult(status, message, 0f, null);
        }
    }
}
