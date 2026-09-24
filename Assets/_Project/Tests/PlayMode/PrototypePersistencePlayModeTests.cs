using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Gameplay;

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
    }
}
