namespace UnityIsekaiGame.Magic
{
    public readonly struct SpellLoadoutOperationResult
    {
        private SpellLoadoutOperationResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public bool Succeeded { get; }
        public string Message { get; }

        public static SpellLoadoutOperationResult Success(string message)
        {
            return new SpellLoadoutOperationResult(true, message);
        }

        public static SpellLoadoutOperationResult Failure(string message)
        {
            return new SpellLoadoutOperationResult(false, message);
        }
    }
}
