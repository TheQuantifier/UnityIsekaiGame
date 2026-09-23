using System;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.PrototypeIntegration
{
    public static class PrototypeQuestSourceSceneFactory
    {
        public static void SeedPrototypeSceneQuestSources(QuestSourceRuntime runtime, DefinitionRegistry registry, string worldId = PersistenceService.LocalWorldId)
        {
            if (runtime == null)
            {
                return;
            }

            string world = string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId.Trim();
            foreach (PrototypeQuestSourceBindingExpectation expectation in PrototypeSceneIntegrationContract.QuestSourceBindings)
            {
                if (runtime.TryGetSource(expectation.QuestSourceId, out _))
                {
                    continue;
                }

                runtime.CreateSource(new QuestSourceCreateRequest
                {
                    transactionId = $"prototype.scene.quest-source.seed.{expectation.QuestSourceId}",
                    questSourceId = expectation.QuestSourceId,
                    questSourceDefinitionId = expectation.DefinitionId,
                    hostLocationId = expectation.HostLocationId,
                    interactionPointId = expectation.InteractionPointId,
                    operatingOrganizationId = expectation.OperatingOrganizationId,
                    operatingGovernmentId = expectation.OperatingGovernmentId,
                    sceneBindingKey = expectation.BindingKey,
                    worldTime = 0d,
                    provenanceId = "prototype.scene.integration.seed"
                });
            }
        }
    }
}
