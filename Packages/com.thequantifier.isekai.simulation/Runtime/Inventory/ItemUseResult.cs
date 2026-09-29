namespace UnityIsekaiGame.Inventory
{
    public readonly struct ItemUseResult
    {
        public ItemUseResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public bool Succeeded { get; }
        public string Message { get; }

        public static ItemUseResult Success(string message)
        {
            return new ItemUseResult(true, message);
        }

        public static ItemUseResult Failure(string message)
        {
            return new ItemUseResult(false, message);
        }
    }
}
