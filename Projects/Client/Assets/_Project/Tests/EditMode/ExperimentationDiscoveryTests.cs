using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Experimentation;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Tests
{
    public sealed class ExperimentationDiscoveryTests
    {
        [Test]
        public void ExperimentDefinition_ValidatesControlsVariablesAndPolicies()
        {
            ExperimentDefinition definition = ExperimentDefinitionFixture();
            DefinitionValidationReport report = new DefinitionValidationReport();

            definition.ValidateCatalogDefinition(new System.Collections.Generic.Dictionary<string, IGameDefinition>
            {
                [definition.Id] = definition
            }, report);

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(definition.Variables.Count, Is.EqualTo(2));
            Assert.That(definition.RequiredControls.Count, Is.EqualTo(1));
        }

        [Test]
        public void ControlledPlanPreviewDoesNotMutateExperimentRuntime()
        {
            ExperimentationRuntime runtime = new ExperimentationRuntime();
            ExperimentDefinition definition = ExperimentDefinitionFixture();
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { definition });
            ExperimentationResult hypothesis = runtime.CreateHypothesis(Hypothesis("hypothesis.preview"));
            long before = runtime.Revision;

            ExperimentationResult preview = runtime.CreatePlan(Plan("plan.preview", definition.Id, hypothesis.Hypothesis.hypothesisId), registry, preview: true);
            ExperimentationResult execute = runtime.CreatePlan(Plan("plan.preview", definition.Id, hypothesis.Hypothesis.hypothesisId), registry);

            Assert.That(preview.Preview, Is.True);
            Assert.That(runtime.PlanCount, Is.EqualTo(1));
            Assert.That(execute.Succeeded, Is.True);
            Assert.That(before, Is.EqualTo(hypothesis.Hypothesis == null ? -1 : before));
        }

        [Test]
        public void EvidenceInferenceClaimAndRegistrationRemainDistinct()
        {
            ExperimentationRuntime runtime = new ExperimentationRuntime();
            ExperimentDefinition definition = ExperimentDefinitionFixture();
            KnowledgeFactDefinition fact = FactDefinitionFixture();
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { definition, fact });
            using KnowledgeFixture knowledge = new KnowledgeFixture(registry);

            ExperimentationResult hypothesis = runtime.CreateHypothesis(Hypothesis("hypothesis.discovery"));
            ExperimentationResult plan = runtime.CreatePlan(Plan("plan.discovery", definition.Id, hypothesis.Hypothesis.hypothesisId), registry);
            ExperimentationResult run = runtime.StartRun("run.discovery", plan.Plan.planId, knowledge.PersonId, "1", registry);
            ExperimentationResult trial = runtime.RecordTrial(new ExperimentTrialData
            {
                trialId = "trial.discovery.1",
                experimentRunId = run.Run.experimentRunId,
                trialIndex = 0,
                deterministicSeed = "seed.discovery.1",
                outcome = ExperimentTrialOutcome.ExpectedSuccess
            });
            KnowledgeObservationRequest observation = Observation(knowledge.PersonId, "knowledge.discovery.1", "evidence.discovery.1");
            ExperimentationResult evidence = runtime.GenerateEvidence(
                run.Run.experimentRunId,
                trial.Trial.trialId,
                hypothesis.Hypothesis.hypothesisId,
                observation,
                knowledge.Runtime,
                ExperimentEvidenceRole.Supporting);
            ExperimentationResult inference = runtime.RecordInference(new ExperimentInferenceData
            {
                inferenceId = "inference.discovery.1",
                experimentRunId = run.Run.experimentRunId,
                inferenceType = ExperimentInferenceType.RecipeFragment,
                subjectId = "recipe.prototype.experimental",
                inferredDefinitionId = "recipe-input.prototype.iron",
                evidenceIds = evidence.Run.evidenceIds,
                confidence = 760
            });
            ExperimentationResult claim = runtime.CreateDiscoveryClaim(new DiscoveryClaimData
            {
                claimId = "claim.discovery.1",
                experimentRunId = run.Run.experimentRunId,
                inferenceId = inference.Inference.inferenceId,
                hypothesisId = hypothesis.Hypothesis.hypothesisId,
                evidenceIds = evidence.Run.evidenceIds,
                supportCount = 1,
                independentReproductionCount = 1,
                confidence = 760
            }, new ExperimentPolicyData { confirmationEvidenceThreshold = 1, independentReproductionThreshold = 1 });

            Assert.That(evidence.Succeeded, Is.True, evidence.Message);
            Assert.That(inference.Succeeded, Is.True, inference.Message);
            Assert.That(claim.Succeeded, Is.True, claim.Message);
            Assert.That(runtime.InferenceCount, Is.EqualTo(1));
            Assert.That(runtime.ClaimCount, Is.EqualTo(1));
            Assert.That(knowledge.Runtime.TryGetBelief(observation.Proposition, out KnowledgeBeliefRecord belief), Is.True);
            Assert.That(belief.State, Is.EqualTo(KnowledgeBeliefState.StronglyBelieved));
            Assert.That(runtime.RegistrationProposalCount, Is.Zero);
        }

        [Test]
        public void AccessProjectionRedactsExperimentDetails()
        {
            ExperimentationRuntime runtime = new ExperimentationRuntime();
            ExperimentDefinition definition = ExperimentDefinitionFixture();
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { definition });
            ExperimentationResult hypothesis = runtime.CreateHypothesis(Hypothesis("hypothesis.access"));
            ExperimentationResult plan = runtime.CreatePlan(Plan("plan.access", definition.Id, hypothesis.Hypothesis.hypothesisId), registry);
            runtime.StartRun("run.access", plan.Plan.planId, "person.owner", "1", registry);
            runtime.RecordTrial(new ExperimentTrialData { trialId = "trial.access.1", experimentRunId = "run.access", outcome = ExperimentTrialOutcome.ExpectedSuccess });
            InformationAccessRuntime access = new InformationAccessRuntime();
            access.Configure(registry, "person.owner");
            access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = "policy.experiment.access",
                subject = ExperimentInformationSubject.Experiment("run.access", definition.Id),
                classification = InformationVisibilityClassification.Confidential,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Redacted,
                allowedPersonIds = new[] { "person.observer" },
                defaultVisibleDetails = new[] { "detail.run" },
                defaultRedactedDetails = new[] { "detail.trials", "detail.evidence" }
            });

            ExperimentProjectionData projection = runtime.ProjectRun("run.access", access, new InformationAccessContext { RequestingPersonId = "person.observer", RedactedAccessAcceptable = true }, "policy.experiment.access");

            Assert.That(projection.Decision, Is.EqualTo(ExperimentProjectionDecision.RedactedAccess));
            Assert.That(projection.Run, Is.Not.Null);
            Assert.That(projection.VisibleTrialIds, Is.Empty);
        }

        [Test]
        public void RestoreRejectsBrokenReferencesWithoutMutatingLiveRuntime()
        {
            ExperimentationRuntime runtime = new ExperimentationRuntime();
            ExperimentDefinition definition = ExperimentDefinitionFixture();
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { definition });
            ExperimentationResult hypothesis = runtime.CreateHypothesis(Hypothesis("hypothesis.restore"));
            ExperimentationResult plan = runtime.CreatePlan(Plan("plan.restore", definition.Id, hypothesis.Hypothesis.hypothesisId), registry);
            runtime.StartRun("run.restore", plan.Plan.planId, "person.owner", "1", registry);
            ExperimentationRuntimeSaveData good = runtime.CreateSaveData();
            ExperimentationRuntimeSaveData broken = good.Clone();
            broken.runs[0].planId = "plan.missing";

            ExperimentationResult restore = runtime.RestoreFromSaveData(broken, registry);

            Assert.That(restore.Succeeded, Is.False);
            Assert.That(runtime.RunCount, Is.EqualTo(1));
            Assert.That(runtime.CreateSaveData().runs[0].planId, Is.EqualTo("plan.restore"));
        }

        private static ExperimentDefinition ExperimentDefinitionFixture()
        {
            ExperimentDefinition definition = ScriptableObject.CreateInstance<ExperimentDefinition>();
            SetPrivate(definition, "experimentId", "experiment.prototype.test");
            SetPrivate(definition, "displayName", "Prototype Test Experiment");
            SetPrivate(definition, "category", ExperimentCategory.SubstitutionTesting);
            SetPrivate(definition, "defaultPlanMode", ExperimentPlanMode.Controlled);
            SetPrivate(definition, "supportedTargetTypes", new[] { "recipe", "material" });
            SetPrivate(definition, "variables", new[]
            {
                new ExperimentVariableDefinitionData { variableId = "variable.material", category = ExperimentVariableCategory.IngredientIdentity, valueType = ExperimentValueType.StableId, role = ExperimentVariableRole.Independent },
                new ExperimentVariableDefinitionData { variableId = "variable.output", category = ExperimentVariableCategory.Custom, valueType = ExperimentValueType.Numeric, role = ExperimentVariableRole.Dependent }
            });
            SetPrivate(definition, "requiredControls", new[]
            {
                new ExperimentControlDefinitionData { controlId = "control.baseline", baselineType = "known-recipe", baselineReferenceId = "recipe.prototype.experimental", heldVariableIds = new[] { "variable.output" } }
            });
            SetPrivate(definition, "evidencePolicy", new ExperimentPolicyData { minimumTrials = 1, confirmationEvidenceThreshold = 1 });
            SetPrivate(definition, "reproducibilityPolicy", new ExperimentPolicyData { minimumTrials = 1, independentReproductionThreshold = 1 });
            SetPrivate(definition, "confirmationPolicy", new ExperimentPolicyData { minimumTrials = 1, independentReproductionThreshold = 1, confirmationEvidenceThreshold = 1, allowAuthoritativeRegistrationProposal = true });
            return definition;
        }

        private static ExperimentHypothesisData Hypothesis(string id)
        {
            return new ExperimentHypothesisData
            {
                hypothesisId = id,
                claim = new HypothesisClaimData { claimType = HypothesisClaimType.MaterialSubstitutesForMaterial, subjectId = "recipe.prototype.experimental", predicateId = "substitutes", objectId = "material.prototype.iron", proposedStableValueId = "material.prototype.iron" },
                targetRecipeId = "recipe.prototype.experimental",
                authorPersonId = "person.owner",
                testability = HypothesisTestabilityState.Testable,
                confidence = 250
            };
        }

        private static ExperimentPlanData Plan(string id, string definitionId, string hypothesisId)
        {
            return new ExperimentPlanData
            {
                planId = id,
                experimentDefinitionId = definitionId,
                hypothesisIds = new[] { hypothesisId },
                mode = ExperimentPlanMode.Controlled,
                trialCount = 1,
                controls =
                {
                    new ExperimentControlAssignmentData { assignmentId = $"{id}.control", controlId = "control.baseline", baselineReferenceId = "recipe.prototype.experimental", heldVariableIds = new[] { "variable.output" } }
                }
            };
        }

        private static KnowledgeObservationRequest Observation(string personId, string transactionId, string evidenceId)
        {
            return new KnowledgeObservationRequest
            {
                PersonId = personId,
                TransactionId = transactionId,
                EvidenceId = evidenceId,
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = "fact.prototype.experiment.discovery",
                    subjectType = KnowledgeSubjectType.Item,
                    subjectId = "recipe.prototype.experimental",
                    objectType = KnowledgeSubjectType.Item,
                    objectId = "recipe-input.prototype.iron",
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = "recipe-input.prototype.iron"
                },
                AcquisitionSource = KnowledgeAcquisitionSource.PersonalExperience,
                Provenance = KnowledgeProvenance.Inference,
                Strength = 800,
                Credibility = 800
            };
        }

        private static KnowledgeFactDefinition FactDefinitionFixture()
        {
            KnowledgeFactDefinition fact = ScriptableObject.CreateInstance<KnowledgeFactDefinition>();
            SetPrivate(fact, "factId", "fact.prototype.experiment.discovery");
            SetPrivate(fact, "displayName", "Prototype Experiment Discovery");
            SetPrivate(fact, "domain", KnowledgeDomain.Crafting);
            SetPrivate(fact, "propositionType", KnowledgePropositionType.Capability);
            SetPrivate(fact, "subjectType", KnowledgeSubjectType.Item);
            SetPrivate(fact, "objectType", KnowledgeSubjectType.Item);
            SetPrivate(fact, "valueType", KnowledgeValueType.StableId);
            SetPrivate(fact, "certaintyThreshold", 700);
            return fact;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private sealed class KnowledgeFixture : IDisposable
        {
            private readonly GameObject owner;

            public KnowledgeFixture(DefinitionRegistry registry)
            {
                PersonId = "person.owner";
                owner = new GameObject("Experimentation Knowledge Test");
                Runtime = owner.AddComponent<PersonKnowledgeRuntime>();
                Runtime.Configure(registry, PersonId);
            }

            public string PersonId { get; }
            public PersonKnowledgeRuntime Runtime { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
