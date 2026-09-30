using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class KnowledgeFactsBeliefsDiscoveryTests
    {
        [Test]
        public void FactDefinitionsResolveByStableIdAndValidate()
        {
            DefinitionRegistry registry = Registry();

            Assert.That(registry.TryGet(BuiltInKnowledgeFacts.BodyInjury, out KnowledgeFactDefinition fact), Is.True);
            Assert.That(fact.Id, Is.EqualTo("fact.body.injury"));
            Assert.That(fact.Domain, Is.EqualTo(KnowledgeDomain.Medical));
            Assert.That(registry.TryGet(BuiltInKnowledgeFacts.BodyReplacement, out KnowledgeFactDefinition replacementFact), Is.True);
            Assert.That(replacementFact.StalenessPolicy, Is.EqualTo(KnowledgeStalenessPolicy.HistoricalOnly));
        }

        [Test]
        public void RuntimeBelongsToExactPersonAndDoesNotRequireBody()
        {
            using TestRuntime fixture = CreateRuntime("person.knowledge.a");

            Assert.That(fixture.Runtime.IsReady, Is.True);
            Assert.That(fixture.Runtime.PersonId, Is.EqualTo("person.knowledge.a"));
            Assert.That(fixture.Runtime.CurrentBodyId, Is.Empty);
        }

        [Test]
        public void DifferentPersonsHaveSeparateKnowledge()
        {
            using TestRuntime first = CreateRuntime("person.knowledge.first");
            using TestRuntime second = CreateRuntime("person.knowledge.second");

            KnowledgeOperationResult result = first.Runtime.RecordObservation(VisibleInjury(first.Runtime, "tx.first"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(first.Runtime.CreateSnapshot().Beliefs.Count, Is.EqualTo(1));
            Assert.That(second.Runtime.CreateSnapshot().Beliefs.Count, Is.EqualTo(0));
        }

        [Test]
        public void PropositionIdentityIsDeterministicAndTypedValueValidates()
        {
            DefinitionRegistry registry = Registry();
            KnowledgePropositionData first = VisibleInjuryData("body.a");
            KnowledgePropositionData second = VisibleInjuryData("body.a");

            Assert.That(KnowledgeProposition.BuildIdentity(first), Is.EqualTo(KnowledgeProposition.BuildIdentity(second)));
            Assert.That(registry.TryGet(first.factDefinitionId, out KnowledgeFactDefinition fact), Is.True);
            Assert.That(KnowledgeProposition.Validate(first, fact, out string failure), Is.True, failure);
        }

        [Test]
        public void PreviewObservationMutatesNothingAndEmitsNoEvents()
        {
            using TestRuntime fixture = CreateRuntime("person.knowledge.preview");
            int events = 0;
            fixture.Runtime.KnowledgeChanged += (_, __) => events++;

            KnowledgeOperationResult result = fixture.Runtime.PreviewObservation(VisibleInjury(fixture.Runtime, "tx.preview"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Preview, Is.True);
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(0));
            Assert.That(fixture.Runtime.CreateSnapshot().Evidence.Count, Is.EqualTo(0));
            Assert.That(events, Is.EqualTo(0));
        }

        [Test]
        public void ObservationCreatesEvidenceAndBelief()
        {
            using TestRuntime fixture = CreateRuntime("person.knowledge.observe");

            KnowledgeOperationResult result = fixture.Runtime.RecordObservation(VisibleInjury(fixture.Runtime, "tx.observe"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ResultingBelief.State, Is.EqualTo(KnowledgeBeliefState.Believed));
            Assert.That(fixture.Runtime.CreateSnapshot().Evidence.Count, Is.EqualTo(1));
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateObservationIsIdempotent()
        {
            using TestRuntime fixture = CreateRuntime("person.knowledge.duplicate");
            KnowledgeObservationRequest request = VisibleInjury(fixture.Runtime, "tx.duplicate");

            KnowledgeOperationResult first = fixture.Runtime.RecordObservation(request);
            KnowledgeOperationResult second = fixture.Runtime.RecordObservation(request);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.Duplicate, Is.True);
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(1));
            Assert.That(fixture.Runtime.CreateSnapshot().Evidence.Count, Is.EqualTo(1));
        }

        [Test]
        public void WeakAndStrongEvidenceProduceDistinctStates()
        {
            using TestRuntime fixture = CreateRuntime("person.knowledge.confidence");
            KnowledgeObservationRequest weak = SpeciesCapability(fixture.Runtime, "tx.weak", 220, 450);
            KnowledgeObservationRequest strong = SpeciesCapability(fixture.Runtime, "tx.strong", 900, 900);

            KnowledgeOperationResult weakResult = fixture.Runtime.RecordObservation(weak);
            KnowledgeOperationResult strongResult = fixture.Runtime.RecordObservation(strong);

            Assert.That(weakResult.ResultingBelief.State, Is.EqualTo(KnowledgeBeliefState.Suspected));
            Assert.That(strongResult.ResultingBelief.Confidence, Is.GreaterThan(weakResult.ResultingBelief.Confidence));
        }

        [Test]
        public void OpposingEvidenceCreatesDisputeDeterministically()
        {
            using TestRuntime fixture = CreateRuntime("person.knowledge.dispute");
            fixture.Runtime.RecordObservation(SpeciesCapability(fixture.Runtime, "tx.support", 800, 800));
            KnowledgeObservationRequest oppose = SpeciesCapability(fixture.Runtime, "tx.oppose", 700, 700);
            oppose.Direction = KnowledgeEvidenceDirection.Opposes;

            KnowledgeOperationResult result = fixture.Runtime.RecordObservation(oppose);

            Assert.That(result.ResultingBelief.State, Is.EqualTo(KnowledgeBeliefState.Disputed));
            Assert.That(result.ResultingBelief.OpposingEvidenceIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void MisconceptionDoesNotChangeAuthoritativeTruthAndCanBeCorrected()
        {
            using TestRuntime fixture = CreateRuntime("person.knowledge.misconception");
            KnowledgeObservationRequest wrong = SpeciesCapability(fixture.Runtime, "tx.wrong", 900, 900);
            wrong.Proposition.stableValueId = "capability.false-spirit-can-bleed";
            wrong.MarkAsMisconception = true;
            wrong.TruthAuthorization = KnowledgeTruthAuthorization.CreateDevelopmentFixture("test.knowledge.misconception");

            KnowledgeOperationResult misconception = fixture.Runtime.RecordObservation(wrong);
            KnowledgeOperationResult correction = fixture.Runtime.RecordObservation(SpeciesCapability(fixture.Runtime, "tx.correct", 950, 950, KnowledgeEvidenceDirection.Corrects));

            Assert.That(misconception.ResultingBelief.State, Is.EqualTo(KnowledgeBeliefState.Misconception));
            Assert.That(correction.ResultingBelief.TruthState, Is.EqualTo(KnowledgeTruthState.Aligned));
        }

        [Test]
        public void DiagnosticOnlyFactBlockedFromOrdinaryAcquisition()
        {
            using TestRuntime fixture = CreateRuntime("person.knowledge.private");
            KnowledgeObservationRequest request = SpeciesCapability(fixture.Runtime, "tx.private", 600, 600);
            request.Visibility = KnowledgeVisibility.DiagnosticOnly;

            KnowledgeOperationResult result = fixture.Runtime.RecordObservation(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(KnowledgeResultCode.DiagnosticFactBlocked));
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(0));
        }

        [Test]
        public void MissingFactDefinitionFailsClearlyWithoutRuntimeFallback()
        {
            GameObject gameObject = new GameObject("Knowledge Missing Fact Test");
            try
            {
                PersonKnowledgeRuntime runtime = gameObject.AddComponent<PersonKnowledgeRuntime>();
                runtime.Configure(new DefinitionRegistry(Array.Empty<IGameDefinition>()), "person.knowledge.missing-fact");

                KnowledgeOperationResult result = runtime.RecordObservation(VisibleInjury(runtime, "tx.missing-fact"));

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Code, Is.EqualTo(KnowledgeResultCode.MissingFactDefinition));
                Assert.That(runtime.KnowledgeRevision, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TruthAuthorizationRequiresTrustedConstruction()
        {
            ConstructorInfo[] publicConstructors = typeof(KnowledgeTruthAuthorization).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            Assert.That(publicConstructors.Length, Is.EqualTo(0));
        }

        [Test]
        public void SharingCreatesListenerTestimonyEvidenceNotCopiedBelief()
        {
            using TestRuntime speaker = CreateRuntime("person.knowledge.speaker");
            using TestRuntime listener = CreateRuntime("person.knowledge.listener");
            KnowledgeOperationResult source = speaker.Runtime.RecordObservation(VisibleInjury(speaker.Runtime, "tx.speaker"));

            KnowledgeOperationResult result = listener.Runtime.ShareBelief(new KnowledgeShareRequest
            {
                TransactionId = "tx.share",
                SpeakerPersonId = speaker.Runtime.PersonId,
                ListenerPersonId = listener.Runtime.PersonId,
                SpeakerBelief = source.ResultingBelief,
                ListenerCredibility = 650
            });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Evidence.Provenance, Is.EqualTo(KnowledgeProvenance.Testimony));
            Assert.That(result.ResultingBelief.BeliefId, Is.Not.EqualTo(source.ResultingBelief.BeliefId));
        }

        [Test]
        public void StaleAndForgetRemainDistinctFromUnknown()
        {
            using TestRuntime fixture = CreateRuntime("person.knowledge.stale");
            KnowledgeBeliefRecord belief = fixture.Runtime.RecordObservation(VisibleInjury(fixture.Runtime, "tx.stale-source")).ResultingBelief;

            KnowledgeOperationResult stale = fixture.Runtime.MarkStale(belief.BeliefId, "tx.stale", "test");
            KnowledgeOperationResult forget = fixture.Runtime.ForgetBelief(belief.BeliefId, "tx.forget", 1000);

            Assert.That(stale.ResultingBelief.State, Is.EqualTo(KnowledgeBeliefState.Stale));
            Assert.That(forget.ResultingBelief.State, Is.EqualTo(KnowledgeBeliefState.Forgotten));
        }

        [Test]
        public void SnapshotIsReadOnlyAndOrdered()
        {
            using TestRuntime fixture = CreateRuntime("person.knowledge.snapshot");
            fixture.Runtime.RecordObservation(SpeciesCapability(fixture.Runtime, "tx.b", 700, 700));
            fixture.Runtime.RecordObservation(VisibleInjury(fixture.Runtime, "tx.a"));

            KnowledgeSnapshot snapshot = fixture.Runtime.CreateSnapshot();

            Assert.That(snapshot.Beliefs.Select(belief => belief.BeliefId), Is.Ordered);
            Assert.That(snapshot.Revision, Is.EqualTo(fixture.Runtime.KnowledgeRevision));
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<KnowledgeBeliefRecord>)snapshot.Beliefs).Add(snapshot.Beliefs[0]));
        }

        [Test]
        public void PersistencePrepareMutatesNothingAndCommitRestoresSilently()
        {
            using TestRuntime fixture = CreateRuntime("person.knowledge.persistence");
            fixture.Runtime.RecordObservation(VisibleInjury(fixture.Runtime, "tx.persist"));
            PersonKnowledgePersistenceParticipant participant = new PersonKnowledgePersistenceParticipant(fixture.Runtime, Registry);
            string payload = JsonUtility.ToJson(fixture.Runtime.CreateSaveData());
            long revision = fixture.Runtime.KnowledgeRevision;
            int events = 0;
            fixture.Runtime.KnowledgeChanged += (_, __) => events++;

            var prepared = participant.PreparePayload(payload, PersonKnowledgePersistenceParticipant.CurrentParticipantSchemaVersion);
            var committed = participant.CommitPreparedPayload(prepared.PreparedPayload);

            Assert.That(prepared.Succeeded, Is.True, prepared.Message);
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(revision));
            Assert.That(committed.Succeeded, Is.True, committed.Message);
            Assert.That(events, Is.EqualTo(0));
        }

        private static DefinitionRegistry Registry()
        {
            return new DefinitionRegistry(new IGameDefinition[]
            {
                CreateFact(BuiltInKnowledgeFacts.SpeciesIdentity, "Species Identity", KnowledgeDomain.Species, KnowledgePropositionType.Identity, KnowledgeSubjectType.Body, KnowledgeValueType.StableId, KnowledgeVisibility.Public, KnowledgeStalenessPolicy.BodyRevisionSensitive),
                CreateFact(BuiltInKnowledgeFacts.SpeciesCapability, "Species Capability", KnowledgeDomain.Species, KnowledgePropositionType.Capability, KnowledgeSubjectType.Species, KnowledgeValueType.StableId),
                CreateFact(BuiltInKnowledgeFacts.BodyInjury, "Body Injury", KnowledgeDomain.Medical, KnowledgePropositionType.Injury, KnowledgeSubjectType.Body, KnowledgeValueType.StableId, KnowledgeVisibility.PersonallyObservable, KnowledgeStalenessPolicy.BodyRevisionSensitive),
                CreateFact(BuiltInKnowledgeFacts.BodySymptom, "Body Symptom", KnowledgeDomain.Medical, KnowledgePropositionType.Symptom, KnowledgeSubjectType.Body, KnowledgeValueType.Qualitative, KnowledgeVisibility.Public, KnowledgeStalenessPolicy.RequiresReverification),
                CreateFact(BuiltInKnowledgeFacts.BodyTransformation, "Body Transformation", KnowledgeDomain.Transformation, KnowledgePropositionType.Transformation, KnowledgeSubjectType.Body, KnowledgeValueType.StableId, KnowledgeVisibility.Hidden, KnowledgeStalenessPolicy.EventInvalidated),
                CreateFact(BuiltInKnowledgeFacts.BodyPreviousBody, "Previous Body", KnowledgeDomain.Historical, KnowledgePropositionType.History, KnowledgeSubjectType.Person, KnowledgeValueType.StableId, KnowledgeVisibility.Private, KnowledgeStalenessPolicy.HistoricalOnly),
                CreateFact(BuiltInKnowledgeFacts.BodyReplacement, "Body Replacement", KnowledgeDomain.Historical, KnowledgePropositionType.History, KnowledgeSubjectType.Person, KnowledgeValueType.StableId, KnowledgeVisibility.Private, KnowledgeStalenessPolicy.HistoricalOnly),
                CreateFact(BuiltInKnowledgeFacts.CompatibilityResistance, "Compatibility Resistance", KnowledgeDomain.Compatibility, KnowledgePropositionType.Compatibility, KnowledgeSubjectType.Species, KnowledgeValueType.StableId, KnowledgeVisibility.Hidden, KnowledgeStalenessPolicy.RequiresReverification),
                CreateFact(BuiltInKnowledgeFacts.PersonIdentity, "Person Identity", KnowledgeDomain.Personal, KnowledgePropositionType.Identity, KnowledgeSubjectType.Person, KnowledgeValueType.StableId),
                CreateFact(BuiltInKnowledgeFacts.EventOccurred, "Event Occurred", KnowledgeDomain.Historical, KnowledgePropositionType.Event, KnowledgeSubjectType.Event, KnowledgeValueType.Boolean, KnowledgeVisibility.Public, KnowledgeStalenessPolicy.HistoricalOnly)
            });
        }

        private static KnowledgeFactDefinition CreateFact(
            string id,
            string displayName,
            KnowledgeDomain domain,
            KnowledgePropositionType propositionType,
            KnowledgeSubjectType subjectType,
            KnowledgeValueType valueType,
            KnowledgeVisibility visibility = KnowledgeVisibility.Public,
            KnowledgeStalenessPolicy stalenessPolicy = KnowledgeStalenessPolicy.NeverStale)
        {
            KnowledgeFactDefinition definition = ScriptableObject.CreateInstance<KnowledgeFactDefinition>();
            definition.name = displayName;
            Set(definition, "factId", id);
            Set(definition, "displayName", displayName);
            Set(definition, "domain", domain);
            Set(definition, "propositionType", propositionType);
            Set(definition, "subjectType", subjectType);
            Set(definition, "valueType", valueType);
            Set(definition, "defaultVisibility", visibility);
            Set(definition, "stalenessPolicy", stalenessPolicy);
            Set(definition, "certaintyThreshold", 700);
            Set(definition, "requiredEvidenceCount", 1);
            return definition;
        }

        private static void Set<T>(KnowledgeFactDefinition definition, string fieldName, T value)
        {
            FieldInfo field = typeof(KnowledgeFactDefinition).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(definition, value);
        }

        private static TestRuntime CreateRuntime(string personId)
        {
            GameObject gameObject = new GameObject($"Knowledge Test - {personId}");
            PersonKnowledgeRuntime runtime = gameObject.AddComponent<PersonKnowledgeRuntime>();
            runtime.Configure(Registry(), personId);
            return new TestRuntime(gameObject, runtime);
        }

        private static KnowledgeObservationRequest VisibleInjury(PersonKnowledgeRuntime runtime, string transactionId)
        {
            return new KnowledgeObservationRequest
            {
                PersonId = runtime.PersonId,
                TransactionId = transactionId,
                Proposition = VisibleInjuryData("body.test"),
                AcquisitionSource = KnowledgeAcquisitionSource.DirectObservation,
                Provenance = KnowledgeProvenance.DirectObservation,
                Direction = KnowledgeEvidenceDirection.Supports,
                Strength = KnowledgeConfidence.DefaultObservation,
                Credibility = KnowledgeConfidence.Maximum,
                Visibility = KnowledgeVisibility.Public
            };
        }

        private static KnowledgePropositionData VisibleInjuryData(string bodyId)
        {
            return new KnowledgePropositionData
            {
                factDefinitionId = BuiltInKnowledgeFacts.BodyInjury,
                subjectType = KnowledgeSubjectType.Body,
                subjectId = bodyId,
                valueType = KnowledgeValueType.StableId,
                stableValueId = "injury.visible-wound",
                bodyContextId = bodyId,
                sourceRevision = 1
            };
        }

        private static KnowledgeObservationRequest SpeciesCapability(PersonKnowledgeRuntime runtime, string transactionId, int strength, int credibility, KnowledgeEvidenceDirection direction = KnowledgeEvidenceDirection.Supports)
        {
            return new KnowledgeObservationRequest
            {
                PersonId = runtime.PersonId,
                TransactionId = transactionId,
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.SpeciesCapability,
                    subjectType = KnowledgeSubjectType.Species,
                    subjectId = "species.basic-spirit",
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = "capability.can.bleed"
                },
                AcquisitionSource = KnowledgeAcquisitionSource.Testimony,
                Provenance = KnowledgeProvenance.Testimony,
                Direction = direction,
                Strength = strength,
                Credibility = credibility,
                Visibility = KnowledgeVisibility.Public,
                TruthAuthorization = direction == KnowledgeEvidenceDirection.Corrects
                    ? KnowledgeTruthAuthorization.CreateDevelopmentFixture("test.knowledge.corrects")
                    : null
            };
        }

        private sealed class TestRuntime : IDisposable
        {
            private readonly GameObject gameObject;

            public TestRuntime(GameObject gameObject, PersonKnowledgeRuntime runtime)
            {
                this.gameObject = gameObject;
                Runtime = runtime;
            }

            public PersonKnowledgeRuntime Runtime { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
