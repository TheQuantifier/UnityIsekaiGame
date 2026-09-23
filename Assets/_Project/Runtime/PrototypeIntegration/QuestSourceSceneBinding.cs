using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.PrototypeIntegration
{
    public sealed class QuestSourceSceneBinding : MonoBehaviour
    {
        [SerializeField] private string questSourceId;
        [SerializeField] private string questSourceDefinitionId;
        [SerializeField] private string sceneBindingKey;
        [SerializeField] private string sceneKey = PrototypeSceneIntegrationIds.SceneKey;
        [SerializeField] private string worldId = PersistenceService.LocalWorldId;
        [SerializeField] private string displayName;
        [SerializeField] private string hostLocationId;
        [SerializeField] private string interactionPointId;
        [SerializeField] private bool required = true;

        public string QuestSourceId => questSourceId ?? string.Empty;
        public string QuestSourceDefinitionId => questSourceDefinitionId ?? string.Empty;
        public string SceneBindingKey => sceneBindingKey ?? string.Empty;
        public string SceneKey => sceneKey ?? string.Empty;
        public string WorldId => worldId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public string HostLocationId => hostLocationId ?? string.Empty;
        public string InteractionPointId => interactionPointId ?? string.Empty;
        public bool Required => required;

        public void ConfigureQuestSource(
            string sourceId,
            string definitionId,
            string bindingKey,
            string display,
            string hostLocation,
            string interactionPoint,
            string scene = PrototypeSceneIntegrationIds.SceneKey,
            string world = PersistenceService.LocalWorldId,
            bool requiredBinding = true)
        {
            questSourceId = N(sourceId);
            questSourceDefinitionId = N(definitionId);
            sceneBindingKey = N(bindingKey);
            displayName = N(display);
            hostLocationId = N(hostLocation);
            interactionPointId = N(interactionPoint);
            sceneKey = string.IsNullOrWhiteSpace(scene) ? PrototypeSceneIntegrationIds.SceneKey : scene.Trim();
            worldId = string.IsNullOrWhiteSpace(world) ? PersistenceService.LocalWorldId : world.Trim();
            required = requiredBinding;
        }

        public PrototypeQuestSourceSceneBindingSnapshot CreateSnapshot()
        {
            return new PrototypeQuestSourceSceneBindingSnapshot(
                questSourceId,
                questSourceDefinitionId,
                sceneBindingKey,
                sceneKey,
                worldId,
                DisplayName,
                hostLocationId,
                interactionPointId,
                required);
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
