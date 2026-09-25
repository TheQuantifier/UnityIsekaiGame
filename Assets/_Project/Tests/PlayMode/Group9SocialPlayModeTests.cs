using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Interactions;

namespace UnityIsekaiGame.Tests
{
    public sealed class Group9SocialPlayModeTests
    {
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [UnityTest]
        public IEnumerator PrototypeScene_SocialSimulationDiscoversPeopleAndRecordsGameplayInteraction()
        {
            yield return SceneManager.LoadSceneAsync(PrototypeScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            PrototypePersistenceServiceBehaviour persistence = Object.FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            Assert.That(persistence, Is.Not.Null);
            persistence.EnsureInitialized();

            string player = persistence.PlayerPersonId;
            const string merchant = "person.prototype.merchant";
            Assert.That(persistence.KnownSocialPersonIds, Does.Contain(player));
            Assert.That(persistence.KnownSocialPersonIds, Does.Contain("person.prototype.prisoner"));
            Assert.That(persistence.KnownSocialPersonIds, Does.Contain(merchant));
            Assert.That(persistence.KnownSocialPersonIds, Does.Contain("person.prototype.guildmaster"));

            int trustBefore = persistence.InterpersonalAttitudes
                .ResolveValue(merchant, player, PrototypeAttitudeDefinitionFactory.TrustId)
                .EffectiveValue;
            SocialInteractionResult result = persistence.RecordSocialInteraction(
                PrototypeSocialInteractionDefinitionFactory.TradeId,
                player,
                merchant,
                "playmode.social",
                "social.playmode.trade");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Duplicate, Is.False);
            int trustAfter = persistence.InterpersonalAttitudes
                .ResolveValue(merchant, player, PrototypeAttitudeDefinitionFactory.TrustId)
                .EffectiveValue;
            Assert.That(trustAfter, Is.GreaterThan(trustBefore));
            Assert.That(persistence.BuildSocialDebugReport(merchant), Does.Contain("Known people:"));
        }
    }
}
