namespace UnityIsekaiGame.Gameplay
{
    public readonly struct VitalChangeResult
    {
        public VitalChangeResult(bool succeeded, float requestedAmount, float changedAmount, string message)
        {
            Succeeded = succeeded;
            RequestedAmount = requestedAmount;
            ChangedAmount = changedAmount;
            Message = message;
        }

        public bool Succeeded { get; }
        public float RequestedAmount { get; }
        public float ChangedAmount { get; }
        public string Message { get; }

        public static VitalChangeResult Success(float requestedAmount, float changedAmount, string message)
        {
            return new VitalChangeResult(true, requestedAmount, changedAmount, message);
        }

        public static VitalChangeResult Failure(float requestedAmount, string message)
        {
            return new VitalChangeResult(false, requestedAmount, 0f, message);
        }
    }
}
