using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public sealed class WorldEntitySceneBinding : WorldSceneBindingComponent
    {
        [SerializeField] private LocationOccupantEntityType entityType = LocationOccupantEntityType.Person;
        [SerializeField] private string entityId;
        [SerializeField] private bool snapToGroundAfterMaterialization = true;
        [SerializeField] private bool preserveSceneTransformOnInitialSync;
        [SerializeField] private float groundProbeHeight = 25f;
        [SerializeField] private float groundProbeDistance = 80f;

        public override WorldSceneBindingCategory Category => WorldSceneBindingCategory.Entity;
        public bool SnapToGroundAfterMaterialization => snapToGroundAfterMaterialization;
        public bool PreserveSceneTransformOnInitialSync => preserveSceneTransformOnInitialSync;
        public float GroundProbeHeight => groundProbeHeight;
        public float GroundProbeDistance => groundProbeDistance;
        public EntityLocationReferenceData EntityReference => new EntityLocationReferenceData { entityType = entityType, entityId = N(entityId), worldId = WorldId };

        public void ConfigureEntity(
            LocationOccupantEntityType type,
            string id,
            string sceneBindingKey,
            string scene,
            string world,
            bool snapToGround = true,
            bool preserveInitialSceneTransform = false)
        {
            entityType = type;
            entityId = N(id);
            ConfigureBinding(EntityLocationReferenceKey.Build(type, entityId, string.IsNullOrWhiteSpace(world) ? PersistenceService.LocalWorldId : world), sceneBindingKey, scene, world, WorldSceneBindingRole.Primary, false);
            snapToGroundAfterMaterialization = snapToGround;
            preserveSceneTransformOnInitialSync = preserveInitialSceneTransform;
        }

        public override void SyncFromAuthoritative(WorldSceneBindingRuntime bindingRuntime, bool initialSync)
        {
            // Initial player placement is owned by the scene spawn/persistence pipeline. A later
            // non-initial sync is still allowed to materialize intentional authoritative travel.
            if (initialSync && preserveSceneTransformOnInitialSync)
            {
                return;
            }

            SceneBindingMaterializationResult result = bindingRuntime.MaterializeEntity(this);
            if (!result.Succeeded)
            {
                ApplyBindingResolution(WorldSceneBindingStatus.Degraded, result.Message);
            }
        }
    }
}
