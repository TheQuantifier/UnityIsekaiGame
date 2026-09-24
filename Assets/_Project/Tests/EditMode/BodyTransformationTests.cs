using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Transformation;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Tests
{
    public sealed class BodyTransformationTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in createdObjects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void PrototypeCatalog_ResolvesCanonicalTransformationsAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.EqualTo(0), report.GetSummary());

            DefinitionRegistry registry = catalog.CreateRegistry(report);
            foreach (string id in RequiredTransformationMethods())
            {
                AssertResolves<TransformationMethodDefinition>(registry, id);
            }

            AssertResolves<TransformationProfileDefinition>(registry, "transformation-profile.alpha.default");
            AssertResolves<TransformationProfileDefinition>(registry, "transformation-profile.species.human");
            AssertResolves<TransformationProfileDefinition>(registry, "transformation-profile.species.basic-construct");
            AssertResolves<TransformationProfileDefinition>(registry, "transformation-profile.species.basic-spirit");
        }

        [Test]
        public void Preview_DoesNotMutateBodyTransformationOrEvents()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.transformation.preview");
            long bodyRevision = body.BodyRevision;
            long transformationRevision = body.Transformation.TransformationRevision;
            string species = body.SpeciesDefinitionId;
            int events = 0;
            body.Transformation.TransformationChanged += (_, _, _) => events++;

            BodyTransformationResult result = body.Transformation.Preview(Request(body, "transformation.polymorph.temporary", "species.basic-construct", "tx.transformation.preview", preview: true));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(body.SpeciesDefinitionId, Is.EqualTo(species));
            Assert.That(body.BodyRevision, Is.EqualTo(bodyRevision));
            Assert.That(body.Transformation.TransformationRevision, Is.EqualTo(transformationRevision));
            Assert.That(body.Transformation.CreateSnapshot().ActiveTemporaryTransformation, Is.False);
            Assert.That(events, Is.EqualTo(0));
        }

        [Test]
        public void TemporaryPolymorph_RevertsCapturedBodyStateOnce()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.transformation.temporary");
            string originalSpecies = body.SpeciesDefinitionId;

            BodyTransformationResult transform = body.Transformation.Execute(Request(body, "transformation.polymorph.temporary", "species.basic-construct", "tx.transformation.temporary"));
            Assert.That(transform.Succeeded, Is.True, transform.Message);
            Assert.That(body.SpeciesDefinitionId, Is.EqualTo("species.basic-construct"));
            Assert.That(body.Transformation.CreateSnapshot().ActiveTemporaryTransformation, Is.True);

            BodyTransformationResult revert = body.Transformation.RevertTemporaryTransformation("tx.transformation.temporary.revert");
            Assert.That(revert.Succeeded, Is.True, revert.Message);
            Assert.That(body.SpeciesDefinitionId, Is.EqualTo(originalSpecies));
            Assert.That(body.Transformation.CreateSnapshot().ActiveTemporaryTransformation, Is.False);

            BodyTransformationResult duplicateRevert = body.Transformation.RevertTemporaryTransformation("tx.transformation.temporary.revert");
            Assert.That(duplicateRevert.Succeeded, Is.True, duplicateRevert.Message);
            Assert.That(duplicateRevert.Duplicate, Is.True);
        }

        [Test]
        public void PermanentSpeciesChange_RebuildsBodyOwnedStateAndPreservesPerson()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.transformation.permanent");
            string personId = body.PersonId;

            BodyTransformationResult result = body.Transformation.Execute(Request(body, "transformation.species-change.permanent", "species.basic-construct", "tx.transformation.permanent"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(body.PersonId, Is.EqualTo(personId));
            Assert.That(body.SpeciesDefinitionId, Is.EqualTo("species.basic-construct"));
            Assert.That(body.CreateAnatomySnapshot().AnatomyDefinitionId, Is.EqualTo("anatomy.basic-construct"));
            Assert.That(body.Transformation.CreateSnapshot().ActiveTemporaryTransformation, Is.False);
        }

        [Test]
        public void DuplicateTransaction_IsIdempotent()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.transformation.duplicate");
            BodyTransformationRequest request = Request(body, "transformation.species-change.permanent", "species.basic-construct", "tx.transformation.duplicate");

            BodyTransformationResult first = body.Transformation.Execute(request);
            long bodyRevision = body.BodyRevision;
            long transformationRevision = body.Transformation.TransformationRevision;
            BodyTransformationResult duplicate = body.Transformation.Execute(request);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(body.BodyRevision, Is.EqualTo(bodyRevision));
            Assert.That(body.Transformation.TransformationRevision, Is.EqualTo(transformationRevision));
        }

        [Test]
        public void BodyReplacementAndPossessionProducePlansWithoutBodyOwnedMutation()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.transformation.plan");
            long bodyRevision = body.BodyRevision;

            BodyTransformationResult replacement = body.Transformation.Preview(Request(body, "transformation.body-replacement", string.Empty, "tx.transformation.replace", preview: true, targetBodyId: "body.target.replace"));
            BodyTransformationResult possession = body.Transformation.Preview(Request(body, "transformation.possession", string.Empty, "tx.transformation.possess", preview: true, targetBodyId: "body.target.possess"));

            Assert.That(replacement.Succeeded, Is.True, replacement.Message);
            Assert.That((replacement.Plan.Flags & TransformationPlanFlags.PersonBodyReassociation) != 0, Is.True);
            Assert.That(possession.Succeeded, Is.True, possession.Message);
            Assert.That((possession.Plan.Flags & TransformationPlanFlags.ControllerReassignment) != 0, Is.True);
            Assert.That(body.BodyRevision, Is.EqualTo(bodyRevision));
        }

        [Test]
        public void CompatibilitySuppression_BlocksTransformationWithoutMutation()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.transformation.suppression");
            RuntimeBiologicalInteractionRule suppression = new RuntimeBiologicalInteractionRule(
                "entry.transformation.suppression",
                BiologicalCompatibilitySourceKind.Development,
                "source.transformation.test",
                BiologicalInteractionIds.Polymorph,
                BiologicalInteractionCategory.Transformation,
                BiologicalInteractionRuleKind.Suppression,
                BiologicalCompatibilityState.Compatible,
                0f,
                0f,
                0f,
                0f,
                999f,
                1000,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AnatomyStructuralCategory>(),
                string.Empty,
                "Test suppression.");
            Assert.That(body.BiologicalCompatibility.AddOrUpdateContribution(suppression).Succeeded, Is.True);

            BodyTransformationResult result = body.Transformation.Execute(Request(body, "transformation.polymorph.temporary", "species.basic-construct", "tx.transformation.suppressed"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(TransformationResultCode.Suppressed));
            Assert.That(body.SpeciesDefinitionId, Is.EqualTo("species.human"));
            Assert.That(body.Transformation.CreateSnapshot().ActiveTemporaryTransformation, Is.False);
        }

        [Test]
        public void SaveRestore_PreservesTemporaryTransformationWithoutReplayEvents()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.transformation.restore");
            BodyTransformationResult transform = body.Transformation.Execute(Request(body, "transformation.polymorph.temporary", "species.basic-construct", "tx.transformation.restore.transform"));
            Assert.That(transform.Succeeded, Is.True, transform.Message);

            BodySaveData saveData = body.CreateSaveData();
            ActorBodyRuntime restored = CreateBodyRuntime(registry, "actor.runtime.transformation.restore", "person.transformation");
            int events = 0;
            restored.Transformation.TransformationChanged += (_, _, _) => events++;
            BodyOperationResult restore = restored.RestoreFromSaveData(saveData, registry, "actor.runtime.transformation.restore", "person.transformation", restoring: true);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.SpeciesDefinitionId, Is.EqualTo("species.basic-construct"));
            Assert.That(restored.Transformation.CreateSnapshot().ActiveTemporaryTransformation, Is.True);
            Assert.That(restored.Transformation.CreateSnapshot().OriginalSpeciesId, Is.EqualTo("species.human"));
            Assert.That(events, Is.EqualTo(0));
        }

        [Test]
        public void RuntimeHasNoDevelopmentDependency()
        {
            string[] referenced = typeof(BodyTransformationRuntime).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
            Assert.That(referenced, Does.Not.Contain("UnityIsekaiGame.Development"));
            Assert.That(referenced, Does.Not.Contain("UnityIsekaiGame.EditModeTests"));
        }

        private ActorBodyRuntime CreateHumanBody(string actorBodyId)
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), actorBodyId, "person.transformation");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(body.Transformation.IsReady, Is.True);
            return body;
        }

        private ActorBodyRuntime CreateBodyRuntime(DefinitionRegistry registry, string actorBodyId, string personId)
        {
            GameObject owner = new GameObject("Body Transformation Test Body");
            createdObjects.Add(owner);
            owner.AddComponent<CharacterAttributes>();
            owner.AddComponent<CalculatedStatCollection>();
            owner.AddComponent<CharacterTraitCollection>();
            owner.AddComponent<UnityIsekaiGame.Capabilities.CharacterCapabilityCollection>();
            owner.AddComponent<ActorBodyRuntime>();

            CharacterAttributes attributes = owner.GetComponent<CharacterAttributes>();
            CalculatedStatCollection stats = owner.GetComponent<CalculatedStatCollection>();
            CharacterTraitCollection traits = owner.GetComponent<CharacterTraitCollection>();
            UnityIsekaiGame.Capabilities.CharacterCapabilityCollection capabilities = owner.GetComponent<UnityIsekaiGame.Capabilities.CharacterCapabilityCollection>();
            ActorBodyRuntime body = owner.GetComponent<ActorBodyRuntime>();

            attributes.Configure(registry);
            stats.Configure(registry, attributes);
            capabilities.Configure(registry);
            traits.Configure(registry, stats, null, capabilities, personId);
            body.Configure(registry, actorBodyId, personId, traits, stats, capabilityCollection: capabilities);
            return body;
        }

        private static BodyTransformationRequest Request(ActorBodyRuntime body, string methodId, string targetSpeciesId, string transactionId, bool preview = false, string targetBodyId = "", string targetNodeId = "")
        {
            BodySnapshot snapshot = body.CreateSnapshot();
            return new BodyTransformationRequest(
                methodId,
                transactionId,
                snapshot.PersonId,
                snapshot.ActorBodyId,
                snapshot.ActorBodyId,
                targetBodyId,
                targetSpeciesId,
                string.Empty,
                targetNodeId,
                string.Empty,
                "edit-mode-test",
                "Edit Mode transformation test",
                "Feature 7.8 test",
                preview,
                requestedDurationSeconds: 0f,
                expectedBodyRevision: snapshot.BodyRevision,
                expectedAnatomyRevision: snapshot.Anatomy?.AnatomyRevision ?? 0L,
                expectedCompatibilityRevision: snapshot.BiologicalCompatibility?.CompatibilityRevision ?? 0L);
        }

        private static IReadOnlyList<string> RequiredTransformationMethods()
        {
            return new[]
            {
                "transformation.polymorph.temporary",
                "transformation.species-change.permanent",
                "transformation.body-form-change",
                "transformation.body-replacement",
                "transformation.body-swap",
                "transformation.possession",
                "transformation.reincarnation",
                "transformation.resurrection-body",
                "transformation.spirit-embodiment",
                "transformation.structure-replacement",
                "transformation.organ-replacement",
                "transformation.limb-replacement",
                "transformation.construct-component-replacement"
            };
        }

        private static DefinitionRegistry LoadRegistry()
        {
            return LoadCatalog().CreateRegistry();
        }

        private static DefinitionCatalog LoadCatalog()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog;
        }

        private static void AssertResolves<TDefinition>(DefinitionRegistry registry, string id)
            where TDefinition : class, IGameDefinition
        {
            Assert.That(registry.TryGet(id, out TDefinition definition), Is.True, $"Definition '{id}' did not resolve as {typeof(TDefinition).Name}.");
            Assert.That(definition, Is.Not.Null);
        }
    }
}
