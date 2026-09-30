namespace UnityIsekaiGame.Beings
{
    public enum ActorSaveRestoreOrder
    {
        ResolveBeingDefinition = 0,
        ResolveActorProfileDefinition = 1,
        InitializeActorStatsBaseValues = 2,
        RestoreInventoryAndEquipment = 3,
        RestoreStatuses = 4,
        RebuildRuntimeModifiers = 5,
        RestoreCurrentVitals = 6,
        ClampVitalsToFinalMaximums = 7
    }
}
