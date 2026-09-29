namespace UnityIsekaiGame.StatusEffects
{
    public enum StatusPersistencePolicy
    {
        SaveRemainingDuration = 0,
        DoNotSave = 1,
        PersistentUntilRemoved = 2,
        ExpireOnLoad = 3
    }
}
