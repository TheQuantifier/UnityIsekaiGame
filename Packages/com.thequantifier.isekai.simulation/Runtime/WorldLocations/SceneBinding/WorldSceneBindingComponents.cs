using System;
using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public abstract class WorldSceneBindingComponent : MonoBehaviour
    {
        [SerializeField] private string instanceId;
        [SerializeField] private string worldId = PersistenceService.LocalWorldId;
        [SerializeField] private string sceneKey = "scene.prototype";
        [SerializeField] private string logicalId;
        [SerializeField] private string bindingKey;
        [SerializeField] private string displayName;
        [SerializeField] private WorldSceneBindingRole role = WorldSceneBindingRole.Primary;
        [SerializeField] private bool required;
        [SerializeField] private bool autoRegister = true;
        [SerializeField] private Transform anchor;

        private WorldSceneBindingStatus status = WorldSceneBindingStatus.Unregistered;
        private string diagnostics = string.Empty;
        private WorldSceneBindingRuntime runtime;

        public abstract WorldSceneBindingCategory Category { get; }
        public string InstanceId => instanceId ?? string.Empty;
        public string WorldId => worldId ?? string.Empty;
        public string SceneKey => sceneKey ?? string.Empty;
        public string LogicalId => logicalId ?? string.Empty;
        public string BindingKey => bindingKey ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public string AuthoredDisplayName => displayName ?? string.Empty;
        public WorldSceneBindingRole Role => role;
        public bool Required => required;
        public bool AutoRegister => autoRegister;
        public Transform BindingTransform => anchor != null ? anchor : transform;
        public WorldSceneBindingStatus Status => status;
        public string Diagnostics => diagnostics ?? string.Empty;
        protected WorldSceneBindingRuntime Runtime => runtime ?? WorldSceneBindingRuntime.Default;

        protected virtual void Reset()
        {
            RefreshGeneratedInstanceId();
            if (string.IsNullOrWhiteSpace(bindingKey))
            {
                bindingKey = $"{gameObject.scene.name}.{gameObject.name}".Replace(' ', '-').ToLowerInvariant();
            }
        }

        protected virtual void OnEnable()
        {
            if (autoRegister)
            {
                Register(WorldSceneBindingRuntime.Default);
            }
        }

        protected virtual void OnDisable()
        {
            if (autoRegister)
            {
                Runtime.Unregister(this);
            }
        }

        public void ConfigureBinding(string logicalRecordId, string sceneBindingKey, string scene, string world, WorldSceneBindingRole bindingRole = WorldSceneBindingRole.Primary, bool requiredBinding = false, string authoredDisplayName = null)
        {
            logicalId = N(logicalRecordId);
            bindingKey = N(sceneBindingKey);
            sceneKey = string.IsNullOrWhiteSpace(scene) ? sceneKey : scene.Trim();
            worldId = string.IsNullOrWhiteSpace(world) ? worldId : world.Trim();
            role = bindingRole;
            required = requiredBinding;
            if (authoredDisplayName != null)
            {
                displayName = N(authoredDisplayName);
            }
        }

        public WorldSceneBindingSnapshot Register(WorldSceneBindingRuntime targetRuntime)
        {
            runtime = targetRuntime ?? WorldSceneBindingRuntime.Default;
            RefreshGeneratedInstanceId();
            return runtime.Register(this);
        }

        internal void AttachRuntime(WorldSceneBindingRuntime targetRuntime)
        {
            runtime = targetRuntime ?? WorldSceneBindingRuntime.Default;
        }

        public void RefreshGeneratedInstanceId()
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                instanceId = $"{GetType().Name}.{Guid.NewGuid():N}";
            }
        }

        public void ApplyBindingResolution(WorldSceneBindingStatus nextStatus, string message)
        {
            status = nextStatus;
            diagnostics = message ?? string.Empty;
            OnBindingResolutionChanged(nextStatus, diagnostics);
        }

        public virtual void SyncFromAuthoritative(WorldSceneBindingRuntime bindingRuntime, bool initialSync)
        {
        }

        protected virtual void OnBindingResolutionChanged(WorldSceneBindingStatus nextStatus, string message)
        {
        }

        public WorldSceneBindingSnapshot CreateSnapshot()
        {
            return new WorldSceneBindingSnapshot(
                instanceId,
                worldId,
                sceneKey,
                gameObject.scene.name,
                Category,
                role,
                logicalId,
                bindingKey,
                DisplayName,
                status,
                required,
                diagnostics);
        }

        protected static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

}
