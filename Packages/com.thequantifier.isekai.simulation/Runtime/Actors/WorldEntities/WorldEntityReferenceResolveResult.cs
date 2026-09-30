namespace UnityIsekaiGame.WorldEntities
{
    public sealed class WorldEntityReferenceResolveResult
    {
        private WorldEntityReferenceResolveResult(bool succeeded, WorldEntityIdentity identity, string code, string message)
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

        public static WorldEntityReferenceResolveResult Success(WorldEntityIdentity identity)
        {
            return new WorldEntityReferenceResolveResult(true, identity, "Resolved", "World entity reference resolved.");
        }

        public static WorldEntityReferenceResolveResult Failure(string code, string message)
        {
            return new WorldEntityReferenceResolveResult(false, null, code, message);
        }
    }
}
