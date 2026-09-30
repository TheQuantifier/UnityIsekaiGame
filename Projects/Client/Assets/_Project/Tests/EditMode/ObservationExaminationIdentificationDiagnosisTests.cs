using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology.Integration;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Observation;

namespace UnityIsekaiGame.Tests
{
    public sealed class ObservationExaminationIdentificationDiagnosisTests
    {
        [Test]
        public void ObservationPreviewDoesNotMutateKnowledge()
        {
            using TestRuntime fixture = CreateRuntime("person.observation.preview");
            ObservationService service = new ObservationService(fixture.Registry);

            ObservationResult result = service.Observe(fixture.Runtime, Context(fixture.Runtime, "tx.observation.preview", "observation-method.ordinary-visual"), VisibleInjuryProjection(), preview: true);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Code, Is.EqualTo(ObservationOutcomeCode.Preview));
            Assert.That(result.KnowledgeResult.Preview, Is.True);
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(0));
            Assert.That(fixture.Runtime.CreateSnapshot().Evidence.Count, Is.EqualTo(0));
        }

        [Test]
        public void ObservationExecutionRecordsEvidenceThroughKnowledgeRuntime()
        {
            using TestRuntime fixture = CreateRuntime("person.observation.execute");
            ObservationService service = new ObservationService(fixture.Registry);

            ObservationResult result = service.Observe(fixture.Runtime, Context(fixture.Runtime, "tx.observation.execute", "observation-method.ordinary-visual"), VisibleInjuryProjection(), preview: false);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Code, Is.EqualTo(ObservationOutcomeCode.Success));
            Assert.That(result.KnowledgeResult.ResultingBelief, Is.Not.Null);
            Assert.That(result.KnowledgeResult.ResultingBelief.State, Is.EqualTo(KnowledgeBeliefState.Believed));
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(1));
            Assert.That(fixture.Runtime.CreateSnapshot().Evidence.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateObservationUsesKnowledgeRuntimeTransactionProtection()
        {
            using TestRuntime fixture = CreateRuntime("person.observation.duplicate");
            ObservationService service = new ObservationService(fixture.Registry);
            ObservationContext context = Context(fixture.Runtime, "tx.observation.duplicate", "observation-method.ordinary-visual");

            ObservationResult first = service.Observe(fixture.Runtime, context, VisibleInjuryProjection(), preview: false);
            ObservationResult second = service.Observe(fixture.Runtime, context, VisibleInjuryProjection(), preview: false);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(second.Code, Is.EqualTo(ObservationOutcomeCode.Duplicate));
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(1));
            Assert.That(fixture.Runtime.CreateSnapshot().Evidence.Count, Is.EqualTo(1));
        }

        [Test]
        public void TrackingPoliciesCoverNpcPlayerRemoteAndDevelopmentObservers()
        {
            using TestRuntime npcFixture = CreateRuntime("person.observation.npc");
            ObservationResult npc = new ObservationService(npcFixture.Registry).Observe(
                npcFixture.Runtime,
                Context(npcFixture.Runtime, "tx.observation.npc", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.NpcFullTracking, mechanicallyRelevant: false),
                VisibleInjuryProjection(mechanicallyRelevant: false),
                preview: false);

            using TestRuntime playerFixture = CreateRuntime("person.observation.player-filter");
            ObservationResult player = new ObservationService(playerFixture.Registry).Observe(
                playerFixture.Runtime,
                Context(playerFixture.Runtime, "tx.observation.irrelevant", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.PlayerMechanicalOnly, mechanicallyRelevant: false),
                VisibleInjuryProjection(mechanicallyRelevant: false),
                preview: false);

            using TestRuntime remoteFixture = CreateRuntime("person.observation.remote");
            ObservationResult remote = new ObservationService(remoteFixture.Registry).Observe(
                remoteFixture.Runtime,
                Context(remoteFixture.Runtime, "tx.observation.remote", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.RemotePlayerMechanicalOnly, mechanicallyRelevant: false),
                VisibleInjuryProjection(mechanicallyRelevant: false),
                preview: false);

            using TestRuntime developmentFixture = CreateRuntime("person.observation.development");
            ObservationResult development = new ObservationService(developmentFixture.Registry).Observe(
                developmentFixture.Runtime,
                Context(developmentFixture.Runtime, "tx.observation.development", "observation-method.ordinary-visual", KnowledgeTrackingPolicy.DevelopmentObserverNoMutation),
                VisibleInjuryProjection(),
                preview: false);

            Assert.That(npc.Succeeded, Is.True, npc.Message);
            Assert.That(npc.Tracked, Is.True);
            Assert.That(npcFixture.Runtime.KnowledgeRevision, Is.EqualTo(1));
            Assert.That(player.Succeeded, Is.True, player.Message);
            Assert.That(player.Tracked, Is.False);
            Assert.That(playerFixture.Runtime.KnowledgeRevision, Is.EqualTo(0));
            Assert.That(remote.Succeeded, Is.True, remote.Message);
            Assert.That(remote.Tracked, Is.False);
            Assert.That(remoteFixture.Runtime.KnowledgeRevision, Is.EqualTo(0));
            Assert.That(development.Succeeded, Is.True, development.Message);
            Assert.That(development.Tracked, Is.False);
            Assert.That(developmentFixture.Runtime.KnowledgeRevision, Is.EqualTo(0));
        }

        [Test]
        public void SameTargetProducesObserverSpecificEvidenceAndQuality()
        {
            using TestRuntime novice = CreateRuntime("person.observation.novice");
            using TestRuntime expert = CreateRuntime("person.observation.expert");

            ObservationResult noviceResult = new ObservationService(novice.Registry).Observe(
                novice.Runtime,
                Context(novice.Runtime, "tx.observation.novice", "observation-method.ordinary-visual", expertiseQuality: 250),
                VisibleInjuryProjection(),
                preview: false);
            ObservationResult expertResult = new ObservationService(expert.Registry).Observe(
                expert.Runtime,
                Context(expert.Runtime, "tx.observation.expert", "observation-method.ordinary-visual", expertiseQuality: 950),
                VisibleInjuryProjection(),
                preview: false);

            Assert.That(noviceResult.Succeeded, Is.True, noviceResult.Message);
            Assert.That(expertResult.Succeeded, Is.True, expertResult.Message);
            Assert.That(expertResult.Quality, Is.GreaterThan(noviceResult.Quality));
            Assert.That(noviceResult.KnowledgeResult.Evidence.EvidenceId, Is.Not.EqualTo(expertResult.KnowledgeResult.Evidence.EvidenceId));
            Assert.That(novice.Runtime.KnowledgeRevision, Is.EqualTo(1));
            Assert.That(expert.Runtime.KnowledgeRevision, Is.EqualTo(1));
        }

        [Test]
        public void PrivacyAndConcealmentAreResolvedBeforeKnowledgeMutation()
        {
            using TestRuntime fixture = CreateRuntime("person.observation.privacy");
            ObservationService service = new ObservationService(fixture.Registry);
            ObservationContext clear = Context(fixture.Runtime, "tx.observation.clear", "observation-method.ordinary-visual");
            ObservationContext concealed = Context(fixture.Runtime, "tx.observation.concealed", "observation-method.ordinary-visual", concealment: ConcealmentState.Major);

            ObservationResult privateResult = service.Observe(fixture.Runtime, clear, VisibleInjuryProjection(KnowledgeVisibility.Private), preview: false);

            Assert.That(ObservationService.CalculateQuality(550, concealed, privacyBypass: false), Is.LessThan(ObservationService.CalculateQuality(550, clear, privacyBypass: false)));
            Assert.That(privateResult.Succeeded, Is.False);
            Assert.That(privateResult.Code, Is.EqualTo(ObservationOutcomeCode.AccessDenied));
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(0));
        }

        [Test]
        public void OrdinaryProjectionDtosDoNotHoldAuthoritativeStep7Snapshots()
        {
            Type forbidden = typeof(BodyBiologySnapshot);
            Type[] projectionTypes =
            {
                typeof(ObservableProjection),
                typeof(ExaminationProjection),
                typeof(DiagnosticProjection),
                typeof(KnowledgeObservationRequest)
            };

            foreach (Type type in projectionTypes)
            {
                bool containsForbidden = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(field => field.FieldType == forbidden)
                    || type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(property => property.PropertyType == forbidden);
                Assert.That(containsForbidden, Is.False, $"{type.Name} must not retain a full Step 7 body snapshot.");
            }
        }

        [Test]
        public void RepeatedIdenticalObservationIsBoundedBeyondTransactionDeduplication()
        {
            using TestRuntime fixture = CreateRuntime("person.observation.repeat");
            ObservationService service = new ObservationService(fixture.Registry);

            ObservationResult first = service.Observe(fixture.Runtime, Context(fixture.Runtime, "tx.observation.repeat-a", "observation-method.ordinary-visual"), VisibleInjuryProjection(), preview: false);
            int confidence = first.KnowledgeResult.ResultingBelief.Confidence;
            long revision = fixture.Runtime.KnowledgeRevision;
            ObservationResult second = service.Observe(fixture.Runtime, Context(fixture.Runtime, "tx.observation.repeat-b", "observation-method.ordinary-visual"), VisibleInjuryProjection(), preview: false);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(second.Code, Is.EqualTo(ObservationOutcomeCode.Duplicate));
            Assert.That(second.KnowledgeResult.ResultingBelief.Confidence, Is.EqualTo(confidence));
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(revision));
            Assert.That(fixture.Runtime.CreateSnapshot().Evidence.Count, Is.EqualTo(1));
        }

        [Test]
        public void StaleObservationProjectionIsRejectedBeforeKnowledgeMutation()
        {
            using TestRuntime fixture = CreateRuntime("person.observation.stale");
            ObservationService service = new ObservationService(fixture.Registry);

            ObservationResult result = service.Observe(
                fixture.Runtime,
                Context(fixture.Runtime, "tx.observation.stale", "observation-method.ordinary-visual", expectedConditionRevision: 2),
                VisibleInjuryProjection(sourceRevision: 1),
                preview: false);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ObservationOutcomeCode.StaleTarget));
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(0));
        }

        [Test]
        public void DiagnosisRetainsMultipleHypothesesAndUsesFamilyLevelResultBelowExactThreshold()
        {
            using TestRuntime fixture = CreateRuntime("person.observation.diagnosis");
            ObservationService service = new ObservationService(fixture.Registry);
            DiagnosticMethodDefinition method = Require<DiagnosticMethodDefinition>(fixture.Registry, "diagnostic-method.symptom-based");
            DiagnosticProjection projection = new DiagnosticProjection("projection.test.diagnosis", new[]
            {
                new DiagnosticHypothesis("condition.biology.prototype-poison", "condition-family.poison", 620, new[] { "symptom.nausea" }),
                new DiagnosticHypothesis("condition.biology.prototype-infection", "condition-family.infection", 480, new[] { "symptom.fever" }),
                new DiagnosticHypothesis("condition.biology.prototype-fatigue", "condition-family.fatigue", 360, new[] { "symptom.tired" })
            });

            ObservationResult result = service.Diagnose(fixture.Runtime, Context(fixture.Runtime, "tx.observation.diagnosis", method.Id, targetType: ObservationTargetType.BiologicalCondition, privateAccess: true), projection, method, preview: false);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.DiagnosticState, Is.EqualTo(DiagnosticResultState.Differential));
            Assert.That(result.Hypotheses.Count, Is.EqualTo(3));
            Assert.That(result.Hypotheses[0].CandidateId, Is.EqualTo("condition.biology.prototype-poison"));
            Assert.That(result.KnowledgeResult.ResultingBelief.Proposition.Data.stableValueId, Is.EqualTo("condition-family.poison"));
            Assert.That(fixture.Runtime.CreateSnapshot().Evidence.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExaminationUsesAuthoredMethodAndCommitsThroughKnowledgeRuntime()
        {
            using TestRuntime fixture = CreateRuntime("person.observation.examination");
            ObservationService service = new ObservationService(fixture.Registry);
            ExaminationMethodDefinition method = Require<ExaminationMethodDefinition>(fixture.Registry, "examination-method.medical");
            ExaminationProjection examination = new ExaminationProjection("examination.test.medical", new[] { VisibleInjuryProjection(KnowledgeVisibility.PersonallyObservable) }, new[] { "injury", "medical" });

            ObservationResult result = service.Examine(fixture.Runtime, Context(fixture.Runtime, "tx.observation.examination", method.Id, privateAccess: true, expertiseQuality: 800), examination, method, preview: false);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.MethodId, Is.EqualTo("examination-method.medical"));
            Assert.That(result.KnowledgeResult.ResultingBelief, Is.Not.Null);
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(1));
        }

        [Test]
        public void IdentificationUsesAuthoredThresholdsAndSharedKnowledgeCommit()
        {
            using TestRuntime fixture = CreateRuntime("person.observation.identification");
            ObservationService service = new ObservationService(fixture.Registry);
            IdentificationMethodDefinition method = Require<IdentificationMethodDefinition>(fixture.Registry, "identification-method.species");

            ObservationResult result = service.Identify(fixture.Runtime, Context(fixture.Runtime, "tx.observation.identification", method.Id, targetType: ObservationTargetType.Body, expertiseQuality: 800), SpeciesProjection(), method, preview: false);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.IdentificationState, Is.EqualTo(IdentificationResultState.Exact));
            Assert.That(result.KnowledgeResult.ResultingBelief.Proposition.Data.stableValueId, Is.EqualTo("species.human"));
        }

        [Test]
        public void SpeciesIdentificationDoesNotGrantPersistentPersonRecognition()
        {
            using TestRuntime fixture = CreateRuntime("person.observation.recognition");
            ObservationService service = new ObservationService(fixture.Registry);
            IdentificationMethodDefinition method = Require<IdentificationMethodDefinition>(fixture.Registry, "identification-method.species");

            ObservationResult result = service.Identify(fixture.Runtime, Context(fixture.Runtime, "tx.observation.species-only", method.Id, targetType: ObservationTargetType.Body, expertiseQuality: 800), SpeciesProjection(), method, preview: false);
            KnowledgeSnapshot snapshot = fixture.Runtime.CreateSnapshot();

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(snapshot.Beliefs.Any(record => string.Equals(record.Proposition.FactDefinitionId, BuiltInKnowledgeFacts.SpeciesIdentity, StringComparison.Ordinal)), Is.True);
            Assert.That(snapshot.Beliefs.Any(record => string.Equals(record.Proposition.FactDefinitionId, BuiltInKnowledgeFacts.PersonIdentity, StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void InactiveFoundationMethodsCannotExecute()
        {
            using TestRuntime fixture = CreateRuntime("person.observation.inactive");
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[]
            {
                Fact(BuiltInKnowledgeFacts.BodyInjury, "Body Injury", KnowledgeDomain.Medical, KnowledgePropositionType.Injury, KnowledgeSubjectType.Body, KnowledgeValueType.StableId, KnowledgeVisibility.PersonallyObservable),
                ObservationMethod("observation-method.magical-analysis-foundation", active: false)
            });
            ObservationService service = new ObservationService(registry);

            ObservationResult result = service.Observe(fixture.Runtime, Context(fixture.Runtime, "tx.observation.inactive", "observation-method.magical-analysis-foundation"), VisibleInjuryProjection(), preview: false);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ObservationOutcomeCode.MissingMethod));
            Assert.That(fixture.Runtime.KnowledgeRevision, Is.EqualTo(0));
        }

        private static TestRuntime CreateRuntime(string personId)
        {
            DefinitionRegistry registry = Registry();
            GameObject gameObject = new GameObject($"Observation Test - {personId}");
            PersonKnowledgeRuntime runtime = gameObject.AddComponent<PersonKnowledgeRuntime>();
            runtime.Configure(registry, personId, actorId: "actor.test", bodyId: "body.test");
            return new TestRuntime(gameObject, runtime, registry);
        }

        private static DefinitionRegistry Registry()
        {
            return new DefinitionRegistry(new IGameDefinition[]
            {
                Fact(BuiltInKnowledgeFacts.SpeciesIdentity, "Species Identity", KnowledgeDomain.Species, KnowledgePropositionType.Identity, KnowledgeSubjectType.Body, KnowledgeValueType.StableId),
                Fact(BuiltInKnowledgeFacts.BodyInjury, "Body Injury", KnowledgeDomain.Medical, KnowledgePropositionType.Injury, KnowledgeSubjectType.Body, KnowledgeValueType.StableId, KnowledgeVisibility.PersonallyObservable),
                Fact(BuiltInKnowledgeFacts.BodySymptom, "Body Symptom", KnowledgeDomain.Medical, KnowledgePropositionType.Symptom, KnowledgeSubjectType.Body, KnowledgeValueType.StableId, KnowledgeVisibility.Private),
                Fact(BuiltInKnowledgeFacts.PersonIdentity, "Person Identity", KnowledgeDomain.Personal, KnowledgePropositionType.Identity, KnowledgeSubjectType.Person, KnowledgeValueType.StableId),
                ObservationMethod(),
                ExaminationMethod(),
                IdentificationMethod(),
                DiagnosticMethod()
            });
        }

        private static ObservationContext Context(
            PersonKnowledgeRuntime runtime,
            string transactionId,
            string methodId,
            KnowledgeTrackingPolicy trackingPolicy = KnowledgeTrackingPolicy.PlayerMechanicalOnly,
            bool mechanicallyRelevant = true,
            ConcealmentState concealment = ConcealmentState.None,
            ObservationTargetType targetType = ObservationTargetType.Body,
            bool privateAccess = false,
            int expertiseQuality = 650,
            long expectedConditionRevision = 1,
            long expectedBodyRevision = 1)
        {
            return new ObservationContext(
                runtime.PersonId,
                transactionId,
                methodId,
                SensoryChannel.Vision,
                targetType,
                "body.test",
                observerActorId: "actor.test",
                observerBodyId: "body.test",
                targetBodyId: "body.test",
                distanceQuality: 900,
                visibility: ObservationVisibilityState.Clear,
                concealment: concealment,
                accessLevel: privateAccess ? ObservationAccessLevel.Medical : ObservationAccessLevel.Public,
                consent: privateAccess ? ObservationConsentState.Granted : ObservationConsentState.NotRequired,
                environmentalQuality: 900,
                lightingQuality: 900,
                noiseQuality: 900,
                obstructionQuality: 900,
                expertiseQuality: expertiseQuality,
                toolQuality: 700,
                trackingPolicy: trackingPolicy,
                mechanicallyRelevant: mechanicallyRelevant,
                privateAccessAuthorized: privateAccess,
                expectedBodyRevision: expectedBodyRevision,
                expectedConditionRevision: expectedConditionRevision);
        }

        private static ObservableProjection VisibleInjuryProjection(KnowledgeVisibility visibility = KnowledgeVisibility.Public, bool mechanicallyRelevant = true, long sourceRevision = 1)
        {
            return new ObservableProjection(
                "projection.test.visible-injury",
                ObservationTargetType.Body,
                new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.BodyInjury,
                    subjectType = KnowledgeSubjectType.Body,
                    subjectId = "body.test",
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = "injury.visible-wound",
                    bodyContextId = "body.test",
                    sourceRevision = sourceRevision
                },
                visibility,
                300,
                700,
                new[] { SensoryChannel.Vision },
                mechanicallyRelevant);
        }

        private static ObservableProjection SpeciesProjection()
        {
            return new ObservableProjection(
                "projection.test.species",
                ObservationTargetType.Body,
                new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.SpeciesIdentity,
                    subjectType = KnowledgeSubjectType.Body,
                    subjectId = "body.test",
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = "species.human",
                    bodyContextId = "body.test",
                    sourceRevision = 1
                },
                KnowledgeVisibility.Public,
                300,
                700,
                new[] { SensoryChannel.Vision });
        }

        private static KnowledgeFactDefinition Fact(string id, string displayName, KnowledgeDomain domain, KnowledgePropositionType propositionType, KnowledgeSubjectType subjectType, KnowledgeValueType valueType, KnowledgeVisibility visibility = KnowledgeVisibility.Public)
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
            Set(definition, "certaintyThreshold", 700);
            Set(definition, "requiredEvidenceCount", 1);
            return definition;
        }

        private static ObservationMethodDefinition ObservationMethod()
        {
            return ObservationMethod("observation-method.ordinary-visual", active: true);
        }

        private static ObservationMethodDefinition ObservationMethod(string id, bool active)
        {
            ObservationMethodDefinition definition = ScriptableObject.CreateInstance<ObservationMethodDefinition>();
            definition.name = "Ordinary Visual Observation";
            Set(definition, "methodId", id);
            Set(definition, "displayName", "Ordinary Visual Observation");
            Set(definition, "category", ObservationMethodCategory.OrdinaryVisualObservation);
            Set(definition, "sensoryChannels", new[] { SensoryChannel.Vision });
            Set(definition, "targetTypes", new[] { ObservationTargetType.Body });
            Set(definition, "active", active);
            Set(definition, "baseObservationQuality", 550);
            Set(definition, "evidenceStrengthMultiplier", 1000);
            Set(definition, "defaultTrackingPolicy", KnowledgeTrackingPolicy.PlayerMechanicalOnly);
            return definition;
        }

        private static IdentificationMethodDefinition IdentificationMethod()
        {
            IdentificationMethodDefinition definition = ScriptableObject.CreateInstance<IdentificationMethodDefinition>();
            definition.name = "Species Identification";
            Set(definition, "methodId", "identification-method.species");
            Set(definition, "displayName", "Species Identification");
            Set(definition, "category", IdentificationMethodCategory.Species);
            Set(definition, "active", true);
            Set(definition, "targetType", ObservationTargetType.Body);
            Set(definition, "factDefinitionId", BuiltInKnowledgeFacts.SpeciesIdentity);
            Set(definition, "partialThreshold", 350);
            Set(definition, "exactThreshold", 700);
            return definition;
        }

        private static ExaminationMethodDefinition ExaminationMethod()
        {
            ExaminationMethodDefinition definition = ScriptableObject.CreateInstance<ExaminationMethodDefinition>();
            definition.name = "Medical Examination";
            Set(definition, "methodId", "examination-method.medical");
            Set(definition, "displayName", "Medical Examination");
            Set(definition, "category", ExaminationMethodCategory.MedicalExamination);
            Set(definition, "active", true);
            Set(definition, "targetType", ObservationTargetType.Body);
            Set(definition, "requiredAccess", ObservationAccessLevel.Medical);
            Set(definition, "basePrecision", 700);
            Set(definition, "privacyClassification", KnowledgeVisibility.PersonallyObservable);
            return definition;
        }

        private static DiagnosticMethodDefinition DiagnosticMethod()
        {
            DiagnosticMethodDefinition definition = ScriptableObject.CreateInstance<DiagnosticMethodDefinition>();
            definition.name = "Symptom Based Diagnosis";
            Set(definition, "methodId", "diagnostic-method.symptom-based");
            Set(definition, "displayName", "Symptom Based Diagnosis");
            Set(definition, "category", DiagnosticMethodCategory.SymptomBasedDiagnosis);
            Set(definition, "active", true);
            Set(definition, "factDefinitionId", BuiltInKnowledgeFacts.BodySymptom);
            Set(definition, "confidenceCeiling", 800);
            Set(definition, "exactDiagnosisThreshold", 720);
            Set(definition, "differentialHypothesisThreshold", 300);
            Set(definition, "requiredAccess", ObservationAccessLevel.Medical);
            return definition;
        }

        private static T Require<T>(DefinitionRegistry registry, string id)
            where T : class, IGameDefinition
        {
            Assert.That(registry.TryGet(id, out T definition), Is.True, id);
            return definition;
        }

        private static void Set<TTarget, TValue>(TTarget target, string fieldName, TValue value)
        {
            FieldInfo field = typeof(TTarget).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class TestRuntime : IDisposable
        {
            private readonly GameObject gameObject;

            public TestRuntime(GameObject gameObject, PersonKnowledgeRuntime runtime, DefinitionRegistry registry)
            {
                this.gameObject = gameObject;
                Runtime = runtime;
                Registry = registry;
            }

            public PersonKnowledgeRuntime Runtime { get; }
            public DefinitionRegistry Registry { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
