#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Emotions;
using UnityIsekaiGame.Social.Influence;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Tests
{
    public sealed class SocialEmotionsMoodsAffectiveReactionsTests
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
        public void PrototypeDefinitionsResolveAndValidate()
        {
            DefinitionRegistry registry = CreateRegistry();
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IDefinitionCatalogValidationParticipant definition in PrototypeSocialEmotionDefinitionFactory.CreateDefinitions().OfType<IDefinitionCatalogValidationParticipant>())
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(report.WarningCount, Is.Zero, report.ToString());
            Assert.That(registry.TryGet(PrototypeSocialEmotionDefinitionFactory.JoyId, out SocialEmotionDefinition _), Is.True);
            Assert.That(registry.TryGet(PrototypeSocialEmotionDefinitionFactory.DisappointmentId, out SocialEmotionDefinition _), Is.True);
            Assert.That(registry.TryGet(PrototypeSocialEmotionDefinitionFactory.MoodValenceId, out SocialMoodDimensionDefinition _), Is.True);
            Assert.That(registry.TryGet(PrototypeSocialEmotionDefinitionFactory.DetectedDeceptionRuleId, out SocialEmotionAppraisalRuleDefinition _), Is.True);
        }

        [Test]
        public void PreviewDoesNotMutateAndDuplicateExecuteIsIdempotent()
        {
            using TestFixture fixture = CreateFixture();
            SocialEmotionTriggerRequest request = EmotionRequest("gratitude", PrototypeSocialEmotionDefinitionFactory.GratitudeId, target: "person.prototype.friend", subject: "interaction.prototype.help", intensity: 60, duration: 100d, worldTime: 10d);

            SocialEmotionResult preview = fixture.Emotions.Preview(request);
            long afterPreviewRevision = fixture.Emotions.Revision;
            SocialEmotionResult execute = fixture.Emotions.Execute(request);
            SocialEmotionResult duplicate = fixture.Emotions.Execute(request);

            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(afterPreviewRevision, Is.Zero);
            Assert.That(fixture.Emotions.Count, Is.EqualTo(1));
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(fixture.Emotions.Count, Is.EqualTo(1));
            Assert.That(fixture.Emotions.Revision, Is.EqualTo(execute.RevisionAfter));
        }

        [Test]
        public void BeliefRelativeAppraisalUsesBelievedTruthNotObjectiveTruth()
        {
            using TestFixture fixture = CreateFixture();
            SocialEmotionResult result = fixture.Emotions.Execute(new SocialEmotionTriggerRequest
            {
                TransactionId = "test.social-emotions.accepted-threat",
                PersonId = PersistenceService.LocalPlayerId,
                Cause = new SocialEmotionCauseReferenceData
                {
                    category = SocialEmotionCauseCategory.BeliefAccepted,
                    sourceRuntime = "SocialInfluenceRuntime",
                    sourceRecordId = "influence.prototype.threat",
                    subjectId = "claim.prototype.threat",
                    targetPersonId = "person.prototype.rival",
                    responsibility = SocialEmotionResponsibility.Target,
                    believedTruthStatus = SocialInfluenceTruthStatus.True,
                    tags = new[] { "threat" }
                },
                WorldTime = 20d
            });

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Episode.EmotionDefinitionId, Is.EqualTo(PrototypeSocialEmotionDefinitionFactory.FearId));
            Assert.That(result.Episode.TargetPersonId, Is.EqualTo("person.prototype.rival"));
            Assert.That(result.Mood.MoodDimensionId, Is.EqualTo(PrototypeSocialEmotionDefinitionFactory.MoodAnxietyId));
            Assert.That(result.Mood.Value, Is.GreaterThan(0));
        }

        [Test]
        public void DecayStackingAndMoodEvaluationAreDeterministic()
        {
            using TestFixture fixture = CreateFixture();
            SocialEmotionTriggerRequest request = EmotionRequest("gratitude-stack", PrototypeSocialEmotionDefinitionFactory.GratitudeId, target: "person.prototype.friend", subject: "interaction.prototype.help", intensity: 60, duration: 100d, worldTime: 10d);
            SocialEmotionResult first = fixture.Emotions.Execute(request);
            SocialEmotionResult reinforce = fixture.Emotions.Execute(EmotionRequest("gratitude-stack-reinforce", PrototypeSocialEmotionDefinitionFactory.GratitudeId, target: "person.prototype.friend", subject: "interaction.prototype.help", intensity: 45, duration: 100d, worldTime: 20d));

            SocialEmotionEpisodeSnapshot at50a = fixture.Emotions.QueryActiveEpisodes(PersistenceService.LocalPlayerId, 50d).Single();
            SocialEmotionEpisodeSnapshot at50b = fixture.Emotions.QueryActiveEpisodes(PersistenceService.LocalPlayerId, 50d).Single();
            SocialMoodSnapshot moodA = fixture.Emotions.QueryMoods(PersistenceService.LocalPlayerId, 50d).Single(item => item.MoodDimensionId == PrototypeSocialEmotionDefinitionFactory.MoodSocialOpennessId);
            SocialMoodSnapshot moodB = fixture.Emotions.QueryMoods(PersistenceService.LocalPlayerId, 50d).Single(item => item.MoodDimensionId == PrototypeSocialEmotionDefinitionFactory.MoodSocialOpennessId);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(reinforce.Succeeded, Is.True, reinforce.Message);
            Assert.That(fixture.Emotions.Count, Is.EqualTo(1));
            Assert.That(at50a.CurrentIntensity, Is.EqualTo(at50b.CurrentIntensity));
            Assert.That(at50a.CurrentIntensity, Is.GreaterThan(0));
            Assert.That(at50a.CurrentIntensity, Is.LessThan(reinforce.Episode.CurrentIntensity));
            Assert.That(moodA.Value, Is.EqualTo(moodB.Value));
        }

        [Test]
        public void PersistenceProjectionAndValidationPreserveBoundaries()
        {
            using TestFixture fixture = CreateFixture();
            SocialEmotionResult created = fixture.Emotions.Execute(EmotionRequest("concealed-shame", PrototypeSocialEmotionDefinitionFactory.ShameId, target: string.Empty, subject: "event.prototype.mistake", intensity: 55, duration: 150d, worldTime: 30d, concealed: true));
            SocialEmotionPersistenceParticipant participant = new SocialEmotionPersistenceParticipant(fixture.Emotions, () => fixture.Registry, () => KnownPersons);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            SocialEmotionRuntimeSaveData saveData = JsonUtility.FromJson<SocialEmotionRuntimeSaveData>(save.PayloadJson);
            SocialEmotionRuntime restored = new SocialEmotionRuntime();
            restored.Configure(fixture.Registry, KnownPersons);
            SocialEmotionResult restore = restored.RestoreFromSaveData(saveData, fixture.Registry, KnownPersons, restoringState: true);
            SocialEmotionProjection owner = restored.GetProjection(PersistenceService.LocalPlayerId, created.Episode.EpisodeId, privileged: false, worldTime: 31d);
            SocialEmotionProjection other = restored.GetProjection("person.prototype.rival", created.Episode.EpisodeId, privileged: false, worldTime: 31d);
            SocialEmotionRuntimeSaveData corrupt = saveData.Clone();
            corrupt.episodes[0].personId = "person.prototype.unknown";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), SocialEmotionPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(created.Succeeded, Is.True, created.Message);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.Count, Is.EqualTo(fixture.Emotions.Count));
            Assert.That(owner.Access, Is.EqualTo(SocialEmotionProjectionAccess.Full));
            Assert.That(other.Access, Is.EqualTo(SocialEmotionProjectionAccess.Concealed));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Emotions.Count, Is.EqualTo(saveData.episodes.Count));
        }

        [Test]
        public void EmotionDecisionModifierComposesWithInfluenceWithoutOwningDecisions()
        {
            using TestFixture fixture = CreateFixture();
            SocialEmotionResult anger = fixture.Emotions.Execute(new SocialEmotionTriggerRequest
            {
                TransactionId = "test.social-emotions.deception",
                PersonId = PersistenceService.LocalPlayerId,
                Cause = new SocialEmotionCauseReferenceData
                {
                    category = SocialEmotionCauseCategory.DeceptionDetected,
                    targetPersonId = "person.prototype.rival",
                    subjectId = "claim.prototype.lie",
                    responsibility = SocialEmotionResponsibility.Target,
                    believedTruthStatus = SocialInfluenceTruthStatus.False,
                    detectionOutcome = SocialInfluenceDetectionOutcome.Detected,
                    tags = new[] { "deception" }
                },
                WorldTime = 40d
            });

            ISocialDecisionModifierSource aggregate = SocialDecisionModifierSourceCollection.Compose(fixture.Influence, fixture.Emotions);
            int modifier = aggregate.ResolveSocialDecisionScoreModifier(PersistenceService.LocalPlayerId, "person.prototype.rival", string.Empty, string.Empty, 41d, out string sourceId);

            Assert.That(anger.Succeeded, Is.True, anger.Message);
            Assert.That(modifier, Is.LessThan(0));
            Assert.That(sourceId, Does.Contain("social-emotion-modifier"));
            Assert.That(fixture.Decisions.Count, Is.Zero);
        }

        private static SocialEmotionTriggerRequest EmotionRequest(string suffix, string emotionId, string target, string subject, int intensity, double duration, double worldTime, bool concealed = false)
        {
            return new SocialEmotionTriggerRequest
            {
                TransactionId = $"test.social-emotions.{suffix}",
                PersonId = PersistenceService.LocalPlayerId,
                EmotionDefinitionId = emotionId,
                TargetPersonId = target,
                SubjectId = subject,
                IntensityOverride = intensity,
                DurationOverrideSeconds = duration,
                Concealed = concealed,
                Cause = new SocialEmotionCauseReferenceData
                {
                    category = SocialEmotionCauseCategory.Interaction,
                    targetPersonId = target,
                    subjectId = subject,
                    responsibility = SocialEmotionResponsibility.Target,
                    tags = new[] { "test" }
                },
                WorldTime = worldTime
            };
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
            return new TestFixture(registry, relationships, attitudes, reputation, rumors, interactions, norms, networks, influence, emotions, decisions);
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeSocialEmotionDefinitionFactory.AddMissingPrototypeSocialEmotionDefinitions(
                PrototypeSocialInfluenceDefinitionFactory.AddMissingPrototypeSocialInfluenceDefinitions(
                    PrototypeSocialDecisionDefinitionFactory.AddMissingPrototypeSocialDecisionDefinitions(
                        PrototypeSocialNetworkDefinitionFactory.AddMissingPrototypeSocialNetworkDefinitions(
                            PrototypeSocialNormDefinitionFactory.AddMissingPrototypeSocialNormDefinitions(
                                PrototypeSocialInteractionDefinitionFactory.AddMissingPrototypeSocialInteractionDefinitions(
                                    PrototypeRumorDefinitionFactory.AddMissingPrototypeRumorDefinitions(
                                        PrototypeReputationDefinitionFactory.AddMissingPrototypeReputationDefinitions(
                                            PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(
                                                PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(catalog.CreateRegistry()))))))))));
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(DefinitionRegistry registry, RelationshipRuntime relationships, InterpersonalAttitudeRuntime attitudes, ReputationRuntime reputation, RumorRuntime rumors, SocialInteractionRuntime interactions, SocialNormRuntime norms, SocialNetworkRuntime networks, SocialInfluenceRuntime influence, SocialEmotionRuntime emotions, SocialDecisionRuntime decisions)
            {
                Registry = registry;
                Relationships = relationships;
                Attitudes = attitudes;
                Reputation = reputation;
                Rumors = rumors;
                Interactions = interactions;
                Norms = norms;
                Networks = networks;
                Influence = influence;
                Emotions = emotions;
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
            public SocialInfluenceRuntime Influence { get; }
            public SocialEmotionRuntime Emotions { get; }
            public SocialDecisionRuntime Decisions { get; }

            public void Dispose()
            {
                Emotions?.Dispose();
                Influence?.Dispose();
                Decisions?.Dispose();
            }
        }
    }
}
#endif
