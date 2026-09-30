#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
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
    public sealed class Step12SocialSimulationIntegrationFinalizationTests
    {
        private const string CatalogPath = "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset";

        private static readonly string[] KnownPersons =
        {
            PersistenceService.LocalPlayerId,
            "person.prototype.friend",
            "person.prototype.rival",
            "person.prototype.mentor",
            "person.prototype.student",
            "person.prototype.listener",
            "person.prototype.parent",
            "person.prototype.child",
            "person.prototype.partner"
        };

        private static readonly string[] AdultPersons = KnownPersons
            .Where(id => !id.Contains(".child", StringComparison.Ordinal))
            .ToArray();

        [Test]
        public void IntegrationReadinessAuthorityAndPersistenceGraphAreClean()
        {
            using TestFixture fixture = CreateFixture();
            Step12IntegrationValidationReport report = fixture.Facade.ValidateComplete();
            Step12HealthSnapshot health = fixture.Facade.CreateHealthSnapshot();

            Assert.That(report.Succeeded, Is.True, string.Join(Environment.NewLine, report.Diagnostics));
            Assert.That(health.Status, Is.EqualTo(Step12HealthStatus.Ready), string.Join(Environment.NewLine, health.Diagnostics));
            Assert.That(fixture.Facade.AuthorityMap.Select(item => item.DomainId), Is.Unique);
            Assert.That(fixture.Facade.AuthorityMap.Single(item => item.DomainId == "social-graph").Derived, Is.True);
            Assert.That(fixture.Facade.AuthorityMap.Single(item => item.DomainId == "relationships").AuthoritativeRuntime, Is.EqualTo(nameof(RelationshipRuntime)));
            Assert.That(fixture.Facade.PersistenceDependencies.Select(item => item.ParticipantKey), Does.Contain(RelationshipPersistenceParticipant.Key));
            Assert.That(fixture.Facade.PersistenceDependencies.Single(item => item.ParticipantKey == SocialInteractionPersistenceParticipant.Key).DependsOn, Does.Contain(RumorPersistenceParticipant.Key));
        }

        [Test]
        public void ContextSnapshotsAreBoundedImmutableAndDeterministic()
        {
            using TestFixture fixture = CreateFixture();
            SeedContext(fixture);
            Step12SocialContextOptions options = new Step12SocialContextOptions { MaxRelationships = 1, MaxAttitudes = 4, MaxInteractions = 4, MaxHouseholds = 4 };

            Step12SocialContextSnapshot first = fixture.Facade.CreateContextSnapshot(PersistenceService.LocalPlayerId, PersistenceService.LocalPlayerId, "person.prototype.friend", 50d, options);
            Step12SocialContextSnapshot second = fixture.Facade.CreateContextSnapshot(PersistenceService.LocalPlayerId, PersistenceService.LocalPlayerId, "person.prototype.friend", 50d, options);
            Step12ContextRecordReference[] returnedRecords = first.Records as Step12ContextRecordReference[];
            Assert.That(returnedRecords, Is.Not.Null);
            returnedRecords[0] = new Step12ContextRecordReference("mutated", "mutated", Step12SocialProjectionState.HiddenState);

            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(first.Truncated, Is.True);
            Assert.That(first.Records.Any(item => item.RuntimeName == "mutated"), Is.False);
            Assert.That(first.SourceRuntimes.Count, Is.EqualTo(11));
            Assert.That(first.Records.Select(item => $"{item.RuntimeName}:{item.RecordId}").ToArray(), Is.EqualTo(first.Records.Select(item => $"{item.RuntimeName}:{item.RecordId}").OrderBy(item => item, StringComparer.Ordinal).ToArray()));
        }

        [Test]
        public void IntegrationValidatorRejectsCyclesAndUnsafeSchedulerConfiguration()
        {
            Step12IntegrationValidationReport report = new Step12IntegrationValidationReport();

            Step12SocialSimulationValidator.ValidatePersistenceDependencies(new[]
            {
                new Step12PersistenceDependencyEntry("a", "b"),
                new Step12PersistenceDependencyEntry("b", "a")
            }, report);
            Step12SocialSimulationValidator.ValidateSchedulerBudget(new Step12SchedulerBudget
            {
                MaximumEvaluationsPerTick = 0,
                MaximumQueuedConsequences = 10,
                MaximumRecursionDepth = 99,
                UseSystemTime = true,
                AllowImmediateRecursiveDispatch = true
            }, report);

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Diagnostics.Any(item => item.Code == "dependency-cycle"), Is.True);
            Assert.That(report.Diagnostics.Any(item => item.Code == "system-time"), Is.True);
            Assert.That(report.Diagnostics.Any(item => item.Code == "immediate-recursion"), Is.True);
        }

        [Test]
        public void TransactionCoordinatorPreviewsRollsBackAndDeduplicatesWithoutPartialCommit()
        {
            Step12SocialSimulationTransactionCoordinator coordinator = new Step12SocialSimulationTransactionCoordinator();
            bool previewed = false;
            bool committed = false;
            bool rolledBack = false;

            Step12TransactionParticipantPlan[] plans =
            {
                new Step12TransactionParticipantPlan("relationships", Step12TransactionFailurePolicy.Required, () => previewed = true, () => true, () => committed = true, () => rolledBack = true),
                new Step12TransactionParticipantPlan("reputation", Step12TransactionFailurePolicy.Required, () => true, () => true, () => false, () => rolledBack = true)
            };

            Step12TransactionResult preview = coordinator.Execute("tx.step12.integration", plans, preview: true);
            Step12TransactionResult failed = coordinator.Execute("tx.step12.integration", plans);
            Step12TransactionResult success = coordinator.Execute("tx.step12.integration.success", new[]
            {
                new Step12TransactionParticipantPlan("relationships", Step12TransactionFailurePolicy.Required, () => true, () => true, () => true, () => true)
            });
            Step12TransactionResult duplicate = coordinator.Execute("tx.step12.integration.success", plans);

            Assert.That(preview.Succeeded, Is.True);
            Assert.That(preview.Preview, Is.True);
            Assert.That(previewed, Is.True);
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(committed, Is.True);
            Assert.That(rolledBack, Is.True);
            Assert.That(success.Succeeded, Is.True);
            Assert.That(duplicate.Succeeded, Is.True);
            Assert.That(duplicate.Duplicate, Is.True);
        }

        private static void SeedContext(TestFixture fixture)
        {
            fixture.Relationships.CreateRelationship(new RelationshipCreateRequest
            {
                transactionId = "tx.step12.integration.friend",
                recordId = "relationship.step12.integration.friend",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = PersistenceService.LocalPlayerId,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                startWorldTime = 1d
            });
            fixture.Relationships.CreateRelationship(new RelationshipCreateRequest
            {
                transactionId = "tx.step12.integration.rival",
                recordId = "relationship.step12.integration.rival",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.RivalRelationshipId,
                firstPersonId = PersistenceService.LocalPlayerId,
                firstRoleId = "rival",
                secondPersonId = "person.prototype.rival",
                secondRoleId = "rival",
                startWorldTime = 2d
            });
            fixture.Attitudes.Mutate(new AttitudeMutationRequest
            {
                transactionId = "tx.step12.integration.trust",
                recordId = "attitude.step12.integration.trust",
                observerPersonId = PersistenceService.LocalPlayerId,
                subjectPersonId = "person.prototype.friend",
                dimensionId = PrototypeAttitudeDefinitionFactory.TrustId,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = 60,
                sourceCategory = AttitudeContributionSourceCategory.TestLab,
                worldTime = 3d
            });
            fixture.Interactions.Execute(new SocialInteractionRequest
            {
                TransactionId = "tx.step12.integration.greet",
                InteractionRecordId = "interaction.step12.integration.greet",
                InteractionDefinitionId = PrototypeSocialInteractionDefinitionFactory.GreetId,
                InitiatorPersonId = PersistenceService.LocalPlayerId,
                TargetPersonId = "person.prototype.friend",
                WorldTime = 4d,
                DeterministicSeed = "step12.integration"
            });
            fixture.Family.CreateHousehold(new HouseholdMutationRequest
            {
                transactionId = "tx.step12.integration.household",
                householdId = "household.step12.integration",
                householdDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.FamilyHouseholdDefinitionId,
                personId = PersistenceService.LocalPlayerId,
                role = HouseholdRole.Head,
                worldTime = 5d
            });
        }

        private static TestFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            RelationshipRuntime relationships = new RelationshipRuntime();
            relationships.Configure(registry, KnownPersons);
            InterpersonalAttitudeRuntime attitudes = new InterpersonalAttitudeRuntime();
            attitudes.Configure(registry, KnownPersons);
            ReputationRuntime reputation = new ReputationRuntime();
            reputation.Configure(registry, KnownPersons);
            RumorRuntime rumors = new RumorRuntime();
            rumors.Configure(registry, KnownPersons, _ => null, _ => null);
            SocialInteractionRuntime interactions = new SocialInteractionRuntime();
            interactions.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors);
            SocialNormRuntime norms = new SocialNormRuntime();
            norms.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors, interactions);
            SocialNetworkRuntime networks = new SocialNetworkRuntime();
            networks.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors, interactions, norms);
            SocialInfluenceRuntime influence = new SocialInfluenceRuntime();
            influence.Configure(registry, KnownPersons, attitudes, reputation, interactions, Array.Empty<UnityIsekaiGame.Knowledge.PersonKnowledgeRuntime>());
            SocialEmotionRuntime emotions = new SocialEmotionRuntime();
            emotions.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors, interactions, norms, networks, influence);
            SocialDecisionRuntime decisions = new SocialDecisionRuntime();
            decisions.Configure(registry, KnownPersons, interactions, relationships, attitudes, reputation, rumors, norms, networks, SocialDecisionModifierSourceCollection.Compose(influence, emotions));
            FamilyRelationshipRuntime family = new FamilyRelationshipRuntime();
            family.Configure(registry, KnownPersons, relationships, attitudes, interactions, PersistenceService.LocalWorldId, AdultPersons);
            Step12SocialSimulationFacade facade = new Step12SocialSimulationFacade(registry, KnownPersons, PersistenceService.LocalWorldId, relationships, attitudes, reputation, rumors, interactions, norms, networks, decisions, influence, emotions, family);
            return new TestFixture(registry, relationships, attitudes, reputation, rumors, interactions, norms, networks, decisions, influence, emotions, family, facade);
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeFamilyRelationshipDefinitionFactory.AddMissingPrototypeFamilyRelationshipDefinitions(
                PrototypeSocialEmotionDefinitionFactory.AddMissingPrototypeSocialEmotionDefinitions(
                    PrototypeSocialInfluenceDefinitionFactory.AddMissingPrototypeSocialInfluenceDefinitions(
                        PrototypeSocialDecisionDefinitionFactory.AddMissingPrototypeSocialDecisionDefinitions(
                            PrototypeSocialNetworkDefinitionFactory.AddMissingPrototypeSocialNetworkDefinitions(
                                PrototypeSocialNormDefinitionFactory.AddMissingPrototypeSocialNormDefinitions(
                                    PrototypeSocialInteractionDefinitionFactory.AddMissingPrototypeSocialInteractionDefinitions(
                                        PrototypeRumorDefinitionFactory.AddMissingPrototypeRumorDefinitions(
                                            PrototypeReputationDefinitionFactory.AddMissingPrototypeReputationDefinitions(
                                                PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(
                                                    PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(catalog.CreateRegistry())))))))))));
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(DefinitionRegistry registry, RelationshipRuntime relationships, InterpersonalAttitudeRuntime attitudes, ReputationRuntime reputation, RumorRuntime rumors, SocialInteractionRuntime interactions, SocialNormRuntime norms, SocialNetworkRuntime networks, SocialDecisionRuntime decisions, SocialInfluenceRuntime influence, SocialEmotionRuntime emotions, FamilyRelationshipRuntime family, Step12SocialSimulationFacade facade)
            {
                Registry = registry;
                Relationships = relationships;
                Attitudes = attitudes;
                Reputation = reputation;
                Rumors = rumors;
                Interactions = interactions;
                Norms = norms;
                Networks = networks;
                Decisions = decisions;
                Influence = influence;
                Emotions = emotions;
                Family = family;
                Facade = facade;
            }

            public DefinitionRegistry Registry { get; }
            public RelationshipRuntime Relationships { get; }
            public InterpersonalAttitudeRuntime Attitudes { get; }
            public ReputationRuntime Reputation { get; }
            public RumorRuntime Rumors { get; }
            public SocialInteractionRuntime Interactions { get; }
            public SocialNormRuntime Norms { get; }
            public SocialNetworkRuntime Networks { get; }
            public SocialDecisionRuntime Decisions { get; }
            public SocialInfluenceRuntime Influence { get; }
            public SocialEmotionRuntime Emotions { get; }
            public FamilyRelationshipRuntime Family { get; }
            public Step12SocialSimulationFacade Facade { get; }

            public void Dispose()
            {
                Decisions?.Dispose();
                Influence?.Dispose();
                Emotions?.Dispose();
                Interactions?.Dispose();
                Norms?.Dispose();
                Networks?.Dispose();
            }
        }
    }
}
#endif
