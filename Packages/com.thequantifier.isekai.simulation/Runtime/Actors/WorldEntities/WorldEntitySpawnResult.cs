namespace UnityIsekaiGame.WorldEntities
{
    public sealed class WorldEntitySpawnResult
    {
        private WorldEntitySpawnResult(bool succeeded, WorldEntityIdentity identity, string code, string message)
        {
            Succeeded = succeeded;
            Identity = identity;
            Code = code;
            Message = message;
        }

        public bool Succeeded { get; }
        public WorldEntityIdentity Identity { get; }
        public string Code { get; }
        public string Message { get; }

        public static WorldEntitySpawnResult Success(WorldEntityIdentity identity, string message)
        {
            return new WorldEntitySpawnResult(true, identity, "Success", message);
        }

        public static WorldEntitySpawnResult Failure(string code, string message)
        {
            return new WorldEntitySpawnResult(false, null, code, message);
        }
    }
}
