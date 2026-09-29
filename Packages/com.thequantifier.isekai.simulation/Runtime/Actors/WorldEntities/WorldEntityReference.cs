using System;
using UnityEngine;

namespace UnityIsekaiGame.WorldEntities
{
    [Serializable]
    public sealed class WorldEntityReference
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string entityId;
        public string sceneKey;
        public string worldId;
        public int identityKind;
        public string definitionId;
        public string expectedEntityType;

        public static WorldEntityReference FromIdentity(WorldEntityIdentity identity, string expectedType = null)
        {
            if (identity == null)
            {
                return null;
            }

            return new WorldEntityReference
            {
                schemaVersion = CurrentSchemaVersion,
                entityId = identity.EntityId,
                sceneKey = identity.SceneKey,
                worldId = identity.WorldId,
                identityKind = (int)identity.IdentityKind,
                definitionId = identity.DefinitionId,
                expectedEntityType = expectedType
            };
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static bool TryFromJson(string json, out WorldEntityReference reference, out string failureReason)
        {
            reference = null;
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                failureReason = "World entity reference JSON is empty.";
                return false;
            }

            try
            {
                reference = JsonUtility.FromJson<WorldEntityReference>(json);
            }
            catch (Exception)
            {
                failureReason = "World entity reference JSON is malformed.";
                return false;
            }

            if (reference == null)
            {
                failureReason = "World entity reference JSON did not parse.";
                return false;
            }

            return reference.ValidateForPersistence(out failureReason);
        }

        public bool ValidateForPersistence(out string failureReason)
        {
            failureReason = string.Empty;
            if (schemaVersion != CurrentSchemaVersion)
            {
                failureReason = $"Unsupported world entity reference schema version {schemaVersion}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(entityId))
            {
                failureReason = "World entity reference has no entity ID.";
                return false;
            }

            if (WorldEntityIdUtility.IsTransientReference(entityId) || identityKind == (int)WorldEntityIdentityKind.Transient)
            {
                failureReason = "Transient world entities cannot be persisted by reference.";
                return false;
            }

            if (!WorldEntityIdUtility.IsValidEntityId(entityId))
            {
                failureReason = $"World entity reference has invalid entity ID '{entityId}'.";
                return false;
            }

            return true;
        }

        public WorldEntityReferenceResolveResult Resolve()
        {
            if (!ValidateForPersistence(out string failureReason))
            {
                return WorldEntityReferenceResolveResult.Failure("InvalidReference", failureReason);
            }

            if (!WorldEntityRegistry.TryResolve(entityId, out WorldEntityIdentity identity))
            {
                return WorldEntityReferenceResolveResult.Failure("Unresolved", $"World entity '{entityId}' is not currently loaded.");
            }

            if (!string.IsNullOrWhiteSpace(expectedEntityType) && identity.GetComponent(expectedEntityType) == null)
            {
                return WorldEntityReferenceResolveResult.Failure("WrongType", $"World entity '{entityId}' does not have expected component '{expectedEntityType}'.");
            }

            return WorldEntityReferenceResolveResult.Success(identity);
        }
    }
}
