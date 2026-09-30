namespace UnityIsekaiGame.Magic
{
    public readonly struct SpellCastResult
    {
        private SpellCastResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public bool Succeeded { get; }
        public string Message { get; }

        public static SpellCastResult Success(string message)
        {
            return new SpellCastResult(true, message);
        }

        public static SpellCastResult Failure(string message)
        {
            return new SpellCastResult(false, message);
        }
    }
}
