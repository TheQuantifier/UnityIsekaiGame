namespace UnityIsekaiGame.StatusEffects
{
    public readonly struct StatusEffectRestoreResult
    {
        private StatusEffectRestoreResult(StatusApplicationStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public StatusApplicationStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == StatusApplicationStatus.Success;

        public static StatusEffectRestoreResult Success()
        {
            return new StatusEffectRestoreResult(StatusApplicationStatus.Success, "Status effects restored.");
        }

        public static StatusEffectRestoreResult Failure(StatusApplicationStatus status, string message)
        {
            return new StatusEffectRestoreResult(status, message);
        }
    }
}
