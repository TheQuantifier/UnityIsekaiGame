using System;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Sources;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class InformationSourcesReliabilityTests
    {
        [Test]
        public void SourceDefinition_ValidatesCanonicalIdAndCategory()
        {
            InformationSourceDefinition definition = SourceDefinition("information-source.test.direct", InformationSourceCategory.DirectObservation, 850);
            DefinitionValidationReport report = new DefinitionValidationReport();

            definition.ValidateCatalogDefinition(new System.Collections.Generic.Dictionary<string, IGameDefinition>(), report);

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.GetSummary());
            Assert.That(report.WarningCount, Is.EqualTo(0), report.GetSummary());
        }

        [Test]
        public void RegistersSourceInstancesAgainstCanonicalCategoryDefinition()
        {
            InformationSourceRuntime runtime = Runtime();

            InformationSourceOperationResult result = runtime.RegisterSource(Register("tx.direct", "information-source.runtime.direct", InformationSourceCategory.DirectObservation, InformationSourceReferenceType.Body, "body.a"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(runtime.CreateSnapshot().Sources.Count, Is.EqualTo(1));
            Assert.That(runtime.CreateSnapshot().Sources[0].Data.sourceDefinitionId, Is.EqualTo("information-source.test.directobservation"));
        }

        [Test]
        public void PersonRelativeAssessmentsChangeReliabilityForSameSource()
        {
            InformationSourceRuntime runtime = Runtime();
            runtime.RegisterSource(Register("tx.source", "information-source.runtime.expert", InformationSourceCategory.ExpertTestimony, InformationSourceReferenceType.Person, "person.expert"));
            runtime.AssessSource(Assessment("tx.player", "assessment.player", "person.player", "information-source.runtime.expert", 900, 80, 50, 100));
            runtime.AssessSource(Assessment("tx.rival", "assessment.rival", "person.rival", "information-source.runtime.expert", 300, 700, 650, 600));

            SourceReliabilityResult player = runtime.EvaluateReliability(Evaluate("person.player", "information-source.runtime.expert"));
            SourceReliabilityResult rival = runtime.EvaluateReliability(Evaluate("person.rival", "information-source.runtime.expert"));

            Assert.That(player.Succeeded, Is.True, player.Message);
            Assert.That(rival.Succeeded, Is.True, rival.Message);
            Assert.That(player.DerivedOverall, Is.GreaterThan(rival.DerivedOverall));
        }

        [Test]
        public void SourceChainsPreserveOriginalWithoutDuplicatingEvidencePayload()
        {
            InformationSourceRuntime runtime = Runtime();
            runtime.RegisterSource(Register("tx.record", "information-source.runtime.record", InformationSourceCategory.OfficialRecord, InformationSourceReferenceType.Document, "document.record"));
            runtime.TransformSource(new SourceTransformationRequest
            {
                TransactionId = "tx.copy",
                ParentSourceId = "information-source.runtime.record",
                SourceInstanceId = "information-source.runtime.copy",
                TransformationType = InformationSourceTransformationType.Copy,
                ActorPersonId = "person.copyist",
                WorldTimeSeconds = 20d,
                Quality = 800
            });

            SourceChainSnapshot chain = runtime.TraceSourceChain("information-source.runtime.copy", privilegedAccess: true);

            Assert.That(chain.TransmissionDepth, Is.EqualTo(1));
            Assert.That(chain.ImmediateSourceId, Is.EqualTo("information-source.runtime.copy"));
            Assert.That(chain.OriginalSourceId, Is.EqualTo("information-source.runtime.record"));
            Assert.That(runtime.CompareIndependence("information-source.runtime.record", "information-source.runtime.copy"), Is.EqualTo(SourceIndependenceState.Dependent));
        }

        [Test]
        public void AgeTransmissionAndRiskRemainSeparateDimensions()
        {
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { SourceDefinition("information-source.test.record", InformationSourceCategory.HistoricalRecord, 800, KnowledgeStalenessPolicy.TimeLimited, 1000d) });
            InformationSourceRuntime runtime = Runtime(registry);
            InformationSourceRegistrationRequest request = Register("tx.old", "information-source.runtime.old-record", InformationSourceCategory.HistoricalRecord, InformationSourceReferenceType.Document, "document.old");
            request.SourceDefinitionId = "information-source.test.record";
            request.CreationWorldTimeSeconds = 0d;
            request.ObservationWorldTimeSeconds = 0d;
            request.TransmissionWorldTimeSeconds = 0d;
            request.ErrorRisk = 450;
            runtime.RegisterSource(request);

            SourceReliabilityResult now = runtime.EvaluateReliability(Evaluate("person.player", "information-source.runtime.old-record", 0d));
            SourceReliabilityResult later = runtime.EvaluateReliability(Evaluate("person.player", "information-source.runtime.old-record", 5000d));

            Assert.That(later.FinalDimensions.recency, Is.LessThan(now.FinalDimensions.recency));
            Assert.That(later.FinalDimensions.errorRisk, Is.EqualTo(450));
            Assert.That(later.FinalDimensions.transmissionIntegrity, Is.EqualTo(now.FinalDimensions.transmissionIntegrity));
        }

        [Test]
        public void RawEvidenceStrengthAndEffectiveSourceContributionStaySeparate()
        {
            InformationSourceRuntime sources = Runtime();
            sources.RegisterSource(Register("tx.source", "information-source.runtime.rumor", InformationSourceCategory.Hearsay, InformationSourceReferenceType.Person, "person.rumor"));
            sources.AssessSource(Assessment("tx.assess", "assessment.rumor", "person.player", "information-source.runtime.rumor", 400, 700, 650, 600));
            SourceReliabilityResult reliability = sources.EvaluateReliability(Evaluate("person.player", "information-source.runtime.rumor"));
            int effective = sources.CalculateEffectiveEvidenceStrength(900, reliability);

            using TestKnowledgeRuntime fixture = CreateKnowledgeRuntime();
            KnowledgeOperationResult result = fixture.Runtime.RecordObservation(new KnowledgeObservationRequest
            {
                PersonId = fixture.Runtime.PersonId,
                TransactionId = "tx.knowledge.source",
                Proposition = SpeciesCapability(),
                AcquisitionSource = KnowledgeAcquisitionSource.Testimony,
                Provenance = KnowledgeProvenance.Testimony,
                Direction = KnowledgeEvidenceDirection.Supports,
                Strength = 900,
                EffectiveStrengthOverride = effective,
                Credibility = reliability.DerivedOverall,
                SourceId = "person.rumor",
                InformationSourceId = "information-source.runtime.rumor",
                ReliabilityPolicyId = reliability.Request.PolicyId,
                ReliabilityEvaluationId = "source-reliability.test"
            });

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Evidence.RawStrength, Is.EqualTo(900));
            Assert.That(result.Evidence.EffectiveStrength, Is.EqualTo(effective));
            Assert.That(result.Evidence.InformationSourceId, Is.EqualTo("information-source.runtime.rumor"));
            Assert.That(result.ResultingBelief.Confidence, Is.LessThan(900));
        }

        [Test]
        public void SaveRestoreRejectsCorruptPayloadWithoutPartialMutation()
        {
            DefinitionRegistry registry = Registry();
            InformationSourceRuntime runtime = Runtime(registry);
            runtime.RegisterSource(Register("tx.good", "information-source.runtime.good", InformationSourceCategory.DirectObservation, InformationSourceReferenceType.Body, "body.good"));
            InformationSourceSaveData corrupt = runtime.CreateSaveData();
            corrupt.sources[0].sourceInstanceId = "";
            long beforeRevision = runtime.SourceRevision;
            int beforeCount = runtime.CreateSnapshot().Sources.Count;

            InformationSourceOperationResult result = runtime.RestoreFromSaveData(corrupt, registry, "person.player", restoring: true);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(runtime.SourceRevision, Is.EqualTo(beforeRevision));
            Assert.That(runtime.CreateSnapshot().Sources.Count, Is.EqualTo(beforeCount));
        }

        [Test]
        public void PersistenceParticipantPrepareCommitRestoresSilently()
        {
            DefinitionRegistry registry = Registry();
            InformationSourceRuntime runtime = Runtime(registry);
            runtime.RegisterSource(Register("tx.source", "information-source.runtime.persisted", InformationSourceCategory.DirectObservation, InformationSourceReferenceType.Body, "body.persisted"));
            InformationSourcePersistenceParticipant participant = new InformationSourcePersistenceParticipant(runtime, () => registry, "person.player");
            int events = 0;
            runtime.SourcesChanged += (_, __) => events++;

            PersistenceParticipantSaveResult save = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepare = participant.PreparePayload(save.PayloadJson, participant.ParticipantSchemaVersion);
            PersistenceParticipantCommitResult commit = participant.CommitPreparedPayload(prepare.PreparedPayload);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(events, Is.EqualTo(0));
        }

        [Test]
        public void SnapshotsAreImmutableAndPrivateChainCanHideOriginal()
        {
            InformationSourceRuntime runtime = Runtime();
            runtime.RegisterSource(Register("tx.record", "information-source.runtime.hidden-record", InformationSourceCategory.OfficialRecord, InformationSourceReferenceType.Document, "document.secret"));
            runtime.TransformSource(new SourceTransformationRequest
            {
                TransactionId = "tx.hidden-summary",
                ParentSourceId = "information-source.runtime.hidden-record",
                SourceInstanceId = "information-source.runtime.hidden-summary",
                TransformationType = InformationSourceTransformationType.Summary,
                HidesOriginal = true,
                WorldTimeSeconds = 10d
            });

            InformationSourceSnapshot snapshot = runtime.CreateSnapshot();
            snapshot.Sources[0].Data.sourceInstanceId = "mutated";

            Assert.That(runtime.TryGetSource("mutated", out _), Is.False);
            Assert.That(runtime.TraceSourceChain("information-source.runtime.hidden-summary", privilegedAccess: false).OriginalHidden, Is.True);
            Assert.That(runtime.TraceSourceChain("information-source.runtime.hidden-summary", privilegedAccess: true).OriginalSourceId, Is.EqualTo("information-source.runtime.hidden-record"));
        }

        private static InformationSourceRuntime Runtime(DefinitionRegistry registry = null)
        {
            InformationSourceRuntime runtime = new InformationSourceRuntime();
            runtime.Configure(registry ?? Registry(), "person.player");
            return runtime;
        }

        private static DefinitionRegistry Registry()
        {
            return KnowledgeTestDefinitionFactory.AddSourceDefinitions(
                new IGameDefinition[]
            {
                Fact(BuiltInKnowledgeFacts.SpeciesCapability, KnowledgeDomain.Species, KnowledgePropositionType.Capability, KnowledgeSubjectType.Species, KnowledgeValueType.StableId)
            });
        }

        private static InformationSourceDefinition SourceDefinition(string id, InformationSourceCategory category, int dependability, KnowledgeStalenessPolicy policy = KnowledgeStalenessPolicy.NeverStale, double halfLife = 0d)
        {
            InformationSourceDefinition definition = ScriptableObject.CreateInstance<InformationSourceDefinition>();
            ReliabilityProfileData reliability = ReliabilityProfileData.Default();
            reliability.generalDependability = dependability;
            reliability.domainExpertise = dependability;
            reliability.methodQuality = dependability;
            reliability.authenticity = dependability;
            reliability.identityCertainty = dependability;
            reliability.recordIntegrity = dependability;
            reliability.errorRisk = Math.Max(0, 1000 - dependability);
            definition.DevelopmentConfigure(id, id, category, reliability, policy, halfLife);
            return definition;
        }

        private static InformationSourceRegistrationRequest Register(string transactionId, string sourceId, InformationSourceCategory category, InformationSourceReferenceType referenceType, string referencedId)
        {
            return new InformationSourceRegistrationRequest
            {
                TransactionId = transactionId,
                SourceInstanceId = sourceId,
                Category = category,
                ReferenceType = referenceType,
                ReferencedId = referencedId,
                OriginalCreatorPersonId = category == InformationSourceCategory.DirectObservation ? "person.player" : "person.source",
                ObserverPersonId = category == InformationSourceCategory.DirectObservation ? "person.player" : string.Empty,
                HolderPersonId = "person.player",
                CreationWorldTimeSeconds = 10d,
                ObservationWorldTimeSeconds = 10d,
                TransmissionWorldTimeSeconds = 10d,
                Domain = KnowledgeDomain.Medical,
                SubjectId = referencedId,
                MethodId = "method.test",
                ErrorRisk = 200,
                DeceptionRisk = 100,
                BiasRisk = 100
            };
        }

        private static SourceAssessmentRequest Assessment(string transactionId, string assessmentId, string personId, string sourceId, int dependability, int errorRisk, int deceptionRisk, int biasRisk)
        {
            ReliabilityProfileData reliability = ReliabilityProfileData.Default();
            reliability.generalDependability = dependability;
            reliability.domainExpertise = dependability;
            reliability.methodQuality = dependability;
            reliability.authenticity = dependability;
            reliability.identityCertainty = dependability;
            reliability.observationQuality = dependability;
            reliability.recordIntegrity = dependability;
            reliability.errorRisk = errorRisk;
            reliability.deceptionRisk = deceptionRisk;
            reliability.biasRisk = biasRisk;
            reliability.completeness = dependability;
            reliability.precision = dependability;
            reliability.contextFit = dependability;
            return new SourceAssessmentRequest
            {
                TransactionId = transactionId,
                AssessmentId = assessmentId,
                AssessingPersonId = personId,
                SourceInstanceId = sourceId,
                Domain = KnowledgeDomain.Medical,
                SubjectId = "body.a",
                MethodId = "method.test",
                WorldTimeSeconds = 20d,
                Reliability = reliability,
                Authority = dependability,
                ErrorRisk = errorRisk,
                DeceptionRisk = deceptionRisk,
                BiasRisk = biasRisk,
                ConfidenceInAssessment = 900
            };
        }

        private static SourceReliabilityRequest Evaluate(string personId, string sourceId, double time = 100d)
        {
            return new SourceReliabilityRequest
            {
                EvaluatingPersonId = personId,
                SourceInstanceId = sourceId,
                Domain = KnowledgeDomain.Medical,
                SubjectId = "body.a",
                MethodId = "method.test",
                WorldTimeSeconds = time,
                PrivilegedAccess = true
            };
        }

        private static KnowledgePropositionData SpeciesCapability()
        {
            return new KnowledgePropositionData
            {
                factDefinitionId = BuiltInKnowledgeFacts.SpeciesCapability,
                subjectType = KnowledgeSubjectType.Species,
                subjectId = "species.basic-spirit",
                valueType = KnowledgeValueType.StableId,
                stableValueId = "capability.can.bleed"
            };
        }

        private static KnowledgeFactDefinition Fact(string id, KnowledgeDomain domain, KnowledgePropositionType type, KnowledgeSubjectType subject, KnowledgeValueType valueType)
        {
            KnowledgeFactDefinition definition = ScriptableObject.CreateInstance<KnowledgeFactDefinition>();
            definition.name = id;
            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(definition);
            serialized.FindProperty("factId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("domain").enumValueIndex = (int)domain;
            serialized.FindProperty("propositionType").enumValueIndex = (int)type;
            serialized.FindProperty("subjectType").enumValueIndex = (int)subject;
            serialized.FindProperty("valueType").enumValueIndex = (int)valueType;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static TestKnowledgeRuntime CreateKnowledgeRuntime()
        {
            GameObject gameObject = new GameObject("Information Source Knowledge Test");
            PersonKnowledgeRuntime runtime = gameObject.AddComponent<PersonKnowledgeRuntime>();
            runtime.Configure(Registry(), "person.player");
            return new TestKnowledgeRuntime(gameObject, runtime);
        }

        private sealed class TestKnowledgeRuntime : IDisposable
        {
            public TestKnowledgeRuntime(GameObject gameObject, PersonKnowledgeRuntime runtime)
            {
                GameObject = gameObject;
                Runtime = runtime;
            }

            private GameObject GameObject { get; }
            public PersonKnowledgeRuntime Runtime { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(GameObject);
            }
        }
    }
}
