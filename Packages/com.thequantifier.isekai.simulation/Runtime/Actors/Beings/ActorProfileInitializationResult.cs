namespace UnityIsekaiGame.Beings
{
    public readonly struct ActorProfileInitializationResult
    {
        public ActorProfileInitializationResult(ActorProfileInitializationStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public ActorProfileInitializationStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == ActorProfileInitializationStatus.InitializedFromProfile
            || Status == ActorProfileInitializationStatus.AlreadyInitialized;
    }
}
