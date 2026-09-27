using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public sealed class LocationSceneBinding : WorldSceneBindingComponent
    {
        [SerializeField] private string locationDefinitionId;

        public override WorldSceneBindingCategory Category => WorldSceneBindingCategory.Location;
        public string LocationDefinitionId => locationDefinitionId ?? string.Empty;

        public void ConfigureLocation(string locationId, string sceneBindingKey, string scene, string world, string expectedDefinitionId = "", WorldSceneBindingRole bindingRole = WorldSceneBindingRole.Primary, bool requiredBinding = false)
        {
            ConfigureBinding(locationId, sceneBindingKey, scene, world, bindingRole, requiredBinding);
            locationDefinitionId = N(expectedDefinitionId);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Status != WorldSceneBindingStatus.Bound || other == null || other.GetComponentInParent<PlayerInputReader>() == null)
            {
                return;
            }

            PrototypePersistenceServiceBehaviour services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            double worldTime = services?.PlayTime?.CumulativeSeconds ?? Time.unscaledTimeAsDouble;
            string playerId = services?.PlayerPersonId ?? "person.prototype.player";
            string targetId = string.IsNullOrWhiteSpace(LogicalId) ? LocationDefinitionId : LogicalId;
            string sourceId = $"location-enter.{targetId}.{name}.{Time.frameCount}";
            QuestObjectiveSignalBus.Report(QuestObjectiveCategory.VisitLocation, targetId, playerId, worldTime, sourceEventId: sourceId, subjectType: UnityIsekaiGame.Knowledge.Access.InformationSubjectType.Location);
            QuestObjectiveSignalBus.Report(QuestObjectiveCategory.ReachLocation, targetId, playerId, worldTime, sourceEventId: sourceId, subjectType: UnityIsekaiGame.Knowledge.Access.InformationSubjectType.Location);
            QuestObjectiveSignalBus.Report(QuestObjectiveCategory.DiscoverLocation, targetId, playerId, worldTime, sourceEventId: sourceId, subjectType: UnityIsekaiGame.Knowledge.Access.InformationSubjectType.Location);
        }

        private void OnTriggerExit(Collider other)
        {
            if (Status != WorldSceneBindingStatus.Bound || other == null || other.GetComponentInParent<PlayerInputReader>() == null)
            {
                return;
            }

            PrototypePersistenceServiceBehaviour services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            QuestObjectiveSignalBus.Report(
                QuestObjectiveCategory.LeaveLocation,
                string.IsNullOrWhiteSpace(LogicalId) ? LocationDefinitionId : LogicalId,
                services?.PlayerPersonId ?? "person.prototype.player",
                services?.PlayTime?.CumulativeSeconds ?? Time.unscaledTimeAsDouble,
                sourceEventId: $"location-exit.{LogicalId}.{name}.{Time.frameCount}",
                subjectType: UnityIsekaiGame.Knowledge.Access.InformationSubjectType.Location);
        }
    }
}
