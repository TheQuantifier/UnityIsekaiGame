using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public sealed class WorldSceneBindingBootstrap : MonoBehaviour
    {
        [SerializeField] private WorldSceneBindingBootstrapMode bootstrapMode = WorldSceneBindingBootstrapMode.ProductionBindOnly;
        [SerializeField] private bool registerChildrenOnEnable = true;
        [SerializeField] private bool syncAfterRegister = true;

        public WorldSceneBindingBootstrapMode BootstrapMode => bootstrapMode;
        public WorldSceneBindingValidationReport LastReport { get; private set; }

        private void OnEnable()
        {
            if (registerChildrenOnEnable)
            {
                if (bootstrapMode == WorldSceneBindingBootstrapMode.DevelopmentFixtureImport)
                {
                    ConfigurePrototypeFixtureRuntime(WorldSceneBindingRuntime.Default);
                    RegisterLoadedSceneBindings(WorldSceneBindingRuntime.Default);
                }
                else
                {
                    RegisterChildren(WorldSceneBindingRuntime.Default);
                }
            }
        }

        public void ConfigureMode(WorldSceneBindingBootstrapMode mode)
        {
            bootstrapMode = mode;
        }

        public WorldSceneBindingValidationReport RegisterChildren(WorldSceneBindingRuntime runtime)
        {
            WorldSceneBindingRuntime target = runtime ?? WorldSceneBindingRuntime.Default;
            foreach (WorldSceneBindingComponent binding in GetComponentsInChildren<WorldSceneBindingComponent>(true))
            {
                binding.Register(target);
            }

            LastReport = syncAfterRegister ? target.SyncAllFromAuthoritative(true) : target.Validate();
            return LastReport;
        }

        private void RegisterLoadedSceneBindings(WorldSceneBindingRuntime runtime)
        {
            WorldSceneBindingRuntime target = runtime ?? WorldSceneBindingRuntime.Default;
            foreach (WorldSceneBindingComponent binding in FindObjectsByType<WorldSceneBindingComponent>(FindObjectsInactive.Include))
            {
                binding.Register(target);
            }

            LastReport = syncAfterRegister ? target.SyncAllFromAuthoritative(true) : target.Validate();
        }

        private static void ConfigurePrototypeFixtureRuntime(WorldSceneBindingRuntime runtime)
        {
            DefinitionRegistry registry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            registry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(registry);
            registry = PrototypeInteractionPointDefinitionFactory.AddMissingPrototypeInteractionDefinitions(registry);
            registry = PrototypeLocationConnectionDefinitionFactory.AddMissingPrototypeConnectionDefinitions(registry);

            LocationRuntime locations = new LocationRuntime();
            EntityLocationRuntime entityLocations = new EntityLocationRuntime();
            InteractionPointRuntime interactionPoints = new InteractionPointRuntime();
            LocationConnectionRuntime connections = new LocationConnectionRuntime();
            PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locations, registry, PersistenceService.LocalWorldId);
            PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocations, locations, PersistenceService.LocalWorldId);
            PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(interactionPoints, registry, locations, entityLocations, PersistenceService.LocalWorldId);
            PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(connections, registry, locations, entityLocations, interactionPoints, PersistenceService.LocalWorldId);
            runtime.Configure(locations, entityLocations, interactionPoints, connections, runtimeWorldId: PersistenceService.LocalWorldId);
        }
    }
}
