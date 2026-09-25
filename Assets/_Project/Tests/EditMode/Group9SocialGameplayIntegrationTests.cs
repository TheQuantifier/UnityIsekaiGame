#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.People;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Emotions;
using UnityIsekaiGame.Social.Family;
using UnityIsekaiGame.Social.Influence;
using UnityIsekaiGame.Social.Integration;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Tests
{
    public sealed class Group9SocialGameplayIntegrationTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void AuthoredSocialContentAndExplicitAdultDataAreInCatalog()
        {
            DefinitionRegistry registry = Registry();
            Assert.That(registry.TryGet(PrototypeSocialInteractionDefinitionFactory.TradeId, out SocialInteractionDefinition _), Is.True);
            Assert.That(registry.TryGet(PrototypeSocialInteractionDefinitionFactory.AttackId, out SocialInteractionDefinition _), Is.True);
            Assert.That(registry.TryGet(PrototypeReputationDefinitionFactory.CityGuardAudienceId, out ReputationAudienceDefinition _), Is.True);
            Assert.That(registry.TryGet("person.prototype.merchant", out PersonDefinition merchant), Is.True);
            Assert.That(merchant.IsAdult, Is.True);
            Assert.That(merchant.ChronologicalAgeYears, Is.EqualTo(38));
            Assert.That(PrototypeSocialNetworkDefinitionFactory.CreateDefinitions().OfType<InformalSocialGroupDefinition>().Select(item => item.Category),
                Is.EquivalentTo(new[] { InformalSocialGroupCategory.FriendCircle, InformalSocialGroupCategory.AdventuringParty }));
        }

        [Test]
        public void RelationshipAndAttitudePersistenceAreWorldOwned()
        {
            DefinitionRegistry registry = Registry();
            string[] people = { PersistenceService.LocalPlayerId, "person.prototype.merchant" };
            RelationshipPersistenceParticipant relationships = new RelationshipPersistenceParticipant(new RelationshipRuntime(), () => registry, () => people, PersistenceService.LocalWorldId);
            InterpersonalAttitudePersistenceParticipant attitudes = new InterpersonalAttitudePersistenceParticipant(new InterpersonalAttitudeRuntime(), () => registry, () => people, PersistenceService.LocalWorldId);

            Assert.That(relationships.Scope, Is.EqualTo(PersistenceScope.SharedWorld));
            Assert.That(attitudes.Scope, Is.EqualTo(PersistenceScope.SharedWorld));
            Assert.That(relationships.OwnerId, Is.EqualTo(PersistenceService.LocalWorldId));
            Assert.That(attitudes.OwnerId, Is.EqualTo(PersistenceService.LocalWorldId));
        }

        [Test]
        public void CoordinatorRefreshesPeopleRecordsGameplayConsequencesAndPrunesHistory()
        {
            DefinitionRegistry registry = Registry();
            List<string> people = new List<string> { PersistenceService.LocalPlayerId, "person.prototype.merchant" };
            RelationshipRuntime relationships = new RelationshipRuntime();
            InterpersonalAttitudeRuntime attitudes = new InterpersonalAttitudeRuntime();
            ReputationRuntime reputation = new ReputationRuntime();
            RumorRuntime rumors = new RumorRuntime();
            SocialInteractionRuntime interactions = new SocialInteractionRuntime();
            SocialNormRuntime norms = new SocialNormRuntime();
            SocialNetworkRuntime networks = new SocialNetworkRuntime();
            SocialInfluenceRuntime influence = new SocialInfluenceRuntime();
            SocialEmotionRuntime emotions = new SocialEmotionRuntime();
            SocialDecisionRuntime decisions = new SocialDecisionRuntime();
            FamilyRelationshipRuntime family = new FamilyRelationshipRuntime();

            void Configure(IReadOnlyList<string> current)
            {
                relationships.Configure(registry, current);
                attitudes.Configure(registry, current);
                reputation.Configure(registry, current);
                rumors.Configure(registry, current);
                interactions.Configure(registry, current, relationships, attitudes, reputation, rumors);
                norms.Configure(registry, current, relationships, attitudes, reputation, rumors, interactions);
                networks.Configure(registry, current, relationships, attitudes, reputation, rumors, interactions, norms);
                influence.Configure(registry, current, attitudes, reputation, interactions);
                emotions.Configure(registry, current, relationships, attitudes, reputation, rumors, interactions, norms, networks, influence);
                decisions.Configure(registry, current, interactions, relationships, attitudes, reputation, rumors, norms, networks, SocialDecisionModifierSourceCollection.Compose(influence, emotions));
                family.Configure(registry, current, relationships, attitudes, interactions, PersistenceService.LocalWorldId, current);
            }

            Configure(people);
            int dirtySignals = 0;
            SocialSimulationCoordinator coordinator = new SocialSimulationCoordinator(
                () => people,
                () => PersistenceService.LocalPlayerId,
                () => "place.prototype-town",
                Configure,
                _ => dirtySignals++,
                new SocialSimulationSettings { autonomousNpcDecisions = false, maximumInteractionHistory = 2, maintenanceIntervalSeconds = 10d },
                relationships, attitudes, reputation, rumors, interactions, norms, networks, decisions, influence, emotions, family);

            for (int index = 0; index < 5; index++)
            {
                SocialInteractionResult result = coordinator.RecordInteraction(
                    PrototypeSocialInteractionDefinitionFactory.TradeId,
                    PersistenceService.LocalPlayerId,
                    "person.prototype.merchant",
                    $"tx.group9.trade.{index}",
                    index + 1d);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            Assert.That(attitudes.ResolveValue("person.prototype.merchant", PersistenceService.LocalPlayerId, PrototypeAttitudeDefinitionFactory.TrustId).EffectiveValue, Is.EqualTo(5));
            people.Add("person.prototype.guildmaster");
            coordinator.Advance(20d);
            Assert.That(coordinator.KnownPeople, Does.Contain("person.prototype.guildmaster"));
            Assert.That(interactions.Count, Is.EqualTo(2));
            Assert.That(dirtySignals, Is.GreaterThan(0));
        }

        private static DefinitionRegistry Registry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog.CreateRegistry();
        }
    }
}
#endif
