using System;
using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldEntities
{
    [DisallowMultipleComponent]
    public sealed class WorldEntityIdentity : MonoBehaviour
    {
        [SerializeField] private WorldEntityIdentityKind identityKind = WorldEntityIdentityKind.Authored;
        [SerializeField] private string localAuthoredId;
        [SerializeField] private string runtimeEntityId;
        [SerializeField] private string sceneKey = "scene.prototype";
        [SerializeField] private string worldId = PersistenceService.LocalWorldId;
        [SerializeField] private PersistenceScope ownerScope = PersistenceScope.RegionOrScene;
        [SerializeField] private string ownerId;
        [SerializeField] private string parentEntityId;
        [SerializeField] private bool persistenceEligible = true;
        [SerializeField] private string definitionId;
        [SerializeField] private string expectedEntityType;
        [SerializeField] private bool registerInAwake = true;

        private bool registered;
        private string registeredEntityId;

        public string LocalAuthoredId => WorldEntityIdUtility.Normalize(localAuthoredId);
        public string RuntimeEntityId => WorldEntityIdUtility.Normalize(runtimeEntityId);
        public string SceneKey => WorldEntityIdUtility.Normalize(sceneKey);
        public string WorldId => WorldEntityIdUtility.Normalize(worldId);
        public PersistenceScope OwnerScope => ownerScope;
        public string OwnerId => ownerId ?? string.Empty;
        public string ParentEntityId => WorldEntityIdUtility.Normalize(parentEntityId);
        public bool PersistenceEligible => persistenceEligible && identityKind != WorldEntityIdentityKind.Transient;
        public string DefinitionId => WorldEntityIdUtility.Normalize(definitionId);
        public string ExpectedEntityType => expectedEntityType ?? string.Empty;
        public WorldEntityIdentityKind IdentityKind => identityKind;
        public bool IsRegistered => registered;

        public string EntityId
        {
            get
            {
                return identityKind == WorldEntityIdentityKind.Authored
                    ? WorldEntityIdUtility.ComposeAuthoredId(SceneKey, LocalAuthoredId)
                    : RuntimeEntityId;
            }
        }

        private void Awake()
        {
            if (registerInAwake)
            {
                TryRegister(out _);
            }
        }

        private void OnDestroy()
        {
            if (registered)
            {
                WorldEntityRegistry.Unregister(this);
            }
        }

        private void OnValidate()
        {
            localAuthoredId = WorldEntityIdUtility.Normalize(localAuthoredId);
            runtimeEntityId = WorldEntityIdUtility.Normalize(runtimeEntityId);
            sceneKey = WorldEntityIdUtility.Normalize(sceneKey);
            worldId = WorldEntityIdUtility.Normalize(worldId);
            parentEntityId = WorldEntityIdUtility.Normalize(parentEntityId);
            definitionId = WorldEntityIdUtility.Normalize(definitionId);
        }

        public WorldEntityRegistryResult TryRegister(out string failureReason)
        {
            failureReason = string.Empty;
            if (!ValidateIdentity(out failureReason))
            {
                return WorldEntityRegistryResult.Failure("InvalidIdentity", failureReason);
            }

            WorldEntityRegistryResult result = WorldEntityRegistry.Register(this);
            if (!result.Succeeded)
            {
                failureReason = result.Message;
                return result;
            }

            registered = true;
            registeredEntityId = EntityId;
            return result;
        }

        public void MarkRegisteredForRegistry()
        {
            registered = true;
            registeredEntityId = EntityId;
        }

        public void MarkUnregisteredForRegistry()
        {
            registered = false;
            registeredEntityId = string.Empty;
        }

        public bool TryInitializeRuntime(string newEntityId, string newSceneKey, string newWorldId, PersistenceScope scope, string diagnosticDefinitionId, out string failureReason)
        {
            failureReason = string.Empty;
            if (registered)
            {
                failureReason = "Cannot initialize a runtime world entity after registration.";
                return false;
            }

            string normalizedId = WorldEntityIdUtility.Normalize(newEntityId);
            if (!WorldEntityIdUtility.IsValidEntityId(normalizedId))
            {
                failureReason = $"Runtime world entity ID '{newEntityId}' is invalid.";
                return false;
            }

            identityKind = WorldEntityIdentityKind.RuntimeSpawned;
            runtimeEntityId = normalizedId;
            sceneKey = WorldEntityIdUtility.Normalize(newSceneKey);
            worldId = WorldEntityIdUtility.Normalize(newWorldId);
            ownerScope = scope;
            definitionId = WorldEntityIdUtility.Normalize(diagnosticDefinitionId);
            registerInAwake = false;
            return true;
        }

        public bool TryInitializeRestoredRuntime(string savedEntityId, string restoredSceneKey, string restoredWorldId, PersistenceScope scope, string diagnosticDefinitionId, out string failureReason)
        {
            if (!TryInitializeRuntime(savedEntityId, restoredSceneKey, restoredWorldId, scope, diagnosticDefinitionId, out failureReason))
            {
                return false;
            }

            identityKind = WorldEntityIdentityKind.RestoredRuntime;
            return true;
        }

        public bool TrySetAuthoredIdentity(string newLocalAuthoredId, string newSceneKey, PersistenceScope scope, string diagnosticDefinitionId, out string failureReason)
        {
            failureReason = string.Empty;
            if (registered)
            {
                failureReason = "Cannot change an authored world entity ID after registration.";
                return false;
            }

            string normalizedLocal = WorldEntityIdUtility.Normalize(newLocalAuthoredId);
            if (!WorldEntityIdUtility.IsValidLocalAuthoredId(normalizedLocal))
            {
                failureReason = $"Authored world entity local ID '{newLocalAuthoredId}' is invalid.";
                return false;
            }

            identityKind = WorldEntityIdentityKind.Authored;
            localAuthoredId = normalizedLocal;
            sceneKey = WorldEntityIdUtility.Normalize(newSceneKey);
            ownerScope = scope;
            definitionId = WorldEntityIdUtility.Normalize(diagnosticDefinitionId);
            return true;
        }

        public bool TryMarkTransient(out string failureReason)
        {
            failureReason = string.Empty;
            if (registered)
            {
                failureReason = "Cannot mark a registered world entity transient.";
                return false;
            }

            identityKind = WorldEntityIdentityKind.Transient;
            persistenceEligible = false;
            return true;
        }

        public bool ValidateIdentity(out string failureReason)
        {
            failureReason = string.Empty;
            if (identityKind == WorldEntityIdentityKind.Transient)
            {
                return true;
            }

            if (registered && !string.Equals(registeredEntityId, EntityId, StringComparison.Ordinal))
            {
                failureReason = "World entity ID changed after registration.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(SceneKey))
            {
                failureReason = "World entity scene key is missing.";
                return false;
            }

            if (identityKind == WorldEntityIdentityKind.Authored)
            {
                if (!WorldEntityIdUtility.IsValidLocalAuthoredId(LocalAuthoredId))
                {
                    failureReason = $"Authored world entity local ID '{localAuthoredId}' is invalid.";
                    return false;
                }
            }
            else if (!WorldEntityIdUtility.IsValidEntityId(RuntimeEntityId))
            {
                failureReason = $"Runtime world entity ID '{runtimeEntityId}' is invalid.";
                return false;
            }

            if (!WorldEntityIdUtility.IsValidEntityId(EntityId))
            {
                failureReason = $"Resolved world entity ID '{EntityId}' is invalid.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(parentEntityId) && !WorldEntityIdUtility.IsValidEntityId(parentEntityId))
            {
                failureReason = $"Parent world entity ID '{parentEntityId}' is invalid.";
                return false;
            }

            return true;
        }

        public WorldEntityReference CreateReference(string expectedType = null)
        {
            return WorldEntityReference.FromIdentity(this, expectedType ?? expectedEntityType);
        }
    }
}
