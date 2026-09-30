using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.PrototypeIntegration;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Tests
{
    public sealed class Group11WorldTravelIntegrationTests
    {
        [Test]
        public void PrototypeLocationsExposeCanonicalPlaceIdentity()
        {
            Fixture fixture = CreateFixture();

            Assert.That(fixture.Locations.TryGetSnapshot("location.prototype.town", out LocationSnapshot town), Is.True);
            Assert.That(town.AuthoredPlaceDefinitionId, Is.EqualTo("place.settlement.prototype-town"));
            Assert.That(fixture.Locations.TryGetSnapshot("location.prototype.wilderness-ring", out LocationSnapshot wilderness), Is.True);
            Assert.That(wilderness.AuthoredPlaceDefinitionId, Is.EqualTo("place.wilderness.prototype-outskirts"));
            Assert.That(fixture.EntityLocations.TryResolvePersonId(
                PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.MerchantBodyId, fixture.WorldId),
                out string merchantPersonId), Is.True);
            Assert.That(merchantPersonId, Is.EqualTo(PrototypeEntityLocationFactory.MerchantPersonId));
        }

        [Test]
        public void CoordinatorUsesAuthoredSpeedAndPersistsCalculatedMovementRate()
        {
            Fixture fixture = CreateFixture();
            EntityLocationReferenceData traveler = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            WorldTravelCoordinator coordinator = new WorldTravelCoordinator(fixture.Registry, fixture.EntityLocations, fixture.Routes, fixture.Journeys, null);

            WorldTravelResult started = coordinator.StartTravel(new WorldTravelRequest
            {
                RequestId = "test.group11.coordinator",
                Traveler = traveler,
                TravelerPersonId = PrototypeEntityLocationFactory.PlayerPersonId,
                DestinationLocationId = "location.prototype.market-district",
                TravelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId,
                AccessContext = new LocationConnectionAccessContextData { actor = traveler, personId = PrototypeEntityLocationFactory.PlayerPersonId },
                MovementProfile = new TravelMovementProfile { SkillMultiplier = 1.5d },
                WorldTime = 10d
            });

            Assert.That(started.Succeeded, Is.True, started.Message);
            Assert.That(started.Journey.LifecycleState, Is.EqualTo(TravelJourneyLifecycleState.Active));
            Assert.That(started.Journey.MovementRateOverrideMetersPerSecond, Is.EqualTo(2.1d).Within(0.0001d));

            coordinator.AdvanceActiveJourneys(100d);
            Assert.That(fixture.Journeys.TryGetJourney(started.Journey.JourneyId, out TravelJourneySnapshot completed), Is.True);
            Assert.That(completed.LifecycleState, Is.EqualTo(TravelJourneyLifecycleState.Completed));
            Assert.That(fixture.EntityLocations.TryGetActivePlacement(traveler, out EntityPlacementSnapshot placement), Is.True);
            Assert.That(placement.ExactLocationId, Is.EqualTo("location.prototype.market-district"));
        }

        [Test]
        public void SceneContractIncludesTravelBindingsAndExplicitInteractionServices()
        {
            Assert.That(PrototypeSceneIntegrationContract.WorldBindings.Any(value => value.Category == WorldSceneBindingCategory.RouteSegment), Is.True);
            Assert.That(PrototypeSceneIntegrationContract.WorldBindings.Any(value => value.Category == WorldSceneBindingCategory.Checkpoint), Is.True);
            Assert.That(PrototypeSceneIntegrationContract.WorldBindings.Any(value => value.Category == WorldSceneBindingCategory.SpawnAnchor), Is.True);
            Assert.That(PrototypeSceneIntegrationContract.WorldBindings
                .Where(value => value.Category == WorldSceneBindingCategory.InteractionPoint)
                .All(value => !string.IsNullOrWhiteSpace(value.PreferredServiceDefinitionId)), Is.True);
        }

        [Test]
        public void RecreatedCoordinatorGeneratesIdsThatDoNotCollideWithPersistedJourneys()
        {
            Fixture fixture = CreateFixture();
            EntityLocationReferenceData traveler = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            WorldTravelResult outbound = new WorldTravelCoordinator(fixture.Registry, fixture.EntityLocations, fixture.Routes, fixture.Journeys, null)
                .StartTravel(new WorldTravelRequest
                {
                    Traveler = traveler,
                    DestinationLocationId = "location.prototype.market-district",
                    WorldTime = 10d
                });
            Assert.That(outbound.Succeeded, Is.True, outbound.Message);
            new WorldTravelCoordinator(fixture.Registry, fixture.EntityLocations, fixture.Routes, fixture.Journeys, null).AdvanceActiveJourneys(100d);

            WorldTravelResult inbound = new WorldTravelCoordinator(fixture.Registry, fixture.EntityLocations, fixture.Routes, fixture.Journeys, null)
                .StartTravel(new WorldTravelRequest
                {
                    Traveler = traveler,
                    DestinationLocationId = "location.prototype.town",
                    WorldTime = 110d
                });

            Assert.That(inbound.Succeeded, Is.True, inbound.Message);
            Assert.That(inbound.Journey.JourneyId, Is.Not.EqualTo(outbound.Journey.JourneyId));
        }

        [Test]
        public void MissingLegalRequirementIntegrationFailsClosed()
        {
            InteractionServiceDefinition service = ScriptableObject.CreateInstance<InteractionServiceDefinition>();
            try
            {
                service.DevelopmentConfigure(
                    "interaction-service.test.legal",
                    "Legal Requirement Test",
                    InteractionServiceCategory.GovernmentService,
                    new[] { PrototypeInteractionPointDefinitionFactory.MayorDeskDefinitionId },
                    InteractionDestinationRuntime.Legal,
                    legalRequirements: new[] { "legal-requirement.test.permit" });
                InteractionRequirementResolution resolution = new PrototypeInteractionRequirementResolver(null, null).Evaluate(new InteractionRequirementContext
                {
                    Service = service,
                    Provider = PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.GuildMasterPersonId),
                    Consumer = PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.PlayerPersonId)
                });

                Assert.That(resolution.Satisfied, Is.False);
                Assert.That(resolution.UnmetRequirementIds, Does.Contain("legal.legal-requirement.test.permit"));
            }
            finally
            {
                Object.DestroyImmediate(service);
            }
        }

        private static Fixture CreateFixture()
        {
            DefinitionRegistry registry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(null);
            registry = PrototypeInteractionPointDefinitionFactory.AddMissingPrototypeInteractionDefinitions(registry);
            registry = PrototypeLocationConnectionDefinitionFactory.AddMissingPrototypeConnectionDefinitions(registry);
            registry = PrototypeLocationRouteDefinitionFactory.AddMissingPrototypeRouteDefinitions(registry);
            LocationRuntime locations = new LocationRuntime();
            PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locations, registry, PersistenceService.LocalWorldId);
            EntityLocationRuntime entityLocations = new EntityLocationRuntime();
            PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocations, locations, PersistenceService.LocalWorldId);
            InteractionPointRuntime interactions = new InteractionPointRuntime();
            PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(interactions, registry, locations, entityLocations, PersistenceService.LocalWorldId);
            LocationConnectionRuntime connections = new LocationConnectionRuntime();
            PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(connections, registry, locations, entityLocations, interactions, PersistenceService.LocalWorldId);
            LocationRouteRuntime routes = new LocationRouteRuntime();
            PrototypeLocationRouteDefinitionFactory.SeedPrototypeRoutes(routes, registry, locations, connections, PersistenceService.LocalWorldId);
            TravelJourneyRuntime journeys = new TravelJourneyRuntime();
            journeys.Configure(registry, locations, entityLocations, connections, routes, PersistenceService.LocalWorldId);
            return new Fixture(registry, locations, entityLocations, routes, journeys, PersistenceService.LocalWorldId);
        }

        private sealed class Fixture
        {
            public Fixture(DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, LocationRouteRuntime routes, TravelJourneyRuntime journeys, string worldId)
            {
                Registry = registry;
                Locations = locations;
                EntityLocations = entityLocations;
                Routes = routes;
                Journeys = journeys;
                WorldId = worldId;
            }

            public DefinitionRegistry Registry { get; }
            public LocationRuntime Locations { get; }
            public EntityLocationRuntime EntityLocations { get; }
            public LocationRouteRuntime Routes { get; }
            public TravelJourneyRuntime Journeys { get; }
            public string WorldId { get; }
        }
    }
}
