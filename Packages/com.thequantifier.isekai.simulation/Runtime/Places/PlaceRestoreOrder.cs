namespace UnityIsekaiGame.Places
{
    public enum PlaceRestoreOrder
    {
        ResolvePlaceDefinition = 0,
        LoadOrConfirmScene = 1,
        LocateSpawnOrSavedPosition = 2,
        RestoreActorPosition = 3,
        RestoreCurrentPlaceTracking = 4,
        RestorePlaceRuntimeState = 5,
        NotifyQuestsSchedulesAndUi = 6
    }
}
