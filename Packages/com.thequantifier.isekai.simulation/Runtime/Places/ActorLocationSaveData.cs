using System;
using UnityEngine;

namespace UnityIsekaiGame.Places
{
    [Serializable]
    public struct ActorLocationSaveData
    {
        public string currentPlaceId;
        public string sceneKey;
        public Vector3 position;
        public Quaternion rotation;
    }
}
