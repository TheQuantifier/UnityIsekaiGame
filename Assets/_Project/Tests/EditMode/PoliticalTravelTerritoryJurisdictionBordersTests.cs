using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Tests
{
    public sealed class PoliticalTravelTerritoryJurisdictionBordersTests
    {
        private const string TravelerId = "person.prototype.player";
        private const string OriginLocationId = "location.prototype.village";
        private const string DestinationLocationId = "location.prototype.market-district";
        private const string OriginTerritoryId = "political-territory.test.origin";
        private const string DestinationTerritoryId = "political-territory.test.destination";
        private const string OriginGovernmentId = "government.test.origin";
        private const string DestinationGovernmentId = "government.test.destination";
        private const string DestinationJurisdictionId = "jurisdiction.test.destination.border";

        [Test]
        public void TerritoryCrossingUsesStep13OwnersWithoutMutatingThem()
        {
            Fixture fixture = Fixture.Create();
            long governmentRevision = fixture.Governments.Revision;
            long legalRevision = fixture.Laws.Revision;
            long crimeRevision = fixture.Crimes.Revision;

            PoliticalTravelEvaluationResult result = fixture.PoliticalTravel.EvaluateCrossing(fixture.Evaluation(TravelLegalComplianceMode.RequireLegalTravel));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Classification, Is.EqualTo(PoliticalTravelCrossingClassification.BorderCrossing));
            Assert.That(result.OriginTerritory.TerritoryId, Is.EqualTo(OriginTerritoryId));
            Assert.That(result.DestinationTerritory.TerritoryId, Is.EqualTo(DestinationTerritoryId));
            Assert.That(result.DestinationJurisdiction.SelectedJurisdiction.jurisdictionId, Is.EqualTo(DestinationJurisdictionId));
            Assert.That(fixture.Governments.Revision, Is.EqualTo(governmentRevision));
            Assert.That(fixture.Laws.Revision, Is.EqualTo(legalRevision));
            Assert.That(fixture.Crimes.Revision, Is.EqualTo(crimeRevision));
        }

        [Test]
        public void LegalComplianceModesKeepPhysicalAndLegalTravelSeparate()
        {
            Fixture fixture = Fixture.Create();
            fixture.EnactTravelLaw("entry-ban", PoliticalTravelRuntime.CrossBorderActionId, LegalEffectCategory.Prohibition);

            PoliticalTravelOperationResult blocked = fixture.PoliticalTravel.RecordCrossing(fixture.Crossing("blocked", TravelLegalComplianceMode.RequireLegalTravel));
            Assert.That(blocked.Code, Is.EqualTo(PoliticalTravelOperationCode.LegalBlocked));
            Assert.That(fixture.PoliticalTravel.CrossingCount, Is.EqualTo(0));

            PoliticalTravelOperationResult illegal = fixture.PoliticalTravel.RecordCrossing(fixture.Crossing("illegal", TravelLegalComplianceMode.AllowIllegalTravel));
            PoliticalTravelEvaluationResult physicallyBlocked = fixture.PoliticalTravel.EvaluateCrossing(fixture.Evaluation(TravelLegalComplianceMode.AllowIllegalTravel, physicalTravelPossible: false));

            Assert.That(illegal.Succeeded, Is.True, illegal.Message);
            Assert.That(fixture.PoliticalTravel.CrossingCount, Is.EqualTo(1));
            Assert.That(illegal.Crossing.illegalCrossing, Is.True);
            Assert.That(illegal.Crossing.combinedState, Is.EqualTo(PhysicalLegalTravelState.IllegalButPhysicallyPossible));
            Assert.That(physicallyBlocked.Succeeded, Is.False);
            Assert.That(physicallyBlocked.CombinedState, Is.EqualTo(PhysicalLegalTravelState.PhysicallyBlocked));
        }

        [Test]
        public void CheckpointAuthorizationGatesCrossingWithoutChangingRouteOrLaw()
        {
            Fixture fixture = Fixture.Create();
            long routeRevision = fixture.Routes.Revision;
            long legalRevision = fixture.Laws.Revision;
            PoliticalTravelOperationResult checkpoint = fixture.PoliticalTravel.CreateCheckpoint(fixture.Checkpoint(BorderCheckpointPolicy.RequireAuthorization));

            PoliticalTravelOperationResult denied = fixture.PoliticalTravel.RecordCrossing(fixture.Crossing("no-permit", TravelLegalComplianceMode.RequireLegalTravel));
            PoliticalTravelOperationResult grant = fixture.PoliticalTravel.GrantAuthorization(new TravelCrossingAuthorizationRequest
            {
                transactionId = "tx.travel.auth",
                authorizationId = "travel-authorization.test.destination",
                travelerPersonId = TravelerId,
                checkpointId = checkpoint.Checkpoint.CheckpointId,
                territoryId = DestinationTerritoryId,
                authorizedActionIds = new[] { PoliticalTravelRuntime.PassCheckpointActionId },
                effectiveWorldTime = 0d
            });
            PoliticalTravelOperationResult allowed = fixture.PoliticalTravel.RecordCrossing(fixture.Crossing("permit", TravelLegalComplianceMode.RequireLegalTravel));

            Assert.That(checkpoint.Succeeded, Is.True, checkpoint.Message);
            Assert.That(denied.Code, Is.EqualTo(PoliticalTravelOperationCode.LegalBlocked));
            Assert.That(grant.Succeeded, Is.True, grant.Message);
            Assert.That(allowed.Succeeded, Is.True, allowed.Message);
            Assert.That(allowed.Crossing.authorizationId, Is.EqualTo("travel-authorization.test.destination"));
            Assert.That(fixture.Routes.Revision, Is.EqualTo(routeRevision));
            Assert.That(fixture.Laws.Revision, Is.EqualTo(legalRevision));
        }

        [Test]
        public void WantedAndWarrantVisibilityDoesNotLeakHiddenIdentifiers()
        {
            Fixture fixture = Fixture.Create();
            CrimeOperationResult wanted = fixture.Crimes.CreateWantedStatus(new WantedStatusRequest
            {
                transactionId = "tx.wanted.hidden",
                wantedStatusId = "wanted-status.test.hidden",
                wantedDefinitionId = PrototypeCrimeDefinitionFactory.WantedForArrestDefinitionId,
                subjectId = TravelerId,
                territoryId = DestinationTerritoryId,
                jurisdictionId = DestinationJurisdictionId,
                placeIds = new[] { DestinationLocationId },
                activeWorldTime = 0d,
                visibility = PoliticalVisibility.Hidden
            });

            PoliticalTravelEvaluationResult travelerSafe = fixture.PoliticalTravel.EvaluateCrossing(fixture.Evaluation(TravelLegalComplianceMode.RequireLegalTravel, PoliticalTravelVisibilityMode.TravelerSafe));
            PoliticalTravelEvaluationResult privileged = fixture.PoliticalTravel.EvaluateCrossing(fixture.Evaluation(TravelLegalComplianceMode.RequireLegalTravel, PoliticalTravelVisibilityMode.Privileged));

            Assert.That(wanted.Succeeded, Is.True, wanted.Message);
            Assert.That(travelerSafe.Wanted.VisibleWantedStatusIds, Is.Empty);
            Assert.That(travelerSafe.Wanted.HiddenRestrictedInformation, Is.True);
            Assert.That(privileged.Wanted.VisibleWantedStatusIds, Does.Contain("wanted-status.test.hidden"));
            Assert.That(privileged.EnforcementOpportunity, Is.True);
        }

        [Test]
        public void RouteRequirementsAndPersistencePreservePoliticalTravelGraph()
        {
            Fixture fixture = Fixture.Create();
            PoliticalTravelOperationResult checkpoint = fixture.PoliticalTravel.CreateCheckpoint(fixture.Checkpoint(BorderCheckpointPolicy.RequireInspection));
            LocationRouteSearchResult plan = fixture.Routes.PlanRoute(new LocationRouteSearchRequest
            {
                traveler = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId),
                originLocationId = OriginLocationId,
                destinationLocationId = DestinationLocationId,
                travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId,
                worldTime = 5d
            });
            RouteRequirementSummary requirements = fixture.PoliticalTravel.BuildPoliticalRouteRequirements(plan.Plan, fixture.Evaluation(TravelLegalComplianceMode.RequireLegalTravel));
            PoliticalTravelRuntimeSaveData save = fixture.PoliticalTravel.CreateSaveData();
            PoliticalTravelRuntime restored = new PoliticalTravelRuntime();
            restored.Configure(fixture.Registry, fixture.Governments, fixture.Laws, fixture.Crimes, fixture.Justice, fixture.Locations, fixture.Routes, fixture.WorldId);
            PoliticalTravelOperationResult restore = restored.RestoreFromSaveData(save, fixture.Governments, fixture.Laws, fixture.Crimes, fixture.Locations, fixture.Routes, fixture.WorldId);

            Assert.That(checkpoint.Succeeded, Is.True, checkpoint.Message);
            Assert.That(plan.Succeeded, Is.True, plan.Message);
            Assert.That(requirements.requiredLegalTravelActions, Does.Contain(PoliticalTravelRuntime.CrossBorderActionId));
            Assert.That(requirements.requiredCheckpointIds, Does.Contain(checkpoint.Checkpoint.CheckpointId));
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.CheckpointCount, Is.EqualTo(1));
        }

        [Test]
        public void PersistenceRejectsCorruptPoliticalTravelGraphWithoutMutation()
        {
            Fixture fixture = Fixture.Create();
            PoliticalTravelOperationResult checkpoint = fixture.PoliticalTravel.CreateCheckpoint(fixture.Checkpoint(BorderCheckpointPolicy.ObserveOnly));
            PoliticalTravelPersistenceParticipant participant = new PoliticalTravelPersistenceParticipant(
                fixture.PoliticalTravel,
                () => fixture.Governments,
                () => fixture.Laws,
                () => fixture.Crimes,
                () => fixture.Locations,
                () => fixture.Routes,
                fixture.WorldId);
            PoliticalTravelRuntimeSaveData corrupt = fixture.PoliticalTravel.CreateSaveData();
            corrupt.checkpoints[0].destinationTerritoryId = "political-territory.missing";
            long before = fixture.PoliticalTravel.Revision;

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), PoliticalTravelPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(checkpoint.Succeeded, Is.True, checkpoint.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.PoliticalTravel.Revision, Is.EqualTo(before));
            Assert.That(fixture.PoliticalTravel.TryGetCheckpoint(checkpoint.Checkpoint.CheckpointId, out BorderCheckpointRecordData current), Is.True);
            Assert.That(current.destinationTerritoryId, Is.EqualTo(DestinationTerritoryId));
        }

        private sealed class Fixture
        {
            private Fixture(
                DefinitionRegistry registry,
                OrganizationRuntime organizations,
                OrganizationMembershipRuntime memberships,
                OrganizationAuthorityRuntime authority,
                OrganizationResourceRuntime resources,
                OrganizationDecisionRuntime decisions,
                FactionRuntime factions,
                DiplomacyRuntime diplomacy,
                GovernmentRuntime governments,
                LegalRuntime laws,
                CrimeRuntime crimes,
                JusticeRuntime justice,
                LocationRuntime locations,
                EntityLocationRuntime entityLocations,
                InteractionPointRuntime interactions,
                LocationConnectionRuntime connections,
                LocationRouteRuntime routes,
                PoliticalTravelRuntime politicalTravel,
                string worldId)
            {
                Registry = registry;
                Organizations = organizations;
                Memberships = memberships;
                Authority = authority;
                Resources = resources;
                Decisions = decisions;
                Factions = factions;
                Diplomacy = diplomacy;
                Governments = governments;
                Laws = laws;
                Crimes = crimes;
                Justice = justice;
                Locations = locations;
                EntityLocations = entityLocations;
                Interactions = interactions;
                Connections = connections;
                Routes = routes;
                PoliticalTravel = politicalTravel;
                WorldId = worldId;
            }

            public DefinitionRegistry Registry { get; }
            public OrganizationRuntime Organizations { get; }
            public OrganizationMembershipRuntime Memberships { get; }
            public OrganizationAuthorityRuntime Authority { get; }
            public OrganizationResourceRuntime Resources { get; }
            public OrganizationDecisionRuntime Decisions { get; }
            public FactionRuntime Factions { get; }
            public DiplomacyRuntime Diplomacy { get; }
            public GovernmentRuntime Governments { get; }
            public LegalRuntime Laws { get; }
            public CrimeRuntime Crimes { get; }
            public JusticeRuntime Justice { get; }
            public LocationRuntime Locations { get; }
            public EntityLocationRuntime EntityLocations { get; }
            public InteractionPointRuntime Interactions { get; }
            public LocationConnectionRuntime Connections { get; }
            public LocationRouteRuntime Routes { get; }
            public PoliticalTravelRuntime PoliticalTravel { get; }
            public string WorldId { get; }

            public static Fixture Create()
            {
                string worldId = PersistenceService.LocalWorldId;
                DefinitionRegistry registry = CreateRegistry();
                LocationRuntime locations = new LocationRuntime();
                PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locations, registry, worldId);
                locations.Configure(registry, worldId);
                EntityLocationRuntime entityLocations = new EntityLocationRuntime();
                PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocations, locations, worldId);
                InteractionPointRuntime interactions = new InteractionPointRuntime();
                PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(interactions, registry, locations, entityLocations, worldId);
                LocationConnectionRuntime connections = new LocationConnectionRuntime();
                PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(connections, registry, locations, entityLocations, interactions, worldId);
                LocationRouteRuntime routes = new LocationRouteRuntime();
                PrototypeLocationRouteDefinitionFactory.SeedPrototypeRoutes(routes, registry, locations, connections, worldId);
                OrganizationRuntime organizations = new OrganizationRuntime();
                PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(organizations, registry, worldId);
                organizations.Configure(registry, worldId, new[] { TravelerId }, Array.Empty<string>());
                OrganizationMembershipRuntime memberships = new OrganizationMembershipRuntime();
                memberships.Configure(registry, organizations, worldId, new[] { TravelerId }, PrototypeOrganizationDefinitionFactory.PrototypeOrganizationIds);
                OrganizationAuthorityRuntime authority = new OrganizationAuthorityRuntime();
                authority.Configure(registry, organizations, memberships, worldId, new[] { TravelerId }, PrototypeOrganizationDefinitionFactory.PrototypeOrganizationIds);
                OrganizationResourceRuntime resources = new OrganizationResourceRuntime();
                resources.Configure(registry, organizations, authority, null, worldId);
                OrganizationDecisionRuntime decisions = new OrganizationDecisionRuntime();
                decisions.Configure(registry, organizations, memberships, authority, resources, worldId, new[] { TravelerId }, null);
                FactionRuntime factions = new FactionRuntime();
                factions.Configure(registry, organizations, memberships, authority, resources, decisions, worldId, new[] { TravelerId });
                DiplomacyRuntime diplomacy = new DiplomacyRuntime();
                diplomacy.Configure(registry, organizations, factions, authority, decisions, resources, worldId, new[] { TravelerId });
                GovernmentRuntime governments = new GovernmentRuntime();
                governments.Configure(registry, organizations, memberships, authority, decisions, resources, factions, diplomacy, null, worldId, new[] { TravelerId }, Array.Empty<string>());
                CreatePoliticalGraph(governments);
                LegalRuntime laws = new LegalRuntime();
                laws.Configure(registry, governments, organizations, authority, decisions, diplomacy, null, worldId, new[] { TravelerId }, Array.Empty<string>());
                CrimeRuntime crimes = new CrimeRuntime();
                crimes.Configure(registry, governments, laws, authority, diplomacy, worldId, new[] { TravelerId }, Array.Empty<string>());
                JusticeRuntime justice = new JusticeRuntime();
                justice.Configure(registry, governments, laws, organizations, authority, crimes, worldId, new[] { TravelerId }, Array.Empty<string>());
                PoliticalTravelRuntime politicalTravel = new PoliticalTravelRuntime();
                politicalTravel.Configure(registry, governments, laws, crimes, justice, locations, routes, worldId);
                return new Fixture(registry, organizations, memberships, authority, resources, decisions, factions, diplomacy, governments, laws, crimes, justice, locations, entityLocations, interactions, connections, routes, politicalTravel, worldId);
            }

            public PoliticalTravelEvaluationRequest Evaluation(TravelLegalComplianceMode mode, PoliticalTravelVisibilityMode visibility = PoliticalTravelVisibilityMode.Privileged, bool physicalTravelPossible = true)
            {
                return new PoliticalTravelEvaluationRequest
                {
                    travelerPersonId = TravelerId,
                    originLocationId = OriginLocationId,
                    destinationLocationId = DestinationLocationId,
                    routeSegmentId = PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId,
                    physicalTravelPossible = physicalTravelPossible,
                    legalComplianceMode = mode,
                    visibilityMode = visibility,
                    worldTime = 20d
                };
            }

            public PoliticalTravelCrossingRequest Crossing(string suffix, TravelLegalComplianceMode mode)
            {
                return new PoliticalTravelCrossingRequest
                {
                    transactionId = $"tx.travel.crossing.{suffix}",
                    crossingId = $"political-travel-crossing.test.{suffix}",
                    travelerPersonId = TravelerId,
                    originLocationId = OriginLocationId,
                    destinationLocationId = DestinationLocationId,
                    routeSegmentId = PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId,
                    physicalTravelPossible = true,
                    legalComplianceMode = mode,
                    visibilityMode = PoliticalTravelVisibilityMode.Privileged,
                    worldTime = 20d
                };
            }

            public BorderCheckpointCreateRequest Checkpoint(BorderCheckpointPolicy policy)
            {
                return new BorderCheckpointCreateRequest
                {
                    transactionId = $"tx.checkpoint.{policy}",
                    checkpointId = "border-checkpoint.test.market-gate",
                    displayName = "Market Gate",
                    routeSegmentId = PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId,
                    sourceTerritoryId = OriginTerritoryId,
                    destinationTerritoryId = DestinationTerritoryId,
                    governingGovernmentId = DestinationGovernmentId,
                    jurisdictionId = DestinationJurisdictionId,
                    policy = policy,
                    lifecycleState = BorderCheckpointLifecycleState.Active,
                    worldTime = 0d
                };
            }

            public void EnactTravelLaw(string suffix, string actionId, LegalEffectCategory effect)
            {
                string provisionDefinitionId = effect == LegalEffectCategory.Prohibition ? PrototypeLegalDefinitionFactory.ProhibitionProvisionId : PrototypeLegalDefinitionFactory.PermissionProvisionId;
                EnactLegalInstrumentRequest request = new EnactLegalInstrumentRequest
                {
                    transactionId = $"tx.law.{suffix}",
                    instrumentId = $"legal-instrument.test.{suffix}",
                    instrumentDefinitionId = PrototypeLegalDefinitionFactory.CentralStatuteId,
                    authorityDefinitionId = PrototypeLegalDefinitionFactory.SovereignAuthorityId,
                    title = "Travel Law",
                    governmentId = DestinationGovernmentId,
                    organizationId = "organization.prototype.adventurers-guild",
                    jurisdictionIds = new[] { DestinationJurisdictionId },
                    enactmentWorldTime = 1d,
                    publicationWorldTime = 1d,
                    effectiveWorldTime = 1d,
                    published = true,
                    trustedSystemOperation = true,
                    provisions = new[]
                    {
                        new LegalProvisionCreateRequest
                        {
                            provisionId = $"legal-provision.test.{suffix}",
                            provisionDefinitionId = provisionDefinitionId,
                            version = new LegalProvisionVersionData
                            {
                                actionId = actionId,
                                territoryIds = new[] { DestinationTerritoryId },
                                effectiveWorldTime = 1d
                            }
                        }
                    }
                };
                Assert.That(Laws.Enact(request).Succeeded, Is.True);
            }
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionRegistry registry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            registry = PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(registry);
            registry = PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(registry);
            registry = PrototypeOrganizationAuthorityDefinitionFactory.AddMissingPrototypeOrganizationAuthorityDefinitions(registry);
            registry = PrototypeOrganizationResourceDefinitionFactory.AddMissingPrototypeOrganizationResourceDefinitions(registry);
            registry = PrototypeOrganizationDecisionDefinitionFactory.AddMissingPrototypeOrganizationDecisionDefinitions(registry);
            registry = PrototypeFactionDefinitionFactory.AddMissingPrototypeFactionDefinitions(registry);
            registry = PrototypeDiplomacyDefinitionFactory.AddMissingPrototypeDiplomacyDefinitions(registry);
            registry = PrototypeGovernmentDefinitionFactory.AddMissingPrototypeGovernmentDefinitions(registry);
            registry = PrototypeLegalDefinitionFactory.AddMissingPrototypeLegalDefinitions(registry);
            registry = PrototypeCrimeDefinitionFactory.AddMissingPrototypeCrimeDefinitions(registry);
            registry = PrototypeJusticeDefinitionFactory.AddMissingPrototypeJusticeDefinitions(registry);
            registry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(registry);
            registry = PrototypeInteractionPointDefinitionFactory.AddMissingPrototypeInteractionDefinitions(registry);
            registry = PrototypeLocationConnectionDefinitionFactory.AddMissingPrototypeConnectionDefinitions(registry);
            return PrototypeLocationRouteDefinitionFactory.AddMissingPrototypeRouteDefinitions(registry);
        }

        private static void CreatePoliticalGraph(GovernmentRuntime governments)
        {
            Assert.That(governments.CreatePolity(new PolityCreateRequest { transactionId = "tx.polity.origin", polityId = "polity.test.origin", polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Origin Realm", worldTime = 0d }).Succeeded, Is.True);
            Assert.That(governments.CreatePolity(new PolityCreateRequest { transactionId = "tx.polity.destination", polityId = "polity.test.destination", polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Destination Realm", worldTime = 0d }).Succeeded, Is.True);
            Assert.That(governments.RegisterGovernment(new GovernmentRegisterRequest { transactionId = "tx.government.origin", governmentId = OriginGovernmentId, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, polityId = "polity.test.origin", officialName = "Origin Government", primaryGoverningOrganizationId = "organization.prototype.adventurers-guild", governingOrganizationIds = new[] { "organization.prototype.adventurers-guild" }, level = GovernmentLevel.Central, worldTime = 0d }).Succeeded, Is.True);
            Assert.That(governments.RegisterGovernment(new GovernmentRegisterRequest { transactionId = "tx.government.destination", governmentId = DestinationGovernmentId, governmentDefinitionId = PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, polityId = "polity.test.destination", officialName = "Destination Government", primaryGoverningOrganizationId = "organization.prototype.adventurers-guild", governingOrganizationIds = new[] { "organization.prototype.adventurers-guild" }, level = GovernmentLevel.Central, worldTime = 0d }).Succeeded, Is.True);
            Assert.That(governments.CreateTerritory(new TerritoryCreateRequest { transactionId = "tx.territory.origin", territoryId = OriginTerritoryId, territoryDefinitionId = PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId, displayName = "Origin Territory", polityId = "polity.test.origin", primaryGovernmentId = OriginGovernmentId, placeIds = new[] { OriginLocationId }, worldTime = 0d }).Succeeded, Is.True);
            Assert.That(governments.CreateTerritory(new TerritoryCreateRequest { transactionId = "tx.territory.destination", territoryId = DestinationTerritoryId, territoryDefinitionId = PrototypeGovernmentDefinitionFactory.RealmTerritoryDefinitionId, displayName = "Destination Territory", polityId = "polity.test.destination", primaryGovernmentId = DestinationGovernmentId, placeIds = new[] { DestinationLocationId }, worldTime = 0d }).Succeeded, Is.True);
            Assert.That(governments.CreateJurisdiction(new JurisdictionCreateRequest { transactionId = "tx.jurisdiction.origin", jurisdictionId = "jurisdiction.test.origin.border", jurisdictionDefinitionId = PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId, governmentId = OriginGovernmentId, category = JurisdictionCategory.GeneralGovernment, scopeDimensions = JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.SubjectMatter, subjectMatters = new[] { JurisdictionSubjectMatter.BorderAdministration }, territoryIds = new[] { OriginTerritoryId }, priority = 100, worldTime = 0d }).Succeeded, Is.True);
            Assert.That(governments.CreateJurisdiction(new JurisdictionCreateRequest { transactionId = "tx.jurisdiction.destination", jurisdictionId = DestinationJurisdictionId, jurisdictionDefinitionId = PrototypeGovernmentDefinitionFactory.GeneralJurisdictionDefinitionId, governmentId = DestinationGovernmentId, category = JurisdictionCategory.GeneralGovernment, scopeDimensions = JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.SubjectMatter, subjectMatters = new[] { JurisdictionSubjectMatter.BorderAdministration }, territoryIds = new[] { DestinationTerritoryId }, priority = 100, worldTime = 0d }).Succeeded, Is.True);
        }
    }
}
