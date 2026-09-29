using System;
using System.Linq;
using NUnit.Framework;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.Economy.Integration;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Economy.RegionalFlow;
using UnityIsekaiGame.Economy.Trading;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class EconomyIntegrationFinalizationTests
    {
        [Test]
        public void AuthorityMap_HasExactlyOneOwnerPerEconomicDomain()
        {
            var map = EconomyIntegrationFacade.CreateAuthorityMap();
            var result = new EconomyIntegrationFacade(new DefinitionRegistry(Array.Empty<IGameDefinition>())).ValidateAuthorityMap(map);

            Assert.That(result.Succeeded, Is.True, result.Summary);
            Assert.That(map.Select(entry => entry.domainId).Distinct().Count(), Is.EqualTo(map.Count));
            Assert.That(map.Single(entry => entry.domainId == EconomicDomainAuthorityId.CurrencyTransactions).authoritativeRuntime, Is.EqualTo(nameof(EconomyRuntime)));
            Assert.That(map.Single(entry => entry.domainId == EconomicDomainAuthorityId.RegionalFlow).authoritativeRuntime, Is.EqualTo(nameof(RegionalFlowRuntime)));
        }

        [Test]
        public void AuthorityMap_DetectsDuplicateDomainOwners()
        {
            var duplicate = EconomyIntegrationFacade.CreateAuthorityMap().Select(entry => entry.Clone()).ToList();
            duplicate.Add(duplicate[0].Clone());

            var result = new EconomyIntegrationFacade(new DefinitionRegistry(Array.Empty<IGameDefinition>())).ValidateAuthorityMap(duplicate);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.code == EconomicIntegrationDiagnosticCode.DuplicateAuthority), Is.True);
        }

        [Test]
        public void Readiness_IsHostlessWhenAllStep11RuntimesAreAvailable()
        {
            var fixture = CreateFixture();

            EconomicReadinessSnapshot readiness = fixture.Facade.EvaluateReadiness(sceneHostAvailable: false, sceneHostRequired: false);

            Assert.That(readiness.Ready, Is.True, string.Join(Environment.NewLine, readiness.Diagnostics.Select(item => item.message)));
            Assert.That(readiness.RuntimeSummaries.Count, Is.EqualTo(9));
            Assert.That(readiness.RuntimeSummaries.All(summary => summary.present), Is.True);
        }

        [Test]
        public void Readiness_ReportsMissingRuntimeWithoutSubstitutingCopiedState()
        {
            var facade = new EconomyIntegrationFacade(new DefinitionRegistry(Array.Empty<IGameDefinition>()), economy: new EconomyRuntime(), access: new InformationAccessRuntime());

            EconomicReadinessSnapshot readiness = facade.EvaluateReadiness();

            Assert.That(readiness.Ready, Is.False);
            Assert.That(readiness.Diagnostics.Count(diagnostic => diagnostic.code == EconomicIntegrationDiagnosticCode.MissingRuntime), Is.GreaterThan(0));
            Assert.That(readiness.Diagnostics.Any(diagnostic => diagnostic.owningRuntime == nameof(MarketRuntime)), Is.True);
        }

        [Test]
        public void Snapshot_IsImmutableAndReflectsOnlyAuthoritativeRuntimeChanges()
        {
            var fixture = CreateFixture();
            var before = fixture.Facade.CreateSnapshot();

            CurrencyDefinition gold = UnityEngine.ScriptableObject.CreateInstance<CurrencyDefinition>();
            gold.Initialize("currency.integration-test", "Integration Test Gold", "G");
            DefinitionRegistry extended = new DefinitionRegistry(fixture.Registry.DefinitionsById.Values.Concat(new IGameDefinition[] { gold }));
            fixture.Economy.Configure(extended, "world.integration-test");
            fixture.Economy.CreateAccount("account.integration-test", gold, "person.integration-test", EconomyAccountKind.PersonWallet, 10L, "tx.integration-test.open");

            var after = fixture.Facade.CreateSnapshot();

            Assert.That(before.RuntimeSummaries.Single(summary => summary.runtimeName == nameof(EconomyRuntime)).primaryRecordCount, Is.Zero);
            Assert.That(after.RuntimeSummaries.Single(summary => summary.runtimeName == nameof(EconomyRuntime)).primaryRecordCount, Is.EqualTo(1));
            Assert.That(before.Fingerprint, Is.Not.EqualTo(after.Fingerprint));
        }

        [Test]
        public void GraphValidationAndConservationUseAuthoritativeSaveContracts()
        {
            var fixture = CreateFixture();

            EconomicValidationResult graph = fixture.Facade.ValidateEconomicGraph();
            EconomicConservationAuditResult conservation = fixture.Facade.AuditExactArithmeticAndConservation();

            Assert.That(graph.Succeeded, Is.True, graph.Summary);
            Assert.That(conservation.succeeded, Is.True, conservation.message);
            Assert.That(conservation.monetaryLedgerNet, Is.Zero);
            Assert.That(conservation.checkedRuntimeCount, Is.EqualTo(9));
        }

        [Test]
        public void PersistenceDependencyMap_UsesStep11ParticipantKeysWithoutRequiredCycles()
        {
            var fixture = CreateFixture();

            EconomicPersistenceDependencyMapResult result = fixture.Facade.BuildPersistenceDependencyMap();

            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.message)));
            Assert.That(result.Participants.Select(item => item.participantKey), Is.EqualTo(new[]
            {
                EconomyPersistenceParticipant.Key,
                MarketPersistenceParticipant.Key,
                TradePersistenceParticipant.Key,
                PayrollPersistenceParticipant.Key,
                BusinessPersistenceParticipant.Key,
                PropertyPersistenceParticipant.Key,
                ContractEconomyPersistenceParticipant.Key,
                InstitutionalRevenuePersistenceParticipant.Key,
                RegionalFlowPersistenceParticipant.Key
            }));
        }

        [Test]
        public void Step12Signals_AreImmutableMutationFreeContracts()
        {
            var fixture = CreateFixture();

            EconomicSignalContractData signal = fixture.Facade.CreateStep12SignalContract("signal.integration.labor", EconomicSignalCategory.LaborPressure, "region.prototype", 42L, 100d);
            EconomicIntegrationSnapshot snapshot = fixture.Facade.CreateSnapshot(signal);
            signal.exactValue = 99L;
            signal.dependencyRevisions = Array.Empty<long>();

            EconomicSignalContractData captured = snapshot.Signals.Single();
            Assert.That(captured.step12Ready, Is.True);
            Assert.That(captured.mutationFree, Is.True);
            Assert.That(captured.exactValue, Is.EqualTo(42L));
            Assert.That(captured.dependencyRevisions.Length, Is.EqualTo(9));
        }

        [Test]
        public void ValidateAll_ComposesReadinessAuthorityGraphPersistenceAndAccess()
        {
            var fixture = CreateFixture();

            EconomicValidationResult result = fixture.Facade.ValidateAll();

            Assert.That(result.Succeeded, Is.True, result.Summary);
        }

        private static Fixture CreateFixture()
        {
            var registry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            var economy = new EconomyRuntime();
            var markets = new MarketRuntime();
            var trades = new TradeRuntime();
            var payroll = new PayrollRuntime();
            var businesses = new BusinessRuntime();
            var properties = new PropertyRuntime();
            var contracts = new ContractEconomyRuntime();
            var revenue = new InstitutionalRevenueRuntime();
            var regional = new RegionalFlowRuntime();
            var access = new InformationAccessRuntime();
            const string world = "world.integration-test";

            economy.Configure(registry, world);
            markets.Configure(registry, world);
            trades.Configure(registry, world);
            payroll.Configure(registry, world);
            businesses.Configure(registry, world);
            properties.Configure(registry, world);
            contracts.Configure(registry, world);
            revenue.Configure(registry, world);
            regional.Configure(registry, world);
            access.Configure(registry, world);

            var facade = new EconomyIntegrationFacade(registry, economy, markets, trades, payroll, businesses, properties, contracts, revenue, regional, access, world);
            return new Fixture(registry, economy, facade);
        }

        private sealed class Fixture
        {
            public Fixture(DefinitionRegistry registry, EconomyRuntime economy, EconomyIntegrationFacade facade)
            {
                Registry = registry;
                Economy = economy;
                Facade = facade;
            }

            public DefinitionRegistry Registry { get; }
            public EconomyRuntime Economy { get; }
            public EconomyIntegrationFacade Facade { get; }
        }
    }
}
