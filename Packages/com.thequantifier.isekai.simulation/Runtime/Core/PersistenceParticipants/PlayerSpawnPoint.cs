using UnityEngine;
using UnityIsekaiGame.Places;

namespace UnityIsekaiGame.Persistence
{
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string spawnPointId = "spawn.prototype.default";
        [SerializeField] private PlaceDefinition place;
        [SerializeField] private int priority;
        [SerializeField] private string[] purposeTags;

        public string SpawnPointId => string.IsNullOrWhiteSpace(spawnPointId) ? name : spawnPointId;
        public PlaceDefinition Place => place;
        public int Priority => priority;
        public string[] PurposeTags => purposeTags;

        public void DevelopmentConfigure(string id, PlaceDefinition spawnPlace, int spawnPriority)
        {
            spawnPointId = id;
            place = spawnPlace;
            priority = spawnPriority;
        }
    }
}
