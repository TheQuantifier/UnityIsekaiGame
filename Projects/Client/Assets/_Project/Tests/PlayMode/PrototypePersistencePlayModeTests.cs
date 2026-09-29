using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Tests
{
    public sealed class PrototypePersistencePlayModeTests
    {
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [UnityTest]
        public IEnumerator PrototypeScene_PlayerAndWorldPersistenceAreReadyAndCapturable()
        {
            yield return SceneManager.LoadSceneAsync(PrototypeScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            PrototypePersistenceServiceBehaviour persistence = Object.FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            Assert.That(persistence, Is.Not.Null);
            persistence.EnsureInitialized();
            Assert.That(persistence.PlayerReadiness?.succeeded, Is.True, persistence.PlayerReadiness?.message);
            Assert.That(persistence.WorldReadiness?.succeeded, Is.True, persistence.WorldReadiness?.message);

            const string playerSlot = "phase3-validation";
            PersistenceSaveResult playerSave = persistence.SaveNamedSlot(playerSlot, "Phase 3 Validation", markClean: false);
            Assert.That(playerSave.Succeeded, Is.True, playerSave.Message);
            Assert.That(persistence.ValidateSaveSlot(playerSlot).Succeeded, Is.True);

            PersistenceSaveResult worldSave = persistence.SaveWorldCheckpoint("PlayMode validation");
            Assert.That(worldSave.Succeeded, Is.True, worldSave.Message);
            Assert.That(persistence.WorldService.ValidateSlot(PrototypeSaveSlotCatalog.CurrentWorldCheckpointSlotId).Succeeded, Is.True);

            persistence.DeleteSaveSlot(playerSlot);
            persistence.WorldService.DeleteSlot(PrototypeSaveSlotCatalog.CurrentWorldCheckpointSlotId);
        }

        [UnityTest]
        public IEnumerator PrototypeScene_ProfessionPanelDeclarationAndCraftingBridgeAreLive()
        {
            yield return SceneManager.LoadSceneAsync(PrototypeScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            PrototypePersistenceServiceBehaviour persistence = Object.FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            PrototypeProfessionPanel panel = Object.FindAnyObjectByType<PrototypeProfessionPanel>();
            Assert.That(persistence, Is.Not.Null);
            Assert.That(panel, Is.Not.Null, "The Prototype profession/career panel should be installed automatically.");
            persistence.EnsureInitialized();

            string personId = persistence.PlayerPersonId;
            ProfessionEntryOperationResult declaration = persistence.ProfessionCoordinator.DeclareInformalProfession(
                personId,
                ProfessionContentIds.CrafterProfessionId,
                ProfessionContentIds.CrafterSelfDeclaredEntryPathId,
                "10",
                "tx.playmode.declare-crafter");
            Assert.That(declaration.Succeeded, Is.True, declaration.Message);
            Assert.That(persistence.Professions.QueryByProfession(ProfessionContentIds.CrafterProfessionId, true).Count, Is.EqualTo(1));

            CraftingOperationRecordData operation = new CraftingOperationRecordData
            {
                operationId = "crafting-operation.playmode.profession",
                recipeId = "recipe.prototype-sword",
                actorPersonId = personId,
                worldTime = "11",
                state = CraftingOperationState.Completed,
                status = CraftingExecutionStatus.Succeeded,
                outputs = { new CraftingOutputItemData { itemInstanceId = "item.instance.playmode.profession", itemDefinitionId = "item.prototype-sword", quantity = 1f, createdItemInstance = true } }
            };
            var results = persistence.ProfessionCoordinator.RecordCrafting(operation, 800, ProfessionalActivityDifficulty.Skilled);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Succeeded, Is.True, results[0].Message);
            Assert.That(persistence.ProfessionalActivities.Evidence.Count, Is.EqualTo(1));
            Assert.That(persistence.CareerHistory.QueryEpisodesByPerson(personId).Count, Is.EqualTo(1));
            Assert.That(persistence.LifePaths.QueryLifePathsByPerson(personId).Count, Is.EqualTo(1));
        }
    }
}
