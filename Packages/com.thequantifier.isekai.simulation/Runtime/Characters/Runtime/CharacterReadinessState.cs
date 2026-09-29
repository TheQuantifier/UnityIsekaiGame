namespace UnityIsekaiGame.CharacterSystem
{
    public enum CharacterReadinessState
    {
        Uninitialized,
        DefinitionsReady,
        IdentityReady,
        Restoring,
        Rebuilding,
        Ready,
        Failed,
        Disposed
    }
}
