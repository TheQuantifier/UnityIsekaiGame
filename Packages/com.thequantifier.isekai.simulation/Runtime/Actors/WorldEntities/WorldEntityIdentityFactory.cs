using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldEntities
{
    public static class WorldEntityIdentityFactory
    {
        public static WorldEntitySpawnResult CreateRuntimeIdentity(
            GameObject gameObject,
            string sceneKey,
            string worldId,
            string definitionId = null,
            PersistenceScope scope = PersistenceScope.RegionOrScene)
        {
            string entityId = WorldEntityIdUtility.CreateRuntimeId(worldId);
            return InitializeRuntimeIdentity(gameObject, entityId, sceneKey, worldId, definitionId, scope, restored: false);
        }

        public static WorldEntitySpawnResult RestoreRuntimeIdentity(
            GameObject gameObject,
            string savedEntityId,
            string sceneKey,
            string worldId,
            string definitionId = null,
            PersistenceScope scope = PersistenceScope.RegionOrScene)
        {
            return InitializeRuntimeIdentity(gameObject, savedEntityId, sceneKey, worldId, definitionId, scope, restored: true);
        }

        private static WorldEntitySpawnResult InitializeRuntimeIdentity(GameObject gameObject, string entityId, string sceneKey, string worldId, string definitionId, PersistenceScope scope, bool restored)
        {
            if (gameObject == null)
            {
                return WorldEntitySpawnResult.Failure("MissingGameObject", "Cannot initialize world entity identity on a null GameObject.");
            }

            WorldEntityIdentity identity = gameObject.GetComponent<WorldEntityIdentity>();
            if (identity == null)
            {
                identity = gameObject.AddComponent<WorldEntityIdentity>();
            }
            else if (identity.IsRegistered)
            {
                WorldEntityRegistry.Unregister(identity);
            }

            bool initialized = restored
                ? identity.TryInitializeRestoredRuntime(entityId, sceneKey, worldId, scope, definitionId, out string failureReason)
                : identity.TryInitializeRuntime(entityId, sceneKey, worldId, scope, definitionId, out failureReason);
            if (!initialized)
            {
                return WorldEntitySpawnResult.Failure("InitializationFailed", failureReason);
            }

            WorldEntityRegistryResult registration = identity.TryRegister(out failureReason);
            return registration.Succeeded
                ? WorldEntitySpawnResult.Success(identity, registration.Message)
                : WorldEntitySpawnResult.Failure(registration.Code, registration.Message);
        }
    }
}
