#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Tests
{
    public sealed class SocialDecisionMakingRelationshipDrivenNpcBehaviorTests
    {
        private const string CatalogPath = "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset";

        private static readonly string[] KnownPersons =
        {
            PersistenceService.LocalPlayerId,
            "person.prototype.friend",
            "person.prototype.rival",
            "person.prototype.mentor",
            "person.prototype.student"
        };

        [Test]
        public void PrototypeDecisionDefinitionsValidateAndResolve()
        {
            DefinitionRegistry registry = CreateRegistry();

            Assert.That(registry.TryGet(PrototypeSocialDecisionDefinitionFactory.SociableProfileId, out SocialDecisionProfileDefinition profile), Is.True);
            Assert.That(registry.TryGet(PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId, out SocialIntentionDefinition intention), Is.True);
            Assert.That(registry.TryGet(PrototypeSocialDecisionDefinitionFactory.ConsiderTrustId, out SocialConsiderationDefinition consideration), Is.True);
            Assert.That(profile.EnabledIntentionIds, Does.Contain(PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId));
            Assert.That(intention.EligibleInteractionDefinitionIds, Does.Contain(PrototypeSocialInteractionDefinitionFactory.GreetId));
            Assert.That(consideration.Input, Is.EqualTo(SocialDecisionConsiderationInput.TrustTowardTarget));

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (object definition in PrototypeSocialDecisionDefinitionFactory.CreateDefinitions())
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(registry.DefinitionsById, report);
                }
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
        }

        [Test]
        public void EvaluateOnlyIsDeterministicAndDoesNotMutateDecisionState()
        {
            using TestFixture fixture = CreateSeededFixture();
            long beforeRevision = fixture.Decisions.Revision;
            SocialDecisionRequest request = DecisionRequest(PrototypeSocialDecisionDefinitionFactory.SociableProfileId, commit: false);

            SocialDecisionResult first = fixture.Decisions.Evaluate(request);
            SocialDecisionResult second = fixture.Decisions.Evaluate(request);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(first.SelectedCandidate.candidateKey, Is.EqualTo(second.SelectedCandidate.candidateKey));
            Assert.That(first.SelectedCandidate.interactionDefinitionId, Is.EqualTo(second.SelectedCandidate.interactionDefinitionId));
            Assert.That(first.Candidates.Count, Is.EqualTo(second.Candidates.Count));
            Assert.That(fixture.Decisions.Revision, Is.EqualTo(beforeRevision));
            Assert.That(fixture.Decisions.Count, Is.EqualTo(0));
        }

        [Test]
        public void NoTargetsProducesExplicitNoActionWithoutCreatingHiddenState()
        {
            using TestFixture fixture = CreateFixture();

            SocialDecisionResult result = fixture.Decisions.Evaluate(new SocialDecisionRequest
            {
                ActorPersonId = PersistenceService.LocalPlayerId,
                DecisionProfileId = PrototypeSocialDecisionDefinitionFactory.SociableProfileId,
                WorldTime = 10d,
                DeterministicSeed = "no-targets",
                CommitDecisionState = false,
                ForceEvaluate = true,
                MaximumTargetsOverride = 0,
                MaximumCandidatesOverride = 8
            });

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Status, Is.EqualTo(SocialDecisionStatus.NoAction));
            Assert.That(result.Targets.Count, Is.EqualTo(0));
            Assert.That(result.Candidates.Count, Is.EqualTo(0));
            Assert.That(fixture.Decisions.Count, Is.EqualTo(0));
        }

        [Test]
        public void SubmitModeDelegatesExecutionToSocialInteractionRuntime()
        {
            using TestFixture fixture = CreateSeededFixture();
            int beforeInteractions = fixture.Interactions.Count;
            SocialDecisionRequest request = DecisionRequest(PrototypeSocialDecisionDefinitionFactory.ScriptControlledProfileId, PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId, commit: true);
            request.ExecutionMode = SocialDecisionExecutionMode.SubmitForExecution;

            SocialDecisionResult result = fixture.Decisions.Evaluate(request);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Status, Is.EqualTo(SocialDecisionStatus.Submitted));
            Assert.That(result.ExecutionResult, Is.Not.Null);
            Assert.That(result.ExecutionResult.Succeeded, Is.True, result.ExecutionResult.Message);
            Assert.That(fixture.Interactions.Count, Is.EqualTo(beforeInteractions + 1));
            Assert.That(fixture.Decisions.TryGetState(PersistenceService.LocalPlayerId, out SocialDecisionPersonStateSnapshot state), Is.True);
            Assert.That(state.ActiveIntentionDefinitionId, Is.EqualTo(PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId));
        }

        [Test]
        public void PersistenceRoundTripAndInvalidRestoreRejectWithoutMutation()
        {
            using TestFixture fixture = CreateSeededFixture();
            SocialDecisionResult selected = fixture.Decisions.Evaluate(DecisionRequest(PrototypeSocialDecisionDefinitionFactory.SociableProfileId, commit: true));
            SocialDecisionPersistenceParticipant participant = new SocialDecisionPersistenceParticipant(fixture.Decisions, () => fixture.Registry, () => KnownPersons);

            PersistenceParticipantSaveResult save = participant.CapturePayload();
            SocialDecisionRuntimeSaveData saveData = JsonUtility.FromJson<SocialDecisionRuntimeSaveData>(save.PayloadJson);
            SocialDecisionRuntime restored = new SocialDecisionRuntime();
            restored.Configure(fixture.Registry, KnownPersons, fixture.Interactions, fixture.Relationships, fixture.Attitudes, fixture.Reputation, fixture.Rumors, fixture.Norms, fixture.Networks);
            SocialDecisionResult restore = restored.RestoreFromSaveData(saveData, fixture.Registry, KnownPersons, restoringState: true);
            SocialDecisionRuntimeSaveData corrupt = saveData.Clone();
            corrupt.personStates[0].activeTargetPersonId = "person.prototype.unknown";
            int beforeCount = fixture.Decisions.Count;
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), SocialDecisionPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(selected.Succeeded, Is.True, selected.Message);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.Count, Is.EqualTo(fixture.Decisions.Count));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Decisions.Count, Is.EqualTo(beforeCount));
        }

        private static SocialDecisionRequest DecisionRequest(string profileId, string intentionId = "", bool commit = false)
        {
            return new SocialDecisionRequest
            {
                ActorPersonId = PersistenceService.LocalPlayerId,
                DecisionProfileId = profileId,
                ExplicitIntentionDefinitionId = intentionId,
                ExplicitTargetPersonId = "person.prototype.friend",
                AvailableTargetPersonIds = new[] { "person.prototype.friend" },
                ActorControlPolicy = SocialDecisionActorControlPolicy.AutonomousNpc,
                WorldTime = 100d,
                DeterministicSeed = "social-decision-tests",
                CommitDecisionState = commit,
                ForceEvaluate = true,
                MaximumTargetsOverride = 4,
                MaximumCandidatesOverride = 12
            };
        }

        private static TestFixture CreateSeededFixture()
        {
            TestFixture fixture = CreateFixture();
            RelationshipOperationResult relationship = fixture.Relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = "relationship.test.decision.friend",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = PersistenceService.LocalPlayerId,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                startWorldTime = 1d,
                transactionId = "relationship.tx.decision.friend"
            });
            Assert.That(relationship.Succeeded, Is.True, relationship.Message);
            MutateAttitude(fixture.Attitudes, "attitude.tx.decision.trust", PersistenceService.LocalPlayerId, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.TrustId, 70);
            MutateAttitude(fixture.Attitudes, "attitude.tx.decision.affection", PersistenceService.LocalPlayerId, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.AffectionId, 45);
            return fixture;
        }

        private static TestFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            RelationshipRuntime relationships = new RelationshipRuntime();
            InterpersonalAttitudeRuntime attitudes = new InterpersonalAttitudeRuntime();
            ReputationRuntime reputation = new ReputationRuntime();
            RumorRuntime rumors = new RumorRuntime();
            SocialInteractionRuntime interactions = new SocialInteractionRuntime();
            SocialNormRuntime norms = new SocialNormRuntime();
            SocialNetworkRuntime networks = new SocialNetworkRuntime();
            SocialDecisionRuntime decisions = new SocialDecisionRuntime();

            relationships.Configure(registry, KnownPersons);
            attitudes.Configure(registry, KnownPersons);
            reputation.Configure(registry, KnownPersons);
            rumors.Configure(registry, KnownPersons, _ => null, _ => null);
            interactions.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors);
            norms.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors, interactions);
            networks.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors, interactions, norms);
            decisions.Configure(registry, KnownPersons, interactions, relationships, attitudes, reputation, rumors, norms, networks);

            return new TestFixture(registry, relationships, attitudes, reputation, rumors, interactions, norms, networks, decisions);
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            DefinitionRegistry baseRegistry = catalog == null ? new DefinitionRegistry(Array.Empty<IGameDefinition>()) : catalog.CreateRegistry();
            return PrototypeSocialDecisionDefinitionFactory.AddMissingPrototypeSocialDecisionDefinitions(
                PrototypeSocialNetworkDefinitionFactory.AddMissingPrototypeSocialNetworkDefinitions(
                    PrototypeSocialNormDefinitionFactory.AddMissingPrototypeSocialNormDefinitions(
                        PrototypeSocialInteractionDefinitionFactory.AddMissingPrototypeSocialInteractionDefinitions(
                            PrototypeRumorDefinitionFactory.AddMissingPrototypeRumorDefinitions(
                                PrototypeReputationDefinitionFactory.AddMissingPrototypeReputationDefinitions(
                                    PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(
                                        PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(baseRegistry))))))));
        }

        private static void MutateAttitude(InterpersonalAttitudeRuntime runtime, string transactionId, string observer, string subject, string dimension, int value)
        {
            AttitudeMutationResult result = runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = transactionId,
                observerPersonId = observer,
                subjectPersonId = subject,
                dimensionId = dimension,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = value,
                worldTime = 2d
            });
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(DefinitionRegistry registry, RelationshipRuntime relationships, InterpersonalAttitudeRuntime attitudes, ReputationRuntime reputation, RumorRuntime rumors, SocialInteractionRuntime interactions, SocialNormRuntime norms, SocialNetworkRuntime networks, SocialDecisionRuntime decisions)
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

            public void Dispose()
            {
                Decisions.Dispose();
                Networks.Dispose();
                Norms.Dispose();
                Interactions.Dispose();
                Rumors.Dispose();
                Reputation.Dispose();
                Attitudes.Dispose();
            }
        }
    }
}
#endif
