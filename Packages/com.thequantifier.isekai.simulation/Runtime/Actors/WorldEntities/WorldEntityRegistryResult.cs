namespace UnityIsekaiGame.WorldEntities
{
    public readonly struct WorldEntityRegistryResult
    {
        private WorldEntityRegistryResult(bool succeeded, string code, string message)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }

        public static WorldEntityRegistryResult Success(string message = "World entity registry operation succeeded.")
        {
            return new WorldEntityRegistryResult(true, "Success", message);
        }

        public static WorldEntityRegistryResult Failure(string code, string message)
        {
            return new WorldEntityRegistryResult(false, code, message);
        }
    }
}
