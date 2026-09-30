using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Influence;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Tests
{
    public sealed class SocialInfluencePersuasionDeceptionResistanceTests
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
        public void InfluencePreviewIsDeterministicAndNonMutating()
        {
            using TestFixture fixture = CreateFixture();
            SocialInfluenceRequest request = InfluenceRequest("preview", PrototypeSocialInfluenceDefinitionFactory.PresentEvidenceId, SocialInfluenceIntent.ChangeBelief, claim: Claim("preview"));

            long beforeRevision = fixture.Influence.Revision;
            int beforeBeliefs = fixture.Knowledge.CreateSaveData().beliefs.Count();
            SocialInfluenceResult first = fixture.Influence.Preview(request);
            SocialInfluenceResult second = fixture.Influence.Preview(request);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(first.Attempt.margin, Is.EqualTo(second.Attempt.margin));
            Assert.That(first.Attempt.deterministicRoll, Is.EqualTo(second.Attempt.deterministicRoll));
            Assert.That(fixture.Influence.Revision, Is.EqualTo(beforeRevision));
            Assert.That(fixture.Influence.Count, Is.Zero);
            Assert.That(fixture.Knowledge.CreateSaveData().beliefs.Count, Is.EqualTo(beforeBeliefs));
        }

        [Test]
        public void PersuasionCreatesKnowledgeEvidenceWithoutConflatingCompliance()
        {
            using TestFixture fixture = CreateFixture();
            KnowledgePropositionData claim = Claim("accepted");

            SocialInfluenceResult belief = fixture.Influence.Execute(InfluenceRequest("accepted", PrototypeSocialInfluenceDefinitionFactory.PresentEvidenceId, SocialInfluenceIntent.ChangeBelief, claim: claim, speakerResolve: 900, targetResistance: 70, evidenceStrength: 600));
            int beforeInteractions = fixture.Interactions.Count;
            SocialInfluenceResult promise = fixture.Influence.Execute(InfluenceRequest("promise", PrototypeSocialInfluenceDefinitionFactory.PersuadeRequestId, SocialInfluenceIntent.GainPromise, claim: null, subjectKind: SocialInfluenceSubjectKind.Promise, interactionDefinitionId: PrototypeSocialInteractionDefinitionFactory.PromiseId, speakerResolve: 900, targetResistance: 40, worldTime: 30d));

            Assert.That(belief.Succeeded, Is.True, belief.Message);
            Assert.That(belief.KnowledgeResult, Is.Not.Null);
            Assert.That(belief.KnowledgeResult.Succeeded, Is.True, belief.KnowledgeResult.Message);
            Assert.That(fixture.Knowledge.TryGetBelief(claim, out KnowledgeBeliefRecord recorded), Is.True);
            Assert.That(recorded.Confidence, Is.GreaterThan(0));
            Assert.That(promise.Succeeded, Is.True, promise.Message);
            Assert.That(promise.Attempt.complianceOutcome, Is.EqualTo(SocialInfluenceComplianceOutcome.PromiseAccepted));
            Assert.That(promise.InteractionResult, Is.Not.Null);
            Assert.That(promise.InteractionResult.Succeeded, Is.True, promise.InteractionResult.Message);
            Assert.That(fixture.Interactions.Count, Is.EqualTo(beforeInteractions + 1));
        }

        [Test]
        public void DetectedDeceptionMutatesAttitudesButNotKnowledgeWhenRejected()
        {
            using TestFixture fixture = CreateFixture();
            int trustBefore = fixture.Attitudes.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.TrustId).EffectiveValue;
            int hostilityBefore = fixture.Attitudes.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.HostilityId).EffectiveValue;
            KnowledgePropositionData claim = Claim("lie");

            SocialInfluenceResult result = fixture.Influence.Execute(InfluenceRequest(
                "lie",
                PrototypeSocialInfluenceDefinitionFactory.TellDirectLieId,
                SocialInfluenceIntent.ChangeBelief,
                claim: claim,
                speaker: "person.prototype.rival",
                speakerResolve: 0,
                targetResistance: 900,
                difficulty: 300,
                truthStatus: SocialInfluenceTruthStatus.False,
                speakerBelief: SocialInfluenceSpeakerBeliefState.BelievesFalse,
                deception: SocialInfluenceDeceptionMode.DirectFalseAssertion,
                worldTime: 40d));

            int trustAfter = fixture.Attitudes.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.TrustId).EffectiveValue;
            int hostilityAfter = fixture.Attitudes.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.HostilityId).EffectiveValue;

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Attempt.honesty, Is.EqualTo(SocialInfluenceHonestyClassification.DirectLie));
            Assert.That(result.Attempt.detectionOutcome, Is.EqualTo(SocialInfluenceDetectionOutcome.Proven));
            Assert.That(fixture.Knowledge.TryGetBelief(claim, out _), Is.False);
            Assert.That(trustAfter, Is.LessThan(trustBefore));
            Assert.That(hostilityAfter, Is.GreaterThan(hostilityBefore));
        }

        [Test]
        public void InfluenceDecisionModifiersFeedSocialDecisionScoring()
        {
            using TestFixture fixture = CreateFixture();
            SocialDecisionResult before = fixture.Decisions.Evaluate(DecisionRequest(worldTime: 80d));

            SocialInfluenceResult influence = fixture.Influence.Execute(InfluenceRequest(
                "modifier",
                PrototypeSocialInfluenceDefinitionFactory.InspireId,
                SocialInfluenceIntent.EncourageAction,
                claim: null,
                speakerResolve: 900,
                targetResistance: 40,
                subjectKind: SocialInfluenceSubjectKind.Decision,
                ownerPersonId: "person.prototype.friend",
                intentionDefinitionId: PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId,
                interactionDefinitionId: PrototypeSocialInteractionDefinitionFactory.GreetId,
                worldTime: 80d));
            SocialDecisionResult after = fixture.Decisions.Evaluate(DecisionRequest(worldTime: 80d));

            Assert.That(before.Succeeded, Is.True, before.Message);
            Assert.That(before.SelectedCandidate, Is.Not.Null);
            Assert.That(before.SelectedCandidate.externalModifier, Is.Zero);
            Assert.That(influence.Succeeded, Is.True, influence.Message);
            Assert.That(influence.DecisionModifier, Is.Not.Null);
            Assert.That(after.Succeeded, Is.True, after.Message);
            Assert.That(after.SelectedCandidate, Is.Not.Null);
            Assert.That(after.SelectedCandidate.externalModifier, Is.GreaterThan(0));
            Assert.That(after.SelectedCandidate.finalScore, Is.GreaterThan(before.SelectedCandidate.finalScore));
        }

        [Test]
        public void PersistenceRejectsInvalidInfluencePayloadBeforeMutation()
        {
            using TestFixture fixture = CreateFixture();
            SocialInfluenceResult execute = fixture.Influence.Execute(InfluenceRequest("persist", PrototypeSocialInfluenceDefinitionFactory.ReassureId, SocialInfluenceIntent.Reassure, claim: null, subjectKind: SocialInfluenceSubjectKind.Person));
            SocialInfluencePersistenceParticipant participant = new SocialInfluencePersistenceParticipant(fixture.Influence, () => fixture.Registry, () => KnownPersons);

            PersistenceParticipantSaveResult save = participant.CapturePayload();
            SocialInfluenceRuntimeSaveData saveData = JsonUtility.FromJson<SocialInfluenceRuntimeSaveData>(save.PayloadJson);
            SocialInfluenceRuntimeSaveData corrupt = saveData.Clone();
            corrupt.attempts[0].targetPersonId = "person.prototype.unknown";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), SocialInfluencePersistenceParticipant.CurrentParticipantSchemaVersion);
            int liveCount = fixture.Influence.Count;
            SocialInfluenceRuntime restored = new SocialInfluenceRuntime();
            restored.Configure(fixture.Registry, KnownPersons, fixture.Attitudes, fixture.Reputation, fixture.Interactions, new[] { fixture.Knowledge });
            SocialInfluenceResult restore = restored.RestoreFromSaveData(saveData, fixture.Registry, KnownPersons, restoringState: true);

            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Influence.Count, Is.EqualTo(liveCount));
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.Count, Is.EqualTo(fixture.Influence.Count));
        }

        private static SocialDecisionRequest DecisionRequest(double worldTime)
        {
            return new SocialDecisionRequest
            {
                ActorPersonId = PersistenceService.LocalPlayerId,
                DecisionProfileId = PrototypeSocialDecisionDefinitionFactory.ScriptControlledProfileId,
                ExplicitIntentionDefinitionId = PrototypeSocialDecisionDefinitionFactory.GreetKnownPersonId,
                ExplicitTargetPersonId = "person.prototype.friend",
                AvailableTargetPersonIds = new[] { "person.prototype.friend" },
                ActorControlPolicy = SocialDecisionActorControlPolicy.AutonomousNpc,
                WorldTime = worldTime,
                DeterministicSeed = "social-influence-tests",
                CommitDecisionState = false,
                ForceEvaluate = true,
                MaximumTargetsOverride = 4,
                MaximumCandidatesOverride = 12
            };
        }

        private static SocialInfluenceRequest InfluenceRequest(
            string suffix,
            string methodId,
            SocialInfluenceIntent intent,
            KnowledgePropositionData claim,
            string speaker = "person.prototype.friend",
            int speakerResolve = 760,
            int targetResistance = 140,
            int evidenceStrength = 360,
            int difficulty = 0,
            SocialInfluenceSubjectKind subjectKind = SocialInfluenceSubjectKind.Claim,
            string ownerPersonId = "",
            string intentionDefinitionId = "",
            string interactionDefinitionId = "",
            SocialInfluenceTruthStatus truthStatus = SocialInfluenceTruthStatus.True,
            SocialInfluenceSpeakerBeliefState speakerBelief = SocialInfluenceSpeakerBeliefState.BelievesTrue,
            SocialInfluenceDeceptionMode deception = SocialInfluenceDeceptionMode.NoDeception,
            double worldTime = 20d)
        {
            return new SocialInfluenceRequest
            {
                TransactionId = $"test.social-influence.{suffix}",
                AttemptId = $"social-influence.test.{suffix}",
                MethodDefinitionId = methodId,
                SpeakerPersonId = speaker,
                TargetPersonId = PersistenceService.LocalPlayerId,
                WitnessPersonIds = new[] { "person.prototype.mentor" },
                Intent = intent,
                Subject = new SocialInfluenceSubjectData
                {
                    kind = subjectKind,
                    subjectId = claim == null ? $"social-influence.subject.{suffix}" : KnowledgeProposition.BuildIdentity(claim),
                    ownerPersonId = string.IsNullOrWhiteSpace(ownerPersonId) ? speaker : ownerPersonId,
                    tags = new[] { "test" }
                },
                Claim = claim,
                EvidencePackage = evidenceStrength <= 0 ? Array.Empty<SocialInfluenceEvidenceReferenceData>() : new[]
                {
                    new SocialInfluenceEvidenceReferenceData
                    {
                        evidenceId = $"evidence.social-influence.{suffix}",
                        sourceId = speaker,
                        strength = evidenceStrength,
                        credibility = speakerResolve,
                        fabricated = deception != SocialInfluenceDeceptionMode.NoDeception
                    }
                },
                Arguments = new[]
                {
                    new SocialInfluenceArgumentData
                    {
                        argumentId = $"argument.social-influence.{suffix}",
                        premise = "premise",
                        conclusion = "conclusion",
                        clarity = 90
                    }
                },
                TruthStatus = truthStatus,
                SpeakerBeliefState = speakerBelief,
                DeceptionMode = deception,
                SpeakerResolve = speakerResolve,
                TargetResistance = targetResistance,
                EvidenceStrength = evidenceStrength,
                RelationshipModifier = 100,
                ReputationModifier = 100,
                Difficulty = difficulty,
                WorldTime = worldTime,
                DeterministicSeed = "social-influence-tests",
                IntentionDefinitionId = intentionDefinitionId,
                InteractionDefinitionId = interactionDefinitionId,
                Visibility = SocialInfluenceVisibility.Witnessed
            };
        }

        private static KnowledgePropositionData Claim(string suffix)
        {
            return new KnowledgePropositionData
            {
                factDefinitionId = BuiltInKnowledgeFacts.EventOccurred,
                subjectType = KnowledgeSubjectType.Event,
                subjectId = $"event.social-influence.test.{suffix}",
                valueType = KnowledgeValueType.Boolean,
                booleanValue = true,
                sourceContextId = $"source.social-influence.test.{suffix}"
            };
        }

        private static TestFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            GameObject owner = new GameObject("Social Influence Knowledge");
            PersonKnowledgeRuntime knowledge = owner.AddComponent<PersonKnowledgeRuntime>();
            knowledge.Configure(registry, PersistenceService.LocalPlayerId);
            RelationshipRuntime relationships = new RelationshipRuntime();
            relationships.Configure(registry, KnownPersons);
            InterpersonalAttitudeRuntime attitudes = new InterpersonalAttitudeRuntime();
            attitudes.Configure(registry, KnownPersons);
            ReputationRuntime reputation = new ReputationRuntime();
            reputation.Configure(registry, KnownPersons);
            RumorRuntime rumors = new RumorRuntime();
            rumors.Configure(registry, KnownPersons, personId => string.Equals(personId, PersistenceService.LocalPlayerId, StringComparison.Ordinal) ? knowledge : null, _ => null);
            SocialInteractionRuntime interactions = new SocialInteractionRuntime();
            interactions.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors);
            SocialNormRuntime norms = new SocialNormRuntime();
            norms.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors, interactions);
            SocialNetworkRuntime networks = new SocialNetworkRuntime();
            networks.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors, interactions, norms);
            SocialInfluenceRuntime influence = new SocialInfluenceRuntime();
            influence.Configure(registry, KnownPersons, attitudes, reputation, interactions, new[] { knowledge });
            SocialDecisionRuntime decisions = new SocialDecisionRuntime();
            decisions.Configure(registry, KnownPersons, interactions, relationships, attitudes, reputation, rumors, norms, networks, influence);
            return new TestFixture(registry, owner, knowledge, relationships, attitudes, reputation, rumors, interactions, norms, networks, influence, decisions);
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeSocialInfluenceDefinitionFactory.AddMissingPrototypeSocialInfluenceDefinitions(
                PrototypeSocialDecisionDefinitionFactory.AddMissingPrototypeSocialDecisionDefinitions(
                    PrototypeSocialNetworkDefinitionFactory.AddMissingPrototypeSocialNetworkDefinitions(
                        PrototypeSocialNormDefinitionFactory.AddMissingPrototypeSocialNormDefinitions(
                            PrototypeSocialInteractionDefinitionFactory.AddMissingPrototypeSocialInteractionDefinitions(
                                PrototypeRumorDefinitionFactory.AddMissingPrototypeRumorDefinitions(
                                    PrototypeReputationDefinitionFactory.AddMissingPrototypeReputationDefinitions(
                                        PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(
                                            PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(catalog.CreateRegistry())))))))));
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(DefinitionRegistry registry, GameObject owner, PersonKnowledgeRuntime knowledge, RelationshipRuntime relationships, InterpersonalAttitudeRuntime attitudes, ReputationRuntime reputation, RumorRuntime rumors, SocialInteractionRuntime interactions, SocialNormRuntime norms, SocialNetworkRuntime networks, SocialInfluenceRuntime influence, SocialDecisionRuntime decisions)
            {
                Registry = registry;
                Owner = owner;
                Knowledge = knowledge;
                Relationships = relationships;
                Attitudes = attitudes;
                Reputation = reputation;
                Rumors = rumors;
                Interactions = interactions;
                Norms = norms;
                Networks = networks;
                Influence = influence;
                Decisions = decisions;
            }

            public DefinitionRegistry Registry { get; }
            public GameObject Owner { get; }
            public PersonKnowledgeRuntime Knowledge { get; }
            public RelationshipRuntime Relationships { get; }
            public InterpersonalAttitudeRuntime Attitudes { get; }
            public ReputationRuntime Reputation { get; }
            public RumorRuntime Rumors { get; }
            public SocialInteractionRuntime Interactions { get; }
            public SocialNormRuntime Norms { get; }
            public SocialNetworkRuntime Networks { get; }
            public SocialInfluenceRuntime Influence { get; }
            public SocialDecisionRuntime Decisions { get; }

            public void Dispose()
            {
                Influence?.Dispose();
                Decisions?.Dispose();
                if (Owner != null)
                {
                    UnityEngine.Object.DestroyImmediate(Owner);
                }
            }
        }
    }
}
