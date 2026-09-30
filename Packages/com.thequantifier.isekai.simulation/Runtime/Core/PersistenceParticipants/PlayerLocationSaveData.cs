using System;

namespace UnityIsekaiGame.Persistence
{
    [Serializable]
    public sealed class PlayerLocationSaveData
    {
        public int schemaVersion = PlayerLocationPersistenceParticipant.CurrentParticipantSchemaVersion;
        public string sceneKey;
        public int sceneBuildIndex = -1;
        public string diagnosticSceneName;
        public string placeId;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float rotationW = 1f;
        public string spawnPointId;
        public string savedAtUtc;
        public string locationMode;
    }
}
